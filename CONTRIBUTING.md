# Contributing

## Reviewed trunk

`main` is the only maintained long-lived source and release branch. Keep it
buildable and releasable. Create a small, backwards-compatible feature/fix branch
from current `main`, open a PR to `main`, and integrate within a day or a few days.
Split larger work into compatible increments; discuss exceptions with the owner
before a branch becomes permanent. No long-lived feature, development, release or
environment branches. Runtime staging/production environments remain independent
of source branches, including downstream repositories' own `main`/`dev` lines.

Require at least one independent approval, CODEOWNER review, resolved discussions
and successful **`tests`**, **`scan`**, **`validate`** checks on the current PR.
Missing private-registry credentials visibly block `scan`; they are not a pass.
Use a normal merge commit for reviewed feature PRs; never push directly to trunk,
force-push shared branches or bypass required checks. Delete merged short-lived
branches after checking for new work. The one-time `dev` reconciliation preserves
ancestry (#82 and #84); it is not a recurring integration branch workflow.

For an existing `dev`-based contribution, coordinate with its author, retarget only
after its baseline is on `main`, inspect the full diff and commits and rerun CI and
review. Preserve unique work on any retired branch with an explicit disposition.
Historical source snapshots are retained but are not promised to rebuild unchanged.

## BGS image releases

`Dockerfile_bgs` and this repository own the image recipe and delivery. The
committed internal Alpine digest is authoritative. Do not replace it with public
Alpine or copy the recipe into another image repository. The former non-BGS
`Dockerfile` and GuildSwarm Jenkins route are retired (owner confirmed SF-2947).
They are historical, unsupported entry points, not alternate image writers.

PRs build/test/scan without publishing. A push to protected `main`, or an
authorized manual rebuild selected on `main`, builds without cache, scans the
exact built image, verifies its immutable remote manifest/config and promotes
`latest` only after version verification. There is no tag-triggered publisher.
The separate release job checks effective branch/tag rules and that the source
is the merge commit of a PR to `main`, then creates an annotated immutable Git tag:

- Git release: `bgs-image/tgf-<run_number>.<run_attempt>` on the reviewed source SHA.
- Registry build tag: `tgf-<run_number>.<run_attempt>` (never reused).
- Consumption identity: `omicsflowinternal.azurecr.io/base-images/the_good_framework@sha256:…`.
- `latest`: discovery only, never a reviewed build dependency.

The Git tag annotation and `tgf-release-<run>.<attempt>` artifact bind the source,
run, unique build tag, OCI manifest/config digests and scan-report SHA-256.
`tgf-publication-<run>.<attempt>` retains scan/publication evidence even if tagging
fails. A registry build can exist without being an authorized tagged release:
failed scan, verification, tag creation/readback or evidence upload never notifies
consumers. No Git release identity is moved or repaired by overwriting it.

Rerun a failed publication with a **new run attempt**, producing new identities.
An orphaned tag object or partially published build is diagnostic evidence, not
permission to reuse its identity. For a failed notification after a complete
release, inspect the existing reviewed adoption PR before requesting a new run;
digest-deduplication must preserve human work. Security rebuilds of unchanged
reviewed source also use a new run/attempt and a new Git tag. Schedule automation
must invoke the same reviewed-main path; there is no unrestricted scheduled writer.
These image identities do not change `version`, NuGet versions or existing `v…`
package tags.

Hotfix: branch from current `main`, make a minimal compatible fix, run normal
tests/scan and independent review, merge the PR, then release that reviewed merge
commit through the same pipeline. No direct production hotfix pushes or persistent
release branches. Recovery/rollback is a reviewed consumer Dockerfile pin PR to a
retained known-good digest, followed by normal consumer CI and review; it does not
move a Git tag, recreate `dev`, or deploy production automatically.

Retain every referenced digest and approved rollback artifact, its build tag, Git
tag and mapping/scan evidence. Before registry cleanup, inventory all maintained
consumer pins and rollback choices and exclude them. GitHub artifact expiry is not
durable retention: the owner must archive required scan evidence before expiry.
Registry retention enforcement and a real rollback rehearsal are delivery gates,
not established by this policy alone.

## Dependency propagation

Pin owners review internal Alpine updates here. Review integrity and vulnerability
evidence; any fixable vulnerability blocks image publication. Verified tagged TGF
publication notifies Common with the existing digest contract. Common opens a
reviewed exact-pin PR; its publication proposes reviewed Manager/Orchestrator
updates. Keep maintained consumer `main` and `dev` compatible with approved
artifacts. No auto-merge, implicit `latest` adoption or overwrite of human proposals.

## GitHub enforcement and administrator setup

Repository owner **@eyagovbusiness** administers settings; collaborator access alone
cannot enable these rules. Check effective rules after configuration, not merely
the policy or CODEOWNERS file. The release job fails closed unless the required
rules below can be read and verified.

1. Active branch ruleset for `refs/heads/main`: require PR, one independent
   approval, dismiss stale approvals, CODEOWNER review, resolved threads, current
   `tests`, `scan`, `validate` checks; block deletion and non-fast-forward updates.
   No contributor/admin bypass of review or checks. Limit workflow changes by
   CODEOWNER review. Required workflows have no path filters.
2. Active **tag immutability** ruleset: include exactly
   `refs/tags/bgs-image/**/*`, no exclusions, restrict updates and deletions,
   **no bypass actors**, including administrators and the PAT owner.
3. Separate active **tag creation** ruleset: same exact include, no exclusions,
   restrict creation; only repository administrators (RepositoryRole ID 5) may
   bypass creation. Reuse the existing `PAT_TOKEN` secret from TGF's successful
   Common-notification workflow; no new App, App secrets or environment is required.
   Its identity must be a TGF administrator allowed to create these tags, with
   contents write and PR read access. The workflow supplies it only to tag creation
   and notification, never the build/scanner. Dispatch success establishes existing
   Common access, not proof of TGF tag-write authorization; the first release must
   demonstrate that operation. Failed authorization never triggers adoption.

These rules require actual administrator setup before the new pipeline can finish.
Do not add update/deletion bypass to work around a failure. Record settings readback,
failed unauthorized creation/update/delete probes, clean-checkout contributor and
hotfix rehearsals, real tagged publication, cold consumer builds and rollback in
SF-2947 before declaring the transition complete. Remote `dev` deletion additionally
requires a fresh inventory, exact deletion/recovery commands and explicit owner
execution confirmation.
