namespace WastelandSurvivor.Core.Defs;

public sealed record AmmoDefinition : IHasId
{
	public string Id { get; init; } = "";
	public string DisplayName { get; init; } = "";

	public AmmoKind AmmoKind { get; init; } = AmmoKind.Ballistic;

	/// <summary>
	/// Mass per single unit/round (used for vehicle performance/handling).
	/// </summary>
	public float UnitMassKg { get; init; } = 0f;

	public float DamageMultiplier { get; init; } = 1.0f;
	public float TrackingStrength { get; init; } = 0.0f; // missiles
	public string ArmorPenetrationTag { get; init; } = ""; // future tag
}
