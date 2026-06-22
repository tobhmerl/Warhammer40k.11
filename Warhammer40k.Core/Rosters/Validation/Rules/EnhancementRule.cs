namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R6 (Error): at most three Enhancements, each taken once, only on Characters that are not Epic Heroes
/// (captured by <see cref="Catalogue.Datasheet.CanTakeEnhancements"/>), drawn from the selected detachment,
/// and satisfying each enhancement's eligibility constraints (§4/§6/§10/§11). Membership + eligibility are
/// enforced once a detachment's enhancements are authored; a detachment with no authored enhancements stays
/// permissive (those entries are pending 11th-edition content). One enhancement per unit is model-enforced.
/// </summary>
public sealed class EnhancementRule : IRosterRule
{
    public string Id => "R6";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        var assigned = context.Roster.Units
            .Where(u => !string.IsNullOrEmpty(u.AssignedEnhancementId))
            .ToList();

        if (assigned.Count > 3)
            yield return ValidationMessage.Error(Id, $"A roster may include at most 3 Enhancements; {assigned.Count} are assigned.");

        foreach (var dup in assigned
            .GroupBy(u => u.AssignedEnhancementId!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            yield return ValidationMessage.Error(Id,
                $"Enhancement '{EnhancementName(context, dup.Key)}' is assigned to {dup.Count()} units; each may be taken once.");
        }

        foreach (var unit in assigned)
        {
            var sheet = context.DatasheetFor(unit);
            if (sheet is null)
                continue; // dangling reference is reported by R9

            if (!sheet.CanTakeEnhancements)
            {
                yield return ValidationMessage.Error(Id, $"{sheet.Name} cannot be given an Enhancement.", unit.Id);
                continue;
            }

            // Membership + eligibility apply once a selected detachment's enhancements are authored.
            if (!context.AnyEnhancementsAuthored)
                continue;

            var enhancement = context.FindEnhancement(unit.AssignedEnhancementId!);
            if (enhancement is null)
            {
                yield return ValidationMessage.Error(Id, "That Enhancement is not part of your selected detachment(s).", unit.Id);
            }
            else if (!enhancement.IsAvailableTo(sheet))
            {
                yield return ValidationMessage.Error(Id, $"{sheet.Name} is not eligible for {enhancement.Name}.", unit.Id);
            }
        }
    }

    private static string EnhancementName(RosterValidationContext context, string enhancementId) =>
        context.FindEnhancement(enhancementId)?.Name ?? enhancementId;
}
