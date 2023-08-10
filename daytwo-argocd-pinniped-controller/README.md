# daytwo-argocd-pinniped-controller

## features ##
- watches for updates to argocd cluster secrets and updates a configmap with pinniped kubeconfig files
- hosts a website which serves the pinniped kubeconfig files
  - a website may then be added to each clusters in order to obtain the related pinniped kubeconfig file

## rbac ##
- role get/list/watch to 'secrets' in argocd namespace

## installation ##
- helm repo add lknite https://lknite.github.io/charts
- helm repo update lknite
- helm install pinniped-controller

## configuration environment variables ##
- PINNIPED_OIDC_ISSUER: https://keycloak.vc-prod.k.home.net/realms/home.net
- PINNIPED_OIDC_CLIENT_ID: kubernetes
- PINNIPED_OIDC_SCOPES: openid,email,profile,offline_access
- PINNIPED_CONCIERGE_AUTHENTICATOR_NAME: oidc-config
- PINNIPED_CONCIERGE_AUTHENTICATOR_TYPE: jwt
- PINNIPED_SKIP_VALIDATION: true

### required ###
- Select which version of pinniped to install:
  - name: INSTALL_VERSION
    value: "0.24.0"

### optional ###
- To specify an alternative location for argocd ('argocd' by default):
  - ARGOCD_NAMESPACE: argocd
- To disable the website hosting the kubeconfig files (enabled by default):
  - OPTION_DISABLE_HOSTING: true
  
## status ##
- working
- todo:
  - rename project folder from daytwo-argocd-pinniped-controller to argocd-pinniped-controller
  - code cleanup

## reference ##
- [kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
- [Cluster-API cluster auto-registration](https://github.com/argoproj/argo-cd/issues/9033)
