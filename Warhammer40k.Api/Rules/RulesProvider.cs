using System.Reflection;
using System.Text.Json;
using Warhammer40k.Core.RulesAssistant;

namespace Warhammer40k.Api.Rules;

/// <summary>
/// Loads the embedded <c>core-rules.json</c> corpus once and caches it. Mirrors
/// <see cref="CatalogueProvider"/>. Part of the removable Rules Assistant feature
/// (see docs/rules-assistant-REMOVE.md) — deleting this file and its callers reverts it cleanly.
/// </summary>
public sealed class RulesProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lazy<IReadOnlyList<RuleCard>> _rules = new(LoadEmbedded);

    /// <summary>The Core-Rules corpus, materialized on first access and cached thereafter.</summary>
    public IReadOnlyList<RuleCard> Rules => _rules.Value;

    /// <summary>
    /// Reads the rules corpus from the embedded resource. Accepts either a bare array of rule cards or a
    /// wrapper object with a <c>rules</c> array (plus optional meta/categories/table_of_contents). Returns an
    /// empty list when the corpus is the placeholder <c>[]</c> or malformed, so the assistant degrades to an
    /// empty-state rather than throwing.
    /// </summary>
    public static IReadOnlyList<RuleCard> LoadEmbedded()
    {
        try
        {
            var assembly = typeof(RulesProvider).Assembly;
            using var stream = OpenStream(assembly);
            if (stream is null)
                return [];

            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            // Wrapper object: { "rules": [ … ] }. Otherwise treat the root as the array itself.
            JsonElement array = root;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (!root.TryGetProperty("rules", out array) || array.ValueKind != JsonValueKind.Array)
                    return [];
            }
            else if (root.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return array.Deserialize<List<RuleCard>>(JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Stream? OpenStream(Assembly assembly)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("core-rules.json", StringComparison.OrdinalIgnoreCase));
        return resourceName is null ? null : assembly.GetManifestResourceStream(resourceName);
    }
}
