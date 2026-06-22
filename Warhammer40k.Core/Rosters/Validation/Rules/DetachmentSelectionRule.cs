namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R2 (Error): at least one known detachment must be selected, and the total Detachment-Points cost must fit
/// the budget for the points level (11th edition). Enhancement membership/eligibility is enforced by
/// <see cref="EnhancementRule"/> (R6).
/// </summary>
public sealed class DetachmentSelectionRule : IRosterRule
{
    public string Id => "R2";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        var ids = context.Roster.EffectiveDetachmentIds;
        if (ids.Count == 0)
        {
            yield return ValidationMessage.Error(Id, "Select a detachment.");
            yield break;
        }

        foreach (var id in ids)
        {
            if (!context.Detachments.Any(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)))
                yield return ValidationMessage.Error(Id, $"Unknown detachment '{id}'.");
        }

        // 11th edition: detachments are purchased with Detachment Points, capped by the points level.
        var budget = DetachmentCatalogue.Budget(context.Roster.PointsLimit);
        var spent = context.SelectedDetachments.Sum(d => d.DetachmentPoints);
        if (spent > budget)
        {
            yield return ValidationMessage.Error(Id,
                $"Your detachments cost {spent} DP, but only {budget} are available at {context.Roster.PointsLimit} pts.");
        }

        // Exclusivity tags: a "Unique: X" detachment cannot be taken with another X detachment.
        foreach (var tag in context.SelectedDetachments.SelectMany(d => d.Tags).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var clashing = context.SelectedDetachments
                .Where(d => d.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (clashing.Count > 1)
            {
                yield return ValidationMessage.Error(Id,
                    $"Only one {tag.ToUpperInvariant()} detachment may be taken ({string.Join(", ", clashing.Select(d => d.Name))}).");
            }
        }
    }
}
