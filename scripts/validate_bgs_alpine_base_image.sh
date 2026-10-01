#!/bin/bash
set -euo pipefail

image_ref="${ALPINE_BASE_IMAGE:-}"

if [[ -z "$image_ref" ]]; then
    echo "Required environment variable ALPINE_BASE_IMAGE is not set." >&2
    exit 1
fi

# Require a registry host, the canonical repository, and an immutable SHA-256
# digest. A descriptive tag is allowed before the digest; mutable latest is not.
if [[ ! "$image_ref" =~ ^[a-z0-9]([a-z0-9.-]*[a-z0-9])?(:[0-9]+)?/base-images/alpine(:[A-Za-z0-9_][A-Za-z0-9_.-]{0,127})?@sha256:[a-f0-9]{64}$ ]] || [[ "$image_ref" =~ /base-images/alpine:latest@ ]]; then
    echo "ALPINE_BASE_IMAGE must be host[:port]/base-images/alpine[:descriptive-tag]@sha256:<64-lowercase-hex>; latest is not allowed." >&2
    exit 1
fi
