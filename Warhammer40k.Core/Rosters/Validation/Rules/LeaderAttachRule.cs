using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R7 (Error/Info): a Leader may only attach to a unit on its allowed-targets list, and a Bodyguard unit may
/// hold only one Leader unless an additional Leader explicitly allows co-leading (<see
/// cref="Catalogue.Datasheet.AllowsCoLeader"/>). An unattached Leader-capable Character is an Info note (§4).
/// </summary>
public sealed class LeaderAttachRule : IRosterRule
{
    public string Id => "R7";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        var roster = context.Roster;
        var leadersByBodyguard = new Dictionary<string, List<Datasheet>>(StringComparer.Ordinal);

        foreach (var (unit, sheet) in context.ResolvedUnits())
        {
            if (string.IsNullOrEmpty(unit.AttachedToRosterUnitId))
            {
                if (sheet.IsCharacter && sheet.HasLeaderAbility)
                    yield return ValidationMessage.Info(Id, $"{sheet.Name} is not attached to a unit.", unit.Id);
                continue;
            }

            var target = roster.FindUnit(unit.AttachedToRosterUnitId);
            if (target is null)
            {
                yield return ValidationMessage.Error(Id, $"{sheet.Name} is attached to a unit that is no longer in the roster.", unit.Id);
                continue;
            }

            var targetSheet = context.DatasheetFor(target);
            if (targetSheet is not null
                && !sheet.LeaderTargetIds.Contains(targetSheet.Id, StringComparer.OrdinalIgnoreCase))
            {
                yield return ValidationMessage.Error(Id, $"{sheet.Name} cannot be attached to {targetSheet.Name}.", unit.Id);
            }

            if (!leadersByBodyguard.TryGetValue(target.Id, out var leaders))
                leadersByBodyguard[target.Id] = leaders = [];
            leaders.Add(sheet);
        }

        foreach (var (bodyguardId, leaders) in leadersByBodyguard)
        {
            if (leaders.Count <= 1)
                continue;

            // At most one Leader that does not allow co-leading may sit on a Bodyguard.
            if (leaders.Count(l => !l.AllowsCoLeader) > 1)
            {
                var bodyguard = roster.FindUnit(bodyguardId);
                var bodyguardName = (bodyguard is null ? null : context.DatasheetFor(bodyguard)?.Name) ?? "that unit";
                yield return ValidationMessage.Error(Id, $"{bodyguardName} has more than one Leader attached.", bodyguardId);
            }
        }

        // TODO(§10/§11): Cryptothralls / Tomb Crawlers retinue augment (max one, mutually exclusive) on a Cryptek-led unit.
    }
}
