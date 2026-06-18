using System.Text.Json.Serialization;

namespace Warhammer40k.Core.Rosters.Validation;

/// <summary>
/// The outcome of validating a roster: every <see cref="ValidationMessage"/> from rules R1–R11 plus the
/// computed total points. A roster is "Ready" only when it carries no <see cref="ValidationSeverity.Error"/> (§4).
/// Serializable so the server-side validate endpoint can return it to the client unchanged.
/// </summary>
public sealed class ValidationResult
{
    [JsonConstructor]
    public ValidationResult(IReadOnlyList<ValidationMessage> messages, int totalPoints)
    {
        Messages = messages;
        TotalPoints = totalPoints;
    }

    public IReadOnlyList<ValidationMessage> Messages { get; }

    /// <summary>Sum of unit points + enhancement points + Pantheon surcharges (rule R1 input).</summary>
    public int TotalPoints { get; }

    /// <summary>True when there are no Error messages — the roster may be marked Ready.</summary>
    [JsonIgnore]
    public bool IsReady => !Messages.Any(m => m.Severity == ValidationSeverity.Error);

    [JsonIgnore]
    public IEnumerable<ValidationMessage> Errors => Messages.Where(m => m.Severity == ValidationSeverity.Error);

    [JsonIgnore]
    public IEnumerable<ValidationMessage> Warnings => Messages.Where(m => m.Severity == ValidationSeverity.Warning);

    [JsonIgnore]
    public IEnumerable<ValidationMessage> Infos => Messages.Where(m => m.Severity == ValidationSeverity.Info);

    /// <summary>True when any message was raised by the given rule id (e.g. "R4").</summary>
    public bool HasMessageFrom(string ruleId) =>
        Messages.Any(m => string.Equals(m.RuleId, ruleId, StringComparison.Ordinal));
}
