#!/usr/bin/env bash
# Linux cross-publish only. Executing the outputs and TOPDON validation require Windows.
set -euo pipefail
source "$(dirname -- "$0")/env.sh"
cd "$NORDLINK_ROOT"
for arch in x86 x64; do
  dotnet publish src/Nordlink.HardwareWorker -c Release -r "win-$arch" --self-contained true \
    -p:NordlinkPublishRoot="$NORDLINK_ROOT/artifacts/locks" -o "artifacts/workers/win-$arch"
done
dotnet publish src/Nordlink.Gateway -c Release -r win-x64 --self-contained true \
  -p:NordlinkPublishRoot="$NORDLINK_ROOT/artifacts/locks" -o artifacts/windows
mkdir -p artifacts/windows/workers
cp -a artifacts/workers/win-x86 artifacts/workers/win-x64 artifacts/windows/workers/
