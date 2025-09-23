#!/bin/bash
set -eux

# Default values
ENVIRONMENT="${OMICSFLOW_ENVIRONMENT:-development}" 
NO_CACHE=false
REGISTRY_PUSH=false
DOCKERFILE=""
TAG="latest"

# Check if the first argument is a tag (doesn't start with --)
if [[ "$#" -gt 0 && ! "$1" =~ ^-- ]]; then
    TAG="$1"
    shift
fi

# Parse arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --no-cache) NO_CACHE=true ;;
        --registry-push) REGISTRY_PUSH=true ;;
		--bgs) 
			DOCKERFILE="-f Dockerfile_bgs" ;;
        --help) 
            echo "Usage: $0 [--no-cache] [--registry-push] [--bgs]"
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

IMAGE_TAG="${IMAGE_REGISTRY}/base-images/${ENVIRONMENT}/the_good_framework:${TAG}"

# Build the Docker image with or without cache
if [ "$NO_CACHE" = true ]; then
	docker build $DOCKERFILE . --no-cache --build-arg IMAGE_REGISTRY=${IMAGE_REGISTRY} --build-arg ENVIRONMENT=${ENVIRONMENT} -t ${IMAGE_TAG}
else
    docker build $DOCKERFILE . --build-arg IMAGE_REGISTRY=${IMAGE_REGISTRY} --build-arg ENVIRONMENT=${ENVIRONMENT} -t ${IMAGE_TAG}
fi

# If the --registry-push argument is provided, push the image
if [ "$REGISTRY_PUSH" = true ]; then
    docker push ${IMAGE_TAG}
fi
