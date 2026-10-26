// Road surface in meters (UV from CityGen): grain, patches, cracks and tar seams, tinted by the vertex colour.
shader_type spatial;

#include "res://assets/materials/city/shaders/city_noise.gdshaderinc"

uniform float tint_strength : hint_range(0.0, 1.0) = 1.0;
uniform vec4 base_color : source_color = vec4(1.0);
// Light stones in the surface.
uniform float speckle : hint_range(0.0, 1.0) = 0.3;
uniform float speckle_density : hint_range(0.0, 1.0) = 0.12;
// Lighter and darker repaired patches.
uniform float patches : hint_range(0.0, 1.0) = 0.15;
uniform float patch_scale = 0.08;
// Dark tar lines filling cracks.
uniform float cracks : hint_range(0.0, 1.0) = 0.0;
uniform float crack_scale = 0.25;
uniform float roughness_value : hint_range(0.0, 1.0) = 0.85;

void fragment() {
	vec2 uv = UV;
	vec3 tint = mix(vec3(1.0), city_linear(COLOR.rgb), tint_strength) * base_color.rgb;
	vec3 color = tint;

	float blotch = city_fbm(uv * patch_scale);
	color *= 1.0 + (blotch - 0.5) * patches * 1.6;

	vec2 grain_cell = floor(uv * 45.0);
	float stone = step(1.0 - speckle_density, city_hash(grain_cell));
	color = mix(color, color * 1.7 + 0.03, stone * speckle);
	color *= 1.0 + (city_noise(uv * 60.0) - 0.5) * 0.12;

	if (cracks > 0.0) {
		float n = city_fbm(uv * crack_scale + 3.0);
		float line = 1.0 - smoothstep(0.0, 0.012, abs(n - 0.5));
		float mask = smoothstep(0.5, 0.7, city_noise(uv * crack_scale * 0.5 + 9.0));
		color *= 1.0 - line * mask * cracks * 0.7;
	}

	ALBEDO = color;
	ROUGHNESS = roughness_value;
	SPECULAR = 0.25;
}
