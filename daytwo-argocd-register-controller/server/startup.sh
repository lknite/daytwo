#!/bin/bash

# install argocd cli if not already installed
if [ ! -f /usr/local/bin/argocd ]; then
  echo "- installing argocd cli (first run only)"
  pushd /tmp > /dev/null

  curl -sSL -o argocd-linux-amd64 https://github.com/argoproj/argo-cd/releases/download/$INSTALL_VERSION/argocd-linux-amd64
  sudo install -m 555 argocd-linux-amd64 /usr/local/bin/argocd
  rm argocd-linux-amd64
  
  popd > /dev/null
  echo "- here is the downloaded argocd (if this fails then the download failed)"
  ls /usr/local/bin/argocd
fi

# Startup controller
echo "- starting up controller"
./entrypoint --urls "http://*:80"
