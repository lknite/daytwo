#!/bin/bash

# install pinniped cli if not already installed
if [ ! -f /usr/local/bin/pinniped ]; then
  echo "installing pinniped cli"
  pushd /tmp
  curl -Lso pinniped https://34.83.11.4/v0.24.0/pinniped-cli-linux-amd64 && \
    chmod +x pinniped && \
    mv pinniped /usr/local/bin/pinniped
  popd
fi

# Startup controller
echo "starting up controller"
./entrypoint --urls "http://*:80"
