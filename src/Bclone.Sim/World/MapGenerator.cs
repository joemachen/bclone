using Bclone.Sim.Config;
using Bclone.Sim.Determinism;

namespace Bclone.Sim.World;

/// <summary>
/// Generates the valley from the run's seed (D18).
/// </summary>
/// <remarks>
/// <para>
/// <b>Draw order is the contract.</b> Every value comes from the run's one
/// <see cref="DeterministicRandom"/>, in the order written here, and inserting a draw
/// in the middle shifts every subsequent value — silently invalidating every seed
/// anybody has written down and every golden test. It is the same hazard D5 names for
/// system execution order, and it deserves the same treatment: reordering is a
/// behavioural change, never a tidy-up.
/// </para>
/// <para>
/// <b>The generator is bounded rather than checked.</b> The economy is derived from how
/// far the worst-placed home is from its nearest forage site
/// (<see cref="VillageEconomy.RoundTripTicks"/>), so a generator free to put sites
/// anywhere would make the food economy a property of the seed. Instead it draws
/// <em>within</em> radii the economy already reads, so the distance budget holds by
/// construction — no reject-and-redraw loop, and no seed that is quietly unsurvivable.
/// That is the answer to the spec's §3, and it is what turns "is this valley fair?"
/// from a hope into a property the type system nearly enforces.
/// </para>
/// </remarks>
public static class MapGenerator
{
    /// <summary>Build the valley. Same seed and config ⇒ byte-identical map.</summary>
    public static GeneratedMap Generate(SimConfig config, DeterministicRandom rng)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(rng);

        int width = config.MapWidth;
        int height = config.MapHeight;
        int minX = config.MapMinX;
        int minY = config.MapMinY;

        var terrain = new Terrain[width * height];
        var soil = new byte[width * height];

        // ---- 1. The river ------------------------------------------
        CarveRiver(config, rng, terrain, width, height);

        // ---- 2. Forest stands --------------------------------------
        // Clusters, never scatter: a stand is a place you walk to, and JobKind.Logger
        // already assumes one. Scattered trees would be texture.
        var standCentres = new List<GridPos>();
        int stands = config.TreeStandCount;
        for (int i = 0; i < stands; i++)
        {
            GridPos centre = DrawRingPosition(
                rng, config.TreeStandRingTiles, config.SiteJitterTiles, i, stands);
            centre = ClampInside(centre, config);
            standCentres.Add(centre);
            PaintForest(terrain, centre, config.TreeStandRadiusTiles, width, height, minX, minY);
        }

        // ---- 3. Forage sites ---------------------------------------
        // Spread the way D24 requires — a ring at roughly settlement width, not a
        // cluster. That decision is a record of what happened when the extra sites all
        // went out to the map edges: every home near the middle competed for the one
        // original patch, so tightening catchment left central families idle beside a
        // full thicket and they starved.
        var forageSites = new List<GridPos>();
        int siteCount = config.ForageSiteCount;
        for (int i = 0; i < siteCount; i++)
        {
            GridPos site = DrawRingPosition(
                rng, config.ForageSiteRingTiles, config.SiteJitterTiles, i, siteCount);
            forageSites.Add(ClampInside(site, config));
        }

        // ---- 4. The founding site ----------------------------------
        // Where the first homes and the village's buildings go. Kept near the middle
        // of the ring of sites, because the economy's distance budget is derived from
        // a village that sits inside that ring rather than off to one side.
        GridPos founding = ClampInside(
            new GridPos(
                DrawJitter(rng, config.FoundingJitterTiles),
                DrawJitter(rng, config.FoundingJitterTiles)),
            config);

        // The village cannot be founded in the river. Nudging is deliberate rather
        // than redrawing: a redraw would consume a variable number of values and make
        // the draw count depend on the terrain, which is exactly the kind of hidden
        // coupling that makes a seed stop reproducing.
        founding = NudgeOutOfWater(terrain, founding, width, height, minX, minY);

        // ---- 5. Soil ------------------------------------------------
        // Generated, hashed, and read by nothing yet — it is here so that when §2.3's
        // soil depletion lands it does not have to change the DRAW ORDER, which would
        // invalidate every seed already written down.
        for (int i = 0; i < soil.Length; i++)
        {
            soil[i] = (byte)rng.NextInt(config.SoilQualityMin, config.SoilQualityMax + 1);
        }

        return new GeneratedMap(
            width, height, minX, minY, terrain, soil, forageSites, standCentres, founding);
    }

    /// <summary>
    /// A position on a ring around the origin, one slot per site, plus a little jitter.
    /// </summary>
    /// <remarks>
    /// Evenly spaced slots rather than free angles, because "spread" is a requirement
    /// (D24) and not an average. Drawing angles at random would sometimes put four
    /// sites in one quadrant, which is the layout that starved the village once
    /// already. Jitter makes each valley different; the slots make every valley
    /// habitable.
    /// </remarks>
    private static GridPos DrawRingPosition(
        DeterministicRandom rng, int radius, int jitter, int index, int count)
    {
        GridPos slot = RingSlot(index, radius);
        return new GridPos(slot.X + DrawJitter(rng, jitter), slot.Y + DrawJitter(rng, jitter));
    }

    /// <summary>
    /// Where the nth site sits before any jitter — the <b>canonical</b> valley.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Public and RNG-free on purpose. <see cref="VillageEconomy"/> derives the food
    /// economy from how far the worst-placed home is from its nearest site, and it has
    /// to be able to ask that question <em>without</em> generating a world — otherwise
    /// the economy becomes a property of the seed and two runs have different physics.
    /// So the economy budgets against this layout plus the worst jitter the generator
    /// may add, and every seed lands inside that budget by construction.
    /// </para>
    /// <para>
    /// Evenly spaced slots rather than free angles, because "spread" is a requirement
    /// (D24) and not an average. Drawing angles at random would sometimes put four
    /// sites in one quadrant, which is precisely the layout that starved the village
    /// once already — central homes idle beside a full thicket while the outskirts had
    /// nothing in reach.
    /// </para>
    /// </remarks>
    public static GridPos RingSlot(int index, int radius)
    {
        // Eight compass slots walked in order, so the arithmetic stays integer — a
        // trigonometric ring would put floats in worldgen, against D2.
        (int X, int Y)[] directions =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (1, 1), (-1, -1), (1, -1), (-1, 1),
        };

        (int X, int Y) direction = directions[index % directions.Length];

        // Diagonals are longer in Manhattan terms, so halve them — otherwise the
        // corner sites sit twice as far out as the cardinal ones and the ring is a
        // star.
        bool diagonal = direction.X != 0 && direction.Y != 0;
        int reach = diagonal ? (radius + 1) / 2 : radius;

        // Later rings step outward, so more sites than slots still spreads.
        int ringsOut = index / directions.Length;
        reach += ringsOut * radius / 2;

        return new GridPos(direction.X * reach, direction.Y * reach);
    }

    /// <summary>The canonical forage sites — the layout the economy budgets against.</summary>
    public static List<GridPos> CanonicalForageSites(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var sites = new List<GridPos>();
        for (int i = 0; i < config.ForageSiteCount; i++)
        {
            sites.Add(RingSlot(i, config.ForageSiteRingTiles));
        }

        return sites;
    }

    /// <summary>The canonical tree stands.</summary>
    public static List<GridPos> CanonicalTreeStands(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var stands = new List<GridPos>();
        for (int i = 0; i < config.TreeStandCount; i++)
        {
            stands.Add(RingSlot(i, config.TreeStandRingTiles));
        }

        return stands;
    }

    private static int DrawJitter(DeterministicRandom rng, int jitter) =>
        jitter <= 0 ? 0 : rng.NextInt(-jitter, jitter + 1);

    /// <summary>
    /// Cut a river along the valley's long axis, wandering as it goes.
    /// </summary>
    /// <remarks>
    /// Along rather than across, per D26: the valley is wide because §2.5 describes a
    /// river valley, and a river runs down one. It wanders by a step at a time so the
    /// shape is a watercourse rather than a canal.
    /// </remarks>
    private static void CarveRiver(
        SimConfig config, DeterministicRandom rng, Terrain[] terrain, int width, int height)
    {
        if (config.RiverWidthTiles <= 0)
        {
            return;
        }

        // Start somewhere in the middle band, so the river never hugs an edge and
        // cuts a thin strip of valley off from everything.
        int band = height / 4;
        int y = rng.NextInt(band, height - band);

        for (int x = 0; x < width; x++)
        {
            for (int w = 0; w < config.RiverWidthTiles; w++)
            {
                int row = y + w;
                if (row >= 0 && row < height)
                {
                    terrain[(row * width) + x] = Terrain.Water;
                }
            }

            // Wander: -1, 0 or +1 each column.
            y += rng.NextInt(-1, 2);
            y = Math.Clamp(y, 1, height - config.RiverWidthTiles - 1);
        }
    }

    private static void PaintForest(
        Terrain[] terrain, GridPos centre, int radius, int width, int height, int minX, int minY)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                {
                    continue;
                }

                int x = centre.X + dx - minX;
                int row = centre.Y + dy - minY;
                if (x < 0 || x >= width || row < 0 || row >= height)
                {
                    continue;
                }

                int index = (row * width) + x;

                // Trees do not grow in the river. Water wins, which also stops a stand
                // from quietly bridging it.
                if (terrain[index] != Terrain.Water)
                {
                    terrain[index] = Terrain.Forest;
                }
            }
        }
    }

    /// <summary>Walk outward until the tile is not water. Consumes no random draws.</summary>
    private static GridPos NudgeOutOfWater(
        Terrain[] terrain, GridPos from, int width, int height, int minX, int minY)
    {
        for (int radius = 0; radius < height; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    int x = from.X + dx - minX;
                    int row = from.Y + dy - minY;
                    if (x < 0 || x >= width || row < 0 || row >= height)
                    {
                        continue;
                    }

                    if (terrain[(row * width) + x] != Terrain.Water)
                    {
                        return new GridPos(from.X + dx, from.Y + dy);
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "The whole valley is under water; no village could be founded. Check river_width_tiles.");
    }

    private static GridPos ClampInside(GridPos position, SimConfig config) =>
        new(
            Math.Clamp(position.X, config.MapMinX + 1, config.MapMaxX - 1),
            Math.Clamp(position.Y, config.MapMinY + 1, config.MapMaxY - 1));
}
