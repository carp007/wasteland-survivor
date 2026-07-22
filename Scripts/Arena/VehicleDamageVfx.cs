// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Arena/VehicleDamageVfx.cs
// Purpose: Per-section visible vehicle damage (smoke wisps, critical fire) driven by section armor/HP.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Systems;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Master-spec visual goal: vehicles show visible damage per side/section. This node lives as a child
/// of a <see cref="VehiclePawn"/> and renders per-section damage feedback from the authoritative
/// combat state (which ArenaRealtimeView owns as immutable records):
/// - moderate damage: gray smoke wisps drifting from that section,
/// - critical damage: a flickering fire glow plus darker, faster smoke.
/// Smoke puffs spawn into the world VFX root so they linger behind a moving vehicle.
/// </summary>
public partial class VehicleDamageVfx : Node3D
{
	private const float SmokeThreshold = 0.45f;
	private const float FireThreshold = 0.82f;

	private static readonly ArmorSection[] AllSections =
	{
		ArmorSection.Front, ArmorSection.Rear, ArmorSection.Left,
		ArmorSection.Right, ArmorSection.Top, ArmorSection.Undercarriage,
	};

	// Section anchor points in pawn-local space for the 3.25 m reference chassis. NEVER use these
	// raw: X/Z must scale with the hull footprint and Y must track the measured roofline — see
	// <see cref="ScaledAnchor"/>. On big hulls (garbage truck, semi) the fixed offsets sat INSIDE
	// the opaque hull mesh, so every damage wisp/fire glow drew fully occluded and a three-sections-
	// caved WARRIG read factory-fresh at the RTS camera (loop-4 iter 25 fresh-eyes finding).
	private static readonly Dictionary<ArmorSection, Vector3> SectionAnchors = new()
	{
		[ArmorSection.Front] = new Vector3(0f, 0.70f, -1.30f),
		[ArmorSection.Rear] = new Vector3(0f, 0.70f, 1.30f),
		[ArmorSection.Left] = new Vector3(-0.80f, 0.60f, 0f),
		[ArmorSection.Right] = new Vector3(0.80f, 0.60f, 0f),
		[ArmorSection.Top] = new Vector3(0f, 1.10f, 0f),
		[ArmorSection.Undercarriage] = new Vector3(0f, 0.18f, 0.35f),
	};

	/// <summary>
	/// Section anchor adjusted to the ACTUAL hull: X/Z scale with the visual footprint, and the
	/// vertical sits at/near the measured roofline so spawned smoke clears the silhouette
	/// immediately instead of being born (and dying) inside a tall opaque hull.
	/// </summary>
	private Vector3 ScaledAnchor(ArmorSection section)
	{
		if (!SectionAnchors.TryGetValue(section, out var a))
			return new Vector3(0f, _hullTopY, 0f);
		var y = section switch
		{
			ArmorSection.Top => _hullTopY + 0.10f,
			ArmorSection.Undercarriage => a.Y,
			_ => MathF.Max(a.Y, _hullTopY * 0.85f),
		};
		return new Vector3(a.X * _hullScale, y, a.Z * _hullScale);
	}

	// Sections that get a visible ember patch once the vehicle is a charred hulk.
	private static readonly ArmorSection[] EmberSections =
	{
		ArmorSection.Top, ArmorSection.Front, ArmorSection.Rear, ArmorSection.Left,
	};

	private readonly Dictionary<ArmorSection, float> _severity = new();
	private readonly Dictionary<ArmorSection, float> _smokeTimer = new();
	private readonly Dictionary<ArmorSection, MeshInstance3D> _fireGlows = new();
	private readonly List<MeshInstance3D> _embers = new();

	// --- Per-side soot/scorch decals (armor-driven; distinct from the HP-driven smoke/fire) ---
	// Stage 0 = clean, stage 1 = first scorch at <=15% armor, stage 2 = armor stripped (darker +
	// extra patch + a smudge creeping over the roof edge for the top-down read). Flat alpha-eroded
	// planes flush with the hull faces — never floating billboards. Undercarriage has no visible
	// face at the RTS camera and is skipped.
	private const float SootArmorThreshold = 0.15f;
	private readonly Dictionary<ArmorSection, int> _sootStage = new();
	private readonly Dictionary<ArmorSection, Node3D> _sootNodes = new();
	private float _hullScale = 1f;
	private float _hullTopY = 0.95f;
	private static ImageTexture? _sootTexture;

	private Node3D? _worldVfxParent;
	private float _flickerT;
	private bool _destroyed;
	private float _destroyedSmokeTimer;
	private OmniLight3D? _smolderLight;

	// Per-wreck wind: from the near-top-down camera a vertical column projects onto the hull's own
	// footprint, so the plume must DRIFT laterally to gain screen-space presence (loop-4 iter 23).
	private Vector3 _smolderWind = Vector3.Zero;

	// Gentler wind for the still-alive damage wisps (same projection problem at the RTS camera,
	// but a fighting vehicle should trail thin streamers, not a wreck's heavy plume).
	private Vector3? _aliveWind;

	private Vector3 AliveWind()
	{
		if (_aliveWind is { } w)
			return w;
		var angle = (float)(Random.Shared.NextDouble() * Math.Tau);
		var wind = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle))
			* (0.45f + (float)Random.Shared.NextDouble() * 0.25f);
		_aliveWind = wind;
		return wind;
	}

	/// <summary>True once <see cref="MarkDestroyed"/> has run; the wreck stays visually dead.</summary>
	public bool IsDestroyed => _destroyed;

	public void Configure(Node3D worldVfxParent)
	{
		_worldVfxParent = worldVfxParent;
	}

	/// <summary>
	/// Recompute per-section damage severity (0 = pristine, 1 = destroyed) from the live combat state.
	/// Armor soaking keeps the bodywork looking healthy; once armor is stripped and structural HP
	/// drops, the section starts smoking and finally burns.
	/// </summary>
	public void UpdateDamageState(VehicleDefinition def, VehicleInstanceState inst)
	{
		// A destroyed hulk never "heals" visually — ignore further state pushes.
		if (_destroyed)
			return;

		// Soot decals hug the hull, so their frame must track the per-class visual footprint.
		if (GetParent() is VehiclePawn pawn)
		{
			_hullScale = Mathf.Clamp(pawn.VisualFootprintScale, 0.6f, 1.8f);
			_hullTopY = pawn.VisualTopLocalY;
		}

		foreach (var section in AllSections)
		{
			var maxArmor = VehicleCombatMath.GetMaxArmorForSection(inst, def, section);
			var maxHp = VehicleCombatMath.GetMaxHpForSection(def, section);
			inst.CurrentArmorBySection.TryGetValue(section, out var curArmor);
			inst.CurrentHpBySection.TryGetValue(section, out var curHp);

			var armorPct = maxArmor > 0 ? Mathf.Clamp(curArmor / (float)maxArmor, 0f, 1f) : 1f;
			var hpPct = maxHp > 0 ? Mathf.Clamp(curHp / (float)maxHp, 0f, 1f) : 1f;
			var damage01 = 1f - (armorPct * 0.45f + hpPct * 0.55f);
			_severity[section] = Mathf.Clamp(damage01, 0f, 1f);

			// Soot keys off ARMOR alone (the master-spec "visible per-side damage" read). Sections
			// that never had armor (maxArmor == 0) stay clean instead of spawning stage-2 at birth.
			if (section != ArmorSection.Undercarriage)
			{
				var stage = maxArmor <= 0
					? 0
					: (curArmor <= 0 ? 2 : (armorPct <= SootArmorThreshold ? 1 : 0));
				UpdateSootStage(section, stage);
			}
		}
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_flickerT += dt;

		if (_destroyed)
		{
			UpdateDestroyedSmolder(dt);
			return;
		}

		foreach (var section in AllSections)
		{
			if (!_severity.TryGetValue(section, out var severity))
				continue;

			UpdateFireGlow(section, severity);

			if (severity < SmokeThreshold)
				continue;

			_smokeTimer.TryGetValue(section, out var timer);
			timer -= dt;
			if (timer > 0f)
			{
				_smokeTimer[section] = timer;
				continue;
			}

			// Heavier damage = faster, darker smoke.
			var t = Mathf.InverseLerp(SmokeThreshold, 1f, severity);
			var interval = Mathf.Lerp(1.4f, 0.24f, t) * (0.85f + (float)Random.Shared.NextDouble() * 0.30f);
			_smokeTimer[section] = interval;
			SpawnSectionSmoke(section, severity, t);
		}
	}

	private void UpdateSootStage(ArmorSection section, int stage)
	{
		_sootStage.TryGetValue(section, out var current);
		if (current == stage)
			return;
		_sootStage[section] = stage;

		// Rebuild from scratch on any stage change (also handles mid-match repairs regressing it).
		if (_sootNodes.TryGetValue(section, out var old))
		{
			if (old != null && GodotObject.IsInstanceValid(old))
				old.QueueFree();
			_sootNodes.Remove(section);
		}
		if (stage <= 0)
			return;

		var node = BuildSootNode(section, stage);
		AddChild(node);
		_sootNodes[section] = node;
	}

	/// <summary>
	/// Builds the soot decal cluster for one section: 1-2 irregular alpha-eroded quads flush with
	/// that side's hull face (offset ~2.5 cm out to avoid z-fighting), plus — at stage 2 — a flat
	/// smudge lying on the roof edge above that side so the damage still reads from the top-down
	/// RTS camera. Placement is seeded on pawn name + section + stage: identical across runs.
	/// </summary>
	private Node3D BuildSootNode(ArmorSection section, int stage)
	{
		var s = _hullScale;
		var faceY = Mathf.Clamp(_hullTopY * 0.45f, 0.30f, 0.62f);

		// Face frame: local +Z points outward from the hull, local X runs along the face.
		var (framePos, frameRotDeg, faceWidth) = section switch
		{
			ArmorSection.Front => (new Vector3(0f, faceY, -1.62f * s), new Vector3(0f, 180f, 0f), 1.5f * s),
			ArmorSection.Rear => (new Vector3(0f, faceY, 1.62f * s), Vector3.Zero, 1.5f * s),
			ArmorSection.Left => (new Vector3(-0.92f * s, faceY, 0f), new Vector3(0f, -90f, 0f), 2.2f * s),
			ArmorSection.Right => (new Vector3(0.92f * s, faceY, 0f), new Vector3(0f, 90f, 0f), 2.2f * s),
			_ => (new Vector3(0f, _hullTopY + 0.03f, 0f), new Vector3(-90f, 0f, 0f), 1.4f * s), // Top
		};

		var container = new Node3D { Name = $"Soot_{section}" };
		var face = new Node3D { Name = "Face", Position = framePos, RotationDegrees = frameRotDeg };
		container.AddChild(face);

		var rnd = new Random(unchecked(StableSeed() * 131 + (int)section * 17 + stage));
		var patches = stage >= 2 ? 2 : 1;
		for (var k = 0; k < patches; k++)
		{
			var tone = stage >= 2 && k == 0 ? 0.042f : 0.085f;
			var alpha = stage >= 2 ? 0.92f : 0.80f;
			var w = faceWidth * (0.42f + (float)rnd.NextDouble() * 0.22f);
			var h = 0.34f + (float)rnd.NextDouble() * 0.18f;
			var off = ((float)rnd.NextDouble() - 0.5f) * faceWidth * 0.5f;
			var quad = new MeshInstance3D
			{
				Name = $"SootPatch{k}",
				Mesh = new QuadMesh { Size = new Vector2(1f, 1f) },
				Position = new Vector3(off, ((float)rnd.NextDouble() - 0.5f) * 0.12f, 0.025f + k * 0.008f),
				// In-plane spin (about the quad normal) keeps the eroded edge irregular per patch.
				RotationDegrees = new Vector3(0f, 0f, (float)(rnd.NextDouble() * 360.0)),
				Scale = new Vector3(w, h, 1f),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			quad.SetSurfaceOverrideMaterial(0, MakeSootMaterial(tone, alpha));
			face.AddChild(quad);
		}

		// Stage 2 on a vertical face: soot creeps over the roof edge above that side (pawn-local,
		// outside the rotated face frame) so the read survives the near-top-down camera.
		if (stage >= 2 && section != ArmorSection.Top)
		{
			var topLocal = section switch
			{
				ArmorSection.Front => new Vector3(0f, _hullTopY + 0.03f, -0.95f * s),
				ArmorSection.Rear => new Vector3(0f, _hullTopY + 0.03f, 0.95f * s),
				ArmorSection.Left => new Vector3(-0.48f * s, _hullTopY + 0.03f, 0f),
				_ => new Vector3(0.48f * s, _hullTopY + 0.03f, 0f),
			};
			// Elongated along the hull edge it hugs: X for front/rear edges, Z for the sides
			// (local Y maps onto Z through the -90 X rotation).
			var alongX = section is ArmorSection.Front or ArmorSection.Rear;
			var smudge = new MeshInstance3D
			{
				Name = "SootTopSmudge",
				Mesh = new QuadMesh { Size = new Vector2(1f, 1f) },
				Position = topLocal,
				RotationDegrees = new Vector3(-90f, ((float)rnd.NextDouble() - 0.5f) * 24f, 0f),
				Scale = alongX ? new Vector3(1.05f * s, 0.55f, 1f) : new Vector3(0.55f, 1.25f * s, 1f),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			smudge.SetSurfaceOverrideMaterial(0, MakeSootMaterial(0.05f, 0.88f));
			container.AddChild(smudge);
		}

		return container;
	}

	private static StandardMaterial3D MakeSootMaterial(float tone, float alpha) => new()
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		// Slightly warm-black so soot reads as scorch, not a rendering hole.
		AlbedoColor = new Color(tone, tone * 0.95f, tone * 0.88f, alpha),
		AlbedoTexture = GetSootTexture(),
	};

	/// <summary>
	/// Shared 64x64 alpha-eroded soot blob: radial falloff multiplied by a fixed integer-hash noise
	/// (deterministic — no Random.Shared) with a hard cutoff for ragged, irregular edges.
	/// </summary>
	private static ImageTexture GetSootTexture()
	{
		if (_sootTexture != null)
			return _sootTexture;
		const int n = 64;
		var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
		for (var y = 0; y < n; y++)
		{
			for (var x = 0; x < n; x++)
			{
				var dx = (x + 0.5f) / n * 2f - 1f;
				var dy = (y + 0.5f) / n * 2f - 1f;
				var d = MathF.Sqrt(dx * dx + dy * dy);
				var h = unchecked((uint)(x * 374761393 + y * 668265263));
				h ^= h >> 13;
				h = unchecked(h * 1274126177u);
				var noise = ((h >> 9) & 0x3FF) / 1023f;
				var a = Mathf.Clamp(1.15f - d * 1.30f, 0f, 1f);
				a *= 0.45f + 0.55f * noise;
				if (a < 0.10f)
					a = 0f; // eroded edge
				img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
			}
		}
		_sootTexture = ImageTexture.CreateFromImage(img);
		return _sootTexture;
	}

	/// <summary>Stable char-sum of the parent pawn's name (string.GetHashCode is per-process random).</summary>
	private int StableSeed()
	{
		var name = GetParent()?.Name.ToString() ?? Name.ToString();
		var h = 17;
		foreach (var c in name)
			h = unchecked(h * 31 + c);
		return h;
	}

	private void SpawnSectionSmoke(ArmorSection section, float severity, float t01)
	{
		var parent = ResolveWorldVfxParent();
		if (parent == null)
			return;
		var anchor = ScaledAnchor(section);

		var jitter = new Vector3(
			((float)Random.Shared.NextDouble() - 0.5f) * 0.30f,
			(float)Random.Shared.NextDouble() * 0.10f,
			((float)Random.Shared.NextDouble() - 0.5f) * 0.30f);
		var atWorld = ToGlobal(anchor + jitter);

		// Everything below applies the iter-23 wreck-plume lessons to the ALIVE path (which had all
		// the same invisibility bugs at the 37m camera): puffs sized for the hull footprint, a
		// two-tone mix so no single gray vanishes against either floor value, low growth so long
		// wisps don't sheet, low damping so they CLIMB clear of the roofline they now spawn at, and
		// a gentle lateral wind for screen-space presence. Kept lighter/thinner than the wreck
		// smolder so "hurt but fighting" stays visually distinct from "dead".
		var sizeScale = 0.75f + 0.35f * _hullScale;
		Color color;
		if (Random.Shared.NextDouble() < 0.55)
		{
			var tone = Mathf.Lerp(0.52f, 0.24f, t01);
			color = new Color(tone, tone, tone);
		}
		else
		{
			var ash = 0.58f + (float)Random.Shared.NextDouble() * 0.10f;
			color = new Color(ash, ash * 0.88f, ash * 0.74f);
		}
		SmokePuff3D.Spawn(parent, atWorld,
			color: color,
			baseAlpha: Mathf.Lerp(0.68f, 0.98f, t01),
			size: Mathf.Lerp(0.46f, 0.72f, t01) * sizeScale,
			ttlSeconds: Mathf.Lerp(1.5f, 2.6f, t01),
			riseSpeed: severity >= FireThreshold ? 1.3f : 0.9f,
			growthRate: 0.35f,
			riseDamping: 0.30f,
			driftVelocity: AliveWind());
	}

	private void UpdateFireGlow(ArmorSection section, float severity)
	{
		var burning = severity >= FireThreshold;
		_fireGlows.TryGetValue(section, out var glow);

		if (!burning)
		{
			if (glow != null && GodotObject.IsInstanceValid(glow))
				glow.QueueFree();
			if (glow != null)
				_fireGlows.Remove(section);
			return;
		}

		if (glow == null || !GodotObject.IsInstanceValid(glow))
		{
			// Anchor + radius track the hull: the fixed 0.30r sphere at compact-car offsets was
			// buried inside big hulls and surfaced as a near-invisible dot (iter-25 fresh eyes).
			var r = 0.30f * Mathf.Clamp(0.75f + 0.35f * _hullScale, 0.8f, 1.6f);
			glow = new MeshInstance3D
			{
				Name = $"Fire_{section}",
				// Sized to read from the high RTS camera.
				Mesh = new SphereMesh { Radius = r, Height = r * 1.8f, RadialSegments = 10, Rings = 6 },
				Position = ScaledAnchor(section),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			glow.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoColor = new Color(1.0f, 0.55f, 0.12f, 0.85f),
				EmissionEnabled = true,
				Emission = new Color(1.0f, 0.45f, 0.10f),
				EmissionEnergyMultiplier = 2.4f,
			});
			AddChild(glow);
			_fireGlows[section] = glow;
		}

		// Cheap fire flicker: irregular scale + orange/yellow hue wobble per section.
		var phase = (int)section * 1.7f;
		var flicker = 0.75f
			+ 0.18f * MathF.Sin(_flickerT * 11.3f + phase)
			+ 0.12f * MathF.Sin(_flickerT * 23.7f + phase * 2.3f);
		glow.Scale = new Vector3(flicker, flicker * 1.25f, flicker);
		if (glow.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
		{
			var warm = 0.45f + 0.25f * MathF.Sin(_flickerT * 17.1f + phase);
			mat.AlbedoColor = new Color(1.0f, 0.40f + warm * 0.45f, 0.10f, 0.70f + 0.20f * flicker);
		}
	}

	/// <summary>
	/// Permanently switch the parent pawn's visuals to a charred hulk: every visible mesh is
	/// multiplied toward near-black, the raging pre-death fire is replaced with pulsing ember-orange
	/// patches, and the wreck emits lazy heavy smoke — so "dead" reads at a glance through the
	/// salvage phase, distinct from "badly damaged but alive". Idempotent; after this,
	/// <see cref="UpdateDamageState"/> pushes are ignored.
	/// </summary>
	public void MarkDestroyed()
	{
		if (_destroyed)
			return;
		_destroyed = true;

		CharParentMeshes();

		// The identity accents (underglow strips + facing wedge) are emissive and live OUTSIDE the
		// "Visual" subtree the char pass touches — a wreck must not keep glowing team colors.
		if (GetParent() is VehiclePawn pawn)
			pawn.SetIdentityDestroyed();

		// The flickering fire glow means "about to die"; a dead hulk smolders instead.
		foreach (var glow in _fireGlows.Values)
		{
			if (glow != null && GodotObject.IsInstanceValid(glow))
				glow.QueueFree();
		}
		_fireGlows.Clear();

		SpawnEmberPatches();
		SpawnScorchRing();

		// Modest flickering smolder light: the wreck glows warm onto the floor around it without
		// projecting any flat colored disc geometry (the old giant late-life smoke sheets read as a
		// "maroon aura blob" over the lit center floor — eval round 9).
		_smolderLight = new OmniLight3D
		{
			Name = "SmolderLight",
			LightColor = new Color(1.0f, 0.45f, 0.14f),
			// 1.2 @ 4m was imperceptible from the 37m camera against the arena's own floodlights;
			// the warm pool on the floor is what sells "still hot" at gameplay zoom.
			LightEnergy = 2.4f,
			OmniRange = 5.5f,
			ShadowEnabled = false,
			Position = new Vector3(0f, 0.9f, 0f),
		};
		AddChild(_smolderLight);

		// Stable per-wreck wind heading: the plume trails across the floor in one believable
		// direction instead of pooling over the hull.
		var windAngle = (float)(Random.Shared.NextDouble() * Math.Tau);
		_smolderWind = new Vector3(MathF.Cos(windAngle), 0f, MathF.Sin(windAngle))
			* (0.95f + (float)Random.Shared.NextDouble() * 0.35f);

		// Pre-seed the plume so it already exists the moment the destruction fireball clears —
		// at the old 0.4-0.65s cadence the first post-kill frames held 1-3 faint puffs and the
		// wreck read as inert (loop-4 iter 23). Higher fake-age puffs sit farther downwind and are
		// bigger, thinner, and shorter-lived, as if the plume had been streaming for a few seconds.
		var seedParent = ResolveWorldVfxParent() ?? GetParent() as Node3D;
		if (seedParent != null)
		{
			var topAnchor = ScaledAnchor(ArmorSection.Top);
			for (var i = 0; i < 5; i++)
			{
				var age01 = i / 4f;
				var fakeAgeSeconds = age01 * 2.4f;
				var seedJitter = new Vector3(
					((float)Random.Shared.NextDouble() - 0.5f) * (0.4f + age01 * 0.7f),
					age01 * 1.6f,
					((float)Random.Shared.NextDouble() - 0.5f) * (0.4f + age01 * 0.7f));
				var seedAt = ToGlobal(topAnchor + seedJitter) + _smolderWind * fakeAgeSeconds;
				SmokePuff3D.Spawn(seedParent, seedAt,
					color: PickSmolderPuffColor(),
					baseAlpha: 1.0f - age01 * 0.45f,
					size: 1.15f + age01 * 0.55f,
					ttlSeconds: 3.4f - age01 * 2.3f,
					riseSpeed: 0.75f,
					growthRate: 0.28f,
					riseDamping: 0.10f,
					driftVelocity: _smolderWind);
			}
		}

		_destroyedSmokeTimer = 0.30f;
	}

	/// <summary>
	/// One-time near-black scorch stamped on the floor under the wreck (ragged soot-blob quad, not a
	/// hard-edged disc). Spawned into the world VFX root when available so the burn mark stays on the
	/// ground even if the hulk is later towed; falls back to the pawn so the mark always exists.
	/// </summary>
	private void SpawnScorchRing()
	{
		var parent = ResolveWorldVfxParent() ?? GetParent() as Node3D;
		if (parent == null)
			return;

		// Core + halo burn layers, plus a pale ash dusting ring between them: near-black scorch
		// alone disappears on the equally dark panel field at gameplay zoom (loop-4 iter 23) — the
		// light ash ring is what makes the burn mark read on dark ground, while the black core
		// still carries it on the light center disc.
		var at = GlobalPosition;
		var layers = new (float Size, float Alpha, float Y, Color Tint)[]
		{
			(2.9f, 0.72f, 0.045f, new Color(0.030f, 0.026f, 0.022f)),
			(3.7f, 0.26f, 0.040f, new Color(0.42f, 0.39f, 0.34f)),
			(4.6f, 0.34f, 0.035f, new Color(0.030f, 0.026f, 0.022f)),
		};
		foreach (var layer in layers)
		{
			var scorch = new MeshInstance3D
			{
				Name = "WreckScorch",
				Mesh = new QuadMesh { Size = new Vector2(1f, 1f) },
				RotationDegrees = new Vector3(-90f, 0f, (float)(Random.Shared.NextDouble() * 360.0)),
				Scale = new Vector3(layer.Size, layer.Size, 1f),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			scorch.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				// Warm near-black core/halo (burnt ground, not a hole) + pale ash mid-ring.
				AlbedoColor = new Color(layer.Tint.R, layer.Tint.G, layer.Tint.B, layer.Alpha),
				AlbedoTexture = GetSootTexture(),
			});
			parent.AddChild(scorch);
			scorch.GlobalPosition = new Vector3(at.X, layer.Y, at.Z);
		}
	}

	private void CharParentMeshes()
	{
		if (GetParent() is not Node3D pawn)
			return;
		var visual = pawn.GetNodeOrNull<Node3D>("Visual");
		if (visual == null || !GodotObject.IsInstanceValid(visual))
			return;

		StandardMaterial3D? fallback = null;
		CharMeshesRecursive(visual, ref fallback);
	}

	private static void CharMeshesRecursive(Node node, ref StandardMaterial3D? fallback)
	{
		foreach (var childObj in node.GetChildren())
		{
			if (childObj is not Node child)
				continue;
			if (child is MeshInstance3D mi && mi.Mesh != null)
				CharMeshSurfaces(mi, ref fallback);
			CharMeshesRecursive(child, ref fallback);
		}
	}

	private static void CharMeshSurfaces(MeshInstance3D mi, ref StandardMaterial3D? fallback)
	{
		var surfaces = mi.Mesh!.GetSurfaceCount();
		for (var s = 0; s < surfaces; s++)
		{
			if (mi.GetActiveMaterial(s) is BaseMaterial3D active)
			{
				// Duplicate (materials may be shared between pawns) and multiply toward near-black,
				// keeping a faint hue so a red wreck still reads subtly different from a blue one.
				var charred = (BaseMaterial3D)active.Duplicate();
				// Don't let a later body-tint refresh (ws_body_tint fast path) resurrect the paint.
				if (charred.HasMeta("ws_body_tint"))
					charred.RemoveMeta("ws_body_tint");
				var c = charred.AlbedoColor;
				charred.AlbedoColor = new Color(c.R * 0.10f + 0.02f, c.G * 0.08f + 0.02f, c.B * 0.07f + 0.02f, c.A);
				charred.EmissionEnabled = false;
				charred.Roughness = 1f;
				charred.Metallic = 0f;
				mi.SetSurfaceOverrideMaterial(s, charred);
			}
			else
			{
				fallback ??= new StandardMaterial3D
				{
					AlbedoColor = new Color(0.05f, 0.045f, 0.04f),
					Roughness = 1f,
					Metallic = 0f,
				};
				mi.SetSurfaceOverrideMaterial(s, fallback);
			}
		}
	}

	private void SpawnEmberPatches()
	{
		var rnd = Random.Shared;
		foreach (var section in EmberSections)
		{
			var anchor = ScaledAnchor(section);
			var jitter = new Vector3(
				((float)rnd.NextDouble() - 0.5f) * 0.35f * _hullScale,
				0.06f,
				((float)rnd.NextDouble() - 0.5f) * 0.35f * _hullScale);
			// Radius sized for the fixed RTS camera: 0.16-0.24 rendered as 1-2px dots that only
			// showed under 3x magnification — the glow patches must survive the zoom-out. Grows
			// mildly with the hull so ember dots don't vanish on a semi-sized wreck.
			var r = (0.26f + (float)rnd.NextDouble() * 0.10f) * Mathf.Clamp(0.8f + 0.25f * _hullScale, 0.9f, 1.35f);
			var ember = new MeshInstance3D
			{
				Name = $"Ember_{section}",
				Mesh = new SphereMesh { Radius = r, Height = r * 1.5f, RadialSegments = 8, Rings = 5 },
				Position = anchor + jitter,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			ember.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoColor = new Color(1.0f, 0.34f, 0.06f, 0.85f),
				EmissionEnabled = true,
				Emission = new Color(1.0f, 0.30f, 0.05f),
				EmissionEnergyMultiplier = 1.6f,
			});
			AddChild(ember);
			_embers.Add(ember);
		}
	}

	private void UpdateDestroyedSmolder(float dt)
	{
		// Slow, irregular ember pulse — smoldering, not burning.
		for (var i = 0; i < _embers.Count; i++)
		{
			var ember = _embers[i];
			if (ember == null || !GodotObject.IsInstanceValid(ember))
				continue;
			var phase = i * 2.1f;
			var pulse = 0.72f
				+ 0.20f * MathF.Sin(_flickerT * 2.6f + phase)
				+ 0.08f * MathF.Sin(_flickerT * 6.3f + phase * 1.7f);
			ember.Scale = new Vector3(pulse, pulse * 0.8f, pulse);
			if (ember.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
				mat.EmissionEnergyMultiplier = 0.9f + 1.1f * pulse;
		}

		// Irregular warm pulse on the smolder light (energy ~2.4 nominal — must stay visible
		// against the arena floodlights from the 37m camera).
		if (_smolderLight != null && GodotObject.IsInstanceValid(_smolderLight))
		{
			_smolderLight.LightEnergy = 2.4f
				+ 0.55f * MathF.Sin(_flickerT * 2.9f)
				+ 0.25f * MathF.Sin(_flickerT * 7.1f + 1.3f);
		}

		_destroyedSmokeTimer -= dt;
		if (_destroyedSmokeTimer > 0f)
			return;
		// Fast cadence + long TTL = a standing COLUMN of 7-9 overlapping puffs. At the old
		// 1.3-2.2s interval the wreck averaged one faint puff and read as inert (eval round 7:
		// "black hulls with two static ember dots — kills the post-kill drama").
		_destroyedSmokeTimer = 0.30f + (float)Random.Shared.NextDouble() * 0.20f;

		// The smolder must survive the whole salvage phase: if the world VFX root ever goes away,
		// fall back to spawning puffs as children of the pawn itself rather than going silent.
		var parent = ResolveWorldVfxParent() ?? GetParent() as Node3D;
		if (parent == null)
			return;

		// Heavy, dark, lazy smoke from the hull top. Low growth + low damping (vs the SmokePuff3D
		// defaults) keep each puff column-sized while letting it actually climb ~2m, so 4-6 puffs
		// stack into a visible standing column instead of ballooning into one flat translucent
		// sheet over the wreck (eval round 9: "maroon aura blob").
		var anchor = ScaledAnchor(ArmorSection.Top);
		var jitter = new Vector3(
			((float)Random.Shared.NextDouble() - 0.5f) * 0.6f * _hullScale,
			0f,
			((float)Random.Shared.NextDouble() - 0.5f) * 0.6f * _hullScale);
		// Two-tone plume: any single gray vanishes against ONE of the arena's two floor values
		// (near-black soot on the dark panel field, mid-gray on the light center disc — loop-4
		// iter 23 found the smolder running yet invisible at the 37m camera). Alternating dark
		// soot with warm fire-lit ash gives the plume internal contrast that reads on BOTH
		// surfaces, and the warm half ties into the embers/scorch fiction.
		var puffColor = PickSmolderPuffColor();
		SmokePuff3D.Spawn(parent, ToGlobal(anchor + jitter),
			color: puffColor,
			baseAlpha: 1.0f,
			size: 1.30f + (float)Random.Shared.NextDouble() * 0.30f,
			ttlSeconds: 3.4f,
			riseSpeed: 0.75f,
			growthRate: 0.28f,
			riseDamping: 0.10f,
			driftVelocity: _smolderWind);

		// Intermittent ember spark: a brief bright mote popping off the hull keeps the wreck alive.
		if (Random.Shared.NextDouble() < 0.45)
		{
			ImpactFlashVfx3D.Spawn(parent,
				ToGlobal(anchor + jitter * 0.5f) + Vector3.Up * 0.2f,
				new Color(1.0f, 0.55f, 0.18f),
				radius: 0.14f, ttlSeconds: 0.22f, alpha: 0.85f);
		}
	}

	/// <summary>40% dark soot / 60% warm fire-lit ash — see the two-tone plume note above.</summary>
	private static Color PickSmolderPuffColor()
	{
		if (Random.Shared.NextDouble() < 0.40)
		{
			var soot = 0.16f + (float)Random.Shared.NextDouble() * 0.12f;
			return new Color(soot, soot, soot);
		}
		var ash = 0.55f + (float)Random.Shared.NextDouble() * 0.12f;
		return new Color(ash, ash * 0.84f, ash * 0.68f);
	}

	private Node3D? ResolveWorldVfxParent()
	{
		if (_worldVfxParent != null && GodotObject.IsInstanceValid(_worldVfxParent) && _worldVfxParent.IsInsideTree())
			return _worldVfxParent;
		_worldVfxParent = null;
		return null;
	}

	public override void _ExitTree()
	{
		_fireGlows.Clear();
		_embers.Clear();
		_sootNodes.Clear();
		_sootStage.Clear();
	}
}
