using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// House tiers, piece 3 (`specs/house-tiers.md §4.4–§4.5`, §14.3, D721): <b>the family tree learns the stone cottage and the
/// insulated manor</b>, and <b>the player's one choice — <i>Build homes as</i> — says what the village builds</b>; the sim
/// says where and when.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The gates are two factors each</b> (Joe, D717: <i>"population + ???"</i>; D718: <i>"I love the family tree unlock
/// idea!"</i>): a generation born into the valley, and the most people the village has held. Both are read off the
/// villagers' own history (<see cref="DeepestGenerationBorn"/>, <see cref="MostEverAlive"/>), both are checked where they
/// change — a birth and an arrival — and the latch each sets is the state (D676's shape: the counters are statistics,
/// the learned flag is hashed only when true).
/// </para>
/// <para>
/// ⭐ <b>The choice is Cabin until the player changes it, and stays Cabin after learning</b> (§4.5): learning masonry
/// changes nothing by itself, so a stone spend is always a decision somebody made and an unattended village never rebuilds
/// itself. Above the cabin, new homes are raised at the chosen tier and the standing houses below it are rebuilt where
/// they stand, <b>one at a time, behind every other site</b> (<see cref="Workplace.EffectiveQueueRank"/>).
/// </para>
/// <para>
/// ⛔ <b>Nothing here runs per tick</b> (CLAUDE.md). The next rebuild is raised when the choice is made, when a rebuild
/// finishes, and at the season's walk as the catch-all (a site cancelled, a cabin finished from an older mark, a house
/// that came down).
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    // ---------------------------------------------------------------
    //  The family tree (§4.4)
    // ---------------------------------------------------------------

    private int _deepestGenerationFor = -1;
    private int _deepestGeneration;

    /// <summary>
    /// The deepest generation ever born into the valley, the dead included — what both house gates read (§4.4). The
    /// founders and every grown nomad are the first.
    /// </summary>
    /// <remarks>
    /// <b>Derived, neither saved nor hashed</b>, as <see cref="MostEverAlive"/> is: a villager's generation never changes
    /// once they exist and the list keeps the dead, so the answer can only move when somebody is added — and it is
    /// recomputed only then.
    /// </remarks>
    public int DeepestGenerationBorn
    {
        get
        {
            if (_deepestGenerationFor == Villagers.Count)
            {
                return _deepestGeneration;
            }

            int deepest = 0;
            for (int i = 0; i < Villagers.Count; i++)
            {
                deepest = Math.Max(deepest, Villagers[i].Generation);
            }

            _deepestGeneration = deepest;
            _deepestGenerationFor = Villagers.Count;
            return deepest;
        }
    }

    /// <summary>
    /// Whether the village has learned to build in stone (§4.4) — latched by <see cref="LatchTheFamilyTree"/> when
    /// <c>cottage_unlock_generation</c> has been born and the village has held <c>cottage_unlock_population</c>.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>State, hashed sparsely</b> (a tag byte when true, <see cref="WorkshopLearned"/>'s shape): it decides what the
    /// player may choose. The two factors beside it stay statistics (D676). Saved as <c>cottage_learned</c>.
    /// </remarks>
    public bool CottageLearned { get; internal set; }

    /// <summary>Whether the village has learned the insulated manor (§4.4) — the cottage first, then the manor's two factors.</summary>
    public bool ManorLearned { get; internal set; }

    /// <summary>
    /// Latch whichever house the family tree has just earned — called where its factors change, a birth or an arrival,
    /// never per tick. Returns which were learned this call, so the caller says it in its own moment.
    /// </summary>
    internal (bool Cottage, bool Manor) LatchTheFamilyTree()
    {
        bool cottage = false;
        bool manor = false;
        if (!CottageLearned && FamilyTreeHolds(Config.CottageUnlockGeneration, Config.CottageUnlockPopulation))
        {
            CottageLearned = true;
            cottage = true;
        }

        // ⭐ The manor waits on the cottage (§4.4): a village that has never built in stone has not learned to build tight.
        if (CottageLearned && !ManorLearned && FamilyTreeHolds(Config.ManorUnlockGeneration, Config.ManorUnlockPopulation))
        {
            ManorLearned = true;
            manor = true;
        }

        return (cottage, manor);
    }

    private bool FamilyTreeHolds(int generation, int people) =>
        DeepestGenerationBorn >= generation && MostEverAlive >= people;

    /// <summary>
    /// A child was born: if it carries the village over a house's two factors, it stops and says so — through
    /// <c>LearnedByDoing</c>, so the first thing a village learns this way introduces the tech-tree map (D440).
    /// </summary>
    internal void NoticeTheFamilyTree(Villager child)
    {
        ArgumentNullException.ThrowIfNull(child);
        (bool cottage, bool manor) = LatchTheFamilyTree();
        if (cottage)
        {
            LearnedByDoing("The village learned to build in stone", WhatTheFamilyTreeTaught(BuildingKind.Cottage, child));
        }

        if (manor)
        {
            LearnedByDoing("The village learned to build a manor", WhatTheFamilyTreeTaught(BuildingKind.Manor, child));
        }
    }

    /// <summary>
    /// What a band's coming taught, as a paragraph for the nomads' own moment — one popup, not two (Joe, D576) — or null.
    /// </summary>
    /// <remarks>
    /// ⚠️ Not <c>LearnedByDoing</c>: the arrival is the village's size, not anybody's hands, so it does not introduce the
    /// tech-tree map — the bridge's rule for the same moment (D576).
    /// </remarks>
    internal string? WhatTheBandTaughtOfHouses()
    {
        (bool cottage, bool manor) = LatchTheFamilyTree();
        if (!cottage && !manor)
        {
            return null;
        }

        string said = cottage ? WhatTheFamilyTreeTaught(BuildingKind.Cottage, child: null) : string.Empty;
        if (manor)
        {
            said += (said.Length > 0 ? " " : string.Empty) + WhatTheFamilyTreeTaught(BuildingKind.Manor, child: null);
        }

        return said;
    }

    private string WhatTheFamilyTreeTaught(BuildingKind house, Villager? child)
    {
        int deepest = DeepestGenerationBorn;
        string who = child is not null && child.Generation >= deepest
            ? $"{child.FullName} is born — the {GenerationWord(child.Generation)} generation in the valley"
            : $"The village has grown to {MostEverAlive} people, with its {GenerationWord(deepest)} generation born here";
        BuildingRow row = BuildingsCatalog[house] ?? HomeRowOfTheCabin();
        BuildingRow cabin = HomeRowOfTheCabin();
        string why = house == BuildingKind.Cottage
            ? "A village whose grandchildren are born here means to stay, and has learned to build in stone."
            : "A village that has buried generations here builds to last them, and has learned to build tight.";
        string choice = house == BuildingKind.Cottage ? "Cottage" : "Manor";
        return $"{who}. {why} Set the housing brush's Build homes as to {choice}, and new homes are built as {row.Name}s and the "
            + $"cabins rebuilt one at a time — a {row.Name} burns {row.WinterFirewood} firewood a winter where a cabin burns "
            + $"{cabin.WinterFirewood}, and keeps a bigger larder. {Clock.SeasonAndYear()}.";
    }

    private BuildingRow HomeRowOfTheCabin() =>
        BuildingsCatalog[BuildingKind.Home] ?? throw new InvalidOperationException("The catalogue has no house row.");

    /// <summary>"third" — the word the villager card and the moment use for a generation.</summary>
    internal static string GenerationWord(int generation)
    {
        string[] words = { "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth" };
        return generation >= 1 && generation <= words.Length ? words[generation - 1] : $"{generation}th";
    }

    /// <summary>The two houses' locks in words — <see cref="WhyNotYet"/>'s arms for them (§4.4), both numbers each.</summary>
    private string? WhyNoHouseYet(BuildingKind kind) => kind switch
    {
        BuildingKind.Cottage when !CottageLearned =>
            $"Nobody here builds in stone yet — the {GenerationWord(Config.CottageUnlockGeneration)} generation must be born in "
            + $"the valley (the {GenerationWord(DeepestGenerationBorn)} has been) and the village must have held "
            + $"{Config.CottageUnlockPopulation} people ({MostEverAlive} so far).",
        BuildingKind.Manor when !CottageLearned =>
            "Nobody here can build a manor before the village has learned to build in stone.",
        BuildingKind.Manor when !ManorLearned =>
            $"Nobody here builds a manor yet — the {GenerationWord(Config.ManorUnlockGeneration)} generation must be born in "
            + $"the valley (the {GenerationWord(DeepestGenerationBorn)} has been) and the village must have held "
            + $"{Config.ManorUnlockPopulation} people ({MostEverAlive} so far).",
        _ => null,
    };

    // ---------------------------------------------------------------
    //  The choice (§4.5)
    // ---------------------------------------------------------------

    /// <summary>
    /// What the village builds its homes as — the cabin until the player says otherwise (`house-tiers.md §4.5`, Joe:
    /// <i>"agree"</i>). New homes are raised as this row; standing homes of a lower tier are rebuilt as it.
    /// </summary>
    /// <remarks>Saved as <c>homes_built_as</c>; hashed only when it is not the cabin, so a village that never chose hashes as before.</remarks>
    public BuildingKind HomesBuiltAs { get; internal set; } = BuildingKind.Home;

    /// <summary>Why the village cannot build its homes as <paramref name="dwelling"/>, in words — or null when it can.</summary>
    public string? WhyNotBuildHomesAs(BuildingKind dwelling) =>
        !BuildingKinds.IsDwelling(dwelling) || BuildingsCatalog[dwelling] is null
            ? "Only a house can be what the village builds its homes as."
            : WhyNotYet(dwelling);

    /// <summary>
    /// The player's choice on the housing brush: build homes as <paramref name="dwelling"/>. Refused in words above what is
    /// learned (null when done). Raising it starts the rebuilds; lowering it stops them — a rebuild nobody has started is
    /// called off, one already under way is finished, and nothing is pulled back down.
    /// </summary>
    public string? SetHomesBuiltAs(BuildingKind dwelling)
    {
        if (WhyNotBuildHomesAs(dwelling) is string refused)
        {
            return refused;
        }

        if (dwelling == HomesBuiltAs)
        {
            return null;
        }

        HomesBuiltAs = dwelling;

        // ⭐ A rebuild nobody has touched is re-aimed — a cottage site under a fresh Manor would cost the street two lots of
        // stone (§8), and one under Cabin is the rebuild the player just stopped.
        if (StandingRebuild() is Workplace standing && !HasBeenStarted(standing.Construction!))
        {
            RetireWorkplace(standing);
        }

        Narrate(dwelling == BuildingKind.Home
            ? $"New homes are built as wooden cabins again, and no more houses will be rebuilt. {Clock.SeasonAndYear()}."
            : $"New homes are built as {BuildingsCatalog.NameOf(dwelling)}s now, and the builders rebuild the older houses one "
                + $"at a time when nothing else is waiting. {Clock.SeasonAndYear()}.",
            LogCategory.Building);

        RaiseTheNextRebuild();
        return null;
    }

    // ---------------------------------------------------------------
    //  The rebuild (§4.5) — one at a time, behind every other site
    // ---------------------------------------------------------------

    /// <summary>The house rebuild standing, or null — there is never more than one.</summary>
    public Workplace? StandingRebuild()
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Construction is { RebuildsHome: true })
            {
                return Workplaces[i];
            }
        }

        return null;
    }

    /// <summary>Whether a house stands below the chosen tier — what a rebuild is for.</summary>
    private bool IsBelowTheChoice(Household household) =>
        household.HasHome && BuildingKinds.TierOf(household.HomeKind) < BuildingKinds.TierOf(HomesBuiltAs);

    private static bool HasBeenStarted(ConstructionSite site) => site.WorkDone > 0 || site.TotalDelivered > 0;

    /// <summary>
    /// Raise the next house's rebuild if none stands and a house is below the choice — the worn first (its repair becomes
    /// the rebuild, §4.5), then the nearest the builder's hut, then by household id. Deterministic and legible.
    /// </summary>
    internal void RaiseTheNextRebuild()
    {
        if (BuildingKinds.TierOf(HomesBuiltAs) <= 0 || StandingRebuild() is not null)
        {
            return;
        }

        Point from = BuildersHutPosition() ?? Point.CentreOf(FirstHomeOrFoundingSite());
        Household? best = null;
        bool bestWorn = false;
        Fixed bestDistance = Fixed.Zero;
        for (int i = 0; i < Households.Count; i++)
        {
            Household household = Households[i];
            if (!IsBelowTheChoice(household) || HomeFootprintOf(household) is not Footprint house || !MayRebuild(house))
            {
                continue;
            }

            bool worn = ConditionOf(household) >= Condition.Worn;
            Fixed dx = house.Origin.X - from.X;
            Fixed dy = house.Origin.Y - from.Y;
            Fixed distance = (dx * dx) + (dy * dy);
            if (best is null
                || (worn && !bestWorn)
                || (worn == bestWorn && (distance < bestDistance || (distance == bestDistance && household.Id < best.Id))))
            {
                best = household;
                bestWorn = worn;
                bestDistance = distance;
            }
        }

        if (best is not null)
        {
            RaiseARebuild(best, HomeFootprintOf(best)!.Value);
        }
    }

    /// <summary>
    /// Whether a house may take a rebuild now: not coming down, and not half-mended — a repair nobody has started is
    /// called off for the rebuild, one under way is let finish.
    /// </summary>
    private bool MayRebuild(Footprint house)
    {
        GridPos tile = house.Origin.ToTile();
        if (DemolitionSiteAt(tile) is not null)
        {
            return false;
        }

        return RepairSiteAt(tile) is not Workplace mending || !HasBeenStarted(mending.Construction!);
    }

    /// <summary>The first standing builder's hut, by id — where "nearest" is measured from.</summary>
    private Point? BuildersHutPosition()
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Kind == JobKind.Builder && !Workplaces[i].IsSite)
            {
                return Workplaces[i].Position;
            }
        }

        return null;
    }

    /// <summary>
    /// Mark the house to be rebuilt as the chosen tier where it stands: the row's whole recipe, no fence (the fence is
    /// kept), on the house's own footprint. An untouched repair on it is called off — the rebuild mends it.
    /// </summary>
    private void RaiseARebuild(Household household, Footprint house)
    {
        if (RepairSiteAt(house.Origin.ToTile()) is Workplace mending)
        {
            RetireWorkplace(mending);
        }

        BuildingKind to = HomesBuiltAs;
        string name = $"the {household.Name} household's house, rebuilt as a {BuildingsCatalog.NameOf(to)}";
        RaiseSiteFor(to, house.Origin, name, BuildingRecipe.For(to, Config), forHouseholdId: 0, facing: house.Facing,
            rebuildsHome: true);
    }

    /// <summary>
    /// A rebuild finished: the house on its tiles is the new row now — raised now, never mended, open — and the family
    /// that lived in the old one the whole time lives in this. The fence, the door and the plot are untouched.
    /// </summary>
    private void FinishARebuild(Workplace site, ConstructionSite plan)
    {
        GridPos tile = site.Tile;
        RetireWorkplace(site);
        if (HouseholdAt(tile) is not Household family)
        {
            Narrate($"{Capitalised(plan.Name)} was finished, but there was no house left to rebuild. {Clock.SeasonAndYear()}.",
                LogCategory.Warning);
            RaiseTheNextRebuild();
            return;
        }

        int burned = WinterFirewoodOf(family);
        StandingChanged();
        SetHomeKind(family, plan.Kind);
        family.HomeRaisedAtTick = Tick;
        family.HomeRepairedAtTick = 0;
        family.HomeClosed = false;
        Narrate($"The {family.Name} household's house is rebuilt as a {BuildingsCatalog.NameOf(plan.Kind)} — it burns "
            + $"{WinterFirewoodOf(family)} firewood a winter where it burned {burned}. {Clock.SeasonAndYear()}.",
            LogCategory.Building);
        RaiseTheNextRebuild();
    }

    /// <summary>
    /// The season's walk found this house worn: if it stands below the choice and no rebuild is under way elsewhere, it is
    /// rebuilt rather than mended — so a ruin under <i>Cottage</i> comes back a cottage (§4.5). False to mend it as usual.
    /// </summary>
    private bool RebuildsInsteadOfMending(Household household, Footprint house)
    {
        if (!IsBelowTheChoice(household))
        {
            return false;
        }

        if (StandingRebuild() is Workplace standing)
        {
            // Its own rebuild is its mending; somebody else's means this one is mended as it is, and rebuilt in its turn.
            return standing.Footprint.Covers(house.Origin.ToTile());
        }

        if (!_raiseRepairs || !MayRebuild(house))
        {
            return false;
        }

        RaiseARebuild(household, house);
        return true;
    }

    /// <summary>
    /// A site that stands on the building it works on — a repair, or a house's rebuild — so it waits on no clearing, is no
    /// second shape on the ground and has no door of its own (D555, D721).
    /// </summary>
    internal static bool StandsOnABuilding(Workplace workplace) =>
        workplace.Construction is { Repairing: true } or { RebuildsHome: true };
}
