FROM quay.io/centos/centos:stream8

# install pinniped
WORKDIR /tmp
#RUN curl -Lso pinniped https://get.pinniped.dev/v0.24.0/pinniped-cli-linux-amd64c
#RUN curl -Lso pinniped https://34.83.11.4/v0.24.0/pinniped-cli-linux-amd64
#RUN chmod +x pinniped
#RUN mv pinniped /usr/local/bin/pinniped

#RUN curl -Lso pinniped https://34.83.11.4/v0.24.0/pinniped-cli-linux-amd64 && \
#  chmod +x pinniped && \
#  mv pinniped /usr/local/bin/pinniped

# required to run dotnet compiled
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT 1

# can be added here as an env var or in code, required or will crash due to appconfig reloading
ENV DOTNET_hostBuilder:reloadConfigOnChange false

#
ENV TARGET_PATH /opt/daytwo-argocd-pinniped-controller
ENV TARGET_BIN daytwo-argocd-pinniped-controller

# copy over the deployment
COPY src/bin $TARGET_PATH/bin

# copy over startup script
COPY server /opt

WORKDIR $TARGET_PATH/bin/Debug/net7.0/linux-x64/publish
RUN chmod 755 $TARGET_BIN
RUN ln -s $TARGET_BIN -s entrypoint

ENTRYPOINT ["/opt/server/startup.sh"]
#ENTRYPOINT ["./entrypoint","--urls","http://*:80"]
#ENTRYPOINT ["tail", "-f", "/dev/null"]
