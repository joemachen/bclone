# bclone

A ground-up, generational village-builder / survival sim — a spiritual successor to *Banished*. You grow a town across generations against a world that pushes back. No combat, no traditional win condition. The game is watching a lineage survive.

**Design & process docs (read these first):**
- [`DESIGN.md`](./DESIGN.md) — vision, pillars, architecture, build order, progress tracker *(the what)*
- [`CLAUDE.md`](./CLAUDE.md) — working agreement for AI-assisted development *(the how)*
- [`METHODOLOGY.md`](./METHODOLOGY.md) — phasing, specs, testing/QA, logging, versioning, CI *(the standards)*

---

## First-time GitHub setup

The remote is **https://github.com/joemachen/bclone**. From this working directory:

```bash
# 1. Initialize (skip if already a git repo)
git init
git branch -M main

# 2. Stage and commit the scaffold
git add .
git commit -m "Initial scaffold: design docs, tooling, gitignore"

# 3. Connect the remote
git remote add origin https://github.com/joemachen/bclone.git

# 4. Push
git push -u origin main
```

**If you created the repo on GitHub with a README/license already**, the first push will be rejected (remote has commits you don't). Reconcile first:

```bash
git pull --rebase origin main
git push -u origin main
```

**Auth:** use the [GitHub CLI](https://cli.github.com/) (`gh auth login`) — simplest — or an HTTPS Personal Access Token, or set up an SSH key and swap the remote for `git@github.com:joemachen/bclone.git`.

---

## Branch strategy

- `main` — always buildable and test-passing. Never commit broken code here.
- `phase/<n>-<name>` or `feat/<name>` — one branch per phase or feature (e.g. `phase/0-vertical-slice`).
- Merge to `main` via PR once the phase's Definition of Done (see `METHODOLOGY.md`) is met and CI is green. Solo PRs are still worth it — they give CI a gate and leave a paper trail.

---

## Tech stack

**C# (.NET 8) + Godot 4** (`DESIGN.md` §7, decisions D1–D3). Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0); Godot 4 (.NET build) is only needed once the view layer exists in Phase 0.

The simulation lives in `src/Bclone.Sim`, a plain class library with **zero Godot references** — headlessly testable, fast in CI, and portable if the shell ever needs replacing. Godot is the render/UI/input layer only, and never mutates sim state.

```
src/Bclone.Sim/       simulation core (engine-free)
  Core/               SimWorld, SimLoop, FixedTimestepDriver, ISimSystem
  Determinism/        DeterministicRandom (PCG32), StateHash
  Logging/            tick-stamped structured logger
  Config/             JSON-backed tunables
src/Bclone.Game/      Godot view shell — arrives in Phase 0
tests/                xUnit suite, including the P0 determinism test
data/                 content and tunables (JSON, modder-editable)
specs/                one spec per system, written before the code
```

## Running & testing locally

Two Windows batch files wrap the build/run/test commands and capture timestamped logs to `logs/`:

- **`test.bat`** — runs the full suite (`dotnet test`), determinism test included.
- **`run.bat`** — launches the game. **Nothing to run yet:** the tick loop is a headless library, so the Godot project arrives with Phase 0. Use `test.bat` until then.

See `METHODOLOGY.md` for the logging and testing standards.

---

## Releases (from v1)

Release automation lives in `.github/workflows/release.yml`. It is **tag-triggered** and does nothing until you push a version tag (`vX.Y.Z`), which is the v1 moment. See `METHODOLOGY.md` → *Versioning & Releases* for the full process.
