using k8s.Models;
using k8s;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text;
using System.Runtime.CompilerServices;

namespace daytwo.Helpers
{
    public partial class Main
    {
        public static KubernetesClientConfiguration BuildConfigFromArgocdSecret(V1Secret secret)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();


            // form a kubeconfig via the argocd secret
            Globals.log.LogInformation("- get kubeconfig from argocd secret:"
                    + $" name: {Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length)}"
                    + $", server: {Encoding.UTF8.GetString(secret.Data["server"], 0, secret.Data["server"].Length)}"
                    );

            // we have a cluster secret, check its name/server
            data.Add("name", Encoding.UTF8.GetString(secret.Data["name"], 0, secret.Data["name"].Length));
            data.Add("server", Encoding.UTF8.GetString(secret.Data["server"], 0, secret.Data["server"].Length));
            data.Add("config", Encoding.UTF8.GetString(secret.Data["config"], 0, secret.Data["config"].Length));

            //Globals.log.LogInformation("  -        name: " + data["name"]);
            //Globals.log.LogInformation("  -      server: " + data["server"]);

            // parse kubeconfig json data from argocd secret
            JsonElement o = JsonSerializer.Deserialize<JsonElement>(data["config"]);

            // start with an empty kubeconfig
            KubernetesClientConfiguration kubeconfig = new KubernetesClientConfiguration();

            // form kubeconfig using values from argocd secret
            kubeconfig.Host = data["server"];
            kubeconfig.SkipTlsVerify = o.GetProperty("tlsClientConfig").GetProperty("insecure").GetBoolean();
            kubeconfig.ClientCertificateData = o.GetProperty("tlsClientConfig").GetProperty("certData").GetString();
            kubeconfig.ClientCertificateKeyData = o.GetProperty("tlsClientConfig").GetProperty("keyData").GetString();
            // convert caData into an x509 cert & add
            kubeconfig.SslCaCerts = new X509Certificate2Collection();
            kubeconfig.SslCaCerts.Add(
                    X509Certificate2.CreateFromPem(
                        Base64Decode(o.GetProperty("tlsClientConfig").GetProperty("caData").GetString()).AsSpan()
                ));

            return kubeconfig;
        }
    }
}
