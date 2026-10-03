using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// The inspector CARDS — <c>specs/the-cards.md</c> (D376, Joe: *"presently our inspector
/// windows/details windows are such a mess of stacked sentences i dont even know what to read"*).
/// </summary>
/// <remarks>
/// <para>
/// <b>One building or person, five parts, fixed order, and every extra line is a bug:</b> a title
/// (editable — the player names their buildings); one status line, green light for <i>working</i>
/// and amber for the one reason it is not; a workers row with − and +; two or three numbers; a
/// picture — the building's own map drawing, scaled up. A home's card lists its people and
/// scrolls past four. Several cards can be open at once; the ⌖ pin keeps one when the player
/// clicks elsewhere, an unpinned card is replaced by the next click, and any card drags by its
/// head. The docked *Settings* panel carries the per-building controls for the SELECTED card
/// (yellow edge) — the card is what you read, the panel is what you set.
/// </para>
/// <para>
/// Cards are built from what the inspector already knew how to say — <see cref="SimWorld.IdleNote"/>,
/// <see cref="Villager.DescribeState"/>, the same staffing call — and hold no state of their own
/// beyond which subject they show and where the player left them.
/// </para>
/// </remarks>
public partial class Main
{
    private enum CardKind
    {
        Store,
        Workplace,
        Household,
        Villager,

        /// <summary>A library (D396) — its Id is its index in <see cref="SimWorld.Libraries"/>; a library has no id of its own.</summary>
        Library,

        /// <summary>The town hall (D396) — a singleton, Id 0.</summary>
        TownHall,

        /// <summary>A well (D427) — its Id is its index in <see cref="SimWorld.Wells"/>, as a library's is.</summary>
        Well,
    }

    private readonly record struct CardSubject(CardKind Kind, int Id);

    /// <summary>One card: its panel and the labels the refresh writes into.</summary>
    private sealed class Card
    {
        public required PanelContainer Panel { get; init; }
        public required CardSubject Subject { get; set; }

        /// <summary>A person's line under the name — trade · age · household (D431). Hidden on buildings.</summary>
        public required Label Subtitle { get; init; }

        /// <summary>A person's sections — work, tool, needs, skills, the jumps (D431). Hidden on buildings.</summary>
        public required VillagerParts Person { get; init; }
        public bool Pinned { get; set; }
        public required Label Title { get; init; }
        public required LineEdit Rename { get; init; }
        public required Button Edit { get; init; }
        public required Button Pin { get; init; }
        public required ColorRect Light { get; init; }
        public required Label Status { get; init; }
        public required HBoxContainer WorkersRow { get; init; }
        public required Label Workers { get; init; }
        public required Label WorkersLabel { get; init; }
        public required Label[] Values { get; init; }
        public required Label[] Keys { get; init; }
        public required HBoxContainer Numbers { get; init; }

        /// <summary>
        /// A store's storage list (D393): one row per good the store can hold — chip, name,
        /// amount, take/refuse — in place of the three numbers. Hidden on every other kind.
        /// </summary>
        public required VBoxContainer Storage { get; init; }
        public required ScrollContainer PeopleScroll { get; init; }
        public required VBoxContainer People { get; init; }
        public required BuildingPortrait Portrait { get; init; }
        public required Label Caption { get; init; }

        /// <summary>The hall's <i>View records</i> (D397) — hidden on every other kind, wired to a sentence until slice 2 lands.</summary>
        public required Button Records { get; init; }
        public required Button SettingsToggle { get; init; }
        public required VBoxContainer Settings { get; init; }
        public CardKind? SettingsBuiltFor { get; set; }
        public CardControls Controls { get; set; } = new();
    }

    /// <summary>The controls a card's Settings fold holds — which ones exist depends on the kind.</summary>
    private sealed class CardControls
    {
        public VBoxContainer? FullRow { get; set; }
        public Button? FullMarker { get; set; }
        public List<(Goods Goods, HBoxContainer Row, Label Name, Label Amount, Button Take)> Storage { get; } = new();

        /// <summary>The one stocking control (D389): Open / Closed / Emptying.</summary>
        public VBoxContainer? StockingRow { get; set; }

        public List<(Stocking State, Button Button)> StockingButtons { get; } = new();
        public VBoxContainer? LimitRow { get; set; }
        public List<(Goods Goods, Control Cell, SpinBox Amount, Button Clear)> Limits { get; } = new();
        public VBoxContainer? IdleRow { get; set; }
        public Label? IdleLabel { get; set; }
        public Button? IdleMarker { get; set; }
        public VBoxContainer? GroundRow { get; set; }
        public Label? GroundLabel { get; set; }
        public Label? GroundNote { get; set; }
        public Button? Mode { get; set; }
        public VBoxContainer? ForgeRow { get; set; }
        public Label? ForgeLabel { get; set; }
        public OptionButton? Forge { get; set; }

        /// <summary>The dropdown's items, in order — every tool a forge makes (D446).</summary>
        public List<Goods> ForgeGoods { get; } = new();
        public VBoxContainer? QueueRow { get; set; }
        public Label? QueueLabel { get; set; }
    }

    /// <summary>
    /// A person's card, in sections (D431, Joe with Foundation's villager panel: <i>"a UX
    /// nightmare. copy off screen, no real hierarchy of information. nothing about tools"</i>).
    /// </summary>
    /// <remarks>
    /// Built once with the card and shown only while the card is about a villager. Every sentence
    /// wraps — nothing on a person's card ends in an ellipsis, which is the complaint this answers.
    /// </remarks>
    private sealed class VillagerParts
    {
        public required VBoxContainer Box { get; init; }
        public required OptionButton Keep { get; init; }
        public required Label Job { get; init; }
        public required VBoxContainer ToolSection { get; init; }
        public required ProgressBar ToolBar { get; init; }
        public required Label ToolUses { get; init; }
        public required (PanelContainer Pill, Label Text)[] Needs { get; init; }
        public required VBoxContainer SkillsSection { get; init; }
        public required List<(VBoxContainer Row, Label Name, Label Years, ProgressBar Bar)> Skills { get; init; }
        public required Button Find { get; init; }
        public required Button Home { get; init; }
        public required Button Work { get; init; }

        /// <summary>The dropdown's trades, by item index — item 0 is <i>the village decides</i>.</summary>
        public List<JobKind> KeepTrades { get; } = new();
    }

    private readonly List<Card> _cards = new();
    private Card? _selectedCard;

    /// <summary>A card's width in logical pixels — fixed, like every panel since D367.</summary>
    private const float CardWidth = 268f;

    /// <summary>The room between a person's card's border and what it says (D432) — twice a panel's.</summary>
    private const int PersonCardMargin = 12;

    /// <summary>How many people a home's list shows before it scrolls (Joe: *"a vertical scrollbar"*).</summary>
    private const int PeopleRowsShown = 4;

    private static readonly Color LightWorking = new("#7fbf7a");
    private static readonly Color LightStopped = new("#ffc759");
    private static readonly Color CardSelectedEdge = new("#ffd76a");

    // ---------------------------------------------------------------
    //  Opening, pinning, closing
    // ---------------------------------------------------------------

    /// <summary>The player clicked a thing: show its card, in the unpinned slot.</summary>
    private void OpenCard(CardSubject subject)
    {
        Card? existing = _cards.Find(c => c.Subject == subject);
        if (existing is not null)
        {
            SelectCard(existing);
            return;
        }

        Card? free = _cards.Find(c => !c.Pinned);
        if (free is null)
        {
            free = BuildCard(subject);
            _cards.Add(free);
            PlaceNewCard(free);
        }
        else
        {
            free.Subject = subject;
            free.Rename.Visible = false;
            free.Title.Visible = true;
        }

        SelectCard(free);
        RefreshCards(_loop.World);
    }

    private void PlaceNewCard(Card card)
    {
        // Beside the left column and under the top bars (D378), stepping down a little for
        // each card already open, so a second card never lands exactly on the first.
        float x = Edge + DefaultPanelWidth + 16f;
        float y = TopOfTheLeftColumn() + 26f + (24f * (_cards.Count - 1));
        card.Panel.Position = new Vector2(x, y);
    }

    private void SelectCard(Card card)
    {
        _selectedCard = card;
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Panel.AddThemeStyleboxOverride("panel", CardSkin(ReferenceEquals(_cards[i], card), _cards[i].Subject.Kind == CardKind.Villager));
        }

        card.Panel.MoveToFront();
        SelectSubject(card.Subject);
    }

    /// <summary>The docked Settings rows follow the selected card: point the old selection at its subject.</summary>
    private void SelectSubject(CardSubject subject)
    {
        SimWorld world = _loop.World;
        switch (subject.Kind)
        {
            case CardKind.Villager:
                _selectedVillagerId = subject.Id;
                _selectedTile = null;
                break;
            case CardKind.Store:
                _selectedTile = world.StoreBuildings.Find(s => s.Id == subject.Id)?.Tile;
                _selectedVillagerId = 0;
                break;
            case CardKind.Workplace:
                _selectedTile = world.FindWorkplace(subject.Id)?.Tile;
                _selectedVillagerId = 0;
                break;
            case CardKind.Household:
                _selectedTile = world.FindHousehold(subject.Id)?.HomeTile;
                _selectedVillagerId = 0;
                break;
            case CardKind.Library:
                _selectedTile = LibraryOf(card: subject)?.Tile;
                _selectedVillagerId = 0;
                break;
            case CardKind.TownHall:
                _selectedTile = world.TownHall?.Tile;
                _selectedVillagerId = 0;
                break;
            case CardKind.Well:
                _selectedTile = WellOf(subject)?.Tile;
                _selectedVillagerId = 0;
                break;
        }
    }

    /// <summary>The well a card is about — by index, the library's rule (D396) and its caveat.</summary>
    private Well? WellOf(CardSubject card) =>
        card.Id >= 0 && card.Id < _loop.World.Wells.Count ? _loop.World.Wells[card.Id] : null;

    /// <summary>The library a card is about — by index, which is all a library has (D396).</summary>
    /// <remarks>
    /// ⚠️ An index is not an id: demolish the first of two libraries and this card is about the
    /// second. The card refreshes every frame, so it says so at once, and a library that is not
    /// there closes the card like a demolished store. Cheaper than giving the sim an id nothing
    /// else asks for.
    /// </remarks>
    private Library? LibraryOf(CardSubject card) =>
        card.Id >= 0 && card.Id < _loop.World.Libraries.Count ? _loop.World.Libraries[card.Id] : null;

    private void CloseCard(Card card)
    {
        _cards.Remove(card);
        card.Panel.QueueFree();
        if (ReferenceEquals(_selectedCard, card))
        {
            _selectedCard = null;
        }
    }

    /// <summary>
    /// The card's frame, selected or not — and a person's with more room (D432, Joe: <i>"add
    /// breathing room between elements and borders"</i>). ⚠️ A person's only: a building card's
    /// three number cells are a third of what is inside the margin, and at twelve the lodge's
    /// <i>"meat · leather"</i> (D423) no longer fit and read <i>"meat · others"</i>.
    /// </summary>
    private static StyleBoxFlat CardSkin(bool selected, bool roomy = false) =>
        CardSkins[(selected ? 1 : 0) + (roomy ? 2 : 0)];

    /// <summary>Made once — a card is re-skinned on every refresh, and a new box a frame is garbage for nothing.</summary>
    private static readonly StyleBoxFlat[] CardSkins =
    {
        MakeCardSkin(false, false), MakeCardSkin(true, false), MakeCardSkin(false, true), MakeCardSkin(true, true),
    };

    private static StyleBoxFlat MakeCardSkin(bool selected, bool roomy)
    {
        StyleBoxFlat skin = PanelSkin(0.96f);
        skin.BorderWidthTop = skin.BorderWidthBottom = skin.BorderWidthLeft = skin.BorderWidthRight = 1;
        skin.BorderColor = selected ? CardSelectedEdge : new Color("#3a3f47");
        if (roomy)
        {
            skin.SetContentMarginAll(PersonCardMargin);
        }

        return skin;
    }

    // ---------------------------------------------------------------
    //  Building one
    // ---------------------------------------------------------------

    private Card BuildCard(CardSubject subject)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        panel.CustomMinimumSize = new Vector2(CardWidth, 0f);
        panel.Size = new Vector2(CardWidth, 0f);
        panel.AddThemeStyleboxOverride("panel", CardSkin(false));

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        panel.AddChild(column);

        // ---- head: grip · title / rename · ✎ · ⌖ · ✕ ----
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        head.AddThemeConstantOverride("separation", 6);
        column.AddChild(head);

        // The same cursor the panels' grip shows (`Dress`), so the handle says what it does
        // before it is tried (D379, Joe: *"it does for the static panels"*).
        Label grip = Muted("⋮⋮");
        grip.MouseFilter = MouseFilterEnum.Stop;
        grip.MouseDefaultCursorShape = CursorShape.Move;
        head.AddChild(grip);

        Label title = Body(string.Empty);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.ClipText = true;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        title.MouseFilter = MouseFilterEnum.Stop;
        head.AddChild(title);

        var rename = new LineEdit
        {
            Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxLength = SimWorld.NameLengthLimit,
            PlaceholderText = "a name — blank for the old one",
        };
        head.AddChild(rename);

        var edit = new Button { Text = "✎", Flat = true, TooltipText = "Rename" };
        head.AddChild(edit);

        var pin = new Button { Text = "⌖", ToggleMode = true, TooltipText = "Pin: keep this card when you click elsewhere" };
        head.AddChild(pin);

        var close = new Button { Text = "✕", Flat = true, TooltipText = "Close" };
        head.AddChild(close);

        // ---- a person's subtitle (D431) ----
        Label subtitle = Wrapped(Muted(string.Empty));
        subtitle.Visible = false;
        column.AddChild(subtitle);

        // ---- status ----
        var statusRow = new HBoxContainer();
        statusRow.AddThemeConstantOverride("separation", 8);
        column.AddChild(statusRow);
        var light = new ColorRect { CustomMinimumSize = new Vector2(8, 8), SizeFlagsVertical = SizeFlags.ShrinkBegin };
        var lightBox = new MarginContainer();
        lightBox.AddThemeConstantOverride("margin_top", 5);
        lightBox.AddChild(light);
        statusRow.AddChild(lightBox);
        Label status = Wrapped(Body(string.Empty));
        status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        status.MouseFilter = MouseFilterEnum.Stop;
        statusRow.AddChild(status);

        // ---- a person's sections (D431) ----
        VillagerParts person = BuildPersonParts();
        column.AddChild(person.Box);

        // ---- workers ----
        var workersRow = new HBoxContainer();
        workersRow.AddThemeConstantOverride("separation", 6);
        column.AddChild(workersRow);
        Label workersLabel = Muted("Workers");
        workersLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        workersRow.AddChild(workersLabel);
        var fewer = new Button { Text = "−" };
        var count = Body("0");
        count.HorizontalAlignment = HorizontalAlignment.Center;
        count.CustomMinimumSize = new Vector2(22, 0);
        var more = new Button { Text = "+" };
        workersRow.AddChild(fewer);
        workersRow.AddChild(count);
        workersRow.AddChild(more);

        // ---- three numbers ----
        var numbers = new HBoxContainer();
        numbers.AddThemeConstantOverride("separation", 6);
        column.AddChild(numbers);
        var values = new Label[3];
        var keys = new Label[3];
        for (int i = 0; i < 3; i++)
        {
            var cell = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cell.AddThemeConstantOverride("separation", 0);
            values[i] = Body(string.Empty);
            values[i].ClipText = true;
            values[i].TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            keys[i] = Muted(string.Empty);
            keys[i].ClipText = true;
            keys[i].TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            cell.AddChild(values[i]);
            cell.AddChild(keys[i]);
            numbers.AddChild(cell);
        }

        // ---- the storage list (stores, D393) ----
        // Foundation's warehouse card, by Joe's call: a line per good the store can hold, with
        // the amount and that good's own take/refuse on the line. Rows are built per kind with
        // the settings (`BuildSettings`), because a retargeted card changes kind.
        var storage = new VBoxContainer { Visible = false };
        storage.AddThemeConstantOverride("separation", 2);
        column.AddChild(storage);

        // ---- the people (homes) ----
        var peopleScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(0, RowSize * 1.45f * PeopleRowsShown),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var people = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        people.AddThemeConstantOverride("separation", 2);
        peopleScroll.AddChild(people);
        column.AddChild(peopleScroll);

        // ---- the picture ----
        var portrait = new BuildingPortrait { CustomMinimumSize = new Vector2(0, 64) };
        column.AddChild(portrait);
        Label caption = Muted(string.Empty);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.ClipText = true;
        caption.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(caption);

        // ---- View records (the hall, D397) ----
        // One click away from the card, and from the control bar beside it (`_recordsButton`),
        // because the window it will open — collections, charts, the knowledge roster
        // (`town-hall.md` slices 2–4) — is where a lot of what the player wants to know will be.
        // ⛔ Not wired yet, and it says so rather than doing nothing (D103: a button that does
        // nothing silently is a feature the player cannot reach).
        var records = new Button { Text = "View records", Visible = false };
        column.AddChild(records);

        // ---- the Settings fold (D377, Joe: "why 2 panels for one structure?") ----
        // ⭐ OPEN by default since D396 (Joe's QA pass: *"card Settings open by default"*) — it was
        // folded so the five parts stayed what you read, and in play the fold was one more click
        // on every card. It is every control the docked panel used to hold for this thing, and it
        // acts on THIS card's subject.
        var settingsToggle = new Button { Text = "Settings ▾", Flat = true, ToggleMode = true, ButtonPressed = true };
        settingsToggle.Alignment = HorizontalAlignment.Left;
        column.AddChild(settingsToggle);
        var settings = new VBoxContainer { Visible = true };
        settings.AddThemeConstantOverride("separation", 6);
        column.AddChild(settings);

        var card = new Card
        {
            Panel = panel,
            Subject = subject,
            Subtitle = subtitle,
            Person = person,
            Title = title,
            Rename = rename,
            Edit = edit,
            Pin = pin,
            Light = light,
            Status = status,
            WorkersRow = workersRow,
            Workers = count,
            WorkersLabel = workersLabel,
            Values = values,
            Keys = keys,
            Numbers = numbers,
            Storage = storage,
            PeopleScroll = peopleScroll,
            People = people,
            Portrait = portrait,
            Caption = caption,
            Records = records,
            SettingsToggle = settingsToggle,
            Settings = settings,
        };

        // ---- wiring ----
        MakeDraggable(grip, panel);
        MakeDraggable(title, panel);
        grip.GuiInput += _ => SelectCard(card);
        pin.Toggled += on =>
        {
            card.Pinned = on;
            pin.TooltipText = on ? "Pinned — stays when you click elsewhere" : "Pin: keep this card when you click elsewhere";
        };
        close.Pressed += () => CloseCard(card);
        records.Pressed += OpenTheRecords;
        edit.Pressed += () =>
        {
            rename.Text = card.Title.Text;
            title.Visible = false;
            rename.Visible = true;
            rename.GrabFocus();
            rename.SelectAll();
        };
        rename.TextSubmitted += _ => CommitRename(card);
        rename.FocusExited += () => CommitRename(card);
        person.Keep.ItemSelected += index => KeepFromTheCard(card, (int)index);
        person.Find.Pressed += () => JumpFromThePerson(card, PersonJump.Find);
        person.Home.Pressed += () => JumpFromThePerson(card, PersonJump.Home);
        person.Work.Pressed += () => JumpFromThePerson(card, PersonJump.Work);
        fewer.Pressed += () => ChangeStaffingOf(card, -1);
        more.Pressed += () => ChangeStaffingOf(card, +1);
        settingsToggle.Toggled += open =>
        {
            settings.Visible = open;
            settingsToggle.Text = open ? "Settings ▾" : "Settings ▸";
            RefreshCards(_loop.World);
        };

        AddChild(panel);
        return card;
    }

    private void CommitRename(Card card)
    {
        if (!card.Rename.Visible)
        {
            return;
        }

        card.Rename.Visible = false;
        card.Title.Visible = true;

        SimWorld world = _loop.World;
        string typed = card.Rename.Text;
        PlacementVerdict verdict = card.Subject.Kind switch
        {
            CardKind.Store when StoreOf(card) is StoreBuilding store => world.Rename(store, typed),
            CardKind.Workplace when world.FindWorkplace(card.Subject.Id) is Workplace place => world.Rename(place, typed),
            _ => PlacementVerdict.No("Only a building can be renamed."),
        };

        if (!verdict.Allowed)
        {
            Warn(PlacementVerdict.Yes(verdict.Reason));
        }

        RefreshCards(world);
        RefreshInspector(world);
    }

    /// <summary>Nudge a workplace's staffing from its card's − and +.</summary>
    /// <remarks>
    /// Buttons rather than a spinner, because the numbers are small and a click is
    /// cheaper to reach for than a text field.
    /// <para>
    /// <b>⛔ There is no way back to "untouched" and that is deliberate</b> (Joe, 2026-08-16).
    /// The row briefly carried a "Village decides" button and then a "Clear" one; both are
    /// gone, because the whole idea is. An untouched building is staffed by everyone who fits
    /// — a fact about the building — and the moment the player states a number, that number is
    /// the answer from then on.
    /// </para>
    /// </remarks>
    private void ChangeStaffingOf(Card card, int delta)
    {
        if (card.Subject.Kind != CardKind.Workplace
            || _loop.World.FindWorkplace(card.Subject.Id) is not { IsSite: false } workplace)
        {
            return;
        }

        int wanted = Mathf.Max(0, (workplace.StaffingOverride ?? workplace.Places) + delta);
        _loop.World.SetStaffing(workplace, wanted);
        RefreshCards(_loop.World);
        RefreshInspector(_loop.World);
    }

    private StoreBuilding? StoreOf(Card card) =>
        _loop.World.StoreBuildings.Find(s => s.Id == card.Subject.Id);

    // ---------------------------------------------------------------
    //  Refreshing — what each kind of card says
    // ---------------------------------------------------------------

    private void RefreshCards(SimWorld world)
    {
        for (int i = _cards.Count - 1; i >= 0; i--)
        {
            Card card = _cards[i];
            if (!ShowCard(world, card))
            {
                // The subject is gone — a demolished building, a villager who died.
                CloseCard(card);
                continue;
            }

            // ⚠️ A top-level Control grows to its minimum and never shrinks back; a card whose
            // people list just hid would keep the room. Reset to the width and let it find its height.
            card.Panel.Size = new Vector2(CardWidth, 0f);

            // At the UI scale, like every panel (D379, Joe: *"the scale is larger than the rest
            // of the UI panels"*). A card is added straight to the scene and was never in the
            // floaters `FitFloaters` scales, so it drew at full size beside panels at three
            // quarters. Set here rather than by registering it: cards close, and a freed node in
            // a persistent list is a crash waiting for the next frame. About its own corner, so
            // it stays where it was put and where it was dragged.
            card.Panel.PivotOffset = Vector2.Zero;
            card.Panel.Scale = new Vector2(_uiScale, _uiScale);
        }
    }

    /// <summary>Write the card; false if its subject no longer exists.</summary>
    private bool ShowCard(SimWorld world, Card card)
    {
        if (card.SettingsBuiltFor != card.Subject.Kind)
        {
            BuildSettings(card, world);
        }

        card.Records.Visible = false;

        // A person's card is sections, not the five parts (D431): per refresh, because a card is
        // retargeted from a building to a person and back.
        bool person = card.Subject.Kind == CardKind.Villager;
        card.Status.TooltipText = string.Empty;
        card.Panel.AddThemeStyleboxOverride("panel", CardSkin(ReferenceEquals(_selectedCard, card), person));
        card.Subtitle.Visible = person;
        card.Person.Box.Visible = person;
        card.Portrait.Visible = !person;
        card.Caption.Visible = !person;

        bool shown = card.Subject.Kind switch
        {
            CardKind.Store => StoreOf(card) is StoreBuilding store && ShowStore(card, store),
            CardKind.Workplace => world.FindWorkplace(card.Subject.Id) is Workplace place && ShowWorkplace(world, card, place),
            CardKind.Household => world.FindHousehold(card.Subject.Id) is Household home && ShowHousehold(world, card, home),
            CardKind.Villager => world.FindVillager(card.Subject.Id) is Villager villager && ShowVillager(world, card, villager),
            CardKind.Library => LibraryOf(card.Subject) is Library library && ShowLibrary(world, card, library),
            CardKind.TownHall => world.TownHall is TownHall hall && ShowTownHall(world, card, hall),
            CardKind.Well => WellOf(card.Subject) is Well well && ShowWell(world, card, well),
            _ => false,
        };

        if (shown)
        {
            // ⛔ ONLY A BUILDING CAN BE RENAMED (D380, Joe: *"villager names have the edit function
            // (it doesn't save) — let's remove that. only buildings should be renamable (excluding
            // homes)"*). Per refresh, because a retargeted card changes kind.
            card.Edit.Visible = card.Subject.Kind is CardKind.Store or CardKind.Workplace;
            ShowSettings(world, card);
        }

        return shown;
    }

    // ---------------------------------------------------------------
    //  The Settings fold — every control the docked panel held, on the card (D377)
    // ---------------------------------------------------------------

    /// <summary>
    /// Build the rows this kind of card can set. Each handler selects the card first, so the
    /// existing "selected" methods act on this card's subject — one rule, not two.
    /// </summary>
    private void BuildSettings(Card card, SimWorld world)
    {
        while (card.Settings.GetChildCount() > 0)
        {
            Node child = card.Settings.GetChild(0);
            card.Settings.RemoveChild(child);
            child.QueueFree();
        }

        while (card.Storage.GetChildCount() > 0)
        {
            Node child = card.Storage.GetChild(0);
            card.Storage.RemoveChild(child);
            child.QueueFree();
        }

        var c = new CardControls();
        card.Controls = c;
        card.SettingsBuiltFor = card.Subject.Kind;
        VBoxContainer body = card.Settings;

        switch (card.Subject.Kind)
        {
            case CardKind.Store:
            {
                // The full-store ring, per building (Joe, D140).
                (c.FullRow, HFlowContainer fullControls) = InspectorRow(body, Muted("When full:"));
                c.FullMarker = new Button { Text = "Marker: ON" };
                c.FullMarker.Pressed += () => Act(card, ToggleSelectedFullMarker);
                fullControls.AddChild(c.FullMarker);

                // ⭐ ONE CONTROL FOR WHETHER IT TAKES DELIVERIES (Joe, D389): Open takes what the
                // Takes row allows; Closed takes nothing and keeps what it has; Emptying is closed
                // and carried out, and stays Closed when the last armful leaves (D413) until the
                // player opens it. Three
                // exclusive states rather than a switch beside Empty — two switches whose meanings
                // overlap (closed-and-emptying?) is the D139 shape.
                (c.StockingRow, HFlowContainer stockingControls) = InspectorRow(body, Muted("Stocking:"));
                foreach (Stocking state in new[] { Stocking.Open, Stocking.Closed, Stocking.Emptying })
                {
                    Stocking chosen = state;
                    var button = new Button { Text = chosen.ToString(), ToggleMode = true };
                    button.Pressed += () => Act(card, () => SetSelectedStocking(chosen));
                    stockingControls.AddChild(button);
                    c.StockingButtons.Add((chosen, button));
                }

                // ⭐ THE STORAGE LIST (D393, Joe with Foundation's warehouse card: *"add a new
                // line for each item that can go in a warehouse/granary/etc."*). One row per good
                // the kind can hold — chip · name · amount · take/refuse — and the take toggle
                // (Joe, D141) lives ON the row rather than in a Takes: row here, so a good is
                // described in one place (D139: two rows for one good is two ways to say one thing).
                // Every row is the same width whatever it holds: the amount is an `Amount()` cell
                // (D367), so `+12,345` and `—` measure alike.
                for (int g = 0; g < world.GoodsCatalog.Count; g++)
                {
                    var goods = (Goods)g;
                    var row = new HBoxContainer { Visible = false };
                    row.AddThemeConstantOverride("separation", 6);
                    row.AddChild(Chip(ChipColour(goods)));
                    Label name = Body(GoodsName(world, goods));
                    name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    name.ClipText = true;
                    name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    row.AddChild(name);
                    Label amount = Amount();
                    amount.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
                    row.AddChild(amount);
                    var take = new Button { Text = "✓", ToggleMode = true, CustomMinimumSize = new Vector2(30, 0) };
                    take.Pressed += () => Act(card, () => ToggleSelectedAccepts(goods));
                    row.AddChild(take);
                    card.Storage.AddChild(row);
                    c.Storage.Add((goods, row, name, amount, take));
                }

                // How much this counter keeps, per good (Joe, D372) — markets only.
                (c.LimitRow, HFlowContainer limitControls) = InspectorRow(body, Muted("Keeps up to:"));
                for (int g = 0; g < world.GoodsCatalog.Count; g++)
                {
                    var goods = (Goods)g;
                    if (!world.GoodsCatalog.StoredBy(goods, StoreKind.Market))
                    {
                        continue;
                    }

                    var cell = new HBoxContainer();
                    cell.AddChild(Body(GoodsName(world, goods)));
                    var amount = new SpinBox
                    {
                        MinValue = 0,
                        MaxValue = 100_000,
                        Step = 10,
                        Editable = true,
                        CustomMinimumSize = new Vector2(74, 0),
                    };
                    var clear = new Button { Text = "clear", Flat = true, Disabled = true };
                    amount.ValueChanged += value => Act(card, () => SetSelectedMarketLimit(goods, (int)value));
                    clear.Pressed += () => Act(card, () => SetSelectedMarketLimit(goods, null));
                    cell.AddChild(amount);
                    cell.AddChild(clear);
                    limitControls.AddChild(cell);
                    c.Limits.Add((goods, cell, amount, clear));
                }

                break;
            }

            case CardKind.Workplace:
            {
                // The idle ring, per building (D270/D271).
                c.IdleLabel = Muted(string.Empty);
                (c.IdleRow, HFlowContainer idleControls) = InspectorRow(body, c.IdleLabel);
                c.IdleMarker = new Button { Text = "Marker: ON" };
                c.IdleMarker.Pressed += () => Act(card, ToggleSelectedIdleMarker);
                idleControls.AddChild(c.IdleMarker);

                // The ground brush (D86) and the forester's mode.
                c.GroundLabel = Muted(string.Empty);
                (c.GroundRow, HFlowContainer groundControls) = InspectorRow(body, c.GroundLabel);
                var give = new Button { Text = "Give ground" };
                give.Pressed += () => Act(card, () => PaintGroundForSelection(1));
                groundControls.AddChild(give);
                var takeBack = new Button { Text = "Take back" };
                takeBack.Pressed += () => Act(card, () => PaintGroundForSelection(-1));
                groundControls.AddChild(takeBack);
                c.Mode = new Button { Text = "Planting: off" };
                c.Mode.Pressed += () => Act(card, ToggleSelectedMode);
                groundControls.AddChild(c.Mode);
                c.GroundNote = Wrapped(Muted(string.Empty));
                c.GroundNote.Visible = false;
                c.GroundRow.AddChild(c.GroundNote);

                // ⭐ What a smithy forges (Joe, D434: "stone as default and a dropdown with the other
                // option(s)"; `tools-and-the-smith.md §9.3`) — the Kept-on dropdown's shape, every
                // tool row a forge makes, best last so stone (the default) reads first.
                c.ForgeLabel = Muted("Forges:");
                (c.ForgeRow, HFlowContainer forgeControls) = InspectorRow(body, c.ForgeLabel);
                c.Forge = new OptionButton { FitToLongestItem = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                c.Forge.ItemSelected += index => Act(card, () => SetSelectedForgeGood((int)index));
                forgeControls.AddChild(c.Forge);

                // The build queue, for a site.
                c.QueueLabel = Muted("Build queue:");
                (c.QueueRow, HFlowContainer queueControls) = InspectorRow(body, c.QueueLabel);
                var sooner = new Button { Text = "▲ Sooner" };
                sooner.Pressed += () => Act(card, () => MoveSelectedInQueue(-1));
                queueControls.AddChild(sooner);
                var later = new Button { Text = "▼ Later" };
                later.Pressed += () => Act(card, () => MoveSelectedInQueue(+1));
                queueControls.AddChild(later);
                break;
            }

            default:
                break;
        }

        card.SettingsToggle.Visible = card.Settings.GetChildCount() > 0;
    }

    /// <summary>Select the card so the "selected" handlers act on its subject, then act.</summary>
    private void Act(Card card, System.Action action)
    {
        SelectCard(card);
        action();
        RefreshCards(_loop.World);
    }

    /// <summary>Write the fold's rows for the subject as it stands — the docked panel's refresh, per card.</summary>
    private void ShowSettings(SimWorld world, Card card)
    {
        CardControls c = card.Controls;
        switch (card.Subject.Kind)
        {
            case CardKind.Store when StoreOf(card) is StoreBuilding store:
                c.FullRow!.Visible = true;
                c.FullMarker!.Text = _map.FullMarkerShownFor(store.Id) ? "Marker: ON" : "Marker: off";
                c.StockingRow!.Visible = true;
                foreach ((Stocking state, Button button) in c.StockingButtons)
                {
                    button.ButtonPressed = store.Stocking == state;
                }

                // ⚠️ `PlayerAllows`, not `Accepts` (D389): the take toggle is WHAT KINDS, and it read
                // as all-off while the store was emptying because `Accepts` folds the stocking in.
                // A refused good is dimmed and its amount still shown — a store holding what it no
                // longer takes is exactly the state the player wants to see.
                foreach ((Goods goods, HBoxContainer row, Label name, Label amount, Button take) in c.Storage)
                {
                    bool holdable = store.CanEverHold(goods);
                    row.Visible = holdable;
                    if (!holdable)
                    {
                        continue;
                    }

                    bool allowed = store.PlayerAllows(goods);
                    int held = store.Store[goods];
                    amount.Text = held > 0 ? held.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : "—";
                    take.ButtonPressed = allowed;
                    take.Text = allowed ? "✓" : "✕";
                    take.TooltipText = allowed ? $"takes {GoodsName(world, goods)} — click to refuse" : $"refuses {GoodsName(world, goods)} — click to take";
                    Color tone = allowed && held > 0 ? Colors.White : new Color(1, 1, 1, 0.55f);
                    name.Modulate = tone;
                    amount.Modulate = tone;
                }

                c.LimitRow!.Visible = store.Kind == StoreKind.Market;
                if (store.Kind == StoreKind.Market)
                {
                    foreach ((Goods goods, Control cell, SpinBox amount, Button clear) in c.Limits)
                    {
                        cell.Visible = store.CanEverHold(goods);
                        amount.SetValueNoSignal(world.MarketStockLimit(store, goods));
                        clear.Disabled = store.Limits.For(goods) is null;
                    }
                }

                break;

            case CardKind.Workplace when world.FindWorkplace(card.Subject.Id) is Workplace place:
            {
                bool built = !place.IsSite;
                c.IdleRow!.Visible = built;
                if (built)
                {
                    c.IdleLabel!.Text = world.IdleNote(place) is string why ? why : $"{place.Name} is working.";
                    c.IdleMarker!.Text = _map.IdleMarkerShownFor(place.Id) ? "Marker: ON" : "Marker: off";
                }

                bool keepsGround = built && SimWorld.KeepsWorkGround(place.Kind);
                c.GroundRow!.Visible = keepsGround;
                c.GroundNote!.Visible = false;
                if (keepsGround)
                {
                    int tiles = world.Zones.WorkGroundTiles(place.Id);
                    int allowance = world.WorkGroundAllowanceFor(place);
                    c.GroundLabel!.Text = place.WorkerIds.Count == 0
                        ? $"Ground — {tiles} tiles, nobody working it:"
                        : $"Ground — {tiles} tiles, enough hands for {allowance}:";
                    if (world.OverstretchedNote(place) is string stretched)
                    {
                        c.GroundNote.Text = stretched;
                        c.GroundNote.Visible = true;
                    }

                    c.Mode!.Visible = place.Kind == JobKind.Forester;
                    if (c.Mode.Visible)
                    {
                        c.Mode.Text = place.Mode != WorkMode.FellAndPlant
                            ? "Felling: off"
                            : world.MayFell(place) ? "Felling: ON" : "Felling: ON — held by the log limit";
                    }
                }

                c.ForgeRow!.Visible = built && place.Kind == JobKind.Smith;
                if (c.ForgeRow.Visible)
                {
                    FillForge(world, c, place);
                }

                c.QueueRow!.Visible = place.IsSite;
                if (place.IsSite)
                {
                    c.QueueLabel!.Text = $"Build queue — {world.QueuePositionOf(place)} of {world.BuildQueue().Count}:";
                }

                break;
            }

            default:
                break;
        }
    }

    /// <summary>The smithy's dropdown: every tool a forge makes, stone first; the item this smithy is set to selected (D446).</summary>
    private static void FillForge(SimWorld world, CardControls c, Workplace smithy)
    {
        AddForgeItems(world, c);
        int selected = c.ForgeGoods.IndexOf(smithy.ForgeGood);
        if (c.Forge!.Selected != selected)
        {
            c.Forge.Select(selected);
        }
    }

    /// <summary>The dropdown's items, once: every tool row with a recipe, worst first so stone (the default) leads.</summary>
    private static void AddForgeItems(SimWorld world, CardControls c)
    {
        if (c.ForgeGoods.Count == 0)
        {
            IReadOnlyList<Goods> best = world.GoodsCatalog.ToolsBestFirst;
            for (int i = best.Count - 1; i >= 0; i--)
            {
                GoodRow row = world.GoodsCatalog[best[i]];
                if (row.ForgedFrom.Count == 0)
                {
                    continue;
                }

                c.Forge!.AddItem(Capitalise(row.Name));
                c.Forge.SetItemTooltip(c.Forge.ItemCount - 1, $"{Capitalise(row.Name)}: {row.ToolUses} uses, {WhatAToolIsWorth(row)}.");
                c.ForgeGoods.Add(best[i]);
            }
        }
    }

    /// <summary>
    /// Where a workplace's hands work, as the card's middle number (D404, Joe: *"what does no ground
    /// mean?"*). Only foresters and farms work painted ground (<see cref="SimWorld.KeepsWorkGround"/>);
    /// a forager works a ring round the hut, a hunter the woods within a range, a fisher the water —
    /// and the card said *"no ground"* of all three, which read as a fault.
    /// </summary>
    /// <summary>
    /// Whether a caption fits one of a card's three number cells — the cell derived as the probe
    /// derives it (three ExpandFill cells, six apart, inside the panel's margins), from the card's
    /// fixed width rather than its laid-out size, so the first frame is answered too (D422).
    /// </summary>
    private static bool FitsItsCell(Card card, Label label, string caption)
    {
        float inner = CardWidth - card.Panel.GetThemeStylebox("panel").GetMinimumSize().X;
        float cell = (inner - (2 * 6f)) / 3f;
        float wide = label.GetThemeFont("font").GetStringSize(caption, HorizontalAlignment.Left, -1, label.GetThemeFontSize("font_size")).X;
        return wide <= cell + 1f;
    }

    /// <summary>A workplace's third cell: what its store holds at most — or, with no wall, its seats (D422).</summary>
    private static (string Value, string Key) WallCell(int capacity, int places) =>
        capacity != int.MaxValue
            ? ($"{capacity:N0}", "it holds")
            : ($"{places}", places == 1 ? "seat" : "seats");

    /// <summary>
    /// A buffer's goods for a card cell — <b>as many as fit, largest first</b>, the rest folded into
    /// <em>others</em>, and never less than the largest (D422, Joe: the lodge should say *"10 meat · 8
    /// leather"*). <paramref name="fits"/> measures a caption against the cell, so a long modded name
    /// folds instead of clipping.
    /// </summary>
    internal static (string Amounts, string Names) WhatItHolds(SimWorld world, Stockpile store, Func<string, bool> fits)
    {
        var held = new List<(Goods Goods, int Amount)>();
        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            if (store[(Goods)g] > 0)
            {
                held.Add(((Goods)g, store[(Goods)g]));
            }
        }

        if (held.Count == 0)
        {
            return ("0", "held here");
        }

        // Largest first; a tie keeps catalogue order, so the card never flickers between two.
        held.Sort((a, b) => a.Amount != b.Amount ? b.Amount.CompareTo(a.Amount) : a.Goods.CompareTo(b.Goods));

        // Two names at most — a third of a card holds no more — then one and "others", then the
        // largest alone with a count of the rest.
        for (int named = Math.Min(2, held.Count); named >= 1; named--)
        {
            var amounts = new List<string>();
            var names = new List<string>();
            int rest = 0;
            for (int i = 0; i < held.Count; i++)
            {
                if (i < named)
                {
                    amounts.Add($"{held[i].Amount:N0}");
                    names.Add(world.GoodsCatalog.NameOf(held[i].Goods));
                }
                else
                {
                    rest += held[i].Amount;
                }
            }

            if (rest > 0)
            {
                amounts.Add($"{rest:N0}");
                names.Add("others");
            }

            string caption = string.Join(" · ", names);
            if (fits(caption))
            {
                return (string.Join(" · ", amounts), caption);
            }
        }

        int total = 0;
        foreach ((Goods _, int amount) in held)
        {
            total += amount;
        }

        string first = world.GoodsCatalog.NameOf(held[0].Goods);
        return ($"{total:N0}", held.Count > 1 ? $"{first} +{held.Count - 1}" : first);
    }

    private static (string Value, string Key) WhereItWorks(JobKind kind, int ground, int ring, int huntingRange)
    {
        if (SimWorld.KeepsWorkGround(kind))
        {
            return ground > 0 ? ($"{ground}", "tiles of ground") : ("—", "no ground painted");
        }

        if (ring > 0)
        {
            return ($"{ring}", "tiles round it");
        }

        if (kind == JobKind.Hunter)
        {
            return ($"{huntingRange}", "tiles of range");
        }

        return kind == JobKind.Fisher ? ("—", "the water") : ("—", "at the building");
    }

    private bool ShowStore(Card card, StoreBuilding store)
    {
        Title(card, store.Name, renamable: true);

        int used = store.Store.Capacity - store.Store.FreeSpace;
        if (store.Emptying)
        {
            Status(card, working: false, $"Being emptied — {used} left to carry out; open again when it is.");
        }
        else if (store.Stocking == Stocking.Closed)
        {
            Status(card, working: false, $"Closed to deliveries — holds {used}; households still fetch from it.");
        }
        else if (store.Store.IsFull)
        {
            Status(card, working: false, $"Full — {used} of {store.Store.Capacity}. Nothing more fits until something leaves.");
        }
        else
        {
            Status(card, working: true, $"{used} of {store.Store.Capacity} used.");
        }

        card.WorkersRow.Visible = false;
        card.PeopleScroll.Visible = false;

        // ⭐ EVERY GOOD IT CAN HOLD, ON ITS OWN LINE (D393) — in place of the three biggest heaps.
        // Joe, at a warehouse reading *714 used* over three numbers that summed to 660: *"I'm not
        // sure where the iron is being stored."* It was there; the card named three of five. The
        // rows are filled with the settings (`ShowSettings`), because the take toggle is theirs.
        card.Numbers.Visible = false;
        card.Storage.Visible = true;

        card.Portrait.Show(
            VillageMap.ColourOf(store.Kind), store.ExtentWidth * 0.8f, store.ExtentHeight * 0.8f, store.Facing.Raw,
            store.Store.IsFull && _map.FullMarkerShownFor(store.Id) ? VillageMap.FullStoreRing : null);
        card.Caption.Text = $"a {store.Kind.ToString().ToLowerInvariant()} · ring when full: {(_map.FullMarkerShownFor(store.Id) ? "on" : "off")}";
        return true;
    }

    private bool ShowWorkplace(SimWorld world, Card card, Workplace place)
    {
        Title(card, place.Name, renamable: true);
        card.Numbers.Visible = true;
        card.Storage.Visible = false;

        if (place.Construction is { IsFinished: false } site)
        {
            // ⭐ EVERY MATERIAL, NOT JUST THE LOGS (Joe, year 114: a lodge read *40/40 logs* while its
            // builder said *"still wants 5 stone, and nowhere within reach has any"* — the card showed
            // a site fully supplied). The status names what is missing, in the sim's own words
            // (`DescribeWhatIsMissing`), so a clipped number cell can never hide it.
            string queued = $"Being built — {Ordinal(world.QueuePositionOf(place))} in the queue";
            Status(card, working: false, site.HasMaterials
                ? queued + "."
                : $"{queued} · still wants {site.DescribeWhatIsMissing(world.GoodsCatalog)}.");
            card.WorkersRow.Visible = false;
            var have = new List<string>();
            var names = new List<string>();
            foreach (MaterialCost material in site.Recipe.Materials)
            {
                have.Add($"{site.Delivered(material.Goods)}/{material.Amount}");
                names.Add(world.GoodsCatalog.NameOf(material.Goods));
            }

            Number(card, 0, string.Join(" · ", have), string.Join(" · ", names));
            Number(card, 1, $"{site.WorkDone}/{site.Recipe.WorkTicks}", "work");
            Number(card, 2, $"{world.BuildQueue().Count}", "sites queued");
        }
        else
        {
            // ⭐ A BUFFER THE HANDS CANNOT KEEP UP WITH SAYS SO (D394): Joe's lodge held 2,103 meat
            // — a hunt is 900 in fifteen ticks, a carry is forty — and its card read *Working*.
            string? why = world.IdleNote(place);
            bool swollen = why is null && world.BufferIsSwollen(place);
            Status(card, working: why is null && !swollen,
                why ?? (swollen
                    ? $"Working — {world.FoodIn(place.Store):N0} waiting to be carried in, {world.ArmfulsWaitingIn(place)} armfuls."
                    : $"Working — {place.WorkerIds.Count} of {place.Places} at it."));

            card.WorkersRow.Visible = true;
            card.WorkersLabel.Text = ProfessionName(world, place.Kind) + "s";
            card.Workers.Text = $"{place.WorkerIds.Count} / {place.Places}";

            // ⭐ WHAT IT HOLDS, GOOD BY GOOD (D422, Joe's D421 note: the lodge read *"18 of 2,700 hel…"*,
            // meat and hides summed and the caption cut off). The site card's shape — amounts over
            // names, *"10 · 8"* over *"meat · leather"* — read from the store and named by the
            // catalogue, so any workplace with a buffer says it the same way. The wall moved to the
            // third cell (`WallCell`).
            (string amounts, string names) = WhatItHolds(world, place.Store, caption => FitsItsCell(card, card.Keys[0], caption));
            Number(card, 0, amounts, names);
            (string value, string key) reach = WhereItWorks(
                place.Kind,
                world.Zones.WorkGroundTiles(place.Id),
                place.GatheringRadius,
                world.BuildingsCatalog[BuildingKind.HunterLodge]?.HuntingRadius ?? 0);
            Number(card, 1, reach.value, reach.key);

            // ⚠️ A BUFFER WITH NO WALL HAS NO "OF" (Joe: *"what is the 0 of 2,147,483 representative
            // of?"*). A workplace without a store of its own is `int.MaxValue` wide — a sentinel for
            // "no limit", and trap 115's number printed as if it were a size. Seats were here until
            // D422; the Workers row above already says *"1 / 2"*.
            (string wall, string wallKey) = WallCell(place.Store.Capacity, place.Places);
            Number(card, 2, wall, wallKey);
        }

        card.PeopleScroll.Visible = false;

        bool idleRing = !place.IsSite && world.IdleNote(place) is not null && _map.IdleMarkerShownFor(place.Id);
        card.Portrait.Show(
            TradeGlyph.ColourOf(place.Kind), place.ExtentWidth * 0.8f, place.ExtentHeight * 0.8f, place.Facing.Raw,
            idleRing ? VillageMap.IdleWorkplaceRing : null);
        card.Caption.Text = place.IsSite ? "a building site" : Workers(world, place);
        return true;
    }

    private static string Workers(SimWorld world, Workplace place)
    {
        if (place.WorkerIds.Count == 0)
        {
            return "nobody posted here";
        }

        var names = new List<string>(place.WorkerIds.Count);
        for (int i = 0; i < place.WorkerIds.Count && names.Count < 3; i++)
        {
            if (world.FindVillager(place.WorkerIds[i]) is Villager v)
            {
                names.Add(v.Name);
            }
        }

        string list = string.Join(", ", names);
        return place.WorkerIds.Count > 3 ? $"{list} and {place.WorkerIds.Count - 3} more" : list;
    }

    private bool ShowHousehold(SimWorld world, Card card, Household home)
    {
        card.Numbers.Visible = true;
        card.Storage.Visible = false;
        Title(card, $"The {home.Name} household", renamable: true);

        int living = world.LivingMembersOf(home);
        int food = world.FoodIn(home.Stockpile);
        int foodTarget = world.TargetFoodFor(home);
        int fuel = home.Stockpile.Firewood;
        int fuelTarget = VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config);

        if (living == 0)
        {
            Status(card, working: false, home.HomePosition is null ? "Nobody lives here." : "Nobody lives here now. The next couple to pair up will move in.");
        }
        else if (home.HomePosition is null)
        {
            Status(card, working: false, "No house yet — they sleep at the founding cart.");
        }
        else
        {
            int share = foodTarget == 0 ? 100 : food * 100 / foodTarget;
            bool cold = fuel == 0 && !SeasonRules.IsGatherable(world.Clock.Season);
            Status(
                card,
                working: share > world.Config.RestockEmergencyPercent && !cold,
                cold ? $"Cold — no firewood in the house. Larder at {share}%."
                    : share <= world.Config.RestockEmergencyPercent ? $"Nearly out of food — larder at {share}%; somebody is going for more."
                    : home.ToppingUpFood ? $"Fed — larder at {share}%, topping it up from the market."
                    : $"Fed and warm — larder at {share}%; they go to the market at half.");
        }

        card.WorkersRow.Visible = false;
        Number(card, 0, $"{food}/{foodTarget}", "food");
        Number(card, 1, $"{fuel}/{fuelTarget}", "firewood");
        Number(card, 2, $"{living}", living == 1 ? "person" : "people");

        // The people, scrolling past four (Joe).
        card.PeopleScroll.Visible = true;
        ClearThePeople(card);

        int shown = 0;
        for (int i = 0; i < home.MemberIds.Count; i++)
        {
            if (world.FindVillager(home.MemberIds[i]) is not { Alive: true } member)
            {
                continue;
            }

            shown++;

            var row = new HBoxContainer();
            Label who = Body($"{member.Name}, {member.AgeYears}");
            who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            who.ClipText = true;
            who.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            Label what = Muted(TradeWordFor(world, member));
            row.AddChild(who);
            row.AddChild(what);
            card.People.AddChild(row);
        }

        // Room for the rows it has, up to four; past four it scrolls (Joe).
        card.PeopleScroll.CustomMinimumSize = new Vector2(0, RowSize * 1.45f * Mathf.Min(PeopleRowsShown, Mathf.Max(1, shown)));

        float wide = world.BuildingsCatalog[BuildingKind.Home]?.ExtentWidth ?? 1;
        float deep = world.BuildingsCatalog[BuildingKind.Home]?.ExtentHeight ?? 1;
        card.Portrait.Show(VillageMap.HomeColour, wide * 0.8f, deep * 0.8f, home.HomeFacing.Raw, null);

        // ⭐ WHY THE HOUSE IS WHERE IT IS (D386): the chooser's own sentence, in the currency it
        // scored — *"10 tiles to work and 3 to the granary, facing the lane to the south, beside
        // the Ashfords."* Held on the household from the day the site was chosen.
        card.Caption.Text = home.HomePosition is null
            ? "no house"
            : home.WhyHere.Length > 0 ? $"a wooden cabin — {home.WhyHere}" : "a wooden cabin";
        return true;
    }

    /// <summary>
    /// A person's card (D431): who they are, what they are doing now, then WORK · TOOL · NEEDS ·
    /// SKILLS and three jumps — Joe, with Foundation's villager panel: <i>"a UX nightmare. copy off
    /// screen, no real hierarchy of information. nothing about tools."</i>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The banner is the person, now; the workplace's reason sits under WORK.</b> The status
    /// line used to be <see cref="Villager.WorkNote"/> — a building's sentence (<i>"forester's hut 1
    /// has stopped felling…"</i>) in the person's headline. It is <see cref="Villager.DescribeState"/>
    /// now, amber while hungry or while there is a note, and the note is read beside the job.
    /// </para>
    /// <para>
    /// ⛔ <b>Nothing here clips</b>: every sentence wraps, and the probe's <c>villager card:</c> line
    /// fails if a label on this card is set to trim.
    /// </para>
    /// </remarks>
    private bool ShowVillager(SimWorld world, Card card, Villager villager)
    {
        if (!villager.Alive)
        {
            return false;
        }

        Title(card, villager.FullName, renamable: false);
        card.Numbers.Visible = false;
        card.Storage.Visible = false;
        card.WorkersRow.Visible = false;
        card.PeopleScroll.Visible = false;

        VillagerParts p = card.Person;
        Workplace? job = world.FindWorkplace(villager.WorkplaceId);
        Household home = world.HouseholdOf(villager);
        card.Subtitle.Text = $"{Capitalise(TradeWordFor(world, villager))} · {villager.AgeYears} · {home.Name} household";

        bool hungry = villager.Hunger >= world.Config.EatThreshold;
        bool noted = !string.IsNullOrWhiteSpace(villager.WorkNote);
        string doing = villager.DescribeState(job?.Name);
        Status(card, working: !hungry && !noted, hungry ? $"Hungry — {doing}." : Capitalise(doing) + ".");

        // ⭐ A PROBLEM WITH THEIR WORK TURNS THE BANNER AMBER, AND THE REASON IS ITS TOOLTIP (D432,
        // Joe: "way too wordy and crowded"). The note is the workplace's sentence and runs to three
        // lines; printed, it pushed everything else down the card. It is one hover away, on the
        // banner and on the job — ⛔ never dropped: why somebody is not working is §1.1's question.
        card.Status.TooltipText = noted ? villager.WorkNote : string.Empty;

        // ---- WORK ----
        // ⛔ No distances and no runner-up (D432, Joe: "remove all of the information about tiles …
        // all of that needs to be removed from villager cards"). `JobReason` and `CommuteNote` still
        // go to the audit log where they are written (`LabourAllocator.Assign`), which is where
        // §2.2's "why this job, and who else" stays traceable.
        FillKeep(world, p, villager);
        p.Job.Text = job is not null ? $"{ProfessionName(world, job.Kind)} at {job.Name}"
            : villager.CanWork ? "Laborer — goes where the village needs hands"
            : "A child — too young to work";
        p.Job.Modulate = noted ? LightStopped : Colors.White;
        p.Job.TooltipText = card.Status.TooltipText;

        // ---- TOOL (D391, D430) ----
        bool usesTool = job is not null && world.JobsCatalog.UsesTool(job.Kind);
        p.ToolSection.Visible = usesTool;
        if (usesTool)
        {
            // ⭐ WHICH KIND, off the held good's row (`tools-and-the-smith.md §9.2`): "Iron tools — 212 / 250".
            int uses = villager.ToolUses;
            GoodRow held = world.GoodsCatalog[villager.ToolGood];
            p.ToolBar.MaxValue = Mathf.Max(1, Mathf.Max(held.ToolUses, uses));
            p.ToolBar.Value = uses;
            p.ToolBar.Visible = uses > 0;
            p.ToolUses.Text = uses > 0 ? $"{Capitalise(held.Name)} — {uses} / {held.ToolUses}" : "No tool — works slower";
            p.ToolUses.Modulate = uses > 0 ? new Color(1, 1, 1, 0.7f) : LightStopped;

            // What a tool does is one hover away (D432): the section says how much is left.
            p.ToolSection.TooltipText = uses > 0
                ? $"{uses} of {held.ToolUses} uses left. {Capitalise(held.Name)} make them {WhatAToolIsWorth(held)}."
                : $"No tool in hand or on a shelf they can reach. {WhatEachToolIsWorth(world)}";
        }

        // ---- NEEDS: what the sim already knows, read out (D431 — no new mechanics) ----
        SetNeed(p.Needs[0], !hungry, hungry ? "Hungry" : "Fed",
            hungry ? $"{villager.Name} is hungry and will stop to eat." : $"{villager.Name} has eaten.");
        int threshold = world.Config.ExposureThreshold;
        int cold = threshold > 0 ? villager.Cold * 100 / threshold : 0;
        SetNeed(p.Needs[1], villager.Cold == 0, villager.Cold == 0 ? "Warm" : "Cold",
            villager.Cold == 0 ? $"{villager.Name} is warm." : $"{cold}% of the way to freezing — a hearth warms them, a roof slows the cold.");
        bool housed = home.HomePosition is not null;
        SetNeed(p.Needs[2], housed, housed ? "Housed" : "No roof",
            housed ? $"Lives with the {home.Name} household." : $"The {home.Name} household has no home yet.");

        // ---- SKILLS: every trade given a year, longest first; the bar is the way to mastery ----
        var trades = TheirTrades(world, villager);
        p.SkillsSection.Visible = trades.Count > 0;
        while (p.Skills.Count < trades.Count)
        {
            AddSkillRow(p);
        }

        for (int i = 0; i < p.Skills.Count; i++)
        {
            (VBoxContainer row, Label name, Label years, ProgressBar bar) = p.Skills[i];
            row.Visible = i < trades.Count;
            if (!row.Visible)
            {
                continue;
            }

            (SkillRow skill, SkillProgress progress, int held, string sentence) = trades[i];
            int mastery = world.Config.MasteryWorkFor(skill);
            name.Text = Capitalise(skill.Name);
            years.Text = progress.Mastered ? "master" : Years(held).ToLowerInvariant();
            bar.MaxValue = Mathf.Max(1, mastery);
            bar.Value = Mathf.Min(progress.Work, mastery);
            row.TooltipText = sentence;
        }

        p.Work.Disabled = job is null;
        return true;
    }

    /// <summary>Every kind, best first, in one sentence each — <i>"Iron tools would make them 50% quicker…"</i>.</summary>
    private static string WhatEachToolIsWorth(SimWorld world)
    {
        var sentences = new List<string>();
        foreach (Goods tool in world.GoodsCatalog.ToolsBestFirst)
        {
            GoodRow row = world.GoodsCatalog[tool];
            sentences.Add($"{Capitalise(row.Name)} would make them {WhatAToolIsWorth(row)}.");
        }

        return string.Join(" ", sentences);
    }

    /// <summary>What a tool does, from its row's two numbers — <i>"34% quicker at each action and 25% more from it"</i>.</summary>
    private static string WhatAToolIsWorth(GoodRow tool)
    {
        int speed = tool.ToolSpeedBonusPercent;
        int yield = tool.ToolYieldBonusPercent;
        string quicker = speed > 0 ? $"{speed}% quicker at each action" : string.Empty;
        string more = yield > 0 ? $"{yield}% more from it" : string.Empty;
        return quicker.Length > 0 && more.Length > 0 ? $"{quicker} and {more}" : quicker + more;
    }

    /// <summary>The Kept-on dropdown: the village decides, or one trade (Joe, 2026-08-22; a dropdown since D431).</summary>
    private static void FillKeep(SimWorld world, VillagerParts p, Villager villager)
    {
        if (p.KeepTrades.Count == 0)
        {
            p.Keep.AddItem("The village decides");
            foreach (JobKind trade in System.Enum.GetValues<JobKind>())
            {
                p.Keep.AddItem($"Always {world.JobsCatalog.NameOf(trade)}");
                p.KeepTrades.Add(trade);
            }
        }

        int selected = villager.PinnedTrade is JobKind kept ? p.KeepTrades.IndexOf(kept) + 1 : 0;
        if (p.Keep.Selected != selected)
        {
            p.Keep.Select(selected);
        }

        p.Keep.Disabled = !villager.CanWork;
        p.Keep.TooltipText = villager.PinnedTrade is null
            ? $"The village decides where {villager.Name} works — pick a trade to keep them on it."
            : $"{villager.Name} is kept on {world.JobsCatalog.NameOf(villager.PinnedTrade.Value)} — pick \"The village decides\" to hand them back.";
    }

    /// <summary>The player chose from the dropdown: keep them on a trade, or hand them back. The sim owns the decision.</summary>
    private void KeepFromTheCard(Card card, int index)
    {
        if (_loop.World.FindVillager(card.Subject.Id) is not Villager villager)
        {
            return;
        }

        JobKind? wanted = index >= 1 && index <= card.Person.KeepTrades.Count ? card.Person.KeepTrades[index - 1] : null;
        Act(card, () =>
        {
            _loop.World.SetPinnedTrade(villager, wanted);
            RefreshInspector(_loop.World);
        });
    }

    private enum PersonJump
    {
        Find,
        Home,
        Work,
    }

    /// <summary>The card's three jumps: the camera to them, their home's card, their workplace's card.</summary>
    private void JumpFromThePerson(Card card, PersonJump jump)
    {
        SimWorld world = _loop.World;
        if (world.FindVillager(card.Subject.Id) is not Villager villager)
        {
            return;
        }

        switch (jump)
        {
            case PersonJump.Find:
                _map.CentreOn(new Vector2(villager.Tile.X + 0.5f, villager.Tile.Y + 0.5f));
                break;
            case PersonJump.Home:
                OpenCard(new CardSubject(CardKind.Household, world.HouseholdOf(villager).Id));
                break;
            case PersonJump.Work when world.FindWorkplace(villager.WorkplaceId) is Workplace job:
                OpenCard(new CardSubject(CardKind.Workplace, job.Id));
                break;
        }
    }

    private static readonly StyleBoxFlat PillFine = Pill(LightWorking);
    private static readonly StyleBoxFlat PillWanting = Pill(LightStopped);

    private static StyleBoxFlat Pill(Color colour)
    {
        var skin = new StyleBoxFlat
        {
            BgColor = colour with { A = 0.18f },
            BorderColor = colour with { A = 0.7f },
        };

        skin.SetBorderWidthAll(1);
        skin.SetCornerRadiusAll(9);
        skin.ContentMarginLeft = skin.ContentMarginRight = 8;
        skin.ContentMarginTop = skin.ContentMarginBottom = 1;
        return skin;
    }

    private static void SetNeed((PanelContainer Pill, Label Text) need, bool fine, string text, string why)
    {
        need.Text.Text = text;
        need.Text.Modulate = fine ? LightWorking : LightStopped;
        need.Pill.TooltipText = why;
        need.Pill.AddThemeStyleboxOverride("panel", fine ? PillFine : PillWanting);
    }

    /// <summary>A section's head: a small caps word and a rule (D431).</summary>
    private static HBoxContainer SectionHeader(string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(Muted(text));
        row.AddChild(new HSeparator { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter });
        return row;
    }

    private static readonly StyleBoxFlat BarBack = BarSkin(new Color(1, 1, 1, 0.10f));
    private static readonly StyleBoxFlat BarFill = BarSkin(LightWorking);

    private static StyleBoxFlat BarSkin(Color colour)
    {
        var skin = new StyleBoxFlat { BgColor = colour };
        skin.SetCornerRadiusAll(3);
        skin.SetContentMarginAll(0);
        return skin;
    }

    /// <summary>A thin bar in the working green (D432 — the theme's grey fill read as empty).</summary>
    private static ProgressBar Bar()
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 6),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        bar.AddThemeStyleboxOverride("background", BarBack);
        bar.AddThemeStyleboxOverride("fill", BarFill);
        return bar;
    }

    /// <summary>A section: its head and a column under it (D432's rhythm — 4 inside, 14 between).</summary>
    private static VBoxContainer Section(string title)
    {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 4);
        section.AddChild(SectionHeader(title));
        return section;
    }

    /// <summary>A control's text at the card's own size, not the theme's larger default (D432).</summary>
    private static T AtRowSize<T>(T control) where T : Control
    {
        control.AddThemeFontSizeOverride("font_size", RowSize);
        return control;
    }

    /// <summary>One skill row, made when a person first needs it and reused after — never rebuilt per frame.</summary>
    private static void AddSkillRow(VillagerParts p)
    {
        var row = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        row.AddThemeConstantOverride("separation", 1);
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 6);
        Label name = Wrapped(Body(string.Empty));
        Label years = Muted(string.Empty);
        years.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        line.AddChild(name);
        line.AddChild(years);
        row.AddChild(line);
        ProgressBar bar = Bar();
        row.AddChild(bar);
        p.SkillsSection.AddChild(row);
        p.Skills.Add((row, name, years, bar));
    }

    /// <summary>A person's sections, built once with the card and hidden until it shows a villager (D431, D432).</summary>
    private static VillagerParts BuildPersonParts()
    {
        var box = new VBoxContainer { Visible = false };
        box.AddThemeConstantOverride("separation", 14);

        VBoxContainer work = Section("WORK");
        Label job = Wrapped(Body(string.Empty));
        job.MouseFilter = MouseFilterEnum.Stop;
        work.AddChild(job);
        OptionButton keep = AtRowSize(new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FitToLongestItem = false });
        keep.GetPopup().AddThemeFontSizeOverride("font_size", RowSize);
        work.AddChild(keep);
        box.AddChild(work);

        VBoxContainer toolSection = Section("TOOL");
        toolSection.MouseFilter = MouseFilterEnum.Stop;
        var toolRow = new HBoxContainer();
        toolRow.AddThemeConstantOverride("separation", 8);
        ProgressBar toolBar = Bar();
        toolRow.AddChild(toolBar);
        Label toolUses = Body(string.Empty);
        toolRow.AddChild(toolUses);
        toolSection.AddChild(toolRow);
        box.AddChild(toolSection);

        VBoxContainer needsSection = Section("NEEDS");
        var needsRow = new HFlowContainer();
        needsRow.AddThemeConstantOverride("h_separation", 6);
        needsRow.AddThemeConstantOverride("v_separation", 4);
        var needs = new (PanelContainer, Label)[3];
        for (int i = 0; i < needs.Length; i++)
        {
            var pill = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
            Label text = Body(string.Empty);
            pill.AddChild(text);
            needsRow.AddChild(pill);
            needs[i] = (pill, text);
        }

        needsSection.AddChild(needsRow);
        box.AddChild(needsSection);

        VBoxContainer skillsSection = Section("SKILLS");
        skillsSection.AddThemeConstantOverride("separation", 6);
        box.AddChild(skillsSection);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 6);
        Button find = AtRowSize(new Button { Text = "Find", SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Centre the view on them" });
        Button home = AtRowSize(new Button { Text = "Home", SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Open their household's card" });
        Button work2 = AtRowSize(new Button { Text = "Workplace", SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Open their workplace's card" });
        footer.AddChild(find);
        footer.AddChild(home);
        footer.AddChild(work2);
        box.AddChild(footer);

        return new VillagerParts
        {
            Box = box,
            Keep = keep,
            Job = job,
            ToolSection = toolSection,
            ToolBar = toolBar,
            ToolUses = toolUses,
            Needs = needs,
            SkillsSection = skillsSection,
            Skills = new(),
            Find = find,
            Home = home,
            Work = work2,
        };
    }

    /// <summary>What a library says when you click it — its shelves, and what is on them (D396, a card since).</summary>
    /// <remarks>
    /// <b>⭐ THE SHELVES ARE THE WHOLE CARD, because they are the whole decision.</b> The player is
    /// choosing which techniques outlive the people who worked them out, and *"two of three shelves
    /// used"* is the sentence that makes the choice visible before it bites rather than afterwards.
    /// The shelf says who worked it out (Joe, 2026-08-29) — the name is on the record rather than
    /// looked up, because by the time anybody reads this shelf that person has usually been dead
    /// for decades, which is what the library is FOR.
    /// </remarks>
    private bool ShowLibrary(SimWorld world, Card card, Library library)
    {
        var records = new List<(string What, string FoundBy)>(library.Records.Count);
        for (int i = 0; i < library.Records.Count; i++)
        {
            LibraryRecord record = library.Records[i];
            TechniqueRow row = world.TechniquesCatalog[record.TechniqueId];
            records.Add((row.Name, ShelfLine(world, row, record)));
        }

        WriteLibraryCard(card, library.Name, library.Shelves, records, library.ExtentWidth, library.ExtentHeight, library.Facing.Raw);
        return true;
    }

    /// <summary>What a technique is worth, who worked it out and when — the shelf's second line (D397).</summary>
    /// <remarks>
    /// Joe: *"Tended patches — discovered by Amos in Year 58. +15% buff to X."* The benefit is the
    /// row's own `yield_bonus_percent` on the skill's trade — nothing new is typed for the card;
    /// the finder and the year are the record's (history, unhashed — D258).
    /// </remarks>
    private static string ShelfLine(SimWorld world, TechniqueRow row, LibraryRecord record)
    {
        string trade = row.Skill >= 0 && row.Skill < world.Config.Skills.Count ? world.Config.Skills[row.Skill].Name : "its trade";
        string worth = $"+{row.YieldBonusPercent} % to {trade}";
        return record.FoundBy.Length > 0 ? $"{worth} · {record.FoundBy}, Year {record.FoundInYear}" : worth;
    }

    /// <summary>The library card from plain facts — so the probe can pose a full one where none stands.</summary>
    private void WriteLibraryCard(Card card, string name, int shelves, IReadOnlyList<(string What, string FoundBy)> records, int wide, int deep, ushort facing)
    {
        Title(card, name, renamable: false);
        card.Numbers.Visible = true;
        card.Storage.Visible = false;
        card.WorkersRow.Visible = false;

        bool full = records.Count >= shelves;
        Status(
            card,
            working: !full,
            full ? "Full. The next technique anybody works out has nowhere to go, and will die with them unless another library stands."
                : records.Count == 0 ? "Nothing written yet. A master who has worked a trade for twenty years works something out, and it is recorded here."
                : $"Shelves: {records.Count} of {shelves} used — where the village writes things down.");

        Number(card, 0, $"{records.Count}/{shelves}", "shelves used");
        Number(card, 1, $"{records.Count}", records.Count == 1 ? "technique kept" : "techniques kept");
        Number(card, 2, $"{shelves - records.Count}", "free");

        // Two lines a shelf (D397): the technique, then what it is worth and who wrote it —
        // *"+10 % to foraging · Amos, Year 58"* — wrapped, never clipped, so the card holds 268.
        card.PeopleScroll.Visible = true;
        ClearThePeople(card);
        for (int i = 0; i < records.Count; i++)
        {
            var shelf = new VBoxContainer();
            shelf.AddThemeConstantOverride("separation", 0);
            Label what = Body(Capitalise(records[i].What));
            what.ClipText = true;
            what.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            Label worth = Wrapped(Muted(records[i].FoundBy));
            shelf.AddChild(what);
            shelf.AddChild(worth);
            card.People.AddChild(shelf);
        }

        // Two lines a shelf, so the window is twice the height a row of people gets, and it
        // still scrolls past four.
        card.PeopleScroll.CustomMinimumSize = new Vector2(0, RowSize * 2.9f * Mathf.Min(PeopleRowsShown, Mathf.Max(1, records.Count)));
        card.Portrait.Show(VillageMap.LibraryColour, wide * 0.8f, deep * 0.8f, facing, null);
        card.Caption.Text = "where the village writes things down";
        card.Caption.TooltipText = card.Caption.Text;
    }

    /// <summary>What the town hall says when you click it — <b>who it is for</b> (D396, a card since).</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THE FOUNDERS ARE THE WHOLE CARD, AND THAT IS SLICE 1's ENTIRE CLAIM</b>
    /// (`specs/town-hall.md §6`): <em>standing in the village, it says what it is and who it is
    /// for.</em> The collections, the charts and the knowledge roster are slices 2–4 and none of
    /// them is here — but the tribute is, because the tribute is the reason the building exists.
    /// </para>
    /// <para>
    /// <b>⛔ THE ORDERING OF THE SLICES IS A DEFENCE, NOT AN ACCIDENT.</b> `DESIGN.md §1`'s
    /// non-negotiable most at risk in this building is <em>people, not a spreadsheet</em> — charts
    /// and itemised collections are literally a spreadsheet. **Building the Founders panel first
    /// means the first thing anybody ever sees inside a town hall is four people.**
    /// </para>
    /// </remarks>
    private bool ShowTownHall(SimWorld world, Card card, TownHall hall)
    {
        var founders = new List<(string Name, string Life)>();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager founder = world.Villagers[i];
            if (!founder.Founder)
            {
                continue;
            }

            // ⚠️ A founder is dead by the time this building can stand — the hall's own trigger is
            // the last of them dying — so this reads their age at death, which `AgeYears` stops
            // advancing at. **Written as a life rather than as a row**: the register that keeps
            // this card from being a stat block is the same one D195's at-risk line uses.
            // Short numerals on the card (D397, Joe: *"inscrutable. word wrap and use less words"*);
            // the spelled-out register stays in the log, where it has the room.
            founders.Add((founder.FullName, $"{founder.AgeYears} · {founder.WintersSurvived} winters"));
        }

        int raised = (int)(hall.RaisedAtTick / (ulong)world.Config.TicksPerYear) + 1;
        WriteTownHallCard(card, hall.Name, founders, raised, hall.ExtentWidth, hall.ExtentHeight, hall.Facing.Raw);
        return true;
    }

    /// <summary>How many households draw at each well, counted once a tick — the card refreshes every frame.</summary>
    private readonly Dictionary<Well, int> _drawingAt = new();
    private ulong _drawingAtTick = ulong.MaxValue;

    /// <summary>The well card (D427): who walks here for water, and how often they have.</summary>
    private bool ShowWell(SimWorld world, Card card, Well well)
    {
        if (_drawingAtTick != world.Tick)
        {
            _drawingAtTick = world.Tick;
            _drawingAt.Clear();
            for (int i = 0; i < world.Households.Count; i++)
            {
                if (world.Households[i].HomeTile is GridPos home && world.NearestWell(home, out _) is Well theirs)
                {
                    _drawingAt[theirs] = _drawingAt.GetValueOrDefault(theirs) + 1;
                }
            }
        }

        WriteWellCard(card, well.Name, _drawingAt.GetValueOrDefault(well), well.Draws,
            world.Config.HomeWellReachTiles, well.ExtentWidth, well.ExtentHeight, well.Facing.Raw);
        return true;
    }

    /// <summary>The well card from plain facts — so the probe can pose one where none stands.</summary>
    private void WriteWellCard(Card card, string name, int households, int draws, int reach, int wide, int deep, ushort facing)
    {
        Title(card, name, renamable: false);
        card.Numbers.Visible = true;
        card.Storage.Visible = false;
        card.WorkersRow.Visible = false;
        card.PeopleScroll.Visible = false;

        Status(
            card,
            working: households > 0,
            households > 0
                ? $"Where {households} {(households == 1 ? "household draws its" : "households draw their")} water."
                : $"Nobody draws here yet — no house within {reach} tiles.");

        Number(card, 0, $"{households}", households == 1 ? "household" : "households");
        Number(card, 1, $"{draws:N0}", "trips made");
        Number(card, 2, $"{reach}", "tiles it reaches");
        card.Portrait.Show(VillageMap.WellColour, wide * 0.7f, deep * 0.7f, facing, null);
    }

    /// <summary>The town hall card from plain facts — so the probe can pose one where none stands.</summary>
    private void WriteTownHallCard(Card card, string name, IReadOnlyList<(string Name, string Life)> founders, int raisedInYear, int wide, int deep, ushort facing)
    {
        Title(card, name, renamable: false);
        card.Numbers.Visible = true;
        card.Storage.Visible = false;
        card.WorkersRow.Visible = false;

        Status(
            card,
            working: founders.Count > 0,
            founders.Count > 0 ? "Raised to the people who founded this village."
                : "Nobody's names are cut into the lintel, which should not be possible.");

        Number(card, 0, $"{founders.Count}", "founders");
        Number(card, 1, $"Year {raisedInYear}", "raised");
        Number(card, 2, "none yet", "records");

        card.PeopleScroll.Visible = true;
        ClearThePeople(card);
        for (int i = 0; i < founders.Count; i++)
        {
            // The name gives way, the numerals never do — a 40-letter name clips, *77 · 57 winters* reads.
            var row = new HBoxContainer();
            Label who = Body(founders[i].Name);
            who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            who.ClipText = true;
            who.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            Label life = Muted(founders[i].Life);
            row.AddChild(who);
            row.AddChild(life);
            card.People.AddChild(row);
        }

        card.PeopleScroll.CustomMinimumSize = new Vector2(0, RowSize * 1.45f * Mathf.Min(PeopleRowsShown, Mathf.Max(1, founders.Count)));
        card.Portrait.Show(VillageMap.TownHallColour, wide * 0.8f, deep * 0.8f, facing, null);

        // The caption's sentence became the button (D397): the records are what it opens.
        card.Caption.Text = string.Empty;
        card.Caption.TooltipText = string.Empty;
        card.Records.Visible = true;
    }

    /// <summary>
    /// <i>View records</i> — the hall's window is `town-hall.md` slices 2–4 and is not built;
    /// pressing the button says so (D397).
    /// </summary>
    private void OpenTheRecords() =>
        SayInTheLog("The village's records are not written up yet — the hall will hold its collections, its charts and what it knows.");

    private static void ClearThePeople(Card card)
    {
        while (card.People.GetChildCount() > 0)
        {
            Node child = card.People.GetChild(0);
            card.People.RemoveChild(child);
            child.QueueFree();
        }
    }

    // ---- the small writers ----

    private static void Title(Card card, string text, bool renamable)
    {
        card.Title.Text = text;
        card.Title.TooltipText = renamable ? "✎ renames it" : text;
    }

    private static void Status(Card card, bool working, string sentence)
    {
        card.Light.Color = working ? LightWorking : LightStopped;
        card.Status.Text = sentence;
        card.Status.Modulate = working ? Colors.White : LightStopped;
    }

    private static void Number(Card card, int slot, string value, string key)
    {
        card.Values[slot].Text = value;
        card.Keys[slot].Text = key;
    }

    private static string TradeWordFor(SimWorld world, Villager villager)
    {
        if (!villager.CanWork)
        {
            return villager.LifeStage == LifeStage.Child ? "child" : "elder";
        }

        return world.FindWorkplace(villager.WorkplaceId) is Workplace job
            ? ProfessionName(world, job.Kind)
            : "laborer";
    }

    private static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // ---------------------------------------------------------------
    //  The probe line
    // ---------------------------------------------------------------

    /// <summary>
    /// A person's card at its fullest holds 268, clips nothing, and has every section — <b>a probe
    /// line</b> (D431, Joe: <i>"copy off screen, no real hierarchy of information. nothing about tools"</i>).
    /// </summary>
    /// <remarks>
    /// Posed on the labels, as the building cards are, so the sim is not touched: a note three
    /// lines long, a worn tool, all three needs wanting, six skills with forty-letter names. Then
    /// every label on the card is asked whether it may trim — a person's card may not.
    /// </remarks>
    private string ThePersonsCardHoldsItsShape()
    {
        SimWorld world = _loop.World;
        var faults = new List<string>();
        Villager villager = world.Villagers.First(v => v.Alive);
        OpenCard(new CardSubject(CardKind.Villager, villager.Id));
        Card card = _selectedCard!;
        VillagerParts p = card.Person;

        if (!p.Box.Visible || card.Portrait.Visible || card.Numbers.Visible)
        {
            faults.Add("a person's card shows a building's parts, or not its own sections");
        }

        if (card.Subtitle.Text.Length == 0 || card.Status.Text.Length == 0)
        {
            faults.Add("a person's card has no subtitle or no status");
        }

        int trades = System.Enum.GetValues<JobKind>().Length;
        if (p.Keep.ItemCount != trades + 1)
        {
            faults.Add($"the Kept-on dropdown offers {p.Keep.ItemCount} items, not the village plus {trades} trades");
        }

        int kept = villager.PinnedTrade is JobKind pinned ? p.KeepTrades.IndexOf(pinned) + 1 : 0;
        if (p.Keep.Selected != kept)
        {
            faults.Add($"the dropdown reads item {p.Keep.Selected} for a villager kept on item {kept}");
        }

        // ⭐ THE WIDEST FULL NAME THE LISTS CAN MAKE FITS THE HEAD WITHOUT ITS ELLIPSIS (D467). The
        // head may trim a player's forty-letter rename; it must never trim a person's own name.
        // Widths add, so the widest prefix, suffix and surname make the widest name; asked the way
        // every other label on the card is — untrimmed, does the card widen?
        Font titleFont = card.Title.GetThemeFont("font");
        int titleSize = card.Title.GetThemeFontSize("font_size");
        float Measure(string text) => titleFont.GetStringSize(text, HorizontalAlignment.Left, -1, titleSize).X;
        string widestName = world.Config.FirstNamePrefixes.MaxBy(Measure)
            + world.Config.FirstNameSuffixes.MaxBy(Measure) + " " + world.Config.HouseholdNames.MaxBy(Measure);
        card.Title.Text = widestName;
        card.Title.ClipText = false;
        card.Title.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        ForceUpdateTransform();
        float namedWide = card.Panel.GetCombinedMinimumSize().X;
        card.Title.ClipText = true;
        card.Title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (namedWide > CardWidth + 1f)
        {
            faults.Add($"the widest full name, {widestName}, widens the card to {namedWide:F0} — the head would cut it");
        }

        // The fullest a person's card can be.
        string longest = "A status line that runs on for a good deal longer than any the sim writes, to see that it wraps inside the card rather than widening it.";
        card.Title.Text = new string('W', SimWorld.NameLengthLimit);
        card.Subtitle.Text = $"Woodcutter · 88 · {new string('H', SimWorld.NameLengthLimit)} household";
        card.Status.Text = longest;
        p.Job.Text = $"Woodcutter at {new string('W', SimWorld.NameLengthLimit)}";
        p.ToolSection.Visible = true;
        p.ToolBar.Visible = true;
        // ⚠️ The longest the row can read (D446): every kind's name and its full uses, or no tool.
        string longestTool = "No tool — works slower";
        foreach (Goods tool in world.GoodsCatalog.ToolsBestFirst)
        {
            GoodRow row = world.GoodsCatalog[tool];
            string text = $"{Capitalise(row.Name)} — {row.ToolUses} / {row.ToolUses}";
            longestTool = text.Length > longestTool.Length ? text : longestTool;
        }

        p.ToolUses.Text = longestTool;
        foreach ((PanelContainer, Label) need in p.Needs)
        {
            SetNeed(need, false, "No roof", longest);
        }

        p.SkillsSection.Visible = true;
        while (p.Skills.Count < 6)
        {
            AddSkillRow(p);
        }

        foreach ((VBoxContainer row, Label name, Label years, ProgressBar _) in p.Skills)
        {
            row.Visible = true;
            name.Text = new string('W', SimWorld.NameLengthLimit);
            years.Text = "seventy-seven years";
        }

        ForceUpdateTransform();
        float wide = card.Panel.GetCombinedMinimumSize().X;
        if (wide > CardWidth + 1f)
        {
            faults.Add($"a person's card at its fullest widens to {wide:F0}");
        }

        // ⛔ Nothing on a person's card trims (Joe: "copy off screen").
        var clipping = new List<string>();
        void Look(Node node)
        {
            if (node is Label label && label.IsVisibleInTree()
                && (label.ClipText || label.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming)
                && !ReferenceEquals(label, card.Title))
            {
                clipping.Add($"\"{(label.Text.Length > 24 ? label.Text[..24] + "…" : label.Text)}\"");
            }

            foreach (Node child in node.GetChildren())
            {
                Look(child);
            }
        }

        Look(card.Panel);
        if (clipping.Count > 0)
        {
            faults.Add($"{clipping.Count} label(s) on a person's card may trim: {string.Join(", ", clipping)}");
        }

        float tall = card.Panel.GetCombinedMinimumSize().Y;
        CloseCard(card);
        RefreshCards(world);

        return faults.Count == 0
            ? $"[widths] villager card: ✅ subtitle, a banner, WORK with a {p.Keep.ItemCount}-item dropdown, TOOL, NEEDS, six SKILLS and the jumps; at its fullest {wide:F0} wide and {tall:F0} tall; no label trims (the title's ellipsis is the head's); the widest full name, {widestName}, whole in the head ({namedWide:F0} wide)"
            : $"[widths] villager card: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// A card of every kind opens, holds its width, drags, pins and is replaced — <b>a probe line</b> (D376).
    /// </summary>
    private string TheCardsHoldTheirShape()
    {
        SimWorld world = _loop.World;
        var faults = new List<string>();

        StoreBuilding store = world.StoreBuildings[0];
        Workplace? place = world.Workplaces.Find(w => !w.IsSite);
        Household home = world.Households[0];
        Villager villager = world.Villagers[0];

        OpenCard(new CardSubject(CardKind.Store, store.Id));
        Card first = _selectedCard!;
        ForceUpdateTransform();
        float width = first.Panel.Size.X;
        if (Mathf.Abs(width - CardWidth) > 1f)
        {
            faults.Add($"a store card is {width:F0} wide, not {CardWidth:F0}");
        }

        // Only a building can be renamed (D380): a store's card offers ✎, a home's and a person's do not.
        if (!first.Edit.Visible)
        {
            faults.Add("a store card offers no ✎");
        }

        // Unpinned: the next click replaces it. Pinned: the next click opens a second.
        OpenCard(new CardSubject(CardKind.Household, home.Id));
        if (_cards.Count != 1 || _cards[0].Subject.Kind != CardKind.Household)
        {
            faults.Add($"a second click left {_cards.Count} cards open instead of replacing the unpinned one");
        }

        if (_cards[0].Edit.Visible)
        {
            faults.Add("a household card offers ✎");
        }

        _cards[0].Pin.ButtonPressed = true;
        _cards[0].Pinned = true;
        OpenCard(new CardSubject(CardKind.Villager, villager.Id));
        if (_cards.Count != 2)
        {
            faults.Add($"a click beside a pinned card left {_cards.Count} cards, not 2");
        }

        if (_selectedCard!.Edit.Visible)
        {
            faults.Add("a villager card offers ✎");
        }

        // ⭐ EVERY WORKPLACE'S NUMBERS MEAN SOMETHING (D404): no raw `int.MaxValue`, and no "no ground"
        // on a trade that never works painted ground. Asked of every catalogue row that employs
        // somebody, with the numbers a finished building of that row would carry (the sim's own
        // rule: no `local_store_cap` is an unbounded store) — the probe's cold start has none built.
        int tradesAsked = 0;
        BuildingsCatalog rows = world.BuildingsCatalog;
        for (int id = 0; id < rows.Count; id++)
        {
            if (rows.EmployedBy(id) is not JobKind trade)
            {
                continue;
            }

            BuildingRow row = rows[id];
            tradesAsked++;
            (string wallValue, string wallKey) = WallCell(row.LocalStoreCap > 0 ? row.LocalStoreCap : int.MaxValue, row.Seats ?? 1);
            string held = $"{wallValue} {wallKey}";
            (string _, string where) = WhereItWorks(trade, 0, row.GatheringRadius, row.HuntingRadius);
            if (held.Contains("2,147", StringComparison.Ordinal))
            {
                faults.Add($"a {row.Name}'s card would print int.MaxValue ({held})");
            }

            if (!SimWorld.KeepsWorkGround(trade) && where.Contains("ground", StringComparison.Ordinal))
            {
                faults.Add($"a {row.Name}'s card would talk about ground it never works ({where})");
            }
        }

        if (tradesAsked == 0)
        {
            faults.Add("no catalogue row employs anybody, so the card's numbers were asked of nothing");
        }

        // ⭐ A SITE SHOWS EVERY MATERIAL AND SAYS WHICH IS SHORT (D404): posed on the first kind whose
        // recipe wants more than one good, marked on the first tile that takes it.
        string siteShape = "no site siteToShow";
        foreach (BuildingKind kind in System.Enum.GetValues<BuildingKind>())
        {
            if (kind == BuildingKind.Home || BuildingRecipe.For(kind, world.Config).Materials.Count < 2)
            {
                continue;
            }

            Workplace? siteToShow = null;
            GridPos at = world.Map.FoundingSite;
            for (int r = 3; r < 20 && siteToShow is null; r++)
            {
                for (int dx = -r; dx <= r && siteToShow is null; dx++)
                {
                    var tile = new GridPos(at.X + dx, at.Y + r);
                    if (world.CanBuildAt(kind, tile).Allowed && world.Mark(kind, tile).Allowed)
                    {
                        siteToShow = world.Workplaces.Find(w => w.Construction is { IsFinished: false } c && c.Kind == kind);
                    }
                }
            }

            if (siteToShow is null)
            {
                continue;
            }

            OpenCard(new CardSubject(CardKind.Workplace, siteToShow.Id));
            Card siteCard = _selectedCard!;
            ForceUpdateTransform();
            siteShape = $"{siteCard.Values[0].Text} {siteCard.Keys[0].Text} · \"{siteCard.Status.Text}\" at {siteCard.Panel.Size.X:F0} wide";
            if (!siteCard.Keys[0].Text.Contains(" · ", StringComparison.Ordinal) || !siteCard.Status.Text.Contains("still wants", StringComparison.Ordinal))
            {
                faults.Add($"a site needing {siteToShow.Construction!.Recipe.Describe(world.GoodsCatalog)} reads {siteShape}");
            }

            // ⚠️ The number cells CLIP rather than widen, so a width check alone would pass a cell
            // that shows "0/40 · 0/…". Measured against the text itself.
            for (int i = 0; i < 1; i++)
            {
                Label value = siteCard.Values[i];
                Label key = siteCard.Keys[i];
                float valueWide = value.GetThemeFont("font").GetStringSize(value.Text, HorizontalAlignment.Left, -1, value.GetThemeFontSize("font_size")).X;
                float keyWide = key.GetThemeFont("font").GetStringSize(key.Text, HorizontalAlignment.Left, -1, key.GetThemeFontSize("font_size")).X;
                // The row is three ExpandFill cells, six apart, inside the panel's style margins —
                // laid out on the next frame, so derived here as the container will.
                float inner = siteCard.Panel.Size.X - siteCard.Panel.GetThemeStylebox("panel").GetMinimumSize().X;
                float cell = (inner - (2 * 6f)) / 3f;
                siteShape += $"; the cell {cell:F0} for {valueWide:F0} and {keyWide:F0}";
                if (valueWide > cell + 1f || keyWide > cell + 1f)
                {
                    faults.Add($"a site's materials clip in their cell ({valueWide:F0} and {keyWide:F0} in {cell:F0})");
                }

                // ⭐ A BUFFER SAYS WHAT IT HOLDS BY GOOD, AND IT FITS (D422): the lodge posed (meat and
                // leather: both named when they fit), then three goods from the catalogue's head with
                // the LAST the largest, so the order is the amount's and not the catalogue's — measured
                // in this same cell, the largest always named first.
                float Wide(Label label, string text) =>
                    label.GetThemeFont("font").GetStringSize(text, HorizontalAlignment.Left, -1, label.GetThemeFontSize("font_size")).X;
                bool Fits(string caption) => Wide(key, caption) <= cell + 1f;
                var lodge = new Stockpile(world.GoodsCatalog.Count);
                lodge.Receive(Goods.Meat, 10);
                lodge.Receive(Goods.Leather, 8);
                (string lodgeAmounts, string lodgeNames) = WhatItHolds(world, lodge, Fits);
                var three = new Stockpile(world.GoodsCatalog.Count);
                three.Receive((Goods)0, 3);
                three.Receive((Goods)1, 8);
                three.Receive((Goods)2, 10);
                (string threeAmounts, string threeNames) = WhatItHolds(world, three, Fits);
                string both = $"{world.GoodsCatalog.NameOf(Goods.Meat)} · {world.GoodsCatalog.NameOf(Goods.Leather)}";
                if (Fits(both) && lodgeNames != both)
                {
                    faults.Add($"a lodge of 10 meat and 8 leather reads \"{lodgeAmounts}\" over \"{lodgeNames}\" though \"{both}\" fits");
                }

                // ⛔ AND BOTH MUST FIT (D432). The line above passes whenever they do not, so a wider
                // margin on every card (twelve, briefly) turned the lodge back into "meat · others"
                // — D423's own complaint — with this line green. The cell is the budget; hold it.
                if (!Fits(both))
                {
                    faults.Add($"a lodge's \"{both}\" no longer fits its {cell:F0}px cell — it reads \"{lodgeNames}\"");
                }

                siteShape += $"; the lodge reads \"{lodgeAmounts}\" over \"{lodgeNames}\", three goods \"{threeAmounts}\" over \"{threeNames}\"";
                foreach ((string a, string n, Goods largest) in new[] { (lodgeAmounts, lodgeNames, Goods.Meat), (threeAmounts, threeNames, (Goods)2) })
                {
                    if (!n.StartsWith(world.GoodsCatalog.NameOf(largest), StringComparison.Ordinal))
                    {
                        faults.Add($"a buffer's largest good is not named first (\"{a}\" over \"{n}\")");
                    }

                    if (Wide(value, a) > cell + 1f || Wide(key, n) > cell + 1f)
                    {
                        faults.Add($"a buffer's goods clip in their cell (\"{a}\" over \"{n}\" in {cell:F0})");
                    }
                }
            }

            if (siteCard.Panel.Size.X > CardWidth + 1f)
            {
                faults.Add($"a site card widened to {siteCard.Panel.Size.X:F0}");
            }

            break;
        }

        if (place is not null)
        {
            OpenCard(new CardSubject(CardKind.Workplace, place.Id));
        }

        ForceUpdateTransform();
        float widest = 0f;
        float tallest = 0f;
        foreach (Card card in _cards)
        {
            widest = Mathf.Max(widest, card.Panel.Size.X);
            tallest = Mathf.Max(tallest, card.Panel.Size.Y);
            // A person's card has no numbers since D431 — its subtitle is the line that must read.
            string firstLine = card.Subject.Kind == CardKind.Villager ? card.Subtitle.Text : card.Values[0].Text;
            if (card.Status.Text.Length == 0 || firstLine.Length == 0)
            {
                faults.Add($"the {card.Subject.Kind} card has an empty status or first line");
            }
        }

        if (widest > CardWidth + 1f)
        {
            faults.Add($"a card widened to {widest:F0}");
        }

        // Every card's Settings fold open, its rows at their longest, and the width unmoved:
        // the rows wrap (HFlowContainer), so a control belongs to its card at any width.
        float widestOpen = 0f;
        foreach (Card card in _cards)
        {
            card.SettingsToggle.ButtonPressed = true;
            card.Settings.Visible = true;
            CardControls c = card.Controls;
            if (c.GroundLabel is not null) c.GroundLabel.Text = "Ground — 128 tiles, enough hands for 26:";
            if (c.GroundNote is not null) { c.GroundNote.Text = "The south-western farmhouse 2 is 128 tiles of field and 2 pairs of hands can sow 26 of them. The other 102 will lie fallow — put another farmer on, or paint a smaller field."; c.GroundNote.Visible = true; c.GroundRow!.Visible = true; }
            if (c.IdleLabel is not null) c.IdleLabel.Text = "Nothing to sow at the south-western farmhouse 2 — you asked the village to keep 2000 food and it has 1834.";
            if (c.QueueLabel is not null) { c.QueueLabel.Text = "3rd in the queue, after a granary and a stockpile:"; c.QueueRow!.Visible = true; }
            // ⚠️ The storage rows (D393) posed at their widest. Red-checked with a plain label for
            // the amount: ZERO — the name label expands to take what the amount gives up, so a row
            // cannot widen a 268 card whatever the amount reads. Kept as a pose, not as proof; the
            // `Amount()` cell is there so the rows line up, not to hold the width.
            foreach ((Goods _, HBoxContainer row, Label _, Label amount, Button take) in c.Storage)
            {
                row.Visible = true;
                amount.Text = "+12,345";
                take.ButtonPressed = true;
            }

            if (c.LimitRow is not null) c.LimitRow.Visible = true;

            // ⭐ The smithy's Forges dropdown (D446), with every kind a forge makes — and its budget
            // asserted (trap 139): the longest item must fit the card, since the button does not
            // widen to it (FitToLongestItem off) and would trim the kind's name instead.
            if (c.ForgeRow is not null)
            {
                c.ForgeRow.Visible = true;
                AddForgeItems(world, c);
                int forgeable = 0;
                foreach (Goods tool in world.GoodsCatalog.ToolsBestFirst)
                {
                    forgeable += world.GoodsCatalog[tool].ForgedFrom.Count > 0 ? 1 : 0;
                }

                if (c.Forge!.ItemCount != forgeable || forgeable < 2)
                {
                    faults.Add($"the smithy's dropdown offers {c.Forge.ItemCount} kinds of {forgeable} a forge makes");
                }

                float inner = CardWidth - card.Panel.GetThemeStylebox("panel").GetMinimumSize().X;
                for (int i = 0; i < c.Forge.ItemCount; i++)
                {
                    float wide = c.Forge.GetThemeFont("font").GetStringSize(c.Forge.GetItemText(i), HorizontalAlignment.Left, -1, c.Forge.GetThemeFontSize("font_size")).X
                        + c.Forge.GetThemeIcon("arrow").GetWidth() + 24f;
                    if (wide > inner)
                    {
                        faults.Add($"the smithy's dropdown item '{c.Forge.GetItemText(i)}' needs {wide:F0} of the card's {inner:F0}");
                    }
                }
            }
        }

        ForceUpdateTransform();
        foreach (Card card in _cards)
        {
            widestOpen = Mathf.Max(widestOpen, card.Panel.GetCombinedMinimumSize().X);
        }

        if (widestOpen > CardWidth + 1f)
        {
            faults.Add($"with its settings open a card's minimum is {widestOpen:F0}");
        }

        // A name the player typed at the limit, and a status three lines long: neither may
        // widen the card — the bound is the panel's minimum, not what it happens to hold.
        Card posed = _cards[^1];
        posed.Title.Text = new string('W', SimWorld.NameLengthLimit);
        posed.Status.Text = "A status line that runs on for a good deal longer than any the sim writes, to see that it wraps inside the card rather than widening it.";
        ForceUpdateTransform();
        float posedWidth = posed.Panel.GetCombinedMinimumSize().X;
        if (posedWidth > CardWidth + 1f)
        {
            faults.Add($"a {SimWorld.NameLengthLimit}-letter name or a long status widens a card's minimum to {posedWidth:F0}");
        }

        // ⭐ A LIBRARY AND THE HALL HAVE CARDS (D396). Neither stands at the founding, so the
        // writers are posed with the facts a full one would carry — five shelves, every technique
        // name at the rename limit, four founders — and the width read back. Put back by the
        // refresh at the end, which rewrites the card for its real subject.
        var fullShelves = new List<(string What, string FoundBy)>();
        for (int i = 0; i < 5; i++)
        {
            fullShelves.Add((new string('W', SimWorld.NameLengthLimit), $"+15 % to woodcutting · {new string('M', SimWorld.NameLengthLimit)}, Year 158"));
        }

        WriteLibraryCard(posed, new string('L', SimWorld.NameLengthLimit), 5, fullShelves, 1, 1, 0);
        ForceUpdateTransform();
        float libraryWidth = posed.Panel.GetCombinedMinimumSize().X;
        if (libraryWidth > CardWidth + 1f)
        {
            faults.Add($"a full library with 40-letter techniques widens a card's minimum to {libraryWidth:F0}");
        }

        if (posed.Values[0].Text != "5/5" || posed.People.GetChildCount() != 5)
        {
            faults.Add($"a full library card reads {posed.Values[0].Text} with {posed.People.GetChildCount()} rows, not 5/5 with 5");
        }

        var founders = new List<(string Name, string Life)>();
        for (int i = 0; i < 4; i++)
        {
            founders.Add((new string('F', SimWorld.NameLengthLimit), "73 · 71 winters"));
        }

        WriteTownHallCard(posed, new string('H', SimWorld.NameLengthLimit), founders, 58, 3, 2, 0);
        ForceUpdateTransform();
        float hallWidth = posed.Panel.GetCombinedMinimumSize().X;
        if (hallWidth > CardWidth + 1f)
        {
            faults.Add($"a hall with four 40-letter founders widens a card's minimum to {hallWidth:F0}");
        }

        if (posed.Values[0].Text != "4" || posed.People.GetChildCount() != 4)
        {
            faults.Add($"a hall card reads {posed.Values[0].Text} founders with {posed.People.GetChildCount()} rows, not 4 with 4");
        }

        // The numerals beside a 40-letter name are never the part that clips (D397), and the
        // hall's card offers View records.
        foreach (Node row in posed.People.GetChildren())
        {
            if (row.GetChildCount() == 2 && row.GetChild(1) is Label numerals && numerals.Size.X < numerals.GetCombinedMinimumSize().X - 1f)
            {
                faults.Add($"a founder's numerals are squeezed to {numerals.Size.X:F0} of {numerals.GetCombinedMinimumSize().X:F0}");
            }
        }

        if (!posed.Records.Visible)
        {
            faults.Add("the hall's card offers no View records");
        }

        // ⭐ AND A WELL (D427), posed the same way — a 40-letter name, a crowd of households and a
        // lifetime of trips — and then the empty one, whose sentence is the longest.
        WriteWellCard(posed, new string('W', SimWorld.NameLengthLimit), 1234, 12_345_678, world.Config.HomeWellReachTiles, 1, 1, 0);
        ForceUpdateTransform();
        float wellWidth = posed.Panel.GetCombinedMinimumSize().X;
        if (wellWidth > CardWidth + 1f)
        {
            faults.Add($"a busy well with a 40-letter name widens a card's minimum to {wellWidth:F0}");
        }

        if (posed.Values[0].Text != "1234" || posed.Values[1].Text != "12,345,678")
        {
            faults.Add($"a well card reads {posed.Values[0].Text} households and {posed.Values[1].Text} trips, not 1234 and 12,345,678");
        }

        WriteWellCard(posed, "well 1", 0, 0, world.Config.HomeWellReachTiles, 1, 1, 0);
        ForceUpdateTransform();
        float emptyWell = posed.Panel.GetCombinedMinimumSize().X;
        if (emptyWell > CardWidth + 1f)
        {
            faults.Add($"a well nobody draws at widens a card's minimum to {emptyWell:F0}");
        }

        // Drag: move the selected card's panel and read it back.
        Card dragged = _cards[^1];
        Vector2 before = dragged.Panel.Position;
        dragged.Panel.Position = before + new Vector2(40f, 30f);
        if (dragged.Panel.Position != before + new Vector2(40f, 30f))
        {
            faults.Add("a card did not stay where it was put");
        }

        // One structure, one panel (D377): with a store's card open the docked "What's here" is
        // hidden; on bare ground, with no card, it shows.
        OnBuildingClicked(store.Tile);
        RefreshInspector(world);
        bool twoPanels = _whatsHerePanel.Visible;
        GridPos bare = world.Map.FoundingSite;
        for (int dx = 0; dx < 12; dx++)
        {
            var candidate = new GridPos(world.Map.FoundingSite.X + dx, world.Map.FoundingSite.Y + 5);
            if (world.StoreAt(candidate) is null && world.WorkplaceCovering(candidate) is null && world.HouseholdAt(candidate) is null)
            {
                bare = candidate;
                break;
            }
        }

        _selectedTile = bare;
        _selectedVillagerId = 0;
        RefreshInspector(world);
        bool groundReads = _whatsHerePanel.Visible;
        if (twoPanels)
        {
            faults.Add("a store showed two panels — its card and the docked one");
        }

        if (!groundReads)
        {
            faults.Add("bare ground, with no card, read nowhere");
        }

        int open = _cards.Count;
        while (_cards.Count > 0)
        {
            CloseCard(_cards[0]);
        }

        return faults.Count == 0
            ? $"[widths] cards: ✅ a card of each of the four kinds opened and a full library, the hall and a well posed, every workplace's numbers honest, a site {siteShape}, the smithy's Forges dropdown offering every kind a forge makes and fitting, all {CardWidth:F0} wide, the tallest {tallest:F0}px closed and {widestOpen:F0} wide with every setting open; an unpinned card is replaced, a pinned one stays ({open} open at the end); one panel per structure"
            : $"[widths] cards: ⛔ {string.Join("; ", faults)}";
    }
}

/// <summary>
/// A building's own map drawing, scaled up to fill a card's picture (D376, Joe: *"add building's
/// real map drawing scaled up"*): the footprint quad in the map's colour, turned as it is turned,
/// with its ring when the map would draw one.
/// </summary>
public sealed partial class BuildingPortrait : Control
{
    private Color _fill;
    private float _wide = 0.8f;
    private float _tall = 0.8f;
    private ushort _facing;
    private Color? _ring;

    public void Show(Color fill, float wideTiles, float tallTiles, ushort facing, Color? ring)
    {
        _fill = fill;
        _wide = wideTiles;
        _tall = tallTiles;
        _facing = facing;
        _ring = ring;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 centre = Size / 2f;
        float longest = Mathf.Max(_wide, _tall) + 0.6f;
        float pixelsPerTile = Mathf.Min(Size.X, Size.Y) / longest;

        Vector2[] quad = VillageMap.FootprintQuadAt(centre, _wide, _tall, _facing, pixelsPerTile);
        DrawColoredPolygon(quad, _fill with { A = 0.85f });
        for (int i = 0; i < 4; i++)
        {
            DrawLine(quad[i], quad[(i + 1) % 4], _fill, 2f);
        }

        if (_ring is Color ring)
        {
            DrawArc(centre, pixelsPerTile * 0.8f * 0.85f, 0f, Mathf.Tau, 32, ring, 2f);
        }
    }
}
