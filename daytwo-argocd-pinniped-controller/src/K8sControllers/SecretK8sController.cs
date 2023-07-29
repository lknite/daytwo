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

            //
            KubernetesClientConfiguration kubeconfig = Main.BuildConfigFromArgocdSecret(secret);
            Console.WriteLine(JsonSerializer.Serialize(kubeconfig));

            // generate pinniped kubeconfig
            var p = new Process
            {
                StartInfo = {
                    FileName = "pinniped",
                    WorkingDirectory = @"/usr/local/bin",
                    Arguments = "version"
                }
            };
            p.Start();
            p.WaitForExit();
            while (!p.StandardOutput.EndOfStream)
            {
                Console.WriteLine(p.StandardOutput.ReadLine());
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
