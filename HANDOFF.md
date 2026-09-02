# Handoff — bclone: **✅ ON `main`, 900 GREEN. NEXT IS THE REST SLICE; TWO CALLS WAITING ON JOE.**

> **⭐⭐ START HERE. WHERE THINGS ACTUALLY ARE, 2026-08-29 (evening).**
> **900 passing, 0 failing, 1 skipped of 901** — **on `main`, merged and pushed 2026-09-01.**
> `main` = `origin/main` = `slice/two-seats-per-hut`, a clean fast-forward. ⭐ *D217's trap is not
> live: the build Joe plays and the remote are one thing.*
>
> **✅ THE FIRE BURNS TWICE AS FAST (D273, Joe)** — `firewood_per_winter_day` 1 → 2, exactly 2×,
> keeping the three-day beat (the interval dial cannot reach 20 on a 30-day winter). **Measured
> before it was believed: zero frozen either way**, peak 20 against 21, peak year 60 → 262.
> ⛔⛔ **AND D50's GUARD CAUGHT THE BILL THE SAME MINUTE** — *"woodcutter_hut_capacity is 2 but
> heating 20 households needs 3 seats"*, which is the sentence from the run where thirty-six
> people froze. Raised 2 → 3 **as a consequence, not a choice**.
> ⚠️ **TWO OPEN CALLS FOR JOE, BOTH IN D273:**
> 1. **Should a woodcutter's hut seat 3, or should the player build a second one?** Raising it
>    sits against the two-seat doctrine (D256, D262, D267). It was raised because that was the
>    conservative move inside a firewood change — **the doctrinal answer is his.**
> 2. **The fixture and the shipped file disagree about the burn interval** — 4 in
>    `VillageFixtures`, 3 in `data/sim.config.json`, so a fixture winter costs 15 where the
>    game's costs 20. The fixture's own comment is about closing exactly this gap and closed only
>    half of it. Untouched here because it moves goldens for a reason nobody asked for.
>
> **✅ AND THE DISTANCE WARNING SAYS WHAT IT COSTS (D272)** — *"a pair of hands here does about
> 14% of the work a pair at the door would — the rest is road"* for a workplace, *"every load
> carried in or out is a 9-tile walk each way"* for a store. One arithmetic
> (`SimWorld.ShareOfTheDayThatIsWork`), read by the placement warning and the commute note both.
>
> **⭐⭐ NEXT SLICE IS THE REST SHARE (Joe: "rest slice next").** `rest_share_percent` at 10 to
> start. ⛔⛔ **IT MUST BE DERIVED, NOT BOLTED ON**: `VillageEconomy.TripsPerYear` is
> `available ÷ RoundTripTicks`, so the rest share comes out of `available` and every anchor
> re-solves. Bolt 10% on top and every village is 10% poorer against unchanged anchors and the
> thin valleys die — **that is D50's shape, which fired twice this week already.**
> ⚠️ **Measure the FARM specifically**: autumn is a deadline, not a rate. The one previous
> attempt at resting job-holders measured **96% → 74% of what a farm sowed** (D250), which is why
> the rest spell is scoped to `IsLaborer` today.
>
> **⭐⭐ 2026-09-01 — "A JOB IS A JOB" (D270–D271), AND NOT ONE GOLDEN MOVED.** Joe, on a
> forester's hut reading *"nobody working of 2 seats · asked 1 · village wants 0"*: *"It **IS**
> staffed and somebody **DOES** work there."* ⛔ **The panel was not lying — the hut really did
> empty.** `LabourQuota.Asked` cut the seat when a stock limit was met, which contradicted
> **D238, Joe's own earlier call** (*"a met stock limit stops the job and LEAVES the trade"*) —
> built in `BehaviorSystem` and never in the quota.
> ⭐ **THE RULE NOW: the stop lives where the work happens, never on the roster.** The player's
> profession number is the answer. Every capped trade already gates itself (woodcutter D139,
> forager D238, forester `MayFell`, farmer `MaySow`), which is why removing the override was
> safe — **red-checked at 489 firewood against a limit of 40** with the woodcutter's gate
> disabled, reproducing Joe's own *"452 at a limit of 50"*.
> ⚠️ **§4a WAS NOT TOUCHED and did not need to be** — the plan called it the riskiest edit.
> `Asked` ignores the village's figure whenever the player has set a number, and the panel sets
> one for every trade from the first frame, so *"hunger has nothing to do with whether you are
> assigned a job"* falls out of D106's ordering. **§4a still governs any trade left unset, which
> is how the fixtures run** — do not delete it.
> ⛔⛔ **AND A TRAP FOR THE NEXT PERSON: TWO GUARDS WERE PASSING AGAINST A CORPSE.** Posing
> *"cap logs at 0 from the founding"* stops the fuel chain before it starts — **all four founders
> dead by Year 2** — and `IdleNote` is null for a hut in a dead village too. **Let the village
> stand up for twenty years before posing a limit, and assert `Population > 0`** (D7).
> ⚠️ **`main` is at 881 and does NOT have any of this.**
>
> **✅ FIVE MORE THINGS FROM JOE'S PLAY ON 2026-08-30 (D266–D269), ALL BUILT AND GREEN:**
> - **A workplace says WHY nobody is needed** — *"nobody works here — the village needs no
>   foresters at the moment, because the logs limit of 200 is met"* — in the **same words** the
>   professions column uses, from one method (`LabourQuota.WhyTheVillageWantsNone`) that reads the
>   state the quota reads. He was looking at two true sentences that did not add up.
>   ⛔ **And `IdleNote` stopped flagging every empty building**: it fired for *every* unstaffed
>   workplace, so a hut standing quiet because the player's own limit was met got the same marker
>   as one the village was crying out for — D147's own rule broken by D147's own method.
> - **The builder's hut holds three** (`builder_hut_seats`), stated where it was derived. Four
>   goldens moved for it, in one commit. See the open call below — the hut is still free.
> - **Alerts slow the village to 1× rather than pausing it**, and hand back the speed the player
>   chose. One hold shared by both kinds, so a gift arriving under a discovery banner cannot
>   record 1× as *"the speed they were at"*. `Moment.Stops` → **`WaitsToBeDismissed`**.
> - **The side panels are drawn at four fifths** (`Main.PanelScale`). ⚠️ The first cut divided the
>   column WIDTH by the scale too and gave the same screen width with more text in it — the
>   opposite of the ask. Measured with the headless probe: lays out at 346, drawn at 277.
> - ⚠️ **NOBODY HAS LOOKED AT ANY OF THE FOUR ON SCREEN.** They compile, the suite is green and
>   the game starts clean headless — which is what the view has always had (D160), and seven
>   features have shipped here broken because that was mistaken for verification.
>
> **✅✅ ALL OF IT IS MERGED TO `main` AND PUSHED (2026-08-29, Joe: *"Both: Merge to main, push the
> branch"*).** A clean fast-forward, so **`main`, `origin/main`, `slice/the-founders-hall` and
> `origin/slice/the-founders-hall` are all the same commit** — `d0259a5`. Clean tree, both
> projects build. ⭐ **D217's trap is not live: the build Joe plays and the remote are one thing.**
>
> The only other branch is `slice/work-from-the-steading`, one unmerged commit, **93+ commits
> behind** and backed up on `origin` — a decaying asset, not a parked one.
>
> **⭐⭐ JOE SETTLED BOTH OPEN CALLS IN ONE MESSAGE, AND SLICE 1 WAS BUILT ON THEM (D252, D253):**
> 1. **The town hall's trigger is *the last founder dies*, and the gift is a tribute/monument to
>    the founding members.** ⛔ Not the forgiving alternative — a ratio crosses in silence, a
>    funeral is an event the village already narrates.
> 2. **After the town hall comes fishing and hunting** (`buildings-plan.md §10`, now step 0 then 1).
>
> **✅ WHAT IS BUILT AND GREEN** (`specs/town-hall.md`, slice 1 of four): the last founder dies →
> a **stopping moment naming all four** → a **Civic button appears with a ★** → the player places
> it → the crew raise it (**materials free, work owed**, exactly like the library) → it stands, is
> drawn, and clicking it **lists the founders by name**. `Villager.Founder`, `CivicSystem` (a
> thirteenth system — **nomads land there next**), `BuildingKind.TownHall = 11`, and
> `BuildingRow.Civic` / `.Singleton` as **data columns a modder can reach**.
> ⛔ **THERE ARE NO TABS.** Collections, knowledge and charts are slices 2–4.
>
> **⭐⭐ MEASURED: THE SHIPPED VILLAGE IS OWED ITS HALL IN YEAR 58, WITH 35 ALIVE.** The suite's
> fixture reaches it in year 30 with 14 — *and that is why exactly two goldens moved and the
> shipped pair did not.* Well clear of D227's *"you just stabilised, now build a library?"*.
>
> **⛔⛔ TWO THINGS ARE RECORDED RATHER THAN TICKED, AND THEY ARE THE FIRST THINGS TO DO:**
> - **NOBODY HAS LOOKED AT THE VIEW.** The button, the map colour, the inspector panel and the
>   moment are built and compiled; the view has **no automated verification of any kind** (D160).
>   ⭐ *Looking at it is the test, and it needs Joe.* The thing to look at is **the year-58 moment**.
> - **The 200-year clean-log playthrough has not been run.** DoD item 6.
>
> **✅ AND JOE PLAYED IT AND IT WORKED** — the moment fired at **Year 58, exactly as measured**,
> he placed the hall and it was built. His three follow-ups are done (D254, D255): **a 20x speed**,
> **`Skip 1y` / `Skip 10y` QA buttons** (debug builds only — ten years in a quarter of a second),
> and **the village log is timestamped and set one point smaller.**
> - ⚠️ **ONE THING IS OFFERED AND NOT DONE, AND IT IS HIS CALL:** ten narrating call sites still
>   write the date *mid-sentence* (*"Amos was born to the Thatcher household — Spring, Year 2."*),
>   so about half the stamped lines say it twice. **Taking it out is a change to the game's
>   narrative voice across five systems**, and two of those lines carry `Day 4, Spring, Year 58`,
>   which is more precise than the stamp. **Ask him before doing it.**
>
> **✅✅ THE RECONCILIATION IS DONE AND THE SUITE IS GREEN AT TWO SEATS (D262–D264).**
> **894 passing, 0 failing, 1 skipped of 895**, on `slice/two-seats-per-hut` — **NOT on `main`.**
> Joe's call taken as an override (*"my QA trumps your tests"*); the cap, competing rings and the
> warm start's missing food are all built. ⭐ **Measured: nothing dies out** — the shipped seeds
> settle at 20, 23 and 15 against peaks of 26, 32 and 18.
>
> **⭐⭐ TWO REAL SIM BUGS CAME OUT OF IT, WHICH IS WHY IT WAS WORTH DOING BY HAND:**
> - **`LabourQuota` was booking hands for seats that do not exist.** It took as many as it needed
>   to feed everyone and gave every spare body to the berry patch — harmless at seven seats, a
>   labour bug at two. **Measured: *"nobody was ever posted to the forester's hut"***, five hands
>   queued for a room that holds two while farming, timber and the market went unstaffed. Bounded
>   by `SimWorld.GatheringSeats()` in both places now; `ForagersToFeedEveryone` stays the honest
>   need so the panel can still say *"the village wants five"* while two sit down.
> - **`GatheringSeats` asked `Capacity` where `Workplace.Places` exists** — the field whose own doc
>   says *"an override cannot be honoured by half the code and ignored by the other half."* It was
>   booking hands for seats the player had switched off.
> - **And the village had stopped saying it reorganised itself at all.** `NarrateChanges` only
>   counted building-to-building moves; the churn is now job ↔ labourer pool, and a winter (D44)
>   erased what anybody had held. **Not one narrated reshuffle, and not one *"Moved to"*, in a
>   hundred and fifty years.** `Villager.LastWorkplaceId` is the fix — unhashed on the same footing
>   as `JobReason`, which it exists only to word.
>
> **⛔⛔ THREE THINGS ARE JOE'S CALL AND ARE DELIBERATELY NOT TAKEN:**
> 1. **THE OPENING GOT HARDER, MEASURABLY.** A hut seats two; the founding is two couples. With the
>    single-hut opening **household 1 worked 1.2% of its able-adult ticks over twenty years against
>    household 2's 5.2%** — his own *"one couple stays home"*. Two huts and both couples work.
>    ⚠️ Only `ColdStartTests.NeitherFoundingHouseholdRestsWhileTheOtherWorks` poses the second hut
>    (`PlayTheOpeningWithTwoGatheringHuts`); folding it into the shared opening **turned that guard
>    green and reddened four others** — measured. **Whether the shipped opening is now two huts is
>    a design decision.**
> 2. **`gathers_per_thinned_tile` still ships at 0** (D257). Flip it to 3 to feel the copse thin.
> 3. ⛔ **WHAT A BUILDER'S HUT SHOULD COST.** He capped it at **three** seats the morning after
>    D265 raised it (D267) — but **the hut is free and instant** (D108: the building every other
>    building waits on cannot charge timber without making a circle), so *"build another hut"* is a
>    click rather than a decision and three seats is a pacing rule rather than a constraint.
>    ⭐ *This is exactly the gap D260's competing rings had to close before the forager's two seats
>    meant anything.* Recorded, not fixed.
>
> **⚠️ WHAT THE RECONCILIATION TAUGHT, FOR WHOEVER DOES THE NEXT ONE:**
> - **Half the red was fixtures owning premises they used to get free.** A warm start never spent
>   `cart_food`, so *"a village founded with an empty larder has no spare hands by definition"* was
>   true without anybody arranging it, and the farm guards were **measuring the granary rather than
>   the farm**. Pose it (`BuildWithBareStores`, `FarmFixtures.WithNothingInTheStores`); **do not
>   restore the bug** — removing the fix costs 19 guards (46 red against 27).
> - **The count moves both ways.** Bounding the spare-hands top-up by seats was *more* correct and
>   took 27 → 28. Measure after every step.
> - **Three guards had lost their subject, not their tuning.** `StorageTests` compares a bounded
>   granary against an unbounded one and both arms settle at 7–21 — **the third time that guard has
>   been overtaken by a different bottleneck** (D134's timber shed was the second). The lockstep
>   guard's tile measure was **deleted on the written instruction it was carrying**. The rhythm's
>   cost was a **stock read at an instant** and is now trips over fifty years.
> - **A seed may be replaced when its village is dead, never when its village disagrees.** Seed 42
>   left `ApprenticeshipTests` at **0.00 masters in both arms**, where *strictly more* would have
>   passed just as silently the other way round; an anti-vacuity check now catches it.
> ⛔⛔⛔ **DO NOT SET `GathererHutCapacity = 8` IN `VillageFixtures.Village`.** It fixes most of the
> suite in one edit **and stops every one of those guards exercising the shipped seat count while
> reading as passing** — D157 green-and-blind, the fifth time this project has been offered that
> trade. *It is posed in exactly one place, `StorageTests`, which spends eight lines saying why.*
> ⚠️ **And a hard limit found on the way: `MaxHomeToWorkTiles` IS the ring**, so two huts with no
> overlap sit twice as far apart as anybody may walk to work. **Spreading huts is the player's job,
> done with painted neighbourhoods; an unattended fixture cannot do it, and placing more huts made
> thin valleys WORSE** — measured again here: `FoundingGatheringHuts` at three moves nothing and at
> **four or more kills the founding outright**.
>
> **⭐ The copse's thinning rate is still his to feel (D257), and the old note stands:** He has asked three
> times for a **2-seat forager hut**. ✅ **The other half of his design — rings that COMPETE — is
> built and shipped (D260): two huts on one copse are worth exactly one, and beyond twice the
> radius they cost each other nothing.** ⛔ **The cap itself is blocked, and the cause is located to
> the tick: two founders starve on Day 10 of Year 1**, because in the opening **foraging is the only
> food tap and it needs a seat** — a villager without one has nowhere to get a meal.
> ⭐ **The remedy is measured: 2 seats + a stocked granary at founding** turns seed 12345 from dying
> into growing normally and plateauing at 17–20 against 43, *which is the pressure he asked for*.
> ⛔ **But seed 42 still dies, and `MapGenerationTests.EverySeedProducesAValleyAVillageSurvivesIn`
> promises every seed is survivable WITH THE PLAYER DOING NOTHING.** His design withdraws that
> promise on purpose. **Ask him to confirm it, then the cap ships in one slice.**
> ⚠️ **And a real oddity found on the way: the warm start has NO FOOD.** `RaiseTheCart` runs only on
> a cold start, so `ShippedConfig.Established()` begins with `cart_food: 1200` unspent and zero food
> in any store. **Seven seats were hiding it.**
>
> **⭐ The copse's thinning rate is also his to feel (D257).** ⭐ **The thinning is
> BUILT and SHIPPED OFF**: `gathers_per_thinned_tile` in `data/sim.config.json`, at **0**. Every N
> gathers sets the nearest mature tree in the ring back to a sapling and regrowth grows it up
> again — **no new state, and the player watches the ring lighten.** Flip it to **3** to feel it
> (measured: 49.8% mature wood against 55.8% unworked, averaged over a regrowth period); **1**
> swings hard, **12** is barely visible. ⛔ **Turning it on moves five goldens and trips
> `MarketRestockTests`' overflow allowance — one deliberate commit once he picks a number.**
> ⚠️⚠️ **DO NOT TRY TO DERIVE THE RATE FROM AN UNATTENDED RUN.** Population there is capped by the
> GRANARY, not by food (`StorageTests.CapacityIsWhatHoldsThePopulationFlat`), so every rate looks
> inert on good seeds until it is harsh enough to kill a poor one. **Joe plays the other regime.**
>
> *The finding that got here (D256):* From his 111-year run:
> *"forager huts should be capped at 2 workers max so the player cant milk one forager hut for the
> whole game — the copse of wood isnt infinite."* ⭐ **He is right, and it is measured: one hut
> saturates at seven foragers and carries a 44-person village from year 70 to 110.**
> ⛔⛔ **BUT THE CAP WAS BUILT, MEASURED ACROSS FOUR VALUES AND THREE SEEDS, AND REVERTED — the
> band where it works does not exist.** At **5–6 the good valleys are exactly as good as at 7**
> (no pressure at all) **while the poor valley gets worse**; at **4** the pressure appears and
> **Phase 3's apprenticeship pillar inverts** (a village that teaches keeps *fewer* masters,
> because the forager's hut is where pairing happens); at **2–3, two valleys in three die out.**
> ⭐ **The lever is yield against regrowth, not seats** — the ring regrows faster than any number
> of foragers can strip it. **Sixth cause killed by measurement after the farm's five; do not add a
> seventh by reasoning.** *Ask him whether the answer is slower regrowth, thinner yield, or a ring
> that remembers what has been taken (the farm's memory, one building over).*
>
> **⭐ AND ONE FINDING WORTH MORE THAN THE FEATURE: the unattended shipped village NEVER LEARNS TO
> WRITE**, in fifty-eight years. A granary is player-placed, nobody places one in an unattended
> run, so `FirstGranaryTick` stays zero and literacy never starts counting. **Had literacy been
> made a prerequisite for the hall, that village could never have been given one** — Joe's
> *"expected, not enforced"* (D251), vindicated by a guard that went red for the right reason.
>
> ⚠️ **ONE THING HE IS STILL TESTING:** the trade pin (D247). *"Seems to work the way i expect. i
> think. ill keep testing."* **It displaces an incumbent** — pin somebody to a one-seat trade and
> whoever held it is moved off with a sentence naming the player as the reason. **If that reads as
> the village being arbitrary rather than as him deciding, the thing to change is who gives way.**
>
> ⏸️ **AND HUNGER IS DELIBERATELY PARKED.** He said it feels like hunger goes 0→100 too fast
> *and* that food is too abundant — then: *"leave hunger alone for now. ill revisit that later."*
> ⛔ **Do not tune it unasked.** When it comes back he wants **a measured proposal first**, and the
> shape is *slower hunger + more food per meal* ≈ the same food per year in fewer, larger meals.
> ⚠️ `food_per_meal` is the dial D223 spent a decision aligning between fixture and game, and
> hunger feeds the survival floor and the birth gate — **a derived economy, not a slider.**

---

## The stretch before this one — kept because its lessons are still live

> **⛔⛔ THE ECONOMY BUG THAT DEFINED THIS STRETCH (D236–D239), because its lessons outlive it.** Joe played to Year 44
> and reported *"I painted stone deposits in year 25 and they never harvested, which upheld the
> building of my 2nd granary."* **That was the smallest visible corner of a stalled economy.**
> His audit trail says: **clearing runs 40 / 16 / 4 times in Years 1–3 and then once, in Year 31**;
> **granary 2 was marked out in Winter Year 23 and was still an unbuilt site at Year 44**, twenty-one
> years on, while three houses went up around it; and the spare labour force made about **fifteen
> thousand** round trips fetching loads it never picked up — **1,439 to one tile in the last 900
> ticks, by fourteen villagers**, one with no goods line anywhere in the log.
>
> **⭐⭐ THE CAUSE WAS TWO FINDER FUNCTIONS THAT DISAGREED ABOUT WHERE A GOOD MAY GO.**
> `NearestStore` matched on kind and fullness and **never asked `Accepts`**; every other finder
> asks. Fixed, along with the three things that made it invisible: a **stalled site now says what
> it waits for**, a **met limit stops the job and keeps the seat**, and the **limits panel measures
> what the sim decides on**. The forager's hut is also **called what its workers are called** at
> last (D240), and a discovery is now a **celebratory banner that does not pause the game** (D241).
> The **bottom bar wraps** instead of running off both edges once the library button appears
> (D242), and a **villager's panel lists the techniques they carry** (D243).
> **855 passing, 0 failing, 2 skipped of 857.**
>
> **⭐⭐⭐ THE LESSON WORTH MORE THAN THE FIX: IT WAS FOUND IN THE LOG, NOT IN THE CODE.** Nobody
> reading `StoreForTheLoad` had spotted it in months. Twenty minutes of
> `grep`/`awk` over `src/Bclone.Game/logs/` — counting state transitions per year, per villager —
> found it, sized it, and proved it. ⭐ **Bucket the behaviour histogram before forming a
> hypothesis**; "clearing per year" collapsing to zero is a sentence no test in this suite can say.

> **✅✅ MERGED TO `main` AND PUSHED (2026-08-28, Joe: *"then merge to main"*).** A clean
> fast-forward — `main` had no commits of its own — so **`main`, `origin/main` and
> `phase/4-the-tech-tree` are all the same commit.** D217's trap is finally not live: the build
> Joe plays and the remote are one thing again.
>
> ⚠️⚠️ **IT WENT TO `main` WITH PHASE 4'S DEFINITION OF DONE UNMET, AND THAT IS RECORDED RATHER
> THAN TICKED.** **Slice 3 (the knowledge screen) is unbuilt and the §5 QA walk has never been
> performed** — item 4 of the phase's own DoD, the one it says it is *not allowed to waive*.
> **This is Joe's call, asked and answered three times**, and it is exactly what D203 did for
> Phase 3. ⛔ **Do not treat the merge as evidence the phase is finished; it is evidence he
> wanted the work on `main`.** *If a Phase 4 regression ships, this is where it got through.*

> **⭐⭐ WHAT PHASE 4 HAS: techniques (D225), the library (D226), the pacing fixes his play forced
> (D227, D232, D233), and the whole build/undo loop rebuilt around them (D228–D231, D234, D235).**
> **⛔ WHAT IT DOES NOT HAVE: slice 3, the knowledge screen** — `phase-4-the-tech-tree.md §3`. And
> its **QA checklist (§5) has never been walked**, which is the debt Phase 3 left and this phase
> wrote a checklist specifically to avoid repeating.

> **⭐ READ `specs/phase-4-the-tech-tree.md` FIRST** — it is the phase plan, the four design calls
> and the QA checklist in one document, and it is current.

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D224 back to D142**, then
`METHODOLOGY.md`. **Then `specs/content-inventory.md`** — it is the audit of what actually exists
against what the documents claim, and it is the shortest route to being oriented.

> **⚠️ THE MOST EXPENSIVE THING THAT HAPPENED IN THIS STRETCH WAS NOT A BUG — IT WAS AN UNMERGED
> BRANCH.** Eleven commits of finished, green work sat in a worktree while `main` had none of it.
> **Joe played `main`, saw *"villagers harvest stone but the pile shows 0"*, and reported a bug
> that was real on his build and already fixed on the branch he was not running** (D217). Then the
> merge itself was left half-finished — two unresolved conflicts, nothing committed — and a fresh
> session had to find that before it could review anything.
> **Merge it, or say plainly that it is not merged. A branch nobody merges is a branch that lies
> to whoever plays the game.**

> **⛔ WHEN YOU HAND OFF: EDIT THIS FILE, DO NOT REPLACE IT.** The trap list at the bottom is
> accumulated from sessions that each paid for one entry. Rewriting it wholesale drops them
> silently — that happened on 2026-08-22 and cost an hour and three quarters within the same
> session. **Rewrite "where things are"; carry the traps forward.**

⭐ **Four things to know before you touch anything:**
1. **▶️ PHASE 4 IS OPEN — D205's hold was lifted by Joe on 2026-08-26 with one word: *"start"*.**
   ⛔ **Do not re-litigate whether to build it.** `specs/phase-4-the-tech-tree.md §1` already
   answers `content-inventory.md` finding 5's objection, and §2 records four design calls he has
   since confirmed or overruled in play.
2. **⭐⭐ HIS PLAY IS THE BEST BUG-FINDER THIS PROJECT HAS, AND THIS STRETCH PROVED IT SEVEN TIMES.**
   D227, D232, D233, D234 and D235 all came from him playing for ten minutes. ⛔ **Three of them
   were features that existed only in the sim** — the library was invisible, then Move and Empty
   had no buttons. **A sim feature is not done until something in the view calls it, and no test
   in this suite can tell you that.** *Build the button in the same commit as the feature.*
3. **✅ THE BUILD/UNDO LOOP IS PEOPLE-SHAPED NOW** (D228–D231). Demolition is **reverse
   construction** — a builder's job costing half the building's own work. Housing is the **brush's
   business in both directions**: unpaint to mark, repaint to call it off. **Any building can be
   moved**, and **a store can be emptied on request.** ⛔ *Nothing in the village happens by a click
   any more* — demolition was the last exception, unnoticed because it was the player's own hand.
4. **⚠️ THE FARM IS UNPARKED (D194)** and **Joe's thirteen tiles were never available** — thirteen
   tiles ten ticks from a store needs ~230 ticks of a 120-tick autumn. Read the farm section below
   before re-opening it, and **do not propose `farm_store_cap` — it is dead twice over.**

---

## ⭐⭐ What landed while Phase 4 stayed held (D206–D218, 2026-08-24/25)

**The hold was never idleness.** Joe's content pass arrived and the infrastructure it needs got
built underneath it — none of which is Phase 4, and all of which Phase 4 will stand on.

- **⭐ Joe wrote the technique list** (D206, `TECH-EXAMPLE.md`): 45 buildings over four tiers,
  **39 named techniques**, 25 animal species. **They are diegetic, not a research menu** — asked
  directly, because the document reads like one. Mapped into `tech-tree.md §9` with two new trunks.
- **⭐ Morale is real** (D207) — per villager, doing **exactly two things: people leave, and
  households have fewer children.** *Work slows* and *sickness* were both offered and **declined**,
  which is what keeps it clear of §1.1's invisible multiplier and §0.1's death spiral.
  `specs/morale.md`.
- **⛔ Spoilage was re-proposed and refused again** (D208). Winter feed is a seasonal fact, not a
  rot timer. **D37 stands.**
- **✅ The school** (D209) — a teacher, slots for children **12–16**, graduates who work better,
  **paid for with four working years per pupil** because `adult_age` is 12. It cashes a dial
  **D156 reserved in August in the same words.**
- **✅ A good is a ROW, and the set is open to 62** (D210, `specs/goods-catalog.md`). Every switch
  on a good is gone from the sim. **Four ceilings nobody had counted** were found and lifted —
  six goods, thirty goods, stock limits, and the villager's arms.
- **✅⭐⭐ STONE IS REAL END TO END** (D211–D217). A villager **could not carry stone** — the arms
  were the one stockpile D82 never reached, so a cleared seam's yield **stopped existing**.
  Red-checked before the fix: *eight seams cleared in two years, zero stone anywhere.* Then
  **multi-material building costs** (a granary is 40 logs and 10 stone), the stone taken off the
  map rather than out of the cart, and the food limit finally reaching the forager.

⚠️ **Five goldens moved, once, deliberately** — the arms are hashed by index now. **Measured, not
assumed:** restoring the old three-line mix makes every one byte-identical, which is what says the
hash's *shape* moved and the village did not.

- **✅ A TRADE IS A ROW TOO** (D218, `specs/jobs-catalog.md`). `content-inventory.md` finding 8's
  second half. ⛔ **Its red check is the most useful thing either catalogue produced:** *eight of
  nine new guards passed a break they should have caught*, because the test's own JSON listed rows
  in id order and so could not tell **id** from **position**. D157's green-and-blind, third
  instance. The cure is a fixture where the two differ — a file listing the trades backwards.
- **✅ THE GRANARY IS A BOX** (D219). `granary_feeds_people: 30` → **`granary_capacity: 2500`**,
  Joe: *"it's fine if the granary feeds a different number of people. The user should build more
  granaries — and will need to!"* **Deriving the box meant a village that ate more got a bigger
  granary for free**, which is the opposite of a pressure. ⚠️ **The one place D16 does not apply**,
  stated in `VillageEconomy` so nobody restores the derivation.
- **✅ A PLANTED TREE TAKES AS LONG AS A SEEDED ONE** (D220), and **Joe found it by playing**:
  *"trees planted by the forester [are] ready to fell very quickly."* Planted ground matured
  **three times faster** than seeded — near-instant at worst. ⭐ **The cause was a comment that was
  TRUE, for one path**: *"a sapling seen by a sweep has stood for one period"* holds for saplings
  the sweep seeded itself and **was never checked against the forester's.**
- **✅ Player-facing fixes** (D221 and the batch before it): the game **starts paused**; the brush
  is **square**; the stock-limit panel **means the numbers it shows** (food 2000, firewood 400 —
  ⚠️ *above* the village's own 360 target, because a default below it would freeze the village by a
  control nobody touched); the stockpile **refuses food** so it stays on the cart until a granary
  stands; demolition **warns before destroying** what is inside; the comfortable-walk ring is
  **gone**; and **saplings now have a colour and a sentence** — they had *neither*, drawn as mature
  woodland and described as *"open ground"*.

---

## Where things are

**Phase 3 is merged to `main`.** Its Definition of Done is met **with one item waived and written
down rather than ticked** (D203): METHODOLOGY §3's manual QA walk. Joe played the build
repeatedly through the phase and signed off the paint overlay, the market, the staffing cadence
and the whole — **but the phase was never walked end to end against a list, and Phase 3 has no
checklist at all.** *That is an unpaid debt Phase 4 should not inherit.*

**⭐⭐ WHAT PHASE 3 LANDED, in the order it landed:**

1. ✅ **The proficiency substrate** (D181, D183). `Villager.Skills` accrues time on the task,
   hashed sparsely in id order; six skills are **rows in config, not enum values**; the panel
   says *"Sixteen years as a farmer"*; **the mastery line fires** in the village log.
   **Nothing ever takes proficiency away**, and a tick out on the job is worth 1.5 of a tick
   waiting for one. Ages at mastery: **34–55, median 39.**
2. ✅ **Mastery bites** (D187) — **a master takes half the ticks over an action, rounded up.**
   ⚠️ **Below 34% the feature is literally a no-op**: durations are 3 and 4 ticks, so a bonus
   that does not round to a whole tick buys **nothing** — a village at 25% produced population
   and food *identical* to one with the feature off. `AMasterIsFasterAtEveryTrade` fails the
   build if it ever rounds away again.
3. ✅ **The mixed founding and the seeded rhythm** (D190) — a master, a journeyman and two
   novices with **seeded trades**, and a rhythm drawn at birth. **D28 discharged.**

4. ✅ **The at-risk line** (D195) — §11's last outstanding Definition-of-Done item. *"Wendell is
   48 and the only soul in the village who has mastered foraging. Put somebody beside them to
   learn it, or it goes with them."* One method (`SimWorld.KnowledgeAtRiskNote`), read by the
   village log **once on the edge** and by the villager's panel **while it is true**. Both halves
   of the condition are derived — `LifeStage.Elder`, and *the only living master*.

5. ✅ **APPRENTICESHIP** (D202) — §2.1's actual claim. **A youth beside a master of the same
   trade at the same workplace learns twice as fast.** Nobody is assigned to anybody; the master
   pays nothing; there is no dial. §10's anti-vacuity guard is green — **masters alive after a
   century go 3 → 6, 4 → 8, 8 → 10** against a village that never teaches.
   - ⛔ **IT REACHES ONLY 2–3 TRADES OF 5, AND THAT IS RECORDED RATHER THAN PAPERED OVER.**
     Forager and marketer always pair, forester sometimes; **woodcutting and building never do**,
     because they are one-seat trades with nobody to learn from. **The trades most likely to die
     with their last holder are exactly the ones apprenticeship cannot reach** — which is what
     **D196's library** is for, and why that answer is worth more than it looked.
   - ⚠️ **200% was too far**: seed 42 ends the century with **zero food**. A hundred leaves it at
     1,485 against 1,513 with the feature off. *The width is measured, not picked.*

⚠️ **The phase PR is #4, not #3.** Number 3 went to the closed screenshot-hook PR D160 rescued,
and every document in the repo said #3 for a day before anyone checked.

**Merged slice branches are deleted on Joe's standing preference**, each after checking it had
**0 commits not on `main`**. Tips if ever wanted back: `phase/3-skill-and-apprenticeship`
`028f4fc`, `phase/2-wood-fuel-and-tools` `9b9f410`, `slice/per-site-yield` `b2cb718`,
`slice/faster-cost-field` `daec8fd`, `slice/the-market-that-never-gets-staffed` `48ab7e5`.

⛔ **ONE BRANCH IS GENUINELY UNMERGED: `slice/work-from-the-steading` (`e12b20f`, 1 commit).**
Farmhands staying at the farm through the working seasons — **an economic no-op that costs ~13%
of the harvest**, kept for the look, and its cost is still unexplained. ⚠️ **It predates D194's
rewrite of the sowing cap, so it will not merge cleanly and its measurements are stale.**

✅ **Phase 3 is merged AND PUSHED** — `main` and `origin/main` are level.
⚠️ **It went straight to `main` rather than through a PR**, on Joe's call (*"push"*), where Phase
2 went up as [PR #4](https://github.com/joemachen/bclone/pull/4). **There is no PR #5** — do not
go looking for one.

⚠️ **Do not write a commit hash into this file for anything that keeps moving.** The line above
named one and was stale within the minute.


**SUITE, FROM A RUN (2026-08-29, after the town hall's slice 1):**
```
880 passed, 0 failed, 1 skipped of 881 - 2m45s (was 18m52s before D179)
```

**The one remaining skip is a ruling, not unfinished work: D143** — an unattended village is
*supposed* to die out, and that guard measures an empty valley rather than three-century
stability. ⚠️ **Its stated fix — give it `PlayTheOpening` and assert the peak and the causes of
death — is unblocked and has design content in it** (what peak?), so it is a small slice rather
than a housekeeping edit.
> ⭐ **There were two until 2026-08-28.** The other was skipped because *"the timber shed is the
> binding cap at 343/343"* and *"restore when D134's open question is answered"* — **both halves
> had expired** (`ShedCapacity` is floored on the granary's; D143 answered D134) and it passes.
> **A skip is a claim about the world and nothing re-reads it.**

```bash
dotnet test bclone.sln --nologo -v q
```

**It is fast enough to run in the foreground now** (D179 took it from nineteen minutes to two and a half). **Do not start a second run while one is going** —
and **check `Get-Process testhost` first**: one session found the *previous* session's run still
alive on thirteen cores, holding the lock on `Bclone.Sim.dll`, with nobody left to read its
output. Note the CPU figure is summed across cores, so a healthy run shows far more CPU-seconds
than wall-clock.

**The Godot view builds separately** — `dotnet build src/Bclone.Game/Bclone.Game.csproj` (D11) —
and has **no automated verification of any kind** (D160). Looking at it is the test.

---

## ⭐ What to do next — `DESIGN.md §4`'s queue, in its order

> **⭐ THE LIVE CALLS AS OF 2026-08-29 ARE IN THE BANNER AT THE TOP OF THIS FILE.** What follows
> is the record of how the queue emptied — kept for the reasoning, not as a to-do list.
>
> 0. **⭐⭐ JOE SHOULD REPLAY SEED 12345 BEFORE ANYTHING ELSE.** D236–D239 changed how the village
>    works, and **the acceptance criteria are in his log, not in the suite**: clearing must not
>    collapse to zero after Year 3, and fetch trips without pickups must not run to thousands.
>    Re-run the two commands in the audit-trail trap below against a fresh log and compare.
> 0b. **✅ SIX OF HIS SEVEN ARE DONE; ONE IS BLOCKED ON HIM** (2026-08-27/28, he
>    chose "fix the economy first, alone"):
>    - ✅ **The *"gatherer's hut"* naming is DONE** (D240) — it is *"forager's hut"*, and the two
>      hand-written view sentences now read from the catalogues rather than holding the word.
>    - ⏸️ **A UI PASS IS OWED AND DEFERRED ON HIS CALL** (2026-08-28: *"the bar looks better for
>      now. still lots of overlap between menus, but we'll fix that in a later UI pass"*). The
>      bottom bar is fixed and measured; **the panels still overlap** — the left column draws over
>      the speed buttons. ⛔ **Do not start it piecemeal, he has said when.** Two things to fold
>      in when it happens: the panels have **no z-order or reserved regions**, so every new panel
>      is a new overlap; and *"Builder — nobody working of 21 seats"* in a village of four adults
>      is **`BuilderHutCapacity` derived from the economy horizon (D16) and not a bug**, but it
>      reads as one.
>    - ✅ **The technique-discovery modal is DONE** (D241) — a non-pausing celebratory banner,
>      coloured in the log, saying whether the village can keep the technique. The
>      no-library-at-all case, which had no sentence anywhere, now has two (one for a village
>      that cannot write yet).
>    - ✅ **The village log's colours and category filters are DONE** (D244) — seven categories
>      decided at the source, one switch each, and the switch doubles as the legend. **Seasons
>      alone are 42% of a sixty-year log**, so one click does most of the noise reduction.
>      ⚠️ *Warning fires 214 times in sixty years — a warning that frequent is furniture, and is
>      worth a look in the UI pass.*
>    - ✅ **PINNING A VILLAGER TO A TRADE IS DONE** (D247) — **and the guard never forbade it.**
>      `NoPublicApiLetsACallerAssignAVillagerToAWorkplace` blocks naming a person into a
>      *building*; `SetPinnedTrade(Villager, JobKind?)` names a *trade* and passes untouched.
>      ⭐ **Joe offered to overrule it and it turned out not to need overruling** — worth
>      remembering the next time a guard looks like it is in the way. ⚠️ It needed **five**
>      mechanisms and read *0 of 4,311 ticks* through four of them; the last is that **a pin
>      outranks cost in the candidate sort.**
>    - ✅ **ALL SEVEN OF JOE'S ITEMS ARE NOW DONE**, and the QA walk with them (D248).
>
> 1. **✅ BOTH ARE ANSWERED: PUSHED (2026-08-27) AND MERGED TO `main` (2026-08-28).** ⛔ **Do not
>    re-ask either.** ⚠️ **The phase's DoD is still unmet** — slice 3 unbuilt, the QA walk never
>    walked — and that is written into the banner at the top rather than quietly ticked.
> 2. **⛔ SLICE 3 — THE KNOWLEDGE SCREEN — IS THE REST OF PHASE 4.**
>    `phase-4-the-tech-tree.md §3`. It mostly *surfaces* what exists: which techniques the village
>    has, who holds each one, and how close the last knower is to dying.
>    `SimWorld.KnowledgeAtRiskNote` (D195) already answers the third and is on the villager panel.
>    - **⛔⛔ IT IS BLOCKED ON A DESIGN CALL NOBODY HAS MADE, FOUND 2026-08-27 AND WRITTEN INTO THE
>      SPEC AT SLICE 3.** `tech-tree.md §8` says the knowledge screen **is the town hall's interior
>      and is reachable only once one stands** — and the same phase spec's ⏸️ list puts **the town
>      hall explicitly out of Phase 4**. **No `TownHall` exists in `src/` or `data/`** (checked).
>      *The slice asks for a screen whose front door is out of scope.* Three ways out are written
>      out in the spec — ship it ungated, pull a minimal town hall in, or pause the phase here —
>      and **it is a legibility call, so it is Joe's. Do not pick one silently.**
>    - ⭐ **This does not block the QA walk: 21 of the 22 checks cover slices 1 and 2.** Only check
>      21 needs the screen.
> 3. **⛔⛔ THE QA CHECKLIST HAS NEVER BEEN WALKED** — `phase-4-the-tech-tree.md §5`, 22 checks.
>    **Phase 3's walk was waived and this phase wrote its checklist on day one specifically so that
>    debt would not compound.** *Walking it is a Definition-of-Done item, not a formality, and it
>    is the one item this phase is not allowed to waive.*
> 4. ⚠️ **THEN FISHING AND HUNTING** — `buildings-plan.md §10` step 1, food breadth, and **Joe
>    chose it as what comes after Phase 4 pauses** (2026-08-26). Phase 4 does not have to be
>    *finished* first; he agreed to pause it at a clean point.
>
> ⭐ **Recently settled and NOT open, so they are not re-argued:** the build menu becoming
> catalogue-driven is **deferred on his call** (D223); the forester's regrowth pace is **settled by
> play** (D224); the ageing/technique interaction is **fine by him** (D225); `demolition_work_percent`
> at **50% is confirmed** (2026-08-26: *"half the build time is fine"*).
1. ✅ **Phases 0–3 are all merged to `main`.** Phase 2 went up as
   [PR #4](https://github.com/joemachen/bclone/pull/4); **Phase 3 went straight to `main`
   (D203), so there is no PR #5.** Branches are deleted after checking each had 0 commits not on
   `main` — tips recorded above.
2. ✅ **`specs/skills-catalog.md` IS BUILT, and its status line says so** (D181–D202). Read it
   before touching skills — but **read it as a record, not a plan.** Its §12 still holds the
   tuning questions nobody has answered.
3. ✅ **`specs/per-site-yield.md` §4.2a and §4.3** (D194) — the farm remembers what it brought in.
   **Read the farm section below before reopening any of it.**
4. ✅ **`specs/storage-and-distribution.md` §14.8–§14.9** (D197, D199, D201) — the marketer stocks
   the market, storage buildings are separate from it, and its service area is a **count, not a
   ring**.
5. ⏸️ **PHASE 4 — THE TECH TREE (§2.7) AND THE TOWN HALL (D176). HELD, NOT STARTED (D205).**
   - **⛔⛔ THE BLOCKING ITEM IS JOE'S CONTENT PASS, AND IT IS NOT YOURS TO CLEAR.** Do not
     "helpfully" start on the substrate while he thinks — that is precisely the getting-ahead he
     stopped. **Ask him where the content pass got to before proposing anything.**
   - **⭐ WHAT THE AUDIT FOUND, because it changes what Phase 4 even is** (`specs/content-inventory.md`):
     - ⛔ **`buildings-plan.md` is missing four of the ten buildings that exist** — BuilderHut,
       ForesterHut, Farmhouse, Pile — and the work-ground zone. Its ✅ marks claim six built.
       **A catalogue missing 40% of what is built will generate content that duplicates it.**
     - ⛔ **`BuildingRecipe` is `(int Logs, int WorkTicks)` — one material slot for the whole
       catalogue**, against a tier system where the mason's yard *"gates every durable building"*.
       **Stone and iron are already quarried, mined, stored and hashed; nothing spends them.**
       That is structural, not content, and it touches every recipe, the hauling, the build queue
       and the goldens at once.
     - ⛔ **18 catalogue rows carry a knowledge flag and none of those 18 buildings exist**, so a
       tech tree built today would have almost nothing to gate.
   - **✅ D204 SETTLED ONE THING WHILE THE PLAN WAS BEING DRAWN: recording is AUTOMATIC AT
     MASTERY** (Joe), not the seasons-long scriptorium project `tech-tree.md §7b` describes.
     ⚠️ **The consequence to carry: §11's guard against *"the library is mandatory"* rested on
     three costs and this deletes one**, so **the hard shelf cap is carrying it nearly alone** —
     which makes *a full library refuses the record and says so* load-bearing rather than polish.
     **The scriptorium and literacy are deferred, not deleted.**
   - **⭐⭐ JOE'S LIBRARY MODEL IS ALREADY RECORDED AND IT IS CONCRETE (D196).** A master
     woodcutter works out *"splitting lumber in a way that gives more cords — +15% firewood per
     log, +5% mastery"*; **the technique enters the library's records when he reaches mastery**;
     when he dies **his proficiency dies with him** but the technique does not, and **the next
     woodcutter spends idle time in the library learning it.** Where a trade has more than one
     worker the master also passes it to his apprentice directly.
   - **⭐ IT LANDS EXACTLY ON D176's SPLIT WITHOUT HAVING BEEN ASKED TO**, which is the strongest
     sign that split was right: **technique** is the village's and writable, **proficiency** is
     one person's and never writable. **The anti-ratchet holds** — `tech-tree.md §3a`'s *"a record
     preserves the method, not the proficiency"* is what stops §2.3's dead late game.
   - ⚠️ **The one part to measure before it ships:** a technique granting *"+5% mastery gain"* is
     **a soft ratchet on proficiency itself**, one level up. Bounded and probably fine, but it is
     the only piece of the model that touches the rule rather than sitting beside it.
   - **⭐ AND IT IS THE ANSWER TO APPRENTICESHIP'S HOLE**: one-seat trades have nobody to learn
     from, so the library is what carries their knowledge **across a gap in people** where
     apprenticeship carries it **between** people.
   - ⛔ **The list of techniques is deliberately NOT invented yet** (Joe: *"we don't have to come
     up with the full list… eventually they will all have a number of them"*) — `tech-tree.md
     §12`'s refusal of false precision.
   - ⚠️ **WRITE PHASE 4 A QA CHECKLIST.** Phase 3 shipped without one and its walk was waived
     (D203); **that debt should not compound.**
6. **Also on the board, unscheduled**, all recorded with Joe's rulings: **nomads and the
   dead-village revival** (§5, and it needs **building decay**, which reopens D65's *"repair after
   damage, no decay on a timer"*); **house upgrades and the 60–80 firewood target** (§5 — ⚠️ a
   6–8× change to a derived burn, **not a dial**); **foods with different nutritional values**;
   and the **steading slice**, still unmerged on `slice/work-from-the-steading`.

---

## ✅ THE FARM IS UNPARKED (D194) — and here is what is settled, so nobody re-opens it

**The ledger the section below asked for was built, and it answered in one sitting.** Kept as
`FarmLedgerTests` so the numbers can be **re-taken rather than trusted**.

**⛔⛔ THE CAP WAS SELF-FULFILLING, AND `ReapableShareAt` IS DIMENSIONALLY WRONG.** It scaled a
farm's field by `budgeted ÷ haul` — `budgeted` is a **round trip inside the field** (4 ticks),
`haul` is a **one-way walk to a store** (10). *The ratio is not a share of anything.* Measured,
one hand, ten years, committed ground posed at each level:

| farm → store | the cap sowed | what it can actually bring in | autumn spent **idle** at the cap |
|---|---|---|---|
| 10 ticks | 5 | **6** | **27%** |
| 16 ticks | 3 | **5** | **45%** |
| 22 ticks | 2 | **4** | **55%** |

**The cap cut the field, the farmer then had nothing to do, and the idleness read back as proof
the field had been too big.** After: **72 tiles reaped against 51 at ten ticks, idleness 6%.**

**⛔⛔ AND THE THING TO CARRY FORWARD: THIRTEEN TILES TEN TICKS OUT IS PHYSICALLY IMPOSSIBLE.**
Autumn is **120 ticks**; thirteen tiles at that distance needs about **230**. Joe's farmer was
short of **one or two** tiles, not eight. **The lever for thirteen is the walk** — the same
farmer beside a granary commits the whole field. §4.3's placement warning and the farm's own
panel now both say so.

**The fix is memory, not a better formula, and "no formula fits" is a finding.** The true ceiling
depends on the market's drain rate, the painted ground's shape, the granary's fullness and the
hands that turned up; `season ÷ (reap + walk)` wants a different constant at every distance,
moving the *wrong way* with distance. **A farm sows what it has already brought in** — a
high-water mark, per hand, clamped to `FieldTilesOneFarmerKeeps`, re-reckoned when the walk
changes. It converges on **6, 5 and 4** without being told them.

**⛔ CAUSES NOW DEAD — five proposed, all rejected by measurement. Do not add a sixth by
reasoning.**

| proposed cause | what killed it |
|---|---|
| the granary haul | removing it entirely still left the farm at ~7 tiles |
| the daily commute | travel is **11%** of a farmhand's ticks |
| resting outdoors getting cold | farmhands' cold is **zero, always** |
| the buffer (`farm_store_cap`) | raising it gave 13 tiles and **52% brought in** — the rot came back |
| **the buffer, again (D194)** | an **8.7× buffer** moved the ceiling from **6 tiles to 6** at ten ticks and **5 to 5** at sixteen. It still took only **23 of 72 loads** — it fills once and the market cannot keep it drained. **Two independent measurements now.** |

⚠️ **`crop_yield_per_tile` is NOT the lever** and Joe proposed it: raising it would inflate a
derived number to paper over a bug and leave well-sited farms at ~2.5× gathering.

**Still open:** the steading slice (farmhands staying at the farm through the working seasons) is
committed but **unmerged** on `slice/work-from-the-steading` — an economic no-op that costs ~13%
of the harvest, kept for the look, and its cost is still unexplained.

**Two directions Joe set, neither scheduled, both in `DESIGN.md §4`:** **gridless** — the largest
architectural statement anybody has made about this project, and the first question when it is
taken is whether the *sim* goes continuous or only the *presentation* — and **mods that can add
anything** (`BuildingKind`, `JobKind`, `Goods` and `Terrain` are four C# enums hashed by
position; `crops-and-orchards.md §4` is the template for doing it right). Standing discipline for
the second: **when you add a new kind of thing, ask whether it wants to be an enum value or a
data row.**

---

## ⛔⭐⭐ THE TRAP THAT WILL NOT ANNOUNCE ITSELF — read this before building roads

**The travel-cost field is a breadth-first sweep since D179**, and that is correct **only while
every passable tile costs the same to cross.** It replaced an O(n²) Dijkstra that was costing
**four seconds per world** and very nearly the entire test suite.

**§2.6 desire-path roads say crossing thresholds *"lowers pathfinding cost, creating a
reinforcement loop."*** The day a worn path is cheaper than grass, **BFS silently returns wrong
answers** — it keeps the first route it finds rather than the cheapest, and nothing throws.

- **Then, and only then, go back to a priority queue** — `PriorityQueue<int, long>` keyed on
  `((long)cost << 20) | index`, which keeps the tie-break and stays O(E log V).
  **Never back to the scan.**
- ⚠️ **No guard in the suite would catch it.** Every one describes a valley where the uniform-cost
  rule still holds. The symptom would be villagers taking scenic routes for a phase.

Written in three places on purpose: here, `TerrainCostField` itself, and
`pathfinding-and-water.md`'s header.

---

## Tools this project has that you would not guess

- **⭐⭐ `Skip 1y` / `Skip 10y` IN THE CONTROL BAR — debug builds only (D254).** Ten years in about
  **a quarter of a second**, against 10.7 real minutes at 1x. **Use it to reach a late-game QA
  item instead of leaving the game running.** It steps the same `SimLoop.Step` everything else
  does, so a skipped village is byte-identical to a played one; it **stops early on a moment**, and
  leaves the game paused. ⛔ *Debug-gated deliberately — `DESIGN.md §1`'s meditative pace. It is a
  tool, not a speed.*
  - ⚠️ **And know the arithmetic before adding another speed button:** `target_ticks_per_second` is
    **0.75** and a year is **480 ticks**, so 1x is **10.7 minutes a year** and 10x is **64 seconds
    a year**. The sim itself does **58 years in 1.53 seconds** and the spiral guard does not bite
    until **~20,000x**. *The speed buttons are a pacing choice; nothing about them is a limit.*
- **`BCLONE_PROBE_WIDTHS`** (METHODOLOGY §6). Walks the control tree headless in two seconds and
  prints what every panel and inspector row claims as a **minimum width**, including the rows
  posed with their worst-case sentence. **A column is never narrower than its widest child**, so
  every width in `Main.BuildUi` is a *request* — three sessions have asked this question and two
  hand-rolled the same throwaway before it was kept.
  - ⭐ **It now poses the control bar with every category the village will ever unlock** (D242's
    blind spot, which had been left open inside the tool built to close it), and prints **the sum
    of each row's items** beside `wants` — because *an `HFlowContainer`'s minimum is its widest
    single child* and `wants` cannot see a new category at all.
  - ⭐⭐ **AND IT PRINTS WHAT THE VILLAGE LOG WILL ACTUALLY RENDER** (`[log]` lines, D255), through
    the real `LogMarkup` with the BBCode stripped back off. **It caught a sentence left hanging on
    an em dash on its first run**, which nothing in the code would have shown. *Print what the
    control will show before believing a string transform.*
    ⚠️ It **steps the sim twelve years**, which every other probe deliberately does not — safe only
    because it runs one line before `GetTree().Quit()`. **Anything added after it has to move.**
- **`grep "food from the field"`** — `HaulTheHarvest` writes its reason — free space, both costs, which store won — so *"why did
  the farmer walk past the buffer?"* is one grep rather than an afternoon:
  `grep "food from the field" src/Bclone.Game/logs/<newest>.log`
- **The audit trail** at `src/Bclone.Game/logs/`. Almost every bug that mattered came out of it.

---

## Traps, in the order they will cost you

- **⛔⛔ A GUARD WITH A BUILT-IN'S ID TYPED INTO IT BREAKS THE DAY A BUILT-IN IS ADDED, AND THE
  FAILURE MESSAGE BLAMES THE WRONG THING (2026-08-29).** Adding `BuildingKind.TownHall = 11` turned
  **eight guards red across two files** and not one of them was testing anything that had changed.
  Three said `Id = 11` and failed as *"buildings[12] repeats id 11"* — **a true sentence about the
  fixture that says nothing about the code.** Five more were modded-catalogue fixtures that now
  omitted a built-in id.
  - **⭐ The cure is one line: derive it.** `NextFreeId(config)` reads the highest id in the
    catalogue and adds one, so the *next* built-in costs nobody an afternoon. **Read the numbers
    out of the fixture rather than writing them into it** — the same rule D231/D233 wrote for
    positions and quantities, applied to ids.
  - ⭐ **And the repair is worth more than the break was:** the reordering fixture now carries the
    new row **sixth of twelve in a descending list**, so position genuinely cannot pass for id
    (D218's finding, given a sharper fixture).
- **⛔⛔ A GOLDEN THAT *MUST* MOVE IS NOT A BUG — BUT PROVE IT IS THE FINGERPRINT AND NOT THE
  VILLAGE, AND DO IT THE D211 WAY (2026-08-29).** Slice 1's own Definition of Done said **"no
  golden moves"** and two moved. ⛔ **The DoD was wrong**, and it was corrected in place rather
  than quietly met: *a village whose founders have all died IS a different village* — it is owed a
  hall, and `Mark` reads that.
  - **⭐ THE MEASUREMENT THAT SETTLES IT TAKES TWO MINUTES: delete the new mixes from `StateHash`
    and re-run.** Both moved values came back **byte-identical to their old numbers**, which is
    what says the hash's *shape* moved and the village did not. *Assumption would have been
    indistinguishable from a real regression.*
  - ⭐ **And which goldens HELD is the result:** the shipped 50-year pair, the map golden, both
    farm goldens and all three per-site arms. **The two that moved were both *fixture* arms**,
    because the fixture village loses its founders by year 30 and the shipped one has not by
    year 50. *Ask why the ones that held, held.*
- **⚠️ `if (false)` IS NOT A DELIBERATE BREAK — IT DOES NOT COMPILE HERE (2026-08-29).** The trap
  further down says *"write breaks that compile — flip a bool, set a bound to zero"*, and the
  obvious way to disable a block trips `CS0162 unreachable code` against D246's
  `TreatWarningsAsErrors`. Same for dropping a clause from a condition: the now-unused local trips
  `CS0219`. **Delete the block outright, or add `_ = theLocal;`** — and back the file up first.
- **⛔⛔ THE WIDTH PROBE WAS STILL ONLY EVER MEASURING A YOUNG VILLAGE — INSIDE THE TOOL BUILT TO
  STOP THAT (2026-08-29).** D242's whole lesson is *"every look anybody takes at the UI is a look
  at a young village"*, and the bar probe measured the bar **as it starts**, with the conditional
  Knowledge and Civic groups hidden. It poses them visible now.
  - **⭐⭐ AND `wants` COULD NEVER HAVE SEEN THE PROBLEM ANYWAY: an `HFlowContainer`'s minimum
    width is its WIDEST SINGLE CHILD.** Adding a whole category moved `wants` by **exactly zero**.
    The number that decides wrapping is the **sum of the row's items**, which the probe now prints
    beside it. *That is D242's collapse-into-a-corner property read from the other side.*
  - **Measured: the Civic group costs 83px on a row that already wrapped (10 items/1261px →
    11 items/1344px against 1240 available), and the bar's height is unchanged at 189.**
    ⚠️ **That row is the one to watch** — it is the crowded one, and it is where the next category
    will land.
- **⛔⛔⛔ A GREEN RED-CHECK IS A CLAIM ABOUT YOUR FIXTURE BEFORE IT IS A CLAIM ABOUT THE CODE
  (2026-08-27).** The guard for D236 **passed against the live bug on its first run.** Posed with
  **firewood** — which the *market* also holds — a marketer's leg quietly rescued every load: 622
  in the market, none on the ground. **Logs are held by the shed and the pile and nothing else**,
  which was Joe's own case and leaves no third party to save it. Re-posed, it went red instantly:
  300 logs on the ground beside an empty pile.
  - **The rule: when a red check comes back green, interrogate the pose before you doubt the bug.**
    Ask *what else in this village could be quietly solving the problem for me?*
  - ⭐ **Corollary that paid twice more the same afternoon:** the stalled-site guard failed on its
    own pose (a fresh valley has no logs either, so the sentence correctly said *"40 logs"* when
    the test demanded *"stone"*), and the food-limit guard asserted **zero gathering over two
    years and measured 327** — *which was the feature working*, because stores fall back through
    the limit and foraging resumes. **Three fixture bugs, one code bug, in one session.**
- **⛔⛔ A GUARD FORBIDS WHAT IT ASSERTS, NOT WHAT IT IS FILED UNDER (D247, 2026-08-28).** Joe
  explicitly overruled `NoPublicApiLetsACallerAssignAVillagerToAWorkplace` so a villager could be
  pinned to a trade — **and it never forbade that.** It blocks a public method taking a `Villager`
  **and a `Workplace`**: naming a person into a *building*. A method taking a `Villager` and a
  `JobKind` passes untouched, and §2.2 survives whole. ⭐ **Read what a guard actually says before
  spending permission to break it** — the answer was better than the overrule.
  - ⚠️ **And the feature still needed FIVE mechanisms**, reading *0 of 4,311 ticks* through four of
    them. The last is the unobvious one: **a pin has to outrank COST in the candidate sort**, or
    displacing the incumbent just lets the cost sort hire him straight back for living nearer.
- **⛔⛔ A CONTROL THAT ACCEPTS INPUT IT CAN NEVER ACT ON IS A BUTTON YOU CANNOT PRESS, FROM THE
  OTHER SIDE (2026-08-29).** The professions panel let Joe ask for **six jobs against four able
  adults**, and three rows sat reading *"asked 1 · nobody working of 0 seats"* — numbers he had
  typed that could never come true. ⭐ **The distinction worth keeping:** asking for more foragers
  than the *village wants* is a real instruction the sim can honour later (D106, correctly
  ceiling-less); asking for more *people than exist* is arithmetic, not a preference.
- **⛔⛔ A COST THAT LOOKS SMALL ON THE AVERAGE VILLAGE IS A CLIFF ON THE ONE ALREADY STRETCHED
  (D250, 2026-08-28).** The rest spell took a farm **ten ticks from its store** from 88% of what it
  sowed down to **74%**, while the farm **beside** its store stayed at 95%. **A 120-tick autumn
  has no slack to give**, so the tax came straight out of the harvest — and D178 had spent a whole
  slice making that distant farm work.
  - **⭐ Measure the MARGINAL case, not the median one.** The average village absorbed this
    invisibly. If a change costs time, find the configuration that had none spare.
  - ⚠️ **And the dial was NOT monotonic**: `rest_ticks` of 2 cost that farm *more* than 3 did
    (80% against 86%). **One sample per value is not a curve** — say "best measured", not
    "optimum".
- **⛔⛔⛔ ASK THE COMPILER BEFORE YOU BELIEVE A GREP — AND CHECK THAT YOUR ENFORCEMENT IS ACTUALLY
  ENFORCING (D246, 2026-08-28).** `Directory.Build.props` has set `EnforceCodeStyleInBuild=true`
  since the first commit and **there was no `.editorconfig`**, so every `IDEnnnn` analyzer sat at
  `silent` and `TreatWarningsAsErrors=true` had nothing to promote. **A project that fails the
  build on warnings quietly accumulated dead code for a year.**
  - **⭐ A three-agent audit missed things one config file found in thirty seconds:** four unused
    parameters (they need dataflow, not search), a duplicate `RepoRoot` one line below the shared
    one, and 43 redundant usings against an estimate of 65. ⚠️ **The audit said outright it could
    not detect unused parameters. It was right, and the answer was to turn the rule on.**
  - ⚠️ **`src/Bclone.Game` is exempt** (`TreatWarningsAsErrors=false`, for Godot's generators), so
    it **reports and does not fail**. **Read its build output.** A write-only field warned CS0414
    there for months and nobody saw it.
  - ⭐ **And a SKIP is a claim about the world that nothing re-reads.** One of the two had a reason
    where *both halves* had expired; un-skipped, it passes. **Re-read skip reasons the way you
    re-read a status line.**
- **⛔⛔ A LAYOUT THAT IS CORRECT AT STARTUP AND WRONG LATER IS INVISIBLE TO EVERY CHECK ANYBODY
  MAKES (D242, 2026-08-27).** The bottom bar ran off **both** edges — *"Pause"* clipped to
  *"use"* — but only **after the village learned to write**, because the Knowledge group is
  hidden until then and the row grows by a whole category mid-play. **Every look anybody takes at
  the UI is a look at a young village.**
  - **⭐ The general rule: ask what this panel looks like in year forty, not year one.** Things
    that appear on a condition — the library button, a second granary's row, a modded building —
    are exactly the ones no screenshot will ever show you.
  - ⭐ **`HBoxContainer` has no graceful failure**: one line, and anything that does not fit
    leaves the screen. **Prefer `HFlowContainer` for any bar that can grow.** ⚠️ Flow containers
    read `h_separation`/`v_separation`; the plain `separation` an HBox uses is **silently
    ignored**, so a straight swap quietly loses all your spacing.
  - **⛔⛔⛔ AND SWAPPING THE CONTAINER ALONE MADE IT WORSE — JOE CAUGHT IT IN ONE LOOK:** *"i think
    you messed it up. its tall and wide on the right side."* **The bar is a `Floating(...)` panel
    with width 0, so it is sized BY ITS CONTENTS.** An `HBoxContainer`'s minimum width is the
    **sum** of its children, which is what had been holding the bar open (and then dragging it off
    the left edge — the original bug). A flow container's minimum width is its **widest single
    child**, so the panel collapsed to one button wide and wrapped everything into a tall column
    in the corner.
    - **⭐ THE RULE: a wrapping container cannot decide WHERE to wrap unless something else
      decides HOW WIDE it is. Flow containers consume width; they never create it.** The fix is
      `Floating(..., spanWidth: true)` — anchors pinned to both sides — plus
      `SizeFlagsHorizontal = ExpandFill` on the rows. **The two changes are one change, and
      shipping either alone is a different bug.**
    - ⚠️ **Every other floating panel is deliberately content-sized and hangs off one corner.**
      `spanWidth` exists for the control bar alone; do not spread it to the columns.
  - **⛔⛔⛔ AND THE WORST PART: I DECIDED THERE WAS NO GODOT ON THE MACHINE AND THERE WAS.** I
    searched `C:\`, found nothing, said so, and shipped **two** unverifiable UI guesses — the
    second of which Joe had to catch. **`run.bat` has named the path all along**, three lines
    from the top:
    ```bash
    export GODOT="/d/Projects/Godot/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe"
    BCLONE_PROBE_WIDTHS=1 "$GODOT" --headless --path src/Bclone.Game
    ```
    - **⭐ THE RULE: BEFORE CONCLUDING A TOOL IS MISSING, GREP THE REPO FOR ITS NAME.** This one
      is configured, documented and used by the script Joe runs every day. *"Not on `C:`" is not
      "not installed"* — and a wrong "I cannot verify this" is more expensive than a slow check,
      because everything downstream of it becomes a guess.
  - ⭐ **The probe measures the control bar now**, which it did not, and both bugs are one line
    each in its output. ⚠️ **`--resolution` is ignored and the numbers are always 1280 wide** —
    `stretch/mode="canvas_items"` lays the UI out at 1280 logical pixels and scales it, so
    **a row that wraps in the probe wraps on every monitor.** There is no "it will fit on a
    bigger screen".
  - ⚠️ **A taller bar silently ate the columns' clearance.** `ControlsReserve` is a *measured*
    constant at 160; the wrapped bar is **189**, so the columns ran underneath it — **the wrap
    fix created the exact bug that constant exists to prevent.** It reads the bar's real height
    now, which is safe because the dependency runs one way: the bar's height depends on the
    window and its own contents, never on the columns.
- **⛔⛔ THE AUDIT TRAIL FINDS WHAT READING THE CODE DOES NOT — AND NOBODY HAD MINED IT LIKE THIS
  (2026-08-27).** D236 sat in `StoreForTheLoad` for months, read past by several sessions. What
  found it was arithmetic on the log:
  ```bash
  # every state transition, most common first
  grep -oE "DEBUG behavior [A-Za-z]+ #[0-9]+: [a-z ]+ -> [a-z ]+" "$L" \
    | sed -E 's/[A-Za-z]+ #[0-9]+: //' | sort | uniq -c | sort -rn | head -20
  # any activity, bucketed by year (480 ticks/year)
  grep -oE "^\[t +[0-9]+\].*-> clearing painted ground" "$L" | grep -oE "[0-9]+" \
    | awk '{printf "%d\n", ($1/480)+1}' | sort -n | uniq -c
  ```
  - **⭐ The tell was a state transition with no matching `goods` line** — villagers "fetching a
    load" thousands of times who never picked anything up. **Cross-reference the two streams**:
    an action with no consequence is a loop.
  - ⚠️ **And check what a suspicious line actually means before reporting it.** `carrying -13 food`
    looks like a catastrophe and is a **delta** (`+40 … now 360`, `-280 … now` empty). *Nearly
    filed as a bug.*


- **⛔⛔⛔ A SIM FEATURE IS NOT DONE UNTIL SOMETHING IN THE VIEW CALLS IT — SEVEN INSTANCES NOW, THREE
  OF THEM IN ONE WEEK.** The library was built, tested, red-checked and **invisible** (no draw call,
  no inspector row, no demolish path). Then **Move and Empty shipped with no buttons at all.** Every
  one had passing guards. **No test in this suite can catch it, and Joe finds it in ten minutes of
  play, every time.**
  - **The rule: write the button in the SAME COMMIT as the feature**, and if you cannot, say in the
    handoff that the feature is unreachable. *"Placeable is not reachable" was written down after
    the library and the next two features shipped unreachable anyway.*
- **⛔⛔ WHEN A CHANGE MAKES AN ACTION REVERSIBLE, GO BACK AND DELETE THE CONFIRMATION IT USED TO
  NEED (D235).** D228 made unpainting *level* a house, so a second-stroke confirmation was correct.
  **D230 made unpainting only MARK one, with repainting cancelling it — and the gate survived one
  commit past its reason.** To Joe it read as *"it wouldn't let me unpaint the land."*
  - **Friction that outlives its justification is indistinguishable from a bug**, and the two
    commits were both right on their own. *Ask what a safety is protecting against after every
    change to the thing it guards.*
- **⛔⛔ `git checkout -- <file>` DESTROYED UNCOMMITTED WORK AGAIN (D232), IN THE SESSION THAT
  RE-READ D194's WARNING ABOUT IT.** A red-check break failed to compile and I reverted the file
  instead of restoring from the scratchpad — losing three methods. ⚠️ **I had backed up ONE of the
  two files I was about to touch**, which is the exact half-measure the trap warns about.
  - **⭐ AND THE CHEAPER LESSON: a break that does not compile is not a red check, it is an edit to
    undo.** Write breaks that compile — flip a bool, set a bound to zero — and the temptation to
    reach for `git checkout` never arrives.
- **⛔ `grep -c` RETURNS EXIT 1 WHEN THE COUNT IS ZERO, so `grep -c "â" file && git commit` silently
  skips the commit.** Cost one confusing "why did that not land?" It is the encoding check this
  project runs constantly — **put it after the commit, or terminate it with `|| true`.**
- **⚠️ AN INSTRUMENT THAT ASSUMES A SIMPLER WORLD MEASURES SOMETHING ELSE — three times in one
  stretch.** A guard placed its granary by scanning from the map's *corner*, so nobody walked to it
  and *"emptied after three years"* measured the distance (D231). A guard assumed the founders' cart
  was empty and asserted on "40" against a wagon holding 200 (D233). A guard painted a block around
  the founding site that **re-painted the very tile it had just erased**, so the family rebuilt in
  the spot the test had turned them out of (D228).
  - **Read the numbers out of the fixture rather than writing them into it**, and when a guard needs
    people to walk somewhere, put the building where a player would.
- **⭐⭐ AND ONE TICK IS NOT A TREND, ANY MORE THAN ONE SEED IS (D227).** `ApprenticeshipTests` read
  *masters alive at exactly tick N* and a change turned one seed from 8→10 into **8→8**, which
  looked like Phase 3's pillar dying. **Two hypotheses died to a probe** — saturation, then
  *apprenticeship never fired* — before the answer: it is a **spot reading of a fluctuating stock**.
  Averaged over twenty years the same seed has the **widest margin of the three**. *The guard got
  stronger and the fallback plan was not needed.*
- **⛔⛔⛔ `perl -0777 -pi -e` WITH A WIDE CHARACTER IN THE REPLACEMENT DOUBLE-ENCODES THE WHOLE FILE
  (2026-08-26).** This handoff recommends `perl -0777` *because* of the repo's emoji — and that is
  exactly how it bites. Perl reads the file as **latin-1 bytes**; if the replacement string contains
  any code point above 255 (a literal ⭐, or a `\x{2b50}` escape), perl upgrades the entire output
  string and **re-encodes every byte in the file as UTF-8 a second time.** `Construction.cs` came
  back with `â` where every `—` had been, top to bottom.
  - **The tell is a one-line warning you will scroll past: `Wide character in print at -e line 1`.**
    Nothing fails. The build still succeeds. The damage is in 400 lines you did not touch.
  - **The rule: perl is fine for ASCII-only substitutions — a rename, a type change, deleting a
    line. The moment the replacement text contains an emoji or a dash, use Edit/Write instead.**
  - **And back up before you find out**: the recovery was `cp` to the scratchpad, then
    `git checkout --` on a file whose only uncommitted changes were two edits worth redoing. *That
    is the good case.*
- **⭐⭐⭐ A BREAK THAT REDDENS *NOTHING* IS THE MOST VALUABLE RESULT A RED CHECK CAN GIVE, AND IT
  HAPPENED AGAIN (D222).** Renaming the granary in the catalogue — **the word in the
  village log, in the placement sentence and on the panel** — turned **zero** tests red across 786.
  **D108 spent a decision fixing exactly those words** (*"the default arm called every unrecognised
  building a woodcutter's hut, in the log, in the panel, and in every placement sentence"*) **and
  nothing has ever guarded them.**
  - **⭐ The cure is a PAIR of guards, and the pairing is the point:** one proves the catalogue holds
    the word, one proves the code that writes the sentence *uses* it. D108's bug was a naming path
    ignoring the right answer, not a wrong answer stored somewhere — **a guard on the data alone
    would have been green through the original bug.**
  - **Ask, of any slice: which of these strings does the player actually read, and does anything
    check that they arrive?** Fourth in the family after D56, D177, D187 and D194.
- **⛔⛔ AND YOUR OWN SPEC IS A HYPOTHESIS TOO (D222).** `buildings-catalog.md §2.1` said, in bold,
  that `JobRow.WorksAt` **must** stop being an enum or the slice closes nothing. **Changing it
  reddened six `ModdedJobTests` in one run**: their JSON reads `"works_at": "GathererHut"`, a word.
  The enum was an *alias for the first N ids* all along — `ModdedGoodTests` had been casting
  `(Goods)6` since D210 — so what was missing was never the type, only a catalogue to resolve
  against. **The wrong version would also have made every row read `"works_at": 7`.**
  - *Written between reading the code and writing it, and wrong by the time the tests ran. **A spec
    sentence with "must" in it is the one to check first**, not the one to trust.*
- **⛔⛔ COUNT THE GOLDEN *VALUES*, NOT THE FAILING *TESTS* (2026-08-25).** Five tests reddened and
  I re-took five numbers. **`FarmGoldenTests` asserts two** — a full state hash and a
  skills-ignoring one — so fixing the first merely let the test reach the second, and the suite
  came back red again for what looked like the same failure.
  - **The rule: `grep "private const ulong"` across the affected files before replacing anything**,
    and expect parameterised arms (`[InlineData(...)]`) to hold values too.
  - **⭐ And pair each value to its arm by running the tests SEPARATELY.** Reading four `Actual:`
    lines out of one interleaved log and matching them to four arms by eye is how you write the
    fixture's hash into the shipped slot. *"Check every guard red, and count the reds" — counting
    the tests is not counting the reds.*
  - **⛔⛔ AND THE `grep "private const ulong"` RULE IS NOT ENOUGH — IT MISSED ONE ON ME (D223).**
    That grep found five goldens; **`SkillTests` holds its two as bare `InlineData` literals with
    no `const` anywhere**, so the grep never saw them and the enumeration was wrong before the
    first value was replaced. *The line above already said "expect parameterised arms to hold
    values too" — it was read, and still under-applied, because a rule that names one grep invites
    you to run that grep and stop.*
    - **⭐ THE RELIABLE ENUMERATION GREPS FOR THE LITERALS, NOT FOR THE DECLARATION:**
      `grep -rn "[0-9]\{15,\}" tests/Bclone.Sim.Tests/*.cs`. A golden is a 19-to-20-digit number
      however it is spelled — `const`, `InlineData`, or an argument. **It also turns up the history
      comments, which is a feature: those are where you write the old value.**
  - **⭐⭐ AND THE GOLDENS THAT *DO NOT* MOVE ARE THE RESULT, NOT THE LEFTOVERS (D223).** Bringing
    the fixture to `food_per_meal: 4` moved four values and held four — and **every held one runs
    the SHIPPED config** (`ShippedFiftyYearHash`, `SkillTests`' true arm, `GoldenMapHash`, all
    three `PerSiteYieldTests` arms). *That is what proves a fixture change stayed inside the
    fixture. Check it deliberately with a `git diff` on those lines; do not just notice they were
    green.*
- **⭐⭐⭐ A COMMENT THAT IS TRUE OF ONE PATH IS THE HARDEST BUG IN THIS REPO TO SEE (D220).**
  `RegrowthSystem` said *"a sapling seen by a sweep is a sapling that has stood for one period,
  because the sweep visits every tile exactly once per period."* **Perfectly true — for saplings
  the sweep seeded itself.** A forester plants at an arbitrary tick, so the next visit might be the
  very next one, and planted trees matured **three times faster** than seeded ones for as long as
  that comment stood.
  - **It read so plainly that nobody thought to test it**, which is what makes this class worse
    than a wrong comment: a wrong one invites checking. **D200 found the same shape** in
    `LabourSystem`'s *"never moves someone who already has a job."*
  - **The tell: a sentence that explains WHY it is true.** *"…because the sweep visits every tile
    once per period"* is a proof sketch, and a proof sketch names its assumptions. **Ask which
    paths satisfy them.**
- **⛔⛔ FINISH THE MERGE, OR SAY OUT LOUD THAT IT IS NOT MERGED (2026-08-25).** Eleven commits of
  finished, green work sat on a branch in a worktree while `main` had none of it. **Joe played
  `main`, saw *"villagers harvest stone but the pile shows 0 stone"*, and filed a bug that was
  entirely real on his build and entirely fixed on the branch he was not running** (D217). The
  session that had done the work spent its reply diagnosing a build rather than a village.
  - **⚠️ AND THE MERGE WAS THEN LEFT HALF-DONE** — two unresolved conflicts, nothing committed,
    `git log` still showing an older tip. A fresh session asked to *"review what's in"* had to
    discover that **nothing was in** before it could review anything.
  - **The rule: a branch is not done when the tests pass, it is done when it is on `main`.** If it
    cannot be merged yet, **the handoff must say so in the first paragraph** — because the person
    playing the game has no way to tell which build they are on.
  - ⭐ **When two sessions have both edited the docs, expect conflicts and expect BOTH sides to be
    true.** All three here were documentation where each session had updated the same line about
    its own half; the resolution was *and*, never *either*. **Take both, then check the arithmetic:**
    761 + 11 = 772 is what proved neither side's guards were dropped.

- **⛔⛔⛔ `git checkout -- <file>` DESTROYS UNCOMMITTED WORK AND THERE IS NO UNDO — I DID IT TO
  MYSELF (D194).** Mid-slice, wanting to revert *one deliberate break* in `SimWorld.cs`, I ran
  `git checkout --` on it and **reverted the entire slice's uncommitted implementation.** A
  backup taken minutes earlier saved it. Later in the same session I deleted two untracked test
  files with `rm` while splitting a commit, **had no backup of those**, and had to rewrite both
  from scratch.
  - **The rule: before reverting or deleting anything you have not committed, copy it to the
    scratchpad first — every file, not just the ones you think are involved.** A deliberate break
    for a red check is exactly when this bites, because you are *trying* to throw work away and
    it is easy to throw away more than you meant.
  - **⭐ And prefer `perl -0777 -pi -e` to revert a break**, since it undoes precisely what it
    did. `git checkout` cannot tell your break from your feature.
- **⛔⛔ `dotnet test --filter FullyQualifiedName~Foo` MATCHES THE CLASS NAME, NOT THE FILE (D198).**
  Breaking the harvest brush's mode filter appeared to turn **nothing** red, and I nearly recorded
  a coverage hole that does not exist — the guard lives in class `HarvestBrushModeTests` **inside
  `SeamsTests.cs`**, and `~SeamsTests` never ran it. **It reddens three times.** *A surprising
  green is a claim about your filter before it is a claim about the code.*
- **⭐⭐⭐ WRITE THE GUARD FOR A CLAIM THE DOCS MAKE, AND YOU MAY FIND THE CLAIM WAS ALREADY FALSE
  (D200).** `LabourSystem` had said for phases that its slack pass *"never moves someone who
  already has a job."* **It does** — `ShedSurplus` releases somebody and `Match` re-places them in
  the same pass, **67–83 times over fifty years at the cadence that sentence was written for.**
  The behaviour was right and the sentence was wrong. *A long-standing comment is a hypothesis
  nobody has tested.*
- **⚠️ ONE SEED IS NOT A TREND, AND I READ ONE AS A TREND (D200).** Firewood fell 156 → 131 → 91
  as a cadence quickened and I called it a real cost. **Across three seeds it goes down, up, and
  down-then-up.** It was noise. *A spot reading of a fluctuating stock is not a trend — and this
  nearly became the reason not to ship a change.*
- **⚠️ CHANGE A TIMING AND FIXTURES BREAK THAT ARE NOT REGRESSIONS (D200).** One config key moved
  and **three guards went red, none of them a bug**: a life-log guard matched the bare word
  *"foraged"* and flagged the **mastery line** (*"has foraged these woods for 18 years"* — a
  statement about a life, not about this winter); an at-risk guard killed the two masters it had
  posed and **had not noticed the village grows its own**, 15–19 a century; and it picked the
  first frail villager rather than the one with most life left, so **a warning that stopped
  because the person died read exactly like a warning that stopped working.** *Ask what a fixture
  quietly depends on before calling its red a regression.*
- **⭐⭐ THE BUG IS OFTEN A NUMBER RATHER THAN A MECHANISM (D197).** The market restock leg looked
  wrong — distribution effort rose 24–79%. **The mechanism was fine; the target was
  `market_stock_per_household × economy_horizon_households` = 800**, so a village of five homes
  needing forty apiece had a marketer hauling stock for twenty households. *Before rewriting a
  mechanism, check what number it is aiming at.*
- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Repeatedly this has caught a guard that
  proved nothing. **And the guard that catches a bug is often not the obvious one** — *"the
  farm's sentence says farmer"* passes against a generic template with the farm's name in it;
  the one that works reads both sentences with the names masked out and requires them to differ.
- **⭐⭐ A SPEC CAN ASK FOR A GUARD THAT CANNOT EXIST, AND THE DoD IS WHERE IT HIDES (D181).**
  `skills-catalog.md §11.2.1` required *"provable no-op: goldens unmoved"* for a slice whose
  entire content is **new hashed state that grows from tick one.** It was reasoned by analogy
  from a slice where the analogy held, it sat in a Definition of Done for a week, and it would
  have been "met" by quietly not hashing proficiency — which would have cost the determinism
  guarantee and moved the goldens twice later instead of once. **Ask what a DoD item would look
  like if it were satisfied *before* you try to satisfy it.** The fix was to restate the claim in
  a vocabulary that can be true (*nothing anybody DOES changed*), not to weaken the guard.
- **⭐⭐⭐ AND THE BREAK THAT TURNS UP *NOTHING* IS THE ONE THAT CHANGES THE DESIGN (D194).** Two
  drafts of the farm's memory had it commit `learned + 1` a year and latch once a tile rotted.
  **Deleting both turned no guard red** — settled memory and tiles reaped identical at all three
  distances. The mechanism was redundant because `HarvestOneFarmCanBringIn` multiplies by the
  hands standing in the field *at that moment*, so **a farm with two hands in spring and one by
  autumn already over-commits on its own.** *The village probes without being asked.* The probe
  was **deleted rather than guarded** — a fifth invisible no-op after D56, D177 and D187. **Zero
  reds is a result, not a formality passed.**
- **⭐⭐ BREAKING YOUR OWN GUARDS FINDS THE BLIND ONES — DO IT, AND EXPECT A SURPRISE (D181).**
  Nine reds across seven deliberate breaks, and break #2 turned a guard red **for a reason
  unrelated to what it tested**: `LeavingATradeStopsTheClockOnItThatTick` sampled on a year edge,
  so *"the number did not move"* was two effects cancelling — no growth, and no decay only
  because the floor happened to protect a first-year worker. **The red check is not a formality;
  it is the only thing that reads your fixture for you.**
- **⚠️ THE VILLAGE IS BRIEFLY JOBLESS ON THE YEAR EDGE, AND IT WILL BAFFLE YOU (D181).** At
  *Day 1, Spring* the reshuffle has torn every allocation down and not yet rebuilt it: **0 of 4
  able adults hold a job on that exact tick.** Any guard that samples "who is working?" at
  `TicksPerYear * n` is sampling that hole. **Step half a season in.** (Winter is the other one:
  D44 unstaffs seasonal trades, so mid-winter is 1 of 4.)
- **⭐⭐⭐ AND SOME STATE CANNOT BE POSED AT ALL, BECAUSE IT IS DERIVED — TWO REDS TO FIND (D195).**
  An elder cannot be posed. Writing `LifeStage` lasts **one tick** (`AgeingSystem` recomputes it
  from vigour); writing `AgeYears` lasts **one tick** (`ClockSystem` recomputes it as
  `year - BirthYear`) — the guard **watched a 51-year-old turn 21** between the first tick and the
  second and read the resulting silence as a broken feature. `BirthYear` is `init`-only, which was
  the model saying so all along. **The honest fixture steps the sim until somebody genuinely grows
  old**, and it is barely slower. *Before posing a value, ask whether anything recomputes it.*
- **⭐⭐ AND A *FIXTURE* CAN FIGHT THE MECHANISM IT IS TESTING (D194).** Three guards for the
  farm's memory posed *"a clean autumn"* as **one sown tile** — so the farm brought in one tile,
  correctly recorded that one tile was what it had managed, and **the guards failed for the
  feature working.** The memory is a high-water mark, so a posed field *smaller* than the
  building's own commitment is a **worse** year, not an easier one. **Ask what your pose means to
  the system, not just what it means to you.**
- **⭐⭐ A GUARD CAN BE GREEN AND BLIND.** `AFarmBringsInMostOfWhatItSows` reports 93% while the
  played village was at 46%, and it is not wrong — it sites its farm a step from the stores.
  **Unmoved because it does not cover the case** (D157, three times now). Ask what a guard's
  fixture *makes impossible* before trusting its number.
- **⭐⭐ THE INSTRUMENT IS AS LIKELY TO BE WRONG AS THE CODE.** In one session: a probe reported a
  farm reaping 60 of 60 tiles because its *"reaped"* column counted winter rot as harvest, and a
  guard was written claiming an untested happy path when the guard for it was ten lines above.
- **⭐⭐ THE SIM'S AUDIT TRAIL IS EVIDENCE ABOUT THE SIM AND SAYS NOTHING ABOUT THE VIEW.** Two
  sessions hunted a rendering bug in `BehaviorSystem`. **Ask which half the symptom lives in
  before opening the log.**
- **⭐⭐⭐ THE INSTRUMENT WAS WRONG TWICE IN ONE SESSION, AND BOTH TIMES IT NEARLY CHANGED A LOCKED
  NUMBER (D189).** *"Gathering brings in five times what farming does"* came from a probe counting
  food into the **farm's own store** — which a reaper hauling to the granary never touches.
  **Counting reaps instead flipped the answer to "farming wins by 28%".** The wrong number would
  have justified raising `crop_yield_per_tile`, which is derived and locked. **Before a
  measurement justifies a change, ask what the instrument cannot see.**
- **⭐⭐ A DERIVATION THAT AVOIDS STATING A NUMBER STILL STATES ONE (D192).** The thaw rate was
  *derived* by mirroring the outdoor rate, on the explicit grounds that mirroring *"needs no
  number of its own"* — true, and it quietly chose **fifteen days to thaw**, half a winter, which
  nobody noticed until Joe played it. **Check what a derivation came out as, not just that it is
  principled.**
- **⭐⭐ A SMALL-RANGE RNG DRAW AT A FIXED STRIDE CORRELATES, AND THE FOUNDING IS FOUR SUCH DRAWS
  (D190).** Both founding pairs drew the **same** personal rhythm — 1, 1, 2, 2 — so the fix for
  D28 did nothing. **The RNG is not at fault:** forty raw `NextInt(0, 4)` draws come out 9/11/8/12.
  It is the *stride* at the start of the stream. **A generator can be sound and still be the wrong
  tool for four draws that must differ from each other** — deal or rotate, do not draw.
- **⚠️ HUNGER IS A PURE FUNCTION OF TICKS SINCE THE LAST MEAL (D190).** Two villagers who eat on
  the same tick stay in step for ever, **however differently they walk** — so a stagger that
  offsets only movement leaves *identical hunger at 100%*. Anything meant to desynchronise people
  has to touch the hunger clock too.
- **⭐ FINDING A CAUSE IS NOT FINDING THE CAUSE** (D163, D166, D169 — three rounds on one symptom).
  - **⛔⛔ AND THE FOURTH ROUND PUT TWO WRONG CAUSES INTO DOCUMENTS BEFORE THE RIGHT ONE (D182).**
    *Why does a forager take 32 calendar years to reach 20 years on the task?* **Wrong once:**
    *"winter stands the work down"* — the evidence was **1 of 4 able adults hold a job in
    mid-winter**, a **headcount**, read as **availability**. Foraging is worked in all four
    seasons; there are just fewer people on it. **A number that is true can still be evidence
    for the wrong claim.** **Wrong twice:** *"derive each trade's mastery from the share of a
    year it is staffed"* — that measures **demand**, which is the player's business, and would
    have pinned woodcutting at five years because this village wants one occasionally.
    **Right:** decay, taking **37% of everything a career earns.**
  - **⭐ The thing that caught both was building the measurement needed to ACT on the claim.**
    The first survived a probe because the probe answered a different question; the second died
    the moment its own numbers were printed next to what they implied. **If a finding is about to
    become a config number, measure the number — not the story.**
- **⭐ THE HELPER YOU NEED MAY ALREADY EXIST.** `Main.Wrapped` had been doing exactly the right
  thing on five labels for two UI rebuilds while every sentence in the inspector went into a bare
  `Label` in an `HBox`. Grep before writing.
- **⚠️ IF A NUMBER GOES INTO A DOCUMENT, IT COMES FROM A RUN.** Four for four, the fourth being a
  handoff's own warning about it.
- **⚠️ CHECK A DOCUMENT'S REFERENCES AGAINST THE THING.** Every file said "PR #3" for a day.
- **⭐⭐ MEASURE THE TOOLING TOO, NOT JUST THE VILLAGE (D179).** The suite ran nineteen minutes and
  the obvious fix — tag the long acceptance runs as slow — was **wrong**: it was already 9.5×
  parallel, so throughput was never the cost. The real culprit was an **O(n²) Dijkstra nobody had
  ever timed**, four seconds a world. **It is 2m30s now.** *The thing everybody suspects is not
  the thing costing the time — and Joe had to say "measure it first" to stop the wrong fix.*
  - **⭐ AND IT APPLIES TO A GUARD YOU JUST WROTE (D198).** A new sweep built **a fresh world per
    tile** — 28 seconds an arm, **2.8 minutes for one file**. One world was enough *and was a
    truer test*, since the preview and the paint are then asked of the same world in the same
    state. **0.8 seconds now, over nine times as much ground.** *If a new guard is slow, the
    fixture is usually doing something the claim never needed.*
- **⚠️ A FULL RUN IS FOR A VERDICT, NOT FOR DISCOVERY.** One slice here burned four full runs,
  twice to learn what was already knowable. **Use `--filter` while iterating.**
- **⛔⛔ DO NOT WRITE A WAIT-LOOP FOR A RUN THAT IS ALREADY IN THE BACKGROUND.** It is redundant —
  **the completion notification arrives by itself** — and it adds a failure mode that has now
  cost this project two sessions.
  - **2026-08-16:** two shells spun **thirteen hours** waiting for `Passed!` against a file
    written with `--logger "console;verbosity=detailed"`, which ends `Test Run Successful.`
    instead. *The two output formats end with different strings.*
  - **2026-08-22:** two more spun **an hour and three quarters** waiting for `Passed!` against a
    file that was the output of `dotnet test | grep … | head -30` — **the summary line had been
    filtered out before it ever reached the file.** *Grepping a file for a line you already
    grepped away.*
  - **The rule: a wait-loop whose condition cannot be met is a vacuous guard that costs wall
    time instead of passing silently.** If you truly must poll, poll for something the file is
    *guaranteed* to contain — and prefer just waiting for the notification.
- **⛔⭐ AND DO NOT REWRITE THIS TRAP LIST FROM SCRATCH — CARRY IT FORWARD.** The warning directly
  above was written on 2026-08-16 by the session that lost thirteen hours to it. **I deleted it
  on 2026-08-22 while tidying the handoff after the Phase 2 merge, and walked into the identical
  trap ninety minutes later.** *A handoff rewritten wholesale silently drops exactly the
  hard-won warnings it exists to carry* — which is D159's drift running the other way: the
  document losing knowledge the code never had. **Edit this file; do not replace it.**
- **⭐ AND WHEN A SPEC AND A MEASUREMENT DISAGREE, THE SPEC IS THE ONE THAT IS WRONG.** D178 wrote
  a soil algorithm into a spec, probed it, and found it made the number it existed to raise
  *worse* — and separately inferred the founding ground was "already ordinary" from a fact about
  draw order that turned out to imply the opposite. **Both were caught by ten-minute probes.**
- **`python` is not on PATH**, and string edits die on this repo's CRLF and its emoji. Use
  `perl -0777`, or the Edit/Write tools for anything with quoting in it.
- **⚠️ Goldens go last, one commit, one stated reason** (D152). The seam golden moves when a
  village that farms changes; the two fifty-year goldens do not, because **neither village ever
  places a farmhouse** — silent about what they do not reach, loud about what they do.

---

## Working with Joe

Technical, not a game or systems programmer. Casual, direct; **push back honestly**. **End every
message with the explicit ask**, or he cannot tell who is blocking whom. **His play is the best
bug-finder this project has** — and the clearest case is the jitter: he reported one symptom
three times across three sessions, and it took all three to get past the two real bugs it was
hiding to the one actually on his screen.
