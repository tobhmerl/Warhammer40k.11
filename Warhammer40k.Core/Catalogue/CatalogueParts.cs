using System.Text.Json.Serialization;

namespace Warhammer40k.Core.Catalogue;

/// <summary>A model's statline (kept as strings to preserve game notation like <c>8"</c>, <c>2+</c>, <c>D6+3</c>).</summary>
public sealed class StatProfile
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("m")] public string Move { get; set; } = "";
    [JsonPropertyName("t")] public string Toughness { get; set; } = "";
    [JsonPropertyName("sv")] public string Save { get; set; } = "";
    [JsonPropertyName("w")] public string Wounds { get; set; } = "";
    [JsonPropertyName("ld")] public string Leadership { get; set; } = "";
    [JsonPropertyName("oc")] public string ObjectiveControl { get; set; } = "";
}

/// <summary>A single weapon profile (a <c>➤</c>-prefixed name denotes a sub-profile of a multi-mode weapon).</summary>
public sealed class WeaponProfile
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("range")] public string Range { get; set; } = "";
    [JsonPropertyName("attacks")] public string Attacks { get; set; } = "";
    [JsonPropertyName("skill")] public string Skill { get; set; } = "";
    [JsonPropertyName("strength")] public string Strength { get; set; } = "";
    [JsonPropertyName("ap")] public string ArmourPenetration { get; set; } = "";
    [JsonPropertyName("damage")] public string Damage { get; set; } = "";
    [JsonPropertyName("keywords")] public List<string> Keywords { get; set; } = [];
}

/// <summary>A named datasheet ability. Leader targets and "cannot be your Warlord" are parsed from <see cref="Text"/>.</summary>
public sealed class Ability
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
}

/// <summary>A legal unit size and its cost. Cost depends only on model count (wargear is free).</summary>
public sealed class PointsOption
{
    [JsonPropertyName("models")] public int Models { get; set; }
    [JsonPropertyName("points")] public int Points { get; set; }
}

/// <summary>
/// An authored wargear option-group: a player chooses between <see cref="Min"/> and <see cref="Max"/> of its
/// <see cref="Options"/> (rule R8). Authored in the Catalogue editor (AB7); empty-but-present until then (§12).
/// Wargear never costs points beyond model count (§3), so options carry a forward-compat <c>PointDelta = 0</c>.
/// </summary>
public sealed class WargearGroup
{
    /// <summary>Stable id used by <see cref="Warhammer40k.Core.Rosters.WargearSelection.GroupId"/>.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>Minimum options that must be selected (0 = optional group).</summary>
    [JsonPropertyName("min")] public int Min { get; set; }

    /// <summary>Maximum options that may be selected.</summary>
    [JsonPropertyName("max")] public int Max { get; set; } = 1;

    [JsonPropertyName("options")] public List<WargearOption> Options { get; set; } = [];
}

/// <summary>A single selectable wargear option within a <see cref="WargearGroup"/>.</summary>
public sealed class WargearOption
{
    /// <summary>Stable id referenced by a roster unit's selection.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>Points delta — always 0 today (wargear is free, §3); kept for forward-compatibility.</summary>
    [JsonPropertyName("pointDelta")] public int PointDelta { get; set; }
}

/// <summary>A Pantheon of Woe Necrodermal Binding: a points surcharge applied to a specific Monster datasheet (rule R10).</summary>
public sealed class PantheonBinding
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>The exact datasheet name this binding applies to.</summary>
    [JsonPropertyName("unit")] public string Unit { get; set; } = "";

    [JsonPropertyName("points")] public int Points { get; set; }
}
