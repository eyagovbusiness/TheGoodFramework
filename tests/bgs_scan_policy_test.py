import copy
import json
import os
import subprocess
import sys
import tempfile
import unittest

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
POLICY = os.path.join(ROOT, "scripts", "bgs_scan_policy.py")
sys.path.insert(0, os.path.join(ROOT, "scripts"))
import bgs_scan_policy as policy  # noqa: E402

IMAGE_ID = "sha256:" + "a" * 64


def vuln(severity="HIGH", fixed=None, vuln_id="CVE-2026-0001"):
    entry = {"VulnerabilityID": vuln_id, "PkgName": "musl", "InstalledVersion": "1.2.5", "Severity": severity}
    if fixed is not None:
        entry["FixedVersion"] = fixed
    return entry


def report(**changes):
    base = {
        "SchemaVersion": 2,
        "ArtifactType": "container_image",
        "Metadata": {"ImageID": IMAGE_ID, "OS": {"Family": "alpine", "Name": "3.24.2"}},
        "Results": [{"Target": "image (alpine 3.24.2)", "Class": "os-pkgs", "Type": "alpine", "Packages": [{"Name": "musl", "Version": "1.2.5"}]}],
    }
    base.update(copy.deepcopy(changes))
    return base


def with_results(*extra, os_vulns=None):
    data = report()
    if os_vulns is not None:
        data["Results"][0]["Vulnerabilities"] = os_vulns
    data["Results"].extend(extra)
    return data


def gobinary(*versions):
    return {"Target": "usr/bin/tool", "Class": "lang-pkgs", "Type": "gobinary", "Packages": [{"Name": "stdlib", "Version": v} for v in versions]}


class PolicyTest(unittest.TestCase):
    def failing(self, data, fragment):
        errors, fixable, _ = policy.evaluate(data)
        self.assertTrue(any(fragment in line for line in errors + fixable), (fragment, errors, fixable))

    def test_clean_report_passes(self):
        self.assertEqual(policy.evaluate(report()), ([], [], {}))

    def test_unfixed_findings_are_reported_not_blocking(self):
        errors, fixable, unfixed = policy.evaluate(with_results(os_vulns=[vuln("CRITICAL"), vuln("LOW", "", "CVE-2")]))
        self.assertEqual((errors, fixable, unfixed), ([], [], {"CRITICAL": 1, "LOW": 1}))

    def test_fixable_finding_blocks_at_every_severity(self):
        for severity in policy.SEVERITIES:
            with self.subTest(severity=severity):
                self.failing(with_results(os_vulns=[vuln(severity, "1.2.6")]), f"{severity} CVE-2026-0001")

    def test_all_findings_are_collected(self):
        data = with_results(gobinary(""), os_vulns=[vuln("LOW", "2"), vuln("HIGH", "3", "CVE-2")])
        errors, fixable, _ = policy.evaluate(data)
        self.assertEqual((len(errors), len(fixable)), (1, 2))

    def test_missing_or_wrong_os_is_rejected(self):
        for os_info in (None, {}, {"Family": "debian", "Name": "12"}, {"Family": "alpine"}):
            with self.subTest(os_info=os_info):
                data = report()
                if os_info is None:
                    del data["Metadata"]["OS"]
                else:
                    data["Metadata"]["OS"] = os_info
                self.failing(data, "Metadata.OS")

    def test_missing_os_package_inventory_is_rejected(self):
        for results in ([], [{"Target": "t", "Class": "os-pkgs", "Type": "alpine"}], [{"Target": "t", "Class": "os-pkgs", "Type": "debian", "Packages": [{"Name": "a"}]}]):
            with self.subTest(results=results):
                self.failing(report(Results=results), "no Alpine OS package inventory")
        data = report()
        del data["Results"]
        self.failing(data, "Results must be a list")

    def test_go_binary_inventory_is_strict(self):
        for versions in ((), ("",), ("(devel)",), ("1.26.0", "")):
            with self.subTest(versions=versions):
                self.failing(with_results(gobinary(*versions)), "Go binary inventory")
        self.assertEqual(policy.evaluate(with_results(gobinary("1.26.9")))[0], [])

    def test_scalars_must_have_the_exact_type(self):
        self.failing(report(SchemaVersion=True), "SchemaVersion")
        self.failing(report(SchemaVersion=2.0), "SchemaVersion")
        self.failing(report(ArtifactType=True), "ArtifactType")
        for bad in (True, 1, ["1.2.6"]):
            self.failing(with_results(os_vulns=[vuln("HIGH", bad)]), "malformed vulnerability")
        self.failing(with_results(os_vulns=[dict(vuln(), Severity=True)]), "malformed vulnerability")
        self.failing(with_results(os_vulns=[dict(vuln(), PkgName=False)]), "malformed vulnerability")
        self.failing(with_results(os_vulns=["CVE"]), "malformed vulnerability")
        self.failing(with_results(os_vulns={"a": 1}), "Vulnerabilities must be a list")

    def test_unknown_severity_and_bad_image_id_are_rejected(self):
        self.failing(with_results(os_vulns=[vuln("SEVERE")]), "malformed vulnerability")
        data = report()
        data["Metadata"]["ImageID"] = "latest"
        self.failing(data, "ImageID")

    def test_non_object_report_is_rejected(self):
        self.failing([], "not a JSON object")


class CliTest(unittest.TestCase):
    def run_policy(self, text):
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
            handle.write(text)
        self.addCleanup(os.unlink, handle.name)
        return subprocess.run([sys.executable, POLICY, handle.name], capture_output=True, text=True)

    def test_exit_codes(self):
        self.assertEqual(self.run_policy(json.dumps(report())).returncode, 0)
        blocked = self.run_policy(json.dumps(with_results(os_vulns=[vuln("LOW", "9")])))
        self.assertEqual(blocked.returncode, 1)
        self.assertIn("BLOCKING FIXABLE", blocked.stdout)

    def test_unreadable_reports_fail_closed(self):
        valid = json.dumps(report())
        for text in ("", "{", "[]", valid.replace('"SchemaVersion": 2', '"SchemaVersion": 2, "SchemaVersion": 2'), valid.replace('"SchemaVersion": 2', '"SchemaVersion": NaN')):
            with self.subTest(text=text[:40]):
                self.assertEqual(self.run_policy(text).returncode, 1)
        self.assertEqual(subprocess.run([sys.executable, POLICY, "/nonexistent.json"], capture_output=True).returncode, 1)
        self.assertEqual(subprocess.run([sys.executable, POLICY], capture_output=True).returncode, 2)


if __name__ == "__main__":
    unittest.main()
