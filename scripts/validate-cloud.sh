#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "$0")/env.sh"
cd "$NORDLINK_ROOT"
bash scripts/build-cloud.sh
dotnet build Nordlink.sln --no-restore -m:2
dotnet test tests/Nordlink.Tests --no-restore --no-build --logger 'trx;LogFileName=cloud-foundation.trx' --results-directory "$NORDLINK_ROOT/test-results"
cd frontend
npx playwright test --config playwright.cloud.config.ts
