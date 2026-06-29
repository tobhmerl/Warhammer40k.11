namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R8 (Error): a unit's chosen size must match one of its datasheet's legal <see
/// cref="Catalogue.PointsOption"/> model counts, and its wargear selections must satisfy each authored
/// <see cref="Catalogue.WargearGroup"/> (count within Min..Max, option ids known) — §4. Datasheets with no
/// authored wargear groups are unconstrained.
/// </summary>
public sealed class UnitSizeRule : IRosterRule
{
    public string Id => "R8";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        foreach (var (unit, sheet) in context.ResolvedUnits())
        {
            if (sheet.PointsOptions.Count > 0 && !sheet.PointsOptions.Any(o => o.Models == unit.ModelCount))
            {
                var sizes = string.Join(", ", sheet.PointsOptions.Select(o => o.Models));
                yield return ValidationMessage.Error(Id,
                    $"{sheet.Name}: {unit.ModelCount} models is not a valid size (allowed: {sizes}).", unit.Id);
            }

            foreach (var group in sheet.WargearGroups)
            {
                if (group.PerModel)
                    continue; // per-model loadouts are validated in the editor (counts sum to model count), not by Min/Max

                var selection = unit.Wargear
                    .FirstOrDefault(w => string.Equals(w.GroupId, group.Id, StringComparison.OrdinalIgnoreCase));
                var chosen = (selection?.OptionIds ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                foreach (var optionId in chosen)
                {
                    if (!group.Options.Any(o => string.Equals(o.Id, optionId, StringComparison.OrdinalIgnoreCase)))
                    {
                        yield return ValidationMessage.Error(Id,
                            $"{sheet.Name}: unknown wargear option in '{group.Name}'.", unit.Id);
                    }
                }

                if (chosen.Count < group.Min)
                {
                    yield return ValidationMessage.Error(Id,
                        $"{sheet.Name}: choose at least {group.Min} from '{group.Name}'.", unit.Id);
                }
                else if (group.Max > 0 && chosen.Count > group.Max)
                {
                    yield return ValidationMessage.Error(Id,
                        $"{sheet.Name}: choose at most {group.Max} from '{group.Name}'.", unit.Id);
                }
            }
        }
    }
}
