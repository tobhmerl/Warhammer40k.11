# Deleting the Combat Simulator feature

This feature is isolated. To remove it completely, with zero residue:

1. **Delete the folder** `Warhammer40k.11/Features/CombatSimulator/` (this README, all engine/domain/UI code, and the scoped CSS/`_Imports.razor`).
2. **Remove the DI line** from `Warhammer40k.11/Program.cs`:
   ```csharp
   builder.Services.AddCombatSimulator(); // CombatSimulator feature (removable — see Features/CombatSimulator/DELETE.md)
   ```
   (also remove the matching `using Warhammer40k._11.Features.CombatSimulator;` at the top of the file).
3. **Remove the nav link** from `Warhammer40k.11/Layout/MainLayout.razor`:
   ```razor
   <NavLink class="navlink" href="combat-sim">Combat Sim</NavLink>
   ```

That's it. The `@page "/combat-sim"` route lives on the deleted page component, so it disappears with the folder.

### Test-only extra (optional)
The unit tests for the engine live in `Warhammer40k.Tests/CombatSimulator/` and the test project gained one
`ProjectReference` to `Warhammer40k.11` so the engine could be tested. If you delete the feature, also:

4. Delete `Warhammer40k.Tests/CombatSimulator/`.
5. Remove this line from `Warhammer40k.Tests/Warhammer40k.Tests.csproj`:
   ```xml
   <ProjectReference Include="..\Warhammer40k.11\Warhammer40k.11.csproj" />
   ```

Nothing in the existing app (domain models, Play Mode, rosters, catalogue) is touched by this feature — it only **reads** the Necron types through `Adapters/NativeNecronSource.cs`.

### Stored data
The feature persists its setup to browser `localStorage` under the key **`combat-sim-state`**. It's harmless if
left behind, but to fully clean up you can clear that one key from the browser's storage.
