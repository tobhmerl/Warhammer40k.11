using Warhammer40k.Core.Play;

namespace Warhammer40k._11.Pages;

public partial class PlaySession
{
    private static string? StratagemUsageHint(StratView stratagem) =>
        RuleUsage.ManualLimitHint(string.Join(" ", stratagem.When, stratagem.Target, stratagem.Effect, stratagem.Restrictions));

    private static string? DiscountUsageHint(BattleUnit unit)
    {
        var hints = unit.Parts.Where(part => part.ReducesStratagemCpCost)
            .SelectMany(part => part.Datasheet.Abilities)
            .Where(ability => ability.Name == BattlePart.MyWillBeDoneAbility)
            .Select(ability => RuleUsage.ManualLimitHint(ability.Text)).OfType<string>().Distinct().ToList();
        return hints.Count == 0 ? null : string.Join(" · ", hints);
    }

    // This is explicit permission in the entered Target clause, not an inference from a unit merely losing models.
    private static bool AllowsDestroyedTarget(StratView stratagem) =>
        stratagem.Target.Contains("even though it was just destroyed", StringComparison.OrdinalIgnoreCase);

    private bool CanTargetStratagem(BattleUnit unit, StratView stratagem) =>
        (!IsDead(unit) || AllowsDestroyedTarget(stratagem)) && StratagemAppliesTo(unit, stratagem.Target);

    private bool CanUseStratagem(BattleUnit unit, StratView stratagem) =>
        CanTargetStratagem(unit, stratagem) && CanAfford(stratagem, unit)
        && !(IsOncePerBattleStrat(stratagem) && IsOncePerBattleUsed(OncePerBattleStratKey(stratagem)));

    private IReadOnlyList<BattleUnit> StratagemTargetCandidates(StratView stratagem) =>
        OrderedUnits.Where(unit => CanTargetStratagem(unit, stratagem)).ToList();

    private bool CanUseOpenStratagem(StratView stratagem) =>
        StratContextUnit is { } unit && _byId.ContainsKey(unit.Id)
        && PhaseTurnStratagems().Any(current => StratKey(current) == StratKey(stratagem))
        && CanUseStratagem(unit, stratagem);

    private void SelectStratagemTarget(BattleUnit unit)
    {
        if (_nowStratagem is { } stratagem && CanUseStratagem(unit, stratagem))
            _overviewStratUnit = unit;
    }

    private void OpenArmyStratagem(StratView stratagem)
    {
        var usable = StratagemTargetCandidates(stratagem).Where(unit => CanUseStratagem(unit, stratagem)).ToList();
        _overviewStratUnit = usable.Count == 1 ? usable[0] : null;
        _nowStratagem = stratagem;
    }

    private string StratagemCostLabel(StratView stratagem, IEnumerable<BattleUnit>? units = null)
    {
        var targets = units ?? NowContextUnits.Where(unit => CanTargetStratagem(unit, stratagem));
        var costs = targets.Select(unit => EffectiveCost(stratagem, unit)).Distinct().Order().ToList();
        return costs.Count switch
        {
            0 => $"{stratagem.Cost} CP",
            1 => $"{costs[0]} CP",
            _ => $"{costs[0]}–{costs[^1]} CP",
        };
    }
}
