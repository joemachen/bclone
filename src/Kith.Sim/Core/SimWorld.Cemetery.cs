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
    /// CLAUDE.md's rule — kept where the state changes (<see cref="JoinTheUnburied"/> from <c>MortalitySystem.Kill</c>,
    /// <see cref="LayToRest"/> at a burial), rebuilt once on load (<see cref="RebuildTheUnburied"/>), never scanned per tick.
    /// </remarks>
    private readonly List<int> _unburied = new();

    /// <inheritdoc cref="_unburied"/>
    public IReadOnlyList<int> Unburied => _unburied;

    /// <summary>Dead since <see cref="BurialsFromTick"/> and not yet laid to rest. Somebody who left the valley never is.</summary>
    public bool AwaitsBurial(Villager villager) =>
        !villager.Alive && villager.DiedAtTick is ulong at && at >= BurialsFromTick && villager.BuriedAtTick is null;

    /// <summary>A death, from <c>MortalitySystem.Kill</c>: onto the end of the list.</summary>
    /// <remarks>
    /// The end is its place, by the tick and then the id, with nothing to sort: a death is always on the tick being stepped,
    /// and within a tick the mortality system walks <see cref="Villagers"/> in id order (D907 — a sorted insert scored zero
    /// against every guard, because it never moved anyone). <c>CemeteryTests.TheListEqualsAScanOverDecadesOfDeathsAndASaveMidway</c>
    /// is what fails if that ever stops being true.
    /// </remarks>
    internal void JoinTheUnburied(Villager villager)
    {
        if (AwaitsBurial(villager))
        {
            _unburied.Add(villager.Id);
        }
    }

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
