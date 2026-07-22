// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ProjectileTrailParticle3D.cs
// Purpose: Reusable lightweight projectile trail particle for code-only VFX.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Reusable, code-only trail particle for projectiles. Designed so future ammo types can reuse the
/// same scaffolding with different colors / velocities without requiring texture assets.
/// </summary>
public partial class ProjectileTrailParticle3D : Node3D
{
	private float _ttl = 0.34f;
	private float _age;
	private Vector3 _vel;
	private MeshInstance3D? _mesh;
	private StandardMaterial3D? _mat;
	private Color _baseColor;

	public static void Spawn(Node3D parent, Vector3 atWorld, Vector3 projectileDir, Color projectileColor, float ttlSeconds = 0.34f)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
			return;

		var node = new ProjectileTrailParticle3D
		{
			Name = "ProjectileTrailParticle",
			_baseColor = projectileColor,
			_ttl = Mathf.Clamp(ttlSeconds, 0.08f, 1.0f)
		};
		parent.AddChild(node);
		node.GlobalPosition = atWorld;

		var backwards = projectileDir.Length() > 0.001f ? -projectileDir.Normalized() : Vector3.Back;
		var jitter = new Vector3(
			(float)(Random.Shared.NextDouble() * 0.22 - 0.11),
			(float)(Random.Shared.NextDouble() * 0.10 + 0.02),
			(float)(Random.Shared.NextDouble() * 0.22 - 0.11));
		node._vel = backwards * (1.8f + (float)(Random.Shared.NextDouble() * 0.9f)) + jitter;
	}

	public override void _Ready()
	{
		_mesh = new MeshInstance3D
		{
			Name = "Mesh",
			Mesh = new BoxMesh { Size = new Vector3(0.10f, 0.10f, 0.10f) }
		};
		_mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(1.0f, 0.82f, 0.28f, 0.70f),
			EmissionEnabled = true,
			Emission = new Color(_baseColor.R, MathF.Max(0.35f, _baseColor.G), MathF.Max(0.12f, _baseColor.B * 0.45f)),
			EmissionEnergyMultiplier = 0.42f,
			NoDepthTest = true
		};
		_mesh.SetSurfaceOverrideMaterial(0, _mat);
		AddChild(_mesh);
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_age += dt;
		GlobalPosition += _vel * dt;
		_vel *= MathF.Exp(-3.2f * dt);

		if (_mesh != null)
		{
			var s = 1f + _age * 1.6f;
			_mesh.Scale = new Vector3(s, s, s);
		}

		if (_mat != null)
		{
			var t = Mathf.Clamp(_age / _ttl, 0f, 1f);
			var hot = 1f - t;
			_mat.AlbedoColor = new Color(
				Mathf.Lerp(1.0f, 0.32f, t),
				Mathf.Lerp(MathF.Max(0.55f, _baseColor.G), 0.32f, t),
				Mathf.Lerp(MathF.Max(0.18f, _baseColor.B * 0.30f), 0.32f, t),
				Mathf.Lerp(0.70f, 0f, t));
			_mat.EmissionEnergyMultiplier = 0.15f + 0.35f * hot;
		}

		if (_age >= _ttl)
			QueueFree();
	}
}
