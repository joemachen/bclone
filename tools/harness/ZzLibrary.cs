using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Kith.Sim.Systems;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D864's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED from there
// before any commit (D420). `specs/library-borrowing.md §4`: before any number for letters to kin or the almanac is typed,
// what a village already has — where its library stands from its homes, how many homes have a spare bed, how many nomads come
// — and what the two books would change: a relative a household (the letters, posed), firewood held on winter's first day and
// the cold days and goings-in of a winter (the almanac's two halves).
//
// Per save (the NEWEST .save in each folder under ZZ_SAVES, a COPY of Joe's; ZZ_SHARD=k/n splits the folders):
//   ZZA  — at load: people, adults, lived-in homes, libraries, each home's walk to the nearest standing library in tiles
//          (buckets ≤8 ≤12 ≤16 ≤24 >24 none), homes with a spare bed (living < max_household_size), unpaired adults 18+.
//   then ZZ_YEARS (15) stepped on, with ZZ_ARM=base (nothing) or letters (a relative posed, below):
//   ZZY  — a line a year: people, food held (stores + larders), nomads arrived, relatives posed, births, deaths by cause, left.
//   ZZF  — a line a winter: lived-in homes; on Fall's first day and Winter's first day, homes holding ≥ a winter's burn
//          (WinterFirewoodOf), ≥ the wanted (FirewoodWantedFor); on Winter's first day homes at 0; home-days at 0 in winter.
//   ZZC  — a line a winter: working adults (graduates who read the year / others), adult-winters with ≥6 days at the
//          get-warm line (the ColdWinter cause), goings-in (steps into SeekingShelter), cold deaths.
//   ZZE  — the run's totals.
// The letters pose (ZZ_ARM=letters): at each spring turn, a lived-in home with a spare bed and a living adult, within
// ZZ_REACH tiles' walk of a standing library (0 = every home, the library beside each — the ceiling), and no relative posed
// yet this run (one a household a generation: the run is shorter than a generation), receives an unmarried adult of 18–30
// carrying nomads_food_each, ZZ_SPREAD (0) hashed years after it first qualified.
// Copy in; `dotnet test tests/Kith.Sim.Tests -c Release --filter ZzLibrary --logger "console;verbosity=detailed"`; delete.
public sealed class ZzLibrary
{
    private static readonly int[] ReachCuts = { 8, 12, 16, 24 };
    private readonly ITestOutputHelper _o;

    public ZzLibrary(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string what)
    {
        if (what.Length == 0)
        {
            return;
        }

        SaveOpened opened = SaveFile.Open(what, ShippedConfig.Load());
        if (opened.Loop is not SimLoop loop)
        {
            _o.WriteLine($"ZZX {what} refused {opened.Refusal} {opened.Detail}");
            return;
        }

        SimWorld world = loop.World;
        SimConfig c = world.Config;
        string village = Path.GetFileName(Path.GetDirectoryName(what)) ?? what;
        string arm = Environment.GetEnvironmentVariable("ZZ_ARM") ?? "base";
        int years = Env("ZZ_YEARS", 15);
        int reach = Env("ZZ_REACH", 0);
        int spread = Env("ZZ_SPREAD", 0);

        // ---- At load.
        var buckets = new int[ReachCuts.Length + 2];
        int homes = 0, spare = 0;
        foreach (Household h in world.Households)
        {
            if (h.HomeTile is not GridPos home || world.LivingMembersOf(h) == 0)
            {
                continue;
            }

            homes++;
            if (world.LivingMembersOf(h) < c.MaxHouseholdSize)
            {
                spare++;
            }

            int walk = WalkToALibrary(world, home);
            int b = walk < 0 ? ReachCuts.Length + 1 : Array.FindIndex(ReachCuts, cut => walk <= cut);
            buckets[b < 0 ? ReachCuts.Length : b]++;
        }

        int adults = world.Villagers.Count(v => v.Alive && v.CanWork);
        int single = world.Villagers.Count(v => v.Alive && v.CanWork && !v.IsPaired && v.AgeYears >= c.LeaveHomeAge);
        _o.WriteLine(
            $"ZZA {village} {arm} year {world.Clock.Year} people {world.Population} adults {adults} homes {homes} "
            + $"libraries {world.Libraries.Count(l => !l.Closed)} reach {string.Join(' ', buckets)} spare {spare} single {single} "
            + $"graduates {world.Villagers.Count(v => v.Alive && v.CanWork && BehaviorSystem.ReadsTheYear(c, v))}");

        // ---- Stepped on.
        var startIds = new HashSet<int>(world.Villagers.Select(v => v.Id));
        var posedIds = new HashSet<int>();
        var wasAlive = world.Villagers.Where(v => v.Alive).Select(v => v.Id).ToHashSet();
        var seen = new HashSet<int>(startIds);
        var wasState = new Dictionary<int, VillagerState>();
        var posedHouseholds = new Dictionary<int, int>();
        var firstQualified = new Dictionary<int, int>();
        ulong year = (ulong)c.TicksPerYear;
        ulong season = (ulong)c.TicksPerDay * (ulong)c.DaysPerSeason;
        ulong day = (ulong)c.TicksPerDay;

        int yNomads = 0, yPosed = 0, yBirths = 0, yStarved = 0, yCold = 0, yOther = 0, yLeft = 0;
        int tNomads = 0, tPosed = 0, tBirths = 0, tStarved = 0, tCold = 0, tOther = 0, tLeft = 0;
        int fallBurn = 0, fallWanted = 0, fallHomes = 0, fireOutDays = 0, goingsInGrad = 0, goingsInOther = 0, wColdDeaths = 0;
        int tColdGrad = 0, tColdOther = 0, tWintersGrad = 0, tWintersOther = 0, tGoingsGrad = 0, tGoingsOther = 0, tFireOut = 0;
        int tShortAtWinter = 0, tHomeWinters = 0;

        for (long t = 0; t < (long)c.TicksPerYear * years; t++)
        {
            ulong tick = world.Tick;
            bool springTurn = tick % year == 0UL && tick > 0UL;
            if (springTurn && arm == "letters")
            {
                int posed = PoseRelatives(world, reach, spread, posedHouseholds, firstQualified, posedIds);
                yPosed += posed;
                tPosed += posed;
                foreach (int pid in posedIds)
                {
                    if (seen.Add(pid))
                    {
                        wasAlive.Add(pid);
                    }
                }
            }

            // Fall's first day: who already holds a winter.
            if (tick % year == 2UL * season)
            {
                fallBurn = fallWanted = fallHomes = 0;
                foreach (Household h in LivedIn(world))
                {
                    fallHomes++;
                    fallBurn += h.Stockpile.Firewood >= world.WinterFirewoodOf(h) ? 1 : 0;
                    fallWanted += h.Stockpile.Firewood >= world.FirewoodWantedFor(h) ? 1 : 0;
                }
            }

            // Winter's first day: who has their wood in.
            if (tick % year == 3UL * season)
            {
                int homesNow = 0, burn = 0, wanted = 0, none = 0;
                foreach (Household h in LivedIn(world))
                {
                    homesNow++;
                    burn += h.Stockpile.Firewood >= world.WinterFirewoodOf(h) ? 1 : 0;
                    wanted += h.Stockpile.Firewood >= world.FirewoodWantedFor(h) ? 1 : 0;
                    none += h.Stockpile.Firewood == 0 ? 1 : 0;
                }

                tShortAtWinter += homesNow - burn;
                tHomeWinters += homesNow;
                _o.WriteLine(
                    $"ZZF {village} {arm} year {world.Clock.Year} homes {homesNow} fall {fallHomes} fallburn {fallBurn} "
                    + $"fallwanted {fallWanted} winterburn {burn} winterwanted {wanted} winternone {none}");
                fireOutDays = goingsInGrad = goingsInOther = wColdDeaths = 0;
            }

            // A winter day with a lived-in home's fire out.
            if (world.Clock.Season == Season.Winter && tick % day == 0UL)
            {
                foreach (Household h in LivedIn(world))
                {
                    if (h.Stockpile.Firewood == 0)
                    {
                        fireOutDays++;
                        tFireOut++;
                    }
                }
            }

            // Winter's last tick: the cold days before the read clears them.
            if (tick % year == year - 1UL)
            {
                int grad = 0, other = 0, coldGrad = 0, coldOther = 0;
                foreach (Villager v in world.Villagers)
                {
                    if (!v.Alive || !v.CanWork)
                    {
                        continue;
                    }

                    bool reads = BehaviorSystem.ReadsTheYear(c, v);
                    bool cold = v.ColdDaysThisWinter >= 6;
                    if (reads)
                    {
                        grad++;
                        coldGrad += cold ? 1 : 0;
                    }
                    else
                    {
                        other++;
                        coldOther += cold ? 1 : 0;
                    }
                }

                tColdGrad += coldGrad;
                tColdOther += coldOther;
                tWintersGrad += grad;
                tWintersOther += other;
                _o.WriteLine(
                    $"ZZC {village} {arm} year {world.Clock.Year} adults {grad} {other} colddays6 {coldGrad} {coldOther} "
                    + $"goingsin {goingsInGrad} {goingsInOther} colddeaths {wColdDeaths} fireoutdays {fireOutDays}");
            }

            loop.StepOnce();

            foreach (Villager v in world.Villagers)
            {
                if (seen.Add(v.Id) && !posedIds.Contains(v.Id))
                {
                    wasAlive.Add(v.Id);
                    if (v.ArrivedAtTick > 0UL)
                    {
                        yNomads++;
                        tNomads++;
                    }
                    else
                    {
                        yBirths++;
                        tBirths++;
                    }
                }

                VillagerState was = wasState.GetValueOrDefault(v.Id, v.State);
                if (v.State == VillagerState.SeekingShelter && was != VillagerState.SeekingShelter && v.CanWork)
                {
                    if (BehaviorSystem.ReadsTheYear(c, v))
                    {
                        goingsInGrad++;
                        tGoingsGrad++;
                    }
                    else
                    {
                        goingsInOther++;
                        tGoingsOther++;
                    }
                }

                if (v.State == VillagerState.Left && was != VillagerState.Left)
                {
                    yLeft++;
                    tLeft++;
                }

                wasState[v.Id] = v.State;

                if (!v.Alive && wasAlive.Remove(v.Id) && v.State != VillagerState.Left)
                {
                    switch (v.CauseOfDeath)
                    {
                        case CauseOfDeath.Starvation:
                            yStarved++;
                            tStarved++;
                            break;
                        case CauseOfDeath.Cold:
                            yCold++;
                            tCold++;
                            wColdDeaths++;
                            break;
                        default:
                            yOther++;
                            tOther++;
                            break;
                    }
                }
            }

            if (world.Tick % year == 0UL)
            {
                _o.WriteLine(
                    $"ZZY {village} {arm} year {world.Clock.Year} people {world.Population} food {FoodHeld(world)} "
                    + $"nomads {yNomads} posed {yPosed} births {yBirths} starved {yStarved} cold {yCold} other {yOther} left {yLeft}");
                yNomads = yPosed = yBirths = yStarved = yCold = yOther = yLeft = 0;
            }
        }

        _o.WriteLine(
            $"ZZE {village} {arm} years {years} people {world.Population} food {FoodHeld(world)} nomads {tNomads} posed {tPosed} "
            + $"births {tBirths} starved {tStarved} cold {tCold} other {tOther} left {tLeft} "
            + $"homewinters {tHomeWinters} shortatwinter {tShortAtWinter} fireoutdays {tFireOut} "
            + $"winters {tWintersGrad} {tWintersOther} colddays6 {tColdGrad} {tColdOther} goingsin {tGoingsGrad} {tGoingsOther}");
    }

    private static IEnumerable<Household> LivedIn(SimWorld world) =>
        world.Households.Where(h => h.HomeTile is not null && world.LivingMembersOf(h) > 0);

    private static int FoodHeld(SimWorld world)
    {
        int food = 0;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            food += world.FoodIn(store.Store);
        }

        foreach (Household h in world.Households)
        {
            food += world.FoodIn(h.Stockpile);
        }

        return food;
    }

    /// <summary>The walk from a home to the nearest standing library in tiles, or -1 for none reachable.</summary>
    private static int WalkToALibrary(SimWorld world, GridPos home)
    {
        int best = -1;
        foreach (Library library in world.Libraries)
        {
            if (library.Closed)
            {
                continue;
            }

            int cost = world.TravelCost.Cost(home, library.Tile);
            if (cost == TravelCostField.Unreachable)
            {
                continue;
            }

            int tiles = cost / TravelCostField.BaseTileCost;
            if (best < 0 || tiles < best)
            {
                best = tiles;
            }
        }

        return best;
    }

    private static int PoseRelatives(
        SimWorld world, int reach, int spread, Dictionary<int, int> posedHouseholds, Dictionary<int, int> firstQualified, HashSet<int> posedIds)
    {
        SimConfig c = world.Config;
        int posed = 0;
        foreach (Household h in LivedIn(world).ToList())
        {
            if ((posedHouseholds.TryGetValue(h.Id, out int last) && world.Clock.Year - last < Env("ZZ_GEN", 24))
                || world.LivingMembersOf(h) >= c.MaxHouseholdSize
                || !h.MemberIds.Any(id => world.FindVillager(id) is { Alive: true, CanWork: true }))
            {
                continue;
            }

            // ZZ_CASCADE=0: a household holding somebody who came by letter does not write again (a relative's own new home).
            if (Env("ZZ_CASCADE", 1) == 0 && h.MemberIds.Any(m => posedIds.Contains(m) && world.FindVillager(m) is { Alive: true }))
            {
                continue;
            }

            if (reach > 0)
            {
                int walk = WalkToALibrary(world, h.HomeTile!.Value);
                if (walk < 0 || walk > reach)
                {
                    continue;
                }
            }

            int first = firstQualified.TryGetValue(h.Id, out int f) ? f : firstQualified[h.Id] = world.Clock.Year;
            int wait = spread <= 0 ? 0 : (int)(NameHash.Mix(world.Seed, h.Id, 77, 0) % (ulong)(spread + 1));
            if (world.Clock.Year - first < wait)
            {
                continue;
            }

            // ZZ_FOOD=1: the birth gate's food half — the village holds a birth's food, and last year's harvest fed one more.
            if (Env("ZZ_FOOD", 0) == 1
                && (world.FoodTheVillageHolds() < world.FoodABirthNeeds(world.Population) || !world.TheHarvestFeedsOneMore()))
            {
                continue;
            }

            int key = 2_000_000 + h.Id;
            int id = world.NextVillagerId();
            int age = NameHash.NomadAge(world.Seed, key, 18, 30);
            var villager = new Villager
            {
                Id = id,
                Name = NameHash.FirstName(world.Seed, key, 0, c.FirstNamePrefixes, c.FirstNameSuffixes),
                Surname = h.Surname,
                LifespanYears = NameHash.NomadLifespan(world.Seed, key, c.LifespanYearsBase, c.LifespanYearsVariance),
                Rhythm = c.SeededRhythm ? NameHash.NomadRhythm(world.Seed, key, c.TicksPerDay) : 0,
                Carried = world.NewStockpile(),
                Hunger = c.SeededRhythm && c.EatThreshold > 0 ? NameHash.MealPhase(world.Seed, id, c.EatThreshold) : 0,
                Position = Point.CentreOf(h.HomeTile!.Value),
                HouseholdId = h.Id,
                BirthTick = (long)world.Tick - ((long)age * c.TicksPerYear) - NameHash.BirthdayOffset(world.Seed, id, c.TicksPerYear),
                AgeYears = age,
                LifeStage = LifeStage.Adult,
                Founder = false,
                ArrivedAtTick = world.Tick,
            };

            h.AddMember(id);
            world.Villagers.Add(villager);
            h.Stockpile.Receive(Goods.Produce, c.NomadsFoodEach);
            world.FoodEverBroughtIn += c.NomadsFoodEach;
            posedHouseholds[h.Id] = world.Clock.Year;
            firstQualified.Remove(h.Id);
            posedIds.Add(id);
            posed++;
        }

        return posed;
    }

    // ---- The unattended harness valley (seeds 200–299, ZZ_SEEDS narrows): the nomads harness's opening, a granary and a
    // warehouse at Year 3, ZZ_YEARS (50) on. Arm `letters` poses a relative a home with a spare bed from Year ZZ_FROM (15, about
    // when the library gift comes), once a household each ZZ_GEN (24) years — the library beside every home, the ceiling.
    // ZZB: alive at the end, peak, deaths by cause, nomads, posed, births; the year the valley died (-1 never).
    [Theory]
    [MemberData(nameof(Valleys))]
    public void OnTheHarness(string arm, ulong seed)
    {
        if (arm.Length == 0)
        {
            return;
        }

        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Kith.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        var limits = config.StartingStockLimits;
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit))
            {
                world.SetStockLimit((Goods)id, limit);
            }
        }

        ColdStartTests.PlayTheOpening(world);
        int years = Env("ZZ_YEARS", 50);
        int from = Env("ZZ_FROM", 15);
        var posedHouseholds = new Dictionary<int, int>();
        var firstQualified = new Dictionary<int, int>();
        var posedIds = new HashSet<int>();
        var seen = new HashSet<int>(world.Villagers.Select(v => v.Id));
        var alive = world.Villagers.Where(v => v.Alive).Select(v => v.Id).ToHashSet();
        int peak = 0, posed = 0, nomads = 0, births = 0, starved = 0, cold = 0, other = 0, died = -1;
        for (int year = 1; year <= years; year++)
        {
            if (year == 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            if (arm == "letters" && year >= from && world.Population > 0)
            {
                posed += PoseRelatives(world, 0, Env("ZZ_SPREAD", 0), posedHouseholds, firstQualified, posedIds);
                foreach (int pid in posedIds)
                {
                    if (seen.Add(pid))
                    {
                        alive.Add(pid);
                    }
                }
            }

            for (int season = 0; season < 4; season++)
            {
                loop.Step(config.TicksPerSeason);
                peak = Math.Max(peak, world.Population);
                foreach (Villager v in world.Villagers)
                {
                    if (seen.Add(v.Id))
                    {
                        alive.Add(v.Id);
                        if (v.ArrivedAtTick > 0UL)
                        {
                            nomads++;
                        }
                        else
                        {
                            births++;
                        }
                    }

                    if (!v.Alive && alive.Remove(v.Id) && v.State != VillagerState.Left)
                    {
                        _ = v.CauseOfDeath switch
                        {
                            CauseOfDeath.Starvation => starved++,
                            CauseOfDeath.Cold => cold++,
                            _ => other++,
                        };
                    }
                }
            }

            if (died < 0 && world.Population == 0)
            {
                died = year;
            }
        }

        _o.WriteLine(
            $"ZZB {arm} {seed} alive {world.Population} peak {peak} starved {starved} cold {cold} other {other} "
            + $"nomads {nomads} posed {posed} births {births} died {died} food {FoodHeld(world)}");
    }

    public static IEnumerable<object[]> Valleys()
    {
        if (Environment.GetEnvironmentVariable("ZZ_VALLEYS") is null)
        {
            yield return new object[] { string.Empty, 0UL };
            yield break;
        }

        IEnumerable<ulong> seeds = Enumerable.Range(200, 100).Select(x => (ulong)x);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string zs)
        {
            seeds = zs.Split(',').Select(x => ulong.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        }

        foreach (string arm in (Environment.GetEnvironmentVariable("ZZ_ARMS") ?? "base,letters").Split(','))
        {
            foreach (ulong seed in seeds)
            {
                yield return new object[] { arm, seed };
            }
        }
    }

    private static int Env(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : fallback;

    public static IEnumerable<object[]> Runs()
    {
        if (Environment.GetEnvironmentVariable("ZZ_SAVES") is not string folder)
        {
            yield return new object[] { string.Empty };
            yield break;
        }

        string? shard = Environment.GetEnvironmentVariable("ZZ_SHARD");
        int k = 0, n = 1;
        if (shard is not null && shard.Split('/') is [string a, string b])
        {
            k = int.Parse(a, System.Globalization.CultureInfo.InvariantCulture);
            n = int.Parse(b, System.Globalization.CultureInfo.InvariantCulture);
        }

        int index = 0;
        foreach (string dir in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.Ordinal))
        {
            string? newest = Directory.GetFiles(dir, "*.save")
                .OrderByDescending(File.GetLastWriteTimeUtc).ThenBy(p => p, StringComparer.Ordinal).FirstOrDefault();
            if (newest is not null && index++ % n == k)
            {
                yield return new object[] { newest };
            }
        }
    }
}
