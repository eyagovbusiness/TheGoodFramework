"""Repository profile/layout and workflow boundary regression checks (standard library only)."""
import importlib.util
import json
from pathlib import Path
import re
import unittest
import copy
from unittest.mock import patch
from test_base_image_receiver import Registry, SourceApi, TargetApi, OLD, NEW, REPO, REGISTRY, publication, profile

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("receiver_integration", ROOT / "scripts/base_image_receiver.py")
r = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(r)


class IntegrationTests(unittest.TestCase):
    def test_historical_failed_run_between_candidate_and_current_pin(self):
        registry = Registry()
        source = SourceApi(registry)
        candidate = source.runs[0]
        failed = dict(copy.deepcopy(candidate), id=41, run_number=41, created_at="2026-10-26T00:00:00Z")
        older = dict(copy.deepcopy(candidate), id=40, run_number=40, created_at="2026-10-25T00:00:00Z")
        source.runs += [failed, older]
        source.jobs[41] = dict(copy.deepcopy(source.jobs[42]), run_id=41, conclusion="failure")
        source.jobs[40] = dict(copy.deepcopy(source.jobs[42]), run_id=40)
        pub = publication(registry)
        def evidence(api, family, run, attempt, host):
            self.assertNotEqual(41, run["id"], "failed run artifacts must not be trusted")
            return dict(pub, digest=OLD if run["id"] == 40 else pub["digest"], created_at=run["created_at"],
                        run_id=str(run["id"]), run_number=str(run["run_number"]))
        config = profile(branches=("main",))
        config["targets"][0]["bootstrap"] = []
        with patch.object(r, "_read_evidence", side_effect=evidence):
            self.assertEqual("42", r.find_publication(source, r.FAMILIES["alpine"], REGISTRY, [])["run_id"])
            decision = r.analyze_target(TargetApi(), REPO, config, r.FAMILIES["alpine"], config["targets"][0], pub, source)
        self.assertEqual("proposal", decision["status"])

    def test_old_success_proves_current_pin_after_newer_same_run_failure(self):
        registry = Registry()
        source = SourceApi(registry)
        source.runs[0]["run_attempt"] = 2
        old = copy.deepcopy(source.jobs[42])
        source.jobs[42] = [old, dict(copy.deepcopy(old), id=100, run_attempt=2, conclusion="failure")]
        with patch.object(r, "_read_evidence", return_value=publication(registry)) as evidence:
            self.assertEqual(1, r.current_pin_order(source, r.FAMILIES["alpine"], REGISTRY, registry.digest)[-1])
            self.assertEqual([1], [call.args[3]["run_attempt"] for call in evidence.call_args_list])
        with self.assertRaises(r.UpdateError):
            r.find_publication(source, r.FAMILIES["alpine"], REGISTRY, [])

    def test_failed_incomplete_and_partial_never_supply_provenance(self):
        for mode in ("failure", "in_progress", "partial"):
            registry = Registry()
            source = SourceApi(registry)
            job = source.jobs[42]
            if mode == "failure":
                job["conclusion"] = "failure"
            elif mode == "in_progress":
                job["status"] = "in_progress"
            else:
                job["steps"][0]["conclusion"] = "failure"
            with patch.object(r, "_read_evidence") as evidence:
                with self.assertRaises(r.UpdateError):
                    r.current_pin_order(source, r.FAMILIES["alpine"], REGISTRY, registry.digest)
                evidence.assert_not_called()

    def test_cross_date_same_digest_prs_all_states_preserved(self):
        pub = publication(Registry())
        new = r.branch_for("alpine", NEW, "main", pub, "BOT")
        old = new.replace("20261027", "20261020")
        for state, merged in (("open", False), ("closed", False), ("closed", True)):
            api = TargetApi()
            api.pulls = [{"state": state, "merged_at": "2026-10-21T00:00:00Z" if merged else None,
                          "head": {"ref": old, "repo": {"full_name": REPO}}, "base": {"ref": "main"}}]
            api.refs = [{"ref": "refs/heads/" + old}]
            self.assertEqual("already-proposed", r.preflight(api, REPO, new))
            self.assertEqual([], api.writes)

    def test_cross_date_orphans_and_independent_digest_target(self):
        pub = publication(Registry())
        new = r.branch_for("alpine", NEW, "main", pub, "BOT")
        old = new.replace("20261027", "20261020")
        api = TargetApi()
        self.assertEqual("proceed", r.preflight(api, REPO, new))
        api.refs = [{"ref": "refs/heads/" + old}]
        with self.assertRaisesRegex(r.UpdateError, "without a pull request"):
            r.preflight(api, REPO, new)
        self.assertEqual("proceed", r.preflight(api, REPO, new.replace("-main", "-dev")))
        self.assertEqual("proceed", r.preflight(api, REPO, new.replace(NEW[7:], OLD[7:])))
        api.pulls = [{"head": {"ref": old, "repo": {"full_name": REPO}}, "base": {"ref": "main"}}]
        api.refs.append({"ref": "refs/heads/" + old.replace("20261020", "20261021")})
        with self.assertRaises(r.UpdateError):
            r.preflight(api, REPO, new)  # a PR on one date must not hide an orphan on another

    def test_complete_pr_and_branch_pagination_and_visible_bounds(self):
        branch = r.branch_for("alpine", NEW, "main", publication(Registry()), "BOT")
        matching = {"head": {"ref": branch.replace("20261027", "20261020"), "repo": {"full_name": REPO}}, "base": {"ref": "main"}}
        class Pages:
            def __init__(self, mode):
                self.mode, self.calls = mode, []
            def request(self, method, path, body=None):
                self.calls.append(path)
                page = int(path.rsplit("page=", 1)[1])
                if "/pulls?" in path:
                    unrelated = {"head": {"ref": "other", "repo": {"full_name": REPO}}, "base": {"ref": "main"}}
                    return 200, ([unrelated] * 100 if self.mode == "pull-bound" or page == 1 else [matching] if self.mode == "pr-second" else [])
                return 200, ([{"name": "unrelated"}] * 100 if self.mode == "ref-bound" or page == 1 else
                             [{"name": matching["head"]["ref"]}] if self.mode == "orphan-second" else [])
        for mode, expected in (("pr-second", "already-proposed"), ("empty-second", "proceed")):
            api = Pages(mode)
            self.assertEqual(expected, r.preflight(api, REPO, branch))
            self.assertTrue(any("branches?" in path and "page=2" in path for path in api.calls))
        for mode in ("orphan-second", "pull-bound", "ref-bound"):
            with self.assertRaises(r.UpdateError):
                r.preflight(Pages(mode), REPO, branch)

    def test_reviewed_profile_matches_real_main_layout(self):
        path = ROOT / "scripts/base_image_receiver_profile.json"
        if not path.exists():
            self.skipTest("canonical engine repository has no consumer profile")
        profile = r.load_profile(path)
        self.assertEqual("omicsflowinternal.azurecr.io", profile["registry"])
        self.assertEqual(["main"] if profile["public"] else ["main", "dev"],
                         [target["branch"] for target in profile["targets"]])
        for target in profile["targets"]:
            pins = set()
            for name in target["files"]:
                original = (ROOT / name).read_bytes()
                patched, old = r.patch_dockerfile(original, target["layout"], profile["family"], profile["registry"], "sha256:" + "e" * 64)
                pins.add(old)
                self.assertEqual(original.replace(("/base-images/" + profile["family"] + "@" + old).encode(),
                                                  ("/base-images/" + profile["family"] + "@sha256:" + "e" * 64).encode()), patched)
            self.assertEqual(1, len(pins))

    def test_workflow_read_before_write_boundary(self):
        path = ROOT / ".github/workflows/base-image-receiver.yml"
        if not path.exists():
            self.skipTest("canonical engine repository has no receiver workflow")
        text = path.read_text(encoding="utf-8")
        self.assertIn("types: [base-image-published-v1]", text)
        self.assertIn("schedule:", text)
        self.assertIn("default: true", text)
        self.assertIn("if: github.ref == 'refs/heads/main'", text)
        self.assertIn("ref: ${{ github.sha }}", text)
        self.assertIn("persist-credentials: false", text)
        self.assertNotIn("client_payload", text)
        self.assertNotIn("pull_request_target", text)
        validate = text.index("scripts/base_image_receiver.py validate-event")
        plan = text.index("scripts/base_image_receiver.py plan")
        token = text.index("GH_TOKEN: ${{ secrets.PAT_TOKEN }}")
        propose = text.index("scripts/base_image_receiver.py propose")
        self.assertLess(validate, plan)
        self.assertLess(plan, token)
        self.assertLess(token, propose)
        self.assertEqual(1, text.count("if: steps.plan.outputs.has_proposals == 'true'"))
        self.assertNotIn("create-github-app-token", text)
        self.assertIn("secrets.BASE_IMAGE_SOURCE_READ_TOKEN || secrets.PAT_TOKEN", text)
        self.assertIn("secrets.BASE_IMAGE_PULL_USERNAME || secrets.REG_USERNAME", text)
        self.assertNotIn("GH_SOURCE_TOKEN", text[token:])
        self.assertNotIn("REGISTRY_PULL_PASSWORD", text[token:])
        for action in re.findall(r"uses: (\S+)", text):
            self.assertRegex(action, r"@[a-f0-9]{40}$")
        global_permissions = text.split("permissions:", 1)[1].split("concurrency:", 1)[0]
        self.assertNotIn("write", global_permissions)


if __name__ == "__main__":
    unittest.main()
