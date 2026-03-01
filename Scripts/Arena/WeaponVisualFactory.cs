// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/WeaponVisualFactory.cs
// Purpose: Creates weapon visual instances (proxy or model) from config, including heuristic mount/muzzle alignment and per-weapon SFX.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Creates weapon visuals for mounted weapons.
/// - Default: simple proxy box.
/// - Optional: load a glTF/scene specified in Data/Config/weapon_visuals.json and auto-align it.
/// </summary>
public static class WeaponVisualFactory
{
	private const string DefaultConfigPath = "res://Data/Config/weapon_visuals.json";

	public static Node3D CreateWeaponVisual(string mountId, string weaponId)
	{
		var cfg = WeaponVisualConfigStore.Instance.Get(weaponId);
		if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ScenePath))
		{
			var node = TryCreateModelWeaponVisual(mountId, weaponId, cfg.ScenePath!);
			if (node != null)
			{
				// Persist settings for later alignment.
				node.SetMeta("weapon_visual_weapon_id", weaponId);
				node.SetMeta("weapon_visual_scene_path", cfg.ScenePath ?? "");
				node.SetMeta("weapon_visual_scale", cfg.Scale);
				node.SetMeta("weapon_visual_desired_length", cfg.DesiredLength);
				node.SetMeta("weapon_visual_auto_align_yaw", cfg.AutoAlignYaw);
				node.SetMeta("weapon_visual_debug_alignment", cfg.DebugAlignment);
				if (!string.IsNullOrWhiteSpace(cfg.MountPointNodeName)) node.SetMeta("weapon_visual_mount_node", cfg.MountPointNodeName!);
				if (!string.IsNullOrWhiteSpace(cfg.MuzzleNodeName)) node.SetMeta("weapon_visual_muzzle_node", cfg.MuzzleNodeName!);
				return node;
			}
		}

		// Fallback.
		return CreateBoxWeaponVisual(mountId, weaponId);
	}

	public static bool TryAutoAlignWeaponVisual(Node3D weaponRoot)
	{
		if (weaponRoot == null || !GodotObject.IsInstanceValid(weaponRoot)) return false;

		var scenePath = weaponRoot.HasMeta("weapon_visual_scene_path") ? weaponRoot.GetMeta("weapon_visual_scene_path").AsString() : "";
		if (string.IsNullOrWhiteSpace(scenePath)) return false;

		var scaleMul = weaponRoot.HasMeta("weapon_visual_scale") ? weaponRoot.GetMeta("weapon_visual_scale").AsSingle() : 1.0f;
		var desiredLength = weaponRoot.HasMeta("weapon_visual_desired_length") ? weaponRoot.GetMeta("weapon_visual_desired_length").AsSingle() : 0.0f;
		var autoYaw = weaponRoot.HasMeta("weapon_visual_auto_align_yaw") && weaponRoot.GetMeta("weapon_visual_auto_align_yaw").AsBool();
		var debug = weaponRoot.HasMeta("weapon_visual_debug_alignment") && weaponRoot.GetMeta("weapon_visual_debug_alignment").AsBool();
		var mountNodeHint = weaponRoot.HasMeta("weapon_visual_mount_node") ? weaponRoot.GetMeta("weapon_visual_mount_node").AsString() : null;
		var muzzleNodeHint = weaponRoot.HasMeta("weapon_visual_muzzle_node") ? weaponRoot.GetMeta("weapon_visual_muzzle_node").AsString() : null;

		var visual = weaponRoot.GetNodeOrNull<Node3D>("Visual");
		if (visual == null) return false;

		// Model is the first Node3D under Visual.
		var model = visual.GetChildren().OfType<Node3D>().FirstOrDefault();
		if (model == null) return false;

		var muzzle = weaponRoot.GetNodeOrNull<Marker3D>("Muzzle");
		if (muzzle == null) return false;

		// Scale-to-length (optional). This normalizes different weapon models to a baseline size.
		if (desiredLength > 0.01f)
		{
			var pts0 = GatherMeshPointsInLocalSpace(weaponRoot, model);
			if (pts0.Count > 0)
			{
				var bounds0 = ComputeBounds(pts0);
				var size0 = bounds0.Size;
				var curLen = MathF.Max(size0.X, size0.Z);
				if (curLen > 0.0001f)
				{
					var factor = desiredLength / curLen;
					visual.Scale *= new Vector3(factor, factor, factor);
					ForceUpdateTransformsRecursive(weaponRoot);
				}
			}
		}

		// Scale multiplier (optional, per-weapon). Applied AFTER normalization so it can be used
		// as a simple tuning knob without being cancelled out by DesiredLength.
		if (scaleMul > 0.0001f && MathF.Abs(scaleMul - 1.0f) > 0.0001f)
		{
			visual.Scale *= new Vector3(scaleMul, scaleMul, scaleMul);
			ForceUpdateTransformsRecursive(weaponRoot);
		}

		// Compute mount point: prefer explicit node hints/names; fallback to bounds center.
		var mountPointLocal = Vector3.Zero;
		{
			Node3D? mountNode = null;
			if (!string.IsNullOrWhiteSpace(mountNodeHint))
				mountNode = FindNodeByName(model, mountNodeHint!);

			// Heuristic search (strict): only accept explicit "MountPoint" style names (not generic "mount_*").
			mountNode ??= FindFirstMatchingNode(model, new[]
			{
				"MountPoint", "Mount_Point", "mountpoint", "mount_point",
				"AttachPoint", "Attach_Point", "attachpoint", "attach_point",
				"Socket", "socket"
			});

			if (mountNode != null)
			{
				mountPointLocal = weaponRoot.ToLocal(mountNode.GlobalPosition);
			}
			else
			{
				var pts = GatherMeshPointsInLocalSpace(weaponRoot, model);
				if (pts.Count > 0)
				{
					var bounds = ComputeBounds(pts);
					mountPointLocal = bounds.Position + bounds.Size * 0.5f;
				}
			}

			// Shift visual so mount point is at origin.
			visual.Position -= mountPointLocal;
			ForceUpdateTransformsRecursive(weaponRoot);
		}

		// Determine yaw (optional).
		if (autoYaw)
		{
			// Prefer explicit muzzle node if present (lets artists add a helper node for barrel direction).
			Node3D? muzzleNode = null;
			if (!string.IsNullOrWhiteSpace(muzzleNodeHint))
				muzzleNode = FindNodeByName(model, muzzleNodeHint!);

			muzzleNode ??= FindFirstMatchingNode(model, new[] { "Muzzle", "MuzzlePoint", "Muzzle_Point", "muzzle", "muzzlepoint", "BarrelEnd", "Barrel_End" });

			Vector3? forwardHint = null;
			if (muzzleNode != null)
			{
				var muzzlePos = weaponRoot.ToLocal(muzzleNode.GlobalPosition);
				var v = muzzlePos; // mount is at origin after shift
				v.Y = 0f;
				if (v.Length() > 0.001f)
					forwardHint = v.Normalized();
			}

			var fwd = forwardHint ?? ComputeForwardAxisFromPca(weaponRoot, model);
			fwd.Y = 0f;
			if (fwd.Length() > 0.001f)
			{
				fwd = fwd.Normalized();
				// Rotate so "forward" points along -Z (Godot forward for basis is -Z).
				var yaw = Mathf.Atan2(-fwd.X, -fwd.Z);
				visual.Rotation = new Vector3(0f, yaw, 0f);
				ForceUpdateTransformsRecursive(weaponRoot);

				if (debug)
					GD.Print($"[WeaponVisual] yawAlign fwd=({fwd.X:0.###},{fwd.Z:0.###}) yawDeg={Mathf.RadToDeg(yaw):0.##} weapon={weaponRoot.Name}");
			}
		}

		// Set muzzle marker:
		{
			// Prefer explicit muzzle node if present after transforms.
			Node3D? muzzleNode = null;
			if (!string.IsNullOrWhiteSpace(muzzleNodeHint))
				muzzleNode = FindNodeByName(model, muzzleNodeHint!);

			muzzleNode ??= FindFirstMatchingNode(model, new[] { "Muzzle", "MuzzlePoint", "Muzzle_Point", "muzzle", "muzzlepoint", "BarrelEnd", "Barrel_End" });

			Vector3 muzzlePosLocal;
			if (muzzleNode != null)
			{
				muzzlePosLocal = weaponRoot.ToLocal(muzzleNode.GlobalPosition);
			}
			else
			{
				var pts = GatherMeshPointsInLocalSpace(weaponRoot, model);
				if (pts.Count == 0)
					muzzlePosLocal = new Vector3(0f, 0.15f, -0.82f); // fallback similar to proxy
				else
				{
					var minZ = pts.OrderBy(p => p.Z).First();
					muzzlePosLocal = minZ;
				}
			}

			// Push slightly forward so muzzle is outside mesh.
			muzzle.Position = muzzlePosLocal + new Vector3(0f, 0f, -0.04f);
			muzzle.Rotation = Vector3.Zero;
		}

		return true;
	}

	private static Node3D? TryCreateModelWeaponVisual(string mountId, string weaponId, string scenePath)
	{
		try
		{
			var ps = GD.Load<PackedScene>(scenePath);
			if (ps == null) return null;

			var root = new Node3D { Name = $"Weapon_{mountId}" };

			// Mount marker at origin (we align the model to this).
			root.AddChild(new Marker3D { Name = "MountPoint" });

			// Muzzle marker (filled in by alignment).
			root.AddChild(new Marker3D { Name = "Muzzle", Position = new Vector3(0f, 0.15f, -0.82f) });

			var visual = new Node3D { Name = "Visual" };
			root.AddChild(visual);

			var inst = ps.Instantiate();
			if (inst is not Node3D model)
			{
				inst.QueueFree();
				return null;
			}
			model.Name = "Model";
			visual.AddChild(model);

			root.SetMeta("weapon_id", weaponId);
			return root;
		}
		catch
		{
			return null;
		}
	}

	private static Node3D CreateBoxWeaponVisual(string mountId, string weaponId)
	{
		var root = new Node3D { Name = $"Weapon_{mountId}" };
		// Marker defining how the weapon attaches to a vehicle mount.
		var mount = new Marker3D { Name = "MountPoint" };
		root.AddChild(mount);

		// Weapon mesh (proxy).
		var mesh = new MeshInstance3D
		{
			Name = "Mesh",
			Mesh = new BoxMesh { Size = new Vector3(0.38f, 0.20f, 0.85f) },
			Position = new Vector3(0f, 0.12f, -0.35f)
		};
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.55f, 0.55f, 0.60f),
			Roughness = 0.55f,
			Metallic = 0.25f
		};
		mesh.SetSurfaceOverrideMaterial(0, mat);
		root.AddChild(mesh);

		// Muzzle marker (used for tracer origin + aim direction).
		var muzzle = new Marker3D { Name = "Muzzle", Position = new Vector3(0f, 0.15f, -0.82f) };
		root.AddChild(muzzle);

		// Label node for debugging (in case we want to show weapon id in editor).
		root.SetMeta("weapon_id", weaponId);
		return root;
	}

	private static Node3D? FindNodeByName(Node root, string name)
	{
		if (root == null) return null;
		if (string.Equals(root.Name, name, StringComparison.OrdinalIgnoreCase) && root is Node3D n3d)
			return n3d;

		foreach (var childObj in root.GetChildren())
		{
			if (childObj is Node child)
			{
				var found = FindNodeByName(child, name);
				if (found != null) return found;
			}
		}
		return null;
	}

	private static Node3D? FindFirstMatchingNode(Node root, IEnumerable<string> names)
	{
		var set = new HashSet<string>(names.Select(n => n.ToLowerInvariant()));
		return FindFirstMatchingNodeInternal(root, set);
	}

	private static Node3D? FindFirstMatchingNodeInternal(Node root, HashSet<string> namesLower)
	{
		if (root is Node3D n3d)
		{
			var nm = (n3d.Name ?? "").ToString().ToLowerInvariant();
			if (namesLower.Contains(nm))
				return n3d;
		}
		foreach (var childObj in root.GetChildren())
		{
			if (childObj is Node child)
			{
				var found = FindFirstMatchingNodeInternal(child, namesLower);
				if (found != null) return found;
			}
		}
		return null;
	}

	private static List<Vector3> GatherMeshPointsInLocalSpace(Node3D weaponRoot, Node3D modelRoot)
	{
		var meshes = new List<MeshInstance3D>();
		CollectMeshes(modelRoot, meshes);

		var pts = new List<Vector3>(meshes.Count * 8);
		foreach (var mi in meshes)
		{
			if (mi.Mesh == null) continue;
			var aabb = mi.GetAabb();
			foreach (var c in GetAabbCorners(aabb))
			{
				var gp = mi.GlobalTransform * c;
				var lp = weaponRoot.ToLocal(gp);
				pts.Add(lp);
			}
		}
		return pts;
	}

	private static void CollectMeshes(Node node, List<MeshInstance3D> list)
	{
		if (node is MeshInstance3D mi)
			list.Add(mi);
		foreach (var childObj in node.GetChildren())
		{
			if (childObj is Node child)
				CollectMeshes(child, list);
		}
	}

	private static IEnumerable<Vector3> GetAabbCorners(Aabb aabb)
	{
		var p = aabb.Position;
		var s = aabb.Size;
		yield return p;
		yield return p + new Vector3(s.X, 0, 0);
		yield return p + new Vector3(0, s.Y, 0);
		yield return p + new Vector3(0, 0, s.Z);
		yield return p + new Vector3(s.X, s.Y, 0);
		yield return p + new Vector3(s.X, 0, s.Z);
		yield return p + new Vector3(0, s.Y, s.Z);
		yield return p + s;
	}

	private static Aabb ComputeBounds(List<Vector3> pts)
	{
		var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
		var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
		foreach (var p in pts)
		{
			min.X = MathF.Min(min.X, p.X);
			min.Y = MathF.Min(min.Y, p.Y);
			min.Z = MathF.Min(min.Z, p.Z);
			max.X = MathF.Max(max.X, p.X);
			max.Y = MathF.Max(max.Y, p.Y);
			max.Z = MathF.Max(max.Z, p.Z);
		}
		return new Aabb(min, max - min);
	}

	private static Vector3 ComputeForwardAxisFromPca(Node3D weaponRoot, Node3D modelRoot)
	{
		var pts = GatherMeshPointsInLocalSpace(weaponRoot, modelRoot);
		if (pts.Count < 4)
			return Vector3.Forward;

		// Compute mean (XZ only).
		double meanX = 0, meanZ = 0;
		foreach (var p in pts)
		{
			meanX += p.X;
			meanZ += p.Z;
		}
		meanX /= pts.Count;
		meanZ /= pts.Count;

		// Covariance matrix.
		double xx = 0, xz = 0, zz = 0;
		foreach (var p in pts)
		{
			var dx = p.X - meanX;
			var dz = p.Z - meanZ;
			xx += dx * dx;
			xz += dx * dz;
			zz += dz * dz;
		}
		xx /= pts.Count;
		xz /= pts.Count;
		zz /= pts.Count;

		// Principal eigenvector (2x2).
		var a = (float)xx;
		var b = (float)xz;
		var c = (float)zz;

		float vx, vz;
		if (MathF.Abs(b) > 1e-6f)
		{
			var tr = a + c;
			var det = a * c - b * b;
			var disc = MathF.Max(0f, tr * tr - 4f * det);
			var lambdaMax = 0.5f * (tr + MathF.Sqrt(disc));
			vx = b;
			vz = lambdaMax - a;
		}
		else
		{
			// No correlation; pick the axis with larger variance.
			if (a >= c) { vx = 1f; vz = 0f; }
			else { vx = 0f; vz = 1f; }
		}

		var axis = new Vector2(vx, vz);
		if (axis.Length() < 1e-5f) axis = new Vector2(0f, -1f);
		axis = axis.Normalized();

		// Pick sign: choose the end further from the origin (mount point is at origin after shift).
		float maxProj = float.NegativeInfinity;
		float minProj = float.PositiveInfinity;
		foreach (var p in pts)
		{
			var proj = axis.X * p.X + axis.Y * p.Z;
			maxProj = MathF.Max(maxProj, proj);
			minProj = MathF.Min(minProj, proj);
		}
		var useMax = MathF.Abs(maxProj) >= MathF.Abs(minProj);
		var sign = useMax ? MathF.Sign(maxProj) : MathF.Sign(minProj); // MathF.Sign(float) returns int
		if (sign == 0) sign = 1;

		var dir = new Vector3(axis.X * sign, 0f, axis.Y * sign);
		if (dir.Length() < 1e-5f) dir = Vector3.Forward;
		return dir.Normalized();
	}

	private static void ForceUpdateTransformsRecursive(Node node)
	{
		if (node is Node3D n3d)
			n3d.ForceUpdateTransform();

		foreach (var childObj in node.GetChildren())
		{
			if (childObj is Node child)
				ForceUpdateTransformsRecursive(child);
		}
	}
}

public sealed class WeaponVisualConfigStore
{
	public static WeaponVisualConfigStore Instance { get; } = new();

	private bool _loaded;
	private Dictionary<string, WeaponVisualConfig> _byWeaponId = new(StringComparer.OrdinalIgnoreCase);

	public string ConfigPath { get; set; } = "res://Data/Config/weapon_visuals.json";

	public WeaponVisualConfig? Get(string weaponId)
	{
		EnsureLoaded();
		return _byWeaponId.TryGetValue(weaponId, out var cfg) ? cfg : null;
	}

	private void EnsureLoaded()
	{
		if (_loaded) return;
		_loaded = true;

		try
		{
			if (!FileAccess.FileExists(ConfigPath))
				return;

			var json = FileAccess.GetFileAsString(ConfigPath);
			var root = JsonUtil.Deserialize<WeaponVisualConfigRoot>(json);
			if (root?.Weapons == null) return;

			_byWeaponId = new Dictionary<string, WeaponVisualConfig>(root.Weapons, StringComparer.OrdinalIgnoreCase);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[WeaponVisualConfigStore] Failed to load {ConfigPath}: {ex.Message}");
		}
	}

	private sealed class WeaponVisualConfigRoot
	{
		public Dictionary<string, WeaponVisualConfig> Weapons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	}
}

public sealed class WeaponVisualConfig
{
	public string? ScenePath { get; set; }
	public float Scale { get; set; } = 1.0f;
	public float DesiredLength { get; set; } = 0.0f;
	public bool AutoAlignYaw { get; set; } = true;
	public bool DebugAlignment { get; set; } = false;
	public string? MountPointNodeName { get; set; }
	public string? MuzzleNodeName { get; set; }

	// Weapon audio (optional). These are used by arena firing logic.
	public string? FireSoundPath { get; set; }
	public float FireVolumeDb { get; set; } = -4.0f;
	public string? HitVehicleSoundPath { get; set; }
	public float HitVehicleVolumeDb { get; set; } = -3.0f;
	public string? HitWorldSoundPath { get; set; }
	public float HitWorldVolumeDb { get; set; } = -4.0f;
}
