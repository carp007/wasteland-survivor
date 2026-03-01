shader_type spatial;

// World-space triplanar shader for procedural boxes (walls/obstacles).
// Uses fract() so tiling works even if texture repeat flags are disabled.

render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D albedo_tex : source_color;
uniform float meters_per_tile = 4.0;
uniform float sharpness = 2.0;
uniform vec4 tint : source_color = vec4(1.0);
uniform float roughness = 1.0;
uniform float metallic = 0.0;

vec3 sample_triplanar(vec3 world_pos, vec3 world_nrm) {
	float mpt = max(meters_per_tile, 0.001);

	vec3 n = abs(normalize(world_nrm));
	n = pow(n, vec3(sharpness));
	n /= max(n.x + n.y + n.z, 0.00001);

	vec2 uvx = fract(world_pos.zy / mpt);
	vec2 uvy = fract(world_pos.xz / mpt);
	vec2 uvz = fract(world_pos.xy / mpt);

	vec3 cx = texture(albedo_tex, uvx).rgb;
	vec3 cy = texture(albedo_tex, uvy).rgb;
	vec3 cz = texture(albedo_tex, uvz).rgb;

	return cx * n.x + cy * n.y + cz * n.z;
}

void fragment() {
	// Godot 4 spatial shaders don't expose WORLD_POSITION. We reconstruct fragment world position
	// from view-space VERTEX using the built-in inverse view matrix.
	vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 world_nrm = normalize(mat3(INV_VIEW_MATRIX) * NORMAL);

	vec3 col = sample_triplanar(world_pos, world_nrm);
	ALBEDO = col * tint.rgb;
	ROUGHNESS = roughness;
	METALLIC = metallic;
}
