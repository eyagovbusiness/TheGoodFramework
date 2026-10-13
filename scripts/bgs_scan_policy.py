#!/usr/bin/env python3
"""Fail-closed policy for the Trivy JSON report of the TGF Alpine image.

Blocks on every fixable vulnerability (any severity) and reports unfixed ones.
A report that cannot prove the OS package inventory and any Go binary versions
were read is rejected, so a scanner that silently saw nothing never passes.
All problems are collected so one run shows every finding.
"""
import json
import re
import sys

SEVERITIES = {"UNKNOWN", "LOW", "MEDIUM", "HIGH", "CRITICAL"}
IMAGE_ID = re.compile(r"^sha256:[a-f0-9]{64}$")


def _object(pairs):
    keys = [key for key, _ in pairs]
    if len(keys) != len(set(keys)):
        raise ValueError("duplicate JSON key")
    return dict(pairs)


def _constant(name):
    raise ValueError(f"unsupported JSON constant {name}")


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle, object_pairs_hook=_object, parse_constant=_constant)


def _text(value):
    return isinstance(value, str) and value != ""


def evaluate(report):
    """Return (errors, fixable, unfixed_by_severity) for a parsed report."""
    errors, fixable, unfixed = [], [], {}
    if not isinstance(report, dict):
        return ["report is not a JSON object"], fixable, unfixed
    if type(report.get("SchemaVersion")) is not int or report["SchemaVersion"] != 2:
        errors.append("SchemaVersion must be the integer 2")
    if report.get("ArtifactType") != "container_image":
        errors.append("ArtifactType must be container_image")
    metadata = report.get("Metadata")
    os_info = metadata.get("OS") if isinstance(metadata, dict) else None
    if not isinstance(metadata, dict) or not isinstance(metadata.get("ImageID"), str) or not IMAGE_ID.match(metadata["ImageID"]):
        errors.append("Metadata.ImageID must be a sha256 image id")
    family = os_info.get("Family") if isinstance(os_info, dict) else None
    if family != "alpine" or not _text(os_info.get("Name")):
        errors.append("Metadata.OS must identify an Alpine release")
    results = report.get("Results")
    if not isinstance(results, list):
        errors.append("Results must be a list")
        results = []
    os_inventory = False
    for index, result in enumerate(results):
        where = f"Results[{index}]"
        if not isinstance(result, dict) or not all(_text(result.get(key)) for key in ("Target", "Class", "Type")):
            errors.append(f"{where} needs Target, Class and Type strings")
            continue
        packages = result.get("Packages")
        if packages is not None and (not isinstance(packages, list) or not all(isinstance(p, dict) and _text(p.get("Name")) for p in packages)):
            errors.append(f"{where} Packages must be a list of named packages")
            packages = None
        if result["Class"] == "os-pkgs" and result["Type"] == family and packages:
            os_inventory = True
        if result["Type"] == "gobinary":
            versions = [p.get("Version") for p in packages or []]
            if not versions or not all(_text(v) and v != "(devel)" for v in versions):
                errors.append(f"{where} Go binary inventory is empty or versionless: {result['Target']}")
        vulnerabilities = result.get("Vulnerabilities")
        if vulnerabilities is not None and not isinstance(vulnerabilities, list):
            errors.append(f"{where} Vulnerabilities must be a list")
            continue
        for vuln in vulnerabilities or []:
            fixed_in = vuln.get("FixedVersion") if isinstance(vuln, dict) else None
            if not (isinstance(vuln, dict) and all(_text(vuln.get(k)) for k in ("VulnerabilityID", "PkgName", "InstalledVersion"))
                    and vuln.get("Severity") in SEVERITIES and (fixed_in is None or isinstance(fixed_in, str))):
                errors.append(f"{where} has a malformed vulnerability entry")
            elif fixed_in:
                fixable.append(f"{vuln['Severity']} {vuln['VulnerabilityID']} {vuln['PkgName']} {vuln['InstalledVersion']} -> {fixed_in}")
            else:
                unfixed[vuln["Severity"]] = unfixed.get(vuln["Severity"], 0) + 1
    if not os_inventory:
        errors.append("no Alpine OS package inventory in the report")
    return errors, fixable, unfixed


def main(argv):
    if len(argv) != 2:
        print("Usage: bgs_scan_policy.py <trivy-report.json>", file=sys.stderr)
        return 2
    try:
        errors, fixable, unfixed = evaluate(load(argv[1]))
    except (OSError, ValueError) as error:
        errors, fixable, unfixed = [f"unreadable report: {error}"], [], {}
    for line in errors:
        print(f"INVALID REPORT: {line}")
    for line in fixable:
        print(f"BLOCKING FIXABLE: {line}")
    print(f"Unfixed findings (reported, not blocking): {dict(sorted(unfixed.items())) or 'none'}")
    return 1 if errors or fixable else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
