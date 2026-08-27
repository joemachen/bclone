using System.Collections.Generic;
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
    /// <b>The one building that costs nothing</b>, and the first the player places.
    /// <b>⚠️ That sentence used to carry a warning that is now spent</b>, and the reason is worth
    /// keeping: <see cref="BuildingRecipe.For"/>'s default arm once handed out the woodcutter's
    /// hut's recipe, so <em>a pile silently cost 25 logs</em> — which would have deleted the
    /// entire reason it exists. <b>The cost is a row now</b> (`specs/buildings-catalog.md`), and a
    /// building nobody has priced throws rather than becoming a hut.
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

    /// <summary>
    /// A farmhouse — the steading a painted field is worked from
    /// (`specs/crops-and-orchards.md §3`, D161).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ A FARM IS A FORESTER'S HUT WITH A DIFFERENT VERB, and that closes an open
    /// question rather than dodging it.</b> `buildings-plan.md §8.1` argued fields should be
    /// brushes rather than a building with a radius and left the resolution open —
    /// <em>"likely a zone plus a small steading that is the workplace, worth deciding
    /// deliberately rather than by default"</em>. Decided: exactly that, and it costs no new
    /// mechanic, because a workplace whose extent is painted work ground already exists (D86,
    /// D118) and its brush is already a standing instruction rather than a one-off order
    /// (D127).
    /// </para>
    /// <para>
    /// <b>What that buys on day one, stated so it is not re-litigated:</b> the work-ground
    /// allowance, the overstretched warning, the labour quota, the idle ring (D147), the
    /// refusal sentences (D43) and the build queue all apply, because they are properties of
    /// <em>a workplace with painted ground</em> rather than of forestry.
    /// </para>
    /// <para>
    /// <b>⭐ And it is where <c>Workplace.Store</c> comes alive</b> —
    /// `professions.md §4`'s fifth element, on the type since D30 and never once written to.
    /// A harvest is exactly the case that needs a local buffer: reaping is bursty and the
    /// granary is across the village. See <c>SimConfig.FarmStoreCap</c>.
    /// </para>
    /// <para>
    /// <b>Appended, never renumbered</b> — the same rule <see cref="Goods"/> and
    /// <see cref="JobKind"/> carry, for the same reason.
    /// </para>
    /// </remarks>
    Farmhouse = 9,

    /// <summary>
    /// A library — where a technique outlives the person who worked it out
    /// (`specs/tech-tree.md §7c`, Phase 4 slice 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE FIRST BUILDING THAT IS NEITHER A STORE, A WORKPLACE NOR A HOME</b>, and the first
    /// whose point is not production at all. It holds <em>records</em>: a hard number of shelves,
    /// one technique each, no bundling.
    /// </para>
    /// <para>
    /// <b>⛔ IT IS A BUILDING, AND THAT WAS A CONTRADICTION TO RESOLVE RATHER THAN A GIVEN.</b>
    /// `content-inventory.md` finding 3 found three sources disagreeing, **two of them Joe's own
    /// words at different times** — `tech-tree.md §7c` gives it a building, `buildings-plan.md §6`
    /// cut it as *"not a building, the room the scriptorium's output lives in"*, and **D196 says
    /// *"the next woodcutter can spend idle time in the library"***. The cut is void on its own
    /// terms: it cut the library because it was a room inside the scriptorium, and **D204 took the
    /// scriptorium off the path entirely.** *A room inside a building nobody is building is not a
    /// room.*
    /// </para>
    /// <para>
    /// <b>Appended, never renumbered</b> — the same rule <see cref="Goods"/>, <see cref="JobKind"/>
    /// and the buildings catalogue all carry.
    /// </para>
    /// </remarks>
    Library = 10,
}

/// <summary>One material a building costs, and how much of it.</summary>
/// <remarks>
/// <b>A row in a building's cost list</b> since the buildings catalogue landed, so the names are
/// spelled for a config file. The good may be written as a name (<c>"Stone"</c>) or as an id
/// (<c>6</c>) — <b>a mod's good has an id and no enum name</b>, so the number has to be legal.
/// </remarks>
public readonly record struct MaterialCost(
    [property: System.Text.Json.Serialization.JsonPropertyName("goods")] Goods Goods,
    [property: System.Text.Json.Serialization.JsonPropertyName("amount")] int Amount);

/// <summary>What a building costs to raise.</summary>
/// <remarks>
/// <para>
/// <b>Data, not code</b> — the recipe comes out of config so a modder can change what a
/// granary costs without touching the sim (DESIGN.md §3). Materials and work are separate
/// on purpose: materials are what the village must *have*, and work is what it must *spend*,
/// and a building that is expensive in one and cheap in the other is a different
/// decision from one that is expensive in both.
/// </para>
/// <para>
/// <b>⭐⭐ IT WAS ONE MATERIAL SLOT — <c>(int Logs, int WorkTicks)</c> — FOR THE WHOLE
/// CATALOGUE</b> (`content-inventory.md` finding 2, D213). `buildings-plan.md §4.2` has the
/// mason's yard *"gating every durable building"* and §4.3 puts stone behind the civic tier;
/// `TECH-EXAMPLE.md` prices **every one of Joe's 45 buildings in two to four goods**, from
/// *"10 Wood, 10 Cut Stone"* on the first well up to *"80 Stone, 50 Planks, 20 Iron"* on the
/// town hall. <b>There is no version of that content that fits one slot</b>, so this was never
/// a question, only a schedule.
/// </para>
/// <para>
/// <b>A list of pairs rather than an array indexed by good</b>, deliberately. A building costs
/// one to four materials against a catalogue of up to 62 goods, so a per-good array would be
/// mostly zeros — and, worse, it would have to be sized from the run's goods catalogue, which
/// <see cref="For"/> does not have and should not need. The list is <b>sorted by good id and
/// carries no zeros</b>, so iteration is deterministic and two recipes that cost the same
/// things are the same recipe (D5's ordering rule, one type over).
/// </para>
/// </remarks>
public sealed class BuildingRecipe
{
    private readonly MaterialCost[] _materials;

    /// <summary>A recipe costing <paramref name="materials"/> and <paramref name="workTicks"/>.</summary>
    /// <remarks>
    /// <b>Zeros are dropped and the rest are put in good order here</b>, so no caller has to
    /// remember to — <c>For</c> below writes a line per material whether or not the config
    /// prices it, which is what keeps a building's cost readable as one statement.
    /// </remarks>
    public BuildingRecipe(int workTicks, params MaterialCost[] materials)
    {
        ArgumentNullException.ThrowIfNull(materials);

        WorkTicks = workTicks;

        var kept = new List<MaterialCost>(materials.Length);
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i].Amount > 0)
            {
                kept.Add(materials[i]);
            }
        }

        kept.Sort(static (a, b) => ((int)a.Goods).CompareTo((int)b.Goods));
        _materials = kept.ToArray();
    }

    /// <summary>Everything it costs, in good order. Empty for a building that is free.</summary>
    public IReadOnlyList<MaterialCost> Materials => _materials;

    /// <summary>Ticks of work it owes once the materials are on site.</summary>
    public int WorkTicks { get; }

    /// <summary>How much of one good it costs. Zero for a good it does not want.</summary>
    public int Of(Goods goods)
    {
        for (int i = 0; i < _materials.Length; i++)
        {
            if (_materials[i].Goods == goods)
            {
                return _materials[i].Amount;
            }
        }

        return 0;
    }

    /// <summary>Everything it costs, added up — for a progress bar and for a refund.</summary>
    public int TotalMaterials
    {
        get
        {
            int total = 0;
            for (int i = 0; i < _materials.Length; i++)
            {
                total += _materials[i].Amount;
            }

            return total;
        }
    }

    /// <summary>What it costs, as a sentence: <em>"40 logs and 10 stone"</em>.</summary>
    public string Describe(GoodsCatalog catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        if (_materials.Length == 0)
        {
            return "nothing";
        }

        var said = new System.Text.StringBuilder();
        for (int i = 0; i < _materials.Length; i++)
        {
            if (i > 0)
            {
                said.Append(i == _materials.Length - 1 ? " and " : ", ");
            }

            said.Append(_materials[i].Amount)
                .Append(' ')
                .Append(catalogue.NameOf(_materials[i].Goods));
        }

        return said.ToString();
    }

    /// <summary>The recipe for a kind, read from the buildings catalogue.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ IT WAS A SWITCH OVER PER-KIND CONFIG KEYS, AND THAT WAS THE LAST PLACE A BUILDING'S
    /// COST COULD ONLY EVER BE ONE OF TEN</b> (`specs/buildings-catalog.md`). Every arm read
    /// <c>config.GranaryLogs</c>, <c>config.HutStone</c> and their siblings, so a modded building had
    /// nowhere to state a price and the default arm could only throw. <b>The cost is a column on the
    /// row now</b>, and the config keys survive as the dials the built-in ten are priced from.
    /// </para>
    /// <para>
    /// <b>⚠️ A scan rather than an index, deliberately:</b> this takes a <see cref="SimConfig"/>
    /// rather than a <see cref="BuildingsCatalog"/>, because the call sites that have a config and no
    /// world would otherwise each have to build one. It is called at marking and at demolition,
    /// never in a tick loop.
    /// </para>
    /// </remarks>
    public static BuildingRecipe For(BuildingKind kind, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        IReadOnlyList<BuildingRow> rows = config.BuildingRows;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id != (int)kind)
            {
                continue;
            }

            var priced = new MaterialCost[rows[i].Materials.Count];
            for (int m = 0; m < priced.Length; m++)
            {
                priced[m] = rows[i].Materials[m];
            }

            return new BuildingRecipe(rows[i].WorkTicks, priced);
        }

        // ⭐ A KIND NOBODY HAS PRICED IS A BUG, NOT A HUT (D108). The old default arm handed out the
        // woodcutter's hut's recipe — 25 logs and 40 ticks — so a new kind silently cost a hut, and
        // `BuildingKind.Pile`'s own remarks were written about that trap. It is a missing row now
        // rather than a missing arm, and it still says so out loud.
        throw new ArgumentOutOfRangeException(
            nameof(kind), kind, "That kind of building has no row, so it has no recipe.");
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

    /// <summary>
    /// The tile a building is being moved <em>from</em>, or null for an ordinary new building.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ RELOCATION IS A BUILDER'S JOB, NOT A TELEPORT</b> (Joe, 2026-08-26: the sim may only
    /// impose a placement if the player has a remedy). Everything in this village happens because
    /// somebody does it — D14 for distribution, D29 for processing, D43 for building — and **a
    /// building that moved itself would be the one thing on the map that did not.** So a
    /// relocation is a site like any other: it owes <b>work ticks and no materials</b>, because
    /// the timber and stone walk over with the crew.
    /// </para>
    /// <para>
    /// <b>⭐ A TILE RATHER THAN AN ID, AND THAT IS WHY THIS WORKS FOR ALL THREE KINDS.</b> Stores,
    /// workplaces and libraries have three separate id spaces and a library has no id at all — but
    /// <b>only one building can stand on a tile</b>, which the placement rules already guarantee.
    /// *The tile is the identity that all three share.*
    /// </para>
    /// <para>
    /// ⚠️ <b>The source may be gone by the time the crew finish</b> — the player can demolish it
    /// mid-move. <c>SimWorld.Complete</c> says so and abandons rather than raising a phantom.
    /// </para>
    /// </remarks>
    public GridPos? MovingFrom { get; init; }

    /// <summary>
    /// Whether this site is pulling a building <em>down</em> rather than putting one up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ DEMOLITION IS A BUILDER'S JOB, AND IT TAKES TIME</b> (Joe, 2026-08-26): *"when a
    /// building is marked for demolition, it is a builder's job to demolish it. The demolition
    /// should take time, like the construction. Reverse-construction, essentially."* **It used to
    /// be instant** — a click and the building was gone — which made it the one piece of work in
    /// the village that nobody did.
    /// </para>
    /// <para>
    /// <b>⭐ It owes work and no materials, and the work comes off the building's own recipe</b>
    /// (<c>demolition_work_percent</c>). *So a stockpile, which owed nothing to raise, owes nothing
    /// to pull down* — the free-and-instant rule falls straight out of the data rather than needing
    /// a second special case (D108's lesson, one system over).
    /// </para>
    /// <para>
    /// ⚠️ <b>A demolition cannot be cancelled once the crew have started, and construction can</b>
    /// (<c>SimWorld.CancelConstruction</c> refunds what was delivered). **That asymmetry is
    /// deliberate and it is Joe's call:** a half-built house was never a house, and a half-demolished
    /// one is no longer one. *Repainting the ground under a house un-marks it right up until
    /// somebody swings the first hammer.*
    /// </para>
    /// </remarks>
    public bool Demolishing { get; init; }

    /// <summary>What it costs. Set once, at marking.</summary>
    /// <remarks>
    /// <b>A constructor parameter rather than an <c>init</c> property since D213</b>, because
    /// the delivery counts are sized from it: an object initialiser cannot guarantee the recipe
    /// is set before the site starts counting deliveries against it.
    /// </remarks>
    public BuildingRecipe Recipe { get; }

    /// <summary>How much of each material has arrived, parallel to <c>Recipe.Materials</c>.</summary>
    /// <remarks>
    /// <b>Parallel to the recipe's own list, not indexed by good</b> — a site can only ever
    /// receive what its recipe asks for, so the recipe's ordering is the natural index and
    /// there are no empty slots for the sixty-odd goods a building does not want.
    /// </remarks>
    private readonly int[] _delivered;

    public ConstructionSite(BuildingRecipe recipe)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        _delivered = new int[recipe.Materials.Count];
    }

    /// <summary>Ticks of work put in so far.</summary>
    public int WorkDone { get; private set; }

    /// <summary>Logs carried here so far — a named reader over the one array.</summary>
    public int LogsDelivered => Delivered(Goods.Logs);

    /// <summary>How much of one good has been carried here.</summary>
    public int Delivered(Goods goods)
    {
        for (int i = 0; i < _delivered.Length; i++)
        {
            if (Recipe.Materials[i].Goods == goods)
            {
                return _delivered[i];
            }
        }

        return 0;
    }

    /// <summary>Everything carried here so far, of every kind.</summary>
    public int TotalDelivered
    {
        get
        {
            int total = 0;
            for (int i = 0; i < _delivered.Length; i++)
            {
                total += _delivered[i];
            }

            return total;
        }
    }

    /// <summary>Whether everything it needs has been carried here.</summary>
    public bool HasMaterials
    {
        get
        {
            for (int i = 0; i < _delivered.Length; i++)
            {
                if (_delivered[i] < Recipe.Materials[i].Amount)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Whether it is finished and ready to become a building.</summary>
    public bool IsFinished => HasMaterials && WorkDone >= Recipe.WorkTicks;

    /// <summary>How much of one good is still wanted here.</summary>
    public int StillNeeded(Goods goods)
    {
        for (int i = 0; i < _delivered.Length; i++)
        {
            if (Recipe.Materials[i].Goods == goods)
            {
                int shortfall = Recipe.Materials[i].Amount - _delivered[i];
                return shortfall < 0 ? 0 : shortfall;
            }
        }

        return 0;
    }

    /// <summary>Logs still wanted here.</summary>
    public int LogsStillNeeded => StillNeeded(Goods.Logs);

    /// <summary>
    /// The next material this site is short of, or null when it has everything.
    /// </summary>
    /// <remarks>
    /// <b>In recipe order, which is good order</b> — so two runs of one seed send a builder for
    /// the same thing, and a site short of two materials is filled in a fixed sequence rather
    /// than by whichever the nearest store happened to hold most of.
    /// </remarks>
    public Goods? NextMaterialWanted()
    {
        for (int i = 0; i < _delivered.Length; i++)
        {
            if (_delivered[i] < Recipe.Materials[i].Amount)
            {
                return Recipe.Materials[i].Goods;
            }
        }

        return null;
    }

    /// <summary>Everything still wanted here, as a sentence: <em>"13 logs and 4 stone"</em>.</summary>
    public string DescribeWhatIsMissing(GoodsCatalog catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        var missing = new List<MaterialCost>();
        for (int i = 0; i < _delivered.Length; i++)
        {
            int shortfall = Recipe.Materials[i].Amount - _delivered[i];
            if (shortfall > 0)
            {
                missing.Add(new MaterialCost(Recipe.Materials[i].Goods, shortfall));
            }
        }

        return new BuildingRecipe(0, missing.ToArray()).Describe(catalogue);
    }

    /// <summary>Everything carried here so far, for a refund.</summary>
    public IReadOnlyList<MaterialCost> Held()
    {
        var held = new List<MaterialCost>();
        for (int i = 0; i < _delivered.Length; i++)
        {
            if (_delivered[i] > 0)
            {
                held.Add(new MaterialCost(Recipe.Materials[i].Goods, _delivered[i]));
            }
        }

        return held;
    }

    /// <summary>Take in a material somebody carried over. Returns how much was accepted.</summary>
    /// <remarks>
    /// <b>A good this building never asked for is refused rather than swallowed</b>, so the load
    /// stays in the carrier's arms and walks back to a store — D96's conservation rule, which a
    /// second material makes reachable for the first time.
    /// </remarks>
    public int Deliver(Goods goods, int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), $"Cannot deliver {amount} {goods}.");
        }

        for (int i = 0; i < _delivered.Length; i++)
        {
            if (Recipe.Materials[i].Goods != goods)
            {
                continue;
            }

            int room = Recipe.Materials[i].Amount - _delivered[i];
            int accepted = amount < room ? amount : room;
            accepted = accepted < 0 ? 0 : accepted;
            _delivered[i] += accepted;
            return accepted;
        }

        return 0;
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

    /// <summary>Everything handed back if the site is abandoned before it is finished.</summary>
    /// <remarks>
    /// <b>Every material, not the timber</b> (D213). A site abandoned with stone on it would
    /// have handed back only its logs — which, once a recipe can ask for two things, is the
    /// conservation rule leaking in the one direction nothing ever notices: the total only
    /// falls.
    /// </remarks>
    public IReadOnlyList<MaterialCost> Abandon()
    {
        IReadOnlyList<MaterialCost> back = Held();

        for (int i = 0; i < _delivered.Length; i++)
        {
            _delivered[i] = 0;
        }

        WorkDone = 0;
        return back;
    }
}
