// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/DriverPawn.cs
// Purpose: Back-compat wrapper for the on-foot pawn.
// -------------------------------------------------------------------------------------------------
using GamePawnKit.Pawns;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Backwards-compatible wrapper for Wasteland Survivor.
///
/// The reusable implementation now lives in <see cref="HumanoidPawn"/> (GamePawnKit).
/// This wrapper exists so existing scenes that reference res://Scripts/Arena/DriverPawn.cs keep working.
/// </summary>
public partial class DriverPawn : HumanoidPawn
{
	private DriverAvatarConfig _driverAvatarCfg = DriverAvatarConfig.Default();

	/// <summary>
	/// Wasteland Survivor config type (JSON-friendly). Mapped into <see cref="HumanoidPawn.AvatarConfig"/>.
	/// </summary>
	public new DriverAvatarConfig AvatarConfig
	{
		get => _driverAvatarCfg;
		set
		{
			_driverAvatarCfg = value ?? DriverAvatarConfig.Default();
			base.AvatarConfig = _driverAvatarCfg.ToHumanoidAvatarConfig();
		}
	}

	public override void _Ready()
	{
		// Ensure the mapped avatar config is applied before base setup attempts to load it.
		base.AvatarConfig = _driverAvatarCfg.ToHumanoidAvatarConfig();
		base._Ready();

		// Preserve legacy group used by arena raycast detection.
		AddToGroup("driver_pawn");

		EnsureGroundMarker();
	}

	/// <summary>
	/// Hostile identity: bailed-out enemy drivers run a hot red ring instead of the player's warm
	/// gold (spec: killing a driver on foot takes the hull whole — the target needs to read as a
	/// target). Set BEFORE the pawn enters the tree.
	/// </summary>
	public bool Hostile { get; set; }

	/// <summary>
	/// On-foot readability at the fixed RTS altitude: the humanoid is a handful of pixels from
	/// 37m up, and the on-foot beats (tire swaps, wreck salvage, tow hookups) are exactly when
	/// the player must not lose their character. Soft shadow disc + warm gold ring, mirroring
	/// the vehicles' identity treatment.
	/// </summary>
	private void EnsureGroundMarker()
	{
		if (GetNodeOrNull<Node3D>("GroundMarker") != null)
			return;

		var marker = new Node3D { Name = "GroundMarker" };
		AddChild(marker);

		var shadow = new MeshInstance3D
		{
			Name = "Shadow",
			Mesh = new CylinderMesh { TopRadius = 0.40f, BottomRadius = 0.40f, Height = 0.012f, RadialSegments = 20 },
			Position = new Vector3(0f, 0.025f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		shadow.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
		{
			AlbedoColor = new Color(0f, 0f, 0f, 0.38f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		});
		marker.AddChild(shadow);

		var ringColor = Hostile ? new Color(1.0f, 0.28f, 0.16f, 0.60f) : new Color(1.0f, 0.84f, 0.25f, 0.55f);
		var ringEmission = Hostile ? new Color(1.0f, 0.22f, 0.10f) : new Color(1.0f, 0.78f, 0.20f);
		var ring = new MeshInstance3D
		{
			Name = "IdentityRing",
			Mesh = new TorusMesh { InnerRadius = 0.42f, OuterRadius = 0.50f, Rings = 24, RingSegments = 8 },
			Position = new Vector3(0f, 0.05f, 0f),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		ring.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
		{
			AlbedoColor = ringColor,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			EmissionEnabled = true,
			Emission = ringEmission,
			EmissionEnergyMultiplier = Hostile ? 1.1f : 0.9f,
		});
		marker.AddChild(ring);
	}
}
