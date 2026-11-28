using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>The probe's <c>building yards:</c> line (A86, `building-yards.md §7`).</remarks>
public partial class VillageMap
{
    /// <summary>
    /// ⭐ Every expectation named here from the spec — which part is stock, which shows while working, which always stands, the
    /// 3-pixel floor, the fences by tier — never read back from the code's own tables (art traps 17, 25).
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): the look of the things, the softness of the ground are the renders'.</remarks>
    internal string TheBuildingYardsHold()
    {
        try
        {
            var faults = new List<string>();
            const float ppt = 40f;
            var centre = new Vector2(500f, 500f);
            Color kind = new("#9a6b3f");

            List<RoofPiece> Draw(RoofLook look, float wide, float deep, ushort facing, YardState s, Door? door = null, Vector2[]? home = null) =>
                RoofDrawing(look, centre, wide, deep, facing, kind, ppt, door, centre, 5, false, s, home);
            Vector2 Local(Vector2 at, ushort facing)
            {
                float a = facing * Mathf.Tau / 65536f;
                Vector2 d = (at - centre) / ppt;
                return new Vector2((d.X * Mathf.Cos(a)) + (d.Y * Mathf.Sin(a)), (-d.X * Mathf.Sin(a)) + (d.Y * Mathf.Cos(a)));
            }

            static bool Has(List<RoofPiece> pieces, string part) => pieces.Any(p => p.Part == part);

            // §2's table: a stock thing with its stock, none without it. (kind, extent, part, which level carries it)
            var full = new YardState(Stock: 3, Stock2: 3, Working: true, Water: 2);
            foreach ((string what, RoofLook look, float w, float d, string part, YardState without) in new[]
            {
                ("the fishing hut's fish", RoofOf(JobKind.Fisher), 0.8f, 0.8f, "fish rack hung", full with { Stock = 0 }),
                ("the hunter's meat", RoofOf(JobKind.Hunter), 1.6f, 0.8f, "smoking rack hung", full with { Stock = 0 }),
                ("the hunter's hide", RoofOf(JobKind.Hunter), 1.6f, 0.8f, "hide", full with { Stock2 = 0 }),
                ("the farmhouse's grain", RoofOf(JobKind.Farmer), 1.6f, 1.6f, "grain", full with { Stock = 0 }),
                ("the granary's sacks", RoofOf(StoreKind.Granary)!.Value, 1.6f, 1.6f, "grain", full with { Stock = 0 }),
                ("the warehouse's goods", RoofOf(StoreKind.Warehouse)!.Value, 1.6f, 1.6f, "goods", full with { Stock = 0 }),
                ("the warehouse's made cart", RoofOf(StoreKind.Warehouse)!.Value, 1.6f, 1.6f, "made cart", full with { Stock2 = 0 }),
                ("the market's baskets", RoofOf(StoreKind.Market)!.Value, 1.6f, 1.6f, "basket goods", full with { Stock = 0 }),
                ("the trading post's goods", RoofOf(StoreKind.TradingPost)!.Value, 1.6f, 0.8f, "goods", full with { Stock = 0 }),
                ("the barn's hay", RoofOf(StoreKind.Barn)!.Value, 2.4f, 1.6f, "hay rick", full with { Stock = 0 }),
                ("the founders' cart's food", RoofOf(StoreKind.Cart)!.Value, 0.8f, 0.8f, "wagon food", full with { Stock = 0 }),
                ("the founders' cart's tools", RoofOf(StoreKind.Cart)!.Value, 0.8f, 0.8f, "wagon tools", full with { Stock2 = 0 }),
                ("the founders' cart's basket of food", RoofOf(StoreKind.Cart)!.Value, 0.8f, 0.8f, "basket goods", full with { Stock = 0 }),
                // Kept nowhere, so shown while working (§2).
                ("the woodcutter's logs", RoofOf(JobKind.Woodcutter), 1.6f, 0.8f, "log pile", full with { Working = false }),
                ("the builder's planks", RoofOf(JobKind.Builder), 1.6f, 0.8f, "planks", full with { Working = false }),
                ("the builder's stone", RoofOf(JobKind.Builder), 1.6f, 0.8f, "cut stone", full with { Working = false }),
                ("the quarry's blocks", RoofOf(JobKind.Quarrier), 1.6f, 0.8f, "blocks", full with { Working = false }),
                ("the mine's ore", RoofOf(JobKind.Miner), 1.6f, 0.8f, "ore", full with { Working = false }),
                ("the forager's full baskets", RoofOf(JobKind.Forager), 0.8f, 0.8f, "basket goods", full with { Working = false }),
                ("the smith's iron", RoofOf(JobKind.Smith), 1.6f, 0.8f, "iron", full with { Working = false }),
                ("the bakery's firewood", RoofOf(JobKind.Baker), 1.6f, 0.8f, "oven wood", full with { Working = false }),
                ("the tailor's cloth", RoofOf(JobKind.Tailor), 1.6f, 0.8f, "cloth", full with { Working = false }),
                ("the tailor's wool", RoofOf(JobKind.Tailor), 1.6f, 0.8f, "bale", full with { Working = false }),
                ("the mill's sacks", RoofOf(JobKind.Miller), 1.6f, 1.6f, "flour", full with { Working = false }),
            })
            {
                bool with = Has(Draw(look, w, d, 0, full), part);
                bool bare = Has(Draw(look, w, d, 0, without), part);
                if (!with || bare)
                {
                    faults.Add($"{what}: drawn with it {with}, without it {bare} (want true, false)");
                }
            }

            // The dressing, always, whatever the stock (§2).
            var empty = default(YardState);
            foreach ((string what, RoofLook look, float w, float d, string[] parts) in new[]
            {
                ("the fishing hut", RoofOf(JobKind.Fisher), 0.8f, 0.8f, new[] { "net", "creel" }),
                ("the woodcutter", RoofOf(JobKind.Woodcutter), 1.6f, 0.8f, new[] { "sawhorse", "chopping block" }),
                ("the bakery", RoofOf(JobKind.Baker), 1.6f, 0.8f, new[] { "oven" }),
                ("the smithy", RoofOf(JobKind.Smith), 1.6f, 0.8f, new[] { "quench", "charcoal", "wheel" }),
                ("the forester", RoofOf(JobKind.Forester), 0.8f, 0.8f, new[] { "seedling bed" }),
                ("the barn", RoofOf(StoreKind.Barn)!.Value, 2.4f, 1.6f, new[] { "trough" }),
                ("the library", RoofOf(BuildingKind.Library), 1.6f, 1.6f, new[] { "paving", "bench", "sundial" }),
                ("the town hall", RoofOf(BuildingKind.TownHall), 2.85f, 1.9f, new[] { "paving", "notice board", "flag" }),
                ("the well", RoofOf(BuildingKind.Well), 0.7f, 0.7f, new[] { "trough" }),
                ("the granary", RoofOf(StoreKind.Granary)!.Value, 1.6f, 1.6f, new[] { "hoist" }),
                ("the mine", RoofOf(JobKind.Miner), 1.6f, 0.8f, new[] { "rails" }),
                ("the founders' cart", RoofOf(StoreKind.Cart)!.Value, 0.8f, 0.8f, new[] { "wagon wheel", "wagon hub", "wagon bed", "wagon rail", "wagon shaft", "wagon canvas", "wagon bucket" }),
            })
            {
                List<RoofPiece> drawn = Draw(look, w, d, 0, empty);
                string[] missing = parts.Where(p => !Has(drawn, p)).ToArray();
                if (missing.Length > 0)
                {
                    faults.Add($"{what} with nothing in stock lacks its {string.Join(", ", missing)}");
                }
            }

            // The founders' cart is a wagon (§3.4): two wheels, a bed and a canvas, each a filled shape, whatever it holds.
            List<RoofPiece> wagon = Draw(RoofOf(StoreKind.Cart)!.Value, 0.8f, 0.8f, 0, empty);
            int wheels = wagon.Count(p => p.Part == "wagon wheel" && p.Kind == RoofPieceKind.Fill);
            int wagonBeds = wagon.Count(p => p.Part == "wagon bed" && p.Kind == RoofPieceKind.Fill);
            int canvases = wagon.Count(p => p.Part == "wagon canvas" && p.Kind == RoofPieceKind.Fill);
            if (wheels != 2 || wagonBeds != 1 || canvases != 1)
            {
                faults.Add($"the founders' cart is {wheels} wheels, {wagonBeds} beds, {canvases} canvases (want 2, 1, 1)");
            }

            // Every yard thing on the building's own ground — the extent, A85's drawn size ÷ its share — at four turns; the
            // fishing hut's dock and boat out over the water excepted (§3.3).
            int checkedKinds = 0;
            foreach ((string what, RoofLook look, float w, float d, float share) in new[]
            {
                ("woodcutter", RoofOf(JobKind.Woodcutter), 1.6f, 0.8f, 0.8f), ("builder", RoofOf(JobKind.Builder), 1.6f, 0.8f, 0.8f),
                ("hunter", RoofOf(JobKind.Hunter), 1.6f, 0.8f, 0.8f), ("forager", RoofOf(JobKind.Forager), 0.8f, 0.8f, 0.8f),
                ("forester", RoofOf(JobKind.Forester), 0.8f, 0.8f, 0.8f), ("fishing hut", RoofOf(JobKind.Fisher), 0.8f, 0.8f, 0.8f),
                ("granary", RoofOf(StoreKind.Granary)!.Value, 1.6f, 1.6f, 0.8f), ("warehouse", RoofOf(StoreKind.Warehouse)!.Value, 1.6f, 1.6f, 0.8f),
                ("market", RoofOf(StoreKind.Market)!.Value, 1.6f, 1.6f, 0.8f), ("smithy", RoofOf(JobKind.Smith), 1.6f, 0.8f, 0.8f),
                ("quarry", RoofOf(JobKind.Quarrier), 1.6f, 0.8f, 0.8f), ("mine", RoofOf(JobKind.Miner), 1.6f, 0.8f, 0.8f),
                ("workshop", RoofOf(JobKind.Carpenter), 1.6f, 0.8f, 0.8f), ("bakery", RoofOf(JobKind.Baker), 1.6f, 0.8f, 0.8f),
                ("tailor", RoofOf(JobKind.Tailor), 1.6f, 0.8f, 0.8f), ("trading post", RoofOf(StoreKind.TradingPost)!.Value, 1.6f, 0.8f, 0.8f),
                ("farmhouse", RoofOf(JobKind.Farmer), 1.6f, 1.6f, 0.8f), ("mill", RoofOf(JobKind.Miller), 1.6f, 1.6f, 0.8f),
                ("barn", RoofOf(StoreKind.Barn)!.Value, 2.4f, 1.6f, 0.8f), ("library", RoofOf(BuildingKind.Library), 1.6f, 1.6f, 0.8f),
                ("town hall", RoofOf(BuildingKind.TownHall), 2.85f, 1.9f, 0.95f), ("well", RoofOf(BuildingKind.Well), 0.7f, 0.7f, 0.7f),
                ("founders' cart", RoofOf(StoreKind.Cart)!.Value, 0.8f, 0.8f, 0.8f),
            })
            {
                foreach (ushort facing in new ushort[] { 0, 10000, 30000, 50000 })
                {
                    List<RoofPiece> drawn = Draw(look, w, d, facing, full);
                    float gw = (w / share / 2f) + 0.03f;
                    float gd = (d / share / 2f) + 0.03f;
                    var outside = drawn
                        .Where(p => !p.Part.StartsWith("dock", StringComparison.Ordinal) && !p.Part.StartsWith("boat", StringComparison.Ordinal)
                            && !p.Part.Contains("shadow", StringComparison.Ordinal) && !p.Part.StartsWith("face", StringComparison.Ordinal)
                            && !p.Part.StartsWith("step", StringComparison.Ordinal) && !p.Part.StartsWith("chimney", StringComparison.Ordinal)
                            && !p.Part.StartsWith("turret", StringComparison.Ordinal) && !p.Part.StartsWith("ground", StringComparison.Ordinal)
                            && !p.Part.StartsWith("sail", StringComparison.Ordinal) && !p.Part.StartsWith("spar", StringComparison.Ordinal)
                            && !p.Part.StartsWith("storey", StringComparison.Ordinal))
                        .Where(p => p.Points.Any(q => Mathf.Abs(Local(q, facing).X) > gw || Mathf.Abs(Local(q, facing).Y) > gd))
                        .Select(p => p.Part).Distinct().ToArray();
                    if (outside.Length > 0)
                    {
                        faults.Add($"the {what} at turn {facing} has {string.Join(", ", outside)} off its ground");
                        break;
                    }
                }

                checkedKinds++;
            }

            // The roof on the door's half (§3.1): a woodcutter whose door is on its east end has its roof east, and west, west;
            // and the step outside that roof's eave.
            foreach ((Compass faces, float sign) in new[] { (Compass.East, 1f), (Compass.West, -1f) })
            {
                var door = new Door(default, default, faces, Compass.North, DoorBlock.None, default, false);
                List<RoofPiece> drawn = Draw(RoofOf(JobKind.Woodcutter), 1.6f, 0.8f, 0, full, door);
                RoofPiece[] roof = drawn.Where(p => p.Part == "face" && p.Kind == RoofPieceKind.Textured).ToArray();
                float roofX = roof.SelectMany(p => p.Points).Average(q => Local(q, 0).X);
                RoofPiece? step = drawn.FirstOrDefault(p => p.Part == "step" && p.Kind == RoofPieceKind.Fill);
                float stepX = step is null ? 0f : step.Points.Average(q => Local(q, 0).X);
                float roofEdge = roof.SelectMany(p => p.Points).Max(q => Local(q, 0).X * sign);
                if (roofX * sign < 0.2f || step is null || stepX * sign <= roofEdge)
                {
                    faults.Add($"a woodcutter with its door {faces} has its roof at x {roofX:F2} and its step at {stepX:F2} (want the roof on the {faces} half, the step past its eave)");
                }
            }

            // The fishing hut's dock goes out on its water's side, and there is none without water (§3.3).
            foreach (sbyte side in new sbyte[] { 0, 1, 2, 3 })
            {
                List<RoofPiece> drawn = Draw(RoofOf(JobKind.Fisher), 0.8f, 0.8f, 0, full with { Water = side });
                RoofPiece? dock = drawn.FirstOrDefault(p => p.Part == "dock" && p.Kind == RoofPieceKind.Fill);
                Vector2 way = side switch { 0 => new Vector2(0, -1), 1 => new Vector2(1, 0), 2 => new Vector2(0, 1), _ => new Vector2(-1, 0) };
                float out_ = dock is null ? 0f : dock.Points.Average(q => Local(q, 0).Dot(way));
                if (dock is null || out_ < 0.5f)
                {
                    faults.Add($"a fishing hut with water on side {side} has its dock {(dock is null ? "missing" : $"{out_:F2} out")} (want more than half a tile out that way)");
                }
            }

            if (Has(Draw(RoofOf(JobKind.Fisher), 0.8f, 0.8f, 0, full with { Water = -1 }), "dock"))
            {
                faults.Add("a fishing hut with no water by it has a dock");
            }

            // A home (§4): its things on its plot's tiles and off the house; the woodpile as long as its level; a larder of that
            // many; no vegetable beds. A 3 × 2 plot, the house on the front row's left, the front the lane (+y, the door's way).
            var southDoor = new Door(default, default, Compass.South, Compass.North, DoorBlock.None, default, false);
            Vector2[] yardTiles = { new(1f, 0f), new(-1f, -1f), new(0f, -1f), new(1f, -1f) };
            Vector2[] plot = yardTiles.Concat(new[] { new Vector2(-1f, 0f), new Vector2(0f, 0f) }).ToArray();
            Vector2 house = new(-0.5f, 0f);
            float lastPile = 0f;
            foreach (RoofLook home in new[] { RoofOf(BuildingKind.Home), RoofOf(BuildingKind.Cottage), RoofOf(BuildingKind.Manor) })
            {
                for (byte level = 0; level <= 4; level++)
                {
                    var homeCentre = centre + (house * ppt);
                    Vector2[] local = yardTiles.Select(t => t - house).ToArray();
                    List<RoofPiece> drawn = RoofDrawing(home, homeCentre, 1.6f, 0.8f, 0, kind, ppt, southDoor, homeCentre, 9, false, new YardState(Stock: level, Stock2: level), local);
                    Vector2 OnPlot(Vector2 q) => ((q - homeCentre) / ppt) + house;
                    string[] offPlot = drawn
                        .Where(p => !p.Part.StartsWith("face", StringComparison.Ordinal) && !p.Part.StartsWith("storey", StringComparison.Ordinal)
                            && !p.Part.StartsWith("chimney", StringComparison.Ordinal) && !p.Part.StartsWith("step", StringComparison.Ordinal)
                            && !p.Part.Contains("shadow", StringComparison.Ordinal))
                        .Where(p => p.Points.Any(q => !plot.Any(t => Mathf.Abs(OnPlot(q).X - t.X) <= 0.52f && Mathf.Abs(OnPlot(q).Y - t.Y) <= 0.52f)))
                        .Select(p => p.Part).Distinct().ToArray();
                    string[] underHouse = drawn
                        .Where(p => p.Part is "woodpile" or "larder" or "water butt" or "bench" or "washing" or "washing line" or "chopping block" or "sundial")
                        .Where(p => p.Points.Any(q => Mathf.Abs(OnPlot(q).X - house.X) < 0.8f && Mathf.Abs(OnPlot(q).Y - house.Y) < 0.4f))
                        .Select(p => p.Part).Distinct().ToArray();
                    RoofPiece[] pile = drawn.Where(p => p.Part == "woodpile" && p.Kind == RoofPieceKind.Fill).ToArray();
                    float pileLength = pile.Length == 0 ? 0f : pile.SelectMany(p => p.Points).Max(q => q.X) - pile.SelectMany(p => p.Points).Min(q => q.X)
                        + pile.SelectMany(p => p.Points).Max(q => q.Y) - pile.SelectMany(p => p.Points).Min(q => q.Y);
                    int larder = drawn.Count(p => p.Part == "larder" && p.Kind == RoofPieceKind.Fill);
                    bool beds = drawn.Any(p => p.Part.Contains("bed", StringComparison.Ordinal) || p.Part.Contains("seedling", StringComparison.Ordinal));
                    bool grows = level == 0 ? pile.Length == 0 : pileLength > lastPile;
                    if (offPlot.Length > 0 || underHouse.Length > 0 || !grows || larder != level || beds)
                    {
                        faults.Add($"a {home.Yard} home at level {level}: off its plot {string.Join(", ", offPlot)}; under its house {string.Join(", ", underHouse)}; "
                            + $"woodpile {pileLength / ppt:F2} tiles after {lastPile / ppt:F2} (want longer, none at 0); larder {larder} (want {level}); vegetable beds {beds}");
                    }

                    lastPile = level == 0 ? 0f : pileLength;
                }
            }

            // The fences by tier (§4.3).
            foreach ((BuildingKindTier tier, string[] want) in new[]
            {
                (BuildingKindTier.Cabin, new[] { "fence wattle", "fence weave", "fence stake" }),
                (BuildingKindTier.Cottage, new[] { "fence rail", "fence post" }),
                (BuildingKindTier.Manor, new[] { "fence wall", "fence stone" }),
            })
            {
                string[] got = FencePieces(new[] { (new Vector2(0, 0), new Vector2(1, 0), tier, true) }).Select(p => p.Part).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
                if (!got.SequenceEqual(want.OrderBy(s => s, StringComparer.Ordinal)))
                {
                    faults.Add($"a {tier}'s fence is drawn as {string.Join(", ", got)} (want {string.Join(", ", want)})");
                }
            }

            // The pen (§5): each material's strokes inside their roof; thinned with the zoom so they are never closer than 3 px —
            // a roof laid at 20, 40 and 160 px a tile keeps fewer at 20 and all at 160; hatching only on the faces darker than 1.
            foreach (RoofMaterial m in Enum.GetValues<RoofMaterial>())
            {
                var look = new RoofLook(RoofForm.Roof, m);
                List<RoofPiece> drawn = Draw(look, 1.6f, 1.6f, 0, empty);
                RoofPiece[] pen = drawn.Where(p => p.Part == "face pen").ToArray();
                bool inside = pen.SelectMany(p => p.Points).All(q => Mathf.Abs(Local(q, 0).X) <= 0.845f && Mathf.Abs(Local(q, 0).Y) <= 0.845f);
                RoofPiece[] faces = drawn.Where(p => p.Part == "face" && p.Kind == RoofPieceKind.Textured).ToArray();
                int dark = faces.Count(p => p.Colour.R * RoofTextureMean < kind.Lerp(new Color("#efe6d0"), 0.12f).R - 0.001f);
                int hatched = drawn.Count(p => p.Part == "face hatch");
                int Triangles(float zoom) => RoofLineRun(pen.ToList(), zoom).Triangles;
                int all = pen.Sum(p => p.Points.Length / 2) * 4;
                if (pen.Length == 0 || !inside || hatched != dark || dark == 0 || !(Triangles(20f) < Triangles(40f) || Triangles(40f) == all) || Triangles(160f) != all)
                {
                    faults.Add($"the {m} roof: {pen.Length} pen pieces, inside {inside}; hatched {hatched} of {faces.Length} faces, {dark} darker than 1; "
                        + $"pen triangles at 20/40/160 px {Triangles(20f)}/{Triangles(40f)}/{Triangles(160f)} of {all} (want fewer at 20, all at 160)");
                }
            }

            // The parked cart is a cart.
            if (!Has(Draw(new RoofLook(RoofForm.Cart, RoofMaterial.Boards), 0.34f, 0.22f, 0, empty), "cart"))
            {
                faults.Add("a parked cart draws no cart");
            }

            // §2's table, as the spec writes it: what each yard reads from its own store, and which show while working.
            var table = new (YardKind Yard, StockRead Stock, StockRead Stock2, bool WhileWorking)[]
            {
                (YardKind.Fisher, StockRead.Fish, StockRead.None, false), (YardKind.Hunter, StockRead.Meat, StockRead.Leather, false),
                (YardKind.Farmhouse, StockRead.Wheat, StockRead.None, false), (YardKind.Granary, StockRead.Edible, StockRead.None, false),
                (YardKind.Warehouse, StockRead.HeldLessCarts, StockRead.Handcarts, false), (YardKind.Market, StockRead.Held, StockRead.None, false),
                (YardKind.TradingPost, StockRead.Held, StockRead.None, false), (YardKind.Barn, StockRead.Hay, StockRead.None, false),
                (YardKind.FoundersCart, StockRead.Edible, StockRead.Tools, false),
                (YardKind.Woodcutter, StockRead.None, StockRead.None, true), (YardKind.Builder, StockRead.None, StockRead.None, true),
                (YardKind.Forager, StockRead.None, StockRead.None, true), (YardKind.Smith, StockRead.None, StockRead.None, true),
                (YardKind.Quarry, StockRead.None, StockRead.None, true), (YardKind.Mine, StockRead.None, StockRead.None, true),
                (YardKind.Bakery, StockRead.None, StockRead.None, true), (YardKind.Tailor, StockRead.None, StockRead.None, true),
                (YardKind.Mill, StockRead.None, StockRead.None, true),

                // W3's (A199, `tavern-and-brewery-art.md §3`): the tavern keeps its ale; a brewery's batch keeps nothing.
                (YardKind.Tavern, StockRead.Ale, StockRead.None, false), (YardKind.Brewery, StockRead.None, StockRead.None, true),
            };
            foreach (YardKind y in Enum.GetValues<YardKind>())
            {
                (StockRead a, StockRead b, bool w) = StockReadOf(y);
                (YardKind Yard, StockRead Stock, StockRead Stock2, bool WhileWorking) want =
                    table.Any(r => r.Yard == y) ? table.First(r => r.Yard == y) : (y, StockRead.None, StockRead.None, false);
                if (a != want.Stock || b != want.Stock2 || w != want.WhileWorking)
                {
                    faults.Add($"the {y} yard reads {a}, {b}, while working {w} (want {want.Stock}, {want.Stock2}, {want.WhileWorking})");
                }
            }

            // And each read reads its own good: a store of 7 fish, 11 meat, 13 leather, 17 wheat, 19 hay and 2 handcarts.
            var store = new Stockpile(Stockpile.Kinds);
            store.Add(Goods.Fish, 7);
            store.Add(Goods.Meat, 11);
            store.Add(Goods.Leather, 13);
            store.Add(Goods.Wheat, 17);
            store.Add(Goods.Hay, 19);
            store.Add(Goods.Handcart, 2);
            store.Add(Goods.Tools, 3);
            Goods[] edibleHere = { Goods.Fish, Goods.Meat };
            foreach ((StockRead r, int want) in new[]
            {
                (StockRead.Fish, 7), (StockRead.Meat, 11), (StockRead.Leather, 13), (StockRead.Wheat, 17), (StockRead.Hay, 19),
                (StockRead.Handcarts, 2), (StockRead.Held, 72), (StockRead.HeldLessCarts, 70), (StockRead.Edible, 18), (StockRead.Tools, 3), (StockRead.None, 0),
            })
            {
                if (Read(store, r, edibleHere) != want)
                {
                    faults.Add($"reading {r} gives {Read(store, r, edibleHere)} (want {want})");
                }
            }

            // Levels (§2): 0, 1–4, 5–19, 20–59, 60 +.
            if (YardLevel(0) != 0 || YardLevel(1) != 1 || YardLevel(4) != 1 || YardLevel(5) != 2 || YardLevel(19) != 2 || YardLevel(20) != 3 || YardLevel(59) != 3 || YardLevel(60) != 4)
            {
                faults.Add("the stock levels do not break at 1, 5, 20 and 60");
            }

            // A field stops at a building (A142): its tiles under a farmhouse's yard are not drawn as field, in any stage.
            foreach (Terrain stage in new[] { Terrain.Field, Terrain.Sown, Terrain.Ripe })
            {
                if (FieldStageDrawn(stage, standing: true) is Terrain.Field or Terrain.Sown or Terrain.Ripe)
                {
                    faults.Add($"a {stage} tile under a building is drawn as field (want the building's ground)");
                }

                if (FieldStageDrawn(stage, standing: false) != stage)
                {
                    faults.Add($"a {stage} tile in the open is drawn as {FieldStageDrawn(stage, standing: false)} (want {stage})");
                }
            }

            if (FieldStageDrawn(Terrain.Grass, standing: false) is Terrain.Field or Terrain.Sown or Terrain.Ripe)
            {
                faults.Add("grass in a farm's ground is drawn as field");
            }

            // ⭐ The stock piles' line rides on this one (A159): the probe's list is `Main.cs`, the systems lane's.
            return (faults.Count == 0
                ? $"[widths] building yards: ✅ twelve stock things drawn only with their stock (the founders' cart's food and tools among them) and eleven piles only while working (§2's table); the dressing always, the founders' cart a wagon with its wheels, bed, shafts and canvas; every yard thing on its building's ground at four turns ({checkedKinds} kinds), the fishing hut's dock out on its water's side and none without water; the roof on the door's half, the step past its eave; a home's things on its plot and off its house, the woodpile longer with each level, a larder of its level, no vegetable beds, for the cabin, cottage and manor; the fences by tier; the pen's strokes inside their roof for seven materials, thinned under 3 px, hatching only in shade; the parked cart a cart; §2's table of what each yard reads, and each read its own good; the levels at 1, 5, 20, 60; a field stops at a building, every stage (A142)"
                : $"[widths] building yards: ⛔ {string.Join("; ", faults)}") + "\n" + TheStockPilesHold() + "\n" + TheTavernAndBreweryHold() + "\n" + TheHerbalistsHutHolds();
        }
        catch (System.Exception e)
        {
            return $"[widths] building yards: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
