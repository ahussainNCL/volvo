#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "$0")/env.sh"
cd "$NORDLINK_ROOT"
dotnet run --project tools/Nordlink.ContractGenerator --no-restore -- frontend/src/contracts.ts
