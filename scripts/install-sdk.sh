#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "$0")/env.sh"
destination="${NORDLINK_DOTNET_ROOT:-$NORDLINK_ROOT/../.tools/dotnet}"
if [[ -x "$destination/dotnet" && "$("$destination/dotnet" --version)" == 10.0.403 ]]; then exit 0; fi
[[ "$(uname -s)" == Linux && "$(uname -m)" == x86_64 ]] || { echo 'Install the pinned official .NET SDK for your platform; this helper is Linux x64 only.' >&2; exit 1; }
archive="$(mktemp /tmp/nordlink-sdk.XXXXXX.tar.gz)"
trap 'rm -f "$archive"' EXIT
curl --fail --show-error --location --retry 2 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-x64.tar.gz' --output "$archive"
printf '%s  %s\n' 'b7b3d8461cf98b07d2552ce9199702fa3ad010ac2671a5f8cd5136f7534e50f6855ba7bbb02b11494563f1d04156276a31cda1103b900e5eecd0d9473dc7b9c5' "$archive" | sha512sum --check --status
mkdir -p "$destination"
tar -xzf "$archive" -C "$destination"
"$destination/dotnet" --version
