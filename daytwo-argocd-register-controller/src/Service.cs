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

        //
        //public List<ClusterK8sController> clusters = new List<ClusterK8sController>();

        public Service()
        {
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


            string[] clusters = Environment.GetEnvironmentVariable("MANAGEMENT_CLUSTERS").Split(",");
            foreach (var cluster in clusters)
            {
                // Now that we have our kubeconfig, go ahead and instantiate the k8s controllers
                ClusterK8sController clusterController = new ClusterK8sController();

                // We could add so we have a list of management clusters we are tracking, but there is no need
                // clusters.Add(clusterController);

                // Start up all the k8s controllers
                clusterController.Listen(cluster);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("***");
            Console.WriteLine("* TODO: Clean shutdown");

            return Task.CompletedTask;
        }
    }
}
