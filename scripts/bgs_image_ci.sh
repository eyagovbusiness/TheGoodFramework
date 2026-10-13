#!/bin/bash
# CI-only build, scan and canonical publication of the BGS image.
#   bgs_image_ci.sh scan     build locally and scan; never pushes
#   bgs_image_ci.sh publish  scan, then push tgf-<run>.<attempt> and latest (trusted main event only)
# Registry credentials live in a job-scoped Docker config that is removed on exit.
set -euo pipefail

mode="${1:-}"
[[ "$mode" == "scan" || "$mode" == "publish" ]] || { echo "Usage: $0 scan|publish" >&2; exit 2; }

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_repository="eyagovbusiness/TheGoodFramework"
trivy_image="ghcr.io/aquasecurity/trivy:0.75.0@sha256:af6acf9a6b85dfe389a1941505c0ce9efef52a4719635e1a962f022a3d855daa"
die() { echo "$*" >&2; exit 1; }
require() { [[ "${!1:-}" =~ $2 ]] || die "$1 is missing or malformed."; }

[[ -z "${ALPINE_BASE_IMAGE+set}" ]] || die "ALPINE_BASE_IMAGE must be unset in CI; the committed pin is authoritative."
require REPORT_DIR '^/.+'

# Fork pull requests receive no secrets and cannot pull the private base image.
if [[ "$mode" == "scan" && "${GITHUB_EVENT_NAME:-}" == "pull_request" && -z "${REG_PASSWORD:-}" ]]; then
    echo "::notice::Registry credentials are unavailable to this pull request; build and scan are pending until a trusted run."
    exit 0
fi
require IMAGE_REGISTRY '^[a-z0-9][a-z0-9.-]*(:[0-9]+)?$'
[[ -n "${REG_USERNAME:-}" && -n "${REG_PASSWORD:-}" ]] || die "Registry credentials are required."
alpine_ref="$("$repo_root/scripts/validate_bgs_alpine_base_image.sh")"
[[ "${alpine_ref%%/*}" == "$IMAGE_REGISTRY" ]] || die "The base image registry differs from IMAGE_REGISTRY; credentials are scoped to one registry."

if [[ "$mode" == "publish" ]]; then
    [[ "${GITHUB_ACTIONS:-}" == "true" && "${GITHUB_REPOSITORY:-}" == "$source_repository" ]] || die "Publication runs only in the source repository's GitHub Actions."
    [[ "${GITHUB_EVENT_NAME:-}" == "push" || "${GITHUB_EVENT_NAME:-}" == "workflow_dispatch" ]] || die "Publication requires a push or dispatch event."
    [[ "${GITHUB_REF:-}" == "refs/heads/main" ]] || die "Publication requires refs/heads/main."
    require GITHUB_SHA '^[a-f0-9]{40}$'
    require GITHUB_RUN_ID '^[0-9]+$'
    require GITHUB_RUN_NUMBER '^[0-9]+$'
    require GITHUB_RUN_ATTEMPT '^[0-9]+$'
    [[ "$(git -C "$repo_root" rev-parse HEAD)" == "$GITHUB_SHA" ]] || die "The checkout is not the triggering commit."
    # Only the current tip of main may publish; the token is used once and is not persisted.
    [[ -n "${GH_TOKEN:-}" ]] || die "GH_TOKEN is required to verify the current main."
    main_sha="$(gh api "repos/$GITHUB_REPOSITORY/git/ref/heads/main" --jq .object.sha)"
    unset GH_TOKEN
    [[ "$main_sha" == "$GITHUB_SHA" ]] || die "main has moved past $GITHUB_SHA; a newer run owns publication."
    tag="tgf-${GITHUB_RUN_NUMBER}.${GITHUB_RUN_ATTEMPT}"
else
    tag="tgf-scan"
fi
repository="$IMAGE_REGISTRY/base-images/the_good_framework"

DOCKER_CONFIG="$(mktemp -d)"
export DOCKER_CONFIG
work="$(mktemp -d)"
cleanup() { docker logout "$IMAGE_REGISTRY" >/dev/null 2>&1 || true; rm -rf "$DOCKER_CONFIG" "$work"; }
trap cleanup EXIT
printf '%s' "$REG_PASSWORD" | docker login "$IMAGE_REGISTRY" -u "$REG_USERNAME" --password-stdin >/dev/null
unset REG_PASSWORD

if [[ "$mode" == "publish" ]]; then
    # Fail closed: only a definite "no such manifest" answer proves the tag is free.
    if existing="$(docker manifest inspect "$repository:$tag" 2>&1)"; then die "$repository:$tag already exists."; fi
    [[ "$existing" == "no such manifest: $repository:$tag" ]] || die "Tag preflight failed closed: ${existing%%$'\n'*}"
fi

(cd "$repo_root" && IMAGE_REGISTRY="$IMAGE_REGISTRY" bash ./local_deploy.sh "$tag" --no-cache --bgs)
image_id="$(docker image inspect --format '{{.Id}}' "$repository:$tag")"
[[ "$image_id" =~ ^sha256:[a-f0-9]{64}$ ]] || die "Unexpected image id."

# Scan exactly the built image, by id, with a pinned public scanner that holds no credentials.
docker save "$image_id" -o "$work/image.tar"
docker run --rm -v "$work:/work" "$trivy_image" image --input /work/image.tar --scanners vuln --list-all-pkgs \
    --format json --output /work/trivy.json --quiet --skip-version-check --exit-code 0
mkdir -p "$REPORT_DIR"
cp "$work/trivy.json" "$REPORT_DIR/trivy-report.json"
python3 "$repo_root/scripts/bgs_scan_policy.py" "$work/trivy.json"
[[ "$mode" == "publish" ]] || exit 0

# Publish the scanned image without rebuilding; the tag must still be the scanned id.
[[ "$(docker image inspect --format '{{.Id}}' "$repository:$tag")" == "$image_id" ]] || die "$tag no longer points at the scanned image."
push_digest() { docker push "$1" | sed -n 's/.*digest: \(sha256:[a-f0-9]\{64\}\).*/\1/p' | tail -n 1; }
digest="$(push_digest "$repository:$tag")"
[[ "$digest" =~ ^sha256:[a-f0-9]{64}$ ]] || die "The push did not report a digest."
[[ "$(docker buildx imagetools inspect "$repository:$tag" --format '{{.Manifest.Digest}}')" == "$digest" ]] || die "The registry digest differs from the pushed digest."

# Record the immutable version before moving the floating tag; a failed latest keeps this record.
cat >"$REPORT_DIR/publication.json" <<EOF
{
  "schema_version": 1,
  "image_ref": "$repository@$digest",
  "tagged_ref": "$repository:$tag",
  "tag": "$tag",
  "digest": "$digest",
  "image_id": "$image_id",
  "source_repository": "$GITHUB_REPOSITORY",
  "source_ref": "$GITHUB_REF",
  "source_sha": "$GITHUB_SHA",
  "run_id": $GITHUB_RUN_ID,
  "run_number": $GITHUB_RUN_NUMBER,
  "run_attempt": $GITHUB_RUN_ATTEMPT
}
EOF
docker tag "$image_id" "$repository:latest"
[[ "$(push_digest "$repository:latest")" == "$digest" ]] || die "latest did not resolve to $digest; the version tag $tag remains published."
echo "Published $repository@$digest as $tag and latest."
