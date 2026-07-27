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

    /// <summary>Workplace they currently hold a job at, or 0 for none.</summary>
    public int WorkplaceId { get; set; }

    /// <summary>
    /// Plain-language reason they hold this job, naming the runner-up where there
    /// was one.
    /// </summary>
    /// <remarks>
    /// The phase's actual deliverable. With one villager, "why is she doing that" was
    /// self-evident; with twenty simultaneous decisions it is not, and an opaque
    /// simulation is precisely the failure DESIGN.md §2.2 warns against. Every
    /// assignment must be explainable in one sentence, which is only possible because
    /// the assignment rule is a ranked list rather than a weighted score.
    /// </remarks>
    public string JobReason { get; set; } = string.Empty;

    /// <summary>True when they hold a job.</summary>
    public bool HasJob => WorkplaceId != 0;

    /// <summary>
    /// Why someone who holds a job is not doing it today, or empty when they are.
    /// </summary>
    /// <remarks>
    /// <see cref="JobReason"/> answers "why this job?", which is a question about the
    /// allocator. This answers "so why are you sitting at home?", which is a different
    /// question and, before the woodcutter's hut, never had an interesting answer — a
    /// forager rests in winter and everyone knows why.
    /// <para>
    /// The hut changed that: it is the first workplace that can be idle for want of an
    /// <em>input</em> rather than a worker or a season (D29). A manned building doing
    /// nothing, with no explanation, is exactly the opaque simulation §2.2 warns
    /// against — so it says so.
    /// </para>
    /// </remarks>
    public string WorkNote { get; set; } = string.Empty;

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

    /// <summary>
    /// Consecutive ticks spent in an unheated home in winter. Freezing counts from
    /// here, exactly as starvation counts from <see cref="TicksAtMaxHunger"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately the same shape as the hunger counter rather than a cleverer one.
    /// Cold is meant to read as <em>parallel</em> to hunger — a second way the same
    /// kind of neglect kills you — and two mechanics that behave alike are two the
    /// player only has to learn once.
    /// </remarks>
    public int TicksCold { get; set; }

    public VillagerState State { get; set; } = VillagerState.Idle;

    public GridPos Position { get; set; }

    /// <summary>Logs in their arms right now, between the stumps and the shed.</summary>
    /// <remarks>
    /// <para>
    /// Goods used to teleport: a logger finished a cut and the timber appeared in
    /// their household's larder from wherever they were standing. That is what let
    /// logs pile up in the logger's own house where nobody could spend them (D25),
    /// and it is why "the village's logs" had to be faked with a sweep across every
    /// household.
    /// </para>
    /// <para>
    /// Carrying makes the haul a real leg of a real trip, which is the point of D30 —
    /// and it is what gives §2.6's desire paths something to be about, because a route
    /// walked with a load is traffic.
    /// </para>
    /// </remarks>
    public int CarriedLogs { get; set; }

    /// <summary>Firewood in their arms.</summary>
    public int CarriedFirewood { get; set; }

    /// <summary>Food in their arms — used once fetching lands.</summary>
    public int CarriedFood { get; set; }

    /// <summary>True when they are hauling anything at all.</summary>
    public bool IsCarrying => CarriedLogs > 0 || CarriedFirewood > 0 || CarriedFood > 0;

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

    /// <summary>
    /// Plain-language description of the current action, for the UI.
    /// </summary>
    /// <param name="workplaceName">
    /// Where they work, if anywhere — so the line names the actual place. There are
    /// several forage sites now, and "walking to the berry patch" was simply untrue
    /// for most of the village. A UI line that contradicts the map is worse than a
    /// vague one.
    /// </param>
    public string DescribeState(string? workplaceName = null)
    {
        if (JustAte && Alive)
        {
            return "stopping to eat";
        }

        string where = string.IsNullOrEmpty(workplaceName) ? "work" : workplaceName;

        return State switch
        {
            VillagerState.Idle => "standing idle",
            VillagerState.TravelingToFood => $"walking to {where}",
            VillagerState.Gathering => "gathering berries",
            VillagerState.TravelingHome => "walking home",
            VillagerState.TravelingToTrees => $"walking to {where}",
            VillagerState.Cutting => "felling trees",
            VillagerState.TravelingToHut => $"walking to {where}",
            VillagerState.MakingFirewood => "splitting logs into firewood",
            VillagerState.Resting => "resting at home",
            VillagerState.Dead => "dead",
            _ => State.ToString(),
        };
    }
}
