using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ Bridges (D40, D546, D549, `specs/bridges.md`) — a deck over the water, its placement, its raising, its
/// pulling-down, its two unlocks and the crossings that teach the second.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own file because it is its own idea</b>: everything else in the sim that stands on the ground
/// is an obstacle (D383), and this is the one thing whose point is to be walked across. The seams it
/// touches in the rest of <see cref="SimWorld"/> are small and named here so they can be found:
/// <c>StandingShapes</c> and <c>SomethingOverlaps</c> skip a bridge's site (it must not close the bank
/// it is built from); <c>SomethingStandsAt</c> and <c>CanBuildAt</c> answer true on a bridge's ground
/// (nothing else may be built or sited there); the free-ground sweeps and the cost field read
/// <see cref="IsWalkable"/>.
/// </para>
/// <para>
/// ⛔ <b>A deck change is a standing change.</b> Finishing, rebuilding or pulling down a bridge calls
/// <c>StandingChanged</c>, so the cost field forgets its routes and the free-ground sweep re-runs the
/// same way they do when a hut goes up — no second counter for a caller to forget.
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>The bridges standing in the valley, in the order they were finished. Hashed sparsely.</summary>
    public List<Bridge> Bridges { get; } = new();

    /// <summary>
    /// Every time somebody has stepped from land onto a bridge's deck — what the stone bridge's unlock
    /// counts (Joe, D546: DOING). ⛔ <b>State, and hashed sparsely</b>: it decides what the player may build.
    /// </summary>
    public int BridgeCrossings { get; internal set; }

    /// <summary>What a step onto each tile's deck costs, by map-order index — 0 where there is none (derived, never hashed).</summary>
    private byte[]? _deckCost;

    /// <summary>Whether a bridge or a bridge's site lands on or crosses each tile, by map-order index (derived, never hashed).</summary>
    private bool[]? _bridgeLands;

    /// <summary>Whether each tile is the deck of a <em>failing</em> bridge — a leg over it takes longer (`building-condition.md §4.4`). Derived, never hashed.</summary>
    private bool[]? _deckSlow;

    private int _bridgeIndexBuiltAt = -1;

    /// <summary>
    /// What a step onto this tile's deck costs — 0 where no finished bridge crosses it. The cost field's
    /// fifth question (<see cref="IObstacles"/>).
    /// </summary>
    public byte DeckOn(GridPos tile)
    {
        RebuildTheBridgeIndexIfStale();
        int index = IndexOfTile(tile);
        return index < 0 ? (byte)0 : _deckCost![index];
    }

    /// <summary>
    /// ⭐ Whether a villager may stand here — dry ground, or the deck of a finished bridge (D546). The one
    /// rule everything that asks <em>can somebody be here?</em> reads; <em>what ground is this?</em> still
    /// sees water (`bridges.md §6`).
    /// </summary>
    public bool IsWalkable(GridPos tile) =>
        Map.Contains(tile) && (TerrainRules.IsPassable(Map.TerrainAt(tile)) || DeckOn(tile) != 0);

    /// <summary>Whether a bridge, finished or still a site, lands on or crosses this tile — nothing else may stand there.</summary>
    public bool BridgeLandsOn(GridPos tile)
    {
        RebuildTheBridgeIndexIfStale();
        int index = IndexOfTile(tile);
        return index >= 0 && _bridgeLands![index];
    }

    /// <summary>The bridge that lands on or crosses this tile, or null.</summary>
    public Bridge? BridgeAt(GridPos tile)
    {
        for (int i = 0; i < Bridges.Count; i++)
        {
            if (Bridges[i].Holds(tile))
            {
                return Bridges[i];
            }
        }

        return null;
    }

    /// <summary>The site raising or rebuilding a bridge over this tile, or null.</summary>
    public Workplace? BridgeSiteAt(GridPos tile)
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Construction is { BridgeTo: Point far }
                && Bridge.TilesUnder(Workplaces[i].Position, far).Contains(tile))
            {
                return Workplaces[i];
            }
        }

        return null;
    }

    /// <summary>Whether this workplace is a bridge's site — it stands on a bank and must not close it.</summary>
    internal static bool IsABridgeSite(Workplace workplace) => workplace.Construction is { BridgeTo: not null };

    private void RebuildTheBridgeIndexIfStale()
    {
        if (_bridgeIndexBuiltAt == StandingGeneration && _deckCost is not null)
        {
            return;
        }

        int tiles = Map.Width * Map.Height;
        _deckCost ??= new byte[tiles];
        _bridgeLands ??= new bool[tiles];
        _deckSlow ??= new bool[tiles];
        System.Array.Clear(_deckCost);
        System.Array.Clear(_bridgeLands);
        System.Array.Clear(_deckSlow);

        for (int i = 0; i < Bridges.Count; i++)
        {
            // ⭐ A closed bridge has no deck — its water is water again (`building-condition.md §4.4`, D555); a failing
            // one is walked, slower. Read here, at the rebuild, because every transition is a standing change.
            byte cost = (byte)(BuildingsCatalog[Bridges[i].Kind]?.DeckTileCost ?? TravelCostField.BaseTileCost);
            bool closed = Bridges[i].Closed;
            bool slow = !closed && ConditionOf(Bridges[i]) == Condition.Failing;
            IReadOnlyList<GridPos> under = Bridges[i].Tiles();
            for (int t = 0; t < under.Count; t++)
            {
                int index = IndexOfTile(under[t]);
                if (index < 0)
                {
                    continue;
                }

                _bridgeLands[index] = true;
                if (Map.TerrainAt(under[t]) == Terrain.Water && !closed)
                {
                    _deckCost[index] = cost;
                    _deckSlow[index] = slow;
                }
            }
        }

        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Construction is { BridgeTo: Point far })
            {
                List<GridPos> under = Bridge.TilesUnder(Workplaces[i].Position, far);
                for (int t = 0; t < under.Count; t++)
                {
                    int index = IndexOfTile(under[t]);
                    if (index >= 0)
                    {
                        _bridgeLands[index] = true;
                    }
                }
            }
        }

        _bridgeIndexBuiltAt = StandingGeneration;
    }

    // ---------------------------------------------------------------
    //  Placing one — press, drag, release (`bridges.md §4`, Joe D549)
    // ---------------------------------------------------------------

    /// <summary>Where a bridge dragged between two points would lie — or why it cannot (`bridges.md §4`).</summary>
    /// <param name="Allowed">Whether it may be marked.</param>
    /// <param name="Reason">Why not, in the village's words; empty when it may.</param>
    /// <param name="Near">The end its builders work from — the village's side.</param>
    /// <param name="Far">The other end.</param>
    /// <param name="Rebuilding">The standing bridge a stone one would be set over, or null.</param>
    public readonly record struct BridgeLine(bool Allowed, string Reason, Point Near, Point Far, Bridge? Rebuilding)
    {
        /// <summary>How long it is end to end, in tiles rounded up — what it is priced by.</summary>
        public int Length => Bridge.LengthOf(Near, Far);
    }

    /// <summary>
    /// ⭐⭐ The one rule for where a bridge lies — a straight line from the press to the release, at any
    /// angle (Joe, D549: <i>"click and hold on one tile where they want the bridge to begin — then drag where
    /// they want the bridge to end, and release"</i>). Read by <see cref="CanBridge"/>, <see cref="MarkBridge"/>
    /// and the view's ghost, so the ghost can never show a bridge the sim would not build.
    /// </summary>
    /// <remarks>
    /// <b>Exactly as dragged</b> (Joe's call): both ends on dry land, water somewhere under the line, at most
    /// <c>bridge_max_length_tiles</c> end to end, and nothing standing, fenced or bridged under any of it.
    /// Land under the middle is allowed — it is walkable already — and is paid for, because a bridge is priced
    /// by its whole length. A press and release on one tile is a click: it rebuilds a wooden bridge in stone,
    /// and is otherwise told to drag.
    /// </remarks>
    public BridgeLine BridgeLineFrom(BuildingKind kind, Point from, Point to)
    {
        BuildingRow? row = BuildingsCatalog[kind];
        if (row is null || !row.SpansWater)
        {
            throw new ArgumentException($"A {kind} does not span water.", nameof(kind));
        }

        GridPos start = from.ToTile();
        GridPos end = to.ToTile();

        // ⭐ A CLICK, NOT A DRAG: on a standing wooden bridge with the stone one in hand, it is set in stone
        // where it stands (Joe, D549 call 3) — the same line, whichever way it was clicked.
        if (start == end)
        {
            if (row.Rebuilds is BuildingKind over && BridgeAt(start) is Bridge standing)
            {
                if (standing.Kind == kind)
                {
                    return No($"That is a {row.Name} already.");
                }

                if (standing.Kind != over)
                {
                    return No($"A {row.Name} cannot be set over a {BuildingsCatalog.NameOf(standing.Kind)}.");
                }

                if (BridgeSiteAt(start) is not null)
                {
                    return No("That bridge is being rebuilt already.");
                }

                return new BridgeLine(true, string.Empty, standing.Near, standing.Far, standing);
            }

            return No("Press on one bank and drag to the other.");
        }

        if (!Map.Contains(start) || !Map.Contains(end))
        {
            return No("That runs outside the valley.");
        }

        if (Map.TerrainAt(start) == Terrain.Water || Map.TerrainAt(end) == Terrain.Water)
        {
            return No("A bridge begins and ends on dry land.");
        }

        List<GridPos> under = Bridge.TilesUnder(from, to);
        bool crossesWater = false;
        for (int i = 0; i < under.Count; i++)
        {
            if (!Map.Contains(under[i]))
            {
                return No("That runs outside the valley.");
            }

            crossesWater |= Map.TerrainAt(under[i]) == Terrain.Water;
        }

        if (!crossesWater)
        {
            return No("A bridge has to cross water — drag from one bank to the other.");
        }

        int length = Bridge.LengthOf(from, to);
        if (length > Config.BridgeMaxLengthTiles)
        {
            return No($"That is {length} tiles end to end — a bridge can be {Config.BridgeMaxLengthTiles} at most.");
        }

        for (int i = 0; i < under.Count; i++)
        {
            if (WhyNotUnderABridge(under[i]) is string why)
            {
                return No(why);
            }
        }

        // ⭐ THE NEAR END IS THE ONE THE VILLAGE CAN WALK TO — the far one need not be; that is the point.
        // Both reachable is a second crossing, and the nearer end by walking is where its builders stand.
        GridPos village = FirstHomeOrFoundingSite();
        int toStart = TravelCost.Cost(village, start);
        int toEnd = TravelCost.Cost(village, end);
        if (toStart == TravelCostField.Unreachable && toEnd == TravelCostField.Unreachable)
        {
            return No("There is no route to there from the village.");
        }

        return toEnd < toStart
            ? new BridgeLine(true, string.Empty, to, from, null)
            : new BridgeLine(true, string.Empty, from, to, null);

        BridgeLine No(string reason) => new(false, reason, from, to, null);
    }

    /// <summary>Why a bridge may not lie over this tile — or null (`bridges.md §4.5`).</summary>
    /// <remarks>
    /// Trees and rock do not refuse it: they are walkable, so a bridge never asks for clearing.
    /// </remarks>
    private string? WhyNotUnderABridge(GridPos tile)
    {
        if (BridgeLandsOn(tile))
        {
            return "It would cross another bridge.";
        }

        if (StandsOn(tile))
        {
            return $"It would run into {NameOfWhatStandsAt(tile)}.";
        }

        if (WhoseYard(tile) is string yard)
        {
            return $"It would run through {yard} yard.";
        }

        if (Zones.GateOwnerFacing(tile) is int gated and not 0)
        {
            string whose = FindHousehold(gated) is Household family ? $"the {family.Name}s'" : "a family's";
            return $"It would block the lane in front of {whose} gate.";
        }

        return null;
    }

    /// <summary>What a bridge of this kind costs over this many tiles of length — the row is per tile (`bridges.md §4`).</summary>
    public BuildingRecipe BridgeRecipe(BuildingKind kind, int length)
    {
        BuildingRecipe each = BuildingRecipe.For(kind, Config);
        var priced = new MaterialCost[each.Materials.Count];
        for (int i = 0; i < priced.Length; i++)
        {
            priced[i] = new MaterialCost(each.Materials[i].Goods, each.Materials[i].Amount * length);
        }

        return new BuildingRecipe(each.WorkTicks * length, priced);
    }

    /// <summary>
    /// Whether a bridge may be dragged from <paramref name="from"/> to <paramref name="to"/> — the verdict the
    /// ghost shows while the player drags (`bridges.md §4`).
    /// </summary>
    public PlacementVerdict CanBridge(BuildingKind kind, Point from, Point to)
    {
        if (WhyNotYet(kind) is string locked)
        {
            return PlacementVerdict.No(locked);
        }

        BridgeLine line = BridgeLineFrom(kind, from, to);
        if (!line.Allowed)
        {
            return PlacementVerdict.No(line.Reason);
        }

        return WarningForNothingToRaiseItWith(kind) is string noBuilders
            ? PlacementVerdict.Yes(noBuilders)
            : PlacementVerdict.Fine;
    }

    /// <summary>
    /// Mark a bridge from <paramref name="from"/> to <paramref name="to"/> — a site on the near end, carrying the
    /// far one (Joe, D549). Both on one tile is a click: a stone rebuild of a wooden bridge, or a refusal.
    /// </summary>
    public PlacementVerdict MarkBridge(BuildingKind kind, Point from, Point to)
    {
        PlacementVerdict verdict = CanBridge(kind, from, to);
        if (!verdict.Allowed)
        {
            return verdict;
        }

        BridgeLine line = BridgeLineFrom(kind, from, to);
        BuildingRecipe recipe = BridgeRecipe(kind, line.Length);
        string name = line.Rebuilding is Bridge over ? over.Name : NameFor(kind);

        if (recipe.TotalMaterials == 0 && recipe.WorkTicks == 0)
        {
            RaiseTheBridge(kind, line.Near, line.Far, name, rebuilds: line.Rebuilding is not null);
            return verdict;
        }

        RaiseSiteFor(
            kind, line.Near,
            line.Rebuilding is null ? name : $"{name}, in stone",
            recipe, forHouseholdId: 0, bridgeTo: line.Far, rebuildsBridge: line.Rebuilding is not null);
        return verdict;
    }

    /// <summary><see cref="CanBuildAt(BuildingKind, Point, bool, Angle)"/> for a bridge: one point is a click.</summary>
    private PlacementVerdict CanBridgeAt(BuildingKind kind, Point where) => CanBridge(kind, where, where);

    /// <summary>A bridge's site is finished: the deck opens, or the old deck becomes the new (`bridges.md §5`).</summary>
    private void FinishABridge(Workplace site, ConstructionSite plan)
    {
        RetireWorkplace(site);
        string name = plan.RebuildsBridge && plan.Name.EndsWith(", in stone", StringComparison.Ordinal)
            ? plan.Name[..^", in stone".Length]
            : plan.Name;
        RaiseTheBridge(plan.Kind, site.Position, plan.BridgeTo!.Value, name, plan.RebuildsBridge);
    }

    private void RaiseTheBridge(BuildingKind kind, Point near, Point far, string name, bool rebuilds)
    {
        StandingChanged();
        Bridge? old = rebuilds ? Bridges.Find(b => (b.Near == near && b.Far == far) || (b.Near == far && b.Far == near)) : null;
        if (old is not null)
        {
            string was = BuildingsCatalog.NameOf(old.Kind);
            old.Kind = kind;
            old.RaisedAtTick = Tick;
            Narrate($"{Capitalised(name)} is set in stone now — the {was} carried the village across while it "
                + $"was rebuilt, and the crossing is quicker for it. {Clock.SeasonAndYear()}.", LogCategory.Building);
            return;
        }

        var bridge = new Bridge { Near = near, Far = far, Kind = kind, Name = name, RaisedAtTick = Tick };
        Bridges.Add(bridge);
        Narrate($"{Capitalised(name)} is finished — {bridge.Length} {(bridge.Length == 1 ? "tile" : "tiles")} end to "
            + $"end, and the far bank is a walk away now. {Clock.SeasonAndYear()}.", LogCategory.Building);
    }

    // ---------------------------------------------------------------
    //  Pulling one down (`bridges.md §5.4`)
    // ---------------------------------------------------------------

    /// <summary>The water tiles under a bridge — its deck.</summary>
    private List<GridPos> DeckOf(Bridge bridge)
    {
        var deck = new List<GridPos>();
        IReadOnlyList<GridPos> under = bridge.Tiles();
        for (int i = 0; i < under.Count; i++)
        {
            if (Map.TerrainAt(under[i]) == Terrain.Water)
            {
                deck.Add(under[i]);
            }
        }

        return deck;
    }

    /// <summary>Why this bridge may not be pulled down now — or null.</summary>
    public string? WhyNotPullDown(Bridge bridge)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        List<GridPos> deck = DeckOf(bridge);

        if (BridgeSiteAt(bridge.Near.ToTile()) is not null)
        {
            return "It is being rebuilt — pull the site down first.";
        }

        for (int i = 0; i < Villagers.Count; i++)
        {
            if (Villagers[i].Alive && deck.Contains(Villagers[i].Position.ToTile()))
            {
                return $"{Villagers[i].Name} is on the bridge.";
            }
        }

        return WhatLosingTheDeckWouldCutOff(deck);
    }

    /// <summary>Pull a bridge down, if nothing stops it — a share of its materials comes back (`bridges.md §5.4`).</summary>
    public PlacementVerdict Demolish(Bridge bridge)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        if (WhyNotPullDown(bridge) is string why)
        {
            return PlacementVerdict.No(why);
        }

        if (RepairSiteAt(bridge.Near.ToTile()) is Workplace mending)
        {
            CancelConstruction(mending);
        }

        StandingChanged();
        if (!Bridges.Remove(bridge))
        {
            throw new ArgumentException($"{bridge.Name} is not standing.", nameof(bridge));
        }

        IReadOnlyList<MaterialCost> back = RefundFor(BridgeRecipe(bridge.Kind, bridge.Length));
        string recovered = ReturnToStore(bridge.Near.ToTile(), back);
        Narrate(back.Count > 0
            ? $"{Capitalised(bridge.Name)} was pulled down — {recovered} recovered. {Clock.SeasonAndYear()}."
            : $"{Capitalised(bridge.Name)} was pulled down. {Clock.SeasonAndYear()}.", LogCategory.Building);
        return PlacementVerdict.Fine;
    }

    /// <summary>What the village could no longer reach without these deck tiles — a name, or null.</summary>
    private string? WhatLosingTheDeckWouldCutOff(List<GridPos> deck)
    {
        RebuildTheStandingIndexIfStale();
        bool[] before = SweepTheFreeGround(System.Array.Empty<GridPos>(), null);
        bool[] after = SweepTheFreeGround(deck, null);
        for (int i = 0; i < _standingFootprints.Count; i++)
        {
            if (HasAReachedNeighbour(before, _standingFootprints[i], null)
                && !HasAReachedNeighbour(after, _standingFootprints[i], null))
            {
                return $"Pulling it down would cut off {NameOfWhatStandsAt(_standingFootprints[i][0])} across the water.";
            }
        }

        return null;
    }

    // ---------------------------------------------------------------
    //  Learning them (`bridges.md §5.2`)
    // ---------------------------------------------------------------

    private int _mostEverAliveFor = -1;
    private int _mostEverAlive;

    /// <summary>
    /// ⭐ The most people the village has ever held at once — what the wooden bridge's unlock reads (Joe,
    /// D546: SCALE). <b>Derived from the villagers' own history</b>, so it is neither saved nor hashed: a
    /// villager's arrival (a founder at the first tick, a nomad at the tick they settled, everybody else at birth) and death are already
    /// state, and the peak is a function of them.
    /// </summary>
    /// <remarks>
    /// Recomputed only when somebody is added — the peak can only rise at an arrival, and a death after
    /// it changes nothing that already happened. O(n log n) over the village's whole history, once a birth.
    /// </remarks>
    public int MostEverAlive
    {
        get
        {
            if (_mostEverAliveFor == Villagers.Count)
            {
                return _mostEverAlive;
            }

            var events = new List<(ulong Tick, int Change)>(Villagers.Count * 2);
            for (int i = 0; i < Villagers.Count; i++)
            {
                Villager v = Villagers[i];
                // A nomad counts from the tick they came (D564), not from a birth they had elsewhere.
                ulong arrived = v.ArrivedAtTick != 0UL ? v.ArrivedAtTick
                    : v.Founder || v.BirthTick < 0 ? 0UL : (ulong)v.BirthTick;
                events.Add((arrived, +1));
                if (v.DiedAtTick is ulong died)
                {
                    events.Add((died, -1));
                }
            }

            // A death and an arrival on one tick: the death first — the conservative peak.
            events.Sort(static (x, y) => x.Tick != y.Tick ? x.Tick.CompareTo(y.Tick) : x.Change.CompareTo(y.Change));
            int alive = 0;
            int most = 0;
            for (int i = 0; i < events.Count; i++)
            {
                alive += events[i].Change;
                most = Math.Max(most, alive);
            }

            _mostEverAlive = most;
            _mostEverAliveFor = Villagers.Count;
            return most;
        }
    }

    /// <summary>
    /// Somebody has joined the village — and if that makes it big enough to bridge water for the first
    /// time, it stops and says so (D546; a stop like D442's).
    /// </summary>
    internal void NoticeTheVillageSize(bool knewHowToBridge)
    {
        if (knewHowToBridge || !IsUnlocked(BuildingKind.Bridge))
        {
            return;
        }

        // ⚠️ A MOMENT OF ITS OWN, NOT `LearnedByDoing`: this is learned by the village's size, not by
        // anybody's hands, so it does not introduce the tech-tree map — the library's and the town hall's
        // rule (Joe, D440). Measured: through `LearnedByDoing` it set `ShownTheTechTree` in every village
        // that reached ten before it had dug a quarry's stone, and moved four goldens for a popup.
        RaiseMoment(
            "The village learned to bridge water",
            $"{TheVillageLearnedToBridge("The village has grown to")} {Clock.SeasonAndYear()}.");
    }

    /// <summary>
    /// Whether the wooden bridge was learned by the people just added — asked by the knock, which says it in the
    /// nomads' own moment rather than raising a second one beside it (Joe, D576).
    /// </summary>
    internal bool LearnedToBridgeJustNow(bool knewHowToBridge) => !knewHowToBridge && IsUnlocked(BuildingKind.Bridge);

    /// <summary>The bridge's news, one wording for its own moment and for the nomads' (D576).</summary>
    internal string TheVillageLearnedToBridge(string grownTo) =>
        $"{grownTo} {MostEverAlive} people, and somebody has worked out how to put a span of timber across the "
        + "water. The far bank is there to be used: build a wooden bridge at the narrows.";

    /// <summary>
    /// A walker has stepped from a bank onto this deck — counted on the bridge and the village, and the
    /// stone bridge learned when the crossings reach its number (D546).
    /// </summary>
    internal void CrossedOnto(GridPos deckTile)
    {
        if (BridgeAt(deckTile) is not Bridge bridge)
        {
            return;
        }

        bool knewStone = IsUnlocked(BuildingKind.StoneBridge);
        bridge.Crossings++;
        bridge.CrossingsThisYear++;
        BridgeCrossings++;
        if (!knewStone && IsUnlocked(BuildingKind.StoneBridge))
        {
            LearnedByDoing(
                "The village learned to set a span in stone",
                $"Feet have crossed the village's bridges {BridgeCrossings.Grouped()} times, and somebody has "
                + "worked out how to set a span in stone — a quicker crossing than planks. A wooden bridge can be "
                + $"rebuilt in stone where it stands, and stays open while it is. {Clock.SeasonAndYear()}.");
        }
    }

    /// <summary>The year turned: each bridge's "this year" starts again.</summary>
    internal void CloseTheBridgeYear()
    {
        for (int i = 0; i < Bridges.Count; i++)
        {
            Bridges[i].CrossingsThisYear = 0;
        }
    }

    /// <summary>The wooden or stone bridge's lock, in words — <see cref="WhyNotYet"/>'s two bridge arms.</summary>
    private string? WhyNoBridgeYet(BuildingKind kind) => kind switch
    {
        BuildingKind.Bridge when MostEverAlive < Config.BridgeUnlockPopulation =>
            $"Nobody here has bridged water yet — the village has held {MostEverAlive} people, and learns it "
            + $"once it has held {Config.BridgeUnlockPopulation}.",
        BuildingKind.StoneBridge when BridgeCrossings < Config.StoneBridgeUnlockCrossings =>
            $"Nobody here can set a span in stone yet — feet have crossed the bridges {BridgeCrossings} of the "
            + $"{Config.StoneBridgeUnlockCrossings} times it takes to learn.",
        _ => null,
    };
}
