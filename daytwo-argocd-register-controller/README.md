# daytwo-argocd-register-controller

## features ##
- adds/updates argocd secrets automatically in response to new clusters provisioned via clusterapi
- event driven
- able to watch multiple management clusters
- syncs all labels on provider resources with argocd secret

## rbac ##
- role add/get/list/update to 'secrets' in argocd namespace
  - used to communicate with management servers, which must already be added argocd
  - used to add/update/delete provisioned clusters
- role exec to 'pods' in argocd namespace
  - used to run 'argocd cluster add' within the argocd server pod
    - by using this method we are guaranteed the correct argocd version is used
- clusterrole clusterapi secrets
  - used to access workload cluster kubeconfig via namespaced secrets managed by clusterapi

## installation ##
todo

## configuration environment variables ##
### required ###
- Comma separated list of management clusters to sync w/ argocd
  - MANAGEMENT_CLUSTERS: clusters
- Argocd auth token of account which will be used to add clusters to argocd
  - ARGOCD_AUTH_TOKEN: ...
    - you are probably going to inject this using something like [external secrets](https://external-secrets.io/) rather than add to your git repo

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
  - add cluster name conflict when working with multiple management clusters
  - add periodic check for orphaned argocd secrets and delete
  - cleanup/improve logging

## reference ##
- [kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
- [Cluster-API cluster auto-registration](https://github.com/argoproj/argo-cd/issues/9033)
