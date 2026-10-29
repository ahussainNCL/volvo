#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "$0")/env.sh"
cd "$NORDLINK_ROOT"
contracts="$(mktemp)"; trap 'rm -f "$contracts"' EXIT
dotnet run --project tools/Nordlink.ContractGenerator --no-build --no-restore -- "$contracts"
cmp frontend/src/contracts.ts "$contracts"
npm run build --prefix frontend
dotnet build Nordlink.sln --no-restore -m:2
dotnet test tests/Nordlink.Tests --no-restore --no-build --logger 'trx;LogFileName=foundation.trx' --results-directory "$NORDLINK_ROOT/test-results"
dotnet publish src/Nordlink.Gateway --no-restore --no-build -o artifacts/gateway -p:Configuration=Debug
python3 scripts/check-host.py
npm run test:e2e --prefix frontend
