# daytwo-argocd-register-controller

- watches for cluster.yaml (tanzu, clusterapi, etc…) and registers clusters with argocd automatically once they are in ‘ready’ state
- syncs ‘addons’ labels from cluster.yaml to argocd cluster secrets to [auto install addons](https://www.youtube.com/watch?v=bNqljxnV7ZE), including pinniped-concierge, and [pinniped-www](https://www.travisloyd.xyz/2023/07/08/argocd-pinniped/)

## access ##
- install in argocd namespace, or configure access to Secrets in namespace where argocd is installed
- uses the argocd cluster secrets to communicate with clusterapi (or similar) server
  - this makes sense, because argocd is already setup in order to pass cluster.yaml to clusterapi (or similar)
  - also makes sense for needed access to add clusters to argocd, remove clusters from argocd, and update cluster secrets
- for each clusterapi (or similar) must be able to:
  - check status of a new cluster and know when it is ready to be added to argocd
  - obtain 'kubeconfig' from newly created cluster

## status ##
- currently in initial development

## work in progress ##
```
(cluster) Listen begins ...
(vcluster) Listen begins ...
(tanzukubernetescluster) Listen begins ...

(event) [Added] tanzukubernetesclusters.run.tanzu.vmware.com/v1alpha2: exp
Addition/Modify detected: exp
** argocd add cluster ...
- list clusters:
  - namespace: daytwo, tkc: exp
    - (cluster already added to argocd, is it up to date?)
. todo: if add, then add to argocd & add label indicating we added it
. todo: later, with a delete, only delete if we added the cluster ourselves
** add pinniped kubeconfig ...
** sync 'addons' ...
- add missing labels to argocd cluster secret:
  - addons-adcs-issuer-system: true
  - addons-cert-manager: true
- remove deleted labels from argocd cluster secret:

(event) [Deleted] tanzukubernetesclusters.run.tanzu.vmware.com/v1alpha2: k-demo
[k-demo]
Deleted detected: k-demo
** argocd remove cluster ...
** remove pinniped kubeconfig ...
done.
```

## reference ##
[kubernetes daytwo controllers](https://www.travisloyd.xyz/2023/07/08/kubernetes-daytwo-controllers/)
