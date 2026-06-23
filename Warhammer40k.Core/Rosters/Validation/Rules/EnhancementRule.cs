namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R6 (Error): at most three Enhancements, each taken once, drawn from the selected detachment and satisfying
/// each enhancement's eligibility constraints (§4/§6/§10/§11). A Character Enhancement may only go on a
/// Character that is not an Epic Hero (<see cref="Catalogue.Datasheet.CanTakeEnhancements"/>); an 11th-edition
/// unit <b>Upgrade</b> (<see cref="EnhancementScope.Unit"/>) may only go on a whole non-Character unit.
/// Membership + eligibility are enforced once a detachment's enhancements are authored; a detachment with no
/// authored enhancements stays permissive (those entries are pending content). One enhancement per unit is
/// model-enforced.
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

            var enhancement = context.FindEnhancement(unit.AssignedEnhancementId!);

            // Unknown id: a member error once a detachment has authored enhancements, otherwise stay permissive
            // (pending content) but keep the legacy Character guard so it can't sit on a non-Character.
            if (enhancement is null)
            {
                if (context.AnyEnhancementsAuthored)
                {
                    var names = string.Join(", ", context.SelectedDetachments.Select(d => d.Name));
                    yield return ValidationMessage.Error(Id, $"That Enhancement is not part of your detachment(s): {names}.", unit.Id);
                }
                else if (!sheet.CanTakeEnhancements)
                {
                    yield return ValidationMessage.Error(Id, $"{sheet.Name} cannot be given an Enhancement.", unit.Id);
                }
                continue;
            }

            // Scope-appropriate target: a Character Enhancement needs a Character that can take Enhancements; a
            // unit Upgrade must be assigned to a whole (non-Character) unit.
            if (enhancement.Scope == EnhancementScope.Unit)
            {
                if (sheet.IsCharacter)
                {
                    yield return ValidationMessage.Error(Id, $"{enhancement.Name} is a unit Upgrade and cannot be given to a Character.", unit.Id);
                    continue;
                }
            }
            else if (!sheet.CanTakeEnhancements)
            {
                yield return ValidationMessage.Error(Id, $"{sheet.Name} cannot be given an Enhancement.", unit.Id);
                continue;
            }

            if (!enhancement.IsAvailableTo(sheet))
                yield return ValidationMessage.Error(Id, $"{sheet.Name} is not eligible for {enhancement.Name}.", unit.Id);
        }
    }

    private static string EnhancementName(RosterValidationContext context, string enhancementId) =>
        context.FindEnhancement(enhancementId)?.Name ?? enhancementId;
}
