using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D799's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED from there
// before any commit (D420). `morale.md §13` step 1: how often each of W2's causes is met, per adult a season, before a
// weight or a line is typed — and so where a line falls that a well-run village never crosses and a neglected one does.
//
// The causes, read over each season for every living adult (`CanWork`), as `morale.md §4.1` lists them for W2:
//   noroof   — the household had no house on any day of the season;
//   cabin    — the house standing at the season's end is the cabin (recorded, not counted — §4's lever rule: a cause the
//              player cannot yet answer is forbidden, and the cottage's gate is the family tree);
//   cold     — winter only: days the person reached the seek-shelter line, a cause at ZZ_COLDDAYS (3) — the third arm;
//   fireless — winter only: a day housed with no firewood at home;
//   commute  — `CommuteNote` said at the season's end;
//   dry      — a day housed with an empty jar;
//   death    — someone of the household, or the partner, died in the last four seasons.
// "met" is how many of noroof, cold, fireless, commute, dry and death held that season; a streak is consecutive seasons with
// met ≥ k. warnK counts the distinct adults who ever held a streak of 4 (a year: *"thinking of leaving"*, D798) and leaveK
// a streak of 8 (two years: gone), for k = 1..4.
//
// Two modes. Valleys (default): ZzWell's opening — the shipped cold start, the opening played, a granary and a warehouse at
// Year 3 — on seeds 200–299 (ZZ_SEEDS=a,b narrows), ZZ_YEARS (50). Saves: ZZ_SAVES=<folder, a COPY of Joe's saves>,
// ZZ_PICK (autosave-1.save), run on ZZ_YEARS (10).
//
// One ZZC line a run. Copy in; `dotnet test tests/Kith.Sim.Tests -c Release --filter ZzContentment --logger
// "console;verbosity=detailed"`; delete.
public sealed class ZzContentment
{
    private readonly ITestOutputHelper _o;

    public ZzContentment(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string what)
    {
        if (what.Length == 0)
        {
            return;
        }

        SimLoop loop;
        int years;
        bool valley = !what.EndsWith(".save", StringComparison.Ordinal);
        if (valley)
        {
            years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "50", System.Globalization.CultureInfo.InvariantCulture);
            SimConfig config = ShippedConfig.Load() with { Seed = ulong.Parse(what, System.Globalization.CultureInfo.InvariantCulture) };
            loop = SimFactory.CreatePhase0(config, new Kith.Sim.Logging.InMemoryLogSink());
            ColdStartTests.PlayTheOpening(loop.World);
        }
        else
        {
            years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "10", System.Globalization.CultureInfo.InvariantCulture);
            SaveOpened opened = SaveFile.Open(what, ShippedConfig.Load());
            if (opened.Loop is null)
            {
                _o.WriteLine($"ZZC refused {what} {opened.Refusal} {opened.Detail}");
                return;
            }

            loop = opened.Loop;
        }

        SimWorld world = loop.World;
        SimConfig c = world.Config;
        int coldLine = int.Parse(Environment.GetEnvironmentVariable("ZZ_COLDDAYS") ?? "3", System.Globalization.CultureInfo.InvariantCulture);
        long seasonTicks = (long)c.DaysPerSeason * c.TicksPerDay;
        int startYear = world.Clock.Year;
        // D799's second arm: the walk counts only from ZZ_WALKTILES (unset: every note). walk is the adult-seasons
        // by the note's tiles: none, 8–11, 12–15, 16–23, 24+.
        int commuteLine = int.Parse(Environment.GetEnvironmentVariable("ZZ_WALKTILES") ?? "1", System.Globalization.CultureInfo.InvariantCulture);
        var walk = new long[5];
        // D799's third arm: a day is cold if the person reached the seek-shelter line (HearthSystem's warnAt) at any tick —
        // the day they had to break off and go in to get warm; ZZ_COLDDAYS (3) such days make a cold winter. chill is the
        // adult-winters by those days: 0, 1–2, 3–5, 6–10, 11+.
        int warnAt = c.ExposureThreshold * c.SeekShelterPercent / 100;
        var chilledToday = new HashSet<int>();
        var chill = new long[5];
        // And a rolling memory beside the streak: the causes summed over a person's last four seasons, unsettled at ≥ L
        // (L = 2..6), warned after four unsettled seasons running and gone after eight — so a cold winter is remembered
        // into the spring, which a streak of consecutive seasons forgets.
        var memory = new Dictionary<int, Queue<int>>();
        var rollStreak = new Dictionary<int, int[]>();
        var rollWarned = new HashSet<(int, int)>();
        var rollLeft = new HashSet<(int, int)>();
        // D799's fourth arm, the rule as recommended: a cause HELD — the roof and the walk while true; a cold winter
        // (ZZ_COLDWINTER, 6 days at the get-warm line) and a winter with the fire out for the four seasons after it; a
        // death for four seasons; the jar only at ZZ_DRYDAYS (3) dry days in a season. Unsettled at ≥ k held (k = 1..3),
        // warned after four unsettled seasons running, gone after eight.
        int coldWinter = int.Parse(Environment.GetEnvironmentVariable("ZZ_COLDWINTER") ?? "6", System.Globalization.CultureInfo.InvariantCulture);
        int dryLine = int.Parse(Environment.GetEnvironmentVariable("ZZ_DRYDAYS") ?? "3", System.Globalization.CultureInfo.InvariantCulture);
        var dryDays = new Dictionary<int, int>();
        var coldUntil = new Dictionary<int, int>();
        var fireUntil = new Dictionary<int, int>();
        var heldStreak = new Dictionary<int, int[]>();
        var heldWarned = new HashSet<(int, int)>();
        var heldLeft = new HashSet<(int, int)>();
        var held = new long[5];
        int seasonNo = 0;

        var noRoof = new HashSet<int>();
        var fireless = new HashSet<int>();
        var dry = new HashSet<int>();
        var coldDays = new Dictionary<int, int>();
        var streak = new Dictionary<int, int[]>();
        var warned = new HashSet<(int, int)>();
        var left = new HashSet<(int, int)>();
        var adultsSeen = new HashSet<int>();
        long aseasons = 0, nNoRoof = 0, nCabin = 0, nCold = 0, nFireless = 0, nCommute = 0, nDry = 0, nDeath = 0;
        var met = new long[7];
        int peak = 0;
        Season season = world.Clock.Season;

        for (long t = 0; t < (long)c.TicksPerYear * years; t++)
        {
            if (valley && t == c.TicksPerYear * 3)
            {
                MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            loop.StepOnce();
            peak = Math.Max(peak, world.Population);
            if (world.Clock.Season == Season.Winter)
            {
                foreach (Villager v in world.Villagers)
                {
                    if (v.CanWork && v.Cold >= warnAt)
                    {
                        chilledToday.Add(v.Id);
                    }
                }
            }
            if (world.Tick % (ulong)c.TicksPerDay == 0UL)
            {
                foreach (Household h in world.Households)
                {
                    if (world.LivingMembersOf(h) == 0)
                    {
                        continue;
                    }

                    if (!h.HasHome)
                    {
                        noRoof.Add(h.Id);
                        continue;
                    }

                    if (h.Water <= 0)
                    {
                        dry.Add(h.Id);
                        dryDays[h.Id] = dryDays.GetValueOrDefault(h.Id) + 1;
                    }

                    if (world.Clock.Season == Season.Winter && h.Stockpile.Firewood == 0)
                    {
                        fireless.Add(h.Id);
                    }
                }

                foreach (int vid in chilledToday)
                {
                    coldDays[vid] = coldDays.GetValueOrDefault(vid) + 1;
                }

                chilledToday.Clear();
            }

            if (world.Clock.Season == season)
            {
                continue;
            }

            // The season turned: read the one just ended.
            ulong since = world.Tick > (ulong)(4 * seasonTicks) ? world.Tick - (ulong)(4 * seasonTicks) : 0UL;
            foreach (Villager v in world.Villagers)
            {
                if (!v.CanWork)
                {
                    continue;
                }

                adultsSeen.Add(v.Id);
                aseasons++;
                Household? h = world.Households.FirstOrDefault(x => x.Id == v.HouseholdId);
                bool a = h is not null && noRoof.Contains(h.Id);
                bool cabin = h is { HasHome: true } && h.HomeKind == BuildingKind.Home;
                bool cold = season == Season.Winter && coldDays.GetValueOrDefault(v.Id) >= coldLine;
                bool fire = season == Season.Winter && h is not null && fireless.Contains(h.Id);
                int tiles = CommuteTiles(v.CommuteNote);
                bool commute = tiles >= commuteLine;
                walk[tiles == 0 ? 0 : tiles < 12 ? 1 : tiles < 16 ? 2 : tiles < 24 ? 3 : 4]++;
                bool d = h is not null && dry.Contains(h.Id);
                bool death = world.Villagers.Any(o => !o.Alive && o.DiedAtTick is ulong at && at >= since
                    && (o.HouseholdId == v.HouseholdId || o.Id == v.PartnerId));
                int n = (a ? 1 : 0) + (cold ? 1 : 0) + (fire ? 1 : 0) + (commute ? 1 : 0) + (d ? 1 : 0) + (death ? 1 : 0);
                nNoRoof += a ? 1 : 0;
                nCabin += cabin ? 1 : 0;
                nCold += cold ? 1 : 0;
                nFireless += fire ? 1 : 0;
                nCommute += commute ? 1 : 0;
                nDry += d ? 1 : 0;
                nDeath += death ? 1 : 0;
                met[Math.Min(n, met.Length - 1)]++;
                if (season == Season.Winter)
                {
                    int cd = coldDays.GetValueOrDefault(v.Id);
                    chill[cd == 0 ? 0 : cd < 3 ? 1 : cd < 6 ? 2 : cd < 11 ? 3 : 4]++;
                }

                Queue<int> mem = memory.TryGetValue(v.Id, out Queue<int>? m) ? m : memory[v.Id] = new Queue<int>();
                mem.Enqueue(n);
                if (mem.Count > 4)
                {
                    mem.Dequeue();
                }

                int score = mem.Sum();
                int[] rs = rollStreak.TryGetValue(v.Id, out int[]? rh) ? rh : rollStreak[v.Id] = new int[7];
                for (int l = 2; l <= 6; l++)
                {
                    rs[l] = score >= l ? rs[l] + 1 : 0;
                    if (rs[l] >= 4)
                    {
                        rollWarned.Add((v.Id, l));
                    }

                    if (rs[l] >= 8)
                    {
                        rollLeft.Add((v.Id, l));
                    }
                }

                if (season == Season.Winter && coldDays.GetValueOrDefault(v.Id) >= coldWinter)
                {
                    coldUntil[v.Id] = seasonNo + 4;
                }

                if (fire && h is not null)
                {
                    fireUntil[h.Id] = seasonNo + 4;
                }

                int hn = (a ? 1 : 0) + (tiles >= 24 ? 1 : 0)
                    + (coldUntil.GetValueOrDefault(v.Id) > seasonNo ? 1 : 0)
                    + (h is not null && fireUntil.GetValueOrDefault(h.Id) > seasonNo ? 1 : 0)
                    + (h is not null && dryDays.GetValueOrDefault(h.Id) >= dryLine ? 1 : 0)
                    + (death ? 1 : 0);
                held[Math.Min(hn, held.Length - 1)]++;
                int[] hs = heldStreak.TryGetValue(v.Id, out int[]? hh) ? hh : heldStreak[v.Id] = new int[4];
                for (int k = 1; k <= 3; k++)
                {
                    hs[k] = hn >= k ? hs[k] + 1 : 0;
                    if (hs[k] >= 4)
                    {
                        heldWarned.Add((v.Id, k));
                    }

                    if (hs[k] >= 8)
                    {
                        heldLeft.Add((v.Id, k));
                    }
                }

                int[] s = streak.TryGetValue(v.Id, out int[]? had) ? had : streak[v.Id] = new int[5];
                for (int k = 1; k <= 4; k++)
                {
                    s[k] = n >= k ? s[k] + 1 : 0;
                    if (s[k] >= 4)
                    {
                        warned.Add((v.Id, k));
                    }

                    if (s[k] >= 8)
                    {
                        left.Add((v.Id, k));
                    }
                }
            }

            noRoof.Clear();
            fireless.Clear();
            dry.Clear();
            coldDays.Clear();
            dryDays.Clear();
            seasonNo++;
            season = world.Clock.Season;
        }

        string id = valley ? what : Path.GetFileName(Path.GetDirectoryName(what)) ?? what;
        int born = world.Villagers.Count(v => v.BirthTick > 0);
        _o.WriteLine(
            $"ZZC {(valley ? "valley" : "save")} {id} from Y{startYear} years {years} alive {world.Population} peak {peak} born {born} "
            + $"adults {adultsSeen.Count} aseasons {aseasons} noroof {nNoRoof} cabin {nCabin} cold {nCold} fireless {nFireless} "
            + $"commute {nCommute} dry {nDry} death {nDeath} "
            + $"met {string.Join(",", met)} walk {string.Join(",", walk)} chill {string.Join(",", chill)} held {string.Join(",", held)} "
            + string.Join(" ", Enumerable.Range(1, 3).Select(k => $"hwarn{k} {heldWarned.Count(x => x.Item2 == k)} hleave{k} {heldLeft.Count(x => x.Item2 == k)}")) + " "
            + string.Join(" ", Enumerable.Range(2, 5).Select(l => $"rwarn{l} {rollWarned.Count(x => x.Item2 == l)} rleave{l} {rollLeft.Count(x => x.Item2 == l)}")) + " "
            + string.Join(" ", Enumerable.Range(1, 4).Select(k => $"warn{k} {warned.Count(x => x.Item2 == k)} leave{k} {left.Count(x => x.Item2 == k)}")));
    }

    // "It is 14 tiles to …" — the note's own number (LabourAllocator.DescribeTheCommute); 0 when there is no note.
    private static int CommuteTiles(string note)
    {
        const string head = "It is ";
        if (!note.StartsWith(head, StringComparison.Ordinal))
        {
            return 0;
        }

        int end = note.IndexOf(' ', head.Length);
        return end > head.Length && int.TryParse(note.AsSpan(head.Length, end - head.Length), out int n) ? n : 0;
    }

    private static void MarkSomewhereNear(SimWorld world, BuildingKind kind, GridPos site, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.Mark(kind, new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    return;
                }
            }
        }
    }

    public static IEnumerable<object[]> Runs()
    {
        if (Environment.GetEnvironmentVariable("ZZ_SAVES") is string folder)
        {
            string pick = Environment.GetEnvironmentVariable("ZZ_PICK") ?? "autosave-1.save";
            foreach (string p in Directory.GetFiles(folder, pick, SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                yield return new object[] { p };
            }

            yield break;
        }

        IEnumerable<int> seeds = Enumerable.Range(200, 100);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string listed)
        {
            seeds = listed.Split(',').Select(int.Parse);
        }

        foreach (int s in seeds)
        {
            yield return new object[] { s.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        }
    }
}
