using Kith.Sim.Core;
using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ The snowfalls (A76, <c>specs/snowfall.md</c>, Joe: <i>"i hate the way it 'shows up'"</i>, then <i>"Snowfalls"</i>): the
/// snow comes in a few falls a year that you can see falling, and lies on the land as it falls — a frost first, then drifts
/// filling the hollows and the slopes turned from the light — and holds between falls. The sim has no snow and no weather:
/// this is the calendar drawn, a pure function of the tick and the world's seed, so a replayed save snows on the same days.
/// </summary>
public partial class VillageMap
{
    /// <summary>The snow at a moment: how hard it is falling (0–1) and how much lies on the ground (0–1).</summary>
    internal readonly record struct SnowNow(float Falling, float Lying);

    /// <summary>One snowfall: where it starts and how long it lasts, in seasons into the year, and its share of the winter's snow.</summary>
    internal readonly record struct SnowFall(double Start, double Length, double Share);

    /// <summary>
    /// The year's snowfalls (§3.1): <see cref="ValleyWatercolourLook.SnowFallsPerYear"/> of them, one in each equal slot of
    /// the window, each its own length and share — all drawn from the world's seed and the year, so every year differs and
    /// the same year is always the same. The shares sum to one: the winter is whole by the window's end.
    /// </summary>
    internal static SnowFall[] SnowFallsOf(long year, ulong seed, ValleyWatercolourLook look)
    {
        int least = Math.Max(1, look.SnowFallsPerYear.X), most = Math.Max(least, look.SnowFallsPerYear.Y);
        int count = least + (int)(SnowHash(seed, year, 0) % (ulong)(most - least + 1));
        double from = look.SnowFallWindow.X, slot = (look.SnowFallWindow.Y - look.SnowFallWindow.X) / count;
        var falls = new SnowFall[count];
        double total = 0;
        for (int i = 0; i < count; i++)
        {
            double length = Math.Min(slot, Lerp(look.SnowFallSeasons.X, look.SnowFallSeasons.Y, SnowUnit(seed, year, (i * 3) + 1)));
            double start = from + (i * slot) + ((slot - length) * SnowUnit(seed, year, (i * 3) + 2));
            double share = 0.6 + (0.8 * SnowUnit(seed, year, (i * 3) + 3));
            falls[i] = new SnowFall(start, length, share);
            total += share;
        }

        for (int i = 0; i < count; i++)
        {
            falls[i] = falls[i] with { Share = falls[i].Share / total };
        }

        return falls;
    }

    /// <summary>
    /// ⭐ The snow at <paramref name="tick"/> (§3.1): nothing from the end of winter to the window's first fall; during a fall
    /// it falls, easing in and out, and the ground whitens by the fall's share; between falls nothing falls and the ground
    /// holds; from the thaw's start the ground clears, gone by the end of winter. A pure function: the sim is untouched.
    /// </summary>
    internal static SnowNow SnowAt(double tick, int ticksPerSeason, ulong seed, ValleyWatercolourLook look)
    {
        double perSeason = Math.Max(1, ticksPerSeason);
        double seasons = tick / perSeason;
        long year = (long)Math.Floor(seasons / 4.0);
        double into = seasons - (year * 4.0);

        double thawStart = look.SnowThaw.X, thawLength = Math.Max(1e-6, look.SnowThaw.Y);
        if (into >= thawStart)
        {
            double thawed = Smooth(Math.Clamp((into - thawStart) / thawLength, 0.0, 1.0));
            return new SnowNow(0f, (float)(1.0 - thawed));
        }

        if (into < look.SnowFallWindow.X)
        {
            return new SnowNow(0f, 0f);
        }

        double lying = 0, falling = 0;
        foreach (SnowFall fall in SnowFallsOf(year, seed, look))
        {
            double through = (into - fall.Start) / fall.Length;
            lying += fall.Share * Smooth(Math.Clamp(through, 0.0, 1.0));
            if (through > 0.0 && through < 1.0)
            {
                falling = Math.Max(falling, Smooth(Math.Clamp(through / 0.2, 0.0, 1.0)) * Smooth(Math.Clamp((1.0 - through) / 0.2, 0.0, 1.0)));
            }
        }

        return new SnowNow((float)falling, (float)Math.Clamp(lying, 0.0, 1.0));
    }

    private static double Smooth(double t) => t * t * (3.0 - (2.0 * t));

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    /// <summary>A hash of the seed, the year and a salt (splitmix64's finaliser): the snowfalls' only source of variety.</summary>
    private static ulong SnowHash(ulong seed, long year, int salt)
    {
        ulong z = seed ^ (unchecked((ulong)year) * 0x9E3779B97F4A7C15UL) ^ ((ulong)salt * 0xBF58476D1CE4E5B9UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static double SnowUnit(ulong seed, long year, int salt) => (SnowHash(seed, year, salt) >> 11) * (1.0 / (1UL << 53));

    /// <summary>The snow now, as the village's tick and seed give it.</summary>
    private SnowNow TheSnowNow() =>
        SnowAt(_world!.Tick + _alpha, _world.Config.TicksPerSeason, _world.Seed, ValleyWatercolourLook.Current);

    /// <summary>The drifts' values to the ground's layer, once, as it is built (§3.2).</summary>
    private static void HandTheSnowLookOver(ShaderMaterial material, ValleyWatercolourLook look)
    {
        material.SetShaderParameter("snow_year", 0f);
        material.SetShaderParameter("snow_dust", look.SnowDust);
        material.SetShaderParameter("snow_drift", look.SnowDrift);
        material.SetShaderParameter("snow_drift_gain", look.SnowDriftGain);
        material.SetShaderParameter("snow_land", look.SnowLand);
        material.SetShaderParameter("snow_relief", look.SnowRelief);
    }

    // ---------------------------------------------------------------
    //  The falling flakes (§3.3): a see-through layer over the map
    // ---------------------------------------------------------------

    internal const string SnowfallShaderPath = "res://shaders/snowfall.gdshader";

    private Control? _snowfallLayer;
    private ShaderMaterial? _snowfallMaterial;

    /// <summary>The flakes' own clock: real seconds at the game's speed, capped (<see cref="ValleyWatercolourLook.SnowfallPaceCap"/>), held while paused.</summary>
    private double _snowClock;

    /// <summary>The layer, for the probe (null until the watercolour is first switched on).</summary>
    internal Control? SnowfallLayer => _snowfallLayer;

    /// <summary>The snowfall's layer and material — once, with the watercolour's. Over the map, under nothing it owns.</summary>
    private void BuildTheSnowfallLayer()
    {
        if (_snowfallLayer is not null)
        {
            return;
        }

        ValleyWatercolourLook look = ValleyWatercolourLook.Current;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(SnowfallShaderPath) };
        foreach ((string name, Variant value) in FlakeValues(look))
        {
            material.SetShaderParameter(name, value);
        }

        _snowfallMaterial = material;

        var layer = new Control
        {
            Name = "Snowfall",
            MouseFilter = MouseFilterEnum.Ignore,
            Material = material,
            Visible = false,
        };
        layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.Draw += () => layer.DrawRect(new Rect2(Vector2.Zero, layer.Size), Colors.White);
        AddChild(layer);
        _snowfallLayer = layer;
    }

    /// <summary>
    /// The snow to the layers, on the valley's turn of <c>_Draw</c> — uniforms only. The ground takes the snow lying; the
    /// flakes take how hard it falls, the zoom and where the land is on screen, and show only while snow falls.
    /// </summary>
    private void HandTheSnowOver(ShaderMaterial ground, SnowNow snow, long year)
    {
        ground.SetShaderParameter("snow_amount", snow.Lying);
        ground.SetShaderParameter("snow_year", (float)(year % 1024));
        if (_snowfallLayer is null || _snowfallMaterial is null)
        {
            return;
        }

        bool falling = _watercolour && snow.Falling > 0f;
        _snowfallLayer.Visible = falling;
        if (!falling)
        {
            return;
        }

        Transform2D toScreen = TileToScreen();
        _snowfallMaterial.SetShaderParameter("falling", snow.Falling);
        _snowfallMaterial.SetShaderParameter("px_per_tile", _pixelsPerTile);
        _snowfallMaterial.SetShaderParameter("tile_origin_px", toScreen.Origin);
        _snowfallMaterial.SetShaderParameter("clock_s", (float)_snowClock);
    }

    /// <summary>The year the tick is in (for the drifts' patch noise, which shifts a little each year).</summary>
    private long TheSnowYear() => (long)Math.Floor((_world!.Tick + _alpha) / Math.Max(1.0, _world.Config.TicksPerSeason) / 4.0);

    /// <summary>The flakes' clock step: real seconds at the game's speed, capped, while the village runs (§3.3).</summary>
    private void TickTheSnowfall(double delta)
    {
        if (Paused || _snowfallLayer is null)
        {
            return;
        }

        double pace = Math.Min(_flowPace, Math.Max(0.0, ValleyWatercolourLook.Current.SnowfallPaceCap));
        _snowClock = (_snowClock + (delta * pace)) % SnowClockPeriod;
    }

    /// <summary>The flakes' clock wraps here: a whole number of every layer's cells drift past in it, so the wrap is not seen.</summary>
    private const double SnowClockPeriod = 3600.0;

    /// <summary>The look's values for the flakes' layer, by uniform (A77): set once when the layer is built, asked by the probe.</summary>
    private static (string Name, Variant Value)[] FlakeValues(ValleyWatercolourLook look) =>
    [
        ("crystal_cell_tiles", look.SnowflakeCrystalCellTiles),
        ("crystal_size", look.SnowflakeCrystalSize),
        ("crystal_share", look.SnowflakeCrystalShare),
        ("crystal_alpha", look.SnowflakeCrystalAlpha),
        ("speck_cell_tiles", look.SnowflakeSpeckCellTiles),
        ("speck_size_px", look.SnowflakeSpeckSizePx),
        ("speck_share", look.SnowflakeSpeckShare),
        ("speck_alpha", look.SnowflakeSpeckAlpha),
        ("flake_life", look.SnowflakeLife),
        ("flake_wind", look.SnowflakeWind),
        ("flake_rise", look.SnowflakeRise),
        ("flake_spin", look.SnowflakeSpin),
        ("flake_srgb", Srgb(look.SnowflakeColour)),
        ("flake_wash_srgb", Srgb(look.SnowflakeWash)),
        ("flake_ink", new Vector4(look.SnowflakeInk.R, look.SnowflakeInk.G, look.SnowflakeInk.B, look.SnowflakeInk.A)),
        ("flake_ink_px", look.SnowflakeInkPx),
        ("flake_shadow", new Vector4(look.SnowflakeShadow.R, look.SnowflakeShadow.G, look.SnowflakeShadow.B, look.SnowflakeShadow.A)),
        ("flake_fade_px", look.SnowflakeFadePxPerTile),
    ];

    /// <summary>The uniforms the map sets each frame on the snowfall's layer.</summary>
    private static readonly string[] SnowfallFrameUniforms = { "falling", "px_per_tile", "tile_origin_px", "clock_s" };

    /// <summary>
    /// ⭐ The probe's <c>snowfall:</c> line (A76, <c>snowfall.md §6</c>): the schedule, over many years and two seeds, and the
    /// layers' hand-over. ⚠️ The shaders run only in a drawn frame, never headless (D340): renders are their check.
    /// </summary>
    internal string TheSnowfallHolds()
    {
        if (_world is not SimWorld world)
        {
            return "[widths] snowfall: ⛔ no world";
        }

        var faults = new List<string>();
        try
        {
            ValleyWatercolourLook look = ValleyWatercolourLook.Current;
            const int perSeason = 120; // the spec's 30-day season at four ticks a day, named here (trap 17)
            const int perYear = perSeason * 4;
            ulong[] seeds = { 12345UL, 0x9E3779B97F4A7C15UL };
            var schedules = new HashSet<string>();
            int years = 0, fallsSeen = 0;
            foreach (ulong seed in seeds)
            {
                for (long year = 0; year < 12; year++, years++)
                {
                    int falls = 0;
                    var starts = new List<int>();
                    var gains = new List<float>();
                    float atFallStart = 0f;
                    SnowNow before = SnowAt(year * perYear, perSeason, seed, look);
                    if (before.Lying != 0f || before.Falling != 0f)
                    {
                        faults.Add($"seed {seed} year {year}: snow at spring's first tick ({before.Lying:0.###} lying, {before.Falling:0.###} falling)");
                    }

                    for (int t = 1; t < perYear; t++)
                    {
                        double tick = (year * perYear) + t;
                        SnowNow now = SnowAt(tick, perSeason, seed, look);
                        double into = (double)t / perSeason;
                        if (now != SnowAt(tick, perSeason, seed, look))
                        {
                            faults.Add($"seed {seed} year {year} tick {t}: the same tick gave two answers");
                        }

                        // Nothing from spring until autumn's day 18 (2.6 seasons in), named here.
                        if (into < 2.6 && (now.Lying != 0f || now.Falling != 0f))
                        {
                            faults.Add($"seed {seed} year {year}: snow at {into:0.###} seasons in, before autumn's day 18");
                            break;
                        }

                        // Nothing falls after winter's day 20 (3.6667 seasons in).
                        if (into > 3.6667 && now.Falling != 0f)
                        {
                            faults.Add($"seed {seed} year {year}: snow falling at {into:0.###} seasons in, after winter's day 20");
                            break;
                        }

                        if (now.Falling > 0f && before.Falling == 0f)
                        {
                            falls++;
                            starts.Add(t);
                            atFallStart = before.Lying;
                        }

                        if (now.Falling == 0f && before.Falling > 0f)
                        {
                            gains.Add(now.Lying - atFallStart);
                        }

                        // Between falls the ground holds; while it falls it only whitens; in the thaw it only clears.
                        if (into < 3.7333 && now.Lying < before.Lying - 1e-6f)
                        {
                            faults.Add($"seed {seed} year {year}: the snow lying fell from {before.Lying:0.####} to {now.Lying:0.####} at {into:0.###} seasons in, before the thaw");
                            break;
                        }

                        if (now.Falling == 0f && before.Falling == 0f && into < 3.7333 && MathF.Abs(now.Lying - before.Lying) > 1e-6f)
                        {
                            faults.Add($"seed {seed} year {year}: the snow lying moved with nothing falling at {into:0.###} seasons in");
                            break;
                        }

                        if (into >= 3.7333 && now.Lying > before.Lying + 1e-6f)
                        {
                            faults.Add($"seed {seed} year {year}: the snow lying rose in the thaw at {into:0.###} seasons in");
                            break;
                        }

                        before = now;
                    }

                    // Whole at the thaw, gone by the year's last tick; three or four falls.
                    float atThaw = SnowAt((year * perYear) + (3.7333 * perSeason) - 0.5, perSeason, seed, look).Lying;
                    float atEnd = SnowAt((year * perYear) + perYear - 0.001, perSeason, seed, look).Lying;
                    if (MathF.Abs(atThaw - 1f) > 1e-4f || atEnd > 1e-3f || falls < 3 || falls > 4)
                    {
                        faults.Add($"seed {seed} year {year}: {atThaw:0.####} lying as the thaw starts (want 1), {atEnd:0.####} at the year's end (want 0), {falls} falls (want 3 or 4)");
                    }

                    // Every fall adds its share — none adds nothing (so the winter is whole only once the last has fallen).
                    if (gains.Count != falls || gains.Exists(g => g < 0.08f))
                    {
                        faults.Add($"seed {seed} year {year}: the falls added {string.Join(", ", gains.ConvertAll(g => g.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)))} (want each at least 0.08)");
                    }

                    fallsSeen += falls;
                    schedules.Add(string.Join(",", starts));
                }
            }

            // Years and seeds differ: every year's snowfalls, as the ticks they start on, its own.
            if (schedules.Count < years)
            {
                faults.Add($"only {schedules.Count} different years of snowfalls in {years} over two seeds — the years are not their own");
            }

            // The layers: with the tick on, the flakes' layer built, its shader declaring every uniform, the look's values handed
            // over, showing only while snow falls; with the tick off, never.
            bool wasOn = _watercolour;
            ShowWatercolour(true);
            ShaderMaterial? ground = _watercolourMaterial;
            ShaderMaterial? flakes = _snowfallMaterial;
            long y = 3;
            SnowFall first = SnowFallsOf(y, world.Seed, look)[0];
            SnowNow mid = SnowAt(((y * 4.0) + first.Start + (first.Length / 2.0)) * world.Config.TicksPerSeason, world.Config.TicksPerSeason, world.Seed, look);
            if (ground is not null)
            {
                HandTheSnowOver(ground, mid, y);
            }

            bool shownFalling = _snowfallLayer?.Visible == true;
            ShowWatercolour(false);
            bool hiddenAtOnce = _snowfallLayer?.Visible == false;
            ShowWatercolour(true);
            float handedLying = ground?.GetShaderParameter("snow_amount").AsSingle() ?? -1f;
            float handedFalling = flakes?.GetShaderParameter("falling").AsSingle() ?? -1f;
            if (ground is not null)
            {
                HandTheSnowOver(ground, new SnowNow(0f, 0.5f), y);
            }

            bool shownStill = _snowfallLayer?.Visible == true;
            ShowWatercolour(false);
            if (ground is not null)
            {
                HandTheSnowOver(ground, mid, y);
            }

            bool shownOff = _snowfallLayer?.Visible == true;
            ShowWatercolour(wasOn);
            if (!(mid.Falling > 0f) || !shownFalling || !hiddenAtOnce || shownStill || shownOff || MathF.Abs(handedLying - mid.Lying) > 1e-6f || MathF.Abs(handedFalling - mid.Falling) > 1e-6f)
            {
                faults.Add($"the flakes show {shownFalling} mid-fall (want true), {shownStill} with nothing falling and {shownOff} with the tick off (want false, false), hidden the moment the tick goes off {hiddenAtOnce} (want true); handed {handedLying:0.###} lying and {handedFalling:0.###} falling (want {mid.Lying:0.###}, {mid.Falling:0.###})");
            }

            if (flakes is null || flakes.Shader is null)
            {
                faults.Add("no snowfall material");
            }
            else
            {
                var declared = new HashSet<string>();
                foreach (Godot.Collections.Dictionary u in flakes.Shader.GetShaderUniformList())
                {
                    declared.Add(u["name"].AsString());
                }

                foreach (string name in SnowfallFrameUniforms.Concat(FlakeValues(look).Select(v => v.Name)))
                {
                    if (!declared.Contains(name))
                    {
                        faults.Add($"snowfall.gdshader declares no {name} (did it compile?)");
                    }
                }

                string[] off = FlakeValues(look).Where(v => GD.VarToStr(flakes.GetShaderParameter(v.Name)) != GD.VarToStr(v.Value)).Select(v => v.Name).ToArray();
                if (off.Length > 0)
                {
                    faults.Add($"a flake value on the layer is not the look file's: {string.Join(", ", off)}");
                }
            }

            if (ground is null
                || ground.GetShaderParameter("snow_dust").AsVector3() != look.SnowDust
                || ground.GetShaderParameter("snow_drift").AsVector4() != look.SnowDrift
                || ground.GetShaderParameter("snow_drift_gain").AsVector2() != look.SnowDriftGain
                || ground.GetShaderParameter("snow_land").AsVector4() != look.SnowLand
                || ground.GetShaderParameter("snow_relief").AsVector3() != look.SnowRelief)
            {
                faults.Add("a drift value on the ground's layer is not the look file's");
            }

            // The look snows: flakes that never fill a cell, a pace cap, a thaw over by the end of winter, a window before it.
            // Each flake's life a whole number of times into the clock's wrap (else every flake jumps there, once an hour).
            static bool Whole(float life) => life > 0f && Math.Abs((SnowClockPeriod / life) - Math.Round(SnowClockPeriod / life)) < 1e-6;
            if (!Whole(look.SnowflakeLife.X + look.SnowflakeLife.Y) || !Whole(look.SnowflakeLife.Z + look.SnowflakeLife.W)
                || look.SnowflakeLife.X <= 0f || look.SnowflakeLife.Z <= 0f || look.SnowflakeLife.Y <= 0f || look.SnowflakeLife.W <= 0f)
            {
                faults.Add($"a flake's life {look.SnowflakeLife} does not go a whole number of times into the clock's {SnowClockPeriod} s");
            }

            if (look.SnowflakeCrystalShare <= 0f || look.SnowflakeCrystalShare > 1f || look.SnowflakeSpeckShare <= 0f || look.SnowflakeSpeckShare > 1f
                || look.SnowfallPaceCap <= 0f || look.SnowfallPaceCap > 4f
                || look.SnowThaw.X + look.SnowThaw.Y > 4.0001f || look.SnowFallWindow.Y > look.SnowThaw.X || look.SnowFallWindow.X >= look.SnowFallWindow.Y)
            {
                faults.Add($"{ValleyWatercolourLook.Path} does not snow as §3 says: the flakes' share, the pace cap, the thaw over by winter's end, or the window before the thaw");
            }

            return faults.Count == 0
                ? $"[widths] snowfall: ✅ {fallsSeen} falls over {years} years and two seeds, three or four a year, none before autumn's day 18 or after winter's day 20, the ground holding between falls, whole at the thaw, clear by spring, every year's falls its own; the flakes showing only mid-fall with the tick on, every value handed over, every uniform declared"
                : $"[widths] snowfall: ⛔ {string.Join("; ", faults)}";
        }
        catch (Exception e)
        {
            // Trap 33: a probe line that throws never reaches `done.` and leaves a headless Godot running.
            return $"[widths] snowfall: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
