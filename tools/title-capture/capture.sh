#!/bin/bash
# The title's four seasons (A189, specs/title-screen-art.md §3) — one command, no manual steps, the same pictures each run:
#   GODOT=<the mono Godot 4.7.1 binary> tools/title-capture/capture.sh [out_dir]
# Puts village.save in a scratch user folder as its only save (full screen off), opens the game in a window for a few
# seconds, and writes spring.png, summer.png, autumn.png, winter.png (3840 x 2160) to out_dir (default: a temp folder).
# Never part of the suite. Exit 1 and an ERROR line when the save no longer opens or the world moved.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
GODOT="${GODOT:?set GODOT to the mono Godot 4.7.1 binary}"
OUT="${1:-${TMPDIR:-/tmp}/kith-title-capture}"
USERDIR="$(mktemp -d)"
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) BASE="$USERDIR"; export APPDATA="$(cygpath -w "$USERDIR")" ;;
  *) BASE="$USERDIR"; export XDG_DATA_HOME="$USERDIR" ;;
esac
SAVES="$BASE/Godot/app_userdata/bclone/saves/title"
mkdir -p "$SAVES"
cp "$HERE/village.save" "$SAVES/autosave-1.save"
printf '{ "version": 1, "toggles": { "full_screen": false } }\n' > "$BASE/Godot/app_userdata/bclone/settings.json"
mkdir -p "$OUT"
if command -v cygpath >/dev/null; then OUTP="$(cygpath -w "$OUT")"; SCRIPT="$(cygpath -w "$HERE/capture.gd")"; else OUTP="$OUT"; SCRIPT="$HERE/capture.gd"; fi
export TITLE_OUT="$OUTP"
cd "$ROOT"
dotnet build src/Kith.Game/Kith.Game.csproj -nologo -v q | grep -E "Error\(s\)"
set +e
"$GODOT" --path src/Kith.Game -s "$SCRIPT" 2>&1 | grep -E "^capture:|^ERROR|SCRIPT ERROR"
code=${PIPESTATUS[0]}
rm -rf "$USERDIR"
exit $code
