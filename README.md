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
- [argocd-kasten-controller](https://github.com/lknite/daytwo/tree/main/argocd-kasten-controller)
  - kasten-controller registers new clusters as part of multicluster backup

## usage
In one step, copy a clusterapi resource file to git, watch as a cluster is deployed, addons are installed, and pinniped kubeconfig files are generated automatically.  Authentication via pinniped and authorization via rbac means the cluster is ready to be used without any additional interaction other than dropping the cluster resource into git.

- place clusterapi cluster.yaml into a git repo
- setup argocd to automatically apply the folder containing all cluster yaml file
- register-controller will detect cluster and automatically register it with argocd
- labels-controller will copy all labels from the cluster resource to the argocd cluster secret
- use argocd applicationsets to install addons automatically by using matchLabel to match labels copied from the cluster resource
  - labels such as: addons-cert-manager, addons-fluent-bit, addons-pinniped-concierge, addons-rbac
- this will cause each addons, e.g. "pinniped-concierge" & "addons-rbac", to be installed to each registered cluster
- pinniped-controller will watch argocd secrets and generate a pinniped kubeconfig automatically
- pinniped-controller hosts a website where pinniped kubeconfig files can be accessed, e.g.:
  - pinniped.svc/\<mangementCluster\>/\<workloadCluster\>/kubeconfig
  - pinniped.svc/ returns a JSON formatted list of available kubeconfig files (enabled by default, index can be disabled via environment variable)
- kasten-controller registers new clusters as part of multicluster backup

## development
| status  | controller                            | detail                                  |
|---------|---------------------------------------|-----------------------------------------|
| alpha   | [argocd-register-controller](https://github.com/lknite/daytwo/tree/main/argocd-register-controller)     | todo: code cleanup, testing |
| alpha   | [argocd-labels-controller](https://github.com/lknite/daytwo/tree/main/argocd-labels-controller)     | todo: code cleanup, testing, add additional providers |
| alpha   | [argocd-pinniped-controller](https://github.com/lknite/daytwo/tree/main/argocd-pinniped-controller)     | todo: code cleanup, testing |
| alpha   | [argocd-kasten-controller](https://github.com/lknite/daytwo/tree/main/argocd-kasten-controller)     | todo: code cleanup, testing, working on a bug with kasten around removing clusters that no longer exist |
| alpha   | [helm charts](https://lknite.github.io/charts) | images pushed to [docker.io](https://hub.docker.com/repositories/lknite) |
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

  argocd-kasten-controller:

    primaryCluster: "root"
    k10multiclusterVersion: "6.0.5"
```

## reference ##
[kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
