using k8s.Models;

namespace daytwo.Helpers
{
    public partial class Main
    {
        public static bool IsArgocdClusterSecret(V1Secret secret)
        {
            // check that this secret is an argocd cluster secret
            if (secret.Labels() == null)
            {
                return false;
            }
            if (!secret.Labels().TryGetValue("argocd.argoproj.io/secret-type", out var value))
            {
                return false;
            }
            if (value != "cluster")
            {
                return false;
            }

            return true;
        }
    }
}
