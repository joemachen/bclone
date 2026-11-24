using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐ THE FOOD FIGURE AND ITS RUNWAY (<c>hud.md §6.1</c>, D878; Joe, A209: <i>"a total of all food in storage (excluding food
/// in larders)"</i>). The number is <see cref="SimWorld.FoodTheVillageHolds"/> — what the birth gate, the quota and the food
/// limit read, so the strip and the sim cannot disagree; larders are excluded by construction. The runway is that number
/// over the village's year of eating, from the headcount — it moves only when food or a person does, and it is "if nothing
/// more comes in", never a forecast (§1.1). It turns the warnings' colour when the stores won't see the village to next
/// spring as things stand (call 3, A215).
/// </remarks>
public partial class Main
{
    private Label _foodFigure = null!;
    private Label _runwayLabel = null!;

    /// <summary>The food figure's cell, first in the strip's goods: an icon, the number, <i>food</i>, the runway.</summary>
    private void AddTheFoodFigure(HBoxContainer goods)
    {
        var cell = new HBoxContainer();
        cell.AddThemeConstantOverride("separation", 5);
        cell.AddChild(UiIcons.Or("goods", "bread", () => WorldChip(ChipColour(Goods.Bread)), UiIcons.BarSide));
        _foodFigure = Amount(BarAmountWidth);
        _foodFigure.SizeFlagsHorizontal = SizeFlags.Fill;
        _foodFigure.MouseFilter = MouseFilterEnum.Pass;
        cell.AddChild(_foodFigure);
        cell.AddChild(Muted("food"));
        _runwayLabel = Muted(string.Empty);
        _runwayLabel.MouseFilter = MouseFilterEnum.Pass;
        cell.AddChild(_runwayLabel);
        goods.AddChild(cell);
    }

    /// <summary>The seasons the food lasts if nothing more comes in — ⌊food × 4 ÷ a year's eating⌋; null with nobody to eat.</summary>
    internal static int? SeasonsTheFoodLasts(int food, int adults, int children, SimConfig config)
    {
        long year = ((long)adults * VillageEconomy.AdultFoodPerYear(config)) + ((long)children * VillageEconomy.ChildFoodPerYear(config));
        return year <= 0 ? null : (int)System.Math.Min(int.MaxValue, food * 4L / year);
    }

    /// <summary>The runway in words: seasons up to eight, then years, then <i>over 10 years</i> (§6.1).</summary>
    internal static string RunwayWords(int food, int seasons, int adults, int children, SimConfig config)
    {
        if (seasons <= 8)
        {
            return seasons switch { 0 => "lasts under a season", 1 => "lasts 1 season", _ => $"lasts {seasons} seasons" };
        }

        long year = ((long)adults * VillageEconomy.AdultFoodPerYear(config)) + ((long)children * VillageEconomy.ChildFoodPerYear(config));
        long years = food / year;
        return years > 10 ? "lasts over 10 years" : $"lasts {years} years";
    }

    /// <summary>The seasons from this one to next spring: four in spring, one in winter (call 3).</summary>
    private static int SeasonsToNextSpring(Season season) => season switch
    {
        Season.Spring => 4,
        Season.Summer => 3,
        Season.Fall => 2,
        _ => 1,
    };

    private void RefreshTheFoodFigure(SimWorld world, int adults, int children)
    {
        int food = world.FoodTheVillageHolds();
        _foodFigure.Text = food.Grouped();
        int? seasons = SeasonsTheFoodLasts(food, adults, children, world.Config);
        _runwayLabel.Text = seasons is int s ? "· " + RunwayWords(food, s, adults, children, world.Config) : string.Empty;

        bool warns = seasons is int left && left < SeasonsToNextSpring(world.Clock.Season);
        if (warns)
        {
            _foodFigure.AddThemeColorOverride("font_color", LightStopped);
        }
        else
        {
            _foodFigure.RemoveThemeColorOverride("font_color");
        }

        string tip = TheFoodFigureSays(world, food, seasons, adults, children, warns);
        _foodFigure.TooltipText = tip;
        _runwayLabel.TooltipText = tip;
    }

    /// <summary>The figure's tooltip: what it counts, where it is, each kind, the larders named and not counted, the sum.</summary>
    private static string TheFoodFigureSays(SimWorld world, int food, int? seasons, int adults, int children, bool warns)
    {
        var lines = new List<string>
        {
            $"{food.Grouped()} food the village holds — in the cart, granaries, warehouses, piles and markets, and waiting in the huts. Not the larders.",
        };

        int shelves = world.FoodInGranaries();
        int huts = world.FoodWaitingInHuts();
        if (shelves > 0 && huts > 0)
        {
            lines.Add($"On the shelves {shelves.Grouped()} · waiting in the huts {huts.Grouped()}");
        }

        IReadOnlyList<Goods> edible = world.GoodsCatalog.EdibleGoods;
        var kinds = new List<string>();
        for (int i = 0; i < edible.Count; i++)
        {
            int held = world.InStores(edible[i]);
            for (int w = 0; w < world.Workplaces.Count; w++)
            {
                held += world.Workplaces[w].Store[edible[i]];
            }

            if (held > 0)
            {
                kinds.Add($"{world.GoodsCatalog.NameOf(edible[i])} {held.Grouped()}");
            }
        }

        if (kinds.Count > 0)
        {
            lines.Add(string.Join(" · ", kinds));
        }

        lines.Add($"In the larders {world.FoodInLarders().Grouped()} — the households' own, not counted.");
        if (seasons is int s)
        {
            int year = (adults * VillageEconomy.AdultFoodPerYear(world.Config)) + (children * VillageEconomy.ChildFoodPerYear(world.Config));
            lines.Add($"{adults} {(adults == 1 ? "adult eats" : "adults eat")} and {children} {(children == 1 ? "child eats" : "children eat")} {year.Grouped()} a year: "
                + $"it {RunwayWords(food, s, adults, children, world.Config)} if nothing more comes in — no harvest, no foraging."
                + (warns ? " It won't see the village through the winter." : string.Empty));
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// ⭐ <c>food:</c> and <c>runway:</c> (<c>hud.md §6.1</c>, step 5) — the figure is <c>FoodTheVillageHolds()</c>, and 500
    /// food posed in a larder moves neither it nor the runway; the runway reads 7 seasons at the founding (1,200 × 4 ÷ 4 ×
    /// 168), computed here from the config and the headcount, not from the label's own arithmetic.
    /// </summary>
    private string TheFoodFigureIsTheVillages()
    {
        SimWorld world = _loop.World;
        Refresh();
        var faults = new List<string>();
        string figure = _foodFigure.Text;
        string runway = _runwayLabel.Text;
        if (figure != world.FoodTheVillageHolds().Grouped())
        {
            faults.Add($"the figure reads {figure}, the sim holds {world.FoodTheVillageHolds().Grouped()}");
        }

        // Call 3: the warnings' colour exactly when the stores won't see the village to next spring as things stand.
        int adults = world.Villagers.Count(v => v.Alive && v.LifeStage != LifeStage.Child);
        int children = world.Villagers.Count(v => v.Alive && v.LifeStage == LifeStage.Child);
        bool shouldWarn = SeasonsTheFoodLasts(world.FoodTheVillageHolds(), adults, children, world.Config) is int left
            && left < SeasonsToNextSpring(world.Clock.Season);
        if (_foodFigure.HasThemeColorOverride("font_color") != shouldWarn)
        {
            faults.Add($"the figure {(shouldWarn ? "does not warn" : "warns")} with {_runwayLabel.Text} in {world.Clock.Season}");
        }

        Household? home = world.Households.Find(h => h.HasHome) ?? (world.Households.Count > 0 ? world.Households[0] : null);
        if (home is null)
        {
            faults.Add("no household to pose a larder in");
        }
        else
        {
            home.Stockpile.Add(Goods.Produce, 500);
            Refresh();
            bool unmoved = _foodFigure.Text == figure && _runwayLabel.Text == runway;
            home.Stockpile.TryTake(Goods.Produce, 500);
            Refresh();
            if (!unmoved)
            {
                faults.Add($"500 food in a larder moved the figure {figure} → {_foodFigure.Text} or the runway");
            }
        }

        return faults.Count == 0
            ? $"[widths] food: ✅ the figure is FoodTheVillageHolds ({figure}); 500 posed in a larder moved neither it nor the runway"
            : $"[widths] food: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// <c>runway:</c> — the strip's runway against the sum done here from the config and the living (an adult or elder eats an
    /// adult's year, a child a child's): at the founding 1,200 × 4 ÷ (4 × 168) = 7 seasons, the spec's own number.
    /// </summary>
    private string TheRunwayIsTheSum()
    {
        SimWorld world = _loop.World;
        Refresh();
        int adults = world.Villagers.Count(v => v.Alive && v.LifeStage != LifeStage.Child);
        int children = world.Villagers.Count(v => v.Alive && v.LifeStage == LifeStage.Child);
        long year = ((long)adults * VillageEconomy.AdultFoodPerYear(world.Config)) + ((long)children * VillageEconomy.ChildFoodPerYear(world.Config));
        int food = world.FoodTheVillageHolds();
        long seasons = food * 4L / year;
        bool founding = world.Tick < (ulong)world.Config.TicksPerSeason;
        string want = seasons <= 8 ? $"· lasts {seasons} seasons" : _runwayLabel.Text;
        return _runwayLabel.Text == want && (!founding || seasons == 7)
            ? $"[widths] runway: ✅ {food.Grouped()} food × 4 ÷ ({adults} × {VillageEconomy.AdultFoodPerYear(world.Config)} + {children} × {VillageEconomy.ChildFoodPerYear(world.Config)}) = {seasons} seasons, and the strip says \"{_runwayLabel.Text}\""
            : $"[widths] runway: ⛔ the strip says \"{_runwayLabel.Text}\", the sum {seasons} seasons{(founding ? " (the spec: 7 at the founding)" : string.Empty)}";
    }
}
