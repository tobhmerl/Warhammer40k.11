using System.Text.Json.Serialization;

namespace Warhammer40k.Core.RulesAssistant;

/// <summary>
/// A single Core-Rules entry, mapped 1:1 from the embedded <c>core-rules.json</c> (the assistant's only
/// source of truth). The text is shown <b>verbatim</b> — the assistant never paraphrases or invents rules.
/// </summary>
/// <remarks>
/// This whole feature (the <c>RulesAssistant</c> namespace, the API function, and the Play-Mode panel) is a
/// self-contained module that can be deleted without touching the rest of the app — see
/// <c>docs/rules-assistant-REMOVE.md</c>.
/// </remarks>
public sealed class RuleCard
{
    /// <summary>Stable rule reference, e.g. <c>"11.02"</c>.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    /// <summary>Section number, e.g. <c>"11"</c>.</summary>
    [JsonPropertyName("section")] public string Section { get; set; } = "";

    [JsonPropertyName("section_title")] public string SectionTitle { get; set; } = "";

    /// <summary>Category tag, e.g. <c>"Movement"</c>, <c>"Weapon ability"</c>.</summary>
    [JsonPropertyName("category")] public string Category { get; set; } = "";

    [JsonPropertyName("title")] public string Title { get; set; } = "";

    /// <summary>Rulebook page number (used in citations).</summary>
    [JsonPropertyName("page")] public int Page { get; set; }

    /// <summary>The rule text, shown verbatim.</summary>
    [JsonPropertyName("text")] public string Text { get; set; } = "";

    /// <summary>Ids of related rules (e.g. <c>"03.04"</c>) surfaced alongside a match.</summary>
    [JsonPropertyName("references")] public List<string> References { get; set; } = [];

    /// <summary>Search keywords / weapon-ability tags (e.g. <c>"[BLAST]"</c>).</summary>
    [JsonPropertyName("keywords")] public List<string> Keywords { get; set; } = [];
}
