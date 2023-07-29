using k8s;

namespace daytwo.Helpers
{
    public partial class Main
    {
        public static string SerializeKubernetesClientConfig(KubernetesClientConfiguration kubeconfig)
        {
            /*
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

                        return $@"
            apiVersion: v1
            kind: Config
            clusters:
            - cluster:
                certificate-authority-data: {kubeconfig.ClientCertificateData}
                server: {kubeconfig.Host}
              name: k-ceph
            contexts:
            - context:
                cluster: k-root-b
                namespace: argocd
                user: k-root-b-admin
              name: k-argocd-admin
            current-context: k-argocd-admin

            preferences: {{}}
            users:
            - name: k-ceph-admin
              user:
                client-certificate-data:
            ";

                        return kubeconfig;
            */
            return "";
        }
    }
}
