FROM quay.io/centos/centos:stream8

# required to run dotnet compiled
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT 1

# can be added here as an env var or in code, required or will crash due to appconfig reloading
ENV DOTNET_hostBuilder:reloadConfigOnChange false

#
ENV TARGET_PATH /opt/daytwo-argocd-register-controller
ENV TARGET_BIN daytwo-argocd-register-controller

# copy over the deployment
COPY src/bin $TARGET_PATH/bin

WORKDIR $TARGET_PATH/bin/Debug/net7.0/linux-x64/publish
RUN chmod 755 $TARGET_BIN
RUN ln -s $TARGET_BIN -s entrypoint

ENTRYPOINT ["./entrypoint","--urls","http://*:80"]
#ENTRYPOINT ["tail", "-f", "/dev/null"]
