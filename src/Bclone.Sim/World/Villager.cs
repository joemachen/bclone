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
    /// An able adult no workplace currently wants — a laborer (D63).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A reader, not a stored state, and deliberately not a <see cref="JobKind"/>.</b>
    /// Joe's own definition is <em>"villagers who aren't assigned to a specific job"</em>,
    /// which is a question the world can already answer — so asking it beats maintaining a
    /// flag: nothing to hash, nothing to set and fail to clear, and no way for the roster and
    /// the reality to disagree. A `JobKind.Laborer` would need a phantom workplace to hang
    /// off, since every other kind names a place, and `LabourAllocator` would then have to be
    /// taught not to allocate to it — a rule invented purely to undo a type that should not
    /// have existed.
    /// </para>
    /// <para>
    /// <b>Being a laborer is not yet a job of work</b>, and that is measured rather than
    /// assumed. See <c>specs/stock-limits-and-laborers.md §5.2</c>: both errands the spec
    /// proposed for them — clearing workplace buffers, and carrying materials to
    /// construction sites — were measured over a hundred years and occur on <b>0.0%</b> of
    /// ticks, because producers already carry their own output and builders already fetch
    /// their own materials. So this exists to be <em>named</em> on screen today, and gains
    /// work when slice B gives the map raw materials to gather.
    /// </para>
    /// </remarks>
    public bool IsLaborer => CanWork && !HasJob;

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
    /// What this villager's commute costs them, when it costs enough to matter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE CONDITION ON DELETING CATCHMENT, AND IT SHIPS IN THE SAME SLICE AS THE
    /// DELETION</b> (D112, spec §7.1). A fence makes a ruinous commute impossible; removing
    /// it makes a ruinous commute <em>silent</em>, and a village that thins out with nothing
    /// on screen saying why is §1.1 failing and the one uncozy state §0.1 rules out.
    /// </para>
    /// <para>
    /// So the walk is stated as what it actually takes: <em>"Elias walks 19 tiles to the
    /// north hut — about two-fifths of his working year goes on the road."</em> A share of
    /// the year rather than a distance, because tiles are a number and *most of your year*
    /// is a decision. Empty when the walk is ordinary; a note that appears for everybody
    /// says nothing about anybody.
    /// </para>
    /// <para>
    /// <b>Not hashed, and not sim state in any meaningful sense</b> — the same standing as
    /// <see cref="JobReason"/> and <see cref="WorkNote"/>. It is derived from the position
    /// and the workplace, both of which are hashed already.
    /// </para>
    /// </remarks>
    public string CommuteNote { get; set; } = string.Empty;

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
    /// How cold this villager has got, counted from where they have been standing (D45).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not a tick count</b> — it rises faster outdoors than under a fireless roof and
    /// falls beside a burning fire, so its unit is
    /// <see cref="Config.SimConfig.ExposureThreshold"/> rather than time — divide by it
    /// for anything a person sees.
    /// </para>
    /// <para>
    /// It used to be <c>TicksCold</c>: consecutive ticks in a home whose household had no
    /// firewood, asked identically of everyone everywhere. That is the household
    /// bookkeeping D45 replaces — a villager froze because of a number attached to their
    /// family rather than because of where they had been.
    /// </para>
    /// </remarks>
    public int Cold { get; set; }


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

    /// <summary>
    /// The household a marketer's current errand concerns. Zero when none.
    /// </summary>
    /// <remarks>
    /// Held on the villager rather than recomputed, because a marketer's leg has to
    /// finish where it was aimed: re-deciding every tick would let them change their
    /// mind halfway and wander between two equally needy homes forever. It is the same
    /// reason <see cref="ActionTicksRemaining"/> exists.
    /// </remarks>
    public int ErrandHouseholdId { get; set; }

    /// <summary>Where a marketer is walking to pick their load up.</summary>
    /// <remarks>
    /// Remembered rather than recomputed each tick, and that is not an optimisation.
    /// The cheapest pickup is judged from where the villager is <em>standing</em>, so a
    /// marketer who re-decides while walking watches the answer change under them with
    /// every step and can shuttle between two sources forever without reaching either.
    /// A destination has to outlive the walk to it.
    /// </remarks>
    public int ErrandX { get; set; }

    /// <summary>Where a marketer is walking to pick their load up.</summary>
    public int ErrandY { get; set; }

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
    /// <remarks>
    /// <para>
    /// <b>Every state has to be here.</b> This covered ten of them and fell through to
    /// <c>State.ToString()</c> for the rest, so the panel whose entire job is legibility
    /// printed <c>HaulingToStore</c> and <c>SeekingShelter</c> at the player — raw enum
    /// names, in the one place the game is supposed to explain itself. Seven of seventeen
    /// states read like that, and every one of them was added after this method was
    /// written.
    /// </para>
    /// <para>
    /// <b>There is a second mapping</b> — <c>BehaviorSystem.Describe</c> — and it is
    /// deliberate rather than an oversight. That one writes terse log lines
    /// (<em>"idle → gathering"</em>); this one writes a sentence for a person
    /// (<em>"gathering berries"</em>). Two registers for two readers. What is <em>not</em>
    /// allowed is either of them going stale, which is why a test walks every value of the
    /// enum through both and fails if it gets an enum name back.
    /// </para>
    /// </remarks>
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
            VillagerState.HaulingToStore => "carrying a load to the store",
            VillagerState.FetchingFromStore => "fetching supplies for home",
            VillagerState.SeekingShelter => "going indoors to get warm",
            VillagerState.CollectingForMarket => "collecting goods for the market",
            VillagerState.DeliveringToHome => "delivering goods to a home",
            VillagerState.FetchingMaterials => "fetching materials for the building site",
            VillagerState.Building => "raising a building",
            VillagerState.Clearing => "clearing trees the village marked",
            VillagerState.TidyingGround => "fetching a load left on the ground",
            _ => State.ToString(),
        };
    }
}
