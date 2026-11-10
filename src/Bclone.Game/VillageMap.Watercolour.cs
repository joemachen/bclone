using Bclone.Sim.Config;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐⭐ The valley in watercolour — the terrain shader drawn on the real ground (<c>specs/terrain-shader.md §5</c>, A22–A25),
/// behind Joe's tick <i>paint the valley in watercolour</i>, with a before/after line beside it (A23).
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>A material shades every draw of its canvas item</b> (HANDOFF art trap 19), and this map draws the whole village in
/// one <c>_Draw</c>. So the shaded valley is a <b>layer of its own, behind the map</b>: one rect over the map's whole size,
/// whose shader paints the Beyond outside the valley, today's bake left of the line, and the new look right of it. With
/// the tick on, <c>_Draw</c> leaves out the three things the layer now paints (the Beyond, the parchment, the bake) and
/// draws everything else exactly as before — so the paths, fields, washes and everything standing lie over the new ground
/// as they lay over the old, and the grain is on the ground only (A23, call 3).
/// </para>
/// <para>
/// ⭐ <b>With the tick off nothing here runs</b>: the layer does not exist until the tick first goes on, and <c>_Draw</c>
/// takes its old path to the letter. ⭐ <b>Lean (A23):</b> the shader, the grass masks, the four painted tiles and the
/// ground's mix are loaded and baked on that first switching on, never before; per frame the layer is a handful of
/// uniforms (D338: a uniform, never a re-bake).
/// </para>
/// </remarks>
public partial class VillageMap
{
    /// <summary>The valley's shader (A25).</summary>
    internal const string WatercolourShaderPath = "res://shaders/valley_terrain.gdshader";

    /// <summary>How near the before/after line a press must land to take hold of it, in screen pixels.</summary>
    private const float SplitGrabPixels = 10f;

    /// <summary>
    /// The share of a season that holds its own palette before it turns into the next (<c>seasonal-grass.md §6.4</c>, D628:
    /// 20 days of 30, then a 10-day turn) — and so when the snow comes and goes (A23).
    /// </summary>
    private const double SeasonHolds = 2.0 / 3.0;

    private bool _watercolour;
    private bool _beforeAfter;
    private bool _draggingTheSplit;

    /// <summary>Where the before/after line stands, as a share of the map's width. It opens in the middle.</summary>
    private float _splitShare = 0.5f;

    private Control? _watercolourLayer;
    private ShaderMaterial? _watercolourMaterial;

    /// <summary>Whether the valley is drawn in watercolour — the Settings tick's state.</summary>
    public bool WatercolourShown => _watercolour;

    /// <summary>Whether the before/after line is shown — its tick's state. It shows only while the valley is in watercolour.</summary>
    public bool BeforeAfterShown => _beforeAfter;

    /// <summary>The layer's material, for the probe to ask (null until the tick first goes on).</summary>
    internal ShaderMaterial? WatercolourMaterial => _watercolourMaterial;

    /// <summary>The before/after line's place, as a share of the map's width — for the probe.</summary>
    internal float SplitShare => _splitShare;

    /// <summary>The Settings tick <i>paint the valley in watercolour</i> (A23, call 5), off until Joe approves the look.</summary>
    public void ShowWatercolour(bool on)
    {
        _watercolour = on;

        if (on)
        {
            BuildTheWatercolourLayer();
        }

        if (_watercolourLayer is not null)
        {
            _watercolourLayer.Visible = on;
        }

        QueueRedraw();
    }

    /// <summary>The Settings tick <i>before/after slider</i> (A23): the line between today's valley and the watercolour.</summary>
    public void ShowBeforeAfter(bool on)
    {
        _beforeAfter = on;
        _draggingTheSplit = false;
        QueueRedraw();
    }

    /// <summary>
    /// The layer, the material and everything it reads — once, on the first switching on. ⚠️ The game's bake is swapped
    /// for one that keeps the ground's mix (A24); it bakes on the next refresh, a second or so, once.
    /// </summary>
    private void BuildTheWatercolourLayer()
    {
        if (_watercolourLayer is not null)
        {
            return;
        }

        GrassLook look = GrassLook.Approved;
        GrassLabTerrain terrain = GrassLabTerrain.StartingPoint;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(WatercolourShaderPath) };

        // The grass, at the approved look (D632, A10).
        material.SetShaderParameter("grass_masks", GD.Load<Texture2D>($"res://textures/grass/grass_{look.Masks}_masks.png"));
        material.SetShaderParameter("grass_season_atlas", GD.Load<Texture2D>("res://textures/grass/grass_seasons.png"));
        material.SetShaderParameter("grass_jitter", look.Jitter);
        material.SetShaderParameter("grass_strength", look.Strength);
        material.SetShaderParameter("tiles_per_repeat", look.TilesPerRepeat);
        material.SetShaderParameter("macro_on", look.MacroOn);
        material.SetShaderParameter("macro_cycle_tiles", look.MacroCycleTiles);
        material.SetShaderParameter("macro_strength_amount", look.MacroStrengthAmount);
        material.SetShaderParameter("macro_season_amount", look.MacroSeasonAmount);
        material.SetShaderParameter("hex_on", look.HexOn);
        material.SetShaderParameter("hex_sharpness", look.HexSharpness);
        material.SetShaderParameter("height_blend", look.HeightBlend);
        material.SetShaderParameter("grain_amount", look.PaperGrain);

        // The terrain, at Joe's approved values (A7, A9; the coverage fade is the lab's, §5.3).
        material.SetShaderParameter("coverage_fade", terrain.CoverageFade);
        material.SetShaderParameter("splat_sharpness", terrain.SplatSharpness);
        material.SetShaderParameter("bleed_reach_tiles", terrain.BleedReachTiles);
        material.SetShaderParameter("bleed_scale_tiles", terrain.BleedScaleTiles);
        material.SetShaderParameter("grain_scale_tiles", terrain.GrainScaleTiles);
        material.SetShaderParameter("snow_srgb", Srgb(terrain.SnowColour));
        material.SetShaderParameter("snow_blend", terrain.SnowBlend);
        material.SetShaderParameter("snow_patchiness", terrain.SnowPatchiness);
        material.SetShaderParameter("snow_patch_tiles", terrain.SnowPatchTiles);
        material.SetShaderParameter("ground_tex_tiles", terrain.TextureTilesPerRepeat);
        material.SetShaderParameter("ground_tex_antitile", terrain.TextureAntiTiling);

        // The map's own colours, and Joe's four painted tiles (A13–A21).
        material.SetShaderParameter("beyond_srgb", Srgb(Beyond));
        material.SetShaderParameter("ground_srgb", Srgb(Ground));
        material.SetShaderParameter("kind_wood", Srgb(ColourOf(Terrain.Forest)));
        material.SetShaderParameter("kind_young", Srgb(ColourOf(Terrain.Sapling)));
        material.SetShaderParameter("kind_stone", Srgb(ColourOf(Terrain.Rock)));
        material.SetShaderParameter("kind_iron", Srgb(ColourOf(Terrain.IronDeposit)));
        material.SetShaderParameter("kind_water", Srgb(ColourOf(Terrain.Water)));
        material.SetShaderParameter("kind_shallows", Srgb(ShallowsColour));
        material.SetShaderParameter("ground_wood", GD.Load<Texture2D>(GrassLab.WoodsTilePath));
        material.SetShaderParameter("ground_stone", GD.Load<Texture2D>(GrassLab.StoneTilePath));
        material.SetShaderParameter("ground_iron", GD.Load<Texture2D>(GrassLab.IronTilePath));
        material.SetShaderParameter("ground_water", GD.Load<Texture2D>(GrassLab.WaterTilePath));

        _watercolourMaterial = material;
        _valley = new ValleyTexture(withWeights: true);

        var layer = new Control
        {
            Name = "Watercolour",
            ShowBehindParent = true,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = material,
        };
        layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.Draw += () => layer.DrawRect(new Rect2(Vector2.Zero, layer.Size), Colors.White);
        AddChild(layer);
        _watercolourLayer = layer;
    }

    /// <summary>
    /// ⭐ The valley's turn of <c>_Draw</c> with the tick on: the bake brought up to date as ever (it holds the mix now), and
    /// the layer told where the valley is, how near it is, the season, the snow and the line — uniforms only.
    /// </summary>
    private void PaintTheWatercolour(Rect2 valley)
    {
        _valley.Refresh(_world!);
        ShaderMaterial material = _watercolourMaterial!;
        SimConfig config = _world!.Config;

        // After a refresh all three exist: this bake keeps the mix (BuildTheWatercolourLayer).
        material.SetShaderParameter("valley_colour", _valley.Texture!);
        material.SetShaderParameter("ground_map", _valley.WeightsTexture!);
        material.SetShaderParameter("ground_map_shares", _valley.SharesTexture!);
        material.SetShaderParameter("ground_map_tiles", new Vector2(config.MapWidth, config.MapHeight));
        material.SetShaderParameter("valley_origin_px", valley.Position);
        material.SetShaderParameter("valley_size_px", valley.Size);
        material.SetShaderParameter("px_per_tile", _pixelsPerTile);

        (float season, float snow) = TheYearAt(_world.Tick + _alpha, config.TicksPerSeason);
        material.SetShaderParameter("grass_season", season);
        material.SetShaderParameter("snow_amount", snow);

        float split = _beforeAfter && valley.Size.X > 0f ? ((_splitShare * Size.X) - valley.Position.X) / valley.Size.X : -1f;
        material.SetShaderParameter("split_u", split);
    }

    /// <summary>
    /// ⭐ The year as the ground shows it, from the tick: the grass's season (0 spring … 3 winter, 4 spring again), each
    /// holding for <see cref="SeasonHolds"/> of its length and turning into the next over the rest (D628); and the snow,
    /// which lies as autumn turns, is whole through winter's hold, and melts as winter turns to spring (A23 — Joe: white at
    /// mid-winter). A pure function of the tick, so a place and a time always give the same picture; the sim untouched.
    /// </summary>
    internal static (float Season, float Snow) TheYearAt(double tick, int ticksPerSeason)
    {
        double perSeason = Math.Max(1, ticksPerSeason);
        double intoYear = tick % (perSeason * 4.0);
        int season = Math.Min(3, (int)(intoYear / perSeason));
        double within = (intoYear - (season * perSeason)) / perSeason;
        double turning = Math.Clamp((within - SeasonHolds) / (1.0 - SeasonHolds), 0.0, 1.0);

        double snow = season switch
        {
            2 => turning,
            3 => 1.0 - turning,
            _ => 0.0,
        };

        return ((float)(season + turning), (float)snow);
    }

    /// <summary>
    /// The before/after line, over everything: a hairline in the map's ink, a handle at the middle, and the two words —
    /// drawn last in <c>_Draw</c>, as the ghost is, because it is a tool in the hand.
    /// </summary>
    private void DrawTheBeforeAfterLine()
    {
        if (!_watercolour || !_beforeAfter)
        {
            return;
        }

        float x = _splitShare * Size.X;
        var ink = new Color(0.12f, 0.13f, 0.13f, 0.9f);
        var paper = new Color(0.97f, 0.95f, 0.90f, 0.95f);
        DrawLine(new Vector2(x, 0f), new Vector2(x, Size.Y), ink, 2f);

        var handle = new Rect2(x - 14f, (Size.Y / 2f) - 14f, 28f, 28f);
        DrawRect(handle, paper);
        DrawRect(handle, ink, filled: false, width: 1.5f);
        float mid = Size.Y / 2f;
        DrawLine(new Vector2(x - 3f, mid - 5f), new Vector2(x - 8f, mid), ink, 2f);
        DrawLine(new Vector2(x - 8f, mid), new Vector2(x - 3f, mid + 5f), ink, 2f);
        DrawLine(new Vector2(x + 3f, mid - 5f), new Vector2(x + 8f, mid), ink, 2f);
        DrawLine(new Vector2(x + 8f, mid), new Vector2(x + 3f, mid + 5f), ink, 2f);

        Font font = ThemeDB.FallbackFont;
        const int size = 13;
        DrawTheWord("Before", new Vector2(x - 12f - font.GetStringSize("Before", fontSize: size).X, 36f));
        DrawTheWord("After", new Vector2(x + 12f, 36f));

        void DrawTheWord(string word, Vector2 at)
        {
            Vector2 measured = font.GetStringSize(word, fontSize: size);
            DrawRect(new Rect2(at - new Vector2(6f, measured.Y - 2f), measured + new Vector2(12f, 4f)), paper);
            DrawString(font, at, word, fontSize: size, modulate: ink);
        }
    }

    /// <summary>
    /// ⭐ A press on the before/after line takes hold of it, and a drag moves it — <b>before anything else the map does with a
    /// press</b>, so the line moves and nothing under it is clicked. Anywhere else, the press is the map's as ever.
    /// </summary>
    private bool TheBeforeAfterLineTakes(InputEvent @event)
    {
        if (!_watercolour || !_beforeAfter)
        {
            return false;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } press)
        {
            if (press.Pressed && Mathf.Abs(press.Position.X - (_splitShare * Size.X)) <= SplitGrabPixels)
            {
                _draggingTheSplit = true;
                AcceptEvent();
                return true;
            }

            if (!press.Pressed && _draggingTheSplit)
            {
                _draggingTheSplit = false;
                AcceptEvent();
                return true;
            }
        }

        if (_draggingTheSplit && @event is InputEventMouseMotion drag && Size.X > 0f)
        {
            _splitShare = Mathf.Clamp(drag.Position.X / Size.X, 0f, 1f);
            QueueRedraw();
            AcceptEvent();
            return true;
        }

        return false;
    }

    private static Vector3 Srgb(Color colour) => new(colour.R, colour.G, colour.B);

    /// <summary>
    /// Every uniform the map sets on the layer — what the compiled shader must declare (HANDOFF art trap 6: a shader that
    /// did not compile declares none, and every other check stays green over it).
    /// </summary>
    private static readonly string[] WatercolourUniforms =
    {
        "grass_masks", "grass_season_atlas", "grass_jitter", "grass_strength", "tiles_per_repeat", "macro_on",
        "macro_cycle_tiles", "macro_strength_amount", "macro_season_amount", "hex_on", "hex_sharpness", "height_blend",
        "grain_amount", "coverage_fade", "splat_sharpness", "bleed_reach_tiles", "bleed_scale_tiles", "grain_scale_tiles",
        "snow_srgb", "snow_blend", "snow_patchiness", "snow_patch_tiles", "ground_tex_tiles", "ground_tex_antitile",
        "beyond_srgb", "ground_srgb", "kind_wood", "kind_young", "kind_stone", "kind_iron", "kind_water", "kind_shallows",
        "ground_wood", "ground_stone", "ground_iron", "ground_water", "valley_colour", "ground_map", "ground_map_shares",
        "ground_map_tiles", "valley_origin_px", "valley_size_px", "px_per_tile", "grass_season", "snow_amount", "split_u",
    };

    /// <summary>
    /// ⭐ The probe's <c>watercolour:</c> line (A25) — <b>everything about the tick that can be checked headless</b>, which is
    /// not what it looks like (<c>_Draw</c> never runs there, D340; that is the windowed renders and Joe's eyes).
    /// </summary>
    /// <remarks>
    /// It opens off with nothing loaded (lean, A23); switched on, the shader compiles and declares every uniform the map
    /// sets; every look value is the two look files' and every colour the palette's; the valley's turn of <c>_Draw</c>
    /// hands it a bake that keeps the mix, the valley's rect and the zoom; the line takes a press on it and a drag, and
    /// nothing else; the year's grass and snow land where A23 says on five days; and switched off, the map is as it was.
    /// ⚠️ It leaves the layer built but hidden — switching on is a one-time cost the probe has now paid.
    /// </remarks>
    internal string TheWatercolourHolds()
    {
        var faults = new System.Collections.Generic.List<string>();
        if (_world is null)
        {
            return "[widths] watercolour: ⛔ no world to draw";
        }

        bool wasOn = _watercolour;
        bool wasBeforeAfter = _beforeAfter;
        float wasSplit = _splitShare;

        if (wasOn || wasBeforeAfter || (_watercolourLayer is not null && !wasOn))
        {
            faults.Add($"opens with the tick {wasOn}, the slider {wasBeforeAfter}, a layer {_watercolourLayer is not null} (want all off, nothing loaded)");
        }

        ShowWatercolour(true);
        ShaderMaterial? material = _watercolourMaterial;
        if (material is null || _watercolourLayer is not { Visible: true, ShowBehindParent: true } || !_watercolour)
        {
            faults.Add("switched on, there is no visible layer behind the map holding the material");

            // Put it back before saying so: the red-check found this return left the tick on for the lines after (A25).
            ShowWatercolour(wasOn);
            return Said();
        }

        var declared = new System.Collections.Generic.HashSet<string>();
        foreach (Godot.Collections.Dictionary uniform in material.Shader.GetShaderUniformList())
        {
            declared.Add(uniform["name"].AsString());
        }

        foreach (string uniform in WatercolourUniforms)
        {
            if (!declared.Contains(uniform))
            {
                faults.Add($"valley_terrain.gdshader declares no {uniform} ({declared.Count} in all; 0 is a shader that did not compile)");
            }
        }

        GrassLook look = GrassLook.Approved;
        GrassLabTerrain terrain = GrassLabTerrain.StartingPoint;
        Near("grass_strength", look.Strength);
        Near("grass_jitter", look.Jitter);
        Near("tiles_per_repeat", look.TilesPerRepeat);
        Near("macro_cycle_tiles", look.MacroCycleTiles);
        Near("macro_strength_amount", look.MacroStrengthAmount);
        Near("macro_season_amount", look.MacroSeasonAmount);
        Near("hex_sharpness", look.HexSharpness);
        Near("grain_amount", look.PaperGrain);
        Near("coverage_fade", terrain.CoverageFade);
        Near("splat_sharpness", terrain.SplatSharpness);
        Near("bleed_reach_tiles", terrain.BleedReachTiles);
        Near("bleed_scale_tiles", terrain.BleedScaleTiles);
        Near("grain_scale_tiles", terrain.GrainScaleTiles);
        Near("snow_patchiness", terrain.SnowPatchiness);
        Near("snow_patch_tiles", terrain.SnowPatchTiles);
        Near("ground_tex_tiles", terrain.TextureTilesPerRepeat);
        Same("macro_on", material.GetShaderParameter("macro_on").AsBool() == look.MacroOn);
        Same("hex_on", material.GetShaderParameter("hex_on").AsBool() == look.HexOn);
        Same("height_blend", material.GetShaderParameter("height_blend").AsBool() == look.HeightBlend);
        Same("snow_blend", material.GetShaderParameter("snow_blend").AsInt32() == terrain.SnowBlend);
        Same("ground_tex_antitile", material.GetShaderParameter("ground_tex_antitile").AsInt32() == terrain.TextureAntiTiling);
        Same("snow_srgb", material.GetShaderParameter("snow_srgb").AsVector3() == Srgb(terrain.SnowColour));
        Same("ground_srgb", material.GetShaderParameter("ground_srgb").AsVector3() == Srgb(Ground));
        Same("beyond_srgb", material.GetShaderParameter("beyond_srgb").AsVector3() == Srgb(Beyond));
        Same("kind_wood", material.GetShaderParameter("kind_wood").AsVector3() == Srgb(ColourOf(Terrain.Forest)));
        Same("kind_young", material.GetShaderParameter("kind_young").AsVector3() == Srgb(ColourOf(Terrain.Sapling)));
        Same("kind_stone", material.GetShaderParameter("kind_stone").AsVector3() == Srgb(ColourOf(Terrain.Rock)));
        Same("kind_iron", material.GetShaderParameter("kind_iron").AsVector3() == Srgb(ColourOf(Terrain.IronDeposit)));
        Same("kind_water", material.GetShaderParameter("kind_water").AsVector3() == Srgb(ColourOf(Terrain.Water)));
        Same("kind_shallows", material.GetShaderParameter("kind_shallows").AsVector3() == Srgb(ShallowsColour));
        Same("grass_masks", material.GetShaderParameter("grass_masks").As<Texture2D>()?.ResourcePath == $"res://textures/grass/grass_{look.Masks}_masks.png");
        Same("ground_wood", material.GetShaderParameter("ground_wood").As<Texture2D>()?.ResourcePath == GrassLab.WoodsTilePath);
        Same("ground_stone", material.GetShaderParameter("ground_stone").As<Texture2D>()?.ResourcePath == GrassLab.StoneTilePath);
        Same("ground_iron", material.GetShaderParameter("ground_iron").As<Texture2D>()?.ResourcePath == GrassLab.IronTilePath);
        Same("ground_water", material.GetShaderParameter("ground_water").As<Texture2D>()?.ResourcePath == GrassLab.WaterTilePath);

        // The valley's turn of `_Draw`, asked directly: `_Draw` never runs headless (D340).
        SimConfig config = _world.Config;
        Vector2 topLeft = ToScreen(new Vector2(config.MapMinX - 0.5f, config.MapMinY - 0.5f));
        Vector2 bottomRight = ToScreen(new Vector2(config.MapMaxX + 0.5f, config.MapMaxY + 0.5f));
        var valley = new Rect2(topLeft, bottomRight - topLeft);
        PaintTheWatercolour(valley);

        Texture2D? mix = material.GetShaderParameter("ground_map").As<Texture2D>();
        Texture2D? colour = material.GetShaderParameter("valley_colour").As<Texture2D>();
        if (mix is null || colour is null || material.GetShaderParameter("ground_map_shares").As<Texture2D>() is null
            || mix.GetWidth() != config.MapWidth * ValleyTexture.WeightsPerTile
            || colour.GetWidth() != config.MapWidth * ValleyTexture.PixelsPerTile)
        {
            faults.Add($"the valley's turn hands the layer a mix of {mix?.GetWidth()} and a colour of {colour?.GetWidth()} px wide (want {config.MapWidth * ValleyTexture.WeightsPerTile} and {config.MapWidth * ValleyTexture.PixelsPerTile}, both, and the shares)");
        }

        Same("valley_origin_px", material.GetShaderParameter("valley_origin_px").AsVector2() == valley.Position);
        Same("valley_size_px", material.GetShaderParameter("valley_size_px").AsVector2() == valley.Size);
        Same("ground_map_tiles", material.GetShaderParameter("ground_map_tiles").AsVector2() == new Vector2(config.MapWidth, config.MapHeight));
        Near("px_per_tile", _pixelsPerTile);
        Near("split_u", -1f);

        // The line: shown, it stands mid-map; a press on it and a drag move it; a press away from it is the map's.
        ShowBeforeAfter(true);
        PaintTheWatercolour(valley);
        float expected = ((0.5f * Size.X) - valley.Position.X) / valley.Size.X;
        Near("split_u", expected);
        float x = _splitShare * Size.X;
        bool away = TheBeforeAfterLineTakes(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(x + 40f, Size.Y / 2f) });
        bool took = TheBeforeAfterLineTakes(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(x + 4f, Size.Y / 2f) });
        bool dragged = TheBeforeAfterLineTakes(new InputEventMouseMotion { Position = new Vector2(Size.X * 0.25f, Size.Y / 2f) });
        bool let = TheBeforeAfterLineTakes(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(Size.X * 0.25f, Size.Y / 2f) });
        bool after = TheBeforeAfterLineTakes(new InputEventMouseMotion { Position = new Vector2(Size.X * 0.75f, Size.Y / 2f) });
        if (away || !took || !dragged || !let || after || Mathf.Abs(_splitShare - 0.25f) > 1e-4f)
        {
            faults.Add($"the line: a press 40 px off taken {away}, on it {took}, the drag {dragged}, the release {let}, a move after {after}; it stands at {_splitShare:F3} (want false, true, true, true, false, 0.250)");
        }

        // The year (A23): five moments, as seasons into the year, each with what the grass and the snow should be there.
        // A season holds for its first two thirds and turns over the last third.
        Year(1.5, 1f, 0f, "mid-summer: summer, bare");
        Year(2.0 + (5.0 / 6.0), 2.5f, 0.5f, "five-sixths into autumn: half turned to winter, half snow");
        Year(3.25, 3f, 1f, "a quarter into winter: white");
        Year(3.9, 3.7f, 0.3f, "nine-tenths into winter: 70 % turned to spring, the snow 30 %");
        Year(0.1, 0f, 0f, "early spring: bare");

        // Off, the map is as it was.
        ShowBeforeAfter(wasBeforeAfter);
        ShowWatercolour(wasOn);
        _splitShare = wasSplit;
        if (_watercolour || _watercolourLayer is { Visible: true })
        {
            faults.Add("switched off, the layer still shows");
        }

        return Said();

        string Said() => faults.Count == 0
            ? $"[widths] watercolour: ✅ opens off with nothing loaded; on, valley_terrain.gdshader declares all {WatercolourUniforms.Length} uniforms the map sets, every value the look files' and the palette's, the mix and the colour handed over, the line taken and dragged and nothing else, the year's snow on five days; off, as it was"
            : $"[widths] watercolour: ⛔ {string.Join("; ", faults)}";

        void Near(string uniform, float want)
        {
            float got = material!.GetShaderParameter(uniform).AsSingle();
            if (Mathf.Abs(got - want) > 1e-5f)
            {
                faults.Add($"{uniform} is {got} (want {want})");
            }
        }

        void Same(string what, bool holds)
        {
            if (!holds)
            {
                faults.Add($"{what} is not the look files' or the palette's");
            }
        }

        void Year(double seasons, float season, float snow, string when)
        {
            int perSeason = config.TicksPerSeason;
            (float s, float w) = TheYearAt(seasons * perSeason, perSeason);
            if (Mathf.Abs(s - season) > 1e-4f || Mathf.Abs(w - snow) > 1e-4f)
            {
                faults.Add($"{when}: grass season {s:F3}, snow {w:F3} (want {season:F3}, {snow:F3})");
            }
        }
    }
}
