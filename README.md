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
  - watches for new clusters to reach a ready state and automatically adds them to argocd
  - syncs labels between capi provider resources and argocd cluster secrets
    - argocd applicationsets can then target labels, allowing addons to be managed via provider resources
    - the 'cluster.x-k8s.io/cluster-name' is included in the label copy allowing for targeting the cluster name
- [daytwo-argocd-pinniped-controller](https://github.com/lknite/daytwo/tree/main/daytwo-argocd-pinniped-controller)
  - watches for registered argocd clusters and updates pinniped kubeconfig files
  - hosts a website which can be used to access the pinniped kubeconfig files

## intended use
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
  - https://pinniped.<clustername>.<domain>

## development
| status  | controller                            | detail                                  |
|---------|---------------------------------------|-----------------------------------------|
| alpha   | [daytwo-argocd-register-controller](https://github.com/lknite/daytwo/tree/main/daytwo-argocd-register-controller)     | working, todo: improve logging, delete orphaned secrets |
| alpha   | [daytwo-argocd-pinniped-controller](https://github.com/lknite/daytwo/tree/main/daytwo-argocd-pinniped-controller)     | working, todo: add config env vars |
| active  | add helm charts                       |                                         |
| todo    | move builds to use github actions     |                                         |
| todo    | rewrite all controllers using go      |                                         |

## individual installation
- use helm chart to install respective controller
- by default daytwo controllers install to the argocd namespace
- use of an alternative namespace, e.g. daytwo, will work as long as two items are defined:
  - the argocd namespace must be specified
  - access must be granted to argocd secrets (argocd secrets are how argocd registers clusters)


## reference ##
[kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
