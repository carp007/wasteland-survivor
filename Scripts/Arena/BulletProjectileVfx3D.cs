// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/BulletProjectileVfx3D.cs
// Purpose: Arena gameplay/runtime support (3D world, pawns, VFX).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Simple visible "bullet" VFX that travels from muzzle to impact position.
/// This is intentionally lightweight (no textures/particles) and is used for
/// slow ROF projectile weapons like the 50 cal.
/// </summary>
public partial class BulletProjectileVfx3D : Node3D
{
	private MeshInstance3D? _mesh;

	public static float Spawn(
		Node3D parent,
		Vector3 fromWorld,
		Vector3 toWorld,
		float speed,
		Color color,
		Action? onArrive = null)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent)) return 0f;
		var dist = fromWorld.DistanceTo(toWorld);
		var spd = MathF.Max(1f, speed);
		var travel = Mathf.Clamp(dist / spd, 0.04f, 0.40f);

		var node = new BulletProjectileVfx3D { Name = "BulletVfx" };
		parent.AddChild(node);
		node.GlobalPosition = fromWorld;
		node.BuildMesh(color, fromWorld, toWorld);

		// Tween to impact. This avoids having to keep a per-frame _Process.
		try
		{
			var tree = parent.GetTree();
			if (tree == null)
			{
				node.QueueFree();
				onArrive?.Invoke();
				return travel;
			}
			var tween = tree.CreateTween();
			tween.TweenProperty(node, "global_position", toWorld, travel)
				.SetTrans(Tween.TransitionType.Linear)
				.SetEase(Tween.EaseType.InOut);
			tween.TweenCallback(Callable.From(() =>
			{
				try { onArrive?.Invoke(); } catch { /* never break gameplay */ }
				if (GodotObject.IsInstanceValid(node)) node.QueueFree();
			}));
		}
		catch
		{
			if (GodotObject.IsInstanceValid(node)) node.QueueFree();
			try { onArrive?.Invoke(); } catch { }
		}

		return travel;
	}

	private void BuildMesh(Color color, Vector3 fromWorld, Vector3 toWorld)
	{
		// A small capsule reads better than a point/sphere under a top-down camera.
		var capsule = new CapsuleMesh
		{
			Radius = 0.035f,
			Height = 0.14f,
			RadialSegments = 10,
			Rings = 2
		};

		_mesh = new MeshInstance3D
		{
			Name = "Mesh",
			Mesh = capsule
		};

		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = color,
			NoDepthTest = true
		};
		_mesh.SetSurfaceOverrideMaterial(0, mat);
		AddChild(_mesh);

		// Orient along travel direction.
		var dir = (toWorld - fromWorld);
		dir.Y = 0f;
		if (dir.Length() < 0.001f) dir = Vector3.Forward;
		dir = dir.Normalized();
		// Capsule axis is Y. Rotate so Y points along dir in XZ.
		var yaw = Mathf.Atan2(dir.X, dir.Z);
		Rotation = new Vector3(Mathf.Pi * 0.5f, yaw, 0f);
	}
}
