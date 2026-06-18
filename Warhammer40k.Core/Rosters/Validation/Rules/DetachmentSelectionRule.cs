namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R2 (Error): exactly one, known detachment must be selected (§4). Enhancement membership and per-enhancement
/// eligibility — "all enhancements belong to the detachment" — are enforced by <see cref="EnhancementRule"/> (R6).
/// </summary>
public sealed class DetachmentSelectionRule : IRosterRule
{
    public string Id => "R2";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Roster.DetachmentId))
        {
            yield return ValidationMessage.Error(Id, "Select a detachment.");
        }
        else if (context.Detachment is null)
        {
            yield return ValidationMessage.Error(Id, $"Unknown detachment '{context.Roster.DetachmentId}'.");
        }
    }
}
