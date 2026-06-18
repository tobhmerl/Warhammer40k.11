namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R10 (Error): when (and only when) the Pantheon of Woe detachment is selected, every Necrons Monster must
/// carry its matching Necrodermal Binding (§4). <see cref="PantheonBindingApplier"/> performs the auto-apply;
/// this rule confirms it was applied. The surcharge feeds rule R1 via <see cref="RosterCalculator"/>.
/// </summary>
public sealed class PantheonRule : IRosterRule
{
    public string Id => "R10";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        if (context.Detachment?.AppliesPantheonBindings != true)
            yield break;

        foreach (var (unit, sheet) in context.ResolvedUnits())
        {
            if (!sheet.IsMonster)
                continue;

            var binding = context.Catalogue.FindBindingForUnit(sheet.Name);
            if (binding is null)
                continue; // no binding defined for this monster

            if (!string.Equals(unit.AppliedBindingId, binding.Name, StringComparison.Ordinal))
            {
                yield return ValidationMessage.Error(Id,
                    $"{sheet.Name} must take its {binding.Name} Necrodermal Binding (Pantheon of Woe).", unit.Id);
            }
        }
    }
}
