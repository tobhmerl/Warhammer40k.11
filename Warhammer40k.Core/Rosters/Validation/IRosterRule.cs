namespace Warhammer40k.Core.Rosters.Validation;

/// <summary>
/// A single, independently unit-testable validation rule (§4). Implementations are <b>pure</b> — they read
/// the <see cref="RosterValidationContext"/> and yield findings without mutating state or doing I/O.
/// </summary>
public interface IRosterRule
{
    /// <summary>Stable rule id, "R1".."R11"; surfaced on every <see cref="ValidationMessage"/> it raises.</summary>
    string Id { get; }

    /// <summary>Evaluates the rule against the context and yields any findings (possibly none).</summary>
    IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context);
}
