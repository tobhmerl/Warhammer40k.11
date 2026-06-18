using System.Text.Json;
using Warhammer40k.Core.Rosters.Validation;

namespace Warhammer40k.Tests;

/// <summary>
/// Locks the wire contract for the <c>POST /api/rosters/validate</c> response: <see cref="ValidationResult"/>
/// and <see cref="ValidationMessage"/> must survive a JSON round-trip with the Web defaults used by both the
/// Functions host and the Blazor <c>HttpClient</c>, with severity emitted as a readable string and the derived
/// members omitted.
/// </summary>
public class RosterValidationWireTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static ValidationResult SampleResult() => new(
        [
            ValidationMessage.Error("R1", "Roster is 2100 points, over the 2000 point limit."),
            ValidationMessage.Warning("R7", "Overlord is not attached to a unit.", "unit-7"),
            ValidationMessage.Info("R11", "Just so you know."),
        ],
        totalPoints: 2100);

    [Fact]
    public void ValidationResult_round_trips_through_web_json()
    {
        var json = JsonSerializer.Serialize(SampleResult(), Web);
        var back = JsonSerializer.Deserialize<ValidationResult>(json, Web);

        Assert.NotNull(back);
        Assert.Equal(2100, back!.TotalPoints);
        Assert.False(back.IsReady);
        Assert.Equal(3, back.Messages.Count);

        var error = Assert.Single(back.Errors);
        Assert.Equal("R1", error.RuleId);
        Assert.Equal(ValidationSeverity.Error, error.Severity);

        var warning = Assert.Single(back.Warnings);
        Assert.Equal("unit-7", warning.RosterUnitId);
    }

    [Fact]
    public void Severity_is_serialized_as_a_string()
    {
        var json = JsonSerializer.Serialize(SampleResult(), Web);

        Assert.Contains("\"severity\":\"Error\"", json);
        Assert.DoesNotContain("\"severity\":0", json);
    }

    [Fact]
    public void Derived_members_are_not_serialized()
    {
        var json = JsonSerializer.Serialize(SampleResult(), Web);

        Assert.DoesNotContain("isReady", json);
        Assert.DoesNotContain("errors", json);
        Assert.DoesNotContain("warnings", json);
        Assert.DoesNotContain("infos", json);
    }
}
