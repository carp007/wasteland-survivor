// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/SmokePuff3D.cs
// Purpose: Arena gameplay/runtime support (3D world, pawns, VFX).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Tiny, code-only "smoke" puff. Uses a semi-transparent unshaded cube that rises and fades.
/// This avoids textures/particle dependencies while still giving readable feedback.
/// </summary>
public partial class SmokePuff3D : Node3D
{
	private float _ttl = 0.75f;
	private float _age = 0f;
	private Vector3 _vel;
	private MeshInstance3D? _mesh;
	private StandardMaterial3D? _mat;

	public static void Spawn(Node3D parent, Vector3 atWorld)
	{
		var puff = new SmokePuff3D();
		parent.AddChild(puff);
		puff.GlobalPosition = atWorld;
	}

	public override void _Ready()
	{
		_vel = new Vector3(
			(float)(Random.Shared.NextDouble() * 0.6 - 0.3),
			0.55f + (float)(Random.Shared.NextDouble() * 0.35),
			(float)(Random.Shared.NextDouble() * 0.6 - 0.3));

		_mesh = new MeshInstance3D
		{
			Name = "Smoke",
			Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.18f, 0.18f) }
		};
		_mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(0.35f, 0.35f, 0.35f, 0.55f),
			NoDepthTest = false,
		};
		_mesh.SetSurfaceOverrideMaterial(0, _mat);
		AddChild(_mesh);
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_age += dt;
		GlobalPosition += _vel * dt;
		_vel *= MathF.Exp(-1.8f * dt);

		// Fade out.
		if (_mat != null)
		{
			var t = Mathf.Clamp(_age / _ttl, 0f, 1f);
			var a = Mathf.Lerp(0.55f, 0f, t);
			_mat.AlbedoColor = new Color(0.35f, 0.35f, 0.35f, a);
		}

		// Grow slightly.
		if (_mesh != null)
		{
			var s = 1f + _age * 0.9f;
			_mesh.Scale = new Vector3(s, s, s);
		}

		if (_age >= _ttl)
			QueueFree();
	}
}
