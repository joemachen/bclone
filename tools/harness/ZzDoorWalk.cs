using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D623's MEASUREMENT — what a door's in-and-out costs a walk, so the economy can price it (`doors.md §5`). Kept in
// tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit (D420).
// Twenty fixture villages (the village fixture and the established warm start, seeds 1–10, ten years each); in each, the
// same walks priced twice — through the doors, and with `DoorsOnEverySide` (D383's any side) — and the difference in tiles:
//   ZZW passage — a free reached tile within 6 of a doored building, to it: one way in.
//   ZZW leg     — one doored building to another: a way out and a way in.
//   ZZW haul    — a farm's field tile to its farmhouse and back (the village fixture with a farm raised at Year 0).
// Run: cp in, `dotnet test tests/Bclone.Sim.Tests -c Release --filter "FullyQualifiedName~ZzDoorWalk" --logger
// "console;verbosity=detailed"`, delete.
public sealed class ZzDoorWalk
{
    private readonly ITestOutputHelper _o;

    public ZzDoorWalk(ITestOutputHelper o) => _o = o;

    public static IEnumerable<object[]> Runs()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            yield return new object[] { "village", seed };
            yield return new object[] { "established", seed };
        }
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, ulong seed)
    {
        SimConfig config = (arm == "established" ? ShippedConfig.Established() : VillageFixtures.Village) with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace? farm = null;
        if (arm == "village")
        {
            farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 2);
        }

        loop.Step(config.TicksPerYear * 10);

        var shapes = new List<(GridPos Tile, Footprint Shape)>();
        var seen = new HashSet<Footprint>();
        foreach (Workplace w in world.Workplaces)
        {
            if (world.DoorOf(w) is { Shut: false } && seen.Add(w.Footprint))
            {
                shapes.Add((w.Tile, w.Footprint));
            }
        }

        foreach (StoreBuilding s in world.StoreBuildings)
        {
            if (world.DoorOf(s) is { Shut: false } && seen.Add(s.Footprint))
            {
                shapes.Add((s.Tile, s.Footprint));
            }
        }

        foreach (Household h in world.Households)
        {
            if (world.DoorOf(h) is { Shut: false } && world.HomeFootprintOf(h) is Footprint home && seen.Add(home))
            {
                shapes.Add((h.HomeTile!.Value, home));
            }
        }

        GridPos from = world.FirstHomeOrFoundingSite();
        var passages = new List<(GridPos From, GridPos To)>();
        foreach ((GridPos tile, Footprint shape) in shapes)
        {
            for (int dy = -6; dy <= 6; dy++)
            {
                for (int dx = -6; dx <= 6; dx++)
                {
                    var t = new GridPos(tile.X + dx, tile.Y + dy);
                    if (world.IsWalkable(t) && !world.StandsOn(t) && world.TravelCost.CanReach(from, t))
                    {
                        passages.Add((t, tile));
                    }
                }
            }
        }

        var legs = new List<(GridPos From, GridPos To)>();
        for (int i = 0; i < shapes.Count; i++)
        {
            for (int j = 0; j < shapes.Count; j++)
            {
                if (i != j)
                {
                    legs.Add((shapes[i].Tile, shapes[j].Tile));
                }
            }
        }

        var hauls = new List<(GridPos From, GridPos To)>();
        if (farm is not null && world.Workplaces.Contains(farm))
        {
            foreach (int index in world.Zones.WorkGroundOf(farm.Id))
            {
                GridPos field = world.Zones.PositionOf(index);
                hauls.Add((field, farm.Tile));
                hauls.Add((farm.Tile, field));
            }
        }

        int[] Price(List<(GridPos From, GridPos To)> walks) =>
            walks.Select(w => world.TravelCost.Cost(w.From, w.To)).ToArray();

        int[] passagesWith = Price(passages), legsWith = Price(legs), haulsWith = Price(hauls);
        world.DoorsOnEverySide = true;
        world.TravelCost.Forget();
        int[] passagesWithout = Price(passages), legsWithout = Price(legs), haulsWithout = Price(hauls);
        world.DoorsOnEverySide = false;
        world.TravelCost.Forget();

        Say("passage", passagesWith, passagesWithout);
        Say("leg", legsWith, legsWithout);
        Say("haul", haulsWith, haulsWithout);

        void Say(string what, int[] with, int[] without)
        {
            var extra = new List<int>();
            for (int i = 0; i < with.Length; i++)
            {
                if (with[i] != TravelCostField.Unreachable && without[i] != TravelCostField.Unreachable)
                {
                    extra.Add(with[i] - without[i]);
                }
            }

            _o.WriteLine($"ZZW {what} {arm} {seed} n {extra.Count} extra-sum {extra.Sum()} base {TravelCostField.BaseTileCost} "
                + $"never-shorter {extra.All(e => e >= 0)}");
        }
    }
}
