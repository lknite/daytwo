using k8s;
using System.Text.Json;
using gge.K8sControllers;

namespace daytwo
{
    public class Service : IHostedService
    {
        public Main.Main main;

        //
        public KubernetesClientConfiguration kubeconfig;
        public Kubernetes kubeclient;

        // save argocd namespace
        public string argocdNamespace = "argocd";

        //
        public SecretK8sController secretController = new SecretK8sController();

        public Service()
        {
            /*
            // Check for required environment variable(s)
            List<string> required = new List<string>();
            required.Add("MANAGEMENT_CLUSTERS");
            required.Add("ARGOCD_AUTH_TOKEN");
            foreach (string req in required)
            {
                if (Environment.GetEnvironmentVariable(req) == null)
                {
                    throw new Exception("Missing required environment variable: '" + req + "'");
                }
            }
            */

            // If argocd namespace is specified via environment variable then set here
            if (Environment.GetEnvironmentVariable("ARGOCD_NAMESPACE") != null)
            {
                argocdNamespace = Environment.GetEnvironmentVariable("ARGOCD_NAMESPACE");
            }

            main = new Main.Main();

            try
            {
                // Load from the default kubeconfig on the machine.
                kubeconfig = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            }
            catch
            {
                //
                kubeconfig = KubernetesClientConfiguration.InClusterConfig();
            }

            // Use the config object to create a client.
            kubeclient = new Kubernetes(kubeconfig);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // save global reference for easy access
            Globals.service = this;

            //
            main.Start();

            // sync everything at the specified interval, needed in case:
            // - a delete event was missed
            // - provider modify event will cause pinniped kubeconfig generation but pinniped may not be ready
            //secretController.Intermittent(1 * 60);
            // instantly perform work in response to events
            //secretController.Listen();

            // Start k8s event listener & intermittent timer
            secretController.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Globals.log.LogInformation("***");
            Globals.log.LogInformation("* TODO: Clean shutdown");

            return Task.CompletedTask;
        }
    }
}
