using Warhammer40k.Core.Play;

namespace Warhammer40k._11.Pages;

// ---- Auras ---------------------------------------------------------------------------------------
// An aura ability (e.g. Nekrosor Ammentar's "Infectious Murder-madness") reaches beyond its own unit:
// every friendly unit standing inside the bubble gets its effect. The app has no board state, so:
//
//   * the bearer's own unit is always within its own aura -> the effect is applied automatically, with
//     no toggle, which is what puts [SUSTAINED HITS 1] on all of Ammentar's weapons;
//   * every other unit the aura could legally affect is offered it as a manual, per-unit toggle, so the
//     player confirms who is actually in range.
//
// Eligibility comes from AuraParser: the outer "friendly X unit (excluding Y)" clause, narrowed by an
// inner keyword condition unless the rule offers a second, board-state alternative.
public partial class PlaySession
{
    // One aura a unit could benefit from, together with where it comes from.
    private sealed record AuraOffer(BattleUnit Source, BattleAbility Ability, AuraEffect Aura, bool IsSelf)
    {
        // The bearer stands in its own bubble, so its aura needs no confirmation.
        public bool IsAutomatic => IsSelf;
    }

    // Every aura in the army that could affect this unit, the unit's own first.
    //
    // The bearer's own aura is a permanent weapon/unit stat buff, so it is always present. An aura reaching
    // this unit from ANOTHER unit is a decision the player has to make each phase (is it within 6"? is it a
    // Destroyer Cult unit? is it attacking the closest eligible target?), so \u2014 exactly like every other
    // ability that is not chipped as a keyword \u2014 it is only offered in the phase + turn windows ticked for
    // that ability in setup, and applies nowhere else.
    private List<AuraOffer> AuraOffersFor(BattleUnit unit)
    {
        var offers = new List<AuraOffer>();
        if (_battle is null || IsDead(unit))
            return offers;

        var keywords = UnitKeywords(unit);
        foreach (var source in OrderedUnits.Where(u => !IsDead(u)))
            foreach (var ability in source.CombinedAbilities)
            {
                if (AuraParser.Parse(ability.Ability) is not { } aura || !aura.AppliesTo(keywords))
                    continue;
                var isSelf = ReferenceEquals(source, unit);
                if (!isSelf && !IsScheduledNow(ability))
                    continue;
                offers.Add(new AuraOffer(source, ability, aura, isSelf));
            }

        return offers.OrderByDescending(o => o.IsSelf).ToList();
    }

    // True when the player ticked this ability's window for the current phase + turn.
    private bool IsScheduledNow(BattleAbility ability) =>
        ability.Windows.Any(w => w.Phase == _phase && w.Turn == _turn);

    // The auras this unit is offered by OTHER units — the ones the player has to confirm.
    private List<AuraOffer> ForeignAuraOffersFor(BattleUnit unit) =>
        AuraOffersFor(unit).Where(o => !o.IsSelf).ToList();

    private static List<string> UnitKeywords(BattleUnit unit) =>
        unit.Parts
            .SelectMany(p => p.Datasheet.Keywords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // Keyed by the RECEIVING unit, so confirming an aura for one unit never touches another.
    private static string AuraKey(BattleUnit unit, AuraOffer offer) =>
        $"{unit.Id}|aura|{offer.Source.Id}|{offer.Ability.Ability.Name}";

    // An aura applies to a unit when it is the bearer's own (automatic) or the player ticked it for that unit.
    private bool IsAuraActive(BattleUnit unit, AuraOffer offer) =>
        offer.IsAutomatic || _activeEffects.Contains(AuraKey(unit, offer));

    private void ToggleAura(BattleUnit unit, AuraOffer offer, bool on)
    {
        if (offer.IsAutomatic)
            return;
        if (on)
            _activeEffects.Add(AuraKey(unit, offer));
        else
            _activeEffects.Remove(AuraKey(unit, offer));
        ScheduleBattleSave();
    }

    // The weapon keywords the active auras grant this unit (merged into the weapon rows' granted chips).
    private IEnumerable<string> ActiveAuraKeywords(BattleUnit unit)
    {
        foreach (var offer in AuraOffersFor(unit))
            if (IsAuraActive(unit, offer))
                foreach (var keyword in offer.Aura.GrantedKeywords)
                    yield return keyword;
    }

    // True when this ability is an aura that grants weapon keywords — such an ability is handled entirely by
    // the aura surfaces, so it must not also appear as an ordinary manual "effect" on its bearer's card.
    private static bool IsAuraAbility(BattleAbility ability) => AuraParser.Parse(ability.Ability) is not null;

    // "Ammentar · within 6\"" — the aura card's subtitle, naming who projects it and how far it reaches.
    private static string AuraSource(AuraOffer offer) =>
        offer.Aura.RangeInches > 0
            ? $"{ShortName(offer.Source.Name)} \u00b7 within {offer.Aura.RangeInches}\""
            : ShortName(offer.Source.Name);

    private static string AuraLabel(AuraOffer offer) =>
        string.Join(", ", offer.Aura.GrantedKeywords.Select(k => $"[{k}]"));

    // ---- The aura sheet ----------------------------------------------------------------------------
    // Opened from an aura card / chip. It carries the receiving unit, so its checkbox binds to that unit.
    private AuraOffer? _auraCard;
    private BattleUnit? _auraCardUnit;

    private void OpenAuraCard(BattleUnit unit, AuraOffer offer)
    {
        _auraCardUnit = unit;
        _auraCard = offer;
    }

    private void CloseAuraCard()
    {
        _auraCard = null;
        _auraCardUnit = null;
    }
}
