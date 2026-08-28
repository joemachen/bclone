namespace Bclone.Sim.Logging;

/// <summary>
/// What kind of thing a village-log line is about — <b>Joe, 2026-08-27</b>.
/// </summary>
/// <remarks>
/// <para>
/// <i>"the village log is doing so much work that maybe it needs color coded entries and a
/// filter for each category? deaths, important events in colors, general info in white.
/// optimize noise to signal ratio."</i>
/// </para>
/// <para>
/// <b>⭐⭐ DECIDED AT THE SOURCE, WHICH WAS HIS CALL AND IS THE WHOLE DESIGN.</b> The view could
/// have guessed a category by matching words in the sentence, and that would have been a second
/// place that knows what a death looks like — wrong the first time somebody rephrased an
/// epitaph. <b>The system that raises the event says what kind of event it is</b>, once, and the
/// view only chooses a colour.
/// </para>
/// <para>
/// <b>⭐ KEPT SMALL ON PURPOSE.</b> A category the player cannot make a decision about is a
/// filter nobody touches — these are the distinctions worth a switch, not a taxonomy of the
/// fifty-five things that narrate. <see cref="Ordinary"/> is the default and the majority, and
/// that is correct: most of what a village says is ordinary.
/// </para>
/// <para>
/// ⚠️ <b>Order is deliberate</b> — it is the order the filter switches appear in, and roughly
/// the order a player cares. Adding one is a data change plus a colour; it does not touch the
/// sim's decisions.
/// </para>
/// </remarks>
public enum LogCategory
{
    /// <summary>The village getting on with it — the default, and most of the log.</summary>
    Ordinary = 0,

    /// <summary>Somebody died, or is starving.</summary>
    Death = 1,

    /// <summary>Somebody was born, paired, or moved into a home.</summary>
    Life = 2,

    /// <summary>A technique worked out, written down, or lost. The village's memory.</summary>
    Discovery = 3,

    /// <summary>Something the player can still act on, and should.</summary>
    Warning = 4,

    /// <summary>A building marked out, finished, moved or pulled down.</summary>
    Building = 5,

    /// <summary>The turn of a season or a year, and what it carried.</summary>
    Season = 6,
}
