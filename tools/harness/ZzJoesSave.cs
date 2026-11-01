using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D664's MEASUREMENT — Joe's own village run on from his save. Kept in tools/harness/, COPIED into
// tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit.
//
//   ZZ_SAVE=<path to a .save>  ZZ_YEARS=2  dotnet test ... --filter FullyQualifiedName~ZzJoesSave
//
// One ZZJ line a season: where the leather is (the lodges' buffers against the stores), the coats sewn,
// the adults coated, and who holds the tailor's and the marketer's seats. A last ZZJ line sums the run.
public sealed class ZzJoesSave
{
    private readonly ITestOutputHelper _o;

    public ZzJoesSave(ITestOutputHelper o) => _o = o;

    [Fact]
    public void RunOnFromTheSave()
    {
        string path = Environment.GetEnvironmentVariable("ZZ_SAVE") ?? throw new InvalidOperationException("ZZ_SAVE is the save's path.");
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "2", System.Globalization.CultureInfo.InvariantCulture);
        SaveOpened opened = SaveFile.Open(path, ShippedConfig.Load());
        Assert.True(opened.Loop is not null, $"{opened.Refusal} {opened.Detail}");
        SimLoop loop = opened.Loop!;
        SimWorld world = loop.World;
        SimConfig config = world.Config;

        int coatsAtStart = world.CoatsEverSewn;
        int leatherAtStart = world.LeatherEverHeld;
        long hauledToStores = 0;
        int storesLast = world.InStores(Goods.Leather);
        Season season = world.Clock.Season;
        Line("start");
        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            loop.StepOnce();
            int storesNow = world.InStores(Goods.Leather);
            if (storesNow > storesLast)
            {
                hauledToStores += storesNow - storesLast;
            }

            storesLast = storesNow;
            if (world.Clock.Season != season)
            {
                season = world.Clock.Season;
                Line("season");
            }
        }

        _o.WriteLine($"ZZJ end years {years} coatsSewn {world.CoatsEverSewn - coatsAtStart} leatherHandled {world.LeatherEverHeld - leatherAtStart} " +
            $"leatherReachedStores {hauledToStores} lodgeLeatherEnd {LodgeLeather()} storesLeatherEnd {world.InStores(Goods.Leather)}");

        void Line(string why)
        {
            int adults = 0, coated = 0, tailors = 0, marketers = 0, hunters = 0;
            foreach (Villager v in world.Villagers)
            {
                if (!v.Alive || !v.CanWork)
                {
                    continue;
                }

                adults++;
                if (v.CoatDays > 0) { coated++; }
                if (v.HasJob && world.FindWorkplace(v.WorkplaceId) is Workplace w)
                {
                    if (w.Kind == JobKind.Tailor) { tailors++; }
                    if (w.Kind == JobKind.Marketer) { marketers++; }
                    if (w.Kind == JobKind.Hunter) { hunters++; }
                }
            }

            _o.WriteLine($"ZZJ {why} {world.Clock.Season} Y{world.Clock.Year} lodgeLeather {LodgeLeather()} lodgeMeat {LodgeMeat()} storesLeather {world.InStores(Goods.Leather)} " +
                $"clothingStored {world.InStores(Goods.Clothing)} coatsSewn {world.CoatsEverSewn} coated {coated}/{adults} tailors {tailors} marketers {marketers} hunters {hunters}");
        }

        int LodgeLeather() => world.Workplaces.Where(w => w.Kind == JobKind.Hunter && !w.IsSite).Sum(w => w.Store[Goods.Leather]);
        int LodgeMeat() => world.Workplaces.Where(w => w.Kind == JobKind.Hunter && !w.IsSite).Sum(w => w.Store[Goods.Meat]);
    }
}
