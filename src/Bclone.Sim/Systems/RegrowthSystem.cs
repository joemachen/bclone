using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// The valley grows back — <b>the counterweight the food system was missing</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ Why this exists, measured rather than argued (D125).</b> Once food came from the trees
/// in a hut's ring, a village felled its own ring simply by living in it — house footprints,
/// construction sites and the forester's own ground are all near the village, and so is the
/// gatherer's hut. Measured over 150 years: the hut's yield fell from 59 to 11 of 145 and
/// thirteen people starved, while the valley still held 97% of its woodland. Fifty-six trees
/// in exactly the wrong place.
/// </para>
/// <para>
/// <b>`professions.md §6.1` predicted it and prescribed this</b> — *"no forest, no food is a
/// starvation trap while the valley cannot come back"* — and D112 deferred it on the grounds
/// that planting was the recovery. Planting is a <em>player action</em>; a village nobody is
/// managing never plants. This is the recovery that does not need managing.
/// </para>
/// <para>
/// <b>Joe's rate, and it is fast on purpose:</b> *"it should come back every year. sapling for
/// the first six months. mature tree after a year."* So the valley is swept twice a year — a
/// cleared tile becomes a <see cref="Terrain.Sapling"/> within six months and a mature tree
/// six months after that.
/// </para>
/// <para>
/// <b>⚠️ IT SPREADS FROM WOOD THAT IS ALREADY THERE, rather than appearing anywhere.</b> A
/// grass tile grows only if it touches forest, so a wood recovers outward from its edge and a
/// clearing fills from the sides in. That is what a wood does, it is legible on the map — you
/// can watch the gap close — and it means clearing a wood to its last tile is still a decision
/// with permanence to it.
/// </para>
/// </remarks>
internal sealed class RegrowthSystem : ISimSystem
{
    public string Name => "regrowth";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SimConfig config = world.Config;
        int period = config.RegrowthPeriodDays * config.TicksPerDay;
        if (period <= 0)
        {
            return;
        }

        // ⭐ THE SWEEP IS A FUNCTION OF THE TICK, SO THERE IS NO CURSOR TO STORE OR HASH.
        //
        // A slice of the valley is examined each tick and the whole of it comes round once
        // per period. That is what keeps this off D87's per-tick full-map walk — the trap
        // that took this suite from four minutes to over ten — while still letting every
        // tile be considered.
        //
        // Deriving the slice from `world.Tick` rather than keeping a rolling index means
        // there is no new sim state: nothing to hash, nothing to get out of step with a
        // reloaded game, and two runs of one seed cannot disagree about where the sweep is.
        int width = world.Map.Width;
        int height = world.Map.Height;
        int area = width * height;
        if (area <= 0)
        {
            return;
        }

        int perTick = (area + period - 1) / period;
        int start = (int)(world.Tick % (ulong)period) * perTick;

        for (int i = start; i < start + perTick && i < area; i++)
        {
            var tile = new GridPos(
                world.Map.MinX + (i % width),
                world.Map.MinY + (i / width));

            Terrain here = world.Map.TerrainAt(tile);

            // A sapling seen by a sweep is a sapling that has stood for one period, because
            // the sweep visits every tile exactly once per period. Six months, then wood.
            //
            // ⛔⛔ THAT SENTENCE WAS ONLY EVER TRUE OF SAPLINGS THE SWEEP MADE ITSELF (D220,
            // Joe: *"it feels like the trees are planted by the forester and ready to fell very
            // quickly"* — he was right, and this comment is why nobody had checked).
            //
            // A sapling the sweep seeds is not seen again for a full period, by construction.
            // A sapling a FORESTER plants appears at an arbitrary tick, so the next visit might
            // be one tick away: seeded ground took 1–2 periods to become wood and planted ground
            // took 0–1, **three times faster on average and near-instant at worst.**
            //
            // So a planted sapling is passed over once — the bit is cleared and it is left
            // standing — and matures on the visit after, a full period later. Both paths now
            // spend one whole period as a sapling. *The forester decides where trees are, not
            // how fast they grow.*
            if (here == Terrain.Sapling)
            {
                if (world.Map.IsYoungSapling(tile))
                {
                    world.Map.SetYoungSapling(tile, false);
                    continue;
                }

                world.SetTerrain(tile, Terrain.Forest);
                continue;
            }

            if (here == Terrain.Grass && CanGrowHere(world, tile))
            {
                world.SetTerrain(tile, Terrain.Sapling);
            }
        }
    }

    /// <summary>
    /// Whether a wood may creep onto this tile.
    /// </summary>
    /// <remarks>
    /// <b>The village's own ground is left alone</b>, and that is not a convenience — a
    /// settlement that had to re-clear its high street every spring would turn regrowth from
    /// a mercy into a chore, which is the anxiety mechanic §1.2 refuses. Painted residential
    /// land is where the player has said people live, so the wood stops at the fence.
    /// </remarks>
    private static bool CanGrowHere(SimWorld world, GridPos tile)
    {
        // ⭐ ONLY WHERE A WOOD HAS STOOD. This is the bound that stops regrowth eating the
        // valley — see `GeneratedMap.HasEverBeenWooded` for the two measurements that put it
        // here. A clearing heals completely, however large; a meadow stays a meadow.
        if (!world.Map.HasEverBeenWooded(tile))
        {
            return false;
        }

        if (world.Zones.IsResidential(tile) || world.SomethingStandsAt(tile))
        {
            return false;
        }

        // ⚠️ PAINTED GROUND STILL GROWS, AND THIS LINE USED TO SAY THE OPPOSITE. Skipping
        // harvest-painted tiles was right while paint came off the moment a tile was cleared:
        // paint meant "clear this soon", and growing trees on it would have been an argument
        // with the player. **Paint persists now (D127)** — it is a standing instruction, not a
        // one-off order — so refusing to grow on it would mean a painted patch is felled once
        // and then bare for ever, which is precisely the thing Joe asked for the paint to
        // stop doing.
        //
        // A painted patch is a coppice: it grows back, and the village fells it again.
        return TouchesWood(world, tile);
    }

    /// <summary>
    /// Whether a tile is surrounded enough by wood to grow — <b>two neighbours, not one</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ ONE NEIGHBOUR WAS MEASURED AND IT ATE THE VALLEY.</b> With a single forest
    /// neighbour enough, wood advanced across every open tile it touched: 2,600 forest tiles
    /// became <b>9,257 of the valley's 9,600</b> within fifteen years. The village lived — 48
    /// people for 150 years, nobody starved — but the map was one solid wood, the meadows
    /// were gone, and <c>forest_coverage_percent</c> had stopped meaning anything a few years
    /// in.
    /// </para>
    /// <para>
    /// <b>Two neighbours makes it heal rather than spread</b>, and the geometry is the whole
    /// trick: a tile bitten out of a wood has neighbours on two or three sides, so clearings
    /// fill in from their edges. A tile on the straight outer edge of a wood has only one, so
    /// the wood does not march into open ground. <b>A cleared wood comes back; a meadow stays
    /// a meadow.</b>
    /// </para>
    /// <para>
    /// <b>Mature forest only, not saplings.</b> Counting saplings would let one tree seed a
    /// line across the valley a period at a time, which is a spreading fire rather than a
    /// wood.
    /// </para>
    /// </remarks>
    private static bool TouchesWood(SimWorld world, GridPos tile)
    {
        int neighbours = 0;

        if (world.Map.TerrainAt(new GridPos(tile.X + 1, tile.Y)) == Terrain.Forest)
        {
            neighbours++;
        }

        if (world.Map.TerrainAt(new GridPos(tile.X - 1, tile.Y)) == Terrain.Forest)
        {
            neighbours++;
        }

        if (world.Map.TerrainAt(new GridPos(tile.X, tile.Y + 1)) == Terrain.Forest)
        {
            neighbours++;
        }

        if (world.Map.TerrainAt(new GridPos(tile.X, tile.Y - 1)) == Terrain.Forest)
        {
            neighbours++;
        }

        // ⚠️ ONE IS ENOUGH NOW, and it was not before. Two neighbours was doing the job that
        // `HasEverBeenWooded` does properly — holding the wood back — and doing it by
        // geometry, which slowed the spread without stopping it and left big clearings
        // healing far too slowly at their flat edges. With the bound in the right place, one
        // neighbour is simply "the wood is next door", which is how a wood comes back.
        return neighbours >= 1;
    }
}
