using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ THE WINDMILL'S SAILS TURN WHILE IT IS WORKED (A145, <c>specs/building-roofs.md §6a</c>; Joe, relayed by the systems lane:
/// <i>"windmill definitely needs to animate, but only when it is being worked"</i>). The sails are laid with the roof as before,
/// as a run of their own (<see cref="TurnsWithTheSails"/>), and drawn turned about the hub; the body and cap over them stay
/// still. <b>True to the sim:</b> a miller grinding there this tick (<see cref="NoteTheFires"/>, once a tick, as the forge's fire
/// is read). They gather way and slow over a few seconds — no pop — on the people's clock (the game's speed capped at 1.5×,
/// still while paused). View only; nothing here is hashed.
/// </remarks>
public partial class VillageMap
{
    /// <summary>The sails at speed, radians a second (about one turn in seven seconds: a working mill, not a fan).</summary>
    internal const float SailRadiansASecond = 0.9f;

    /// <summary>How long the sails take to gather way from still, and to come to rest.</summary>
    internal const float SailSpinUpSeconds = 2.5f, SailSpinDownSeconds = 4f;

    /// <summary>The mills a miller is grinding at this tick — kept once a tick, where the states change.</summary>
    private readonly HashSet<int> _millsWorked = new();

    /// <summary>Pure: the pieces that turn with the sails — the cloth, its lattice and the spar.</summary>
    internal static bool TurnsWithTheSails(RoofPiece piece) => piece.Part is "sail" or "sail lattice" or "spar";

    /// <summary>Pure: a mill's hub, the centre of its <c>hub</c> piece — zero for a roof with none.</summary>
    internal static Vector2 HubOf(List<RoofPiece> pieces)
    {
        foreach (RoofPiece piece in pieces)
        {
            if (piece.Kind == RoofPieceKind.Fill && piece.Part == "hub")
            {
                return CentreOf(piece.Points);
            }
        }

        return Vector2.Zero;
    }

    /// <summary>Pure: the sails' speed after a frame — toward 1 worked, 0 not; paused (no seconds), held.</summary>
    internal static float SailSpeedOn(float speed, bool worked, double frameSeconds) =>
        worked
            ? Mathf.MoveToward(speed, 1f, (float)frameSeconds / SailSpinUpSeconds)
            : Mathf.MoveToward(speed, 0f, (float)frameSeconds / SailSpinDownSeconds);

    /// <summary>Pure: the turn about the hub, in tile space — the sails' run is drawn through it.</summary>
    internal static Transform2D SailsTurned(Vector2 hub, float radians) =>
        new Transform2D(radians, hub) * new Transform2D(0f, -hub);

    private void TurnTheSails(RoofCache held, bool worked)
    {
        held.SailSpeed = SailSpeedOn(held.SailSpeed, worked, _peopleFrameSeconds);
        held.SailsAt = Mathf.Wrap(held.SailsAt - (held.SailSpeed * SailRadiansASecond * (float)_peopleFrameSeconds), 0f, Mathf.Tau);
    }
}
