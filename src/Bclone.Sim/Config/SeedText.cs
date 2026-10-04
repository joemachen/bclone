using System.Text;
using Bclone.Sim.Determinism;

namespace Bclone.Sim.Config;

/// <summary>
/// What the player types as a seed, and the number it names (D477, `new-game-screen.md §4`).
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔ A seed somebody wrote down must name the same valley forever</b>, so this is pinned by tests
/// and never changed: a new rule would be a new function, not an edit to this one.
/// </para>
/// <para>
/// <b>A seed is read aloud and retyped</b>, so the text is normalised first — trimmed, runs of
/// whitespace made one space, lower-cased — and <em>"Mossy Lantern"</em> is <em>"mossy  lantern "</em>.
/// <b>A plain number stays that number</b>, so every seed already quoted in the decisions log (12345,
/// 41219) still means its valley. Anything else is folded byte by byte through splitmix64 — D466's
/// shape, the one copy of <see cref="SplitMix64.Fold"/>.
/// </para>
/// </remarks>
public static class SeedText
{
    /// <summary>The character that ends a seed and starts a share code's settings.</summary>
    public const char SettingsMark = '#';

    /// <summary>The text as it is hashed: trimmed, whitespace collapsed to one space, lower-cased.</summary>
    public static string Normalise(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var built = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                built.Append(' ');
                space = false;
            }

            built.Append(char.ToLowerInvariant(c));
        }

        return built.ToString();
    }

    /// <summary>
    /// The seed <paramref name="text"/> names, or <c>false</c> with the sentence the player reads.
    /// </summary>
    public static bool TryToSeed(string? text, out ulong seed, out string? refusal)
    {
        seed = 0;
        string normal = Normalise(text ?? string.Empty);

        if (normal.Length == 0)
        {
            refusal = "Type a seed, or roll one.";
            return false;
        }

        if (normal.Contains(SettingsMark, StringComparison.Ordinal))
        {
            refusal = $"A seed cannot hold \"{SettingsMark}\" — it starts the settings of a share code.";
            return false;
        }

        refusal = null;
        if (IsPlainNumber(normal) && ulong.TryParse(normal, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out ulong number))
        {
            seed = number;
            return true;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(normal);
        ulong state = 0;
        foreach (byte b in bytes)
        {
            state = SplitMix64.Fold(state, b);
        }

        seed = SplitMix64.Fold(state, (ulong)bytes.Length);
        return true;
    }

    /// <summary>The seed <paramref name="text"/> names; throws on text <see cref="TryToSeed"/> refuses.</summary>
    public static ulong ToSeed(string text) =>
        TryToSeed(text, out ulong seed, out string? refusal)
            ? seed
            : throw new ArgumentException(refusal, nameof(text));

    private static bool IsPlainNumber(string text)
    {
        foreach (char c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
