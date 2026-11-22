extends SceneTree
# The title's four seasons (A189, specs/title-screen-art.md §3): one committed save, frozen, drawn by the game's own renderer in
# a 3840 x 2160 SubViewport at four seasons through the render-only override (VillageMap.ShowTheSeason) — the sim never steps.
# Run by capture.bat / capture.sh, which put village.save in a scratch user folder as its only save; never by the suite.
#   env TITLE_OUT = the folder the four PNGs are written to.
# Fails LOUDLY (exit 1, an ERROR line) when the save no longer opens, or when the world's hash moved between the captures —
# it never writes a picture of something else.

const SIZE := Vector2i(3840, 2160)
# Where in each season: inside its hold, before it turns (TheYearAt: a season holds, then turns into the next). Winter is
# taken where the snow lies whole. ⭐ SPRING IS THE THAW (Joe, A189: "Use the thaw"): the game has no spring tree set —
# spring's trees are summer's — so mid-spring and mid-summer were near-twins; the last days of winter, the snow gone from the
# grass and lying only on the fields, read as spring at a glance.
var SEASONS := [["spring", 3.86], ["summer", 1.30], ["autumn", 2.30], ["winter", 3.30]]
# The village's middle in the view's tiles, and where it should sit: the middle of the area right of the panel (28 % of the
# width), at the closest zoom (48 px a tile — a house ~40 px wide at 4K). Measured off the first composite: its houses
# centred 13 tiles west and 3 north of the first guess, (4, 4).
var VILLAGE := Vector2(-9.2, 1.3)
const PANEL_SHARE := 0.28
const PPT := 48.0

var vp: SubViewport
var main: Node
var map: Node
var frame := 0
var out := ""
var phase := "open"
var wait := 0
var si := 0
var hash_before := ""

func _initialize() -> void:
	# TITLE_SEASONS="name:phase,…" replaces the four (a comparison for Joe; the composite reads the four names above).
	var asked := OS.get_environment("TITLE_SEASONS")
	if asked != "":
		SEASONS.clear()
		for pair in asked.split(","):
			var p = pair.split(":")
			SEASONS.append([p[0], float(p[1])])
	# TITLE_VILLAGE="x,y" moves the village's middle (a framing compared for Joe).
	var at := OS.get_environment("TITLE_VILLAGE")
	if at != "":
		VILLAGE = Vector2(float(at.split(",")[0]), float(at.split(",")[1]))
	out = OS.get_environment("TITLE_OUT")
	if out == "":
		print("ERROR TITLE_OUT is not set")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(out)
	vp = SubViewport.new()
	vp.size = SIZE
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(vp)
	main = load("res://Main.tscn").instantiate()
	vp.add_child(main)

func find_by(n: Node, f: Callable) -> Node:
	if f.call(n):
		return n
	for c in n.get_children():
		var r = find_by(c, f)
		if r != null:
			return r
	return null

func key(k: int) -> void:
	for pr in [true, false]:
		var ev = InputEventKey.new()
		ev.keycode = k
		ev.physical_keycode = k
		ev.pressed = pr
		vp.push_input(ev)

func fail(why: String) -> bool:
	print("ERROR ", why)
	quit(1)
	return true

func _process(_delta: float) -> bool:
	frame += 1
	if wait > 0:
		wait -= 1
		return false
	match phase:
		"open":
			if frame < 40:
				return false
			var b = find_by(main, func(n): return n is Button and n.text == "Continue" and n.is_visible_in_tree())
			if b == null:
				return fail("the title's village no longer opens (no Continue on the title: the save was not read). Open tools/title-capture/village.save in the game and save it again.")
			b.emit_signal("pressed")
			phase = "loaded"
			wait = 120
		"loaded":
			map = find_by(main, func(n): return n.has_method("ShowTheSeason"))
			if map == null:
				var refusal = find_by(main, func(n): return n is Label and n.is_visible_in_tree() and n.text.length() > 20)
				return fail("the title's village no longer opens (%s). Open tools/title-capture/village.save in the game and save it again." % (refusal.text if refusal != null else "no map"))
			paused = true
			# A moment's popup is answered by its own button, then the furniture hidden, the debug readout too.
			for i in range(6):
				var m = find_by(main, func(n): return n is Button and n.is_visible_in_tree() and (n.text == "Good" or n.text == "Go on"))
				if m != null:
					m.emit_signal("pressed")
			# The furniture hidden: everything in the game's scene but the map's own branch (the H key does not reach a
			# game in a SubViewport, so the windows, the bar, the readout and the minimap are hidden by hand).
			var keep := {}
			var n: Node = map
			while n != null and n != main:
				keep[n] = true
				n = n.get_parent()
			hide_beside(main, keep)
			# The map's markers off: a picture, not a game in progress.
			map.ShowWatercolour(true)
			map.ShowIdleMarkers(false)
			map.ShowFullMarkers(false)
			map.ShowResidentialLand(false)
			map.ShowWorkGround(false)
			map.ShowHarvestLand(false)
			for i in range(40):
				wheel(1)
			# The camera left of the village by the panel's half-share of the map's own width, so the village's middle lands
			# in the middle of the area right of the panel.
			print("capture: the map is ", map.size, " px at ", PPT, " px a tile")
			map.CentreOn(VILLAGE - Vector2((PANEL_SHARE / 2.0) * map.size.x / PPT, 0.0))
			hash_before = map.WorldHashForTheCapture()
			print("capture: the world at ", hash_before)
			phase = "season"
			wait = 20
		"season":
			if si >= SEASONS.size():
				var hash_after = map.WorldHashForTheCapture()
				if hash_after != hash_before:
					return fail("the world moved during the capture (%s -> %s): the seasons are not one moment" % [hash_before, hash_after])
				map.ShowTheRealSeason()
				print("capture: the world unmoved at ", hash_after)
				if SEASONS.size() != 4 or SEASONS[0][0] != "spring":
					quit(0)
					return true
				phase = "compose"
				return false
			map.ShowTheSeason(SEASONS[si][1])
			map.queue_redraw()
			phase = "shoot"
			wait = 30
		"shoot":
			var name: String = SEASONS[si][0]
			var image := vp.get_texture().get_image()
			var path := out.path_join(name + ".png")
			var err := image.save_png(path)
			if err != OK:
				return fail("could not write %s (%d)" % [path, err])
			print("capture: ", name, " ", image.get_size(), " -> ", path)
			shots[name] = image
			si += 1
			phase = "season"
		"compose":
			if ci >= SEAMS.size():
				print("capture: done")
				quit(0)
				return true
			start_composite(SEAMS[ci])
			phase = "write"
			wait = 4
		"write":
			var style: String = SEAMS[ci]
			var image := composer.get_texture().get_image()
			var path := out.path_join("composite_" + style + ".jpg")
			var err := image.save_jpg(path, 0.9)
			if err != OK:
				return fail("could not write %s (%d)" % [path, err])
			print("capture: composite (", style, " seams) -> ", path)
			composer.queue_free()
			ci += 1
			phase = "compose"
	return false

# ---- the composite (part 3, specs/title-screen-art.md §4): four bands, spring · summer · autumn · winter, splitting in equal
# widths only the area right of the panel (spring under it), the seams tilted 13° from vertical — far from the river's own run —
# drawn as a thin ink line or a narrow soft blend. A shader, not a pixel loop: 8 million pixels in a script take minutes.
const SEAM_TILT := 0.2309  # tan 13°
const SEAM_INK := Color("#2a2018")
const COMPOSE := """
shader_type canvas_item;
uniform sampler2D spring_tex : filter_linear;
uniform sampler2D summer_tex : filter_linear;
uniform sampler2D autumn_tex : filter_linear;
uniform sampler2D winter_tex : filter_linear;
uniform vec2 size;
uniform float x0;
uniform float band;
uniform float tilt;
uniform int style;
uniform float soft_px;
uniform float line_px;
uniform vec4 ink : source_color;
void fragment() {
	vec2 p = UV * size;
	float u = (p.x - (p.y - size.y * 0.5) * tilt - x0) / band;
	float across = cos(atan(tilt));
	vec4 c[4] = vec4[4](texture(spring_tex, UV), texture(summer_tex, UV), texture(autumn_tex, UV), texture(winter_tex, UV));
	vec4 col = c[0];
	for (int i = 1; i <= 3; i++) {
		float d = (u - float(i)) * band * across;
		float t = style == 1 ? smoothstep(-soft_px * 0.5, soft_px * 0.5, d) : step(0.0, d);
		col = mix(col, c[i], t);
		if (style == 0) {
			col = mix(col, ink, (1.0 - smoothstep(line_px * 0.5 - 1.0, line_px * 0.5 + 1.0, abs(d))) * ink.a);
		}
	}
	COLOR = vec4(col.rgb, 1.0);
}
"""
var SEAMS := ["ink", "soft"]
var shots := {}
var ci := 0
var composer: SubViewport

func start_composite(style: String) -> void:
	composer = SubViewport.new()
	composer.size = SIZE
	composer.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(composer)
	var rect := ColorRect.new()
	rect.size = Vector2(SIZE)
	var shader := Shader.new()
	shader.code = COMPOSE
	var mat := ShaderMaterial.new()
	mat.shader = shader
	for name in ["spring", "summer", "autumn", "winter"]:
		mat.set_shader_parameter(name + "_tex", ImageTexture.create_from_image(shots[name]))
	mat.set_shader_parameter("size", Vector2(SIZE))
	mat.set_shader_parameter("x0", PANEL_SHARE * SIZE.x)
	mat.set_shader_parameter("band", (1.0 - PANEL_SHARE) * SIZE.x / 4.0)
	mat.set_shader_parameter("tilt", SEAM_TILT)
	mat.set_shader_parameter("style", 0 if style == "ink" else 1)
	mat.set_shader_parameter("soft_px", 90.0)
	mat.set_shader_parameter("line_px", 4.0)
	mat.set_shader_parameter("ink", Color(SEAM_INK, 0.85))
	rect.material = mat
	composer.add_child(rect)

func hide_beside(at: Node, keep: Dictionary) -> void:
	for c in at.get_children():
		if keep.has(c):
			hide_beside(c, keep)
		elif c.has_method("hide"):
			c.hide()

func wheel(n: int) -> void:
	for i in range(abs(n)):
		var ev = InputEventMouseButton.new()
		ev.button_index = MOUSE_BUTTON_WHEEL_UP if n > 0 else MOUSE_BUTTON_WHEEL_DOWN
		ev.pressed = true
		ev.position = map.size / 2.0
		map._gui_input(ev)
