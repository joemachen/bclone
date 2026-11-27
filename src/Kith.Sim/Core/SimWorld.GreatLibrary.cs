using Kith.Sim.World;

namespace Kith.Sim.Core;

/// <summary>⭐ The great library (W5, `skills-for-every-trade.md §7a`, D922).</summary>
public sealed partial class SimWorld
{
    /// <summary>How many techniques the village knows now, written or held — the great library's progress.</summary>
    public int TechniquesKnown()
    {
        int known = 0;
        for (int id = 0; id < KnowledgeStates.Length; id++)
        {
            if (KnowledgeStates[id] != KnowledgeState.Unknown)
            {
                known++;
            }
        }

        return known;
    }

    /// <summary>
    /// The first time the village knows every technique there is: the great library is learned and given (Joe: <i>"should
    /// unlock … when the 11th technique has been learned - whether or not any other libraries have been built"</i>).
    /// </summary>
    /// <remarks>Asked by <see cref="Systems.KnowledgeSystem"/> after its pass — a walk of eleven states, once a tick.</remarks>
    internal void NoticeIfEveryTechniqueIsKnown()
    {
        if (GreatLibraryLearned || TechniquesCatalog.Count == 0 || TechniquesKnown() < TechniquesCatalog.Count)
        {
            return;
        }

        GreatLibraryLearned = true;
        AFreeGreatLibraryIsOwed = true;
        string name = BuildingsCatalog[BuildingKind.GreatLibrary]?.Name ?? "great library";
        RaiseMoment(
            $"The village knows every technique there is",
            $"Every technique the village could work out is known here now — {TechniquesCatalog.Count} of them. They have "
                + $"gathered timber and stone for a {name}, with a shelf for each — put it wherever you like, and it will "
                + $"cost you nothing. {Clock.SeasonAndYear()}.");
    }
}
