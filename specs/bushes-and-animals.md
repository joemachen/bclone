# Bushes and animals — the cattle, the woods' game, the berry patches (A298)

> **Status: ✅ BUILT (A298, 2026-11-26) on `art`, UNREVIEWED — at Joe's *"just power through all these, im going to approve them
> all"* (A295): the recommended option taken without a mock.** Art lane, branch `art`; the art queue's piece 4. View only: nothing in
> `Kith.Sim`, `data/` or `tests/` changes. Film: `D:\Projects\brushes\animals\animals_built.gif`.

## 1. What was asked

Joe's play note (g), 2026-11-20 (A136): **berry bushes / gathering resources and animals for hunting, animated** (`art-inventory.md`
§5, §7; B10 *which animals* still open). The queue's piece 4. ⚠️ Hunting has no animal in the sim (`hunting.md §9`): what is drawn
must be true to it — the woods' game is decoration that roams, and nothing drawn is ever hit.

## 2. What was there

- **The barn's cattle:** cream discs with an edge, drifting on the barn's pasture tiles (`DrawTheHerds`, `HerdSpots`).
- **The woods' game:** brown discs on one woodland tile in 24, roaming a Lissajous path on the sim's clock (`DrawTheWoods`, `Roam`).
- **The berry patches:** three bare berry discs on forest tiles, meshed with the woods' chunks (`BerryPatch`, D366).

## 3. As built (the recommended option)

- **The animals, side-on as the people stand** (`VillageMap.Beasts.cs`, `DrawTheBeast`): a cow cream with a brown patch; a deer
  tawny with a white rump, a hart's branched antlers on one in three. Each faces the way its own path goes (`FacingOf`, the path a
  tick on), steps its legs while it moves, and lowers its head to graze while it goes slower than 0.009 tiles a tick along its path
  (`Grazes`; about a sixth of a roaming deer's season). A soft shadow under each.
- ⛔ **Where each is, and how it wanders, is untouched** — `HerdSpots` and `Roam` are the systems lane's; the view asks the same
  path a tick on. **With the watercolour tick off, the discs, as before.** The two calls in `VillageMap.cs` (the systems lane's
  file) are the only lines there: the discs swapped for `DrawTheBeast` with the tick on — ⚠️ his review asked of that file.
- **The berry patch as a bush** (`VillageMap.Meshes.cs`, `BerryPatch`): three leafy clumps with a shade under them and six smaller
  berries on them; still one mesh a chunk on the terrain counter.
- **Probe `animals:`** (`VillageMap.BeastsProbe.cs`): facing either way and at a standstill; grazing either side of the line; a
  roaming deer's share of grazing; a bush of twelve discs.

## 4. Not in this piece

Species beyond cattle and deer (B10); a calf; cattle wintering in the barn; an animal card; the forage ring thinning as it is
picked (the patches are decoration on forest tiles, as before).

## 5. Checks (A298)

red-checked, 4 mutants, 3 red (facing flipped; grazing inverted; the bush's leaves gone). The 4th, the deer drawn as a disc with the tick on, scored 0: the probe does not draw (D340) — the renders show the animals; kept and the zero written down. **Four checks:** suite skipped — no sim, data or test file differs from `a65eff0` (W4's suite, 2363 / 0 / 5, D916); view build 0 warnings; probe 215 lines, 179 ✅, 0 ⛔, `animals:` ✅, bar height 110, tile centres ✅, new game ✅, `done.`; goldens identical.
