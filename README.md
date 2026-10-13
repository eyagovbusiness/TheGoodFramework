# TheGoodFramework
This project is a set of personal libraries I developed to use in my other projects, it contains pattern implementations, code abstractions and extensions. The most remarkable part is a framework for developing web applications following Clean Architecture(CA) and Domain Driven Design(DDD), a customized Railway Oriented Programming(ROP) library and many other web application abstractions with a big impact on reducing the development time of my projects.
# BGS base-image migration status

The BGS image (`Dockerfile_bgs`, built by `local_deploy.sh --bgs` and
`make local-bgs`) is based on one immutable canonical Alpine image. The literal
`ARG ALPINE_BASE_IMAGE=<reference>` default in `Dockerfile_bgs` is the single
authoritative pin, and it is changed only through review. Do not duplicate it in
workflow variables or other files.

`scripts/validate_bgs_alpine_base_image.sh` resolves and validates the
reference before the project archive is created or Docker starts. When
`ALPINE_BASE_IMAGE` is unset it reads that committed default; when it is set,
even to an empty string, the supplied override must pass the same validation
and is never silently replaced. The reference must match
`host[:port]/base-images/alpine[:descriptive-tag]@sha256:<64-lowercase-hex>`;
tag-only references and `latest`, including `latest@sha256`, fail. The registry
host stays configurable through the reference itself. The non-BGS `Dockerfile`
and Jenkins pipelines, and non-BGS local builds including those pushed to
external registries, are unchanged.

## Canonical image delivery

`main` is the single writer of the canonical image
`<registry>/base-images/the_good_framework`. `scripts/bgs_image_ci.sh` is the CI
entry point. It builds with `--provenance=false --sbom=false` so the published
reference is one verifiable image manifest rather than an index.

- **Pull requests to `dev` or `main`, and `dev` pushes** (`L1_dev_pr.yml`,
  `L1_dev_merge.yml`): `scan` builds the image, scans that exact image, and never
  pushes or notifies anyone. Workflows use `pull_request`, never
  `pull_request_target`, so a fork pull request receives no secrets. Without
  registry credentials nothing is built or scanned, so the job fails with a
  `BLOCKED` error annotation and job summary instead of passing. The
  credential-free `validate` job still runs the static checks and unit tests.
- **`main`** (`L1_main.yml`): `publish` accepts only a push or dispatch on
  `refs/heads/main` in this repository, checks out exactly `github.sha`, and
  requires that commit to be the current tip of `main`. That is checked at the
  start, again immediately before the version push, and again before `latest`
  moves; a lookup error counts as "moved". The version tag must also still be
  absent immediately before its push, where only a definite "no such manifest"
  answer counts as free. A registry cannot create a tag atomically, so a tag
  created between that last check and the push is a residual race that the run
  will detect afterwards, not prevent. A newer unrelated commit can therefore
  make an older run stop; the newest run publishes. Runs are serialized.
- **Identity binding.** The image is saved by its local id, and
  `scripts/bgs_image_identity.py` reads only `manifest.json`, `index.json` and
  the config blob from the archive, hashes the real config bytes, and requires
  exactly one image. The Trivy report must name that config digest in
  `Metadata.ImageID`; the Docker image id is not used because under containerd it
  is a manifest digest. After the push, the registry manifest is fetched by
  digest, its bytes must hash to that digest, be a single image manifest, and name
  the same config. The version tag and, after it moves, `latest` must both resolve
  to that verified manifest.
- **Records.** `publication.json` is written after the version tag is verified and
  before `latest` moves. It carries `image_ref`, `manifest_digest`,
  `config_digest`, `local_image_id`, the source commit and the run, as separate
  identities. `latest-status.txt` says `promoted`, `not-promoted` or
  `pushed-unverified`. If `latest` fails, the version tag and both files remain.
- **Scan policy** (`scripts/bgs_scan_policy.py`, public Trivy pinned by digest):
  any fixable vulnerability, of any severity including UNKNOWN, blocks; unfixed
  ones are reported. A report is rejected when it is for another image, carries
  scanner `Error`/`Errors`, has blank required text, lacks exactly one Alpine OS
  inventory with versions, includes another OS or class, or lists a Go binary
  without versions. There is no exception for versionless Go entries because this
  image ships no Go binaries.
- **Credentials.** The registry login and Docker config are job-scoped and
  removed on exit; the password is passed on stdin and unset after login, and
  GitHub masks the secret values in logs. Docker warns that the temporary config
  is unencrypted, which is why it is deleted. The GitHub token lives in an
  unexported shell variable and is handed only to the one `gh api` call, so the
  build, Docker and the scanner never receive it. The token is not persisted by
  checkout.
- **No automatic downstream adoption.** The former repository dispatch was
  removed. Consumers such as OF.Common adopt the digest from the
  `tgf-publication-<run>.<attempt>` artifact in a reviewed pull request.
- Local `--bgs` builds are tagged `base-images/the_good_framework:<tag>` and
  cannot be pushed: `local_deploy.sh --bgs --registry-push` and
  `make push local-bgs` are rejected.

## Remaining gates

Review and merge the change to `main` first. Publication then happens from the
first `main` push after that, and the resulting digest should be recorded in a
reviewed consumer pin. The CI registry credentials must be allowed to create and
push `base-images/the_good_framework`. The previous per-environment image path
(`base-images/<environment>/the_good_framework`) is no longer written for the
BGS image. The publication path has not yet run against the real registry.