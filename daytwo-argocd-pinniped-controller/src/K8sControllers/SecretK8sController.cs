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


        public async Task Listen()
        {
            // use secret to create kubeconfig
            kubeconfig = KubernetesClientConfiguration.BuildDefaultConfig();
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
                    await foreach (var (type, item) in generic.WatchNamespacedAsync<V1Secret>(Globals.service.argocdNamespace))
                    {
                        // check that this secret is an argocd cluster secret
                        if (item.Labels() == null)
                        {
                            //Console.WriteLine("- ignoring, not a cluster secret");
                            continue;
                        }
                        if (!item.Labels().TryGetValue("argocd.argoproj.io/secret-type", out var value))
                        {
                            //Console.WriteLine("- ignoring, not a cluster secret");
                            continue;
                        }
                        if (value != "cluster")
                        {
                            //Console.WriteLine("- ignoring, not a cluster secret");
                            continue;
                        }

                        Console.WriteLine("");
                        Console.WriteLine("(event) [" + type + "] " + plural + "." + group + "/" + version + ": " + item.Metadata.Name);

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
            Console.WriteLine("update configmap");

            string managementCluster = secret.GetAnnotation("daytwo.aarr.xyz/management-cluster");
            string workloadCluster = Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length);

            if (managementCluster == null)
            {
                managementCluster = "tmp";
            }

            //
            KubernetesClientConfiguration kubeconfig = Main.BuildConfigFromArgocdSecret(secret);
            /*
            try
            {
                Console.WriteLine(JsonSerializer.Serialize(kubeconfig));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            Console.WriteLine("try serialize (2)");
            */
            string json = Main.SerializeKubernetesClientConfig(kubeconfig, workloadCluster);
            //Console.WriteLine(json);
            File.WriteAllText("/tmp/kubeconfig", json);

            // generate pinniped kubeconfig
            Console.WriteLine("generate pinniped kubeconfig");
            var p = new Process
            {
                StartInfo = {
                // pinniped get kubeconfig --kubeconfig /tmp/kubeconfig
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = "pinniped",
                WorkingDirectory = @"/tmp",
                Arguments = "get kubeconfig"
                    + " --kubeconfig /tmp/kubeconfig"
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
            //Console.WriteLine(p.StartInfo.Arguments);
            //
            p.Start();
            p.WaitForExit();

            // if there was an error, we stop here
            if (p.ExitCode != 0)
            {
                Console.WriteLine("error generating pinniped kubeconfig");
                return;
            }

            // capture output
            string tmp = "";
            Console.WriteLine("parse output");
            while (!p.StandardOutput.EndOfStream)
            {
                tmp += p.StandardOutput.ReadLine();
                tmp += "\n";
            }
            Console.WriteLine("display output");
            Console.WriteLine(tmp);

            Console.WriteLine("after generate pinniped kubeconfig");

            // debug, show stdout from the command
            Console.WriteLine("create 'www' folder structure");
            Directory.CreateDirectory($"/opt/www");
            Directory.CreateDirectory($"/opt/www/{managementCluster}");
            Directory.CreateDirectory($"/opt/www/{managementCluster}/{workloadCluster}");

            // save to file (accessible via GET)
            Console.WriteLine("copy to www folder");
            try
            {
                Console.WriteLine("write to file: '"+ $"/opt/www/{managementCluster}/{workloadCluster}/kubeconfig" +"'");
                File.WriteAllText($"/opt/www/{managementCluster}/{workloadCluster}/kubeconfig", tmp);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return;
        }
        public async Task ProcessDeleted(V1Secret secret)
        {
            Console.WriteLine("remove from configmap");
            return;
        }
    }
}
