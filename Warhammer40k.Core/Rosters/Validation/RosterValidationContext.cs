using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Rosters.Validation;

/// <summary>
/// Pre-resolved inputs handed to every <see cref="IRosterRule"/> so each rule stays small and pure: the
/// roster, the catalogue, and the single selected <see cref="Detachment"/>. Catalogue look-ups and the
/// running total are memoised here so rules don't repeat them.
/// </summary>
public sealed class RosterValidationContext
{
    private int? _totalPoints;

    public RosterValidationContext(Roster roster, CatalogueData catalogue, IReadOnlyList<Detachment> detachments)
    {
        Roster = roster;
        Catalogue = catalogue;
        Detachments = detachments;
        Detachment = detachments.FirstOrDefault(d =>
            string.Equals(d.Id, roster.DetachmentId, StringComparison.OrdinalIgnoreCase));
    }

    public Roster Roster { get; }

    public CatalogueData Catalogue { get; }

    /// <summary>All detachments the roster could have chosen (used by R2 to test "exactly one, known").</summary>
    public IReadOnlyList<Detachment> Detachments { get; }

    /// <summary>The detachment resolved from <see cref="Roster.DetachmentId"/>, or <c>null</c> when unset/unknown.</summary>
    public Detachment? Detachment { get; }

    /// <summary>The catalogue datasheet for a unit, or <c>null</c> when the reference is dangling.</summary>
    public Datasheet? DatasheetFor(RosterUnit unit) => Catalogue.FindById(unit.DatasheetId);

    /// <summary>Units paired with their datasheet; units whose datasheet is missing are skipped (R9 reports those).</summary>
    public IEnumerable<(RosterUnit Unit, Datasheet Sheet)> ResolvedUnits()
    {
        foreach (var unit in Roster.Units)
        {
            var sheet = DatasheetFor(unit);
            if (sheet is not null)
                yield return (unit, sheet);
        }
    }

    public int UnitPoints(RosterUnit unit) => RosterCalculator.UnitPoints(unit, DatasheetFor(unit));

    public int EnhancementPoints(RosterUnit unit) => RosterCalculator.EnhancementPoints(unit, Detachment);

    /// <summary>Memoised roster total (rule R1 input).</summary>
    public int TotalPoints => _totalPoints ??= RosterCalculator.TotalPoints(Roster, Catalogue, Detachment);
}
