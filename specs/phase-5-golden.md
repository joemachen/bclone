# Spec: A golden that reaches the Phase 5 chains (Phase 5's exit gate, item 2)

**Decision:** D642 (this document — the audit). Neighbours: D641 (Joe accepted the Phase 5 gate as written; item 2 is
*"a determinism golden that reaches the Phase 5 chains"*), D157 (Phase 2's item 2, the same question then: a golden
that reached `NearestHarvest`), D601 and D617 (no harness village raises a market's worth of trade or a smithy).
**Status:** 🔍 **AUDIT DONE (D642): no pinned golden reaches any Phase 5 production chain.** Housing, doors, maintenance
and nomads are reached. The smithy, the quarry, the mine, the mill, the bakery, the well, bridges, the trading post and
its boat, and the barn and its herd are not. 📝 **§5's posed-village golden: Joe's go-ahead (D642, *"Merge the audit
first"*), 📏 MEASURED (D643, §6), NOT BUILT:** every chain turns over in a posed 50-year run except the herd, whose
cattle starve with hay in the barn even with a pinned herdsman. That is Joe's to call before a golden is pinned over it. Owner: Joe + Claude Code.

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
