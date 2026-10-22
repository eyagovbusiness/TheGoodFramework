#!/usr/bin/env python3
"""Record a verified BGS image as a protected, annotated Git tag; never update a ref.

The tag annotation binds the reviewed source, immutable manifest/config, run and
scan report hash. This runs in a separate write-scoped job after publication.
Existing tags are rejected, including identical ones: reruns use a new attempt.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

from bgs_common_notification import build_payload, NotificationError, SOURCE_REPOSITORY


class ReleaseError(Exception):
    pass


def api(path, payload=None):
    command = ["gh", "api", f"repos/{SOURCE_REPOSITORY}/{path}"]
    if payload is not None:
        command += ["--method", "POST", "--input", "-"]
    result = subprocess.run(command, input=None if payload is None else json.dumps(payload),
                            text=True, capture_output=True, check=False)
    if result.returncode:
        raise ReleaseError("GitHub API request failed; no downstream notification is authorized")
    try:
        return json.loads(result.stdout)
    except ValueError:
        raise ReleaseError("GitHub API returned invalid JSON") from None


def authorize_source(call, sha):
    # Effective rules include inherited rules. Documentation and protected=true alone
    # are insufficient: require reviews, named checks, no force push and no deletion.
    rules = call("rules/branches/main")
    kinds = {rule["type"] for rule in rules}
    required = {"pull_request", "required_status_checks", "non_fast_forward", "deletion"}
    if not required <= kinds:
        raise ReleaseError("main lacks required effective branch rules")
    review = next(rule for rule in rules if rule["type"] == "pull_request")["parameters"]
    checks = next(rule for rule in rules if rule["type"] == "required_status_checks")["parameters"]
    contexts = {item["context"] for item in checks["required_status_checks"]}
    if (review["required_approving_review_count"] < 1
            or not review.get("require_code_owner_review")
            or not review.get("dismiss_stale_reviews_on_push")
            or not review.get("required_review_thread_resolution")
            or not checks.get("strict_required_status_checks_policy")
            or not {"tests", "scan", "validate"} <= contexts):
        raise ReleaseError("main must require review and the tests, scan and validate checks")
    # A bypassed direct push is not a release source. Require a merged PR whose
    # actual merge commit is this SHA and whose base is main in this repository.
    pulls = call(f"commits/{sha}/pulls?per_page=100")
    candidates = [pull for pull in pulls if pull.get("merged_at") and pull.get("merge_commit_sha") == sha
               and pull["base"]["ref"] == "main"
               and pull["base"]["repo"]["full_name"] == SOURCE_REPOSITORY]
    if not candidates:
        raise ReleaseError("source is not a merged PR commit on main")
    pull = candidates[0]
    reviews = call(f"pulls/{pull['number']}/reviews?per_page=100")
    latest_reviews = {}
    for entry in reviews:
        if entry["state"] != "COMMENTED":
            latest_reviews[entry["user"]["login"]] = entry
    if not any(entry["state"] == "APPROVED" and entry["commit_id"] == pull["head"]["sha"]
               and entry["user"]["login"] != pull["user"]["login"] for entry in latest_reviews.values()):
        raise ReleaseError("merged source lacks a current independent approval")
    if call("git/ref/heads/main")["object"]["sha"] != sha:
        raise ReleaseError("main moved; release the newer reviewed commit")


def authorize_tags(call):
    # Deliberately require the exact namespace condition: broad/glob-negated rule
    # inference is unsafe. Creation is limited to the documented release actor;
    # update/deletion must have a separate ruleset with NO bypass actors.
    immutable = False
    controlled_creation = False
    for summary in call("rulesets?per_page=100"):
        if summary.get("target") != "tag" or summary.get("enforcement") != "active":
            continue
        rule = call(f"rulesets/{summary['id']}")
        refs = rule.get("conditions", {}).get("ref_name", {})
        if refs.get("include") != ["refs/tags/bgs-image/**/*"] or refs.get("exclude"):
            continue
        kinds = {entry["type"] for entry in rule["rules"]}
        bypass = rule.get("bypass_actors", [])
        immutable |= {"update", "deletion"} <= kinds and not bypass
        controlled_creation |= "creation" in kinds and bool(bypass) and all(
            actor["actor_type"] == "RepositoryRole" and actor["bypass_mode"] == "always"
            and actor["actor_id"] == 5 for actor in bypass)  # GitHub's repository admin role
    if not immutable or not controlled_creation:
        raise ReleaseError("image tags require active no-bypass immutability and admin-only creation rulesets")


def release(report_dir, call=api):
    if os.environ.get("GITHUB_ACTIONS") != "true":
        raise ReleaseError("image releases run only in GitHub Actions")
    if not os.environ.get("GH_TOKEN"):
        raise ReleaseError("the existing PAT_TOKEN release credential is missing")
    payload = build_payload(str(report_dir))["client_payload"]
    sha = payload["source_commit"]
    authorize_source(call, sha)
    authorize_tags(call)
    tag = "bgs-image/" + payload["source_tag"]
    # The bounded match list proves absence, not a suppressed 404/auth error.
    if call(f"git/matching-refs/tags/{tag}"):
        raise ReleaseError("image Git release tag already exists; use a new run attempt")
    scan = report_dir / "trivy-report.json"
    if scan.is_symlink() or not scan.is_file():
        raise ReleaseError("scan evidence is missing or not a regular file")
    annotation = {"schema_version": 1, "git_tag": tag, **payload,
                  "scan_report_sha256": hashlib.sha256(scan.read_bytes()).hexdigest()}
    message = json.dumps(annotation, sort_keys=True)
    obj = call("git/tags", {"tag": tag, "message": message, "object": sha, "type": "commit"})
    if obj.get("object", {}).get("sha") != sha or obj.get("object", {}).get("type") != "commit":
        raise ReleaseError("created tag object does not target the source commit")
    # POST fails atomically on collision. Never PATCH, force or delete a tag.
    call("git/refs", {"ref": "refs/tags/" + tag, "sha": obj["sha"]})
    ref = call(f"git/ref/tags/{tag}")
    if ref["object"]["type"] != "tag" or ref["object"]["sha"] != obj["sha"]:
        raise ReleaseError("release ref readback differs from the created tag object")
    verified = call(f"git/tags/{obj['sha']}")
    if verified["object"]["sha"] != sha or verified["object"]["type"] != "commit" or verified["message"].rstrip("\n") != message:
        raise ReleaseError("release annotation readback differs from verified image mapping")
    (report_dir / "release.json").write_text(json.dumps(annotation, indent=2) + "\n", encoding="utf-8")
    print(f"Released {tag} from {sha}")


if __name__ == "__main__":
    try:
        if sys.argv[1:] == ["--authorize-source"]:
            if (os.environ.get("GITHUB_ACTIONS") != "true"
                    or os.environ.get("GITHUB_REPOSITORY") != SOURCE_REPOSITORY
                    or os.environ.get("GITHUB_REF") != "refs/heads/main"
                    or os.environ.get("GITHUB_EVENT_NAME") not in {"push", "workflow_dispatch"}):
                raise ReleaseError("publication requires a trusted main push or manual rebuild")
            sha = os.environ.get("GITHUB_SHA", "")
            import re
            if not re.fullmatch(r"[a-f0-9]{40}", sha):
                raise ReleaseError("source SHA is malformed")
            authorize_source(api, sha)
        elif not sys.argv[1:]:
            release(Path(os.environ["REPORT_DIR"]))
        else:
            raise ReleaseError("usage: bgs_image_release.py [--authorize-source]")
    except (ReleaseError, NotificationError, OSError, KeyError, TypeError, ValueError) as error:
        print(f"NOT RELEASING: {error}", file=sys.stderr)
        sys.exit(1)
