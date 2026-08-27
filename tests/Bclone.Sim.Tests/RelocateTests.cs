using System.Linq;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Relocation — <b>the remedy that lets the sim impose a placement at all</b>.
/// </summary>
/// <remarks>
/// <para>
/// Joe, 2026-08-26: *"if we are going to allow the sim to place it, then we need to add a
/// 'relocate' function for all buildings (storage buildings must be 'emptied' first)."* **The sim
/// may only impose a placement if the player can undo it** — `§0.1`'s *recoverable by design*
/// applied to layout, and the reason a gifted building is a gift rather than a trap.
/// </para>
/// <para>
/// ⛔ <b>Houses are not here, and that is D228</b>: housing is the brush's business in both
/// directions. <see cref="ResidentialZoneTests"/> holds that half.
/// </para>
/// </remarks>
public sealed class RelocateTests
{
    private readonly ITestOutputHelper _output;

    public RelocateTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink());

    /// <summary>A tile the village may build on, found rather than assumed.</summary>
    private static GridPos Buildable(SimWorld world, params GridPos[] avoid)
    {
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (System.Array.Exists(avoid, a => a == at))
                {
                    continue;
                }

                if (world.Map.TerrainAt(at) == Terrain.Grass
                    && world.CanBuildAt(BuildingKind.Granary, at).Allowed)
                {
                    return at;
                }
            }
        }

        throw new System.InvalidOperationException("No buildable tile in the valley.");
    }

    /// <summary>Deliver a site's materials and work it to completion, as a crew would.</summary>
    private static void Finish(SimWorld world, GridPos site)
    {
        Workplace found = world.Workplaces.Single(w => w.Position == site && w.IsSite);
        ConstructionSite plan = found.Construction!;

        foreach (MaterialCost owed in plan.Recipe.Materials)
        {
            plan.Deliver(owed.Goods, owed.Amount);
        }

        while (!plan.IsFinished)
        {
            plan.Work();
        }

        world.Complete(found);
    }

    private static StoreBuilding StandAGranary(SimWorld world, out GridPos at)
    {
        at = Buildable(world);
        world.Mark(BuildingKind.Granary, at);
        Finish(world, at);
        return world.StoreAt(at)!;
    }

    // -----------------------------------------------------------------

    /// <summary>⭐ An empty store moves, and the same building arrives.</summary>
    [Fact]
    public void AnEmptyStoreMovesAndKeepsItsIdentity()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        StoreBuilding granary = StandAGranary(world, out GridPos from);
        string name = granary.Name;
        int capacity = granary.Store.Capacity;

        GridPos to = Buildable(world, from);
        PlacementVerdict verdict = world.MarkRelocation(from, to);
        Assert.True(verdict.Allowed, verdict.Reason);

        Finish(world, to);

        // ⭐ Not a new granary — the same object, at a different tile.
        Assert.Same(granary, world.StoreAt(to));
        Assert.Null(world.StoreAt(from));
        Assert.Equal(name, world.StoreAt(to)!.Name);
        Assert.Equal(capacity, world.StoreAt(to)!.Store.Capacity);
    }

    /// <summary>
    /// ⛔ A store with goods in it refuses to move, and the sentence says what to do.
    /// </summary>
    /// <remarks>
    /// <b>Joe's second half:</b> *"storage buildings must be 'emptied' first."* ⚠️ **A refusal with
    /// a reason, not a disabled control** (D43) — and it names the number, because *"empty it
    /// first"* against a granary the player thought was empty is the untraceable outcome §1.1
    /// forbids.
    /// </remarks>
    [Fact]
    public void AStoreWithGoodsInItRefusesToMove()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        StoreBuilding granary = StandAGranary(world, out GridPos from);
        granary.Store.Receive(Goods.Food, 200);

        GridPos to = Buildable(world, from);
        PlacementVerdict refused = world.MarkRelocation(from, to);

        _output.WriteLine(refused.Reason);

        Assert.False(refused.Allowed);
        Assert.Contains("Empty it first", refused.Reason, System.StringComparison.Ordinal);
        Assert.Contains("200", refused.Reason, System.StringComparison.Ordinal);

        // ⭐ And emptying it is the whole of the remedy.
        granary.Store.TakeAll(Goods.Food);
        Assert.True(world.MarkRelocation(from, to).Allowed);
    }

    /// <summary>⛔ A house is never moved by hand — the brush is its only control (D228).</summary>
    [Fact]
    public void AHouseIsNotMovedByHand()
    {
        SimLoop loop = Loop();
        loop.Step(Config.TicksPerYear * 20);
        SimWorld world = loop.World;

        GridPos lived = world.Households.First(h => h.HasHome).Home();
        PlacementVerdict refused = world.MarkRelocation(lived, Buildable(world, lived));

        _output.WriteLine(refused.Reason);

        Assert.False(refused.Allowed);
        Assert.Contains("Unpaint", refused.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>⭐⭐ A library takes its records with it — the shelves are the building.</summary>
    [Fact]
    public void AMovedLibraryKeepsItsRecords()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        GridPos from = Buildable(world);
        var library = new Library
        {
            Position = from,
            Name = "library 1",
            Shelves = Config.LibraryShelves,
        };
        library.Records.Add(2);
        world.Libraries.Add(library);

        GridPos to = Buildable(world, from);
        Assert.True(world.MarkRelocation(from, to).Allowed);
        Finish(world, to);

        Assert.Equal(to, library.Position);
        Assert.True(world.IsWrittenDown(2));
    }

    /// <summary>⛔ Nothing to move is a sentence, not a silent no-op.</summary>
    [Fact]
    public void ThereIsNothingThereToMove()
    {
        SimWorld world = Loop().World;
        GridPos empty = Buildable(world);

        PlacementVerdict refused = world.MarkRelocation(empty, Buildable(world, empty));

        Assert.False(refused.Allowed);
        Assert.Contains("nothing there", refused.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ⚠️ A source demolished mid-move raises nothing, and the village says so.
    /// </summary>
    /// <remarks>
    /// <b>The phantom this guards against would have been a free building.</b> A relocation site
    /// that fell through to the ordinary raise path would hand the player a granary nobody paid
    /// for — the *plausible default* shape of bug this project keeps finding, rather than a crash.
    /// </remarks>
    [Fact]
    public void ASourceDemolishedMidMoveRaisesNothing()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        StoreBuilding granary = StandAGranary(world, out GridPos from);
        GridPos to = Buildable(world, from);
        Assert.True(world.MarkRelocation(from, to).Allowed);

        int stores = world.StoreBuildings.Count;
        world.Demolish(granary);
        Finish(world, to);

        Assert.Null(world.StoreAt(to));
        Assert.Equal(stores - 1, world.StoreBuildings.Count);
    }
}
