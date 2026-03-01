// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ArenaWorld.cs
// Purpose: Procedural arena world builder (floor/walls/obstacles) plus shared camera rig/roots for actors and VFX.
// -------------------------------------------------------------------------------------------------
using System;
using System.IO;
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
	private const string ConcreteTexturePath = "res://Assets/Images/Textures/Ground/concrete_1.png";
	private const string MetalTexturePath = "res://Assets/Images/Textures/Metal/metal_1.png";

	// Poly Haven (PBR) wall set. The user should unzip rock_wall_16_4k.blend.zip into:
	//   Assets/Images/Textures/Ground/PolyHaven/rock_wall_16_4k/
	// so the following res:// paths exist.
	private const string RockWallRoot = "res://Assets/Images/Textures/Ground/PolyHaven/rock_wall_16_4k";
	private const string RockWallAlbedoPath = RockWallRoot + "/textures/rock_wall_16_diff_4k.jpg";
	// Poly Haven often ships normal/roughness as EXR in the "blend" pack. Godot's EXR loader may not support
	// every compression type used by those files, so we prefer PNG if it exists.
	private const string RockWallNormalPathPng = RockWallRoot + "/textures/rock_wall_16_nor_gl_4k.png";
	private const string RockWallNormalPathExr = RockWallRoot + "/textures/rock_wall_16_nor_gl_4k.exr";
	private const string RockWallRoughnessPathPng = RockWallRoot + "/textures/rock_wall_16_rough_4k.png";
	private const string RockWallRoughnessPathExr = RockWallRoot + "/textures/rock_wall_16_rough_4k.exr";

	// Poly Haven (PBR) floor set. The user should unzip clean_asphalt_4k.blend.zip into:
	//   Assets/Images/Textures/Ground/PolyHaven/clean_asphalt_4k/
	// so the following res:// paths exist.
	private const string AsphaltRoot = "res://Assets/Images/Textures/Ground/PolyHaven/clean_asphalt_4k";
	private const string AsphaltAlbedoPath = AsphaltRoot + "/textures/clean_asphalt_diff_4k.jpg";
	private const string AsphaltNormalPathPng = AsphaltRoot + "/textures/clean_asphalt_nor_gl_4k.png";
	private const string AsphaltNormalPathExr = AsphaltRoot + "/textures/clean_asphalt_nor_gl_4k.exr";
	private const string AsphaltRoughnessPathPng = AsphaltRoot + "/textures/clean_asphalt_rough_4k.png";
	private const string AsphaltRoughnessPathExr = AsphaltRoot + "/textures/clean_asphalt_rough_4k.exr";


	// Prefer brick walls if present (user feedback), otherwise fall back to rock wall.
	private const string WallPolyHavenDir = "res://Assets/Images/Textures/Ground/PolyHaven";

	private sealed class PbrSet
	{
		public string AlbedoPath = "";
		public string NormalPath = "";
		public string RoughnessPath = "";
		public float TileMeters = 4.5f;
	}

	private static PbrSet? CachedWallSet;

	private static PbrSet GetPreferredWallSet()
	{
		if (CachedWallSet != null) return CachedWallSet;

		// User request: outer arena walls should use the PolyHaven rock_wall_16_4k pack located under Ground/PolyHaven.
		if (ResourceLoader.Exists(RockWallAlbedoPath))
		{
			CachedWallSet = new PbrSet
			{
				AlbedoPath = RockWallAlbedoPath,
				NormalPath = PickFirstExisting(RockWallNormalPathPng, RockWallNormalPathExr) ?? RockWallNormalPathExr,
				RoughnessPath = PickFirstExisting(RockWallRoughnessPathPng, RockWallRoughnessPathExr) ?? RockWallRoughnessPathExr,
				TileMeters = RockWallTileMeters
			};
			return CachedWallSet;
		}

		// Final fallback: metal albedo triplanar (no PBR).
		CachedWallSet = new PbrSet
		{
			AlbedoPath = MetalTexturePath,
			NormalPath = "",
			RoughnessPath = "",
			TileMeters = MetalTileMeters
		};
		return CachedWallSet;
	}

	private static PbrSet? TryDetectPolyHavenPbrSet(string rootDir, string keyword, float defaultTileMeters)
	{
		try
		{
			var dir = DirAccess.Open(rootDir);
			if (dir == null) return null;
			dir.ListDirBegin();
			while (true)
			{
				var name = dir.GetNext();
				if (string.IsNullOrEmpty(name)) break;
				if (name == "." || name == "..") continue;
				if (!dir.CurrentIsDir()) continue;
				if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;

				var texDir = $"{rootDir}/{name}/textures";
				var tex = DirAccess.Open(texDir);
				if (tex == null) continue;
				tex.ListDirBegin();
				string? diffFile = null;
				while (true)
				{
					var f = tex.GetNext();
					if (string.IsNullOrEmpty(f)) break;
					if (tex.CurrentIsDir()) continue;
					// Prefer JPG/PNG albedo files with the PolyHaven naming convention.
					if (f.Contains("_diff_", StringComparison.OrdinalIgnoreCase) && (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
					{
						diffFile = f;
						break;
					}
				}
				tex.ListDirEnd();
				if (diffFile == null) continue;

				var idx = diffFile.IndexOf("_diff_", StringComparison.OrdinalIgnoreCase);
				if (idx <= 0) continue;
				var baseName = diffFile.Substring(0, idx);
				var suffix = diffFile.Substring(idx + "_diff_".Length);
				var dot = suffix.LastIndexOf('.');
				if (dot > 0) suffix = suffix.Substring(0, dot);

				var albedo = $"{texDir}/{diffFile}";
				if (!ResourceLoader.Exists(albedo)) continue;

				// Normal + roughness are optional; we try PNG first, then EXR.
				var normalPng = $"{texDir}/{baseName}_nor_gl_{suffix}.png";
				var normalExr = $"{texDir}/{baseName}_nor_gl_{suffix}.exr";
				var roughPng = $"{texDir}/{baseName}_rough_{suffix}.png";
				var roughExr = $"{texDir}/{baseName}_rough_{suffix}.exr";

				return new PbrSet
				{
					AlbedoPath = albedo,
					NormalPath = PickFirstExisting(normalPng, normalExr) ?? normalExr,
					RoughnessPath = PickFirstExisting(roughPng, roughExr) ?? roughExr,
					TileMeters = defaultTileMeters
				};
			}
			dir.ListDirEnd();
		}
		catch
		{
			// Ignore; we'll fall back.
		}
		return null;
	}

	private const string ArenaFloorShaderPath = "res://Shaders/arena_floor.gdshader";
	private const string ArenaTriplanarShaderPath = "res://Shaders/arena_triplanar.gdshader";

	// Textures imported into the project sometimes have mipmaps disabled (common when adding files by hand).
	// In 3D this causes severe aliasing / "static" at distance. We generate mipmaps at runtime for these
	// arena procedural materials so the floor/walls look like the source texture.
	private static readonly System.Collections.Generic.Dictionary<string, Texture2D> RuntimeTextureCache = new();
	private static readonly System.Collections.Generic.HashSet<string> ExrWarnOnce = new();

	// Triplanar tile density is expressed as "meters per texture repeat".
	// Concrete (obstacles) should feel relatively detailed.
	private const float ConcreteTileMeters = 6.0f;
	// Metal (arena bounds walls) tends to look better with larger repeats.
	private const float MetalTileMeters = 6.0f;
	private const float RockWallTileMeters = 4.5f;
	// Floor should read like concrete at gameplay zoom. Too-large tiling makes the (subtle) source texture
	// average out to a nearly flat gray once mipmapped.
	private const float FloorTileMeters = 6.0f;
	private const float AsphaltTileMeters = 6.0f;

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
		SpawnArenaFloor(geom, "Floor", new Vector3(0, -0.1f, 0), new Vector3(120, 0.2f, 120),
			fallbackColor: new Color(0.18f, 0.18f, 0.18f));

		// Bounds (simple walls)
		var bounds = new Node3D { Name = "Bounds" };
		var wallSet = GetPreferredWallSet();
		geom.AddChild(bounds);
		// Prefer PolyHaven rock wall PBR. If absent, fall back to the older metal albedo.
		SpawnPbrTexturedBox(bounds, "WallN", new Vector3(0, 1.2f, -55), new Vector3(120, 3, 2),
			albedoPath: wallSet.AlbedoPath,
			normalPath: wallSet.NormalPath,
			roughnessPath: wallSet.RoughnessPath,
			tileMeters: wallSet.TileMeters,
			fallbackAlbedoPath: MetalTexturePath,
			fallbackTileMeters: MetalTileMeters);
		SpawnPbrTexturedBox(bounds, "WallS", new Vector3(0, 1.2f, 55), new Vector3(120, 3, 2),
			albedoPath: wallSet.AlbedoPath,
			normalPath: wallSet.NormalPath,
			roughnessPath: wallSet.RoughnessPath,
			tileMeters: wallSet.TileMeters,
			fallbackAlbedoPath: MetalTexturePath,
			fallbackTileMeters: MetalTileMeters);
		SpawnPbrTexturedBox(bounds, "WallW", new Vector3(-55, 1.2f, 0), new Vector3(2, 3, 120),
			albedoPath: wallSet.AlbedoPath,
			normalPath: wallSet.NormalPath,
			roughnessPath: wallSet.RoughnessPath,
			tileMeters: wallSet.TileMeters,
			fallbackAlbedoPath: MetalTexturePath,
			fallbackTileMeters: MetalTileMeters);
		SpawnPbrTexturedBox(bounds, "WallE", new Vector3(55, 1.2f, 0), new Vector3(2, 3, 120),
			albedoPath: wallSet.AlbedoPath,
			normalPath: wallSet.NormalPath,
			roughnessPath: wallSet.RoughnessPath,
			tileMeters: wallSet.TileMeters,
			fallbackAlbedoPath: MetalTexturePath,
			fallbackTileMeters: MetalTileMeters);
	}

	private static void SpawnArenaFloor(
		Node3D parent,
		string name,
		Vector3 pos,
		Vector3 collisionSize,
		Color fallbackColor)
	{
		var body = new StaticBody3D { Name = name, Position = pos };

		var shape = new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = collisionSize }
		};
		body.AddChild(shape);

		// Visual plane at the top surface of the collision box.
		var plane = new MeshInstance3D
		{
			Name = "FloorVisual",
			Mesh = new PlaneMesh { Size = new Vector2(collisionSize.X, collisionSize.Z) },
			Position = new Vector3(0f, collisionSize.Y * 0.5f, 0f)
		};

		// Prefer a real PBR floor set (Poly Haven asphalt). If it's not present, fall back to the legacy
		// concrete albedo with a world-space shader.
		var mat = CreateAsphaltPbrFloorMaterial(collisionSize, fallbackColor)
			?? CreateArenaFloorMaterial(ConcreteTexturePath, FloorTileMeters, fallbackColor);

		plane.SetSurfaceOverrideMaterial(0, mat);
		body.AddChild(plane);
		parent.AddChild(body);
	}

	private static Material? CreateAsphaltPbrFloorMaterial(Vector3 collisionSize, Color fallback)
	{
		// If the user hasn't unzipped the pack to the expected Assets path yet, keep the floor usable.
		if (!ResourceLoader.Exists(AsphaltAlbedoPath))
		{
			GD.Print($"[ArenaWorld] Asphalt pack not found (expected: {AsphaltAlbedoPath}). Using fallback floor material.");
			return null;
		}

		var albedo = LoadTextureFor3D(AsphaltAlbedoPath);
		if (albedo == null)
			return null;

		var normalPath = PickFirstExisting(AsphaltNormalPathPng, AsphaltNormalPathExr);
		var roughPath = PickFirstExisting(AsphaltRoughnessPathPng, AsphaltRoughnessPathExr);
		var normal = normalPath != null ? LoadTextureFor3D(normalPath) : null;
		var rough = roughPath != null ? LoadTextureFor3D(roughPath) : null;

		var mat = new StandardMaterial3D
		{
			AlbedoColor = Colors.White,
			AlbedoTexture = albedo,
			Roughness = 1.0f,
			Metallic = 0.0f,
		};

		// Ensure tiling regardless of import settings.
		mat.Set("texture_repeat", 1); // Enabled
		mat.Set("texture_filter", 5); // Linear mipmap anisotropic

		// PlaneMesh UVs go 0..1 across the mesh, so scale UVs to get "meters per tile".
		var uScale = Mathf.Max(0.01f, collisionSize.X / AsphaltTileMeters);
		var vScale = Mathf.Max(0.01f, collisionSize.Z / AsphaltTileMeters);
		mat.Set("uv1_scale", new Vector3(uScale, vScale, 1.0f));

		if (normal != null)
		{
			mat.Set("normal_enabled", true);
			mat.Set("normal_texture", normal);
			mat.Set("normal_scale", 0.85f);
		}
		if (rough != null)
		{
			mat.Set("roughness_texture", rough);
			mat.Set("roughness", 1.0f);
		}

		return mat;
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

	private static void SpawnTexturedBox(Node3D parent, string name, Vector3 pos, Vector3 size, string texturePath, float tileMeters = ConcreteTileMeters)
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
		mesh.SetSurfaceOverrideMaterial(0, CreateArenaTriplanarMaterial(texturePath, tileMeters, new Color(0.20f, 0.20f, 0.20f)));
		body.AddChild(mesh);

		parent.AddChild(body);
	}

	private static void SpawnPbrTexturedBox(
		Node3D parent,
		string name,
		Vector3 pos,
		Vector3 size,
		string albedoPath,
		string normalPath,
		string roughnessPath,
		float tileMeters,
		string? fallbackAlbedoPath = null,
		float fallbackTileMeters = ConcreteTileMeters)
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

		Material mat;
		var pbr = CreatePbrBoxMaterial(albedoPath, normalPath, roughnessPath, size, tileMeters);
		if (pbr != null)
		{
			mat = pbr;
		}
		else if (!string.IsNullOrWhiteSpace(fallbackAlbedoPath))
		{
			mat = CreateArenaTriplanarMaterial(fallbackAlbedoPath!, fallbackTileMeters, new Color(0.20f, 0.20f, 0.20f));
		}
		else
		{
			mat = new StandardMaterial3D { AlbedoColor = new Color(0.20f, 0.20f, 0.20f), Roughness = 1.0f };
		}

		mesh.SetSurfaceOverrideMaterial(0, mat);
		body.AddChild(mesh);
		parent.AddChild(body);
	}

	private static Material? CreatePbrBoxMaterial(
		string albedoPath,
		string normalPath,
		string roughnessPath,
		Vector3 boxSize,
		float tileMeters)
	{
		if (!ResourceLoader.Exists(albedoPath))
			return null;

		var albedo = LoadTextureFor3D(albedoPath);
		if (albedo == null)
			return null;

		var normal = ResourceLoader.Exists(normalPath) ? LoadTextureFor3D(normalPath) : null;
		var rough = ResourceLoader.Exists(roughnessPath) ? LoadTextureFor3D(roughnessPath) : null;

		var mat = new StandardMaterial3D
		{
			AlbedoColor = Colors.White,
			AlbedoTexture = albedo,
			Roughness = 1.0f,
			Metallic = 0.0f,
		};

		mat.Set("texture_repeat", 1); // Enabled
		mat.Set("texture_filter", 5); // Linear mipmap anisotropic

		// BoxMesh has UVs. Scale them so texture repeats roughly by real-world meters.
		// This isn't perfect per-face, but it gives consistent readable tiling quickly.
		var uScale = Mathf.Max(0.01f, Mathf.Max(boxSize.X, boxSize.Z) / Mathf.Max(0.25f, tileMeters));
		var vScale = Mathf.Max(0.01f, boxSize.Y / Mathf.Max(0.25f, tileMeters));
		mat.Set("uv1_scale", new Vector3(uScale, vScale, 1.0f));

		if (normal != null)
		{
			mat.Set("normal_enabled", true);
			mat.Set("normal_texture", normal);
			mat.Set("normal_scale", 0.9f);
		}
		if (rough != null)
		{
			mat.Set("roughness_texture", rough);
			mat.Set("roughness", 1.0f);
		}

		return mat;
	}

	
	private static Material CreateArenaFloorMaterial(string texturePath, float metersPerTile, Color fallback)
	{
		var tex = (!string.IsNullOrWhiteSpace(texturePath) && ResourceLoader.Exists(texturePath))
			? LoadTextureFor3D(texturePath)
			: null;

		if (tex == null || !ResourceLoader.Exists(ArenaFloorShaderPath))
		{
			var mat = new StandardMaterial3D
			{
				Roughness = 1.0f,
				AlbedoColor = tex == null ? fallback : Colors.White,
				AlbedoTexture = tex
			};
			// Avoid enum binding differences by setting the property directly.
			mat.Set("texture_repeat", 1); // Enabled
			return mat;
		}

		var shader = GD.Load<Shader>(ArenaFloorShaderPath);
		var matShader = new ShaderMaterial { Shader = shader };
		matShader.SetShaderParameter("albedo_tex", tex);
		matShader.SetShaderParameter("meters_per_tile", Mathf.Max(0.25f, metersPerTile));
		matShader.SetShaderParameter("tint", Colors.White);
		matShader.SetShaderParameter("roughness", 1.0f);
		matShader.SetShaderParameter("metallic", 0.0f);
		return matShader;
	}

	private static Material CreateArenaTriplanarMaterial(string texturePath, float metersPerTile, Color fallback)
	{
		var tex = (!string.IsNullOrWhiteSpace(texturePath) && ResourceLoader.Exists(texturePath))
			? LoadTextureFor3D(texturePath)
			: null;

		if (tex == null || !ResourceLoader.Exists(ArenaTriplanarShaderPath))
		{
			var mat = new StandardMaterial3D
			{
				Roughness = 1.0f,
				AlbedoColor = tex == null ? fallback : Colors.White,
				AlbedoTexture = tex
			};
			mat.Set("texture_repeat", 1); // Enabled
			return mat;
		}

		var shader = GD.Load<Shader>(ArenaTriplanarShaderPath);
		var matShader = new ShaderMaterial { Shader = shader };
		matShader.SetShaderParameter("albedo_tex", tex);
		matShader.SetShaderParameter("meters_per_tile", Mathf.Max(0.25f, metersPerTile));
		matShader.SetShaderParameter("sharpness", 2.0f);
		matShader.SetShaderParameter("tint", Colors.White);
		matShader.SetShaderParameter("roughness", 1.0f);
		matShader.SetShaderParameter("metallic", 0.0f);
		return matShader;
	}


	private static Texture2D? LoadTextureFor3D(string texturePath)
	{
		var ext = Path.GetExtension(texturePath).ToLowerInvariant();
		if (ext == ".exr")
		{
			// Godot's EXR loader supports only a subset of OpenEXR compression formats.
			// Poly Haven "blend" packs sometimes ship EXRs using compression that Godot can't decode,
			// resulting in spammy runtime errors. Treat EXR maps as optional and prefer PNG variants.
			if (ExrWarnOnce.Add(texturePath))
				GD.Print($"[ArenaWorld] Skipping EXR texture (unsupported compression in Godot): {texturePath}. " +
				         "Use the PNG versions if available, or convert EXR -> PNG.");
			return null;
		}

		if (RuntimeTextureCache.TryGetValue(texturePath, out var cached)) return cached;

		try
		{
			// Prefer runtime image load so we can generate mipmaps even if the .import has them disabled.
			var img = new Image();
			// Image.Load is most reliable with a globalized filesystem path on Windows.
			var abs = ProjectSettings.GlobalizePath(texturePath);
			var err = img.Load(abs);
			if (err != Error.Ok)
				err = img.Load(texturePath);
			if (err == Error.Ok)
			{
				img.GenerateMipmaps();
				var tex = ImageTexture.CreateFromImage(img);
				RuntimeTextureCache[texturePath] = tex;
				return tex;
			}
		}
		catch
		{
			// Fall back below.
		}

		// Fallback: whatever Godot imported.
		try
		{
			if (ResourceLoader.Exists(texturePath))
			{
				var tex = GD.Load<Texture2D>(texturePath);
				if (tex != null)
				{
					RuntimeTextureCache[texturePath] = tex;
					return tex;
				}
			}
		}
		catch
		{
			// ignore
		}

		return null;
	}

	private static string? PickFirstExisting(params string[] candidates)
	{
		foreach (var c in candidates)
		{
			if (!string.IsNullOrWhiteSpace(c) && ResourceLoader.Exists(c))
				return c;
		}
		return null;
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

		// A few hand-placed starter obstacles.
		SpawnObstacle(obstacles, new Vector3(-10, 0.9f, -8), new Vector3(6, 1.8f, 2));
		SpawnObstacle(obstacles, new Vector3(12, 0.9f, -2), new Vector3(3, 1.8f, 8));
		SpawnObstacle(obstacles, new Vector3(0, 0.9f, 10), new Vector3(8, 1.8f, 2));

		// Add a few more interior walls (deterministic "random") so fights feel less empty.
		SpawnRandomInteriorWalls(obstacles, seed: 12873, count: 5);
	}

	private static void SpawnObstacle(Node3D parent, Vector3 pos, Vector3 size)
	{
		SpawnTexturedBox(parent, name: "Obstacle", pos: pos, size: size, texturePath: ConcreteTexturePath);
	}

	private static void SpawnRandomInteriorWalls(Node3D parent, int seed, int count)
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)Math.Abs(seed) };
		var placed = 0;

		// Avoid vehicle spawn points.
		var avoidA = new Vector3(-16, 0, 10);
		var avoidB = new Vector3(16, 0, -10);

		for (var attempt = 0; attempt < 60 && placed < count; attempt++)
		{
			var longAxis = rng.Randf() < 0.5f ? "x" : "z";
			var length = rng.RandfRange(8f, 18f);
			var thickness = rng.RandfRange(1.0f, 1.8f);
			var height = rng.RandfRange(1.6f, 2.4f);
			var size = longAxis == "x"
				? new Vector3(length, height, thickness)
				: new Vector3(thickness, height, length);

			var x = rng.RandfRange(-34f, 34f);
			var z = rng.RandfRange(-34f, 34f);
			var pos = new Vector3(x, height * 0.5f, z);

			if (Distance2D(pos, avoidA) < 10f) continue;
			if (Distance2D(pos, avoidB) < 10f) continue;

			SpawnPbrTexturedBox(parent, name: $"Wall_{placed}", pos: pos, size: size,
				albedoPath: RockWallAlbedoPath,
				normalPath: PickFirstExisting(RockWallNormalPathPng, RockWallNormalPathExr) ?? RockWallNormalPathExr,
				roughnessPath: PickFirstExisting(RockWallRoughnessPathPng, RockWallRoughnessPathExr) ?? RockWallRoughnessPathExr,
				tileMeters: RockWallTileMeters,
				fallbackAlbedoPath: ConcreteTexturePath,
				fallbackTileMeters: ConcreteTileMeters);
			placed++;
		}
	}

	private static float Distance2D(in Vector3 a, in Vector3 b)
	{
		var dx = a.X - b.X;
		var dz = a.Z - b.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}
}