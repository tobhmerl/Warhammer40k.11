using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k._11.Pages;

public partial class Play
{
    private Roster? _checkedRoster;
    private PlayReadinessResult? _readiness;
    private string? _readinessError;
    private bool _checkingPlay;

    private async Task CheckPlayAsync(Roster roster)
    {
        _checkedRoster = roster;
        _readiness = null;
        _readinessError = null;
        _checkingPlay = true;
        try
        {
            var library = await Api.GetScheduleLibraryAsync();
            if (roster.Units.Count > 0 && _catalogue.Datasheets.Count == 0)
                throw new InvalidOperationException("Catalogue data is unavailable.");
            _readiness = PlayReadiness.Check(roster, _catalogue, library, Settings.PlayCardSwipe);
        }
        catch (Exception)
        {
            _readinessError = "Could not load or verify Play setup. Retry before starting; missing data is not an empty schedule.";
        }
        finally
        {
            _checkingPlay = false;
        }
    }

    private void CloseReadiness() => _checkedRoster = null;
}
