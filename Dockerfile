FROM registry.guildswarm.org/base-images/alpine-base:latest as base

ARG ASPNET_VERSION=8.0.1

USER root
# Install ASP.NET Core
RUN wget -O aspnetcore.tar.gz https://dotnetcli.azureedge.net/dotnet/aspnetcore/Runtime/$ASPNET_VERSION/aspnetcore-runtime-$ASPNET_VERSION-linux-musl-x64.tar.gz \
    && aspnetcore_sha512='b749398f5ad059c9d51e3153c9f41ac23145aea38e83a736259c4206fdb920c245685a60a6d4bcf74ce41c70f751fd133219fb66b263018ae53025e129535063' \
    && echo "$aspnetcore_sha512  aspnetcore.tar.gz" | sha512sum -c - \
    && tar -oxzf aspnetcore.tar.gz -C /usr/share/dotnet ./shared/Microsoft.AspNetCore.App \
    && rm aspnetcore.tar.gz

# BUILD IMAGE
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

## Copy and extract project files and restore dependencies in a docker cache friendly way
COPY projectfiles.tar .
RUN tar -xvf projectfiles.tar && dotnet restore \
    && rm projectfiles.tar  # Remove the tar file to reduce image size

## Copy the rest of the source code
COPY . .

## Build and package each project as a NuGet package
ARG BUILD_CONFIGURATION=Release
RUN dotnet build "TheGoodFramework.sln" -c $BUILD_CONFIGURATION --no-restore \
    && dotnet pack "TheGoodFramework.sln" -c $BUILD_CONFIGURATION --no-build -o /src/TGFPackages 

# FINAL STAGE/IMAGE
FROM base AS final
WORKDIR /app/BasePackages
## Copy NuGet packages and other necessary files from the build stage
COPY --from=build /src/TGFPackages ./TGF
USER guildswarm
CMD ["/bin/sh"]
