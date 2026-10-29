#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "$0")/env.sh"
cd "$NORDLINK_ROOT"
[[ "$(node --version)" == "v$(cat .nvmrc)" ]] || { echo 'Activate the Node version in .nvmrc.' >&2; exit 1; }
bash scripts/install-sdk.sh
dotnet restore Nordlink.sln --locked-mode --configfile NuGet.Config --disable-parallel
npm ci --prefix frontend --no-fund
bash scripts/generate-contracts.sh
npm run build --prefix frontend
dotnet build Nordlink.sln --no-restore -m:2
dotnet publish src/Nordlink.Gateway --no-restore --no-build -o artifacts/gateway -p:Configuration=Debug
