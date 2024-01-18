# BASE IMAGE for runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine as base
EXPOSE 8080 8081

#FROM gswi-base:latest as base
#USER root
# Add some libs required by .NET runtime 
#RUN apk add --no-cache libstdc++ libintl icu
#USER guildswarm


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

