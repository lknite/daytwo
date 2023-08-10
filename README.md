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
- [daytwo-argocd-register-controller](https://github.com/lknite/daytwo/tree/main/daytwo-argocd-register-controller)
  - watches for new clusters stood up via clusterapi and automatically syncs them with argocd (adds & removes)
- [argocd-labels-controller](https://github.com/lknite/daytwo/tree/main/argocd-labels-controller)
  - syncs labels between clusterapi provider resources and argocd cluster secrets
    - argocd applicationsets can then target labels, allowing addons to be managed via provider resources
    - adds an annotation 'daytwo.aarr.xyz/workload-cluster' which can be used to target an application to a cluster name
- [daytwo-argocd-pinniped-controller](https://github.com/lknite/daytwo/tree/main/daytwo-argocd-pinniped-controller)
  - watches for registered argocd clusters and updates pinniped kubeconfig files (adds & removes)
  - hosts a website which can be used to access the pinniped kubeconfig files

## intended use
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
- pinniped-www can be used by consumers to access the cluster-specific pinniped kubeconfig file e.g.
  - pinniped.\<clustername\>.\<domain\>

## development
| status  | controller                            | detail                                  |
|---------|---------------------------------------|-----------------------------------------|
| alpha   | [argocd-register-controller](https://github.com/lknite/daytwo/tree/main/daytwo-argocd-register-controller)     | todo: code cleanup, rename folder to without 'daytwo-' |
| alpha   | [argocd-labels-controller](https://github.com/lknite/daytwo/tree/main/argocd-labels-controller)     | todo: code cleanup |
| alpha   | [argocd-pinniped-controller](https://github.com/lknite/daytwo/tree/main/daytwo-argocd-pinniped-controller)     | todo: code cleanup, rename folder to without 'daytwo-' |
| alpha   | [helm charts](https://lknite.github.io/charts) | images pushed to docker.io, will be used when installing via helm chart |
| todo    | move builds to use github actions     |                                         |
| todo    | rewrite all controllers using go      |                                         |

## getting started
- helm repo add lknite https://lknite.github.io/charts
- helm repo update lknite
- helm install argocd-register-controller
- helm install argocd-labels-controller
- helm install argocd-pinniped-controller

## reference ##
[kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
