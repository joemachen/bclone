using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ The herd lives (D592, commit 2 of `specs/trade.md §11`; `specs/livestock.md §0`) — on a barn, once a day.
/// </summary>
/// <remarks>
/// <para>
/// <b>The woodpile's shape a second time</b> (`livestock.md §3`, D53): grazing is free spring to autumn; winter is not,
/// and the honest answer to <i>"why did the cattle die?"</i> is <b>the hay chain failed</b> — one beast for each winter
/// day the barn comes up short (Joe, D60: <i>the herd starves</i>).
/// </para>
/// <para>
/// ⭐ <b>Once a day, never per tick</b>, on the day's first tick (the hearth's beat): everything here is a whole day's
/// worth, in integers. The pasture is read from <c>ZoneMap.WorkGroundTiles</c>, a count kept where the paint changes —
/// never a walk of the ground (CLAUDE.md).
/// </para>
/// <para>
/// ⚠️ Nobody tends the herd yet: the herdsman — cutting hay, feeding it out, the cull as work — is commit 3. Here a
/// barn's hay simply feeds its herd, and a beast born to a full pasture is culled into the barn's buffer.
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>How many head a barn's pasture holds: its painted tiles over the tiles a head grazes.</summary>
    public int PastureFor(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        return Config.Animals.Count == 0 ? 0 : Zones.WorkGroundTiles(barn.Id) / Config.Animals[0].PastureTilesPerHead;
    }

    /// <summary>The store a barn keeps its hay in — the barn's own building, on its own tile.</summary>
    public StoreBuilding? BarnStoreOf(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        return StoreAt(barn.Tile) is { Kind: StoreKind.Barn } store ? store : null;
    }

    /// <summary>A day's grazing, feeding, growth, cull and starvation for every herd — on the day's first tick only.</summary>
    internal void TendTheHerds()
    {
        if (Config.Animals.Count == 0 || Tick % (ulong)Config.TicksPerDay != 0UL)
        {
            return;
        }

        AnimalRow animal = Config.Animals[0];
        bool winter = Clock.IsWinter;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace barn = Workplaces[i];
            if (barn.Kind != JobKind.Herdsman || barn.IsSite || barn.HerdHead <= 0)
            {
                continue;
            }

            if (winter)
            {
                FeedTheHerd(barn, animal);
            }
            else
            {
                GrazeAndGrow(barn, animal);
            }
        }
    }

    /// <summary>
    /// A winter day: the herd eats its hay from the barn, and if the barn comes up short a beast is lost.
    /// </summary>
    private void FeedTheHerd(Workplace barn, AnimalRow animal)
    {
        int wanted = barn.HerdHead * animal.HayPerHeadPerWinterDay;
        Stockpile? store = BarnStoreOf(barn)?.Store;
        bool fed = store is not null && store.TryTake(Goods.Hay, wanted);
        if (fed)
        {
            return;
        }

        store?.TakeAll(Goods.Hay);
        barn.HerdHead--;
        Narrate(
            $"One of the {animal.Plural} at {barn.Name} starved — the barn ran out of hay. "
            + $"{barn.HerdHead} left. {Clock.SeasonAndYear()}.",
            LogCategory.Warning);
    }

    /// <summary>
    /// A grazing day: a herd larger than its pasture is culled down a beast a day; otherwise it grows by its head count,
    /// a beast for every <c>head_days_per_birth</c>, and a beast born to a full pasture is culled.
    /// </summary>
    private void GrazeAndGrow(Workplace barn, AnimalRow animal)
    {
        int pasture = PastureFor(barn);
        if (barn.HerdHead > pasture)
        {
            barn.HerdHead--;
            Cull(barn, animal);
            return;
        }

        barn.HerdGrowth += barn.HerdHead;
        while (barn.HerdGrowth >= animal.HeadDaysPerBirth)
        {
            barn.HerdGrowth -= animal.HeadDaysPerBirth;
            if (barn.HerdHead < pasture)
            {
                barn.HerdHead++;
            }
            else
            {
                Cull(barn, animal);
            }
        }
    }

    /// <summary>One beast into meat and leather in the barn's buffer — the laborers carry it away as any buffer's.</summary>
    private void Cull(Workplace barn, AnimalRow animal)
    {
        barn.Store.Add(Goods.Meat, animal.MeatPerHead);
        barn.Store.Add(Goods.Leather, animal.LeatherPerHead);
        RecordFoodProduced(Goods.Meat, animal.MeatPerHead);
    }
}
