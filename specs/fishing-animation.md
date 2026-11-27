# Fishing you can watch: the cast, the wait, the catch (A275)

> **Status: ✅ BUILT (A277, 2026-11-26), played and merged into `main` at Joe's word (A278, *"Played it, merge"*, `3b50939`); not pushed. His call: *"B, as recommended"*. Mocked A276.** Art lane (CLAUDE.md *Two tracks*),
> branch `art`; the art queue's piece 3, *Animations* (Joe: *"proceed with the art arm as outlined"*). View only: nothing in
> `Kith.Sim`, `data/` or `tests/` changes. Mocks: `D:\Projects\brushes\animations\` (`fish_A_dock.gif`, `fish_B_shuffled.gif`,
> `fish_C_boat.gif`, `fish_contact.jpg`).

## 1. What was asked, and what is already built

Joe's play note (b), 2026-11-20 (A136, HANDOFF's words): **fishing — the cast, the catch with a splash, several versions
shuffled; stand on the dock, or out in the boat.** His note (c), varied work for every trade (stone gathering named), is the
next part of this piece, after fishing.

Already in `main`, and kept: the fisher in their blue coat with a rod; one work motion, the rod's tip dipping and lifting
every 4.8 s (A130, `villager-animation.md §1`); the fishing hut's yard paints a dock and a moored boat (A86,
`VillageMap.Yards.cs`).

## 2. What the sim gives the view

A cast is a countdown: `fish_ticks` (10 ticks, about 13 s at 1×) set into the fisher's `ActionTicksRemaining`; when it runs out
the catch is counted and the next cast starts at once (`BehaviorSystem`, `case VillagerState.Fishing`). The view reads the
villager directly, so **the catch can be drawn at the moment it happens**, and the cast at the moment the countdown starts.
Nothing is invented: every cast in the sim catches, so every drawn cast ends in a catch.

⚠️ **Where the fisher stands today:** on the hut's own spot. In his Oakhaven the hut sits under the wood's canopy and the
fisher cannot be seen at all. For a cast to read, the fisher is drawn **at the end of the hut's dock** (or in its boat) while
in the `Fishing` state. The sim still says they are at the hut; the view only places them on its water side.

## 3. The options mocked

On the real dock (Oakhaven's fishing hut, the game's closest zoom; left 3×, right 1:1), with the mock painter that matches
`PersonPainter` (A129's `anim.py`), the rod turned about the hand. Three casts each; the wait is cut to 3 s in the mock.

- **A · on the dock:** the rod back over the shoulder, forward; the line flies out and the float lands with a ring; the wait,
  the float bobbing and the rod dipping as today; the catch — the float pulled under, the rod up, a fish out with a splash,
  swung in to the dock.
- ⭐ **B · A, each cast shuffled:** a small fish, a big one (the rod bends further, a bigger splash), or a thrown net that spreads
  on the water and is hauled in with fish in it. Chosen per cast from the person and the cast's start, so two fishers never
  repeat each other and a reload shows the same thing.
- **C · out in the boat:** the hut's own boat taken off its mooring a little way out, the fisher sitting in it and casting.
  It reads as fishing from further off, but it puts the fisher a couple of tiles from where the sim says they are, and the
  moored boat has to vanish while they are out.

## 4. Calls — answered: Joe, 2026-11-26, *"B, as recommended"* (A277)

B, the dock with the casts shuffled; the float bobbing with today's dip; (c) next, the quarrier first.

1. **Which:** ⭐ B, the dock with the casts shuffled; A, one cast every time; C, from the boat (or the boat as one more version in B's shuffle).
2. **The wait:** ⭐ the float bobbing and today's dip, as mocked; or the dip alone.
3. **Then (c):** ⭐ varied work trade by trade, the quarrier first (stone gathering, his own example), each mocked before it is built.

## 5. Wiring, when he answers — every file the art lane's

| File | Change |
|---|---|
| `VillageMap.PeopleMotion.cs` | the cast's beats from `ActionTicksRemaining`: cast, wait, catch; the shuffle from the villager's id and the cast's start tick |
| `PersonPainter.cs` | the rod's poses (back, forward, strike, bent, haul); the net |
| `VillageMap.People.cs` | the fisher drawn at the dock's end while fishing; the line, float, fish and splash |
| `VillageMap.Yards.cs` | the dock's end as a point the people code can ask for (C: the moored boat hidden while out) |

The probe gains a `fishing:` line: a posed fisher at the cast's start, mid-wait and at the catch draws those beats, at the dock's
end, and the shuffle is the same on a second ask.

## 6. As built (A277)

- **On the dock** (`VillageMap.Indoors.cs`, `AtTheDoor`): a fisher in the `Fishing` state is drawn at
  `DockStandLocal(water)` — the yard's own dock (`DockOut`, `DockAside`, `DockLength`, now named in `VillageMap.Yards.cs`), 0.15
  tiles in from its end — turned by the hut's facing. Clicking them and their walk onto the dock follow from that, as a door's
  step does. A hut with no water by it (no dock drawn) keeps them where they were. The huts are found by a table from each fishing
  hut's position, laid again only when the workplaces change (their count, the newest id) or, once a tick, when a fisher stands
  on no known hut (a hut moved).
- **The cast** (`VillageMap.Fishing.cs`): pure `CastAt` — the throw's first 1.4 s (back, forward, then waiting), the catch's last
  2 s (the float under, struck or bent, hauled), the wait between (the dip at the start of every 2.4 s). Its length is the sim's
  (`WorkTicksFor`: a tool and mastery shorten it), and how far in is the countdown read back, so the catch lands when the
  sim's does. ⭐ **Quick:** a cast under 5 real seconds (2× and up, or a fast fisher) waits and dips only, the catch a splash.
  `VersionOf` — half small fish, a quarter big, a quarter the net — by the person and the tick the cast began, so a reload shows
  the same cast. The float lands 0.8–1.3 tiles out, nearer if the water ends first.
- **The painter** (`PersonPainter.cs`): eight rod beats (`RodBack` … `RodHaul`, `NetThrow`), `fish.py`'s numbers; from
  `RodBack` on, the rod's own hanging line is left to the map; `NetThrow` lays the rod by. `RodTip(beat)` says where the tip is,
  so the map's line starts there. The map keeps every beat of a fisher's look while one is drawn.
- ⚠️ **An elder fishing holds the rod.** Since A93 an elder carries the stick at every trade; Oakhaven's only fisher is an elder,
  and with the stick nothing could be cast. Fishing only; every other trade keeps the stick.
- **The map** draws the rings, the splash and the net under the fisher, the line, the float and the fish over them, all through
  `AtNight`.
- **Probe `fishing:`** (`VillageMap.FishingProbe.cs`, printed with the people's lines): red-checked, 8 mutants, 7 red (the throw's back and forward swapped; a big fish not bending the rod; quick casts off; every cast small; the shuffle blind to the cast's start; standing past the dock's end; an elder fishing with the stick). The 8th, the net keeping the rod in hand, scored 0: the line does not look for the rod in the net's pose — kept, and the zero written down.
- Filmed on a copy of his Oakhaven (`D:\Projects\brushes\animations\built\`): the rod cast, the fish hauled in, the net
  thrown and hauled, the fisher walking off when the hut is full.
