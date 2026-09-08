using Warhammer40k.Core.Play;

namespace Warhammer40k._11.Pages;

public partial class PlaySession
{
    private string? _loadError;
    private string? _readinessError;
    private bool _readinessOpen;
    private bool? _readinessFocus;
    private PlayReadinessResult? _readiness;

    private PlayReadinessResult? Readiness
    {
        get
        {
            if (_roster is null || _loadError is not null)
                return null;
            if (_readinessFocus != _cardSwipe)
            {
                _readiness = null;
                _readinessError = null;
            }
            if (_readiness is null && _readinessError is null)
            {
                _readinessFocus = _cardSwipe;
                try { _readiness = PlayReadiness.Check(_roster, _catalogue, ScheduleLibrary.Empty, _cardSwipe); }
                catch (Exception) { _readinessError = "Play setup could not be verified. Review the roster before starting a new game."; }
            }
            return _readiness;
        }
    }

    private void OpenReadiness() => _readinessOpen = true;
    private void CloseReadiness() => _readinessOpen = false;

    private void RecheckReadiness()
    {
        _readiness = null;
        _readinessError = null;
    }

    private void UseFocusedForReadiness()
    {
        if (ActiveUnit is { } unit)
            FocusReminder(unit);
        RecheckReadiness();
    }
}
