// One window per panel (UV 0..1 from CityGen; UV2.x random per window, UV2.y per building): frame, panes,
// sky reflection, curtains or blinds, and some lit from inside. The vertex colour tints the glass.
shader_type spatial;

#include "res://assets/materials/city/shaders/city_noise.gdshaderinc"

uniform vec4 frame_color : source_color = vec4(0.97, 0.97, 0.95, 1.0);
// Mix of the frame toward a colour picked per building.
uniform float frame_variety : hint_range(0.0, 1.0) = 0.0;
uniform vec4 frame_alt_a : source_color = vec4(0.2, 0.22, 0.3, 1.0);
uniform vec4 frame_alt_b : source_color = vec4(0.55, 0.35, 0.22, 1.0);
uniform float frame_width : hint_range(0.0, 0.3) = 0.08;
uniform float bar_width : hint_range(0.0, 0.2) = 0.035;
// Panes across and up (a bar between each).
uniform int panes_across = 2;
uniform int panes_up = 1;
// A ledge under the window.
uniform float sill : hint_range(0.0, 0.3) = 0.0;
uniform vec4 sill_color : source_color = vec4(0.85, 0.85, 0.82, 1.0);

uniform vec4 sky_top : source_color = vec4(0.75, 0.88, 1.0, 1.0);
uniform vec4 sky_bottom : source_color = vec4(0.25, 0.4, 0.6, 1.0);
uniform float tint_strength : hint_range(0.0, 1.0) = 0.5;
uniform float glare : hint_range(0.0, 1.0) = 0.35;

uniform float curtain_chance : hint_range(0.0, 1.0) = 0.4;
uniform float blind_chance : hint_range(0.0, 1.0) = 0.2;
uniform vec4 curtain_a : source_color = vec4(0.95, 0.45, 0.4, 1.0);
uniform vec4 curtain_b : source_color = vec4(0.98, 0.85, 0.45, 1.0);
uniform vec4 curtain_c : source_color = vec4(0.5, 0.75, 0.6, 1.0);
uniform vec4 blind_color : source_color = vec4(0.93, 0.9, 0.82, 1.0);

uniform float lit_chance : hint_range(0.0, 1.0) = 0.12;
uniform vec4 lit_color : source_color = vec4(1.0, 0.85, 0.55, 1.0);
uniform float lit_energy : hint_range(0.0, 8.0) = 1.2;
// At night (sky_night from the SkyCycle) more rooms are lit, they glow brighter and dark glass reflects less sky.
uniform float lit_chance_night : hint_range(0.0, 1.0) = 0.55;
uniform float lit_energy_night : hint_range(0.0, 8.0) = 3.0;
uniform float glass_night : hint_range(0.0, 1.0) = 0.12;
global uniform float sky_night;

float box(vec2 p, vec2 lo, vec2 hi) {
	vec2 aa = fwidth(p) * 0.75;
	vec2 a = smoothstep(lo - aa, lo + aa, p) * (1.0 - smoothstep(hi - aa, hi + aa, p));
	return a.x * a.y;
}

void fragment() {
	vec2 uv = UV;
	float r = UV2.x;
	float b = UV2.y;
	float r_curtain = fract(r * 7.31);
	float r_lit = fract(r * 13.17 + b * 3.1);
	float r_open = fract(r * 3.77);

	// Glass: sky fading down, a diagonal glare and the style's window colour.
	vec3 glass = mix(sky_top.rgb, sky_bottom.rgb, smoothstep(0.0, 1.0, uv.y));
	glass = mix(glass, glass * city_linear(COLOR.rgb) * 1.6, tint_strength);
	float streak = smoothstep(0.08, 0.0, abs(fract(uv.x * 0.9 + uv.y * 0.6 + r) - 0.5) - 0.12);
	glass += vec3(streak * glare * 0.35);
	glass *= mix(1.0, glass_night, sky_night);
	vec3 emission = vec3(0.0);
	float rough = 0.08;

	bool lit = r_lit < mix(lit_chance, lit_chance_night, sky_night);
	if (lit) {
		glass = mix(glass * 0.4, lit_color.rgb, 0.85);
		emission = lit_color.rgb * mix(lit_energy, lit_energy_night, sky_night);
	}

	// Curtains hang from both sides; blinds come down from the top.
	vec3 inside = glass;
	float cover = 0.0;
	vec3 cloth = curtain_a.rgb;
	if (r_curtain < curtain_chance) {
		float pick = fract(r * 5.3 + b);
		cloth = pick < 0.33 ? curtain_a.rgb : (pick < 0.66 ? curtain_b.rgb : curtain_c.rgb);
		float open = 0.18 + r_open * 0.2;
		float side = min(uv.x, 1.0 - uv.x);
		cover = 1.0 - smoothstep(open - 0.01, open + 0.01, side);
		cloth *= 0.85 + 0.15 * sin(uv.x * 70.0);
	} else if (r_curtain < curtain_chance + blind_chance) {
		cloth = blind_color.rgb;
		float down = 0.25 + r_open * 0.55;
		cover = 1.0 - smoothstep(down - 0.01, down + 0.01, uv.y);
		cloth *= 0.9 + 0.1 * step(0.5, fract(uv.y * 28.0));
	}

	if (cover > 0.0) {
		inside = mix(glass, lit ? cloth * 1.15 : cloth, cover * 0.92);
		emission *= mix(1.0, 0.6, cover);
		rough = mix(rough, 0.6, cover * 0.5);
	}

	// Frame and bars.
	vec3 frame = frame_color.rgb;
	if (frame_variety > 0.0) {
		vec3 alt = b < 0.5 ? frame_alt_a.rgb : frame_alt_b.rgb;
		frame = mix(frame, alt, step(1.0 - frame_variety, fract(b * 9.13)));
	}

	float pane = box(uv, vec2(frame_width), vec2(1.0 - frame_width));
	vec2 inner = (uv - frame_width) / max(1.0 - 2.0 * frame_width, 1e-3);
	vec2 count = vec2(float(max(panes_across, 1)), float(max(panes_up, 1)));
	vec2 cell = fract(inner * count);
	vec2 half_bar = vec2(bar_width) * count * 0.5;
	float bars = 1.0 - box(cell, half_bar, 1.0 - half_bar);
	float glass_mask = pane * (1.0 - bars * step(0.0, inner.x) * step(inner.x, 1.0));

	vec3 color = mix(frame, inside, glass_mask);
	if (sill > 0.0) {
		float ledge = smoothstep(1.0 - sill - 0.01, 1.0 - sill, uv.y);
		color = mix(color, sill_color.rgb, ledge);
		glass_mask *= 1.0 - ledge;
	}

	ALBEDO = color;
	EMISSION = emission * glass_mask;
	ROUGHNESS = mix(0.55, rough, glass_mask);
	METALLIC = 0.0;
	SPECULAR = mix(0.3, 0.7, glass_mask);
}
