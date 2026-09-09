# Play Mode packets 5 and 7 — validation and handoff

Source: the selected packets in `~/.copilot/plans/plan-selectable-next-steps-iphone-play-mode.md` (5: phone layout; 7: actual-army rehearsal, validation only).

## Status

- **Packet 5: implemented; automated phone-layout validation passed.** Final full-solution regression results are recorded below.
- **Packet 7: actual-army rehearsal NOT performed.** No relevant Settings backup was supplied or found in the workspace. The ordinary roster export and existing sample files were not substituted for a backup.
- **Physical iPhone verification: NOT performed.** WebKit emulation is not evidence of an installed Home Screen app running on a physical iPhone.
- Feature branch: `feature/iphone-play-mode-5-7-20260909-231726`.
- Rollback tag: `rollback/iphone-play-mode-5-7-20260909-231726` (baseline `dd02a81`, the deployed points update).
- These changes require review before merging to production. No production account data was read or changed by the validation tooling.

## Packet 5 changes

- Army overview opens the existing, complete **wrapped Now cards**. **Matrix (wide)** is an explicit optional view; returning to the overview starts with wrapped cards again.
- Focus, list, wrapped overview, and matrix use the existing action projections and handlers. The older, separate overview-card implementation was not re-enabled.
- The Now surface still has at most three cards per row. Printed rule names, scoped member names, selected options, usage reminders, and full conditions remain available without text ellipses in Now cards.
- Frequent phone controls have a minimum 44-pixel touch target, including CP, phases, unit selectors, counters, and applicable weapon-stat markers.
- The bottom HUD still contains only CP, YOU/OPP, and the five phase buttons. Its safe-area offsets and reserved content clearance accommodate the larger controls.
- Shared sheets have a 44-pixel close button, a non-shrinking header, safe-area padding, and a separately scrollable body.
- New phase/view selection attributes use explicit ARIA `true`/`false` strings. A double-encoded Rules tooltip found during browser validation was corrected.
- No changes to rule content, schedules, unit prices, roster-export format, persistence, authentication, framework/package versions, or unuploaded detachment content.

## Automated evidence

### Component regressions

`PlayPhoneLayoutTests` adds 23 cases:

- All five phases × both turns × two fixture variants (including aura/buff coverage) preserve the complete focused-action inventory in the wrapped overview and optional matrix.
- Switching views preserves CP, unit tracks, the selected shooting option, and roster/schedule data.
- Opening Microscarab Swarm preserves its complete When, Target, and Effect text and does not spend CP.
- The HUD retains exactly nine buttons: two CP, two turn, and five phase controls, with one active phase and four inactive phases.

Existing matrix-specific tests select the matrix explicitly; printed-name checks cover both overview presentations.

- Final solution build (`Warhammer40k.11.slnx`): **passed**.
- Final full test suite (`Warhammer40k.Tests`): **919 passed, 0 failed, 0 skipped**.

### Browser checks — synthetic data only

Tooling: **Playwright 1.63.0, WebKit 26.6**, using the iPhone 17 Pro descriptor (DPR 3, touch enabled). The Home Screen-sized viewport uses the descriptor's 402 × 874 screen dimensions rather than its shorter browser viewport.

| Scenario | Viewport | Simulated safe areas (top/right/bottom/left) | Result |
|---|---|---|---|
| Home Screen-sized portrait | 402 × 874 | 62 / 0 / 34 / 0 px | Passed, 10 windows |
| Safari-sized portrait | 402 × 681 | 0 / 0 / 0 / 0 px | Passed, 10 windows |
| Portrait with larger rem-based text | 402 × 874, 20 px root font | 62 / 0 / 34 / 0 px | Passed, 10 windows |
| Narrow portrait regression | 375 × 812 | 59 / 0 / 34 / 0 px | Passed, 10 windows |
| Home Screen-sized landscape | 874 × 402 | 0 / 62 / 21 / 62 px | Passed, 10 windows |

**50 synthetic phase/turn windows passed.** Every profile checked:

- Touch-target dimensions, horizontal bounds, HUD safe-area clearance, and no more than three Now cards per row.
- Unclipped Now names/details and readable printed-name font sizes.
- Explicit matrix entry and return to the wrapped action view without duplicate action surfaces.
- Technosorcerous Augmentations and Atomic Disintegrators' existing choose-one interaction in the player's Shooting phase; no shooting-choice card in opponent Fight.
- Microscarab Swarm at 2 CP in opponent Fight, with the supplied condition text retained in its sheet.
- Reachable sheet-close controls and scrolling long rules inside the sheet.
- Closing and reopening a browser page on the same local origin preserves CP, phase, turn, unit tracks, and the selected shooting option.
- No unhandled browser/Blazor errors or unexpected API/external requests.

The fixture is generated from the repository's embedded catalogue and Core factories. Its deliberately broad timings stress layout coverage; they are **not a player's actual schedules or a certification of tabletop eligibility**. It contains five roster entries forming three attached/standalone combat units.

API responses are intercepted locally; writes are blocked except for a mocked, read-only roster-validation request. Service workers are blocked in this harness. Safe-area values are substituted only in test HTTP responses, not production CSS. These checks therefore do **not** certify SWA sign-in, installed-PWA update behavior, physical iOS safe areas/font metrics, or background/resume behavior on the actual phone.

Local, ignored artifacts:

- `.vs/phone-validation/results.json` — latest structured result, explicitly setting `actualArmyRehearsed` and `physicalDeviceVerified` to `false`.
- `.vs/phone-validation/*-overview.png` and `*-stratagem.png` — screenshots for review.
- `.vs/phone-validation/fixture.json` — generated synthetic data; **never import into an account**.

## Packet 7 — remaining inputs and checks

Required before the actual-army rehearsal:

1. A current **Settings backup**, including the user's catalogue, rosters, and schedule library—not the friends-facing roster export.
2. The intended roster name/ID if the backup contains multiple armies.
3. User participation for physical iPhone confirmation.

Use the backup only in an isolated local rehearsal. Do not restore it or the synthetic fixture into the production account. Walk all five phases for both turns against an independently listed inventory of the selected army's entered rules, attachments, selected wargear, enhancements, choices, costs, and usage reminders. Report exceptions belonging to unselected packets without implementing their fixes. Missing unrelated detachment uploads are not defects.

### Physical iPhone checklist — still unchecked

Use a fresh/non-active match session for counter and resume checks; do not reset an active game or change server-saved roster/catalogue/settings data.

- [ ] After an approved deployment, close older app/Safari instances and launch from the Home Screen online. If behavior differs, check the loaded client version before changing rules.
- [ ] Confirm the unit selector, Now cards, HUD, and sheet close button clear the Dynamic Island, rounded corners, and home indicator in portrait and landscape.
- [ ] Confirm frequent controls can be tapped reliably without adjacent actions firing, and the HUD still contains only CP, turn ownership, and phases.
- [ ] Confirm full printed names, roll/choice identity, and critical conditions are readable with the phone's actual text settings. Normal action discovery must not require sideways scrolling.
- [ ] Compare focus, whole-army Now, and the explicitly selected matrix using the actual army. If applicable, check Immortals + Plasmancer / Microscarab Swarm in the configured Fight window at 2 CP and Atomic's single selected shooting option in the player's Shooting phase.
- [ ] Open a long rule sheet, scroll its body, and close it without losing the current phase/unit. Confirm the final content is not covered by the HUD.
- [ ] Background and reopen the Home Screen app online; in the fresh rehearsal session, verify CP, wounds/models, phase/turn, and selected effects/options survive correctly.

## Reproduce packet 5 browser validation locally

From the repository root, using .NET 10 and Node.js:

```powershell
npm.cmd install --prefix .vs/play-phone-tools --no-save --no-package-lock --ignore-scripts --no-audit --no-fund playwright@1.63.0
$env:NODE_PATH = Join-Path $PWD '.vs/play-phone-tools/node_modules'
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $PWD '.vs/play-phone-browsers'
node .vs/play-phone-tools/node_modules/playwright/cli.js install webkit
dotnet run --file tools/export-play-phone-fixture.cs -- .vs/phone-validation/fixture.json
dotnet build Warhammer40k.11.slnx
```

Start the client in another terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project Warhammer40k.11/Warhammer40k.11.csproj --no-build --no-restore --no-launch-profile -- --urls http://127.0.0.1:5298
```

Then run the harness in the first terminal:

```powershell
node tools/validate-play-phone.cjs http://127.0.0.1:5298
```

The harness refuses non-local app URLs and actual backup files. Stop the local client when validation is finished. Browser tooling, binaries, generated JSON, and screenshots remain ignored; no application dependency is added.
