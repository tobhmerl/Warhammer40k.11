using Microsoft.AspNetCore.Components.Routing;
using Warhammer40k.Core.Play;

namespace Warhammer40k._11.Pages;

public partial class RosterEditor
{
    private string? _loadError;
    private string? _saveError;
    private string? _readinessError;
    private bool _readinessOpen;
    private bool _checkingReadiness;
    private PlayReadinessResult? _readiness;
    private string? _pendingRuleAnchor;
    private string? _handledSetupQuery;
    private Task? _saveTask;

    private bool HasPendingSetup => _dirty || _libraryDirty || _saving;

    private async Task CheckPlayAsync()
    {
        _readinessOpen = true;
        _readiness = null;
        _readinessError = null;
        _checkingReadiness = true;
        _autoSaveCts?.Cancel();
        try
        {
            if (HasPendingSetup)
                await SaveAsync();
            if (HasPendingSetup || _loadError is not null || _roster is null)
                return;
            await Settings.InitializeAsync();
            _readiness = PlayReadiness.Check(_roster, _catalogue, _library, Settings.PlayCardSwipe);
        }
        catch (Exception)
        {
            _readinessError = "Could not complete the Play setup check. Reload the roster and try again.";
        }
        finally
        {
            _checkingReadiness = false;
        }
    }

    private void CloseReadiness() => _readinessOpen = false;

    private async Task BeforePlayNavigation(LocationChangingContext context)
    {
        var target = Nav.ToBaseRelativePath(context.TargetLocation).Split('?', '#')[0];
        if (!(target == "play" || target.StartsWith("play/", StringComparison.OrdinalIgnoreCase)) || !HasPendingSetup)
            return;
        _autoSaveCts?.Cancel();
        await SaveAsync();
        if (HasPendingSetup)
            context.PreventNavigation();
    }

    private void SetReferenceReview(string key, string name, string text, bool reviewed)
    {
        _library.GetOrCreate(key).ReviewedReferenceHash = reviewed ? PlayReadiness.ReviewHash(name, text) : null;
        MarkLibraryChanged();
    }

    private static string ScheduleAnchor(string key) => "schedule-" + Uri.EscapeDataString(key);

    private string? SetupQueryValue(string name)
    {
        foreach (var part in new Uri(Nav.Uri).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && string.Equals(Uri.UnescapeDataString(pair[0]), name, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(pair[1].Replace('+', ' '));
        }
        return null;
    }

    private void ApplySetupQuery()
    {
        if (_roster is null || !_authenticated || _loadError is not null || _notFound)
            return;
        var query = new Uri(Nav.Uri).Query;
        if (_handledSetupQuery == query)
            return;
        _handledSetupQuery = query;

        if (SetupQueryValue("unit") is { } unitId && _roster.FindUnit(unitId) is { } unit)
        {
            CloseReadiness();
            CloseTiming();
            OpenConfig(unit);
        }
        else if (SetupQueryValue("timing") == "1")
        {
            CloseReadiness();
            CloseConfig();
            OpenTiming();
        }

        if (SetupQueryValue("rule") is { } key)
        {
            _openSchedText.Add(key);
            if (_configuring is not null && CombinedScheduleAbilities(_configuring).FirstOrDefault(ability => ability.Key == key) is { } ability)
                _openSchedText.Add(ability.Name);
            _pendingRuleAnchor = ScheduleAnchor(key);
        }
    }
}
