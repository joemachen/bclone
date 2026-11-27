shader_type canvas_item;

// ⭐ The trees in a gentle wind (A299, `specs/tree-sway.md`; Joe's play note (i): "a gentle wind animation to trees that is a degree or
// two more intense in the winter. think of how the water animates"; the art queue's piece 5, at his "just power through all these").
// The map's own material: every draw on the map passes through it untouched but the painted trees' TOP corners, which MeshBuilder
// marks by a 4e-5 nudge of their UV.x (fract(UV.x * 24) = 0.001 — a hair of the atlas, invisible). Those lean on a slow sway, each
// tree on its own phase from where it stands; trunks stay put. In the trees' own units (tiles) — anything else that happened to carry
// the mark moves the same few hundredths of a PIXEL, nothing to see. A uniform, never a re-mesh (D338).

uniform float sway_seconds = 0.0;
uniform float sway_tiles = 0.035;

const float MARK = 0.001;

void vertex() {
	float f = fract(UV.x * 24.0);
	if (abs(f - MARK) < 0.0003) {
		float phase = (VERTEX.y * 0.9) + (VERTEX.x * 0.15);
		float gust = sin((sway_seconds * 0.9) + phase) * 0.75 + sin((sway_seconds * 2.3) + (phase * 1.7)) * 0.25;
		VERTEX.x += gust * sway_tiles;
	}
}
