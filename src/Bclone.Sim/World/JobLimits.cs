namespace Bclone.Sim.World;

/// <summary>
/// How many people the player wants on each kind of work — Banished's professions panel
/// (D106, Joe).
/// </summary>
/// <remarks>
/// <para>
/// <b>The player says how many; the sim still says who.</b> A target states a number, never a
/// person: proximity, household and catchment go on choosing the worker, so every
/// <em>"why is Elias at the stand?"</em> sentence stays true. That is the line §2.2 holds, and
/// this sits the same side of it as <see cref="Workplace.StaffingOverride"/> (D51) and
/// <see cref="StockLimits"/> (D62). <b>Three halves of one control, now:</b> an override says
/// how many hands at <em>this building</em>, a stock limit says <em>until when</em>, and a job
/// target says how many hands on <em>this kind of work</em> anywhere.
/// </para>
/// <para>
/// <b>⭐ It is the answer to a problem the village could not solve for itself (D103).</b>
/// Building is funded from whatever is left after eating and heating, and measured, that is
/// <em>nothing</em> for most of the year: the quota rounds one spare hand down to no builders,
/// and a village that is short of its food target wants none at all. Joe watched a house go up
/// on the last day of the year and a second never get built. <b>A rule that fixes it for every
/// village kills some of them</b> — the same narrowing was tried twice and killed a valley
/// both times. <b>A player who can say "two builders" fixes it for the village in front of
/// them</b>, which is what a management sim is for.
/// </para>
/// <para>
/// <b>Derived floor, player ceiling — and here the player may raise as well as lower.</b>
/// Unlike a stock limit, a job target can ask for <em>more</em> than the village would choose,
/// because that is the whole point: the village's own answer is the thing being corrected. It
/// is still bounded by how many places actually exist (<see cref="Workplace.Capacity"/>) and by
/// how many people there are — you cannot staff a hut that has not been built.
/// </para>
/// <para>
/// <b>Player intent, therefore sim state:</b> hashed, deterministic, part of the seed contract,
/// exactly as zones (D42), staffing overrides (D51) and stock limits (D62) are. Null is the
/// default and hashes as absent, so a village played without ever opening the panel is
/// byte-identical to the one that came before the panel existed.
/// </para>
/// </remarks>
public sealed class JobLimits
{
    /// <summary>
    /// The kinds of work a target can be set on, in a fixed order.
    /// </summary>
    /// <remarks>
    /// Read off the enum rather than typed, so a new job kind cannot be left out of the panel
    /// by somebody forgetting a line — which is the mistake <see cref="StockLimits.Kinds"/>
    /// made and had to correct when stone and tools arrived. The order is the enum's own and is
    /// part of the hash, so a kind may be appended and never renumbered.
    /// </remarks>
    public static readonly IReadOnlyList<JobKind> Kinds = Enum.GetValues<JobKind>();

    private readonly int?[] _targets = new int?[Kinds.Count];

    /// <summary>
    /// How many the player has asked for on this kind of work, or null for
    /// <em>"let the village decide"</em>.
    /// </summary>
    /// <remarks>
    /// <b>Null and zero are different states, and both are hashed.</b> Null is <em>"I have no
    /// opinion"</em>; zero is <em>"nobody on this, I mean it"</em> — which is a real
    /// instruction, and the one that turns everybody into a laborer. Conflating them would let
    /// a determinism test pass across a real divergence (D51's trap, two controls over).
    /// </remarks>
    public int? For(JobKind kind)
    {
        int index = IndexOf(kind);
        return index < 0 ? null : _targets[index];
    }

    /// <summary>Set or clear the target for one kind. Returns true if it changed anything.</summary>
    /// <remarks>
    /// Negative targets are clamped to zero rather than rejected, exactly as a stock limit is:
    /// "nobody on this" is a coherent instruction and minus two is the same instruction typed
    /// badly.
    /// </remarks>
    public bool Set(JobKind kind, int? target)
    {
        int index = IndexOf(kind);
        if (index < 0)
        {
            return false;
        }

        if (target is < 0)
        {
            target = 0;
        }

        if (_targets[index] == target)
        {
            return false;
        }

        _targets[index] = target;
        return true;
    }

    /// <summary>Whether the player has expressed any opinion at all.</summary>
    public bool AnySet
    {
        get
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Copy the player's opinions onto another set, for cloning a world.</summary>
    public void CopyTo(JobLimits other)
    {
        ArgumentNullException.ThrowIfNull(other);

        for (int i = 0; i < _targets.Length; i++)
        {
            other._targets[i] = _targets[i];
        }
    }

    private static int IndexOf(JobKind kind)
    {
        for (int i = 0; i < Kinds.Count; i++)
        {
            if (Kinds[i] == kind)
            {
                return i;
            }
        }

        return -1;
    }
}
