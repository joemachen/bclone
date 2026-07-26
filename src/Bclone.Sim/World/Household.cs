namespace Bclone.Sim.World;

/// <summary>
/// Where a villager lives, and — crucially — where their food is.
/// </summary>
/// <remarks>
/// <para>
/// Food is stored <b>per household</b>, not in one village pile (decision D14). That
/// is what makes one family starving beside a thriving neighbour possible, and that
/// asymmetry is where the inequality stories come from. A single global stockpile
/// would quietly make the village one organism.
/// </para>
/// <para>
/// The sharing policy that softens this is a <b>placeholder</b>. The intended form is
/// a manned market or food stall that redistributes within its catchment — a building
/// someone works at, not a menu setting. See DESIGN.md §2.2 and D14.
/// </para>
/// </remarks>
public sealed class Household
{
    public required int Id { get; init; }

    /// <summary>A family name, so a household reads as people rather than "Household 3".</summary>
    public required string Name { get; init; }

    public required GridPos HomePosition { get; init; }

    /// <summary>This household's food. Not the village's.</summary>
    public Stockpile Stockpile { get; } = new();

    /// <summary>
    /// Member ids, kept sorted ascending.
    /// </summary>
    /// <remarks>
    /// Sorted because iteration order is part of the determinism contract — an
    /// unordered membership list would make "who eats first" depend on insertion
    /// history. See specs/phase-1-households-and-labour.md §4b.
    /// </remarks>
    private readonly List<int> _memberIds = new();

    public IReadOnlyList<int> MemberIds => _memberIds;

    public void AddMember(int villagerId)
    {
        if (_memberIds.Contains(villagerId))
        {
            return;
        }

        // Insert in sorted position rather than appending and re-sorting, so the
        // list is never briefly out of order.
        int index = _memberIds.BinarySearch(villagerId);
        _memberIds.Insert(index < 0 ? ~index : index, villagerId);
    }

    public bool RemoveMember(int villagerId) => _memberIds.Remove(villagerId);

    /// <summary>True when nobody lives here any more — a house that outlived its family.</summary>
    public bool IsEmpty => _memberIds.Count == 0;
}
