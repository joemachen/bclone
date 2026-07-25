# Spec: Deterministic Fixed-Timestep Tick Loop

> Status: **in progress** · Owner: Joe + Claude Code · Phase: pre-Phase-0 scaffold
> Format per `METHODOLOGY.md §2`. This is a living doc — update it if reality diverges.

---

## 1. Goal

Build the spine every other system will stand on: a **deterministic, fixed-timestep simulation loop, fully decoupled from rendering**.

The contract, stated once:

> **Same seed + same config + same number of ticks ⇒ byte-identical state and byte-identical log, on any machine, at any playback speed, forever.**

Nothing here simulates anything yet. There is no villager, no hunger, no season. This phase builds the *harness* — the loop, the RNG, the state hash, the logger — and the test that proves the contract holds. Phase 0 (`specs/phase-0-vertical-slice.md`) plugs the first real systems into it.

---

## 2. Which pillars / non-negotiables this serves

- **Non-negotiable: Legibility above all.** Determinism is legibility applied to the codebase. If a run is reproducible, any bug is reproducible, and any death in the town can be replayed and traced rather than guessed at.
- **Non-negotiable: Slow and traceable > clever and opaque.** The loop is deliberately the dumbest thing that works: a fixed list of systems, executed in a fixed order, one tick at a time.
- **Architecture (`DESIGN.md §3`):** deterministic fixed-timestep tick loop, decoupled from rendering; renderer interpolates between ticks and never mutates sim state.
- **Enables later:** clean saves, replays, golden tests, and co-op — all of which are only cheap if determinism is architectural from tick zero.

---

## 3. Inputs / outputs

| | |
|---|---|
| **Inputs** | `SimConfig` (loaded from `data/sim.config.json`), a `seed` (u64), and a tick count. |
| **Outputs** | A mutated `SimWorld`; a stream of tick-stamped `LogEntry` records; a `StateHash` (u64) usable as a cheap equality witness. |
| **Explicitly not an input** | Wall-clock time. The sim never reads a clock. |

---

## 4. Data model

```
SimWorld {                        // the single root of all sim state
    Tick:   ulong                 // monotonic, incremented once per StepOnce()
    Rng:    DeterministicRandom   // explicit state — part of the hash
    Config: SimConfig             // immutable for the run
    Log:    ISimLogger            // tick-stamped sink
}

DeterministicRandom {             // PCG32 (XSH-RR). Explicit, serializable state.
    State: ulong
    Inc:   ulong                  // stream selector, always odd
}

SimConfig {                       // loaded from JSON, never hardcoded
    Seed:               ulong
    TicksPerDay:        int
    TargetTicksPerSec:  double    // playback pacing only — NOT a sim value
    MaxTicksPerFrame:   int       // spiral-of-death guard
}

LogEntry {
    Tick:      ulong              // every entry is tied to an exact sim state
    Level:     LogLevel           // Trace | Debug | Info | Warn | Error
    Subsystem: string             // "sim" | "render" | "pathing" | ...
    Message:   string
}
```

**Note on `SimConfig`:** `TargetTicksPerSec` and `MaxTicksPerFrame` are *playback* concerns and live in the driver, not the sim. They're carried in the same config file for convenience but must never be read by sim logic — changing them must not change sim outcomes. Covered by a test (§8).

---

## 5. The two halves, and why they're separate

This is the load-bearing idea of the whole spec.

### 5a. `SimLoop` — inside the sim, no time

```csharp
public void StepOnce() {
    foreach (var system in _systems)   // fixed, explicit order
        system.Execute(_world);
    _world.Tick++;
}

public void Step(int ticks) { for (int i = 0; i < ticks; i++) StepOnce(); }
```

`Step` takes a **count**, never a duration. There is no `deltaTime` anywhere in sim logic. A tick is an indivisible, dimensionless unit; how much "game time" it represents is a config interpretation (`TicksPerDay`), not a property of the loop.

**System order is part of the determinism contract.** Systems run in registration order, always, single-threaded. Reordering them is a behavioral change and must be treated as one.

### 5b. `FixedTimestepDriver` — outside the sim, owns the clock

```csharp
public int Advance(double deltaSeconds);  // → how many ticks to run
public double Alpha { get; }              // [0,1) — render interpolation only
```

The driver accumulates real elapsed time and returns *how many whole ticks are owed*. Critically, **it takes `deltaSeconds` as a parameter rather than reading a clock itself** — so it's fully testable, and the wall-clock read happens exactly once, in the Godot view layer (`_Process(delta)`), which is outside the sim entirely.

**Why a float delta at this boundary is safe:** it only ever decides *how many* times to call `StepOnce()`. It cannot influence what happens *inside* a tick. So float noise here changes pacing, never outcomes. This is what lets the sim be integer-only while playback stays smooth.

**But the accumulator itself is an integer — whole nanoseconds — and that matters.** The obvious implementation is a subtraction loop:

```csharp
while (acc >= secondsPerTick) { acc -= secondsPerTick; ticks++; }   // WRONG
```

`0.1` is not representable in binary, so subtracting it 25 times from `2.5` does not land on zero — the loop returns 24 ticks, not 25. Determinism survives (the driver still cannot reach inside a tick), but the error compounds every frame and the game clock falls steadily behind real time. Accumulating in whole nanoseconds and taking a single integer division removes the drift completely, and the remainder carries into the next frame exactly.

*(This was found by the test suite during implementation, not by design — `WholeSecondDeltas_YieldExactTickCounts` exists as its regression guard.)*

`Alpha` is the fractional progress toward the next tick, handed to the renderer so it can interpolate positions between two sim states. The renderer reads it; the sim never sees it.

### 5c. Speed controls — the rule that's easy to get wrong

Pause / 1× / 2× / 4× must be implemented as **"how many ticks per real second"**, never as "make each tick bigger."

- ✅ `SpeedMultiplier = 4` → the driver returns ~4× as many ticks per second. Each tick is identical to a 1× tick.
- ❌ Scaling a `dt` passed into the sim → different arithmetic per speed → different outcomes → determinism dead.

Pause is simply `SpeedMultiplier = 0`, which returns zero ticks. The sim state is untouched while paused — not frozen by a flag inside sim logic.

Consequence, and it's the one worth remembering: **a run at 4× produces exactly the same history as a run at 1×.** Tested in §8.

---

## 6. Determinism rules for `Bclone.Sim`

These are enforced at build time by `Microsoft.CodeAnalysis.BannedApiAnalyzers` (`BannedSymbols.txt`), so violating one is a **compile error**, not a code-review miss.

| Banned | Why | Use instead |
|---|---|---|
| `System.Random` | Its algorithm changed between .NET Framework and .NET Core; not stable across runtimes. | `DeterministicRandom` |
| `DateTime.Now` / `.UtcNow`, `Stopwatch`, `Environment.TickCount` | Wall-clock in sim logic destroys reproducibility. | `SimWorld.Tick` |
| `Guid.NewGuid()` | Non-deterministic. | Seeded IDs from the sim. |
| Iterating `Dictionary` / `HashSet` | .NET guarantees no ordering, and randomizes string hashing per process. | Arrays, `SortedDictionary`, or iterate a sorted key list. |
| LINQ in sim logic | Hides allocation and ordering assumptions. | Explicit loops. |
| Parallelism (`Parallel.*`, `Task`) in sim | Non-deterministic interleaving. | Single-threaded. |
| Floats in **sim state** | Per Decision #2 — sim state is integer-only; fixed-point (`Q32.32`) gets introduced at the first system that genuinely needs fractional math. | `int` / `long`, later `Fixed`. |

**Also required, but not analyzer-enforceable** (guard by review + tests):
- `Bclone.Sim` must never reference Godot or any engine type. Enforced by the project having no such package reference and CI building it standalone.
- The renderer reads sim state and never writes it.

---

## 7. Tick update order

At this stage the system list is empty — the ordering rule exists so that Phase 0 slots into a defined contract rather than inventing one. Phase 0's order is specified in `specs/phase-0-vertical-slice.md §5`.

Per tick, in order:
1. Execute each registered `ISimSystem` in registration order.
2. Increment `SimWorld.Tick`.

`Tick` increments **after** systems run, so a system observing `world.Tick` sees the tick it is currently computing (0-based). The first `StepOnce()` executes systems at `Tick == 0` and leaves `Tick == 1`.

---

## 8. Testing

**Determinism test (P0 — keep green forever; a regression here is a P0 bug):**
- `SameSeed_ProducesIdenticalState` — two worlds, same seed, 10,000 ticks each ⇒ equal `StateHash`.
- `SameSeed_ProducesIdenticalLog` — and equal log entry sequences (per Phase 0 spec §8).

**Anti-vacuity tests** — these exist because a determinism test that can't fail is worse than no test at all:
- `DifferentSeed_ProducesDifferentState` — proves the hash actually reads state.
- `StateHash_ChangesWhenTickChanges` / `...WhenRngStateChanges` — proves the hash isn't constant.

**Decoupling tests:**
- `BatchedSteps_EqualSingleSteps` — 1×10,000 ticks == 100×100 ticks == 10,000×1 tick. Proves the driver's batching cannot leak into sim state.
- `PlaybackSpeed_DoesNotAffectState` — driving 10,000 ticks at 1× and at 4× yields the same hash.

**Driver tests** (pure, no clock):
- Correct whole-tick counts from accumulated deltas; remainder carries over.
- `Alpha` always in `[0,1)`.
- Spiral guard: a huge delta clamps to `MaxTicksPerFrame`, drops the backlog, and logs a `WARN` — never silently (`METHODOLOGY §4`).
- `SpeedMultiplier = 0` yields zero ticks.

**RNG tests:**
- Known-answer vectors for PCG32 — catches an accidental algorithm change, which would otherwise silently invalidate every golden test in the project.
- Same seed ⇒ same sequence; different streams ⇒ different sequences.

**Logger tests:**
- Every entry carries the tick at which it was emitted.

---

## 9. Definition of Done

1. This spec written and current. ✅
2. Unit tests written and passing.
3. Determinism test green — and demonstrably *able to fail* (anti-vacuity tests present).
4. `dotnet build` clean with `TreatWarningsAsErrors` and the banned-API analyzer active.
5. `Bclone.Sim` has zero engine references and builds standalone in CI.
6. `DESIGN.md` Progress Tracker (§6) + Decisions Log (§7) updated.

No manual QA checklist here — there is nothing to watch yet. That arrives with Phase 0.

---

## 10. Open questions (resolve later; log to `DESIGN.md §7`)

- **`TargetTicksPerSec` value** — what pacing actually feels meditative? Placeholder for now; tuned in Phase 0 against the pace non-negotiable (Phase 0 spec §11 raises the same question).
- **`TicksPerDay` / days-per-season** — likewise Phase 0's call.
- **State hash vs. full serialization** — the hash is a cheap witness for tests. When save/load arrives, full canonical serialization will exist and the determinism test should probably assert on *that* instead, with the hash kept as the fast path.
- **Fixed-point introduction point** — `Fixed` (Q32.32) is specced but not built. It gets written the first time a system genuinely needs fractional math, not before.
