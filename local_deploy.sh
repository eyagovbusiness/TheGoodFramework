#!/bin/bash
set -eux

cd "$(dirname "$0")"

ENVIRONMENT="${OMICSFLOW_ENVIRONMENT:-development}"
NO_CACHE=false
REGISTRY_PUSH=false
DOCKERFILE=()
BASE_IMAGE_ARG=()
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
    if [ "$REGISTRY_PUSH" = true ]; then
        echo "--registry-push is not supported with --bgs; canonical publication is done only by the approved main CI publisher." >&2
        exit 1
    fi
    # Validate first (a failing assignment aborts under set -e), then pass the resolved ref.
    base_image="$(./scripts/validate_bgs_alpine_base_image.sh)"
    BASE_IMAGE_ARG=(--build-arg ALPINE_BASE_IMAGE="$base_image")
fi

trap 'rm -f projectfiles.tar' EXIT
find . \( -name "*.csproj" -o -name "*.sln" -o -name "NuGet.docker.config" \) -print0 | tar -cvf projectfiles.tar --null -T -

IMAGE_TAG="${IMAGE_REGISTRY}/base-images/${ENVIRONMENT}/the_good_framework:${TAG}"
if [[ ${#DOCKERFILE[@]} -gt 0 ]]; then
    # The BGS image is built locally under its canonical name; only CI publishes it.
    IMAGE_TAG="${IMAGE_REGISTRY}/base-images/the_good_framework:${TAG}"
fi
if [ "$NO_CACHE" = true ]; then
    docker build "${DOCKERFILE[@]}" . --no-cache "${BASE_IMAGE_ARG[@]}" --build-arg IMAGE_REGISTRY="${IMAGE_REGISTRY}" --build-arg ENVIRONMENT="${ENVIRONMENT}" -t "${IMAGE_TAG}"
else
    docker build "${DOCKERFILE[@]}" . "${BASE_IMAGE_ARG[@]}" --build-arg IMAGE_REGISTRY="${IMAGE_REGISTRY}" --build-arg ENVIRONMENT="${ENVIRONMENT}" -t "${IMAGE_TAG}"
fi

if [ "$REGISTRY_PUSH" = true ]; then
    docker push "${IMAGE_TAG}"
fi
