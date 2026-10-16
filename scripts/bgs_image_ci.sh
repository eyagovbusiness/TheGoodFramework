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
identity="$repo_root/scripts/bgs_image_identity.py"
die() { echo "$*" >&2; exit 1; }
require() { [[ "${!1:-}" =~ $2 ]] || die "$1 is missing or malformed."; }

[[ -z "${ALPINE_BASE_IMAGE+set}" ]] || die "ALPINE_BASE_IMAGE must be unset in CI; the committed pin is authoritative."
require REPORT_DIR '^/.+'

# The token is kept in an unexported variable and handed only to the gh call below,
# so docker, the build and the scanner never see it in their environment.
gh_token="${GH_TOKEN:-}"
unset GH_TOKEN

# Without credentials nothing is built or scanned, so this is a failure and never a green pass.
if [[ -z "${REG_PASSWORD:-}" || -z "${REG_USERNAME:-}" || -z "${IMAGE_REGISTRY:-}" ]]; then
    message="BLOCKED: registry credentials are unavailable (for example a fork pull request); the image was NOT built or scanned."
    echo "::error::$message"
    [[ -z "${GITHUB_STEP_SUMMARY:-}" ]] || echo "### $message" >>"$GITHUB_STEP_SUMMARY"
    exit 1
fi
require IMAGE_REGISTRY '^[a-z0-9][a-z0-9.-]*(:[0-9]+)?$'
alpine_ref="$("$repo_root/scripts/validate_bgs_alpine_base_image.sh")"
[[ "${alpine_ref%%/*}" == "$IMAGE_REGISTRY" ]] || die "The base image registry differs from IMAGE_REGISTRY; credentials are scoped to one registry."

# True only while this commit is still the tip of main; any lookup problem counts as false.
main_is_current() {
    local tip
    [[ -n "$gh_token" ]] || return 1
    tip="$(GH_TOKEN="$gh_token" gh api "repos/$source_repository/git/ref/heads/main" --jq .object.sha)" || return 1
    [[ "$tip" =~ ^[a-f0-9]{40}$ && "$tip" == "$GITHUB_SHA" ]]
}
# True only on a definite "no such manifest" answer; success or any other error is false.
tag_is_free() {
    local existing
    if existing="$(docker manifest inspect "$1" 2>&1)"; then echo "$1 already exists." >&2; return 1; fi
    [[ "$existing" == "no such manifest: $1" ]] || { echo "Tag check failed closed: ${existing%%$'\n'*}" >&2; return 1; }
}

if [[ "$mode" == "publish" ]]; then
    [[ "${GITHUB_ACTIONS:-}" == "true" && "${GITHUB_REPOSITORY:-}" == "$source_repository" ]] || die "Publication runs only in the source repository's GitHub Actions."
    [[ "${GITHUB_EVENT_NAME:-}" == "push" || "${GITHUB_EVENT_NAME:-}" == "workflow_dispatch" ]] || die "Publication requires a push or dispatch event."
    [[ "${GITHUB_REF:-}" == "refs/heads/main" ]] || die "Publication requires refs/heads/main."
    require GITHUB_SHA '^[a-f0-9]{40}$'
    require GITHUB_RUN_ID '^[0-9]+$'
    require GITHUB_RUN_NUMBER '^[0-9]+$'
    require GITHUB_RUN_ATTEMPT '^[0-9]+$'
    [[ "$(git -C "$repo_root" rev-parse HEAD)" == "$GITHUB_SHA" ]] || die "The checkout is not the triggering commit."
    main_is_current || die "main has moved past $GITHUB_SHA (or could not be read); a newer run owns publication."
    tag="tgf-${GITHUB_RUN_NUMBER}.${GITHUB_RUN_ATTEMPT}"
else
    tag="tgf-scan"
fi
repository="$IMAGE_REGISTRY/base-images/the_good_framework"

DOCKER_CONFIG="$(mktemp -d)"
export DOCKER_CONFIG
work="$(mktemp -d)"
latest_state="not-promoted"
cleanup() {
    [[ ! -e "$REPORT_DIR/publication.json" ]] || echo "latest: $latest_state" >"$REPORT_DIR/latest-status.txt"
    docker logout "$IMAGE_REGISTRY" >/dev/null 2>&1 || true
    rm -rf "$DOCKER_CONFIG" "$work"
}
trap cleanup EXIT
mkdir -p "$REPORT_DIR"
printf '%s' "$REG_PASSWORD" | docker login "$IMAGE_REGISTRY" -u "$REG_USERNAME" --password-stdin >/dev/null
unset REG_PASSWORD

[[ "$mode" == "scan" ]] || tag_is_free "$repository:$tag" || exit 1

(cd "$repo_root" && IMAGE_REGISTRY="$IMAGE_REGISTRY" bash ./local_deploy.sh "$tag" --no-cache --bgs)
# The local Docker id is only a handle: under containerd it is a manifest digest, not the config digest.
image_id="$(docker image inspect --format '{{.Id}}' "$repository:$tag")"
[[ "$image_id" =~ ^sha256:[a-f0-9]{64}$ ]] || die "Unexpected image id."

# Scan exactly the built image, by id, with a pinned public scanner that holds no credentials.
# The report must name the config digest hashed from the archive itself.
docker save "$image_id" -o "$work/image.tar"
config_digest="$(python3 "$identity" archive "$work/image.tar")"
docker run --rm -v "$work:/work" "$trivy_image" image --input /work/image.tar --scanners vuln --list-all-pkgs \
    --format json --output /work/trivy.json --quiet --skip-version-check --exit-code 0
cp "$work/trivy.json" "$REPORT_DIR/trivy-report.json"
python3 "$repo_root/scripts/bgs_scan_policy.py" "$work/trivy.json" --image-id "$config_digest"
[[ "$mode" == "publish" ]] || exit 0

# Publish the scanned image without rebuilding. Everything is re-checked right before each push.
local_tag_is_scanned() { [[ "$(docker image inspect --format '{{.Id}}' "$repository:$tag")" == "$image_id" ]]; }
push_digest() { docker push "$1" | sed -n 's/.*digest: \(sha256:[a-f0-9]\{64\}\).*/\1/p' | tail -n 1; }
# The reference must resolve to the verified manifest, whose bytes hash to its digest and name the scanned config.
verify_remote() {
    local raw="$work/remote-manifest.json"
    [[ "$(docker buildx imagetools inspect "$1" --format '{{.Manifest.Digest}}')" == "$digest" ]] || return 1
    docker buildx imagetools inspect "$repository@$digest" --raw >"$raw" || return 1
    python3 "$identity" manifest "$raw" "$digest" "$config_digest"
}

local_tag_is_scanned || die "$tag no longer points at the scanned image."
main_is_current || die "main moved during the scan; nothing was pushed."
tag_is_free "$repository:$tag" || die "$repository:$tag appeared after the preflight; nothing was pushed."
digest="$(push_digest "$repository:$tag")"
[[ "$digest" =~ ^sha256:[a-f0-9]{64}$ ]] || die "The push did not report a digest."
verify_remote "$repository:$tag" || die "The registry does not hold the scanned image for $tag; latest was not moved."

# Record the immutable version, with its separate identities, before moving the floating tag.
cat >"$REPORT_DIR/publication.json" <<EOF
{
  "schema_version": 1,
  "image_ref": "$repository@$digest",
  "tagged_ref": "$repository:$tag",
  "tag": "$tag",
  "manifest_digest": "$digest",
  "config_digest": "$config_digest",
  "local_image_id": "$image_id",
  "source_repository": "$GITHUB_REPOSITORY",
  "source_ref": "$GITHUB_REF",
  "source_sha": "$GITHUB_SHA",
  "run_id": $GITHUB_RUN_ID,
  "run_number": $GITHUB_RUN_NUMBER,
  "run_attempt": $GITHUB_RUN_ATTEMPT
}
EOF
main_is_current || die "main moved after the version push; $tag stays published and latest was not moved."
verify_remote "$repository:$tag" || die "The version tag changed after the push; latest was not moved."
docker tag "$image_id" "$repository:latest"
[[ "$(push_digest "$repository:latest")" == "$digest" ]] || die "latest did not resolve to $digest; the version tag $tag remains published."
latest_state="pushed-unverified"
verify_remote "$repository:latest" || die "latest does not resolve to the verified manifest for $tag."
latest_state="promoted"
echo "Published $repository@$digest as $tag and latest."
