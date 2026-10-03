# Spec: The tech-tree map — what the village may yet learn, and what it takes

**Decisions:** D440 (Joe's calls, 2026-09-30: the first "doing unlocks a building" moment
introduces a map of what can be unlocked and what it requires, with fog over the later nodes; two
screens; the first version is what exists plus a fogged horizon; library and town hall on it as
nodes; a *Tree* button on the control bar). Neighbours: D176 / `tech-tree.md §8` (the knowledge
screen — *who knows what* — stays the town hall's tab: **amended, not reversed**), §2.7 (unlocks are
diegetic: by people, by doing, by scale — never research points), D232 / D252 (the gifts and their
moment), D434 / D442 (the quarry's unlock and its stop), `tools-and-the-smith.md §9.1` (the smithy's).
**Status:** ✅ **BUILT (D443, 2026-09-30), played and merged with `slice/quarry` (D445, D447).** The
smithy's node and its gate (D444) read the one key, `smithy_unlock_iron`; **the iron mine's node is
real since D449** (`iron_tools_forged` against `mine_unlock_iron_tools`, `specs/iron-mine.md §3.4`). Owner: Joe + Claude Code.

---

## 1. Why this exists

Joe, at Year 35 of `slice/quarry`, having missed the quarry's unlock in the log: *"whichever
'behavior that unlocks a building' happens first (except for the library or the town hall) should
be an announce in a similar style as the gifts, but be the intro to the technology tree — which
will be a map of what can be unlocked and what is required to unlock it with some 'fog of war' over
the later tech nodes."*

The game already unlocks four things by what the village does — the quarry (stone dug by hand),
the smithy (iron dug, `tools-and-the-smith.md §9.1`), the library (fifteen years of a kept granary),
the town hall (the last founder's death) — and **none of them is visible before it happens.** A
player cannot plan toward a thing they cannot see, which is §0.1's *challenge in the planning*
with nothing to plan against.

## 2. Pillars, and what it must not become

- **§2.7 — diegetic, not a menu.** Every node states what the village must **do** (*"dig 100 stone
  by hand — 47 so far"*), never a cost in points. Nothing on the map is clicked to research; it is
  read. ⛔ **No node is unlocked from the map.**
- **§1.1 — legible.** Progress is live and exact, read from the same state the sim gates on, so the
  map and `Mark` cannot disagree.
- **§0.1 — challenge in the planning.** The fog keeps the later tree a discovery, and the visible
  edge of it something to plan toward.
- **D176 amended, not reversed:** `tech-tree.md §8`'s roster (techniques, who knows them, at-risk
  lines) stays the town hall's tab. The map is *what may be unlocked*; the roster is *who knows*.
- ⛔ **Nothing hashed but one flag**, and nothing derived per frame: the map's state is a reader over
  existing state, rebuilt when the panel is opened or a node changes state.

## 3. The rules

### 3.1 Nodes are rows

`tech_nodes` — `TechTree.DefaultNodes()` unless a config file gives its own list (the buildings
catalogue's shape), so a modder adds a node in data:

| Column | What |
|---|---|
| `id` | a stable string (`"quarry"`) |
| `name` | as the player reads it (*"The quarry"*) |
| `unlocks` | a building kind, or none (a horizon node) |
| `condition` | `StoneDug` · `IronDug` · `KeptGranaryYears` · `FoundersGone` · `NotYet` (horizon) — by name, the loader's global enum converter |
| *(no amount column)* | the condition's number is read from the key the sim gates on (`quarry_unlock_stone`, `smithy_unlock_iron`, `literacy_years`) — never a second copy, so the map and `Mark` cannot disagree |
| `requires` | ids of the nodes it hangs from — the map's edges |
| `says` | one sentence: what it takes, in the village's words |

### 3.2 Three states, and the fog

`TechTree.StateOf(world, node)`, a pure reader:

- **Known** — the condition is met (the building is in the menu).
- **In sight** — a node it `requires` is known, **or** its own progress is above zero. Name,
  sentence and progress shown.
- **Fogged** — otherwise. A silhouette in place, no name, *"Not yet seen."*

A horizon node (`not_yet`) can be in sight, never known: *"Nobody in this valley knows how yet."*

### 3.3 Progress is the sim's own number

| Condition | Progress | Of |
|---|---|---|
| `stone_dug` | `StoneEverDug` | `quarry_unlock_stone` |
| `iron_dug` | `IronEverDug` | `smithy_unlock_iron` |
| `kept_granary_years` | years since `FirstGranaryTick` (0 with no granary) | `literacy_years` |
| `founders_gone` | founders dead | founders |
| `iron_tools_forged` (D449) | `IronToolsEverForged` | `mine_unlock_iron_tools` |

### 3.4 The first version's nodes (Joe: what exists, plus a fogged horizon)

```
  stone dug ──► Quarry ──► Mason's yard ··· ──► Stone cottage ···
  iron dug ───► Smithy ──► Iron mine (an iron tool forged, D449)
  a kept granary ► Library ──► School ···
  the founders gone ► Town hall
```

The `···` nodes are horizon nodes (`tech-tree.md §9`): in sight once their parent is known, never
known in this version.

### 3.5 The introduction

`SimWorld.ShownTheTechTree` is hashed sparsely. The first crossing of a `stone_dug` or `iron_dug`
node (or, since D449, `iron_tools_forged` — never `kept_granary_years` or `founders_gone` — Joe) sets it, and that moment's body ends:
*"…It is the first thing this village has learned by doing — the Tree shows what else it may, and
what each will take."* Every later unlock is its moment without the line.

### 3.6 Where it lives

- **A *Tree* button on the control bar, beside Settings,** shown once `ShownTheTechTree` is set.
  Before then the bar is unchanged.
- **The moment's panel gets a second button, *See the tree*,** on the introducing moment only.
- **The panel is a card skin like the others:** draggable and foldable. Nodes are drawn as small
  cards in columns by depth, with the `requires` edges as lines between them. A known node is in the
  colour of the building it unlocks; one in sight is plain with a progress bar; a fogged one is a
  greyed silhouette.

## 4. Data

`tech_nodes` as §3.1, with the seven rows of §3.4. Validated at load: ids unique, every `requires`
names a node, no cycles, and a node unlocks a building exactly when it is not on the horizon.

## 5. Failure modes designed against

- **A menu by another name:** nothing is clicked to unlock; there are no points and no cost.
- **The map and the sim disagreeing:** progress and *known* are read from the gates' own state
  (`WhyNotYet`, `HasLiteracy`, `SaidTheFoundersAreGone`). A guard compares a node's *known* with
  `IsUnlocked` for every building node.
- **A per-frame rebuild:** the panel is built when opened, and refreshed once a season while open.
- **A new bar button moving the bar:** the probe poses the bar with *Tree* showing; the height stays
  161.
- **A label trimmed past an ellipsis** (trap 138): the probe poses the panel at its fullest and
  fails on a trimming label.

## 6. How it is tested

- `TechTreeTests`:
  - every building node's *known* equals `IsUnlocked`, across a village's life;
  - the fog rule's three states, posed;
  - progress equals the gate's own number;
  - the introduction fires on the first doing-unlock only, never on the library or the town hall;
  - validation refuses a cycle, an unknown id, and a mismatched amount.
- **Probe:** `tech tree:` poses the panel at full width and fails on trimming; the bar height stays
  161 with the button shown.
- Red-checked, and the reds counted.

**Built, and red-checked (D443):** `TheQuarrysNodeReadsTheQuarrysGate` (red 1 with *known* at
`>` not `>=`, which also reddens the fog guard — 2), `FogLiftsANodeAtATime` (1 with the parent rule
off), `TheFirstThingLearnedByDoingIntroducesTheTree` (1 with the flag inverted),
`BeingShownTheTreeIsInTheFingerprint` (1 unhashed), `ABrokenTreeIsRefusedAtLoad` — ⚠️ with cycle
detection off the test host **dies of a stack overflow** rather than failing by name: red, loudly,
written down rather than counted. Probe `tech tree:` red 1 with the cards 128 tall (*"a card needs
176×154"* — the first build's real overflow, which is why they are 160) and 1 with labels allowed to
trim (*"24 label(s) may trim"*). The bar probe poses the *Tree* button: 161 holds.

## 7. Definition of Done

1. This spec current.
2. Guards green and red-checked.
3. The view builds clean; the probe is green.
4. Joe plays: a new game, about 8 seam tiles dug → the game slows to 1× and the moment introduces
   the tree → *See the tree* opens the map: the quarry known, the smithy in sight at n/50 iron, the
   library at n/15 years, the town hall, and fog beyond.
5. DESIGN §6/§7, `tech-tree.md §8` (the amendment noted), handoff.
