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
        // Clusters, never scatter: a stand is a place you walk to, and JobKind.Forester
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

        // Berries do not grow in the river. Nudged out rather than redrawn, and that
        // is deliberate: a redraw would consume a variable number of random values,
        // making the draw COUNT depend on the terrain — which is exactly the hidden
        // coupling that stops a seed reproducing its world.
        for (int i = 0; i < forageSites.Count; i++)
        {
            forageSites[i] = NudgeOutOfWater(terrain, forageSites[i], width, height, minX, minY);
        }

        for (int i = 0; i < standCentres.Count; i++)
        {
            standCentres[i] = NudgeOutOfWater(terrain, standCentres[i], width, height, minX, minY);
        }

        // ---- 4. The founding site ----------------------------------
        // Where the first homes and the village's buildings go. Kept near the middle
        // of the ring of sites, because the economy's distance budget is derived from
        // a village that sits inside that ring rather than off to one side.
        GridPos wanted = ClampInside(
            new GridPos(
                DrawJitter(rng, config.FoundingJitterTiles),
                DrawJitter(rng, config.FoundingJitterTiles)),
            config);

        // AND ON THE SAME SIDE OF THE RIVER AS ITS WORK.
        //
        // Not being IN the water is not enough, which is what the first version
        // assumed. Water is impassable (D40), so a settlement founded on the far bank
        // from every berry patch is a village that starves in its first year without
        // anybody having made a decision — measured, on seed 1, where the river runs
        // straight through where the village wanted to be: peak population zero.
        //
        // Until bridges exist the generator owes the village a valley it can live in
        // (spec §6), so the founding site moves to the reachable side rather than the
        // map being redrawn. Costs no random draws, so the seed contract is untouched.
        GridPos founding = ChooseFoundingSite(
            terrain, wanted, forageSites, standCentres, width, height, minX, minY);

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

    /// <summary>
    /// A spot to found the village that can actually reach its work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Walkable tiles are labelled into connected components — one sweep — and the
    /// village goes into the component holding the most forage sites, as near to where
    /// it wanted to be as that allows. A village with berries it cannot walk to is not
    /// a hard start, it is an unplayable one, and non-negotiable 1 says a death has to
    /// be traceable to a decision. Nobody decided the river was there.
    /// </para>
    /// <para>
    /// Ties go to the tile nearest the wanted spot, then to the lower y and then the
    /// lower x — a total order, because "whichever the scan found first" would make the
    /// whole world depend on iteration order.
    /// </para>
    /// </remarks>
    private static GridPos ChooseFoundingSite(
        Terrain[] terrain,
        GridPos wanted,
        List<GridPos> forageSites,
        List<GridPos> stands,
        int width,
        int height,
        int minX,
        int minY)
    {
        int[] component = LabelComponents(terrain, width, height);

        // How much work each land mass can reach.
        var sitesPerComponent = new Dictionary<int, int>();
        var standsPerComponent = new Dictionary<int, int>();

        foreach (GridPos site in forageSites)
        {
            int label = ComponentAt(component, site, width, height, minX, minY);
            if (label >= 0)
            {
                sitesPerComponent[label] = sitesPerComponent.GetValueOrDefault(label) + 1;
            }
        }

        foreach (GridPos stand in stands)
        {
            int label = ComponentAt(component, stand, width, height, minX, minY);
            if (label >= 0)
            {
                standsPerComponent[label] = standsPerComponent.GetValueOrDefault(label) + 1;
            }
        }

        GridPos best = wanted;
        int bestSites = -1;
        int bestStands = -1;
        int bestDistance = int.MaxValue;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                if (terrain[index] == Terrain.Water)
                {
                    continue;
                }

                int label = component[index];
                int sites = sitesPerComponent.GetValueOrDefault(label);
                int standCount = standsPerComponent.GetValueOrDefault(label);
                var here = new GridPos(x + minX, y + minY);
                int distance = here.ManhattanDistanceTo(wanted);

                bool better = sites > bestSites
                    || (sites == bestSites && standCount > bestStands)
                    || (sites == bestSites && standCount == bestStands && distance < bestDistance);

                if (better)
                {
                    best = here;
                    bestSites = sites;
                    bestStands = standCount;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    /// <summary>Label each walkable tile with the land mass it belongs to. -1 is water.</summary>
    private static int[] LabelComponents(Terrain[] terrain, int width, int height)
    {
        var label = new int[width * height];
        for (int i = 0; i < label.Length; i++)
        {
            label[i] = -1;
        }

        var queue = new Queue<int>();
        int next = 0;

        for (int start = 0; start < label.Length; start++)
        {
            if (terrain[start] == Terrain.Water || label[start] >= 0)
            {
                continue;
            }

            int current = next++;
            label[start] = current;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;

                Visit(x + 1, y);
                Visit(x - 1, y);
                Visit(x, y + 1);
                Visit(x, y - 1);

                void Visit(int nx, int ny)
                {
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    {
                        return;
                    }

                    int neighbour = (ny * width) + nx;
                    if (terrain[neighbour] == Terrain.Water || label[neighbour] >= 0)
                    {
                        return;
                    }

                    label[neighbour] = current;
                    queue.Enqueue(neighbour);
                }
            }
        }

        return label;
    }

    private static int ComponentAt(
        int[] component, GridPos position, int width, int height, int minX, int minY)
    {
        int x = position.X - minX;
        int y = position.Y - minY;

        return x < 0 || x >= width || y < 0 || y >= height ? -1 : component[(y * width) + x];
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
