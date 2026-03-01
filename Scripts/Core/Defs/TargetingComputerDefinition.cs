// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Core/Defs/TargetingComputerDefinition.cs
// Purpose: Data definition model loaded from JSON under Data/Defs (stable, Godot-independent).
// -------------------------------------------------------------------------------------------------
namespace WastelandSurvivor.Core.Defs;

/// <summary>
/// Definition for a targeting computer (loaded from Data/Defs/Computers). Governs how many weapon groups can be active and (later) auto-aim behavior.
/// </summary>
public sealed record TargetingComputerDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";

    public int MaxActiveWeaponGroups { get; init; } = 1;
    public int AutoAimSlots { get; init; } = 0;
    public float LockRange { get; init; } = 50f;
}
