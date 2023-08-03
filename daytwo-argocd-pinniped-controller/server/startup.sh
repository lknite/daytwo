#!/bin/bash

# install pinniped cli if not already installed
if [ ! -f /usr/local/bin/pinniped ]; then
  echo "- installing pinniped cli (first run only)"
  pushd /tmp > /dev/null
  curl -Lso pinniped https://get.pinniped.dev/$INSTALL_VERSION/pinniped-cli-linux-amd64 --insecure && \
    chmod +x pinniped && \
    mv pinniped /usr/local/bin/pinniped
  popd > /dev/null
fi

# Startup controller
echo "- starting up controller"
./entrypoint --urls "http://*:80"
