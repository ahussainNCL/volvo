#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "$0")/env.sh"
cd "$NORDLINK_ROOT"
exec dotnet artifacts/gateway/Nordlink.Gateway.dll --data-root "$NORDLINK_ROOT/data" --worker-root "$NORDLINK_ROOT/artifacts/workers" "$@"
