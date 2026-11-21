using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ The title screen — what the game opens on (D516, `specs/title-and-pause.md §3`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Its rules are Main's and the sim's; the screen only lays them out</b> — the new-game screen's shape. It
/// raises an event for every button and Main acts on it, so a village still comes in from disk by one door
/// (<c>Main.OpenTheVillage</c>).
/// </para>
/// <para>
/// ⭐ <b>Behind it, the valley of the newest save that opens</b> (Joe, D516): opened and baked whole by the game's
/// own <see cref="ValleyTexture"/>, clearings, fields and paths and all — then the opened village is dropped. ⛔
/// <i>Continue</i> does not reuse it: a second door in from disk is how the two would come to differ. With no
/// save, a valley from a rolled seed — and under the probe there are never saves.
/// </para>
/// </remarks>
public sealed partial class TitleScreen : Control
{
    /// <summary>The name on the title (Joe, D516: <i>"bclone" for now</i>) — one string, changed once.</summary>
    internal const string GameName = "Kith";

    /// <summary>The backdrop's bake, a little finer than the new-game preview's: it fills the window.</summary>
    private const int BackdropPixelsPerTile = 10;

    private readonly SimConfig _config;
    private readonly string _build;
    private readonly ValleyTexture _bake = new(BackdropPixelsPerTile);

    private TextureRect _backdrop = null!;
    private Label _refusal = null!;

    /// <summary>For Godot, which may construct a script's node itself; the game uses the other.</summary>
    public TitleScreen()
        : this(new SimConfig(), Array.Empty<SaveListing>(), "?")
    {
    }

    /// <summary>The title over <paramref name="saves"/> (newest first, as the load list shows them).</summary>
    internal TitleScreen(SimConfig config, IReadOnlyList<SaveListing> saves, string build)
    {
        _config = config;
        Saves = saves;
        _build = build;
    }

    /// <summary>Asked to open the save at this path — <i>Continue</i> or <i>Load…</i>.</summary>
    public event Action<string>? LoadAsked;

    /// <summary><i>New village</i>: the new-game screen.</summary>
    public event Action? NewVillageAsked;

    /// <summary><i>Quit</i>: no village is open, so nothing is saved.</summary>
    public event Action? QuitAsked;

    /// <summary>A save the player can open, as the list shows it — or one it can only show, greyed, with why.</summary>
    internal sealed record SaveListing(string Path, string Line, string? Refusal);

    /// <summary>Every save on disk, newest first.</summary>
    internal IReadOnlyList<SaveListing> Saves { get; }

    /// <summary>For the probe: the band of words and buttons, whose right edge must be inside the window.</summary>
    internal Control Band { get; private set; } = null!;

    /// <summary>For the probe and the tooltip: <i>Continue</i>.</summary>
    internal Button ContinueButton { get; private set; } = null!;

    /// <summary>For the probe: <i>New village</i>.</summary>
    internal Button NewVillageButton { get; private set; } = null!;

    /// <summary>For the probe: the backdrop has a valley on it.</summary>
    internal bool HasABackdrop => _bake.Texture is not null && _backdrop.Texture is not null;

    /// <summary>For the probe: how long the backdrop took to open and bake, in milliseconds.</summary>
    internal double BackdropMs { get; private set; }

    /// <summary>For the probe: the backdrop is a save's valley rather than a rolled one.</summary>
    internal bool BackdropIsASave { get; private set; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var floor = new Panel();
        floor.AddThemeStyleboxOverride("panel", UiLook.Backdrop());
        floor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(floor);

        _backdrop = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_backdrop);

        AddChild(BuildTheBand());
        BakeTheBackdrop();
    }

    /// <summary>A save that would not open, said where the player is looking. The title stays.</summary>
    internal void RefuseTheLoad(string sentence)
    {
        _refusal.Text = sentence;
        _refusal.Visible = true;
    }

    /// <summary>For the probe: what pressing <i>New village</i> does.</summary>
    internal void PressNewVillage() => NewVillageAsked?.Invoke();

    /// <summary>
    /// <b>Load…</b>: every save, newest first, a refused one greyed with its refusal as the tooltip — the title's
    /// and the pause screen's (`title-and-pause.md §3, §5`). A popup, never a list: a list widens its column to
    /// its longest line.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>It owns its list</b> (D518): the pause screen's stayed as it was opened, so a save made while it was up
    /// never appeared in it — Joe saved <i>test</i> three times and saw no <i>test</i>. ⭐ <b>And the pause screen's
    /// reads it only as its popup opens</b> (D805, §5.2): built when the pause screen opened, it read every save on disk
    /// and froze Joe's game for 830 ms. Read at the popup, it is today's list every time, a fresh save included.
    /// </remarks>
    internal sealed class LoadMenu
    {
        /// <summary>The one row a list read at the popup holds when there is no save — the title's <i>Continue</i> tooltip.</summary>
        internal const string NoSaveYet = "No saved village yet.";

        private readonly List<SaveListing> _listed = new();

        /// <summary>The title's: the list it was given, read up front — it needs the newest save for <i>Continue</i>.</summary>
        internal LoadMenu(IReadOnlyList<SaveListing> saves, Action<string> picked)
            : this(picked)
        {
            Fill(saves);
            Button.Disabled = _listed.Count == 0;
        }

        /// <summary>The pause screen's: nothing read until the popup is about to open, then <paramref name="read"/> each time.</summary>
        internal LoadMenu(Func<IReadOnlyList<SaveListing>> read, Action<string> picked)
            : this(picked)
        {
            Button.AboutToPopup += () => Fill(read());
        }

        private LoadMenu(Action<string> picked)
        {
            Button = new MenuButton { Text = "Load…", Flat = false };
            Button.GetPopup().IdPressed += id =>
            {
                if (id < _listed.Count)
                {
                    picked(_listed[(int)id].Path);
                }
            };
        }

        internal MenuButton Button { get; }

        /// <summary>Every row again, from <paramref name="saves"/>; with none, one greyed row that says so.</summary>
        private void Fill(IReadOnlyList<SaveListing> saves)
        {
            _listed.Clear();
            _listed.AddRange(saves);
            PopupMenu menu = Button.GetPopup();
            menu.Clear();
            for (int i = 0; i < _listed.Count; i++)
            {
                menu.AddItem(_listed[i].Line, i);
                menu.SetItemDisabled(i, _listed[i].Refusal is not null);
                menu.SetItemTooltip(i, _listed[i].Refusal ?? _listed[i].Path);
            }

            if (_listed.Count == 0)
            {
                menu.AddItem(NoSaveYet, 0);
                menu.SetItemDisabled(0, true);
            }
        }
    }

    private Control BuildTheBand()
    {
        // ⭐ A dimmed band down the left, so the words read over any valley.
        var band = new PanelContainer { CustomMinimumSize = new Vector2(340, 0) };
        band.AddThemeStyleboxOverride("panel", UiLook.TitleBand());
        band.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        band.OffsetRight = 340;
        Band = band;

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 10);
        band.AddChild(column);

        var title = new Label { Text = GameName };
        title.AddThemeFontSizeOverride("font_size", 56);
        column.AddChild(title);

        var build = new Label { Text = _build };
        UiLook.Tint(build, UiLook.Faint);
        build.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(build);
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 28) });

        SaveListing? newest = Saves.FirstOrDefault(s => s.Refusal is null);
        ContinueButton = Big(new Button
        {
            Text = "Continue",
            Disabled = newest is null,
            TooltipText = newest?.Line ?? "No saved village yet.",
        });
        ContinueButton.Pressed += () => LoadAsked?.Invoke(newest!.Path);
        column.AddChild(ContinueButton);

        NewVillageButton = Big(new Button { Text = "New village" });
        NewVillageButton.Pressed += () => NewVillageAsked?.Invoke();
        column.AddChild(NewVillageButton);

        column.AddChild(Big(new LoadMenu(Saves, path => LoadAsked?.Invoke(path)).Button));

        Button quit = Big(new Button { Text = "Quit" });
        quit.Pressed += () => QuitAsked?.Invoke();
        column.AddChild(quit);

        _refusal = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false,
            CustomMinimumSize = new Vector2(240, 0),
        };
        UiLook.Tint(_refusal, UiLook.Refused);
        _refusal.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_refusal);
        return band;
    }

    private static T Big<T>(T button)
        where T : Button
    {
        button.CustomMinimumSize = new Vector2(240, 40);
        button.AddThemeFontSizeOverride("font_size", 18);
        return button;
    }

    /// <summary>The newest save's valley, or a rolled one — never a half-made picture (§3).</summary>
    private void BakeTheBackdrop()
    {
        long before = System.Diagnostics.Stopwatch.GetTimestamp();
        SimWorld world = TheNewestValley() ?? ARolledValley();
        _bake.BakeAfresh(world);
        _backdrop.Texture = _bake.Texture;
        BackdropMs = (System.Diagnostics.Stopwatch.GetTimestamp() - before) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    private SimWorld? TheNewestValley()
    {
        SaveListing? newest = Saves.FirstOrDefault(s => s.Refusal is null);
        if (newest is null)
        {
            return null;
        }

        SaveOpened opened = SaveFile.Open(newest.Path, _config);
        if (opened.Loop is null)
        {
            // ⚠️ Not swallowed: the reason is said where a developer looks, and the title falls back to a rolled
            // valley. Continue will say it to the player in words if it is pressed.
            GD.Print($"[Kith] the title's backdrop could not open {newest.Path}: {opened.Refusal} ({opened.Detail})");
            return null;
        }

        BackdropIsASave = true;
        return opened.Loop.World;
    }

    private SimWorld ARolledValley()
    {
        NewGameSettings settings = NewGame.Defaults(_config, NewGameScreen.RollASeed(_config, new System.Random()));
        return SimWorld.Create(NewGame.Apply(_config, settings));
    }
}
