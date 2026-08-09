using Bclone.Sim.Config;

namespace Bclone.Sim.World;

/// <summary>What the player can put up.</summary>
/// <remarks>
/// Deliberately a small closed set rather than "any building". Forage sites and tree
/// stands are terrain — you find them, you do not build them — and homes are not here
/// either, because the player paints a residential zone and the village builds inside
/// it (D42) rather than placing houses one at a time.
/// </remarks>
public enum BuildingKind
{
    Granary = 0,
    Shed = 1,
    Market = 2,
    WoodcutterHut = 3,

    /// <summary>
    /// A storage pile — cleared ground with goods stacked on it (D76).
    /// </summary>
    /// <remarks>
    /// <b>The one building that costs nothing</b>, and the first the player places. Note
    /// that <see cref="BuildingRecipe.For"/>'s default arm hands out the hut's recipe, so
    /// this kind must be named there explicitly or a pile silently costs 25 logs — which
    /// would delete the entire reason it exists.
    /// </remarks>
    Pile = 4,

    /// <summary>
    /// A house — and it is a construction site like everything else now (Joe, D102).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one inconsistency `specs/cold-start.md §7.1b` has been carrying since Joe's
    /// second run</b>: <em>"they built homes (immediate builds btw, not a visual timed thing
    /// like other buildings)"</em>. `HouseTheRoofless` and `FormNewHouseholds` took the timber
    /// and set `HomePosition` in a single tick, where every other building is marked, hauled
    /// to and worked on. That hid what a house costs, and — worse — meant houses never
    /// competed with anything for builders, which is exactly the distortion that made winter 1
    /// look winnable when it was not.
    /// </para>
    /// <para>
    /// <b>Still not player-placed</b>, and that is D42's settled division rather than an
    /// oversight: the player paints the neighbourhood and the sim picks the tile, because
    /// `MaxHomeToWorkTiles` is the bound the whole food economy is derived against. What
    /// changes is that the house the sim chose now has to be <em>built</em>.
    /// </para>
    /// </remarks>
    Home = 5,

    /// <summary>
    /// A builder's hut — where the village's builders work from (D64, D108).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It changes what a construction site IS.</b> Joe: <em>"a construction site is a place
    /// that builders should treat as errands. If there is an incomplete construction site on the
    /// map and an active/staffed builder's hut, then the builders' priority should be completing
    /// the construction site."</em> A site stops being somewhere people are assigned and becomes
    /// a job of work the hut's crew walks out to, taken in build-queue order (D105).
    /// </para>
    /// <para>
    /// <b>Free and instant, like the pile (Joe).</b> It is the one building that must exist
    /// before any other can be raised, so charging timber for it would be the same circle the
    /// pile exists to avoid. Its cost is the ground it stands on and the hands the player puts
    /// in it.
    /// </para>
    /// <para>
    /// <b>Appended, never renumbered</b> — the same rule <see cref="Goods"/> and
    /// <see cref="JobKind"/> carry, for the same reason.
    /// </para>
    /// </remarks>
    BuilderHut = 6,

    /// <summary>
    /// A gatherer's hut — where food comes from, and the first building whose yield
    /// depends on the ground around it (`specs/forests-and-gathering.md`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ It has a ring, and the trees inside it decide what a trip is worth</b> (Joe):
    /// <em>"the gatherer's hut should have a maximum gatherable area in a ring and workers
    /// cannot gather outside that ring — the number of trees/forest in the circle has a
    /// relation to the volume of food gathered. Less trees = less food."</em>
    /// </para>
    /// <para>
    /// <b>That is what makes the harvest brush cost something.</b> Until now felling was free
    /// money — paint trees, get logs. Timber and food come out of the same wood now, so a
    /// player clearing the ground beside their gatherers is spending food to get logs and can
    /// see themselves doing it. §2.3's *"every escalating problem should be back-traceable to
    /// something the player did"*, arriving out of a system built for another reason.
    /// </para>
    /// <para>
    /// <b>Appended, never renumbered</b> — the same rule <see cref="Goods"/> and
    /// <see cref="JobKind"/> carry, for the same reason.
    /// </para>
    /// </remarks>
    GathererHut = 7,

    /// <summary>
    /// A forester's hut — a wood somebody keeps, rather than one they only ever take from
    /// (`specs/forests-and-gathering.md`, D86).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ It is the first workplace that can PUT SOMETHING BACK.</b> Joe: <em>"foresters can
    /// plant trees/forests in a painted area — this will allow the user to sculpt their forests
    /// to their own desires."</em> Every other job in this game consumes or converts; a forester
    /// with the planting mode on is the only one that makes the valley richer than they found
    /// it.
    /// </para>
    /// <para>
    /// <b>And that is what makes over-clearing recoverable rather than terminal</b>, which
    /// §0.1 requires of any mistake: *"you lose villagers, not runs"*. It is the safety net that
    /// has to exist before the thickets can retire, because after that a felled ring is a
    /// village with no food.
    /// </para>
    /// <para>
    /// ⚠️ <b>Planting ships UNGATED (Joe), overturning `building-placement.md §12.5(3)` and
    /// `professions.md §6.2`</b>, which held it behind the managed-forestry unlock. The cost is
    /// named in `forests-and-gathering.md §6`: §2.7's headline unlock-by-doing example loses its
    /// content, and the node survives with different content — a debt taken on purpose.
    /// </para>
    /// </remarks>
    ForesterHut = 8,
}

/// <summary>What a building costs to raise.</summary>
/// <remarks>
/// <b>Data, not code</b> — the recipe comes out of config so a modder can change what a
/// granary costs without touching the sim (DESIGN.md §3). The two numbers are separate
/// on purpose: logs are what the village must *have*, and work is what it must *spend*,
/// and a building that is expensive in one and cheap in the other is a different
/// decision from one that is expensive in both.
/// </remarks>
public readonly record struct BuildingRecipe(int Logs, int WorkTicks)
{
    /// <summary>The recipe for a kind, read from config.</summary>
    public static BuildingRecipe For(BuildingKind kind, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return kind switch
        {
            BuildingKind.Granary => new BuildingRecipe(config.GranaryLogs, config.GranaryWorkTicks),
            BuildingKind.Shed => new BuildingRecipe(config.ShedLogs, config.ShedWorkTicks),
            BuildingKind.Market => new BuildingRecipe(config.MarketLogs, config.MarketWorkTicks),

            // NOTHING AT ALL — no materials and no work (D96). A village with nowhere to put
            // things cannot begin, and asking it to build a store out of timber it has
            // nowhere to stack is a circle.
            //
            // ⭐ The work went because the cost moved somewhere better rather than because it
            // was abolished: a pile may only be placed on ground that is already clear, so
            // ITS COST IS THE CLEARING. `pile_work_ticks` was eight ticks of levelling bare
            // earth, which was strange on its own terms; clearing a wood to make room for the
            // store is a decision with a visible price, paid in the currency the rest of the
            // game uses. A pile is therefore instant, and `SimWorld.Mark` never makes a
            // construction site for one — which is also what closes D95's window, where the
            // cart refused logs and the pile that would take them was not standing yet.
            //
            // The recipe survives at zero because `Demolish` reads one to work out a refund,
            // and zero is the right answer there too: you get nothing back from a heap you
            // never paid for.
            BuildingKind.Pile => new BuildingRecipe(0, 0),

            // A house costs what it has always cost in timber (`logs_per_house`, which the
            // whole timber economy is derived against) and now owes work as well (D102).
            BuildingKind.Home => new BuildingRecipe(config.LogsPerHouse, config.HomeWorkTicks),

            // ⭐ FREE AND INSTANT, LIKE THE PILE (D108). The builder's hut is the one building
            // that must exist before any other can be raised, so charging timber for it would
            // be a circle exactly like charging the pile for somewhere to stack timber. It is
            // the player's first act, and its cost is the ground and the hands they put in it.
            BuildingKind.BuilderHut => new BuildingRecipe(0, 0),

            BuildingKind.WoodcutterHut => new BuildingRecipe(config.HutLogs, config.HutWorkTicks),

            // Costs timber and work like every other real building. Deliberately NOT free:
            // the pile and the builder's hut are free because nothing can be built without
            // them, and a gatherer's hut has no such circle to break — it is the first thing
            // the player spends logs on because they chose to eat better.
            BuildingKind.GathererHut =>
                new BuildingRecipe(config.GathererHutLogs, config.GathererHutWorkTicks),

            BuildingKind.ForesterHut =>
                new BuildingRecipe(config.ForesterHutLogs, config.ForesterHutWorkTicks),

            // ⭐ NAMED RATHER THAN DEFAULTED, and this arm is why. It used to hand out the
            // woodcutter's hut's recipe to anything it did not recognise — 25 logs and 40
            // ticks — so a new kind silently cost a hut. `BuildingKind.Pile`'s own remarks
            // were written about that trap. A kind nobody has priced is a bug, not a hut.
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "That kind of building has no recipe."),
        };
    }
}

/// <summary>
/// A building that has been decided on but not yet raised.
/// </summary>
/// <remarks>
/// <para>
/// <b>A building does not appear the instant it is paid for</b> (D43). The player marks
/// a site, materials are carried to it, somebody builds it, and only then is it a
/// building. That is the same claim D14 makes about distribution and D29 about
/// processing: things that happen in this village are work somebody does.
/// </para>
/// <para>
/// It buys three things. A half-built granary is legible on the map — you can see what
/// the village is spending itself on. Building competes for hands with eating, so
/// placement is a real trade rather than a purchase. And §2.6's desire paths get a
/// burst of traffic to a spot the player chose.
/// </para>
/// <para>
/// The site is carried by a <see cref="Workplace"/> of kind
/// <see cref="JobKind.Builder"/>, so it inherits labour allocation, catchment and
/// refusal reasons rather than growing a second system that does the same things
/// slightly differently.
/// </para>
/// </remarks>
public sealed class ConstructionSite
{
    public required BuildingKind Kind { get; init; }

    /// <summary>What it will be called once it stands.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The household this is being built for, or 0 for a building that belongs to the
    /// village (D102).
    /// </summary>
    /// <remarks>
    /// <b>Only a home has one</b>, because a home is the one building that belongs to
    /// somebody. Recorded on the site rather than worked out at completion, so a family who
    /// waited two years for a house gets <em>that</em> house — the one the sim sited for them
    /// while they were still counted as roofless — rather than whichever roofless family
    /// happens to be first in the list on the day it is finished.
    /// </remarks>
    public int ForHouseholdId { get; init; }

    public required BuildingRecipe Recipe { get; init; }

    /// <summary>Logs carried here so far.</summary>
    public int LogsDelivered { get; private set; }

    /// <summary>Ticks of work put in so far.</summary>
    public int WorkDone { get; private set; }

    /// <summary>Whether everything it needs has been carried here.</summary>
    public bool HasMaterials => LogsDelivered >= Recipe.Logs;

    /// <summary>Whether it is finished and ready to become a building.</summary>
    public bool IsFinished => HasMaterials && WorkDone >= Recipe.WorkTicks;

    /// <summary>Logs still wanted here.</summary>
    public int LogsStillNeeded
    {
        get
        {
            int short_ = Recipe.Logs - LogsDelivered;
            return short_ < 0 ? 0 : short_;
        }
    }

    /// <summary>Take in logs somebody carried over. Returns how many were accepted.</summary>
    public int Deliver(int logs)
    {
        if (logs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logs), $"Cannot deliver {logs} logs.");
        }

        int accepted = logs < LogsStillNeeded ? logs : LogsStillNeeded;
        LogsDelivered += accepted;
        return accepted;
    }

    /// <summary>Put a tick of work in. Does nothing until the materials are here.</summary>
    /// <remarks>
    /// Materials first, deliberately: a builder cannot make progress on a site with
    /// nothing delivered to it, which is what makes the hauling a real leg of the job
    /// rather than a formality that happens to precede it.
    /// </remarks>
    public void Work()
    {
        if (HasMaterials && WorkDone < Recipe.WorkTicks)
        {
            WorkDone++;
        }
    }

    /// <summary>Logs handed back if the site is abandoned before it is finished.</summary>
    public int Abandon()
    {
        int back = LogsDelivered;
        LogsDelivered = 0;
        WorkDone = 0;
        return back;
    }
}
