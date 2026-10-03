# Spec: Names, surnames and birthdays — a person is somebody's, and born on a day

**Decisions:** D395 (Joe, 2026-09-19: *"proceed"* — a prefix + suffix generator hashed from (seed,
villager id), never an `Rng` draw; `Household.Surname` with a `FullName` reader; `Villager.BirthTick`
with life stages on the exact birthday, its own golden-moving commit), D448 (*"when do we get to the
villager naming plan?"*), D465 (Joe, 2026-10-03: the slot; his prefix, suffix and surname lists; a
couple carries **the older partner's** surname). Neighbours: D344/D392 (*a list content can grow is
not a list a draw may range over*), D335 (a derived value is never hashed), D376 (renaming), D252
(`Founder`, never hashed), D190 (rhythm, drawn at birth).
**Status:** ✅ **BUILT — §3 FIRST NAMES (D466), §4 SURNAMES (D467), §5 BIRTHDAYS (D468) AND EACH
HOUSEHOLD'S OWN DAY FOR A CHILD (D469)**; D466–D468 played by Joe (*"looks great after playing"*);
merged to `main` and pushed at his word (D469). Owner: Joe + Claude Code.

---

## 1. Why this exists

§1.4 — *stories come from people, not spreadsheets* — and three things on screen undercut it today:

1. **The shipped game names its villagers from a pool of eight.** `data/sim.config.json` overrides
   the code's 32 with *Mabel, Otto, Bess, Silas, Agnes, Wendell, Hattie, Amos*; `DrawUnusedName`
   walks to an unused one and, past eight living, reuses. A village of forty is five Mabels.
2. **A household's name ignores the seed and the family.** `HouseholdNames[count % 6]`: every
   valley's first family is the Thatchers, the seventh household is the Thatchers again, and a couple
   taking over a dead family's house *becomes* that family because of whose house it was.
3. **Everybody's birthday is New Year's Day.** `ClockSystem` sets `AgeYears = Year − BirthYear`, so
   the four founders are the same age to the tick and every old-age death of a run falls on Day 1 of
   Spring.

§1.5 — *generational time is the core loop* — is the other half: a surname that passes from parent
to child is the first thing in the game that makes a lineage visible without a screen.

## 2. Joe's calls

- **First names are prefix + suffix** from his lists (2026-10-03): 100 prefixes, 100 suffixes. Two
  entries are repeated (*Val* twice in the prefixes, *tis* twice in the suffixes); the second copy of
  each is dropped — **99 × 99 = 9,801 names** — and the validator refuses a repeat from now on.
- **Surnames are his 60** (*Thatcher, Cooper, Mason … Stout, Sharp*), replacing the six.
- **A couple carries the older partner's surname** (D465). The sim has no gender, so there is no
  default to inherit; *the elder's name* is a rule the player can read off two cards.
- **Hashed, never drawn** (D395). A name is a function of (seed, id) — content can grow without
  reshuffling a seed (D392), and naming takes nothing from the stream the economy draws on.

## 3. First names (commit 1)

**Data.** `first_name_prefixes` and `first_name_suffixes`, code defaults in `SimConfig` — the
`household_names` / `town_names` / `skills` precedent: one source of truth, a modder's json replaces
a list wholesale. `villager_names` and the shipped json's eight-name override are **deleted**; there
is one way to name a person.

**The rule.** `NameHash.Mix(seed, id, salt, attempt)` is a splitmix64 finaliser (the shape of
`MapGenerator.HashJitter`). A villager's first name is `prefixes[Mix(…, PrefixSalt, k) % P] +
suffixes[Mix(…, SuffixSalt, k) % S]` for the first attempt `k = 0, 1, 2, …` that **no living villager
holds**. Plain concatenation — *Ag + nes* is Agnes, *Mab + bel* is Mabbel; it is his list.

- **The dead free their names** (`IsNameInUse` counts the living, unchanged): a grandchild may be
  Agnes again.
- **Bounded:** after `P × S` attempts the first attempt's name is used — a repeated name is a
  blemish, a crash is a bug (the old rule).
- **Founders and newborns the same way.** Nothing else reads the id-to-name mapping.

**⚠️ What it costs: one `Rng` draw fewer per founder and per birth.** The stream shifts, so **every
seed replays as a different valley's history** — every golden moves once, and Joe's familiar seeds
will not play as he remembers them. That is the price of *never a draw* and D395 accepted it. It is
measured (§7) to show it is a reshuffle, not a change in kind. **Built (D466): 55 villages 390 → 453
alive, 100 seeds 488 → 473 — opposite signs.** ⚠️ It also exposed that the founders' anti-lockstep
rotation, `(draw + a)`, guaranteed nothing — it held only while the stream happened to repeat its
draws; a household's rhythm is now drawn once and each adult a step on from it (D466).

**Not hashed.** `Villager.Name` is not in `StateHash` today and stays out: it is a function of the
seed, the id and who is alive, all of which are.

## 4. Surnames (commit 2)

**`Household.Surname`** is the family's name. `Household.Name` is what the village calls the
household: the player's rename if there is one (D376), otherwise the surname.

- **⛔ A rename is the household's label, never anybody's surname.** The rename box is free text
  (*"the Ashfords"*, *"Wendell's lot"*); a villager called *Ren the Ashfords* would be a bug. So a
  child born after a rename takes the **surname**, and the rename changes no card's title. (The plan
  said a renamed family would pass its name down; reading the rename box said otherwise.)
- **The founding households:** `surnames[Mix(seed, householdId, SurnameSalt, k) % N]` for the first
  `k` no living household carries.
- **A new couple** (`HouseholdSystem.Pair`, and a couple taking over an empty house): the household
  carries **the older partner's surname** — the earlier birth (`BirthYear` in commit 2, `BirthTick`
  from commit 3), the lower id on a tie. Two siblings who both pair start two households of one
  name; that is what families do, and each card says whose they are.
- **Taking over a dead family's house takes the couple's name** and drops any rename the player gave
  the dead family. The house is the same building; the household is a new family.
- **`Villager.Surname`** is stored — set at the founding, at birth (the household's surname), and on
  moving in (the new household's). It has to be stored rather than read through the household,
  because **the dead stay in their household's member list and a takeover renames it**: read through
  the household, a dead Thatcher would become a Cooper after death.
- **`Villager.FullName`** is `"{Name} {Surname}"`.
- **Not hashed**, like `Name`: a function of the seed, the ids and the pairing rule, all hashed.
  `GivenName` stays hashed sparsely (D376) — it is the player's input.

**Where it shows.** The person card's title and the roster line use the full name; the household
card says *The Cooper household* and lists its members by first name; the town hall's founders by
full name. In the village log, births, pairings and deaths use the full name; everything else keeps
the first name (a season line is not a census).

*"Agnes Cooper of the Cooper household and Ren Mason of the Mason household started the Cooper
household — Agnes's name, as the elder."*

**Built (D467)** as written, with the pairing line reading *"Agnes Cooper of the Cooper household and
Ren Mason of the Mason household started the Cooper household (Agnes's name, as the elder)"* and a
takeover *"… took over the empty Ashfords house - the Cooper household now (Agnes's name, as the
elder)"*. No golden moved. The guards are `SurnameTests` (the table in §6, the takeover and the dead
keeping theirs as one guard), red-checked under eight mutants.

**Out of scope:** a lineage *map*. Parent ids belong with the town hall's records (`town-hall.md`),
where Joe put *"some version of mappable lineage"*.

## 5. Birthdays (commit 3)

**`Villager.BirthTick`** (hashed) replaces `BirthYear`. It may be negative: a founder was born before
the valley was.

- **Newborns:** `BirthTick` = the tick they are born on.
- **Founders:** `−FounderAge × TicksPerYear − Mix(seed, id, BirthdaySalt) % TicksPerYear` — every
  founder is still `founder_age` on the first tick, and each has a birthday of their own in Year 1.
- **`AgeYears` stays a field and is maintained on the villager's birthday**:
  `(tick − BirthTick) % TicksPerYear == 0` → one year older. That is the CLAUDE.md rule (*maintain it
  where the state changes*) replacing a per-tick recomputation. It is derived from `BirthTick`, so
  it **leaves the hash** (D335).
- **Everything that reads `AgeYears` now turns on the exact birthday:** coming of age
  (`AgeingSystem.StageForAge`) and old age (`MortalitySystem` — lifespan is in whole years), so the
  year's old-age deaths spread across its days. Pairing still runs at the year's turn and reads age
  as it stands. Fertility and `LastBirthYear` are unchanged.

**On the card:** *born Day 20, Fall, Year 10* (`SimClock.ToString()` of the birth tick). A founder:
*born Day 12, Summer — 20 years before the founding* — no negative years on screen.

**⛔ FOUND WHILE BUILDING (D468): EVERY CHILD WAS BORN ON DAY 1 OF SPRING** — `HouseholdSystem` decided
births once a year, at its turn, so only the founders' birthdays were spread.

**✅ EACH HOUSEHOLD TRIES FOR A CHILD ON A DAY OF ITS OWN (D469, Joe: *"spread births through the year -
each household has its own day"*).** `Household.DayForAChild` is a hash of the seed and the household's
id (`NameHash.DayForAChild`, never a draw), set once when the household is made — at the founding or
when a couple pairs — and not hashed (a function of hashed things). On the first tick of that day each
year, and only then, the household is asked the same questions as before (`IsReadyForAChild`: room, a
roof, the interval, the village's food, the harvest, a fertile couple) — read as the village stands on
that day. Still once a year, so `birth_interval_years` means what it said and a household's children
are whole years apart. Pairing stays at the year's turn. ⚠️ A founding household may now have a child
in its first year (nobody was born before Year 2's turn until D469).

**⚠️ What it costs:** every golden moves a second time (`BirthTick` hashed, `AgeYears` not), and a
year's old-age deaths and comings-of-age move within the year. Measured (§7); the year-boundary age
guards are re-posed, each with its reason.

## 6. Guards

| Guard | Claim |
|---|---|
| `NamingDrawsNothingFromTheStream` | the RNG state is identical before and after naming a villager |
| `NoTwoLivingVillagersShareAFirstName` | a fifty-year village never holds two living of one name |
| `TheSameSeedAndIdGiveTheSameName` | naming is a function, not a history |
| `ADeadVillagersNameMayComeBack` | a collision only counts the living |
| `ANameListMayNotRepeatAnEntry` | the validator refuses a repeated prefix, suffix or surname |
| `FoundingHouseholdsHaveDifferentSurnames` | the founding never names two families alike |
| `ACoupleCarriesTheOlderPartnersName` | the elder's surname, the lower id on a tie |
| `TakingOverAnEmptyHouseTakesTheCouplesName` | and drops the dead family's rename |
| `TheDeadKeepTheirSurname` | a takeover does not rename the dead |
| `ARenameIsALabelNotASurname` | a child born after a rename takes the surname |
| `FoundersAreTheirAgeOnTheFirstTickWithBirthdaysOfTheirOwn` | `founder_age`, distinct birth ticks |
| `AVillagerAgesOnTheirBirthdayNotAtNewYear` | the year turns nobody older but whose day it is |
| `AChildComesOfAgeOnTheirBirthday` | `LifeStage` flips on the birthday tick |
| `TheBirthTickIsInTheFingerprint` | hashed; and `AgeYears` posed alone does not move the hash |
| `EachHouseholdTriesForAChildOnItsOwnDay` | every birth on its household's hashed day; siblings whole years apart, at least the interval; not all at New Year (D469) |

Every guard red-checked and the reds counted (D326).

## 7. Measured before each golden-moving commit merges

D420's 55 unattended villages and the 100 shipped seeds 200–299, fifty years, against `main`:
alive / peak / starved / cold. Expected: a reshuffle inside the arms' ±50 noise — these are
unattended villages and are read as that, not as a verdict on the design.

## 8. Definition of Done

Spec current; the guards above green and red-checked; determinism green; the goldens moved only in
commits 1 and 3 and each move explained; view built with 0 warnings; probe green (bar height 161,
the card's longest *born …* line posed); `DESIGN.md` §6/§7 and `HANDOFF.md` updated; **Joe plays it**.
