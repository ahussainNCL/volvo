#!/usr/bin/env bash
# Source this file; keep caches writable without changing HOME or credential variables.
NORDLINK_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
export PATH="${NORDLINK_DOTNET_ROOT:-$NORDLINK_ROOT/../.tools/dotnet}:$PATH"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$NORDLINK_ROOT/../.cache/nordlink/nuget}"
export NUGET_HTTP_CACHE_PATH="${NUGET_HTTP_CACHE_PATH:-$NORDLINK_ROOT/../.cache/nordlink/nuget-http}"
export NUGET_SCRATCH="${NUGET_SCRATCH:-$NORDLINK_ROOT/../.cache/nordlink/nuget-scratch}"
export npm_config_cache="${npm_config_cache:-$NORDLINK_ROOT/../.cache/nordlink/npm}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
