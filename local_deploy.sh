#!/bin/bash
set -eux

Environment=development

# Default values
NO_CACHE=false
REGISTRY_PUSH=false
DOCKERFILE=""

# Parse arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --no-cache) NO_CACHE=true ;;
        --registry-push) REGISTRY_PUSH=true ;;
		--bgs) 
			DOCKERFILE="-f Dockerfile_bgs" ;;
        --help) 
            echo "Usage: $0 [--no-cache] [--registry-push]"
            exit 0
            ;;
        *) 
            # Ignore unknown options
            ;;
    esac
    shift
done


# Check if required environment variables are set
if [[ -z "${IMAGE_REGISTRY:-}" ]]; then
    echo "Required environment variable IMAGE_REGISTRY is not set."
    exit 1
fi

# Ensure the tarball is always removed, even if the script fails
trap 'rm -f projectfiles.tar' EXIT

# tarball csproj files, sln files, and NuGet.config
find . \( -name "*.csproj" -o -name "*.sln" -o -name "NuGet.docker.config" \) -print0 \
    | tar -cvf projectfiles.tar --null -T -

# Build the Docker image with or without cache
if [ "$NO_CACHE" = true ]; then
	docker build $DOCKERFILE . --no-cache --build-arg IMAGE_REGISTRY=${IMAGE_REGISTRY} --build-arg ENVIRONMENT=${Environment} -t ${IMAGE_REGISTRY}/base-images/${Environment}/the_good_framework:latest
else
    docker build $DOCKERFILE . --build-arg IMAGE_REGISTRY=${IMAGE_REGISTRY} --build-arg ENVIRONMENT=${Environment} -t ${IMAGE_REGISTRY}/base-images/${Environment}/the_good_framework:latest
fi

# If the --registry-push argument is provided, push the image
if [ "$REGISTRY_PUSH" = true ]; then
    docker push ${IMAGE_REGISTRY}/base-images/${Environment}/the_good_framework:latest
fi