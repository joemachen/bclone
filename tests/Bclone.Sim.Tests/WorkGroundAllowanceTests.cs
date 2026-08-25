using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Ground is priced in workers — D86, and the first limit in this game that is not distance.
/// </summary>
/// <remarks>
/// <para>
/// Joe: <em>"more foresters in a hut, the bigger the painted area can be (to a limit). The
/// system should warn when an area is too big for the number of assigned workers."</em>
/// </para>
/// <para>
/// <b>Painting is unbounded and the warning is the whole mechanism</b> (Joe's choice of two):
/// the brush claims whatever ground you drag over, and the hands you have decide how much of
/// it is actually kept. Bounding the brush by a radius as well would be two constraints doing
/// similar work, where this way the staffing control is the only answer.
/// </para>
/// </remarks>
public sealed class WorkGroundAllowanceTests
{
    private readonly ITestOutputHelper _output;

    public WorkGroundAllowanceTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());

    /// <summary>A workplace with people assigned to it and <b>no ground of its own yet</b>.</summary>
    /// <remarks>
    /// <para>
    /// <b>Built, not found.</b> Every guard in this class paints ground and then asks what
    /// the village makes of it, so it needs a workplace that started with none and has hands
    /// on it. This used to <em>search</em> the founded village for one and throw when no
    /// workplace happened to match — which made four guards a report on whichever buildings
    /// the allocator had staffed that morning rather than on the allowance. D137 changed
    /// exactly that (a forester that tends rather than only fells is staffed differently) and
    /// all four went red without the mechanic moving an inch.
    /// </para>
    /// <para>
    /// So the condition is <b>created</b>: a hut of its own, at the founding site, with two
    /// named villagers on it. Two rather than one because
    /// <see cref="LosingAWorkerIsWhatMakesGroundTooMuch"/> takes one away and still wants
    /// somebody there afterwards, and because an allowance of one worker's ground cannot tell
    /// "per worker" from "per workplace".
    /// </para>
    /// </remarks>
    private static Workplace AStaffedWorkplace(SimWorld world, out int hands)
    {
        const int Hands = 2;

        int id = 1;
        foreach (Workplace standing in world.Workplaces)
        {
            if (standing.Id >= id)
            {
                id = standing.Id + 1;
            }
        }

        var hut = new Workplace
        {
            Store = world.NewStockpile(),
            Id = id,
            Kind = JobKind.Forester,
            Name = $"forester's hut {id}",
            Position = world.Map.FoundingSite,
            Capacity = Hands,
        };

        Assert.True(
            world.Villagers.Count >= Hands,
            "The fixture village must have somebody in it, or this proves nothing.");

        for (int i = 0; i < Hands; i++)
        {
            hut.WorkerIds.Add(world.Villagers[i].Id);
        }

        world.Workplaces.Add(hut);
        Assert.Equal(0, world.Zones.WorkGroundTiles(hut.Id));

        hands = Hands;
        return hut;
    }

    /// <summary>Tiles near the founding site that are not water, so painting takes.</summary>
    private static IEnumerable<GridPos> DryGround(SimWorld world, int count)
    {
        GridPos site = world.Map.FoundingSite;
        int found = 0;

        for (int radius = 1; radius < 40 && found < count; radius++)
        {
            for (int dy = -radius; dy <= radius && found < count; dy++)
            {
                for (int dx = -radius; dx <= radius && found < count; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.Map.Contains(at)
                        && world.Map.TerrainAt(at) != Terrain.Water
                        && world.Zones.WorkGroundOwner(at) == 0)
                    {
                        found++;
                        yield return at;
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------
    //  The allowance
    // ---------------------------------------------------------------

    /// <summary>The allowance is the hands assigned, not the seats available.</summary>
    /// <remarks>
    /// Joe's wording, and the livelier of the two: a hut whose forester dies is overstretched
    /// that moment, where a capacity-based allowance would go on claiming the land was fine
    /// while nobody worked it.
    /// </remarks>
    [Fact]
    public void GroundIsPricedInTheHandsYouActuallyHave()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace place = AStaffedWorkplace(world, out int hands);
        int perWorker = VillageFixtures.Village.WorkGroundTilesPerWorker;

        _output.WriteLine(
            $"{place.Name}: {hands} of {place.Capacity} seats filled, "
            + $"allowance {world.WorkGroundAllowanceFor(place)} tiles");

        Assert.Equal(hands * perWorker, world.WorkGroundAllowanceFor(place));
        Assert.False(world.IsOverstretched(place), "Ground nobody painted cannot be too much.");
    }

    /// <summary>
    /// ⭐ Paint past what the hands can keep and the village says so — and still paints it.
    /// </summary>
    /// <remarks>
    /// <b>A warning, never a refusal</b> (D86, and D43's rule for a site that is merely far).
    /// Painting big and hiring afterwards is an ordinary way to play; a brush that stopped at
    /// the current headcount would make the player fight the staffing control every stroke.
    /// </remarks>
    [Fact]
    public void PaintingPastTheHandsWarnsAndIsStillAllowed()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace place = AStaffedWorkplace(world, out int hands);
        int allowance = world.WorkGroundAllowanceFor(place);

        PlacementVerdict last = PlacementVerdict.Fine;
        int painted = 0;
        int warned = 0;

        foreach (GridPos tile in DryGround(world, allowance + 3))
        {
            PlacementVerdict verdict = world.PaintWorkGround(place, tile);
            Assert.True(verdict.Allowed, "Painting work ground must never be refused for size.");

            painted++;
            if (verdict.HasWarning)
            {
                warned++;
                last = verdict;
            }
        }

        _output.WriteLine(
            $"{hands} hands, allowance {allowance}; painted {painted}, warned on {warned}");
        _output.WriteLine(last.Warning);

        Assert.Equal(painted, world.Zones.WorkGroundTiles(place.Id));
        Assert.True(world.IsOverstretched(place));

        // The warning starts exactly one tile past the allowance, not before.
        Assert.Equal(painted - allowance, warned);

        // And it is a sentence a player can act on: it names the place, the ground and
        // the hands (§1.1).
        Assert.Contains(place.Name, last.Warning, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(hands.ToString(), last.Warning, System.StringComparison.Ordinal);
    }

    /// <summary>Inside the allowance nothing is said at all.</summary>
    /// <remarks>
    /// The anti-vacuity half (D7): a warning that fires on the first tile would pass the
    /// guard above while being useless, because the player would learn to ignore it.
    /// </remarks>
    [Fact]
    public void PaintingWithinTheAllowanceIsSilent()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace place = AStaffedWorkplace(world, out _);
        int allowance = world.WorkGroundAllowanceFor(place);
        Assert.True(allowance > 1, "The fixture must staff somebody, or this proves nothing.");

        foreach (GridPos tile in DryGround(world, allowance))
        {
            Assert.False(
                world.PaintWorkGround(place, tile).HasWarning,
                "The village complained about ground it has the hands to keep.");
        }

        Assert.False(world.IsOverstretched(place));
    }

    /// <summary>Being overstretched outlives the painting that caused it.</summary>
    /// <remarks>
    /// The warning is a moment; the condition is a state. Somebody dies, or the staffing is
    /// turned down, and ground that was fine this morning is too much — the panel has to be
    /// able to ask, or the player is told once and never again.
    /// </remarks>
    [Fact]
    public void LosingAWorkerIsWhatMakesGroundTooMuch()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace place = AStaffedWorkplace(world, out int hands);
        Assert.True(hands >= 1);

        foreach (GridPos tile in DryGround(world, world.WorkGroundAllowanceFor(place)))
        {
            world.PaintWorkGround(place, tile);
        }

        Assert.False(world.IsOverstretched(place));

        // One hand fewer, and the same ground is now more than the hut can keep.
        place.WorkerIds.RemoveAt(place.WorkerIds.Count - 1);

        _output.WriteLine(
            $"{place.Name} kept {world.Zones.WorkGroundTiles(place.Id)} tiles with {hands} hands; "
            + $"with {place.WorkerIds.Count} its allowance is {world.WorkGroundAllowanceFor(place)}");

        Assert.True(world.IsOverstretched(place));
    }

    // ---------------------------------------------------------------
    //  ⭐ What the sentence actually says (D169, Joe)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A field that outruns its farmers is told so in farming words, and both remedies
    /// are named.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe, painting a farm: <em>"it would be helpful if, when painting the farm size, the UI
    /// told the user 'at this size you dont have enough farmers to utilize the land — add more
    /// farmers or make your field smaller' (which the user can choose to ignore and 'waste'
    /// land if they want)."</em>
    /// </para>
    /// <para>
    /// <b>The remedies are the half that was missing.</b> The old sentence stated the problem
    /// and stopped — and a warning naming no action is the always-on alert D42 and D123
    /// deleted, arriving one control along.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFieldTooBigForItsFarmersIsToldSoInFarmingWords()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace farm = AStaffedFarm(world, out int hands);
        int allowance = world.WorkGroundAllowanceFor(farm);

        PlacementVerdict last = PlacementVerdict.Fine;
        foreach (GridPos tile in DryGround(world, allowance + 3))
        {
            PlacementVerdict verdict = world.PaintWorkGround(farm, tile);
            Assert.True(verdict.Allowed, "Over-painting a field must never be refused (Joe).");
            if (verdict.HasWarning)
            {
                last = verdict;
            }
        }

        _output.WriteLine($"{hands} farmers, allowance {allowance}");
        _output.WriteLine(last.Warning);

        Assert.True(last.HasWarning, "The village said nothing about a field it cannot work.");

        // It is about farming, not forestry.
        Assert.Contains("farmer", last.Warning, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("field", last.Warning, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("untended", last.Warning, System.StringComparison.OrdinalIgnoreCase);

        // And it names both of the two things the player can do about it.
        Assert.Contains("another farmer", last.Warning, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("smaller field", last.Warning, System.StringComparison.OrdinalIgnoreCase);

        // The ground is painted anyway, because wasting land is a decision the player is
        // allowed to make.
        Assert.True(world.IsOverstretched(farm));
        Assert.Equal(allowance + 3, world.Zones.WorkGroundTiles(farm.Id));
    }

    /// <summary>
    /// ⛔ The anti-vacuity companion: a farm and a forester do not share one sentence.
    /// </summary>
    /// <remarks>
    /// <b>This is the guard that fails against the code Joe was playing</b>, and the one above
    /// is not. The old wording was a single template with the building's name substituted in,
    /// so a farm was told its field would <em>"go untended"</em> — the forester's word, and the
    /// wrong one for ground that since D167 is simply never sown. A guard that only reads the
    /// farm's sentence cannot tell a farm-shaped warning from a generic one that happens to
    /// contain the farm's name; reading both and requiring them to differ can.
    /// </remarks>
    [Fact]
    public void AFarmAndAForesterDoNotShareOneSentence()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace hut = AStaffedWorkplace(world, out _);
        foreach (GridPos tile in DryGround(world, world.WorkGroundAllowanceFor(hut) + 2))
        {
            world.PaintWorkGround(hut, tile);
        }

        Workplace farm = AStaffedFarm(world, out _);
        foreach (GridPos tile in DryGround(world, world.WorkGroundAllowanceFor(farm) + 2))
        {
            world.PaintWorkGround(farm, tile);
        }

        string? forestry = world.OverstretchedNote(hut);
        string? farming = world.OverstretchedNote(farm);

        _output.WriteLine($"forester: {forestry}");
        _output.WriteLine($"farm:     {farming}");

        Assert.NotNull(forestry);
        Assert.NotNull(farming);

        // Not the same sentence with a different name in it.
        Assert.NotEqual(
            forestry!.Replace(hut.Name, "X", System.StringComparison.OrdinalIgnoreCase),
            farming!.Replace(farm.Name, "X", System.StringComparison.OrdinalIgnoreCase));

        Assert.Contains("forester", forestry, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("farmer", farming, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fallow", forestry, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("untended", farming, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A field nobody farms is told that nobody is on it — not shown an allowance of zero.
    /// </summary>
    /// <remarks>
    /// <b>"Enough hands for 0" is arithmetic, not a sentence</b> (§1.1). It reads as though the
    /// ground itself were worthless, where what is true is that nobody has been put on it —
    /// a different thing, and one the player can go and fix.
    /// </remarks>
    [Fact]
    public void AFieldNobodyFarmsIsToldNobodyIsOnItRatherThanShownAZero()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace farm = FarmFixtures.RaiseAFarm(world);
        Assert.Empty(farm.WorkerIds);

        foreach (GridPos tile in DryGround(world, 4))
        {
            world.PaintWorkGround(farm, tile);
        }

        string? note = world.OverstretchedNote(farm);
        _output.WriteLine(note);

        Assert.NotNull(note);
        Assert.Contains("nobody", note!, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("put a farmer on", note, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" 0 ", note, System.StringComparison.Ordinal);
    }

    /// <summary>A farm with people on it and no ground of its own yet.</summary>
    /// <remarks>
    /// <see cref="AStaffedWorkplace"/>'s twin, and built rather than found for the same reason
    /// that one is: a guard that searches a founded village for a staffed farm is a report on
    /// what the allocator did that morning.
    /// </remarks>
    private static Workplace AStaffedFarm(SimWorld world, out int hands)
    {
        const int Hands = 2;

        Workplace farm = FarmFixtures.RaiseAFarm(world);
        Assert.Empty(farm.WorkerIds);
        Assert.True(world.Villagers.Count >= Hands);

        for (int i = 0; i < Hands; i++)
        {
            farm.WorkerIds.Add(world.Villagers[i].Id);
        }

        hands = Hands;
        return farm;
    }

    // ---------------------------------------------------------------
    //  What the brush refuses
    // ---------------------------------------------------------------

    /// <summary>Another building's ground is refused, and the refusal names them.</summary>
    /// <remarks>
    /// A careless drag across the valley must not quietly unstaff somebody else's hut, and
    /// <em>"that ground is taken"</em> without saying by whom is the kind of refusal §1.1
    /// exists to prevent.
    /// </remarks>
    [Fact]
    public void AnotherBuildingsGroundIsRefusedByName()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        Workplace first = world.Workplaces[0];
        Workplace second = world.Workplaces[1];
        GridPos tile = DryGround(world, 1).Single();

        Assert.True(world.PaintWorkGround(first, tile).Allowed);

        PlacementVerdict refused = world.PaintWorkGround(second, tile);
        _output.WriteLine(refused.Reason);

        // A refusal carries Reason; only a permitted-but-questionable answer carries
        // Warning. Two words for two outcomes, and the panel shows them differently.
        Assert.False(refused.Allowed);
        Assert.Contains(first.Name, refused.Reason, System.StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.Id, world.Zones.WorkGroundOwner(tile));
    }

    /// <summary>Water and the world's edge are never painted.</summary>
    [Fact]
    public void NobodyIsGivenWaterOrTheEdgeOfTheValley()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        Workplace place = world.Workplaces[0];

        var offMap = new GridPos(world.Map.MinX - 50, world.Map.MinY - 50);
        Assert.False(world.PaintWorkGround(place, offMap).Allowed);

        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) == Terrain.Water)
                {
                    Assert.False(world.PaintWorkGround(place, at).Allowed);
                    Assert.Equal(0, world.Zones.WorkGroundOwner(at));
                    return;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("The generated valley has no water to refuse.");
    }

    /// <summary>Ground can be handed back one tile at a time.</summary>
    [Fact]
    public void GroundCanBeTakenBack()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        Workplace place = world.Workplaces[0];
        GridPos tile = DryGround(world, 1).Single();

        world.PaintWorkGround(place, tile);
        Assert.Equal(1, world.Zones.WorkGroundTiles(place.Id));

        Assert.True(world.EraseWorkGround(place, tile));
        Assert.Equal(0, world.Zones.WorkGroundTiles(place.Id));

        // Somebody else's ground is not yours to rub out.
        world.PaintWorkGround(place, tile);
        Assert.False(world.EraseWorkGround(world.Workplaces[1], tile));
        Assert.Equal(place.Id, world.Zones.WorkGroundOwner(tile));
    }

    /// <summary>The shipped config carries the number, so the game the player runs has it.</summary>
    /// <remarks>
    /// METHODOLOGY §3 — the fixture derives its numbers and <c>data/sim.config.json</c> is
    /// typed by hand, and the gap between them has produced D48, D49 and D50.
    /// </remarks>
    [Fact]
    public void TheShippedConfigPricesGroundToo()
    {
        SimConfig shipped = ShippedConfig.Load();

        Assert.True(
            shipped.WorkGroundTilesPerWorker > 0,
            "data/sim.config.json must say how much ground one worker keeps.");
        Assert.Equal(
            VillageFixtures.Village.WorkGroundTilesPerWorker,
            shipped.WorkGroundTilesPerWorker);
    }
}
