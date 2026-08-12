using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k._11.Pages;

// ---- Compact rules (experimental) ----------------------------------------------------------------
// With the "Compact rules" preference on, a NOW card shows WHAT a rule does instead of what it is
// called: "SH1: Destroyer Cult or closest target" rather than "Infectious Murder-madness (Aura)".
//
// This is presentation only. The printed name and text are untouched and remain the source of truth:
// every card still opens its sheet with the full rules text, and the card's tooltip carries the
// original name. All shortening lives in Core's CompactRule, so the UI has no abbreviation logic and
// every card kind reads the same way. When a rule cannot be summarised safely, CompactRule returns
// null and the card falls back to the name.
public partial class PlaySession
{
    private bool CompactRules => Settings.Current.PlayCompactRules;

    /// <summary>The title for an ability / effect / enhancement card.</summary>
    private string AbilityTitle(BattleAbility ability) =>
        Compact(CompactRule.Summarize(ability.Ability.Text), ability.Ability.Name);

    /// <summary>The title for an aura card offered to another unit.</summary>
    private string AuraTitle(AuraOffer offer) =>
        Compact(CompactRule.Summarize(offer.Ability.Ability.Text), offer.Ability.Ability.Name);

    /// <summary>The title for a detachment buff card (its Effect line is already a short sentence).</summary>
    private string BuffTitle(ConditionalUnitBuff buff) =>
        Compact(CompactRule.Summarize(buff.Effect), buff.Label);

    /// <summary>The title for an army-rule card.</summary>
    private string ArmyRuleTitle(ArmyRule rule) =>
        Compact(CompactRule.Summarize(rule.Text), rule.Name);

    /// <summary>The title for a stratagem card: its effect, falling back to its restrictions.</summary>
    private string StratagemTitle(StratView strat) =>
        Compact(CompactRule.SummarizeStratagem(strat.Effect, strat.Restrictions), strat.Name);

    // The rule's name is always the tooltip in compact mode, so nothing is lost by swapping the title.
    private string CardTooltip(string name) => CompactRules ? name : "";

    private string Compact(string? summary, string name) =>
        CompactRules && !string.IsNullOrWhiteSpace(summary) ? summary! : name;
}
