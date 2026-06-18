namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>R4 (Error): each named Epic Hero may be included at most once (§4), with its own clear message.</summary>
public sealed class EpicHeroRule : IRosterRule
{
    public string Id => "R4";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        foreach (var group in context.ResolvedUnits()
            .Where(x => x.Sheet.IsEpicHero)
            .GroupBy(x => x.Sheet.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                var sheet = group.First().Sheet;
                yield return ValidationMessage.Error(Id, $"You can only include one {sheet.Name} (Epic Hero).");
            }
        }
    }
}
