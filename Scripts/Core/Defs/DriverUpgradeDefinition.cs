// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Defs/DriverUpgradeDefinition.cs
// Purpose: Data definition model loaded from JSON under Data/Defs (stable, Godot-independent).
// -------------------------------------------------------------------------------------------------
namespace WastelandSurvivor.Core.Defs;

/// <summary>
/// Category of a driver upgrade sold at the Clinic &amp; Outfitter.
/// </summary>
public enum DriverUpgradeKind
{
    /// <summary>One-time surgical install (clone-facility cities only). Raises max driver HP; stacks.</summary>
    Cybernetic,

    /// <summary>Wearable vest replacing the equipped driver armor (single slot, old vest trades in).</summary>
    Armor,
}

/// <summary>
/// Driver-side store item: cybernetic implants (permanent max-HP boosts, installed at clone
/// facilities — they cut you open and re-upload) and body armor vests (upgrade over basic kevlar).
/// Distinct from vehicle parts and from the legacy <see cref="ArmorDefinition"/> repair data.
/// </summary>
public sealed record DriverUpgradeDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";

    public DriverUpgradeKind Kind { get; init; } = DriverUpgradeKind.Cybernetic;

    /// <summary>Max driver HP added when installed (cybernetics only).</summary>
    public int HpBonus { get; init; } = 0;

    /// <summary>Armor points granted while worn (armor vests only). Replaces the current vest's pool.</summary>
    public int ArmorPoints { get; init; } = 0;

    public int PriceUsd { get; init; } = 0;

    /// <summary>Short fiction line shown under the stat line in the store.</summary>
    public string Flavor { get; init; } = "";
}
