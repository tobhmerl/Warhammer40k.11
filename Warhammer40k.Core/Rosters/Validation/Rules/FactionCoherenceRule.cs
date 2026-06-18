namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R9 (Error): every unit must resolve to a datasheet carrying the <c>Faction: Necrons</c> keyword (§4 safety).
/// A dangling datasheet reference is also reported here.
/// </summary>
public sealed class FactionCoherenceRule : IRosterRule
{
    private const string FactionKeyword = "Faction: Necrons";

    public string Id => "R9";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        foreach (var unit in context.Roster.Units)
        {
            var sheet = context.DatasheetFor(unit);
            if (sheet is null)
            {
                yield return ValidationMessage.Error(Id, $"A unit references an unknown datasheet '{unit.DatasheetId}'.", unit.Id);
                continue;
            }

            if (!sheet.Keywords.Any(k => k.Equals(FactionKeyword, StringComparison.OrdinalIgnoreCase)))
                yield return ValidationMessage.Error(Id, $"{sheet.Name} is not a Necrons unit.", unit.Id);
        }
    }
}
