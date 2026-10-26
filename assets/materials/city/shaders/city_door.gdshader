// One door per panel (UV 0..1 from CityGen; UV2.x random per door): frame, two raised panels or a glass top,
// a handle. The colour is the vertex colour, or one of a few picked per door.
shader_type spatial;

#include "res://assets/materials/city/shaders/city_noise.gdshaderinc"

uniform vec4 frame_color : source_color = vec4(0.95, 0.95, 0.92, 1.0);
uniform float frame_width : hint_range(0.0, 0.3) = 0.07;
// 0 = the style's door colour, 1 = always one from the palette.
uniform float palette_mix : hint_range(0.0, 1.0) = 0.0;
uniform vec4 door_a : source_color = vec4(0.85, 0.25, 0.25, 1.0);
uniform vec4 door_b : source_color = vec4(0.2, 0.45, 0.75, 1.0);
uniform vec4 door_c : source_color = vec4(0.2, 0.55, 0.4, 1.0);
uniform vec4 door_d : source_color = vec4(0.95, 0.75, 0.3, 1.0);
uniform float glass_chance : hint_range(0.0, 1.0) = 0.4;
uniform vec4 glass_color : source_color = vec4(0.4, 0.55, 0.7, 1.0);
uniform vec4 handle_color : source_color = vec4(0.85, 0.75, 0.4, 1.0);
uniform float wood_grain : hint_range(0.0, 0.5) = 0.0;

float box(vec2 p, vec2 lo, vec2 hi) {
	vec2 aa = fwidth(p) * 0.75;
	vec2 a = smoothstep(lo - aa, lo + aa, p) * (1.0 - smoothstep(hi - aa, hi + aa, p));
	return a.x * a.y;
}

void fragment() {
	vec2 uv = UV;
	float r = UV2.x;
	vec3 base = city_linear(COLOR.rgb);
	float pick = fract(r * 4.7);
	vec3 paint = pick < 0.25 ? door_a.rgb : (pick < 0.5 ? door_b.rgb : (pick < 0.75 ? door_c.rgb : door_d.rgb));
	base = mix(base, paint, step(1.0 - palette_mix, fract(r * 2.3)));
	base *= 1.0 + (city_noise(vec2(uv.x * 40.0, uv.y * 3.0)) - 0.5) * wood_grain;

	float leaf = box(uv, vec2(frame_width, frame_width), vec2(1.0 - frame_width, 1.0));
	vec3 color = mix(frame_color.rgb, base, leaf);
	float rough = 0.6;
	float specular = 0.3;

	bool glazed = fract(r * 9.1) < glass_chance;
	float top = glazed ? box(uv, vec2(0.22, 0.12), vec2(0.78, 0.42)) : box(uv, vec2(0.22, 0.12), vec2(0.78, 0.45));
	float bottom = box(uv, vec2(0.22, 0.55), vec2(0.78, 0.9));
	if (glazed) {
		color = mix(color, glass_color.rgb * (1.1 - uv.y), top);
		rough = mix(rough, 0.1, top);
		specular = mix(specular, 0.7, top);
	} else {
		color *= 1.0 - top * 0.18;
	}

	color *= 1.0 - bottom * 0.18;
	float handle = 1.0 - smoothstep(0.025, 0.035, length((uv - vec2(0.84, 0.55)) * vec2(1.0, 0.55)));
	color = mix(color, handle_color.rgb, handle);

	ALBEDO = color;
	ROUGHNESS = mix(rough, 0.3, handle);
	METALLIC = handle * 0.8;
	SPECULAR = specular;
}
