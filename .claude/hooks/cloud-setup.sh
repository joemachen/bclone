#!/usr/bin/env bash
# SessionStart: make a Claude Code CLOUD session able to run CLAUDE.md's four verification lines.
# Does nothing on Joe's machine (CLAUDE_CODE_REMOTE is only set in the cloud).
#
#   1. the .NET 8 SDK (the suite and the view build)        -> ~/.dotnet
#   2. Godot 4.7.1 .NET, Linux, for the headless probe       -> ~/godot, exported as $GODOT
#   3. a NuGet restore of the solution and the view
#
# Idempotent and quiet when everything is already there. Never fails the session: what could not be
# installed is printed, and the session says so instead of claiming a verification it did not run.
[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] || exit 0

GODOT_VERSION="4.7.1"   # must match Godot.NET.Sdk in src/Bclone.Game/Bclone.Game.csproj
ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
ENVF="${CLAUDE_ENV_FILE:-/dev/null}"
say() { echo "[cloud-setup] $*"; }

# ---- .NET 8 ----
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  say "installing the .NET 8 SDK..."
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$DOTNET_ROOT" >/tmp/dotnet-install.log 2>&1 \
    || say "⛔ .NET 8 install FAILED (see /tmp/dotnet-install.log) — is dot.net / builds.dotnet.microsoft.com allowed?"
fi
{
  echo "export DOTNET_ROOT=\"$DOTNET_ROOT\""
  echo "export PATH=\"$DOTNET_ROOT:$DOTNET_ROOT/tools:\$PATH\""
  echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1"
} >> "$ENVF"

# ---- Godot (for the probe) ----
GDIR="$HOME/godot"
GODOT_BIN="$(find "$GDIR" -maxdepth 2 -type f -name 'Godot_v*_mono_linux*.x86_64' 2>/dev/null | head -1)"
if [ -z "$GODOT_BIN" ]; then
  say "installing Godot $GODOT_VERSION (.NET, Linux)..."
  mkdir -p "$GDIR"
  ZIP="Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64.zip"
  if curl -sSL -o "/tmp/$ZIP" "https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-stable/$ZIP" \
     && (cd "$GDIR" && (unzip -q -o "/tmp/$ZIP" || python3 -m zipfile -e "/tmp/$ZIP" .)); then
    GODOT_BIN="$(find "$GDIR" -maxdepth 2 -type f -name 'Godot_v*_mono_linux*.x86_64' | head -1)"
    chmod +x "$GODOT_BIN" 2>/dev/null
  else
    say "⛔ Godot download FAILED — is github.com allowed? The probe cannot run without it."
  fi
fi
[ -n "$GODOT_BIN" ] && echo "export GODOT=\"$GODOT_BIN\"" >> "$ENVF"

# ---- restore ----
if command -v dotnet >/dev/null; then
  (cd "$ROOT" && dotnet restore bclone.sln -v q >/tmp/restore.log 2>&1 \
    && dotnet restore src/Bclone.Game/Bclone.Game.csproj -v q >>/tmp/restore.log 2>&1) \
    || say "⛔ NuGet restore FAILED (see /tmp/restore.log) — is api.nuget.org allowed?"
fi

say "dotnet: $(dotnet --version 2>/dev/null || echo MISSING) · GODOT: ${GODOT_BIN:-MISSING}"
exit 0
