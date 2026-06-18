using Microsoft.Extensions.DependencyInjection;
using Warhammer40k.Core;

namespace Warhammer40k._11;

/// <summary>
/// Single composition root for the app's services. Grows in later milestones
/// (repositories, view-model services, validation engine wiring).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWarhammer40k(this IServiceCollection services)
    {
        services.AddScoped<IApiClient, ApiClient>();
        services.AddScoped<SettingsState>();
        return services;
    }
}
