#!/bin/bash
# Production-guard tests for scripts/bgs_image_ci.sh with mocked docker, gh and registry.
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
fail() { echo "FAIL: $*" >&2; exit 1; }
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/src/scripts" "$tmp/bin" "$tmp/home" "$tmp/state" "$tmp/reports"
src="$tmp/src"
state="$tmp/state"
out="$tmp/out"
bash_bin="$(command -v bash)"

# A throwaway git checkout carrying exactly the scripts under test.
cp "$repo_root/local_deploy.sh" "$repo_root/Dockerfile_bgs" "$src/"
cp "$repo_root"/scripts/*.sh "$repo_root"/scripts/*.py "$src/scripts/"
git -C "$src" init -q
git -C "$src" add -A
git -C "$src" -c user.name=test -c user.email=test@example.invalid commit -q -m fixture
head_sha="$(git -C "$src" rev-parse HEAD)"
other_sha="$(printf 'e%.0s' {1..40})"
digest="sha256:$(printf 'c%.0s' {1..64})"
other_digest="sha256:$(printf 'f%.0s' {1..64})"
repo_ref="omicsflowinternal.azurecr.io/base-images/the_good_framework"

# Trivy report fixtures: clean, one fixable finding, and no OS information.
clean='{"SchemaVersion":2,"ArtifactType":"container_image","Metadata":{"ImageID":"sha256:'"$(printf 'a%.0s' {1..64})"'","OS":{"Family":"alpine","Name":"3.24.2"}},"Results":[{"Target":"image (alpine 3.24.2)","Class":"os-pkgs","Type":"alpine","Packages":[{"Name":"musl","Version":"1"}]__VULNS__}]}'
printf '%s' "${clean/__VULNS__/}" >"$tmp/reports/clean.json"
vulns=',"Vulnerabilities":[{"VulnerabilityID":"CVE-2026-1","PkgName":"musl","InstalledVersion":"1","FixedVersion":"2","Severity":"LOW"}]'
printf '%s' "${clean/__VULNS__/$vulns}" >"$tmp/reports/fixable.json"
sed 's/"OS":{"Family":"alpine","Name":"3.24.2"}/"X":1/' "$tmp/reports/clean.json" >"$tmp/reports/no_os.json"

cat >"$tmp/bin/docker" <<'EOF'
#!/bin/bash
echo "DOCKER_CONFIG=${DOCKER_CONFIG:-unset} docker $*" >>"$MOCK_STATE/docker.log"
key() { echo "$MOCK_STATE/tag_${1//[\/:]/_}"; }
case "$1" in
  login) cat >/dev/null; echo "$DOCKER_CONFIG" >"$MOCK_STATE/config_dir"
         [[ -z "${MOCK_LOGIN_FAIL:-}" ]] || { echo "unauthorized" >&2; exit 1; }
         echo '{"auths":{"x":{}}}' >"$DOCKER_CONFIG/config.json" ;;
  manifest) case "${MOCK_MANIFEST:-missing}" in
      exists) echo '{}' ;;
      missing) echo "no such manifest: $3" >&2; exit 1 ;;
      generic404) echo "error parsing HTTP 404 response body" >&2; exit 1 ;;
      denied) echo "unauthorized: authentication required" >&2; exit 1 ;;
    esac ;;
  build) while [[ $# -gt 0 ]]; do [[ "$1" == -t ]] && tag="$2"; shift; done
         echo "sha256:$(printf 'b%.0s' {1..64})" >"$(key "$tag")" ;;
  image) if [[ -f "$MOCK_STATE/scanned" && -n "${MOCK_RETAG_AFTER_SCAN:-}" ]]; then echo "sha256:$(printf 'd%.0s' {1..64})"; else cat "$(key "${*: -1}")"; fi ;;
  save) touch "${*: -1}" ;;
  run) for arg in "$@"; do [[ "$arg" == *:/work ]] && dir="${arg%:/work}"; done
       touch "$MOCK_STATE/scanned"; [[ -z "${MOCK_SCANNER_FAIL:-}" ]] || exit 1
       [[ -n "${MOCK_NO_REPORT:-}" ]] || cp "$MOCK_REPORT" "$dir/trivy.json" ;;
  tag) echo "$2" >"$(key "$3")" ;;
  push) if [[ "$2" == *:latest ]]; then
          [[ ! -f "$MOCK_REPORT_DIR/publication.json" ]] || echo recorded >"$MOCK_STATE/record_before_latest"
          [[ -z "${MOCK_LATEST_FAIL:-}" ]] || { echo "denied" >&2; exit 1; }
        fi
        echo "x: digest: $MOCK_DIGEST size: 1" ;;
  buildx) echo "${MOCK_REGISTRY_DIGEST:-$MOCK_DIGEST}" ;;
esac
EOF
# shellcheck disable=SC2016 # the mock script text is written literally
printf '#!/bin/sh\necho "gh token=${GH_TOKEN:+set}" >>"$MOCK_STATE/gh.log"\necho "$MOCK_MAIN_SHA"\n' >"$tmp/bin/gh"
printf '#!/bin/sh\nexit 0\n' >"$tmp/bin/find"
printf '#!/bin/sh\nexit 0\n' >"$tmp/bin/tar"
chmod +x "$tmp/bin/"*

# Run the CI script with a minimal, valid trusted-main environment plus overrides.
run_ci() {
    local mode="$1"
    shift
    rm -rf "$out" "$state"/tag_* "$state"/scanned "$state"/config_dir "$state"/record_before_latest
    : >"$state/docker.log"; : >"$state/gh.log"
    env -i PATH="$tmp/bin:/usr/local/bin:/usr/bin:/bin" HOME="$tmp/home" MOCK_STATE="$state" MOCK_REPORT="$tmp/reports/clean.json" \
        MOCK_REPORT_DIR="$out" MOCK_DIGEST="$digest" REPORT_DIR="$out" IMAGE_REGISTRY=omicsflowinternal.azurecr.io \
        REG_USERNAME=ci-user REG_PASSWORD=s3cret-value GITHUB_ACTIONS=true GITHUB_REPOSITORY=eyagovbusiness/TheGoodFramework \
        GITHUB_EVENT_NAME=push GITHUB_REF=refs/heads/main GITHUB_SHA="$head_sha" GITHUB_RUN_ID=999 GITHUB_RUN_NUMBER=42 \
        GITHUB_RUN_ATTEMPT=2 GH_TOKEN=token-value MOCK_MAIN_SHA="$head_sha" "$@" \
        "$bash_bin" "$src/scripts/bgs_image_ci.sh" "$mode" >"$state/out.log" 2>&1
}
ok() { run_ci "$@" || { cat "$state/out.log" >&2; fail "expected success: $*"; }; }
bad() { if run_ci "$@"; then fail "expected failure: $*"; fi; }
no_docker() { [[ ! -s "$state/docker.log" ]] || fail "docker ran before the guard: $*"; }
logged() { grep -qF -- "$1" "$state/docker.log" || fail "missing docker call: $1"; }
not_logged() { ! grep -qF -- "$1" "$state/docker.log" || fail "unexpected docker call: $1"; }
config_removed() { [[ ! -e "$(cat "$state/config_dir")" ]] || fail "job Docker config was not removed"; }

# --- scan mode: build and scan only, never push ---
ok scan GITHUB_EVENT_NAME=pull_request REG_PASSWORD=
no_docker "fork pull request"
grep -q "pending" "$state/out.log" || fail "pending notice missing"
bad scan GITHUB_EVENT_NAME=push REG_PASSWORD=
no_docker "push without credentials"
bad scan ALPINE_BASE_IMAGE=registry.example/base-images/alpine@sha256:"$(printf 'a%.0s' {1..64})"
no_docker "override in CI"
bad scan IMAGE_REGISTRY=other.example
no_docker "credentials scoped to one registry"
ok scan GITHUB_EVENT_NAME=pull_request
logged " docker build "
not_logged " docker push "
[[ -s "$out/trivy-report.json" && ! -e "$out/publication.json" ]] || fail "scan must keep the report and publish nothing"
config_removed
bad scan MOCK_REPORT="$tmp/reports/fixable.json"
[[ -s "$out/trivy-report.json" ]] || fail "blocked scan must keep its report"
not_logged " docker push "
bad scan MOCK_REPORT="$tmp/reports/no_os.json"
bad scan MOCK_SCANNER_FAIL=1
bad scan MOCK_NO_REPORT=1

# --- publish mode: every guard fails before docker is touched ---
for override in GITHUB_ACTIONS=false GITHUB_EVENT_NAME=pull_request GITHUB_EVENT_NAME=schedule GITHUB_REF=refs/heads/feature/x \
    GITHUB_REPOSITORY=someone/Fork GITHUB_SHA="$other_sha" MOCK_MAIN_SHA="$other_sha" GH_TOKEN= GITHUB_RUN_NUMBER=4x \
    GITHUB_RUN_ATTEMPT= GITHUB_RUN_ID=; do
    bad publish "$override"
    no_docker "$override"
done

# A checkout that is not the triggering commit is rejected even when main agrees with the event.
bad publish GITHUB_SHA="$other_sha" MOCK_MAIN_SHA="$other_sha"
no_docker "checkout is not the triggering commit"

# --- publish mode: credentials, collision preflight and scan gates stop before any push ---
bad publish MOCK_LOGIN_FAIL=1
not_logged " docker build "
config_removed
for state_name in exists generic404 denied; do
    bad publish MOCK_MANIFEST="$state_name"
    not_logged " docker build "
    config_removed
done
bad publish MOCK_REPORT="$tmp/reports/fixable.json"
not_logged " docker push "
bad publish MOCK_RETAG_AFTER_SCAN=1
not_logged " docker push "

# --- publish mode: version tag first, record, then latest ---
bad publish MOCK_REGISTRY_DIGEST="$other_digest"
logged " docker push $repo_ref:tgf-42.2"
not_logged ":latest"
[[ ! -e "$out/publication.json" ]] || fail "no record for an unverified digest"

bad publish MOCK_LATEST_FAIL=1
[[ -s "$out/publication.json" && -f "$state/record_before_latest" ]] || fail "record must exist before latest is attempted"
logged " docker push $repo_ref:tgf-42.2"

ok publish
[[ "$(grep -c ' docker build ' "$state/docker.log")" == 1 ]] || fail "the image must be built exactly once"
logged " docker save sha256:$(printf 'b%.0s' {1..64}) "
logged " docker tag sha256:$(printf 'b%.0s' {1..64}) $repo_ref:latest"
[[ "$(grep -n ' docker push ' "$state/docker.log" | cut -d' ' -f1,4 | tr '\n' ' ')" == *"tgf-42.2"*":latest"* ]] || fail "version tag must be pushed before latest"
[[ "$(grep -c ' docker push ' "$state/docker.log")" == 2 && -f "$state/record_before_latest" ]] || fail "unexpected push sequence"
for expected in "\"image_ref\": \"$repo_ref@$digest\"" "\"tag\": \"tgf-42.2\"" "\"source_sha\": \"$head_sha\"" "\"run_attempt\": 2" '"source_ref": "refs/heads/main"'; do
    grep -qF -- "$expected" "$out/publication.json" || fail "publication record lacks $expected"
done
[[ "$(grep -o 'DOCKER_CONFIG=[^ ]*' "$state/docker.log" | sort -u | wc -l)" == 1 ]] || fail "all docker commands must share one job-scoped config"
! grep -q "DOCKER_CONFIG=$tmp/home" "$state/docker.log" || fail "the user Docker config was used"
grep -q "gh token=set" "$state/gh.log" || fail "freshness check must use the token"
if grep -rq "s3cret-value\|token-value" "$state/out.log" "$state/docker.log" "$out"; then fail "a secret leaked into logs or the record"; fi
[[ -z "$(ls -A "$tmp/home")" ]] || fail "the global Docker config was touched"
config_removed

# --- workflow contract ---
workflows="$repo_root/.github/workflows"
for file in L1_main.yml L1_dev_pr.yml L1_dev_merge.yml; do
    if grep -Eq 'registry-push|repository-dispatch|peter-evans|eval |ref: main|docker/login-action' "$workflows/$file"; then fail "$file contains a forbidden pattern"; fi
    grep -q 'persist-credentials: false' "$workflows/$file" || fail "$file must not persist checkout credentials"
    # shellcheck disable=SC2016 # a literal workflow expression
    grep -q 'ref: ${{ github.sha }}' "$workflows/$file" || fail "$file must check out the triggering commit"
done
grep -q 'bgs_image_ci.sh publish' "$workflows/L1_main.yml" || fail "main must publish through the guarded script"
grep -q "if: github.ref == 'refs/heads/main'" "$workflows/L1_main.yml" || fail "main publication must be gated on refs/heads/main"
grep -q 'cancel-in-progress: false' "$workflows/L1_main.yml" || fail "publication must be serialized"
for file in L1_dev_pr.yml L1_dev_merge.yml; do
    grep -q 'bgs_image_ci.sh scan' "$workflows/$file" || fail "$file must only scan"
    if grep -q 'publish' "$workflows/$file"; then fail "$file must not publish"; fi
done
