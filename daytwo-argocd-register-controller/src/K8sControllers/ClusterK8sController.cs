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
using System.Diagnostics;
using System.Threading;
using k8s.KubeConfigModels;
using System.Net.Sockets;

namespace daytwo.K8sControllers
{
    public class ClusterK8sController
    {
        static string api = "cluster";
        static string group = "cluster.x-k8s.io";
        static string version = "v1beta1";
        static string plural = api + "s";

        public string managementCluster;

        public Kubernetes kubeclient = null;
        public KubernetesClientConfiguration kubeconfig = null;

        public GenericClient generic = null;

        // Enforce only processing one watch event at a time
        SemaphoreSlim semaphore = null;

        // providers here as they are associated with each management cluster
        public List<ProviderK8sController> providers = new List<ProviderK8sController>();


        public ClusterK8sController(string managementCluster) //string api, string group, string version, string plural)
        {
            // start listening
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"**** Cluster.Add({api}s.{group}/{version})");

            // remember the management cluster
            this.managementCluster = managementCluster;

            // locate the provisioning cluster argocd secret
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "GetClusterArgocdSecret");
            V1Secret? secret = daytwo.Helpers.Main.GetClusterArgocdSecret(managementCluster);
            // use secret to create kubeconfig
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "BuildConfigFromArgocdSecret");
            kubeconfig = daytwo.Helpers.Main.BuildConfigFromArgocdSecret(secret);
            // use kubeconfig to create client
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "Create kubeclient");
            kubeclient = new Kubernetes(kubeconfig);

            //
            generic = new GenericClient(kubeclient, group, version, plural);

            // Prep semaphore for only 1 action at a time
            semaphore = new SemaphoreSlim(1);

            // Start the intermittent timer
            //(new Thread(new ThreadStart(Timer))).Start();
        }
        public async Task Start()
        {
            // Start the k8s event listener
            //Listen();
            // Start the intermittent timer
            (new Thread(new ThreadStart(Timer))).Start();

            // Start up provider listeners
            //await AddProviders();
        }
        public void Timer()
        {
            while (!Globals.cancellationToken.IsCancellationRequested)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "sleeping");
                Thread.Sleep(60 * 1000);

                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "Intermittent");
                Intermittent();
            }
        }
        public async Task Intermittent()//(int seconds)
        {
            CustomResourceList<CrdCluster> clusters = await generic.ListNamespacedAsync<CustomResourceList<CrdCluster>>("");

            // Acquire Semaphore
            semaphore.Wait(Globals.cancellationToken);

            try
            {
                //**
                // add: loop through clusters and add to argocd if missing or out of sync

                // acquire list of all provider resources
                clusters = await generic.ListNamespacedAsync<CustomResourceList<CrdCluster>>("");
                foreach (var cluster in clusters.Items)
                {
                    // always attempt to add cluster, method provides safety checks to only add when needed
                    await ProcessAdded(cluster);
                }

                //**
                // remove: loop through argocd secrets and remove if no cluster exists

                List<string> rmClusters = new List<string>();

                // acquire list of all arogcd secrets
                V1SecretList secrets = await kubeclient.ListNamespacedSecretAsync(Globals.service.argocdNamespace);
                foreach (var secret in secrets)
                {
                    // skip if not an argocd cluster secret
                    if (!Helpers.Main.IsArgocdClusterSecret(secret))
                    {
                        continue;
                    }

                    // only remove from argocd if we added this cluster to argocd
                    string annotation = secret.GetAnnotation("daytwo.aarr.xyz/management-cluster");
                    if (annotation == null)
                    {
                        continue;
                    }


                    // loop through clusters to see if we have one matching this secret
                    bool found = false;
                    clusters = await generic.ListNamespacedAsync<CustomResourceList<CrdCluster>>("");
                    foreach (var cluster in clusters.Items)
                    {
                        if ((managementCluster == secret.GetAnnotation("daytwo.aarr.xyz/management-cluster"))
                            && (cluster.Name() == secret.GetAnnotation("daytwo.aarr.xyz/workload-cluster")))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        rmClusters.Add(secret.GetAnnotation("daytwo.aarr.xyz/workload-cluster"));
                    }
                }

                // remove the stale clusters identified
                foreach (string cluster in rmClusters)
                {
                    Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"argocd cluster rm {cluster}");

                    var p = new Process
                    {
                        StartInfo = {
                                // pinniped get kubeconfig --kubeconfig /tmp/kubeconfig
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                FileName = "sh",
                                WorkingDirectory = @"/tmp",
                                Arguments = "-c"
                            }
                    };

                    p.StartInfo.Arguments += " "
                            + "\""
                            + $"/usr/local/bin/argocd cluster rm {cluster}"
                            + $" -y"
                            + $" --grpc-web"
                            + $" --server={Environment.GetEnvironmentVariable("ARGOCD_SERVER_URI")}"
                            //+ $" --server=localhost:8080"
                            //+ $" --plaintext"
                            + ((Environment.GetEnvironmentVariable("ARGOCD_INSECURE_SKIP_TLS_VERIFY") != null) ?
                                ((Environment.GetEnvironmentVariable("ARGOCD_INSECURE_SKIP_TLS_VERIFY").Equals("true", StringComparison.CurrentCultureIgnoreCase)) ?
                                    $" --insecure" : "")
                                : "")
                            + $" --auth-token={Environment.GetEnvironmentVariable("ARGOCD_AUTH_TOKEN")}"
                            + "\""
                            ;

                    Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), p.StartInfo.Arguments);
                    p.Start();
                    p.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"{ex.Message}", ex);
            }

            try
            {
                // Release semaphore
                semaphore.Release();
            }
            catch
            {
                // release will fail if exception was before semaphore was acquired, ignore
            }
        }
        public async Task Listen(/*string managementCluster*/)
        {
            /*
            // remember the management cluster
            this.managementCluster = managementCluster;

            // locate the provisioning cluster argocd secret
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "GetClusterArgocdSecret");
            V1Secret? secret = daytwo.Helpers.Main.GetClusterArgocdSecret(managementCluster);
            // use secret to create kubeconfig
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "BuildConfigFromArgocdSecret");
            kubeconfig = daytwo.Helpers.Main.BuildConfigFromArgocdSecret(secret);
            // use kubeconfig to create client
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "Create kubeclient");
            kubeclient = new Kubernetes(kubeconfig);

            //
            generic = new GenericClient(kubeclient, group, version, plural);
            */

            // Start up all provider listeners
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "Starting up provider listeners ...");


            // Watch is a tcp connection therefore it can drop, use a while loop to restart as needed.
            while (true)
            {
                // Prep semaphore (reset in case of exception)
                semaphore = new SemaphoreSlim(1);

                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "("+ api +") Listen begins ...");
                try
                {
                    await foreach (var (type, item) in generic.WatchNamespacedAsync<CrdCluster>(""))
                    {
                        Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "");
                        Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "(event) [" + type + "] " + plural + "." + group + "/" + version + ": " + item.Metadata.Name);

                        // Acquire Semaphore
                        semaphore.Wait(Globals.cancellationToken);
                        //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[" + item.Metadata.Name + "]");

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
                        //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "done.");
                        semaphore.Release();
                    }
                }
                catch (k8s.Autorest.HttpOperationException ex)
                {
                    Globals.log.LogInformation(new EventId(0, api), "Exception: " + ex);
                    switch (ex.Response.StatusCode)
                    {
                        // crd is missing, sleep to avoid an error loop
                        case System.Net.HttpStatusCode.NotFound:
                            Globals.log.LogInformation(new EventId(0, api), "listen recieved: 404, is clusters.cluster.x-k8s.io crd is missing?");
                            Thread.Sleep(1000);
                            break;
                    }

                    try
                    {
                        // Release semaphore
                        semaphore.Release();
                    }
                    catch
                    {
                        // release will fail if exception was before semaphore was acquired, ignore
                    }
                }
                catch (Exception ex)
                {
                    //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "Exception occured while performing 'watch': " + ex);

                    try
                    {
                        // Release semaphore
                        semaphore.Release();
                    }
                    catch
                    {
                        // release will fail if exception was before semaphore was acquired, ignore
                    }
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

            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId),
                        "  - namespace: " + cluster.Namespace()
                        + ", cluster: " + cluster.Name()
                        + ((cluster.Status != null) ? $", status: {cluster.Status.phase}" : ""));

            // is this cluster in a ready state?
            if (!(
                (cluster.Status != null)
                && (cluster.Status.phase == "Provisioned")
                && cluster.Status.infrastructureReady
                && cluster.Status.controlPlaneReady
                ))
            {
                // cluster not yet ready
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "    - cluster not ready yet");

                return;
            }

            // has this cluster been added to argocd?
            V1Secret? tmp = daytwo.Helpers.Main.GetClusterArgocdSecret(cluster.Name(), managementCluster);
            /*
            if (tmp != null)
            {
                // use resourceVersion to determine if cluster secret has been updated
                //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"    -          cluster yaml resourceVersion: {cluster.Metadata.ResourceVersion}");
                try
                {
                    Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"    - argocd secret cluster resourceVersion: {tmp.Metadata.EnsureAnnotations()["daytwo.aarr.xyz/resourceVersion"]}");
                }
                catch
                {
                    Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"    - argocd secret cluster resourceVersion: daytwo annotation missing, ignoring cluster");
                    return;
                }
            }
            */

            if (tmp != null)
            {
                // check to see that this argocd cluster secret is managed by daytwo
                string annotation = tmp.GetAnnotation("daytwo.aarr.xyz/resourceVersion");
                if (annotation == null)
                {
                    return;
                }
            }

            // if cluster yaml is newer then secret, then we re-add to argocd
            if (tmp == null)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "      - add cluster to argocd");

                // add new cluster to argocd
                //KubernetesClientConfiguration tmpkubeconfig = await GetClusterKubeConfig(cluster.Name(), cluster.Namespace());
            }
            // has the cluster resourceVersion changed since we last updated?  if so, update argocd secret
            else if (cluster.Metadata.ResourceVersion != tmp.Metadata.EnsureAnnotations()["daytwo.aarr.xyz/resourceVersion"])
            //else if (DateTime.Compare((DateTime)cluster.Metadata.CreationTimestamp, (DateTime)tmp.Metadata.CreationTimestamp) > 0)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "      - update argocd cluster secret");

                // add new cluster to argocd
                //KubernetesClientConfiguration tmpkubeconfig = await GetClusterKubeConfig(cluster.Name(), cluster.Namespace());
            }
            else
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "      - cluster already added to argocd");

                //
                //await AddProvider(cluster);

                return;
            }

            // add new cluster to argocd
            KubernetesClientConfiguration tmpkubeconfig = await GetClusterKubeConfig(cluster.Name(), cluster.Namespace(), managementCluster);

            // acquire argocd cluster secret to so we can add annotation and labels
            tmp = daytwo.Helpers.Main.GetClusterArgocdSecret(cluster.Name(), managementCluster);
            if (tmp == null)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "unable to add argocd secret, or cluster not managed by daytwo");
                return;
            }

            // add/update cluster resourceVersion, we use this later to check for changes
            tmp.SetAnnotation("daytwo.aarr.xyz/resourceVersion", cluster.Metadata.ResourceVersion);

            //
            Globals.service.kubeclient.CoreV1.PatchNamespacedSecret(
                new V1Patch(tmp, V1Patch.PatchType.MergePatch), tmp.Name(), tmp.Namespace());

            //await AddProvider(cluster);
            

            return;
        }
        public async Task ProcessDeleted(CrdCluster cluster)
        {
            // check if we should remove this from argocd
            V1Secret tmp = daytwo.Helpers.Main.GetClusterArgocdSecret(cluster.Name(), managementCluster);
            if (tmp == null)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "argocd is not managing this cluster, no need to remove it");
                return;
            }

            // only remove from argocd if we added this cluster to argocd
            string annotation = tmp.GetAnnotation("daytwo.aarr.xyz/management-cluster");
            if (annotation == null)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "** annotation is null **");
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "** (don't delete cluster) **");
                return;
            }
            /*
            else
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "** annotation is: "+ annotation);
            }
            */

            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "** argocd remove cluster ...");

            /*
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
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "server pod not found, unable to remove cluster from argocd");
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
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] before exec");
                int asdf = await Globals.service.kubeclient.NamespacedPodExecAsync(
                    pod.Name(), pod.Namespace(), pod.Spec.Containers[0].Name, cmds, false, One, Globals.cancellationToken);
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] after exec");
            }
            catch
            {
            }
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] after exec (2)");
            */

            var p = new Process
            {
                StartInfo = {
                // pinniped get kubeconfig --kubeconfig /tmp/kubeconfig
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = "sh",
                WorkingDirectory = @"/tmp",
                Arguments = "-c"
            }
            };

            p.StartInfo.Arguments += " "
                    + "\""
                    + $"/usr/local/bin/argocd cluster rm {cluster.Name()}"
                    + $" -y"
                    + $" --grpc-web"
                    + $" --server={Environment.GetEnvironmentVariable("ARGOCD_SERVER_URI")}"
                    //+ $" --server=localhost:8080"
                    //+ $" --plaintext"
                    + ((Environment.GetEnvironmentVariable("ARGOCD_INSECURE_SKIP_TLS_VERIFY") != null) ?
                        ((Environment.GetEnvironmentVariable("ARGOCD_INSECURE_SKIP_TLS_VERIFY").Equals("true", StringComparison.CurrentCultureIgnoreCase)) ?
                            $" --insecure" : "")
                        : "")
                    + $" --auth-token={Environment.GetEnvironmentVariable("ARGOCD_AUTH_TOKEN")}"
                    + "\""
                    ;

            //Globals.log.LogInformation(p.StartInfo.Arguments);
            p.Start();
            p.WaitForExit();
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
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"[cluster] GetClusterKubeConfig ({clusterName}, {clusterNamespace})");

            V1Secret secret = null;
            try
            {
                secret = await kubeclient.ReadNamespacedSecretAsync(clusterName + "-kubeconfig", clusterNamespace);
            }
            catch (Exception ex)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] locating cluster secret failed: "+ ex.Message);
                return null;
            }
            secret.Data.TryGetValue("value", out byte[] bytes);
            string kubeconfig = System.Text.Encoding.UTF8.GetString(bytes);
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] kubeconfig:\n" + kubeconfig);
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

            /*
            // save kubeconfig to a temporary file
            //string path = Path.GetTempFileName();
            //string path = "/tmp/asdf.txt";
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "tmp path: " + path);

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
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "server pod not found");
                return null;
            }
            */

            /*
            try
            {
                cmds = new List<string>();
                cmds.Add("pwd");
                //cmds = new List<string>();
                //cmds.Add("pwd");
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] (test) before exec");
                await Globals.service.kubeclient.NamespacedPodExecAsync(
                    pod.Name(), pod.Namespace(), pod.Spec.Containers[0].Name, cmds, false, One, Globals.cancellationToken).ConfigureAwait(false);
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] (test) after exec");

                //await ExecInPod(Globals.service.kubeclient, pod, "pwd");
            }
            catch (Exception ex)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "exception caught when performing 'exec', cmd ran though, ignoring exception for now");
                //Globals.log.LogInformation(ex.ToString());
            }
            */

            /*
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
                //Globals.log.LogInformation(cmds[2]);
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] before exec");
                int asdf = await Globals.service.kubeclient.NamespacedPodExecAsync(
                    pod.Name(), pod.Namespace(), pod.Spec.Containers[0].Name, cmds, false, One, Globals.cancellationToken).ConfigureAwait(false);
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] after exec");
            }
            catch (Exception ex)
            {
                //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "exception caught when performing 'exec', cmd ran though, ignoring exception for now");
                //Globals.log.LogInformation(ex.ToString());
            }
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "[cluster] after exec (2)");
            */

            var p = new Process
            {
                StartInfo = {
                // pinniped get kubeconfig --kubeconfig /tmp/kubeconfig
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = "sh",
                WorkingDirectory = @"/tmp",
                Arguments = "-c"
            }
            };

            File.WriteAllText($"/tmp/{clusterName}.conf", kubeconfig);

            /*
            p.StartInfo.Arguments += " "
                        + $"echo {Convert.ToBase64String(bytes)} > /tmp/{clusterName}.b64;"
                        + $"cat /tmp/{clusterName}.b64 | base64 -d > /tmp/{clusterName}.conf;"
            */
            p.StartInfo.Arguments += " "
                        + "\""
                        + $"/usr/local/bin/argocd cluster add {context}"
                        + $" -y"
                        + $" --grpc-web"
                        + $" --upsert"
                        + $" --name {clusterName}"
                        + ((managementCluster != null) ? $" --annotation 'daytwo.aarr.xyz/management-cluster'='{managementCluster}'" : "")
                        + $" --annotation 'daytwo.aarr.xyz/workload-cluster'='{clusterName}'"
                        + $" --kubeconfig /tmp/{clusterName}.conf"
                        + $" --server={Environment.GetEnvironmentVariable("ARGOCD_SERVER_URI")}"
                        //+ $" --server=localhost:8080"
                        //+ $" --plaintext"
                        + ((Environment.GetEnvironmentVariable("ARGOCD_INSECURE_SKIP_TLS_VERIFY") != null) ?
                            ((Environment.GetEnvironmentVariable("ARGOCD_INSECURE_SKIP_TLS_VERIFY").Equals("true", StringComparison.CurrentCultureIgnoreCase)) ?
                                $" --insecure" : "")
                            : "")
                        + $" --auth-token={Environment.GetEnvironmentVariable("ARGOCD_AUTH_TOKEN")}"
                        + "\""
                        ;

            //Globals.log.LogInformation(p.StartInfo.Arguments);
            p.Start();
            p.WaitForExit();


            return null;
        }

        /*
        public static void PrintEvenNumbers()
        {
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "all is done");
        }
        */
        /*
        public static Task One(Stream stdIn, Stream stdOut, Stream stdErr)
        {
            StreamReader sr = new StreamReader(stdOut);
            while (!sr.EndOfStream)
            {
                Globals.log.LogInformation(sr.ReadLine());
            }

            // returning null will cause an exception, but it also let's us return back to the processing
            return null;
            //return new Task(PrintEvenNumbers);
        }
        */

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
            Globals.log.LogInformation(str);

            //return new Task(PrintEvenNumbers);
        }
        */

        public async Task AddProviders() //CrdCluster cluster)
        {
            // Check for environment variable asking us not to copy labels
            string? disable = Environment.GetEnvironmentVariable("OPTION_DISABLE_LABEL_COPY");
            if ((disable != null) && (disable.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                // do not monitor providers or copy labels
                return;
            }


            List<string> known = new List<string>();
            known.Add("vclusters.infrastructure.cluster.x-k8s.io");
            known.Add("tanzukubernetesclusters.run.tanzu.vmware.com");

            //
            V1CustomResourceDefinitionList list = await kubeclient.ListCustomResourceDefinitionAsync();
            foreach (var next in list)
            {
                string _kind = string.Empty;
                string _group = string.Empty;
                string _version = string.Empty;
                string _plural = string.Empty;
                if (known.Contains(next.Name()))
                {
                    Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"Known provider identified: {next.Name()}");

                    // load up crd using our template in order to parse out the values we need
                    GenericClient _generic = new GenericClient(kubeclient, "apiextensions.k8s.io", "v1", "customresourcedefinitions");
                    CrdProviderCluster providerCrd = await _generic.ReadAsync<CrdProviderCluster>(next.Name());

                    _group = providerCrd.Spec.group;
                    _kind = providerCrd.Spec.names.singular;
                    _plural = providerCrd.Spec.names.plural;
                    
                    // there may be multiple versions, add provider for each
                    foreach (var nextVersion in providerCrd.Spec.versions)
                    {
                        _version = nextVersion.name;

                        // check if provider is already present
                        ProviderK8sController? item = providers.Find(item => (item.api == _kind) && (item.group == _group) && (item.version == _version) && (item.plural == _plural));
                        if (item == null)
                        {
                            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"   _kind: {_kind}");
                            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"  _group: {_group}");
                            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $"_version: {_version}");
                            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), $" _plural: {_plural}");

                            // if not, start monitoring
                            ProviderK8sController provider = new ProviderK8sController(
                                    managementCluster,
                                    _kind, _group, _version, _plural);

                            // add to list of providers we are monitoring
                            providers.Add(provider);

                            // sync everything at the specified interval, needed in case:
                            // - a delete event was missed
                            // - provider modify event will cause pinniped kubeconfig generation but pinniped may not be ready

                            // Start the k8s event listener
                            //provider.Listen();
                            // Start the intermittent timer
                            //(new Thread(new ThreadStart(provider.Timer))).Start();

                            // Start k8s event listener & intermittent timer
                            provider.Start();
                        }
                        /*
                        else
                        {
                            // provider already exists, nudge it to recheck this cluster which just had its secret updated
                            CrdProviderCluster crd = await item.generic.ReadNamespacedAsync<CrdProviderCluster>(cluster.Namespace(), cluster.Name());
                            item.ProcessModified(crd);
                        }
                        */
                    }
                }
            }

            /*
            //
            string _api = cluster.Spec.controlPlaneRef.kind.ToLower();
            string _group = cluster.Spec.controlPlaneRef.apiVersion.Substring(0, cluster.Spec.controlPlaneRef.apiVersion.IndexOf("/"));
            string _version = cluster.Spec.controlPlaneRef.apiVersion.Substring(cluster.Spec.controlPlaneRef.apiVersion.IndexOf("/") + 1);
            string _plural = _api + "s";
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "api: " + _api);
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "group: " + _group);
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "version: " + _version);
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "plural: " + _plural);

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
            */
        }
    }
}
