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
				// Cross-axis slimming is applied at the ROOT (its -Z is the aligned barrel axis, so
				// X/Y are always "across/above the barrel"), but the pawn overwrites the root
				// transform when snapping the mount point — so stash the factors in meta and let
				// ApplyCrossAxisScale() re-apply them after that snap.
				node.SetMeta("weapon_visual_width_scale", cfg.WidthScale > 0.05f ? cfg.WidthScale : 1.0f);
				node.SetMeta("weapon_visual_height_scale", cfg.HeightScale > 0.05f ? cfg.HeightScale : 1.0f);
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

	/// <summary>
	/// Re-applies the config's cross-axis (width/height) slimming to the weapon root. Call AFTER
	/// any code that assigns the root's Transform wholesale (mount-point snapping), which would
	/// otherwise wipe the scale back to identity.
	/// </summary>
	public static void ApplyCrossAxisScale(Node3D weaponRoot)
	{
		if (weaponRoot == null || !GodotObject.IsInstanceValid(weaponRoot)) return;
		var w = weaponRoot.HasMeta("weapon_visual_width_scale") ? weaponRoot.GetMeta("weapon_visual_width_scale").AsSingle() : 1.0f;
		var h = weaponRoot.HasMeta("weapon_visual_height_scale") ? weaponRoot.GetMeta("weapon_visual_height_scale").AsSingle() : 1.0f;
		if (MathF.Abs(w - 1f) < 0.001f && MathF.Abs(h - 1f) < 0.001f) return;
		weaponRoot.Scale = new Vector3(w, h, 1f);
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

			// The Blaster Kit ships in toy colormap colors (mint/orange/white) that read as plastic
			// toys bolted to the hull from the RTS camera. Repaint every surface into a hardware
			// palette (gunmetal with slight per-part variation) so mounted weapons read as weapons.
			RepaintAsHardware(model);

			root.SetMeta("weapon_id", weaponId);
			return root;
		}
		catch
		{
			return null;
		}
	}

	private static void RepaintAsHardware(Node3D model)
	{
		var stack = new Stack<Node>();
		stack.Push(model);
		while (stack.Count > 0)
		{
			var n = stack.Pop();
			foreach (var childObj in n.GetChildren())
			{
				if (childObj is Node child)
					stack.Push(child);
			}

			if (n is not MeshInstance3D mi || mi.Mesh == null) continue;
			var surfaces = mi.Mesh.GetSurfaceCount();
			for (var s = 0; s < surfaces; s++)
			{
				// Per-part shade variation (receiver vs barrel vs magazine) so the weapon
				// silhouette still articulates without the toy colors.
				var seed = (mi.Name.ToString().GetHashCode() ^ (s * 397)) & 0x7fffffff;
				var shade = 0.105f + (seed % 1000) / 1000f * 0.055f;
				mi.SetSurfaceOverrideMaterial(s, new StandardMaterial3D
				{
					AlbedoColor = new Color(shade, shade * 1.04f, shade * 1.12f),
					Roughness = 0.38f + (seed % 613) / 613f * 0.18f,
					Metallic = 0.72f,
				});
			}
		}
	}

	private static Node3D CreateBoxWeaponVisual(string mountId, string weaponId)
	{
		var root = new Node3D { Name = $"Weapon_{mountId}" };
		// Marker defining how the weapon attaches to a vehicle mount.
		var mount = new Marker3D { Name = "MountPoint" };
		root.AddChild(mount);

		// Weapon proxy: dark gunmetal receiver + barrel so it reads as hardware instead of a glued-on
		// white box from the gameplay camera.
		var gunmetal = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.12f, 0.13f, 0.15f),
			Roughness = 0.42f,
			Metallic = 0.78f
		};

		var receiver = new MeshInstance3D
		{
			Name = "Mesh",
			Mesh = new BoxMesh { Size = new Vector3(0.26f, 0.16f, 0.50f) },
			Position = new Vector3(0f, 0.10f, -0.12f)
		};
		receiver.SetSurfaceOverrideMaterial(0, gunmetal);
		root.AddChild(receiver);

		var barrel = new MeshInstance3D
		{
			Name = "Barrel",
			Mesh = new CylinderMesh { TopRadius = 0.040f, BottomRadius = 0.048f, Height = 0.58f, RadialSegments = 10 },
			Position = new Vector3(0f, 0.14f, -0.55f),
			RotationDegrees = new Vector3(-90f, 0f, 0f)
		};
		barrel.SetSurfaceOverrideMaterial(0, gunmetal);
		root.AddChild(barrel);

		var brake = new MeshInstance3D
		{
			Name = "MuzzleBrake",
			Mesh = new BoxMesh { Size = new Vector3(0.10f, 0.10f, 0.12f) },
			Position = new Vector3(0f, 0.14f, -0.80f)
		};
		brake.SetSurfaceOverrideMaterial(0, gunmetal);
		root.AddChild(brake);

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

			// mesh -> weaponRoot via accumulated LOCAL transforms. GlobalTransform/ToLocal need
			// the nodes inside the scene tree, but weapon visuals are assembled off-tree — the
			// old path spammed "get_global_transform: node not inside tree" into every arena log
			// (hundreds per run) while silently returning garbage-identity transforms.
			var toRoot = Transform3D.Identity;
			Node? cur = mi;
			var reached = false;
			while (cur != null)
			{
				if (cur == weaponRoot)
				{
					reached = true;
					break;
				}
				if (cur is Node3D n3)
					toRoot = n3.Transform * toRoot;
				cur = cur.GetParent();
			}
			if (!reached) continue; // not under the weapon root; skip rather than misplace

			var aabb = mi.GetAabb();
			foreach (var c in GetAabbCorners(aabb))
				pts.Add(toRoot * c);
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
		// Off-tree there is nothing to flush — ForceUpdateTransform() logs a native
		// "!is_inside_tree()" error per node, and global transforms are recomputed when the
		// node enters the tree anyway. Alignment math stays correct off-tree because it reads
		// accumulated LOCAL transforms (GatherMeshPointsInLocalSpace).
		if (!node.IsInsideTree())
			return;

		if (node is Node3D n3d)
			n3d.ForceUpdateTransform();

		foreach (var childObj in node.GetChildren())
		{
			if (childObj is Node child)
				ForceUpdateTransformsRecursive(child);
		}
	}
}

public sealed class WeaponVisualConfigStore : JsonConfigStore<WeaponVisualConfigRoot>
{
	private const string ConfigFilePath = "res://Data/Config/weapon_visuals.json";

	public static WeaponVisualConfigStore Instance { get; } = new();

	private WeaponVisualConfigStore() : base(ConfigFilePath)
	{
	}

	public WeaponVisualConfig? Get(string weaponId)
	{
		var root = base.Get();
		return root.Weapons.TryGetValue(weaponId, out var cfg) ? cfg : null;
	}

	protected override WeaponVisualConfigRoot CreateDefault() => new();

	protected override WeaponVisualConfigRoot Normalize(WeaponVisualConfigRoot config)
	{
		var normalized = new Dictionary<string, WeaponVisualConfig>(StringComparer.OrdinalIgnoreCase);
		if (config.Weapons != null)
		{
			foreach (var kv in config.Weapons)
			{
				if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null)
					continue;
				normalized[kv.Key] = kv.Value;
			}
		}

		config.Weapons = normalized;
		return config;
	}
}

public sealed class WeaponVisualConfigRoot
{
	public Dictionary<string, WeaponVisualConfig> Weapons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}


public sealed class WeaponVisualConfig
{
	public string? ScenePath { get; set; }
	public float Scale { get; set; } = 1.0f;
	public float DesiredLength { get; set; } = 0.0f;
	/// <summary>
	/// Cross-axis slimming applied at the weapon root (barrel axis keeps DesiredLength).
	/// The Blaster Kit models are chunky sci-fi props; real vehicle guns are long and THIN, so
	/// sub-1.0 values here are what make mounts read proportional on the toy-scale hulls.
	/// Applied on the root (post-alignment axes), so width is always across the barrel.
	/// </summary>
	public float WidthScale { get; set; } = 1.0f;
	public float HeightScale { get; set; } = 1.0f;
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
