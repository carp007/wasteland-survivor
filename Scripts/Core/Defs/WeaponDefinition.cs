namespace WastelandSurvivor.Core.Defs;

public sealed record WeaponDefinition : IHasId
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";

    public WeaponType WeaponType { get; init; } = WeaponType.MG;
    public FireMode FireMode { get; init; } = FireMode.DumbFire;

    public float ProjectileSpeed { get; init; } = 0f; // 0 for hitscan (MG V1)
    public float BaseDamage { get; init; } = 5f;
    public float? SplashRadius { get; init; } = null;

    public int CooldownMs { get; init; } = 200;

    /// <summary>
    /// Approximate mass of the weapon (used for vehicle performance/handling).
    /// </summary>
    public float MassKg { get; init; } = 0f;

    public string[] AmmoTypeIds { get; init; } = System.Array.Empty<string>();
}
