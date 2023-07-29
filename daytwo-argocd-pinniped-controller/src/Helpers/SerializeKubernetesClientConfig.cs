using k8s;

namespace daytwo.Helpers
{
    public partial class Main
    {
        public static string? SerializeKubernetesClientConfig(KubernetesClientConfiguration kubeconfig)
        {
            string pem = kubeconfig.SslCaCerts[0].ExportCertificatePem();
            //Console.WriteLine(pem);
            //Console.WriteLine(Base64Encode(pem.ToString()));

            return $@"
apiVersion: v1
kind: Config
clusters:
- cluster:
    certificate-authority-data: {Base64Encode(pem.ToString())}
    server: {kubeconfig.Host}
  name: cluster
contexts:
- context:
    cluster: cluster
    namespace: default
    user: user
  name: context
current-context: context
users:
- name: user
  user:
    client-certificate-data: {kubeconfig.ClientCertificateData}
    client-key-data: {kubeconfig.ClientCertificateKeyData}
";
        }
    }
}
