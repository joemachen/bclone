using Kith.Sim.World;

namespace Kith.Sim.Core;

/// <summary>
/// ⭐ The tavern's questions (W3, `specs/tavern.md`, D834): which tavern a home is near enough to, and — from step 3 — whether
/// it is open. One copy each, read by the brewer's stop, the hiring, the evening's offer and the card.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>
    /// The nearest standing tavern within <c>tavern_reach_tiles</c> of this adult's home by the one cost field — or null
    /// (no home, no tavern, or none near enough). The walk, never a ring on the map (D120): a tavern across a river is far.
    /// </summary>
    /// <remarks>
    /// Ties go to the lower id (D15). The cost is asked toward the tavern, so the field is the tavern's, cached by the cost
    /// field and shared by every home that asks.
    /// </remarks>
    public Workplace? TavernWithinReachOf(Villager villager)
    {
        ArgumentNullException.ThrowIfNull(villager);
        if (FindHousehold(villager.HouseholdId) is not { HomeTile: GridPos home })
        {
            return null;
        }

        int reach = Config.TavernReachTiles * TravelCostField.BaseTileCost;
        Workplace? best = null;
        int bestCost = int.MaxValue;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace tavern = Workplaces[i];
            if (tavern.Kind != JobKind.TavernKeeper || tavern.IsSite || tavern.Closed)
            {
                continue;
            }

            int cost = TravelCost.Cost(home, tavern.Tile);
            if (cost != TravelCostField.Unreachable && cost <= reach && cost < bestCost)
            {
                best = tavern;
                bestCost = cost;
            }
        }

        return best;
    }

    /// <summary>The standing tavern on this tile (an errand's fixed destination), or null.</summary>
    public Workplace? TavernAt(GridPos tile)
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i] is { Kind: JobKind.TavernKeeper, IsSite: false } tavern && tavern.Tile == tile)
            {
                return tavern;
            }
        }

        return null;
    }

    /// <summary>
    /// ⭐ Whether a tavern is open (W3, `tavern.md §5.4`; Joe's call 3B): <b>kept</b> — a keeper seated — and holding ale of its
    /// own. One question, read by the evening's offer, its arrival and the card's sentence.
    /// </summary>
    public bool TavernIsOpen(Workplace tavern) => WhyTheTavernIsShut(tavern) is null;

    /// <summary>
    /// Why a tavern is shut, in a sentence naming what to do — or null while it is open (W3). In the order the player can act
    /// on them: fallen in, no keeper (and why, when food comes first), dry.
    /// </summary>
    public string? WhyTheTavernIsShut(Workplace tavern)
    {
        ArgumentNullException.ThrowIfNull(tavern);
        if (tavern.IsSite)
        {
            return "Not built yet.";
        }

        if (tavern.Closed)
        {
            return "It has fallen in — the tavern is shut until the builders mend it.";
        }

        if (tavern.WorkerIds.Count == 0)
        {
            return SeasonRules.IsGatherable(Clock.Season) && LabourQuota.VillageIsShortOfFood(this)
                ? "Shut while the village is short of food — its keeper is needed for the food first."
                : "No keeper — the tavern is shut until somebody keeps it.";
        }

        if (tavern.Store[Goods.Ale] <= 0)
        {
            return StoreHoldingAle(tavern.Tile) is null
                ? "The tavern is dry — no store within reach has ale. A brewery makes it from barley."
                : "The tavern is dry until its keeper carries the ale in.";
        }

        return null;
    }

    /// <summary>Whether the keeper should fetch ale: the tavern holds less than <c>tavern_stock</c>.</summary>
    public bool TavernWantsAle(Workplace tavern)
    {
        ArgumentNullException.ThrowIfNull(tavern);
        return tavern.Store[Goods.Ale] < Config.TavernStock;
    }

    /// <summary>
    /// The nearest store a keeper may fetch ale from — never the market (only a trader stocks the counter, D358), never a
    /// cart (a household's). The one question the keeper's walk, the hiring and the card ask (D713's shape).
    /// </summary>
    public StoreBuilding? StoreHoldingAle(GridPos from) =>
        NearestStoreAccepting(from, Goods.Ale, store => store.Kind is not (StoreKind.Market or StoreKind.Cart) && store.Store[Goods.Ale] > 0);
}
