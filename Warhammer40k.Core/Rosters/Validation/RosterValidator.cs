using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters.Validation.Rules;

namespace Warhammer40k.Core.Rosters.Validation;

/// <summary>
/// The pure roster validation engine: runs rules R1–R11 over a roster and returns a <see
/// cref="ValidationResult"/> (every finding + the computed total). No I/O, so the API and the Blazor UI share
/// it. Rules are independent, so the default set can be replaced or extended for testing.
/// </summary>
public sealed class RosterValidator
{
    private readonly IReadOnlyList<IRosterRule> _rules;

    /// <summary>Creates a validator with the default rule set (R1–R11).</summary>
    public RosterValidator() : this(DefaultRules())
    {
    }

    /// <summary>Creates a validator with a custom rule set (used by per-rule unit tests).</summary>
    public RosterValidator(IEnumerable<IRosterRule> rules) => _rules = rules.ToList();

    /// <summary>The eleven rules in order (§4).</summary>
    public static IReadOnlyList<IRosterRule> DefaultRules() =>
    [
        new PointsLimitRule(),        // R1
        new DetachmentSelectionRule(),// R2
        new CopyLimitRule(),          // R3
        new EpicHeroRule(),           // R4
        new WarlordRule(),            // R5
        new EnhancementRule(),        // R6
        new LeaderAttachRule(),       // R7
        new UnitSizeRule(),           // R8
        new FactionCoherenceRule(),   // R9
        new PantheonRule(),           // R10
        new BattleSizeRule(),         // R11
    ];

    /// <summary>Validates a roster against the built-in detachments (<see cref="DetachmentCatalogue.BuiltIn"/>).</summary>
    public ValidationResult Validate(Roster roster, CatalogueData catalogue) =>
        Validate(roster, catalogue, DetachmentCatalogue.BuiltIn);

    /// <summary>Validates a roster against the supplied catalogue and detachment definitions.</summary>
    public ValidationResult Validate(Roster roster, CatalogueData catalogue, IReadOnlyList<Detachment> detachments)
    {
        var context = new RosterValidationContext(roster, catalogue, detachments);
        var messages = _rules.SelectMany(rule => rule.Evaluate(context)).ToList();
        return new ValidationResult(messages, context.TotalPoints);
    }
}
