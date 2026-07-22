// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/ArenaVfx.cs
// Purpose: Arena gameplay/runtime support (3D world, pawns, VFX).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Minimal/cheap arena VFX (no textures, no particle dependencies).
/// We keep this code-only so early builds are robust and compile-safe.
/// </summary>
public static class ArenaVfx
{
	// Hitscan tracer render caps (meters from the muzzle). ~19m is about a third of the visible
	// arena at the sacred RTS zoom — long enough to read as a burst, short enough that a 90m miss
	// can never lay a wall-to-wall ribbon across the frame.
	private const float BrightTracerMeters = 13f;
	private const float MaxTracerMeters = 19f;

	public static void SpawnShot(
		ArenaWorld world,
		Vector3 muzzleWorld,
		Vector3 endWorld,
		bool fromPlayer,
		bool hit,
		float smallArmsScale = 1f)
	{
		var root = world.GetVfxRoot();

		// Core-dominant tapered streak (round 10 P0-2). The old dual BoxMesh beams read as an
		// opaque flat salmon SLAB with visible box end-caps at the fixed RTS altitude — the fat
		// 0.70 shell swallowed the 0.22 core and nothing crossed the glow HDR threshold, so
		// tracers never bloomed. SpawnTracerStreak builds tapered cross-ribbons (width -> 0 at
		// both ends, no caps) with a hot near-white core that dominates the read and a thin
		// additive halo, at emission energies high enough to bloom slightly under ACES.
		//
		// LONG-SHOT CAP (loop #4 iter 28, preserved): tracers render at most MaxTracerMeters from
		// the muzzle — full strength to BrightTracerMeters, then a dissolving burn-out tail —
		// so a 90m joust miss can never lay a wall-to-wall ribbon across the frame.
		// smallArmsScale < 1 shrinks both the length cap and the ribbon width: a 9mm from a
		// standing driver must read as a pistol crack, not the half-arena ordnance beam the
		// vehicle guns draw (judge round, loop 6).
		var scale = Mathf.Clamp(smallArmsScale, 0.15f, 1f);
		var diff = endWorld - muzzleWorld;
		var len = diff.Length();
		if (len >= 0.05f)
		{
			var dir = diff / len;
			SpawnTracerStreak(root, muzzleWorld, dir, MathF.Min(len, MaxTracerMeters * scale), fromPlayer, widthScale: scale);
		}
		SpawnMuzzleFlash(root, muzzleWorld, (endWorld - muzzleWorld).Normalized(), fromPlayer);

		if (hit)
			SpawnHitImpact(world, endWorld);
	}

	/// <summary>Impact punch on a struck vehicle: soft radial flash + spark burst + warm light splash.</summary>
	public static void SpawnHitImpact(ArenaWorld world, Vector3 atWorld)
	{
		var root = world.GetVfxRoot();
		// Soft additive billboard — the old 1.05r opaque emissive sphere read as a featureless
		// white paper disc stamped on the vehicle in every hit frame (eval round 6 P0-1).
		ImpactFlashVfx3D.Spawn(root, atWorld, new Color(1.0f, 0.90f, 0.62f), radius: 1.05f, ttlSeconds: 0.15f);
		SpawnSparks(world, atWorld + new Vector3(0f, 0.06f, 0f), count: 8);

		var light = new OmniLight3D
		{
			Name = "HitLight",
			LightColor = new Color(1.0f, 0.82f, 0.45f),
			LightEnergy = 4.2f,
			OmniRange = 7.5f,
			ShadowEnabled = false,
		};
		root.AddChild(light);
		light.GlobalPosition = atWorld + Vector3.Up * 0.5f;
		AutoFree(root, light, 0.08f);
	}

	/// <summary>Dust/spark scatter where a shot connects with a wall or prop, so misses read too.</summary>
	public static void SpawnWorldImpact(ArenaWorld world, Vector3 atWorld)
	{
		var root = world.GetVfxRoot();
		ImpactFlashVfx3D.Spawn(root, atWorld, new Color(0.80f, 0.72f, 0.56f), radius: 0.45f, ttlSeconds: 0.11f, alpha: 0.6f);
		SpawnSparks(world, atWorld + Vector3.Up * 0.05f, count: 4);
	}

	/// <summary>
	/// Tapered core-dominant tracer streak (round 10 P0-2). Geometry is a cross of triangle-strip
	/// ribbons built in parent-local space whose width eases to zero at both ends — no box caps.
	/// Two layers: a thin white-hot core (the read) and a wider faint additive halo (the color).
	/// Vertex alpha carries the build-168 burn-out: full strength to BrightTracerMeters, then a
	/// dissolving tail out to the hard cap. Both layers fade out over the streak's short life via
	/// a material alpha tween (Expo-In: bright hold, quick dissolve).
	/// </summary>
	private static void SpawnTracerStreak(Node3D parent, Vector3 fromWorld, Vector3 dir, float drawLen, bool fromPlayer, float widthScale = 1f)
	{
		if (drawLen < 0.05f) return;

		// Player: warm gold-white. Enemy: hot red-orange (the old (1.0, 0.45, 0.45) read as
		// desaturated pastel salmon). Cores stay near-white so both read as burning-hot metal.
		var coreColor = fromPlayer ? new Color(1.00f, 0.97f, 0.80f) : new Color(1.00f, 0.84f, 0.58f);
		var haloColor = fromPlayer ? new Color(1.00f, 0.74f, 0.22f) : new Color(1.00f, 0.30f, 0.10f);
		// Emission well above the glow HDR threshold (1.0, ACES) so tracers bloom slightly.
		var coreMat = MakeTracerMaterial(coreColor, emissionEnergy: 7.0f);
		var haloMat = MakeTracerMaterial(haloColor, emissionEnergy: 3.2f);

		var up = MathF.Abs(dir.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
		var side = dir.Cross(up).Normalized();
		var norm = side.Cross(dir).Normalized();

		var segments = Math.Clamp((int)MathF.Ceiling(drawLen * 1.6f), 8, 30);
		var im = new ImmediateMesh();

		void BuildRibbons(Material mat, float halfWidth, float layerAlpha)
		{
			// Two perpendicular ribbons per layer so the streak has presence from any view angle.
			foreach (var axis in new[] { side, norm })
			{
				im.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip, mat);
				for (var i = 0; i <= segments; i++)
				{
					var t = i / (float)segments;
					var meters = t * drawLen;
					// Rounded taper: ease in over the first 10%, out over the last 14%.
					var w = MathF.Min(MathF.Min(t / 0.10f, (1f - t) / 0.14f), 1f);
					w = MathF.Pow(MathF.Max(w, 0f), 0.65f) * halfWidth;
					// Burn-out: dissolve between the bright cap and the hard cap.
					var burn = meters <= BrightTracerMeters
						? 1f
						: 1f - (meters - BrightTracerMeters) / (MaxTracerMeters - BrightTracerMeters);
					var c = new Color(1f, 1f, 1f, layerAlpha * Mathf.Clamp(burn, 0f, 1f));
					var world = fromWorld + dir * meters;
					im.SurfaceSetColor(c);
					im.SurfaceAddVertex(parent.ToLocal(world - axis * w));
					im.SurfaceSetColor(c);
					im.SurfaceAddVertex(parent.ToLocal(world + axis * w));
				}
				im.SurfaceEnd();
			}
		}

		BuildRibbons(coreMat, halfWidth: 0.10f * widthScale, layerAlpha: 1.00f);
		BuildRibbons(haloMat, halfWidth: 0.34f * widthScale, layerAlpha: 0.38f);

		var mesh = new MeshInstance3D
		{
			Name = "TracerStreak",
			Mesh = im,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		parent.AddChild(mesh);

		const float ttl = 0.22f;
		var tween = mesh.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(coreMat, "albedo_color:a", 0f, ttl)
			.SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
		tween.TweenProperty(haloMat, "albedo_color:a", 0f, ttl)
			.SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
		AutoFree(parent, mesh, ttl + 0.03f);
	}

	private static StandardMaterial3D MakeTracerMaterial(Color color, float emissionEnergy)
	{
		return new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = emissionEnergy,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			DisableReceiveShadows = true,
		};
	}

	/// <summary>
	/// Projectile-style shot: a single visible bullet traveling from muzzle to end.
	/// Damage application is handled by the caller; this is purely visuals.
	/// </summary>
	public static float SpawnProjectileShot(
		ArenaWorld world,
		Vector3 muzzleWorld,
		Vector3 endWorld,
		bool fromPlayer,
		bool hit,
		float projectileSpeed)
	{
		var root = world.GetVfxRoot();
		// Same faction language as the hitscan streaks: player warm gold, enemy hot red-orange
		// (the old (1.0, 0.45, 0.45) read as desaturated pastel salmon — round 10 P0-2).
		var color = fromPlayer
			? new Color(1.0f, 0.80f, 0.28f)
			: new Color(1.0f, 0.32f, 0.12f);

		SpawnMuzzleFlash(root, muzzleWorld, (endWorld - muzzleWorld).Normalized(), fromPlayer);

		return BulletProjectileVfx3D.Spawn(root, muzzleWorld, endWorld, projectileSpeed, color,
			onArrive: () =>
			{
				if (hit)
					SpawnHitImpact(world, endWorld);
			});
	}

	/// <summary>
	/// Muzzle flash tuned for the fixed RTS camera: a hot-orange core sphere plus a brief warm
	/// OmniLight pulse that splashes on the vehicle/floor — the light does the reading at 30m up,
	/// the mesh just anchors it to the barrel.
	/// </summary>
	private static void SpawnMuzzleFlash(Node3D root, Vector3 muzzleWorld, Vector3 fireDir, bool fromPlayer)
	{
		var color = fromPlayer ? new Color(1.0f, 0.72f, 0.28f) : new Color(1.0f, 0.5f, 0.35f);

		// Directional flash tongue instead of a blown-out sphere (the ball obscured the vehicle
		// and read as a rendering error — eval round 5). A short hot prism along the fire line
		// plus a small core pop; the light still does most of the reading at altitude.
		if (fireDir.LengthSquared() > 0.001f)
		{
			var tongue = new MeshInstance3D
			{
				Name = "MuzzleTongue",
				Mesh = new PrismMesh { Size = new Vector3(0.30f, 1.05f, 0.22f) },
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			tongue.SetSurfaceOverrideMaterial(0, MakeUnshaded(new Color(1.0f, 0.88f, 0.55f, 0.85f)));
			root.AddChild(tongue);
			var basePos = muzzleWorld + fireDir * 0.55f;
			// PrismMesh points along local +Y; build a basis whose Y column IS the fire line.
			var side = fireDir.Cross(Vector3.Up);
			side = side.LengthSquared() < 0.001f ? Vector3.Right : side.Normalized();
			var normal = side.Cross(fireDir).Normalized();
			tongue.GlobalTransform = new Transform3D(new Basis(side, fireDir, normal), basePos);
			AutoFree(root, tongue, 0.05f);
		}
		// Small additive pop only — an opaque 0.22r sphere bloomed into a ~1m white ball that
		// obscured the vehicle at the RTS camera (recurring "white blob" in screenshot sweeps).
		SpawnFlash(root, muzzleWorld + fireDir * 0.15f, new Color(1.0f, 0.90f, 0.62f, 0.72f), radius: 0.11f, ttlSeconds: 0.035f);

		var light = new OmniLight3D
		{
			Name = "MuzzleLight",
			LightColor = color,
			LightEnergy = 4.6f,
			OmniRange = 9.0f,
			ShadowEnabled = false,
		};
		root.AddChild(light);
		light.GlobalPosition = muzzleWorld + Vector3.Up * 0.35f;
		AutoFree(root, light, 0.06f);
	}

	public static void SpawnSparks(ArenaWorld world, Vector3 atWorld, int count = 3)
	{
		var root = world.GetVfxRoot();
		count = Math.Max(1, Math.Min(20, count));
		for (var i = 0; i < count; i++)
		{
			var jitter = new Vector3(
				(float)(Random.Shared.NextDouble() * 0.60 - 0.30),
				(float)(Random.Shared.NextDouble() * 0.26),
				(float)(Random.Shared.NextDouble() * 0.60 - 0.30));
			// Small soft motes (additive) — a cluster of opaque spheres merged into the disc read.
			ImpactFlashVfx3D.Spawn(root, atWorld + jitter, new Color(1.0f, 0.85f, 0.35f),
				radius: 0.16f, ttlSeconds: 0.10f, alpha: 0.8f);
		}
	}



	public static void SpawnProjectileTrail(Node3D parent, Vector3 atWorld, Vector3 projectileDir, Color tint, float ttlSeconds = 0.34f)
	{
		ProjectileTrailParticle3D.Spawn(parent, atWorld, projectileDir, tint, ttlSeconds);
	}

	public static void SpawnProjectileImpact(ArenaWorld world, Vector3 atWorld, Color tint, float radius = 2.0f)
	{
		var root = world.GetVfxRoot();
		ProjectileImpactVfx3D.Spawn(root, atWorld, tint, radius);
		SpawnSparks(world, atWorld + Vector3.Up * 0.08f, count: Math.Max(8, (int)MathF.Round(radius * 4.0f)));
	}

	/// <summary>
	/// Full vehicle-kill payoff: tumbling debris chunks (dark shards + body-colored panels), emissive
	/// sparks, a ~6m ground shockwave ring, a rising layered fireball, a light pulse and a heavy smoke
	/// column — all sized to read from the fixed RTS camera. Self-frees after ~2.5s.
	/// </summary>
	public static void SpawnVehicleDestruction(Node3D parent, Vector3 atWorld, Color bodyColor)
	{
		VehicleDestructionVfx3D.Spawn(parent, atWorld, bodyColor);
	}

	public static void SpawnVehicleDestruction(ArenaWorld world, Vector3 atWorld, Color bodyColor)
	{
		SpawnVehicleDestruction(world.GetVfxRoot(), atWorld, bodyColor);
	}

	public static void SpawnSkidMark(ArenaWorld world, Vector3 atWorld, float yawRad, float ttlSeconds = 8.0f)
	{
		var root = world.GetVfxRoot();
		var mesh = new MeshInstance3D
		{
			Name = "SkidMark",
			Mesh = new BoxMesh { Size = new Vector3(0.26f, 0.01f, 0.70f) },
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
		mesh.GlobalPosition = atWorld;
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
		// Sphere, not cube — box flashes read as bright square cards from the top-down camera.
		var r = Math.Max(0.01f, radius);
		var mesh = new MeshInstance3D
		{
			Name = "Flash",
			Mesh = new SphereMesh { Radius = r, Height = r * 2f, RadialSegments = 10, Rings = 6 },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		mesh.SetSurfaceOverrideMaterial(0, MakeUnshaded(color));
		parent.AddChild(mesh);
		mesh.GlobalPosition = atWorld;
		AutoFree(parent, mesh, ttlSeconds);
	}

	private static Material MakeUnshaded(Color color, bool noDepthTest = false)
	{
		// Emissive so the arena glow pass blooms flashes/sparks slightly. A translucent color
		// (A < 1) switches to additive alpha so glow shells layer instead of occluding.
		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = noDepthTest,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = 2.2f,
		};
		if (color.A < 0.999f)
		{
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			mat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
		}
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
