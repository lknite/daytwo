using daytwo;
using k8s.Models;
using k8s;
using System.Text.Json;
using daytwo.K8sHelpers;
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
using k8s.KubeConfigModels;
using System.Runtime.CompilerServices;

namespace gge.K8sControllers
{
    /*
    public class Provider : IComparable<Provider>
    {
        string api;
        string group;
        string version;
        string plural;

        public GenericClient generic = null;// new GenericClient(Globals.service.kubeclient, group, version, plural);
        public Kubernetes kubeclient = null;
        //public KubernetesClientConfiguration kubeconfig = null;

        public Provider(Kubernetes kubeclient, string api, string group, string version, string plural)
        {
            generic = new GenericClient(kubeclient, group, version, plural);
        }

        public int CompareTo(Provider? other)
        {
            throw new NotImplementedException();
        }
    }
    */
    public class ProviderK8sController
    {
        public string api; // = "tanzukubernetescluster";
        public string group; // = "run.tanzu.vmware.com";
        public string version; // = "v1alpha2";
        public string plural; // = api + "s";

        public string managementCluster;

        public Kubernetes kubeclient = null;
        public KubernetesClientConfiguration kubeconfig = null;

        public GenericClient generic = null;


        public ProviderK8sController(string api, string group, string version, string plural)
        {
            // initialize properties
            this.api = api;
            this.group = group;
            this.version = version;
            this.plural = plural;

            // start listening
            Console.WriteLine($"**** Provider.Add({api}s.{group}.{version})");
        }

        public async Task Listen(string managementCluster)
        {
            // remember which management cluster is using this provider type
            this.managementCluster = managementCluster;

            // locate the provisioning cluster argocd secret
            V1Secret? secret = GetClusterArgocdSecret(managementCluster);
            // use secret to create kubeconfig
            kubeconfig = BuildConfigFromArgocdSecret(secret);
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

                Console.WriteLine("(" + api +") Listen begins ...");
                try
                {
                    await foreach (var (type, item) in generic.WatchNamespacedAsync<CrdProviderCluster>(""))
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

        public async Task ProcessAdded(CrdProviderCluster provider)
        {
            ProcessModified(provider);
        }
        public async Task ProcessModified(CrdProviderCluster provider)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            string patchStr = string.Empty;

            Console.WriteLine("Addition/Modify detected: " + provider.Metadata.Name);

            // acquire argocd cluster secret to so we can sync labels
            V1Secret? secret = GetClusterArgocdSecret(provider.Name());
            if (secret == null)
            {
                Console.WriteLine("(vcluster) unable to locate argocd secret");
                return;
            }

            // check if daytwo is managing this secret
            if (secret.Metadata.EnsureAnnotations()["daytwo.aarr.xyz/resourceVersion"] == null)
            {
                Console.WriteLine("(vcluster) secret is not managed by daytwo, ignoring");
                return;
            }

            // locate argocd cluster secret representing this cluster
            Console.WriteLine("** sync 'addons' ...");

            // add missing labels to argocd cluster secret
            Console.WriteLine("- add missing labels to argocd cluster secret:");
            foreach (var l in provider.Metadata.Labels)
            {
                /*
                // only process labels starting with 'addons-'
                if (!l.Key.StartsWith("addons-"))
                {
                    // skip
                    continue;
                }
                */

                // is this label already on the secret?
                bool found = false;

                // use try catch to avoid listing labels on a secret without labels
                foreach (var label in secret.Labels())
                {
                    /*
                    // only process labels starting with 'addons-'
                    if (!label.Key.StartsWith("addons-"))
                    {
                        // skip
                        continue;
                    }
                    */

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
                    secret.SetLabel(l.Key, l.Value);
                }
            }

            /*
            // patch secret with new labels
            Globals.service.kubeclient.CoreV1.PatchNamespacedSecret(
                    new V1Patch(secret, V1Patch.PatchType.MergePatch), secret.Name(), secret.Namespace());
            */

            // remove deleted labels from argocd cluster secret
            Console.WriteLine("- remove deleted labels from argocd cluster secret:");
            foreach (var label in secret.Labels())
            {
                // avoid deleting argocd labels
                if (label.Key.StartsWith("argocd.argoproj.io/"))
                {
                    continue;
                }

                /*
                // only process labels starting with 'addons-'
                if (!label.Key.StartsWith("addons-"))
                {
                    // skip
                    continue;
                }
                */

                // is this label already on the secret?
                bool found = false;

                // use try catch to avoid listing labels on a secret without labels
                foreach (var l in provider.Metadata.Labels)
                {
                    /*
                    // only process labels starting with 'addons-'
                    if (!l.Key.StartsWith("addons-"))
                    {
                        // skip
                        continue;
                    }
                    */

                    //
                    if ((l.Key == label.Key) && (l.Value == label.Value))
                    {
                        found = true;
                        break;
                    }
                }

                // if not found, remove to cluster secret
                if (!found)
                {
                    Console.WriteLine("  - " + label.Key + ": " + label.Value);
                    secret.SetLabel(label.Key, null);
                }
            }

            Console.WriteLine("resulting label list:");
            foreach (var next in secret.Labels())
            {
                Console.WriteLine("- "+ next.Key +": "+ next.Value);
            }

            // patch secret without removed labels
            string patch = @"{""metadata"":""labels"":";
            patch += JsonSerializer.Serialize(secret.Metadata.Labels);
            patch += "}";
            Console.WriteLine("patch:");
            Console.WriteLine(patch);
            try
            {
                Globals.service.kubeclient.CoreV1.PatchNamespacedSecret(
                        new V1Patch(patch, V1Patch.PatchType.JsonPatch), secret.Name(), secret.Namespace());
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }
            Console.WriteLine("afterpatch");

            return;
        }
        public async Task ProcessDeleted(CrdProviderCluster provider)
        {
            Console.WriteLine("Deleted detected: " + provider.Metadata.Name);
        }

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
        public static KubernetesClientConfiguration GetClusterKubeConfig(string clusterName, string clusterNamespace)
        {
            // clusterctl - n vc - test get kubeconfig vc - test
            // k -n vc-test get secrets vc-test-kubeconfig -o jsonpath='{.data.value}' | base64 -d
            V1Secret secret = Globals.service.kubeclient.ReadNamespacedSecret(clusterName + "-kubeconfig", clusterNamespace);
            secret.Data.TryGetValue("value", out byte[] bytes);
            string kubeconfig = System.Text.Encoding.UTF8.GetString(bytes);
            Console.WriteLine(kubeconfig);

            return null;
        }
    }
}
