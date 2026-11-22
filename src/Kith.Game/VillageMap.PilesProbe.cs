using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>The probe for the stock piles (A159, `specs/stock-piles.md §9`): every number named from the spec (art traps 17, 25).</remarks>
public partial class VillageMap
{
    /// <summary>
    /// ⭐ <c>stock piles:</c> — the sizes' edges; a stockpile's sizes as its yard state; a pile for each good it holds and none
    /// without; the bays inside the yard and apart; a bay unmoved when its neighbour grows; a good no painter names as sacks;
    /// every good's pile growing with its size; the heap's spill in its good's colour; on the map, two heaps at a store's
    /// door as wide as their sizes, clear of the building and of each other, each the click's; a heap laid again only when
    /// its size moves; and with the tick off the chips as before. Posed and put back; nothing in the sim moves.
    /// </summary>
    internal string TheStockPilesHold()
    {
        try
        {
            var faults = new List<string>();

            // §5: six sizes — 1–9, 10–29, 30–69, 70–149, 150–399, 400 +.
            foreach ((int amount, byte want) in new (int, byte)[]
            {
                (0, 0), (1, 1), (9, 1), (10, 2), (29, 2), (30, 3), (69, 3), (70, 4), (149, 4), (150, 5), (399, 5), (400, 6), (1518, 6),
            })
            {
                if (PileLevel(amount) != want)
                {
                    faults.Add($"{amount} is size {PileLevel(amount)}, not {want}");
                }
            }

            // §5: a stockpile's sizes, laid again only when a size moves — Larkspur's year-2 stockpile, 19 logs, 112 firewood, 7 stone.
            var store = new Stockpile(17);
            if (PileSizesOf(store, 17) is not null)
            {
                faults.Add("an empty stockpile has sizes");
            }

            store.Add(Goods.Logs, 19);
            store.Add(Goods.Firewood, 112);
            store.Add(Goods.Stone, 7);
            string? larkspur = PileSizesOf(store, 17);
            if (larkspur != "1.2,2.4,3.1")
            {
                faults.Add($"19 logs, 112 firewood, 7 stone read \"{larkspur}\", not \"1.2,2.4,3.1\"");
            }

            store.Add(Goods.Logs, 6);
            string? within = PileSizesOf(store, 17);
            store.Add(Goods.Logs, 5);
            string? grown = PileSizesOf(store, 17);
            if (new YardState(Piles: within) != new YardState(Piles: larkspur) || new YardState(Piles: grown) == new YardState(Piles: larkspur))
            {
                faults.Add("a stockpile is laid again when an amount moves inside a size, or not when it crosses one (25 → 30 logs)");
            }

            List<(Goods Goods, byte Level)> read = PileSizesRead(larkspur);
            if (read.Count != 3 || read[1] != (Goods.Firewood, 4))
            {
                faults.Add("the sizes do not read back as written");
            }

            // §6: the bays — one to nine, inside the yard and apart; ten goods draw nine.
            const float hw = 0.4f;
            for (int n = 1; n <= 10; n++)
            {
                List<(Vector2 At, float Hx, float Hy)> bays = PileBays(n, hw, hw);
                if (bays.Count != Mathf.Min(n, 9))
                {
                    faults.Add($"{n} goods make {bays.Count} bays");
                }

                for (int i = 0; i < bays.Count; i++)
                {
                    (Vector2 a, float ax, float ay) = bays[i];
                    if (Mathf.Abs(a.X) + ax > hw + 1e-4f || Mathf.Abs(a.Y) + ay > hw + 1e-4f)
                    {
                        faults.Add($"bay {i + 1} of {n} runs off the yard");
                    }

                    for (int j = i + 1; j < bays.Count; j++)
                    {
                        (Vector2 b, float bx, float by) = bays[j];
                        if (Mathf.Abs(a.X - b.X) < ax + bx - 1e-4f && Mathf.Abs(a.Y - b.Y) < ay + by - 1e-4f)
                        {
                            faults.Add($"bays {i + 1} and {j + 1} of {n} overlap");
                        }
                    }
                }
            }

            // §4, §6: the stockpile drawn — a pile for each good it holds, none without, every piece on its yard at two turns.
            const float ppt = 40f;
            var centre = new Vector2(500f, 500f);
            RoofLook pile = RoofOf(StoreKind.Pile)!.Value;
            List<RoofPiece> Stockpile(string? sizes, ushort facing = 0) =>
                RoofDrawing(pile, centre, 0.8f, 0.8f, facing, MarketColour, ppt, null, centre, 2, false, new YardState(Piles: sizes));
            static bool Has(List<RoofPiece> pieces, string part) => pieces.Any(p => p.Part == part);

            List<RoofPiece> bare = Stockpile(null);
            List<RoofPiece> stocked = Stockpile(larkspur);
            foreach (string part in new[] { "logs", "firewood", "stone" })
            {
                if (!Has(stocked, part) || Has(bare, part))
                {
                    faults.Add($"the stockpile's {part} is {(Has(bare, part) ? "drawn with none" : "missing with some")}");
                }
            }

            if (Has(Stockpile("1.4,3.4"), "firewood"))
            {
                faults.Add("firewood drawn on a stockpile holding none");
            }

            foreach (ushort facing in new ushort[] { 0, 16384 + 3000 })
            {
                float turn = facing * Mathf.Tau / 65536f;
                foreach (RoofPiece piece in Stockpile("1.5,2.5,3.5,5.5", facing))
                {
                    if (piece.Part is "ground" or "post" || piece.Part.Contains("shadow") || piece.Part.Contains("stipple") || piece.Part.Contains("tuft") || piece.Part.Contains("pebble"))
                    {
                        continue;
                    }

                    foreach (Vector2 p in piece.Points)
                    {
                        Vector2 local = ((p - centre) / ppt).Rotated(-turn);
                        if (Mathf.Abs(local.X) > 0.43f || Mathf.Abs(local.Y) > 0.43f)
                        {
                            faults.Add($"the stockpile's {piece.Part} lies off its yard at facing {facing}");
                            goto offYard;
                        }
                    }
                }
            }

            offYard:

            // §6: a good's bay does not move when its neighbour grows.
            static string StonesOf(List<RoofPiece> pieces) =>
                string.Join(";", pieces.Where(p => p.Part == "stone").SelectMany(p => p.Points).Select(v => $"{v.X:F3},{v.Y:F3}"));
            if (StonesOf(Stockpile("1.2,3.1")) != StonesOf(Stockpile("1.5,3.1")))
            {
                faults.Add("the stone moved when the logs beside it grew");
            }

            // §4: a good no painter names — barley, beer, a mod's — is sacks in its colour.
            if (!Has(Stockpile("99.3"), "99"))
            {
                faults.Add("a good no painter names draws nothing");
            }

            // §4, §5: every good's pile grows with its size — its extent never shrinks, and the sixth is half as big again as the first.
            var mainPart = new (Goods Goods, string Part)[]
            {
                (Goods.Produce, "basket"), (Goods.Logs, "logs"), (Goods.Firewood, "firewood"), (Goods.Stone, "stone"),
                (Goods.Tools, "tools"), (Goods.Iron, "ore"), (Goods.Fish, "creel"), (Goods.Meat, "joint"), (Goods.Leather, "hides"),
                (Goods.Wheat, "Wheat"), (Goods.IronTools, "tools"), (Goods.Flour, "Flour"), (Goods.Bread, "basket"),
                (Goods.Hay, "hay rick"), (Goods.Clothing, "cloth"), (Goods.Handcart, "cart"), ((Goods)99, "99"),
            };
            foreach ((Goods goods, string part) in mainPart)
            {
                float last = 0f;
                float first = 0f;
                for (int level = 1; level <= 6; level++)
                {
                    Vector2[] points = HeapDrawing(goods, level, 7, Vector2.Zero).Where(p => p.Part == part).SelectMany(p => p.Points).ToArray();
                    if (points.Length == 0)
                    {
                        faults.Add($"{goods} at size {level} draws no {part}");
                        break;
                    }

                    var box = new Rect2(points[0], Vector2.Zero);
                    foreach (Vector2 p in points)
                    {
                        box = box.Expand(p);
                    }

                    float extent = box.Size.X * box.Size.Y;
                    if (level == 1)
                    {
                        first = extent;
                    }

                    if (extent < last * 0.98f)
                    {
                        faults.Add($"{goods} shrinks from size {level - 1} to {level}");
                    }

                    last = extent;
                }

                if (last < first * 1.5f)
                {
                    faults.Add($"{goods} at size 6 is not half as big again as at size 1 ({first:F3} → {last:F3})");
                }
            }

            // §7: the spill is the good's own colour (the chip's), so a heap reads at a glance.
            RoofPiece? spill = HeapDrawing(Goods.Fish, 3, 1, Vector2.Zero).FirstOrDefault(p => p.Part == "heap spill" && p.Kind == RoofPieceKind.Fill);
            if (spill is null || !spill.Colour.IsEqualApprox(Washed(GoodsPalette.ColourOf(Goods.Fish)) with { A = 0.6f }))
            {
                faults.Add("a fish heap's spill is not the fish's colour");
            }

            string onTheMap = HeapsOnTheMap(faults);

            return faults.Count == 0
                ? $"[widths] stock piles: ✅ six sizes, 1–9 to 400 + (§5); a stockpile's sizes its yard state, laid again across a size and not within one; logs, firewood and stone each drawn only when held, on the yard at two turns; one to nine bays inside the yard and apart, ten goods nine; the stone unmoved when the logs grow; a good no painter names as sacks; all 17 goods' piles grow with their size; the spill the good's colour; {onTheMap}"
                : $"[widths] stock piles: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] stock piles: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }

    /// <summary>§7 on the live map: two heaps posed at the first store's door, with the tick on and off; put back.</summary>
    private string HeapsOnTheMap(List<string> faults)
    {
        SimWorld world = _world!;
        if (world.StoreBuildings.Count == 0)
        {
            faults.Add("no store to pose heaps at");
            return "";
        }

        StoreBuilding store = world.StoreBuildings[0];
        GridPos tile = store.Tile;
        var logs = new GroundStack { Position = tile, Goods = Goods.Logs, Amount = 78 };
        var food = new GroundStack { Position = tile, Goods = Goods.Produce, Amount = 1472 };
        bool watercolourWas = _watercolour;
        float zoomWas = _pixelsPerTile;
        world.GroundStacks.Add(logs);
        world.GroundStacks.Add(food);
        try
        {
            _pixelsPerTile = MaxPixelsPerTile;
            (Vector2 _, Rect2 building) = BuildingBoxOn(tile)!.Value;
            Rect2 BoxOf(GroundStack heap) => EveryHeapChip().First(e => ReferenceEquals(e.Heap, heap)).Box;

            _watercolour = true;
            Rect2 a = BoxOf(logs);
            Rect2 b = BoxOf(food);
            if (Mathf.Abs(a.Size.X - (HeapWidthOf(4) * _pixelsPerTile)) > 0.5f || Mathf.Abs(b.Size.X - (HeapWidthOf(6) * _pixelsPerTile)) > 0.5f)
            {
                faults.Add($"78 logs and 1,472 food lie {a.Size.X:F1} and {b.Size.X:F1} px wide, not their sizes' widths");
            }

            if (a.Intersects(building) || b.Intersects(building))
            {
                faults.Add("a heap lies over the building whose door it is at");
            }

            if (a.Intersects(b))
            {
                faults.Add("two heaps on one tile lie over each other");
            }

            if (HeapUnder(a.GetCenter()) != tile || HeapUnder(b.GetCenter()) != tile)
            {
                faults.Add("a click on a heap is not the heap");
            }

            // CLAUDE.md: laid once — again only when its size moves.
            int layings = HeapLayings;
            CachedHeap(logs, a);
            CachedHeap(logs, a);
            logs.Amount = 100;
            CachedHeap(logs, a);
            int within = HeapLayings - layings;
            logs.Amount = 160;
            CachedHeap(logs, a);
            int across = HeapLayings - layings - within;
            logs.Amount = 78;
            if (within > 1 || across != 1)
            {
                faults.Add($"a heap was laid {within} times for one size and {across} more when it crossed one");
            }

            // Tick off: today's chips, the same width, side by side.
            _watercolour = false;
            Rect2 c = BoxOf(logs);
            Rect2 d = BoxOf(food);
            float chip = Mathf.Max(3f, _pixelsPerTile * HeapSize);
            if (Mathf.Abs(c.Size.X - chip) > 0.01f || Mathf.Abs(d.Size.X - chip) > 0.01f || Mathf.Abs(d.Position.X - c.Position.X - (chip * 1.15f)) > 0.01f)
            {
                faults.Add("with the tick off a heap is not today's chip");
            }

            return $"on the map two heaps at {store.Name}'s door {a.Size.X:F0} and {b.Size.X:F0} px wide, clear of it and of each other, each the click's, laid once a size; tick off, the chips";
        }
        finally
        {
            _watercolour = watercolourWas;
            _pixelsPerTile = zoomWas;
            world.GroundStacks.Remove(logs);
            world.GroundStacks.Remove(food);
        }
    }
}
