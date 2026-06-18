namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R3 (Error): each datasheet may appear at most <see cref="Catalogue.Datasheet.MaxCopies"/> times — 6 for
/// Battleline / Dedicated Transport, 3 otherwise (§3/§4). Epic Heroes (cap 1) are reported by
/// <see cref="EpicHeroRule"/> with a clearer message, so they are skipped here to avoid a duplicate finding.
/// Attached Characters still count toward their datasheet's cap (§4 note).
/// </summary>
public sealed class CopyLimitRule : IRosterRule
{
    public string Id => "R3";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        foreach (var group in context.ResolvedUnits()
            .Where(x => !x.Sheet.IsEpicHero)
            .GroupBy(x => x.Sheet.Id, StringComparer.OrdinalIgnoreCase))
        {
            var sheet = group.First().Sheet;
            var count = group.Count();
            if (count > sheet.MaxCopies)
            {
                yield return ValidationMessage.Error(Id,
                    $"{sheet.Name}: {count} selected but only {sheet.MaxCopies} allowed.");
            }
        }
    }
}
