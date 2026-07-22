// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Game/Systems/DamageMatrix.cs
// Purpose: Pure ammo-tag vs armor-type damage-multiplier matrix (rock-paper-scissors combat layer).
// -------------------------------------------------------------------------------------------------
using System;
using WastelandSurvivor.Core.Defs;

namespace WastelandSurvivor.Game.Systems;

/// <summary>
/// Static lookup for the ammo-vs-armor rock-paper-scissors layer (master-spec "deep customization"
/// pillar: ammo choice should matter against specific opponents, not just raw DPS).
///
/// Intent of the v1 matrix:
/// <list type="bullet">
/// <item><b>Ball</b> (plain MG rounds) — the neutral baseline. Full damage against Steel and Reactive
/// (reactive blocks don't trigger on small-arms fire), slightly blunted by Composite layering.</item>
/// <item><b>AP</b> (kinetic penetrators, e.g. 20mm autocannon) — punches through homogeneous Steel
/// plate (1.25x) and layered Composite (1.1x), but Reactive armor defeats penetrators (0.8x).</item>
/// <item><b>HE</b> (blast warheads: rockets, missiles, mines) — Composite delaminates under blast
/// (1.25x), Steel sheds most of it (0.9x), and Reactive armor eats blasts outright (0.7x).</item>
/// </list>
/// Unknown or empty tags (oil, smoke, legacy defs) are neutral: 1.0x. Tag matching is
/// case-insensitive so def JSON stays forgiving.
/// </summary>
public static class DamageMatrix
{
	public const string TagBall = "Ball";
	public const string TagAp = "AP";
	public const string TagHe = "HE";

	/// <summary>
	/// Damage multiplier for <paramref name="penetrationTag"/> (an <see cref="AmmoDefinition.ArmorPenetrationTag"/>)
	/// striking a chassis with <paramref name="armor"/>. Unknown/empty tags return 1.0 (neutral).
	/// </summary>
	public static float GetMultiplier(string? penetrationTag, ArmorType armor)
	{
		if (string.IsNullOrWhiteSpace(penetrationTag))
			return 1.0f;

		if (penetrationTag.Equals(TagBall, StringComparison.OrdinalIgnoreCase))
		{
			return armor switch
			{
				ArmorType.Steel => 1.0f,
				// 0.70 (was 0.85): a 1-point-per-hit difference vanished into range-falloff noise,
				// so the counter-shopping layer never registered in play. -30% is felt.
				ArmorType.Composite => 0.70f,
				ArmorType.Reactive => 1.0f,
				_ => 1.0f
			};
		}

		if (penetrationTag.Equals(TagAp, StringComparison.OrdinalIgnoreCase))
		{
			return armor switch
			{
				ArmorType.Steel => 1.25f,
				ArmorType.Composite => 1.1f,
				ArmorType.Reactive => 0.8f, // reactive blocks defeat kinetic penetrators
				_ => 1.0f
			};
		}

		if (penetrationTag.Equals(TagHe, StringComparison.OrdinalIgnoreCase))
		{
			return armor switch
			{
				ArmorType.Steel => 0.9f,
				ArmorType.Composite => 1.25f,
				ArmorType.Reactive => 0.7f, // reactive armor eats blasts
				_ => 1.0f
			};
		}

		// Unknown future tag: neutral until the matrix learns about it.
		return 1.0f;
	}

	/// <summary>
	/// Applies the matrix to an integer damage roll: rounds to nearest, and never scales a real hit
	/// (original damage >= 1) below 1 so resistant armor still chips.
	/// </summary>
	public static int Scale(int damage, string? penetrationTag, ArmorType armor)
	{
		if (damage <= 0)
			return damage;

		var scaled = (int)MathF.Round(damage * GetMultiplier(penetrationTag, armor));
		return Math.Max(1, scaled);
	}
}
