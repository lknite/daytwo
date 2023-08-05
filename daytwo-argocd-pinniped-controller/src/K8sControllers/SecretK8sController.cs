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

        public async Task Intermittent(int seconds)
        {
            while (true)
            {
                // intermittent delay in between checks
                Globals.log.LogInformation("sleeping");
                Thread.Sleep(seconds * 1000);

                // Acquire Semaphore
                semaphore.Wait(Globals.cancellationToken);

                try
                {
                    //**
                    // add pinniped secrets

                    // acquire list of all arogcd secrets
                    V1SecretList list = await kubeclient.ListNamespacedSecretAsync(Globals.service.argocdNamespace);
                    foreach (var item in list)
                    {
                        //
                        if (!Main.IsArgocdClusterSecret(item))
                        {
                            continue;
                        }

                        // attempt to add pinniped kubeconfig
                        await ProcessAdded(item);
                    }

                    //**
                    // remove pinniped secrets

                    // check if existing pinniped secrets have a matching secret
                    bool found = false;
                    if (Directory.Exists("/opt/www"))
                    {
                        var files = from file in Directory.EnumerateFiles("/opt/www", "*kubeconfig", SearchOption.AllDirectories) select file;
                        //Globals.log.LogInformation("Files: {0}", files.Count<string>().ToString());
                        //Globals.log.LogInformation("List of Files");
                        foreach (var file in files)
                        {
                            //Globals.log.LogInformation("{0}", file);
                            string[] parts = file.Split('/');

                            // does this pinniped kubeconfig match up with an existing cluster?
                            found = false;
                            foreach (V1Secret secret in list)
                            {
                                //
                                if (!Main.IsArgocdClusterSecret(secret))
                                {
                                    continue;
                                }

                                string managementCluster = secret.GetAnnotation("daytwo.aarr.xyz/management-cluster");
                                /*
                                foreach (var asdf in secret.Data.Keys)
                                {
                                    Globals.log.LogInformation($"key: {asdf}");
                                }
                                */
                                string workloadCluster = Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length);

                                if (managementCluster == null)
                                {
                                    managementCluster = "tmp";
                                }

                                //Globals.log.LogInformation($"parts[3]: {parts[3]}");
                                //Globals.log.LogInformation($"parts[4]: {parts[4]}");
                                if ((managementCluster == parts[3]) && (workloadCluster == parts[4]))
                                {
                                    found = true;
                                    break;
                                }
                            }

                            // if not, then remove stale pinniped kubeconfig
                            if (!found) 
                            {
                                Globals.log.LogInformation($"Removing stale pinniped kubeconfig: {file}");
                                File.Delete(file);
                            }
                        }
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
        }
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

                        Globals.log.LogInformation("");
                        Globals.log.LogInformation("(event) [" + type + "] " + plural + "." + group + "/" + version + ": " + item.Metadata.Name);

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

                                /*
                                // if reCheck is true, restart listen, this will readd all secrets
                                if (reCheck)
                                {
                                    Thread.Sleep(reCheckSeconds * 1000);
                                    Globals.log.LogInformation("begin recheck");
                                    reCheck = false;
                                    throw new Exception();
                                }
                                */

                                break;
                        }

                        // Release semaphore
                        semaphore.Release();
                    }
                }
                catch (k8s.Autorest.HttpOperationException ex)
                {
                    Globals.log.LogInformation("Exception? " + ex);
                    switch (ex.Response.StatusCode)
                    {
                        // crd is missing, sleep to avoid an error loop
                        case System.Net.HttpStatusCode.NotFound:
                            Globals.log.LogInformation("crd is missing, pausing for a second before retrying");
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
                    //Globals.log.LogInformation("Exception occured while performing 'watch': " + ex);
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

            // Check if we already created a pinniped config from this argocd cluster secret
            if (File.Exists($"/opt/www/{managementCluster}/{workloadCluster}/resourceVersion-{resourceVersion}"))
            {
                Globals.log.LogInformation("- pinniped kubeconfig is up-to-date");
                return;
            }

            //
            KubernetesClientConfiguration tmpkubeconfig = Main.BuildConfigFromArgocdSecret(secret);

            //
            string json = Main.SerializeKubernetesClientConfig(tmpkubeconfig, workloadCluster);
            //Globals.log.LogInformation(json);
            File.WriteAllText("/tmp/tmpkubeconfig", json);
            //File.WriteAllText($"/tmp/{workloadCluster}.conf", json);

            // generate pinniped kubeconfig
            Globals.log.LogInformation("- generate pinniped kubeconfig");
            var p = new Process
            {
                StartInfo = {
                // pinniped get kubeconfig --kubeconfig /tmp/kubeconfig
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = "/usr/local/bin/pinniped",
                WorkingDirectory = @"/tmp",
                Arguments = "get kubeconfig"
                    + " --kubeconfig /tmp/tmpkubeconfig"
            }
            };

            // append whatever parameters are passed in via environment variables
            foreach (string key in Environment.GetEnvironmentVariables().Keys)
            {
                // check if this is a pinniped parameter
                if (key.StartsWith("PINNIPED_"))
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
                Globals.log.LogInformation($"- error generating pinniped kubeconfig (is pinniped-concierge installed and running?)");

                return;
            }

            // capture output
            string tmp = "";
            //Globals.log.LogInformation("parse output");
            while (!p.StandardOutput.EndOfStream)
            {
                tmp += p.StandardOutput.ReadLine();
                tmp += "\n";
            }
            //Globals.log.LogInformation("display output");
            //Globals.log.LogInformation(tmp);

            //Globals.log.LogInformation("after generate pinniped kubeconfig");

            // debug, show stdout from the command
            //Globals.log.LogInformation("create 'www' folder structure");
            Directory.CreateDirectory($"/opt/www");
            Directory.CreateDirectory($"/opt/www/{managementCluster}");
            Directory.CreateDirectory($"/opt/www/{managementCluster}/{workloadCluster}");

            // save to file (accessible via GET)
            //Globals.log.LogInformation("copy to www folder");
            try
            {
                // write out pinniped kubeconfig
                File.WriteAllText($"/opt/www/{managementCluster}/{workloadCluster}/kubeconfig", tmp);

                // also write out argocd resourceVersion, use to check for up-to-date pinniped config
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
            Globals.log.LogInformation("remove pinniped kubeconfig");

            string managementCluster = secret.GetAnnotation("daytwo.aarr.xyz/management-cluster");
            string workloadCluster = Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length);

            if (managementCluster == null)
            {
                managementCluster = "tmp";
            }

            File.Delete($"/opt/www/{managementCluster}/{workloadCluster}/kubeconfig");

            return;
        }
    }
}
