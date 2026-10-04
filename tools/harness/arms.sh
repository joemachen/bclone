#!/usr/bin/env bash
# D420's harness: run each arm over 100 shipped seeds x fifty years, unattended, and summarise.
#
#   tools/harness/arms.sh base woods=0 woods=100        # ZZ_ROW arms (a new-game row id=value)
#   ZZ_WIDE=2 tools/harness/arms.sh base               # seeds 300-399 instead of 200-299
#   OUT=/tmp/arms tools/harness/arms.sh base           # where the outputs go (default: ./harness-out)
#
# "base" is the shipped settings. Any other arm is a new-game row as id=value (NewGame rows only;
# for a bare config key, add a ZZ_ variable to ZzBase.cs the way ZZ_SPEED is done).
# Outputs are row-<arm>.txt; dead.py prints alive / dead valleys / peak / starved / froze per arm.
# ⛔ ZzBase.cs is copied into the test project and removed at the end — never commit it there.
# ⏱️ About 2-4 minutes an arm on Joe's machine; one arm at a time.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="${OUT:-$ROOT/harness-out}"
export ZZ_WIDE="${ZZ_WIDE:-1}"
mkdir -p "$OUT"
cd "$ROOT"
cp tools/harness/ZzBase.cs tests/Bclone.Sim.Tests/ZzBase.cs
trap 'rm -f "$ROOT/tests/Bclone.Sim.Tests/ZzBase.cs"' EXIT
dotnet build tests/Bclone.Sim.Tests --nologo -v q > "$OUT/build.txt" 2>&1 || { cat "$OUT/build.txt"; exit 1; }
outs=()
for arm in "$@"; do
  out="$OUT/row-${arm/=/-}.txt"
  if [ "$arm" = base ]; then unset ZZ_ROW; else export ZZ_ROW="$arm"; fi
  dotnet test tests/Bclone.Sim.Tests --nologo --no-build --filter "FullyQualifiedName~ZzBase" \
    --logger "console;verbosity=detailed" > "$out" 2>&1 || true
  echo "$arm: $(grep -c 'ZZB ' "$out") runs"
  outs+=("$out")
done
PYTHONIOENCODING=utf-8 python3 tools/harness/dead.py "${outs[@]}"
