#!/bin/bash
# shellcheck disable=SC2154 # the digests are assigned from the generated artifact metadata
# Production-guard tests for scripts/bgs_image_ci.sh. Docker, gh and the registry are mocked, but the
# image archive, Trivy report and registry manifests follow the real formats and are verified for real.
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
fail() { echo "FAIL: $*" >&2; exit 1; }
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/src/scripts" "$tmp/bin" "$tmp/home" "$tmp/state" "$tmp/art"
src="$tmp/src"
state="$tmp/state"
art="$tmp/art"
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
repo_ref="omicsflowinternal.azurecr.io/base-images/the_good_framework"

# Artifacts in the real formats: a docker-save archive, Trivy reports for exactly that config, registry manifests.
python3 - "$art" "$repo_root/tests/fixtures/trivy-alpine-clean.json" >"$art/meta" <<'PY'
import hashlib, io, json, sys, tarfile
art, fixture = sys.argv[1:3]
sha = lambda b: "sha256:" + hashlib.sha256(b).hexdigest()
config = json.dumps({"architecture": "amd64", "rootfs": {"type": "layers", "diff_ids": [sha(b"layer")]}}).encode()
digest = sha(config)

def tar(name, entries, files):
    with tarfile.open(f"{art}/{name}", "w") as out:
        for member, data in {**files, "manifest.json": json.dumps(entries).encode()}.items():
            info = tarfile.TarInfo(member); info.size = len(data); out.addfile(info, io.BytesIO(data))

path = "blobs/sha256/" + digest[7:]
good = [{"Config": path, "Layers": ["blobs/sha256/" + "1" * 64]}]
tar("image.tar", good, {path: config})
tar("bad-hash.tar", [{"Config": "blobs/sha256/" + "2" * 64, "Layers": ["l"]}], {"blobs/sha256/" + "2" * 64: config})
tar("two-images.tar", good * 2, {path: config})

def report(name, image_id=digest, vulns=None, drop_os=False):
    data = json.load(open(fixture))
    data["Metadata"]["ImageID"] = image_id
    if drop_os:
        del data["Metadata"]["OS"]
    if vulns:
        data["Results"][0]["Vulnerabilities"] = vulns
    json.dump(data, open(f"{art}/{name}", "w"))

report("clean.json")
report("wrong-id.json", image_id="sha256:" + "a" * 64)
report("no-os.json", drop_os=True)
report("fixable.json", vulns=[{"VulnerabilityID": "CVE-2026-1", "PkgName": "musl", "InstalledVersion": "1", "FixedVersion": "2", "Severity": "LOW"}])

def manifest(name, config_digest=digest, **changes):
    data = {"schemaVersion": 2, "mediaType": "application/vnd.docker.distribution.manifest.v2+json",
            "config": {"mediaType": "application/vnd.docker.container.image.v1+json", "digest": config_digest, "size": len(config)},
            "layers": [{"mediaType": "application/vnd.docker.image.rootfs.diff.tar.gzip", "digest": sha(b"layer"), "size": 5}], **changes}
    raw = json.dumps(data, indent=3).encode()
    open(f"{art}/{name}", "wb").write(raw)
    return sha(raw)

print("config_digest=" + digest)
print("manifest_digest=" + manifest("remote-good.json"))
print("wrong_config_digest=" + manifest("remote-wrong-config.json", config_digest="sha256:" + "9" * 64))
print("index_digest=" + manifest("remote-index.json", mediaType="application/vnd.oci.image.index.v1+json", manifests=[{}]))
open(f"{art}/remote-garbage.json", "wb").write(b"<html>gateway</html>")
print("garbage_digest=" + sha(b"<html>gateway</html>"))
PY
while IFS='=' read -r key value; do declare "$key=$value"; done <"$art/meta"
local_image_id="sha256:$(printf 'b%.0s' {1..64})"
[[ "$config_digest" != "$local_image_id" ]] || fail "fixture must separate the local image id from the config digest"

cat >"$tmp/bin/docker" <<'EOF'
#!/bin/bash
# shellcheck disable=SC2154 # the digests are assigned from the generated artifact metadata
echo "DOCKER_CONFIG=${DOCKER_CONFIG:-unset} GHTOKEN=${GH_TOKEN:-none} docker $*" >>"$MOCK_STATE/docker.log"
key() { echo "$MOCK_STATE/tag_${1//[\/:]/_}"; }
pick() { # n-th word of a sequence (the last one repeats), counting calls per file
    local -a items=($1); local n=0; [[ -f "$2" ]] && n=$(<"$2"); echo $((n + 1)) >"$2"
    (( n < ${#items[@]} )) || n=$(( ${#items[@]} - 1 )); echo "${items[$n]}"
}
case "$1" in
  login) cat >/dev/null; echo "$DOCKER_CONFIG" >"$MOCK_STATE/config_dir"
         [[ -z "${MOCK_LOGIN_FAIL:-}" ]] || { echo "unauthorized" >&2; exit 1; }
         echo '{"auths":{"x":{}}}' >"$DOCKER_CONFIG/config.json" ;;
  manifest) case "$(pick "${MOCK_MANIFEST_SEQ:-missing}" "$MOCK_STATE/manifest.count")" in
      exists) echo '{}' ;;
      missing) echo "no such manifest: $3" >&2; exit 1 ;;
      generic404) echo "error parsing HTTP 404 response body" >&2; exit 1 ;;
      denied) echo "unauthorized: authentication required" >&2; exit 1 ;;
    esac ;;
  build) while [[ $# -gt 0 ]]; do [[ "$1" == -t ]] && tag="$2"; shift; done; echo "$MOCK_LOCAL_ID" >"$(key "$tag")" ;;
  image) if [[ -f "$MOCK_STATE/scanned" && -n "${MOCK_RETAG_AFTER_SCAN:-}" ]]; then echo "sha256:$(printf 'd%.0s' {1..64})"; else cat "$(key "${*: -1}")"; fi ;;
  save) cp "$MOCK_TAR" "${*: -1}" ;;
  run) for arg in "$@"; do [[ "$arg" == *:/work ]] && dir="${arg%:/work}"; done
       touch "$MOCK_STATE/scanned"; [[ -z "${MOCK_SCANNER_FAIL:-}" ]] || exit 1
       [[ -n "${MOCK_NO_REPORT:-}" ]] || cp "$MOCK_REPORT" "$dir/trivy.json" ;;
  tag) echo "$2" >"$(key "$3")" ;;
  push) if [[ "$2" == *:latest ]]; then
          [[ ! -f "$MOCK_REPORT_DIR/publication.json" ]] || echo recorded >"$MOCK_STATE/record_before_latest"
          [[ -z "${MOCK_LATEST_FAIL:-}" ]] || { echo "denied" >&2; exit 1; }
        fi
        echo "x: digest: $MOCK_DIGEST size: 1" ;;
  buildx) if [[ " $* " == *" --raw "* ]]; then
            [[ -z "${MOCK_RAW_FAIL:-}" ]] || { echo "not found" >&2; exit 1; }
            cat "$MOCK_RAW"
          elif [[ "$4" == *:latest ]]; then echo "${MOCK_LATEST_DIGEST:-${MOCK_REGISTRY_DIGEST:-$MOCK_DIGEST}}"
          else echo "${MOCK_REGISTRY_DIGEST:-$MOCK_DIGEST}"; fi ;;
esac
EOF
cat >"$tmp/bin/gh" <<'EOF'
#!/bin/bash
# shellcheck disable=SC2154 # the digests are assigned from the generated artifact metadata
echo "gh token=${GH_TOKEN:+set}" >>"$MOCK_STATE/gh.log"
n=0; [[ -f "$MOCK_STATE/gh.count" ]] && n=$(<"$MOCK_STATE/gh.count"); echo $((n + 1)) >"$MOCK_STATE/gh.count"
read -r -a items <<<"${MOCK_MAIN_SEQ:-head}"; (( n < ${#items[@]} )) || n=$(( ${#items[@]} - 1 ))
case "${items[$n]}" in head) echo "$MOCK_HEAD_SHA" ;; other) echo "$MOCK_OTHER_SHA" ;; junk) echo '{"object":"x"}' ;; *) exit 1 ;; esac
EOF
printf '#!/bin/sh\nexit 0\n' >"$tmp/bin/find"
printf '#!/bin/sh\nexit 0\n' >"$tmp/bin/tar"
chmod +x "$tmp/bin/"*

# Run the CI script with a minimal, valid trusted-main environment plus overrides.
run_ci() {
    local mode="$1"
    shift
    rm -rf "$out" "$state"/tag_* "$state"/*.count "$state"/scanned "$state"/config_dir "$state"/record_before_latest "$state"/summary
    : >"$state/docker.log"
    : >"$state/gh.log"
    env -i PATH="$tmp/bin:/usr/local/bin:/usr/bin:/bin" HOME="$tmp/home" MOCK_STATE="$state" MOCK_REPORT="$art/clean.json" \
        MOCK_TAR="$art/image.tar" MOCK_RAW="$art/remote-good.json" MOCK_LOCAL_ID="$local_image_id" MOCK_HEAD_SHA="$head_sha" \
        MOCK_OTHER_SHA="$other_sha" MOCK_REPORT_DIR="$out" MOCK_DIGEST="$manifest_digest" REPORT_DIR="$out" \
        GITHUB_STEP_SUMMARY="$state/summary" IMAGE_REGISTRY=omicsflowinternal.azurecr.io REG_USERNAME=ci-user REG_PASSWORD=s3cret-value \
        GITHUB_ACTIONS=true GITHUB_REPOSITORY=eyagovbusiness/TheGoodFramework GITHUB_EVENT_NAME=push GITHUB_REF=refs/heads/main \
        GITHUB_SHA="$head_sha" GITHUB_RUN_ID=999 GITHUB_RUN_NUMBER=42 GITHUB_RUN_ATTEMPT=2 GH_TOKEN=token-value "$@" \
        "$bash_bin" "$src/scripts/bgs_image_ci.sh" "$mode" >"$state/out.log" 2>&1
}
ok() { run_ci "$@" || { cat "$state/out.log" >&2; fail "expected success: $*"; }; }
bad() { if run_ci "$@"; then fail "expected failure: $*"; fi; }
no_docker() { [[ ! -s "$state/docker.log" ]] || fail "docker ran before the guard: $*"; }
logged() { grep -qF -- "$1" "$state/docker.log" || fail "missing docker call: $1"; }
not_logged() { ! grep -qF -- "$1" "$state/docker.log" || fail "unexpected docker call: $1"; }
config_removed() { [[ ! -e "$(cat "$state/config_dir")" ]] || fail "job Docker config was not removed"; }
no_push() { not_logged " docker push "; [[ ! -e "$out/publication.json" ]] || fail "a record exists without a push"; }
# The version tag is published and evidenced even though latest was not promoted.
evidence_kept() {
    logged " docker push $repo_ref:tgf-42.2"
    [[ -s "$out/publication.json" && "$(cat "$out/latest-status.txt")" == "latest: not-promoted" ]] || fail "partial evidence was not preserved"
}
# ... and latest was neither tagged nor pushed.
version_only() {
    evidence_kept
    not_logged " docker push $repo_ref:latest"
}

# --- scan mode: build and scan only, never push ---
for blocked in REG_PASSWORD= REG_USERNAME= IMAGE_REGISTRY=; do
    bad scan GITHUB_EVENT_NAME=pull_request "$blocked"
    no_docker "missing credentials $blocked"
    grep -q '::error::BLOCKED' "$state/out.log" || fail "a BLOCKED error annotation is required"
    grep -q 'BLOCKED' "$state/summary" || fail "a BLOCKED job summary is required"
done
bad publish REG_PASSWORD=
no_docker "publish without credentials"
bad scan ALPINE_BASE_IMAGE="omicsflowinternal.azurecr.io/base-images/alpine@sha256:$(printf 'a%.0s' {1..64})"
no_docker "override in CI"
grep -q 'must be unset' "$state/out.log" || fail "an override must be rejected for being an override, not for its registry"
bad scan IMAGE_REGISTRY=other.example
no_docker "credentials scoped to one registry"
ok scan GITHUB_EVENT_NAME=pull_request
logged " docker build "
logged "--provenance=false --sbom=false"
not_logged " docker push "
[[ -s "$out/trivy-report.json" && ! -e "$out/publication.json" ]] || fail "scan must keep the report and publish nothing"
config_removed
bad scan MOCK_REPORT="$art/fixable.json"
[[ -s "$out/trivy-report.json" ]] || fail "blocked scan must keep its report"
not_logged " docker push "
bad scan MOCK_REPORT="$art/no-os.json"
bad scan MOCK_REPORT="$art/wrong-id.json"
grep -q 'Metadata.ImageID' "$state/out.log" || fail "a report for another image must be rejected for its id"
bad scan MOCK_SCANNER_FAIL=1
bad scan MOCK_NO_REPORT=1

# --- the scan is bound to the archive config, never to the local Docker id ---
bad scan MOCK_TAR="$art/bad-hash.tar"
not_logged " docker run "
bad scan MOCK_TAR="$art/two-images.tar"
not_logged " docker run "
bad scan MOCK_REPORT="$art/clean.json" MOCK_LOCAL_ID="$config_digest" MOCK_TAR="$art/bad-hash.tar"

# --- publish mode: every guard fails before docker is touched ---
for override in GITHUB_ACTIONS=false GITHUB_EVENT_NAME=pull_request GITHUB_EVENT_NAME=schedule GITHUB_REF=refs/heads/feature/x \
    GITHUB_REPOSITORY=someone/Fork GITHUB_SHA="$other_sha" MOCK_MAIN_SEQ=other MOCK_MAIN_SEQ=ERROR MOCK_MAIN_SEQ=junk GH_TOKEN= \
    GITHUB_RUN_NUMBER=4x GITHUB_RUN_ATTEMPT= GITHUB_RUN_ID=; do
    bad publish "$override"
    no_docker "$override"
done
# A checkout that is not the triggering commit is rejected even when main agrees with the event.
bad publish GITHUB_SHA="$other_sha" MOCK_HEAD_SHA="$other_sha"
no_docker "checkout is not the triggering commit"

# --- publish mode: credentials and the collision preflight stop before the build ---
bad publish MOCK_LOGIN_FAIL=1
not_logged " docker build "
config_removed
for answer in exists generic404 denied; do
    bad publish MOCK_MANIFEST_SEQ="$answer"
    not_logged " docker build "
    config_removed
done

# --- publish mode: main and tag absence are re-checked immediately before each push ---
bad publish MOCK_MAIN_SEQ="head other"
no_push
bad publish MOCK_MAIN_SEQ="head ERROR"
no_push
bad publish MOCK_MAIN_SEQ="head junk"
no_push
bad publish MOCK_MANIFEST_SEQ="missing exists"
no_push
bad publish MOCK_MANIFEST_SEQ="missing denied"
no_push
bad publish MOCK_MANIFEST_SEQ="missing generic404"
no_push
bad publish MOCK_REPORT="$art/fixable.json"
no_push
bad publish MOCK_REPORT="$art/wrong-id.json"
no_push
bad publish MOCK_TAR="$art/bad-hash.tar"
no_push
bad publish MOCK_RETAG_AFTER_SCAN=1
no_push

# main moves, or cannot be read, after the version push: evidence stays, latest never moves.
bad publish MOCK_MAIN_SEQ="head head other"
version_only
bad publish MOCK_MAIN_SEQ="head head ERROR"
version_only

# --- the published version must be the scanned image, proven from the registry's own bytes ---
for failure in "MOCK_RAW=$art/remote-wrong-config.json MOCK_DIGEST=$wrong_config_digest" \
    "MOCK_RAW=$art/remote-index.json MOCK_DIGEST=$index_digest" \
    "MOCK_RAW=$art/remote-garbage.json MOCK_DIGEST=$garbage_digest" \
    "MOCK_RAW_FAIL=1" \
    "MOCK_DIGEST=$wrong_config_digest" \
    "MOCK_REGISTRY_DIGEST=sha256:$(printf 'f%.0s' {1..64})"; do
    # shellcheck disable=SC2086 # intentional word splitting of KEY=value pairs
    bad publish $failure
    logged " docker push $repo_ref:tgf-42.2"
    not_logged " docker push $repo_ref:latest"
    [[ ! -e "$out/publication.json" ]] || fail "no record for an unverified version: $failure"
done

# --- latest must resolve to the verified manifest; a failed latest keeps the version record ---
bad publish MOCK_LATEST_FAIL=1
evidence_kept
logged " docker tag $local_image_id $repo_ref:latest"
bad publish MOCK_LATEST_DIGEST="sha256:$(printf 'f%.0s' {1..64})"
logged " docker push $repo_ref:latest"
[[ "$(cat "$out/latest-status.txt")" == "latest: pushed-unverified" ]] || fail "an unverified latest must not be reported as promoted"

# --- the complete publication ---
ok publish
[[ "$(grep -c ' docker build ' "$state/docker.log")" == 1 ]] || fail "the image must be built exactly once"
logged " docker save $local_image_id "
logged " docker tag $local_image_id $repo_ref:latest"
[[ "$(grep -c ' docker push ' "$state/docker.log")" == 2 ]] || fail "unexpected push sequence"
[[ "$(grep -n 'docker push ' "$state/docker.log" | cut -d: -f1 | head -1)" -lt "$(grep -n ' docker tag ' "$state/docker.log" | cut -d: -f1 | head -1)" ]] || fail "version tag must be pushed before latest"
[[ -f "$state/record_before_latest" && "$(cat "$out/latest-status.txt")" == "latest: promoted" ]] || fail "the record must exist before latest and end promoted"
[[ "$(wc -l <"$state/gh.log")" == 3 ]] || fail "main must be checked at the start, before the version push and before latest"
for expected in "\"image_ref\": \"$repo_ref@$manifest_digest\"" "\"tag\": \"tgf-42.2\"" "\"manifest_digest\": \"$manifest_digest\"" \
    "\"config_digest\": \"$config_digest\"" "\"local_image_id\": \"$local_image_id\"" "\"source_sha\": \"$head_sha\"" "\"run_attempt\": 2" '"source_ref": "refs/heads/main"'; do
    grep -qF -- "$expected" "$out/publication.json" || fail "publication record lacks $expected"
done
python3 -c 'import json,sys; json.load(open(sys.argv[1]))' "$out/publication.json" || fail "the record must be valid JSON"
[[ "$(grep -o 'DOCKER_CONFIG=[^ ]*' "$state/docker.log" | sort -u | wc -l)" == 1 ]] || fail "all docker commands must share one job-scoped config"
! grep -q "DOCKER_CONFIG=$tmp/home" "$state/docker.log" || fail "the user Docker config was used"
! grep -q 'GHTOKEN=token' "$state/docker.log" || fail "the GitHub token leaked into the environment of docker, the build or the scanner"
grep -q "gh token=set" "$state/gh.log" || fail "the freshness checks must receive the token"
if grep -rq "s3cret-value\|token-value" "$state/out.log" "$state/docker.log" "$out"; then fail "a secret leaked into logs or the record"; fi
[[ -z "$(ls -A "$tmp/home")" ]] || fail "the global Docker config was touched"
config_removed

# --- workflow contract ---
workflows="$repo_root/.github/workflows"
if grep -rq 'pull_request_target' "$workflows"; then fail "pull_request_target would expose secrets to forks"; fi
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
