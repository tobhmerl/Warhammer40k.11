# Necron unit-points update — 2026-09-02

## Scope and source

Completes the existing `feature/unit-points-2026-09-02-20260909-121538` unit-points change using the canonical unit JSON in [the supplied points reference](necrons-munitorum-points-2026-09-02.md).

- Price revision: `2026-09-02`, stored as `CatalogueData.UnitPointsVersion`.
- Comparison baseline: `34d5b87` (`rollback/unit-points-20260909-121538`).
- Unit pricing only: no detachment, enhancement, or Necrodermal Binding repricing. Enhancement records updated: **0**. The four existing Pantheon bindings are unchanged.
- No rule text, IDs, names, keywords, weapons, wargear, model counts, or other non-pricing seed content changed. An explicit UTF-8 JSON comparison against the baseline verified this after removing only the price fields and revision stamp.

## Reconciliation

| Check | Result |
|---|---:|
| Source units matched | 52 / 52 |
| Exact-name matches | 48 |
| Explicit apostrophe aliases | 4 |
| Supported model-count/price tiers | 71, all preserved |
| Unit records with a pricing change | 38 |
| Base display prices changed | 30 |
| Model-count tier prices changed | 44 |
| Old per-copy escalation ranks removed | 17 |
| Unmatched source units | 0 |
| Unmatched existing seed units | 0 |
| New or duplicate units | 0 |

The supplied unit table contains flat prices, not separate per-copy surcharges. Its prices apply to every copy; the generic escalation calculator remains supported for manually authored catalogue prices. The Silent King remains a three-model unit costing 400 points.

### Explicit name mappings

Source spelling is retained in the supplied reference; existing app labels and IDs are preserved.

| Source name | Existing app name | Stable ID |
|---|---|---|
| C’tan Shard of the Deceiver | C'tan Shard of the Deceiver | `ctan-shard-of-the-deceiver` |
| C’tan Shard of the Nightbringer | C'tan Shard of the Nightbringer | `ctan-shard-of-the-nightbringer` |
| C’tan Shard of the Void Dragon | C'tan Shard of the Void Dragon | `ctan-shard-of-the-void-dragon` |
| Transcendent C’tan | Transcendent C'tan | `transcendent-ctan` |

## Existing saved catalogues

- Loads reconcile older per-user catalogue prices with the dated embedded reference; users do not need to reset their catalogue.
- Reads do not rewrite the stored Table Storage payload. The revision is persisted on the next catalogue save.
- Matching uses exact names first, then normalized IDs. Ambiguous or unknown matches are not guessed.
- Custom units, additional model-count tiers, and non-pricing content are retained.
- A current or newer revision is not reapplied, preserving manual price edits saved after the update.
- Missing or invalid reference revisions do not trigger a price update. An entirely unmatched catalogue is not marked as updated.

## Changed files

- `Warhammer40k.Api/Seed/necron-catalogue-seed.json`
- `Warhammer40k.Api/CatalogueRepository.cs`
- `Warhammer40k.Core/Catalogue/CatalogueData.cs`
- `Warhammer40k.Core/Catalogue/UnitPointsUpdate.cs`
- `Warhammer40k.Tests/CatalogueProviderTests.cs`
- `Warhammer40k.Tests/PointsEscalationTests.cs`
- `Warhammer40k.Tests/RosterWorkedExampleTests.cs`
- `Warhammer40k.Tests/UnitPointsUpdateTests.cs`
- `Warhammer40k.Tests/Warhammer40k.Tests.csproj`
- `docs/points/necrons-munitorum-points-2026-09-02.md`
- `docs/points/necrons-munitorum-points-2026-09-02-report.md`

Existing local edits to `.github/copilot-instructions.md` and the untracked `CORE_CHAT_HANDOFF.md` are outside this release and are preserved separately.

## Validation

- Seed comparison against the rollback baseline: passed; all non-pricing content and supported sizes unchanged.
- Full solution build (`Warhammer40k.11.slnx`): passed.
- Full Test Explorer project run (`Warhammer40k.Tests`): **896 passed, 0 failed, 0 skipped**.
- Updated the end-to-end worked-example assertion from 630 to 625: Overlord 85 + Warriors 90 + Warriors 90 + Nightbringer 340 + unchanged Enlivened Sentinels upgrade 20. All readiness and legality checks remain in place.
