// Parchment overlay shader for the strategic map.
// Adds paper grain + vignette darkening at edges, in an additive/multiplicative
// blend with the underlying hex map. Tasteful, low-intensity by default —
// the goal is "old map feel", not "buried in noise".
//
// See game-visual-style skill for the paint-overlay aesthetic intent.

shader_type canvas_item;

uniform float grain_strength : hint_range(0.0, 0.4) = 0.07;
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.45;
uniform float vignette_inner : hint_range(0.0, 0.6) = 0.35;
uniform float vignette_outer : hint_range(0.4, 1.5) = 0.85;
uniform vec4 vignette_tint : source_color = vec4(0.08, 0.05, 0.03, 1.0);
uniform vec4 paper_tint : source_color = vec4(0.97, 0.91, 0.78, 1.0);
uniform float paper_strength : hint_range(0.0, 0.5) = 0.15;
uniform float grain_scale : hint_range(100.0, 4000.0) = 1500.0;

// Cheap deterministic pseudo-random (hash-based, no texture sample)
float hash21(vec2 p) {
    p = fract(p * vec2(443.8975, 397.2973));
    p += dot(p, p.yx + 19.19);
    return fract(p.x * p.y);
}

void fragment() {
    vec2 uv = SCREEN_UV;

    // Paper grain (fine noise)
    float noise = hash21(floor(uv * grain_scale));
    float grain = (noise - 0.5) * grain_strength;

    // Vignette: 0 at centre, ramps up toward corners
    vec2 centred = uv - vec2(0.5);
    float dist = length(centred);
    float vignette = smoothstep(vignette_inner, vignette_outer, dist) * vignette_strength;

    // Combined overlay color: paper tint with grain + vignette darkening at edges
    vec3 overlay = mix(paper_tint.rgb, vignette_tint.rgb, vignette);
    overlay += vec3(grain);

    // Alpha: paper_strength in centre, ramping to (paper_strength + vignette) at edges
    float alpha = paper_strength + vignette * 0.6;
    alpha = clamp(alpha, 0.0, 0.9);

    COLOR = vec4(overlay, alpha);
}
