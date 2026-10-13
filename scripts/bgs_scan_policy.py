#!/usr/bin/env python3
"""Fail-closed policy for the Trivy JSON report of the TGF Alpine image.

  bgs_scan_policy.py <trivy-report.json> --image-id sha256:<archive config digest>

Blocks on every fixable vulnerability (any severity, including UNKNOWN) and
reports unfixed ones. A report that is not for the expected image, carries
scanner errors, or cannot prove the single Alpine package inventory and any Go
binary versions is rejected, so a scanner that silently saw nothing never
passes. No exception exists for versionless Go entries: this image ships no Go
binaries, so any versionless one is a finding. All problems are collected so one
run shows every finding.
"""
import json
import re
import sys

SEVERITIES = {"UNKNOWN", "LOW", "MEDIUM", "HIGH", "CRITICAL"}
CLASSES = {"os-pkgs", "lang-pkgs"}
IMAGE_ID = re.compile(r"^sha256:[a-f0-9]{64}$")
MAX_BYTES = 64 << 20  # a --list-all-pkgs report is a few MB; refuse anything absurd


def _object(pairs):
    keys = [key for key, _ in pairs]
    if len(keys) != len(set(keys)):
        raise ValueError("duplicate JSON key")
    return dict(pairs)


def _constant(name):
    raise ValueError(f"unsupported JSON constant {name}")


def load(path):
    with open(path, "rb") as handle:
        raw = handle.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES:
        raise ValueError(f"report exceeds {MAX_BYTES} bytes")
    return json.loads(raw.decode("utf-8"), object_pairs_hook=_object, parse_constant=_constant)


def _text(value):
    return isinstance(value, str) and value.strip() != ""


def _scanner_error(obj):
    return any(key in obj and obj[key] not in (None, "", [], {}) for key in ("Error", "Errors"))


def evaluate(report, image_id):
    """Return (errors, fixable, unfixed_by_severity) for a parsed report."""
    errors, fixable, unfixed = [], [], {}
    if not isinstance(report, dict):
        return ["report is not a JSON object"], fixable, unfixed
    if _scanner_error(report):
        errors.append("the scanner reported an error")
    if type(report.get("SchemaVersion")) is not int or report["SchemaVersion"] != 2:
        errors.append("SchemaVersion must be the integer 2")
    if report.get("ArtifactType") != "container_image":
        errors.append("ArtifactType must be container_image")
    metadata = report.get("Metadata")
    if not isinstance(metadata, dict) or metadata.get("ImageID") != image_id:
        errors.append(f"Metadata.ImageID is not the archive config digest {image_id}")
    os_info = metadata.get("OS") if isinstance(metadata, dict) else None
    family = os_info.get("Family") if isinstance(os_info, dict) else None
    if family != "alpine" or not _text(os_info.get("Name")):
        errors.append("Metadata.OS must identify an Alpine release")
    results = report.get("Results")
    if not isinstance(results, list):
        errors.append("Results must be a list")
        results = []
    os_results = 0
    for index, result in enumerate(results):
        where = f"Results[{index}]"
        if not isinstance(result, dict) or not all(_text(result.get(key)) for key in ("Target", "Class", "Type")):
            errors.append(f"{where} needs Target, Class and Type strings")
            continue
        if _scanner_error(result):
            errors.append(f"{where} carries a scanner error")
        if result["Class"] not in CLASSES:
            errors.append(f"{where} has unsupported class {result['Class']}")
        packages = result.get("Packages")
        if packages is not None and (not isinstance(packages, list) or not all(isinstance(p, dict) and _text(p.get("Name")) for p in packages)):
            errors.append(f"{where} Packages must be a list of named packages")
            packages = None
        if result["Class"] == "os-pkgs":
            os_results += 1
            if result["Type"] != family:
                errors.append(f"{where} is an unsupported OS report: {result['Type']}")
            if not packages or not all(_text(p.get("Version")) for p in packages):
                errors.append(f"{where} OS packages must be listed, each with a version")
        if result["Type"] == "gobinary" and not (packages and all(_text(p.get("Version")) and p["Version"] != "(devel)" for p in packages)):
            errors.append(f"{where} Go binary inventory is empty or versionless: {result['Target']}")
        vulnerabilities = result.get("Vulnerabilities")
        if vulnerabilities is not None and not isinstance(vulnerabilities, list):
            errors.append(f"{where} Vulnerabilities must be a list")
            continue
        for vuln in vulnerabilities or []:
            fixed_in = vuln.get("FixedVersion") if isinstance(vuln, dict) else None
            if not (isinstance(vuln, dict) and all(_text(vuln.get(k)) for k in ("VulnerabilityID", "PkgName", "InstalledVersion"))
                    and vuln.get("Severity") in SEVERITIES and (fixed_in in (None, "") or _text(fixed_in))):
                errors.append(f"{where} has a malformed vulnerability entry")
            elif fixed_in:
                fixable.append(f"{vuln['Severity']} {vuln['VulnerabilityID']} {vuln['PkgName']} {vuln['InstalledVersion']} -> {fixed_in}")
            else:
                unfixed[vuln["Severity"]] = unfixed.get(vuln["Severity"], 0) + 1
    if os_results != 1:
        errors.append(f"expected exactly one Alpine OS package inventory, found {os_results}")
    return errors, fixable, unfixed


def main(argv):
    if len(argv) != 4 or argv[2] != "--image-id" or not IMAGE_ID.match(argv[3]):
        print("Usage: bgs_scan_policy.py <trivy-report.json> --image-id sha256:<64 hex>", file=sys.stderr)
        return 2
    try:
        errors, fixable, unfixed = evaluate(load(argv[1]), argv[3])
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
