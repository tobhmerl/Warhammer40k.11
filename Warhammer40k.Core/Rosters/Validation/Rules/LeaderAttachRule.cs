using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Rosters.Validation.Rules;

/// <summary>
/// R7 (Error/Info): a Leader may only attach to a unit on its allowed-targets list, and a Bodyguard unit may
/// hold only one Leader unless an additional Leader explicitly allows co-leading (<see
/// cref="Catalogue.Datasheet.AllowsCoLeader"/>). An unattached Leader-capable Character is an Info note (§4).
/// <para>
/// A <i>retinue</i> unit (<see cref="Catalogue.Datasheet.IsRetinue"/> — Canoptek Tomb Crawlers, Cryptothralls)
/// is not a Leader: it joins a unit that is already being led by a
/// <see cref="Catalogue.Datasheet.RetinueLeaderKeyword"/> model, and a host may hold at most one retinue
/// (which is what "no more than one Tomb Crawlers, and not both Tomb Crawlers and Cryptothralls" amounts to).
/// </para>
/// </summary>
public sealed class LeaderAttachRule : IRosterRule
{
    public string Id => "R7";

    public IEnumerable<ValidationMessage> Evaluate(RosterValidationContext context)
    {
        var roster = context.Roster;
        var leadersByBodyguard = new Dictionary<string, List<Datasheet>>(StringComparer.Ordinal);
        var retinuesByHost = new Dictionary<string, List<Datasheet>>(StringComparer.Ordinal);

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
                var what = sheet.IsRetinue ? "joined to" : "attached to";
                yield return ValidationMessage.Error(Id, $"{sheet.Name} is {what} a unit that is no longer in the roster.", unit.Id);
                continue;
            }

            if (sheet.IsRetinue)
            {
                // A retinue joins the BODYGUARD unit, so the host must itself be led by the required keyword.
                if (!IsLedBy(context, target, sheet.RetinueLeaderKeyword))
                {
                    var targetName = context.DatasheetFor(target)?.Name ?? "that unit";
                    yield return ValidationMessage.Error(Id,
                        $"{sheet.Name} can only join a unit led by a {sheet.RetinueLeaderKeyword} model; {targetName} is not.", unit.Id);
                }

                if (!retinuesByHost.TryGetValue(target.Id, out var retinues))
                    retinuesByHost[target.Id] = retinues = [];
                retinues.Add(sheet);
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
                yield return ValidationMessage.Error(Id, $"{HostName(context, bodyguardId)} has more than one Leader attached.", bodyguardId);
            }
        }

        foreach (var (hostId, retinues) in retinuesByHost)
        {
            if (retinues.Count > 1)
            {
                yield return ValidationMessage.Error(Id,
                    $"{HostName(context, hostId)} cannot have more than one retinue unit joined to it.", hostId);
            }
        }
    }

    /// <summary>True when <paramref name="host"/> has a Leader attached whose datasheet carries <paramref name="keyword"/>.</summary>
    private static bool IsLedBy(RosterValidationContext context, RosterUnit host, string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
            return false;

        foreach (var (unit, sheet) in context.ResolvedUnits())
        {
            if (sheet.IsRetinue || !string.Equals(unit.AttachedToRosterUnitId, host.Id, StringComparison.Ordinal))
                continue;
            if (sheet.Keywords.Any(k => KeywordMatches(k, keyword)))
                return true;
        }
        return false;
    }

    /// <summary>A datasheet keyword matches when it equals the wanted one, or is the qualified "Prefix: Value" form of it.</summary>
    private static bool KeywordMatches(string datasheetKeyword, string wanted)
    {
        if (string.Equals(datasheetKeyword, wanted, StringComparison.OrdinalIgnoreCase))
            return true;
        var colon = datasheetKeyword.IndexOf(':');
        return colon >= 0 && string.Equals(datasheetKeyword[(colon + 1)..].Trim(), wanted, StringComparison.OrdinalIgnoreCase);
    }

    private static string HostName(RosterValidationContext context, string hostId)
    {
        var host = context.Roster.FindUnit(hostId);
        return (host is null ? null : context.DatasheetFor(host)?.Name) ?? "that unit";
    }
}
