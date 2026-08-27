namespace Bclone.Sim.World;

/// <summary>The four seasons, in order. Winter is the one with teeth.</summary>
public enum Season
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3,
}

/// <summary>What the calendar decides, asked of the season rather than listed at call sites.</summary>
/// <remarks>
/// <b>Deliberately the same shape as <see cref="TerrainRules"/></b>, which is this project's
/// existing answer to the same problem: a rule about a value belongs with the value, not
/// repeated at eleven call sites where one of them eventually gets it backwards.
/// </remarks>
public static class SeasonRules
{
    /// <summary>
    /// Nothing can be picked in winter, which is what makes it the year's pressure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Food scarcity was winter's <em>only</em> weapon in Phase 0, whose spec ruled out
    /// "a second overlapping death system" by name. D17 reversed that deliberately once
    /// there were households to heat, so cold is now a second axis — see
    /// <see cref="Villager.Cold"/> and <see cref="CauseOfDeath.Cold"/>. The half of the
    /// Phase 0 reasoning that did not expire is that a death must never be ambiguous
    /// between the two.
    /// </para>
    /// <para>
    /// <b>This was <c>FoodSource.IsGatherable</c> until D159</b>, and it was the only live
    /// member of a class whose other half — a position and a yield — belonged to Phase 0's
    /// single berry patch and had been dead since step C retired it. Reading
    /// <c>FoodSource.IsGatherable(season)</c> asked a berry patch a question in a game that
    /// no longer has berry patches.
    /// </para>
    /// </remarks>
    public static bool IsGatherable(Season season) => season != Season.Winter;

    /// <summary>Spring, and only spring — the year's one commitment (D161).</summary>
    /// <remarks>
    /// <b>⚠️ A missed sowing is a missed year, and that is the mechanic rather than a
    /// harshness.</b> If a field could be sown in summer, spring would stop being a decision
    /// and the calendar would stop having a shape — which is the whole thing
    /// <c>crops-and-orchards.md</c> exists to give the year. It is only fair because the
    /// village says so early and loudly (§5.1, §1.1, D88).
    /// </remarks>
    public static bool IsSowing(Season season) => season == Season.Spring;

    /// <summary>Autumn, and only autumn. What fall is <em>for</em>.</summary>
    /// <remarks>
    /// The counterweight to <see cref="IsSowing"/>: a standing crop that is not taken in autumn
    /// is taken by winter instead (Joe — *use it or lose it*).
    /// </remarks>
    public static bool IsReaping(Season season) => season == Season.Fall;
}

/// <summary>What a villager is doing right now.</summary>
public enum VillagerState
{
    Idle,
    TravelingToFood,
    Gathering,
    TravelingHome,
    TravelingToTrees,
    Cutting,
    Resting,
    Dead,

    /// <summary>Walking to the woodcutter's hut.</summary>
    TravelingToHut,

    /// <summary>Splitting logs into firewood at the hut (D29).</summary>
    MakingFirewood,

    /// <summary>Hauling a load to a store building (D30).</summary>
    HaulingToStore,

    /// <summary>Walking to a store to collect what the household is short of (D30).</summary>
    FetchingFromStore,

    /// <summary>Breaking off work to get to a fire before the cold does for them (D45).</summary>
    SeekingShelter,

    /// <summary>A marketer walking to pick up goods that are in the wrong place (D14).</summary>
    CollectingForMarket,

    /// <summary>A marketer carrying goods to the household that needs them (D14).</summary>
    DeliveringToHome,

    /// <summary>
    /// A marketer carrying a load to the <b>market's own store</b>
    /// (`storage-and-distribution.md §14.8`, D197).
    /// </summary>
    /// <remarks>
    /// <b>Its own state rather than a variant of <see cref="HaulingToStore"/>, for the reason
    /// <see cref="HaulingToFarm"/> is one:</b> `HaulingToStore` re-asks *"which store is nearest
    /// with room?"* on every tick of the walk, so a marketer who picked up at the granary would
    /// re-target the granary and put the load straight back. **This leg has a destination that
    /// was chosen when it was planned**, which is the same rule the collect and deliver legs
    /// already follow — *re-deciding mid-walk let a marketer shuttle between two sources for
    /// ever and complete nothing.*
    /// </remarks>
    StockingTheMarket,

    /// <summary>A builder fetching materials for a site the player marked out (D43).</summary>
    FetchingMaterials,

    /// <summary>A builder raising a marked building.</summary>
    Building,

    /// <summary>Clearing a tile the village painted for harvest (D87).</summary>
    /// <remarks>
    /// <b>Not a job, and that is the point.</b> This is what an able adult does when their
    /// own work has nothing for them this tick — a laborer who holds no job at all, or a
    /// forester in a winter with no timber wanted, or a woodcutter whose yard is empty.
    /// Their primary work always comes first (Joe, D87); this is helping out on the side.
    /// </remarks>
    Clearing,

    /// <summary>Fetching a load somebody left on the ground and taking it to a store (D96).</summary>
    /// <remarks>
    /// <b>Its own errand, and it has to be.</b> Goods on the ground are supply-invisible, so
    /// nothing can pull them in the way <em>"the village wants more food"</em> pulls a
    /// forager — that question reads stores. The errand is stated from the other end instead:
    /// <em>there is a load lying about; take it to a store.</em> That is D66's missing hauling
    /// arriving a second time, and unlike the first it needs no construction site to exist.
    /// Like <see cref="Clearing"/> it is what somebody does when their own work has nothing
    /// for them this tick.
    /// </remarks>
    TidyingGround,

    /// <summary>Walking out to a tile of the farm's own ground (`crops-and-orchards.md`).</summary>
    TravelingToField,

    /// <summary>Putting a tile of field under seed. Spring's one commitment.</summary>
    Sowing,

    /// <summary>Taking a tile of standing crop. What autumn is for.</summary>
    Reaping,

    /// <summary>
    /// Carrying a harvest to the farm's own store, which is underfoot (`farm_store_cap`).
    /// </summary>
    /// <remarks>
    /// <b>Its own state rather than a variant of <see cref="HaulingToStore"/>, because the
    /// destination is a different kind of thing.</b> A <c>StoreBuilding</c> and a
    /// <c>Workplace.Store</c> live in two lists with two id spaces (D36's seam), and
    /// <c>StoreForTheLoad</c> answers only about the first. Folding them together would make
    /// this a second way to find a store, which D145 names as the moment a control becomes
    /// unsafe — so the two destinations stay two answers, chosen once, in one place.
    /// </remarks>
    HaulingToFarm,

    /// <summary>Walking to a store the player has asked to be cleared out.</summary>
    /// <remarks>
    /// <b>Its own errand, and it only carries the OUTWARD leg.</b> Once they have an armful the
    /// ordinary carrying logic takes over -- `HaulOrSetDown` walks it to the nearest store that
    /// will have it, and the emptying store refuses everything while the request stands, so the
    /// load can never come straight back. <em>Two rules, no shuttling, and no new hauling
    /// machinery.</em>
    ///
    /// Appended, never renumbered, like every other hashed enum in this project.
    /// </remarks>
    ClearingAStore,
}

/// <summary>
/// Coarse bands of physical decline, used to narrate the turn of a life once
/// rather than every tick.
/// </summary>
public enum VigourStage
{
    /// <summary>Working at full strength.</summary>
    Prime = 0,

    /// <summary>Past their peak — the same work takes more trips.</summary>
    Slowing = 1,

    /// <summary>Visibly failing. Every winter is now a question.</summary>
    Frail = 2,
}

/// <summary>
/// What a villager is capable of, derived from age.
/// </summary>
/// <remarks>
/// Childhood exists from Phase 1 rather than Phase 0 because it only makes sense
/// once there is a household to depend on — a frail child alone is just an
/// unsurvivable opening (decision D13).
/// </remarks>
public enum LifeStage
{
    /// <summary>Too young to work. Eats from the household store and gives nothing back.</summary>
    Child = 0,

    /// <summary>Working age.</summary>
    Adult = 1,

    /// <summary>Still working, but visibly declining — see <see cref="VigourStage"/>.</summary>
    Elder = 2,
}

/// <summary>How a villager's life ended.</summary>
public enum CauseOfDeath
{
    /// <summary>Still alive.</summary>
    None,

    /// <summary>Ran out of food. The failure arc.</summary>
    Starvation,

    /// <summary>Lived a full life. The good arc.</summary>
    OldAge,

    /// <summary>
    /// Froze. The household ran out of firewood in winter (D29).
    /// </summary>
    /// <remarks>
    /// Phase 0 refused a second death system on the grounds that winter's danger
    /// should be food and nothing else. Reversing that is deliberate, and the
    /// condition attached to the reversal is that a death must never be <em>ambiguous</em>
    /// between this and <see cref="Starvation"/> — see <c>MortalitySystem</c>.
    /// </remarks>
    Cold,
}
