using Warhammer40k.Core.Play;

namespace Warhammer40k.Core.Rosters.Validation;

/// <summary>
/// Keeps unit Enhancements consistent with the selected detachments. Enhancements are detachment-specific, so
/// when the player switches detachment (e.g. Starshatter Arsenal → Awakened Dynasty) any assignment drawn from
/// a no-longer-selected detachment is revoked, and its now-dead Play-Mode schedule is dropped. Like
/// <see cref="PantheonBindingApplier"/> this is the engine's only mutation here: the UI calls it on roster
/// edits, then validates. It is pure (no I/O) and idempotent.
/// </summary>
public static class EnhancementReconciler
{
    /// <summary>
    /// Revokes any unit Enhancement that none of <paramref name="selectedDetachments"/> offers, and removes the
    /// orphaned enhancement schedule. Returns <c>true</c> when at least one assignment was revoked.
    /// </summary>
    public static bool Revoke(Roster roster, IReadOnlyList<Detachment> selectedDetachments)
    {
        var valid = new HashSet<string>(
            selectedDetachments.SelectMany(d => d.Enhancements).Select(e => e.Id),
            StringComparer.OrdinalIgnoreCase);

        var revokedIds = new List<string>();
        foreach (var unit in roster.Units)
        {
            var id = unit.AssignedEnhancementId;
            if (!string.IsNullOrEmpty(id) && !valid.Contains(id))
            {
                unit.AssignedEnhancementId = null;
                revokedIds.Add(id);
            }
        }

        foreach (var id in revokedIds)
        {
            // An enhancement schedule is shared wherever that enhancement is assigned; drop it only once no unit
            // still holds the enhancement (after revocation that is always the case for an orphaned id).
            if (roster.Units.Any(u => string.Equals(u.AssignedEnhancementId, id, StringComparison.OrdinalIgnoreCase)))
                continue;
            var key = AbilityScheduleKeys.ForEnhancement(id);
            roster.AbilitySchedules.RemoveAll(s => string.Equals(s.Key, key, StringComparison.Ordinal));
        }

        return revokedIds.Count > 0;
    }
}
