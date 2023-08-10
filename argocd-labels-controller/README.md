# argocd-labels-controller

## features ##
- watches for updates to provider yaml (clusterapi supports several providers)
- synchronizes labels from provider resources to argocd cluster secret resources

## compatible ##
- vcluster
- tanzukubernetescluster
- ... will add additional upon request

## rbac ##
- role get/list/update 'secrets' in argocd namespace

## installation ##
- helm repo add lknite https://lknite.github.io/charts
- helm repo update lknite
- helm install argocd-labels-controller

## configuration environment variables ##

### required ###
- Comma separated list of management clusters to sync w/ argocd
  - MANAGEMENT_CLUSTERS: clusters

### optional ###
- To specify an alternative location for argocd ('argocd' by default):
  - ARGOCD_NAMESPACE: argocd
  
## status ##
- working
- todo:
  - code cleanup

## reference ##
- [kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
- [Cluster-API cluster auto-registration](https://github.com/argoproj/argo-cd/issues/9033)
