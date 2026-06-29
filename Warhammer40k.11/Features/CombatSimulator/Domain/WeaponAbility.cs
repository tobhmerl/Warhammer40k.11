namespace Warhammer40k._11.Features.CombatSimulator.Domain;

/// <summary>
/// A weapon's special ability, as a discriminated set. The engine matches on the concrete subtype. An
/// <see cref="UnknownAbility"/> preserves a token we couldn't classify so it is displayed, never silently
/// dropped. Part of the removable Combat Simulator feature — see <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public abstract record WeaponAbility
{
    /// <summary>Short label for the UI chip.</summary>
    public abstract string Label { get; }
}

/// <summary>Rapid Fire X: +X attacks when the target is within half range.</summary>
public sealed record RapidFire(int X) : WeaponAbility { public override string Label => $"Rapid Fire {X}"; }

/// <summary>Sustained Hits X: a critical hit generates X additional hits.</summary>
public sealed record SustainedHits(int X) : WeaponAbility { public override string Label => $"Sustained Hits {X}"; }

/// <summary>Anti-[keyword] Y+: a wound roll of Y+ against a matching target is a critical wound.</summary>
public sealed record Anti(string Keyword, int CritThreshold) : WeaponAbility { public override string Label => $"Anti-{Keyword} {CritThreshold}+"; }

/// <summary>Melta X: +X damage when the target is within half range.</summary>
public sealed record Melta(int X) : WeaponAbility { public override string Label => $"Melta {X}"; }

/// <summary>Blast: +1 attack per 5 models in the target unit.</summary>
public sealed record Blast : WeaponAbility { public override string Label => "Blast"; }

/// <summary>Lethal Hits: a critical hit automatically wounds.</summary>
public sealed record LethalHits : WeaponAbility { public override string Label => "Lethal Hits"; }

/// <summary>Devastating Wounds: a critical wound becomes mortal wounds (bypass saves).</summary>
public sealed record DevastatingWounds : WeaponAbility { public override string Label => "Devastating Wounds"; }

/// <summary>Twin-linked: re-roll the wound roll.</summary>
public sealed record TwinLinked : WeaponAbility { public override string Label => "Twin-linked"; }

public sealed record Assault : WeaponAbility { public override string Label => "Assault"; }
public sealed record Heavy : WeaponAbility { public override string Label => "Heavy"; }
public sealed record Pistol : WeaponAbility { public override string Label => "Pistol"; }
public sealed record Psychic : WeaponAbility { public override string Label => "Psychic"; }
public sealed record Hazardous : WeaponAbility { public override string Label => "Hazardous"; }
public sealed record IgnoresCover : WeaponAbility { public override string Label => "Ignores Cover"; }
public sealed record Precision : WeaponAbility { public override string Label => "Precision"; }
public sealed record Lance : WeaponAbility { public override string Label => "Lance"; }

/// <summary>Torrent: the weapon automatically hits (no hit roll).</summary>
public sealed record Torrent : WeaponAbility { public override string Label => "Torrent"; }

public sealed record OneShot : WeaponAbility { public override string Label => "One Shot"; }
public sealed record ExtraAttacks : WeaponAbility { public override string Label => "Extra Attacks"; }
public sealed record IndirectFire : WeaponAbility { public override string Label => "Indirect Fire"; }

/// <summary>A keyword token we couldn't classify — kept so it's displayed, never dropped.</summary>
public sealed record UnknownAbility(string Raw) : WeaponAbility { public override string Label => Raw; }
