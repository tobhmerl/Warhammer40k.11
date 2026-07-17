# Tactical Map — removable feature

This folder is a **self-contained, removable feature** (same pattern as `Features/CombatSimulator`).
The interactive board lives entirely here; server persistence reuses the shared `IApiClient`.

## External touch-points (only these reference the feature)
1. `Program.cs` — `builder.Services.AddTacticalMap();`
2. `Layout/MainLayout.razor` — one `<NavLink href="tactical-map">Tactical Map</NavLink>`
3. The route `@page "/tactical-map"` in `Components/TacticalMapPage.razor`

## Shared (Core/Api) support this feature relies on
- `Warhammer40k.Core/Tactical/TacticalPlan.cs` — plan/token/map models + base-size defaults
- `Warhammer40k.Api/TacticalPlanRepository.cs` + `TacticalPlanFunctions.cs` — `/api/tactical-plans` CRUD
- `IApiClient` / `ApiClient` — `Get/Save/Delete TacticalPlan(s)Async`
- `wwwroot/maps/Layout A.jpg` — the board background

## To remove the feature
Delete this folder, remove the three touch-points above, and (optionally) the Core/Api tactical files,
the `ITacticalPlanRepository` DI registration, and the map asset.
