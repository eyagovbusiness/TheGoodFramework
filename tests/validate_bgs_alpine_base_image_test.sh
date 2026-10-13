#!/bin/bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
validator="$repo_root/scripts/validate_bgs_alpine_base_image.sh"
digest="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
valid="registry.example:5000/base-images/alpine:3.21@sha256:$digest"
committed_default="$(grep -E '^ARG ALPINE_BASE_IMAGE=' "$repo_root/Dockerfile_bgs" | tr -d '\r' | cut -d= -f2-)"

fail() { echo "$*" >&2; exit 1; }

# The committed default is the single authoritative pin and must be valid.
[[ "$(env -u ALPINE_BASE_IMAGE "$validator")" == "$committed_default" ]] || fail "unset env must resolve the committed default"
[[ "$committed_default" =~ ^omicsflowinternal\.azurecr\.io/base-images/alpine@sha256:[a-f0-9]{64}$ ]] || fail "committed default is not a canonical digest reference"
# shellcheck disable=SC2016 # the Dockerfile text is a literal, not a shell expansion
grep -qF 'FROM ${ALPINE_BASE_IMAGE} AS base' "$repo_root/Dockerfile_bgs" || fail "Dockerfile_bgs must build FROM the ARG"

# A valid supplied reference overrides the default and is echoed unchanged.
[[ "$(ALPINE_BASE_IMAGE="$valid" "$validator")" == "$valid" ]] || fail "valid override must be returned"

# Every invalid supplied value fails, including empty; it must not fall back.
for invalid in "" \
    "registry.example/base-images/wrong@sha256:$digest" \
    "registry.example/base-images/alpine:latest@sha256:$digest" \
    "registry.example/base-images/alpine:3.21" \
    "registry.example/base-images/alpine@sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"; do
    if ALPINE_BASE_IMAGE="$invalid" "$validator" >/dev/null 2>&1; then
        fail "Expected invalid reference to fail: ${invalid:-<empty>}"
    fi
done

# The committed default must itself be validated, with exactly one declaration.
fixture="$(mktemp -d)"
mock_bin="$(mktemp -d)"
trap 'rm -rf "$fixture" "$mock_bin"' EXIT
mkdir -p "$fixture/scripts"
cp "$validator" "$fixture/scripts/"
fixture_validator="$fixture/scripts/validate_bgs_alpine_base_image.sh"

# shellcheck disable=SC2016 # Dockerfile fixture text is literal
printf 'ARG ALPINE_BASE_IMAGE\nFROM ${ALPINE_BASE_IMAGE}\n' >"$fixture/Dockerfile_bgs"
if env -u ALPINE_BASE_IMAGE "$fixture_validator" >/dev/null 2>&1; then fail "missing default must fail"; fi
printf 'ARG ALPINE_BASE_IMAGE=registry.example/base-images/alpine:latest@sha256:%s\n' "$digest" >"$fixture/Dockerfile_bgs"
if env -u ALPINE_BASE_IMAGE "$fixture_validator" >/dev/null 2>&1; then fail "invalid default must fail"; fi
printf 'ARG ALPINE_BASE_IMAGE=%s\nARG ALPINE_BASE_IMAGE=%s\n' "$valid" "$valid" >"$fixture/Dockerfile_bgs"
if env -u ALPINE_BASE_IMAGE "$fixture_validator" >/dev/null 2>&1; then fail "duplicate defaults must fail"; fi
printf 'ARG ALPINE_BASE_IMAGE=%s\r\n' "$valid" >"$fixture/Dockerfile_bgs"
[[ "$(env -u ALPINE_BASE_IMAGE "$fixture_validator")" == "$valid" ]] || fail "CRLF default must resolve"

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

# Run a command with ALPINE_BASE_IMAGE unset ("@unset") or set to the given value.
with_ref() {
    local ref="$1"
    shift
    if [[ "$ref" == "@unset" ]]; then
        env -u ALPINE_BASE_IMAGE "$@"
    else
        env ALPINE_BASE_IMAGE="$ref" "$@"
    fi
}

# Run local_deploy.sh with a mocked toolchain.
deploy() {
    local ref="$1"
    shift
    : >"$mock_bin/docker.log"
    with_ref "$ref" env DOCKER_LOG="$mock_bin/docker.log" PATH="$mock_bin:$PATH" IMAGE_REGISTRY="destination.example" \
        bash "$repo_root/local_deploy.sh" "$@" >/dev/null 2>&1
}

# No env: the committed default is passed to docker.
deploy "@unset" --bgs
grep -qF -- "--build-arg ALPINE_BASE_IMAGE=$committed_default" "$mock_bin/docker.log" || fail "default ref was not passed to docker"
grep -qF -- "-f Dockerfile_bgs" "$mock_bin/docker.log" || fail "Dockerfile_bgs not used"
deploy "@unset" --no-cache --bgs
grep -q -- '--no-cache' "$mock_bin/docker.log" || fail "--no-cache not passed"

# Valid override wins over the committed default.
deploy "$valid" --bgs
grep -qF -- "--build-arg ALPINE_BASE_IMAGE=$valid" "$mock_bin/docker.log" || fail "override ref was not passed to docker"

# Invalid supplied refs, including empty, fail before docker runs.
for invalid in "" "registry.example/base-images/wrong@sha256:$digest" "registry.example/base-images/alpine:latest@sha256:$digest"; do
    if deploy "$invalid" --bgs; then fail "deploy must reject invalid ref: ${invalid:-<empty>}"; fi
    [[ ! -s "$mock_bin/docker.log" ]] || fail "docker ran despite invalid ref"
done

# Non-BGS builds do not use or require the base image variable.
deploy "invalid"
if grep -q 'ALPINE_BASE_IMAGE' "$mock_bin/docker.log"; then fail "non-BGS build must not pass the base image"; fi

# Make path: same validation, default and override behavior.
if command -v make >/dev/null 2>&1; then
    make_build() {
        local ref="$1"
        : >"$mock_bin/docker.log"
        (cd "$repo_root" && with_ref "$ref" env DOCKER_LOG="$mock_bin/docker.log" PATH="$mock_bin:$PATH" IMAGE_REGISTRY="destination.example" make -s local-bgs >/dev/null 2>&1)
    }
    make_build "@unset"
    grep -qF -- "--build-arg ALPINE_BASE_IMAGE=$committed_default" "$mock_bin/docker.log" || fail "make default ref was not passed"
    make_build "$valid"
    grep -qF -- "--build-arg ALPINE_BASE_IMAGE=$valid" "$mock_bin/docker.log" || fail "make override ref was not passed"
    if make_build ""; then fail "make must reject empty ref"; fi
    [[ ! -s "$mock_bin/docker.log" ]] || fail "docker ran from make despite invalid ref"
    rm -f "$repo_root/projectfiles.tar"
fi