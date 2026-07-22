shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

// Arena fog-of-war: shades the out-of-bounds floor outside a "plus"-shaped clear region
// (main combat bowl + north/south start lanes). This is meant to be rendered on a plane that
// sits essentially coplanar with the arena floor; an elevated plane parallax-projects the
// opaque region across the screen from the tilted top-down camera (see ArenaWorld.SpawnArenaFogOfWar).
uniform vec4 fog_color : source_color = vec4(0.006, 0.006, 0.011, 0.9);
uniform float main_clear_half_width = 54.0;
uniform float main_clear_half_height = 54.0;
uniform float lane_clear_half_width = 15.25;
uniform float lane_clear_half_height = 85.5;
uniform float fade_distance = 3.0;
uniform float density_power = 0.26;
uniform float noise_scale = 0.16;
uniform float noise_strength = 0.02;

varying vec3 world_pos;

float hash12(vec2 p)
{
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float rect_sdf(vec2 p, vec2 half_size)
{
	vec2 d = abs(p) - half_size;
	return length(max(d, vec2(0.0))) + min(max(d.x, d.y), 0.0);
}

void vertex()
{
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment()
{
	vec2 p = world_pos.xz;
	float main_rect = rect_sdf(p, vec2(main_clear_half_width, main_clear_half_height));
	float lane_rect = rect_sdf(p, vec2(lane_clear_half_width, lane_clear_half_height));
	float union_distance = min(main_rect, lane_rect);
	float outside_distance = max(union_distance, 0.0);
	float alpha01 = smoothstep(0.0, max(0.001, fade_distance), outside_distance);
	alpha01 = pow(alpha01, max(0.02, density_power));
	alpha01 = smoothstep(0.02, 0.18, alpha01);

	vec2 noise_uv = floor(p * noise_scale);
	float n = hash12(noise_uv);
	alpha01 *= clamp(1.0 - noise_strength + (n * noise_strength * 2.0), 0.0, 1.0);

	ALBEDO = fog_color.rgb;
	ALPHA = fog_color.a * clamp(alpha01, 0.0, 1.0);
}
