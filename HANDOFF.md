# Handoff — bclone: **Phase 2 is one QA pass and three chores from done**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D162 back to D142**, then
`METHODOLOGY.md`. `specs/crops-and-orchards.md` is now a **record** rather than a job — read §12
if you touch the crop numbers, and nothing else there needs you.

---

## Where things are

**Branch `phase/2-wood-fuel-and-tools`.** The farm (D162) is in.

**Suite: 612 passing, 0 failing, 2 skipped of 614. Green** (was 589 / 0 / 2 of 591). Both skips
are rulings, not debt (D143's unattended village; D134's granary cap). **The full run is ~13
minutes.**

**The Godot view builds** — `dotnet build src/Bclone.Game/Bclone.Game.csproj`. The solution build
does **not** cover it (D11), and since D160 the view has **no automated verification of any
kind**: looking at it is the test.

---

## ⛔ THE JOB: close Phase 2 and merge it.

**`DESIGN.md §4`'s Definition of Done, and only two of five items are left that are yours.**

1. ✅ **Crops** — D162. Built, guarded, documented.
2. ✅ **A golden over a village that clears ground** — `FarmGoldenTests`, which does it and the
   crop seam in one run. D157's hole is closed.
3. ⛔ **A QA playthrough against a written checklist** — **Joe walks it, not you.** Phase 1 has
   one in its spec and Phase 2 has none. **Writing the checklist is yours; walking it is his.**
   Start from Phase 1's and add what Phase 2 built: the build queue, stock limits, the
   professions panel, the brushes, the minimap, and now the farm.
4. ⛔ **The release blockers** (METHODOLOGY §5): `VERSION` is read by nothing, and
   `src/Bclone.Game/export_presets.cfg` does not exist — without which `release.yml` can never
   succeed, because it exports the "Windows Desktop" preset from a clean checkout.
5. ⛔ **`CHANGELOG.md`'s header** still instructs the practice METHODOLOGY §5 deleted (D160): it
   says the `[Unreleased]` section accumulates as we work, and the rule is now that it is
   written in one pass at the first tag.

**Then merge to `main` via PR #3.**

---

## ⭐ What the farm slice left open, and both are Joe's calls

### 1. ⚠️ The derived field is about twice what a farmer really gets through

`FieldTilesOneFarmerKeeps` says **13 tiles**; the seam golden measures **≈5.75 reaped a year**.
Every other budget in `VillageEconomy` is a worst case of *cost*; this one over-states
*capacity*, which is the unsafe direction — the village believes a farm feeds a household when
it feeds rather less. The gap is the ordinary business of a working day: meals taken mid-field, a
fetch for one's own larder, a walk in from the cold. **`TripsPerYear` carries the same gap and
has never stated it**, so this is a pattern rather than a farm bug.

**Recorded rather than tuned** (D112's rule). The honest fixes are a season budget that charges
the working day's interruptions, or a stated derating factor applied to all of them — and either
one re-derives the whole food economy, so it wants Joe's word and a fresh session.

### 2. The farm has one seat, and that is on purpose

`RequiredFarmerSeats` = *one farm keeps one household fed*, which comes out at **1**. Scale is a
second farmhouse — `granary_feeds_people`'s pattern deliberately reused (D39). **If Joe plays it
and a one-seat building reads as broken rather than as small, that is the number to revisit**,
and the derivation is where to do it rather than the config.

---

## ⚠️ Traps this session met, in the order they will cost you

- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Done twice for the demand arm: **3 of 5**
  with it disabled, **2 of 5** with only the seasonality removed. The two that stay green both
  times are the anti-vacuity guards that assert zero — which is what counting tells you and
  running does not.
- **⭐ `SimLoop` runs the systems and *then* advances the tick.** Third and fourth instances this
  week. The seam golden's first run reported *"0 lost to winter, 344 vanished unexplained"* — a
  harvest apparently being eaten by the harvest brush — and the bug was that the harness read
  `Clock.Season` one tick to the right of the event. **An off-by-one in a harness reads exactly
  like a broken feature**, and the temptation is to go and fix the feature.
- **⭐ A GREEN GOLDEN CAN MEAN "NOT COVERED"** (D157) — and it did again. The plan said the two
  50-year goldens were *supposed* to move once a farmer sowed. **They did not, because neither
  village ever places a farmhouse.** Say which of the two it is, measured, before you believe
  either.
- **A derivation that reads a number derived from itself is not a derivation.** *"Enough yield
  that a farm's seats feed a household"* produced a farmhouse with fourteen seats and 173 food
  from one tile. State the target as a **comparison** against something already derived.
- **A control tested at its predicate and never at its deposit is a control nobody has tested**
  (D144). The market's widened reach needed the *loading* branch as well as the *choosing* one,
  or traders walk to the farm and stand there.
- **A new deposit path means a new leak.** `RetireWorkplace` had ignored `Workplace.Store` for
  five phases — correctly, because nothing wrote to one. The farm's buffer made demolition
  destroy up to 100 food silently. **Found by reading the method, not by a failing test.**
- **`python` string edits die on this repo's CRLF *and* its emoji** (`UnicodeDecodeError:
  charmap`), and `python` is not even on PATH here. Use the Edit tool.
- **`dotnet test` buffers stdout when redirected**, so a background run looks frozen at zero
  lines for a quarter of an hour. `Get-Process testhost` and look at CPU to tell working from
  hung.
- **The full suite is ~13 minutes.** Background it, and **do not start a second one** — the first
  holds a lock on `Bclone.Sim.dll` and no test project will build until it exits, so a second run
  fails on the copy step and wastes the wait.

---

## Working with Joe

Technical, not a game/systems programmer. Casual, direct; **push back honestly**. **End every
message with the explicit ask**, or he cannot tell who is blocking whom.
