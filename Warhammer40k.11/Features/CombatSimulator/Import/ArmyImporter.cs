using System.Text.Json;
using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k._11.Features.CombatSimulator.Import;

/// <summary>
/// Routes an army JSON to the parser that understands it, so the user never has to say which tool produced
/// the file. Two formats are supported: this app's own <c>tombworld.roster-export/1</c> and the
/// New Recruit / BattleScribe 11th-edition export.
/// Part of the removable Combat Simulator feature — see <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public static class ArmyImporter
{
    public static ImportResult Import(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ImportResult([], ["Nothing to import."]);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new ImportResult([], [$"Could not parse JSON: {ex.Message}"]);
        }

        using (doc)
        {
            if (RosterExportImporter.CanImport(doc.RootElement))
                return RosterExportImporter.Import(doc.RootElement);
        }

        // Not a roster export: fall through to New Recruit, which reports its own format errors.
        return NewRecruitImporter.Import(json);
    }
}
