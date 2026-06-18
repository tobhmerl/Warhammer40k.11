using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R5 (Error): a roster with units must nominate exactly one Warlord, and the Warlord must be a Character
/// that is Warlord-eligible (this blocks C'tan, which "cannot be your Warlord") — §4/§5.
/// </summary>
public sealed class WarlordRule : IRosterRule
{
    public string Id => "R5";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        var warlords = context.Roster.Units.Where(u => u.IsWarlord).ToList();

        if (warlords.Count == 0)
        {
            if (context.Roster.Units.Count > 0)
                yield return ValidationMessage.Error(Id, "Nominate exactly one Warlord.");
            yield break;
        }

        if (warlords.Count > 1)
            yield return ValidationMessage.Error(Id, $"Only one Warlord is allowed; {warlords.Count} are nominated.");

        foreach (var unit in warlords)
        {
            Datasheet? sheet = context.DatasheetFor(unit);
            if (sheet is null)
                continue; // dangling reference is reported by R9

            if (!sheet.IsCharacter)
                yield return ValidationMessage.Error(Id, $"{sheet.Name} cannot be the Warlord — only Characters can.", unit.Id);
            else if (!sheet.WarlordEligible)
                yield return ValidationMessage.Error(Id, $"{sheet.Name} is not eligible to be your Warlord.", unit.Id);
        }
    }
}
