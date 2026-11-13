# Spec: The build queue panel — every site in the queue, at a glance, with ▲ and ▼

**Decisions:** D730 (this document). Neighbours: D104 (the queue is a real order), D105 (it is the
order things were marked, and the player can reorder it), D380 (a window, its Settings tick and its ✕),
D706 (no button keeps keyboard focus), D721 (a house's rebuild waits behind every other site).
**Status:** 🔨 **BUILT on `slice/build-queue-panel` (D730, 2026-11-12), unplayed, unmerged.** View only;
the probe's `build queue:` line is its test (§4).

---

## 1. Why this exists

Joe, 2026-11-11, on the art lane: *"add a 'build queue' panel that shows everything in the build queue
at a glance with increase/decrease priority buttons. this panel should be off by default in the
settings."*

A site's card already says where it stands (*"Being built — 3rd in the queue"*) and has ▲ Sooner and
▼ Later (D105). The whole order could only be read by opening every site's card in turn. The sim has
had the order as one list since D104: `SimWorld.BuildQueue()`.

## 2. The shape — `Main.BuildQueue.cs`

- **A window titled *Build queue*.** It is built by `Floating` like *Stock limits*, so it is in
  `_windows`. That gives it a Settings tick under *Windows*, a ✕ on its header, and a remembered
  shown/open/place in the settings file by its title. None of that is new code.
- **⭐ Off by default.** `Wanted = false` and hidden at build, exactly as *Stock limits* is (D380).
  The tick starts unticked. Once the player ticks it, the settings file remembers that, as for any
  window.
- **Its default place** is the left-hand column, after *Stock limits* (`_docked`, in build order).
- **A caption**, then one row per site in `BuildQueue()` order, the head of the queue at the top:

  | Column | What it says | From |
  |---|---|---|
  | `#` | `1`, `2`, `3` … — the same number the card says | the row's index + 1 = `QueuePositionOf` |
  | Site | the site's name; a flat button that opens its card and centres the map on it | `Workplace.Name` |
  | Materials | `12/40 logs · 0/5 stone`, every material, not only the logs | `Delivered(goods)` / `MaterialCost.Amount` |
  | Work | `0/60` | `WorkDone` / `Recipe.WorkTicks` |
  | ▲ | one place sooner | `MoveInBuildQueue(site, -1)` |
  | ▼ | one place later | `MoveInBuildQueue(site, +1)` |

  The materials cell is clipped to its column. Its tooltip is the sim's own sentence, *"Still wants
  5 stone."* (`DescribeWhatIsMissing`), or *"Everything is delivered."* — so a clipped cell never
  hides what is missing (the card's lesson, Joe's year-114 lodge).
- **▲ and ▼ are the card's ▲ Sooner and ▼ Later**, the same sim call, so one press moves a site
  exactly one place (D105). They are **disabled where the sim would refuse**: ▲ on the first row,
  ▼ on the last, both on a house's rebuild, and either one pointing at a rebuild (D721). A disabled
  button for a rebuild says why in its tooltip: *"A house's rebuild waits behind every other site."*
- **Empty:** *"Nothing is waiting to be built."*, and no header row.
- **It scrolls** once the rows are taller than `BuildQueueHeight` (260 logical px, about ten rows).
  Below that it is exactly as tall as its rows.
- **No button in it takes keyboard focus** (D706). A focused button fires on Space's release, which
  is what made the pause read as "held".

## 3. When it is rebuilt — not every frame (CLAUDE.md, Joe's 2026-09-10 rule)

The rows are refilled only **while the window is shown**, and only when the key they were built from
changes:

`(world, Tick, BuildingGeneration, _buildQueueAsked)`

- **`Tick`**: deliveries, work and a finished site all happen on a tick.
- **`BuildingGeneration`**: the sim's counter for a building or a site arriving or leaving. A site
  marked or cancelled while paused moves it.
- **`_buildQueueAsked`**: a counter in the view, bumped where the view changes a row between ticks.
  That is a ▲ or ▼ from this panel or from a card, and a rename. Those are the only callers of
  `MoveInBuildQueue` and `Rename(Workplace)`.

A window that was hidden while things changed has a stale key, so it refills the first frame it is
shown. Rows are pooled, not rebuilt: a row's controls are made once and refilled or hidden.

⛔ **No sim change, so no golden can move.** The order, the moves and the refusals are all
`SimWorld`'s already (D104, D105, D721).

## 4. How it is tested — the probe

The view has no unit tests (D11, D160); the probe is the verification. One line, **`build queue:`**,
and the `windows:` line learns the new window.

- **`windows:`** — *Build queue* starts hidden and unticked, beside *Stock limits*.
- **`build queue:`** —
  1. If fewer than three sites are queued, it marks forager's huts (`GathererHut`) near the founding
     until three are (cancelled at the end).
  2. Ticked in Settings, the window shows.
  3. Its rows are `BuildQueue()`, in order, each read cell by cell against the sim: place, name,
     materials, work.
  4. ▲ on the first row and ▼ on the last are disabled, and no button takes focus.
  5. ▼ on the first movable row moves that site one place, in the sim and in the panel, with no tick
     between.
  6. ▲ on it moves it back, and the order is the order it started in.
  7. A card's ▲ Sooner is seen by the panel with no tick between.
  8. Unticked, the window hides.

⛔ Every expected value is read from the sim, never from the panel it checks (D419's trap).
  9. Every piece of view state it moves is put back: the tick, the queue's order, and the selection (D730's
     trap: a card leaves its subject selected, and *What's here* follows the selection).

**Red-check (D326): 8 mutants, 8 red, each where it was aimed.** On by default (`windows:` and `build queue:`);
▼ wired to ▲; the panel's own move not bumping the key; a card's ▲ Sooner not bumping it; ▲ live on the top row; a
button that takes focus; the rows in the wrong order; only the first material said.
⚠️ **Unguarded, written down:** the rename bump. A site renamed while paused would show its old name until the
next tick, and no probe check sees it.
