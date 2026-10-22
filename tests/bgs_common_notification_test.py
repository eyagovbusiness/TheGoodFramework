import json
import os
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

HERE = os.path.dirname(os.path.abspath(__file__))
HELPER = os.path.join(HERE, "..", "scripts", "bgs_common_notification.py")
sys.path.insert(0, os.path.join(HERE, "..", "scripts"))
import bgs_common_notification as notification  # noqa: E402

REGISTRY = "omicsflowinternal.azurecr.io"
CANONICAL = f"{REGISTRY}/base-images/the_good_framework"
SHA = "a" * 40
MANIFEST = "sha256:" + "1" * 64
CONFIG = "sha256:" + "2" * 64
LOCAL = "sha256:" + "3" * 64
ENV = {
    "GITHUB_REPOSITORY": "eyagovbusiness/TheGoodFramework",
    "GITHUB_REF": "refs/heads/main",
    "GITHUB_EVENT_NAME": "push",
    "GITHUB_SHA": SHA,
    "GITHUB_RUN_ID": "999",
    "GITHUB_RUN_NUMBER": "42",
    "GITHUB_RUN_ATTEMPT": "2",
    "IMAGE_REGISTRY": REGISTRY,
}


def record(**changes):
    data = {
        "schema_version": 1,
        "image_ref": f"{CANONICAL}@{MANIFEST}",
        "tagged_ref": f"{CANONICAL}:tgf-42.2",
        "tag": "tgf-42.2",
        "manifest_digest": MANIFEST,
        "config_digest": CONFIG,
        "local_image_id": LOCAL,
        "source_repository": "eyagovbusiness/TheGoodFramework",
        "source_ref": "refs/heads/main",
        "source_sha": SHA,
        "run_id": 999,
        "run_number": 42,
        "run_attempt": 2,
    }
    data.update(changes)
    return data


class NotificationTest(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        saved = {name: os.environ.get(name) for name in ENV}
        self.addCleanup(lambda: [os.environ.pop(n, None) if v is None else os.environ.__setitem__(n, v) for n, v in saved.items()])
        os.environ.update(ENV)

    def write(self, data=None, status="latest: promoted\n", raw=None):
        path = os.path.join(self.dir.name, "publication.json")
        status_path = os.path.join(self.dir.name, "latest-status.txt")
        for target in (path, status_path):
            if os.path.exists(target):
                os.remove(target)
        if data is not None or raw is not None:
            with open(path, "w", encoding="utf-8") as handle:
                handle.write(raw if raw is not None else json.dumps(data))
        if status is not None:
            with open(status_path, "w", encoding="utf-8") as handle:
                handle.write(status)

    def rejects(self, fragment=""):
        with self.assertRaises(notification.NotificationError) as caught:
            notification.build_payload(self.dir.name)
        self.assertIn(fragment, str(caught.exception))

    def test_a_verified_publication_yields_the_complete_payload(self):
        self.write(record())
        payload = notification.build_payload(self.dir.name)
        self.assertEqual(payload["event_type"], "L1-dependency-update")
        self.assertEqual(payload["client_payload"], {
            "source_repo": "eyagovbusiness/TheGoodFramework",
            "source_tag": "tgf-42.2",
            "source_commit": SHA,
            "source_image": f"{CANONICAL}@{MANIFEST}",
            "source_digest": MANIFEST,
            "source_config_digest": CONFIG,
            "source_run_id": "999",
            "source_run_attempt": "2",
        })
        self.assertLessEqual(len(payload["client_payload"]), 10, "repository_dispatch allows at most 10 properties")

    def test_release_required_notification_rejects_missing_or_mismatched_tag_mapping(self):
        self.write(record())
        payload = notification.build_payload(self.dir.name)["client_payload"]
        import hashlib
        scan = b'{"scan": "evidence"}'
        mapping = {"schema_version": 1, "git_tag": "bgs-image/tgf-42.2", **payload,
                   "scan_report_sha256": hashlib.sha256(scan).hexdigest()}
        with open(os.path.join(self.dir.name, "trivy-report.json"), "wb") as handle:
            handle.write(scan)
        path = os.path.join(self.dir.name, "release.json")
        with patch.dict(os.environ, {"REQUIRE_IMAGE_RELEASE": "true"}):
            self.rejects("release.json")
            with open(path, "w", encoding="utf-8") as handle:
                json.dump(mapping, handle)
            self.assertEqual(notification.build_payload(self.dir.name)["client_payload"], payload)
            for field in ("git_tag", "source_commit", "source_digest", "source_config_digest", "source_run_id", "scan_report_sha256"):
                with self.subTest(field=field):
                    with open(path, "w", encoding="utf-8") as handle:
                        json.dump({**mapping, field: "wrong"}, handle)
                    self.rejects()

    def test_only_a_promoted_latest_is_announced(self):
        for status in ("latest: not-promoted\n", "latest: pushed-unverified\n", "latest: promoted", "latest: promoted\nextra\n",
                       "latest: promoted \n", "Latest: promoted\n", ""):
            with self.subTest(status=status):
                self.write(record(), status=status)
                self.rejects("latest was not promoted")
        self.write(record(), status=None)
        self.rejects("latest-status.txt")

    def test_missing_or_malformed_records_are_rejected(self):
        self.write(None)
        self.rejects("publication.json")
        for raw in ("", "{", "[]", "null", '"text"', json.dumps(record())[:-1], "\ufeff" + json.dumps(record())):
            with self.subTest(raw=raw):
                self.write(raw=raw)
                self.rejects()
        self.write(raw=json.dumps(record()).replace('"tag"', '"tag": "tgf-1.1", "tag"', 1))
        self.rejects("repeats a key")

    def test_an_oversized_or_non_utf8_record_is_rejected(self):
        self.write(raw=json.dumps(record(tag="x" * (70 << 10))))
        self.rejects("larger")
        path = os.path.join(self.dir.name, "publication.json")
        self.write(record())
        with open(path, "wb") as handle:
            handle.write(b"\xff\xfe")
        self.rejects("UTF-8")

    def test_every_recorded_field_is_required_and_no_other_is_allowed(self):
        for name in record():
            with self.subTest(missing=name):
                data = record()
                del data[name]
                self.write(data)
                self.rejects("exactly the recorded fields")
        self.write(record(unexpected="x"))
        self.rejects("exactly the recorded fields")

    def test_wrong_types_are_rejected(self):
        for name, value in (("image_ref", None), ("tag", 7), ("manifest_digest", ["x"]), ("source_sha", 1), ("run_id", "999"),
                            ("run_number", 42.0), ("run_attempt", True), ("run_id", -1), ("schema_version", "1"), ("schema_version", True)):
            with self.subTest(name=name, value=value):
                self.write(record(**{name: value}))
                self.rejects()

    def test_unsupported_schema_version_is_rejected(self):
        self.write(record(schema_version=2))
        self.rejects("schema_version")

    def test_bad_digests_are_rejected(self):
        bad = ("sha256:" + "1" * 63, "sha256:" + "1" * 65, "sha256:" + "A" * 64, "1" * 64, "sha512:" + "1" * 64,
               MANIFEST + "\n", " " + MANIFEST, "")
        for name in ("manifest_digest", "config_digest", "local_image_id"):
            for value in bad:
                with self.subTest(name=name, value=value):
                    changes = {name: value}
                    if name == "manifest_digest":
                        changes["image_ref"] = f"{CANONICAL}@{value}"
                    self.write(record(**changes))
                    self.rejects("sha256:<64 lowercase hex>")

    def test_the_image_reference_must_be_the_canonical_one_for_the_digest(self):
        other = "sha256:" + "9" * 64
        for ref in (f"{CANONICAL}@{other}", f"{CANONICAL}:tgf-42.2", f"{CANONICAL}:latest@{MANIFEST}", f"{CANONICAL}@{MANIFEST}\n",
                    f"other.example/base-images/the_good_framework@{MANIFEST}", f"{REGISTRY}/base-images/alpine@{MANIFEST}",
                    f"{REGISTRY}/the_good_framework@{MANIFEST}", f"{REGISTRY}/base-images/the_good_framework/x@{MANIFEST}",
                    f"{REGISTRY}/base-images/dev/the_good_framework@{MANIFEST}", f"x{CANONICAL}@{MANIFEST}", ""):
            with self.subTest(ref=ref):
                self.write(record(image_ref=ref))
                self.rejects("canonical reference")

    def test_the_registry_must_be_the_configured_one(self):
        self.write(record(image_ref=f"other.example/base-images/the_good_framework@{MANIFEST}"))
        self.rejects("canonical reference")
        self.write(record())
        for value in ("other.example", "", "UPPER.example", "bad registry", "host/path", "-x.example"):
            with self.subTest(registry=value):
                os.environ["IMAGE_REGISTRY"] = value
                self.rejects()

    def test_the_version_tag_must_belong_to_this_run(self):
        for changes in ({"tag": "tgf-42.1"}, {"tag": "latest"}, {"tag": "tgf-42.2\n"}, {"tagged_ref": f"{CANONICAL}:tgf-42.1"},
                        {"tagged_ref": f"{CANONICAL}:latest"}, {"tagged_ref": f"other.example/base-images/the_good_framework:tgf-42.2"}):
            with self.subTest(changes=changes):
                self.write(record(**changes))
                self.rejects("version tag")

    def test_the_source_must_be_the_allowlisted_repository_on_main(self):
        for changes in ({"source_repository": "someone/Fork"}, {"source_repository": "eyagovbusiness/thegoodframework"},
                        {"source_ref": "refs/heads/dev"}, {"source_ref": "refs/pull/1/merge"}):
            with self.subTest(changes=changes):
                self.write(record(**changes))
                self.rejects("not from")
        self.write(record(source_repository="someone/Fork"))
        os.environ["GITHUB_REPOSITORY"] = "someone/Fork"
        self.rejects("allowlisted")
        os.environ.update(ENV)
        self.write(record())
        for name, value in (("GITHUB_REF", "refs/heads/dev"), ("GITHUB_REF", "refs/tags/v1"), ("GITHUB_REF", ""),
                            ("GITHUB_EVENT_NAME", "pull_request"), ("GITHUB_EVENT_NAME", "pull_request_target"),
                            ("GITHUB_EVENT_NAME", "schedule"), ("GITHUB_EVENT_NAME", "")):
            with self.subTest(name=name, value=value):
                os.environ[name] = value
                self.rejects()
                os.environ.update(ENV)
        for event in ("push", "workflow_dispatch"):
            os.environ["GITHUB_EVENT_NAME"] = event
            notification.build_payload(self.dir.name)

    def test_the_record_must_describe_this_commit_and_run(self):
        for changes in ({"source_sha": "b" * 40}, {"source_sha": SHA.upper()}, {"run_id": 1000}, {"run_number": 43}, {"run_attempt": 3}):
            with self.subTest(changes=changes):
                self.write(record(**changes))
                self.rejects()
        self.write(record())
        for name, value in (("GITHUB_SHA", "b" * 40), ("GITHUB_SHA", "abc"), ("GITHUB_SHA", ""), ("GITHUB_RUN_ID", "1000"),
                            ("GITHUB_RUN_ID", "9x"), ("GITHUB_RUN_ID", ""), ("GITHUB_RUN_NUMBER", "43"), ("GITHUB_RUN_ATTEMPT", "3"),
                            ("GITHUB_RUN_ATTEMPT", "")):
            with self.subTest(name=name, value=value):
                os.environ[name] = value
                self.rejects()
                os.environ.update(ENV)

    def test_a_missing_trusted_environment_is_rejected(self):
        self.write(record())
        for name in ENV:
            with self.subTest(name=name):
                del os.environ[name]
                self.rejects()
                os.environ.update(ENV)


class CommandLineTest(unittest.TestCase):
    def run_helper(self, report_dir, **env):
        base = {"PATH": os.environ.get("PATH", ""), **ENV, **env}
        return subprocess.run([sys.executable, "-B", HELPER, report_dir], capture_output=True, text=True, env=base, check=False)

    def test_stdout_carries_one_json_payload_only_on_success(self):
        with tempfile.TemporaryDirectory() as report:
            with open(os.path.join(report, "publication.json"), "w", encoding="utf-8") as handle:
                json.dump(record(), handle)
            with open(os.path.join(report, "latest-status.txt"), "w", encoding="utf-8") as handle:
                handle.write("latest: promoted\n")
            good = self.run_helper(report)
            self.assertEqual((good.returncode, good.stderr), (0, ""))
            self.assertEqual(json.loads(good.stdout)["client_payload"]["source_digest"], MANIFEST)
            bad = self.run_helper(report, GITHUB_SHA="b" * 40, GH_TOKEN="token-value")
            self.assertEqual((bad.returncode, bad.stdout), (1, ""))
            self.assertIn("NOT NOTIFYING", bad.stderr)
            self.assertNotIn("token-value", good.stdout + good.stderr + bad.stdout + bad.stderr)

    def test_a_missing_directory_and_wrong_usage_are_failures(self):
        self.assertEqual(self.run_helper(os.path.join(tempfile.gettempdir(), "no-such-report-dir")).returncode, 1)
        self.assertEqual(subprocess.run([sys.executable, "-B", HELPER], capture_output=True, text=True, check=False).returncode, 2)


if __name__ == "__main__":
    unittest.main()
