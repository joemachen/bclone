using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// Building condition and maintenance — `specs/building-condition.md §4.3–4.5` (D552–D555). What stands wears on a
/// derived clock (<see cref="ConditionRules"/>); at each season's turn the village looks at what it has, raises a
/// repair site for anything worn, and closes a ruin. The builders do the rest, as they do for any site.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>ONCE A SEASON, NEVER PER TICK</b> (CLAUDE.md). <see cref="TendWhatStands()"/> runs from <c>ClockSystem</c>
/// on the season's turn only, and compares two <em>derived</em> answers — the state now and the state a season
/// ago, both read off the same two stamps — so nothing is stored to know that something changed.
/// </para>
/// <para>
/// ⭐ <b>"CLOSED" IS A FLAG THE SIM SETS AT THE TRANSITION, NOT A PER-READ DERIVATION.</b> A bridge's closing has
/// exceptions — somebody on it, a house it is the only way to — that are judged at the season's turn, so the outcome
/// is a decision made at a moment, like <c>RepairedAtTick</c>: a fact, hashed and saved, cleared by the repair.
/// Every other kind closes the same way so that one word means one thing.
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    // ---------------------------------------------------------------
    //  What state a thing is in (derived — `building-condition.md §4.1`)
    // ---------------------------------------------------------------

    /// <summary>The row a standing workplace was raised from, or null for a site or a trade with no building.</summary>
    private BuildingRow? RowOf(Workplace workplace) =>
        workplace.IsSite || JobsCatalog.WorksAt(workplace.Kind) is not BuildingKind kind ? null : BuildingsCatalog[kind];

    private BuildingRow? RowOf(StoreBuilding store) =>
        BuildingKindOf(store) is BuildingKind kind ? BuildingsCatalog[kind] : null;

    public Condition ConditionOf(Workplace workplace) =>
        RowOf(workplace) is BuildingRow row ? ConditionRules.Of(workplace.RaisedAtTick, workplace.RepairedAtTick, Tick, row, Config) : Condition.Sound;

    public Condition ConditionOf(StoreBuilding store) =>
        RowOf(store) is BuildingRow row ? ConditionRules.Of(store.RaisedAtTick, store.RepairedAtTick, Tick, row, Config) : Condition.Sound;

    /// <summary>⚠️ A library carries no kind; the shipped library's row is read (`building-condition.md §4.5`).</summary>
    public Condition ConditionOf(Library library) =>
        BuildingsCatalog[BuildingKind.Library] is BuildingRow row ? ConditionRules.Of(library.RaisedAtTick, library.RepairedAtTick, Tick, row, Config) : Condition.Sound;

    public Condition ConditionOf(Well well) =>
        BuildingsCatalog[well.Kind] is BuildingRow row ? ConditionRules.Of(well.RaisedAtTick, well.RepairedAtTick, Tick, row, Config) : Condition.Sound;

    public Condition ConditionOf(Longhouse longhouse) =>
        BuildingsCatalog[BuildingKind.Longhouse] is BuildingRow row ? ConditionRules.Of(longhouse.RaisedAtTick, longhouse.RepairedAtTick, Tick, row, Config) : Condition.Sound;

    public Condition ConditionOf(TownHall hall) =>
        BuildingsCatalog[BuildingKind.TownHall] is BuildingRow row ? ConditionRules.Of(hall.RaisedAtTick, hall.RepairedAtTick, Tick, row, Config) : Condition.Sound;

    /// <summary>The family's house, or sound for a family with no house.</summary>
    public Condition ConditionOf(Household household) =>
        household.HasHome && BuildingsCatalog[BuildingKind.Home] is BuildingRow row
            ? ConditionRules.Of(household.HomeRaisedAtTick, household.HomeRepairedAtTick, Tick, row, Config)
            : Condition.Sound;

    public Condition ConditionOf(Bridge bridge) =>
        BuildingsCatalog[bridge.Kind] is BuildingRow row ? ConditionRules.Of(bridge.RaisedAtTick, bridge.RepairedAtTick, Tick, row, Config) : Condition.Sound;

    /// <summary>The wear a thing has taken, for the cards' "about N years" — the same stamps the state is read from.</summary>
    public ulong WearTicksOf(ulong raisedAt, ulong repairedAt) => ConditionRules.WearTicks(raisedAt, repairedAt, Tick, Config);

    // ---------------------------------------------------------------
    //  What the cards say (`building-condition.md §6`, D556) — null for a thing with no row (a site, the cart)
    // ---------------------------------------------------------------

    public WearReport? WearOf(Workplace workplace) =>
        RowOf(workplace) is BuildingRow row ? Report(workplace.RaisedAtTick, workplace.RepairedAtTick, row) : null;

    public WearReport? WearOf(StoreBuilding store) =>
        RowOf(store) is BuildingRow row ? Report(store.RaisedAtTick, store.RepairedAtTick, row) : null;

    public WearReport? WearOf(Library library) =>
        BuildingsCatalog[BuildingKind.Library] is BuildingRow row ? Report(library.RaisedAtTick, library.RepairedAtTick, row) : null;

    public WearReport? WearOf(Well well) =>
        BuildingsCatalog[well.Kind] is BuildingRow row ? Report(well.RaisedAtTick, well.RepairedAtTick, row) : null;

    public WearReport? WearOf(Longhouse longhouse) =>
        BuildingsCatalog[BuildingKind.Longhouse] is BuildingRow row ? Report(longhouse.RaisedAtTick, longhouse.RepairedAtTick, row) : null;

    public WearReport? WearOf(TownHall hall) =>
        BuildingsCatalog[BuildingKind.TownHall] is BuildingRow row ? Report(hall.RaisedAtTick, hall.RepairedAtTick, row) : null;

    /// <summary>The family's house, or null for a family with none.</summary>
    public WearReport? WearOf(Household household) =>
        household.HasHome && BuildingsCatalog[BuildingKind.Home] is BuildingRow row
            ? Report(household.HomeRaisedAtTick, household.HomeRepairedAtTick, row)
            : null;

    public WearReport? WearOf(Bridge bridge) =>
        BuildingsCatalog[bridge.Kind] is BuildingRow row ? Report(bridge.RaisedAtTick, bridge.RepairedAtTick, row) : null;

    private WearReport Report(ulong raisedAt, ulong repairedAt, BuildingRow row)
    {
        ulong wear = WearTicksOf(raisedAt, repairedAt);
        Condition state = ConditionRules.Of(wear, row, Config);
        Condition next = state switch
        {
            Condition.Sound => Condition.Worn,
            Condition.Worn => Condition.Failing,
            _ => Condition.Ruin,
        };

        return new WearReport(
            state,
            ConditionRules.PercentWorn(wear, row, Config),
            ConditionRules.YearsUntil(wear, next, row, Config),
            ConditionRules.SeasonsUntil(wear, Condition.Ruin, row, Config),
            Math.Max(raisedAt, repairedAt),
            repairedAt > raisedAt);
    }

    /// <summary>How many times the season's walk has run — the probe's and the guards' way to see it is seasonal, not per tick.</summary>
    public int ConditionWalks { get; private set; }

    // ---------------------------------------------------------------
    //  The season's walk (`building-condition.md §4.3`)
    // ---------------------------------------------------------------

    /// <summary>
    /// Look at everything standing, once, at the season's turn: narrate what turned worn, failing or ruined since the
    /// last turn, raise a repair site for anything worn that has none, and close what has become a ruin.
    /// </summary>
    /// <summary>Whether this season's walk may raise repair sites — not in an empty valley (D567), where nobody could serve one.</summary>
    private bool _raiseRepairs = true;

    internal void TendWhatStands() => TendWhatStands(raiseRepairs: true);

    internal void TendWhatStands(bool raiseRepairs)
    {
        _raiseRepairs = raiseRepairs;
        ConditionWalks++;
        ulong aSeasonAgo = Tick >= (ulong)Config.TicksPerSeason ? Tick - (ulong)Config.TicksPerSeason : 0UL;

        // Homes.
        for (int i = 0; i < Households.Count; i++)
        {
            Household household = Households[i];
            if (!household.HasHome || BuildingsCatalog[BuildingKind.Home] is not BuildingRow row)
            {
                continue;
            }

            Condition now = ConditionOf(household);
            Condition before = ConditionRules.Of(household.HomeRaisedAtTick, household.HomeRepairedAtTick, aSeasonAgo, row, Config);
            string name = $"the {household.Name} household's house";
            SayWhatTurned(name, before, now);
            if (now >= Condition.Worn)
            {
                // The house's own footprint — its origin is the anchor, not the front point the household keeps (`HomeAnchorOn`).
                if (HomeFootprintOf(household) is Footprint house)
                {
                    RaiseARepairIfNoneFor(BuildingKind.Home, house.Origin, name, house.Facing, BuildingRecipe.For(BuildingKind.Home, Config));
                }
            }

            if (now == Condition.Ruin && !household.HomeClosed)
            {
                household.HomeClosed = true;
                StandingChanged();
            }
        }

        // Workplaces that stand (sites are not buildings yet).
        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace workplace = Workplaces[i];
            if (RowOf(workplace) is not BuildingRow row)
            {
                continue;
            }

            Condition now = ConditionOf(workplace);
            Condition before = ConditionRules.Of(workplace.RaisedAtTick, workplace.RepairedAtTick, aSeasonAgo, row, Config);
            SayWhatTurned(workplace.Name, before, now);
            if (now >= Condition.Worn)
            {
                RaiseARepairIfNoneFor((BuildingKind)row.Id, workplace.Position, workplace.Name, workplace.Facing, BuildingRecipe.For((BuildingKind)row.Id, Config));
            }

            if (now == Condition.Ruin && !workplace.Closed)
            {
                workplace.Closed = true;
                ReleaseWorkers(workplace);
                StandingChanged();
            }
        }

        // Stores (the stockpile and the cart have no wearing row).
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            StoreBuilding store = StoreBuildings[i];
            if (RowOf(store) is not BuildingRow row)
            {
                continue;
            }

            Condition now = ConditionOf(store);
            Condition before = ConditionRules.Of(store.RaisedAtTick, store.RepairedAtTick, aSeasonAgo, row, Config);
            SayWhatTurned(store.Name, before, now);
            if (now >= Condition.Worn)
            {
                RaiseARepairIfNoneFor((BuildingKind)row.Id, store.Position, store.Name, store.Facing, BuildingRecipe.For((BuildingKind)row.Id, Config));
            }

            if (now == Condition.Ruin && !store.Closed)
            {
                store.Closed = true;
                StandingChanged();
            }
        }

        for (int i = 0; i < Libraries.Count; i++)
        {
            Library library = Libraries[i];
            if (BuildingsCatalog[BuildingKind.Library] is not BuildingRow row)
            {
                continue;
            }

            Condition now = ConditionOf(library);
            Condition before = ConditionRules.Of(library.RaisedAtTick, library.RepairedAtTick, aSeasonAgo, row, Config);
            SayWhatTurned(library.Name, before, now);
            if (now >= Condition.Worn)
            {
                RaiseARepairIfNoneFor(BuildingKind.Library, library.Position, library.Name, library.Facing, BuildingRecipe.For(BuildingKind.Library, Config));
            }

            if (now == Condition.Ruin && !library.Closed)
            {
                library.Closed = true;
                StandingChanged();
            }
        }

        // The town hall's row never wears (D553 call 2); it is walked so a modder's row that does is honoured.
        if (TownHall is TownHall hall && BuildingsCatalog[BuildingKind.TownHall] is BuildingRow hallRow)
        {
            Condition now = ConditionOf(hall);
            Condition before = ConditionRules.Of(hall.RaisedAtTick, hall.RepairedAtTick, aSeasonAgo, hallRow, Config);
            SayWhatTurned(hall.Name, before, now);
            if (now >= Condition.Worn)
            {
                RaiseARepairIfNoneFor(BuildingKind.TownHall, hall.Position, hall.Name, hall.Facing, BuildingRecipe.For(BuildingKind.TownHall, Config));
            }
        }

        for (int i = 0; i < Wells.Count; i++)
        {
            Well well = Wells[i];
            if (BuildingsCatalog[well.Kind] is not BuildingRow row)
            {
                continue;
            }

            Condition now = ConditionOf(well);
            Condition before = ConditionRules.Of(well.RaisedAtTick, well.RepairedAtTick, aSeasonAgo, row, Config);
            SayWhatTurned(well.Name, before, now);
            if (now >= Condition.Worn)
            {
                RaiseARepairIfNoneFor(well.Kind, well.Position, well.Name, well.Facing, BuildingRecipe.For(well.Kind, Config));
            }

            if (now == Condition.Ruin && !well.Closed)
            {
                well.Closed = true;
                StandingChanged();
            }
        }

        // The longhouses (D566): worn gets a repair, a ruin is closed — its lodgers are in the open until it is mended.
        for (int i = 0; i < Longhouses.Count; i++)
        {
            Longhouse longhouse = Longhouses[i];
            if (BuildingsCatalog[BuildingKind.Longhouse] is not BuildingRow row)
            {
                continue;
            }

            Condition now = ConditionOf(longhouse);
            Condition before = ConditionRules.Of(longhouse.RaisedAtTick, longhouse.RepairedAtTick, aSeasonAgo, row, Config);
            SayWhatTurned(longhouse.Name, before, now);
            if (now >= Condition.Worn)
            {
                RaiseARepairIfNoneFor(BuildingKind.Longhouse, longhouse.Position, longhouse.Name, longhouse.Facing, BuildingRecipe.For(BuildingKind.Longhouse, Config));
            }

            if (now == Condition.Ruin && !longhouse.Closed)
            {
                longhouse.Closed = true;
                StandingChanged();
            }
        }

        // Bridges: a failing deck slows and a ruined one closes — never with anybody on it, never stranding (§4.4).
        for (int i = 0; i < Bridges.Count; i++)
        {
            Bridge bridge = Bridges[i];
            if (BuildingsCatalog[bridge.Kind] is not BuildingRow row)
            {
                continue;
            }

            Condition now = ConditionOf(bridge);
            Condition before = ConditionRules.Of(bridge.RaisedAtTick, bridge.RepairedAtTick, aSeasonAgo, row, Config);
            SayWhatTurned(bridge.Name, before, now, isABridge: true);
            if (now >= Condition.Worn)
            {
                RaiseARepairIfNoneFor(bridge.Kind, bridge.Near, bridge.Name, Angle.Zero, BridgeRecipe(bridge.Kind, bridge.Length));
            }

            if (now == Condition.Ruin && !bridge.Closed)
            {
                // Judged here, once a season, and kept for the card (D556): the strand sweep is not a per-frame question.
                bridge.KeptOpenBecause = WhyNotClose(bridge);
                if (bridge.KeptOpenBecause is null)
                {
                    bridge.Closed = true;
                    StandingChanged();
                    Narrate($"{Capitalised(bridge.Name)} is closed — nobody will cross it until it is mended. {Clock.SeasonAndYear()}.", LogCategory.Building);
                }
            }

            // The deck index prices a failing deck and refuses a closed one; it re-reads on a standing change.
            // ⚠️ The close above is one too, on its own: a bridge stamped a ruin twenty years back has no transition
            // this season, and the index would have gone on selling a deck that was no longer there.
            if (before != now)
            {
                StandingChanged();
            }
        }
    }

    /// <summary>The level a thing reached since the last season's turn, said once (`building-condition.md §6`).</summary>
    private void SayWhatTurned(string name, Condition before, Condition now, bool isABridge = false)
    {
        if (before >= now)
        {
            return;
        }

        string line = now switch
        {
            Condition.Worn => $"{Capitalised(name)} is showing its age — the builders will see to it when they can.",
            Condition.Failing => isABridge
                ? $"{Capitalised(name)} is failing — it slows everyone crossing, and closes without repair."
                : $"{Capitalised(name)} is failing — it will fall in without repair.",
            _ => isABridge
                ? $"{Capitalised(name)} has fallen into ruin."
                : $"{Capitalised(name)} has fallen in — it is closed until it is mended or pulled down.",
        };

        Narrate($"{line} {Clock.SeasonAndYear()}.", LogCategory.Building);
    }

    // ---------------------------------------------------------------
    //  The repair site (`building-condition.md §4.3`) — the demolition site's shape, the other way round
    // ---------------------------------------------------------------

    /// <summary>What mending a thing of this row costs: a share of its logs and its work, rounded up, no stone.</summary>
    public BuildingRecipe RepairRecipeOf(BuildingRecipe whole)
    {
        ArgumentNullException.ThrowIfNull(whole);
        int pct = Config.RepairCostPercent;
        int logs = whole.Of(Goods.Logs);
        int logsOwed = logs > 0 ? Math.Max(1, ((logs * pct) + 99) / 100) : 0;
        int work = Math.Max(1, ((whole.WorkTicks * pct) + 99) / 100);
        return logsOwed > 0
            ? new BuildingRecipe(work, new[] { new MaterialCost(Goods.Logs, logsOwed) })
            : new BuildingRecipe(work);
    }

    /// <summary>The repair job standing on a tile, or null.</summary>
    public Workplace? RepairSiteAt(GridPos tile)
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Construction is { Repairing: true } && Workplaces[i].Footprint.Covers(tile))
            {
                return Workplaces[i];
            }
        }

        return null;
    }

    /// <summary>
    /// A repair site the village could serve now — reachable, and its logs in some store (or already delivered) — or
    /// null. What the labour rule spares one hand for when half the spare hands rounds to nobody (D453's exception).
    /// </summary>
    public Workplace? AServableRepairSite()
    {
        GridPos village = FirstHomeOrFoundingSite();
        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace candidate = Workplaces[i];
            if (candidate.Construction is not { Repairing: true, IsFinished: false } plan
                || !TravelCost.CanReach(village, candidate.Tile))
            {
                continue;
            }

            if (plan.NextMaterialWanted() is not Goods wanted || AnyStoreHolding(wanted))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Whether this workplace is a repair's site — it stands on the building it mends and must not wait on clearing.</summary>
    internal static bool IsARepairSite(Workplace workplace) => workplace.Construction is { Repairing: true };

    internal void RaiseARepairIfNoneFor(BuildingKind kind, Point position, string name, Angle facing, BuildingRecipe whole)
    {
        GridPos tile = position.ToTile();
        if (!_raiseRepairs || RepairSiteAt(tile) is not null || DemolitionSiteAt(tile) is not null)
        {
            return;
        }

        BuildingRecipe recipe = RepairRecipeOf(whole);
        BuildingRow? row = BuildingsCatalog[kind];
        StandingChanged();
        Workplaces.Add(new Workplace
        {
            Store = NewStockpile(),
            Id = NextWorkplaceId(),
            Kind = JobKind.Builder,
            Name = $"{name} (being repaired)",
            Position = position,
            Capacity = 0,
            ExtentWidth = kind is BuildingKind.Bridge or BuildingKind.StoneBridge ? 1 : row?.ExtentWidth ?? 1,
            ExtentHeight = kind is BuildingKind.Bridge or BuildingKind.StoneBridge ? 1 : row?.ExtentHeight ?? 1,
            Facing = facing,
            Construction = new ConstructionSite(recipe)
            {
                Kind = kind,
                Name = name,
                Facing = facing,
                Repairing = true,
            },
        });
    }

    /// <summary>A repair site finished: whatever stands on its tile is mended now (`Complete`).</summary>
    /// <remarks>
    /// ⚠️ <b>Everything on the tile, not the first thing found.</b> The market is a store and a workplace standing on
    /// one tile (`RaiseFinished`); stamping only the store left its workplace half worn, and the season's walk raised a
    /// fresh repair for it every season — a log tax nobody asked for, found by the fixture's own nine-year run.
    /// </remarks>
    private void Mend(GridPos tile, string name)
    {
        bool mended = false;
        if (HouseholdAt(tile) is Household family)
        {
            family.HomeRepairedAtTick = Tick;
            family.HomeClosed = false;
            mended = true;
        }

        if (StoreAt(tile) is StoreBuilding store)
        {
            store.RepairedAtTick = Tick;
            store.Closed = false;
            mended = true;
        }

        if (LibraryCovering(tile) is Library library)
        {
            library.RepairedAtTick = Tick;
            library.Closed = false;
            mended = true;
        }

        if (TownHall is TownHall hall && hall.Footprint.Covers(tile))
        {
            hall.RepairedAtTick = Tick;
            mended = true;
        }

        if (WellCovering(tile) is Well well)
        {
            well.RepairedAtTick = Tick;
            well.Closed = false;
            mended = true;
        }

        if (LonghouseCovering(tile) is Longhouse longhouse)
        {
            longhouse.RepairedAtTick = Tick;
            longhouse.Closed = false;
            mended = true;
        }

        if (StandingWorkplaceCovering(tile) is Workplace workplace)
        {
            workplace.RepairedAtTick = Tick;
            workplace.Closed = false;
            mended = true;
        }

        if (BridgeAt(tile) is Bridge bridge)
        {
            bridge.RepairedAtTick = Tick;
            bridge.Closed = false;
            bridge.KeptOpenBecause = null;
            mended = true;
        }

        StandingChanged();
        Narrate(mended
            ? $"{Capitalised(name)} was mended. {Clock.SeasonAndYear()}."
            : $"{Capitalised(name)} was being mended, but there was nothing left to mend. {Clock.SeasonAndYear()}.",
            mended ? LogCategory.Building : LogCategory.Warning);
    }

    // ---------------------------------------------------------------
    //  The bridge: slows, then closes (`building-condition.md §4.4`, D546 call 6)
    // ---------------------------------------------------------------

    /// <summary>Why a ruined bridge stays open this season — somebody is on it, or it is the only way somewhere — or null.</summary>
    public string? WhyNotClose(Bridge bridge)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        List<GridPos> deck = DeckOf(bridge);
        for (int i = 0; i < Villagers.Count; i++)
        {
            if (Villagers[i].Alive && deck.Contains(Villagers[i].Position.ToTile()))
            {
                return $"{Villagers[i].Name} is on the bridge.";
            }
        }

        return WhatLosingTheDeckWouldCutOff(deck) is string cutOff
            ? cutOff.Replace("Pulling it down would cut off", "It stays open — closing it would cut off", StringComparison.Ordinal)
            : null;
    }

    /// <summary>Whether a step onto this tile is over a failing deck — the leg takes longer there (`PlanLeg`).</summary>
    public bool DeckIsSlow(GridPos tile)
    {
        RebuildTheBridgeIndexIfStale();
        int index = IndexOfTile(tile);
        return index >= 0 && _deckSlow![index];
    }
}
