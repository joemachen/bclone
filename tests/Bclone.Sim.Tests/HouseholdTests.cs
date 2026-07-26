using Bclone.Sim.Config;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

public sealed class HouseholdTests
{
    private static SimConfig Config => Phase0Fixtures.Plenty;

    private static Household NewHousehold() => new()
    {
        Id = 1,
        Name = "Thatcher",
        HomePosition = new GridPos(0, 0),
    };

    [Fact]
    public void MembersAreKeptSortedRegardlessOfInsertionOrder()
    {
        // Membership order is iteration order, and iteration order is part of the
        // determinism contract. "Who eats first" must not depend on who moved in
        // first (spec §4b).
        var a = NewHousehold();
        var b = NewHousehold();

        foreach (int id in new[] { 7, 2, 9, 4 })
        {
            a.AddMember(id);
        }

        foreach (int id in new[] { 9, 4, 2, 7 })
        {
            b.AddMember(id);
        }

        Assert.Equal(new[] { 2, 4, 7, 9 }, a.MemberIds);
        Assert.Equal(a.MemberIds, b.MemberIds);
    }

    [Fact]
    public void AddingTheSameMemberTwiceIsANoOp()
    {
        var household = NewHousehold();
        household.AddMember(3);
        household.AddMember(3);

        Assert.Single(household.MemberIds);
    }

    [Fact]
    public void RemovingAMemberKeepsTheRestSorted()
    {
        var household = NewHousehold();
        foreach (int id in new[] { 1, 2, 3, 4 })
        {
            household.AddMember(id);
        }

        Assert.True(household.RemoveMember(2));
        Assert.Equal(new[] { 1, 3, 4 }, household.MemberIds);
    }

    [Fact]
    public void AHouseholdCanOutliveItsFamily()
    {
        var household = NewHousehold();
        household.AddMember(1);

        Assert.False(household.IsEmpty);
        household.RemoveMember(1);
        Assert.True(household.IsEmpty);
    }

    [Fact]
    public void FoodIsHeldPerHouseholdNotShared()
    {
        // The asymmetry decision D14 exists to create: one family can starve
        // beside a thriving neighbour.
        var poor = NewHousehold();
        var rich = new Household { Id = 2, Name = "Fletcher", HomePosition = new GridPos(9, 0) };

        rich.Stockpile.Add(80);

        Assert.Equal(0, poor.Stockpile.Food);
        Assert.Equal(80, rich.Stockpile.Food);
    }

    // ---------------------------------------------------------------
    //  Life stages
    // ---------------------------------------------------------------

    [Fact]
    public void ChildhoodIsAnAgeGate()
    {
        Assert.Equal(LifeStage.Child, AgeingSystem.StageForAge(0, 100, Config));
        Assert.Equal(LifeStage.Child, AgeingSystem.StageForAge(Config.AdultAge - 1, 100, Config));
        Assert.Equal(LifeStage.Adult, AgeingSystem.StageForAge(Config.AdultAge, 100, Config));
    }

    [Fact]
    public void ElderhoodTracksVigourNotASecondAgeThreshold()
    {
        // "Elder" must mean the same thing as the frailty already on screen, or the
        // UI ends up with two definitions of old that can disagree.
        Assert.Equal(LifeStage.Adult, AgeingSystem.StageForAge(40, 100, Config));
        Assert.Equal(LifeStage.Adult, AgeingSystem.StageForAge(40, AgeingSystem.FrailThreshold + 1, Config));
        Assert.Equal(LifeStage.Elder, AgeingSystem.StageForAge(40, AgeingSystem.FrailThreshold, Config));
    }

    [Fact]
    public void AFrailChildIsStillAChild()
    {
        // Age wins over vigour: a young villager cannot be an elder.
        Assert.Equal(LifeStage.Child, AgeingSystem.StageForAge(5, 55, Config));
    }

    [Fact]
    public void OnlyNonChildrenCanWork()
    {
        var child = new Villager { Id = 1, Name = "Tam", LifespanYears = 45, LifeStage = LifeStage.Child };
        var adult = new Villager { Id = 2, Name = "Bess", LifespanYears = 45, LifeStage = LifeStage.Adult };
        var elder = new Villager { Id = 3, Name = "Mabel", LifespanYears = 45, LifeStage = LifeStage.Elder };
        var dead = new Villager { Id = 4, Name = "Otto", LifespanYears = 45, LifeStage = LifeStage.Adult, Alive = false };

        Assert.False(child.CanWork);
        Assert.True(adult.CanWork);
        Assert.True(elder.CanWork, "An elder still works — declining vigour, not retirement.");
        Assert.False(dead.CanWork);
    }

    [Fact]
    public void LifeStageIsTrackedThroughAWholeLife()
    {
        var (loop, _) = Phase0Fixtures.Build(Config);
        var seen = new HashSet<LifeStage>();

        while (loop.World.Villager.Alive)
        {
            seen.Add(loop.World.Villager.LifeStage);
            loop.StepOnce();
        }

        // Phase 0's lone villager is not gated on this yet — the gate arrives with
        // households, which is what a child depends on.
        Assert.Contains(LifeStage.Child, seen);
        Assert.Contains(LifeStage.Adult, seen);
        Assert.Contains(LifeStage.Elder, seen);
    }
}
