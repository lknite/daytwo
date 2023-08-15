using k8s;
using System.Text.Json;
using daytwo.K8sControllers;

namespace daytwo
{
    public class Service : IHostedService
    {
        public Main.Main main;

        //
        public KubernetesClientConfiguration kubeconfig;
        public Kubernetes kubeclient;

        // argocd namespace
        public string argocdNamespace = "argocd";
        // reconciliation loop interval
        public int loopInterval = 60;

        //
        //public List<ClusterK8sController> clusters = new List<ClusterK8sController>();

        public Service()
        {
            // 
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

            // Todo: If no management_clusters was specified, see if we can
            //       get a default managementCluster using existing kubeconfig

            // Check for required environment variable(s)
            List<string> required = new List<string>();
            required.Add("MANAGEMENT_CLUSTERS");
            //required.Add("ARGOCD_AUTH_TOKEN");
            //required.Add("ARGOCD_SERVER_URI");
            foreach (string req in required)
            {
                if ((Environment.GetEnvironmentVariable(req) == null)
                    || (Environment.GetEnvironmentVariable(req) == ""))
                {
                    throw new Exception("Missing required environment variable: '" + req + "'");
                }
            }

            // If argocd namespace is specified via environment variable then set here
            if (Environment.GetEnvironmentVariable("ARGOCD_NAMESPACE") != null)
            {
                argocdNamespace = Environment.GetEnvironmentVariable("ARGOCD_NAMESPACE");
            }

            //
            if (Environment.GetEnvironmentVariable("LOOP_INTERVAL") != null)
            {
                loopInterval = int.Parse(Environment.GetEnvironmentVariable("LOOP_INTERVAL"));
            }

            main = new Main.Main();
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
                ClusterK8sController clusterController = new ClusterK8sController(cluster);

                // We could add so we have a list of management clusters we are tracking, but there is no need
                // clusters.Add(clusterController);

                // Start the k8s event listener
                //clusterController.Listen(/*cluster*/);
                // Start the intermittent timer
                //(new Thread(new ThreadStart(clusterController.Timer))).Start();

                // Start k8s event listener & intermittent timer
                clusterController.Start();
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "***");
            Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId), "* TODO: Clean shutdown");

            return Task.CompletedTask;
        }
    }
}
