#!/bin/bash
# Tell OF.Common about a verified canonical publication (trusted main event only).
# Runs after bgs_image_ci.sh publish has succeeded, as the single step that holds the
# dispatch credential. The request body is built and validated by bgs_common_notification.py
# from the publisher's own records, so nothing is announced for a partial publication.
# The token is read from GH_TOKEN, kept in an unexported variable and handed only to `gh api`.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
target_repository="Biognosys/OF.Common"
die() { echo "$*" >&2; exit 1; }

[[ "${GITHUB_ACTIONS:-}" == "true" ]] || die "Notification runs only in GitHub Actions."
[[ "${REPORT_DIR:-}" =~ ^/.+ ]] || die "REPORT_DIR is missing or malformed."
[[ -n "${GH_TOKEN:-}" ]] || die "GH_TOKEN (the OF.Common dispatch credential) is not set."
gh_token="$GH_TOKEN"
unset GH_TOKEN

payload="$(python3 "$repo_root/scripts/bgs_common_notification.py" "$REPORT_DIR")" || die "The publication is not verified; OF.Common was not notified."
printf '%s\n' "$payload" | GH_TOKEN="$gh_token" gh api --method POST -H "Accept: application/vnd.github+json" \
    "repos/$target_repository/dispatches" --input - ||
    die "The dispatch to $target_repository failed; the image stays published. Check that the credential may dispatch to it."
echo "Notified $target_repository of the verified publication."
