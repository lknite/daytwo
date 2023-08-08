using daytwo;
using k8s;
using k8s.Models;
using System.Text;

namespace daytwo.Helpers
{
    public partial class Main
    {
        public static V1Secret? GetClusterArgocdSecret(string clusterName, string? managementCluster = null)
        {
            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "- GetClusterSecret, clusterName: "+ clusterName);
            V1SecretList secrets = Globals.service.kubeclient.ListNamespacedSecret(Globals.service.argocdNamespace);

            //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "- argocd cluster secrets:");
            foreach (V1Secret secret in secrets)
            {
                if (!IsArgocdClusterSecret(secret))
                {
                    continue;
                }

                // is this secret associated with the specified management cluster?
                if (managementCluster != null)
                {
                    string? tmp = secret.GetAnnotation("daytwo.aarr.xyz/management-cluster");
                    if (managementCluster != tmp)
                    {
                        continue;
                    }
                }

                // is this the cluster we are looking for?
                string name = Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length);
                //Globals.log.LogInformation(new EventId(Thread.CurrentThread.ManagedThreadId, api), "  - name: " + name +", tkcName: "+ tkc.Metadata.Name);
                if (name != clusterName)
                {
                    continue;
                }

                // secret located
                return secret;
            }

            return null;
        }
    }
}
