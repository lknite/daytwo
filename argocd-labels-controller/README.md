# argocd-labels-controller

## features ##
- watches for updates to provider yaml (clusterapi supports several providers)
- synchronizes labels on provider yaml with argocd cluster secrets

## rbac ##
- role get/list/update 'secrets' in argocd namespace

## installation ##
- helm repo add lknite https://lknite.github.io/charts
- helm repo update lknite
- helm install argocd-labels-controller

## configuration environment variables ##

### optional ###
- To specify an alternative location for argocd ('argocd' by default):
  - ARGOCD_NAMESPACE: argocd
  
## status ##
- working
- todo:
  - improve logging

## reference ##
- [kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
- [Cluster-API cluster auto-registration](https://github.com/argoproj/argo-cd/issues/9033)
