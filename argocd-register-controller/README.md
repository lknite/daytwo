# argocd-register-controller

## features ##
- adds/updates argocd secrets automatically in response to new clusters provisioned via clusterapi
- able to watch multiple management clusters
  - mangement clusters must have different names
  - able to handle workload clusters having the same name on different management servers
- syncs all labels on provider resources with argocd secret
  - including 'cluster.x-k8s.io/cluster-name', which is nice, allows for applicationsets to target by cluster name

## rbac ##
- role add/get/list/update to 'secrets' in argocd namespace
  - used to communicate with management servers, which must already be added argocd
  - used to add/update/delete provisioned clusters
- clusterrole clusterapi secrets
  - used to access workload cluster kubeconfig via namespaced secrets managed by clusterapi

## installation ##
- helm repo add lknite https://lknite.github.io/charts
- helm repo update lknite
- helm install lknite/argocd-register-controller

## configuration environment variables ##
### required ###
- Comma separated list of management clusters to sync w/ argocd
  - MANAGEMENT_CLUSTERS: clusters
- Argocd auth token of account which will be used to add clusters to argocd
  - ARGOCD_AUTH_TOKEN: ...
    - recommend injecting this using something like [external secrets](https://external-secrets.io/) rather than add to your git repo

### optional ###
- To specify an alternative location for argocd ('argocd' by default):
  - ARGOCD_NAMESPACE: argocd
- To disable the label copy feature (enabled by default):
  - OPTION_DISABLE_LABEL_COPY: true

## auth token: how to generate? ##
Basic steps to generate token (your implementation may vary):
- kubectl -n argocd edit argocd-cm
- add 'data.accounts.admin: apiKey, login'
- argocd account generate-token --account admin
  
## status ##
- main functionality implemented
- todo:
  - code cleanup

## reference ##
- [kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
- [Cluster-API cluster auto-registration](https://github.com/argoproj/argo-cd/issues/9033)
