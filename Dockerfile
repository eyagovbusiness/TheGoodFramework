ARG BUILD_CONFIGURATION=Release
ARG IMAGE_REGISTRY=registry.guildswarm.org
ARG ENVIRONMENT=Testportal

FROM $IMAGE_REGISTRY/base-images/alpine:latest AS base
# BUILD IMAGE
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
# Copy and extract project files and restore dependencies in a docker cache friendly way
COPY projectfiles.tar .
RUN tar -xvf projectfiles.tar && dotnet restore "TheGoodFramework.sln" -r linux-musl-x64 \
    && rm projectfiles.tar  # Remove the tar file to reduce image size
# Copy the rest of the source code
COPY . .
# Build and package each project as a NuGet package
ARG BUILD_CONFIGURATION
RUN dotnet build "TheGoodFramework.sln" -c $BUILD_CONFIGURATION --no-restore \
    && dotnet pack "TheGoodFramework.sln" -c $BUILD_CONFIGURATION --no-build -o /src/TGFPackages


# FINAL STAGE/IMAGE
FROM base AS final
WORKDIR /app/BasePackages
# Copy NuGet packages and other necessary files from the build stage
COPY --from=build /src/TGFPackages ./TGFPackages
COPY --from=build /root/.nuget/packages ./TGFRestored

USER root

# Add user and group only if they don't exist
RUN getent group $USER || addgroup -S $USER && \
    id -u $USER &>/dev/null || adduser -S $USER -G $USER

# Change ownership and set permissions
RUN chown -R $USER:$USER /app/ && \
    chmod -R 700 /app/

USER guildswarm
CMD ["/bin/sh"]
