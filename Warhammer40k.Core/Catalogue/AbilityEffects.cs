using System.Text.Json.Serialization;

namespace Warhammer40k.Core.Catalogue;

/// <summary>
/// Which weapons a conferred effect targets. Declared in the Catalogue namespace (not reusing
/// <c>Rosters.DetachmentWeaponClass</c>) so a <see cref="Datasheet"/> can carry conferred effects without
/// the Catalogue layer depending on the Rosters layer.
/// </summary>
public enum WeaponClass
{
    Any = 0,
    Ranged = 1,
    Melee = 2,
}

/// <summary>
/// The characteristic a numeric <see cref="StatModifier"/> adjusts. Covers the unit statline
/// (M/T/Sv/W/Ld/OC) and the per-weapon characteristics that can be improved on a battle card. A
/// <c>+1 to the Hit roll</c> buff targets <see cref="Skill"/> (the weapon's BS/WS), so e.g. a 4+ shows as 3+.
/// </summary>
public enum StatTarget
{
    // Unit statline
    Move = 0,
    Toughness = 1,
    Save = 2,
    Wounds = 3,
    Leadership = 4,
    ObjectiveControl = 5,

    // Weapon profile
    Attacks = 10,

    /// <summary>The weapon's Ballistic/Weapon Skill — i.e. the Hit roll target (a +1 buff improves 4+ to 3+).</summary>
    Skill = 11,
    Strength = 12,
    Damage = 13,

    /// <summary>The weapon's Range (e.g. <c>24"</c>); a positive delta adds inches.</summary>
    Range = 14,
}

/// <summary>
/// A single numeric buff applied directly to a characteristic instead of being shown as ability prose, e.g.
/// <c>+1 to Hit</c>. <see cref="WeaponClass"/> is only meaningful when <see cref="Target"/> is a weapon
/// characteristic (it scopes the buff to ranged or melee weapons).
/// </summary>
public sealed class StatModifier
{
    [JsonPropertyName("target")] public StatTarget Target { get; set; }

    /// <summary>The signed amount to change the characteristic by (e.g. <c>+1</c>).</summary>
    [JsonPropertyName("delta")] public int Delta { get; set; }

    /// <summary>
    /// An <b>absolute</b> value that overrides the characteristic entirely (e.g. <c>"3+"</c> Save, <c>"8\""</c>
    /// Move), used by self-affecting abilities that state a fixed characteristic ("has a Save characteristic of
    /// 3+"). When set, it wins over any <see cref="Delta"/> for the same target. <c>null</c> = delta mode.
    /// </summary>
    [JsonPropertyName("setValue")] public string? SetValue { get; set; }

    /// <summary>For weapon-characteristic targets, which weapons are affected. Ignored for unit-statline targets.</summary>
    [JsonPropertyName("weaponClass")] public WeaponClass WeaponClass { get; set; } = WeaponClass.Any;

    /// <summary>
    /// When set on an Enhancement's modifier, this single buff applies to <b>every model in the bearer's
    /// unit</b> even if the enhancement is otherwise bearer-only. Lets one enhancement mix scopes — e.g.
    /// Destroyer Ankh's <c>+2"</c> Move is unit-wide while its <c>+2</c> Attacks stays on the bearer.
    /// </summary>
    [JsonPropertyName("affectsWholeUnit")] public bool AffectsWholeUnit { get; set; }

    /// <summary>Optional override for the short display label; <see cref="Describe"/> computes a default when empty.</summary>
    [JsonPropertyName("label")] public string Label { get; set; } = "";

    /// <summary>True when this is an absolute set (vs. a signed delta).</summary>
    [JsonIgnore]
    public bool IsSet => SetValue is not null;

    /// <summary>True when this modifier targets a weapon characteristic (vs. the unit statline).</summary>
    [JsonIgnore]
    public bool IsWeaponStat => Target is StatTarget.Attacks or StatTarget.Skill or StatTarget.Strength or StatTarget.Damage or StatTarget.Range;

    /// <summary>A short human label such as <c>"+1 to Hit"</c>, <c>"+1 Move"</c>, or <c>"Save 3+"</c>.</summary>
    public string Describe()
    {
        if (!string.IsNullOrWhiteSpace(Label))
            return Label;
        if (SetValue is not null)
            return $"{Name(Target)} {SetValue}";
        var sign = Delta >= 0 ? "+" : "−";
        return $"{sign}{Math.Abs(Delta)} {Name(Target)}";
    }

    private static string Name(StatTarget target) => target switch
    {
        StatTarget.Move => "Move",
        StatTarget.Toughness => "Toughness",
        StatTarget.Save => "Save",
        StatTarget.Wounds => "Wounds",
        StatTarget.Leadership => "Leadership",
        StatTarget.ObjectiveControl => "OC",
        StatTarget.Attacks => "Attacks",
        StatTarget.Skill => "to Hit",
        StatTarget.Strength => "Strength",
        StatTarget.Damage => "Damage",
        StatTarget.Range => "Range",
        _ => target.ToString(),
    };
}

/// <summary>
/// The structured effect parsed from a single datasheet ability that confers a buff on the unit a model
/// leads (e.g. "While this model is leading a unit, melee weapons … have the [LETHAL HITS] ability."). Stored
/// on <see cref="Datasheet.LeaderConferrals"/>, derived once at load so Play Mode can apply it to the led
/// unit's card instead of showing the raw ability text.
/// </summary>
public sealed class ConferredEffect
{
    /// <summary>The name of the ability this was parsed from, so the card can mark it "Applied".</summary>
    [JsonPropertyName("sourceAbility")] public string SourceAbility { get; set; } = "";

    /// <summary>Which of the led unit's weapons the granted <see cref="WeaponAbilities"/> apply to.</summary>
    [JsonPropertyName("weaponClass")] public WeaponClass WeaponClass { get; set; } = WeaponClass.Any;

    /// <summary>Weapon abilities granted to the led unit's weapons, in catalogue spelling (e.g. "Lethal Hits").</summary>
    [JsonPropertyName("weaponAbilities")] public List<string> WeaponAbilities { get; set; } = [];

    /// <summary>Unit-wide abilities granted to the led unit (e.g. "Feel No Pain 5+").</summary>
    [JsonPropertyName("unitAbilities")] public List<string> UnitAbilities { get; set; } = [];

    /// <summary>Numeric characteristic buffs applied to the led unit / its weapons.</summary>
    [JsonPropertyName("statModifiers")] public List<StatModifier> StatModifiers { get; set; } = [];

    /// <summary>
    /// The improved Critical Hit threshold this effect confers on the scoped weapons (e.g. <c>5</c> for "scores a
    /// Critical Hit on an unmodified 5+"), or <c>0</c> when the ability does not change it. Scoped by
    /// <see cref="WeaponClass"/> just like the granted <see cref="WeaponAbilities"/>.
    /// </summary>
    [JsonPropertyName("criticalHitOn")] public int CriticalHitOn { get; set; }

    /// <summary>True when this effect grants nothing (used to drop no-op parses).</summary>
    [JsonIgnore]
    public bool IsEmpty => WeaponAbilities.Count == 0 && UnitAbilities.Count == 0 && StatModifiers.Count == 0 && CriticalHitOn == 0;

    /// <summary>A compact one-line summary for the "Applied: …" note, e.g. "LETHAL HITS on melee weapons".</summary>
    [JsonIgnore]
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (WeaponAbilities.Count > 0)
                parts.Add($"{string.Join(", ", WeaponAbilities)} on {WeaponScope()}");
            parts.AddRange(UnitAbilities);
            parts.AddRange(StatModifiers.Select(m => m.Describe()));
            if (CriticalHitOn > 0)
                parts.Add($"Critical Hit on {CriticalHitOn}+ for {WeaponScope()}");
            return string.Join("; ", parts);
        }
    }

    private string WeaponScope() => WeaponClass switch
    {
        WeaponClass.Ranged => "ranged weapons",
        WeaponClass.Melee => "melee weapons",
        _ => "weapons",
    };
}
