namespace Bclone.Sim.World;

/// <summary>
/// How much of each good the player wants the village to keep — the ceiling on production
/// (D62).
/// </summary>
/// <remarks>
/// <para>
/// <b>The player says how much; the sim still says who.</b> A limit states a goal, never a
/// person: proximity, household and catchment go on choosing the worker, so every
/// <em>"why is Elias at the stand?"</em> sentence stays true. That is the line §2.2 actually
/// holds, and this sits the same side of it as <see cref="Workplace.StaffingOverride"/>
/// (D51) — <b>an override says how many hands, a limit says until when.</b> Two halves of
/// one control.
/// </para>
/// <para>
/// <b>Derived floor, player ceiling.</b> <see cref="VillageEconomy"/> goes on deriving what
/// the village must produce not to die; a limit governs everything above that. Nothing
/// derived is deleted or overridden — the cap is applied after the derivation, not instead
/// of it. A limit set below the floor is <em>allowed and warned about</em>, which is D43's
/// pattern: a game that refuses the player's number is arguing with them, and one that obeys
/// it silently has killed them without saying so.
/// </para>
/// <para>
/// <b>Player intent, therefore sim state:</b> hashed, deterministic, part of the seed
/// contract, exactly as zones (D42) and staffing overrides (D51) are.
/// </para>
/// </remarks>
public sealed class StockLimits
{
    /// <summary>
    /// The goods a limit can be set on, in a fixed order.
    /// </summary>
    /// <remarks>
    /// <b>Enumerated, not a fixed list typed in somewhere</b> — which is what this said
    /// while being a fixed list typed in here. Adding stone and tools was the moment that
    /// mattered, and the list would have gone stale exactly as
    /// <c>EveryGoodTheGameHasCanBeLimited</c> was written to catch. It reads the enum now,
    /// so the guard cannot fail and the control describes what the game <em>has</em> rather
    /// than what somebody remembered. The order is the enum's own and is part of the hash,
    /// so a good may be appended and never renumbered.
    /// </remarks>
    public static readonly IReadOnlyList<Goods> Kinds = Enum.GetValues<Goods>();

    private readonly int?[] _limits;

    /// <summary>Limits for a run with <paramref name="slots"/> goods.</summary>
    /// <remarks>
    /// <b>Sized from the goods catalogue, not from <see cref="Kinds"/></b> (D210). The remark above
    /// says this control should *"describe what the game has rather than what somebody
    /// remembered"* — and once goods became rows, **the catalogue is what the game has.**
    /// </remarks>
    public StockLimits(int slots)
    {
        if (slots < 1)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(slots), $"A village needs at least one good to limit (got {slots}).");
        }

        _limits = new int?[slots];
    }

    /// <summary>How many goods this run can have an opinion about.</summary>
    public int Slots => _limits.Length;

    /// <summary>
    /// The limit on one good, or null for <em>"let the village decide"</em>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Null is the default and must stay the default.</b> A game that opens with a number
    /// against every good is the spreadsheet game non-negotiable 2 exists to delete, whatever
    /// the numbers say. Null is exactly today's derived behaviour, so a player who never
    /// opens this control plays the village that already existed — and there is a golden test
    /// which says so in hashes rather than in prose.
    /// </para>
    /// <para>
    /// <b>Null and zero are different states, and both are hashed.</b> Null is <em>"I have no
    /// opinion"</em>; zero is <em>"stop making this, I mean it"</em>. Conflating them would
    /// let a determinism test pass across a real divergence — D51 records this exact trap one
    /// control over.
    /// </para>
    /// </remarks>
    public int? For(Goods goods)
    {
        int index = IndexOf(goods);
        return index < 0 ? null : _limits[index];
    }

    /// <summary>Set or clear the limit on one good. Returns true if it changed anything.</summary>
    /// <remarks>
    /// Negative limits are clamped to zero rather than rejected. "Stop making this" is a
    /// coherent instruction and minus twenty is the same instruction typed badly; refusing it
    /// would be the game arguing with the player over a slip.
    /// </remarks>
    public bool Set(Goods goods, int? limit)
    {
        int index = IndexOf(goods);
        if (index < 0)
        {
            return false;
        }

        if (limit is < 0)
        {
            limit = 0;
        }

        if (_limits[index] == limit)
        {
            return false;
        }

        _limits[index] = limit;
        return true;
    }

    /// <summary>Whether the player has expressed any opinion at all.</summary>
    /// <remarks>
    /// Used by the view to decide whether to say anything, and by the hash to stay silent
    /// when there is nothing to say — see <c>StateHash</c>.
    /// </remarks>
    public bool AnySet
    {
        get
        {
            for (int i = 0; i < _limits.Length; i++)
            {
                if (_limits[i] is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Whether a good has reached its limit, given how much the village holds of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The caller passes the total, and which total it is matters more than it looks.</b>
    /// A limit must read the same supply the good's demand function reads, or the two
    /// disagree and the village acts on one while the player is shown the other.
    /// <c>LabourQuota.WoodcuttersWanted</c> counts <em>only firewood in the warehouse</em>,
    /// deliberately: firewood stacked in somebody else's home is not supply, because no
    /// errand reaches it. D29 is the record of the other reading — the village believed
    /// itself stocked, staffed one woodcutter, and froze to extinction with a hundred and
    /// eighty firewood sitting in homes and an empty warehouse.
    /// </para>
    /// <para>
    /// So this takes the total rather than fetching one, and the caller is responsible for
    /// asking the same question twice.
    /// </para>
    /// </remarks>
    public bool IsMet(Goods goods, int held)
    {
        int? limit = For(goods);
        return limit is not null && held >= limit.Value;
    }

    /// <summary>Copy the player's opinions onto another set, for cloning a world.</summary>
    public void CopyTo(StockLimits other)
    {
        ArgumentNullException.ThrowIfNull(other);

        for (int i = 0; i < _limits.Length; i++)
        {
            other._limits[i] = _limits[i];
        }
    }

    /// <summary>The slot for a good, or -1 if this run has no such good.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THIS WAS AN O(n) SEARCH FOR SOMETHING THAT IS ALREADY THE INDEX</b> (D210). It walked
    /// <see cref="Kinds"/> comparing each entry until it found a match — for a value whose
    /// <c>int</c> cast <em>is</em> its position, by the same appended-never-renumbered rule
    /// everything else here relies on. The same redundancy <c>BehaviorSystem.HeldOf</c> had.
    /// </para>
    /// <para>
    /// <b>And bounding it by the array rather than by the enum is what lets a modded good be
    /// limited at all</b> — with <see cref="Kinds"/> as the bound, a good above the built-in six
    /// returned -1, and <see cref="Set"/> silently answered <c>false</c>: *the player sets a limit,
    /// the control reports no change, and nothing says why.*
    /// </para>
    /// </remarks>
    private int IndexOf(Goods goods)
    {
        int index = (int)goods;
        return index >= 0 && index < _limits.Length ? index : -1;
    }
}
