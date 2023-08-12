# daytwo
A collection of controllers which perform work in response to a new cluster deployment.

In the world of gitops it is preferred to define a kubernetes cluster (number of control plane nodes, number of
worker nodes, version) as yaml and add to a git repo.  Argocd then applies the cluster.yaml from the git repo to
the targed kubernetes cluster.  Clusterapi, tanzu, etc.. then detects the cluster yaml and either deploys a new
cluster or updates an existing cluster to match the cluster.yaml .

After this though, there is a gap.  The new cluster must be added to argocd among other tasks to make it useful.

To solve the gap, a common solution is to use a pipeline which commits the cluster.yaml, waits for it to become
ready then performs additional idempotent actions.  However, this method is not exactly gitops.  A better solution
would be a controller which recognizes a new cluster and then takes care of things automatically, that way the
environment always matches what is in git.

## compatible
- clusterapi (a.k.a. capi)
- tanzu (which uses capi)

## controllers
- [argocd-register-controller](https://github.com/lknite/daytwo/tree/main/argocd-register-controller)
  - watches for new clusters stood up via clusterapi and automatically syncs them with argocd (adds & removes)
- [argocd-labels-controller](https://github.com/lknite/daytwo/tree/main/argocd-labels-controller)
  - syncs labels between clusterapi provider resources and argocd cluster secrets
    - argocd applicationsets can then target labels, allowing addons to be managed via provider resources
    - adds an annotation 'daytwo.aarr.xyz/workload-cluster' which can be used to target an application to a cluster name
- [argocd-pinniped-controller](https://github.com/lknite/daytwo/tree/main/argocd-pinniped-controller)
  - watches for registered argocd clusters and updates pinniped kubeconfig files (adds & removes)
  - hosts a website which can be used to access the pinniped kubeconfig files

## usage
In one step, copying a clusterapi resource file to git, cause a cluster to be deployed, addons installed, enable authentication via pinniped, and authorization via rbac.

- place clusterapi cluster.yaml into a git repo
- use argocd to automatically apply the folder containing all cluster yaml file
- register-controller will detect cluster and automatically register it with argocd
- register-controller will also copy all labels from the cluster resource to the argocd cluster secret
- use argocd applicationsets to install addons automatically by using matchLabel to match labels copied from the cluster resource
  - labels such as: addons-cert-manager, addons-fluent-bit, addons-pinniped-concierge, addons-pinniped-www, addons-rbac
- this will cause "pinniped-concierge" & "pinniped-www" to be installed to each registered cluster
- pinniped-controller will watch argocd secrets and generate a pinniped kubeconfig automatically
- pinniped-controller also hosts a website to access the pinniped kubeconfig files it generates
  - pinniped.svc/\<mangementCluster\>/\<workloadCluster\>/kubeconfig
  - pinniped.svc/ returns a JSON formatted list of available kubeconfig files (enabled by default, index can be disabled via environment variable)
- pinniped-www deployed to each cluster allows a cluster-specific url to access the kubeconfig file hosted on the pinniped controller

## development
| status  | controller                            | detail                                  |
|---------|---------------------------------------|-----------------------------------------|
| alpha   | [argocd-register-controller](https://github.com/lknite/daytwo/tree/main/argocd-register-controller)     | todo: code cleanup, testing |
| alpha   | [argocd-labels-controller](https://github.com/lknite/daytwo/tree/main/argocd-labels-controller)     | todo: code cleanup, testing, add additional providers |
| alpha   | [argocd-pinniped-controller](https://github.com/lknite/daytwo/tree/main/argocd-pinniped-controller)     | todo: code cleanup, testing |
| alpha   | [helm charts](https://lknite.github.io/charts) | images pushed to [docker.io](https://hub.docker.com/repositories/lknite) |
| dev     | one install all helm chart | create single helm chart to install all |
| todo    | pinniped-www | |
| todo    | move to github actions     |                                         |
| todo    | rewrite using go      |                                         |

## getting started
- helm repo add lknite https://lknite.github.io/charts
- helm repo update lknite
- helm install lknite/daytwo

## example helm values file
```
daytwo:

  argocd-register-controller:

    managementClusters: "root"
    argocdServerUri: "argocd.root.k.home.net"
    argocdInsecureSkipTlsVerify: "true"

  argocd-labels-controller:

    managementClusters: "root"

  argocd-pinniped-controller:

    requiredLabel: "addons-pinniped-concierge"

    env:
    - name: PINNIPED_OIDC_ISSUER
      value: "https://keycloak.vc-prod.k.home.net/realms/home.net"
    - name: PINNIPED_OIDC_CLIENT_ID
      value: "kubernetes"
    - name: PINNIPED_OIDC_SCOPES
      value: "openid,email,profile,offline_access"
    - name: PINNIPED_CONCIERGE_AUTHENTICATOR_NAME
      value: "oidc-config"
    - name: PINNIPED_CONCIERGE_AUTHENTICATOR_TYPE
      value: "jwt"
    - name: PINNIPED_SKIP_VALIDATION
      value: "true"

    persistence:
      enabled: true
      storageClass: cephfs
      accessModes:
      - ReadWriteMany

    ingress:
      enabled: true
      ingressClassName: nginx
      hostname: pinniped.root.k.home.net
      tls: true
      annotations:
        cert-manager.io/issuer: "cluster-adcs-issuer" #use specific name of issuer
        cert-manager.io/issuer-kind: "ClusterAdcsIssuer" #or AdcsClusterIssuer
        cert-manager.io/issuer-group: "adcs.certmanager.csf.nokia.com"
```

## reference ##
[kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
