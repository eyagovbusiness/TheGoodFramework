#!/bin/bash
set -eux

DockerVersion=1.0.0

# tarball csproj files, sln files, and NuGet.config
find . \( -name "*.csproj" -o -name "*.sln" -o -name "NuGet.docker.config" \) -print0 \
    | tar -cvf projectfiles.tar --null -T -

docker build . -t registry.guildswarm.org/base-images/thegoodframework:$DockerVersion -t registry.guildswarm.org/base-images/thegoodframework:latest
if [[ $0 == 1 || 0 ]]
then
    rm projectfiles.tar
fi