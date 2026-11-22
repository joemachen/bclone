// ⛔ D843's DIAGNOSIS — why nobody but the keeper goes to Joe's tavern. Kept in tools/harness/, COPIED into
// tests/Kith.Sim.Tests/ to run and DELETED from there before any commit (D420).
//
//   ZZ_SAVE=<a COPY of a .save>  dotnet test ... --filter FullyQualifiedName~ZzTavernSave
//
// ZZT lines: each tavern (open or why shut), each villager's home, walk to the tavern in tiles and the tavern within reach,
// then a year stepped — rests begun at home and evenings by adult, the states by tick-count — and the villagers again.
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

public sealed class ZzTavernSave
{
    private readonly ITestOutputHelper _o;

    public ZzTavernSave(ITestOutputHelper o) => _o = o;

    [Fact]
    public void WhyNobodyGoes()
    {
        string path = Environment.GetEnvironmentVariable("ZZ_SAVE") ?? throw new InvalidOperationException("ZZ_SAVE");
        SaveOpened opened = SaveFile.Open(path, ShippedConfig.Load());
        Assert.True(opened.Loop is not null, $"{opened.Refusal} {opened.Detail}");
        SimLoop loop = opened.Loop!;
        SimWorld world = loop.World;
        SimConfig config = world.Config;
        _o.WriteLine($"ZZT today {world.Today} {world.Clock.Season} Y{world.Clock.Year} reach {config.TavernReachTiles} base {TravelCostField.BaseTileCost}");
        foreach (Workplace w in world.Workplaces)
        {
            if (w.Kind == JobKind.TavernKeeper)
            {
                _o.WriteLine($"ZZT tavern {w.Name} at {w.Tile} site {w.IsSite} closed {w.Closed} ale {w.Store[Goods.Ale]} keepers {w.WorkerIds.Count} shut '{world.WhyTheTavernIsShut(w)}'");
            }
        }

        Dump();
        var homeRests = new Dictionary<int, int>();
        var evenings = new Dictionary<int, int>();
        int ale0 = 0;
        foreach (Workplace w in world.Workplaces)
        {
            if (w.Kind == JobKind.TavernKeeper) { ale0 += w.Store[Goods.Ale]; }
        }

        var states = new Dictionary<VillagerState, int>();
        for (int t = 0; t < config.TicksPerYear; t++)
        {
            var before = world.Villagers.ToDictionary(v => v.Id, v => v.State);
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (!v.Alive || !v.CanWork) { continue; }
                if (v.State == VillagerState.Resting && before[v.Id] != VillagerState.Resting && v.Tile == world.HomePlaceOf(v))
                {
                    homeRests[v.Id] = homeRests.GetValueOrDefault(v.Id) + 1;
                }

                if (v.State == VillagerState.AtTheTavern && before[v.Id] != VillagerState.AtTheTavern)
                {
                    evenings[v.Id] = evenings.GetValueOrDefault(v.Id) + 1;
                }

                states[v.State] = states.GetValueOrDefault(v.State) + 1;
            }
        }

        foreach (Villager v in world.Villagers.Where(v => v.Alive && v.CanWork))
        {
            _o.WriteLine($"ZZT year {v.Name} homeRestsBegun {homeRests.GetValueOrDefault(v.Id)} evenings {evenings.GetValueOrDefault(v.Id)}");
        }

        _o.WriteLine("ZZT states " + string.Join(", ", states.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
        Dump();

        void Dump()
        {
            foreach (Villager v in world.Villagers)
            {
                if (!v.Alive || v.State == VillagerState.Left) { continue; }
                Household h = world.HouseholdOf(v);
                Workplace? near = world.TavernWithinReachOf(v);
                string cost = "-";
                foreach (Workplace w in world.Workplaces)
                {
                    if (w.Kind == JobKind.TavernKeeper && h.HomeTile is GridPos home)
                    {
                        cost = $"{world.TravelCost.Cost(home, w.Tile) / (double)TravelCostField.BaseTileCost:F1}t";
                    }
                }

                _o.WriteLine($"ZZT {v.Name} age {v.AgeYears} canWork {v.CanWork} state {v.State} home {h.HomeTile} tile {v.Tile} homePlace {(h.HasHome ? world.HomePlaceOf(v) : default)} last {v.LastTavernDay} costToTavern {cost} near {near?.Name ?? "none"} job {v.WorkplaceId}");
            }
        }
    }
}
