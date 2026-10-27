# Verified private base-image receiver (SF-2942)

**Status: implemented locally, pending reviewed rollout and live validation.** No deployment,
image publication, Git ref, PR, secret or installation has been created by this implementation.

## Inventory and provenance

The canonical standard-library Python engine is `Biognosys/of.devops:scripts/base_image_receiver.py`.
Its two `tests/test_base_image_receiver*.py` suites, this runbook and test workflow are vendored
byte-for-byte in each consumer. This permits public TGF to run without calling a private reusable
workflow. Changes must be reviewed together and copies compared by SHA-256. Record the canonical
reviewed commit and copy hashes in each rollout PR once commits are authorized. Workstation-specific
delivery helpers are local delivery artifacts, excluded from repository candidates and commits.

| Consumer | Family | Target branches | Allowlisted files | Daily UTC |
| --- | --- | --- | --- | --- |
| eyagovbusiness/TheGoodFramework | Alpine | main | Dockerfile_bgs global ALPINE_BASE_IMAGE ARG, base stage | 04:13 |
| Biognosys/of.frontend | Alpine | main, dev | Dockerfile + Dockerfile_internal, one atomic commit per target | 04:23 |
| Biognosys/of.manager | ASP.NET | main, dev | src/SF.Manager.API/Dockerfile, base stage | 04:33 |
| Biognosys/of.orchestrator | ASP.NET | main, dev | src/SF.Orchestrator.API/Dockerfile, base stage | 04:43 |

All active backend branches already use .NET10. TGF dev is deleted and is not recreated. Only the
runtime digest changes: Frontend Bun, service Common pins, SDKs, users, entrypoints, self-contained
publication, file modes, CRLF and unrelated bytes stay as read from each target branch.

## Trigger and evidence contract

Receiver workflow: `.github/workflows/base-image-receiver.yml`. It runs only in main context and
checks out the event SHA with persisted credentials disabled. It never checks out or executes dev
code. Each target branch tip, tree and allowlisted blob is read through the Git database API.

Event `base-image-published-v1` has exactly these client_payload fields:

```json
{"schema":1,"image":"alpine","source_repo":"Biognosys/bgsg.base-images","run_id":"42","run_attempt":"1"}
```

`image` is `alpine` or `aspnet` for its recipient; schema is an integer and run identifiers are
positive decimal strings. The event is only a discovery hint. Daily and manual runs use identical
bounded discovery (100 source runs listed, 10 newest approved runs inspected). Only main push,
schedule or workflow_dispatch runs of `alpine-main.yml` / `aspnet-main.yml` in the canonical source
are eligible. The newest successful publication is selected by source-run creation chronology;
rerunning an older run does not displace a newer source publication. No mutable latest discovery
and no fallback past invalid evidence is allowed. No evidence in the bounded window is an actionable
failure requiring a fresh approved source publication.

The exact job `build-and-push / publish-image` must succeed, including `Publish scanned identity`
and `Upload version manifest evidence`; the aggregate run may fail because its notifier failed.
The engine reads bounded `filter=all` job history and selects the latest actual publisher attempt,
which must be no greater than the aggregate run attempt. A notifier-only retry (aggregate2,
publisher1, notifier2) consumes artifact attempt1. A real publisher2 requires artifact attempt2.
Failed, incomplete, partial or ambiguous latest publisher attempts fail closed, never selecting an
earlier successful attempt. Duplicate matching artifacts also fail closed.
The single retained artifact is `image-version-manifest-{image}-{run_id}-{publish_job_attempt}`. Its source
commit, run, attempt, registry repository, immutable reference and version tag must match. Artifact
expiry (30-day publisher retention), wrong identity, ambiguous records or partial publication fail.

Pull-authenticated registry checks hash manifest and config bytes, support a single image or an
index with exactly one linux/amd64 image, require the non-root `biognosys` config user, and require
ASPNET_VERSION to be 10.x.y for ASP.NET. The version tag must still resolve to the recorded digest.
A different ASP.NET major requires separate reviewed compatibility work. Alpine compatibility is
the canonical Alpine family plus linux/amd64/non-root identity; its .NET10 SDK remains untouched.
GitHub artifact and registry blob redirects receive no Authorization header; registry Basic auth
is sent only to a same-host HTTPS bearer realm requesting `repository:<image>:pull`.

## Credentials and administrative ownership

Provision through the responsible repository/organization administrators after review; never put
values in source, summaries or PRs. The workflow references dedicated credentials, not publisher
push credentials. Actual installation permissions and effective registry scopes must be checked
by administrators before live acceptance.

| Setting | Owner / minimum effective access | Used by |
| --- | --- | --- |
| BASE_IMAGE_SOURCE_READ_TOKEN | Biognosys administrator; only bgsg.base-images Actions read, metadata read; short-lived or rotated fine-grained identity | plan step only |
| BASE_IMAGE_PULL_USERNAME / BASE_IMAGE_PULL_PASSWORD | Registry owner; repository-scoped pull on base-images/alpine or base-images/aspnet, no push/delete | plan step only |
| BASE_IMAGE_PROPOSAL_APP_ID / BASE_IMAGE_PROPOSAL_APP_PRIVATE_KEY | Consumer owner; App installed only on that consumer with contents + pull requests write, metadata read; no workflows/admin/members permission | token step only, after verified plan has proposals |
| github.token | Consumer repository contents + pull requests read | checkout and plan only |

For public cross-owner TGF, Biognosys must explicitly approve exposing registry hostname, image
reference, version tag, image/config digests, runtime and publication run identifier. Public evidence
omits private source links and commit identity; it cannot conceal metadata already present in the
Dockerfile. TGF owner must install the proposal App on eyagovbusiness/TheGoodFramework. Do not
assume an organization App installation or organization secret is available across owners.

Read-only verification and target-layout checks finish before the proposal App token is minted.
The App token step and propose step have no source or registry secrets. Dry-run, already-current,
existing PR, pause and suppression yield no write credential. App-created PRs run normal CI;
reviewers must verify authenticated builds and existing CODEOWNERS/protection requirements.

## Operations, recovery and rollback

### Current-pin provenance and reviewed bootstrap

Equality is a verified no-op. Every differing current pin must resolve to retained approved source
run/job/artifact provenance before proposing. Chronology is source run creation UTC, run number,
run ID, then actual publisher attempt; this distinguishes same-source rebuilds. An older current pin
permits advance; a newer current pin yields `noop-newer`, never a downgrade. Unknown or expired
current-pin evidence fails visibly before a write token is minted. Each main/dev target is evaluated
independently; one unknown target blocks the plan, while a known-newer target is left alone.
Candidate eligibility and historical provenance are separate: `find_publication` retains strict
latest-publisher-attempt gating. Current-pin provenance reads successful, completed historical jobs
with both evidence steps successful, skipping failed/incomplete/partial attempts without requesting
their artifacts. An unrelated failed run, or a later failed attempt in the pin's own run, does not erase
an earlier verified publication. Every successful attempt used as provenance still requires its exact
retained artifact and full identity checks; unknown/expired provenance still fails visibly.

Legacy current pins whose 30-day evidence has expired require an explicit reviewed `bootstrap` list
on the specific target in the main profile. Each entry contains an exact current `digest` and a UTC
`published_before` bound. Repository, family and target scope are inherited from that profile entry.
Only a fully verified candidate whose source creation timestamp is strictly after the bound may
replace that exact current digest. The entry does not authorize any other digest or target and does
not weaken candidate artifact/registry verification. Empty `bootstrap` means no exception.

This delivery proposes bootstrap entries for the observed legacy pins: TGF Alpine `b361ecc8...e658`,
Frontend main/dev Alpine `49abdfc2...e177`, and Manager/Orchestrator main/dev ASP.NET
`a86d5cc0...cd65`, with full digests in their profiles and bound `2026-10-27T00:00:00Z`.
Reviewers must approve these exact legacy-pin assertions before rollout; the receiver will require a
fresh approved source publication after that bound. Remove obsolete entries after adoption. A later
unknown manual pin needs separate reviewed provenance/bootstrap work, never blanket authorization.

### Automation naming policy

`botOwner: "BOT"` is the explicit automation identity for this receiver (not a human employee ID).
Review of the main profile approves that identity for SF-2942 automation. New branches use
`feature/YYYYMMDD_SF-2942_BOT_<family>-base-image-<full-digest-hex>-<target>`; YYYYMMDD is the
immutable source run creation UTC date, never receiver wall-clock or notifier retry time. Commits
use `[SF-2942] fix(deps): adopt ...`. No branch/commit governance exception is requested. Existing
proposal branches from a previous naming policy require manual reconciliation before rollout.
The date is naming metadata, not deduplication identity. Before any proposal, the receiver exhausts
repository REST pull-request pages (`state=all`, target base) and branch pages, 100 records per page,
up to 20 pages per collection. A short final page proves completion; a full twentieth page fails visibly
for manual reconciliation. GitHub Search is not used. It matches valid governed dates by family,
target and full digest across all date prefixes. Open, closed and merged PRs remain untouched, even
after their branch is deleted. Every matching extant branch must have its own matching PR; an orphan
on another date fails even if a different dated PR exists. Different targets and digests are independent.
Concurrent external mutations can race any REST listing; receiver concurrency serializes its runs,
and the write phase repeats preflight before create-only publication. No existing refs are rewritten.

Local `scripts/__pycache__/` and `tests/__pycache__/` directories are narrowly ignored by directory
`.gitignore` files so ordinary Python imports do not enter a portable staged candidate. Existing cache
files are preserved; source, profile, plan and evidence files are not ignored by these rules.

1. Roll out canonical engine/tests and the four receivers through reviewed PRs; then enable the
   publisher notifications from the separate publisher delivery. Confirm main is the default branch.
2. Provision/verify the scoped identities above. Run the receiver manually on main with
   `dry_run: true` (the manual default). Inspect the candidate digest summary and both target decisions.
3. Run manually with `dry_run: false` to create proposals. Scheduled/event runs propose by default.
   Review CI/build evidence for TGF main, Frontend main/dev, Manager main/dev, Orchestrator main/dev.
   No adoption is operationally proven until these actual reviewed PRs and builds are recorded.
4. A missed notification is recovered daily or manually. A notifier failure does not invalidate a
   successful publication job. Rerun notification independently or invoke the receiver; do not rebuild
   merely to repeat a dispatch. Missing/expired artifacts require a fresh approved publication.
5. Existing PRs in any state are untouched. Deterministic identity is consumer/family/target/full
   digest, with independent main/dev parents. An orphan ref fails visibly for manual reconciliation.
   If a PR creation fails after ref creation, retain the ref and investigate; the receiver never deletes
   it. A target that moved after planning fails and requires reconciliation/rerun. GitHub has no
   compare-and-create against a different ref: a final target check narrows but cannot eliminate the
   race after that check; the proposal always retains its validated parent and normal PR review.
6. To pause, review `paused: true` globally or on a target in the main profile. To reject a known-bad
   publication, review its full `sha256:...` in `suppressed_digests`. Suppression stops at that candidate;
   no older automatic fallback is selected. Roll back by reviewed revert/retained digest and suppress
   the rejected digest. Existing closed PRs are not reopened; a later reconsideration is manual.

Local verification (no credentials or network):

```sh
python3 -B -m unittest discover -s tests -p 'test_base_image_receiver*.py' -v
git diff --check
```

Run actionlint against both new workflows, and independently validate the final assembled candidate
before acceptance. Live registry pulls, proposal Apps, builds and rollback rehearsal remain rollout
evidence; offline fake API/registry tests are not a claim that those permissions are provisioned.
