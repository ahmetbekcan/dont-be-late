// Tiles, slabs, bricks or plaster bands in meters (UV from CityGen), tinted by the vertex colour.
shader_type spatial;
render_mode specular_schlick_ggx;

#include "res://assets/materials/city/shaders/city_noise.gdshaderinc"

uniform vec4 base_color : source_color = vec4(1.0);
// 1 = tint by the style's colour (vertex colour), 0 = base colour only.
uniform float tint_strength : hint_range(0.0, 1.0) = 1.0;
uniform vec2 tile_size = vec2(0.6, 0.6);
// Shift of every other row, as a fraction of a tile (0.5 for a brick bond).
uniform float row_offset : hint_range(0.0, 1.0) = 0.0;
uniform vec2 uv_offset = vec2(0.0);
uniform float grout_width : hint_range(0.0, 0.2) = 0.02;
// Grout colour as a shade of the tile colour.
uniform float grout_shade : hint_range(0.0, 2.0) = 0.7;
// Darkening toward the grout, like a bevel.
uniform float edge_shade : hint_range(0.0, 1.0) = 0.15;
uniform float edge_width : hint_range(0.001, 0.3) = 0.05;
// Per-tile brightness change.
uniform float tile_variation : hint_range(0.0, 0.5) = 0.08;
// Wavy grout lines, in meters, for a hand-drawn look.
uniform float wobble : hint_range(0.0, 0.1) = 0.0;
// Large blotches of dirt.
uniform float grime : hint_range(0.0, 1.0) = 0.1;
uniform float grime_scale = 0.35;
// Rain streaks running down (walls).
uniform float streaks : hint_range(0.0, 1.0) = 0.0;
// Fine grain.
uniform float grain : hint_range(0.0, 0.5) = 0.05;
uniform float roughness_value : hint_range(0.0, 1.0) = 0.9;

void fragment() {
	vec2 uv = UV + uv_offset;
	vec3 tint = mix(vec3(1.0), city_linear(COLOR.rgb), tint_strength) * base_color.rgb;

	vec2 p = uv;
	if (wobble > 0.0) {
		p += (vec2(city_noise(uv * 2.7), city_noise(uv * 2.7 + 11.0)) - 0.5) * wobble * 2.0;
	}

	vec2 cell = p / tile_size;
	float row = floor(cell.y);
	cell.x += row_offset * mod(row, 2.0);
	vec2 id = floor(cell);
	vec2 f = fract(cell);
	vec2 to_edge = min(f, 1.0 - f) * tile_size;
	float edge = min(to_edge.x, to_edge.y);
	float aa = max(fwidth(edge), 1e-4);
	float grout = 1.0 - smoothstep(grout_width * 0.5 - aa, grout_width * 0.5 + aa, edge);
	float bevel = 1.0 - smoothstep(grout_width * 0.5, grout_width * 0.5 + edge_width, edge);

	vec3 tile = tint * (1.0 + (city_hash(id + 0.37) - 0.5) * 2.0 * tile_variation);
	tile *= 1.0 - bevel * edge_shade;
	tile *= 1.0 + (city_noise(uv * 23.0) - 0.5) * grain;

	vec3 color = mix(tile, tint * grout_shade, grout);
	float dirt = smoothstep(0.45, 0.85, city_fbm(uv * grime_scale)) * grime;
	float streak = smoothstep(0.55, 0.9, city_noise(vec2(uv.x * 3.1, uv.y * 0.12))) * streaks;
	color *= 1.0 - clamp(dirt + streak, 0.0, 0.8) * 0.45;

	ALBEDO = color;
	ROUGHNESS = roughness_value;
	SPECULAR = 0.3;
}
