"""Release authorization and atomic immutable-tag protocol, without network writes."""
import copy
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import bgs_image_release as release


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.report = Path(self.tmp.name)
        self.sha = "a" * 40
        self.payload = {"source_repo": release.SOURCE_REPOSITORY, "source_commit": self.sha,
                        "source_tag": "tgf-42.2", "source_image": "registry/image@sha256:" + "1" * 64,
                        "source_digest": "sha256:" + "1" * 64, "source_config_digest": "sha256:" + "2" * 64,
                        "source_run_id": "999", "source_run_attempt": "2"}
        self.rules = [{"type": name} for name in ("deletion", "non_fast_forward")]
        self.rules += [{"type": "pull_request", "parameters": {"required_approving_review_count": 1,
                       "require_code_owner_review": True, "dismiss_stale_reviews_on_push": True,
                       "required_review_thread_resolution": True}},
                       {"type": "required_status_checks", "parameters": {
                           "strict_required_status_checks_policy": True,
                           "required_status_checks": [{"context": name} for name in (
                               "build", "tests", "image-build-and-scan", "delivery-validation")]}}]
        refs = {"ref_name": {"include": ["refs/tags/bgs-image/**/*"], "exclude": []}}
        self.tag_rules = {
            1: {"conditions": refs, "rules": [{"type": "update"}, {"type": "deletion"}],
                "current_user_can_bypass": "never", "bypass_actors": []},
            2: {"conditions": refs, "rules": [{"type": "creation"}],
                "current_user_can_bypass": "always", "bypass_actors": [
                    {"actor_type": "User", "actor_id": 123, "bypass_mode": "always"}]}}
        self.pull = {"merged_at": "2026-10-22T12:00:00Z", "merge_commit_sha": self.sha,
                     "number": 99, "head": {"sha": "d" * 40}, "user": {"login": "author"},
                     "base": {"ref": "main", "repo": {"full_name": release.SOURCE_REPOSITORY}}}
        self.calls = []
        self.reviews = [{"state": "APPROVED", "commit_id": "d" * 40, "user": {"login": "reviewer"}}]
        self.collision = False
        self.moved = False
        self.ref_failure = False
        self.bad_readback = False
        self.annotation = None
        (self.report / "trivy-report.json").write_text('{}', encoding="utf-8")
        self.env = patch.dict(os.environ, {"GITHUB_ACTIONS": "true", "GH_TOKEN": "test-only"})
        self.env.start()
        self.addCleanup(self.env.stop)
        self.payload_patch = patch.object(release, "build_payload", return_value={"client_payload": self.payload})
        self.payload_patch.start()
        self.addCleanup(self.payload_patch.stop)

    def api(self, path, payload=None):
        self.calls.append((path, payload))
        if path == "rules/branches/main":
            return self.rules
        if path.startswith("commits/"):
            return [self.pull]
        if path.startswith("pulls/"):
            return self.reviews
        if path == "git/ref/heads/main":
            return {"object": {"sha": "b" * 40 if self.moved else self.sha}}
        if path == "rulesets?per_page=100":
            return [{"id": key, "target": "tag", "enforcement": rule.get("enforcement", "active")}
                    for key, rule in self.tag_rules.items()]
        if path.startswith("rulesets/"):
            return self.tag_rules[int(path.split("/")[1])]
        if path.startswith("git/matching-refs/"):
            return [{"ref": "existing"}] if self.collision else []
        if path == "git/tags":
            self.annotation = payload
            return {"sha": "c" * 40, "object": {"sha": self.sha, "type": "commit"}}
        if path == "git/refs":
            if self.ref_failure:
                raise release.ReleaseError("collision or denied")
            return {}
        if path.startswith("git/ref/tags/"):
            return {"object": {"sha": "c" * 40, "type": "tag"}}
        if path == "git/tags/" + "c" * 40:
            return {"object": {"sha": self.sha, "type": "commit"},
                    "message": "wrong" if self.bad_readback else self.annotation["message"]}
        self.fail("unexpected endpoint " + path)

    def run_release(self):
        release.release(self.report, self.api)

    def rejected(self):
        with self.assertRaises(release.ReleaseError):
            self.run_release()
        self.assertFalse((self.report / "release.json").exists())

    def test_release_binds_source_manifest_config_run_and_scan_in_annotated_tag(self):
        self.run_release()
        mapping = json.loads((self.report / "release.json").read_text())
        self.assertEqual(mapping["git_tag"], "bgs-image/tgf-42.2")
        self.assertEqual(mapping["source_digest"], self.payload["source_digest"])
        self.assertEqual(mapping["scan_report_sha256"], release.hashlib.sha256(b'{}').hexdigest())
        self.assertEqual(json.loads(self.annotation["message"]), mapping)
        self.assertEqual([p for p, body in self.calls if body], ["git/tags", "git/refs"])

    def test_missing_effective_main_rules_cannot_create_any_tag(self):
        self.rules.pop(0)
        self.rejected()
        self.assertFalse(any(body for _, body in self.calls))

    def test_review_and_all_named_checks_required(self):
        for edit in (lambda: self.rules[-2]["parameters"].update(required_approving_review_count=0),
                     lambda: self.rules[-1]["parameters"].update(required_status_checks=[])):
            with self.subTest(edit=edit):
                saved = copy.deepcopy(self.rules)
                edit()
                self.rejected()
                self.rules = saved

    def test_direct_push_and_non_main_pr_cannot_release(self):
        for edit in ({"merged_at": None}, {"merge_commit_sha": "b" * 40},
                     {"base": {"ref": "dev", "repo": {"full_name": release.SOURCE_REPOSITORY}}}):
            with self.subTest(edit=edit):
                saved = copy.deepcopy(self.pull)
                self.pull.update(edit)
                self.rejected()
                self.pull = saved

    def test_each_independent_validation_check_must_be_required(self):
        checks = self.rules[-1]["parameters"]["required_status_checks"]
        for missing in checks:
            with self.subTest(missing=missing["context"]):
                self.rules[-1]["parameters"]["required_status_checks"] = [item for item in checks if item != missing]
                self.rejected()
                self.assertFalse(any(body for _, body in self.calls))
        self.rules[-1]["parameters"]["required_status_checks"] = checks

    def test_main_movement_stops_release(self):
        self.moved = True
        self.rejected()

    def test_missing_stale_self_or_dismissed_approval_cannot_release(self):
        for reviews in ([], [{"state": "APPROVED", "commit_id": "e" * 40, "user": {"login": "reviewer"}}],
                        [{"state": "APPROVED", "commit_id": "d" * 40, "user": {"login": "author"}}],
                        self.reviews + [{"state": "DISMISSED", "commit_id": "d" * 40, "user": {"login": "reviewer"}}]):
            with self.subTest(reviews=reviews):
                self.reviews = reviews
                self.rejected()

    def test_immutable_tag_rules_cannot_have_bypass(self):
        self.tag_rules[1]["bypass_actors"] = [{"actor_type": "Integration", "bypass_mode": "always"}]
        self.rejected()

    def test_explicit_user_creation_bypass_is_authorized(self):
        self.run_release()
        self.assertTrue((self.report / "release.json").exists())

    def test_collaborator_hidden_bypass_lists_use_authenticated_capabilities(self):
        for rule in self.tag_rules.values():
            del rule["bypass_actors"]
        with patch("sys.stderr") as stderr:
            self.run_release()
        self.assertTrue((self.report / "release.json").exists())
        self.assertIn("global bypass list", "".join(str(c.args) for c in stderr.write.call_args_list))

    def test_missing_or_disallowed_creation_capability_fails_before_writes(self):
        for capability in (None, "never", "pull_requests_only", "exempt", "unexpected"):
            with self.subTest(capability=capability):
                self.tag_rules[2]["current_user_can_bypass"] = capability
                self.rejected()
                self.assertFalse(any(body for _, body in self.calls))

    def test_hidden_immutable_lists_do_not_authorize_unknown_or_bypassed_permissions(self):
        del self.tag_rules[1]["bypass_actors"]
        for capability in (None, "always", "pull_requests_only", "exempt", "unexpected"):
            with self.subTest(capability=capability):
                self.tag_rules[1]["current_user_can_bypass"] = capability
                self.rejected()
                self.assertFalse(any(body for _, body in self.calls))

    def test_combined_creation_update_bypass_fails_before_writes(self):
        self.tag_rules[2]["rules"].append({"type": "update"})
        self.rejected()
        self.assertFalse(any(body for _, body in self.calls))

    def test_missing_inactive_wrong_namespace_or_excluded_rule_fails_closed(self):
        for edit in (lambda: self.tag_rules.pop(1),
                     lambda: self.tag_rules[1].update(enforcement="disabled"),
                     lambda: self.tag_rules[1]["rules"].pop(),
                     lambda: self.tag_rules[1]["conditions"]["ref_name"].update(include=["refs/tags/other/**/*"]),
                     lambda: self.tag_rules[1]["conditions"]["ref_name"].update(exclude=["refs/tags/bgs-image/tgf-42.2"])):
            with self.subTest(edit=edit):
                saved = copy.deepcopy(self.tag_rules)
                edit()
                self.rejected()
                self.assertFalse(any(body for _, body in self.calls))
                self.tag_rules = saved

    def test_existing_tag_is_never_updated_even_for_same_source(self):
        self.collision = True
        self.rejected()
        self.assertFalse(any(body for _, body in self.calls))

    def test_atomic_ref_collision_preserves_failure_and_never_notifies(self):
        self.ref_failure = True
        self.rejected()
        self.assertEqual([p for p, body in self.calls if body], ["git/tags", "git/refs"])

    def test_readback_failure_never_records_success(self):
        self.bad_readback = True
        self.rejected()

    def test_missing_scan_cannot_create_tag(self):
        (self.report / "trivy-report.json").unlink()
        self.rejected()
        self.assertFalse(any(body for _, body in self.calls))

    def test_missing_release_credential_fails_closed(self):
        with patch.dict(os.environ, {"GH_TOKEN": ""}):
            self.rejected()


if __name__ == "__main__":
    unittest.main()
