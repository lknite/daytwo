using daytwo;
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
using System.Net.Sockets;
using System.Diagnostics;
using daytwo.Helpers;
using Microsoft.Win32;
using daytwo.crd.K10Cluster;
using Json.More;

namespace gge.K8sControllers
{
    public class SecretK8sController
    {
        static string api = "secret";
        static string group = "";
        static string version = "v1";
        static string plural = api + "s";

        public Kubernetes kubeclient = null;
        public KubernetesClientConfiguration kubeconfig = null;

        public GenericClient generic = null;

        // Enforce only processing one watch event at a time
        SemaphoreSlim semaphore = null;


        public SecretK8sController()
        {
            // use secret to create kubeconfig
            kubeconfig = KubernetesClientConfiguration.BuildDefaultConfig();
            // use kubeconfig to create client
            kubeclient = new Kubernetes(kubeconfig);

            //
            generic = new GenericClient(kubeclient, group, version, plural);

            // Prep semaphore for only 1 action at a time
            semaphore = new SemaphoreSlim(1);
        }
        public async Task Start()
        {
            // Start the k8s event listener
            //Listen();
            // Start the intermittent timer
            (new Thread(new ThreadStart(Timer))).Start();
        }
        public void Timer()
        {
            while (!Globals.cancellationToken.IsCancellationRequested)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "sleeping");
                Thread.Sleep(Globals.service.loopInterval * 1000);

                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "Intermittent");
                Intermittent();
            }
        }

        public async Task Intermittent(/*int seconds*/)
        {
            /*
            while (true)
            {
                // intermittent delay in between checks
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "sleeping");
                Thread.Sleep(seconds * 1000);
            */

            // Acquire Semaphore
            semaphore.Wait(Globals.cancellationToken);

            // Get kubeconfig of primary cluster
            V1Secret? k10primary = Main.GetClusterArgocdSecret(Environment.GetEnvironmentVariable("PRIMARY_CLUSTER"));
            if (k10primary == null)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api),
                        "Unable to load up primary_cluster argocd cluster secret, this needs to already exist, abort");
                return;
            }
            KubernetesClientConfiguration k10kubeconfig = Main.BuildConfigFromArgocdSecret(k10primary);
            Kubernetes k10kubeclient = new Kubernetes(k10kubeconfig);

            // Check if primary cluster is configured
            GenericClient gk10 = new GenericClient(
                    k10kubeclient,
                    "dist.kio.kasten.io",
                    "v1alpha1",
                    "clusters");
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), $"Primary Cluster: {Environment.GetEnvironmentVariable("PRIMARY_CLUSTER")}");
            try
            {
                CrdK10Cluster primary = await gk10.ReadNamespacedAsync<CrdK10Cluster>("kasten-io-mc", Environment.GetEnvironmentVariable("PRIMARY_CLUSTER"));
            }
            catch
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "ex, primary not found");
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "register primary");

                await AddPrimary(k10kubeconfig);
            }

            try
            {
                //**
                // add kasten secondaries

                // acquire list of all arogcd secrets
                V1SecretList list = await kubeclient.ListNamespacedSecretAsync(Globals.service.argocdNamespace);
                foreach (var item in list)
                {
                    //
                    if (!Main.IsArgocdClusterSecret(item))
                    {
                        continue;
                    }

                    // if requiredLabel is defined, only process if is present
                    //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId),
                    //        $"REQUIRED_LABEL: {Environment.GetEnvironmentVariable("REQUIRED_LABEL")}");
                    if ((Environment.GetEnvironmentVariable("REQUIRED_LABEL") != null)
                        && (Environment.GetEnvironmentVariable("REQUIRED_LABEL").Length > 0))
                    {
                        if (item.GetLabel(Environment.GetEnvironmentVariable("REQUIRED_LABEL")) == null)
                        {
                            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId),
                            //        $"missing required label: {Environment.GetEnvironmentVariable("REQUIRED_LABEL")}");

                            continue;
                        }
                    }

                    // is there a k10 cluster resouce for this cluster?
                    string clusterName = Encoding.UTF8.GetString(item.Data["name"]);
                    //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), $"- {clusterName}");
                    try
                    {
                        CrdK10Cluster cluster = await gk10.ReadNamespacedAsync<CrdK10Cluster>(
                                "kasten-io-mc", clusterName);

                        // skip if this is the primary
                        string? label = cluster.GetLabel("dist.kio.kasten.io/cluster-type");
                        if (label != null)
                        {
                            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), $"  - k10 cluster found ({label})");
                            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), $"- {clusterName}: k10 cluster found ({label})");

                            if (label == "primary")
                            {
                                continue;
                            }

                            // finished processing secondary
                            continue;
                        }
                        else
                        {
                            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), $"  - cluster-type label not found");
                        }
                    }
                    catch
                    {
                        //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), $"  - k10 cluster not found, todo: register secondary");
                        Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), $"- {clusterName}: k10 cluster not found");
                    }


                    //
                    KubernetesClientConfiguration secondaryk10kubeconfig = Main.BuildConfigFromArgocdSecret(item);

                    // add secondary
                    await AddSecondary(k10kubeconfig, secondaryk10kubeconfig, clusterName);
                }


                //**
                // remove kasten secondaries

                CustomResourceList<CrdK10Cluster> items = await gk10.ListNamespacedAsync<CustomResourceList<CrdK10Cluster>>("kasten-io-mc");
                foreach (var cluster in items.Items)
                {
                    // we have a registered k10 cluster, check if it matches up with an argocd cluster secret

                    // does this registered k10 cluster have an associated argocd cluster secret?
                    if (Main.GetClusterArgocdSecret(cluster.Name()) != null)
                    {
                        // yes, matching argocd cluster secret found
                        //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api),
                        //        $"- matching argocd cluster secret found");
                        continue;
                    }

                    // unregister this cluster
                    Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api),
                            $"- missing matching argocd cluster secret, removing: {cluster.Name()}");
                    await RemoveSecondary(k10kubeconfig, cluster.Name());
                }
            }
            catch (Exception ex)
            {
                Globals.log.LogInformation($"{ex.Message}", ex);
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
        public async Task AddPrimary(KubernetesClientConfiguration primaryk10kubeconfig)
        {
            string output = string.Empty;

            output = Main.SerializeKubernetesClientConfig(primaryk10kubeconfig, Environment.GetEnvironmentVariable("PRIMARY_CLUSTER"));
            File.WriteAllText("/tmp/primary.conf", output);

            // add secondary cluster
            string primaryClusterContextName = Environment.GetEnvironmentVariable("PRIMARY_CLUSTER");
            string primaryClusterName = Environment.GetEnvironmentVariable("PRIMARY_CLUSTER");
            var p = new Process
            {
                StartInfo = {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            FileName = "/usr/local/bin/k10multicluster",
                            WorkingDirectory = @"/tmp",
                            Arguments = "setup-primary"
                                + $" --name={primaryClusterName}"
                                + $" --context={primaryClusterContextName}"
                                + $" --kubeconfig=/tmp/primary.conf"
                        }
            };

            try
            {
                Globals.log.LogInformation(p.StartInfo.Arguments);
                //
                p.Start();
                p.WaitForExit();

                // if there was an error, we stop here
                if (p.ExitCode != 0)
                {
                    // add kasten secondary
                    //await ProcessAdded(item);
                }

                // capture output
                string tmp = "";
                //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "parse output");
                while (!p.StandardOutput.EndOfStream)
                {
                    tmp += p.StandardOutput.ReadLine();
                    tmp += "\n";
                }
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "output:");
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), tmp);
            }
            catch (Exception ex)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "ex: " + ex.Message);
            }
        }

        public async Task AddSecondary(
                KubernetesClientConfiguration primaryk10kubeconfig,
                KubernetesClientConfiguration secondaryk10kubeconfig,
                string clusterName
                )
        {
            string output = string.Empty;

            output = Main.SerializeKubernetesClientConfig(primaryk10kubeconfig, Environment.GetEnvironmentVariable("PRIMARY_CLUSTER"));
            File.WriteAllText("/tmp/primary.conf", output);
            output = Main.SerializeKubernetesClientConfig(secondaryk10kubeconfig, clusterName);
            File.WriteAllText("/tmp/secondary.conf", output);

            // get ingress
            Kubernetes secondaryk10kubeclient = new Kubernetes(secondaryk10kubeconfig);
            V1Ingress ingress = null;
            try
            {
                ingress = await secondaryk10kubeclient.ReadNamespacedIngressAsync("k10-ingress", "kasten-io");
            }
            catch
            {
                // ingress is required, abandon now if not present
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "- ingress not found, skipping");
                return;
            }

            // add secondary cluster
            string primaryClusterContextName = Environment.GetEnvironmentVariable("PRIMARY_CLUSTER");
            string primaryClusterName = Environment.GetEnvironmentVariable("PRIMARY_CLUSTER");
            string secondaryClusterContextName = clusterName;
            string secondaryClusterName = clusterName;
            var p = new Process
            {
                StartInfo = {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            FileName = "/usr/local/bin/k10multicluster",
                            WorkingDirectory = @"/tmp",
                            Arguments = "bootstrap"
                                + $" --primary-name={primaryClusterName}"
                                + $" --primary-context={primaryClusterContextName}"
                                + $" --primary-kubeconfig=/tmp/primary.conf"
                                + $" --secondary-name={secondaryClusterName}"
                                + $" --secondary-context={secondaryClusterContextName}"
                                + $" --secondary-kubeconfig=/tmp/secondary.conf"
                                + $" --secondary-cluster-ingress=\"https://{ingress.Spec.Rules[0].Host}/k10\""
                                + $" --secondary-cluster-ingress-tls-insecure"
                        }
            };

            try
            {
                Globals.log.LogInformation(p.StartInfo.Arguments);
                //
                p.Start();
                p.WaitForExit();

                // if there was an error, we stop here
                if (p.ExitCode != 0)
                {
                    // add kasten secondary
                    //await ProcessAdded(item);
                }

                // capture output
                string tmp = "";
                //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "parse output");
                while (!p.StandardOutput.EndOfStream)
                {
                    tmp += p.StandardOutput.ReadLine();
                    tmp += "\n";
                }
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "output:");
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), tmp);
            }
            catch (Exception ex)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "ex: " + ex.Message);
            }
        }
        public async Task RemoveSecondary(
                KubernetesClientConfiguration primaryk10kubeconfig,
                string clusterName
                )
        {
            // k10multicluster has a bug with disconnect, just delete the crd resource instead as a work-around
            Kubernetes primaryk10kubeclient = new Kubernetes(primaryk10kubeconfig);

            GenericClient gk10 = new GenericClient(
                    primaryk10kubeclient,
                    "dist.kio.kasten.io",
                    "v1alpha1",
                    "clusters");
            try
            {
                /*
                // testing access
                Console.WriteLine("check access ...");
                CustomResourceList<CrdK10Cluster> items = await gk10.ListNamespacedAsync<CustomResourceList<CrdK10Cluster>>("kasten-io-mc");
                foreach (var cluster in items.Items)
                {
                    Console.WriteLine("- "+ cluster.Name());
                }
                */

                // get resource
                CrdK10Cluster cluster = await gk10.ReadNamespacedAsync<CrdK10Cluster>("kasten-io-mc", clusterName);

                // delete resource
                await gk10.DeleteNamespacedAsync<CrdK10Cluster>("kasten-io-mc", clusterName);

                // delete resource finalizers
                cluster.Finalizers().Clear();

                // update resource, now without finalizers
                await gk10.PatchNamespacedAsync<CrdK10Cluster>(
                    new V1Patch(cluster.Metadata, V1Patch.PatchType.MergePatch), "kasten-io-mc", clusterName);

            }
            catch (Exception ex)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "ex: " + ex.Message);
            }

            /*
            string output = string.Empty;

            output = Main.SerializeKubernetesClientConfig(primaryk10kubeconfig, Environment.GetEnvironmentVariable("PRIMARY_CLUSTER"));
            File.WriteAllText("/tmp/primary.conf", output);

            // remove secondary cluster
            string primaryClusterContextName = Environment.GetEnvironmentVariable("PRIMARY_CLUSTER");
            string primaryClusterName = Environment.GetEnvironmentVariable("PRIMARY_CLUSTER");
            string secondaryClusterName = clusterName;
            var p = new Process
            {
                StartInfo = {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            FileName = "/usr/local/bin/k10multicluster",
                            WorkingDirectory = @"/tmp",
                            Arguments = "disconnect"
                                + $" --primary-name={primaryClusterName}"
                                + $" --primary-context={primaryClusterContextName}"
                                + $" --primary-kubeconfig=/tmp/primary.conf"
                                + $" --secondary-name={secondaryClusterName}"
                        }
            };

            try
            {
                Globals.log.LogInformation(p.StartInfo.Arguments);
                //
                p.Start();
                p.WaitForExit();

                // if there was an error, we stop here
                if (p.ExitCode != 0)
                {
                    // add kasten secondary
                    //await ProcessAdded(item);
                }

                // capture output
                string tmp = "";
                //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "parse output");
                while (!p.StandardOutput.EndOfStream)
                {
                    tmp += p.StandardOutput.ReadLine();
                    tmp += "\n";
                }
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "output:");
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), tmp);
            }
            catch (Exception ex)
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "ex: " + ex.Message);
            }
            */
        }
        /*
        public async Task Listen()
        {
            // Watch is a tcp connection therefore it can drop, use a while loop to restart as needed.
            while (true)
            {
                Globals.log.LogInformation(DateTime.UtcNow +" (" + api +") Listen begins ...");
                try
                {
                    await foreach (var (type, item) in generic.WatchNamespacedAsync<V1Secret>(Globals.service.argocdNamespace))
                    {
                        if (!Main.IsArgocdClusterSecret(item))
                        {
                            continue;
                        }

                        Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "");
                        Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "(event) [" + type + "] " + plural + "." + group + "/" + version + ": " + item.Metadata.Name);

                        // Acquire Semaphore
                        semaphore.Wait(Globals.cancellationToken);

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
                        semaphore.Release();
                    }
                }
                catch (k8s.Autorest.HttpOperationException ex)
                {
                    Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "Exception? " + ex);
                    switch (ex.Response.StatusCode)
                    {
                        // crd is missing, sleep to avoid an error loop
                        case System.Net.HttpStatusCode.NotFound:
                            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "crd is missing, pausing for a second before retrying");
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
                    //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "Exception occured while performing 'watch': " + ex);
                    //Globals.log.LogInformation(new EventId(100, "asdf"), "exception caught");

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

        public async Task ProcessAdded(V1Secret secret)
        {
            await ProcessModified(secret);
        }
        public async Task ProcessModified(V1Secret secret)
        {
            string managementCluster = secret.GetAnnotation("daytwo.aarr.xyz/management-cluster");
            string workloadCluster = Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length);
            string resourceVersion = secret.Metadata.ResourceVersion;

            if (managementCluster == null)
            {
                managementCluster = "tmp";
                Globals.log.LogInformation($"syncing argocd cluster secret: {workloadCluster}");
            }
            else
            {
                Globals.log.LogInformation($"syncing argocd cluster secret: {managementCluster}/{workloadCluster}");
            }

            // Check if we already created a kasten config from this argocd cluster secret
            if (File.Exists($"/opt/www/{managementCluster}/{workloadCluster}/resourceVersion-{resourceVersion}"))
            {
                Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "- kasten kubeconfig is up-to-date");
                return;
            }

            //
            KubernetesClientConfiguration tmpkubeconfig = Main.BuildConfigFromArgocdSecret(secret);

            //
            string json = Main.SerializeKubernetesClientConfig(tmpkubeconfig, workloadCluster);
            //Globals.log.LogInformation(json);
            File.WriteAllText("/tmp/tmpkubeconfig", json);
            //File.WriteAllText($"/tmp/{workloadCluster}.conf", json);

            // generate kasten kubeconfig
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "- generate kasten kubeconfig");
            var p = new Process
            {
                StartInfo = {
                // kasten get kubeconfig --kubeconfig /tmp/kubeconfig
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = "/usr/local/bin/kasten",
                WorkingDirectory = @"/tmp",
                Arguments = "get kubeconfig"
                    + " --kubeconfig /tmp/tmpkubeconfig"
            }
            };

            // append whatever parameters are passed in via environment variables
            foreach (string key in Environment.GetEnvironmentVariables().Keys)
            {
                // check if this is a kasten parameter
                if (key.StartsWith("kasten_"))
                {
                    string name = key.Substring(9).ToLower().Replace("_", "-");
                    string value = string.Empty;

                    if (Environment.GetEnvironmentVariable(key) == "false")
                    {
                        continue;
                    }
                    else if (Environment.GetEnvironmentVariable(key) == "true")
                    {
                        // append new parameter
                        p.StartInfo.Arguments += $" --{name}";
                    }
                    else
                    {
                        value = Environment.GetEnvironmentVariable(key);

                        // append new parameter
                        p.StartInfo.Arguments += $" --{name} {value}";
                    }
                }
            }
            Globals.log.LogInformation(p.StartInfo.Arguments);
            //
            p.Start();
            p.WaitForExit();

            // if there was an error, we stop here
            if (p.ExitCode != 0)
            {
                Globals.log.LogInformation($"- error generating kasten kubeconfig (is kasten-concierge installed and running?)");

                return;
            }

            // capture output
            string tmp = "";
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "parse output");
            while (!p.StandardOutput.EndOfStream)
            {
                tmp += p.StandardOutput.ReadLine();
                tmp += "\n";
            }
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "display output");
            //Globals.log.LogInformation(tmp);

            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "after generate kasten kubeconfig");

            // debug, show stdout from the command
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "create 'www' folder structure");
            Directory.CreateDirectory($"/opt/www");
            Directory.CreateDirectory($"/opt/www/{managementCluster}");
            Directory.CreateDirectory($"/opt/www/{managementCluster}/{workloadCluster}");

            // save to file (accessible via GET)
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "copy to www folder");
            try
            {
                // write out kasten kubeconfig
                File.WriteAllText($"/opt/www/{managementCluster}/{workloadCluster}/kubeconfig", tmp);

                // also write out argocd resourceVersion, use to check for up-to-date kasten config
                File.WriteAllText($"/opt/www/{managementCluster}/{workloadCluster}/resourceVersion-{resourceVersion}", "");
            }
            catch (Exception ex)
            {
                Globals.log.LogInformation(ex.ToString());
            }

            return;
        }
        public async Task ProcessDeleted(V1Secret secret)
        {
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "remove kasten kubeconfig");

            string managementCluster = secret.GetAnnotation("daytwo.aarr.xyz/management-cluster");
            string workloadCluster = Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length);

            if (managementCluster == null)
            {
                managementCluster = "tmp";
            }

            File.Delete($"/opt/www/{managementCluster}/{workloadCluster}/kubeconfig");

            return;
        }
        */
    }
}
