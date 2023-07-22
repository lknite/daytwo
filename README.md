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
- clusterapi
- tanzu

## controllers
- daytwo-argocd-register-controller
  - watches for new clusters to reach a ready state and automatically adds them to argocd
- (daytwo-argocd-addons-controller)[https://github.com/lknite/daytwo/tree/main/daytwo-argocd-addons-controller]
  - syncs 'addons-*' labels between cluster.yaml and argocd cluster secrets
  - argocd applications can then target labels, allowing addons to be managed via cluster.yaml
- daytwo-argocd-pinniped-controller
  - watches for registered argocd clusters and updates pinniped kubeconfig files
  - hosts a website which can be used to access the pinniped kubeconfig files
- daytwo-argocd-external-dns-controller
  - adds a label to the service which provides kubeapi access
  - allows for fqdn access to clusters
  - note: if certificates are not generated w/ fqdn access will be denied, use (-insecure) to get around
    - recommend regenerate certificates
    - research 'insecure-skip-tls-verify' in kubeconfig before deciding to use it
- daytwo-argocd-trigger-controller
  - calls a provided script allowing for daytwo actions to be performed elsewhere
  - use as needed for actions which require more customization (e.g. creating cluster-specific ad groups)

## operator-controller
- use to allow for a single helm chart deployment and values file

## individual installation
- use helm chart to install respective controller
- by default daytwo controllers install to the argocd namespace
- use of an alternative namespace, e.g. daytwo, will work as long as two items are defined:
  - the argocd namespace must be specified
  - access must be granted to argocd secrets (argocd secrets are how argocd registers clusters)


## reference ##
[kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
