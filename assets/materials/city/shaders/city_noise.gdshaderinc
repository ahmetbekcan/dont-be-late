// Small noise helpers shared by the city surface shaders.

float city_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float city_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(city_hash(i), city_hash(i + vec2(1.0, 0.0)), u.x),
		mix(city_hash(i + vec2(0.0, 1.0)), city_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float city_fbm(vec2 p) {
	float value = 0.0;
	float amount = 0.5;
	for (int i = 0; i < 4; i++) {
		value += amount * city_noise(p);
		p = p * 2.03 + vec2(17.0, 9.0);
		amount *= 0.5;
	}
	return value;
}

// Vertex colours are sRGB; ALBEDO wants linear.
vec3 city_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}
