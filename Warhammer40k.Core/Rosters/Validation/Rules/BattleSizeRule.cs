namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R11 (Info): battle-size minimums. There are none currently, so this rule never blocks and is present for
/// completeness and forward-compatibility (§4). Add Info findings here if minimums are introduced later.
/// </summary>
public sealed class BattleSizeRule : IRosterRule
{
    public string Id => "R11";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context) => [];
}
