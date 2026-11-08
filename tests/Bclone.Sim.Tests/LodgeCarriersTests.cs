using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// `specs/hunting.md §6a`, D683 — the stalled lodge. Joe's Hollowmere, Year 25: a lodge holding 2,184 meat and 636 on
/// the shelves met the 2,500 meat row by themselves (the limit counts every buffer, D420), the met limit stood the
/// hunters down, nobody but the producer and the marketer clears a buffer (D370), and one carrier at a time drained
/// the lodge an armful a trip over years while two laborers stood idle. His pick: <i>"Hunters carry it in."</i> While a
/// lodge is swollen (food at or above half its room, D394) a met meat limit keeps every seat of it and the hunters
/// carry instead of hunting, all at once; below half the old rule holds and the marketer takes the rest.
/// </summary>
public sealed class LodgeCarriersTests
{
    private readonly ITestOutputHelper _output;

    public LodgeCarriersTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>
    /// Joe's lodge under a met limit, posed the way the fisher's and the leather guards pose a hut (D664's trap): the
    /// village settled a year with a lodge, one marketer, every other trade at nought and the hunters as typed —
    /// <c>null</c> is <i>the village decides</i>, the state Joe played in — every larder full, half the shelves cleared
    /// so storage has room (D370's premise), the lodge holding exactly <paramref name="meat"/> and the meat row set to
    /// <paramref name="limit"/>, which the lodge alone meets. ⭐ D708: the leather row is set too — met by what the village
    /// holds when <paramref name="leatherMet"/> (D683's poses, where nothing but the carrying is wanted), five hundred short
    /// otherwise (the hides keep the hunters out).
    /// </summary>
    private static (SimLoop Loop, Workplace Lodge) ALodgeUnderAMetLimit(int meat, int limit, int? huntersTyped, bool leatherMet = true)
    {
        // Eight founders, not four: the carriers are asked at the marketer's rank, after the foragers and the
        // woodcutter the derivation wants first (a typed nought is applied after the hands are shared out, `Asked`),
        // and four hands leave one for the lodge.
        SimConfig config = Config with { StartingHouseholds = 4 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        loop.Step(config.TicksPerYear + 1);

        foreach (JobKind kind in JobLimits.Kinds)
        {
            int? typed = kind == JobKind.Marketer ? 1 : kind == JobKind.Hunter ? huntersTyped : 0;
            world.SetJobLimit(kind, typed);
        }

        foreach (Household household in world.Households)
        {
            int wanted = world.TargetFoodFor(household);
            if (world.FoodIn(household.Stockpile) < wanted)
            {
                household.Stockpile.Add(Goods.Produce, wanted);
            }
        }

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            foreach (Goods goods in new[] { Goods.Produce, Goods.Logs, Goods.Firewood, Goods.Meat, Goods.Leather })
            {
                store.Store.TryTake(goods, store.Store[goods] / 2);
            }
        }

        lodge.Store.TakeAll(Goods.Meat);
        lodge.Store.TakeAll(Goods.Leather);
        lodge.Store.Add(Goods.Meat, meat);
        Assert.Equal(meat, lodge.Store[Goods.Meat]);
        Assert.True(world.SomewhereToPut(Goods.Meat), "the premise: storage with room for the meat");
        Assert.True(world.SetStockLimit(Goods.Meat, limit).Allowed);
        Assert.True(world.LimitIsMet(Goods.Meat), $"the premise: the meat row is met ({world.HeldAgainstItsLimit(Goods.Meat)} held against {limit})");
        int leather = world.HeldAgainstItsLimit(Goods.Leather);
        Assert.True(world.SetStockLimit(Goods.Leather, leatherMet ? leather : leather + 500).Allowed);
        Assert.Equal(leatherMet, world.LimitIsMet(Goods.Leather));
        return (loop, lodge);
    }

    /// <summary>A hunter walking to the lodge for an armful — the leg D370's one claim is on.</summary>
    private static bool IsWalkingToTheLodge(Villager hunter, Workplace lodge) =>
        hunter.State == VillagerState.ClearingABuffer && hunter.ErrandX == lodge.Tile.X && hunter.ErrandY == lodge.Tile.Y;

    /// <summary>A hunter walking to the lodge for an armful, or carrying one away from it.</summary>
    private static bool IsCarrying(Villager hunter, Workplace lodge) =>
        IsWalkingToTheLodge(hunter, lodge)
        || (hunter.State == VillagerState.HaulingToStore && hunter.Carried[Goods.Meat] > 0);

    /// <summary>
    /// The allocator's own slack pass, run now rather than waited for: the game runs it every <c>labour_slack_ticks</c>
    /// (a month in the fixture), and two hunters already seated drain a posed lodge below half in less than that — so a
    /// guard that waited for the pass watched the quota fall to nought before anybody new was seated. The claim is
    /// about the seats and the carrying, not the cadence.
    /// </summary>
    private static void SeatThem(SimWorld world) => LabourAllocator.TakeUpSlack(world);

    /// <summary>
    /// ⭐⭐ Joe's state: the lodge swollen and the meat row met. Every seat is kept, every hunter carries, all at once,
    /// the lodge is below half within two seasons, and then the seats are released.
    /// </summary>
    [Fact]
    public void AMetMeatLimitKeepsTheHuntersCarryingWhileTheLodgeIsSwollen()
    {
        (SimLoop loop, Workplace lodge) = ALodgeUnderAMetLimit(meat: 2_000, limit: 1_500, huntersTyped: null);
        SimWorld world = loop.World;
        SimConfig config = world.Config;
        Assert.True(world.BufferIsSwollen(lodge), "the premise: 2,000 meat in a lodge of " + lodge.Store.Capacity + " is swollen");

        // The quota asks for every seat of the swollen lodge, not none — and does not report the trade as capped.
        LabourQuota quota = LabourQuota.For(world);
        _output.WriteLine($"posed: the village wants {quota.For(JobKind.Hunter)} hunters of {lodge.Places} seats; wants none because: {LabourQuota.WhyTheVillageWantsNone(world, JobKind.Hunter) ?? "(it wants some)"}");
        Assert.Equal(lodge.Places, quota.For(JobKind.Hunter));
        Assert.Equal(lodge.Places, quota.Needed(JobKind.Hunter));
        Assert.Null(LabourQuota.WhyTheVillageWantsNone(world, JobKind.Hunter));

        SeatThem(world);
        _output.WriteLine($"{lodge.WorkerIds.Count} of {lodge.Places} seated by the slack pass");
        Assert.Equal(lodge.Places, lodge.WorkerIds.Count);

        // Every hunter carries, and at some tick all of them are walking to the lodge together — D370's one claim is
        // on the walk to the hut, not on the haul away from it, so the haul leg is what to leave out of the count.
        var carried = new HashSet<int>();
        int atOnce = 0;
        int hunted = 0;
        int tick = 0;
        for (; tick < config.TicksPerSeason * 2 && world.BufferIsSwollen(lodge); tick++)
        {
            loop.StepOnce();
            int now = 0;
            foreach (int id in lodge.WorkerIds)
            {
                Villager hunter = world.FindVillager(id)!;
                if (IsCarrying(hunter, lodge))
                {
                    carried.Add(id);
                }

                if (IsWalkingToTheLodge(hunter, lodge))
                {
                    now++;
                }

                if (hunter.State is VillagerState.Hunting or VillagerState.TravelingToGame)
                {
                    hunted++;
                }
            }

            atOnce = Math.Max(atOnce, now);
        }

        _output.WriteLine($"tick {tick}: the lodge holds {lodge.Store[Goods.Meat]} meat; {carried.Count} hunters carried, at most {atOnce} walking to the lodge at once; {hunted} hunter-ticks hunting");
        Assert.False(world.BufferIsSwollen(lodge), $"two seasons on the lodge still holds {lodge.Store[Goods.Meat]} meat — the hunters did not drain it");
        Assert.Equal(lodge.Places, carried.Count);

        // Two of the three set off together (the third was mid-errand when the pass seated them); under D370's one claim
        // it reads exactly 1, which is the red check (M2).
        Assert.True(atOnce >= 2, $"only {atOnce} hunter at a time ever walked to the swollen lodge — D370's one claim is still being asked");
        Assert.Equal(0, hunted);

        // Below half the old rule holds: the row is still met, and the seats are released at the next slack pass.
        Assert.True(world.LimitIsMet(Goods.Meat), "the premise moved: the meat row stopped being met");
        Assert.Equal(0, LabourQuota.For(world).For(JobKind.Hunter));
        int released = 0;
        for (; released < config.TicksPerSeason && lodge.WorkerIds.Count > 0; released++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"seats released {released} ticks after the lodge fell below half");
        Assert.Empty(lodge.WorkerIds);
    }

    /// <summary>The old rule, kept: a lodge below half under a met limit still stands the hunters down (D370, D409).</summary>
    [Fact]
    public void ALodgeBelowHalfUnderAMetLimitStillStandsTheHuntersDown()
    {
        (SimLoop loop, Workplace lodge) = ALodgeUnderAMetLimit(meat: 1_000, limit: 500, huntersTyped: null);
        SimWorld world = loop.World;
        Assert.False(world.BufferIsSwollen(lodge), "the premise: 1,000 meat in a lodge of " + lodge.Store.Capacity + " is not swollen");
        Assert.True(world.BufferWorthClearing(lodge), "the premise: an armful waits");

        LabourQuota quota = LabourQuota.For(world);
        string? why = LabourQuota.WhyTheVillageWantsNone(world, JobKind.Hunter);
        _output.WriteLine($"the village wants {quota.For(JobKind.Hunter)} hunters: {why}");
        Assert.Equal(0, quota.For(JobKind.Hunter));
        Assert.Equal(0, quota.Needed(JobKind.Hunter));
        Assert.Contains("meat limit of 500 is met", why);

        loop.Step(world.Config.TicksPerSeason);
        Assert.Equal(0, LabourQuota.For(world).For(JobKind.Hunter));
        Assert.Empty(lodge.WorkerIds);
    }

    /// <summary>
    /// D370's one claim per hut holds while the buffer is not swollen: three hunters the player typed (D238 keeps the
    /// seats under a met limit) clear a half-empty lodge one at a time.
    /// </summary>
    [Fact]
    public void OneCarrierAtATimeWhileTheLodgeIsNotSwollen()
    {
        (SimLoop loop, Workplace lodge) = ALodgeUnderAMetLimit(meat: 1_000, limit: 500, huntersTyped: 3);
        SimWorld world = loop.World;
        SimConfig config = world.Config;
        Assert.False(world.BufferIsSwollen(lodge));
        Assert.Equal(lodge.Places, LabourQuota.For(world).For(JobKind.Hunter));
        SeatThem(world);
        Assert.Equal(lodge.Places, lodge.WorkerIds.Count);

        int before = lodge.Store[Goods.Meat];
        int atOnce = 0;
        var carried = new HashSet<int>();
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            int now = 0;
            foreach (int id in lodge.WorkerIds)
            {
                Villager hunter = world.FindVillager(id)!;
                if (IsWalkingToTheLodge(hunter, lodge))
                {
                    now++;
                    carried.Add(id);
                }
            }

            atOnce = Math.Max(atOnce, now);
        }

        _output.WriteLine($"the lodge went {before} → {lodge.Store[Goods.Meat]}; {carried.Count} hunters walked to it, at most {atOnce} at once");
        Assert.True(lodge.Store[Goods.Meat] <= before - config.CarryCapacity, "nobody carried an armful out of the lodge");
        Assert.Equal(1, atOnce);
    }

    /// <summary>
    /// ⭐⭐ D708, Joe: <i>"if meat limit is met, keep hunters there for leather."</i> A lodge below half under a met meat row
    /// keeps every seat while leather is under its row, and the hunters go out and hunt — the hides come home, and a hunter
    /// says why they are out.
    /// </summary>
    [Fact]
    public void AMetMeatLimitKeepsTheHuntersOutForTheHidesWhileLeatherIsShort()
    {
        (SimLoop loop, Workplace lodge) = ALodgeUnderAMetLimit(meat: 1_000, limit: 500, huntersTyped: null, leatherMet: false);
        SimWorld world = loop.World;
        SimConfig config = world.Config;

        // D714: met, but under twice the row — the limit set to what the village holds, so the cap does not bind.
        Assert.True(world.SetStockLimit(Goods.Meat, world.HeldAgainstItsLimit(Goods.Meat)).Allowed);
        Assert.True(world.LimitIsMet(Goods.Meat));
        Assert.False(world.PastTheSecondGoodCap(Goods.Meat), "the premise: under twice the meat row");
        Assert.False(world.BufferIsSwollen(lodge), "the premise: not swollen, so D683's carriers do not keep the seats");
        Assert.True(world.KeptWorkingForItsSecondGood(JobKind.Hunter));

        LabourQuota quota = LabourQuota.For(world);
        _output.WriteLine($"the village wants {quota.For(JobKind.Hunter)} hunters of {lodge.Places} seats; needed {quota.Needed(JobKind.Hunter)}");
        Assert.Equal(lodge.Places, quota.For(JobKind.Hunter));
        Assert.Equal(lodge.Places, quota.Needed(JobKind.Hunter));
        Assert.Null(LabourQuota.WhyTheVillageWantsNone(world, JobKind.Hunter));

        SeatThem(world);
        Assert.Equal(lodge.Places, lodge.WorkerIds.Count);

        int leatherBefore = world.HeldAgainstItsLimit(Goods.Leather);
        int hunted = 0;
        string? said = null;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (int id in lodge.WorkerIds)
            {
                Villager hunter = world.FindVillager(id)!;
                if (hunter.State is VillagerState.Hunting or VillagerState.TravelingToGame)
                {
                    hunted++;
                    said ??= hunter.WorkNote;
                }
            }
        }

        int leatherAfter = world.HeldAgainstItsLimit(Goods.Leather);
        _output.WriteLine($"{hunted} hunter-ticks hunting in a season; leather {leatherBefore} → {leatherAfter}; said: {said}");
        Assert.True(hunted > 0, "nobody hunted under a met meat row with leather short");
        Assert.True(leatherAfter > leatherBefore, "no hide came home");
        Assert.Contains("but only", said);
        Assert.Contains("leather it asked for", said);
    }

    /// <summary>
    /// ⭐ D714, Joe: <i>"Cap at 2× meat limit."</i> His Elderwood ran the meat to twice its 2,500 with hunters out for hides
    /// (~69 meat a hide). With leather short but the village holding twice its meat row, nobody is kept for the hides,
    /// nobody hunts in a season, and the hunter's sentence names the cap.
    /// </summary>
    [Fact]
    public void PastTwiceTheMeatRowTheHidesWait()
    {
        (SimLoop loop, Workplace lodge) = ALodgeUnderAMetLimit(meat: 1_000, limit: 500, huntersTyped: null, leatherMet: false);
        SimWorld world = loop.World;
        int held = world.HeldAgainstItsLimit(Goods.Meat);
        Assert.True(world.SetStockLimit(Goods.Meat, held / 2).Allowed);
        Assert.True(world.PastTheSecondGoodCap(Goods.Meat), $"the premise: {held} meat is twice a row of {held / 2}");
        Assert.False(world.LimitIsMet(Goods.Leather), "the premise: leather is short");
        Assert.False(world.BufferIsSwollen(lodge), "the premise: not swollen, so D683's carriers do not keep the seats");

        Assert.False(world.KeptWorkingForItsSecondGood(JobKind.Hunter));
        Assert.Equal(0, LabourQuota.For(world).For(JobKind.Hunter));
        Assert.Equal(0, LabourQuota.For(world).Needed(JobKind.Hunter));

        string? note = world.SecondGoodCapNote(JobKind.Hunter);
        _output.WriteLine(note ?? "(null)");
        Assert.NotNull(note);
        Assert.Contains("past twice its", note);
        Assert.Contains("the leather waits", note);

        int hunted = 0;
        for (int tick = 0; tick < world.Config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive && villager.State is VillagerState.Hunting or VillagerState.TravelingToGame)
                {
                    hunted++;
                }
            }
        }

        Assert.Equal(0, hunted);

        // Under the cap the D708 rule holds again, and the sentence says nothing about the cap.
        Assert.True(world.SetStockLimit(Goods.Meat, world.HeldAgainstItsLimit(Goods.Meat)).Allowed);
        Assert.Null(world.SecondGoodCapNote(JobKind.Hunter));
    }

    /// <summary>
    /// D708's other half: <i>"when leather and meat limits are met they can idle."</i> The same lodge with both rows met
    /// stands the hunters down — nobody seated, nobody hunting — and the trade is reported as capped.
    /// </summary>
    [Fact]
    public void WithTheMeatAndTheLeatherBothMetTheHuntersIdle()
    {
        (SimLoop loop, Workplace lodge) = ALodgeUnderAMetLimit(meat: 1_000, limit: 500, huntersTyped: null, leatherMet: true);
        SimWorld world = loop.World;
        Assert.False(world.KeptWorkingForItsSecondGood(JobKind.Hunter));
        Assert.Equal(0, LabourQuota.For(world).For(JobKind.Hunter));
        Assert.Equal(0, LabourQuota.For(world).Needed(JobKind.Hunter));
        Assert.NotNull(LabourQuota.WhyTheVillageWantsNone(world, JobKind.Hunter));

        int hunted = 0;
        for (int tick = 0; tick < world.Config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive && villager.State is VillagerState.Hunting or VillagerState.TravelingToGame)
                {
                    hunted++;
                }
            }
        }

        Assert.Equal(0, hunted);
        Assert.Empty(lodge.WorkerIds);
    }

    /// <summary>The lodge card names the reason: the limit, and that the hunters are carrying rather than hunting.</summary>
    [Fact]
    public void TheLodgeSaysTheHuntersAreCarryingRatherThanHunting()
    {
        (SimLoop loop, Workplace lodge) = ALodgeUnderAMetLimit(meat: 2_000, limit: 1_500, huntersTyped: null);
        SimWorld world = loop.World;

        string note = world.SwollenBufferNote(lodge);
        _output.WriteLine(note);
        Assert.Contains("1,500 meat", note);
        Assert.Contains("hunters are carrying it in rather than hunting", note);
        Assert.Contains("2,000 waiting", note);
        Assert.Null(world.IdleNote(lodge));

        // With the row cleared the card says what it said before (D394).
        Assert.True(world.SetStockLimit(Goods.Meat, null).Allowed);
        string plain = world.SwollenBufferNote(lodge);
        _output.WriteLine(plain);
        Assert.StartsWith("Working — 2,000 waiting to be carried in", plain);
    }

    /// <summary>
    /// A stood-down lodge with nobody seated says so — the card used to read <i>Working</i>, because `IdleNote` is
    /// silent for an empty building the village has no use for (D147) and the fallback said so.
    /// </summary>
    [Fact]
    public void AStoodDownLodgeSaysNobodyIsPosted()
    {
        // Just under half (1,350 of 2,700), so that a season of the marketer's armfuls still leaves one waiting.
        (SimLoop loop, Workplace lodge) = ALodgeUnderAMetLimit(meat: 1_300, limit: 500, huntersTyped: null);
        SimWorld world = loop.World;
        loop.Step(world.Config.TicksPerSeason);
        Assert.Empty(lodge.WorkerIds);
        Assert.Null(world.IdleNote(lodge));

        string? note = world.NobodyPostedNote(lodge);
        _output.WriteLine(note ?? "(null)");
        Assert.NotNull(note);
        Assert.StartsWith("Nobody is posted here — the meat limit of 500 is met.", note);
        Assert.Contains("The marketer carries in what is left", note);

        // And nothing while somebody is seated, or while the village wants hands here.
        world.SetJobLimit(JobKind.Hunter, 3);
        Assert.Null(world.NobodyPostedNote(lodge));
    }
}
