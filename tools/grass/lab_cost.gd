# The grass look's GPU cost (`specs/grass-look.md` §5, D632) — a tool, never part of the game. Re-run it whenever the
# terrain shader gains a layer, and put the new table beside the old.
#
#   "$GODOT" --path src/Bclone.Game -s "$(pwd -W)/tools/grass/lab_cost.gd"
# (PowerShell: -s "$PWD\tools\grass\lab_cost.gd". The path must be absolute: a relative one is read as res://.)
#
# ⭐ THE METHOD, AND WHY (D631's first pass was noise — "all three" cheaper than mixing alone):
#   - The lab renders into a 3840 x 2160 SubViewport, so the grass shader's work dwarfs timer noise and GPU clock
#     changes; a full-screen fragment shader's cost scales with pixels, so 1280 x 800 is 0.1235 of it.
#   - Cases are interleaved over ROUNDS rounds, so clock drift lands on every case alike: each round, every case gets
#     WARM frames to settle and SAMPLES frames timed by RenderingServer.viewport_get_measured_render_time_gpu; the
#     round's number is the median, and the case's is the median of its rounds.
#   - Zoom is set in pixels a tile, which is what decides each pixel's work (and the mip level it reads), so a 4K
#     window at 13 px a tile costs per pixel what the game's widest zoom does.
#   - Placeholders off and panels hidden, so only the ground is timed. Read it on a quiet machine.
extends SceneTree

const SIZE := Vector2i(3840, 2160)
const ROUNDS := 6
const WARM := 20
const SAMPLES := 60
const ZOOMS := [["widest", 13.0], ["gameplay", 48.0]]

# Each case starts from the approved look (the lab's Reset), then changes what it names.
const CASES := [
	["pack only (no anti-tiling, no grain)", {"Macro variation": false, "Hex tiling": false, "Paper grain": 0.0}],
	["approved, no grain", {"Paper grain": 0.0}],
	["approved, with the lab's grain", {}],
]


func _initialize() -> void:
	_run()


func _put(lab: Node, name: String, value) -> void:
	var n := lab.find_child(name, true, false)
	if n is OptionButton:
		n.select(value)
		n.emit_signal("item_selected", value)
	elif n is CheckBox:
		n.button_pressed = value
	else:
		n.value = value


func _run() -> void:
	var vp := SubViewport.new()
	vp.size = SIZE
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(vp)
	RenderingServer.viewport_set_measure_render_time(vp.get_viewport_rid(), true)
	var lab: Control = load("res://GrassLab.tscn").instantiate()
	vp.add_child(lab)
	await process_frame
	(lab.find_child("Panel", true, false) as Control).visible = false
	(lab.find_child("Tiling", true, false) as Control).visible = false
	print("GPU: ", RenderingServer.get_video_adapter_name(), " · ", SIZE, " · ", ROUNDS, " rounds of ", SAMPLES, " frames")
	for zoom in ZOOMS:
		var rounds := {}
		for c in CASES:
			rounds[c[0]] = []
		for r in ROUNDS:
			for c in CASES:
				(lab.find_child("Reset", true, false) as Button).emit_signal("pressed")
				_put(lab, "Placeholders", false)
				_put(lab, "Pixels per tile", zoom[1])
				for k in c[1]:
					_put(lab, k, c[1][k])
				for i in WARM:
					await process_frame
				var samples: Array[float] = []
				for i in SAMPLES:
					await RenderingServer.frame_post_draw
					samples.append(RenderingServer.viewport_get_measured_render_time_gpu(vp.get_viewport_rid()))
				samples.sort()
				rounds[c[0]].append(samples[SAMPLES / 2])
		for c in CASES:
			var m: Array = rounds[c[0]]
			m.sort()
			var mid: float = (m[ROUNDS / 2 - 1] + m[ROUNDS / 2]) / 2.0
			print("COST | %-8s %2d px/tile | %-38s | 4K %.3f ms | 1280x800 %.3f ms | 2560x1440 %.3f ms | rounds %s" % [
				zoom[0], int(zoom[1]), c[0], mid, mid * 0.1235, mid * 0.4444, str(m)])
	quit(0)
