# Spec: Clothing — a coat is a roof you wear

**Decision:** D45's third clause, as Joe re-decided it in D659. **Slice:** `specs/environment-and-seasons.md §11`, slice 4;
Phase 5 gate item 1, second of Joe's four (D656: doors → **clothing** → logistics → house tiers).
**Status:** ✅ **BUILT AND MEASURED on `slice/clothing` (D659–D663, 2026-10-31), UNPLAYED, NOT MERGED.** C0 (this spec, the
baseline §5.3, the stale lines; D659), C1 (the good and the coat on the villager, §3.1–3.5; D660), C2 (the tailor's shop, the
batch, the quota and the unlock, §3.5–3.6; D661), C3 (the view; D662) and C4 (the real build measured against §7, §5.4; D663).
**Next: Joe plays it** (`.\run.bat`), then the merge at his word. Nothing of it is on `main`. *History: specced 2026-08-01 as "why clothing is not next"; its blocker fell at D291 with
nobody writing it down (found D533); re-measured D533; parked D534; unparked D656.*

---

## 1. Goal

A manufactured garment from leather — later wool or cotton — that a villager wears, and that makes the
open ground in winter **as kind as a roof**. That is the mechanic. The *point* is what the player reads:
fewer *"… is dangerously cold and is going in to get warm"* lines, and the few people who freeze on a
winter errand no longer do. Winter stays a rhythm the player learns (§0 of DESIGN.md), and a coat is one
more thing they prepare — the seasons spec's own rule: *a season with teeth is one the player prepares for.*

⚠️ **What it is not** (D533, and Joe's call in D659): it is *not* D45's *"winter as a working season"*.
Winter work is bounded by what the village wants (the stock limits), not by the cold; a coat does not
raise the work done. §5.2 measured that and §7 is written to it.

---

## 2. Which pillars this serves

- **§2.7 knowledge-based tech tree.** The tailor's shop is learned by doing — the village has handled
  enough leather to know what to do with it — and the reason you wanted it is a thing you *felt*: the
  warning line, every winter, about somebody you know.
- **§2.5 environment with teeth.** The cold keeps its teeth; clothing is preparation you can point at,
  the same shape as firewood.
- **§1.1 legibility.** A coat is a thing on a person, on their card, with a number of winters left in it.
  Nothing invisible multiplies anything.

---

## 3. The model — Joe's four calls (D659) and what they make

### 3.1 What a coat does: a roof you wear
`HearthSystem` adds cold by where a villager stands (`shelter-and-exposure.md §4.1`): outdoors
`exposure_days_outdoors` (15 days to the threshold), under a fireless roof `exposure_days_sheltered`
(25), at a fire it thaws. **A clothed villager outdoors chills at the sheltered rate** — the `Outdoors`
arm of the one `switch` in `ChillTheUnsheltered` reads the roof's rate when they wear a coat. The
`Roof` and `Fire` arms do not change: **a coat never helps under a fireless roof**, so a household out
of firewood still freezes, and fuel stays the winter's axis (D45, D53, D69).

⛔ **Not D45's literal *"removes the outdoor danger entirely"*** (open ground safer than an unheated
house), and ⛔ **not D69's third *"tattered furs"* rate.** Today's 15 days *is* the furs the founders
arrived in; the coat is the upgrade with a before-and-after D69 wanted, and the cold-start floor D122
tuned (`cold-start.md §7.3`) does not move. Joe, 2026-10-31: *"A roof you wear."*

### 3.2 Who wears one: every adult
Every living villager past childhood — working-age and elders, the carriers included, because walking
is outdoors (D53: *"walking is outdoors, always"*) and the market trip is a winter walk. **Children do
not count**: they stay by the hearth in winter, and a coat on a child protects nobody. Joe: *"Every
adult."*

### 3.3 The coat on the villager
`Villager.CoatDays` — the winter days left in the coat, **0 = no coat** — is `ToolUses`' exact shape
(`tools-and-the-smith.md §3.1`). Hashed sparsely under its own tag after `ServingSiteId`, so a village
with no coat anywhere hashes byte for byte as before (D291's rule). Saved as `coat_days`.

**Wear:** one day off per **winter day the coat is worn**, on the daily beat where the firewood burns —
whatever the day held. A coat lasts `coat_winters` winters (`coat_winters × days_per_season` days). At
zero the DEBUG log says *"Hattie's coat is worn through"*, she goes on at the bare rate, and she fetches
another when a store has one. ⚠️ Wear-by-day, not wear-by-outdoor-tick, on purpose: demand is then
*adults ÷ coat_winters* a year — steady, derivable, a livelihood (D22's errand trap, §7) — and a coat
in a drawer wears no faster than one on a back, which is the only way a player can count them.

**Fetching:** the tool's errand one good over. An adult without a coat, empty-handed, walks to the
nearest store holding clothing (`FetchingACoat`), takes one on arrival. Never the market, whose counter
is the household shop (D372). In any season: a coat taken in spring wears nothing until winter.

**The founders:** nothing. No coat in the cart (D69), the shipped game at tick 0 byte-identical.

### 3.4 The good: `Goods.Clothing`
A material (D107), warehouse-, cart- and pile-stored, never a granary. Stock limit **50** — Joe's own
number in D62 (*"… 50 tools, 50 clothes"*). `trade_worth` sized from leather's 3 and the leather a coat
takes. A new good gets its stock-limits row for free (`StockLimits.Kinds`); its colour is picked under
the three-band rule (D637) and checked by `tools/legibility/check.py`.

### 3.5 The tailor's shop and the tailor
`BuildingKind.TailorShop`, `JobKind.Tailor`, skill *tailoring*. D522's `Batch` shape, third of its kind
after the mill and the oven: **`leather_per_coat` leather → 1 clothing**, no firing, `sew_ticks` a
coat, `sews_per_stint` a stint, verb *sew*. `WhyTheBatchWaits` already says why a shop stands idle — the
limit met, or no store in reach with the leather — and the card reads it.

**Demand** is the smith's shape, not the mill's: `ClothingShortfall` = adults without a coat + a year's
wear (adults ÷ `coat_winters`) − clothing on the shelves; `TailorsWanted` = that ÷ the coats one tailor
sews a year at the worst walk, rounded up. Taken after smiths in the quota's order. The shortfall is
the panel's sentence (*"the village wants a tailor"*) and the chip's amber.

### 3.6 The unlock: fifty leather held
The smithy's shape (D395: *"a gift after 50 iron dug"*): `SimWorld.LeatherEverHeld` counts every hide
the village has ever taken in — the hunt and the herd's cull — and the shop reaches the build menu at
`tailor_unlock_leather` **50** (about five hunts). Hashed sparsely like `IronToolsEverForged`, saved,
a `tech-tree-map.md` node. Joe: *"50 leather held."* ⚠️ The Phase 5 golden's posed lodge holds leather,
so this counter **moves that golden** (`phase-5-golden.md §11`); C2 proves it moves for that alone.

### 3.7 Numbers, and where they come from
| Key | Value | Why |
|---|---|---|
| `coat_winters` | **3** | Joe's *"a few winters"* (D69) as a lifetime; demand of a third of the adults a year. ⏳ **C4 measures** the tailor's seat-years and the leather flow against it. |
| `leather_per_coat` | **5** | Half a hunt's hide (`leather_yield` 10). §5.3's leather flow is the bound it must sit under. ⏳ C4. |
| `sew_ticks` / `sews_per_stint` | **10 / 2** | A day's stint for two coats, between the oven (15 a bake) and the grind. ⏳ C4. |
| `tailor_unlock_leather` | **50** | Joe, D659. |
| limit `clothing` | **50** | Joe, D62. |

The numbers are typed so the suite can run; **§5.3's leather flow and C4's measurement are what settle
them**, and each is a dial in `data/sim.config.json` a modder can turn.

---

## 4. Inputs — one of three exists

| Needs | Comes from | Built? |
|---|---|---|
| Leather | hunting (D69's road), the herd's cull | ✅ `leather_yield` 10 a catch (D291), `leather_per_head` 10 (D592); limit 200; nothing spends it until the tailor |
| Wool | sheep, shearing | ❌ a second input arm on the `Batch` when sheep exist |
| Cotton / flax | farming | ❌ likewise |

---

## 5. The payoff, measured three times

Clothing's benefit could be measured before a line of it existed, because `exposure_days_outdoors`
already poses a coat. Each measurement is kept: the numbers below are what the real build is held
against (§7).

### 5.1 2026-08-01 — a no-op (D59)
Over 300 years on the village fixture, *everyone clothed* changed nothing: 0% of winter was spent
working outdoors, because the labour quota wanted nothing done (D52). A second probe (D59) found
**12.7 spare adults out of 14.7 through every winter** — the hands exist; the work did not — and that at
the seven-tile bound a working villager can never freeze (break-off at 50%, the walk home ≤ 7), so
clothing's payoff was labour, not lives. Its *"3× on winter labour"* assumed demand without end.

### 5.2 2026-10-08 — real but small (D533), `tools/harness/ZzClothing.cs`
30 shipped valleys (the played opening) and 13 every-source valleys (lodge, fishery and farm free at
t0), fifty years, three cold worlds: **bare** (15 / 25), **coat** (25 / 25 — the §3.1 model, posed),
**off** (`exposure_days_outdoors` 0, an upper bound — ⚠️ it switches roof cold off too, HANDOFF trap D533).

**At the shipped stock limits, nothing after the founding changes:**

| | at a fire | working outdoors | break-offs / valley | cold deaths, Year 1 | cold deaths, Years 2–50 |
|---|---|---|---|---|---|
| played: bare / coat / off | 71 / 71 / 72 % | 1.0 / 1.0 / 1.1 % | 61 / 16 / 0 | 16 / 16 / 0 | **0 / 0 / 0** |
| every: bare / coat / off | 71 / 76 / 70 % | 1.4 / 1.2 / 1.9 % | 50 / 20 / 0 | 28 / 13 / 0 | **0 / 0 / 0** |

Every cold death is a founding party in Year 1 — under a fireless roof (no coat helps) or in D515's
fixture race. **No hunter is hired in any winter** in 37 of 39 every-source runs (traced in §5.3).

**When the player asks for more** (every limit but tools ×10 at Year 5):

| | working outdoors | break-offs / valley | cold deaths, Year 3+ (outdoors / roof) | alive at 50 |
|---|---|---|---|---|
| played: bare / coat / off | 6.2 / 5.9 / 6.0 % | 73 / 26 / 0 | 7+7 / 2+0 / 0 | 158 / 171 / 179 |
| every: bare / coat / off | 8.2 / 8.2 / 8.7 % | 56 / 28 / 0 | 5+14 / 2+18 / 0 | 98 / 164 / 221 |

**A coat removes about two-thirds of the break-offs and nearly all the outdoor freezes, and does not
raise the work done** (6.2 → 5.9 %, 8.2 → 8.2 %): the limit bounds the work, not the cold. Roof deaths
(a household out of firewood) are untouched — that is fuel. ⚠️ Limits ×10 *from day one* kill every
every-source founding in Year 1: the harness playing badly (D447), not a finding.

### 5.3 2026-10-31 — the baseline this build is held against (D659), and the leather flow
Re-taken on `main` at a9c6fde before C1, bare and coat (posed), limits ×10 at Year 5, with two new
lines: **`ZZL`**, leather produced a valley (every rise in the valley's total leather — stores, buffers
and arms), and **`ZZW`**, a trace on the first day of every winter of the lodges standing, the hunters
hired, the meat held against its limit and whether the village is short of food.

**The cold, bare against the posed coat** (per valley, fifty years; shares are of working-age winter ticks):

| | alive at 50 | starved | frozen | work outdoors | seeking warmth | at a fire | break-offs |
|---|---|---|---|---|---|---|---|
| played: bare / coat | 12.3 / 13.8 | 16.0 / 16.4 | 8.6 / 6.6 | 7.7 / 7.6 % | 5.5 / 3.0 % | 59 / 60 % | **229 / 158** |
| every: bare / coat | 30.8 / 31.4 | 0 / 0 | 16.0 / 14.9 | 10.9 / 10.5 % | 9.3 / 4.9 % | 43 / 47 % | **473 / 327** |

| cold deaths, all valleys | Year 1 | Year 3+, outdoors | Year 3+, under a roof |
|---|---|---|---|
| played: bare / coat | 16 / 16 | **41 / 2** | 202 / 180 |
| every: bare / coat | 52 / 52 | **86 / 63** | 70 / 78 |

- **A coat removes about a third of the break-offs**, not D533's two-thirds: the village is bigger now (nomads,
  D565; trade and the herd, D590), its laborers dig and haul further, and a coat at the roof's rate still breaks
  a long stint off at 50 ticks where bare broke it at 30. The work done does not move (7.7 → 7.6 %), as D533 said.
- **Outdoor freezes after the founding nearly vanish in the played opening** (41 → 2) and drop a quarter in the
  every-source one, whose villages are twice the size and walk twice as far. **Roof deaths are untouched** — a
  household out of firewood (202 of the played arm's 243 cold deaths). Fuel, not clothing.
- **The starving is not clothing's**: the played arm loses 16 a valley to hunger in both worlds — D143's
  unattended village, found again in D648. The Year-1 deaths are the founding parties (D533).
- **Leather (`ZZL`):** the unattended played opening **never builds a lodge** in fifty years, so no played valley
  makes a hide; the every-source arm's one lodge makes **≈ 51 leather a year** (2,535 a valley; 0 to 6,485) and
  sits at its ×10 limit of 2,000 by the end. Five hunts a year. At `leather_per_coat` 5 that is ten coats a year a
  lodge, against a year's wear of ten for thirty adults at `coat_winters` 3: **one lodge dresses a village of
  thirty, and no more** — which is the bound §3.7's numbers were typed to, and the thing C4 re-reads.
- **The winter lodge (`ZZW`), D533's untraced question, traced:** of 650 lodge-winters in the every arm, a hunter
  was hired in **151 (23 %)**. In **387** the meat limit was met on winter's first day; in the other 138 with no
  hunter, meat stood within one hunt of it (24,938 of 25,000) — `StoppedByItsOwnLimit` leaving no room for a
  hunt's 900. Never once was the village short of food. **So the lodge is idle in winter because the meat limit is
  met, which is the stock limit doing what D62 asked**; the comment *"a lodge is what winter is for"* is true
  exactly when meat is wanted. No code changed; raising the meat limit is the player's lever, and whether the
  quota should keep one hunter at the lodge for leather's sake is **Joe's call** (§6).

### 5.4 2026-10-31 — the real build, held against §7 (D663)

`ZzClothing` on the C0–C3 tree, the 13 every-source valleys (the only opening the unattended harness builds a lodge in — the
played one never does, §5.3, so it cannot differ), fifty years, limits ×10 at Year 5. **Bare** is the shipped game with the
shop never learned (`tailor_unlock_leather` out of reach); **coat** is the shipped game, the harness marking a tailor's shop
near the founding site — once the tick it is learned (Year 1, five hunts in), once held to Year 5; and a diagnostic arm,
**coats that never wear**, with the shop marked in Year 1.

| | alive at 50 | frozen | break-offs | freezes Y3+, outdoors / roof | froze *seeking shelter* | coats sewn | adults coated | tailor seat-years |
|---|---|---|---|---|---|---|---|---|
| bare (no shop ever) | 30.8 | 16.0 | 473 | 86 / 70 | 0 | — | — | — |
| coat, shop marked in Year 1 | **26.2** | **20.1** | 330 (−30 %) | **122 / 87** | **34** | 121 | 55 % | 3.4 |
| coat, shop held to Year 5 | **30.8** | **15.0** | 391 (−17 %) | **76 / 67** | 1 | 113 | 48 % | 2.8 |
| coats never wear, shop Year 1 | 26.6 | 20.3 | 299 (−37 %) | 106 / 106 | — | 29 | 74 % | 0.6 |

- **The bare arm is the C0 baseline to the number** (30.8 / 16.0 / 473 / 86 / 70): a village that never learns the shop plays
  as the shipped game did — §7 item 3, and the goldens say the same.
- **Marked in Year 1 the coat world ends smaller and colder, and that is the harness, not the coat.** A stone-costing site
  marked by four founders before any rock is painted (Year 3) is D515's free farm and D648's finished farm again (D447: *a
  harness is not a player*); with coats that never wear and the same Year-1 site the deaths are the same (26.6 / 20.3), and
  with the shop held to Year 5 they are gone. How a Year-1 site costs lives in years 21–50 is **not traced**; the shape is.
- **Held to Year 5 — the player's timing — the coat is what D659 asked for:** 17 % fewer cold break-offs (the posed coat's
  third needed every adult coated; here half are), outdoor freezes 86 → 76, roof deaths 70 → 67 (fuel, as expected), one
  fewer frozen a valley, no fewer alive. The 34 *froze seeking shelter* of the Year-1 arm are 1.
- **Leather:** the coat arms spent ≈ 590 leather a valley — 118 coats at 5 each, to the recipe; one lodge's 50 a year covers
  it, so `leather_per_coat` 5 stands (§3.7).
- ⚠️ **The tailor is hired about 7 % of the time** (2.8 seat-years in ~40 shop-years), sews about 3 coats a year against a
  want of ~8, and half the adults wear one at any moment; the shelves stay near empty (1.3). The demand is continuous — not
  D22's errand — but at ×10 limits every trade wants hands and tailors are taken after smiths. ⏸️ Whether coats outrank
  stone and iron in the quota is **Joe's call** (§7 item 4).

---

## 6. What makes clothing matter — unchanged, and no longer a reason to wait

**Winter needs work worth doing before a coat changes much.** Herding is both clothing's input and
clothing's reason (D44, D39) — and the herd exists now (D590–D608), the lodge exists (D291), the
laborers dig and haul to the limits the player sets (D62/D63). What §5.2 found is that at the *shipped*
limits the village wants little in winter; when the player raises them, the cold bites and a coat
answers it. That is enough for Joe (D656) — the slice is a small, legible unlock, and the day winter has
more work it is already worth more. The winter lodge the quota never staffs (§5.3) is the nearest lever
and is **his call, not this slice's**.

---

## 7. Acceptance — the bar, and the failure modes

**The bar (Joe, D659: *"fewer break-offs"*)**, measured in C4 on `ZzClothing` with the real build —
*bare* = the tailor never unlocked, *coat* = the shipped game — with limits ×10 at Year 5, 43 valleys:
1. **Fewer cold break-offs** in the coat world than the bare one (§5.2's pose: about two-thirds fewer) — ✅ **D663:
   473 → 391 a valley (−17 %) with the shop at Year 5; −30 % marked at once (§5.4).**
2. **Fewer outdoor freezes** after the founding, and **no fewer alive at fifty** — ✅ **D663: 86 → 76 and 30.8 → 30.8 with
   the shop at Year 5.** ⚠️ Marked in Year 1 by four founders it is 122 and 26.2 — the harness's play (§5.4), not the coat.
3. **A village that never unlocks the tailor plays byte for byte as today** — every golden but the Phase
   5 golden holds, and that one moves for `LeatherEverHeld` alone (proved, §3.6) — ✅ **D661, D663: the bare arm is the
   baseline to the number**; the Phase 5 golden moved for three named causes (the counter, the unlock's moment, a neighbour's
   stock slot), proved.
4. **The tailor is a livelihood, not an errand**: seat-years per valley and coats sewn a year reported,
   against a demand of adults ÷ `coat_winters` — ⚠️ **D663: continuous demand (~8 a year), ~3 sewn, 2.8 seat-years in ~40
   shop-years.** Hired too little rather than wanted in bursts; the quota's rank is Joe's (§5.4).

⛔ ~~*A clothed village must do measurably more winter work than an unclothed one.*~~ The 2026-08-01 bar
is struck, not met: D533 showed the work is bounded by the limits, so it cannot be met by construction.

**Failure modes:**
- **Clothing becoming mandatory rather than an upgrade.** Every valley must stay survivable without it
  — D45's condition, true today (zero cold deaths after Year 1 at the shipped limits, §5.2).
- **A tailor who is an errand** (D22). Wear-by-day makes demand a steady fraction of the adults (§3.3);
  item 4 above checks it.
- **A coat that hides the cold.** The card says *"Coat — 2 winters left"* or *"No coat — the cold bites
  outdoors"*, and the going-in-to-get-warm line is unchanged for whoever still crosses it.
- **Two hard things at once** (D42). Wool and sheep are not this slice; neither is the winter lodge.

---

## 8. Definition of Done

1. This spec current, with §5.3 and C4's numbers in it.
2. **C1** — the good and the coat: `Goods.Clothing`, `CoatDays` (hashed sparsely, saved; format 9), the
   `Outdoors` arm, wear by winter day, the fetch; guards for each; **no golden moves**.
3. **C2** — the tailor: shop, trade, skill, `Batch` arm, every Miller/Baker switch gains Tailor,
   `ClothingShortfall` / `TailorsWanted`, the allocator's order, `LeatherEverHeld` and the unlock; guards;
   the Phase 5 golden re-pinned and proved to move for the counter alone.
4. **C3** — the view: the shop's learned button, glyph and colour under the three-band rule, the card's
   coat line, the tailor's card sentence, the stock-limits row; probe lines; bar height 151.
5. **C4** — §7's four items measured and written here; `DESIGN.md §4`, §6, §7 and `HANDOFF.md` true;
   `professions.md`, `save-load.md §9`, `content-inventory.md` rows.
6. The four checks green on every commit; every new guard red-checked by mutant with the reds counted and
   any zero written down; the suite's clock noted.
7. Joe plays it: the shop appearing after his fifth hide, somebody fetching a coat, the card, a quieter
   winter log. Merged at his word.
