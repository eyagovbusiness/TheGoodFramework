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
entry point and runs only in GitHub Actions with registry credentials in a
job-scoped Docker config that is removed on exit.

- **Pull requests to `dev` or `main`, and `dev` pushes** (`L1_dev_pr.yml`, `L1_dev_merge.yml`):
  `scan` builds the image, scans that exact image, and never pushes or notifies
  anyone. A fork pull request has no secrets and cannot pull the private base
  image, so its build and scan stay pending until a trusted run.
- **`main`** (`L1_main.yml`): `publish` accepts only a push or dispatch on
  `refs/heads/main` in this repository, checks out exactly `github.sha`, and
  refuses to run unless that commit is still the tip of `main`. This can skip an
  older, unrelated commit when a newer one lands; the newest run publishes. It
  then pushes `tgf-<run>.<attempt>`, writes `publication.json`, and only then
  moves `latest` to the same digest. If `latest` fails, the version tag and its
  record remain. An existing version tag fails the run, and only a definite
  "no such manifest" answer counts as free.
- **Scan policy** (`scripts/bgs_scan_policy.py`, public Trivy pinned by digest):
  any fixable vulnerability, of any severity, blocks publication; unfixed ones
  are reported. A report that does not prove the Alpine package inventory, or
  that lists a Go binary without versions, is rejected.
- **No automatic downstream adoption.** The former repository dispatch was
  removed. Consumers such as OF.Common adopt the digest from the
  `tgf-publication-<run>.<attempt>` artifact (`image_ref`, source commit and
  run) in a reviewed pull request.
- Local `--bgs` builds are tagged `base-images/the_good_framework:<tag>` and
  cannot be pushed: `local_deploy.sh --bgs --registry-push` and
  `make push local-bgs` are rejected.

## Remaining gates

Review and merge the change to `main` first. Publication then happens from the
first `main` push after that, and the resulting digest should be recorded in a
reviewed consumer pin. The CI registry credentials must be allowed to create and
push `base-images/the_good_framework`. The previous per-environment image path
(`base-images/<environment>/the_good_framework`) is no longer written for the
BGS image.