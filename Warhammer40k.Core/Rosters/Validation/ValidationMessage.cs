using System.Text.Json.Serialization;

namespace Warhammer40k.Core.Rosters.Validation;

/// <summary>Severity of a <see cref="ValidationMessage"/>. Only <see cref="Error"/> blocks "Ready" (§1/§4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ValidationSeverity>))]
public enum ValidationSeverity
{
    /// <summary>Informational note; never blocks readiness.</summary>
    Info,

    /// <summary>Soft rule; warns the user but does not block readiness.</summary>
    Warning,

    /// <summary>Hard rule; blocks "Ready" until resolved.</summary>
    Error,
}

/// <summary>
/// A single validation finding produced by a rule (§4: <c>{Severity, Text, RosterUnitId?}</c>).
/// <see cref="RuleId"/> ("R1".."R11") is carried so the UI can group findings and tests can target a rule.
/// </summary>
public sealed class ValidationMessage
{
    [JsonConstructor]
    public ValidationMessage(ValidationSeverity severity, string ruleId, string text, string? rosterUnitId = null)
    {
        Severity = severity;
        RuleId = ruleId;
        Text = text;
        RosterUnitId = rosterUnitId;
    }

    public ValidationSeverity Severity { get; }

    /// <summary>The rule that raised this message, e.g. "R1".</summary>
    public string RuleId { get; }

    public string Text { get; }

    /// <summary>The roster unit this message points at, when applicable (lets the UI highlight a card).</summary>
    public string? RosterUnitId { get; }

    public static ValidationMessage Error(string ruleId, string text, string? rosterUnitId = null) =>
        new(ValidationSeverity.Error, ruleId, text, rosterUnitId);

    public static ValidationMessage Warning(string ruleId, string text, string? rosterUnitId = null) =>
        new(ValidationSeverity.Warning, ruleId, text, rosterUnitId);

    public static ValidationMessage Info(string ruleId, string text, string? rosterUnitId = null) =>
        new(ValidationSeverity.Info, ruleId, text, rosterUnitId);
}
