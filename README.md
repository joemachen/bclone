# bclone

A ground-up, generational village-builder / survival sim — a spiritual successor to *Banished*. You grow a town across generations against a world that pushes back. No combat, no traditional win condition. The game is watching a lineage survive.

Everything on screen is explained by something you can click on: why Amos walks to that thicket rather than the nearer one, what the shed is holding, which work the village wants that nobody is doing.

**Design & process docs (read these first):**
- [`DESIGN.md`](./DESIGN.md) — vision, pillars, architecture, build order, progress tracker *(the what)*. §6 is where the project actually is; §7 is why it got there.
- [`CLAUDE.md`](./CLAUDE.md) — working agreement for AI-assisted development *(the how)*
- [`METHODOLOGY.md`](./METHODOLOGY.md) — phasing, specs, testing/QA, logging, versioning, CI *(the standards)*
- [`specs/`](./specs) — one spec per system, written before the code

---

## Where it is

**Phases 0 and 1 are merged to `main`.** Phase 2 — wood as fuel, goods that live in buildings, a generated valley, building placement, and seasons with teeth — is on `phase/2-wood-fuel-and-tools` and unmerged until its Definition of Done is met. `DESIGN.md` §6 has the detail.

The game runs. A village founds itself, feeds itself, cuts timber, builds, has children, freezes if you let the woodpile fail, and dies out if you get it badly wrong — and every one of those outcomes can be traced back to a decision by clicking on the people it happened to.

---

## Tech stack

**C# (.NET 8) + Godot 4.7.1** (`DESIGN.md` §7, D1–D3). You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build and test, and Godot 4 (the .NET/Mono build) to run the game.

The simulation lives in `src/Bclone.Sim`, a plain class library with **zero Godot references** — headlessly testable, fast in CI, and portable if the shell ever needs replacing. Godot is the render/UI/input layer only, and never mutates sim state.

```
src/Bclone.Sim/       simulation core (engine-free)
  Core/               SimWorld, SimLoop, FixedTimestepDriver, ISimSystem
  Determinism/        DeterministicRandom (PCG32), StateHash
  Logging/            tick-stamped structured logger
  Config/             JSON-backed tunables
  Systems/            one file per tick-order step
  World/              villagers, households, workplaces, stores, the map
src/Bclone.Game/      Godot view shell — NOT in bclone.sln, see below
tests/                xUnit suite, including the P0 determinism test
data/                 content and tunables (JSON, modder-editable)
specs/                one spec per system, written before the code
```

### ⚠️ `bclone.sln` does not contain the Godot project

This is the first thing that will bite you. `src/Bclone.Game` is deliberately outside the solution (D11 — a root Godot project globs `**/*.cs` and would swallow the sim and the tests into the game build). So:

```bash
dotnet build bclone.sln                              # sim + tests. NOT the view.
dotnet build src/Bclone.Game/Bclone.Game.csproj      # the view.
```

A green solution build says nothing about whether the game compiles. CI has a separate step for it, and `run.bat` builds it before launching. Found the hard way: a build menu was written, wired up, and never appeared, because the assembly Godot ran was a day old.

---

## Running & testing locally

Two Windows batch files wrap the commands and capture timestamped logs to `logs/`:

- **`test.bat`** — the full suite, determinism test included.
- **`run.bat`** — builds the view, then launches Godot. Set `GODOT` to your editor executable if it is not at `D:\Projects\Godot\Godot_v4.7.1-stable_mono_win64\`.

Every run also writes a full audit trail to `logs/bclone-<timestamp>.log` — every villager state change, every load carried, every job taken and every refusal, tick-stamped. Together with the seed shown in the header, that is what reproduces and explains a run.

**Nothing in `src/Bclone.Game` can be unit-tested** (D11), so looking at it *is* the verification — there is no automated check on the view, and Joe's eyes are the test.

---

## Branch strategy

- `main` — always buildable and test-passing. Never commit broken code here.
- `phase/<n>-<name>` or `feat/<name>` — one branch per phase or feature.
- Merge to `main` via PR once the phase's Definition of Done (`METHODOLOGY.md`) is met and CI is green. Solo PRs are still worth it — they give CI a gate and leave a paper trail.

---

## Releases (from v1)

Release automation lives in `.github/workflows/release.yml`. It is **tag-triggered** and has never run: there are no tags yet, `VERSION` is not wired into the build, and the Godot export preset is not committed. See `METHODOLOGY.md` → *Versioning & Releases* for what the first tag needs.
