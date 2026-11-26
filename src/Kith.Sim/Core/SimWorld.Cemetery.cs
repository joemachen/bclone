using Kith.Sim.World;

namespace Kith.Sim.Core;

// ---------------------------------------------------------------
//  W4, the cemetery and the groundskeeper (`specs/cemetery.md`, D905) — step 1: who lies unburied
// ---------------------------------------------------------------

public sealed partial class SimWorld
{
    /// <summary>
    /// ⭐ <b>The dead who died before this tick are gone</b> — never buried, never waited for, held by nobody (Joe, D905, call 6 b:
    /// <i>"the dead from before do not get buried too. they are gone."</i>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 0 in a new village, so every death there is buried. <b>Written in every save from W4 on, 0 included</b>; a save without it
    /// is older, and on load it is set to that save's tick — the systems run on <see cref="Tick"/> and then it moves on
    /// (<c>SimLoop.StepOnce</c>), so every death already in the save is before it and every later one at it or after.
    /// </para>
    /// <para>Hashed only when it is not 0, so a new village hashes as it did before W4.</para>
    /// </remarks>
    public ulong BurialsFromTick { get; internal set; }

    /// <summary>The ids of the dead waiting for a grave, by the tick they died and then id. Derived: never saved, never hashed.</summary>
    /// <remarks>
    /// CLAUDE.md's rule — kept where the state changes (<see cref="ADeath"/> from <c>MortalitySystem.Kill</c>,
    /// <see cref="LayToRest"/> at a burial), rebuilt once on load (<see cref="RebuildTheUnburied"/>), never scanned per tick.
    /// </remarks>
    private readonly List<int> _unburied = new();

    /// <inheritdoc cref="_unburied"/>
    public IReadOnlyList<int> Unburied => _unburied;

    /// <summary>Dead since <see cref="BurialsFromTick"/> and not yet laid to rest. Somebody who left the valley never is.</summary>
    public bool AwaitsBurial(Villager villager) =>
        !villager.Alive && villager.DiedAtTick is ulong at && at >= BurialsFromTick && villager.BuriedAtTick is null;

    /// <summary>
    /// How many of the village have ever died — a leaver is not one. Derived: kept in <see cref="ADeath"/>, rebuilt on load,
    /// never saved or hashed. <b>Above nought, the cemetery is learned</b> (`cemetery.md §5.1`; D395, Joe's: the first death).
    /// </summary>
    public int DeathsEver { get; private set; }

    /// <summary>
    /// The small cemetery is the first death's gift (Joe, D906: <i>"first free"</i>): its materials fall away, the work stands,
    /// once. Saved, and hashed sparsely, because it changes what a building costs — the smithy's reason.
    /// </summary>
    public bool AFreeCemeteryIsOwed { get; internal set; }

    internal const string NobodyHasDiedYet = "Nobody has died here yet — the village has no need of a cemetery.";

    /// <summary>
    /// A death, from <c>MortalitySystem.Kill</c>: counted; the first raises the cemetery's one moment and owes the gift; and
    /// they join the end of the waiting list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ No <i>said it</i> flag for the moment (D313's shape): the count goes 0 → 1 once in a village's life and never again,
    /// and an older save with dead in it loads with the count above nought, so it never says it (call 10, D906).
    /// </para>
    /// <para>
    /// The end of the list is its place, by the tick and then the id, with nothing to sort: a death is always on the tick being
    /// stepped, and within a tick the mortality system walks <see cref="Villagers"/> in id order (D907 — a sorted insert scored
    /// zero against every guard, because it never moved anyone). <c>CemeteryTests.TheListEqualsAScanOverDecadesOfDeathsAndASaveMidway</c>
    /// is what fails if that ever stops being true.
    /// </para>
    /// </remarks>
    internal void ADeath(Villager villager)
    {
        DeathsEver++;
        if (DeathsEver == 1)
        {
            AFreeCemeteryIsOwed = true;
            RaiseMoment(
                "The village's first death",
                $"{villager.FullName} is dead at {villager.AgeYears} — the first this village has lost. They will need somewhere "
                + "to lie: a small cemetery is yours to place, and it will cost you nothing. "
                + $"{Clock.SeasonAndYear()}.");
        }

        if (AwaitsBurial(villager))
        {
            _unburied.Add(villager.Id);
        }
    }

    /// <summary>The cemetery row with the fewest graves (ties: the lower id) — the gift's. Null if the data has none.</summary>
    internal BuildingKind? TheFreeCemetery()
    {
        BuildingKind? free = null;
        int fewest = int.MaxValue;
        for (int id = 0; id < BuildingsCatalog.Count; id++)
        {
            if (BuildingsCatalog[id] is { Graves: > 0 } row && row.Graves < fewest)
            {
                (free, fewest) = ((BuildingKind)id, row.Graves);
            }
        }

        return free;
    }

    /// <summary>On load, after the villagers: the count of the dead, from what the save holds.</summary>
    private void RebuildTheDeathsEver() => DeathsEver = Villagers.Count(v => v.DiedAtTick is not null);

    /// <summary>A burial: the tick is set and they leave the list.</summary>
    internal void LayToRest(Villager villager, ulong tick)
    {
        villager.BuriedAtTick = tick;
        _unburied.Remove(villager.Id);
    }

    /// <summary>On load, after the villagers: the list again, from what the save holds.</summary>
    private void RebuildTheUnburied()
    {
        _unburied.Clear();
        foreach (Villager villager in Villagers
            .Where(AwaitsBurial)
            .OrderBy(v => v.DiedAtTick)
            .ThenBy(v => v.Id))
        {
            _unburied.Add(villager.Id);
        }
    }
}
