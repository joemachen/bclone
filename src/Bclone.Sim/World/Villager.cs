namespace Bclone.Sim.World;

/// <summary>A position on the abstract map. Integers only — see decision D2.</summary>
public readonly record struct GridPos(int X, int Y)
{
    /// <summary>
    /// Manhattan distance. Deliberately not straight-line: a square root would put a
    /// float in the middle of travel-cost arithmetic, and travel cost feeds movement,
    /// which feeds sim state.
    /// </summary>
    public int ManhattanDistanceTo(GridPos other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>One axis-aligned step toward <paramref name="target"/>. X first, then
    /// Y — a fixed rule, because "pick the nearer axis" would need a tiebreak and
    /// tiebreaks are where determinism bugs live.</summary>
    public GridPos StepToward(GridPos target)
    {
        if (X != target.X)
        {
            return this with { X = X + Math.Sign(target.X - X) };
        }

        if (Y != target.Y)
        {
            return this with { Y = Y + Math.Sign(target.Y - Y) };
        }

        return this;
    }

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// One villager — a person with a name and a history, not a headcount
/// (DESIGN.md §1.4).
/// </summary>
public sealed class Villager
{
    public required int Id { get; init; }

    /// <summary>"Mabel", never "Villager_01". The story depends on this.</summary>
    public required string Name { get; init; }

    /// <summary>The year they would die of old age, drawn once at birth.</summary>
    public required int LifespanYears { get; init; }

    /// <summary>Which household they belong to — where they live and whose food they eat.</summary>
    public int HouseholdId { get; set; }

    /// <summary>
    /// The villager they founded a household with, or 0 if unpaired.
    /// </summary>
    /// <remarks>
    /// Only a couple has children, which is what stops siblings breeding in the
    /// parental home and forces the village to actually form new households in
    /// order to grow.
    /// </remarks>
    public int PartnerId { get; set; }

    /// <summary>True once they have a partner.</summary>
    public bool IsPaired => PartnerId != 0;

    /// <summary>
    /// What they are capable of, derived from age each tick by <c>AgeingSystem</c>.
    /// </summary>
    public LifeStage LifeStage { get; set; } = LifeStage.Adult;

    /// <summary>True when they are old enough to do a day's work.</summary>
    public bool CanWork => Alive && LifeStage != LifeStage.Child;

    /// <summary>
    /// The in-game year they were born. Zero for founders, who arrive already grown
    /// and whose age therefore tracks the calendar directly.
    /// </summary>
    public int BirthYear { get; init; }

    /// <summary>Years lived. Advances on the new-year boundary.</summary>
    public int AgeYears { get; set; }

    /// <summary>
    /// Physical capability, 0–100. Full through the prime years, then declining
    /// with age. Scales how much a foraging trip actually brings home, which is
    /// what turns ageing from a countdown into something you can watch happen.
    /// </summary>
    public int Vigour { get; set; } = 100;

    /// <summary>Which band <see cref="Vigour"/> currently falls in. Stored so the
    /// life log narrates each turn once, not every tick.</summary>
    public VigourStage Stage { get; set; } = VigourStage.Prime;

    /// <summary>0 = full, rising to <c>hunger_max</c>.</summary>
    public int Hunger { get; set; }

    /// <summary>Consecutive ticks spent at maximum hunger. Starvation counts from here.</summary>
    public int TicksAtMaxHunger { get; set; }

    public VillagerState State { get; set; } = VillagerState.Idle;

    public GridPos Position { get; set; }

    /// <summary>Ticks left in the current timed action (gathering, travel delay).</summary>
    public int ActionTicksRemaining { get; set; }

    /// <summary>
    /// True on a tick where they paused to eat. Eating preempts whatever they were
    /// doing rather than replacing it, so <see cref="State"/> keeps the underlying
    /// activity and the interrupted action resumes next tick.
    /// </summary>
    public bool JustAte { get; set; }

    public bool Alive { get; set; } = true;

    public CauseOfDeath CauseOfDeath { get; set; } = CauseOfDeath.None;

    /// <summary>Tick at which they died. Null while alive.</summary>
    public ulong? DiedAtTick { get; set; }

    /// <summary>Winters survived — the unit a hard life is measured in.</summary>
    public int WintersSurvived { get; set; }

    /// <summary>Lifetime count of completed gathers, for the epitaph.</summary>
    public int TotalGathers { get; set; }

    /// <summary>Gathers since the season turned. Reset by <c>ClockSystem</c> after
    /// it summarises the season into the life log.</summary>
    public int GathersThisSeason { get; set; }

    /// <summary>Plain-language description of the current action, for the UI.</summary>
    public string DescribeState()
    {
        if (JustAte && Alive)
        {
            return "stopping to eat";
        }

        return State switch
        {
            VillagerState.Idle => "standing idle",
            VillagerState.TravelingToFood => "walking to the berry patch",
            VillagerState.Gathering => "gathering berries",
            VillagerState.TravelingHome => "walking home",
            VillagerState.Resting => "resting at home",
            VillagerState.Dead => "dead",
            _ => State.ToString(),
        };
    }
}
