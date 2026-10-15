#!/bin/bash
# Resolve and validate the immutable Alpine base image used by Dockerfile_bgs.
#
# The single authoritative pin is the literal `ARG ALPINE_BASE_IMAGE=<ref>`
# default committed in Dockerfile_bgs. When ALPINE_BASE_IMAGE is unset the
# committed default is used; when it is set (even to an empty string) the
# supplied value must pass the same validation, so an invalid override is never
# silently replaced. The validated reference is printed on stdout.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dockerfile="$repo_root/Dockerfile_bgs"

# Require a registry host, the canonical repository, and an immutable SHA-256
# digest. A descriptive tag is allowed before the digest; mutable latest is not.
reference_pattern='^[a-z0-9]([a-z0-9.-]*[a-z0-9])?(:[0-9]+)?/base-images/alpine(:[A-Za-z0-9_][A-Za-z0-9_.-]{0,127})?@sha256:[a-f0-9]{64}$'

if [[ "${ALPINE_BASE_IMAGE+set}" == "set" ]]; then
    image_ref="$ALPINE_BASE_IMAGE"
    source_name="ALPINE_BASE_IMAGE"
else
    # Bounded extraction: exactly one `ARG ALPINE_BASE_IMAGE=<value>` line.
    mapfile -t defaults < <(grep -E '^ARG[[:space:]]+ALPINE_BASE_IMAGE=' "$dockerfile" | tr -d '\r' || true)
    if [[ "${#defaults[@]}" -ne 1 ]]; then
        echo "Dockerfile_bgs must declare exactly one 'ARG ALPINE_BASE_IMAGE=<reference>' default; found ${#defaults[@]}." >&2
        exit 1
    fi
    image_ref="${defaults[0]#*=}"
    source_name="Dockerfile_bgs ARG ALPINE_BASE_IMAGE default"
fi

if [[ ! "$image_ref" =~ $reference_pattern ]] || [[ "$image_ref" =~ /base-images/alpine:latest@ ]]; then
    echo "$source_name must be host[:port]/base-images/alpine[:descriptive-tag]@sha256:<64-lowercase-hex>; latest is not allowed." >&2
    exit 1
fi

printf '%s\n' "$image_ref"
