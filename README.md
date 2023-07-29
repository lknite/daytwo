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
  - syncs labels between capi provider.yaml and argocd cluster secrets
    - argocd applicationsets can then target labels, allowing addons to be managed via provider.yaml
    - the 'cluster.x-k8s.io/cluster-name' is included in the label copy allowing for targeting the cluster name
- [daytwo-argocd-pinniped-controller](https://github.com/lknite/daytwo/tree/main/daytwo-argocd-pinniped-controller)
  - watches for registered argocd clusters and updates pinniped kubeconfig files
  - hosts a website which can be used to access the pinniped kubeconfig files

## development
| status  | controller                            | detail                                  |
|---------|---------------------------------------|-----------------------------------------|
| active  | daytwo-argocd-register-controller     | working, todo: improve logging, delete orphaned secrets |
| active  | daytwo-argocd-pinniped-controller     |                                         |
| todo    | add helm charts                       |                                         |
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
