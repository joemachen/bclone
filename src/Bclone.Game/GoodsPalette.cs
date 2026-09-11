using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// What colour a good reads as — <b>one answer, used by the panel chip and the heap on the map</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>It moved out of <c>Main</c> the day a second caller appeared</b> (2026-09-05, goods drawn
/// where they lie). A chip in the overview and a heap in the valley have to be the same colour or
/// they are two facts rather than one, and the alternative — <c>VillageMap</c> reaching into the
/// UI class for a private helper — has the dependency backwards.
/// </para>
/// <para>
/// Colours are borrowed from the map where the map has one: logs and firewood are the timber
/// colours, stone and iron the seam colours, so a chip here and a tile in the valley mean the
/// same thing. That is the only reason to colour them at all.
/// </para>
/// <para>
/// ⚠️ <b>Coloured shapes rather than icons</b>: the project ships no image assets (D26), and an
/// emoji glyph is at the mercy of whatever the default font happens to cover. A
/// <c>ColorRect</c> draws the same on every machine.
/// </para>
/// </remarks>
internal static class GoodsPalette
{
    /// <summary>The colour this good is drawn in, anywhere it is drawn.</summary>
    internal static Color ColourOf(Goods goods) => goods switch
    {
        // Produce has no tile of its own since the thickets became forest, so it keeps the
        // berry colour it had when it did.
        Goods.Produce => new Color(0.82f, 0.35f, 0.38f),
        Goods.Logs => new Color(0.45f, 0.33f, 0.20f),
        Goods.Firewood => new Color(0.88f, 0.55f, 0.24f),
        Goods.Stone => new Color(0.62f, 0.62f, 0.64f),
        Goods.Tools => new Color(0.72f, 0.76f, 0.82f),
        Goods.Iron => new Color(0.55f, 0.36f, 0.30f),

        // The three that shipped without one and read as drab white beside the food they
        // belong to. Fish takes the river's blue, meat a deeper red than the berries above
        // it, leather the tan of the hide it is.
        Goods.Fish => new Color(0.38f, 0.60f, 0.78f),
        Goods.Meat => new Color(0.66f, 0.26f, 0.28f),
        Goods.Leather => new Color(0.60f, 0.45f, 0.30f),

        // ⭐ Straw gold (D348) — and it is the colour a ripe field draws in, so the map and
        // the Overview agree about what wheat looks like.
        Goods.Wheat => new Color(0.86f, 0.72f, 0.32f),

        // ⚠️ A MOD-ADDED GOOD GETS A COLOUR RATHER THAN A CRASH, and it is deliberately drab.
        // `goods-catalog.md §9.4` asks whether a mod-added good needs a display colour and calls
        // it *"the first thing a modder will ask for"*. Until that is answered, a neutral grey is
        // the honest answer: the row is legible, and the greyness says *nobody chose this colour*
        // rather than implying somebody did.
        _ => new Color(0.70f, 0.70f, 0.70f),
    };
}
