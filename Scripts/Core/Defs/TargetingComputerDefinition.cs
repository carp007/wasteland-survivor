namespace WastelandSurvivor.Core.Defs;

public sealed record TargetingComputerDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";

    public int MaxActiveWeaponGroups { get; init; } = 1;
    public int AutoAimSlots { get; init; } = 0;
    public float LockRange { get; init; } = 50f;
}
