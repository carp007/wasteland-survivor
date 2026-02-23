using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Minimal/cheap arena VFX (no textures, no particle dependencies).
/// We keep this code-only so early builds are robust and compile-safe.
/// </summary>
public static class ArenaVfx
{
	public static void SpawnShot(
		ArenaWorld world,
		Vector3 muzzleWorld,
		Vector3 endWorld,
		bool fromPlayer,
		bool hit)
	{
		var root = world.GetVfxRoot();
		var tracerColor = fromPlayer
			? new Color(1.0f, 0.92f, 0.35f) // warm yellow
			: new Color(1.0f, 0.45f, 0.45f); // red-ish

		// Tracer is the most readable from the fixed top-down camera.
		// Use an ImmediateMesh line to avoid transform/orientation surprises.
		SpawnLine(root, muzzleWorld, endWorld, tracerColor, ttlSeconds: 0.06f);
		SpawnFlash(root, muzzleWorld, fromPlayer ? new Color(1.0f, 0.95f, 0.55f) : new Color(1.0f, 0.6f, 0.6f), radius: 0.18f, ttlSeconds: 0.05f);

		if (hit)
			SpawnFlash(root, endWorld, new Color(1.0f, 0.95f, 0.75f), radius: 0.22f, ttlSeconds: 0.10f);
	}

	public static void SpawnSparks(ArenaWorld world, Vector3 atWorld, int count = 3)
	{
		var root = world.GetVfxRoot();
		count = Math.Max(1, Math.Min(20, count));
		for (var i = 0; i < count; i++)
		{
			var jitter = new Vector3(
				(float)(Random.Shared.NextDouble() * 0.20 - 0.10),
				(float)(Random.Shared.NextDouble() * 0.05),
				(float)(Random.Shared.NextDouble() * 0.20 - 0.10));
			SpawnFlash(root, atWorld + jitter, new Color(1.0f, 0.85f, 0.35f), radius: 0.08f, ttlSeconds: 0.07f);
		}
	}

	public static void SpawnSkidMark(ArenaWorld world, Vector3 atWorld, float yawRad, float ttlSeconds = 8.0f)
	{
		var root = world.GetVfxRoot();
		var mesh = new MeshInstance3D
		{
			Name = "SkidMark",
			Mesh = new BoxMesh { Size = new Vector3(0.26f, 0.01f, 0.70f) },
			GlobalPosition = atWorld,
			Rotation = new Vector3(0f, yawRad, 0f)
		};
		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(0.04f, 0.04f, 0.04f, 0.45f),
			Roughness = 1.0f
		};
		mesh.SetSurfaceOverrideMaterial(0, mat);
		root.AddChild(mesh);
		AutoFree(root, mesh, Math.Max(0.25f, ttlSeconds));
	}

	private static void SpawnLine(Node3D parent, Vector3 fromWorld, Vector3 toWorld, Color color, float ttlSeconds)
	{
		var len = fromWorld.DistanceTo(toWorld);
		if (len < 0.05f) return;

		// Build the line in parent-local space so we don't have to manage transforms.
		var localFrom = parent.ToLocal(fromWorld);
		var localTo = parent.ToLocal(toWorld);

		var mat = MakeUnshaded(color, noDepthTest: true);
		var im = new ImmediateMesh();
		im.SurfaceBegin(Mesh.PrimitiveType.Lines, mat);
		im.SurfaceAddVertex(localFrom);
		im.SurfaceAddVertex(localTo);
		im.SurfaceEnd();

		var inst = new MeshInstance3D
		{
			Name = "TracerLine",
			Mesh = im
		};
		parent.AddChild(inst);
		AutoFree(parent, inst, ttlSeconds);
	}

	private static void SpawnFlash(Node3D parent, Vector3 atWorld, Color color, float radius, float ttlSeconds)
	{
		var size = Math.Max(0.01f, radius) * 2f;
		var mesh = new MeshInstance3D
		{
			Name = "Flash",
			Mesh = new BoxMesh { Size = new Vector3(size, size, size) },
			GlobalPosition = atWorld
		};
		mesh.SetSurfaceOverrideMaterial(0, MakeUnshaded(color));
		parent.AddChild(mesh);
		AutoFree(parent, mesh, ttlSeconds);
	}

	private static Material MakeUnshaded(Color color, bool noDepthTest = false)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = noDepthTest,
		};
		return mat;
	}

	private static void AutoFree(Node parent, Node node, float ttlSeconds)
	{
		try
		{
			var tree = parent.GetTree();
			if (tree == null)
			{
				node.QueueFree();
				return;
			}
			var t = tree.CreateTimer(Math.Max(0.01f, ttlSeconds));
			t.Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(node))
					node.QueueFree();
			};
		}
		catch
		{
			// Never let VFX timing break gameplay.
			if (GodotObject.IsInstanceValid(node))
				node.QueueFree();
		}
	}
}
