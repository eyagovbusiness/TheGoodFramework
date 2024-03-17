ARG BUILD_CONFIGURATION=Release ENVIRONMENT=staging
FROM registry.guildswarm.org/baseimages/alpine_base:latest as base

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
RUN chown -R guildswarm:guildswarm /app/ && \
    chmod -R 700 /app/ 
USER guildswarm 
CMD ["/bin/sh"]