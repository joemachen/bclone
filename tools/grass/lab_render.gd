# Renders the grass lab (`specs/grass-look.md`, D632) into PNGs — a tool, never part of the game (D160 deleted the
# in-game screenshot hook; this runs the lab scene from outside it). Windowed, never --headless: a headless Godot has no
# image to give and hangs (HANDOFF trap 39).
#
#   "$GODOT" --path src/Bclone.Game -s "$(pwd -W)/tools/grass/lab_render.gd" -- OUT_DIR [CASES.json]
# (PowerShell: -s "$PWD\tools\grass\lab_render.gd". The path must be absolute: a relative one is read as res://.)
#
# With no CASES file it renders the reference set at the APPROVED look, exactly as the lab opens on it
# (looks/grass_approved.tres), placeholders off and panels hidden:
#   ref_s<season>_widest.png    13 px a tile — the game's widest zoom at 1280 wide
#   ref_s<season>_gameplay.png  48 px a tile — the founding view, where the game opens
#   strip_<from>_<pct>.png      24 px a tile, each season into the next at 0/25/50/75/100 %
#   parity_s<season>.png        33 px a tile x 63 tiles a repeat (about one mask texel a pixel), grain 0 — for parity.py
# A CASES file is {name: {control name: value}}; controls not named keep the approved look.
extends SceneTree

const SIZE := Vector2i(1280, 800)


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		push_error("usage: -- OUT_DIR [CASES.json]")
		quit(2)
		return
	var cases: Dictionary = _reference_set() if args.size() < 2 else JSON.parse_string(FileAccess.get_file_as_string(args[1]))
	_run(args[0], cases)


func _reference_set() -> Dictionary:
	var cases := {}
	for s in 4:
		cases["ref_s%d_widest" % s] = {"Season": float(s), "Pixels per tile": 13.0}
		cases["ref_s%d_gameplay" % s] = {"Season": float(s), "Pixels per tile": 48.0}
		for pct in [0, 25, 50, 75, 100]:
			cases["strip_%d_%03d" % [s, pct]] = {"Season": s + pct / 100.0, "Pixels per tile": 24.0}
		cases["parity_s%d" % s] = {"Season": float(s), "Pixels per tile": 33.0, "Paper grain": 0.0}
	return cases


func _put(lab: Node, name: String, value) -> void:
	var n := lab.find_child(name, true, false)
	if n == null:
		push_error("no control " + name)
		return
	if n is OptionButton:
		n.select(value)
		n.emit_signal("item_selected", value)
	elif n is CheckBox:
		n.button_pressed = value
	else:
		n.value = value


func _run(out_dir: String, cases: Dictionary) -> void:
	var vp := SubViewport.new()
	vp.size = SIZE
	vp.transparent_bg = false
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(vp)
	var lab: Control = load("res://GrassLab.tscn").instantiate()
	vp.add_child(lab)
	await process_frame
	(lab.find_child("Panel", true, false) as Control).visible = false
	(lab.find_child("Tiling", true, false) as Control).visible = false
	for name in cases:
		# Every case starts from the approved look, through the lab's own button.
		(lab.find_child("Reset", true, false) as Button).emit_signal("pressed")
		_put(lab, "Placeholders", false)
		var c: Dictionary = cases[name]
		for k in c:
			_put(lab, k, c[k])
		for i in 4:
			await process_frame
		await RenderingServer.frame_post_draw
		await RenderingServer.frame_post_draw
		var img := vp.get_texture().get_image()
		img.convert(Image.FORMAT_RGB8)
		img.save_png(out_dir.path_join("%s.png" % name))
		print("saved ", name)
	quit(0)
