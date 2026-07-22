// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/DriverPawnConfigStore.cs
// Purpose: Arena gameplay/runtime support (3D world, pawns, VFX).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;
using GamePawnKit.Pawns;
using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Small JSON-backed config for the Phase 1 driver on-foot prototype.
/// Kept intentionally tiny so we can tune feel without touching code.
/// </summary>
public sealed class DriverPawnConfigStore : JsonConfigStore<DriverPawnConfig>
{
	public static DriverPawnConfigStore Instance { get; } = new();

	private DriverPawnConfigStore() : base("res://Data/Config/driver_pawn.json")
	{
	}

	protected override DriverPawnConfig CreateDefault() => DriverPawnConfig.Default();

	protected override DriverPawnConfig Normalize(DriverPawnConfig config)
	{
		config.ExitOffset ??= new Vec3();
		config.Avatar ??= DriverAvatarConfig.Default();
		config.Avatar.ModelLocalOffset ??= new Vec3();
		return config;
	}
}


public sealed class DriverPawnConfig
{
	public Vec3 ExitOffset { get; set; } = new() { X = -1.45f, Y = 0.0f, Z = 0.65f };
	public float EnterRadius { get; set; } = 2.5f;
	public float MoveSpeed { get; set; } = 7.0f;
	public float SprintMultiplier { get; set; } = 1.6f;
	public float Acceleration { get; set; } = 38.0f;
	public float Deceleration { get; set; } = 55.0f;

	public DriverAvatarConfig Avatar { get; set; } = DriverAvatarConfig.Default();

	public static DriverPawnConfig Default() => new();

	public Vector3 ExitOffsetVec3() => new(ExitOffset.X, ExitOffset.Y, ExitOffset.Z);
}

public sealed class DriverAvatarConfig
{
	/// <summary>
	/// Path to a PackedScene imported from a .glb/.gltf (or .tscn) that contains the skinned character
	/// and the idle/walk/run animations.
	///
	/// NOTE: This lives under Assets/ locally and is intentionally excluded from AI zips.
	/// </summary>
	public string ModelScenePath { get; set; } = "res://Assets/Characters/Driver/Mixamo/Driver.glb";

	// Animation names as they appear in AnimationPlayer. The loader resolves exact or contains matches.
	public string IdleAnim { get; set; } = "Idle";
	public string WalkAnim { get; set; } = "Walking";
	public string RunAnim { get; set; } = "Running";
	// Optional death animation (non-looping). If present, the driver will play this when killed on-foot.
	public string DeathAnim { get; set; } = "Death";

	public float ModelScale { get; set; } = 1.0f;
	// Mixamo exports are commonly oriented opposite of Godot's -Z forward.
	public float ModelYawOffsetDegrees { get; set; } = 180.0f;
	public Vec3 ModelLocalOffset { get; set; } = new() { X = 0.0f, Y = 0.0f, Z = 0.0f };

	public static DriverAvatarConfig Default() => new();

	public Vector3 ModelLocalOffsetVec3() => new(ModelLocalOffset.X, ModelLocalOffset.Y, ModelLocalOffset.Z);

	public HumanoidAvatarConfig ToHumanoidAvatarConfig()
	{
		return new HumanoidAvatarConfig
		{
			ModelScenePath = ModelScenePath,
			IdleAnim = IdleAnim,
			WalkAnim = WalkAnim,
			RunAnim = RunAnim,
			DeathAnim = DeathAnim,
			ModelScale = ModelScale,
			ModelYawOffsetDegrees = ModelYawOffsetDegrees,
			ModelLocalOffset = ModelLocalOffsetVec3(),
		};
	}
}

public sealed class Vec3
{
	public float X { get; set; }
	public float Y { get; set; }
	public float Z { get; set; }
}
