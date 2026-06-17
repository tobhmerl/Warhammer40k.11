# Necron Army Builder — Master Plan & Resume Doc

> **Resume in a fresh chat:** read this file and `.github/copilot-instructions.md`, then continue from
> **§ Current status**. All spec context (originally chat Messages 1–3) is digested below so no prior
> conversation is needed.

## Architecture directive (non-negotiable)
Translate every requirement into the **existing** solution — do **not** add new projects, SQLite, or MAUI/native:
- **`Warhammer40k.11`** — Blazor WASM UI (net10), Necron dark theme already in place. Spec's "iOS sheets / swipe" → Blazor modal/bottom-sheet components + buttons.
- **`Warhammer40k.Api`** — Azure Functions isolated API (net9). Persistence = **Azure Table Storage** (repository pattern, see `ArmyRepository`). Catalogue seeding + roster CRUD live here.
- **`Warhammer40k.Core`** — shared POCOs + the **pure, unit-tested** validation engine (net9).
- **`Warhammer40k.Tests`** — xUnit (net10), references Core + Api.
- Auth = SWA built-in GitHub. The app **shell is public** so the SPA always boots and sign-in is a deliberate action on the Home page; `/api/*` requires the built-in **`authenticated`** role (except `/api/whoami`, which is anonymous so the landing page can detect sign-in state). The API derives identity from the SWA principal header and partitions Table Storage **per user**, so each account only ever sees its own data. (To make the site single-owner exclusive later: invite yourself as a custom `owner` role in the portal **and** gate `/api/*` on it, or add a server-side allow-list of your `userId` — don't lock `/*`, or the framework files 403 and the app can't boot.)

## Packet roadmap (AB1–AB9)
Each packet must build and keep tests green before the next.

| Packet | Scope | Projects | Gate |
|---|---|---|---|
| **AB1** | Catalogue domain POCOs + `CatalogueSeedLoader` (derivation) + loader unit tests | Core, Tests | builds, tests green |
| **AB2** | Embed `necron-catalogue-seed.json` + `CatalogueProvider` + `GET /api/catalogue` + client method + real-seed test (52 datasheets / 4 bindings + spot-checks) | Api, Core, UI, Tests | deployable |
| **AB3** | Tabbed nav + **Catalogue browse** tab (52 units grouped by role, searchable, detail sheet), themed | UI | deployable |
| **AB4** | `Roster`/`RosterUnit`/`Detachment`/`Enhancement` model + **rules R1–R11** + `RosterValidator` + rules tests + §6 integration test | Core, Tests | tests green |
| **AB5** | Roster persistence (Table Storage, per-user; evolves M1 `Army`) + `/api/rosters` CRUD + validate endpoint + client | Api, Core, UI | deployable |
| **AB6** | **Roster builder UI**: New-Roster wizard, editor (running points + validity pill, role sections, validation panel), Add-Unit picker (remaining copies), Unit configurator sheet | UI | deployable |
| **AB7** | **Catalogue editor** (Tab 2 authoring): CRUD + nested editors + wargear option-group authoring + referential-integrity checks | UI, Api | deployable |
| **AB8** | **Settings** tab: default points limit, theme, backup/restore (export/import JSON) | UI, Api | deployable |
| **AB9** | Wire **§10/§11** (detachment rules, per-enhancement eligibility, stratagems) → finalize R2/R6 + missing enhancement points | Core, Api, UI | complete |

## Current status
- **AB1 — done.** Catalogue POCOs (`Warhammer40k.Core/Catalogue/`), `CatalogueSeedLoader.Load`/`Enrich` (derivation), and loader unit tests (`Warhammer40k.Tests/CatalogueSeedLoaderTests.cs`, inline fixture covering every derivation branch). `dotnet build` + `dotnet test` green.
- **AB2 — done.** Deployable.
  - Seed embedded as `<EmbeddedResource>` in `Warhammer40k.Api` (`Seed/necron-catalogue-seed.json`).
  - `CatalogueProvider` (`Warhammer40k.Api/Catalogue/`) lazily loads + caches the enriched catalogue once; static `LoadEmbedded()` resolves the manifest resource by suffix so tests can reuse it.
  - `GET /api/catalogue` (`Catalogue` function, `Anonymous` — SWA gates `/api/*` to the `owner` role) returns the enriched `CatalogueData`; `CatalogueProvider` registered as a singleton in `Program.cs`.
  - Client: `IApiClient.GetCatalogueAsync` (Core) + `ApiClient` impl (UI) with empty-`CatalogueData` fallback matching the existing resilience pattern.
  - Real-seed test `Warhammer40k.Tests/CatalogueProviderTests.cs`: 52 datasheets / 4 bindings + derivation spot-checks (slug ids, copy caps, Overlord leader/Warlord eligibility, C'tan restrictions, every Monster → its binding, binding details, provider caching). **43 tests green.**
  - NEXT: **AB3** — tabbed nav + Catalogue browse tab (52 units grouped by role, searchable, detail sheet), themed.
- **AB3 — done.** Deployable.
  - Nav: added a **Catalogue** pill to `MainLayout` (Home · Catalogue · Armies).
  - Reusable `Components/Sheet.razor` (+ scoped CSS): accessible `role="dialog"` modal that renders as a bottom-sheet on mobile and a centered modal ≥720px (the spec's "iOS sheet"; reused by AB6). Closes via backdrop or button.
  - `Pages/Catalogue.razor` (+ scoped CSS): auth-gated load via `IApiClient.GetCatalogueAsync`; live search over name/keywords/faction-rules; all 52 datasheets grouped by `PrimaryRole` in battle order; themed unit cards open the detail Sheet (stat table, weapons table, cleaned ability text, leader targets, unit sizes, faction-rule/keyword chips, and the Pantheon binding surcharge for Monsters).
  - UI builds clean (0/0); 43 tests still green.
  - NEXT: **AB4** — `Roster`/`RosterUnit`/`Detachment`/`Enhancement` model + rules R1–R11 + `RosterValidator` + rules tests + §6 integration test (Core, Tests).
- **Pending from user:** spec **§10 & §11** (detachment rules, enhancement definitions + per-enhancement eligibility, stratagems). Until then R2/R6 eligibility stays permissive with a TODO.

## Derived-at-load fields (computed once by `CatalogueSeedLoader.Enrich`)
- `Id` = slug of name ("C'tan Shard of the Deceiver" -> `ctan-shard-of-the-deceiver`).
- `IsMonster` = keyword `Monster`. `IsUnique` = `IsEpicHero`.
- `MaxCopies` = EpicHero/Unique -> 1; Battleline or Dedicated Transport -> 6; else 3.
- `WarlordEligible` = `IsCharacter && !ability text "cannot be your Warlord"`.
- `CanTakeEnhancements` = `IsCharacter && !IsEpicHero && !ability text "cannot be given Enhancements"`.
- `HasLeaderAbility`, `AllowsCoLeader` (text "…already been attached"), `LeaderTargetIds` (match known unit names in the Leader ability text; tolerate `^^**`/`■` markup).

## Spec digest

### §1 Domain rules
Necrons only. Points presets **1250 / 1500 / 2000** + free custom (default 2000); same Strike-Force rules for all. Enhancements <= **3**. Copy limits: Epic Hero/Unique **1x**, Battleline & Dedicated Transport **6x**, else **3x**. Hard rules **block** "Ready"; soft rules **warn**; never auto-fix silently.

### §2 Flow
New Roster (name -> faction fixed Necrons -> points limit -> **one of 7 detachments**) -> live Roster editor -> Add Unit picker (grouped by role, shows remaining copies) -> Unit configurator (size/points, wargear, leader attach, enhancement, Warlord) -> Mark Ready / export.
7 detachments (11th-ed line-up): Hand of the Dynasty, Skyshroud Spearhead, The Phaeron's Armoury, Starshatter Arsenal, Cryptek Conclave, Cursed Legion, Pantheon of Woe.

### §3 Catalogue
**52 datasheets — seed-driven, never hand-typed.** `points` = smallest size; `pointsOptions` is the list of legal sizes. Wargear is **free** (cost = model count only; keep a `PointDelta = 0` for forward-compat).

### §4 Validation rules (each a small, independently unit-tested rule -> `ValidationMessage{Severity,Text,RosterUnitId?}`; Error blocks Ready)
- **R1 Points limit (E):** sum of unit points + enhancement points + Pantheon surcharges <= limit.
- **R2 One detachment (E):** exactly one; all enhancements/strats belong to it.
- **R3 Copy limits (E):** per datasheet caps (1 / 6 / 3). Surface remaining in picker.
- **R4 Epic Heroes 0-1 (E):** each named Epic Hero <= 1 (own clear message).
- **R5 Warlord (E):** exactly one; must be a Character and Warlord-eligible (block C'tan).
- **R6 Enhancements (E):** <=3; each once; Characters only, never Epic Heroes; one per Character; must come from the detachment and satisfy its eligibility constraint (structured data — *permissive until §10/§11*). Points -> R1.
- **R7 Leader attach (E/W):** target must be in the Leader's allowed list; one Leader per Bodyguard unless `AllowsCoLeader`; Cryptothralls/Tomb Crawlers retinue augment (max one, mutually exclusive) on a Cryptek-led unit; unattached Character = Info only.
- **R8 Unit size & wargear (E):** chosen size must be a valid `pointsOptions`; wargear group min/max — *unconstrained until option-groups authored*.
- **R9 Faction coherence (E):** every unit has `Faction: Necrons` (safety).
- **R10 Pantheon of Woe (E + auto-apply):** only when that detachment selected — every Necrons **Monster** takes its matching Necrodermal Binding and pays the surcharge (Deceiver/Singularity Matrix 55, Nightbringer/Quantum Goad 45, Void Dragon/Animus Damper 35, Transcendent C'tan/Reletavistic Tether 40; editable). Surcharge counts in R1.
- **R11 Battle-size minimums (Info only):** none currently; don't block.
Notes: points are by model count (never per-weapon); an attached Character still counts for R3/R4 and its points; "Ready" = no Error messages.

### §5 Roster data (store references, resolve catalogue at display)
- **Roster:** name, faction (Necrons), pointsLimit, detachmentId, createdUtc, modifiedUtc, computed totalPoints, warlordRosterUnitId, RosterUnits[], catalogueVersion.
- **RosterUnit:** datasheetId, chosen pointsOption (modelCount->points), selected wargear per group, attachedToRosterUnitId / attachedLeaderIds, assignedEnhancementId (Characters only), isWarlord, appliedBindingId, bindingSurcharge.

### §6 Screens (3 tabs) + worked example
Tabs: **Rosters** (card list, swipe/buttons, "+", New-Roster wizard, Roster editor with sticky header `1,910 / 2,000`, validity pill, `Enhancements 2/3`, role sections, unit cards, collapsible validation panel, Add-Unit sheet, Unit configurator, duplicate/rename/delete + summary/export); **Catalogue** (browse/search Factions->Detachments->Datasheets + Weapons/Abilities/Enhancements/Keywords libraries, full CRUD, referential integrity, fast entry); **Settings** (default points, theme, backup/restore, about).
**Worked example -> integration test:** detachment Hand of the Dynasty @2000 -> add Overlord (85, Warlord) + enhancement; x2 Necron Warriors (90); attach Overlord to a Warriors unit; add C'tan Nightbringer (340, can't 2nd, can't be Warlord); assert total <= 2000, exactly one Warlord, no cap breach, <=3 eligible enhancements, Ready only when all R-rules pass.

### §7 Seed field map -> model
`{ faction, datasheets:[{ name, points, primaryRole, isEpicHero, isBattleline, isDedicatedTransport, isCharacter, keywords[], factionRules[], statProfiles[{name,m,t,sv,w,ld,oc}], abilities[{name,text}], weapons[{name,type,range,attacks,skill,strength,ap,damage,keywords[]}], pointsOptions[{models,points}] }], pantheonBindings:[{name,unit,points}] }`. Derive the fields listed above at load.

### §8 Points / Pantheon
Size tiers + binding surcharges baked into the seed (`pointsOptions`, `pantheonBindings`). Enhancement points (10th MFM) for: **Cryptek Conclave** (Atomic Disintegrators 10, Gauntlet of Compression 20, Gravitic Bolas 15, Quantum Abacus 15); **Cursed Legion** (Cursed Circlet 25, Destroyer Ankh 20, Mark of the Nekrosor 20, Murdermind 15); **Starshatter Arsenal** (Chrono-impedance Fields 25, Demanding Leader 10, Dread Majesty 30, Miniaturised Nebuloscope 15); **Pantheon of Woe** (the four bindings). Hand of the Dynasty / Skyshroud Spearhead / The Phaeron's Armoury have no 10th points yet (fill from 11th MFM). Expose all points as **editable**.

### §12 Seeding
Idempotent `CatalogueSeeder`/loader consuming any `*-catalogue-seed.json`; de-dupe shared keywords/abilities; seed full weapon **pool**, leave wargear option-groups **empty-but-present** (authored later in the Catalogue editor). The Python converter `convert_newrecruit_to_seed.py` (regenerates the seed from a NewRecruit export) is a repo tool/reference only — keep under `tools/`, not part of the .NET build.

## Conventions
- File-scoped namespaces, `Nullable` enabled, `TreatWarningsAsErrors` in Core — keep it warning-clean.
- Validation engine is **pure** (no I/O) in Core so API + UI share it and it's unit-testable.
- Derive once at load; never re-parse ability text at runtime.
- Reuse the existing `ClientPrincipalReader`/repository + per-user partitioning patterns from M1.
