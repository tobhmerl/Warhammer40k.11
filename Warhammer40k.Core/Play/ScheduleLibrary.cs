using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Core.Play;

/// <summary>
/// The signed-in user's persistent, cross-roster library of Play-Mode schedules (keyed by
/// <see cref="AbilityScheduleKeys"/>). Because scheduling keys are global (per datasheet ability,
/// enhancement, stratagem, army rule or detachment buff — never per roster-unit), a single library
/// can seed every roster: define "Their Number Is Legion" once and every future roster with Warriors
/// already has it. The library is the single source of truth — it wins over any legacy per-roster copy.
/// </summary>
public sealed class ScheduleLibrary
{
    /// <summary>Every schedule the player has configured, keyed by <see cref="AbilitySchedule.Key"/>.</summary>
    public List<AbilitySchedule> Schedules { get; set; } = [];

    /// <summary>A fresh, empty library.</summary>
    public static ScheduleLibrary Empty => new();

    /// <summary>The schedule for a key, or null when the player has not configured it yet.</summary>
    public AbilitySchedule? Find(string key) =>
        Schedules.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

    /// <summary>The schedule for a key, creating (and storing) an empty one if none exists yet.</summary>
    public AbilitySchedule GetOrCreate(string key)
    {
        var existing = Find(key);
        if (existing is not null)
            return existing;
        var created = new AbilitySchedule { Key = key };
        Schedules.Add(created);
        return created;
    }

    /// <summary>
    /// Copies any of the roster's own schedules whose key the library does not yet have into the library,
    /// migrating legacy per-roster definitions so they become global defaults. Returns true when anything
    /// was added (so the caller can persist the library).
    /// </summary>
    public bool SeedMissingFrom(Roster roster)
    {
        var added = false;
        foreach (var schedule in roster.AbilitySchedules)
            if (Find(schedule.Key) is null)
            {
                Schedules.Add(schedule);
                added = true;
            }
        return added;
    }

    /// <summary>
    /// The effective schedules for a roster: the roster's own list with the library merged on top so the
    /// library wins per key, while any key present only on the roster (not yet in the library) is preserved.
    /// </summary>
    public List<AbilitySchedule> EffectiveFor(Roster roster)
    {
        var map = new Dictionary<string, AbilitySchedule>(StringComparer.Ordinal);
        foreach (var schedule in roster.AbilitySchedules)
            map[schedule.Key] = schedule;
        foreach (var schedule in Schedules)
            map[schedule.Key] = schedule;
        return [.. map.Values];
    }
}
