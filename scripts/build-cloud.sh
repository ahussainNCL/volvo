#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "$0")/env.sh"
cd "$NORDLINK_ROOT"
dotnet restore Nordlink.sln --locked-mode --configfile NuGet.Config --disable-parallel
bash scripts/generate-contracts.sh
VITE_NORDLINK_HOSTING=cloud npm run build --prefix frontend
dotnet publish src/Nordlink.Cloud --no-restore -c Release -o artifacts/cloud
dotnet publish src/Nordlink.Helper --no-restore -c Release -o artifacts/helper
