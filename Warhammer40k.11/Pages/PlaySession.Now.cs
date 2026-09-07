using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k._11.Pages;

public partial class PlaySession
{
    private BattleUnit? _shootingCardUnit;
    private bool _abilityReference;

    private IReadOnlyList<BattleUnit> NowContextUnits =>
        NowUnit is { } unit ? [unit] : !_cardSwipe ? OrderedUnits : [];

    private IReadOnlyList<string> ShootingOptionsFor(BattleUnit unit) =>
        _battle?.ShootingOptionsFor(unit, _phase, _turn) ?? [];

    private IReadOnlyList<BattleUnit> NowShootingChoices =>
        NowContextUnits.Where(unit => !IsDead(unit) && ShootingOptionsFor(unit).Count > 0).ToList();

    private static IEnumerable<BattleAbility> ReferenceAbilitiesFor(BattleUnit unit) =>
        unit.CombinedAbilities.Where(ability => !ability.IsShootingChoice && ability.AppliedSummary is null
            && !ability.IsAlwaysAvailable && !ability.HasManualKeyword && ability.Windows.Count == 0);

    private IReadOnlyList<(BattleUnit Unit, BattleAbility Ability)> NowReferenceAbilities =>
        NowContextUnits.Where(unit => !IsDead(unit))
            .SelectMany(unit => ReferenceAbilitiesFor(unit).Select(ability => (Unit: unit, Ability: ability)))
            .ToList();

    private string ShootingChoiceName(BattleUnit unit) =>
        _battle?.WeaponChoicesFor(unit).FirstOrDefault()?.Name ?? "Shooting ability";

    private string ShootingChoiceTitle(BattleUnit unit) =>
        SelectedChoice(unit) ?? (CompactRules ? "Choose shooting ability" : ShootingChoiceName(unit));

    private static IReadOnlyList<Enhancement> ShootingEnhancements(BattleUnit unit) =>
        unit.Parts.Select(part => part.Enhancement).OfType<Enhancement>()
            .Where(enhancement => enhancement.ShootingAbilityOptions.Count > 0).ToList();

    private bool ShootingEnhancementEnabled(Enhancement enhancement) =>
        _battle?.Source.IsApplied(AbilityScheduleKeys.ForEnhancement(enhancement.Id)) ?? false;

    private string ShootingChoiceSources(BattleUnit unit) =>
        string.Join(", ", ShootingEnhancements(unit).Select(enhancement =>
            enhancement.Name + (ShootingEnhancementEnabled(enhancement) ? "" : " · needs setup")));

    private IReadOnlyList<DetachmentRule> ShootingChoiceRules(BattleUnit unit)
    {
        if (_battle is null)
            return [];
        var names = _battle.WeaponChoicesFor(unit).Select(choice => choice.Name).ToHashSet(StringComparer.Ordinal);
        return _battle.Detachments.SelectMany(detachment => detachment.Rules)
            .Where(rule => names.Contains(rule.Name)).ToList();
    }

    private void OpenShootingCard(BattleUnit unit)
    {
        if (!IsDead(unit) && ShootingOptionsFor(unit).Count > 0)
            _shootingCardUnit = unit;
    }

    private void CloseShootingCard() => _shootingCardUnit = null;

    private void ChooseShootingOption(BattleUnit unit, string option)
    {
        ToggleChoice(unit, option);
        CloseShootingCard();
    }

    private void OpenReferenceAbility(BattleUnit unit, BattleAbility ability)
    {
        OpenAbilityCard(unit, ability);
        _abilityReference = true;
    }
}
