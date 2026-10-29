# Spec: A golden that reaches the Phase 5 chains (Phase 5's exit gate, item 2)

**Decision:** D642 (this document — the audit). Neighbours: D641 (Joe accepted the Phase 5 gate as written; item 2 is
*"a determinism golden that reaches the Phase 5 chains"*), D157 (Phase 2's item 2, the same question then: a golden
that reached `NearestHarvest`), D601 and D617 (no harness village raises a market's worth of trade or a smithy).
**Status:** 🔍 **AUDIT DONE (D642): no pinned golden reaches any Phase 5 production chain.** Housing, doors, maintenance
and nomads are reached. The smithy, the quarry, the mine, the mill, the bakery, the well, bridges, the trading post and
its boat, and the barn and its herd are not. 📝 **§5's posed-village golden: Joe's go-ahead (D642, *"Merge the audit
first"*), 📏 MEASURED (D643, §6), NOT BUILT:** every chain turns over in a posed 50-year run except the herd, whose
cattle starve with hay in the barn even with a pinned herdsman. 🔍 **Traced (D644, §7) — ⚠️ AND §7's DIAGNOSIS
WITHDRAWN (D645, §8):** seeds 2 and 42 open winter with no hay out, the traced herdsman on 12345 was homeless, and a
housed one still loses 13 head. 🔍 **Every death read (D646, §9):** errands that outlast the 20-tick feed window (the
household's ordinary shopping ranks above all work), long walks, the cold they bring, and no slack at the herd's end. Four
fixes were tried and none saves seeds 2 and 42. ✅ **Joe picked the barn (D647, §10):** a kept herd eats from the barn's
own hay when nothing is out, built and guarded (`livestock.md §0.3`); no posed herd starves while a herdsman lives. ⚠️ **And
§10 found the pose kills villages:** seed 42's is dead by year 18, seed 2's loses 56 to cold and hunger, seed 12345's 13.
§9's seed-42 reading was taken on a dying village. ⏸️ **Joe's call:** pin the golden on seed 12345 as it is, or make the
pose play like a player first. Owner: Joe + Claude Code.

---

## 1. Which goldens pin a village over years

A golden is a pinned state hash. The suite's other pinned values are not village runs, and they stay out of this
audit: `MapGenerationTests` (terrain only), `NewGameScreenTests` (seed text to a seed), `StateHashTests` (the FNV
basis), `AngleTests` and `FixedTests` (the trig and fixed-point tables). `DeterminismTests` runs a world twice and
compares the two runs, so it pins nothing, and its world is an RNG churn, not a village.

Five hashes in three tests pin a village that lives for years:

| Run | Test | Village | Years | Hash |
|---|---|---|---|---|
| `fixture50` | `StockLimitTests.AVillagePlayedWithoutLimitsIsTheVillageThatCameBefore` | `VillageFixtures.Village` | 50 | `FixtureFiftyYearHash` |
| `shipped50` | the same | `ShippedConfig.EstablishedWithNoLimitsSet()` | 50 | `ShippedFiftyYearHash` |
| `fixture50-noskill` | `SkillTests.FiftyYearsOfVillageAndOnlyTheCountersMoved` | the fixture, mastery off | 50 | without skills |
| `shipped50-noskill` | the same | `ShippedConfig.Established()`, mastery off | 50 | without skills |
| `farm20` | `FarmGoldenTests.AVillageThatFarmsAndClearsAtOnceRunsTheSameWayEveryTime` | the fixture + a posed farm, painted harvest | 20 | `SeamGoldenHash` |

## 2. How it was measured

`tools/harness/ZzGoldens.cs` replays each run exactly as its test builds it. It asserts that the final hash equals the
golden's own constant, so the run measured is the run pinned, and all five matched. It samples once a game day:

- workplaces standing, by job, and the days each had a worker;
- stores by kind;
- every good held across all stockpiles;
- bridges, the herd's head and whether a boat ever came;
- bands that knocked, and workplaces ever repaired;
- wells, and homes and their doors.

All five together take 18 s in Release, build included. Copy it into the test project, run `-c Release --filter ZzGoldens`, then delete
it.

## 3. What the goldens reach

| Phase 5 item | Reached? | Evidence (50-year runs; `farm20` where it differs) |
|---|---|---|
| Organic housing | ✅ | up to 6 homes raised (`farm20`: 4); peak 34–44 people |
| Doors (D619–D625, the sim) | ✅ | every home has its door, 6 of 6 |
| Building condition and maintenance | ✅ | 4 workplaces repaired (`farm20`: 5) |
| Nomads | ✅ | 5 bands knocked (`farm20`: 1) |
| The market as a shop | ✅ | a founding market; marketers on shift 4,200–4,500 days |
| Tools | ⚠️ the founders' only | tools held peak at 20; nobody ever forges one |
| The well | ❌ | 0 wells |
| The smithy, iron, tool kinds | ❌ | no smith ever; iron and iron tools never held |
| The quarry | ❌ | no quarrier; stone never held |
| The mine | ❌ | no miner |
| The food chain (mill, bakery) | ❌ | no miller or baker; flour and bread never held. **The 50-year goldens never farm at all**; only `farm20` does, with a posed farm (wheat peak 1,640), and it raises no mill |
| Bridges | ❌ | 0 |
| Trade (the post, the merchant, the boat) | ❌ | no merchant; no boat ever |
| The herd (barn, hay, leather) | ❌ | no herdsman; herd 0; hay and leather never held |

Every 50-year run stands the same five workplaces: forager, forester, woodcutter, marketer and builder. No golden
reaches fishing or hunting either. Those are Phase 3–4 content, so they are outside this gate, but they are noted.

⚠️ **What this means:** a change to how the smithy forges, how the mill grinds, how a bridge is crossed, how the boat
trades or how a herd grows **moves no golden**. Each of those facts has a unit guard that proves it reaches the hash.
For example: `MineTests.IronToolsForgedAreInTheFingerprint`, `FoodChainTests.TheWheatCounterIsInTheFingerprint`,
`BridgeTests.ABridgesKindIsInTheHash`, `TradeRowsTests.EveryNewFactReachesTheHashOnlyWhenItIsSet`,
`MaintenanceTests.TheStampsAreHashedOnEveryKind`. A unit guard proves the fact is fingerprinted. It does not run the
chain for years and pin where it ends. So a regression in a Phase 5 chain is caught only if some unit test happens to
pin that number.

## 4. Found in passing: two goldens the CLAUDE.md grep cannot see

CLAUDE.md's fourth verification line is `grep -rn "[0-9]\{15,\}" tests/Bclone.Sim.Tests/*.cs`, and it misses goldens
written with digit separators:

- `AngleTests.cs:336`: `14_897_684_010_207_565_105UL`
- `FixedTests.cs:440`: `18_274_212_356_918_558_434UL`

Both pin the math tables, not a village. Still, *"388 before and after"* has never covered them. A grep that sees both
spellings is `grep -rnE "[0-9](_?[0-9]){14,}"`, but its count is a new baseline. ⏸️ Changing that line is Joe's call:
CLAUDE.md is his working agreement.

## 5. Proposed: a posed Phase 5 village, pinned (NOT BUILT)

*Gate item 2 says: "audit which of those any golden reaches; pose a village for the rest". This is the pose.*

1. **One new golden test, on the shipped data** (`ShippedConfig.Established()`, with the shipped limits), at the
   config's seed.
2. **At tick 0, pose everything §3 marks ❌, with the fixtures that already exist:**
   - a farm with ground (`FarmFixtures.RaiseAFarm`, `GiveItGround`);
   - a mill and a bakery (`FoodChainTests.RaiseA`);
   - a quarry and a mine (`RaiseA`);
   - a smithy (`ToolsTests.RaiseASmithy`);
   - a well (`ObstacleTests.RaiseAWellNearTheFounding`);
   - a bridge (`BridgeTests.RaiseABridge`, which needs a crossing on the seed);
   - a trading post and a barn with its herd (`TradeRowsTests.PoseTradeAndAHerd`).

   Posing skips the unlock gates: knowledge, wheat reaped, people. **This golden pins the chains, not the unlocks**,
   and the unlocks keep their own tests.
3. **Pin both hashes**, `Compute` and `ComputeIgnoringSkills`, as `farm20` does. The number of years is measured
   before it is typed. The village has to live long enough for every chain to turn over several times, and cheaply
   enough not to move the suite's clock (D329). All five §1 runs together take 18 s in Release.
4. ⛔ **A coverage test beside it is part of the golden, not optional.** Like `TheRunActuallyPaintsClearsSowsAndReaps`,
   it asserts that each chain actually turned over: iron mined, a tool forged, stone quarried, flour milled, bread
   baked, the bridge crossed, a boat docked and traded, and a beast born and culled. A golden without one can pass
   while reaching nothing, which is exactly the state §3 found.
5. **Red-check (D326):** break one chain, for example the smithy's output, and count the reds. The golden and the
   coverage test must both go red.
6. ⚠️ **Re-takes:** every later change to a Phase 5 chain will move this golden. That is the point of it, and it costs
   a §7 line each time, as the 50-year goldens already do.

## 6. Measured: the posed village, 50 years on the shipped data (D643)

`tools/harness/ZzPose.cs` builds §5's pose, steps it 50 years and prints one `ZZP` line a year: what each chain made,
the villager-ticks spent in each chain's working state, and the log's herd and boat lines. One run takes 45 s in
Release, with the probe sampling every tick. Three things §5 did not foresee had to change before the chains turned:

- **The bridge cannot be posed at tick 0.** It unlocks once the village has held 10 people (`MostEverAlive`), and that
  count is derived from the villagers' own history, so it cannot be set. The pose raises the bridge the first tick the
  village has held 10 (year 20 on this seed), and marks a well across the river, as `BridgeTests` does, so someone has
  a reason to cross.
- **A herd posed at tick 0 dies in year 1.** Herdsmen give way whenever food comes first, and four founders' food
  always comes first. The herd arrives by trade instead: a pasture is painted round the barn and an animal order is set
  (`SetTradeBuyHead(4)`; refused without a pasture). Trade orders are posed too, keep 300 firewood and buy up to 50
  stone, because a merchant is only wanted once the player has asked for something (`LabourQuota.MerchantsWanted`).
- **`StoneEverDug` counts digging by hand only**, as the quarry's unlock. The quarry's own output never passes through
  it, so the coverage reads stone held in stores and villager-ticks spent `Quarrying`.

| Chain | Turns over? | When, and how much (50 years) |
|---|---|---|
| Farm → mill → bakery | ✅ | flour from year 9; 7,040 ground and 6,840 baked |
| Quarry | ✅ | from year 1; 550 stone held at most, and quarrying stops when the stock limit is met |
| Mine | ✅ | from year 6; 222 iron held at most |
| Smithy | ✅ | from year 20; 46 tools forged |
| Well | ✅ | water drawn from year 1 |
| Bridge | ✅ | raised in year 20; 139 crossings |
| Trade | ✅ | 19 boats; *"The boat from Larkspur took # firewood and left # cattle"*, 4 times |
| **The herd** | ⚠️ **barely** | 11 head landed or born and 11 lost; **10 of them starved "with hay still in the barn — nobody carried it out"**; the one cull came in year 42 |

⚠️ **The herd is not a pose problem that the pose can fix.** A living herdsman was pinned the first tick cattle stood in
the barn (`SetPinnedTrade`, the player's own control), hay was cut (500 at most), and the cattle still starved every
winter with hay in the barn. `HerdWantsFeeding` also asks `AnyPastureToFeedOn(barn)`, which is one candidate; this was
not traced further. **Unattended-village losses are the harness playing badly until a played run says otherwise**, so
this is put to Joe with that caveat, not as a bug. A golden pinned over it would hold one cull in fifty years, and its
coverage test would flip whenever that cull crossed the cutoff. **So the golden is NOT BUILT** until the herd is
understood: traced as a bug, accepted, or taken out of the golden's coverage.

## 7. Traced: why the cattle starve with hay in the barn (D644) — ⚠️ withdrawn by §8 (D645)

*Joe, D644: "Trace the herd". `ZzPose.cs` with `ZZ_TRACE=1` prints a `ZZH` line every feeding day while beasts stand:
head, hay out, hay in the barn's store, `HerdWantsFeeding`, where the hay goes, and the pinned herdsman's state and tile.
A `ZZW` line gives each refill's response time: the ticks from the herd first wanting hay to hay going out.*

**What it is not.**
- Not distance: the herdsman's home is about 8 tiles from the barn, and the pasture is beside it.
- Not the store: hay is in the barn's own store (`BarnStoreOf` is the barn).
- Not the pasture: `WhereTheHayGoesOut` always has a tile, and `AnyPastureToFeedOn` holds.
- Not the pin: he is alive, seated at the barn, and an adult.

**What it is: the herdsman answers later than the lead allows.** He fetches hay when less than `hay_feed_ahead_days`
of the herd's hay is out, which is 5 days, or 20 ticks at 4 ticks a day. The trace shows him `Resting` or
`SeekingShelter` through spells of 3 to 4 days while the herd wants feeding. He re-decides only when that spell ends,
which is D593's own finding at 3 days (*"a herdsman finishes his rest before he re-decides and spends winter going in
to get warm"*). On seed 12345 his response is a median of 12 ticks and up to 22. The tail runs past the lead, and a
beast starves each time it does.

**A bigger number is not a fix.** Measured with 4 head posed at the village's 10th villager, independent of what the
boats bring:

| Seed | Lead 5 days (shipped): starved | Lead 10 days: starved |
|---|---|---|
| 12345 | 11 | **0** (45 culls) |
| 2 | 4 | **16** |
| 42 | 4 | 4 |

At 15 days on seed 12345, 4 starve again. Every death on every seed and lead is the same sentence: *"starved with hay
still in the barn — nobody carried it out"*. ⚠️ `ZZW`'s largest waits (2,600 and 8,908 ticks) are a measurement
artefact: a herd that dies out leaves the clock running until the next beasts come. They are not a herdsman gone for
years.

**Never played.** Joe's 1,286 logs in `src/Bclone.Game/logs/` show barns marked and finished (3) and boats docking.
None has a beast landing, a herdsman, or any herd line. D600 played the fences, not a winter herd. **The winter herd
has never been played**, so this harness result has nothing played to check it against.

⏸️ **Joe's call: the feeding rule assumes a herdsman who answers within the lead, and nothing makes him answer.**
Three shapes, none built:
1. **A hungry herd calls its herdsman out of rest and shelter**, as an empty larder calls a household
   (`TryEmergencyRestock`'s shape). The carry stays visible.
2. **The herd eats from the barn's own hay when none is out.** This is cheap, but it deletes the carry, the legible
   act, in exactly the case where the player would see it.
3. **The herdsman puts out enough to last until he next decides**, sized from his own day and not from a lead.

*Recommended: 1*, because it keeps the work on screen (§1.1) and copies a shape that already exists. The golden waits on
this, since its herd would pin the bug.

## 8. ⚠️ §7's diagnosis does not hold, and fix 1 is not built (D645)

*Joe picked §7's fix 1 (D645: "Herd calls him (1)"). Before speccing it, a tick-by-tick trace of the herdsman (`ZzPose.cs`,
`ZZ_TICK=1`: state, ticks left, cold, tile, arms and home every winter tick the herd wants hay) and a count of where each of
his winter shelter spells begins (`ZZC`) were run. They do not support §7.*

1. **On seeds 2 and 42 the failure is a different one.** `ZZ_HERD=1` put 4 head in the barn on spring's first day: ticks
   4321 and 7681, each the first tick of a 480-tick year. Each first winter then opens with **nothing put out**. A beast
   starves on winter's first day while the herdsman is still bringing the first armful in, so the late-fall feed-ahead
   window put no hay out. Why it didn't is **not traced**. It is not a late answer from rest, so §7's seed table, read as
   one cause, is withdrawn.
2. **On seed 12345, where the cattle came by trade, the pinned herdsman is homeless.** His household has no house:
   `HomeTile` is empty. The pose pinned the newest adult, and a new household waits for its house. So he rests in the open
   at the founding site, his cold climbs at rest, and **51 of his 59 winter shelter spells begin from resting**. Only 3
   begin on a hay errand: 2 walking to the barn, and 1 with 80 hay in his arms one tick from putting it out.
3. **A housed herdsman does not cure it.** With the newest adult *whose household has a home* pinned (`ZZ_PIN=housed`),
   13 still starve on seed 12345. Those 13 are **not traced**.
4. **So fix 1 is not shown to help.** It cuts a rest short, but rest is where the homeless herdsman gets cold, not where a
   herdsman idles while the herd waits. The housed run's deaths and seeds 2 and 42's empty first day are unexplained.
   Building it now would be building on a guess.

**What stands:** every death is narrated *"starved with hay still in the barn — nobody carried it out"*; the winter herd
has never been played; and the posed golden still waits on the herd. **What does not:** §7's single cause, its seed
table read as one cause, and its ranking of the three shapes.

⏸️ **Joe's call:** trace the deaths first, one at a time: the housed run on seed 12345, and the empty first day on seeds 2
and 42. Or take the herd out of the posed golden's coverage for now, so it checks that cattle land by trade, not that
they live, and pin the rest.

## 9. Every death read (D646)

*Joe, D646: "Trace the deaths", then "Keep tracing", and asked whether this is about hay not being a real item. It is
not: hay is a good (`Goods.Hay`), cut off the pasture, 67 a tile, carried to the barn, and shown on the barn's card. Every
death here had about 400–500 hay in the barn.*

**Method.** `ZzPose.cs` with `ZZ_DEATHS=1` keeps the last 48 ticks of the barn and the man seated at it, and prints the
whole ring at every head lost. Each line has his state, ticks left, cold, tile, arms, home, and his household's food and
firewood against their targets. `tools/harness/zzdeaths.py` turns each loss into one line: a cull, or a starving with the
run of his states over the ticks the herd wanted hay. It was run on seeds 12345, 2 and 42, with the newest and the
newest-housed herdsman pinned, and the prototype call off and on.

**Where he was while the herd wanted hay**, over every window that ended in a starving (209 ticks):

| State | Ticks | Share |
|---|---|---|
| `TravelingHome` | 92 | 44% |
| `FetchingFromStore` | 48 | 23% |
| `HaulingToStore` | 20 | 10% |
| `FetchingHay` + `FeedingTheHerd` (the herd's own work) | 29 | 14% |
| `Resting`, `Idle`, `TidyingGround`, `SeekingShelter` | 20 | 10% |

**What the reports show**, every death explained by one or more of these:

1. **An errand that outlasts the window.** The feed-ahead window is 20 ticks (`hay_feed_ahead_days` 5 × 4). A shopping
   trip and its walk home run 16–20 ticks, and a chore plus the walk home runs 22. Whatever he was doing when the window
   opened, he was still doing when winter began. **The household's ordinary shopping is not an emergency:** his larder
   stood at 47–86% of target and his firewood at 44–100%. It is the top-up errand (D372: it fires at half a larder and
   fetches back to target, trip after trip). It ranks above every job in `Decide` (`PlanFetch` comes first), and late
   autumn is firewood season, exactly when the window opens.
2. **Short walks take long.** On seed 2 a shop about 5 tiles from home is a 20-tick round trip, because the path loops
   round the building, probably to its door (D619–D625).
3. **The cold from those walks cuts the hay trip.** He sets out with cold already at 1,500–2,600, and the shelter line
   (3,000) takes him off the errand.
4. **The first day without hay kills, then one beast a day.** Twice the armful landed on the very tick a beast died
   (`out=80` at the loss), because the day's feeding is checked before he puts the hay down.

Two more were seen and are **not explained**: on seed 42 in year 18 nobody was seated at the barn for a whole window
although a herdsman was pinned; and §8's homeless herdsman, who came from the pose's own pick.

**Every fix tested, each a scratch change reverted at once** (no `src/` file differs from the commit):

| Change | Seed 12345 (housed) | Seed 12345 (newest) | Seed 2 | Seed 42 |
|---|---|---|---|---|
| None (today) | 13 | 11 | 4 of 4 | 4 of 4 |
| The herd calls him off rest, chores and the walk home (harness) | 8 | not run | 4 | 4 |
| He skips the ordinary shopping while the herd wants feeding | 13 | 6 | 4 | 4 |
| Both | 8 | 9 | 4 | 4–5 |
| A beast survives 2 hayless days and starves on the 3rd | 10 | 6 | 4 | 4–5 |

**None of them saves seeds 2 and 42**, where the first winter's herd is lost every time. Each closes one door and the next
cause walks through it: freed from a chore he goes shopping; kept from shopping he is still mid-walk; given a grace, he is
still not back by the third day.

⏸️ **Joe's call, from what was measured:**
- **Recommended: the herd eats from the barn's own hay when none is put out.** Every death above had hay in the barn,
  and this is the one change that needs nothing from the herdsman's day. The cost is legibility: the carry is the visible
  act. It can be kept honest by narration (*"The cattle at barn 1 ate from the barn — nobody carried hay out"*), and the
  herdsman still carries hay out whenever he can, as today. Cattle fed in their barn through winter is also the ordinary
  shape of the thing.
- **Or keep the carry and rebuild the herdsman's winter:** the herd outranks the household's ordinary shopping, a hungry
  herd calls him off any errand, and the day's feeding waits for the tick's hay. That is three changes, each measured
  here to be not enough alone.

The posed golden still waits on this.

## 10. The barn feeds a kept herd, and the pose is killing its villages (D647)

Joe, 2026-10-28: *"Barn feeds them"*. Built as `livestock.md §0.3`: on a winter day the herd eats what is put out first,
and with a herdsman seated at the barn it makes up the rest from the barn's own hay, narrated. With nobody seated, D593
stands. Measured with `ZzPose`, `ZZ_HERD=1 ZZ_DEATHS=1`, 50 years:

| Arm | Starved, D646 | Starved, D647 | Days the barn fed them |
|---|---|---|---|
| 12345, newest pinned | 11 | **0** | 13 |
| 12345, housed pinned | 13 | **0** | 24 |
| 2, newest pinned | 4 | **0** | 261 |
| 42, newest pinned | 4 | 5, every one with nobody seated | 9 |

**Seed 42's five are not the herd's.** A new `ZZX` line prints every death by year and cause and who is alive at the end,
and `people=` turns out to be `Villagers.Count`, **the dead included**:

| Seed | Deaths over 50 years | Alive at year 50 |
|---|---|---|
| 12345 | 2 founders of cold in year 1; 11 starved in years 40–41; 5 of old age | 24 of 42 |
| 2 | 10 of cold in years 1–4; 45 starved in years 21–43; 1 cold in year 37; 5 of old age | 9 of 70 |
| 42 | 4 founders of cold and 1 starved in year 1; then each nomad party frozen within a few years (5, 8, 6, 6, 6, 6, 6, 6, 6) | 0 of 60 |

**So, corrections to §9:**
- On seed 42 the herd's first winter, year 18, is the village's last: 8 of its people froze that year. No herdsman fix
  could have saved a herd with no herdsman alive. *The empty barn seat §9 left unexplained* is that: `seats=[]`, and `k=`
  empty because nobody living was left to pin. The run's 4–5 seconds, against 41 for seed 12345, said so in every run
  from D643 on, and nobody asked.
- On seed 2 the village was alive through the herd's winters, and the fix saves its herd. Its many barn narrations are
  the starving years, when its herdsman has other work.

**The chains on seed 12345 still all turn over** at year 50: 38,651 wheat, 3,780 flour, 3,580 bread, 38 tools forged, 142
crossings, 19 boats, the herd at 4 head with 51 born and 47 culled into 470 leather, and the quarry, mine and smithy
worked (`quarry:160 mine:135 smithy:75`).

⏸️ **Joe's call, from what was measured:**
- **Pin the golden on seed 12345 as it is.** A determinism golden pins *what the code does*, and this run reaches every
  Phase 5 chain. The cost: it pins a village that loses two founders to cold in year one, which a later fix to the
  village's first winter will move, as any golden moves with the code.
- **Or make the pose play like a player first.** Nine buildings at tick 0 on four founders is not how anyone plays
  (D643's trap). The likely cause, untraced: the posed workplaces take the founders off firewood before the first winter.
