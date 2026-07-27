using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;

namespace Warhammer40k.Core.Rosters;

/// <summary>
/// Builds a view of a roster in which every conferral is active. Play Mode gates leader conferrals and
/// enhancements behind the player's "apply to unit" tick, because during a game they are switched on as the
/// situation demands. Tools that ask "what can this army actually do" — the Combat Simulator and the roster
/// export — want the full picture instead, without the player having had to configure anything.
/// </summary>
public static class RosterConferrals
{
    /// <summary>
    /// A shallow copy of <paramref name="roster"/> whose ability schedules all have
    /// <see cref="AbilitySchedule.ApplyToUnit"/> set, plus a schedule for every leader conferral and assigned
    /// enhancement it can reach. Units are shared and never mutated, so the stored roster is untouched.
    /// </summary>
    /// <remarks>
    /// Board-state conditional detachment buffs ("+1 to Hit while the target is on an objective") are
    /// deliberately left alone — nothing can know whether their condition holds.
    /// </remarks>
    public static Roster WithAllApplied(Roster roster, CatalogueData catalogue)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(catalogue);

        var copy = new Roster
        {
            Id = roster.Id,
            Name = roster.Name,
            Faction = roster.Faction,
            PointsLimit = roster.PointsLimit,
            DetachmentId = roster.DetachmentId,
            DetachmentIds = roster.DetachmentIds,
            Units = roster.Units,
            AbilitySchedules = roster.AbilitySchedules
                .Select(s => new AbilitySchedule
                {
                    Key = s.Key,
                    Windows = s.Windows,
                    ApplyToUnit = true,
                    ManualKeyword = s.ManualKeyword,
                })
                .ToList(),
        };

        foreach (var unit in roster.Units)
        {
            if (catalogue.FindById(unit.DatasheetId) is { } sheet)
            {
                foreach (var conferral in sheet.LeaderConferrals.Where(c => !string.IsNullOrEmpty(c.SourceAbility)))
                    copy.GetOrCreateSchedule(AbilityScheduleKeys.ForUnitAbility(sheet.Id, conferral.SourceAbility)).ApplyToUnit = true;
            }

            if (!string.IsNullOrEmpty(unit.AssignedEnhancementId))
                copy.GetOrCreateSchedule(AbilityScheduleKeys.ForEnhancement(unit.AssignedEnhancementId)).ApplyToUnit = true;
        }

        return copy;
    }
}
