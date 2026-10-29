# Nordlink project guidance

Use the existing checkout. Each cloud task is already isolated; do not create a worktree unless requested.

Read README.md, docs/architecture.md and docs/feature-register.json before changing vehicle behavior. Contracts in src/Nordlink.Contracts are authoritative; run scripts/generate-contracts.sh after changes and commit the generated frontend/src/contracts.ts.

Run scripts/validate.sh for the foundation checks. Use Node from .nvmrc and the SDK in global.json. Linux supports the gateway, simulation, IPC tests and frontend. Windows hardware, DPAPI and real vehicle validation require recorded evidence on Windows; never mark them passed from simulation.

Never log, export or put PIN material into contracts, filenames, operation inputs or SignalR events. Do not enable vehicle writes without a documented applicable procedure, exact identity, backup/restoration evidence and explicit reviewed execution. Do not invent VINs or modules outside clearly labelled simulation fixtures. Keep unknown, blocked, unsupported and equipment non-applicability distinct. No licence purchases/subscriptions are authorized.

Update docs/implementation-backlog.md, docs/hardware-validation.md and feature evidence when behavior changes. Foundation completion does not imply full V40 parity.
