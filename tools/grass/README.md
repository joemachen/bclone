# tools/grass — the grass look's checks

Tools for the seasonal grass (`specs/seasonal-grass.md`, `specs/grass-look.md`). None of them is part of the game:
the Godot scripts run the lab scene from outside (`-s`), and the Python ones read what they render. The approved look
lives in `src/Bclone.Game/looks/grass_approved.tres`; everything here reads it from there.

| file | what |
|---|---|
| `grass_antitile_ref.py` | the Python mirror of `shaders/grass_antitile.gdshaderinc` and the lab's fragment; calls Joe's `grass_ref.py` (`$BCLONE_GRASS_PACK`, default `D:/Projects/brushes/seasons`). `--selfcheck` holds its recolour to his. |
| `lab_render.gd` | renders the lab to PNGs — the reference set at the approved look, or any `{name: {control: value}}` cases file |
| `lab_check.py` | `parity` (renders vs the mirror) and `shots` (the reference JPEGs in `art/grass/`) |
| `lab_cost.gd` | the GPU cost table, at the widest and the gameplay zoom — re-run when a terrain layer is added |

From the repo root, in Git Bash (`$GODOT` as `run.bat` finds it). **The `-s` path must be absolute** — a relative one
is read as `res://` and Godot says *File not found*. Windowed, never `--headless` (a headless Godot hangs on an image).

```bash
python -X utf8 -I -B tools/grass/grass_antitile_ref.py --selfcheck
"$GODOT" --path src/Bclone.Game -s "$(pwd -W)/tools/grass/lab_render.gd" -- "$TMP/grass"
python -X utf8 -I -B tools/grass/lab_check.py parity "$TMP/grass"
python -X utf8 -I -B tools/grass/lab_check.py shots "$TMP/grass" art/grass
"$GODOT" --path src/Bclone.Game -s "$(pwd -W)/tools/grass/lab_cost.gd"
```

`-B` keeps Python from leaving a `__pycache__` here.
