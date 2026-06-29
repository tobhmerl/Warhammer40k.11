# Combat Simulator

A self-contained Monte-Carlo attack-and-save simulator for Warhammer 40,000 (11th edition), built as a
**removable feature** inside the Blazor WASM app. It simulates one combat exchange — a chosen set of an
attacker's weapons resolving against a target unit — and reports the full outcome distribution plus a
deterministic expected-value cross-check.

Everything lives under `Features/CombatSimulator/`. See [`DELETE.md`](DELETE.md) for one-step removal.

## UI flow (`/combat-sim`)
The screen is two armies: **My army** (your saved Necron roster) and **Opponent** (imported). A direction
toggle at the top picks who attacks; the attacker and target pickers are then each scoped to one army — never
a mixed list.
1. **Choose direction** — "My army attacks" or "Opponent attacks".
2. **Pick attacker** — from the attacking army.
3. **Pick target** — from the defending army. When the target is the opponent, an **Import opponent** button
   sits in that box header and opens the import dialog (file upload or paste), held in the feature's own
   in-memory store, never the app's roster store.
4. **Select weapons & firing modes** — per-weapon "firing models" pre-filled; multi-mode weapons get a dropdown.
5. **Set modifiers** for both sides, grouped into labelled sections (To hit / To wound / Attacks & damage;
   Saves / FNP & damage / Profile overrides).
6. **Set iterations** (default 10,000) and an optional seed.
7. **Run** → results: stat cards, an average funnel, a damage histogram, the models-slain distribution,
   `P(wiped)`, and the closed-form expected-value comparison.

The layout is mobile-first (single column, full-width controls) and widens on tablets/desktops.

## Rule decisions (§8 of the build spec)
- **Two hit-modifier buckets (11th edition):** the BS/WS-characteristic modifier is uncapped and **cover applies
  here** (worsens the attacker's BS by 1, not the defender's save); the hit-roll ±1 modifier is separate and the
  net is clamped to [−1, +1]. They stack, so cover (−1 BS) + a −1 hit debuff = −2 effective.
- **S/T wound table:** `S≥2T`→2+, `S>T`→3+, `S=T`→4+, `T>S`→5+, `T≥2S`→6+.
- **Critical hits** (default 6+, configurable to 5+): **Lethal Hits** auto-wound (a *normal* wound — no Dev
  Wounds); **Sustained Hits X** add X normal hits that still roll to wound.
- **Critical wounds** (default 6+; **Anti-X Y+** lowers to Y vs a matching target): with **Devastating Wounds**
  they become mortal wounds = Damage, bypassing saves, **one model destroyed max per crit** (excess lost), FNP
  still applies.
- **Saves:** best of (armour − AP) vs invuln; AP never touches the invuln; cover does **not** add to the save.
- **Damage:** flat reduction → halve (round up) → floor at 1; FNP point-by-point (also on mortals, honouring a
  mortal-only flag); sequential allocation so **excess damage is lost per model**.
- **Reanimation Protocols** is a Command-phase heal, **not** a save step — shown only as an out-of-exchange
  preview.

## Closed-form cross-check
`ExpectedValueCalculator` computes a simplified deterministic mean (probabilities × dice means along the
headline hit→wound→save→damage path) and is shown next to the Monte-Carlo mean. It does not model every
interaction (e.g. the one-model Devastating cap or sequential allocation), so small differences there are
expected; a *large* divergence signals a bug.

## Layout
```
Features/CombatSimulator/
  CombatSimulatorModule.cs     AddCombatSimulator() DI + ImportedUnitStore
  Dice/DiceExpression.cs       parse + evaluate mDk±c
  Domain/                      CombatUnit, profiles, weapon, abilities, modifiers, config, result
  Engine/                      DiceRoller, AttackResolver (one iteration), MonteCarloRunner, ExpectedValueCalculator
  Import/                      WeaponKeywordParser, NewRecruitImporter (+ DefensiveAbilityDetector)
  Adapters/NativeNecronSource  read-only map from the app's BattleUnit/WeaponProfile
  Components/                  CombatSimulatorPage (@page "/combat-sim") + UnitPicker, WeaponSelector, ModifierPanel, ResultsView
```
