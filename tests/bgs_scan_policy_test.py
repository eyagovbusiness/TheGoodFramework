import copy
import json
import os
import subprocess
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
POLICY = os.path.join(HERE, "..", "scripts", "bgs_scan_policy.py")
FIXTURE = os.path.join(HERE, "fixtures", "trivy-alpine-clean.json")  # trimmed real Trivy 0.75 report
sys.path.insert(0, os.path.join(HERE, "..", "scripts"))
import bgs_scan_policy as policy  # noqa: E402

IMAGE_ID = "sha256:" + "a" * 64


def vuln(severity="HIGH", fixed=None, vuln_id="CVE-2026-0001"):
    entry = {"VulnerabilityID": vuln_id, "PkgName": "musl", "InstalledVersion": "1.2.5", "Severity": severity}
    if fixed is not None:
        entry["FixedVersion"] = fixed
    return entry


def os_result(**changes):
    result = {"Target": "image (alpine 3.24.2)", "Class": "os-pkgs", "Type": "alpine", "Packages": [{"Name": "musl", "Version": "1.2.5"}]}
    result.update(changes)
    return result


def report(*extra, **changes):
    data = {
        "SchemaVersion": 2,
        "ArtifactType": "container_image",
        "Metadata": {"ImageID": IMAGE_ID, "OS": {"Family": "alpine", "Name": "3.24.2"}},
        "Results": [os_result()] + list(extra),
    }
    data.update(copy.deepcopy(changes))
    return data


def gobinary(*versions):
    return {"Target": "usr/bin/tool", "Class": "lang-pkgs", "Type": "gobinary", "Packages": [{"Name": "stdlib", "Version": v} for v in versions]}


def real():
    return policy.load(FIXTURE)


class PolicyTest(unittest.TestCase):
    def failing(self, data, fragment, image_id=IMAGE_ID):
        errors, fixable, _ = policy.evaluate(data, image_id)
        self.assertTrue(any(fragment in line for line in errors + fixable), (fragment, errors, fixable))

    def test_clean_report_passes(self):
        self.assertEqual(policy.evaluate(report(), IMAGE_ID), ([], [], {}))

    def test_real_alpine_report_passes_and_is_bound_to_its_image(self):
        data = real()
        real_id = data["Metadata"]["ImageID"]
        self.assertEqual(policy.evaluate(data, real_id), ([], [], {}))
        self.failing(data, "Metadata.ImageID", image_id=IMAGE_ID)

    def test_real_report_mutations_are_rejected(self):
        real_id = real()["Metadata"]["ImageID"]
        mutations = {
            "Metadata.OS": lambda d: d["Metadata"].pop("OS"),
            "exactly one Alpine OS": lambda d: d["Results"].pop(0),
            "each with a version": lambda d: d["Results"][0]["Packages"][0].update(Version=""),
            "unsupported OS report": lambda d: d["Results"].append(os_result(Type="debian")),
            "scanner reported an error": lambda d: d.update(Errors=["db download failed"]),
            "carries a scanner error": lambda d: d["Results"][1].update(Error="parse failed"),
            "UNKNOWN CVE-2026-0001": lambda d: d["Results"][0].update(Vulnerabilities=[vuln("UNKNOWN", "9")]),
        }
        for fragment, mutate in mutations.items():
            with self.subTest(fragment=fragment):
                data = real()
                mutate(data)
                self.failing(data, fragment, image_id=real_id)

    def test_unfixed_findings_are_reported_not_blocking(self):
        data = report()
        data["Results"][0]["Vulnerabilities"] = [vuln("CRITICAL"), vuln("LOW", "", "CVE-2")]
        self.assertEqual(policy.evaluate(data, IMAGE_ID), ([], [], {"CRITICAL": 1, "LOW": 1}))

    def test_fixable_finding_blocks_at_every_severity(self):
        for severity in policy.SEVERITIES:
            with self.subTest(severity=severity):
                data = report()
                data["Results"][0]["Vulnerabilities"] = [vuln(severity, "1.2.6")]
                self.failing(data, f"{severity} CVE-2026-0001")

    def test_all_findings_are_collected(self):
        data = report(gobinary(""))
        data["Results"][0]["Vulnerabilities"] = [vuln("LOW", "2"), vuln("HIGH", "3", "CVE-2")]
        errors, fixable, _ = policy.evaluate(data, IMAGE_ID)
        self.assertEqual((len(errors), len(fixable)), (1, 2))

    def test_wrong_or_missing_image_id_is_rejected(self):
        for image_id in ("sha256:" + "b" * 64, None, "latest"):
            with self.subTest(image_id=image_id):
                data = report()
                data["Metadata"]["ImageID"] = image_id
                self.failing(data, "Metadata.ImageID")
        data = report()
        del data["Metadata"]["ImageID"]
        self.failing(data, "Metadata.ImageID")

    def test_missing_or_wrong_os_is_rejected(self):
        for os_info in (None, {}, {"Family": "debian", "Name": "12"}, {"Family": "alpine"}, {"Family": "alpine", "Name": "  "}):
            with self.subTest(os_info=os_info):
                data = report()
                if os_info is None:
                    del data["Metadata"]["OS"]
                else:
                    data["Metadata"]["OS"] = os_info
                self.failing(data, "Metadata.OS")

    def test_os_inventory_must_be_single_complete_and_alpine(self):
        cases = {
            "exactly one Alpine OS": [[], [os_result(), os_result()]],
            "each with a version": [[os_result(Packages=None)], [os_result(Packages=[])], [os_result(Packages=[{"Name": "musl"}])], [os_result(Packages=[{"Name": "musl", "Version": " "}])]],
            "unsupported OS report": [[os_result(Type="debian"), os_result()]],
        }
        for fragment, variants in cases.items():
            for results in variants:
                with self.subTest(fragment=fragment, results=results):
                    self.failing(report(Results=results), fragment)
        data = report()
        del data["Results"]
        self.failing(data, "Results must be a list")

    def test_unsupported_result_classes_are_rejected(self):
        self.failing(report({"Target": "t", "Class": "secret", "Type": "secret"}), "unsupported class")

    def test_scanner_errors_are_rejected_when_non_empty_only(self):
        for key in ("Error", "Errors"):
            for value in ("boom", ["boom"], {"a": 1}, 1, " "):
                with self.subTest(key=key, value=value):
                    self.failing(report(**{key: value}), "scanner reported an error")
                    self.failing(report(os_result(**{key: value})), "scanner error")
            for value in (None, "", [], {}):
                with self.subTest(key=key, empty=value):
                    self.assertEqual(policy.evaluate(report(**{key: value}), IMAGE_ID)[0], [])

    def test_whitespace_only_text_is_rejected(self):
        self.failing(report(os_result(Target=" ")), "needs Target")
        self.failing(report(os_result(Type="\t")), "needs Target")
        self.failing(report(os_result(Packages=[{"Name": " ", "Version": "1"}])), "named packages")
        data = report()
        data["Results"][0]["Vulnerabilities"] = [dict(vuln(), PkgName=" "), vuln("LOW", " ")]
        errors, _, _ = policy.evaluate(data, IMAGE_ID)
        self.assertEqual(len(errors), 2)

    def test_go_binary_inventory_is_strict_with_no_exception(self):
        for versions in ((), ("",), (" ",), ("(devel)",), ("1.26.0", "")):
            with self.subTest(versions=versions):
                self.failing(report(gobinary(*versions)), "Go binary inventory")
        self.assertEqual(policy.evaluate(report(gobinary("1.26.9")), IMAGE_ID)[0], [])

    def test_scalars_must_have_the_exact_type(self):
        self.failing(report(SchemaVersion=True), "SchemaVersion")
        self.failing(report(SchemaVersion=2.0), "SchemaVersion")
        self.failing(report(ArtifactType=True), "ArtifactType")
        for bad in (True, 1, ["1.2.6"]):
            data = report()
            data["Results"][0]["Vulnerabilities"] = [vuln("HIGH", bad)]
            self.failing(data, "malformed vulnerability")
        for entry in (dict(vuln(), Severity=True), dict(vuln(), PkgName=False), "CVE", dict(vuln(), Severity="SEVERE")):
            data = report()
            data["Results"][0]["Vulnerabilities"] = [entry]
            self.failing(data, "malformed vulnerability")
        data = report()
        data["Results"][0]["Vulnerabilities"] = {"a": 1}
        self.failing(data, "Vulnerabilities must be a list")

    def test_non_object_report_is_rejected(self):
        self.failing([], "not a JSON object")


class CliTest(unittest.TestCase):
    def run_policy(self, text, image_id=IMAGE_ID):
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
            handle.write(text)
        self.addCleanup(os.unlink, handle.name)
        return subprocess.run([sys.executable, POLICY, handle.name, "--image-id", image_id], capture_output=True, text=True)

    def test_exit_codes(self):
        self.assertEqual(self.run_policy(json.dumps(report())).returncode, 0)
        blocked = report()
        blocked["Results"][0]["Vulnerabilities"] = [vuln("LOW", "9")]
        result = self.run_policy(json.dumps(blocked))
        self.assertEqual(result.returncode, 1)
        self.assertIn("BLOCKING FIXABLE", result.stdout)
        self.assertEqual(self.run_policy(json.dumps(report()), "sha256:" + "b" * 64).returncode, 1)

    def test_real_report_passes_through_the_cli(self):
        with open(FIXTURE, encoding="utf-8") as handle:
            text = handle.read()
        self.assertEqual(self.run_policy(text, real()["Metadata"]["ImageID"]).returncode, 0)

    def test_unreadable_reports_fail_closed(self):
        valid = json.dumps(report())
        for text in ("", "{", "[]", valid.replace('"SchemaVersion": 2', '"SchemaVersion": 2, "SchemaVersion": 2'), valid.replace('"SchemaVersion": 2', '"SchemaVersion": NaN')):
            with self.subTest(text=text[:40]):
                self.assertEqual(self.run_policy(text).returncode, 1)
        self.assertEqual(subprocess.run([sys.executable, POLICY, "/nonexistent.json", "--image-id", IMAGE_ID], capture_output=True).returncode, 1)

    def test_oversized_reports_are_refused(self):
        original = policy.MAX_BYTES
        policy.MAX_BYTES = 100
        self.addCleanup(setattr, policy, "MAX_BYTES", original)
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
            handle.write("{}" + " " * 200)  # valid JSON, so only the size bound can reject it
        self.addCleanup(os.unlink, handle.name)
        with self.assertRaises(ValueError):
            policy.load(handle.name)

    def test_usage_errors(self):
        for args in ([], ["r.json"], ["r.json", "--image-id"], ["r.json", "--image-id", "latest"], ["r.json", "--other", IMAGE_ID]):
            with self.subTest(args=args):
                self.assertEqual(subprocess.run([sys.executable, POLICY, *args], capture_output=True).returncode, 2)


if __name__ == "__main__":
    unittest.main()
