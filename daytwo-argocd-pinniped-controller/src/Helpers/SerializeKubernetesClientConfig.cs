using k8s;

namespace daytwo.Helpers
{
    public partial class Main
    {
        public static string? SerializeKubernetesClientConfig(KubernetesClientConfiguration kubeconfig, string name)
        {
            string pem = kubeconfig.SslCaCerts[0].ExportCertificatePem();

            return $@"
apiVersion: v1
kind: Config
clusters:
- cluster:
"
+ ((!kubeconfig.SkipTlsVerify) ?
$@"
    certificate-authority-data: {Base64Encode(pem.ToString())}
"
:
@"
    insecure-skip-tls-verify: true
")
+ $@"
    server: {kubeconfig.Host}
  name: {name}
contexts:
- context:
    cluster: {name}
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
