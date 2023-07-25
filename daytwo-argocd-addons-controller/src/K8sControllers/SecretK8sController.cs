using daytwo;
using k8s.Models;
using k8s;
using System.Text.Json;
using daytwo.CustomResourceDefinitions;
using daytwo.crd.tanzukubernetescluster;
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

namespace gge.K8sControllers
{
    public class ClusterK8sController
    {
        /*
        static string api = "cluster";
        static string group = "cluster.x-k8s.io";
        static string version = "v1beta1";
        static string plural = api + "s";
        */
        static string api = "secret";
        static string group = "";
        static string version = "v1";
        static string plural = api + "s";

        static GenericClient generic = null;// new GenericClient(Globals.service.kubeclient, group, version, plural);
        //static Kubernetes kubeclient = null;
        //static KubernetesClientConfiguration kubeconfig = null;

        public async Task Listen()
        {
            //
            generic = new GenericClient(Globals.service.kubeclient, group, version, plural);

            // Enforce only processing one watch event at a time
            SemaphoreSlim semaphore;


            // Watch is a tcp connection therefore it can drop, use a while loop to restart as needed.
            while (true)
            {
                // Prep semaphore (reset in case of exception)
                semaphore = new SemaphoreSlim(1);

                Console.WriteLine("(" + api +") Listen begins ...");
                try
                {
                    await foreach (var (type, item) in generic.WatchNamespacedAsync<V1Secret>("argocd"))
                    {
                        Console.WriteLine("");
                        Console.WriteLine("(event) [" + type + "] " + plural + "." + group + "/" + version + ": " + item.Metadata.Name);

                        // Acquire Semaphore
                        semaphore.Wait(Globals.cancellationToken);
                        //Console.WriteLine("[" + item.Metadata.Name + "]");

                        // Handle event type
                        switch (type)
                        {
                            /*
                            case WatchEventType.Added:
                                await ProcessAdded(item);
                                break;
                            */
                            //case WatchEventType.Bookmark:
                            //    break;
                            /*
                            case WatchEventType.Deleted:
                                await ProcessDeleted(item);
                                break;
                            */
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

        public async Task ProcessAdded(V1Secret secret)
        {
            ProcessModified(secret);
        }
        public async Task ProcessModified(V1Secret secret)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            string patchStr = string.Empty;

            Console.WriteLine("Addition/Modify detected: " + secret.Metadata.Name);

            /*
            // locate argocd cluster secret representing this cluster
            Console.WriteLine("** sync 'addons' ...");
            V1Secret? secret = GetClusterArgocdSecret(tkc.Metadata.Name);

            // add missing labels to argocd cluster secret
            Console.WriteLine("- add missing labels to argocd cluster secret:");
            foreach (var l in tkc.Metadata.Labels)
            {
                // only process labels starting with 'addons-'
                if (!l.Key.StartsWith("addons-"))
                {
                    // skip
                    continue;
                }

                // is this label already on the secret?
                bool found = false;

                // use try catch to avoid listing labels on a secret without labels
                foreach (var label in secret.Labels())
                {
                    // only process labels starting with 'addons-'
                    if (!label.Key.StartsWith("addons-"))
                    {
                        // skip
                        continue;
                    }

                    //
                    if ((l.Key == label.Key) && (l.Value == label.Value))
                    {
                        found = true;
                        break;
                    }
                }

                // if not found, add to cluster secret
                if (!found)
                {
                    Console.WriteLine("  - " + l.Key + ": " + l.Value);
                }
            }

            // remove deleted labels from argocd cluster secret
            Console.WriteLine("- remove deleted labels from argocd cluster secret:");
            foreach (var label in secret.Labels())
            {
                // only process labels starting with 'addons-'
                if (!label.Key.StartsWith("addons-"))
                {
                    // skip
                    continue;
                }

                // is this label already on the secret?
                bool found = false;

                // use try catch to avoid listing labels on a secret without labels
                foreach (var l in tkc.Metadata.Labels)
                {
                    // only process labels starting with 'addons-'
                    if (!l.Key.StartsWith("addons-"))
                    {
                        // skip
                        continue;
                    }

                    //
                    if ((l.Key == label.Key) && (l.Value == label.Value))
                    {
                        found = true;
                        break;
                    }
                }

                // if not found, add to cluster secret
                if (!found)
                {
                    Console.WriteLine("  - " + label.Key + ": " + label.Value);
                }
            }
            */

            return;
        }
        /*
        public async Task ProcessDeleted(CrdCluster cluster)
        {
            Console.WriteLine("Deleted detected: " + cluster.Metadata.Name);
            Console.WriteLine("** argocd remove cluster ...");

            // locate server pod
            V1PodList list = await Globals.service.kubeclient.ListNamespacedPodAsync("argocd");
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
            Console.WriteLine("[cluster] before exec");
            int asdf = await Globals.service.kubeclient.NamespacedPodExecAsync(
                pod.Name(), pod.Namespace(), pod.Spec.Containers[0].Name, cmds, false, One, Globals.cancellationToken);
            Console.WriteLine("[cluster] after exec");

            Console.WriteLine("** remove pinniped kubeconfig ...");
        }
        */

        public static string Base64Encode(string text)
        {
            var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
            return System.Convert.ToBase64String(textBytes);
        }
        public static string Base64Decode(string base64)
        {
            var base64Bytes = System.Convert.FromBase64String(base64);
            return System.Text.Encoding.UTF8.GetString(base64Bytes);
        }

        public static V1Secret? GetClusterArgocdSecret(string clusterName)
        {
            //Console.WriteLine("- GetClusterSecret, clusterName: "+ clusterName);
            V1SecretList secrets = Globals.service.kubeclient.ListNamespacedSecret("argocd");

            //Console.WriteLine("- argocd cluster secrets:");
            foreach (V1Secret secret in secrets)
            {
                // is there a label indicating this is a cluster secret?
                if (secret.Labels() == null)
                {
                    //Console.WriteLine("  - skipping, a");
                    continue;
                }
                if (!secret.Labels().TryGetValue("argocd.argoproj.io/secret-type", out var value))
                {
                    //Console.WriteLine("  - skipping, b");
                    continue;
                }
                if (value != "cluster")
                {
                    //Console.WriteLine("  - skipping, c, value: "+ value);
                    continue;
                }

                // is this the cluster we are looking for?
                string name = Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length);
                //Console.WriteLine("  - name: " + name +", tkcName: "+ tkc.Metadata.Name);
                if (name != clusterName)
                {
                    //Console.WriteLine("  - skipping, d");
                    continue;
                }

                /*
                // use regex to match cluster name via argocd secret which represents cluster
                if (!Regex.Match(next.Name(), "cluster-" + tkc.Metadata.Name + "-" + "\\d").Success)
                {
                    // skip secret, this is not the cluster secret
                    continue;
                }
                */

                // secret located
                //Console.WriteLine("- secret located: " + secret.Name());
                return secret;
            }

            return null;
        }

        public static KubernetesClientConfiguration BuildConfigFromArgocdSecret(V1Secret secret)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();

            // form a kubeconfig via the argocd secret
            Console.WriteLine("- form kubeconfig from argocd cluster secret ...");

            // we have a cluster secret, check its name/server
            data.Add("name", Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length));
            data.Add("server", Encoding.UTF8.GetString(secret.Data["server"], 0, secret.Data["server"].Length));
            data.Add("config", Encoding.UTF8.GetString(secret.Data["config"], 0, secret.Data["config"].Length));

            Console.WriteLine("  -   name: " + data["name"]);
            Console.WriteLine("  - server: " + data["server"]);

            // parse kubeconfig json data from argocd secret
            JsonElement o = JsonSerializer.Deserialize<JsonElement>(data["config"]);

            // start with an empty kubeconfig
            KubernetesClientConfiguration kubeconfig = new KubernetesClientConfiguration();

            // form kubeconfig using values from argocd secret
            kubeconfig.Host = data["server"];
            kubeconfig.SkipTlsVerify = o.GetProperty("tlsClientConfig").GetProperty("insecure").GetBoolean();
            kubeconfig.ClientCertificateData = o.GetProperty("tlsClientConfig").GetProperty("certData").GetString();
            kubeconfig.ClientCertificateKeyData = o.GetProperty("tlsClientConfig").GetProperty("keyData").GetString();
            // convert caData into an x509 cert & add
            kubeconfig.SslCaCerts = new X509Certificate2Collection();
            kubeconfig.SslCaCerts.Add(
                    X509Certificate2.CreateFromPem(
                        Base64Decode(o.GetProperty("tlsClientConfig").GetProperty("caData").GetString()).AsSpan()
                ));

            return kubeconfig;
        }
        /// <summary>
        /// With knowledge of the innerworkings of the cluster provisioning process,
        /// obtain the default admin kubeconfig '/etc/kubernetes/admin.conf'.
        /// </summary>
        /// <param name="clusterName"></param>
        /// <returns></returns>
        public static async Task<KubernetesClientConfiguration> GetClusterKubeConfig(string clusterName, string clusterNamespace)
        {
            // clusterctl - n vc - test get kubeconfig vc - test
            // k -n vc-test get secrets vc-test-kubeconfig -o jsonpath='{.data.value}' | base64 -d
            Console.WriteLine($"[cluster] GetClusterKubeConfig ({clusterName}, {clusterNamespace})");

            V1Secret secret = null;
            try
            {
                secret = Globals.service.kubeclient.ReadNamespacedSecret(clusterName + "-kubeconfig", clusterNamespace);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
            secret.Data.TryGetValue("value", out byte[] bytes);
            string kubeconfig = System.Text.Encoding.UTF8.GetString(bytes);
            //Console.WriteLine("[cluster] kubeconfig:\n" + kubeconfig);

            // save kubeconfig to a temporary file
            //string path = Path.GetTempFileName();
            //string path = "/tmp/asdf.txt";
            //Console.WriteLine("tmp path: " + path);

            // exec into server pod, see if we can use 'argocd' there
            //ExecAsyncCallback handler = One;
            var cmds = new List<string>();

            // todo get actual pod name of 'server' pod 

            // todo get clustername used in provided kubeconfig


            // locate server pod
            V1PodList list = await Globals.service.kubeclient.ListNamespacedPodAsync("argocd");
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
            cmds = new List<string>();
            cmds.Add("pwd");
            Console.WriteLine("[cluster] (test) before exec");
            await Globals.service.kubeclient.NamespacedPodExecAsync(
                "argocd-server-57d9b8db7-v8ldh", "argocd", "server", cmds, false, One, Globals.cancellationToken);
            Console.WriteLine("[cluster] (test) after exec");
            */


            /*
            // test
            Console.WriteLine("[cluster] (test) before exec");
            //V1Pod pod = await Globals.service.kubeclient.ReadNamespacedPodAsync("argocd-server-57d9b8db7-v8ldh", "argocd");
            ExecInPod(Globals.service.kubeclient, pod, "pwd");
            //Console.WriteLine("[cluster] (test) output:" + output);
            Console.WriteLine("[cluster] (test) after exec");
            */

            cmds = new List<string>();
            cmds.Add("sh");
            cmds.Add("-c");
            cmds.Add($"echo {Convert.ToBase64String(bytes)} > /tmp/{clusterName}.b64;"
                    + $"cat /tmp/{clusterName}.b64 | base64 -d > /tmp/{clusterName}.conf;"
                    + $"argocd cluster add my-vcluster"
                    + $" -y"
                    + $" --name {clusterName}"
                    + $" --kubeconfig /tmp/{clusterName}.conf"
                    + $" --server=localhost:8080"
                    + $" --plaintext"
                    + $" --insecure"
                    + $" --auth-token={Environment.GetEnvironmentVariable("ARGOCD_AUTH_TOKEN")};"
                    );
            Console.WriteLine("[cluster] before exec");
            int asdf = await Globals.service.kubeclient.NamespacedPodExecAsync(
                pod.Name(), pod.Namespace(), pod.Spec.Containers[0].Name, cmds, false, One, Globals.cancellationToken);
            Console.WriteLine("[cluster] after exec");


            return null;
        }

        public static Task One(Stream stdIn, Stream stdOut, Stream stdErr)
        {
            StreamReader sr = new StreamReader(stdOut);
            while (!sr.EndOfStream)
            {
                Console.WriteLine(sr.ReadLine());
            }

            return null;
        }

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

            //return str;
        }
    }
}
