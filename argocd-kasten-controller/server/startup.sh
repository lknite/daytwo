#!/bin/bash

# install k10multicluster cli if not already installed
if [ ! -f /usr/local/bin/k10multicluster ]; then
  echo "- installing k10multicluster cli (first run only)"

  pushd /tmp > /dev/null
  curl -LOs https://github.com/kastenhq/external-tools/releases/download/${INSTALL_VERSION}/k10multicluster_${INSTALL_VERSION}_linux_amd64.tar.gz --insecure && \
	  tar zxvf k10multicluster_${INSTALL_VERSION}_linux_amd64.tar.gz > /dev/null && \
    chmod +x k10multicluster && \
    mv k10multicluster /usr/local/bin/k10multicluster
  popd > /dev/null

  echo "- install version: ${INSTALL_VERSION}"
  echo "- downloaded binary: `ls /usr/local/bin/k10multicluster`"
fi

# Startup controller
echo "- starting up controller"
./entrypoint --urls "http://*:80"
