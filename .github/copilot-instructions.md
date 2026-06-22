# Copilot Instructions

## Project Guidelines
- For this repo (Warhammer40k.11), ignore architecture changes proposed in specs (e.g., new projects like TombWorld.UI/TombWorld.Data, SQLite, MAUI/native). Always translate new requirements into the existing architecture: Blazor WASM (Warhammer40k.11) + Azure Functions isolated API (Warhammer40k.Api) + shared Warhammer40k.Core + Azure Table Storage persistence, with SWA built-in auth.

## Active work — Necron Army Builder
- **Master plan & resume doc:** `docs/army-builder-plan.md` — delivered in packets **AB1–AB9**. On a fresh session, read it and continue from its **Current status** section.
- **Catalogue seed data:** `Warhammer40k.Api/Seed/necron-catalogue-seed.json` (52 datasheets + 4 pantheon bindings) — load it, do not hand-type units.
- **Pending from user:** spec §10 & §11 (detachment rules, per-enhancement eligibility, stratagems) — needed for packet AB9 and to finalize rules R2/R6.

## Code Management
- After making code changes, once the build and tests pass, automatically commit and push to the current branch (origin) without waiting to be asked.