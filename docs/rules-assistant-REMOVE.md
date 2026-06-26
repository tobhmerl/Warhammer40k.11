# Rules Assistant — removal checklist

The **Rules Assistant** is a self-contained, optional feature: a Play-Mode panel that searches an embedded
Core-Rules corpus and shows matching rules **verbatim** with citations. It uses a **deterministic search
engine — no LLM**. It touches no existing models, validation, persistence, or Play logic, so it can be removed
without affecting the rest of the app.

## Delete these files
- `Warhammer40k.Core/RulesAssistant/` (whole folder: `RuleCard.cs`, `RulesAnswer.cs`, `RulesSearchEngine.cs`)
- `Warhammer40k.Api/RulesAssistant.cs` (the `POST /api/rules/search` Function + `RulesQuery`)
- `Warhammer40k.Api/Rules/RulesProvider.cs`
- `Warhammer40k.Api/Seed/core-rules.json`
- `Warhammer40k.11/Components/RulesAssistantPanel.razor` (+ `.razor.css`)
- `Warhammer40k.Tests/RulesSearchTests.cs`
- `docs/rules-assistant-REMOVE.md` (this file)

## Revert these small edits
1. **`Warhammer40k.Api/Warhammer40k.Api.csproj`** — remove the `<EmbeddedResource Include="Seed\core-rules.json" />`
   line (and its comment).
2. **`Warhammer40k.Api/Program.cs`** — remove the
   `builder.Services.AddSingleton<Warhammer40k.Api.Rules.RulesProvider>();` line (and its comment).
3. **`Warhammer40k.Core/IApiClient.cs`** — remove the `// ---- Rules Assistant … ----` region and the
   `SearchRulesAsync` declaration.
4. **`Warhammer40k.11/ApiClient.cs`** — remove the `using Warhammer40k.Core.RulesAssistant;` import and the
   `// ---- Rules Assistant … ----` region containing `SearchRulesAsync`.
5. **`Warhammer40k.11/Pages/PlaySession.razor`** — remove:
   - the `<button … @onclick="OpenRulebook" … >Rulebook</button>` in the HUD row,
   - the `<Sheet Open="_rulebookOpen" … ><RulesAssistantPanel /></Sheet>` block,
   - the `_rulebookOpen` field + `OpenRulebook`/`CloseRulebook` methods (the "Core Rules lookup" comment block).

## Verify
- `dotnet build` is clean and the test suite is green (the 9 `RulesSearchTests` go away with the file; nothing
  else references the feature).

## Note on the corpus
`Warhammer40k.Api/Seed/core-rules.json` ships as `[]` (empty) until the real Core-Rules JSON is pasted in. With
an empty corpus the panel shows a "No rules have been loaded yet" state; it never throws.
