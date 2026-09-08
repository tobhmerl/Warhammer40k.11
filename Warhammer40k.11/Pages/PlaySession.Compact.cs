using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k._11.Pages;

// Rule cards always retain their printed names, including when an older settings backup enables compact rules.
public partial class PlaySession
{
    /// <summary>The title for an ability / effect / enhancement card.</summary>
    private string AbilityTitle(BattleAbility ability) =>
        ability.Ability.Name;

    /// <summary>The title for an aura card offered to another unit.</summary>
    private string AuraTitle(AuraOffer offer) =>
        offer.Ability.Ability.Name;

    /// <summary>The authored name of a detachment buff.</summary>
    private string BuffTitle(ConditionalUnitBuff buff) =>
        buff.Label;

    /// <summary>The title for an army-rule card.</summary>
    private string ArmyRuleTitle(ArmyRule rule) =>
        rule.Name;

    /// <summary>The printed stratagem name, never an effect summary.</summary>
    private string StratagemTitle(StratView strat) =>
        strat.Name;

    private string CardTooltip(string name) => name;
}
