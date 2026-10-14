#!/bin/bash
# Tests for scripts/bgs_notify_common.sh. gh is mocked: it records its arguments, its stdin and whether it
# received the token. Every unauthentic or partial publication must end without a single gh call.
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
fail() { echo "FAIL: $*" >&2; exit 1; }
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/bin" "$tmp/state" "$tmp/report"
state="$tmp/state"
report="$tmp/report"
bash_bin="$(command -v bash)"
registry="omicsflowinternal.azurecr.io"
canonical="$registry/base-images/the_good_framework"
sha="$(printf 'a%.0s' {1..40})"
manifest="sha256:$(printf '1%.0s' {1..64})"

cat >"$tmp/bin/gh" <<'EOF'
#!/bin/bash
echo "$*" >"$MOCK_STATE/gh.args"
cat >"$MOCK_STATE/gh.stdin"
echo "token=${GH_TOKEN:-none}" >"$MOCK_STATE/gh.token"
echo called >>"$MOCK_STATE/gh.calls"
[[ -z "${MOCK_GH_FAIL:-}" ]] || { echo "HTTP 403: Resource not accessible by personal access token" >&2; exit 1; }
EOF
chmod +x "$tmp/bin/gh"
# python3 only reports whether the token is in its environment, then runs the real interpreter.
real_python="$(command -v python3)"
cat >"$tmp/bin/python3" <<EOF
#!/bin/bash
echo "token=\${GH_TOKEN:-none}" >>"\$MOCK_STATE/python.token"
exec "$real_python" "\$@"
EOF
chmod +x "$tmp/bin/python3"

run_notify() {
    rm -f "$state"/gh.* "$state/python.token"
    env -i PATH="$tmp/bin:/usr/local/bin:/usr/bin:/bin" MOCK_STATE="$state" REPORT_DIR="$report" IMAGE_REGISTRY="$registry" \
        GITHUB_ACTIONS=true GITHUB_REPOSITORY=eyagovbusiness/TheGoodFramework GITHUB_EVENT_NAME=push GITHUB_REF=refs/heads/main \
        GITHUB_SHA="$sha" GITHUB_RUN_ID=999 GITHUB_RUN_NUMBER=42 GITHUB_RUN_ATTEMPT=2 GH_TOKEN=pat-token-value "$@" \
        "$bash_bin" "$repo_root/scripts/bgs_notify_common.sh" >"$state/out.log" 2>&1
}
# The record exactly as bgs_image_ci.sh writes it, plus a promoted latest status.
good_record() {
    python3 - "$report" "$canonical" "$manifest" "$sha" <<'PY'
import json, sys
report, canonical, manifest, sha = sys.argv[1:5]
d = {"schema_version": 1, "image_ref": f"{canonical}@{manifest}", "tagged_ref": f"{canonical}:tgf-42.2", "tag": "tgf-42.2",
     "manifest_digest": manifest, "config_digest": "sha256:" + "2" * 64, "local_image_id": "sha256:" + "3" * 64,
     "source_repository": "eyagovbusiness/TheGoodFramework", "source_ref": "refs/heads/main", "source_sha": sha,
     "run_id": 999, "run_number": 42, "run_attempt": 2}
open(f"{report}/publication.json", "w").write(json.dumps(d))
PY
    printf 'latest: promoted\n' >"$report/latest-status.txt"
}
ok() { run_notify || { cat "$state/out.log" >&2; fail "expected success"; }; }
bad() { if run_notify "$@"; then fail "expected failure: $*"; fi; [[ ! -e "$state/gh.calls" ]] || fail "gh was called for a rejected publication: $*"; }
# Edit one field of the good record, written back through python so the JSON stays valid.
mutate() {
    good_record
    python3 - "$report/publication.json" "$1" <<'PY'
import json, sys
path, edit = sys.argv[1:3]
d = json.load(open(path))
exec(edit)
open(path, "w").write(json.dumps(d))
PY
}

# --- the complete, verified publication is announced exactly once ---
good_record
ok
[[ "$(wc -l <"$state/gh.calls")" == 1 ]] || fail "gh must be called exactly once"
[[ "$(cat "$state/gh.args")" == "api --method POST -H Accept: application/vnd.github+json repos/Biognosys/OF.Common/dispatches --input -" ]] || fail "unexpected gh arguments: $(cat "$state/gh.args")"
[[ "$(cat "$state/gh.token")" == "token=pat-token-value" ]] || fail "gh must receive the dispatch token through GH_TOKEN"
python3 - "$state/gh.stdin" "$canonical" "$manifest" "$sha" <<'PY' || fail "unexpected dispatch payload"
import json, sys
path, canonical, manifest, sha = sys.argv[1:5]
expected = {"event_type": "L1-dependency-update", "client_payload": {
    "source_repo": "eyagovbusiness/TheGoodFramework", "source_tag": "tgf-42.2", "source_commit": sha,
    "source_image": f"{canonical}@{manifest}", "source_digest": manifest, "source_config_digest": "sha256:" + "2" * 64,
    "source_run_id": "999", "source_run_attempt": "2"}}
assert json.load(open(path)) == expected, open(path).read()
assert len(expected["client_payload"]) <= 10
PY
for leak in "$state/gh.args" "$state/gh.stdin" "$state/out.log"; do
    ! grep -q 'pat-token-value' "$leak" || fail "the token leaked into $leak"
done

# --- the token reaches gh only; the payload builder never has it in its environment ---
[[ "$(cat "$state/python.token")" == "token=none" ]] || fail "the payload builder must not receive the dispatch token"

# --- gh failure is a failure (the image stays published, the run turns red) ---
good_record
if run_notify MOCK_GH_FAIL=1; then fail "a rejected dispatch must fail the step"; fi
grep -q 'may dispatch' "$state/out.log" || fail "a failed dispatch must explain the likely cause"
! grep -q 'pat-token-value' "$state/out.log" || fail "the token leaked after a failed dispatch"

# --- nothing is sent without a trusted, authenticated, complete publication ---
good_record; bad GITHUB_ACTIONS=false
good_record; bad GITHUB_ACTIONS=
good_record; bad GH_TOKEN=
good_record; bad REPORT_DIR=
good_record; bad REPORT_DIR=relative/path
good_record; bad IMAGE_REGISTRY=
good_record; bad IMAGE_REGISTRY=other.example
good_record; bad IMAGE_REGISTRY="bad registry"
good_record; bad GITHUB_EVENT_NAME=pull_request
good_record; bad GITHUB_EVENT_NAME=pull_request_target
good_record; bad GITHUB_EVENT_NAME=schedule
good_record; bad GITHUB_REF=refs/heads/dev
good_record; bad GITHUB_REF=refs/pull/71/merge
good_record; bad GITHUB_REPOSITORY=someone/Fork
good_record; bad GITHUB_SHA="$(printf 'b%.0s' {1..40})"
good_record; bad GITHUB_RUN_ID=1000
good_record; bad GITHUB_RUN_NUMBER=43
good_record; bad GITHUB_RUN_ATTEMPT=3

# Missing, partial and non-promoted publication records.
good_record; rm "$report/publication.json"; bad
good_record; rm "$report/latest-status.txt"; bad
good_record; : >"$report/publication.json"; bad
good_record; printf '{' >"$report/publication.json"; bad
good_record; printf '[]' >"$report/publication.json"; bad
good_record; printf 'latest: not-promoted\n' >"$report/latest-status.txt"; bad
good_record; printf 'latest: pushed-unverified\n' >"$report/latest-status.txt"; bad
good_record; printf 'latest: promoted' >"$report/latest-status.txt"; bad
good_record; printf 'latest: promoted\nlatest: not-promoted\n' >"$report/latest-status.txt"; bad
good_record; : >"$report/latest-status.txt"; bad
good_record; python3 -c 'print("x" * 70000)' >"$report/latest-status.txt"; bad

# Bad digests, false configs and references that are not the canonical image of this run.
mutate 'd["manifest_digest"] = "sha256:" + "1" * 63'; bad
mutate 'd["manifest_digest"] = "sha256:" + "9" * 64'; bad
mutate 'd["manifest_digest"] = "1" * 64'; bad
mutate 'd["config_digest"] = "sha256:" + "G" * 64'; bad
mutate 'd["config_digest"] = ""'; bad
mutate 'del d["config_digest"]'; bad
mutate 'del d["local_image_id"]'; bad
mutate 'd["extra"] = "x"'; bad
mutate 'd["schema_version"] = 2'; bad
mutate 'd["image_ref"] = "other.example/base-images/the_good_framework@" + d["manifest_digest"]'; bad
mutate 'd["image_ref"] = d["image_ref"].replace("the_good_framework", "alpine")'; bad
mutate 'd["image_ref"] = d["tagged_ref"]'; bad
mutate 'd["image_ref"] = d["image_ref"] + "\n"'; bad
mutate 'd["tag"] = "latest"'; bad
mutate 'd["tagged_ref"] = d["tagged_ref"].replace("tgf-42.2", "latest")'; bad
mutate 'd["source_repository"] = "someone/Fork"'; bad
mutate 'd["source_ref"] = "refs/heads/dev"'; bad
mutate 'd["source_sha"] = "b" * 40'; bad
mutate 'd["run_id"] = 1000'; bad
mutate 'd["run_attempt"] = "2"'; bad

echo "bgs_notify_common tests passed"
