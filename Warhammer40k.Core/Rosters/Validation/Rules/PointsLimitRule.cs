namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>R1 (Error): total points (units + enhancements + Pantheon surcharges) must not exceed the limit (§4).</summary>
public sealed class PointsLimitRule : IRosterRule
{
    public string Id => "R1";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        if (context.TotalPoints > context.Roster.PointsLimit)
        {
            yield return ValidationMessage.Error(Id,
                $"Roster is {context.TotalPoints} points, over the {context.Roster.PointsLimit} point limit.");
        }
    }
}
