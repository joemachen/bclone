# Spec: Phase 0 — Vertical Slice (one villager, one resource loop)

> Status: **not started** · Owner: Joe + Claude Code · Phase gate: **yes** (must pass before any Phase 1 work)
> Format per `METHODOLOGY.md §2`. This is a living doc — update it if reality diverges.

---

## 1. Goal

The entire game, shrunk to a single soul. One villager must **gather food → eat → survive a winter → age → die.** The player watches, and can read — off the screen and the life log — *exactly why* that villager lived the life they did and died the death they died.

This phase exists to prove the **feeling**, not to build systems. If watching one villager live and die doesn't land emotionally, no amount of later economy/labor/tech sim will save the game. Everything in Phases 1+ bolts onto the spine built here.

**This is the phase gate.** Do not start Phase 1 until the Success Test (§9) passes.

---

## 2. Which pillars / non-negotiables this serves

- **Non-negotiable: Legibility above all.** The life log and UI must make the causal chain of this villager's life fully readable. This is the primary deliverable, not a nice-to-have.
- **Non-negotiable: Stories come from people, not spreadsheets.** The villager has a name and a readable life; their death is an event with a cause, not a decremented counter.
- **Non-negotiable: Meditative pace.** Calm, slow, watchable. No micro.
- **Non-negotiable: Generational time is the core loop.** Aging and a natural death-of-old-age are in from the first slice — this is the seed the whole generational system grows from.
- **Architecture: deterministic fixed-timestep tick loop, decoupled from render** (`DESIGN.md §3`). Established here, relied on forever.
- **Architecture: data-driven from day one.** All tunables live in a data file, not in code — the habit starts now.

---

## 3. Scope

### In scope
- A simulation clock: **tick → day → season → year**, fixed timestep, deterministic.
- Four seasons; **winter is the pressure** (food cannot be gathered — the villager lives off what they stored).
- One villager with: a name, an age, a hunger need, a behavior state, and an alive/dead status.
- One food source that yields food in non-winter seasons.
- A small personal food stockpile.
- A minimal behavior state machine (gather / eat / rest).
- Two death paths, clearly distinguished: **starvation** (failure) and **old age** (a full life).
- A **life log**: tick-stamped, human-readable narrative events — the heart of the legibility deliverable.
- A UI legible enough to read the villager's state, the clock, the stockpile, and the life log at a glance.
- A determinism test and unit tests (see §8).

### Out of scope — explicitly deferred (do NOT build these here)
- A second villager, reproduction, households → later.
- Labor/job assignment system (`DESIGN.md §2.2`) → Phase 1.
- Skill growth & knowledge transfer (`§2.1`), tech tree (`§2.7`) → later phases.
- **Pathfinding & desire-path roads** (`§2.6`) → deferred. Movement here is abstract: the villager has a position and travel to the food source costs a fixed number of ticks (distance-based, straight-line). This establishes *position* as a concept without building the pathfinding/cost-field system. **Do not build pathfinding in Phase 0.**
- Warmth/cold as a separate stat → deferred. Winter's danger in Phase 0 is **food scarcity only**, one survival axis. Do not add a second overlapping death system.
- Economy, trade, region (`§2.4`); biomes (`§2.5`); systemic pressures — soil, disease, climate drift (`§2.3`) → later.
- Save/load → deferred (determinism makes it cheap to add later; not needed for the Success Test).

If any of the above starts feeling necessary to hit the goal, **stop and flag it** — it almost certainly means the slice is being over-scoped.

---

## 4. Data model (language-agnostic — implement in the chosen stack's idiom)

Keep all sim state in plain, pure data structures. Sim logic operates on them deterministically; the renderer only *reads* them.

```
SimClock {
    tick: u64            // atomic sim unit, monotonic
    ticks_per_day: u32   // from config
    day_of_season: u32
    days_per_season: u32 // from config
    season: enum { Spring, Summer, Fall, Winter }
    year: u32
}

Villager {
    id: u32
    name: string         // for story legibility — "Mabel", not "Villager_01"
    age_years: u32
    hunger: u32          // 0 = full, rises over time, capped at hunger_max
    state: enum { Idle, TravelingToFood, Gathering, Eating, Resting, Dead }
    position: Vec2        // abstract; used only for travel-time, not pathfinding
    alive: bool
    cause_of_death: enum { None, Starvation, OldAge }
}

FoodSource {
    position: Vec2
    gatherable: bool     // false in Winter
    yield_per_gather: u32
}

Stockpile {
    food: u32
}

LifeLogEntry {
    tick: u64
    season/year: (for display)
    text: string         // "Winter began — 14 food stored", "Died of old age at 52"
}
```

### Config (data-driven — lives in a data file, NOT hardcoded)
Establish the data-driven principle here. Example tunables (format per chosen stack — RON/JSON/etc.):

```
ticks_per_day        = 4
days_per_season      = 30
hunger_per_tick      = 1
hunger_max           = 100
starvation_ticks     = 20     // ticks at hunger_max before death
eat_reduces_hunger   = 60
gather_yield         = 8
gather_ticks         = 3      // time to gather once at the source
travel_ticks_per_unit= 1      // abstract movement cost
food_per_meal        = 5      // stockpile consumed per eat
lifespan_years       = 50     // + small seeded variance for a natural feel
seed                 = 12345
villager_names       = ["Mabel", "Otto", "Bess", ...]
```

---

## 5. Tick update order (explicit — ordering is part of determinism)

Every tick, in this exact order:

1. **Advance clock** — `tick++`; roll over day → season → year as thresholds hit; on new year, advance villager `age_years`.
2. **Update needs** — `hunger += hunger_per_tick` (clamped to `hunger_max`).
3. **Villager decides & acts** — evaluate state machine (§6) and advance the current action by one tick.
4. **Check death** — starvation (hunger at max for `starvation_ticks`) or old age (`age_years >= lifespan`). Set `alive=false`, record `cause_of_death`.
5. **Emit life-log events** — for anything notable this tick (season change, first gather of the day, hunger critical, death, etc.).
6. *(Render, decoupled, interpolates between ticks — never mutates sim state.)*

RNG: a single seeded generator, used only where variance is wanted (lifespan variance, name pick). No wall-clock, no unseeded randomness, no iteration-order-dependent behavior anywhere in steps 1–5.

---

## 6. Behavior (minimal state machine)

Priority logic each tick, evaluated top-down:

- If **dead** → `Dead`, no action.
- If **hunger high** AND **stockpile has food** → `Eating` (consume `food_per_meal`, reduce hunger).
- If **stockpile low** AND **food is gatherable** (not winter) → travel to source (`TravelingToFood`) → `Gathering` (add `gather_yield` to stockpile after `gather_ticks`).
- Else → `Resting` / `Idle`.

The intended emergent arc: gather a surplus across spring/summer/fall, live off the stockpile through winter, repeat for years, age, and die of old age. The failure arc: insufficient surplus → starve during a winter. **Both arcs must be legible in the life log.**

---

## 7. Life log & UI (the legibility deliverable — this is the point)

**Life log** is the primary artifact. Tick-stamped, plain-language, narrative. It should read like a life:

```
Spring, Year 1 — Mabel begins. 0 food stored.
Day 6 — gathered berries (8 → 24 stored).
Winter, Year 1 — foraging stops. 41 food stored.
Winter, Year 2 — food running low (6 left).
Day 18, Winter, Year 4 — hunger critical.
Day 21, Winter, Year 4 — Mabel died of starvation, age 4.
```
…or, on the good path: `Fall, Year 52 — Mabel died of old age, having survived 51 winters.`

The life log should share the same backing as the structured logger from `METHODOLOGY.md §4` (tick-stamped, leveled) — the player-facing life log is essentially the `INFO`-level narrative view of sim events.

**UI (minimum):** current tick/day/season/year; villager name, age, hunger, current action; stockpile count; scrolling life log. Legible at a glance, calm, uncluttered. No polish required — clarity required.

---

## 8. Testing (per `METHODOLOGY.md §3`)

**Unit tests (pure sim logic):**
- Clock rollover: ticks → day → season → year, including year boundary advancing age.
- Hunger accrues per tick and clamps at max.
- Gathering adds `gather_yield` to stockpile after `gather_ticks`; not possible in winter.
- Eating consumes stockpile and reduces hunger; blocked when stockpile empty.
- Starvation death fires exactly at the threshold (define boundary: `>=`).
- Old-age death fires at lifespan; `cause_of_death` set correctly for each path.

**Determinism test (P0 — write this first, keep it green forever):**
- Run N ticks twice from the same seed + config; assert **byte-identical final state AND identical life log**. A failure here is a P0 bug.

**Scenario / golden tests (prove both arcs + lock behavior):**
- "Scarcity" config → villager reliably starves in an early winter.
- "Plenty" config → villager reaches old age.
- Golden replay: a fixed seed + config produces a known life story; lock it and catch drift.

---

## 9. Definition of Done

Standard DoD (`METHODOLOGY.md §3`):
1. This spec is written and current.
2. Unit tests written and passing.
3. Determinism test green.
4. Manual QA checklist passed (see §10).
5. No new errors in the log during a clean playthrough.
6. `DESIGN.md` Progress Tracker (§6) + Decisions Log (§7) updated.

**Plus the phase Success Test (the gate):**
> A person watches the villager live and die, and it *means something*. They can read the life log afterward and understand — without guessing — exactly why this villager lived as long as they did and died the way they did. A death of old age after a long life feels different from starving in the second winter, and the log makes the difference plain.

If that subjective test fails, the phase is not done — regardless of green tests. Legibility and feeling are the deliverable.

---

## 10. QA checklist (manual, per playthrough before merge)

- [ ] The clock advances smoothly; seasons and years roll over correctly on screen.
- [ ] I can tell, at any moment, what the villager is doing and why.
- [ ] A starvation death and an old-age death both occur (across runs/configs) and are clearly distinguishable.
- [ ] The life log alone is enough to reconstruct the villager's story.
- [ ] Same seed → same life, every time (determinism holds in practice, not just in the test).
- [ ] Pace feels calm and watchable, not busy.
- [ ] Nothing on screen is unexplained or requires reading code to understand.

---

## 11. Open questions

### Resolved 2026-07-25 — pacing (derived from "a life should take 9–12 minutes")

Joe set the target: **a full life plays out in 9–12 minutes of real time.** Everything else falls out of that.

| Value | Setting | Reasoning |
|---|---|---|
| `ticks_per_day` | **4** | A day is four beats — enough granularity to see an action take time, few enough that days pass readably. |
| `days_per_season` | **15** | 60 ticks per season, 240 per year. Long enough for winter to bite, short enough that a year reads as one breath. |
| `target_ticks_per_second` | **20** | 240 ticks/year ÷ 20 = **12 s per in-game year**. |
| `lifespan_years_base` | **52** | 52 × 12 s ≈ 10.4 min — the middle of the window. |
| `lifespan_years_variance` | **±6** | Range 46–58 years ⇒ **9.2–11.6 min**. Lands inside 9–12 for every seeded outcome, and a little spread stops old-age death landing on a suspiciously round number. |

**Hunger cadence is the legibility lever.** `hunger_per_tick = 10` against `hunger_max = 100` with an eat threshold of 80 means the villager eats **every two days** — a rhythm a person can read off the screen without arithmetic. It also gives ~30 meals a year, so winter (60 ticks ≈ 7–8 meals ≈ 38 food) demands a real stockpile rather than a token one. That is what makes the winter drain visible as a sawtooth in the food counter: climbing through spring/summer/fall, falling through winter.

### Resolved 2026-07-25 — other open questions

- **Starvation boundary:** `>=`. Death fires when the villager has been at `hunger_max` for `starvation_ticks` or more — 24 ticks, i.e. **six days at maximum hunger**. Chosen over `>` so the boundary is inclusive and easy to state in the log ("went hungry six days").
- **Lifespan variance:** small seeded spread (see table), drawn once at birth from the sim RNG.
- **Calendar is derived, not stored.** Day/season/year are a pure function of `tick` and config (`SimClock.FromTick`). Less mutable state means fewer places determinism can break, and the calendar can never drift out of sync with the tick.

### Still open (carry into Phase 1)

- **Childhood.** The villager is able-bodied from age 0, which is obviously wrong — a toddler does not forage. Phase 0 has no childhood mechanics by design (out of scope, §3), and with a single villager there is nobody to depend on. Dependency and age-gated capability belong with households in Phase 1. **Flagged rather than silently shipped.**
- **Stockpile is always accessible**, not stored at a location. Keeps Phase 0 from producing a "starved two tiles from home" death that would read as unfair rather than instructive. Revisit when granaries arrive.
