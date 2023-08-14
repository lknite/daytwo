using k8s;

namespace daytwo.Helpers
{
    public partial class Main
    {
        public static string? SerializeKubernetesClientConfig(KubernetesClientConfiguration kubeconfig, string name)
        {
            return $@"
apiVersion: v1
kind: Config
clusters:
- cluster:
"
+ ((!kubeconfig.SkipTlsVerify) ?
$@"
    certificate-authority-data: {Base64Encode(kubeconfig.SslCaCerts[0].ExportCertificatePem().ToString())}
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
    user: {name}-admin
  name: {name}
current-context: {name}
users:
- name: {name}-admin
  user:
    client-certificate-data: {kubeconfig.ClientCertificateData}
    client-key-data: {kubeconfig.ClientCertificateKeyData}
";
        }
    }
}
