#!/bin/bash
set -euo pipefail

validator="$(dirname "$0")/../scripts/validate_bgs_alpine_base_image.sh"
valid="registry.example:5000/base-images/alpine:3.21@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"

ALPINE_BASE_IMAGE="$valid" "$validator"

for invalid in "" \
    "registry.example/base-images/wrong@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" \
    "registry.example/base-images/alpine:latest@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" \
    "registry.example/base-images/alpine:3.21" \
    "registry.example/base-images/alpine@sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"; do
    if ALPINE_BASE_IMAGE="$invalid" "$validator" >/dev/null 2>&1; then
        echo "Expected invalid reference to fail: ${invalid:-<missing>}" >&2
        exit 1
    fi
done

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
mock_bin="$(mktemp -d)"
trap 'rm -rf "$mock_bin"' EXIT
cat >"$mock_bin/find" <<'EOF'
#!/bin/sh
exit 0
EOF
cat >"$mock_bin/tar" <<'EOF'
#!/bin/sh
exit 0
EOF
cat >"$mock_bin/docker" <<'EOF'
#!/bin/sh
printf '%s\n' "$*" >>"$DOCKER_LOG"
EOF
chmod +x "$mock_bin/find" "$mock_bin/tar" "$mock_bin/docker"

run_build() {
    DOCKER_LOG="$mock_bin/docker.log" PATH="$mock_bin:$PATH" IMAGE_REGISTRY="destination.example" ALPINE_BASE_IMAGE="$valid" \
        bash "$repo_root/local_deploy.sh" "$@" >/dev/null
}

run_build --bgs
grep -q -- '--build-arg ALPINE_BASE_IMAGE=registry.example:5000/base-images/alpine:3.21@sha256:' "$mock_bin/docker.log"
run_build --no-cache --bgs
grep -q -- '--no-cache' "$mock_bin/docker.log"
DOCKER_LOG="$mock_bin/docker.log" PATH="$mock_bin:$PATH" IMAGE_REGISTRY="destination.example" \
    bash "$repo_root/local_deploy.sh" --bgs >/dev/null 2>&1 && exit 1
DOCKER_LOG="$mock_bin/docker.log" PATH="$mock_bin:$PATH" IMAGE_REGISTRY="destination.example" ALPINE_BASE_IMAGE="registry.example/base-images/wrong@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" \
    bash "$repo_root/local_deploy.sh" --bgs >/dev/null 2>&1 && exit 1
DOCKER_LOG="$mock_bin/docker.log" PATH="$mock_bin:$PATH" IMAGE_REGISTRY="destination.example" ALPINE_BASE_IMAGE="registry.example/base-images/alpine:latest@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" \
    bash "$repo_root/local_deploy.sh" --bgs >/dev/null 2>&1 && exit 1
DOCKER_LOG="$mock_bin/docker.log" PATH="$mock_bin:$PATH" IMAGE_REGISTRY="destination.example" \
    bash "$repo_root/local_deploy.sh" >/dev/null
