using System.Reflection;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Api;

/// <summary>
/// Loads the embedded <c>necron-catalogue-seed.json</c> once and caches the enriched
/// <see cref="CatalogueData"/>. Registered as a singleton so the seed is parsed and derived
/// (copy caps, Warlord eligibility, Leader targets, …) exactly once per worker process.
/// </summary>
public sealed class CatalogueProvider
{
    private readonly Lazy<CatalogueData> _catalogue = new(LoadEmbedded);

    /// <summary>The enriched catalogue, materialized on first access and cached thereafter.</summary>
    public CatalogueData Catalogue => _catalogue.Value;

    /// <summary>
    /// Reads and enriches the catalogue straight from the embedded seed resource. Exposed as a
    /// static so tests can assert against the real seed without standing up the function host.
    /// </summary>
    public static CatalogueData LoadEmbedded()
    {
        var assembly = typeof(CatalogueProvider).Assembly;
        using var stream = OpenSeedStream(assembly);
        return CatalogueSeedLoader.Load(stream);
    }

    private static Stream OpenSeedStream(Assembly assembly)
    {
        // Resolve by suffix so a change to the project's root namespace / folder can't silently break loading.
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("necron-catalogue-seed.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "Embedded catalogue seed 'necron-catalogue-seed.json' was not found. " +
                "Ensure it is included as an <EmbeddedResource> in Warhammer40k.Api.csproj.");

        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded catalogue seed resource '{resourceName}' could not be opened.");
    }
}
