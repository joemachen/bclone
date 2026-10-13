using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ THE NOMADS' MEASUREMENT HARNESS (specs/nomads.md §8) — kept in tools/harness/, COPIED into
// tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit. Never committed under tests/.
//
// Measured before any of it exists, by posing the arrivals from the test side:
//   ZZK — what a knock would meet: at Spring, Day 1 of Years 8, 10 and 12, the people alive, living households, how
//         many have a house, empty houses standing, food in stores (and per head), whole residential tiles painted,
//         whether the village already has nowhere to build, and the band the "a fifth, 2–8" rule would send.
//   ZZS — arm `settle`: ZZ_SETTLE people (default: the rule's band) arrive roofless at Spring, Year 10 in one or two
//         families with 20 food each in the larder; at the end, how many were housed and when, how many died and of what,
//         and in which year.
//   ZZD — a valley that died: what stands 0, 2, 4 and 6 years after the last death — by condition state and kind —
//         and the food and firewood still in the stores.
//   ZZR — arm `refound`: ZZ_REFOUND years (default 3) after the last death, four adults in two households (the founding's
//         shape) arrive at
//         the founding site and the cart is restocked with cart_food; then alive 1, 5, 10 and 20 years on, when they
//         were housed, and how the dead died.
//   ZZB — the usual survival line so dead.py reads it (alive at 50, peak, starved, cold).
// Arms: `knock` (unanswered), `settle`, `refound`. Seeds 200–299 (ZZ_WIDE=1's hundred); ZZ_SEEDS narrows.
public sealed class ZzNomads
{
    private readonly ITestOutputHelper _o;

    public ZzNomads(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Valleys))]
    public void Valley(string arm, ulong seed)
    {
        // The arms pose their own arrivals (D564's measurement); since D572 the sim settles bands itself, so the
        // switch is off here or every arm would count two arrivals. `arms.sh base` measures the sim's own.
        SimConfig config = ShippedConfig.Load() with { Seed = seed, NomadsEnabled = false };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        var limits = ShippedConfig.Load().StartingStockLimits;
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit)) { world.SetStockLimit((Goods)id, limit); }
        }

        ColdStartTests.PlayTheOpening(world);

        int tpy = config.TicksPerYear;
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "50");
        int refoundAfter = int.Parse(Environment.GetEnvironmentVariable("ZZ_REFOUND") ?? "3");
        int peak = 0;
        int deathYear = -1;
        int refoundYear = -1;
        var settlers = new List<int>();
        var settlerHouseholds = new List<int>();
        var housedIn = new Dictionary<int, int>();   // household id → first year it had a house
        var sampled = new HashSet<int>();

        // The horizon grows for a dead valley (its ruins are sampled six years on; a re-founding runs twenty more).
        int year = 1;
        while (true)
        {
            int end = years;
            if (deathYear > 0) { end = Math.Max(end, deathYear + 6); }
            if (refoundYear > 0) { end = Math.Max(end, refoundYear + 20); }
            if (year > end) { break; }

            if (year == 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            // Spring, Day 1 of this year — the tick a knock would land on.
            if (year is 8 or 10 or 12 && deathYear < 0)
            {
                int alive = world.Population;
                int band = Math.Clamp(alive / 5, 2, 8);
                int living = world.Households.Count(h => world.LivingMembersOf(h) > 0);
                int homed = world.Households.Count(h => world.LivingMembersOf(h) > 0 && h.HasHome);
                int empty = world.Households.Count(h => world.LivingMembersOf(h) == 0 && h.HasHome);
                int food = world.FoodTheVillageHolds();
                _o.WriteLine(
                    $"ZZK {arm} {seed} year {year} alive {alive} hh {living} homed {homed} empty-houses {empty} food {food} "
                    + $"per-head {(alive == 0 ? 0 : food / alive)} firewood {world.InStores(Goods.Firewood)} logs {world.InStores(Goods.Logs)} "
                    + $"res-tiles {world.Zones.WholeResidentialTiles.Count} nowhere-to-build {world.NeedsMoreResidentialLand} band {band}");
            }

            if (arm == "settle" && year == 10 && deathYear < 0)
            {
                int people = Environment.GetEnvironmentVariable("ZZ_SETTLE") is string zs ? int.Parse(zs) : Math.Clamp(world.Population / 5, 2, 8);
                Settle(world, config, people, people <= 4 ? 1 : 2, settlers, settlerHouseholds, founders: false);
            }

            if (arm == "refound" && deathYear > 0 && refoundYear < 0 && year == deathYear + refoundAfter)
            {
                // The founding's shape: two households of two adults (§4.6), never a family with children.
                Settle(world, config, 4, 2, settlers, settlerHouseholds, founders: true);
                int took = world.TheCart is StoreBuilding cart ? cart.Store.Receive(Goods.Produce, config.CartFood) : -1;
                refoundYear = year;
                _o.WriteLine($"ZZR {arm} {seed} refound-year {year} cart-took {took} "
                    + $"houses-standing {world.Households.Count(h => h.HasHome && !h.HomeClosed)} {Standing(world)}");
            }

            for (int season = 0; season < 4; season++)
            {
                loop.Step(config.TicksPerSeason);
                peak = Math.Max(peak, world.Population);
                foreach (int hh in settlerHouseholds)
                {
                    if (!housedIn.ContainsKey(hh) && world.Households.First(h => h.Id == hh).HasHome) { housedIn[hh] = year; }
                }

                if (deathYear < 0 && world.Population == 0 && world.Villagers.Count > 0) { deathYear = year; }
            }

            if (deathYear > 0 && refoundYear < 0)
            {
                int on = year - deathYear;
                if (on is 0 or 2 or 4 or 6 && sampled.Add(on))
                {
                    _o.WriteLine($"ZZD {arm} {seed} died-year {deathYear} on {on} food {world.FoodTheVillageHolds()} firewood {world.InStores(Goods.Firewood)} {Standing(world)}");
                }
            }

            if (refoundYear > 0 && (year - refoundYear) is 1 or 5 or 10 or 20)
            {
                _o.WriteLine($"ZZR {arm} {seed} on {year - refoundYear} alive {world.Population}");
            }

            year++;
        }

        if (settlers.Count > 0)
        {
            var died = world.Villagers.Where(v => settlers.Contains(v.Id) && !v.Alive).ToList();
            string deaths = string.Join(",", died.Select(v => $"{v.CauseOfDeath}@{(v.DiedAtTick ?? 0) / (ulong)tpy + 1}"));
            string housed = string.Join(",", settlerHouseholds.Select(hh => housedIn.TryGetValue(hh, out int y) ? y.ToString() : "never"));
            _o.WriteLine($"ZZS {arm} {seed} settled {settlers.Count} families {settlerHouseholds.Count} housed-years {housed} "
                + $"died {died.Count} starved {died.Count(v => v.CauseOfDeath == CauseOfDeath.Starvation)} cold {died.Count(v => v.CauseOfDeath == CauseOfDeath.Cold)} "
                + $"deaths {(deaths.Length == 0 ? "-" : deaths)} alive-now {world.Villagers.Count(v => settlers.Contains(v.Id) && v.Alive)}");
        }

        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        int cold = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        _o.WriteLine($"ZZB {arm} {seed} alive {world.Population} peak {peak} starved {starved} cold {cold} dead-year {deathYear} hash {StateHash.Compute(world)}");
    }

    /// <summary>People arrive roofless at the founding site, in one or two families, 20 food each in the larder.</summary>
    private static void Settle(SimWorld world, SimConfig config, int people, int families, List<int> settlers, List<int> households, bool founders)
    {
        int nextHousehold = world.Households.Max(h => h.Id) + 1;
        int nextVillager = world.Villagers.Max(v => v.Id) + 1;
        Point at = world.TheCart?.Position ?? Point.CentreOf(world.Map.FoundingSite);
        int left = people;
        for (int f = 0; f < families; f++)
        {
            int size = f == families - 1 ? left : people / families;
            left -= size;
            var household = new Household
            {
                Id = nextHousehold,
                Surname = $"Nomad{nextHousehold}",
                Stockpile = world.NewLarder(),
                DayForAChild = NameHash.DayForAChild(world.Seed, nextHousehold, config.DaysPerSeason * 4),
                HomePosition = null,
            };
            household.Stockpile.Add(Goods.Produce, 20 * size);
            world.Households.Add(household);
            households.Add(household.Id);
            for (int m = 0; m < size; m++)
            {
                bool adult = m < 2;
                int age = adult ? 25 - (3 * m) : 7 - (2 * (m - 2));
                int id = nextVillager++;
                var villager = new Villager
                {
                    Id = id,
                    Name = $"Nomad {id}",
                    LifespanYears = 67,
                    Carried = world.NewStockpile(),
                    BirthTick = (long)world.Tick - ((long)age * config.TicksPerYear),
                    AgeYears = age,
                    LifeStage = adult ? LifeStage.Adult : LifeStage.Child,
                    HouseholdId = household.Id,
                    Position = at,
                    Rhythm = (id * 7) % config.TicksPerDay,
                    Founder = founders,
                };
                villager.Surname = household.Surname;
                household.AddMember(id);
                world.Villagers.Add(villager);
                settlers.Add(id);
            }

            // ⛔ THE TWO ADULTS ARE A COUPLE, as the founding pairs its founders (`PairFounders`). Unpaired, they
            // re-paired across the band on the spring tick they arrived, took over the dead households whole
            // (`FindAnEmptyHome(aHouseOnly: false)`) and left the larder this pose had filled behind — the first
            // run read "never housed" off the households they had walked out of.
            int first = settlers[settlers.Count - size];
            Villager a = world.Villagers.First(v => v.Id == first);
            Villager b = world.Villagers.First(v => v.Id == first + 1);
            a.PartnerId = b.Id;
            b.PartnerId = a.Id;

            nextHousehold++;
        }
    }

    /// <summary>Everything standing, counted by condition state, and by kind with its state.</summary>
    private static string Standing(SimWorld world)
    {
        var states = new int[4];
        var byKind = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
        void Count(string kind, Condition c)
        {
            states[(int)c]++;
            if (!byKind.TryGetValue(kind, out int[]? arr)) { arr = new int[4]; byKind[kind] = arr; }
            arr[(int)c]++;
        }

        foreach (Workplace w in world.Workplaces)
        {
            if (w.IsSite || world.JobsCatalog.WorksAt(w.Kind) is not BuildingKind kind || kind == BuildingKind.Market) { continue; }
            Count(kind.ToString(), world.ConditionOf(w));
        }

        foreach (StoreBuilding s in world.StoreBuildings)
        {
            BuildingKind? kind = s.RaisedAs ?? world.BuildingKindOf(s);
            if (kind is null || kind == BuildingKind.Pile) { continue; }
            Count(kind.Value.ToString(), world.ConditionOf(s));
        }

        foreach (Library l in world.Libraries) { Count("Library", world.ConditionOf(l)); }
        foreach (Well w in world.Wells) { Count(w.Kind.ToString(), world.ConditionOf(w)); }
        if (world.TownHall is TownHall hall) { Count("TownHall", world.ConditionOf(hall)); }
        foreach (Household h in world.Households) { if (h.HasHome) { Count("Home", world.ConditionOf(h)); } }
        foreach (Bridge b in world.Bridges) { Count(b.Kind.ToString(), world.ConditionOf(b)); }

        string kinds = string.Join(" ", byKind.Select(kv => $"{kv.Key}:{kv.Value[0]}/{kv.Value[1]}/{kv.Value[2]}/{kv.Value[3]}"));
        return $"standing {states.Sum()} sound {states[0]} worn {states[1]} failing {states[2]} ruin {states[3]} | {kinds}";
    }

    public static IEnumerable<object[]> Valleys()
    {
        IEnumerable<ulong> seeds = Enumerable.Range(200, 100).Select(x => (ulong)x);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string zs) { seeds = zs.Split(',').Select(ulong.Parse).ToArray(); }
        string[] arms = (Environment.GetEnvironmentVariable("ZZ_ARMS") ?? "knock,settle,refound").Split(',');
        foreach (string arm in arms)
        {
            foreach (ulong s in seeds) { yield return new object[] { arm, s }; }
        }
    }
}
