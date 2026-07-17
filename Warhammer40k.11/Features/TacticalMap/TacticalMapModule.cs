using Microsoft.Extensions.DependencyInjection;

namespace Warhammer40k._11.Features.TacticalMap;

/// <summary>
/// DI wiring for the Tactical Map feature. The whole feature is registered by the single call
/// <c>builder.Services.AddTacticalMap()</c> in <c>Program.cs</c> — one of only three external touch-points
/// (the others are one <c>&lt;NavLink&gt;</c> and the <c>@page "/tactical-map"</c> route). Server persistence
/// of plans reuses the shared <c>IApiClient</c>, so nothing else in the app depends on this feature.
/// </summary>
public static class TacticalMapModule
{
    public static IServiceCollection AddTacticalMap(this IServiceCollection services)
    {
        // No feature-local services yet; plans are loaded/saved through the shared IApiClient.
        return services;
    }
}
