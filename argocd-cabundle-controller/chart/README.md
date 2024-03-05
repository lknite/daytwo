## argocd-cabundle-controller

1. Merges default certificates along with passed in certificates using update-ca-trust
2. Stores the result in a pvc
3. Shares resulting ca-bundle via http(s)

### Additional ca-bundle formats
1. Generates jks from ca-bundle
2. Shares resulting keytool format via http(s)
3. Generates pkcs12 from ca-bundle
4. Shares resulting keytool format via http(s)

### Namespace labels
Since kubernetes v1.26 [a single pvc may be shared and accessed via multiple namespaces](https://kubernetes.io/docs/concepts/storage/persistent-volumes/#cross-namespace-data-sources) making the copying of a kubernetes resources to each namespace unnecessary.  However, for compatibility, resources may be generated as needed.

#### Auto-Generate Kubernetes resource
- argocd-cabundle-generate-pvc: true
- argocd-cabundle-generate-configmap: true
- argocd-cabundle-generate-secret: true

### Pod / Deployment labels
Works to mount the pvc ca-bundle automatically, via a 'mutating admission webhook', but there are many edge cases. 
- argocd-cabundle-automount-cabundle: true
- argocd-cabundle-automount-jks: true
- argocd-cabundle-automount-pkcs12: true

### Experimental: Pod / Deployment labels
When it comes to determining where to mount a ca-bundle there is no single use case.  Base container images may use different locations, different programming languages may want certs in different locations, some containers come with certs which should not be replaced, othertimes they should be replaced.  If the automount options do not work instead there are some manual configuration options which may work.

However, though we work to make edge cases function, the best solution will be an upstream solution via kubernetes itsself.  In the meantime it might be easier to exec into your pods and determine where the certificates need to be mounted and mount the cabundle pvc yourself, or connect with connect with the application developers and ask them to provide 'global.ca-bundle' or something similar.

- argocd-cabundle-mount-os-redhat-cabundle: true
- argocd-cabundle-mount-os-redhat-jks: true
- argocd-cabundle-mount-os-redhat-pkcs12: true
- argocd-cabundle-mount-os-ubuntu-cabundle: true
- argocd-cabundle-mount-os-ubuntu-jks: true
- argocd-cabundle-mount-os-ubuntu-pkcs12: true
- argocd-cabundle-mount-lang-java-jks: true
- argocd-cabundle-mount-lang-python-cabundle: true
- additional ...
