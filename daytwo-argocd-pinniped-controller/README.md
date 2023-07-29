# daytwo-argocd-pinniped-controller

## features ##
- watches for updates to argocd cluster secrets and updates a configmap with pinniped kubeconfig files
- hosts a website which serves the pinniped kubeconfig files
  - a website may then be added to each clusters in order to obtain the related pinniped kubeconfig file

## rbac ##
- role get/list/watch to 'secrets' in argocd namespace
  - why

## installation ##
todo

## configuration environment variables ##
### required ###
- Comma separated list of management clusters to sync w/ argocd
  - NAME: value

### optional ###
  
## status ##
- initial development
- todo:

## reference ##
- [kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
- [Cluster-API cluster auto-registration](https://github.com/argoproj/argo-cd/issues/9033)
