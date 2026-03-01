shader_type spatial;

// Simple world-space tiled floor shader.
// Uses WORLD_POSITION so we don't depend on mesh UVs or texture repeat flags.

render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D albedo_tex : source_color;
uniform float meters_per_tile = 6.0;
uniform vec4 tint : source_color = vec4(1.0);
uniform float roughness = 1.0;
uniform float metallic = 0.0;

void fragment() {
	// Godot 4 spatial shaders don't expose WORLD_POSITION. We reconstruct fragment world position
	// from view-space VERTEX using the built-in inverse view matrix.
	vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;

	float mpt = max(meters_per_tile, 0.001);
	vec2 uv = world_pos.xz / mpt;
	uv = fract(uv);

	vec4 tex = texture(albedo_tex, uv);
	ALBEDO = tex.rgb * tint.rgb;
	ROUGHNESS = roughness;
	METALLIC = metallic;
}
