// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ProjectileImpactVfx3D.cs
// Purpose: Lightweight code-only missile explosion visual.
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Short-lived projectile impact / explosion effect with flash, shockwave, sparks, and smoke.
/// Kept code-only so it survives zip iterations without texture/asset dependencies.
/// </summary>
public partial class ProjectileImpactVfx3D : Node3D
{
	private float _ttl = 0.42f;
	private float _age;
	private float _radius = 1.8f;
	private Color _tint = new(1f, 0.65f, 0.22f, 1f);
	private MeshInstance3D? _flash;
	private MeshInstance3D? _shockwave;
	private StandardMaterial3D? _flashMat;
	private StandardMaterial3D? _shockwaveMat;

	public static void Spawn(Node3D parent, Vector3 atWorld, Color tint, float radius)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
			return;

		var node = new ProjectileImpactVfx3D
		{
			Name = "ProjectileImpactVfx",
			_tint = tint,
			_radius = Mathf.Clamp(radius, 0.8f, 5.0f)
		};
		parent.AddChild(node);
		node.GlobalPosition = atWorld;
	}

	public override void _Ready()
	{
		_flash = new MeshInstance3D
		{
			Name = "Flash",
			Mesh = new SphereMesh
			{
				Radius = 0.18f,
				Height = 0.36f,
				RadialSegments = 16,
				Rings = 8
			}
		};
		_flashMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(1.0f, 0.82f, 0.36f, 0.90f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.62f, 0.20f),
			EmissionEnergyMultiplier = 1.25f,
			NoDepthTest = true
		};
		_flash.SetSurfaceOverrideMaterial(0, _flashMat);
		AddChild(_flash);

		_shockwave = new MeshInstance3D
		{
			Name = "Shockwave",
			Mesh = new CylinderMesh
			{
				TopRadius = 0.30f,
				BottomRadius = 0.30f,
				Height = 0.04f,
				RadialSegments = 28,
				Rings = 1
			},
			Position = new Vector3(0f, 0.02f, 0f)
		};
		_shockwaveMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(_tint.R, _tint.G * 0.86f, _tint.B * 0.72f, 0.34f),
			EmissionEnabled = true,
			Emission = new Color(_tint.R, _tint.G * 0.68f, _tint.B * 0.48f),
			EmissionEnergyMultiplier = 0.28f,
			NoDepthTest = true
		};
		_shockwave.SetSurfaceOverrideMaterial(0, _shockwaveMat);
		AddChild(_shockwave);

		var smokeCount = Math.Max(4, (int)MathF.Round(_radius * 3.0f));
		var parent = GetParent() as Node3D;
		if (parent != null)
		{
			for (var i = 0; i < smokeCount; i++)
			{
				var angle = (MathF.Tau * i) / smokeCount;
				var dist = 0.10f + (float)Random.Shared.NextDouble() * (_radius * 0.35f);
				var jitter = new Vector3(
					MathF.Cos(angle) * dist,
					(float)(Random.Shared.NextDouble() * 0.05),
					MathF.Sin(angle) * dist);
				SmokePuff3D.Spawn(parent, GlobalPosition + jitter + Vector3.Up * 0.04f);
			}
		}
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_age += dt;
		var t = Mathf.Clamp(_age / _ttl, 0f, 1f);

		if (_flash != null)
		{
			var s = Mathf.Lerp(0.55f, _radius * 1.25f, t);
			_flash.Scale = new Vector3(s, s, s);
		}
		if (_flashMat != null)
		{
			var alpha = Mathf.Lerp(0.92f, 0f, t);
			_flashMat.AlbedoColor = new Color(1.0f, Mathf.Lerp(0.86f, 0.42f, t), Mathf.Lerp(0.36f, 0.18f, t), alpha);
			_flashMat.EmissionEnergyMultiplier = Mathf.Lerp(1.25f, 0.08f, t);
		}

		if (_shockwave != null)
		{
			var ringScale = Mathf.Lerp(0.25f, _radius * 2.2f, t);
			_shockwave.Scale = new Vector3(ringScale, 1f, ringScale);
		}
		if (_shockwaveMat != null)
		{
			_shockwaveMat.AlbedoColor = new Color(_tint.R, _tint.G * 0.82f, _tint.B * 0.72f, Mathf.Lerp(0.32f, 0f, t));
			_shockwaveMat.EmissionEnergyMultiplier = Mathf.Lerp(0.32f, 0f, t);
		}

		if (_age >= _ttl)
			QueueFree();
	}
}
