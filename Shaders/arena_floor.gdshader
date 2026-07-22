shader_type spatial;

// Arena floor shader tuned for a high fixed top-down camera (eye distance ~37m).
// Layer order (bottom -> top):
//   1. tiled asphalt albedo + optional detail/roughness/macro-mask anti-tiling breakup
//   2. large-scale procedural patch variation (packed-dirt islands / oil-soaked zones),
//      per-city seeded so no two venues share a floor
//   3. painted arena markings: worn two-tone center circle + inner keyline, start-lane
//      hashes, dashed joust midline, gate-mouth hazard stripes, faded sponsor rings —
//      all cut by a noise paint-erosion mask so they read driven-over, never like decals
//   4. persistent wear: rubber-buildup orbit ring, arcing tire ruts, braking streaks
// Every new layer defaults to 0 strength, so legacy material paths render exactly as
// before. All variation keys off world XZ plus a per-city seed uniform — deterministic
// per city, no time/randomness. (Godot 4: no WORLD_POSITION; reconstruct from VERTEX.)

render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D albedo_tex : source_color;
uniform sampler2D detail_albedo_tex : source_color;
uniform sampler2D roughness_tex;
uniform sampler2D macro_mask_tex;
uniform float meters_per_tile = 4.0;
uniform float detail_meters_per_tile = 2.5;
uniform float macro_meters_per_tile = 40.0;
uniform float use_detail_tex = 0.0;
uniform float use_roughness_tex = 0.0;
uniform float use_macro_mask_tex = 0.0;
uniform vec4 tint : source_color = vec4(1.0);
uniform float roughness = 1.0;
uniform float metallic = 0.0;
uniform float detail_strength = 0.28;
uniform float macro_strength = 0.12;
uniform float grime_strength = 0.10;
uniform float oil_strength = 0.08;
uniform float patch_strength = 0.0;
uniform float macro_blend_strength = 0.15;

// ---- Large-scale albedo variation + persistent wear (0 = legacy flat look) ----
uniform float variation_seed = 0.0;
uniform float patch_variation_strength = 0.0;
uniform vec4 patch_light_tint : source_color = vec4(1.12, 1.08, 1.01, 1.0);
uniform vec4 patch_dark_tint : source_color = vec4(0.75, 0.74, 0.79, 1.0);
uniform float rubber_ring_strength = 0.0;
uniform float center_oil_strength = 0.0;
uniform float wear_strength = 0.0;

// ---- Painted arena markings (0 = none) ----
uniform float markings_strength = 0.0;
uniform float paint_wear = 0.5;
uniform vec4 paint_base_color : source_color = vec4(0.93, 0.90, 0.82, 0.88);
uniform vec4 paint_accent_color : source_color = vec4(0.95, 0.62, 0.18, 0.85);
// Off-center faded "sponsor" rings; positions supplied per city (rotated by city seed).
uniform vec2 sponsor_pos_a = vec2(-24.0, -14.0);
uniform vec2 sponsor_pos_b = vec2(21.0, -27.0);
uniform vec2 sponsor_pos_c = vec2(-9.0, 26.0);

// ---- In-bowl spectacle: value zones + stencil emblems + title gold (0 = legacy/salvage) ----
// Round 10 P0-1: from the fixed RTS camera the combat frame IS the floor, so the floor carries
// the stage composition — a lighter poured "center stage" disc, the untouched mid maneuvering
// field, and a darker outer service band hugging the walls. P0-3: emblem count/boldness and the
// gold title accents scale with the encounter tier (uniforms driven by ArenaWorld).
uniform float zone_strength = 0.0;        // 0-1 value-contrast floor zoning
uniform float emblem_strength = 0.0;      // 0-1 stenciled emblem paint weight
uniform float emblem_count = 0.0;         // 0-1 tier scalar gating which emblem slots are painted
uniform float title_gold_strength = 0.0;  // tier-5 title-fight gold center accents
uniform vec4 title_gold_color : source_color = vec4(1.0, 0.82, 0.38, 0.9);
// Round 11 N-1: perimeter value fill. The service band + unlit wall base went near-void at the
// frame edge from the fixed camera (~25% dead pixels in the most-seen combat frame). A dim
// wall-bounce gradient lifts the outer band's albedo and adds a faint cool ambient emission so
// the frame edge reads as dim venue, not void. Default 0 = legacy/salvage behavior (yard-safe).
uniform float perimeter_lift = 0.0;
uniform vec4 perimeter_glow_color : source_color = vec4(0.36, 0.40, 0.52, 1.0);
// 7-segment masks for the big "CTY-0T" city-code floor stencil (bits: 1=a top, 2=b top-right,
// 4=c bottom-right, 8=d bottom, 16=e bottom-left, 32=f top-left, 64=g middle).
uniform int stencil_ch0 = 0;
uniform int stencil_ch1 = 0;
uniform int stencil_ch2 = 0;
uniform int stencil_tier_digit = 1;       // 1..5, drawn as "-0T"

const int DIGIT_SEGS[10] = int[10](63, 6, 91, 79, 102, 109, 125, 7, 127, 111);

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

vec3 overlay_blend(vec3 base, vec3 blend) {
	vec3 lo = 2.0 * base * blend;
	vec3 hi = 1.0 - 2.0 * (1.0 - base) * (1.0 - blend);
	return mix(lo, hi, step(0.5, base));
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// 3-octave value noise: the "2-3 octave large-scale variation" workhorse.
float fbm3(vec2 p) {
	return 0.5 * vnoise(p)
		+ 0.3 * vnoise(p * 2.17 + vec2(13.7, 71.3))
		+ 0.2 * vnoise(p * 4.31 + vec2(47.9, 19.1));
}

// Anti-aliased inside-[lo,hi] band.
float band(float x, float lo, float hi, float aa) {
	return smoothstep(lo - aa, lo + aa, x) * (1.0 - smoothstep(hi - aa, hi + aa, x));
}

// Sponsor mark: thin outer ring + thin inner ring (text-free painted shape).
float ring_mark(vec2 wp, vec2 c, float rad) {
	float d = length(wp - c);
	float outer = band(d, rad - 0.26, rad + 0.26, 0.10);
	float inner = band(d, rad * 0.55 - 0.18, rad * 0.55 + 0.18, 0.10);
	return max(outer, inner);
}

// Anti-aliased axis-aligned stencil box (center c, half-size h).
float sbox(vec2 p, vec2 c, vec2 h, float aa) {
	return band(p.x, c.x - h.x, c.x + h.x, aa) * band(p.y, c.y - h.y, c.y + h.y, aa);
}

// 7-segment stencil glyph: p in meters from the glyph center, w/h = half width/height of the
// segment skeleton. Chunky strokes so the paint reads stenciled-on-concrete from 37m up.
// Round 11 N-3: strokes thickened + segment ends overlap (the old 1.05t end gaps read as dotted
// breaks under foreshortening), and one-sided glyphs ("I" = right bar only) draw centered so the
// empty half of their cell doesn't read as a word break ("PIt" -> "P It").
float seg7(vec2 p, float w, float h, int mask) {
	float t = w * 0.44;            // stroke half-thickness
	float aa = 0.07;
	if (mask == 6 || mask == 48)   // b+c or e+f: a lone vertical bar — center it in the cell
		return sbox(p, vec2(0.0, 0.0), vec2(t, h + t * 0.5), aa);
	float hw = w - t * 0.45;       // horizontal segment half-length (overlaps the verticals)
	float hh = (h - t * 0.45) * 0.5; // vertical segment half-length (upper/lower halves)
	float yv = h * 0.5;
	float m = 0.0;
	if ((mask & 1) != 0)  m = max(m, sbox(p, vec2(0.0, h), vec2(hw, t), aa));
	if ((mask & 64) != 0) m = max(m, sbox(p, vec2(0.0, 0.0), vec2(hw, t), aa));
	if ((mask & 8) != 0)  m = max(m, sbox(p, vec2(0.0, -h), vec2(hw, t), aa));
	if ((mask & 2) != 0)  m = max(m, sbox(p, vec2(w, yv), vec2(t, hh), aa));
	if ((mask & 4) != 0)  m = max(m, sbox(p, vec2(w, -yv), vec2(t, hh), aa));
	if ((mask & 32) != 0) m = max(m, sbox(p, vec2(-w, yv), vec2(t, hh), aa));
	if ((mask & 16) != 0) m = max(m, sbox(p, vec2(-w, -yv), vec2(t, hh), aa));
	return m;
}

// Triple chevron arrow pointing toward +u (u = toward arena center, v = lateral meters).
float chevron_mark(float u, float v) {
	float m = 0.0;
	float c = u + abs(v) * 0.85;
	for (int k = 0; k < 3; k++) {
		float base = 1.2 + float(k) * 2.3;
		m = max(m, band(c, base, base + 1.15, 0.12));
	}
	return m * step(abs(v), 5.0) * step(-1.0, u) * step(u, 9.5);
}

void fragment() {
	// Godot 4 spatial shaders don't expose WORLD_POSITION. Reconstruct from view-space VERTEX.
	vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;

	float base_mpt = max(meters_per_tile, 0.001);
	vec2 base_uv = world_pos.xz / base_mpt;
	vec3 color = texture(albedo_tex, base_uv).rgb * tint.rgb;
	float out_roughness = roughness;

	if (use_detail_tex > 0.5) {
		float detail_mpt = max(detail_meters_per_tile, 0.001);
		vec2 detail_uv = world_pos.xz / detail_mpt;
		vec3 detail = texture(detail_albedo_tex, detail_uv).rgb;
		color = mix(color, overlay_blend(color, detail), detail_strength);
	}

	// Tiny non-repeating tonal shift so the floor doesn't flatten out at distance.
	float tonal = mix(0.98, 1.02, hash12(floor(world_pos.xz / 6.0)));
	color *= tonal;

	if (use_roughness_tex > 0.5) {
		out_roughness = texture(roughness_tex, base_uv).r * roughness;
	}

	if (use_macro_mask_tex > 0.5) {
		float macro_mpt = max(macro_meters_per_tile, 0.001);
		vec2 macro_uv = world_pos.xz / macro_mpt;
		vec3 macro = texture(macro_mask_tex, macro_uv).rgb;
		float grime = macro.r;
		float oil = macro.g;
		float patch = macro.b;

		color *= mix(1.0, mix(0.985, 1.015, grime), macro_strength);
		color = mix(color, color * 0.92, grime * grime_strength);
		color = mix(color, vec3(0.08, 0.08, 0.085), oil * oil_strength);
		color = mix(color, color * 0.985, patch * patch_strength);

		out_roughness = clamp(
			out_roughness
			+ grime * 0.05
			- oil * 0.08
			+ patch * 0.02,
			0.35,
			1.0);
	}

	vec2 wp = world_pos.xz;
	vec2 seed2 = vec2(variation_seed, variation_seed * 1.31);
	float r = length(wp);
	float ax = abs(world_pos.x);
	float az = abs(world_pos.z);

	// 0) Sector construction panels: an 11m concrete-pour grid with per-panel tone shifts and
	//    thin recessed seams. This is the value-zone breakup the midfield was missing — at the
	//    fixed RTS altitude the noise layers below average into one flat brown (eval rounds 6-7).
	{
		vec2 cell_uv = wp / 11.0;
		vec2 cell_id = floor(cell_uv);
		// Quantized 3-step tone (-0.5 / 0 / +0.5) so panels read as distinct poured slabs at the
		// combat camera — the old continuous +/-4% hash averaged into featureless brown (round 9).
		float panel_tone = (floor(hash12(cell_id + seed2) * 3.0) - 1.0) * 0.5;
		color *= 1.0 + panel_tone * 0.22;
		vec2 cell_edge = abs(fract(cell_uv) - 0.5);
		float seam = smoothstep(0.450, 0.484, max(cell_edge.x, cell_edge.y));
		color *= 1.0 - seam * 0.30;
		out_roughness = clamp(out_roughness + seam * 0.05 + panel_tone * 0.03, 0.30, 1.0);
	}

	// 0.5) Spectacle value zones (stadium venues; round 10 P0-1). The RTS camera frames ~70m of
	//      floor, so only structure at this scale can read as STAGE instead of parking lot:
	//      a lighter poured-concrete "center stage" disc enclosing the orbit-fight ring, the
	//      untouched mid maneuvering field, and a darker outer service band along the walls.
	float stage_keyline = 0.0;
	if (zone_strength > 0.001) {
		float edge_wob = (fbm3(wp * 0.05 + seed2 * 7.3) - 0.5) * 5.0;
		float stage_r = r + edge_wob;
		float stage = 1.0 - smoothstep(16.0, 19.5, stage_r);
		// Pour-brightness noise so the stage reads as poured slabs, not one flat sticker.
		float pour = 0.90 + 0.20 * fbm3(wp * 0.16 + seed2 * 9.1);
		color = mix(color, color * vec3(1.28, 1.26, 1.20) * pour, stage * zone_strength);
		// Service band runs parallel to the walls (square metric), darker + slightly cooler.
		float sq = max(ax, az) + edge_wob * 0.6;
		float service = smoothstep(38.5, 43.0, sq);
		color = mix(color, color * vec3(0.62, 0.62, 0.68), service * zone_strength);
		out_roughness = clamp(out_roughness - stage * 0.10 * zone_strength + service * 0.05 * zone_strength, 0.25, 1.0);
		// Painted keyline where the stage pour ends (fed into the eroded paint pass below).
		stage_keyline = band(stage_r, 19.9, 20.45, 0.10) * zone_strength;
	}

	// 0.6) Perimeter value fill (round 11 N-1): dim wall-bounce gradient over the outer band so
	//      the frame edge holds a value story between the light pools — dim venue, never void.
	//      Noise-broken so it reads as bounced houselight, not a painted ring. The faint cool
	//      emission keeps a floor under the darkest corners regardless of light coverage.
	float perim_emission = 0.0;
	if (perimeter_lift > 0.001) {
		float perim_sq = max(ax, az) + (fbm3(wp * 0.05 + seed2 * 7.3) - 0.5) * 4.0;
		float bounce = smoothstep(30.0, 44.0, perim_sq);
		float bn = 0.72 + 0.28 * fbm3(wp * 0.045 + seed2 * 3.1);
		color *= 1.0 + bounce * bn * 0.34 * perimeter_lift;
		perim_emission = bounce * bn * perimeter_lift;
	}

	// 1) Large-scale patch variation: lighter packed-dirt islands + darker oil-soaked zones.
	if (patch_variation_strength > 0.001) {
		float patches = fbm3(wp * 0.055 + seed2);
		float light_mask = smoothstep(0.56, 0.74, patches);
		float dark_mask = 1.0 - smoothstep(0.30, 0.46, patches);
		// Oil-soaked ground pools densest toward the center where fights end.
		dark_mask *= 0.75 + 0.5 * (1.0 - smoothstep(10.0, 40.0, r));
		color = mix(color, color * patch_light_tint.rgb, light_mask * patch_variation_strength);
		color = mix(color, color * patch_dark_tint.rgb, clamp(dark_mask, 0.0, 1.0) * patch_variation_strength);
		out_roughness = clamp(out_roughness + (light_mask - dark_mask) * 0.05 * patch_variation_strength, 0.30, 1.0);
	}

	// 2) Oil-soaked pit at arena center. Soft-edged wet stain with a faint slick sheen —
	//    the hard-edged near-black ellipse read as a hole in the world (eval round 4 P0).
	if (center_oil_strength > 0.001) {
		float oil_noise = 0.55 + 0.45 * vnoise(wp * 0.45 + seed2 * 1.7);
		float oil_zone = (1.0 - smoothstep(1.2, 9.0, r)) * oil_noise;
		// Wet brown-black, not void-black; keep some albedo so light pools read on it.
		color = mix(color, vec3(0.105, 0.095, 0.085), oil_zone * center_oil_strength);
		// Iridescent slick rim: a faint violet-blue band near the stain edge.
		float rim = smoothstep(4.5, 7.0, r) * (1.0 - smoothstep(7.0, 8.8, r)) * oil_noise;
		color = mix(color, vec3(0.16, 0.13, 0.22), rim * center_oil_strength * 0.55);
		// Glossy where soaked: the corner light pools catch a specular sheen on the slick.
		out_roughness = clamp(out_roughness - oil_zone * center_oil_strength * 0.55, 0.12, 1.0);
	}

	// 3) Persistent wear masks. Computed before paint (so paint erodes where cars drive),
	//    applied after paint (so rubber/ruts darken the paint too).
	float rubber = 0.0;
	if (rubber_ring_strength > 0.001) {
		// Rubber buildup where orbit fights happen (~8-16m radius), noise-broken.
		float ring_zone = smoothstep(7.5, 10.0, r) * (1.0 - smoothstep(14.0, 17.5, r));
		float ring_noise = 0.35 + 0.65 * fbm3(wp * 0.19 + seed2 * 2.9);
		rubber = ring_zone * ring_noise * rubber_ring_strength;
	}
	float ruts = 0.0;
	if (wear_strength > 0.001) {
		// Orbit ruts: concentric grooves with radius wobble + angular gaps.
		float rr = r + (vnoise(wp * 0.21 + seed2 * 4.3) - 0.5) * 2.6;
		float groove = 1.0 - smoothstep(0.14, 0.55, abs(fract(rr / 3.3) - 0.5) * 3.3);
		groove *= smoothstep(8.0, 10.0, r) * (1.0 - smoothstep(17.5, 21.5, r));
		groove *= smoothstep(0.34, 0.62, vnoise(wp * 0.11 + seed2 * 5.7));
		// Braking streaks running out of the launch lanes into the bowl.
		float cell = (world_pos.x + 12.0) / 1.7;
		float streak_rand = hash12(vec2(floor(cell), variation_seed + step(0.0, world_pos.z) * 31.0));
		float streak_core = 1.0 - smoothstep(0.12, 0.34, abs(fract(cell) - 0.5));
		float streak_span = smoothstep(13.0, 24.0, az) * (1.0 - smoothstep(47.0, 52.5, az));
		float streak_len = smoothstep(0.30, 0.62, vnoise(vec2(world_pos.x * 0.83, world_pos.z * 0.17) + seed2 * 6.1));
		float streaks = streak_core * step(0.42, streak_rand) * streak_span * streak_len * step(ax, 12.5);
		ruts = clamp(groove * 0.9 + streaks, 0.0, 1.0) * wear_strength;
	}

	// 4) Painted markings, all alpha-eroded. World-space, so the three floor bodies
	//    (main bowl + both start boxes) paint one continuous scheme with no seams.
	if (markings_strength > 0.001) {
		float paint_accent = 0.0;
		float paint_base = 0.0;

		// Worn two-tone center circle: accent ring, base-white inner keyline, accent hub.
		paint_accent += band(r, 7.05, 8.05, 0.09);
		paint_base += band(r, 6.30, 6.62, 0.07);
		paint_accent += (1.0 - smoothstep(1.05, 1.30, r)) * 0.9;

		// Hazard chevron collar just outside the center ring (24 alternating segments). The stripe
		// phase is skewed with radius so each segment angles like an arrow instead of reading as a
		// plain square patch (round 9: "collar stripes read as squares").
		float mark_ang = atan(world_pos.z, world_pos.x);
		float chev_t = abs(fract(mark_ang * 3.8197186 + (r - 8.45) * 0.55) - 0.5) * 2.0;
		paint_accent += band(r, 8.45, 9.35, 0.10) * smoothstep(0.40, 0.58, chev_t) * 0.55;

		// Painted sector spokes: four heavily-faded DIAGONAL lines only (the 8-spoke version
		// at 0.40 weight rendered a giant white asterisk across the bowl — worse than the
		// flatness it fixed). Diagonals avoid stacking on the joust midline.
		float spoke_a = abs(mod(mark_ang, 1.5707963) - 0.7853982);
		float spoke_d = spoke_a * r;
		float spoke = (1.0 - smoothstep(0.08, 0.24, spoke_d))
			* smoothstep(15.0, 18.0, r) * (1.0 - smoothstep(30.0, 36.0, r));
		paint_base += spoke * 0.14;

		// Start-lane threshold hashes: same 7 positions/size as the old mesh hashes at
		// z = +/-48.5 (x = -10.8 + i*3.6, 1.9m wide) — the start-zone meaning stays put.
		float hash_cell = abs(fract((world_pos.x + 10.8) / 3.6 + 0.5) - 0.5);
		float hash_x = (1.0 - smoothstep(0.235, 0.29, hash_cell)) * step(ax, 12.7);
		paint_base += hash_x * band(az, 48.29, 48.71, 0.05);

		// Dashed worn midline down the joust lane (clear of the center circle).
		float dash_t = abs(fract(world_pos.z / 4.4) - 0.5) * 2.0;
		float mid = (1.0 - smoothstep(0.16, 0.30, ax)) * (1.0 - smoothstep(0.58, 0.70, dash_t));
		mid *= smoothstep(9.5, 11.0, az) * (1.0 - smoothstep(45.5, 47.0, az));
		paint_base += mid;

		// Radial hazard stripes at the four gate mouths (N/S lane mouths at z ~ +/-52,
		// E/W service-wall bases at x ~ +/-52) plus the south exit apron in the start box.
		float stripe_t = abs(fract((world_pos.x + world_pos.z) / 3.4) - 0.5) * 2.0;
		float diag = smoothstep(0.45, 0.60, stripe_t);
		float ns_mouth = step(ax, 14.6) * band(az, 50.6, 53.6, 0.18);
		float ew_mouth = step(az, 8.0) * band(ax, 50.6, 53.6, 0.18);
		float exit_apron = step(ax, 5.4) * band(world_pos.z, 78.8, 80.4, 0.12);
		paint_accent += (ns_mouth + ew_mouth + exit_apron) * diag;

		// Faded sponsor-style rings at off-center spots (text-free shapes). Mostly neutral paint
		// at low weight with varied radii — at the old 0.45-0.55 accent weights the identical
		// double-rings read as glowing orange donuts stamped across the floor (eval round 6).
		// The rings rotate per city, so they can land on the fixed city-code stencils — clear a
		// box around each stencil or the code reads as glyph soup (round 10 iteration 1).
		float code_clear = 1.0;
		if (emblem_strength > 0.001) {
			code_clear -= sbox(wp, vec2(13.0, -26.0), vec2(11.2, 3.3), 0.5);
			code_clear -= sbox(wp, vec2(-13.0, 26.0), vec2(11.2, 3.3), 0.5);
			code_clear = clamp(code_clear, 0.0, 1.0);
		}
		paint_accent += ring_mark(wp, sponsor_pos_a, 4.6) * 0.30 * code_clear;
		paint_base += ring_mark(wp, sponsor_pos_b, 3.3) * 0.28 * code_clear;
		paint_base += ring_mark(wp, sponsor_pos_c, 2.6) * 0.24 * code_clear;

		// Stage keyline (pour edge from the value-zone pass), eroded like all paint.
		paint_base += stage_keyline * 0.45;

		// ---- Stenciled floor emblems (round 10 P0-1/P0-3). emblem_count gates slots in tier
		//      order so a tier-1 pit carries one faded code stencil while a tier-5 headline
		//      venue is fully dressed; emblem_strength scales the paint weight itself. ----
		if (emblem_strength > 0.001) {
			// Big "CTY-0T" city-code stencils, one per half-field, reading upright from the
			// fixed south camera (glyph top faces -Z). Round 11 N-3: pitch tightened 3.6 -> 3.35m
			// and paint weight raised so each code reads as ONE token under foreshortening.
			for (int side = 0; side < 2; side++) {
				vec2 anchor = side == 0 ? vec2(13.0, -26.0) : vec2(-13.0, 26.0);
				vec2 sp = vec2(wp.x - anchor.x, -(wp.y - anchor.y));
				float code = 0.0;
				code = max(code, seg7(sp - vec2(-7.9, 0.0), 1.05, 2.05, stencil_ch0));
				code = max(code, seg7(sp - vec2(-4.55, 0.0), 1.05, 2.05, stencil_ch1));
				code = max(code, seg7(sp - vec2(-1.2, 0.0), 1.05, 2.05, stencil_ch2));
				code = max(code, sbox(sp, vec2(1.42, 0.0), vec2(0.55, 0.36), 0.07)); // dash
				code = max(code, seg7(sp - vec2(4.05, 0.0), 1.05, 2.05, DIGIT_SEGS[0]));
				code = max(code, seg7(sp - vec2(7.4, 0.0), 1.05, 2.05, DIGIT_SEGS[clamp(stencil_tier_digit, 0, 9)]));
				paint_base += code * 0.72 * emblem_strength;
			}

			// Worn chevron arrows in the E/W mid-field pointing at the stage (tier 2+).
			if (emblem_count >= 0.20) {
				paint_accent += chevron_mark(30.0 - wp.x, wp.y + 4.0) * 0.55 * emblem_strength;
				paint_accent += chevron_mark(wp.x + 30.0, wp.y - 4.0) * 0.55 * emblem_strength;
			}

			// Lane numerals 1-7 behind the start-lane threshold hashes (tier 3+). Same 3.6m
			// pitch as the hashes so each number tags its lane. Glyph top faces NORTH on both
			// rows so they read upright from the fixed south camera (the earlier abs() form
			// flipped the north row upside down — screenshot-verified glyph soup).
			if (emblem_count >= 0.45) {
				for (int lane = 0; lane < 7; lane++) {
					float lx = -10.8 + float(lane) * 3.6;
					vec2 np = vec2(wp.x - lx, sign(wp.y) * 46.1 - wp.y);
					paint_base += seg7(np, 0.62, 1.15, DIGIT_SEGS[lane + 1]) * 0.55 * emblem_strength;
				}
			}

			// Broadcast-style corner brackets framing the stage pour (tier 4+).
			if (emblem_count >= 0.70) {
				for (int cx = 0; cx < 2; cx++) {
					for (int cz = 0; cz < 2; cz++) {
						vec2 bc = vec2(cx == 0 ? -14.6 : 14.6, cz == 0 ? -14.6 : 14.6);
						vec2 bp = (wp - bc) * vec2(cx == 0 ? 1.0 : -1.0, cz == 0 ? 1.0 : -1.0);
						float bracket = sbox(bp, vec2(1.15, 0.0), vec2(1.5, 0.30), 0.07)
							+ sbox(bp, vec2(0.0, 1.15), vec2(0.30, 1.5), 0.07);
						paint_accent += min(bracket, 1.0) * 0.50 * emblem_strength;
					}
				}
			}
		}

		// ---- Tier-5 "title fight" gold: extra center-ring bands + crown ticks. Painted after
		//      the base scheme so gold sits ON the concrete, eroded like everything else. ----
		float paint_gold = 0.0;
		if (title_gold_strength > 0.001) {
			paint_gold += band(r, 5.30, 5.95, 0.08);   // title ring inside the white keyline
			paint_gold += band(r, 1.78, 2.30, 0.07);   // collar around the accent hub
			float tick_t = abs(fract(mark_ang * 1.9098593) - 0.5) * 2.0; // 12 crown ticks
			paint_gold += band(r, 4.50, 5.05, 0.09) * smoothstep(0.58, 0.78, tick_t) * 0.85;
		}

		// Sparse painted wear decals on the 11m construction grid: ~1 in 5 panels carries ONE faded
		// mark centered in its cell — stencil bay bars, a small accent square outline, or an oil
		// blotch (albedo darkening, applied after `keep` below) — so midfield slabs read as a worked
		// work surface. Gated off the center-circle furniture (cell centers inside ~13m stay clean).
		float wear_blotch = 0.0;
		{
			vec2 dcell_uv = wp / 11.0;
			vec2 dcell_id = floor(dcell_uv);
			vec2 dm = (fract(dcell_uv) - 0.5) * 11.0; // meters from the panel center
			float dgate = step(13.0, length((dcell_id + 0.5) * 11.0));
			float dh = hash12(dcell_id * 1.73 + seed2 + 41.7);
			if (dh > 0.80 && dgate > 0.5) {
				// Alternate bar orientation per cell so the bay lines don't all run one way.
				if (hash12(dcell_id + seed2 + 3.3) > 0.5) { dm = dm.yx; }
				float dvar = floor(hash12(dcell_id + seed2 * 2.9 + 17.3) * 3.0);
				if (dvar < 0.5) {
					// Stencil "bay lines": two short parallel base-paint bars.
					float bars = band(dm.x, -1.5, 1.5, 0.06)
						* (band(dm.y, -0.62, -0.30, 0.05) + band(dm.y, 0.30, 0.62, 0.05));
					paint_base += bars * 0.42;
				} else if (dvar < 1.5) {
					// Small accent square outline.
					float sq_o = band(dm.x, -1.0, 1.0, 0.05) * band(dm.y, -1.0, 1.0, 0.05);
					float sq_i = band(dm.x, -0.74, 0.74, 0.05) * band(dm.y, -0.74, 0.74, 0.05);
					paint_accent += max(sq_o - sq_i, 0.0) * 0.38;
				} else {
					// Oil-stain blotch: darkening worn into the slab, noise-ragged edge.
					vec2 doff = vec2(hash12(dcell_id + 5.1), hash12(dcell_id + 9.7)) - 0.5;
					float dd = length(dm - doff * 2.4);
					wear_blotch = (1.0 - smoothstep(0.8, 2.1, dd))
						* (0.45 + 0.55 * vnoise(wp * 0.7 + seed2 * 2.1));
				}
			}
		}

		// Paint erosion: noise chips plus extra loss where rubber/ruts say cars drive.
		float chips = fbm3(wp * 0.85 + seed2 * 3.7);
		float keep = smoothstep(paint_wear - 0.28, paint_wear + 0.22, chips);
		keep = 0.30 + 0.70 * keep; // never fully erase — start zones must stay legible
		keep *= 1.0 - rubber * 0.55;
		keep *= 1.0 - ruts * 0.45;

		// Oil-blotch wear decal: albedo darkening (not paint), eroded like everything else.
		color = mix(color, color * vec3(0.60, 0.585, 0.555), wear_blotch * keep * markings_strength * 0.85);

		paint_accent = min(clamp(paint_accent, 0.0, 1.0) * keep * markings_strength * paint_accent_color.a, 1.0);
		paint_base = min(clamp(paint_base, 0.0, 1.0) * keep * markings_strength * paint_base_color.a, 1.0);
		paint_gold = min(clamp(paint_gold, 0.0, 1.0) * keep * markings_strength * title_gold_strength * title_gold_color.a, 1.0);

		color = mix(color, paint_accent_color.rgb, paint_accent);
		color = mix(color, paint_base_color.rgb, paint_base);
		color = mix(color, title_gold_color.rgb, paint_gold);
		out_roughness = mix(out_roughness, 0.62, max(paint_accent, paint_base) * 0.7);
		// Gold title paint is glossier than the utility scheme — it catches the pool lights.
		out_roughness = mix(out_roughness, 0.42, paint_gold * 0.8);
	}

	// 5) Rubber and ruts go over everything — they darken the worn paint as well.
	color = mix(color, color * vec3(0.42, 0.42, 0.46), rubber);
	color = mix(color, color * vec3(0.50, 0.50, 0.54), ruts);
	out_roughness = clamp(out_roughness - rubber * 0.12 - ruts * 0.08, 0.30, 1.0);

	ALBEDO = color;
	ROUGHNESS = out_roughness;
	METALLIC = metallic;
	// Perimeter wall-bounce ambient (N-1). Far below the glow HDR threshold — a value floor for
	// the outer band, never a bloom source. Zero whenever perimeter_lift is 0 (yards/legacy).
	EMISSION = perimeter_glow_color.rgb * (perim_emission * 0.055);
}
