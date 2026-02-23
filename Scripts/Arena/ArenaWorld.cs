using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Arena world container (floor + walls + camera rig + actor roots).
/// Gameplay is driven by the UI view; this node is just the 3D world graph.
///
/// NOTE: This class avoids relying on its own _Ready having run before callers
/// access child nodes, because the UI may spawn and immediately interact with it.
/// </summary>
public partial class ArenaWorld : Node3D
{
	public Node3D ActorsRoot => GetNode<Node3D>("Actors");
	public Node3D ObstaclesRoot => GetNode<Node3D>("Obstacles");
	public FollowCameraRig CameraRig => GetNode<FollowCameraRig>("CameraRig");

	public Node3D GetVfxRoot()
	{
		var vfx = GetNodeOrNull<Node3D>("Vfx");
		if (vfx != null) return vfx;
		vfx = new Node3D { Name = "Vfx" };
		AddChild(vfx);
		return vfx;
	}

	public override void _Ready()
	{
		EnsureGeometry();
		// Safe even if called multiple times.
		EnsureObstacles();
		EnsureCamera();
		GetVfxRoot();
	}

	/// <summary>
	/// Ensure the arena has basic visible geometry (floor + bounds).
	/// We build this procedurally so the .tscn can remain minimal/robust.
	/// </summary>
	public void EnsureGeometry()
	{
		if (GetNodeOrNull<Node3D>("Geometry") != null) return;

		var geom = new Node3D { Name = "Geometry" };
		AddChild(geom);

		// Floor
		SpawnStaticBox(geom, "Floor", new Vector3(0, -0.1f, 0), new Vector3(120, 0.2f, 120),
			new Color(0.18f, 0.18f, 0.18f));

		// Bounds (simple walls)
		var bounds = new Node3D { Name = "Bounds" };
		geom.AddChild(bounds);
		SpawnStaticBox(bounds, "WallN", new Vector3(0, 1.2f, -55), new Vector3(120, 3, 2), new Color(0.12f, 0.12f, 0.12f));
		SpawnStaticBox(bounds, "WallS", new Vector3(0, 1.2f, 55), new Vector3(120, 3, 2), new Color(0.12f, 0.12f, 0.12f));
		SpawnStaticBox(bounds, "WallW", new Vector3(-55, 1.2f, 0), new Vector3(2, 3, 120), new Color(0.12f, 0.12f, 0.12f));
		SpawnStaticBox(bounds, "WallE", new Vector3(55, 1.2f, 0), new Vector3(2, 3, 120), new Color(0.12f, 0.12f, 0.12f));
	}

	private static void SpawnStaticBox(Node3D parent, string name, Vector3 pos, Vector3 size, Color color)
	{
		var body = new StaticBody3D { Name = name, Position = pos };

		var shape = new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = size }
		};
		body.AddChild(shape);

		var mesh = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size }
		};
		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 1.0f
		};
		mesh.SetSurfaceOverrideMaterial(0, mat);
		body.AddChild(mesh);

		parent.AddChild(body);
	}

	/// <summary>
	/// Make the arena camera current and ensure the follow rig is processing.
	/// This is safe to call immediately after instantiation (before _Ready).
	/// </summary>
	public void EnsureCamera()
	{
		var rig = GetNodeOrNull<FollowCameraRig>("CameraRig");
		if (rig != null) rig.SetProcess(true);
		var cam = GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
		if (cam != null) cam.Current = true;
	}

	public void SetCameraTarget(Node3D? target)
	{
		var rig = GetNodeOrNull<FollowCameraRig>("CameraRig");
		rig?.SetTarget(target);
	}

	public void EnsureObstacles()
	{
		var obstacles = GetNodeOrNull<Node3D>("Obstacles");
		if (obstacles == null) return;
		// Spawn once; fixed layout.
		if (obstacles.GetChildCount() > 0) return;

		SpawnObstacle(obstacles, new Vector3(-10, 0.5f, -8), new Vector3(6, 1, 2));
		SpawnObstacle(obstacles, new Vector3(12, 0.5f, -2), new Vector3(3, 1, 8));
		SpawnObstacle(obstacles, new Vector3(0, 0.5f, 10), new Vector3(8, 1, 2));
	}

	private static void SpawnObstacle(Node3D parent, Vector3 pos, Vector3 size)
	{
		var body = new StaticBody3D { Position = pos };

		var shape = new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = size }
		};
		body.AddChild(shape);

		var mesh = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size }
		};
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.25f, 0.25f, 0.25f),
			Roughness = 1.0f
		};
		mesh.SetSurfaceOverrideMaterial(0, mat);
		body.AddChild(mesh);

		parent.AddChild(body);
	}
}
