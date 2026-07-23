# Copilot Instructions

## Project Guidelines
- For this repo (Warhammer40k.11), ignore architecture changes proposed in specs (e.g., new projects like TombWorld.UI/TombWorld.Data, SQLite, MAUI/native). Always translate new requirements into the existing architecture: Blazor WASM (Warhammer40k.11) + Azure Functions isolated API (Warhammer40k.Api) + shared Warhammer40k.Core + Azure Table Storage persistence, with SWA built-in auth.

## Active work — Necron Army Builder
- **Master plan & resume doc:** `docs/army-builder-plan.md` — delivered in packets **AB1–AB9**. On a fresh session, read it and continue from its **Current status** section.
- **Catalogue seed data:** `Warhammer40k.Api/Seed/necron-catalogue-seed.json` (52 datasheets + 4 pantheon bindings) — load it, do not hand-type units.
- **Pending from user:** spec §10 & §11 (detachment rules, per-enhancement eligibility, stratagems) — needed for packet AB9 and to finalize rules R2/R6.

## Code Management
- After making code changes, once the build and tests pass, automatically commit and push to the current branch (origin) without waiting to be asked.
- When proposing solutions, prefer minimal, non-over-engineered changes: don't add automatic injection or new abstraction layers when an adequate manual mechanism already exists (e.g., the Combat Simulator already has a manual 5+/6+ crit modifier, so no automatic crit wiring should be added there).

## Play Mode Instructions
- In Play Mode, use the unified Now ribbon as the single place for currently available stratagems and abilities; do not duplicate those actions in a separate stratagem dropdown or glowing green unit-card row.
- In Play Mode, phase-limited abilities and stratagems must appear only in the unified Now ribbon buttons, not on unit cards; unit cards may retain only always-active abilities such as Guardian Protocols, subject to a deliberate display treatment.
- Minimize card noise in the Play Mode Now ribbon: omit the focused unit name for actions applying to the whole unit, show a name only for actions scoped to a specific model/member, fit up to three cards on one row, and wrap additional cards onto further rows rather than requiring horizontal scrolling.
- Keep the Play Mode bottom HUD minimal and non-distracting: show only CP, the YOU/OPP turn selector, and phase buttons; do not show round tracking, Next progression, current-window summary, or First-turn controls.