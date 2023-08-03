using daytwo;
using daytwo.Helpers;
using k8s.Models;
using k8s;
using System.Text.Json;
using daytwo.CustomResourceDefinitions;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Buffers.Text;
using Microsoft.AspNetCore.DataProtection;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;
using daytwo.crd.cluster;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using daytwo.crd.provider;
using Microsoft.AspNetCore.Antiforgery;
using System.Runtime.CompilerServices;

namespace daytwo.K8sControllers
{
    public class ClusterK8sController
    {
        public string managementCluster;

        static string api = "cluster";
        static string group = "cluster.x-k8s.io";
        static string version = "v1beta1";
        static string plural = api + "s";

        public Kubernetes kubeclient = null;
        public KubernetesClientConfiguration kubeconfig = null;

        public GenericClient generic = null;

        // providers here as they are associated with each management cluster
        public List<ProviderK8sController> providers = new List<ProviderK8sController>();


        public ClusterK8sController() //string api, string group, string version, string plural)
        {
            /*
            // initialize properties
            this.api = api;
            this.group = group;
            this.version = version;
            this.plural = plural;
            */

            // start listening
            Console.WriteLine($"**** Cluster.Add({api}s.{group}/{version})");
        }

        public async Task Listen(string managementCluster)
        {
            // remember the management cluster
            this.managementCluster = managementCluster;

            // locate the provisioning cluster argocd secret
            V1Secret? secret = daytwo.Helpers.Main.GetClusterArgocdSecret(managementCluster);
            // use secret to create kubeconfig
            kubeconfig = daytwo.Helpers.Main.BuildConfigFromArgocdSecret(secret);
            // use kubeconfig to create client
            kubeclient = new Kubernetes(kubeconfig);

            //
            generic = new GenericClient(kubeclient, group, version, plural);

            // Enforce only processing one watch event at a time
            SemaphoreSlim semaphore;


            // Watch is a tcp connection therefore it can drop, use a while loop to restart as needed.
            while (true)
            {
                // Prep semaphore (reset in case of exception)
                semaphore = new SemaphoreSlim(1);

                Console.WriteLine(DateTime.UtcNow +" (" + api +") Listen begins ...");
                try
                {
                    await foreach (var (type, item) in generic.WatchNamespacedAsync<CrdCluster>(""))
                    {
                        Console.WriteLine("");
                        Console.WriteLine("(event) [" + type + "] " + plural + "." + group + "/" + version + ": " + item.Metadata.Name);

                        // Acquire Semaphore
                        semaphore.Wait(Globals.cancellationToken);
                        //Console.WriteLine("[" + item.Metadata.Name + "]");

                        // Handle event type
                        switch (type)
                        {
                            case WatchEventType.Added:
                                await ProcessAdded(item);
                                break;
                            //case WatchEventType.Bookmark:
                            //    break;
                            case WatchEventType.Deleted:
                                await ProcessDeleted(item);
                                break;
                            //case WatchEventType.Error:
                            //    break;
                            case WatchEventType.Modified:
                                await ProcessModified(item);
                                break;
                        }

                        // Release semaphore
                        //Console.WriteLine("done.");
                        semaphore.Release();
                    }
                }
                catch (k8s.Autorest.HttpOperationException ex)
                {
                    Console.WriteLine("Exception? " + ex);
                    switch (ex.Response.StatusCode)
                    {
                        // crd is missing, sleep to avoid an error loop
                        case System.Net.HttpStatusCode.NotFound:
                            Console.WriteLine("crd is missing, pausing for a second before retrying");
                            Thread.Sleep(1000);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine("Exception occured while performing 'watch': " + ex);
                }
            }
        }

        public async Task ProcessAdded(CrdCluster tkc)
        {
            await ProcessModified(tkc);
        }
        public async Task ProcessModified(CrdCluster cluster)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            string patchStr = string.Empty;

            Console.WriteLine("  - namespace: " + cluster.Namespace() + ", cluster: " + cluster.Name());

            // is this cluster in a ready state?
            if (!(
                (cluster.Status != null)
                && (cluster.Status.phase == "Provisioned")
                && cluster.Status.infrastructureReady
                && cluster.Status.controlPlaneReady
                ))
            {
                // cluster not yet ready
                Console.WriteLine("    - cluster not ready yet");

                return;
            }

            // has this cluster been added to argocd?
            V1Secret? tmp = daytwo.Helpers.Main.GetClusterArgocdSecret(cluster.Name(), managementCluster);

            if (tmp != null)
            {
                // timestamp was old technique, using resourceVersion now
                //Console.WriteLine($"    -  cluster yaml timestamp: {cluster.Metadata.CreationTimestamp}");
                //Console.WriteLine($"    - argocd secret timestamp: {tmp.Metadata.CreationTimestamp}");
                Console.WriteLine($"    -          cluster yaml resourceVersion: {cluster.Metadata.ResourceVersion}");
                try
                {
                    Console.WriteLine($"    - argocd secret cluster resourceVersion: {tmp.Metadata.EnsureAnnotations()["daytwo.aarr.xyz/resourceVersion"]}");
                }
                catch
                {
                    Console.WriteLine($"    - argocd secret cluster resourceVersion: daytwo annotation missing, ignoring cluster");
                    return;
                }
            }

            // if cluster yaml is newer then secret, then we re-add to argocd
            if (tmp == null)
            {
                Console.WriteLine("      - add cluster to argocd");

                // add new cluster to argocd
                //KubernetesClientConfiguration tmpkubeconfig = await GetClusterKubeConfig(cluster.Name(), cluster.Namespace());
            }
            // has the cluster resourceVersion changed since we last updated?  if so, update argocd secret
            else if (cluster.Metadata.ResourceVersion != tmp.Metadata.EnsureAnnotations()["daytwo.aarr.xyz/resourceVersion"])
            //else if (DateTime.Compare((DateTime)cluster.Metadata.CreationTimestamp, (DateTime)tmp.Metadata.CreationTimestamp) > 0)
            {
                Console.WriteLine("      - update argocd cluster secret");

                // add new cluster to argocd
                //KubernetesClientConfiguration tmpkubeconfig = await GetClusterKubeConfig(cluster.Name(), cluster.Namespace());
            }
            else
            {
                Console.WriteLine("      - cluster already added to argocd");

                //
                await AddProvider(cluster);

                return;
            }

            // add new cluster to argocd
            KubernetesClientConfiguration tmpkubeconfig = await GetClusterKubeConfig(cluster.Name(), cluster.Namespace(), managementCluster);

            // acquire argocd cluster secret to so we can add annotation and labels
            tmp = daytwo.Helpers.Main.GetClusterArgocdSecret(cluster.Name(), managementCluster);
            if (tmp == null)
            {
                Console.WriteLine("unable to add argocd secret, or cluster not managed by daytwo");
                return;
            }

            // add/update cluster resourceVersion, we use this later to check for changes
            tmp.SetAnnotation("daytwo.aarr.xyz/resourceVersion", cluster.Metadata.ResourceVersion);

            //
            Globals.service.kubeclient.CoreV1.PatchNamespacedSecret(
                new V1Patch(tmp, V1Patch.PatchType.MergePatch), tmp.Name(), tmp.Namespace());

            await AddProvider(cluster);
            

            return;
        }
        public async Task ProcessDeleted(CrdCluster cluster)
        {
            // check if we should remove this from argocd
            V1Secret tmp = daytwo.Helpers.Main.GetClusterArgocdSecret(cluster.Name(), managementCluster);
            if (tmp == null)
            {
                Console.WriteLine("argocd is not managing this cluster, no need to remove it");
                return;
            }

            // only remove from argocd if we added this cluster to argocd
            string annotation = tmp.GetAnnotation("daytwo.aarr.xyz/resourceVersion");
            if (annotation == null)
            {
                Console.WriteLine("** annotation is null **");
                Console.WriteLine("** (don't delete cluster) **");
                return;
            }
            /*
            else
            {
                Console.WriteLine("** annotation is: "+ annotation);
            }
            */

            Console.WriteLine("** argocd remove cluster ...");

            // locate server pod
            V1PodList list = await Globals.service.kubeclient.ListNamespacedPodAsync(Globals.service.argocdNamespace);
            V1Pod pod = null;
            foreach (var item in list.Items)
            {
                if (item.Spec.Containers[0].Name == "server")
                {
                    pod = item;

                    break;
                }
            }

            // this shouldn't happen, but could if server is not running
            if (pod == null)
            {
                Console.WriteLine("server pod not found, unable to remove cluster from argocd");
                return;
            }

            // todo get clustername used in provided kubeconfig

            var cmds = new List<string>();
            cmds.Add("sh");
            cmds.Add("-c");
            cmds.Add( $"argocd cluster rm {cluster.Name()}"
                    + $" -y"
                    + $" --server=localhost:8080"
                    + $" --plaintext"
                    + $" --insecure"
                    + $" --auth-token={Environment.GetEnvironmentVariable("ARGOCD_AUTH_TOKEN")};"
                    );
            try
            {
                Console.WriteLine("[cluster] before exec");
                int asdf = await Globals.service.kubeclient.NamespacedPodExecAsync(
                    pod.Name(), pod.Namespace(), pod.Spec.Containers[0].Name, cmds, false, One, Globals.cancellationToken);
                Console.WriteLine("[cluster] after exec");
            }
            catch
            {
            }
            Console.WriteLine("[cluster] after exec (2)");
        }


        
        /// <summary>
        /// With knowledge of the innerworkings of the cluster provisioning process,
        /// obtain the default admin kubeconfig '/etc/kubernetes/admin.conf'.
        /// </summary>
        /// <param name="clusterName"></param>
        /// <returns></returns>
        public async Task<KubernetesClientConfiguration> GetClusterKubeConfig(string clusterName, string clusterNamespace, string? managementCluster)
        {
            // clusterctl - n vc - test get kubeconfig vc - test
            // k -n vc-test get secrets vc-test-kubeconfig -o jsonpath='{.data.value}' | base64 -d
            Console.WriteLine($"[cluster] GetClusterKubeConfig ({clusterName}, {clusterNamespace})");

            V1Secret secret = null;
            try
            {
                secret = await kubeclient.ReadNamespacedSecretAsync(clusterName + "-kubeconfig", clusterNamespace);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
            secret.Data.TryGetValue("value", out byte[] bytes);
            string kubeconfig = System.Text.Encoding.UTF8.GetString(bytes);
            //Console.WriteLine("[cluster] kubeconfig:\n" + kubeconfig);
            //Convert.ToBase64String(bytes);

            // locate context within kubeconfig
            string[] lines = kubeconfig.Split("\n");
            string context = string.Empty;
            foreach (var line in lines)
            {
                if (line.StartsWith("current-context:")) {
                    context = line.Substring("current-context: ".Length);
                    break;
                }
            }

            // save kubeconfig to a temporary file
            //string path = Path.GetTempFileName();
            //string path = "/tmp/asdf.txt";
            //Console.WriteLine("tmp path: " + path);

            // exec into server pod, see if we can use 'argocd' there
            var cmds = new List<string>();

            // todo get actual pod name of 'server' pod 

            // todo get clustername used in provided kubeconfig


            // locate server pod
            V1PodList list = await Globals.service.kubeclient.ListNamespacedPodAsync(Globals.service.argocdNamespace);
            V1Pod pod = null;
            foreach (var item in list.Items)
            {
                if (item.Spec.Containers[0].Name == "server")
                {
                    pod = item;
                    
                    break;
                }
            }

            // this shouldn't happen, but could if server is not running
            if (pod == null)
            {
                Console.WriteLine("server pod not found");
                return null;
            }

            /*
            // test
            try
            {
                cmds = new List<string>();
                cmds.Add("pwd");
                //cmds = new List<string>();
                //cmds.Add("pwd");
                Console.WriteLine("[cluster] (test) before exec");
                await Globals.service.kubeclient.NamespacedPodExecAsync(
                    pod.Name(), pod.Namespace(), pod.Spec.Containers[0].Name, cmds, false, One, Globals.cancellationToken).ConfigureAwait(false);
                Console.WriteLine("[cluster] (test) after exec");

                //await ExecInPod(Globals.service.kubeclient, pod, "pwd");
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception caught when performing 'exec', cmd ran though, ignoring exception for now");
                //Console.WriteLine(ex.ToString());
            }
            */

            try
            {
                cmds = new List<string>();
                cmds.Add("sh");
                cmds.Add("-c");
                cmds.Add($"echo {Convert.ToBase64String(bytes)} > /tmp/{clusterName}.b64;"
                        + $"cat /tmp/{clusterName}.b64 | base64 -d > /tmp/{clusterName}.conf;"
                        + $"argocd cluster add {context}"
                        + $" -y"
                        + $" --upsert"
                        + $" --name {clusterName}"
                        + ((managementCluster != null) ? $" --annotation 'daytwo.aarr.xyz/management-cluster'='{managementCluster}'" : "")
                        + $" --annotation 'daytwo.aarr.xyz/workload-cluster'='{clusterName}'"
                        + $" --kubeconfig /tmp/{clusterName}.conf"
                        + $" --server=localhost:8080"
                        + $" --plaintext"
                        + $" --insecure"
                        + $" --auth-token={Environment.GetEnvironmentVariable("ARGOCD_AUTH_TOKEN")};"
                        );
                //Console.WriteLine(cmds[2]);
                Console.WriteLine("[cluster] before exec");
                int asdf = await Globals.service.kubeclient.NamespacedPodExecAsync(
                    pod.Name(), pod.Namespace(), pod.Spec.Containers[0].Name, cmds, false, One, Globals.cancellationToken).ConfigureAwait(false);
                Console.WriteLine("[cluster] after exec");
            }
            catch (Exception ex)
            {
                //Console.WriteLine("exception caught when performing 'exec', cmd ran though, ignoring exception for now");
                //Console.WriteLine(ex.ToString());
            }
            Console.WriteLine("[cluster] after exec (2)");


            return null;
        }

        /*
        public static void PrintEvenNumbers()
        {
            //Console.WriteLine("all is done");
        }
        */
        public static Task One(Stream stdIn, Stream stdOut, Stream stdErr)
        {
            StreamReader sr = new StreamReader(stdOut);
            while (!sr.EndOfStream)
            {
                Console.WriteLine(sr.ReadLine());
            }

            // returning null will cause an exception, but it also let's us return back to the processing
            return null;
            //return new Task(PrintEvenNumbers);
        }

        /*
        private static async Task ExecInPod(IKubernetes client, V1Pod pod, string cmd)
        {
            var webSocket =
                await client.WebSocketNamespacedPodExecAsync(pod.Metadata.Name, "default", cmd,
                    pod.Spec.Containers[0].Name).ConfigureAwait(false);

            var demux = new StreamDemuxer(webSocket);
            demux.Start();

            var buff = new byte[4096];
            var stream = demux.GetStream(1, 1);
            stream.Read(buff, 0, 4096);
            var str = Encoding.Default.GetString(buff);
            Console.WriteLine(str);

            //return new Task(PrintEvenNumbers);
        }
        */

        public async Task AddProvider(CrdCluster cluster)
        {
            // Check for environment variable asking us not to copy labels
            string? disable = Environment.GetEnvironmentVariable("OPTION_DISABLE_LABEL_COPY");
            if ((disable != null) && (disable.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                // do not monitor providers or copy labels
                return;
            }

            //
            string _api = cluster.Spec.controlPlaneRef.kind.ToLower();
            string _group = cluster.Spec.controlPlaneRef.apiVersion.Substring(0, cluster.Spec.controlPlaneRef.apiVersion.IndexOf("/"));
            string _version = cluster.Spec.controlPlaneRef.apiVersion.Substring(cluster.Spec.controlPlaneRef.apiVersion.IndexOf("/") + 1);
            string _plural = _api + "s";
            Console.WriteLine("api: " + _api);
            Console.WriteLine("group: " + _group);
            Console.WriteLine("version: " + _version);
            Console.WriteLine("plural: " + _plural);

            // check if provider is already present
            ProviderK8sController? item = providers.Find(item => (item.api == _api) && (item.group == _group) && (item.version == _version) && (item.plural == _plural));
            if (item != null)
            {
                // provider already exists, nudge it to recheck this cluster which just had its secret updated
                CrdProviderCluster crd = await item.generic.ReadNamespacedAsync<CrdProviderCluster>(cluster.Namespace(), cluster.Name());
                item.ProcessModified(crd);
            }
            else //if (item == null)
            {
                // if not, start monitoring
                ProviderK8sController provider = new ProviderK8sController(
                        _api, _group, _version, _plural);

                // add to list of providers we are monitoring
                providers.Add(provider);

                // start listening
                provider.Listen(managementCluster);
            }
        }
    }
}
