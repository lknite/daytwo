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
using YamlDotNet.Serialization.NamingConventions;
using System.Reflection.Emit;
using Json.Patch;
using daytwo.Helpers;

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
            V1Secret? secret = Main.GetClusterArgocdSecret(managementCluster);
            // use secret to create kubeconfig
            kubeconfig = Main.BuildConfigFromArgocdSecret(secret);
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
                            //case WatchEventType.Added:
                            //    await ProcessAdded(item);
                            //    break;
                            //case WatchEventType.Bookmark:
                            //    break;
                            //case WatchEventType.Deleted:
                            //    await ProcessDeleted(item);
                            //    break;
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
            V1Secret? secret = Main.GetClusterArgocdSecret(provider.Name(), managementCluster);
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

            //
            var before = JsonSerializer.SerializeToDocument(secret);
            bool isChange = false;

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
                    isChange = true;
                }
            }

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
                    isChange = true;
                }
            }

            // if no changes then return now without patching
            if (!isChange)
            {
                Console.WriteLine("- no changes detected, update complete");
                return;
            }

            // display adjusted label list
            Console.WriteLine("resulting label list:");
            foreach (var next in secret.Labels())
            {
                Console.WriteLine("- "+ next.Key +": "+ next.Value);
            }

            // generate json patch
            var patch = before.CreatePatch(secret);

            /*
            var patch = new JsonPatchDocument<V1Secret>();
            patch.Replace(x => x.Metadata.Labels, secret.Labels());
            patchStr = Newtonsoft.Json.JsonConvert.SerializeObject(patch);
            patchStr = patchStr.Replace("/Metadata/Labels", "/metadata/labels");
            Console.WriteLine("patch:");
            Console.WriteLine(patchStr);
            */
            try
            {
                Globals.service.kubeclient.CoreV1.PatchNamespacedSecret(
                        new V1Patch(patch, V1Patch.PatchType.JsonPatch), secret.Name(), secret.Namespace());
                        //new V1Patch(patchStr, V1Patch.PatchType.JsonPatch), secret.Name(), secret.Namespace());
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }

            return;
        }
        public async Task ProcessDeleted(CrdProviderCluster provider)
        {
            Console.WriteLine("Deleted detected: " + provider.Metadata.Name);
        }
    }
}
