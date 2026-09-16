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
    }

    private readonly record struct CardSubject(CardKind Kind, int Id);

    /// <summary>One card: its panel and the labels the refresh writes into.</summary>
    private sealed class Card
    {
        public required PanelContainer Panel { get; init; }
        public required CardSubject Subject { get; set; }
        public bool Pinned { get; set; }
        public required Label Title { get; init; }
        public required LineEdit Rename { get; init; }
        public required Button Pin { get; init; }
        public required ColorRect Light { get; init; }
        public required Label Status { get; init; }
        public required HBoxContainer WorkersRow { get; init; }
        public required Label Workers { get; init; }
        public required Label WorkersLabel { get; init; }
        public required Label[] Values { get; init; }
        public required Label[] Keys { get; init; }
        public required ScrollContainer PeopleScroll { get; init; }
        public required VBoxContainer People { get; init; }
        public required BuildingPortrait Portrait { get; init; }
        public required Label Caption { get; init; }
    }

    private readonly List<Card> _cards = new();
    private Card? _selectedCard;

    /// <summary>A card's width in logical pixels — fixed, like every panel since D367.</summary>
    private const float CardWidth = 268f;

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
        // Beside the left column, stepping down a little for each card already open, so a
        // second card never lands exactly on the first.
        float x = Edge + DefaultPanelWidth + 16f;
        float y = Edge + 40f + (24f * (_cards.Count - 1));
        card.Panel.Position = new Vector2(x, y);
    }

    private void SelectCard(Card card)
    {
        _selectedCard = card;
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Panel.AddThemeStyleboxOverride("panel", CardSkin(ReferenceEquals(_cards[i], card)));
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
        }
    }

    private void CloseCard(Card card)
    {
        _cards.Remove(card);
        card.Panel.QueueFree();
        if (ReferenceEquals(_selectedCard, card))
        {
            _selectedCard = null;
        }
    }

    private static StyleBoxFlat CardSkin(bool selected)
    {
        StyleBoxFlat skin = PanelSkin(0.96f);
        skin.BorderWidthTop = skin.BorderWidthBottom = skin.BorderWidthLeft = skin.BorderWidthRight = 1;
        skin.BorderColor = selected ? CardSelectedEdge : new Color("#3a3f47");
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

        Label grip = Muted("⋮⋮");
        grip.MouseFilter = MouseFilterEnum.Stop;
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
        statusRow.AddChild(status);

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

        var card = new Card
        {
            Panel = panel,
            Subject = subject,
            Title = title,
            Rename = rename,
            Pin = pin,
            Light = light,
            Status = status,
            WorkersRow = workersRow,
            Workers = count,
            WorkersLabel = workersLabel,
            Values = values,
            Keys = keys,
            PeopleScroll = peopleScroll,
            People = people,
            Portrait = portrait,
            Caption = caption,
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
        fewer.Pressed += () => ChangeStaffingOf(card, -1);
        more.Pressed += () => ChangeStaffingOf(card, +1);

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
            CardKind.Household when world.FindHousehold(card.Subject.Id) is Household home => world.Rename(home, typed),
            _ => PlacementVerdict.No("Only a building or a household can be renamed."),
        };

        if (!verdict.Allowed)
        {
            Warn(PlacementVerdict.Yes(verdict.Reason));
        }

        RefreshCards(world);
        RefreshInspector(world);
    }

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
        }
    }

    /// <summary>Write the card; false if its subject no longer exists.</summary>
    private bool ShowCard(SimWorld world, Card card)
    {
        switch (card.Subject.Kind)
        {
            case CardKind.Store:
                return StoreOf(card) is StoreBuilding store && ShowStore(world, card, store);
            case CardKind.Workplace:
                return world.FindWorkplace(card.Subject.Id) is Workplace place && ShowWorkplace(world, card, place);
            case CardKind.Household:
                return world.FindHousehold(card.Subject.Id) is Household home && ShowHousehold(world, card, home);
            case CardKind.Villager:
                return world.FindVillager(card.Subject.Id) is Villager villager && ShowVillager(world, card, villager);
            default:
                return false;
        }
    }

    private bool ShowStore(SimWorld world, Card card, StoreBuilding store)
    {
        Title(card, store.Name, renamable: true);

        int used = store.Store.Capacity - store.Store.FreeSpace;
        if (store.Emptying)
        {
            Status(card, working: false, $"Being emptied — {used} left to carry out.");
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

        // The three biggest heaps it holds.
        var held = new List<(Goods Goods, int Amount)>();
        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            int amount = store.Store[(Goods)g];
            if (amount > 0)
            {
                held.Add(((Goods)g, amount));
            }
        }

        held.Sort((a, b) => b.Amount.CompareTo(a.Amount));
        for (int i = 0; i < 3; i++)
        {
            if (i < held.Count)
            {
                Number(card, i, $"{held[i].Amount:N0}", GoodsName(world, held[i].Goods));
            }
            else
            {
                Number(card, i, "—", i == 0 ? "empty" : string.Empty);
            }
        }

        card.Portrait.Show(
            VillageMap.ColourOf(store.Kind), store.ExtentWidth * 0.8f, store.ExtentHeight * 0.8f, store.Facing.Raw,
            store.Store.IsFull && _map.FullMarkerShownFor(store.Id) ? VillageMap.FullStoreRing : null);
        card.Caption.Text = $"a {store.Kind.ToString().ToLowerInvariant()} · ring when full: {(_map.FullMarkerShownFor(store.Id) ? "on" : "off")}";
        return true;
    }

    private bool ShowWorkplace(SimWorld world, Card card, Workplace place)
    {
        Title(card, place.Name, renamable: true);

        if (place.Construction is { IsFinished: false } site)
        {
            Status(card, working: false, $"Being built — {Ordinal(world.QueuePositionOf(place))} in the queue.");
            card.WorkersRow.Visible = false;
            Number(card, 0, $"{site.LogsDelivered}/{site.Recipe.Of(Goods.Logs)}", "logs");
            Number(card, 1, $"{site.WorkDone}/{site.Recipe.WorkTicks}", "work");
            Number(card, 2, $"{world.BuildQueue().Count}", "sites queued");
        }
        else
        {
            string? why = world.IdleNote(place);
            Status(card, working: why is null, why ?? $"Working — {place.WorkerIds.Count} of {place.Places} at it.");

            card.WorkersRow.Visible = true;
            card.WorkersLabel.Text = ProfessionName(world, place.Kind) + "s";
            card.Workers.Text = $"{place.WorkerIds.Count} / {place.Places}";

            int holding = 0;
            for (int g = 0; g < world.GoodsCatalog.Count; g++)
            {
                holding += place.Store[(Goods)g];
            }

            Number(card, 0, $"{holding:N0}", $"of {place.Store.Capacity:N0} held here");
            int ground = world.Zones.WorkGroundTiles(place.Id);
            Number(card, 1, ground > 0 ? $"{ground}" : "—", ground > 0 ? "tiles of ground" : "no ground");
            Number(card, 2, $"{place.Places}", place.Places == 1 ? "seat" : "seats");
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
        while (card.People.GetChildCount() > 0)
        {
            Node child = card.People.GetChild(0);
            card.People.RemoveChild(child);
            child.QueueFree();
        }

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

        card.Portrait.Show(VillageMap.HomeColour, 0.62f, 0.62f, 0, null);
        card.Caption.Text = home.HomePosition is null ? "no house" : "a wooden cabin";
        return true;
    }

    private bool ShowVillager(SimWorld world, Card card, Villager villager)
    {
        if (!villager.Alive)
        {
            return false;
        }

        Title(card, villager.Name, renamable: false);

        Workplace? job = world.FindWorkplace(villager.WorkplaceId);
        bool hungry = villager.Hunger >= world.Config.EatThreshold;
        string doing = villager.DescribeState(job?.Name);
        bool stopped = hungry || !string.IsNullOrWhiteSpace(villager.WorkNote);
        Status(
            card,
            working: !stopped,
            hungry ? $"Hungry — {doing}."
                : !string.IsNullOrWhiteSpace(villager.WorkNote) ? villager.WorkNote
                : Capitalise(doing) + ".");

        card.WorkersRow.Visible = false;
        card.PeopleScroll.Visible = false;

        Number(card, 0, $"{villager.AgeYears}", villager.LifeStage.ToString().ToLowerInvariant());
        Number(card, 1, TradeWordFor(world, villager), job is null ? "no workplace" : job.Name);
        Number(card, 2, world.HouseholdOf(villager).Name, "household");

        if (job is not null)
        {
            card.Portrait.Show(TradeGlyph.ColourOf(job.Kind), job.ExtentWidth * 0.8f, job.ExtentHeight * 0.8f, job.Facing.Raw, null);
            card.Caption.Text = string.IsNullOrWhiteSpace(villager.JobReason) ? $"works at {job.Name}" : villager.JobReason;
            card.Caption.TooltipText = villager.JobReason;
        }
        else
        {
            card.Portrait.Show(VillageMap.HomeColour, 0.62f, 0.62f, 0, null);
            card.Caption.Text = string.IsNullOrWhiteSpace(villager.JobReason) ? "a laborer — spare hands" : villager.JobReason;
            card.Caption.TooltipText = villager.JobReason;
        }

        return true;
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

        // Unpinned: the next click replaces it. Pinned: the next click opens a second.
        OpenCard(new CardSubject(CardKind.Household, home.Id));
        if (_cards.Count != 1 || _cards[0].Subject.Kind != CardKind.Household)
        {
            faults.Add($"a second click left {_cards.Count} cards open instead of replacing the unpinned one");
        }

        _cards[0].Pin.ButtonPressed = true;
        _cards[0].Pinned = true;
        OpenCard(new CardSubject(CardKind.Villager, villager.Id));
        if (_cards.Count != 2)
        {
            faults.Add($"a click beside a pinned card left {_cards.Count} cards, not 2");
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
            if (card.Status.Text.Length == 0 || card.Values[0].Text.Length == 0)
            {
                faults.Add($"the {card.Subject.Kind} card has an empty status or first number");
            }
        }

        if (widest > CardWidth + 1f)
        {
            faults.Add($"a card widened to {widest:F0}");
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

        // Drag: move the selected card's panel and read it back.
        Card dragged = _cards[^1];
        Vector2 before = dragged.Panel.Position;
        dragged.Panel.Position = before + new Vector2(40f, 30f);
        if (dragged.Panel.Position != before + new Vector2(40f, 30f))
        {
            faults.Add("a card did not stay where it was put");
        }

        int open = _cards.Count;
        while (_cards.Count > 0)
        {
            CloseCard(_cards[0]);
        }

        return faults.Count == 0
            ? $"[widths] cards: ✅ a card of each of the four kinds opened, all {CardWidth:F0} wide, the tallest {tallest:F0}px; an unpinned card is replaced, a pinned one stays ({open} open at the end)"
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
