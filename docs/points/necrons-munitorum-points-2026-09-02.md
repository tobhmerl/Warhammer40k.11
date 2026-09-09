# Necrons — Munitorum Field Manual points

Machine-readable reference for updating Necron points in an application.

## Source metadata

- **Faction:** Necrons
- **Game:** Warhammer 40,000
- **Edition:** 11th Edition
- **Official MFM update date:** 2026-09-02
- **Extracted:** 2026-09-08
- **Primary faction page:** https://mfm.warhammer-community.com/en/necrons
- **Official downloads page:** https://www.warhammer-community.com/en-gb/downloads/warhammer-40000/
- **Official source PDF:** https://assets.warhammer-community.com/eng_22-04_wh40k_core%26key_munitorum_field_manual-pz8ducvfz8-rsapu3xmc9.pdf
- **PDF version:** 4.2
- **PDF pages:** Necron units on printed page 27; enhancements on printed page 28

## Instructions for GitHub Copilot

1. Treat the JSON below as the authoritative point-cost input for Necrons.
2. Match existing app records by exact `name` first, then by normalized `id`.
3. Preserve every supported unit size. Do not reduce multi-size units to a single points value.
4. `composition` is authoritative when a source entry is not adequately described by a simple model count.
5. Update point values only. Do not change datasheets, rules, wargear, keywords, IDs, or UI labels unless required to represent the size tiers.
6. Do not create duplicates. Report unmatched source entries and unmatched existing app entries.
7. Preserve source spelling in `name`. If the app uses a known corrected spelling or punctuation variant, map it explicitly and report that mapping.
8. After updating, run the existing tests/build and report changed files, mappings, unmatched entries, and validation results.
9. Do not infer missing values.

## Unit points — canonical JSON

```json
{
  "schemaVersion": 1,
  "faction": "Necrons",
  "effectiveDate": "2026-09-02",
  "pointsUnit": "pts",
  "units": [
    { "id": "annihilation-barge", "name": "Annihilation Barge", "costs": [{ "models": 1, "points": 105 }] },
    { "id": "ctan-shard-of-the-deceiver", "name": "C’tan Shard of the Deceiver", "costs": [{ "models": 1, "points": 310 }] },
    { "id": "ctan-shard-of-the-nightbringer", "name": "C’tan Shard of the Nightbringer", "costs": [{ "models": 1, "points": 340 }] },
    { "id": "ctan-shard-of-the-void-dragon", "name": "C’tan Shard of the Void Dragon", "costs": [{ "models": 1, "points": 330 }] },
    { "id": "canoptek-doomstalker", "name": "Canoptek Doomstalker", "costs": [{ "models": 1, "points": 140 }] },
    { "id": "canoptek-macrocytes", "name": "Canoptek Macrocytes", "costs": [{ "models": 5, "points": 85 }] },
    { "id": "canoptek-reanimator", "name": "Canoptek Reanimator", "costs": [{ "models": 1, "points": 75 }] },
    { "id": "canoptek-scarab-swarms", "name": "Canoptek Scarab Swarms", "costs": [{ "models": 3, "points": 40 }, { "models": 6, "points": 80 }] },
    { "id": "canoptek-spyders", "name": "Canoptek Spyders", "costs": [{ "models": 1, "points": 75 }, { "models": 2, "points": 150 }] },
    { "id": "canoptek-tomb-crawlers", "name": "Canoptek Tomb Crawlers", "costs": [{ "models": 2, "points": 50 }] },
    { "id": "canoptek-wraiths", "name": "Canoptek Wraiths", "costs": [{ "models": 3, "points": 110 }, { "models": 6, "points": 220 }] },
    { "id": "catacomb-command-barge", "name": "Catacomb Command Barge", "costs": [{ "models": 1, "points": 120 }] },
    { "id": "chronomancer", "name": "Chronomancer", "costs": [{ "models": 1, "points": 65 }] },
    { "id": "convergence-of-dominion", "name": "Convergence of Dominion", "costs": [{ "models": 1, "points": 60 }, { "models": 2, "points": 120 }, { "models": 3, "points": 180 }] },
    { "id": "cryptothralls", "name": "Cryptothralls", "costs": [{ "models": 2, "points": 60 }] },
    { "id": "deathmarks", "name": "Deathmarks", "costs": [{ "models": 5, "points": 60 }, { "models": 10, "points": 120 }] },
    { "id": "doom-scythe", "name": "Doom Scythe", "costs": [{ "models": 1, "points": 230 }] },
    { "id": "doomsday-ark", "name": "Doomsday Ark", "costs": [{ "models": 1, "points": 200 }] },
    { "id": "flayed-ones", "name": "Flayed Ones", "costs": [{ "models": 5, "points": 60 }, { "models": 10, "points": 120 }] },
    { "id": "geomancer", "name": "Geomancer", "costs": [{ "models": 1, "points": 75 }] },
    { "id": "ghost-ark", "name": "Ghost Ark", "costs": [{ "models": 1, "points": 115 }] },
    { "id": "hexmark-destroyer", "name": "Hexmark Destroyer", "costs": [{ "models": 1, "points": 75 }] },
    { "id": "illuminor-szeras", "name": "Illuminor Szeras", "costs": [{ "models": 1, "points": 165 }] },
    { "id": "immortals", "name": "Immortals", "costs": [{ "models": 5, "points": 70 }, { "models": 10, "points": 150 }] },
    { "id": "imotekh-the-stormlord", "name": "Imotekh the Stormlord", "costs": [{ "models": 1, "points": 100 }] },
    { "id": "lokhust-destroyers", "name": "Lokhust Destroyers", "costs": [{ "models": 1, "points": 40 }, { "models": 2, "points": 60 }, { "models": 3, "points": 90 }, { "models": 6, "points": 180 }] },
    { "id": "lokhust-heavy-destroyers", "name": "Lokhust Heavy Destroyers", "costs": [{ "models": 1, "points": 55 }, { "models": 2, "points": 110 }, { "models": 3, "points": 165 }] },
    { "id": "lokhust-lord", "name": "Lokhust Lord", "costs": [{ "models": 1, "points": 80 }] },
    { "id": "lychguard", "name": "Lychguard", "costs": [{ "models": 5, "points": 85 }, { "models": 10, "points": 170 }] },
    { "id": "monolith", "name": "Monolith", "costs": [{ "models": 1, "points": 400 }] },
    { "id": "necron-warriors", "name": "Necron Warriors", "costs": [{ "models": 10, "points": 90 }, { "models": 20, "points": 200 }] },
    { "id": "nekrosor-ammentar", "name": "Nekrosor Ammentar", "costs": [{ "models": 1, "points": 185 }] },
    { "id": "night-scythe", "name": "Night Scythe", "costs": [{ "models": 1, "points": 145 }] },
    { "id": "obelisk", "name": "Obelisk", "costs": [{ "models": 1, "points": 300 }] },
    { "id": "ophydian-destroyers", "name": "Ophydian Destroyers", "costs": [{ "models": 3, "points": 80 }, { "models": 6, "points": 160 }] },
    { "id": "orikan-the-diviner", "name": "Orikan the Diviner", "costs": [{ "models": 1, "points": 80 }] },
    { "id": "overlord", "name": "Overlord", "costs": [{ "models": 1, "points": 85 }] },
    { "id": "overlord-with-translocation-shroud", "name": "Overlord with Translocation Shroud", "costs": [{ "models": 1, "points": 85 }] },
    { "id": "plasmancer", "name": "Plasmancer", "costs": [{ "models": 1, "points": 55 }] },
    { "id": "psychomancer", "name": "Psychomancer", "costs": [{ "models": 1, "points": 55 }] },
    { "id": "royal-warden", "name": "Royal Warden", "costs": [{ "models": 1, "points": 50 }] },
    { "id": "skorpekh-destroyers", "name": "Skorpekh Destroyers", "costs": [{ "models": 3, "points": 90 }, { "models": 6, "points": 180 }] },
    { "id": "skorpekh-lord", "name": "Skorpekh Lord", "costs": [{ "models": 1, "points": 90 }] },
    { "id": "technomancer", "name": "Technomancer", "costs": [{ "models": 1, "points": 80 }] },
    { "id": "tesseract-vault", "name": "Tesseract Vault", "costs": [{ "models": 1, "points": 425 }] },
    { "id": "the-silent-king", "name": "The Silent King", "costs": [{ "models": 3, "points": 400 }] },
    { "id": "tomb-blades", "name": "Tomb Blades", "costs": [{ "models": 3, "points": 75 }, { "models": 6, "points": 150 }] },
    { "id": "transcendent-ctan", "name": "Transcendent C’tan", "costs": [{ "models": 1, "points": 325 }] },
    { "id": "trazyn-the-infinite", "name": "Trazyn the Infinite", "costs": [{ "models": 1, "points": 75 }] },
    { "id": "triarch-praetorians", "name": "Triarch Praetorians", "costs": [{ "models": 5, "points": 90 }, { "models": 10, "points": 180 }] },
    { "id": "triarch-stalker", "name": "Triarch Stalker", "costs": [{ "models": 1, "points": 110 }] },
    { "id": "seraptek-heavy-construct", "name": "Seraptek Heavy Construct", "category": "Forge World", "costs": [{ "models": 1, "points": 540 }] }
  ]
}
```

## Detachment enhancement points — canonical JSON

```json
{
  "schemaVersion": 1,
  "faction": "Necrons",
  "effectiveDate": "2026-09-02",
  "pointsUnit": "pts",
  "detachments": [
    {
      "name": "Annihilation Legion",
      "enhancements": [
        { "name": "Eldritch Nightmare", "points": 15 },
        { "name": "Eternal Madness", "points": 25 },
        { "name": "Ingrained Superiority", "points": 10 },
        { "name": "Soulless Reaper", "points": 20 }
      ]
    },
    {
      "name": "Awakened Dynasty",
      "enhancements": [
        { "name": "Enaegic Dermal Bond", "points": 30 },
        { "name": "Nether-realm Casket", "points": 20 },
        { "name": "Phasal Subjugator", "points": 35 },
        { "name": "Veil of Darkness", "points": 20 }
      ]
    },
    {
      "name": "Canoptek Court",
      "enhancements": [
        { "name": "Autodivinator", "points": 15 },
        { "name": "Dimensional Sanctum", "points": 20 },
        { "name": "Hyperphasic Fulcrum", "points": 15 },
        { "name": "Metalodermal Tesla Weave", "points": 10 }
      ]
    },
    {
      "name": "Cryptek Conclave",
      "enhancements": [
        { "name": "Atomic Disintegrators", "points": 10 },
        { "name": "Gauntlet of Compression", "points": 20 },
        { "name": "Gravitic Bolas", "points": 15 },
        { "name": "Quantum Abacus", "points": 15 }
      ]
    },
    {
      "name": "Cursed Legion",
      "enhancements": [
        { "name": "Cursed Circlet", "points": 25 },
        { "name": "Destroyer Ankh", "points": 20 },
        { "name": "Mark of the Nekrosor", "points": 20 },
        { "name": "Murdermind", "points": 15 }
      ]
    },
    {
      "name": "Hypercrypt Legion",
      "enhancements": [
        { "name": "Arisen Tyrant", "points": 25 },
        { "name": "Dimensional Overseer", "points": 25 },
        { "name": "Hyperspatial Transfer Node", "points": 15 },
        { "name": "Osteoclave Fulcrum", "points": 20 }
      ]
    },
    {
      "name": "Obeisance Phalanx",
      "enhancements": [
        { "name": "Eternal Conqueror", "points": 25 },
        { "name": "Honourable Combatant", "points": 10 },
        { "name": "Unflinching Will", "points": 20 },
        { "name": "Warrior Noble", "points": 15 }
      ]
    },
    {
      "name": "Pantheon of Woe",
      "enhancements": [
        { "name": "Animus Damper", "points": 35 },
        { "name": "Quantum Goad", "points": 45 },
        { "name": "Reletavistic Tether", "points": 40 },
        { "name": "Singularity Matrix", "points": 55 }
      ]
    },
    {
      "name": "Starshatter Arsenal",
      "enhancements": [
        { "name": "Chrono-impedance Fields", "points": 25 },
        { "name": "Demanding Leader", "points": 10 },
        { "name": "Dread Majesty", "points": 30 },
        { "name": "Miniaturised Nebuloscope", "points": 15 }
      ]
    }
  ]
}
```

## Expected implementation report

GitHub Copilot should finish with:

- files changed;
- unit and enhancement records updated;
- exact-name matches;
- explicit alias mappings;
- source entries not found in the app;
- app entries not found in this source;
- tests/build executed and their results.

## Source fidelity note

The values above were transcribed from the official Munitorum Field Manual, version 4.2, linked from the official Warhammer 40,000 downloads page updated 2026-09-02. The interactive faction URL is retained as the primary source link. The source spelling `Reletavistic Tether` is preserved exactly; if the app uses `Relativistic Tether`, treat it as an explicit alias rather than creating a duplicate.
