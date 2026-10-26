// Grass in meters (UV from CityGen): two shades in soft patches, fine blades and optional little flowers.
shader_type spatial;

#include "res://assets/materials/city/shaders/city_noise.gdshaderinc"

uniform float tint_strength : hint_range(0.0, 1.0) = 1.0;
uniform vec4 base_color : source_color = vec4(1.0);
// How far the patches swing lighter and darker.
uniform float patch_contrast : hint_range(0.0, 1.0) = 0.25;
uniform float patch_scale = 0.15;
uniform float blades : hint_range(0.0, 1.0) = 0.2;
uniform float flower_density : hint_range(0.0, 1.0) = 0.0;
uniform vec4 flower_a : source_color = vec4(1.0, 1.0, 0.95, 1.0);
uniform vec4 flower_b : source_color = vec4(1.0, 0.85, 0.3, 1.0);

void fragment() {
	vec2 uv = UV;
	vec3 tint = mix(vec3(1.0), city_linear(COLOR.rgb), tint_strength) * base_color.rgb;
	float n = city_fbm(uv * patch_scale);
	vec3 color = tint * (1.0 + (n - 0.5) * patch_contrast * 2.0);
	color *= mix(vec3(1.0), vec3(1.0, 1.08, 0.85), smoothstep(0.55, 0.8, n) * patch_contrast);
	color *= 1.0 + (city_noise(uv * vec2(70.0, 25.0)) - 0.5) * blades;

	if (flower_density > 0.0) {
		vec2 cell = floor(uv * 3.0);
		vec2 f = fract(uv * 3.0);
		float h = city_hash(cell);
		vec2 spot = vec2(city_hash(cell + 5.1), city_hash(cell + 9.7)) * 0.6 + 0.2;
		float dot_mask = 1.0 - smoothstep(0.04, 0.07, length(f - spot));
		float has = step(1.0 - flower_density, h);
		vec3 flower = mix(flower_a.rgb, flower_b.rgb, step(0.5, city_hash(cell + 2.3)));
		color = mix(color, flower, dot_mask * has);
	}

	ALBEDO = color;
	ROUGHNESS = 0.95;
	SPECULAR = 0.2;
}
