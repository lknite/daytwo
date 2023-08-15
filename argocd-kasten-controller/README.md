# argocd-kasten-controller

## features ##
- watches for updates to argocd cluster secrets and adds secondary clusters to kasten
- if primary is not added to kasten, will initially add the primary before looking for secondary clusters

## rbac ##
- role get/list/watch to 'secrets' in argocd namespace

## installation ##
- helm repo add lknite https://lknite.github.io/charts
- helm repo update lknite
- helm install argocd-kasten-controller

## configuration environment variables ##
- (see values.yaml)

### required ###
- Select which cluster k10multicluster will use as the primary:
  - name: PRIMARY_CLUSTER
    value: "root"
- Select which version of k10multicluster to install:
  - name: INSTALL_VERSION
    value: "6.0.5"

### optional ###
- To specify a label required on an argocd cluster secret in order to be processed:
  - REQUIRED_LABEL: "addons-kasten"
- To specify an alternative location for argocd ('argocd' by default):
  - ARGOCD_NAMESPACE: "argocd"
- Seconds between reconcilation checks ('60' by default):
  - LOOP_INTERVAL: "60"
  
## status ##
- working
- todo:
  - code cleanup

## reference ##
- [kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
- [Cluster-API cluster auto-registration](https://github.com/argoproj/argo-cd/issues/9033)
