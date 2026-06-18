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
- **AB4 — done.** Tests green (Core + Tests only; not yet wired into UI/API — that's AB5/AB6).
  - **Models** (`Warhammer40k.Core/Rosters/`): `Roster` (header + 1250/1500/2000 presets + `NecronsFaction`/`DefaultPointsLimit` constants), `RosterUnit` (+ `WargearSelection`, `FromDatasheet` factory), `Detachment`/`Enhancement` + `DetachmentCatalogue.BuiltIn` — the 7 detachments with §8 enhancement points for **Starshatter Arsenal / Cryptek Conclave / Cursed Legion**; Hand of the Dynasty / Skyshroud Spearhead / The Phaeron's Armoury left empty until 11th MFM; **Pantheon of Woe** flagged `AppliesPantheonBindings`.
  - **Engine** (`Warhammer40k.Core/Rosters/Validation/`): pure `RosterValidator` runs **R1–R11** (one `IRosterRule` class each under `Rules/`) → `ValidationResult{Messages, TotalPoints, IsReady}` (Ready = no Error). Helpers: `RosterCalculator` (points = model-count `PointsOption` + enhancement + binding surcharge), `PantheonBindingApplier` (R10 auto-apply; idempotent + preserves an edited surcharge), `RosterValidationContext` (memoised catalogue/detachment/total resolution). Extracted shared `Warhammer40k.Core/Text/Slugger.cs` from `CatalogueSeedLoader` (identical output; AB1/AB2 ids unchanged).
  - **Deviations from §5** (documented inline): Warlord marked on `RosterUnit.IsWarlord` (no `warlordRosterUnitId`) and attachment via `RosterUnit.AttachedToRosterUnitId` (no `attachedLeaderIds`) — single source of truth so R5 can validate "exactly one" and R7 the co-leader rule.
  - **Permissive until §10/§11** (TODO in code): R6 enhancement→detachment membership only enforced once a detachment's `Enhancements` are authored; R7 Cryptothrall/Tomb-Crawler retinue augment; R8 wargear group min/max (option-groups unauthored); R11 intentionally empty (Info-only, none currently). R3 skips Epic Heroes so R4 owns their clear 0–1 message.
  - **Tests:** `RosterValidatorTests.cs` (per-rule R1–R11 on synthetic catalogues + `RosterCalculator` + `PantheonBindingApplier` + validator composition) and `RosterWorkedExampleTests.cs` (§6 worked example on the **real** seed: Hand of the Dynasty @2000 → Overlord 85 (Warlord+enh) + 2×Necron Warriors 90 + Nightbringer 340 = **605/2000 Ready**; 2nd Nightbringer → R4; Nightbringer as Warlord → R5). **83 tests green**; full solution builds 0/0.
  - NEXT: **AB5** — roster persistence (Table Storage, per-user; evolves M1 `Army`) + `/api/rosters` CRUD + validate endpoint + client (Api, Core, UI).
- **AB5 — done.** Deployable (full solution builds 0/0; **91 tests green**).
  - **Persistence** (`Warhammer40k.Api/RosterRepository.cs`): `IRosterRepository` + `TableRosterRepository` over a new **`Rosters`** table (auto-created), mirroring M1's army repo — per-user partition (`PartitionKey = userId`, `RowKey = rosterId`), Guid-`n` id on create, re-read after upsert. `RosterEntity` stores the unit list as a JSON string column (`UnitsJson`); `ModifiedUtc` = row `Timestamp`, `CreatedUtc` is a persisted column (defaulted on first save). Added **alongside** M1 `Army`/`Armies` (kept working) — Roster supersedes it; AB6 swaps the UI and Army can retire later.
  - **API** (`Warhammer40k.Api/Rosters.cs`): `/api/rosters` List/Get/Create/Update/Delete (same `ClientPrincipalReader` identity pattern — `userId` never from body/route; route id authoritative on update) + **`POST /api/rosters/validate`** which applies Pantheon bindings then runs `RosterValidator` against the server `CatalogueProvider` catalogue and returns a `ValidationResult`. Registered `IRosterRepository` in `Program.cs`. SWA already gates `/api/*` to `authenticated`, so functions stay `Anonymous`.
  - **Wire contract:** made `ValidationResult`/`ValidationMessage` JSON round-trippable (`[JsonConstructor]`, `JsonStringEnumConverter<ValidationSeverity>` so severity serializes as "Error"/"Warning"/"Info", `[JsonIgnore]` on derived `IsReady`/`Errors`/`Warnings`/`Infos`).
  - **Client** (`Warhammer40k.Core/IApiClient.cs` + `Warhammer40k.11/ApiClient.cs`): `GetRostersAsync`/`GetRosterAsync`/`SaveRosterAsync`/`DeleteRosterAsync` (same resilient empty/null fallbacks as armies) + `ValidateRosterAsync` → `ValidationResult?` (**null when the API is unreachable** so AB6 can fall back to local Core validation).
  - **Tests:** `RosterEntityTests.cs` (From→entity→ToRoster round-trip incl. units-through-JSON, blank-JSON fallback, ModifiedUtc) and `RosterValidationWireTests.cs` (result round-trip, severity-as-string, derived members omitted). Repo tests stay pure (no Azurite), matching the suite.
  - NEXT: **AB6** — Roster builder UI: New-Roster wizard, editor (running points + validity pill, role sections, validation panel), Add-Unit picker (remaining copies), Unit configurator sheet (UI).
- **AB6 — done.** Deployable (full solution builds 0/0; **91 tests green** — UI packet, no new tests).
  - **Nav swap:** `MainLayout` nav and the Home CTA now point at **`/rosters`** (replacing the M1 "Armies" link). Removed the superseded `Pages/Armies.razor`(+css); the `Army` model/`/api/armies`/client methods stay for later retirement.
  - **Rosters list** (`Pages/Rosters.razor` + scoped css, `/rosters`): auth-gated card list — each card shows detachment, computed `total/limit` (via local `RosterValidator`/`RosterCalculator`) and a **Ready/issues pill** — plus Duplicate (re-ids units and **remaps leader attachments**) and Delete, a `+` FAB, and a **New-Roster wizard** `Sheet` (name → fixed **Necrons** → points presets `1250/1500/2000` + custom → one of the seven `DetachmentCatalogue.BuiltIn`) that `SaveRosterAsync` → navigates to the editor.
  - **Roster editor** (`Pages/RosterEditor.razor` + scoped css, `/rosters/{Id}`): loads catalogue + roster, then **validates locally** on every edit (`PantheonBindingApplier.Apply` → `RosterValidator.Validate`) — no per-keystroke round-trip; `SaveRosterAsync` persists (keeps the in-memory instance, syncs server id/timestamps). Sticky header = editable name + running `total/limit` (turns red over limit) + validity pill + `Enhancements n/3` + Save (disabled unless dirty). Collapsible **validation panel** (severity-ordered R1–R11 messages). Units grouped into **role sections** (battle order) as cards with Warlord/→leader-target/enhancement/binding chips + points.
  - **Add-Unit picker** `Sheet`: searchable datasheets grouped by role, each showing **remaining copies = `MaxCopies − count`** and disabled at 0; adding keeps the sheet open. **Unit configurator** `Sheet`: size radios (`PointsOptions`), Warlord toggle (only when `WarlordEligible`, mutually exclusive across the roster), leader-attach `<select>` (targets = `LeaderTargetIds` ∩ roster units), enhancement `<select>` (from the detachment; note when none authored), read-only Pantheon binding/surcharge, and Remove (clears inbound attachments). Plus a **Summary/export** `Sheet` (text list in a selectable textarea).
  - Reused the existing `Sheet` component + theme variables; wargear option-groups intentionally deferred to AB7 (kept free/non-blocking, §R8).
  - NEXT: **AB7** — Catalogue editor (Tab 2 authoring): CRUD + nested editors + wargear option-group authoring + referential-integrity checks (UI, Api).
- **AB7 — done.** Deployable (full solution builds 0/0; **105 tests green**).
  - **Editable catalogue, per-user.** New `Warhammer40k.Api/CatalogueRepository.cs`: `ICatalogueRepository` + `TableCatalogueRepository` over a `Catalogue` table — the whole document is one entity per user (PartitionKey=userId, RowKey=`necrons`) with JSON **chunked** across `Json_n` props (the catalogue ~122 KB exceeds the 64 KiB property limit but fits the 1 MiB entity limit). `CatalogueChunking.Split/Join` + public `BuildEntity`/`ExtractJson` (unit-tested). The seed remains the immutable **default**: a user with no row reads the default; first save materializes their copy; reset deletes it.
  - **API:** `Catalogue.cs` now `GET /api/catalogue` per-user (saved ?? default; anonymous still gets default), `PUT /api/catalogue` (save, requires auth + ≥1 datasheet), `POST /api/catalogue/reset` (→ default). Registered `ICatalogueRepository`. Client gained `SaveCatalogueAsync` (throws to surface errors) + `ResetCatalogueAsync`.
  - **Core:** new `WargearGroup{Id,Name,Min,Max,Options}` / `WargearOption{Id,Name,PointDelta=0}` on `Datasheet.WargearGroups` (seed empty-but-present); **R8 now enforces** wargear (per group: known option ids + distinct count within Min..Max; Max=0 = unlimited). `CatalogueSeedLoader.Enrich` is **id-preserving** (slugs only when `Id` empty) so editing/renaming keeps roster references stable while other derived fields still recompute. New pure `CatalogueIntegrity.Check` (duplicate ids/names, missing sizes, wargear group/option name+id uniqueness + Min>Max, monster-without-binding, binding→unknown-unit).
  - **UI:** `Catalogue.razor` gained an **Edit catalogue** link → new `Pages/CatalogueEditor.razor` (`/catalogue/edit`): searchable datasheet list with New/Delete, a collapsible **integrity panel**, **Save** (dirty-gated) / **Reset** (confirm), and a datasheet editor `Sheet` with nested editors — name/role/flags, keyword & faction-rule chips, unit sizes, stat profiles, weapons (incl. comma keywords), abilities, and **wargear option-groups** (name/Min/Max + options). Edits re-`Enrich` + re-check integrity live. The AB6 Unit configurator gained a **wargear selection** section (toggle options per group with a choose-range hint) so R8's wargear rule is satisfiable.
  - **Tests:** wargear R8 (min/max/unknown option), `CatalogueAuthoringTests` (id-preserving enrich + recompute, integrity cases), `CatalogueChunkingTests` (lossless split/join + entity round-trip of a multi-chunk document). UI ships without bUnit (none in repo); guarded by full build + green suite.
  - NEXT: **AB8** — Settings tab: default points limit, theme, backup/restore (export/import JSON) (UI, Api).
- **AB8 — done.** Deployable (full solution builds 0/0; **110 tests green**).
  - **Settings persistence:** `UserSettings{DefaultPointsLimit, Theme}` + `AppThemes` (4 palettes: phosphor/arcane/ember/blood, `Normalize`) + `BackupBundle{Format, CreatedUtc, Settings, Catalogue?, Rosters}` in Core. `Warhammer40k.Api/SettingsRepository.cs` (`ISettingsRepository` + `SettingsEntity` + `TableSettingsRepository`, per-user single entity in a `Settings` table). Registered in `Program.cs`.
  - **API:** `Settings.cs` (`GET`/`PUT /api/settings`; anonymous GET → defaults). `Backup.cs` composes the three per-user repos: `GET /api/backup` assembles a bundle (settings + **customized** catalogue or null + all rosters); `POST /api/restore` replaces — saves settings, saves-or-resets catalogue, then clears and recreates rosters with fresh ids. Client gained `GetSettingsAsync`/`SaveSettingsAsync`/`GetBackupAsync`(nullable)/`RestoreBackupAsync`.
  - **Theme:** `--accent-rgb` added to `app.css` so accent tints/glows/backgrounds follow the palette; `[data-theme=…]` blocks for the 3 alternates. Tiny `window.tombforge` JS helper (`setTheme`, `download`) in `index.html`. Scoped `SettingsState` service loads settings once, applies the theme (via JS), exposes `DefaultPointsLimit`, and supports live preview / post-restore apply. `MainLayout` applies the theme on first render + gained a **Settings** nav link.
  - **UI:** `Pages/Settings.razor`(+css, `/settings`): default-points presets + custom, theme picker with **instant preview** + Save, and **Backup & restore** — Export (server bundle → JSON file download) and Import (`InputFile` → parse → confirm-gated destructive **Restore**). The New-Roster wizard now seeds its points from `SettingsState.DefaultPointsLimit`.
  - **Tests:** `SettingsAndBackupTests` (settings entity round-trip + theme normalization, `AppThemes.Normalize`, `BackupBundle` JSON round-trip incl. null catalogue).
  - NEXT: **AB9** — wire **§10/§11** (detachment rules, per-enhancement eligibility, stratagems) → finalize R2/R6 + missing enhancement points (Core, Api, UI).
- **AB9 — done.** 🎉 **Roadmap complete (AB1–AB9).** Deployable (full solution builds 0/0; **114 tests green**).
  - **Eligibility machinery finalized:** `EnhancementEligibility{RequiredKeywords[], ExcludedKeywords[]}` + `Enhancement.Eligibility`/`IsAvailableTo(Datasheet)` (all-required + none-excluded keywords; empty = unconstrained) and a `Stratagem{Id,Name,Type,CpCost,Text}` model on `Detachment.Stratagems`.
  - **R6 finalized** (`EnhancementRule`): now enforces per-enhancement **eligibility** (`IsAvailableTo`) alongside count/uniqueness/`CanTakeEnhancements`/membership — runs identically client-side (live editor validation) and on the server validate endpoint. **R2** doc clarified (R2 owns "exactly one detachment"; R6 owns enhancement membership + eligibility). A detachment with **no authored enhancements stays permissive** (the 3 without published points).
  - **UI:** the Unit configurator now offers **only eligible enhancements** (keeps a current pick visible; shows a "not eligible for this character" note); the roster editor's detachment name is a button opening a **Detachment-info Sheet** (enhancements with points + eligibility text, and stratagems) that degrades to "none defined yet".
  - **Scope note (no-op Api):** detachment definitions are static Core reference data (`DetachmentCatalogue.BuiltIn`) consumed identically by client and server, so finalizing R6 in Core finalized it everywhere — no new endpoint was needed. `DetachmentCatalogue` remains the single editable source for the **pending 11th-edition content** (the 3 detachments' enhancement points/names, exact per-enhancement eligibility keywords, and stratagem entries) — fill them in there to activate, **no engine change required**.
  - **Tests:** R6 eligibility (required-missing → block, required-present → allow, excluded → block) + `Enhancement.IsAvailableTo` matrix.
- **AB10 — done.** (Follow-up polish.) Deployable (builds 0/0; **114 tests green**).
  - Extracted the catalogue datasheet detail into a shared **`Components/DatasheetDetail.razor`**(+css) — stats/weapons/abilities/leader-targets/sizes/keywords + Pantheon binding (params: `Datasheet` + null-safe `CatalogueData`). `Catalogue.razor` now consumes it (no visual change; moved its detail CSS out).
  - The **roster builder's Unit configurator** gained a collapsed-by-default **"Datasheet"** section (toggle, reset per unit) rendering the same component, so you can see a unit's profile/weapons/abilities while list-building.
- **Still pending from user (content, not code):** the **11th-edition** values for **§10/§11** — enhancement points/names for Hand of the Dynasty / Skyshroud Spearhead / The Phaeron's Armoury, per-enhancement eligibility keywords, and stratagems. Drop them into `DetachmentCatalogue` (`Enhancement.Points`/`Eligibility.RequiredKeywords`/`ExcludedKeywords`, `Detachment.Stratagems`); R1/R2/R6 + the UI pick them up automatically.

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
