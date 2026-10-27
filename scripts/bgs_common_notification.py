#!/usr/bin/env python3
"""Build the OF.Common notification for a verified TheGoodFramework publication.

  bgs_common_notification.py <report-dir>

Reads <report-dir>/publication.json and <report-dir>/latest-status.txt, which
bgs_image_ci.sh publish wrote, and prints ONE repository_dispatch request body
on stdout. Nothing is printed to stdout, and the exit status is 1, unless the
publication is authentic: every recorded identity must agree with the trusted
GitHub Actions environment of this very run, `latest` must have been promoted,
and the image reference must be the canonical one for the configured registry.
The token is never read here and never appears in the output.

Environment: GITHUB_REPOSITORY, GITHUB_SHA, GITHUB_REF, GITHUB_EVENT_NAME,
GITHUB_RUN_ID, GITHUB_RUN_NUMBER, GITHUB_RUN_ATTEMPT and IMAGE_REGISTRY.
"""
import json
import os
import re
import sys

MAX_BYTES = 64 << 10
EVENT_TYPE = "L1-dependency-update"
SOURCE_REPOSITORY = "eyagovbusiness/TheGoodFramework"  # the only repository allowed to notify
SOURCE_REF = "refs/heads/main"
IMAGE_PATH = "base-images/the_good_framework"
LATEST_PROMOTED = "latest: promoted\n"
DIGEST = re.compile(r"sha256:[a-f0-9]{64}")
COMMIT = re.compile(r"[a-f0-9]{40}")
NUMBER = re.compile(r"[0-9]{1,18}")
REGISTRY = re.compile(r"[a-z0-9][a-z0-9.-]*(:[0-9]+)?")
TRIGGER_EVENTS = {"push", "workflow_dispatch"}
STRINGS = ("image_ref", "tagged_ref", "tag", "manifest_digest", "config_digest", "local_image_id",
           "source_repository", "source_ref", "source_sha")
INTEGERS = ("schema_version", "run_id", "run_number", "run_attempt")
RECORD_KEYS = frozenset(STRINGS + INTEGERS)


class NotificationError(Exception):
    pass


def _unique(pairs):
    keys = [key for key, _ in pairs]
    if len(keys) != len(set(keys)):
        raise NotificationError("publication.json repeats a key")
    return dict(pairs)


def _read(path, what):
    if os.path.islink(path) or not os.path.isfile(path):
        raise NotificationError(f"{what} is missing or not a regular file")
    with open(path, "rb") as handle:
        raw = handle.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES:
        raise NotificationError(f"{what} is larger than {MAX_BYTES} bytes")
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        raise NotificationError(f"{what} is not UTF-8") from None


def _env(name, pattern):
    value = os.environ.get(name, "")
    if not pattern.fullmatch(value):
        raise NotificationError(f"{name} is missing or malformed")
    return value


def build_payload(report_dir):
    if _read(os.path.join(report_dir, "latest-status.txt"), "latest-status.txt") != LATEST_PROMOTED:
        raise NotificationError("latest was not promoted and verified; nothing is announced")
    try:
        record = json.loads(_read(os.path.join(report_dir, "publication.json"), "publication.json"), object_pairs_hook=_unique)
    except ValueError as error:
        raise NotificationError(f"publication.json is not valid JSON: {error}") from None
    if not isinstance(record, dict) or set(record) != RECORD_KEYS:
        raise NotificationError("publication.json does not have exactly the recorded fields")
    for name in STRINGS:
        if not isinstance(record[name], str):
            raise NotificationError(f"{name} must be a string")
    for name in INTEGERS:
        if type(record[name]) is not int or record[name] < 0:
            raise NotificationError(f"{name} must be a non-negative integer")
    if record["schema_version"] != 1:
        raise NotificationError("unsupported publication schema_version")

    # The trusted identity of this run, as GitHub Actions set it.
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    if repository != SOURCE_REPOSITORY or record["source_repository"] != SOURCE_REPOSITORY:
        raise NotificationError("the publication is not from the allowlisted source repository")
    if os.environ.get("GITHUB_REF") != SOURCE_REF or record["source_ref"] != SOURCE_REF:
        raise NotificationError(f"the publication is not from {SOURCE_REF}")
    if os.environ.get("GITHUB_EVENT_NAME") not in TRIGGER_EVENTS:
        raise NotificationError("only a push or dispatch on main may notify")
    sha = _env("GITHUB_SHA", COMMIT)
    run_id = _env("GITHUB_RUN_ID", NUMBER)
    run_number = _env("GITHUB_RUN_NUMBER", NUMBER)
    run_attempt = _env("GITHUB_RUN_ATTEMPT", NUMBER)
    registry = _env("IMAGE_REGISTRY", REGISTRY)
    if record["source_sha"] != sha:
        raise NotificationError("the recorded commit differs from the triggering commit")
    for name, expected in (("run_id", run_id), ("run_number", run_number), ("run_attempt", run_attempt)):
        if str(record[name]) != expected:
            raise NotificationError(f"the recorded {name} differs from this run")

    # The canonical reference, rebuilt from trusted parts and compared as a whole string.
    canonical = f"{registry}/{IMAGE_PATH}"
    tag = f"tgf-{run_number}.{run_attempt}"
    for name in ("manifest_digest", "config_digest", "local_image_id"):
        if not DIGEST.fullmatch(record[name]):
            raise NotificationError(f"{name} must be sha256:<64 lowercase hex>")
    if record["tag"] != tag or record["tagged_ref"] != f"{canonical}:{tag}":
        raise NotificationError("the recorded version tag is not this run's canonical tag")
    if record["image_ref"] != f"{canonical}@{record['manifest_digest']}":
        raise NotificationError("the recorded image reference is not the canonical reference for its digest")

    if os.environ.get("REQUIRE_IMAGE_RELEASE") == "true":
        try:
            release = json.loads(_read(os.path.join(report_dir, "release.json"), "release.json"), object_pairs_hook=_unique)
        except ValueError:
            raise NotificationError("release.json is not valid JSON") from None
        expected = {"git_tag": f"bgs-image/{tag}", "source_commit": sha,
                    "source_tag": tag, "source_repo": repository,
                    "source_image": record["image_ref"], "source_digest": record["manifest_digest"],
                    "source_config_digest": record["config_digest"], "source_run_id": run_id,
                    "source_run_attempt": run_attempt, "schema_version": 1}
        if not isinstance(release, dict) or any(release.get(key) != value for key, value in expected.items()):
            raise NotificationError("Git release mapping differs from the verified publication")
        import hashlib
        scan_path = os.path.join(report_dir, "trivy-report.json")
        if os.path.islink(scan_path) or not os.path.isfile(scan_path):
            raise NotificationError("scan evidence is missing or not a regular file")
        with open(scan_path, "rb") as handle:
            scan_digest = hashlib.file_digest(handle, "sha256").hexdigest()
        if release.get("scan_report_sha256") != scan_digest:
            raise NotificationError("Git release scan hash differs from the publication evidence")

    return {
        "event_type": EVENT_TYPE,
        "client_payload": {
            # Fields the receiver has always been sent.
            "source_repo": repository,
            "source_tag": tag,
            "source_commit": sha,
            # The verified identity of the published image.
            "source_image": record["image_ref"],
            "source_digest": record["manifest_digest"],
            "source_config_digest": record["config_digest"],
            "source_run_id": run_id,
            "source_run_attempt": run_attempt,
        },
    }


def main(argv):
    if len(argv) != 2:
        print(__doc__, file=sys.stderr)
        return 2
    try:
        payload = build_payload(argv[1])
    except (NotificationError, OSError) as error:
        print(f"NOT NOTIFYING: {error}", file=sys.stderr)
        return 1
    print(json.dumps(payload, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
