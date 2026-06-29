using Microsoft.Extensions.DependencyInjection;
using Warhammer40k._11.Features.CombatSimulator.Domain;
using Warhammer40k._11.Features.CombatSimulator.Engine;

namespace Warhammer40k._11.Features.CombatSimulator;

/// <summary>
/// A feature-local, in-memory store for units imported from a New Recruit JSON export. Scoped to the app
/// session; it deliberately does <b>not</b> touch the app's roster store. Part of the removable Combat
/// Simulator feature — see <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public sealed class ImportedUnitStore
{
    private readonly List<CombatUnit> _units = [];

    public IReadOnlyList<CombatUnit> Units => _units;

    public void AddRange(IEnumerable<CombatUnit> units) => _units.AddRange(units);

    public void Clear() => _units.Clear();
}

/// <summary>
/// DI wiring for the Combat Simulator. The whole feature is registered by the single call
/// <c>builder.Services.AddCombatSimulator()</c> in <c>Program.cs</c> — one of only three external touch-points
/// (the others are one <c>&lt;NavLink&gt;</c> and the <c>@page "/combat-sim"</c> route). See
/// <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public static class CombatSimulatorModule
{
    public static IServiceCollection AddCombatSimulator(this IServiceCollection services)
    {
        services.AddSingleton<MonteCarloRunner>();
        services.AddScoped<ImportedUnitStore>();
        return services;
    }
}
