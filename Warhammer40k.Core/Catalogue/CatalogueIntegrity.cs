namespace Warhammer40k.Core.Catalogue;

/// <summary>Severity of a <see cref="CatalogueIssue"/>. Errors are structural; warnings are advisory.</summary>
public enum CatalogueIssueLevel
{
    Warning,
    Error,
}

/// <summary>A single referential-integrity finding for the catalogue (surfaced in the Catalogue editor, AB7).</summary>
public sealed class CatalogueIssue
{
    public CatalogueIssue(CatalogueIssueLevel level, string text, string? datasheetId = null)
    {
        Level = level;
        Text = text;
        DatasheetId = datasheetId;
    }

    public CatalogueIssueLevel Level { get; }

    public string Text { get; }

    /// <summary>The datasheet this issue relates to, when applicable (lets the editor jump to it).</summary>
    public string? DatasheetId { get; }

    public bool IsError => Level == CatalogueIssueLevel.Error;

    public static CatalogueIssue Error(string text, string? datasheetId = null) =>
        new(CatalogueIssueLevel.Error, text, datasheetId);

    public static CatalogueIssue Warning(string text, string? datasheetId = null) =>
        new(CatalogueIssueLevel.Warning, text, datasheetId);
}

/// <summary>
/// Pure referential-integrity checks for an edited <see cref="CatalogueData"/> (no I/O): duplicate ids/names,
/// missing unit sizes, wargear-group/option consistency, and Pantheon binding ↔ datasheet linkage. Used by the
/// Catalogue editor to surface problems without blocking saves of work-in-progress.
/// </summary>
public static class CatalogueIntegrity
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public static IReadOnlyList<CatalogueIssue> Check(CatalogueData catalogue)
    {
        var issues = new List<CatalogueIssue>();

        foreach (var dup in catalogue.Datasheets
            .Where(d => !string.IsNullOrEmpty(d.Id))
            .GroupBy(d => d.Id, Ci)
            .Where(g => g.Count() > 1))
        {
            issues.Add(CatalogueIssue.Error($"Duplicate datasheet id '{dup.Key}' ({dup.Count()} datasheets).", dup.First().Id));
        }

        foreach (var dup in catalogue.Datasheets
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .GroupBy(d => d.Name, Ci)
            .Where(g => g.Count() > 1))
        {
            issues.Add(CatalogueIssue.Warning($"Duplicate datasheet name '{dup.Key}'.", dup.First().Id));
        }

        foreach (var d in catalogue.Datasheets)
        {
            if (string.IsNullOrWhiteSpace(d.Name))
                issues.Add(CatalogueIssue.Error("A datasheet has no name.", d.Id));

            if (d.PointsOptions.Count == 0)
                issues.Add(CatalogueIssue.Warning($"{Label(d)} has no unit sizes (points options).", d.Id));

            CheckWargear(d, issues);

            if (d.IsMonster && catalogue.FindBindingForUnit(d.Name) is null)
                issues.Add(CatalogueIssue.Warning($"{Label(d)} is a Monster with no Pantheon binding.", d.Id));
        }

        foreach (var binding in catalogue.PantheonBindings)
        {
            if (!catalogue.Datasheets.Any(d => Ci.Equals(d.Name, binding.Unit)))
                issues.Add(CatalogueIssue.Warning($"Pantheon binding '{binding.Name}' references unknown unit '{binding.Unit}'."));
        }

        return issues;
    }

    private static void CheckWargear(Datasheet d, List<CatalogueIssue> issues)
    {
        var groupIds = new HashSet<string>(Ci);

        foreach (var group in d.WargearGroups)
        {
            if (string.IsNullOrWhiteSpace(group.Name))
                issues.Add(CatalogueIssue.Error($"{Label(d)} has a wargear group with no name.", d.Id));

            if (!string.IsNullOrEmpty(group.Id) && !groupIds.Add(group.Id))
                issues.Add(CatalogueIssue.Error($"{Label(d)} has a duplicate wargear group id '{group.Id}'.", d.Id));

            if (group.Max > 0 && group.Min > group.Max)
                issues.Add(CatalogueIssue.Error($"{Label(d)}: wargear group '{group.Name}' has Min {group.Min} above Max {group.Max}.", d.Id));

            var optionIds = new HashSet<string>(Ci);
            foreach (var option in group.Options)
            {
                if (string.IsNullOrWhiteSpace(option.Name))
                    issues.Add(CatalogueIssue.Error($"{Label(d)}: wargear group '{group.Name}' has an unnamed option.", d.Id));

                if (!string.IsNullOrEmpty(option.Id) && !optionIds.Add(option.Id))
                    issues.Add(CatalogueIssue.Error($"{Label(d)}: wargear group '{group.Name}' has a duplicate option id '{option.Id}'.", d.Id));
            }
        }
    }

    private static string Label(Datasheet d) =>
        !string.IsNullOrWhiteSpace(d.Name) ? d.Name
        : !string.IsNullOrEmpty(d.Id) ? d.Id
        : "Unnamed datasheet";
}
