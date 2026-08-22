using Bclone.Sim.World;

namespace Bclone.Sim.Tests;

/// <summary>
/// Test-side readers for state that became optional with the cold start (D70).
/// </summary>
/// <remarks>
/// <see cref="Household.HomePosition"/> is nullable now, because the founders arrive with no
/// roof. Almost every test in this suite is about a village that has long since built one, so
/// they want the position and not the question — but reaching for <c>!</c> in each of them
/// would turn a real regression into a <see cref="NullReferenceException"/> forty lines from
/// the cause. This says what the test is assuming, and says it in the failure message.
/// </remarks>
internal static class HouseholdTestExtensions
{
    /// <summary>The house this family lives in, asserting that they have one.</summary>
    public static GridPos Home(this Household household)
    {
        ArgumentNullException.ThrowIfNull(household);

        return household.HomePosition
            ?? throw new InvalidOperationException(
                $"The {household.Name} household has no house. This test assumes a village "
                + "that has already built one — if that is the thing under test, ask "
                + "HasHome rather than Home.");
    }
}
