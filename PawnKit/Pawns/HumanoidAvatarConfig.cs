// -------------------------------------------------------------------------------------------------
// GamePawnKit
// File: PawnKit/Pawns/HumanoidAvatarConfig.cs
// Purpose: Generic avatar/animation configuration for HumanoidPawn.
// -------------------------------------------------------------------------------------------------
using Godot;

namespace GamePawnKit.Pawns;

/// <summary>
/// Optional avatar/animation configuration for a humanoid pawn.
/// This is intentionally asset-agnostic: it references a PackedScene path and animation clip names.
/// </summary>
public sealed class HumanoidAvatarConfig
{
	/// <summary>
	/// Path to a PackedScene imported from a .glb/.gltf (or .tscn) that contains the skinned character
	/// and the idle/walk/run animations.
	/// </summary>
	public string ModelScenePath { get; set; } = "";

	// Animation names as they appear in AnimationPlayer. The loader resolves exact or contains matches.
	public string IdleAnim { get; set; } = "Idle";
	public string WalkAnim { get; set; } = "Walking";
	public string RunAnim { get; set; } = "Running";
	public string DeathAnim { get; set; } = "Death";

	public float ModelScale { get; set; } = 1.0f;
	public float ModelYawOffsetDegrees { get; set; } = 180.0f;
	public Vector3 ModelLocalOffset { get; set; } = Vector3.Zero;

	public static HumanoidAvatarConfig Default() => new();
}
