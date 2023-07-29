using k8s;

namespace daytwo.Helpers
{
    public partial class Main
    {
        public static string? SerializeKubernetesClientConfig(KubernetesClientConfiguration kubeconfig)
        {
            Span<char> span = new Span<char>();
            if (!kubeconfig.SslCaCerts[0].TryExportCertificatePem(span, out int count))
            {
                Console.WriteLine("unable to serialize kubeconfig");
                return null;
            }
            Console.WriteLine(Base64Encode(span.ToString()));

            return $@"
apiVersion: v1
kind: Config
clusters:
- cluster:
    certificate-authority-data: {kubeconfig.ClientCertificateData}
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
