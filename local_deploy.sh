#!/bin/bash
set -eux

cd "$(dirname "$0")"

ENVIRONMENT="${OMICSFLOW_ENVIRONMENT:-development}"
NO_CACHE=false
REGISTRY_PUSH=false
DOCKERFILE=()
TAG="latest"

if [[ "$#" -gt 0 && "$1" != --* ]]; then
    TAG="$1"
    shift
fi

while [[ "$#" -gt 0 ]]; do
    case $1 in
        --no-cache) NO_CACHE=true ;;
        --registry-push) REGISTRY_PUSH=true ;;
        --bgs) DOCKERFILE=(-f Dockerfile_bgs) ;;
        --help) echo "Usage: $0 [--no-cache] [--registry-push] [--bgs]"; exit 0 ;;
        *) ;;
    esac
    shift
done

if [[ -z "${IMAGE_REGISTRY:-}" ]]; then
    echo "Required environment variable IMAGE_REGISTRY is not set."
    exit 1
fi

if [[ "${DOCKERFILE[*]}" == "-f Dockerfile_bgs" ]]; then
    ./scripts/validate_bgs_alpine_base_image.sh
fi

trap 'rm -f projectfiles.tar' EXIT
find . \( -name "*.csproj" -o -name "*.sln" -o -name "NuGet.docker.config" \) -print0 | tar -cvf projectfiles.tar --null -T -

IMAGE_TAG="${IMAGE_REGISTRY}/base-images/${ENVIRONMENT}/the_good_framework:${TAG}"
if [ "$NO_CACHE" = true ]; then
    docker build "${DOCKERFILE[@]}" . --no-cache --build-arg ALPINE_BASE_IMAGE="${ALPINE_BASE_IMAGE:-}" --build-arg IMAGE_REGISTRY="${IMAGE_REGISTRY}" --build-arg ENVIRONMENT="${ENVIRONMENT}" -t "${IMAGE_TAG}"
else
    docker build "${DOCKERFILE[@]}" . --build-arg ALPINE_BASE_IMAGE="${ALPINE_BASE_IMAGE:-}" --build-arg IMAGE_REGISTRY="${IMAGE_REGISTRY}" --build-arg ENVIRONMENT="${ENVIRONMENT}" -t "${IMAGE_TAG}"
fi

if [ "$REGISTRY_PUSH" = true ]; then
    docker push "${IMAGE_TAG}"
fi
