using Godot;
using System;
using System.Collections.Generic;

namespace WastelandSurvivor.Game.Arena;

/// <summary>
/// Procedural top-face detail for the single-mesh Kenney vehicle hulls.
///
/// The kit ships one "body" mesh per car with one shared colormap material; the flat team tint
/// that replaces it (for unambiguous player/enemy colors) also erases every cue that the shape
/// is a car — from the fixed RTS camera the hulls read as featureless colored boxes while the
/// arena floor around them carries paint, wear, and grime.
///
/// This class paints that detail back on in the material: a per-archetype detail texture is
/// top-projected in mesh-local space (so it needs no UVs and rotates with the car) carrying
/// panel seams, smoked-glass cabin zones, two-tone roof, weathering, and an edge AO vignette.
/// Textures are generated once per (archetype, seed-bucket) and cached for the session.
/// </summary>
public static class VehicleHullDetailer
{
    private static readonly Dictionary<string, ImageTexture> TextureCache = new();
    private static Shader? _hullShader;

    private const int TexW = 256; // across car width (u)
    private const int TexH = 512; // front (v=0) -> rear (v=1)

    /// <summary>
    /// Hull layout archetype inferred from the visual model filename. The filename is a better
    /// signal than VehicleClass because enemy presets override the model independently of class
    /// (a "Sedan"-class preset can drive a box van).
    /// </summary>
    public static string ResolveArchetype(string? modelPath)
    {
        var name = (modelPath ?? string.Empty).ToLowerInvariant();
        if (name.Contains("garbage") || name.Contains("fire")) return "industrial";
        if (name.Contains("truck")) return "truck";
        if (name.Contains("van") || name.Contains("ambulance") || name.Contains("delivery")) return "van";
        if (name.Contains("suv")) return "suv";
        if (name.Contains("race") || name.Contains("sports") || name.Contains("hatchback")) return "sports";
        if (name.Contains("trailer") || name.Contains("box")) return "boxtop";
        return "sedan";
    }

    /// <summary>
    /// Builds (or refreshes) the hull ShaderMaterial for one body surface. axisU/axisV map
    /// mesh-local positions into canonical car space (u across the width, v front->rear);
    /// bounds are the projections of the mesh AABB onto those axes.
    /// When <paramref name="derelict"/> is true the detail texture switches to the dead-chassis
    /// variant (heavier rust, missing-panel dark patches, dusted-over glass, no racing stripes)
    /// used by salvage-yard husks and crushed-car piles — same shader, different story.
    /// </summary>
    public static ShaderMaterial BuildHullMaterial(
        Color bodyColor, string archetype,
        Vector3 axisU, Vector3 axisV,
        Vector2 uvMin, Vector2 uvSize,
        float skirtY0, float skirtY1,
        bool derelict = false,
        bool hostile = false)
    {
        var mat = new ShaderMaterial { Shader = GetHullShader() };
        mat.SetShaderParameter("detail_tex", GetDetailTexture(archetype, derelict));
        mat.SetShaderParameter("axis_u", axisU);
        mat.SetShaderParameter("axis_v", axisV);
        mat.SetShaderParameter("uv_min", uvMin);
        mat.SetShaderParameter("uv_size", uvSize);
        mat.SetShaderParameter("skirt_y", new Vector2(skirtY0, skirtY1));
        mat.SetShaderParameter("body_color", bodyColor);
        if (hostile && !derelict)
        {
            // Hostile faction trim (round 11 N-4): a faint constant red edge/skirt emission plus a
            // luma floor on the body paint, so dark enemy presets (navy sedan on dark floor) stay
            // readable between light pools at 1x. Tuned to read as "hostile paint job", not neon —
            // the target brackets should never be the only thing carrying the enemy.
            mat.SetShaderParameter("rim_color", new Color(1.0f, 0.26f, 0.13f));
            mat.SetShaderParameter("rim_strength", 0.32f);
            mat.SetShaderParameter("luma_floor", 0.17f);
        }
        if (derelict)
        {
            // Dead paint: rust runs darker/browner, the glass is dust-grey (not smoked black), and
            // the paint goes fully matte — live-paint specular washed derelict top faces to white.
            mat.SetShaderParameter("rust_color", new Color(0.24f, 0.14f, 0.075f));
            mat.SetShaderParameter("glass_color", new Color(0.085f, 0.09f, 0.09f));
            mat.SetShaderParameter("matte", 1.0f);
        }
        mat.SetMeta("ws_body_tint", true);
        return mat;
    }

    public static void UpdateBodyColor(ShaderMaterial mat, Color bodyColor)
        => mat.SetShaderParameter("body_color", bodyColor);

    private static Shader GetHullShader()
    {
        if (_hullShader != null) return _hullShader;
        _hullShader = new Shader
        {
            Code = @"
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 body_color : source_color = vec4(0.8, 0.8, 0.8, 1.0);
uniform vec4 glass_color : source_color = vec4(0.055, 0.075, 0.10, 1.0);
uniform vec4 rust_color : source_color = vec4(0.28, 0.17, 0.10, 1.0);
uniform sampler2D detail_tex : filter_linear, repeat_disable;
uniform vec3 axis_u = vec3(1.0, 0.0, 0.0);
uniform vec3 axis_v = vec3(0.0, 0.0, 1.0);
uniform vec2 uv_min = vec2(-1.0, -2.0);
uniform vec2 uv_size = vec2(2.0, 4.0);
uniform vec2 skirt_y = vec2(0.0, 0.5);
// 1.0 = dead matte paint (derelict husks/crushed piles): kills the metallic/specular sheen that
// washed the top faces out to near-white under the overhead sun. 0.0 keeps live-vehicle paint.
uniform float matte = 0.0;
// Hostile faction trim (round 11 N-4). rim_strength > 0 adds a faint constant emission along the
// hull outline (top-face edge band) and the lower skirt; luma_floor lifts too-dark body paint to
// a readable minimum while preserving hue. Both default OFF (player/derelict hulls unchanged).
uniform vec4 rim_color : source_color = vec4(1.0, 0.26, 0.13, 1.0);
uniform float rim_strength = 0.0;
uniform float luma_floor = 0.0;

varying vec3 v_pos;
varying vec3 v_nrm;

void vertex() {
    v_pos = VERTEX;
    v_nrm = NORMAL;
}

void fragment() {
    vec2 uv = vec2(
        (dot(v_pos, axis_u) - uv_min.x) / max(uv_size.x, 0.001),
        (dot(v_pos, axis_v) - uv_min.y) / max(uv_size.y, 0.001));
    vec3 det = texture(detail_tex, clamp(uv, vec2(0.0), vec2(1.0))).rgb;

    // Detail only applies to up-facing surfaces; sides keep clean paint.
    float topw = smoothstep(0.12, 0.45, v_nrm.y);
    float mult = mix(1.0, clamp(det.r * 1.6, 0.0, 1.5), topw);
    // Slanted cabin sides still catch glass (windshields are ~45deg in the kit).
    float glass = det.g * smoothstep(0.08, 0.30, v_nrm.y);
    float rust = det.b * topw;

    // Readability floor: dark preset paints get their luma lifted to luma_floor (hue preserved)
    // so an enemy hull never dissolves into the dark floor between light pools.
    vec3 base_col = body_color.rgb;
    float bl = dot(base_col, vec3(0.299, 0.587, 0.114));
    if (luma_floor > 0.001 && bl < luma_floor)
        base_col *= luma_floor / max(bl, 0.02);

    vec3 paint = base_col * mult;
    // Skirt shading: the lower hull sits in its own shadow.
    float h = clamp((v_pos.y - skirt_y.x) / max(skirt_y.y - skirt_y.x, 0.001), 0.0, 1.0);
    paint *= mix(0.78, 1.0, smoothstep(0.0, 0.55, h));

    vec3 col = mix(paint, rust_color.rgb * (0.7 + 0.6 * det.r), rust * 0.55);
    col = mix(col, glass_color.rgb, glass);

    // Grounded paint (play-test: cars read a bit cartoony): pull saturation down a touch and
    // streak faint wear onto the side panels so hulls read as working machines, not toys. Glass
    // and derelict-matte are exempt; the effect is deliberately below conscious notice at 1x.
    float sidew = (1.0 - topw) * (1.0 - glass) * (1.0 - matte);
    col *= mix(1.0, 0.90 + 0.10 * det.r, sidew * 0.55);
    float cl = dot(col, vec3(0.299, 0.587, 0.114));
    col = mix(col, vec3(cl), 0.10 * (1.0 - glass) * (1.0 - matte));

    // Hostile rim: faint edge emission along the hull outline + lower skirt. Constant (not
    // pulsing), dim enough to read as trim paint at 1x from 37m, never a glow blob.
    vec2 cuv = clamp(uv, vec2(0.0), vec2(1.0));
    float edge_u = min(cuv.x, 1.0 - cuv.x) / 0.075;
    float edge_v = min(cuv.y, 1.0 - cuv.y) / 0.05;
    float rim_band = 1.0 - clamp(min(edge_u, edge_v), 0.0, 1.0);
    // Skirt kept to the lowest hull band at 0.7 weight — the taller 0.12-0.45 band read as pink
    // smears up the body sides at 2x (A/B round 11).
    float skirt_band = (1.0 - smoothstep(0.08, 0.30, h)) * (1.0 - topw) * 0.7;
    float rim = clamp(rim_band * topw + skirt_band, 0.0, 1.0) * (1.0 - glass);
    EMISSION = rim_color.rgb * (rim * rim_strength);

    ALBEDO = col;
    // Automotive paint response: lower base roughness + higher metallic than the old plasticky
    // 0.62/0.20 pair, so highlights read as car paint instead of toy plastic.
    float rough = mix(mix(0.52, 0.36, topw * clamp(det.r * 1.6 - 0.6, 0.0, 1.0)), 0.14, glass);
    ROUGHNESS = mix(rough, max(rough, 0.92), matte * (1.0 - glass));
    METALLIC = mix(mix(0.30, 0.42, glass), 0.02, matte * (1.0 - glass));
    SPECULAR = mix(0.5, 0.15, matte * (1.0 - glass));
}
"
        };
        return _hullShader;
    }

    private static ImageTexture GetDetailTexture(string archetype, bool derelict = false)
    {
        var key = derelict ? archetype + "|derelict" : archetype;
        if (TextureCache.TryGetValue(key, out var cached)) return cached;

        var img = Image.CreateEmpty(TexW, TexH, false, Image.Format.Rgb8);
        PaintDetail(img, archetype, derelict);
        var tex = ImageTexture.CreateFromImage(img);
        TextureCache[key] = tex;
        return tex;
    }

    // ---------------------------------------------------------------- painting

    private struct Layout
    {
        public float WindshieldV0, WindshieldV1;   // glass band (full width-ish)
        public float RoofV0, RoofV1;               // metal roof zone (two-tone + inset seam)
        public float RearGlassV0, RearGlassV1;     // rear window band (0 size = none)
        public float CargoV0;                      // ribbed cargo/bed zone start (>=1 = none)
        public bool Stripes;                       // racing stripes down the center
        public float HoodVentV;                    // hood vent cluster center (<0 = none)
    }

    private static Layout LayoutFor(string archetype) => archetype switch
    {
        "sports" => new Layout
        {
            WindshieldV0 = 0.30f, WindshieldV1 = 0.44f,
            RoofV0 = 0.44f, RoofV1 = 0.60f,
            RearGlassV0 = 0.60f, RearGlassV1 = 0.70f,
            CargoV0 = 1f, Stripes = true, HoodVentV = 0.18f,
        },
        "van" => new Layout
        {
            WindshieldV0 = 0.10f, WindshieldV1 = 0.22f,
            RoofV0 = 0.24f, RoofV1 = 0.98f,
            RearGlassV0 = 0f, RearGlassV1 = 0f,
            CargoV0 = 0.28f, Stripes = false, HoodVentV = -1f,
        },
        "truck" => new Layout
        {
            WindshieldV0 = 0.14f, WindshieldV1 = 0.26f,
            RoofV0 = 0.26f, RoofV1 = 0.42f,
            RearGlassV0 = 0f, RearGlassV1 = 0f,
            CargoV0 = 0.48f, Stripes = false, HoodVentV = 0.07f,
        },
        "industrial" => new Layout
        {
            WindshieldV0 = 0.08f, WindshieldV1 = 0.19f,
            RoofV0 = 0.20f, RoofV1 = 0.34f,
            RearGlassV0 = 0f, RearGlassV1 = 0f,
            CargoV0 = 0.36f, Stripes = false, HoodVentV = -1f,
        },
        "boxtop" => new Layout
        {
            WindshieldV0 = 0f, WindshieldV1 = 0f,
            RoofV0 = 0.04f, RoofV1 = 0.96f,
            RearGlassV0 = 0f, RearGlassV1 = 0f,
            CargoV0 = 0.06f, Stripes = false, HoodVentV = -1f,
        },
        "suv" => new Layout // short hood, long wagon roof, near-vertical tail glass
        {
            WindshieldV0 = 0.22f, WindshieldV1 = 0.34f,
            RoofV0 = 0.34f, RoofV1 = 0.84f,
            RearGlassV0 = 0.84f, RearGlassV1 = 0.92f,
            CargoV0 = 1f, Stripes = false, HoodVentV = 0.12f,
        },
        _ => new Layout // sedan / taxi / police 3-box
        {
            WindshieldV0 = 0.28f, WindshieldV1 = 0.41f,
            RoofV0 = 0.41f, RoofV1 = 0.64f,
            RearGlassV0 = 0.64f, RearGlassV1 = 0.74f,
            CargoV0 = 1f, Stripes = false, HoodVentV = 0.16f,
        },
    };

    private static void PaintDetail(Image img, string archetype, bool derelict = false)
    {
        var lay = LayoutFor(archetype);
        // Stable seed: string.GetHashCode is randomized per process — never use it for
        // deterministic art. FNV-1a over the archetype name instead.
        var seed = unchecked((int)2166136261u);
        foreach (var c in archetype)
            seed = unchecked((seed ^ c) * 16777619);
        var rng = new Random(seed ^ (derelict ? 0x5a11a6e : 0));

        // Low-frequency blotch field for paint fading (sampled per pixel below).
        var blotches = new (float u, float v, float r, float amp)[6];
        for (var i = 0; i < blotches.Length; i++)
        {
            blotches[i] = (
                (float)rng.NextDouble(),
                (float)rng.NextDouble(),
                0.10f + (float)rng.NextDouble() * 0.22f,
                ((float)rng.NextDouble() - 0.5f) * 0.11f);
        }

        // Rust patches biased to edges and wheel arches. Derelicts rot much harder: more spots,
        // bigger blooms, and they creep onto the middle of the panels too.
        var rustSpots = new (float u, float v, float r)[derelict ? 26 : 10];
        for (var i = 0; i < rustSpots.Length; i++)
        {
            var nearEdge = rng.NextDouble() < (derelict ? 0.5 : 0.7);
            var u = nearEdge
                ? (rng.NextDouble() < 0.5 ? 0.03f + (float)rng.NextDouble() * 0.10f : 0.87f + (float)rng.NextDouble() * 0.10f)
                : (float)rng.NextDouble();
            var maxR = derelict ? 0.085f : 0.045f;
            rustSpots[i] = (u, (float)rng.NextDouble(), 0.015f + (float)rng.NextDouble() * maxR);
        }

        // Derelict missing-panel patches: dark rectangular holes where the hood/trunk/roof skin
        // was stripped for scrap. Aligned loosely to the panel grid so they read as absent metal,
        // not shadows.
        var missing = new (float u0, float v0, float u1, float v1)[derelict ? 4 : 0];
        for (var i = 0; i < missing.Length; i++)
        {
            var mu = 0.14f + (float)rng.NextDouble() * 0.5f;
            var mv = 0.04f + (float)rng.NextDouble() * 0.85f;
            missing[i] = (mu, mv, mu + 0.14f + (float)rng.NextDouble() * 0.22f, mv + 0.05f + (float)rng.NextDouble() * 0.08f);
        }

        // Scratch streaks (thin bright lines along v).
        var scratches = new (float u, float v0, float v1)[7];
        for (var i = 0; i < scratches.Length; i++)
        {
            var v0 = (float)rng.NextDouble() * 0.8f;
            scratches[i] = ((float)rng.NextDouble(), v0, v0 + 0.05f + (float)rng.NextDouble() * 0.20f);
        }

        for (var y = 0; y < TexH; y++)
        {
            var v = (y + 0.5f) / TexH;
            for (var x = 0; x < TexW; x++)
            {
                var u = (x + 0.5f) / TexW;

                // --- base paint multiplier (encoded R = mult / 1.6) ---
                var mult = 1.0f;

                // per-pixel micro noise
                var h = Hash(x * 73856093 ^ y * 19349663);
                mult += (h - 0.5f) * 0.055f;

                // low-freq fade blotches
                foreach (var b in blotches)
                {
                    var du = (u - b.u) * 0.55f; // width counts less (car is narrow)
                    var dv = v - b.v;
                    var d = MathF.Sqrt(du * du + dv * dv);
                    if (d < b.r) mult += b.amp * (1f - d / b.r);
                }

                // edge AO vignette (rounded-edge shading + grounds the silhouette)
                var eu = MathF.Min(u, 1f - u) / 0.085f;
                var ev = MathF.Min(v, 1f - v) / 0.055f;
                var edge = MathF.Min(1f, MathF.Min(eu, ev));
                mult *= 0.74f + 0.26f * edge;

                var glass = 0f;
                var rust = 0f;

                // --- glass zones ---
                if (v >= lay.WindshieldV0 && v < lay.WindshieldV1 && u > 0.13f && u < 0.87f)
                    glass = GlassSoft(u, v, lay.WindshieldV0, lay.WindshieldV1);
                else if (lay.RearGlassV1 > lay.RearGlassV0 && v >= lay.RearGlassV0 && v < lay.RearGlassV1 && u > 0.16f && u < 0.84f)
                    glass = GlassSoft(u, v, lay.RearGlassV0, lay.RearGlassV1);
                // side windows along the roof zone (slanted cab sides catch these)
                else if (v >= lay.RoofV0 + 0.01f && v < MathF.Min(lay.RoofV1, lay.CargoV0) - 0.01f
                         && (u is > 0.035f and < 0.125f || u is > 0.875f and < 0.965f))
                    glass = 0.85f;

                // --- roof zone: two-tone + inset seam ---
                if (v >= lay.RoofV0 && v < lay.RoofV1 && glass <= 0f)
                {
                    var roofInsetU = u is > 0.16f and < 0.84f;
                    var roofInsetV = v > lay.RoofV0 + 0.015f && v < lay.RoofV1 - 0.015f;
                    if (roofInsetU && roofInsetV)
                        mult *= 0.84f; // darker roof panel (two-tone)
                    // seam outline around the roof panel
                    var nearU = MathF.Abs(u - 0.16f) < 0.008f || MathF.Abs(u - 0.84f) < 0.008f;
                    var nearV = MathF.Abs(v - (lay.RoofV0 + 0.015f)) < 0.006f || MathF.Abs(v - (lay.RoofV1 - 0.015f)) < 0.006f;
                    if ((nearU && roofInsetV) || (nearV && roofInsetU))
                        mult *= 0.50f;
                }

                // --- transverse panel seams (hood cut, trunk cut, glass borders) ---
                foreach (var seam in stackalloc float[] { lay.WindshieldV0, lay.WindshieldV1, lay.RearGlassV1, lay.CargoV0 })
                {
                    if (seam is <= 0.01f or >= 0.99f) continue;
                    if (MathF.Abs(v - seam) < 0.006f && u > 0.05f && u < 0.95f)
                        mult *= 0.48f;
                }

                // hood center crease
                if (v < lay.WindshieldV0 && MathF.Abs(u - 0.5f) < 0.006f)
                    mult *= 0.85f;

                // --- cargo/bed ribs ---
                if (lay.CargoV0 < 0.99f && v >= lay.CargoV0 && v < 0.97f && glass <= 0f)
                {
                    var rib = MathF.Abs(((v - lay.CargoV0) * 16.5f) % 1f - 0.5f);
                    if (rib < 0.10f && u > 0.07f && u < 0.93f)
                        mult *= 0.72f;
                    // bed side walls
                    if (u is < 0.10f or > 0.90f)
                        mult *= 0.82f;
                }

                // --- hood vents ---
                if (lay.HoodVentV > 0f && MathF.Abs(v - lay.HoodVentV) < 0.028f)
                {
                    var slot = MathF.Abs((u * 26f) % 1f - 0.5f);
                    if (slot < 0.16f && u > 0.30f && u < 0.70f)
                        mult *= 0.68f;
                }

                // --- racing stripes (dead cars lost theirs to sun and rot) ---
                if (lay.Stripes && !derelict && glass <= 0f
                    && (u is > 0.415f and < 0.465f || u is > 0.535f and < 0.585f)
                    && v is > 0.04f and < 0.96f)
                    mult *= 1.22f;

                // --- derelict: stripped-panel holes (ragged dark rectangles) ---
                foreach (var mp in missing)
                {
                    if (u < mp.u0 || u > mp.u1 || v < mp.v0 || v > mp.v1) continue;
                    // ragged edge so the hole doesn't read as a decal sticker
                    var edgeIn = MathF.Min(
                        MathF.Min(u - mp.u0, mp.u1 - u) / 0.02f,
                        MathF.Min(v - mp.v0, mp.v1 - v) / 0.012f);
                    if (edgeIn > 0.35f + (Hash(x * 92821 ^ y * 48271) - 0.5f) * 0.8f)
                        mult *= 0.30f;
                }

                // --- scratches (thin bright wear lines) ---
                foreach (var s in scratches)
                {
                    if (glass > 0f) break;
                    if (MathF.Abs(u - s.u) < 0.004f && v >= s.v0 && v <= s.v1)
                        mult = MathF.Max(mult, mult * 1.16f);
                }

                // --- rust spots ---
                foreach (var rs in rustSpots)
                {
                    var du = (u - rs.u) * 0.55f;
                    var dv = v - rs.v;
                    var d = MathF.Sqrt(du * du + dv * dv);
                    if (d < rs.r)
                    {
                        var t = 1f - d / rs.r;
                        // ragged edge via hash
                        if (t > 0.25f + (Hash(x * 40503 ^ y * 69061) - 0.5f) * 0.5f)
                            rust = MathF.Max(rust, MathF.Min(1f, t * 1.4f));
                    }
                }

                if (glass > 0f) rust = 0f;

                if (derelict)
                {
                    // Faded paint + dust film: pull the whole panel toward matte and let the glass
                    // read dusted-over (weaker glass weight = grey windows, not black mirrors).
                    mult *= 0.90f;
                    glass *= 0.55f;
                }

                img.SetPixel(x, y, new Color(
                    Math.Clamp(mult / 1.6f, 0f, 1f),
                    Math.Clamp(glass, 0f, 1f),
                    Math.Clamp(rust, 0f, 1f)));
            }
        }
    }

    private static float GlassSoft(float u, float v, float v0, float v1)
    {
        // Soft edges so the glass band doesn't alias into a hard sticker.
        var fu = MathF.Min((u - 0.13f) / 0.05f, (0.87f - u) / 0.05f);
        var fv = MathF.Min((v - v0) / 0.012f, (v1 - v) / 0.012f);
        return Math.Clamp(MathF.Min(fu, fv), 0f, 1f);
    }

    private static float Hash(int n)
    {
        unchecked
        {
            n = (n << 13) ^ n;
            n = n * (n * n * 15731 + 789221) + 1376312589;
            return ((n & 0x7fffffff) % 100000) / 100000f;
        }
    }
}
