"""Offline contract tests. Fake GitHub/registry exercise the real adapters and engine."""
import base64
import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
import urllib.parse
import zipfile
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("receiver", Path(__file__).resolve().parents[1] / "scripts/base_image_receiver.py")
r = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(r)
OLD = "sha256:" + "a" * 64
NEW = "sha256:" + "b" * 64
SHA = "1" * 40
TREE = "2" * 40
REGISTRY = "omicsflowinternal.azurecr.io"
REPO = "Biognosys/of.frontend"


def encoded(value):
    return json.dumps(value, separators=(",", ":")).encode()


def dockerfile(family="alpine", internal=False):
    build = b"FROM oven/bun:alpine AS build\r\nRUN bun install\r\n" if internal else b""
    suffix = b" AS base" if family == "aspnet" else b""
    common = b"FROM ${IMAGE_REGISTRY}/biognosys/omicsflow/of.common@" + OLD.encode() + b" AS base-packages\r\n" if family == "aspnet" else b""
    return (b"ARG IMAGE_REGISTRY=" + REGISTRY.encode() + b"\r\n" + build
            + b"FROM ${IMAGE_REGISTRY}/base-images/" + family.encode() + b"@" + OLD.encode() + suffix
            + b"\r\n" + common + b"USER root\r\nENTRYPOINT [\"unchanged\"]")


def profile(family="alpine", branches=("main", "dev")):
    return r.parse_profile({"schema": 1, "repository": REPO, "family": family, "registry": REGISTRY, "botOwner": "BOT",
                            "public": False, "paused": False, "suppressed_digests": [],
                            "targets": [{"branch": branch, "paused": False,
                                         "files": ["Dockerfile", "Dockerfile_internal"],
                                         "bootstrap": [{"digest": OLD, "published_before": "2026-10-26T00:00:00Z"}],
                                         "layout": {"kind": "registry-from", "stage": "base" if family == "aspnet" else None}}
                                        for branch in branches]})


class Registry:
    def __init__(self, family="alpine", version="10.0.4", user="biognosys", index=False):
        self.calls = []
        self.config = encoded({"os": "linux", "architecture": "amd64", "config": {
            "User": user, "Env": ["ASPNET_VERSION=" + version]}})
        self.config_digest = r._sha256_of(self.config)
        manifest = encoded({"schemaVersion": 2, "mediaType": r.IMAGE_TYPES[0], "config": {
            "mediaType": r.CONFIG_TYPES[0], "digest": self.config_digest, "size": len(self.config)}})
        self.manifests = {r._sha256_of(manifest): manifest}
        self.body = encoded({"schemaVersion": 2, "mediaType": r.INDEX_TYPES[0], "manifests": [{
            "digest": r._sha256_of(manifest), "platform": {"os": "linux", "architecture": "amd64"}}]}) if index else manifest
        self.digest = r._sha256_of(self.body)
        self.manifests[self.digest] = self.body
        self.family = family

    def get_manifest(self, repository, reference):
        self.calls.append(("manifest", repository, reference))
        body = self.manifests.get(reference, self.body)
        return json.loads(body)["mediaType"], body, r._sha256_of(body)

    def get_blob(self, repository, digest, maximum):
        self.calls.append(("blob", repository, digest))
        return self.config


def publication(registry):
    return {"image": registry.family, "repository": f"{REGISTRY}/base-images/{registry.family}",
            "registry": REGISTRY, "digest": registry.digest, "reference": f"{REGISTRY}/base-images/{registry.family}@{registry.digest}",
            "tag": "42.1", "source_repository": "Biognosys/bgsg.base-images", "source_sha": SHA,
            "run_id": "42", "run_attempt": "1", "run_number": "42", "event": "push",
            "completed_at": "2026-10-27T01:00:00Z", "created_at": "2026-10-27T00:00:00Z", "config_digest": registry.config_digest,
            "runtime": "aspnet 10.0.4" if registry.family == "aspnet" else "alpine"}


class SourceApi:
    def __init__(self, registry):
        self.calls = []
        self.runs = [{"id": 42, "run_number": 42, "run_attempt": 1, "created_at": "2026-10-27T00:00:00Z",
                      "head_sha": SHA, "repository": {"full_name": "Biognosys/bgsg.base-images"},
                      "head_repository": {"full_name": "Biognosys/bgsg.base-images"}, "head_branch": "main",
                      "event": "push", "path": r.FAMILIES[registry.family]["workflow"],
                      "status": "completed", "conclusion": "failure"}]  # notifier may fail
        self.jobs = {42: {"id": 99, "run_id": 42, "run_attempt": 1, "head_sha": SHA,
                          "name": r.PUBLISH_JOB, "status": "completed", "conclusion": "success",
                          "completed_at": "2026-10-27T01:00:00Z", "steps": [
                              {"name": name, "conclusion": "success"} for name in r.PUBLISH_STEPS]}}
        self.artifact = {"id": 55, "name": f"image-version-manifest-{registry.family}-42-1", "expired": False,
                         "size_in_bytes": 500, "workflow_run": {"id": 42, "head_branch": "main", "head_sha": SHA}}
        self.evidence = {key: publication(registry)[key] for key in ("repository", "tag", "digest", "reference", "run_id", "run_attempt")}
        self.evidence["source"] = SHA
        self.archive_override = None

    def request(self, method, path, body=None):
        self.calls.append((method, path, body))
        assert method == "GET", "source token must never write"
        if "/workflows/" in path:
            return 200, {"workflow_runs": self.runs}
        if "/jobs?" in path:
            run = int(path.split("/runs/")[1].split("/")[0])
            return 200, {"jobs": self.jobs[run] if isinstance(self.jobs[run], list) else [self.jobs[run]]}
        if "/artifacts?" in path:
            return 200, {"artifacts": self.artifact if isinstance(self.artifact, list) else [self.artifact]}
        raise AssertionError(path)

    def download(self, path, maximum):
        self.calls.append(("DOWNLOAD", path, None))
        if self.archive_override is not None:
            return self.archive_override
        out = io.BytesIO()
        with zipfile.ZipFile(out, "w") as archive:
            archive.writestr("evidence.json", encoded(self.evidence))
        return out.getvalue()


class TargetApi:
    def __init__(self, family="alpine"):
        self.calls = []
        self.parents = {"main": SHA, "dev": "3" * 40}
        self.data = {"Dockerfile": dockerfile(family), "Dockerfile_internal": dockerfile(family, True)}
        self.blobs = {r._git_blob_sha(data): data for data in self.data.values()}
        self.pulls = []
        self.refs = []
        self.mode = "100755"
        self.fail_pull = False
        self.race_ref = False
        self.move_after_commit = False

    def request(self, method, path, body=None):
        self.calls.append((method, path, body))
        if method == "GET":
            if "/git/ref/heads/" in path:
                branch = path.rsplit("/", 1)[1]
                return 200, {"object": {"type": "commit", "sha": self.parents[branch]}}
            if "/git/commits/" in path:
                return 200, {"sha": path.rsplit("/", 1)[1], "tree": {"sha": TREE}}
            if "/git/trees/" in path:
                return 200, {"tree": [{"path": name, "type": "blob", "mode": self.mode,
                                       "sha": r._git_blob_sha(data)} for name, data in self.data.items()]}
            if "/git/blobs/" in path:
                return 200, {"encoding": "base64", "content": base64.b64encode(self.blobs[path.rsplit("/", 1)[1]]).decode()}
            if "/pulls?" in path:
                return 200, self.pulls
            if "/branches?" in path:
                return 200, [{"name": item["ref"][len("refs/heads/"):]} for item in self.refs]
        if method == "POST":
            if path.endswith("/git/blobs"):
                data = base64.b64decode(body["content"])
                self.blobs[r._git_blob_sha(data)] = data
                return 201, {"sha": r._git_blob_sha(data)}
            if path.endswith("/git/trees"):
                return 201, {"sha": "4" * 40}
            if path.endswith("/git/commits"):
                if self.move_after_commit:
                    self.parents["main"] = "6" * 40
                return 201, {"sha": "5" * 40}
            if path.endswith("/git/refs"):
                return (422, {}) if self.race_ref else (201, {})
            if path.endswith("/pulls"):
                return (500, {}) if self.fail_pull else (201, {"number": 123})
            if "/labels" in path:
                return 200, []
        raise AssertionError((method, path, body))

    @property
    def writes(self):
        return [(method, path, body) for method, path, body in self.calls if method != "GET"]


class ReceiverTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.env = {"GITHUB_REF": "refs/heads/main", "GITHUB_REPOSITORY": REPO,
                    "GITHUB_EVENT_NAME": "schedule", "RUNNER_TEMP": self.temp.name,
                    "GITHUB_OUTPUT": str(Path(self.temp.name) / "outputs")}
        self.registry = Registry()
        self.source = SourceApi(self.registry)
        self.target = TargetApi()
        self.profile = profile()

    def plan(self):
        return r.run_plan(self.env, self.source, self.registry, self.target, self.profile)

    def rejected(self, function, message=None):
        return self.assertRaisesRegex(r.UpdateError, message or ".+")

    def test_end_to_end_atomic_two_file_main_dev(self):
        result = self.plan()
        self.assertEqual("proposals", result["result"])
        self.assertEqual([], self.target.writes)
        self.assertEqual(0, r.run_propose(self.env, self.target, self.profile))
        trees = [body for _, path, body in self.target.writes if path.endswith("/git/trees")]
        self.assertEqual(2, len(trees))
        for tree in trees:
            self.assertEqual(["Dockerfile", "Dockerfile_internal"], [entry["path"] for entry in tree["tree"]])
            self.assertTrue(all(entry["mode"] == "100755" for entry in tree["tree"]))
        commits = [body for _, path, body in self.target.writes if path.endswith("/git/commits")]
        self.assertEqual([[SHA], ["3" * 40]], [body["parents"] for body in commits])
        self.assertTrue(all(body["message"].startswith("[SF-2942] fix(deps): ") for body in commits))
        refs = [body["ref"] for _, path, body in self.target.writes if path.endswith("/git/refs")]
        self.assertTrue(all(ref.startswith("refs/heads/feature/20261027_SF-2942_BOT_alpine-base-image-") for ref in refs))
        for original in self.target.data.values():
            updated = original.replace(OLD.encode(), self.registry.digest.encode())
            self.assertIn(updated, self.target.blobs.values())
        self.assertFalse(any(method in ("PATCH", "PUT", "DELETE") for method, _, _ in self.target.calls))

    def test_dry_run_never_writes_plan_or_refs(self):
        self.env["DRY_RUN"] = "true"
        self.assertEqual("dry-run", self.plan()["result"])
        self.assertFalse(r._plan_path(self.env).exists())
        self.assertEqual([], self.target.writes)
        with self.rejected(r.run_propose):
            r.run_propose(self.env, self.target, self.profile)

    def test_noop_current(self):
        self.target.data = {key: value.replace(OLD.encode(), self.registry.digest.encode()) for key, value in self.target.data.items()}
        self.target.blobs = {r._git_blob_sha(value): value for value in self.target.data.values()}
        self.assertEqual("noop", self.plan()["result"])
        self.assertFalse(r._plan_path(self.env).exists())

    def test_existing_closed_or_open_pr_is_untouched(self):
        branch = r.branch_for("alpine", self.registry.digest, "main", publication(self.registry), "BOT")
        self.profile = profile(branches=("main",))
        for state in ("open", "closed"):
            self.target.pulls = [{"state": state, "head": {"ref": branch, "repo": {"full_name": REPO}}, "base": {"ref": "main"}}]
            self.assertEqual("noop", self.plan()["result"])
        self.assertEqual([], self.target.writes)

    def test_orphan_ref_fails_before_write(self):
        branch = r.branch_for("alpine", self.registry.digest, "main", publication(self.registry), "BOT")
        self.target.refs = [{"ref": "refs/heads/" + branch}]
        with self.rejected(self.plan, "targets"):
            self.plan()
        self.assertEqual([], self.target.writes)

    def test_pause_no_network(self):
        self.profile["paused"] = True
        self.assertEqual("paused", self.plan()["result"])
        self.assertEqual([], self.source.calls + self.registry.calls + self.target.calls)

    def test_target_pause(self):
        self.profile["targets"][1]["paused"] = True
        result = self.plan()
        self.assertEqual(["proposal", "paused"], [item["status"] for item in result["decisions"]])

    def test_suppression_no_fallback(self):
        self.profile["suppressed"] = [self.registry.digest]
        self.assertEqual("suppressed", self.plan()["result"])
        self.assertEqual([], self.registry.calls + self.target.calls)

    def test_one_invalid_target_prevents_all_proposals(self):
        self.target.data["Dockerfile_internal"] = b"FROM scratch\n"
        self.target.blobs = {r._git_blob_sha(value): value for value in self.target.data.values()}
        with self.rejected(self.plan):
            self.plan()
        self.assertFalse(r._plan_path(self.env).exists())

    def test_mismatched_frontend_pins(self):
        self.target.data["Dockerfile_internal"] = self.target.data["Dockerfile_internal"].replace(OLD.encode(), NEW.encode())
        self.target.blobs = {r._git_blob_sha(value): value for value in self.target.data.values()}
        with self.rejected(self.plan):
            self.plan()

    def test_symlink_rejected(self):
        self.target.mode = "120000"
        with self.rejected(self.plan):
            self.plan()

    def test_stale_parent_no_writes(self):
        self.plan()
        self.target.parents = {"main": "7" * 40, "dev": "8" * 40}
        self.assertEqual(2, r.run_propose(self.env, self.target, self.profile))
        self.assertEqual([], self.target.writes)

    def test_target_moves_during_prepare_no_ref(self):
        self.profile = profile(branches=("main",))
        self.plan()
        self.target.move_after_commit = True
        self.assertEqual(1, r.run_propose(self.env, self.target, self.profile))
        self.assertFalse(any(path.endswith("/git/refs") for _, path, _ in self.target.writes))

    def test_concurrent_ref_is_not_adopted(self):
        self.plan()
        self.target.race_ref = True
        self.assertEqual(2, r.run_propose(self.env, self.target, self.profile))
        self.assertFalse(any(path.endswith("/pulls") for _, path, _ in self.target.writes))

    def test_pull_failure_leaves_ref_for_manual_reconciliation(self):
        self.plan()
        self.target.fail_pull = True
        self.assertEqual(2, r.run_propose(self.env, self.target, self.profile))
        self.assertTrue(any(path.endswith("/git/refs") for _, path, _ in self.target.writes))
        self.assertFalse(any(method == "DELETE" for method, _, _ in self.target.calls))

    def test_plan_path_tamper_rejected_before_writes(self):
        self.plan()
        path = r._plan_path(self.env)
        data = json.loads(path.read_bytes())
        data["targets"][0]["files"][0]["path"] = ".github/workflows/evil.yml"
        path.write_bytes(encoded(data))
        with self.rejected(r.run_propose):
            r.run_propose(self.env, self.target, self.profile)
        self.assertEqual([], self.target.writes)

    def test_run_failure_after_publication_is_eligible(self):
        found = r.find_publication(self.source, r.FAMILIES["alpine"], REGISTRY, [])
        self.assertEqual(self.registry.digest, found["digest"])

    def test_failed_or_partial_publish_is_not_eligible(self):
        for key in ("conclusion", "steps"):
            source = SourceApi(self.registry)
            source.jobs[42][key] = "failure" if key == "conclusion" else []
            with self.rejected(r.find_publication):
                r.find_publication(source, r.FAMILIES["alpine"], REGISTRY, [])

    def test_wrong_job_attempt_rejected(self):
        self.source.jobs[42]["run_attempt"] = 2
        with self.rejected(self.plan, "does not belong"):
            self.plan()

    def test_notifier_only_retry_aggregate2_publish1_notifier2(self):
        self.source.runs[0]["run_attempt"] = 2
        notifier = dict(self.source.jobs[42], id=100, name="notify-frontend", run_attempt=2)
        self.source.jobs[42] = [self.source.jobs[42], notifier]
        result = self.plan()
        self.assertEqual("1", result["publication"]["run_attempt"])
        self.assertTrue(any("image-version-manifest-alpine-42-1" in path for _, path, _ in self.source.calls))

    def test_real_publish_retry_aggregate2_publish2_artifact2(self):
        self.source.runs[0]["run_attempt"] = 2
        old = copy.deepcopy(self.source.jobs[42])
        self.source.jobs[42].update(run_attempt=2, id=100)
        self.source.jobs[42] = [old, self.source.jobs[42]]
        self.source.artifact["name"] = "image-version-manifest-alpine-42-2"
        self.source.evidence["run_attempt"] = "2"
        self.assertEqual("2", self.plan()["publication"]["run_attempt"])

    def test_job_attempt3_aggregate2_rejected(self):
        self.source.runs[0]["run_attempt"] = 2
        self.source.jobs[42]["run_attempt"] = 3
        with self.rejected(self.plan, "does not belong"):
            self.plan()

    def test_real_newer_failed_partial_or_ambiguous_attempt_never_uses_old_artifact(self):
        for mode in ("failed", "partial", "ambiguous"):
            self.source = SourceApi(self.registry)
            self.source.runs[0]["run_attempt"] = 2
            old = self.source.jobs[42]
            newer = dict(copy.deepcopy(old), id=100, run_attempt=2)
            if mode == "failed":
                newer["conclusion"] = "failure"
            elif mode == "partial":
                newer["steps"][0]["conclusion"] = "failure"
            self.source.jobs[42] = [old, newer] + ([dict(newer, id=101)] if mode == "ambiguous" else [])
            with self.rejected(self.plan):
                self.plan()
            self.assertFalse(any(method == "DOWNLOAD" for method, _, _ in self.source.calls))

    def test_duplicate_matching_artifacts_rejected(self):
        self.source.artifact = [self.source.artifact, dict(self.source.artifact, id=56)]
        with self.rejected(self.plan, "ambiguous"):
            self.plan()

    def test_unknown_current_pin_without_bootstrap_fails_before_write(self):
        for target in self.profile["targets"]:
            target["bootstrap"] = []
        with self.rejected(self.plan, "targets"):
            self.plan()
        self.assertEqual([], self.target.writes)
        self.assertFalse(r._plan_path(self.env).exists())

    def test_bootstrap_exact_target_digest_and_time_bound(self):
        target = self.profile["targets"][0]
        pub = publication(self.registry)
        for bootstrap in ([], [{"digest": NEW, "published_before": "2026-10-26T00:00:00Z"}],
                          [{"digest": OLD, "published_before": pub["created_at"]}]):
            target["bootstrap"] = bootstrap
            decision = r.analyze_target(self.target, REPO, self.profile, r.FAMILIES["alpine"], target, pub)
            self.assertEqual("error", decision["status"])

    def test_current_provenance_older_newer_and_same_source_rebuilds(self):
        pub = publication(self.registry)
        target = self.profile["targets"][0]
        target["bootstrap"] = []
        for created, run_number, attempt, expected in (
                ("2026-10-26T00:00:00Z", "41", "1", "proposal"),
                ("2026-10-28T00:00:00Z", "43", "1", "noop-newer"),
                (pub["created_at"], pub["run_number"], "2", "noop-newer")):
            current = dict(pub, created_at=created, run_number=run_number, run_attempt=attempt)
            with patch.object(r, "current_pin_order", return_value=r.publication_order(current)):
                decision = r.analyze_target(self.target, REPO, self.profile, r.FAMILIES["alpine"], target, pub, self.source)
            self.assertEqual(expected, decision["status"])
        self.assertEqual([], self.target.writes)

    def test_current_provenance_expired_or_unknown_fails(self):
        for expired in (False, True):
            self.source.artifact["expired"] = expired
            with self.rejected(r.current_pin_order):
                r.current_pin_order(self.source, r.FAMILIES["alpine"], REGISTRY, OLD)

    def test_current_pin_provenance_resolves_real_source_evidence(self):
        order = r.current_pin_order(self.source, r.FAMILIES["alpine"], REGISTRY, self.registry.digest)
        self.assertEqual(r.publication_order(publication(self.registry)), order)

    def test_same_run_rebuild_provenance_uses_exact_older_attempt_artifact(self):
        self.source.runs[0]["run_attempt"] = 2
        old = copy.deepcopy(self.source.jobs[42])
        self.source.jobs[42] = [old, dict(copy.deepcopy(old), id=100, run_attempt=2)]
        newer = dict(publication(self.registry), digest=NEW, run_attempt="2")
        def evidence(api, family, run, job, registry):
            return newer if job["run_attempt"] == 2 else publication(self.registry)
        with patch.object(r, "_read_evidence", side_effect=evidence) as read:
            order = r.current_pin_order(self.source, r.FAMILIES["alpine"], REGISTRY, self.registry.digest)
        self.assertEqual(1, order[-1])
        self.assertEqual([2, 1], [call.args[3]["run_attempt"] for call in read.call_args_list])
        self.assertLess(order, r.publication_order(newer))

    def test_main_dev_have_independent_current_histories(self):
        pub = publication(self.registry)
        for target in self.profile["targets"]:
            target["bootstrap"] = []
        with patch.object(r, "current_pin_order", side_effect=[
                ("2026-10-26T00:00:00Z", 41, 41, 1), ("2026-10-28T00:00:00Z", 43, 43, 1)]):
            decisions = [r.analyze_target(self.target, REPO, self.profile, r.FAMILIES["alpine"], target, pub, self.source)
                         for target in self.profile["targets"]]
        self.assertEqual(["proposal", "noop-newer"], [decision["status"] for decision in decisions])

    def test_governed_branch_date_is_source_creation_not_retry_date(self):
        pub = dict(publication(self.registry), completed_at="2026-11-01T10:00:00Z", run_attempt="2")
        self.assertEqual("feature/20261027_SF-2942_BOT_alpine-base-image-" + self.registry.digest[7:] + "-main",
                         r.branch_for("alpine", self.registry.digest, "main", pub, "BOT"))

    def test_old_rerun_cannot_displace_new_run(self):
        old = copy.deepcopy(self.source.runs[0])
        old.update(id=41, run_number=41, created_at="2026-10-26T00:00:00Z")
        self.source.runs.append(old)
        job = copy.deepcopy(self.source.jobs[42])
        job.update(run_id=41, completed_at="2026-10-28T00:00:00Z")
        self.source.jobs[41] = job
        self.assertEqual("42", r.find_publication(self.source, r.FAMILIES["alpine"], REGISTRY, [])["run_id"])

    def test_malformed_newest_evidence_never_falls_back(self):
        self.source.evidence["source"] = "f" * 40
        with self.rejected(self.plan, "does not match"):
            self.plan()
        self.assertEqual([], self.target.calls)

    def test_artifact_expired_missing_or_mismatched(self):
        for mutation in ({"expired": True}, {"name": "wrong"}, {"workflow_run": {"id": 999}}):
            self.source = SourceApi(self.registry)
            self.source.artifact.update(mutation)
            with self.rejected(self.plan):
                self.plan()

    def test_artifact_identity_fields_rejected(self):
        for field in ("repository", "reference", "source", "run_id", "run_attempt", "digest"):
            self.source = SourceApi(self.registry)
            self.source.evidence[field] = "invalid"
            with self.rejected(self.plan):
                self.plan()

    def test_bad_zip_rejected(self):
        self.source.archive_override = b"not zip"
        with self.rejected(self.plan, "archive"):
            self.plan()

    def test_registry_index_and_single_manifest(self):
        for index in (True, False):
            registry = Registry(index=index)
            result = r.verify_registry_image(registry, r.FAMILIES["alpine"], publication(registry))
            self.assertEqual("alpine", result["runtime"])

    def test_manifest_config_tag_hash_tamper(self):
        for mode in ("manifest", "config", "tag"):
            registry = Registry()
            pub = publication(registry)
            if mode == "manifest":
                pub["digest"] = NEW
            elif mode == "config":
                registry.config += b" "
            else:
                registry.body += b" "
            with self.rejected(r.verify_registry_image):
                r.verify_registry_image(registry, r.FAMILIES["alpine"], pub)

    def test_aspnet10_only_and_nonroot(self):
        for version, user, accepted in (("10.0.4", "biognosys", True), ("9.0.9", "biognosys", False),
                                        ("11.0.0", "biognosys", False), ("10.0.4", "root", False)):
            registry = Registry("aspnet", version, user)
            if accepted:
                self.assertEqual("aspnet 10.0.4", r.verify_registry_image(registry, r.FAMILIES["aspnet"], publication(registry))["runtime"])
            else:
                with self.rejected(r.verify_registry_image):
                    r.verify_registry_image(registry, r.FAMILIES["aspnet"], publication(registry))

    def test_service_preserves_common_sdk_user_and_entrypoint(self):
        data = dockerfile("aspnet")
        patched, current = r.patch_dockerfile(data, {"kind": "registry-from", "stage": "base"}, "aspnet", REGISTRY, NEW)
        self.assertEqual(OLD, current)
        self.assertEqual(data.replace(b"aspnet@" + OLD.encode(), b"aspnet@" + NEW.encode()), patched)

    def test_tgf_arg_layout_preserves_every_other_byte(self):
        data = (b"ARG BUILD_CONFIGURATION=Release\nARG ALPINE_BASE_IMAGE=" + REGISTRY.encode()
                + b"/base-images/alpine@" + OLD.encode() + b"\nFROM ${ALPINE_BASE_IMAGE} AS base\n"
                  b"FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build\nFROM base AS final\nUSER biognosys\n")
        patched, _ = r.patch_dockerfile(data, {"kind": "arg-default", "arg": "ALPINE_BASE_IMAGE", "stage": "base"}, "alpine", REGISTRY, NEW)
        self.assertEqual(data.replace(OLD.encode(), NEW.encode()), patched)

    def test_ambiguous_docker_syntax_rejected(self):
        data = dockerfile()
        cases = [b"# escape=`\n" + data, data + b"\nFROM ${IMAGE_REGISTRY}/base-images/alpine@" + OLD.encode(),
                 data.replace(b"ARG IMAGE_REGISTRY=", b"ARG IMAGE_REGISTRY\nARG IMAGE_REGISTRY="),
                 data.replace(b"FROM ${", b"FROM --platform=linux/amd64 ${"),
                 data + b"\nRUN <<EOF\nFROM scratch\nEOF\n", data.replace(b"\r\n", b"\x0b")]
        for candidate in cases:
            with self.rejected(r.patch_dockerfile):
                r.patch_dockerfile(candidate, {"kind": "registry-from", "stage": None}, "alpine", REGISTRY, NEW)

    def test_duplicate_base_stage_rejected(self):
        with self.rejected(r.patch_dockerfile):
            r.patch_dockerfile(dockerfile("aspnet") + b"\nFROM scratch AS base\n",
                               {"kind": "registry-from", "stage": "base"}, "aspnet", REGISTRY, NEW)

    def test_exact_dispatch_contract(self):
        event = {"action": r.EVENT_TYPE, "client_payload": {"schema": 1, "image": "alpine",
                 "source_repo": "Biognosys/bgsg.base-images", "run_id": "42", "run_attempt": "1"}}
        path = Path(self.temp.name) / "event.json"
        self.env.update(GITHUB_EVENT_NAME="repository_dispatch", GITHUB_EVENT_PATH=str(path))
        path.write_bytes(encoded(event))
        self.assertEqual("42", r.load_hint(self.env, "alpine")["run_id"])
        for field, value in (("schema", True), ("run_id", 42), ("run_attempt", "1\n::error::"),
                             ("image", "aspnet"), ("source_repo", "attacker/source"), ("path", "Dockerfile")):
            bad = copy.deepcopy(event)
            bad["client_payload"][field] = value
            path.write_bytes(encoded(bad))
            with self.rejected(r.load_hint):
                r.load_hint(self.env, "alpine")

    def test_untrusted_execution_context_rejected(self):
        for field, value in (("GITHUB_REF", "refs/heads/dev"), ("GITHUB_REPOSITORY", "attacker/fork")):
            env = dict(self.env, **{field: value})
            with self.rejected(r.run_plan):
                r.run_plan(env, self.source, self.registry, self.target, self.profile)
        self.assertEqual([], self.source.calls)

    def test_duplicate_json_and_malformed_profile_fail_closed(self):
        with self.rejected(r._strict_json):
            r._strict_json(b'{"schema":1,"schema":1}', "profile")
        raw = {"schema": 1, "repository": REPO, "family": "alpine", "registry": REGISTRY,
               "public": False, "paused": False, "suppressed_digests": [{}], "targets": []}
        with self.rejected(r.parse_profile):
            r.parse_profile(raw)

    def test_public_evidence_has_no_private_source_links(self):
        pub = publication(self.registry)
        body = r.pull_request_body(self.profile, pub, "main", [{"path": "Dockerfile"}], OLD, True)
        self.assertNotIn("github.com/Biognosys/bgsg.base-images", body)
        self.assertIn(OLD, body)


class FakeHttp:
    def __init__(self, responses):
        self.responses = list(responses)
        self.calls = []

    def fetch(self, method, url, headers=None, body=None, max_bytes=None):
        self.calls.append((method, url, headers or {}, body, max_bytes))
        if not self.responses:
            raise AssertionError("unexpected request")
        return self.responses.pop(0)


class AdapterTests(unittest.TestCase):
    def test_artifact_redirect_does_not_replay_authorization(self):
        http = FakeHttp([(302, {"location": "https://artifacts.example.com/evidence?sig=example"}, b""), (200, {}, b"zip")])
        api = r.GitHubApi("source-secret", http)
        self.assertEqual(b"zip", api.download("repos/owner/repo/actions/artifacts/1/zip", 100))
        self.assertEqual("Bearer source-secret", http.calls[0][2]["Authorization"])
        self.assertNotIn("Authorization", http.calls[1][2])

    def test_invalid_or_multiple_redirects_rejected(self):
        for location in ("http://artifacts.example.com/file", "https://user:pass@artifacts.example.com/file", "https://127.0.0.1/file"):
            http = FakeHttp([(302, {"location": location}, b"")])
            with self.assertRaises(r.UpdateError):
                r.GitHubApi("secret", http).download("artifact", 100)
            self.assertEqual(1, len(http.calls))
        http = FakeHttp([(302, {"location": "https://artifacts.example.com/file"}, b""), (302, {}, b"")])
        with self.assertRaises(r.UpdateError):
            r.GitHubApi("secret", http).download("artifact", 100)

    def test_registry_pull_scope_and_credential_boundaries(self):
        http = FakeHttp([(401, {"www-authenticate": f'Bearer realm="https://{REGISTRY}/oauth2/token",service="{REGISTRY}"'}, b""),
                         (200, {}, b'{"token":"pull-token"}'), (200, {"content-type": r.IMAGE_TYPES[0]}, b"manifest"),
                         (307, {"location": "https://blobs.example.com/config"}, b""), (200, {}, b"config")])
        client = r.RegistryClient(REGISTRY, "reader", "pull-password", http)
        client.get_manifest("base-images/alpine", OLD)
        self.assertEqual(b"config", client.get_blob("base-images/alpine", NEW, 100))
        query = urllib.parse.parse_qs(urllib.parse.urlsplit(http.calls[1][1]).query)
        self.assertEqual(["repository:base-images/alpine:pull"], query["scope"])
        self.assertTrue(http.calls[1][2]["Authorization"].startswith("Basic "))
        self.assertEqual("Bearer pull-token", http.calls[2][2]["Authorization"])
        self.assertNotIn("Authorization", http.calls[4][2])

    def test_foreign_token_realm_never_gets_basic_credential(self):
        http = FakeHttp([(401, {"www-authenticate": 'Bearer realm="https://evil.example.com/token"'}, b"")])
        with self.assertRaises(r.UpdateError):
            r.RegistryClient(REGISTRY, "reader", "secret", http).get_manifest("base-images/alpine", OLD)
        self.assertEqual(1, len(http.calls))
        self.assertNotIn("Authorization", http.calls[0][2])

    def test_http_reads_retry_but_writes_do_not(self):
        for method, count in (("GET", 3), ("POST", 1)):
            http = r.Http(sleep=lambda _: None)
            with patch.object(http, "_once", return_value=(503, {}, b"")) as once:
                self.assertEqual(503, http.fetch(method, "https://api.github.com/test")[0])
                self.assertEqual(count, once.call_count)


if __name__ == "__main__":
    unittest.main()
