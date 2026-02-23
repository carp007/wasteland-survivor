namespace WastelandSurvivor.Core.Defs;

/// <summary>
/// Player/driver wearable armor. Distinct from vehicle armor plating.
/// For now it is a single equipped slot that provides Armor Points (AP) as an HP buffer.
/// </summary>
public sealed record ArmorDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Maximum armor points granted by this armor.
    /// </summary>
    public int MaxArmorPoints { get; init; } = 0;

    /// <summary>
    /// Money cost per armor point to repair this armor back toward full.
    /// </summary>
    public int RepairCostPerPointUsd { get; init; } = 0;
}
