#!/usr/bin/env python3
"""Bounded identity checks that bind a scan to the image that is published.

  bgs_image_identity.py archive <docker-save.tar>
      Print the config digest of the single image in a `docker save` archive,
      after hashing the real config bytes. Only manifest.json, the optional
      index.json and the config blob are read; layers are never read.
  bgs_image_identity.py manifest <raw-manifest.json> <manifest-digest> <config-digest>
      Verify a raw registry manifest is the one addressed by its digest, is a
      single image manifest (not an index), and references the expected config.
"""
import hashlib
import json
import re
import sys
import tarfile

MAX_BYTES = 4 << 20  # manifests and image configs are small
DIGEST = re.compile(r"^sha256:[a-f0-9]{64}$")
CONFIG_NAME = re.compile(r"(?:^|/)([a-f0-9]{64})(?:\.json)?$")
IMAGE_MANIFEST_TYPES = {
    "application/vnd.docker.distribution.manifest.v2+json",
    "application/vnd.oci.image.manifest.v1+json",
}


class IdentityError(Exception):
    pass


def _json(raw, what):
    try:
        return json.loads(raw)
    except ValueError as error:
        raise IdentityError(f"{what} is not valid JSON: {error}") from None


def _read(archive, name):
    try:
        member = archive.getmember(name)
    except KeyError:
        raise IdentityError(f"archive has no {name}") from None
    if not member.isfile() or member.size > MAX_BYTES:
        raise IdentityError(f"{name} is not a regular file within {MAX_BYTES} bytes")
    return archive.extractfile(member).read()


def archive_config_digest(path):
    try:
        archive = tarfile.open(path, "r:")
    except (OSError, tarfile.TarError) as error:
        raise IdentityError(f"cannot read archive: {error}") from None
    with archive:
        entries = _json(_read(archive, "manifest.json"), "manifest.json")
        if not (isinstance(entries, list) and len(entries) == 1 and isinstance(entries[0], dict)):
            raise IdentityError("archive must contain exactly one image")
        config_path, layers = entries[0].get("Config"), entries[0].get("Layers")
        if not isinstance(config_path, str) or not isinstance(layers, list) or not layers:
            raise IdentityError("archive manifest needs a Config path and Layers")
        named = CONFIG_NAME.search(config_path)
        digest = "sha256:" + hashlib.sha256(_read(archive, config_path)).hexdigest()
        if not named or "sha256:" + named.group(1) != digest:
            raise IdentityError(f"config {config_path} does not match its content hash {digest}")
        if "index.json" in archive.getnames():
            index = _json(_read(archive, "index.json"), "index.json")
            manifests = index.get("manifests") if isinstance(index, dict) else None
            if not isinstance(manifests, list) or len(manifests) != 1 or not isinstance(manifests[0], dict):
                raise IdentityError("index.json must reference exactly one image")
            annotated = (manifests[0].get("annotations") or {}).get("config.digest")
            if annotated is not None and annotated != digest:
                raise IdentityError("index.json config digest differs from the archive config")
    return digest


def verify_manifest(raw, manifest_digest, config_digest):
    if not DIGEST.match(manifest_digest) or not DIGEST.match(config_digest):
        raise IdentityError("digests must be sha256:<64 lowercase hex>")
    if len(raw) > MAX_BYTES:
        raise IdentityError("manifest is too large")
    if "sha256:" + hashlib.sha256(raw).hexdigest() != manifest_digest:
        raise IdentityError("the registry content does not hash to the pushed manifest digest")
    manifest = _json(raw, "manifest")
    if not isinstance(manifest, dict) or type(manifest.get("schemaVersion")) is not int or manifest["schemaVersion"] != 2:
        raise IdentityError("manifest must be schemaVersion 2")
    if manifest.get("mediaType") not in IMAGE_MANIFEST_TYPES or "manifests" in manifest:
        raise IdentityError("the published reference must be a single image manifest, not an index")
    config = manifest.get("config")
    if not isinstance(config, dict) or config.get("digest") != config_digest:
        raise IdentityError("the published config digest differs from the scanned archive config")
    layers = manifest.get("layers")
    if not isinstance(layers, list) or not layers:
        raise IdentityError("manifest has no layers")


def main(argv):
    try:
        if len(argv) == 3 and argv[1] == "archive":
            print(archive_config_digest(argv[2]))
        elif len(argv) == 5 and argv[1] == "manifest":
            with open(argv[2], "rb") as handle:
                raw = handle.read(MAX_BYTES + 1)
            verify_manifest(raw, argv[3], argv[4])
        else:
            print(__doc__, file=sys.stderr)
            return 2
    except (IdentityError, OSError) as error:
        print(f"IDENTITY MISMATCH: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
