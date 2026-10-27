#!/usr/bin/env python3
"""Verify a canonical private base-image publication and propose it as a reviewed pull request.

This single file is the shared receiver engine. It is vendored byte-for-byte into every consumer
repository (the canonical copy and its tests live in Biognosys/of.devops, scripts/ and tests/).
A thin per-repository profile (scripts/base_image_receiver_profile.json) allowlists the repository,
image family, registry, target branches and Dockerfiles. Nothing in an event can choose a path,
branch, file, image or command.

Commands (the workflow passes credentials only to the step that needs them):

  validate-event  validate an optional repository_dispatch hint; no network, no credentials
  plan            discover and verify the newest publication, read each target as data, write a plan.
                  Credentials are read-only: GH_SOURCE_TOKEN (Actions read on the source repository),
                  REGISTRY_PULL_USERNAME / REGISTRY_PULL_PASSWORD (pull-only registry credential) and
                  GH_TOKEN (the workflow's read-only token for the consumer repository).
  propose         create one commit, one create-only branch and one pull request per planned target.
                  Needs only GH_TOKEN: a repository-scoped GitHub App token that is minted AFTER the plan
                  proved that there is something to propose.

Trust model
  * The dispatch payload is a HINT. The same newest-verified-publication discovery runs for dispatch,
    schedule and manual runs, so a missed or forged notification can neither add nor hide a publication.
  * A publication is accepted only when ALL of these hold: the run belongs to the approved source
    repository and workflow, ran on main from a push, manual or scheduled event, its publish job (not
    merely the run) succeeded including the publish and evidence steps, the retained evidence artifact
    matches that run/attempt/commit, and the authenticated registry returns manifest and config blobs
    whose bytes hash to the recorded identities for a supported runtime. Any failure is a visible
    error. There is never a fallback to `latest`, to a partial publication or to an older digest.
  * Target branches are read through the Git API as data. Only the allowlisted Dockerfile blobs are
    patched, and only the 64 hex characters of the pinned digest change; every other byte and the file
    mode are preserved. All files of a target change in one commit.
  * Proposals are create-only and deterministic per family/target/digest. An existing pull request in
    any state, or an orphan branch, is never updated, adopted, reopened or deleted. Nothing is merged.
  * Reviewed pause and suppression controls in the profile stop proposals without code changes.
  * Diagnostics are static text. Event, API and registry values are never echoed into the log (only a
    GitHub API error message, stripped of control characters and `::`, is appended to an API failure),
    so they cannot inject workflow commands. Tokens are only ever request headers, and credentials are never
    replayed to a redirect target or to a host other than the one they were issued for.
"""
import argparse
import base64
import datetime
import hashlib
import http.client
import io
import json
import os
import re
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path

EVENT_NAME = "repository_dispatch"
EVENT_TYPE = "base-image-published-v1"  # exact publisher contract, schema 1
HINT_SCHEMA = 1
HINT_RUN_ID = re.compile(r"[1-9][0-9]{0,19}")
HINT_ATTEMPT = re.compile(r"[1-9][0-9]{0,3}")
PROFILE_NAME = "base_image_receiver_profile.json"
PLAN_NAME = "base-image-receiver-plan.json"
PLAN_SCHEMA = 1
PROFILE_SCHEMA = 1
LABEL = "dependency-update"
BRANCH_ROOT = "feature/"

MAX_EVENT_BYTES = 1 << 20
MAX_PROFILE_BYTES = 64 << 10
MAX_PLAN_BYTES = 1 << 20
MAX_DOCKERFILE_BYTES = 1 << 20
MAX_API_BYTES = 4 << 20
MAX_ZIP_BYTES = 1 << 20
MAX_EVIDENCE_BYTES = 64 << 10
MAX_MANIFEST_BYTES = 4 << 20
MAX_CONFIG_BYTES = 4 << 20
MAX_TOKEN_BYTES = 16 << 10
MAX_RUN_PAGES = 2
RUNS_PER_PAGE = 50
MAX_INSPECTED_RUNS = 10  # newest runs whose jobs are inspected; the publisher serializes publications
MAX_FILES_PER_TARGET = 4

ALLOWED_RUN_EVENTS = ("push", "workflow_dispatch", "schedule")
PUBLISH_JOB = "build-and-push / publish-image"  # caller job id of the approved workflows + reusable publisher job
PUBLISH_STEPS = ("Publish scanned identity", "Upload version manifest evidence")
TARGET_BRANCHES = ("main", "dev")
REGULAR_FILE_MODES = ("100644", "100755")
USER = "biognosys"  # the approved canonical non-root user; a digest-only updater must not change it

FAMILIES = {
    "alpine": {
        "source_repository": "Biognosys/bgsg.base-images",
        "workflow": ".github/workflows/alpine-main.yml",
        "image": "alpine",
        "runtime": "alpine",
    },
    "aspnet": {
        "source_repository": "Biognosys/bgsg.base-images",
        "workflow": ".github/workflows/aspnet-main.yml",
        "image": "aspnet",
        "runtime": "aspnet",
    },
}
ASPNET_MAJOR = "10"  # a different major needs separate reviewed compatibility work

SHA256 = re.compile(r"sha256:[0-9a-f]{64}")
GIT_SHA = re.compile(r"[0-9a-f]{40}")
COUNTER = re.compile(r"[1-9][0-9]{0,17}")
TAG = re.compile(r"[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}")
REPOSITORY = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+")
HOST = re.compile(
    r"[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)*(?::[0-9]{1,5})?")
DNS_NAME = re.compile(r"(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}")
API_URL = re.compile(r"https://[A-Za-z0-9.-]+(:[0-9]+)?(/[A-Za-z0-9._/-]*)?")
TIMESTAMP = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z")
BRANCH_NAME = re.compile(r"feature/[0-9]{8}_SF-2942_BOT_(?:alpine|aspnet)-base-image-[0-9a-f]{64}-(?:main|dev)")
RELATIVE_FILE = re.compile(r"[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*")
IDENT = re.compile(r"[A-Za-z][A-Za-z0-9_]{0,63}")

INDEX_TYPES = ("application/vnd.oci.image.index.v1+json",
               "application/vnd.docker.distribution.manifest.list.v2+json")
IMAGE_TYPES = ("application/vnd.oci.image.manifest.v1+json",
               "application/vnd.docker.distribution.manifest.v2+json")
CONFIG_TYPES = ("application/vnd.oci.image.config.v1+json",
                "application/vnd.docker.container.image.v1+json")
MANIFEST_ACCEPT = ", ".join(INDEX_TYPES + IMAGE_TYPES)

ESCAPE_DIRECTIVE = re.compile(rb"#\s*escape\s*=", re.IGNORECASE)
HEREDOC = re.compile(rb"<<-?\s*[\"']?[A-Za-z_]")


class UpdateError(Exception):
    """A rejection. Messages are static text: they must never contain event, API or registry values."""


# ----------------------------------------------------------------------------------------------
# Strict parsing helpers
# ----------------------------------------------------------------------------------------------

def _no_duplicates(pairs):
    keys = [key for key, _ in pairs]
    if len(keys) != len(set(keys)):
        raise ValueError("duplicate key")
    return dict(pairs)


def _no_constants(_name):
    raise ValueError("non-finite number")


def _strict_json(raw, what):
    try:
        return json.loads(raw.decode("utf-8"), object_pairs_hook=_no_duplicates, parse_constant=_no_constants)
    except UnicodeDecodeError:
        raise UpdateError(f"{what} is not UTF-8") from None
    except (ValueError, RecursionError):
        raise UpdateError(f"{what} is not strict JSON (malformed, duplicate key or non-finite number)") from None


def _text(mapping, key, pattern, what):
    value = mapping.get(key) if isinstance(mapping, dict) else None
    if type(value) is not str:
        raise UpdateError(f"{what}: {key} must be a string")
    if not pattern.fullmatch(value):
        raise UpdateError(f"{what}: {key} is malformed")
    return value


def _integer(mapping, key, what, minimum=1):
    value = mapping.get(key) if isinstance(mapping, dict) else None
    if type(value) is not int or value < minimum:
        raise UpdateError(f"{what}: {key} must be a positive integer")
    return value


def _sha256_of(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def _git_blob_sha(data):
    return hashlib.sha1(b"blob %d\0" % len(data) + data).hexdigest()


def _same_name(left, right):
    return isinstance(left, str) and isinstance(right, str) and left.lower() == right.lower()


def _write_atomic(path, data, mode):
    handle, temporary = tempfile.mkstemp(dir=str(path.parent), prefix=".receiver-")
    try:
        with os.fdopen(handle, "wb") as out:
            out.write(data)
        os.chmod(temporary, mode)
        os.replace(temporary, path)
    except BaseException:
        if os.path.exists(temporary):
            os.unlink(temporary)
        raise


def _write_outputs(values, environ=None):
    environ = os.environ if environ is None else environ
    target = environ.get("GITHUB_OUTPUT")
    if not target:
        return
    with open(target, "a", encoding="utf-8", newline="\n") as handle:
        for key, value in values.items():
            if not re.fullmatch(r"[A-Za-z0-9_:./@-]*", value):  # validated values only; no newline can pass
                raise UpdateError("refusing to write an unvalidated output")
            handle.write(f"{key}={value}\n")


def _append_summary(lines, environ):
    target = environ.get("GITHUB_STEP_SUMMARY")
    if not target:
        return
    with open(target, "a", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")


# ----------------------------------------------------------------------------------------------
# Profile (reviewed, trusted configuration read from the checked-out main)
# ----------------------------------------------------------------------------------------------

def _relative_file(value):
    if type(value) is not str or len(value) > 200 or not RELATIVE_FILE.fullmatch(value):
        return False
    return all(part not in (".", "..", ".git") for part in value.split("/"))


def load_profile(path):
    profile_file = Path(path)
    if profile_file.is_symlink() or not profile_file.is_file():
        raise UpdateError("the receiver profile is missing or not a regular file")
    with profile_file.open("rb") as handle:
        raw = handle.read(MAX_PROFILE_BYTES + 1)
    if len(raw) > MAX_PROFILE_BYTES:
        raise UpdateError("the receiver profile is too large")
    return parse_profile(_strict_json(raw, "the receiver profile"))


def parse_profile(data):
    expected = {"schema", "repository", "family", "registry", "public", "paused", "suppressed_digests", "targets", "botOwner"}
    if not isinstance(data, dict) or set(data) != expected:
        raise UpdateError("the profile does not have exactly the expected fields")
    if data["schema"] != PROFILE_SCHEMA or type(data["schema"]) is not int:
        raise UpdateError("the profile schema is unsupported")
    repository = _text(data, "repository", REPOSITORY, "profile")
    if data["botOwner"] != "BOT":
        raise UpdateError("profile: botOwner must be the reviewed BOT automation identity")
    family = data["family"]
    if type(family) is not str or family not in FAMILIES:
        raise UpdateError("profile: family is not an approved image family")
    registry = _text(data, "registry", HOST, "profile")
    for key in ("public", "paused"):
        if type(data[key]) is not bool:
            raise UpdateError(f"profile: {key} must be a boolean")
    suppressed = data["suppressed_digests"]
    if not isinstance(suppressed, list) or len(suppressed) > 100 \
            or any(type(item) is not str or not SHA256.fullmatch(item) for item in suppressed) \
            or len(set(suppressed)) != len(suppressed):
        raise UpdateError("profile: suppressed_digests must be a list of unique sha256 digests")
    targets = data["targets"]
    if not isinstance(targets, list) or not 1 <= len(targets) <= len(TARGET_BRANCHES):
        raise UpdateError("profile: targets must list one or two branches")
    parsed, seen = [], set()
    for target in targets:
        if not isinstance(target, dict) or set(target) != {"branch", "paused", "files", "layout", "bootstrap"}:
            raise UpdateError("profile: a target does not have exactly the expected fields")
        branch = target["branch"]
        if type(branch) is not str or branch not in TARGET_BRANCHES or branch in seen:
            raise UpdateError("profile: target branch must be a unique allowlisted branch")
        seen.add(branch)
        if type(target["paused"]) is not bool:
            raise UpdateError("profile: target paused must be a boolean")
        files = target["files"]
        if not isinstance(files, list) or not 1 <= len(files) <= MAX_FILES_PER_TARGET \
                or not all(_relative_file(item) for item in files) or len(set(files)) != len(files):
            raise UpdateError("profile: target files must be unique safe relative paths")
        bootstrap = target["bootstrap"]
        if not isinstance(bootstrap, list) or len(bootstrap) > 10:
            raise UpdateError("profile: bootstrap must be a bounded reviewed list")
        seen_pins = set()
        for pin in bootstrap:
            if not isinstance(pin, dict) or set(pin) != {"digest", "published_before"}:
                raise UpdateError("profile: bootstrap entries require exact digest and published_before")
            digest = _text(pin, "digest", SHA256, "bootstrap")
            _timestamp(pin, "published_before", "bootstrap")
            if digest in seen_pins:
                raise UpdateError("profile: duplicate bootstrap digest")
            seen_pins.add(digest)
        parsed.append({"branch": branch, "paused": target["paused"], "files": list(files), "bootstrap": bootstrap,
                       "layout": _parse_layout(target["layout"])})
    return {"repository": repository, "family": family, "registry": registry, "public": data["public"], "botOwner": data["botOwner"],
            "paused": data["paused"], "suppressed": list(suppressed), "targets": parsed}


def _parse_layout(layout):
    if not isinstance(layout, dict) or type(layout.get("kind")) is not str:
        raise UpdateError("profile: layout must be an object with a kind")
    kind = layout["kind"]
    if kind == "registry-from":
        keys = {"kind", "stage"}
    elif kind == "arg-default":
        keys = {"kind", "arg", "stage"}
    else:
        raise UpdateError("profile: layout kind is not supported")
    if set(layout) != keys:
        raise UpdateError("profile: layout does not have exactly the expected fields")
    stage = layout["stage"]
    if stage is not None and (type(stage) is not str or not IDENT.fullmatch(stage)):
        raise UpdateError("profile: layout stage is malformed")
    parsed = {"kind": kind, "stage": stage}
    if kind == "arg-default":
        if type(layout["arg"]) is not str or not IDENT.fullmatch(layout["arg"]):
            raise UpdateError("profile: layout arg is malformed")
        if stage is None:
            raise UpdateError("profile: an arg-default layout needs a stage")
        parsed["arg"] = layout["arg"]
    return parsed


# ----------------------------------------------------------------------------------------------
# Optional dispatch hint
# ----------------------------------------------------------------------------------------------

def load_hint(environ, family):
    """Return the validated hint dict, or None when this is not a dispatch run.

    The hint never selects what is adopted. It is validated strictly so that a malformed
    notification is a visible failure before any credential exists.
    """
    name = environ.get("GITHUB_EVENT_NAME")
    if name != EVENT_NAME:
        return None
    path = environ.get("GITHUB_EVENT_PATH")
    if not path:
        raise UpdateError("GITHUB_EVENT_PATH is not set")
    event_file = Path(path)
    if event_file.is_symlink() or not event_file.is_file():
        raise UpdateError("the event file is missing or not a regular file")
    with event_file.open("rb") as handle:
        raw = handle.read(MAX_EVENT_BYTES + 1)
    if len(raw) > MAX_EVENT_BYTES:
        raise UpdateError("the event payload is larger than 1 MiB")
    event = _strict_json(raw, "the event payload")
    if not isinstance(event, dict):
        raise UpdateError("the event payload is not a JSON object")
    if event.get("action") != EVENT_TYPE:
        raise UpdateError("the event type is not the base image notification type")
    payload = event.get("client_payload")
    if not isinstance(payload, dict):
        raise UpdateError("client_payload is missing or not an object")
    if set(payload) != {"schema", "image", "source_repo", "run_id", "run_attempt"}:
        raise UpdateError("client_payload does not have exactly the expected fields")
    if type(payload["schema"]) is not int or payload["schema"] != HINT_SCHEMA:
        raise UpdateError("the notification schema is unsupported")
    if payload["image"] != family:
        raise UpdateError("the notified image is not this receiver's image family")
    if not _same_name(payload["source_repo"], FAMILIES[family]["source_repository"]):
        raise UpdateError("the notified source repository is not the approved publisher")
    return {"image": family, "run_id": _text(payload, "run_id", HINT_RUN_ID, "notification"),
            "run_attempt": _text(payload, "run_attempt", HINT_ATTEMPT, "notification")}


# ----------------------------------------------------------------------------------------------
# HTTP: bounded, https only, never follows a redirect, retries only idempotent reads
# ----------------------------------------------------------------------------------------------

class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None  # a redirect is surfaced to the caller: credentials are never replayed


REDIRECT_STATUSES = (301, 302, 303, 307, 308)
RETRY_STATUSES = (429, 500, 502, 503, 504)


class Http:
    """fetch(method, url, headers, body, max_bytes) -> (status, lower-cased headers, body bytes)."""

    def __init__(self, opener=None, https_only=True, timeout=60, attempts=3, sleep=time.sleep):
        self._opener = opener or urllib.request.build_opener(_NoRedirect)
        self._https_only = https_only
        self._timeout = timeout
        self._attempts = attempts
        self._sleep = sleep

    def fetch(self, method, url, headers=None, body=None, max_bytes=MAX_API_BYTES):
        parts = urllib.parse.urlsplit(url)
        if parts.scheme != "https" and (self._https_only or parts.scheme != "http"):
            raise UpdateError("only https endpoints are allowed")
        if not parts.hostname or parts.username is not None or parts.password is not None:
            raise UpdateError("the endpoint is malformed")
        idempotent = method in ("GET", "HEAD")
        for attempt in range(1, self._attempts + 1):
            try:
                status, response_headers, raw = self._once(method, url, headers or {}, body, max_bytes)
            except (urllib.error.URLError, OSError, http.client.HTTPException):
                if idempotent and attempt < self._attempts:
                    self._sleep(attempt)
                    continue
                raise UpdateError("an endpoint is unreachable") from None
            if status in RETRY_STATUSES and idempotent and attempt < self._attempts:
                self._sleep(attempt)
                continue
            if len(raw) > max_bytes:
                raise UpdateError("an endpoint response is too large")
            return status, response_headers, raw
        raise UpdateError("an endpoint is unreachable")

    def _once(self, method, url, headers, body, max_bytes):
        request = urllib.request.Request(url, data=body, method=method, headers=headers)
        try:
            with self._opener.open(request, timeout=self._timeout) as response:
                return response.status, _lower(response.headers), response.read(max_bytes + 1)
        except urllib.error.HTTPError as error:
            with error:
                return error.code, _lower(error.headers), error.read(max_bytes + 1)


def _lower(headers):
    return {str(key).lower(): str(value) for key, value in (headers.items() if headers else [])}


def _redirect_target(headers):
    """Validate a redirect Location. Credentials are never sent to it, so only its shape matters."""
    location = headers.get("location", "")
    parts = urllib.parse.urlsplit(location)
    try:
        port = parts.port
    except ValueError:
        port = -1
    host = parts.hostname or ""
    if parts.scheme != "https" or parts.username is not None or parts.password is not None \
            or port not in (None, 443) or not DNS_NAME.fullmatch(host) or host == "localhost" \
            or len(location) > 4096 or re.search(r"[\x00-\x20\x7f]", location):
        raise UpdateError("the redirect target is not an https DNS host")
    return location


def _download_redirected(http, status, headers, body, max_bytes):
    """Follow exactly one hop WITHOUT any Authorization header, then require 200."""
    if status == 200:
        return body
    if status not in REDIRECT_STATUSES:
        raise UpdateError(f"a download failed (HTTP {status})")
    target = _redirect_target(headers)
    status, _, body = http.fetch("GET", target, {"Accept": "*/*", "User-Agent": "of-base-image-receiver"}, None, max_bytes)
    if status != 200:
        raise UpdateError(f"a redirected download failed (HTTP {status})")
    return body


class GitHubApi:
    """Minimal REST adapter. One token, one API base: the token is only ever a request header."""

    def __init__(self, token, http=None, base_url="https://api.github.com"):
        if not token or re.search(r"\s", token) or len(token) > MAX_TOKEN_BYTES:
            raise UpdateError("a GitHub token is missing or malformed")
        if not API_URL.fullmatch(base_url):
            raise UpdateError("GITHUB_API_URL is malformed")
        self._token = token
        self._base = base_url.rstrip("/")
        self._http = http or Http()

    def _headers(self, extra=None):
        headers = {"Authorization": f"Bearer {self._token}", "Accept": "application/vnd.github+json",
                   "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "of-base-image-receiver"}
        headers.update(extra or {})
        return headers

    def request(self, method, path, body=None):
        payload, extra = None, {}
        if body is not None:
            payload = json.dumps(body).encode()
            extra["Content-Type"] = "application/json"
        status, _, raw = self._http.fetch(method, f"{self._base}/{path}", self._headers(extra), payload, MAX_API_BYTES)
        try:
            return status, (json.loads(raw.decode("utf-8")) if raw.strip() else None)
        except ValueError:
            raise UpdateError("the GitHub API returned malformed JSON") from None

    def download(self, path, max_bytes):
        status, headers, raw = self._http.fetch(
            "GET", f"{self._base}/{path}", self._headers({"Accept": "application/vnd.github+json"}), None, max_bytes)
        return _download_redirected(self._http, status, headers, raw, max_bytes)


def _call(api, stage, method, path, body=None, expect=(200, 201)):
    status, data = api.request(method, path, body)
    if status not in expect:
        detail = ""
        if isinstance(data, dict) and isinstance(data.get("message"), str):
            detail = ": " + re.sub(r"[^\x20-\x7e]|::", " ", data["message"])[:200].strip()
        raise UpdateError(f"{stage} failed (HTTP {status}){detail}")
    return data


class RegistryClient:
    """Pull-only registry access using the Docker token flow.

    The Basic credential is sent exactly once, to the token realm announced by the registry, and only
    when that realm is on the registry's own host. Bearer tokens are repository:pull scoped.
    """

    def __init__(self, host, username, password, http=None):
        if not HOST.fullmatch(host or ""):
            raise UpdateError("the registry host is malformed")
        if not username or not password or re.search(r"[\r\n]", username + password) or ":" in username:
            raise UpdateError("the registry pull credential is missing or malformed")
        self._host = host
        self._basic = base64.b64encode(f"{username}:{password}".encode()).decode("ascii")
        self._http = http or Http()
        self._challenge = None
        self._tokens = {}

    def _realm(self):
        if self._challenge is None:
            status, headers, _ = self._http.fetch(
                "GET", f"https://{self._host}/v2/", {"User-Agent": "of-base-image-receiver"}, None, 64 << 10)
            header = headers.get("www-authenticate", "")
            if status != 401 or not header[:7].lower() == "bearer ":
                raise UpdateError("the registry did not present a bearer token challenge")
            params = dict(re.findall(r'([A-Za-z_]+)="([^"]*)"', header))
            realm = urllib.parse.urlsplit(params.get("realm", ""))
            try:
                port = realm.port
            except ValueError:
                port = -1
            same_host = (realm.hostname or "") == self._host.split(":")[0] and port in (None, 443)
            if realm.scheme != "https" or realm.username is not None or not same_host or realm.query \
                    or realm.fragment or re.search(r"[\x00-\x20\x7f]", params.get("realm", "")):
                raise UpdateError("the registry token realm is not on the registry host")
            service = params.get("service", "")
            if service and not re.fullmatch(r"[A-Za-z0-9._:-]{1,255}", service):
                raise UpdateError("the registry token service is malformed")
            self._challenge = (params["realm"], service)
        return self._challenge

    def _token(self, repository):
        if repository not in self._tokens:
            realm, service = self._realm()
            query = {"scope": f"repository:{repository}:pull"}
            if service:
                query["service"] = service
            status, _, raw = self._http.fetch(
                "GET", f"{realm}?{urllib.parse.urlencode(query)}",
                {"Authorization": f"Basic {self._basic}", "User-Agent": "of-base-image-receiver"}, None, MAX_API_BYTES)
            if status != 200:
                raise UpdateError(f"the registry rejected the pull credential (HTTP {status})")
            data = _strict_json(raw, "the registry token response")
            token = data.get("token") or data.get("access_token") if isinstance(data, dict) else None
            if type(token) is not str or not token or re.search(r"\s", token) or len(token) > MAX_TOKEN_BYTES:
                raise UpdateError("the registry returned no usable token")
            print(f"::add-mask::{token}")  # defense in depth: it is never printed otherwise
            self._tokens[repository] = token
        return self._tokens[repository]

    def _headers(self, repository, accept):
        return {"Authorization": f"Bearer {self._token(repository)}", "Accept": accept,
                "User-Agent": "of-base-image-receiver"}

    def get_manifest(self, repository, reference):
        status, headers, raw = self._http.fetch(
            "GET", f"https://{self._host}/v2/{repository}/manifests/{reference}",
            self._headers(repository, MANIFEST_ACCEPT), None, MAX_MANIFEST_BYTES)
        if status != 200:
            raise UpdateError(f"reading a registry manifest failed (HTTP {status})")
        return headers.get("content-type", "").split(";")[0].strip(), raw, headers.get("docker-content-digest", "")

    def get_blob(self, repository, digest, max_bytes):
        status, headers, raw = self._http.fetch(
            "GET", f"https://{self._host}/v2/{repository}/blobs/{digest}",
            self._headers(repository, "application/octet-stream"), None, max_bytes)
        return _download_redirected(self._http, status, headers, raw, max_bytes)


# ----------------------------------------------------------------------------------------------
# Source discovery: approved workflow run, successful publish job, retained evidence artifact
# ----------------------------------------------------------------------------------------------

def _timestamp(mapping, key, what):
    value = mapping.get(key) if isinstance(mapping, dict) else None
    if type(value) is not str or not TIMESTAMP.fullmatch(value):
        raise UpdateError(f"{what}: {key} is not a UTC timestamp")
    try:
        datetime.datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        raise UpdateError(f"{what}: {key} is not a valid UTC timestamp") from None
    return value


def _list_runs(api, family):
    repository, workflow = family["source_repository"], family["workflow"].rsplit("/", 1)[1]
    runs = []
    for page in range(1, MAX_RUN_PAGES + 1):
        query = urllib.parse.urlencode({"branch": "main", "exclude_pull_requests": "true",
                                        "per_page": str(RUNS_PER_PAGE), "page": str(page)})
        data = _call(api, "listing publication runs", "GET",
                     f"repos/{repository}/actions/workflows/{workflow}/runs?{query}", expect=(200,))
        items = data.get("workflow_runs") if isinstance(data, dict) else None
        if not isinstance(items, list):
            raise UpdateError("the GitHub API returned an unexpected run list")
        runs.extend(items)
        if len(items) < RUNS_PER_PAGE:
            break
    return runs


def _approved_run(run, family):
    """True for a run of the approved workflow on main; raises for a structurally broken record."""
    if not isinstance(run, dict):
        raise UpdateError("the GitHub API returned an unexpected run record")
    _integer(run, "id", "run")
    _integer(run, "run_number", "run")
    _integer(run, "run_attempt", "run")
    _timestamp(run, "created_at", "run")
    _text(run, "head_sha", GIT_SHA, "run")
    repository = run.get("repository")
    head = run.get("head_repository")
    path = run.get("path")
    if type(path) is not str or not isinstance(repository, dict) or not isinstance(head, dict):
        raise UpdateError("the GitHub API returned an unexpected run record")
    return (run.get("head_branch") == "main"
            and run.get("event") in ALLOWED_RUN_EVENTS
            and path.split("@", 1)[0] == family["workflow"]
            and _same_name(repository.get("full_name"), family["source_repository"])
            and _same_name(head.get("full_name"), family["source_repository"]))


def _publisher_history(api, family, run):
    """Bounded actual publisher job history, including retained jobs from notifier-only retries."""
    repository = family["source_repository"]
    jobs = []
    for page in range(1, 4):
        data = _call(api, "listing publication jobs", "GET",
                     f"repos/{repository}/actions/runs/{run['id']}/jobs?filter=all&per_page=100&page={page}", expect=(200,))
        batch = data.get("jobs") if isinstance(data, dict) else None
        if not isinstance(batch, list):
            raise UpdateError("the GitHub API returned an unexpected job list")
        jobs.extend(batch)
        if len(batch) < 100:
            break
    else:
        raise UpdateError("publication job history exceeds the bounded lookback")
    matching = [job for job in jobs if isinstance(job, dict) and job.get("name") == PUBLISH_JOB]
    if not matching:
        return None
    for job in matching:
        attempt = _integer(job, "run_attempt", "job")
        if job.get("run_id") != run["id"] or job.get("head_sha") != run["head_sha"] or attempt > run["run_attempt"]:
            raise UpdateError("the publish job does not belong to the run")
    if len({job["run_attempt"] for job in matching}) != len(matching):
        raise UpdateError("publication job history has an ambiguous attempt")
    return matching


def _publish_job(api, family, run):
    """Select the latest actual publisher attempt, never an older success after a newer failure."""
    matching = _publisher_history(api, family, run)
    if not matching:
        return None
    latest = max(job["run_attempt"] for job in matching)
    matching = [job for job in matching if job["run_attempt"] == latest]
    if len(matching) != 1:
        raise UpdateError("the latest publication job attempt is ambiguous")
    job = matching[0]
    if job.get("status") != "completed" or job.get("conclusion") != "success":
        raise UpdateError("the latest publish attempt is incomplete or failed; no stale publication is used")
    _integer(job, "id", "job")
    _integer(job, "run_attempt", "job")
    _timestamp(job, "completed_at", "job")
    steps = job.get("steps")
    if not isinstance(steps, list):
        raise UpdateError("the publish job has no step list")
    outcome = {step.get("name"): step.get("conclusion") for step in steps if isinstance(step, dict)}
    if any(outcome.get(name) != "success" for name in PUBLISH_STEPS):
        raise UpdateError("the latest publish attempt has partial publication evidence")
    return job


def _read_evidence(api, family, run, job, registry):
    repository = family["source_repository"]
    name = f"image-version-manifest-{family['image']}-{run['id']}-{job['run_attempt']}"
    query = urllib.parse.urlencode({"name": name, "per_page": "100"})
    data = _call(api, "listing evidence artifacts", "GET",
                 f"repos/{repository}/actions/runs/{run['id']}/artifacts?{query}", expect=(200,))
    items = data.get("artifacts") if isinstance(data, dict) else None
    if not isinstance(items, list):
        raise UpdateError("the GitHub API returned an unexpected artifact list")
    matching = [item for item in items if isinstance(item, dict) and item.get("name") == name]
    if not matching:
        raise UpdateError("the publication evidence artifact is missing or expired; "
                          "request a fresh approved publication of the source image")
    if len(matching) != 1:
        raise UpdateError("the publication evidence artifact is ambiguous")
    artifact = matching[0]
    if artifact.get("expired") is not False:
        raise UpdateError("the publication evidence artifact has expired; "
                          "request a fresh approved publication of the source image")
    artifact_id = _integer(artifact, "id", "artifact")
    size = _integer(artifact, "size_in_bytes", "artifact", minimum=0)
    if size > MAX_ZIP_BYTES:
        raise UpdateError("the publication evidence artifact is too large")
    origin = artifact.get("workflow_run")
    if not isinstance(origin, dict) or origin.get("id") != run["id"] or origin.get("head_branch") != "main" \
            or origin.get("head_sha") != run["head_sha"]:
        raise UpdateError("the publication evidence artifact does not belong to the publication run")
    archive = api.download(f"repos/{repository}/actions/artifacts/{artifact_id}/zip", MAX_ZIP_BYTES)
    try:
        with zipfile.ZipFile(io.BytesIO(archive)) as bundle:
            infos = bundle.infolist()
            if len(infos) != 1 or infos[0].is_dir() or infos[0].file_size > MAX_EVIDENCE_BYTES:
                raise UpdateError("the publication evidence archive is not a single small file")
            with bundle.open(infos[0]) as handle:
                raw = handle.read(MAX_EVIDENCE_BYTES + 1)
    except (zipfile.BadZipFile, NotImplementedError, RuntimeError, EOFError, OSError):
        raise UpdateError("the publication evidence archive is malformed") from None
    if len(raw) > MAX_EVIDENCE_BYTES:
        raise UpdateError("the publication evidence is too large")
    evidence = _strict_json(raw, "the publication evidence")
    keys = {"repository", "tag", "digest", "reference", "source", "run_id", "run_attempt"}
    if not isinstance(evidence, dict) or set(evidence) != keys:
        raise UpdateError("the publication evidence does not have exactly the expected fields")
    image_repository = f"{registry}/base-images/{family['image']}"
    if evidence["repository"] != image_repository:
        raise UpdateError("the publication evidence names a different image repository or registry")
    tag = _text(evidence, "tag", TAG, "evidence")
    if tag == "latest":
        raise UpdateError("the publication evidence names the mutable latest tag")
    digest = _text(evidence, "digest", SHA256, "evidence")
    if evidence["reference"] != f"{image_repository}@{digest}":
        raise UpdateError("the publication evidence reference does not match its digest")
    if evidence["source"] != run["head_sha"] or evidence["run_id"] != str(run["id"]) \
            or evidence["run_attempt"] != str(job["run_attempt"]):
        raise UpdateError("the publication evidence does not match the publication run, attempt and commit")
    return {
        "image": family["image"], "repository": image_repository, "registry": registry, "tag": tag,
        "digest": digest, "reference": evidence["reference"], "config_digest": "", "runtime": "",
        "source_repository": family["source_repository"], "source_sha": run["head_sha"],
        "run_id": str(run["id"]), "run_attempt": str(job["run_attempt"]), "run_number": str(run["run_number"]),
        "event": run["event"], "completed_at": job["completed_at"], "created_at": run["created_at"],
    }


def find_publication(api, family, registry, notes):
    """Return the newest eligible publication by source workflow chronology, evidence verified.

    Runs are inspected newest first, bounded. A job decides eligibility, not the run conclusion, so a
    failed later notification job never hides a valid publication, and a failed or scan-only job is never
    adopted. Once a publication is chosen, a verification failure is an error: no older fallback.
    """
    runs = _list_runs(api, family)
    approved = [run for run in runs if _approved_run(run, family)]
    approved.sort(key=lambda run: (run["created_at"], run["id"]), reverse=True)
    for run in approved[:MAX_INSPECTED_RUNS]:
        job = _publish_job(api, family, run)
        if job is None:
            notes.append("A run of the approved workflow has no completed publish job and was skipped.")
            continue
        # Already sorted by source chronology: unrelated older failures cannot hide this success.
        return _read_evidence(api, family, run, job, registry)
    raise UpdateError("no completed publication of the approved source workflow was found in the "
                      "bounded lookback; request a fresh approved publication of the source image")


# ----------------------------------------------------------------------------------------------
# Registry identity and runtime compatibility
# ----------------------------------------------------------------------------------------------

def _require_digest(body, digest, header, what):
    if _sha256_of(body) != digest or (header and header != digest):
        raise UpdateError(f"the registry {what} bytes do not match the recorded digest")


def _manifest_type(manifest, header_type):
    declared = manifest.get("mediaType")
    if declared is not None and (type(declared) is not str or (header_type and declared != header_type)):
        raise UpdateError("the registry manifest media type is inconsistent")
    return declared or header_type


def verify_registry_image(client, family, publication):
    """Verify manifest and config by content hash and the supported runtime. Returns a summary."""
    repository = f"base-images/{family['image']}"
    digest = publication["digest"]
    header_type, body, header = client.get_manifest(repository, digest)
    _require_digest(body, digest, header, "manifest")
    manifest = _strict_json(body, "the registry manifest")
    if not isinstance(manifest, dict):
        raise UpdateError("the registry manifest is not an object")
    media_type = _manifest_type(manifest, header_type)
    manifest_digest = digest
    if media_type in INDEX_TYPES:
        entries = manifest.get("manifests")
        if not isinstance(entries, list):
            raise UpdateError("the registry index has no manifest list")
        picks = [entry for entry in entries if isinstance(entry, dict) and isinstance(entry.get("platform"), dict)
                 and entry["platform"].get("os") == "linux" and entry["platform"].get("architecture") == "amd64"]
        if len(picks) != 1:
            raise UpdateError("the registry index must contain exactly one linux/amd64 image")
        manifest_digest = _text(picks[0], "digest", SHA256, "index entry")
        header_type, body, header = client.get_manifest(repository, manifest_digest)
        _require_digest(body, manifest_digest, header, "image manifest")
        manifest = _strict_json(body, "the registry image manifest")
        if not isinstance(manifest, dict):
            raise UpdateError("the registry image manifest is not an object")
        media_type = _manifest_type(manifest, header_type)
    if media_type not in IMAGE_TYPES:
        raise UpdateError("the registry manifest is not a supported image manifest")
    config = manifest.get("config")
    if not isinstance(config, dict) or config.get("mediaType") not in CONFIG_TYPES:
        raise UpdateError("the registry manifest has no supported config descriptor")
    config_digest = _text(config, "digest", SHA256, "config descriptor")
    size = _integer(config, "size", "config descriptor")
    if size > MAX_CONFIG_BYTES:
        raise UpdateError("the registry config is too large")
    blob = client.get_blob(repository, config_digest, MAX_CONFIG_BYTES)
    if len(blob) != size or _sha256_of(blob) != config_digest:
        raise UpdateError("the registry config bytes do not match the recorded digest")
    image = _strict_json(blob, "the registry config")
    if not isinstance(image, dict) or image.get("os") != "linux" or image.get("architecture") != "amd64":
        raise UpdateError("the image is not linux/amd64")
    container = image.get("config")
    if not isinstance(container, dict):
        raise UpdateError("the image config has no container configuration")
    if container.get("User") != USER:
        raise UpdateError("the image does not run as the approved non-root user")
    runtime = family["runtime"]
    if runtime == "aspnet":
        env = container.get("Env")
        versions = [item[len("ASPNET_VERSION="):] for item in env if type(item) is str
                    and item.startswith("ASPNET_VERSION=")] if isinstance(env, list) else []
        if len(versions) != 1 or not re.fullmatch(re.escape(ASPNET_MAJOR) + r"\.[0-9]+\.[0-9]+", versions[0]):
            raise UpdateError(f"the image does not report an ASP.NET {ASPNET_MAJOR} runtime; "
                              "a different major needs separate reviewed compatibility work")
        runtime = f"aspnet {versions[0]}"
    # The version tag must still resolve to the recorded manifest: a moved tag is a tamper signal.
    header_type, tag_body, header = client.get_manifest(repository, publication["tag"])
    if _sha256_of(tag_body) != digest or (header and header != digest):
        raise UpdateError("the version tag no longer resolves to the recorded digest")
    return {"config_digest": config_digest, "image_manifest_digest": manifest_digest, "runtime": runtime}


# ----------------------------------------------------------------------------------------------
# Dockerfile patching (bytes only; never decoded, never executed)
# ----------------------------------------------------------------------------------------------

def _split_lines(data):
    lines = []
    for raw in data.splitlines(keepends=True):
        body = raw.rstrip(b"\r\n")
        lines.append((body, raw[len(body):]))
    return lines


def _instruction_starts(lines):
    starts, continued = {}, False
    for index, (body, _) in enumerate(lines):
        stripped = body.strip()
        if not stripped or stripped.startswith(b"#"):
            continue
        if not continued:
            starts[index] = stripped.split(None, 1)[0].upper()
        continued = stripped.endswith(b"\\")
    return starts


def _arg_names(lines, starts):
    """Map ARG name -> list of instruction indexes declaring it."""
    declared = {}
    for index, keyword in starts.items():
        if keyword == b"ARG":
            for token in lines[index][0].split()[1:]:
                declared.setdefault(token.split(b"=", 1)[0], []).append(index)
    return declared


def inspect_dockerfile(data, layout, image, registry):
    """Return (lines, index, (start, end), current_digest) for the single supported pin, or raise."""
    if len(data) > MAX_DOCKERFILE_BYTES or re.search(rb"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]", data):
        raise UpdateError("a Dockerfile is too large or not text")
    if re.search(rb"\r(?!\n)", data):
        raise UpdateError("a Dockerfile contains a bare carriage return, so its lines are ambiguous")
    lines = _split_lines(data)
    starts = _instruction_starts(lines)
    first_instruction = min(starts, default=len(lines))
    for body, _ in lines[:first_instruction]:
        if ESCAPE_DIRECTIVE.match(body.strip()):
            raise UpdateError("a Dockerfile changes the escape character")
    for index in starts:
        if HEREDOC.search(lines[index][0]):
            raise UpdateError("a Dockerfile uses heredoc syntax, which is not supported")
    from_lines = [index for index, keyword in starts.items() if keyword == b"FROM"]
    if not from_lines:
        raise UpdateError("a Dockerfile has no FROM instruction")
    first_from = from_lines[0]

    needle = b"base-images/" + image.encode("ascii")
    mentions = [index for index, (body, _) in enumerate(lines) if needle in body]
    if len(mentions) != 1 or data.count(needle) != 1:
        raise UpdateError("the base image must be named by exactly one declaration and nowhere else")
    index = mentions[0]
    body = lines[index][0]
    stage = layout["stage"]
    if stage:
        aliases = [lines[i][0].split()[-1].lower() for i in from_lines
                   if re.search(rb"\sAS\s+[A-Za-z][A-Za-z0-9_]*\s*$", lines[i][0], re.IGNORECASE)]
        if aliases.count(stage.encode().lower()) != 1:
            raise UpdateError("the base stage alias must be declared exactly once")
    digest = rb"@(?P<digest>sha256:[0-9a-f]{64})"
    if layout["kind"] == "registry-from":
        suffix = (rb" AS " + re.escape(stage.encode())) if stage else b""
        pin = re.compile(rb"FROM \$\{IMAGE_REGISTRY\}/base-images/" + re.escape(image.encode()) + digest
                         + suffix + rb"[ \t]*")
        if starts.get(index) != b"FROM" or not pin.fullmatch(body):
            raise UpdateError("the base image declaration is not the supported registry-variable FROM form")
        declared = _arg_names(lines, starts).get(b"IMAGE_REGISTRY", [])
        if len(declared) != 1 or declared[0] >= first_from or lines[declared[0]][0].rstrip().endswith(b"\\"):
            raise UpdateError("IMAGE_REGISTRY must have exactly one single-line global ARG declaration")
    else:
        name = layout["arg"].encode()
        pin = re.compile(rb"ARG " + re.escape(name) + b"=" + re.escape(registry.encode()) + b"/base-images/"
                         + re.escape(image.encode()) + digest + rb"[ \t]*")
        if starts.get(index) != b"ARG" or not pin.fullmatch(body) or index >= first_from:
            raise UpdateError("the base image declaration is not the supported global ARG default form")
        declared = _arg_names(lines, starts).get(name, [])
        if declared != [index]:
            raise UpdateError("the base image ARG must be declared exactly once")
        consume = re.compile(rb"FROM (?:\$\{" + re.escape(name) + rb"\}|\$" + re.escape(name) + rb") AS "
                             + re.escape(stage.encode()) + rb"[ \t]*")
        references = [i for i, (text, _) in enumerate(lines)
                      if i != index and (b"${" + name + b"}" in text or re.search(rb"\$" + re.escape(name) + rb"(?![A-Za-z0-9_])", text))
                      and not text.lstrip().startswith(b"#")]
        if len(references) != 1 or starts.get(references[0]) != b"FROM" or references[0] != first_from \
                or not consume.fullmatch(lines[references[0]][0]):
            raise UpdateError("the base FROM must be the first FROM and the only consumer of the base image ARG")
    found = pin.fullmatch(body)
    return lines, index, found.span("digest"), found.group("digest").decode("ascii")


def patch_dockerfile(data, layout, image, registry, digest):
    """Return (patched bytes, current digest). Only the digest characters on one line can change."""
    if type(digest) is not str or not SHA256.fullmatch(digest):
        raise UpdateError("the proposed digest is malformed")
    lines, index, (start, end), current = inspect_dockerfile(data, layout, image, registry)
    body, eol = lines[index]
    lines[index] = (body[:start] + digest.encode("ascii") + body[end:], eol)
    return b"".join(text + ending for text, ending in lines), current


# ----------------------------------------------------------------------------------------------
# Target branches: read as data through the Git API
# ----------------------------------------------------------------------------------------------

def _decode_blob(blob, sha):
    try:
        data = base64.b64decode("".join(blob["content"].split()), validate=True) if blob.get("encoding") == "base64" else None
    except (TypeError, KeyError, ValueError, AttributeError):
        data = None
    if data is None or len(data) > MAX_DOCKERFILE_BYTES or _git_blob_sha(data) != sha:
        raise UpdateError("a target file's content does not match its blob id")
    return data


def _resolve_entry(api, repo, tree_sha, path):
    parts = path.split("/")
    current = tree_sha
    for position, part in enumerate(parts):
        tree = _call(api, "reading a target tree", "GET", f"{repo}/git/trees/{current}", expect=(200,))
        listing = tree.get("tree") if isinstance(tree, dict) else None
        if not isinstance(listing, list) or tree.get("truncated"):
            raise UpdateError("a target tree is truncated or malformed")
        entries = [entry for entry in listing if isinstance(entry, dict) and entry.get("path") == part]
        if len(entries) != 1:
            raise UpdateError("a target path is missing or ambiguous")
        entry = entries[0]
        sha = entry.get("sha")
        if type(sha) is not str or not GIT_SHA.fullmatch(sha):
            raise UpdateError("a target tree entry has no valid object id")
        if position == len(parts) - 1:
            if entry.get("type") != "blob" or entry.get("mode") not in REGULAR_FILE_MODES:
                raise UpdateError("a target file is not a regular file (symlinks, submodules and directories are rejected)")
            return entry["mode"], sha
        if entry.get("type") != "tree" or entry.get("mode") != "040000":
            raise UpdateError("a target path component is not a directory")
        current = sha
    raise UpdateError("a target path is empty")


def read_target(api, repository, branch, files):
    """Return {parent, tree, files:[{path, mode, blob, data}]} for the target branch tip."""
    repo = f"repos/{repository}"
    ref = _call(api, "reading the target branch", "GET", f"{repo}/git/ref/heads/{branch}", expect=(200,))
    obj = ref.get("object") if isinstance(ref, dict) else None
    parent = obj.get("sha") if isinstance(obj, dict) else None
    if not isinstance(obj, dict) or obj.get("type") != "commit" or type(parent) is not str or not GIT_SHA.fullmatch(parent):
        raise UpdateError("the target branch did not resolve to a commit")
    commit = _call(api, "reading the target commit", "GET", f"{repo}/git/commits/{parent}", expect=(200,))
    tree_sha = commit.get("tree", {}).get("sha") if isinstance(commit, dict) and isinstance(commit.get("tree"), dict) else None
    if commit.get("sha") != parent or type(tree_sha) is not str or not GIT_SHA.fullmatch(tree_sha):
        raise UpdateError("the target commit has no valid tree")
    entries = []
    for path in files:
        mode, sha = _resolve_entry(api, repo, tree_sha, path)
        blob = _call(api, "reading a target file", "GET", f"{repo}/git/blobs/{sha}", expect=(200,))
        entries.append({"path": path, "mode": mode, "blob": sha, "data": _decode_blob(blob, sha)})
    return {"parent": parent, "tree": tree_sha, "files": entries}


def branch_for(family, digest, target, publication, bot_owner):
    stamp = _timestamp(publication, "created_at", "publication")[:10].replace("-", "")
    if bot_owner != "BOT" or family not in FAMILIES or target not in TARGET_BRANCHES or not SHA256.fullmatch(digest):
        raise UpdateError("invalid governed proposal identity")
    return f"feature/{stamp}_SF-2942_{bot_owner}_{family}-base-image-{digest[7:]}-{target}"


def _complete_proposal_list(api, repository, resource, filters):
    """Exhaust REST pages (not Search's capped index); never infer absence from a full last page."""
    items = []
    for page in range(1, 21):
        query = urllib.parse.urlencode(dict(filters, per_page=100, page=page))
        batch = _call(api, "listing proposal history", "GET", f"repos/{repository}/{resource}?{query}", expect=(200,))
        if not isinstance(batch, list) or len(batch) > 100:
            raise UpdateError("proposal history returned an unexpected page")
        items.extend(batch)
        if len(batch) < 100:
            return items
    raise UpdateError("proposal history exceeds the complete discovery bound; reconcile manually")


def _proposal_identity(branch):
    if type(branch) is not str or not BRANCH_NAME.fullmatch(branch):
        return None
    try:
        datetime.datetime.strptime(branch.split("/", 1)[1][:8], "%Y%m%d")
    except ValueError:
        return None
    return branch.split("_", 3)[3]  # family/full digest/target, deliberately independent of date


def preflight(api, repository, branch):
    """Return `proceed` or `already-proposed`; raise for an orphan branch."""
    if type(branch) is not str or not BRANCH_NAME.fullmatch(branch):
        raise UpdateError("the branch name is not a digest-derived automation branch")
    if type(repository) is not str or not REPOSITORY.fullmatch(repository):
        raise UpdateError("GITHUB_REPOSITORY is malformed")
    identity = _proposal_identity(branch)
    if identity is None:
        raise UpdateError("the proposal branch has an invalid publication date")
    target = branch.rsplit("-", 1)[1]
    pulls = _complete_proposal_list(api, repository, "pulls", {"state": "all", "base": target, "sort": "created", "direction": "desc"})
    refs = _complete_proposal_list(api, repository, "branches", {})
    proposed = set()
    for pull in pulls:
        if not isinstance(pull, dict) or not isinstance(pull.get("head"), dict) or not isinstance(pull.get("base"), dict):
            raise UpdateError("proposal pull request history is malformed")
        head, base = pull["head"], pull["base"]
        if _proposal_identity(head.get("ref")) != identity:
            continue
        source = head.get("repo")
        if not isinstance(source, dict) or not _same_name(source.get("full_name"), repository):
            raise UpdateError("matching proposal has unverifiable head repository")
        if base.get("ref") == target:
            proposed.add(head["ref"])
    existing = set()
    for ref in refs:
        if not isinstance(ref, dict) or type(ref.get("name")) is not str:
            raise UpdateError("proposal branch history is malformed")
        if _proposal_identity(ref["name"]) == identity:
            existing.add(ref["name"])
    if existing - proposed:
        raise UpdateError("the proposal branch exists without a pull request; reconcile it manually, it was not touched")
    if proposed:
        return "already-proposed"
    return "proceed"


# ----------------------------------------------------------------------------------------------
# Plan
# ----------------------------------------------------------------------------------------------

def _runtime_context(environ, profile):
    if environ.get("GITHUB_REF") != "refs/heads/main":
        raise UpdateError("the receiver must execute from the trusted main branch")
    repository = environ.get("GITHUB_REPOSITORY", "")
    if not _same_name(repository, profile["repository"]):
        raise UpdateError("the receiver is restricted to its profile repository")
    dry = environ.get("DRY_RUN", "false")
    if dry not in ("true", "false"):
        raise UpdateError("DRY_RUN must be true or false")
    return repository, dry == "true"


def _plan_path(environ):
    runner_temp = environ.get("RUNNER_TEMP") or ""
    if not os.path.isabs(runner_temp):
        raise UpdateError("RUNNER_TEMP is missing or not absolute")
    return Path(runner_temp) / PLAN_NAME


def publication_order(publication):
    # Source chronology plus actual publishing attempt; identical source commits can be rebuilt.
    return (publication["created_at"], int(publication["run_number"]), int(publication["run_id"]),
            int(publication["run_attempt"]))


def current_pin_order(source_api, family, registry, digest):
    """Resolve retained source provenance, never infer an unknown digest to be old."""
    if source_api is None:
        raise UpdateError("current pin has no verified provenance or reviewed bootstrap authorization")
    runs = [run for run in _list_runs(source_api, family) if _approved_run(run, family)]
    runs.sort(key=lambda run: (run["created_at"], run["id"]), reverse=True)
    for run in runs[:MAX_INSPECTED_RUNS]:
        history = _publisher_history(source_api, family, run) or []
        history.sort(key=lambda item: item["run_attempt"], reverse=True)
        for attempt in history:
            # Provenance proves what was published, not whether this run is eligible now.
            # Never even request artifacts for failed/incomplete/partial historical attempts.
            if attempt.get("conclusion") != "success" or attempt.get("status") != "completed":
                continue
            steps = attempt.get("steps")
            if not isinstance(steps, list):
                continue
            outcomes = {step.get("name"): step.get("conclusion") for step in steps if isinstance(step, dict)}
            if any(outcomes.get(name) != "success" for name in PUBLISH_STEPS):
                continue
            evidence = _read_evidence(source_api, family, run, attempt, registry)
            if evidence["digest"] == digest:
                return publication_order(evidence)
    raise UpdateError("current pin provenance is unknown or expired; review an exact target bootstrap entry")


def analyze_target(api, repository, profile, family, target, publication, source_api=None):
    """Return a decision dict. Never raises for a per-target problem: it is recorded as an error."""
    branch = target["branch"]
    decision = {"branch": branch, "status": "error", "detail": "", "current": ""}
    try:
        if target["paused"]:
            decision.update(status="paused", detail="this target is paused by reviewed configuration")
            return decision
        state = read_target(api, repository, branch, target["files"])
        digests = set()
        for item in state["files"]:
            _, current = patch_dockerfile(item["data"], target["layout"], family["image"], profile["registry"],
                                          publication["digest"])
            digests.add(current)
        if len(digests) != 1:
            raise UpdateError("the target files do not pin the same digest; reconcile them manually")
        current = digests.pop()
        decision["current"] = current
        if current == publication["digest"]:
            decision.update(status="noop-current", detail="the target already pins the verified digest")
            return decision
        bootstrap = next((pin for pin in target["bootstrap"] if pin["digest"] == current), None)
        if bootstrap is not None:
            if publication["created_at"] <= bootstrap["published_before"]:
                raise UpdateError("candidate is not newer than the reviewed bootstrap publication bound")
        else:
            order = current_pin_order(source_api, family, profile["registry"], current)
            if order >= publication_order(publication):
                decision.update(status="noop-newer", detail="current pin publication is newer; downgrade refused")
                return decision
        proposal = branch_for(profile["family"], publication["digest"], branch, publication, profile["botOwner"])
        if preflight(api, repository, proposal) == "already-proposed":
            decision.update(status="already-proposed", detail="a proposal for this digest already exists; it was not touched")
            return decision
        files = []
        for item in state["files"]:
            patched, _ = patch_dockerfile(item["data"], target["layout"], family["image"], profile["registry"],
                                          publication["digest"])
            files.append({"path": item["path"], "mode": item["mode"], "original_blob": item["blob"],
                          "new_blob": _git_blob_sha(patched)})
        decision.update(status="proposal", detail="a reviewed digest update is needed", parent=state["parent"],
                        tree=state["tree"], proposal_branch=proposal, files=files)
    except UpdateError as error:
        decision.update(status="error", detail=str(error))
    return decision


def _summary_lines(profile, family, publication, decisions, dry_run, result, public):
    lines = ["### Base image receiver: candidate digest report", "",
             f"- Consumer: `{profile['repository']}`", f"- Image family: `{profile['family']}`",
             f"- Result: **{result}**" + (" (dry run: no branch or pull request was created)" if dry_run else "")]
    if publication:
        lines += [f"- Verified image: `{publication['reference']}`", f"- Version tag: `{publication['tag']}`",
                  f"- Config digest: `{publication['config_digest']}`", f"- Runtime: `{publication['runtime']}`",
                  f"- Publication run: {publication['run_id']} (attempt {publication['run_attempt']})"]
        if not public:
            lines.append(f"- Publication run link: https://github.com/{publication['source_repository']}"
                         f"/actions/runs/{publication['run_id']}")
    if decisions:
        lines += ["", "| Target | Decision | Current pin |", "| --- | --- | --- |"]
        for item in decisions:
            lines.append(f"| `{item['branch']}` | {item['status']} | `{item['current'] or 'n/a'}` |")
    return lines


def paused_result(environ):
    _write_outputs({"result": "paused", "has_proposals": "false", "target_errors": "0"}, environ)
    _append_summary(["### Base image receiver", "", "Paused by reviewed configuration; nothing was evaluated."], environ)
    print("The receiver is paused by reviewed configuration; nothing was evaluated.")
    return {"result": "paused", "decisions": []}


def run_plan(environ, source_api, registry_client, target_api, profile):
    repository, dry_run = _runtime_context(environ, profile)
    family = FAMILIES[profile["family"]]
    hint = load_hint(environ, profile["family"])
    if profile["paused"]:
        return paused_result(environ)
    notes = []
    publication = find_publication(source_api, family, profile["registry"], notes)
    for note in notes:
        print(note)
    if hint and "run_id" in hint and hint["run_id"] != publication["run_id"]:
        print("The notified run is not the newest eligible verified publication; the newest verified one is processed.")
    if publication["digest"] in profile["suppressed"]:
        _write_outputs({"result": "suppressed", "has_proposals": "false", "target_errors": "0"}, environ)
        _append_summary(_summary_lines(profile, family, publication, [], dry_run, "suppressed", profile["public"]), environ)
        print("The newest verified publication is suppressed by reviewed configuration; nothing was proposed "
              "and no older publication is used instead.")
        return {"result": "suppressed", "publication": publication, "decisions": []}
    summary = verify_registry_image(registry_client, family, publication)
    publication = dict(publication, config_digest=summary["config_digest"], runtime=summary["runtime"])
    decisions = [analyze_target(target_api, repository, profile, family, target, publication, source_api)
                 for target in profile["targets"]]
    for item in decisions:
        print(f"Target {item['branch']}: {item['status']}" + (f" - {item['detail']}" if item["status"] == "error" else ""))
    proposals = [item for item in decisions if item["status"] == "proposal"]
    errors = [item for item in decisions if item["status"] == "error"]
    if errors:
        # Fail closed: one unsupported or inconsistent target stops the whole run before any plan file or
        # write credential exists, so a half-evaluated publication is never partially proposed.
        _write_outputs({"result": "errors", "has_proposals": "false", "target_errors": str(len(errors))}, environ)
        _append_summary(_summary_lines(profile, family, publication, decisions, dry_run, "errors", profile["public"]), environ)
        raise UpdateError("one or more targets could not be evaluated; nothing was proposed")
    result = "dry-run" if dry_run and proposals else ("proposals" if proposals else "noop")
    if proposals and not dry_run:
        plan = {"schema": PLAN_SCHEMA, "repository": repository, "family": profile["family"],
                "publication": publication,
                "targets": [{key: item[key] for key in ("branch", "parent", "tree", "proposal_branch", "files")}
                            for item in proposals]}
        _write_atomic(_plan_path(environ), json.dumps(plan, sort_keys=True).encode(), 0o600)
    _write_outputs({"result": result, "has_proposals": "true" if proposals and not dry_run else "false",
                    "target_errors": str(len(errors))}, environ)
    _append_summary(_summary_lines(profile, family, publication, decisions, dry_run, result, profile["public"]), environ)
    return {"result": result, "publication": publication, "decisions": decisions}


# ----------------------------------------------------------------------------------------------
# Propose: create-only
# ----------------------------------------------------------------------------------------------

def load_plan(path, profile):
    plan_file = Path(path)
    if plan_file.is_symlink() or not plan_file.is_file():
        raise UpdateError("the plan is missing or not a regular file")
    with plan_file.open("rb") as handle:
        raw = handle.read(MAX_PLAN_BYTES + 1)
    if len(raw) > MAX_PLAN_BYTES:
        raise UpdateError("the plan is too large")
    plan = _strict_json(raw, "the plan")
    if not isinstance(plan, dict) or set(plan) != {"schema", "repository", "family", "publication", "targets"} \
            or type(plan["schema"]) is not int or plan["schema"] != PLAN_SCHEMA or plan["family"] != profile["family"] \
            or not _same_name(plan["repository"], profile["repository"]):
        raise UpdateError("the plan does not have exactly the expected fields for this profile")
    publication = parse_publication(plan["publication"], profile)
    allowed = {target["branch"]: target for target in profile["targets"]}
    targets = plan["targets"]
    if not isinstance(targets, list) or not targets:
        raise UpdateError("the plan has no targets")
    seen = set()
    for target in targets:
        if not isinstance(target, dict) or set(target) != {"branch", "parent", "tree", "proposal_branch", "files"}:
            raise UpdateError("a plan target does not have exactly the expected fields")
        branch = target["branch"]
        if branch not in allowed or branch in seen or allowed[branch]["paused"]:
            raise UpdateError("a plan target is not an allowed, unpaused profile target")
        seen.add(branch)
        for key in ("parent", "tree"):
            _text(target, key, GIT_SHA, "plan target")
        if target["proposal_branch"] != branch_for(profile["family"], publication["digest"], branch, publication, profile["botOwner"]):
            raise UpdateError("a plan target branch is not the deterministic proposal branch")
        files = target["files"]
        if not isinstance(files, list) or [f.get("path") if isinstance(f, dict) else None for f in files] != allowed[branch]["files"]:
            raise UpdateError("the plan files differ from the profile allowlist")
        for item in files:
            if set(item) != {"path", "mode", "original_blob", "new_blob"} or item["mode"] not in REGULAR_FILE_MODES:
                raise UpdateError("a plan file does not have exactly the expected fields")
            _text(item, "original_blob", GIT_SHA, "plan file")
            _text(item, "new_blob", GIT_SHA, "plan file")
    return publication, targets


def parse_publication(data, profile):
    keys = {"image", "repository", "registry", "tag", "digest", "reference", "config_digest", "runtime",
            "source_repository", "source_sha", "run_id", "run_attempt", "run_number", "event", "completed_at", "created_at"}
    family = FAMILIES[profile["family"]]
    if not isinstance(data, dict) or set(data) != keys:
        raise UpdateError("the plan publication does not have exactly the expected fields")
    if data["image"] != family["image"] or data["registry"] != profile["registry"] \
            or data["repository"] != f"{profile['registry']}/base-images/{family['image']}" \
            or data["source_repository"] != family["source_repository"] or data["event"] not in ALLOWED_RUN_EVENTS:
        raise UpdateError("the plan publication is not for this profile")
    _text(data, "tag", TAG, "plan publication")
    digest = _text(data, "digest", SHA256, "plan publication")
    _text(data, "config_digest", SHA256, "plan publication")
    _text(data, "source_sha", GIT_SHA, "plan publication")
    for key in ("run_id", "run_attempt", "run_number"):
        _text(data, key, COUNTER, "plan publication")
    _text(data, "completed_at", TIMESTAMP, "plan publication")
    _timestamp(data, "created_at", "plan publication")
    if data["reference"] != f"{data['repository']}@{digest}" or data["tag"] == "latest" \
            or type(data["runtime"]) is not str or not re.fullmatch(r"[A-Za-z0-9 ._-]{1,64}", data["runtime"]):
        raise UpdateError("the plan publication identity is inconsistent")
    return data


def pull_request_body(profile, publication, target, files, current, public):
    rows = [("Image", f"`{publication['reference']}`"), ("Previous pin", f"`{current}`"),
            ("Version tag", f"`{publication['tag']}`"), ("Config digest", f"`{publication['config_digest']}`"),
            ("Runtime", f"`{publication['runtime']}`"),
            ("Publication run", f"{publication['run_id']} (attempt {publication['run_attempt']})")]
    if not public:
        source = publication["source_repository"]
        rows[-1] = ("Publication run", f"[{publication['run_id']} (attempt {publication['run_attempt']})]"
                    f"(https://github.com/{source}/actions/runs/{publication['run_id']})")
        rows.append(("Source commit", f"[`{publication['source_sha']}`](https://github.com/{source}/commit/{publication['source_sha']})"))
    table = "| | |\n| --- | --- |\n" + "\n".join(f"| {key} | {value} |" for key, value in rows)
    changed = "\n".join(f"- `{item['path']}`" for item in files)
    return (
        f"Pins the `{profile['family']}` base image to a newly verified canonical publication on `{target}`.\n\n"
        f"{table}\n\n"
        f"Changed files (only the 64 hex characters of the pinned digest; all other bytes and file modes are preserved):\n"
        f"{changed}\n\n"
        "### How this was verified\n"
        "The receiver discovered the newest eligible publication itself (a repository_dispatch notification is "
        "only a hint). It verified the approved source workflow on main, a successful publish job, the retained "
        "evidence artifact for that run, attempt and commit, and then the authenticated registry: manifest and "
        "config bytes hash to the recorded digests, the version tag still resolves to the digest, the image is "
        f"linux/amd64, runs as the non-root user and reports the supported runtime (`{publication['runtime']}`).\n\n"
        "### Human review required\n"
        "Check CI, including the authenticated image build. Nothing is merged, updated, closed or deleted "
        "automatically; later runs for the same digest leave this pull request and its branch untouched.\n\n"
        "### Rollback\n"
        "Revert this change (or pin a retained digest) through review, and add the rejected digest to "
        "`suppressed_digests` in `scripts/base_image_receiver_profile.json` on `main` so it is not proposed "
        "again. Set `paused` to stop all proposals. The receiver never downgrades or falls back automatically.\n"
    )


def _create_proposal(api, repository, profile, publication, target):
    repo = f"repos/{repository}"
    branch = target["proposal_branch"]
    base = target["branch"]
    ref = _call(api, "re-reading the target branch", "GET", f"{repo}/git/ref/heads/{base}", expect=(200,))
    if not isinstance(ref, dict) or not isinstance(ref.get("object"), dict) or ref["object"].get("sha") != target["parent"]:
        raise UpdateError("the target branch moved after planning; nothing was created, rerun the receiver")
    if preflight(api, repository, branch) == "already-proposed":
        return None
    family = FAMILIES[profile["family"]]
    layout = next(item["layout"] for item in profile["targets"] if item["branch"] == base)
    entries, pins = [], set()
    for item in target["files"]:
        blob = _call(api, "reading a planned file", "GET", f"{repo}/git/blobs/{item['original_blob']}", expect=(200,))
        original = _decode_blob(blob, item["original_blob"])
        patched, current = patch_dockerfile(original, layout, family["image"], profile["registry"], publication["digest"])
        pins.add(current)
        if _git_blob_sha(patched) != item["new_blob"]:
            raise UpdateError("a planned file no longer matches the validated patch")
        before, after = original.splitlines(keepends=True), patched.splitlines(keepends=True)
        if len(before) != len(after) or sum(1 for a, b in zip(before, after) if a != b) != 1:
            raise UpdateError("a patched file is not a single-line change")
        created = _call(api, "creating a blob", "POST", f"{repo}/git/blobs",
                        {"content": base64.b64encode(patched).decode("ascii"), "encoding": "base64"}, expect=(201,))
        if not isinstance(created, dict) or created.get("sha") != item["new_blob"]:
            raise UpdateError("creating a blob returned an unexpected id")
        entries.append({"path": item["path"], "mode": item["mode"], "type": "blob", "sha": item["new_blob"]})
    if len(pins) != 1:
        raise UpdateError("the planned files do not pin the same digest")
    current_digest = pins.pop()
    tree = _call(api, "creating the tree", "POST", f"{repo}/git/trees", {"base_tree": target["tree"], "tree": entries},
                 expect=(201,))
    short = publication["digest"][7:19]
    commit = _call(api, "creating the commit", "POST", f"{repo}/git/commits", {
        "message": f"[SF-2942] fix(deps): adopt {profile['family']} base image {publication['digest']}",
        "tree": tree.get("sha") if isinstance(tree, dict) else None, "parents": [target["parent"]]}, expect=(201,))
    for created in (tree, commit):
        if not isinstance(created, dict) or type(created.get("sha")) is not str or not GIT_SHA.fullmatch(created["sha"]):
            raise UpdateError("the GitHub API returned an unexpected object id")
    ref = _call(api, "re-checking the target branch", "GET", f"{repo}/git/ref/heads/{base}", expect=(200,))
    if not isinstance(ref, dict) or not isinstance(ref.get("object"), dict) or ref["object"].get("sha") != target["parent"]:
        raise UpdateError("the target branch moved while the commit was prepared; no branch or pull request was created")
    status, _ = api.request("POST", f"{repo}/git/refs", {"ref": f"refs/heads/{branch}", "sha": commit["sha"]})
    if status != 201:
        if status == 422:
            raise UpdateError("the proposal branch already exists (created after the preflight); it was not adopted, "
                              "updated or deleted and no pull request was created")
        raise UpdateError(f"creating the branch failed (HTTP {status})")
    try:
        pull = _call(api, "creating the pull request", "POST", f"{repo}/pulls", {
            "title": f"fix(deps): adopt {profile['family']} base image {short}",
            "head": branch, "base": base, "draft": False,
            "body": pull_request_body(profile, publication, base, target["files"], current_digest, profile["public"])},
            expect=(201,))
        number = pull.get("number") if isinstance(pull, dict) else None
        if type(number) is not int or number < 1:
            raise UpdateError("the GitHub API returned an unexpected pull request")
    except UpdateError as error:
        raise UpdateError(f"{error}; the new branch was left in place for manual reconciliation, "
                          "nothing was updated or deleted") from None
    try:  # cosmetic, best effort
        api.request("POST", f"{repo}/issues/{number}/labels", {"labels": [LABEL]})
    except UpdateError:
        print("WARNING: the pull request label could not be added.")
    return number


def run_propose(environ, api, profile):
    repository, dry_run = _runtime_context(environ, profile)
    if dry_run:
        raise UpdateError("refusing to propose during a dry run")
    if profile["paused"]:
        raise UpdateError("the receiver is paused by reviewed configuration")
    publication, targets = load_plan(_plan_path(environ), profile)
    if publication["digest"] in profile["suppressed"]:
        raise UpdateError("the planned digest is suppressed by reviewed configuration")
    failures, created = 0, []
    for target in targets:
        try:
            number = _create_proposal(api, repository, profile, publication, target)
            if number is None:
                print(f"Target {target['branch']}: a proposal appeared after planning and was preserved.")
            else:
                created.append((target["branch"], number))
                print(f"Target {target['branch']}: created pull request #{number}.")
        except UpdateError as error:
            failures += 1
            print(f"REJECTED ({target['branch']}): {error}", file=sys.stderr)
    _write_outputs({"created": str(len(created)), "failed": str(failures)}, environ)
    _append_summary(["### Base image receiver: proposals", ""] + [
        f"- `{branch}`: pull request #{number}" for branch, number in created] +
        ([f"- {failures} target(s) failed; see the log."] if failures else []), environ)
    return failures


# ----------------------------------------------------------------------------------------------
# Entry point
# ----------------------------------------------------------------------------------------------

def build_clients(environ, profile, http=None):
    http = http or Http()
    base = environ.get("GITHUB_API_URL") or "https://api.github.com"
    source = GitHubApi(environ.get("GH_SOURCE_TOKEN", ""), http, base)
    registry = RegistryClient(profile["registry"], environ.get("REGISTRY_PULL_USERNAME", ""),
                              environ.get("REGISTRY_PULL_PASSWORD", ""), http)
    target = GitHubApi(environ.get("GH_TOKEN", ""), http, base)
    return source, registry, target


def main(argv=None, environ=None, profile_path=None, http=None):
    environ = os.environ if environ is None else environ
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("command", choices=("validate-event", "plan", "propose"))
    args = parser.parse_args(argv)
    try:
        profile = load_profile(profile_path or Path(__file__).resolve().parent / PROFILE_NAME)
        if args.command == "validate-event":
            _runtime_context(environ, profile)
            load_hint(environ, profile["family"])
            _write_outputs({"paused": "true" if profile["paused"] else "false"}, environ)
        elif args.command == "plan":
            if profile["paused"]:
                _runtime_context(environ, profile)
                load_hint(environ, profile["family"])
                paused_result(environ)
                return 0
            source, registry, target = build_clients(environ, profile, http)
            run_plan(environ, source, registry, target, profile)
        else:
            api = GitHubApi(environ.get("GH_TOKEN", ""), http or Http(), environ.get("GITHUB_API_URL") or "https://api.github.com")
            return 1 if run_propose(environ, api, profile) else 0
    except UpdateError as error:
        print(f"REJECTED: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
