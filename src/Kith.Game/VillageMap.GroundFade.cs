using System.Diagnostics;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ THE GROUND'S FADE (A300, <c>specs/ground-fade.md</c>; the art queue's piece 6, the trees' extras, at Joe's <i>"just power
/// through all these"</i>). <c>tree-motion.md</c> measured what still switched at once when a tile's wood changed: the valley's
/// painted floor — woods floor to grass at a fell, young-wood ground to woods floor at a growing — in the frame the sim changed
/// it, the largest change of a growing's film. Now the floor fades as the paths' field does (A169): the layer keeps the mix
/// as it was before the change beside the new one and blends between them over <see cref="GroundFadeTicks"/> of game time,
/// so a pause holds it and the game's speed scales it. Kept where the mix changes — on the bake's own upload counter — and a
/// frame only reads the clock (CLAUDE.md). A change mid-fade bakes what was on screen into the "from" side once, so nothing jumps.
/// </remarks>
public partial class VillageMap
{
    /// <summary>The fade's length in ticks: 2.25, three seconds at 1× — a growing's own length (A197), so the floor turns as the trees do.</summary>
    internal const float GroundFadeTicks = 2.25f;

    private ValleyTexture? _groundOf;
    private int _groundUploadsSeen;
    private byte[]? _groundShown;
    private byte[]? _groundSharesShown;
    private byte[]? _groundWas;
    private byte[]? _groundSharesWas;
    private ImageTexture? _groundFrom;
    private ImageTexture? _groundSharesFrom;
    private double _groundFadeStart = double.NegativeInfinity;

    /// <summary>How many fades have started, how many began mid-fade (the "from" side baked), and what the last start cost — for the probe.</summary>
    internal int GroundFades { get; private set; }

    internal int GroundFadeMidBakes { get; private set; }

    internal double LastGroundFadeMs { get; private set; }

    /// <summary>Pure: the fade, eased, <paramref name="ticks"/> after a change — 0 the old floor, 1 the new.</summary>
    internal static float GroundFadeAfter(double ticks)
    {
        float u = (float)System.Math.Clamp(ticks / GroundFadeTicks, 0.0, 1.0);
        return u * u * (3f - (2f * u));
    }

    /// <summary>
    /// The fade to the layer, after the bake's refresh: a new bake (a load, a founding, a first frame) snaps; a mix the bake
    /// has uploaded since last looked starts a fade from the floor as it showed; otherwise only the clock goes up.
    /// </summary>
    private void FadeTheGround(ShaderMaterial material, double now)
    {
        byte[]? weights = _valley.WeightsBytes, shares = _valley.SharesBytes;
        if (weights is null || shares is null || _world is null)
        {
            material.SetShaderParameter("ground_fade", 1f);
            return;
        }

        if (!ReferenceEquals(_valley, _groundOf) || _groundShown is null || _groundShown.Length != weights.Length)
        {
            _groundOf = _valley;
            _groundUploadsSeen = _valley.WeightsUploads;
            _groundShown = (byte[])weights.Clone();
            _groundSharesShown = (byte[])shares.Clone();
            _groundWas = null;
            _groundSharesWas = null;
            _groundFadeStart = double.NegativeInfinity;
        }
        else if (_valley.WeightsUploads != _groundUploadsSeen)
        {
            long started = Stopwatch.GetTimestamp();
            _groundUploadsSeen = _valley.WeightsUploads;
            float t = GroundFadeAfter(now - _groundFadeStart);
            bool mid = t < 1f && _groundWas is not null && _groundSharesWas is not null;
            _groundWas = mid ? LerpPathField(_groundWas!, _groundShown, t) : _groundShown;
            _groundSharesWas = mid ? LerpPathField(_groundSharesWas!, _groundSharesShown!, t) : _groundSharesShown!;
            if (mid)
            {
                GroundFadeMidBakes++;
            }

            int wide = _world.Config.MapWidth * ValleyTexture.WeightsPerTile, tall = _world.Config.MapHeight * ValleyTexture.WeightsPerTile;
            Upload(ref _groundFrom, _groundWas, Image.Format.Rgba8);
            Upload(ref _groundSharesFrom, _groundSharesWas, Image.Format.Rg8);
            _groundShown = (byte[])weights.Clone();
            _groundSharesShown = (byte[])shares.Clone();
            _groundFadeStart = now;
            GroundFades++;
            LastGroundFadeMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            void Upload(ref ImageTexture? texture, byte[] bytes, Image.Format format)
            {
                using Image image = Image.CreateFromData(wide, tall, false, format, bytes); // ⛔ `using` (A45)
                if (texture is null || texture.GetWidth() != wide || texture.GetHeight() != tall)
                {
                    texture = ImageTexture.CreateFromImage(image);
                }
                else
                {
                    texture.Update(image);
                }
            }
        }

        material.SetShaderParameter("ground_fade", GroundFadeAfter(now - _groundFadeStart));
        if (_groundFrom is not null && _groundSharesFrom is not null)
        {
            material.SetShaderParameter("ground_map_from", _groundFrom);
            material.SetShaderParameter("ground_map_shares_from", _groundSharesFrom);
        }
    }
}
