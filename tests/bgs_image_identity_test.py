import hashlib
import io
import json
import os
import subprocess
import sys
import tarfile
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
HELPER = os.path.join(HERE, "..", "scripts", "bgs_image_identity.py")
sys.path.insert(0, os.path.join(HERE, "..", "scripts"))
import bgs_image_identity as identity  # noqa: E402

CONFIG = json.dumps({"architecture": "amd64", "rootfs": {"type": "layers", "diff_ids": []}}).encode()
CONFIG_DIGEST = "sha256:" + hashlib.sha256(CONFIG).hexdigest()
DOCKER_MEDIA = "application/vnd.docker.distribution.manifest.v2+json"


def sha(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def archive(path, entries, files):
    """Write a tar with a manifest.json and the named files (name -> bytes)."""
    members = dict(files)
    if entries is not None:
        members["manifest.json"] = json.dumps(entries).encode()
    with tarfile.open(path, "w") as out:
        for name, data in members.items():
            info = tarfile.TarInfo(name)
            info.size = len(data)
            out.addfile(info, io.BytesIO(data))


def manifest(config_digest=CONFIG_DIGEST, **changes):
    data = {"schemaVersion": 2, "mediaType": DOCKER_MEDIA, "config": {"mediaType": "x", "digest": config_digest, "size": 1}, "layers": [{"digest": sha(b"l"), "size": 1}]}
    data.update(changes)
    return json.dumps(data).encode()


class ArchiveTest(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.path = os.path.join(self.dir.name, "image.tar")

    def digest(self, entries, files):
        archive(self.path, entries, files)
        return identity.archive_config_digest(self.path)

    def rejects(self, entries, files, fragment):
        archive(self.path, entries, files)
        with self.assertRaises(identity.IdentityError) as caught:
            identity.archive_config_digest(self.path)
        self.assertIn(fragment, str(caught.exception))

    def test_blob_style_archive_yields_the_hashed_config_digest(self):
        name = "blobs/sha256/" + CONFIG_DIGEST[7:]
        self.assertEqual(self.digest([{"Config": name, "Layers": ["blobs/sha256/" + "1" * 64]}], {name: CONFIG}), CONFIG_DIGEST)

    def test_classic_style_archive_yields_the_hashed_config_digest(self):
        name = CONFIG_DIGEST[7:] + ".json"
        self.assertEqual(self.digest([{"Config": name, "Layers": ["abc/layer.tar"]}], {name: CONFIG}), CONFIG_DIGEST)

    def test_matching_index_annotation_is_accepted_and_a_wrong_one_is_not(self):
        name = "blobs/sha256/" + CONFIG_DIGEST[7:]
        entries = [{"Config": name, "Layers": ["l"]}]
        index = {"manifests": [{"annotations": {"config.digest": CONFIG_DIGEST}}]}
        self.assertEqual(self.digest(entries, {name: CONFIG, "index.json": json.dumps(index).encode()}), CONFIG_DIGEST)
        index["manifests"][0]["annotations"]["config.digest"] = "sha256:" + "0" * 64
        self.rejects(entries, {name: CONFIG, "index.json": json.dumps(index).encode()}, "index.json config digest")
        self.rejects(entries, {name: CONFIG, "index.json": json.dumps({"manifests": []}).encode()}, "exactly one image")

    def test_config_bytes_must_hash_to_their_name(self):
        name = "blobs/sha256/" + "2" * 64
        self.rejects([{"Config": name, "Layers": ["l"]}], {name: CONFIG}, "does not match its content hash")

    def test_config_name_must_carry_a_digest(self):
        self.rejects([{"Config": "config.json", "Layers": ["l"]}], {"config.json": CONFIG}, "does not match its content hash")

    def test_exactly_one_image_is_required(self):
        name = "blobs/sha256/" + CONFIG_DIGEST[7:]
        one = {"Config": name, "Layers": ["l"]}
        for entries in ([], [one, one], {"Config": name}, ["x"]):
            with self.subTest(entries=entries):
                self.rejects(entries, {name: CONFIG}, "exactly one image")

    def test_malformed_or_missing_manifest_is_rejected(self):
        name = "blobs/sha256/" + CONFIG_DIGEST[7:]
        self.rejects(None, {name: CONFIG}, "no manifest.json")
        self.rejects([{"Layers": ["l"]}], {name: CONFIG}, "Config path and Layers")
        self.rejects([{"Config": name, "Layers": []}], {name: CONFIG}, "Config path and Layers")
        self.rejects([{"Config": "blobs/missing", "Layers": ["l"]}], {name: CONFIG}, "no blobs/missing")
        archive(self.path, None, {"manifest.json": b"{not json"})
        with self.assertRaises(identity.IdentityError):
            identity.archive_config_digest(self.path)

    def test_oversized_members_are_refused_without_reading_layers(self):
        name = "blobs/sha256/" + CONFIG_DIGEST[7:]
        original = identity.MAX_BYTES
        identity.MAX_BYTES = 10
        self.addCleanup(setattr, identity, "MAX_BYTES", original)
        self.rejects([{"Config": name, "Layers": ["l"]}], {name: CONFIG}, "within 10 bytes")

    def test_unreadable_archive_is_rejected(self):
        with open(self.path, "wb") as handle:
            handle.write(b"not a tar")
        with self.assertRaises(identity.IdentityError):
            identity.archive_config_digest(self.path)
        with self.assertRaises(identity.IdentityError):
            identity.archive_config_digest(os.path.join(self.dir.name, "absent.tar"))


class ManifestTest(unittest.TestCase):
    def rejects(self, raw, fragment, digest=None, config=CONFIG_DIGEST):
        with self.assertRaises(identity.IdentityError) as caught:
            identity.verify_manifest(raw, digest or sha(raw), config)
        self.assertIn(fragment, str(caught.exception))

    def test_matching_single_image_manifest_passes(self):
        for media in (DOCKER_MEDIA, "application/vnd.oci.image.manifest.v1+json"):
            raw = manifest(mediaType=media)
            identity.verify_manifest(raw, sha(raw), CONFIG_DIGEST)

    def test_content_must_hash_to_the_pushed_digest_even_when_it_names_the_config(self):
        self.rejects(manifest(), "does not hash", digest=sha(b"other"))

    def test_config_digest_must_match_the_scanned_archive(self):
        self.rejects(manifest("sha256:" + "0" * 64), "config digest differs")
        self.rejects(manifest(), "config digest differs", config="sha256:" + "0" * 64)

    def test_indexes_and_unknown_media_types_are_rejected(self):
        self.rejects(manifest(mediaType="application/vnd.oci.image.index.v1+json", manifests=[]), "single image manifest")
        self.rejects(manifest(manifests=[{}]), "single image manifest")
        self.rejects(manifest(mediaType=None), "single image manifest")

    def test_malformed_or_missing_evidence_is_rejected(self):
        self.rejects(b"", "not valid JSON", digest=sha(b""))
        self.rejects(b"[]", "schemaVersion 2")
        self.rejects(b"{", "not valid JSON")
        self.rejects(manifest(schemaVersion=True), "schemaVersion 2")
        self.rejects(manifest(schemaVersion=2.0), "schemaVersion 2")
        self.rejects(manifest(config=None), "config digest differs")
        self.rejects(manifest(config={}), "config digest differs")
        self.rejects(manifest(layers=[]), "no layers")
        self.rejects(manifest(layers=None), "no layers")

    def test_digest_arguments_are_validated(self):
        raw = manifest()
        for digest, config in (("latest", CONFIG_DIGEST), (sha(raw), "latest"), (sha(raw).upper(), CONFIG_DIGEST)):
            with self.subTest(digest=digest, config=config):
                with self.assertRaises(identity.IdentityError):
                    identity.verify_manifest(raw, digest, config)

    def test_oversized_manifest_is_rejected(self):
        self.rejects(b" " * (identity.MAX_BYTES + 1), "too large", digest=sha(b""))


class CliTest(unittest.TestCase):
    def run_helper(self, *args):
        return subprocess.run([sys.executable, HELPER, *args], capture_output=True, text=True)

    def test_exit_codes_and_output(self):
        with tempfile.TemporaryDirectory() as directory:
            tar_path, raw_path = os.path.join(directory, "i.tar"), os.path.join(directory, "m.json")
            name = "blobs/sha256/" + CONFIG_DIGEST[7:]
            archive(tar_path, [{"Config": name, "Layers": ["l"]}], {name: CONFIG})
            ok = self.run_helper("archive", tar_path)
            self.assertEqual((ok.returncode, ok.stdout.strip()), (0, CONFIG_DIGEST))
            raw = manifest()
            with open(raw_path, "wb") as handle:
                handle.write(raw)
            self.assertEqual(self.run_helper("manifest", raw_path, sha(raw), CONFIG_DIGEST).returncode, 0)
            self.assertEqual(self.run_helper("manifest", raw_path, sha(b"x"), CONFIG_DIGEST).returncode, 1)
            self.assertEqual(self.run_helper("manifest", os.path.join(directory, "absent"), sha(raw), CONFIG_DIGEST).returncode, 1)
            self.assertEqual(self.run_helper("archive", os.path.join(directory, "absent")).returncode, 1)
        self.assertEqual(self.run_helper().returncode, 2)
        self.assertEqual(self.run_helper("archive").returncode, 2)


if __name__ == "__main__":
    unittest.main()
