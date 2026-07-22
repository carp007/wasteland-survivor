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
/// Supports a configurable variant (color/size/ttl/rise) used by per-section vehicle damage smoke.
/// </summary>
public partial class SmokePuff3D : Node3D
{
	private float _ttl = 0.75f;
	private float _age = 0f;
	private Vector3 _vel;
	private MeshInstance3D? _mesh;
	private StandardMaterial3D? _mat;

	private Color _color = new(0.35f, 0.35f, 0.35f);
	private float _baseAlpha = 0.55f;
	private float _size = 0.18f;
	private float _riseSpeed = 0.55f;
	private float _growthRate = 0.9f;
	private float _riseDamping = 1.8f;
	private Vector3 _drift = Vector3.Zero;

	public static void Spawn(Node3D parent, Vector3 atWorld)
	{
		var puff = new SmokePuff3D();
		parent.AddChild(puff);
		puff.GlobalPosition = atWorld;
	}

	/// <summary>Configurable puff (vehicle damage smoke, heavier wreck smoke, etc.).</summary>
	/// <param name="growthRate">Scale gain per second of age. The 0.9 default balloons a long-ttl
	/// puff into a huge translucent sheet (a wreck's 3.4s puffs read as a flat aura blob from the
	/// RTS camera — eval round 9); long-lived smolder smoke should pass ~0.3.</param>
	/// <param name="riseDamping">Exponential velocity damping. The 1.8 default kills the rise in
	/// ~0.5s (total lift ≈ riseSpeed * 0.56m), fine for muzzle/hit wisps; a standing smoke column
	/// needs a lower value so puffs actually climb and stack.</param>
	/// <param name="driftVelocity">Optional lateral wind (m/s, world space) added to the puff's
	/// initial velocity. From the near-top-down RTS camera a purely vertical column projects onto
	/// the emitter's own footprint and never gains screen-space presence — a horizontal drift is
	/// what turns a smolder into a readable plume trailing across the floor.</param>
	public static void Spawn(
		Node3D parent,
		Vector3 atWorld,
		Color color,
		float baseAlpha = 0.55f,
		float size = 0.18f,
		float ttlSeconds = 0.75f,
		float riseSpeed = 0.55f,
		float growthRate = 0.9f,
		float riseDamping = 1.8f,
		Vector3? driftVelocity = null)
	{
		var puff = new SmokePuff3D
		{
			_color = color,
			_baseAlpha = Mathf.Clamp(baseAlpha, 0f, 1f),
			_size = MathF.Max(0.02f, size),
			_ttl = MathF.Max(0.1f, ttlSeconds),
			_riseSpeed = MathF.Max(0.05f, riseSpeed),
			_growthRate = MathF.Max(0f, growthRate),
			_riseDamping = Mathf.Clamp(riseDamping, 0.05f, 8f),
			_drift = driftVelocity ?? Vector3.Zero,
		};
		parent.AddChild(puff);
		puff.GlobalPosition = atWorld;
	}

	public override void _Ready()
	{
		_vel = new Vector3(
			(float)(Random.Shared.NextDouble() * 0.6 - 0.3),
			_riseSpeed + (float)(Random.Shared.NextDouble() * 0.35),
			(float)(Random.Shared.NextDouble() * 0.6 - 0.3)) + _drift;

		// Textured soft billboard: even at 55% alpha, an UNTEXTURED sphere still reads as a flat
		// hard-edged disc from the RTS camera (eval round 6 P0-2: "paper cutouts"). The generated
		// blotchy radial texture gives each puff internal structure and a ragged dissolving edge.
		var tone = 0.82f + (float)Random.Shared.NextDouble() * 0.30f;
		var smoke = new Color(
			Mathf.Clamp(_color.R * tone * 0.92f + 0.03f, 0f, 1f),
			Mathf.Clamp(_color.G * tone * 0.88f + 0.025f, 0f, 1f),
			Mathf.Clamp(_color.B * tone * 0.84f + 0.02f, 0f, 1f));
		_color = smoke;
		_mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(smoke.R, smoke.G, smoke.B, _baseAlpha * 0.62f),
			AlbedoTexture = GetSmokeTexture(Random.Shared.Next(SmokeTextureVariants)),
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
			DisableReceiveShadows = true,
			NoDepthTest = false,
		};
		_mesh = new MeshInstance3D
		{
			Name = "Smoke",
			Mesh = new QuadMesh { Size = new Vector2(_size * 2.3f, _size * 2.3f) },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			// Random roll so clustered puffs never share blotch orientation.
			RotationDegrees = new Vector3(0f, 0f, (float)(Random.Shared.NextDouble() * 360.0)),
		};
		_mesh.SetSurfaceOverrideMaterial(0, _mat);
		AddChild(_mesh);
	}

	// ---- Generated smoke sprites (cached; 3 variants so clusters don't repeat) ----
	private const int SmokeTextureVariants = 3;
	private static readonly ImageTexture?[] SmokeTextures = new ImageTexture?[SmokeTextureVariants];

	/// <summary>Shared access for other smoke systems (the smoke-screen countermeasure cloud reuses
	/// these sprites so it stops rendering as untextured faceted spheres — judge round, loop 6).</summary>
	public static ImageTexture GetSharedSmokeTexture(int variant) => GetSmokeTexture(variant);

	private static ImageTexture GetSmokeTexture(int variant)
	{
		variant = Math.Clamp(variant, 0, SmokeTextureVariants - 1);
		if (SmokeTextures[variant] is { } cached) return cached;

		const int size = 96;
		var rng = new Random(7919 + variant * 104729);
		// Blotch field: a handful of soft sub-circles jittered around the center makes the
		// silhouette lumpy instead of circular.
		var blobs = new (float x, float y, float r, float w)[7];
		for (var i = 0; i < blobs.Length; i++)
		{
			var ang = (float)(rng.NextDouble() * Math.Tau);
			var d = (float)(rng.NextDouble() * 0.30);
			blobs[i] = (
				0.5f + MathF.Cos(ang) * d,
				0.5f + MathF.Sin(ang) * d,
				0.20f + (float)rng.NextDouble() * 0.22f,
				0.55f + (float)rng.NextDouble() * 0.45f);
		}

		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var u = (x + 0.5f) / size;
				var v = (y + 0.5f) / size;
				var acc = 0f;
				foreach (var b in blobs)
				{
					var dx = u - b.x;
					var dy = v - b.y;
					var d = MathF.Sqrt(dx * dx + dy * dy);
					if (d < b.r)
					{
						var f = 1f - d / b.r;
						acc += b.w * f * f;
					}
				}
				// Per-pixel grain for a dissolving edge.
				var grainSeed = (x * 73856093) ^ (y * 19349663) ^ (variant * 83492791);
				grainSeed = (grainSeed << 13) ^ grainSeed;
				var grain = (((grainSeed * (grainSeed * grainSeed * 15731 + 789221) + 1376312589) & 0x7fffffff) % 1000) / 1000f;
				var a = Math.Clamp(acc, 0f, 1f);
				a *= 0.75f + 0.25f * grain;
				// Slight brightness variance inside the puff (structure at zoom).
				var bright = 0.88f + 0.12f * Math.Clamp(acc - 0.4f, 0f, 1f);
				img.SetPixel(x, y, new Color(bright, bright, bright, a));
			}
		}
		var tex = ImageTexture.CreateFromImage(img);
		SmokeTextures[variant] = tex;
		return tex;
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_age += dt;
		GlobalPosition += _vel * dt;
		_vel *= MathF.Exp(-_riseDamping * dt);

		// Fade out (from the softened spawn alpha, not full base opacity).
		if (_mat != null)
		{
			var t = Mathf.Clamp(_age / _ttl, 0f, 1f);
			var a = Mathf.Lerp(_baseAlpha * 0.62f, 0f, t);
			_mat.AlbedoColor = new Color(_color.R, _color.G, _color.B, a);
		}

		// Grow slightly.
		if (_mesh != null)
		{
			var s = 1f + _age * _growthRate;
			_mesh.Scale = new Vector3(s, s, s);
		}

		if (_age >= _ttl)
			QueueFree();
	}
}
