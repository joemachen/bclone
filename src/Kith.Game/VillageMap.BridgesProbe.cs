using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>painted bridges:</c> line (A175, <c>painted-bridges.md §6</c>). Every expectation is named from the spec —
    /// the files, the widths, the spacings — never read back from the code's own lists (art traps 17, 25). Bridges are posed in
    /// a valley of the probe's own: a river across the span between 0.9 tiles from each end.
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): the wash, the foam and the shadow's softness are the renders'.</remarks>
    internal string ThePaintedBridgesHold()
    {
        try
        {
            var faults = new List<string>();

            // The two decks, as the spec names them: 1536 × 96, lossless, mipmapped, drawn with mipmaps, mean tone 0.8 ± 0.03.
            foreach ((bool stone, string file) in new[] { (false, "deck_planks"), (true, "deck_setts") })
            {
                string path = $"res://textures/buildings/{file}.png";
                CanvasTexture? held = BridgeDeckOf(stone);
                if (held?.DiffuseTexture?.ResourcePath != path || held.TextureFilter != TextureFilterEnum.LinearWithMipmaps)
                {
                    faults.Add($"a {(stone ? "stone" : "wooden")} deck draws {held?.DiffuseTexture?.ResourcePath} with {held?.TextureFilter} (want {path}, LinearWithMipmaps)");
                    continue;
                }

                using Image image = held.DiffuseTexture.GetImage();
                if (image.GetWidth() != 1536 || image.GetHeight() != 96 || !image.HasMipmaps() || image.IsCompressed())
                {
                    faults.Add($"{file} is {image.GetWidth()} × {image.GetHeight()}, mipmaps {image.HasMipmaps()}, compressed {image.IsCompressed()} (want 1536 × 96, mipmapped, lossless)");
                    continue;
                }

                double sum = 0;
                for (int y = 0; y < 96; y += 2)
                {
                    for (int x = 0; x < 1536; x += 2)
                    {
                        Color c = image.GetPixel(x, y);
                        sum += (c.R + c.G + c.B) / 3.0;
                    }
                }

                double mean = sum / (768 * 48);
                if (System.Math.Abs(mean - 0.8) > 0.03)
                {
                    faults.Add($"{file}'s mean tone is {mean:F3} (want 0.8 ± 0.03, or a deck is not its bridge's colour)");
                }
            }

            // What a bridge is drawn as (§3): stone never wears, closed is fallen in, a ruin kept open is failing.
            foreach ((bool stone, Condition worn, bool closed, Condition want) in new[]
            {
                (false, Condition.Worn, false, Condition.Worn), (false, Condition.Ruin, false, Condition.Failing),
                (false, Condition.Ruin, true, Condition.Ruin), (true, Condition.Failing, false, Condition.Sound),
            })
            {
                if (DrawnCondition(stone, worn, closed) != want)
                {
                    faults.Add($"a {(stone ? "stone" : "wooden")} bridge {worn}, closed {closed}, is drawn {DrawnCondition(stone, worn, closed)} (want {want})");
                }
            }

            // And the map's own reading of a bridge: a ruin closed is drawn fallen in, the same ruin kept open is drawn failing.
            foreach ((bool closed, Condition want) in new[] { (true, Condition.Ruin), (false, Condition.Failing) })
            {
                var standing = new Bridge { Near = Point.Origin, Far = Point.Origin, Kind = BuildingKind.Bridge, Name = "probe", Closed = closed };
                Condition drawn = BridgeSignatureOf(standing, Condition.Ruin).Drawn;
                if (drawn != want)
                {
                    faults.Add($"a wooden bridge in ruin, closed {closed}, reaches the map as {drawn} (want {want})");
                }
            }

            // Posed bridges: six turns (along an axis, slants, along and across the light, past a half turn), 3 and 12 tiles.
            int posed = 0;
            foreach (float degrees in new[] { 0f, 30f, 45f, 90f, 135f, 200f })
            {
                foreach (float len in new[] { 3f, 12f })
                {
                    float r = Mathf.DegToRad(degrees);
                    var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
                    var across = new Vector2(-dir.Y, dir.X);
                    var near = new Vector2(5.25f, -3.5f);
                    Vector2 far = near + (dir * len);
                    float Along(Vector2 p) => (p - near).Dot(dir);
                    float Across(Vector2 p) => (p - near).Dot(across);
                    bool Wet(float a) => a > 0.9f && a < len - 0.9f;
                    bool water(Vector2 p) => Wet(Along(p));
                    string where = $"a {len:0}-tile bridge at {degrees:0}°";

                    List<RoofPiece> Drawn(bool stone, Condition worn) =>
                        BridgeDrawing(new BridgeSignature(near, far, stone, worn, BridgeSeedOf(near)), water);
                    List<(float A0, float A1)> Boards(List<RoofPiece> pieces) => pieces
                        .Where(p => p.Part == "board").Select(p => (p.Points.Min(Along), p.Points.Max(Along))).OrderBy(b => b.Item1).ToList();
                    Vector2 Centre(IEnumerable<Vector2> points) => points.Aggregate(Vector2.Zero, (s, p) => s + p) / points.Count();
                    List<float> Bents(List<RoofPiece> pieces, string part) => pieces
                        .Where(p => p.Part == part).Select(p => (float)System.Math.Round(Along(Centre(p.Points)), 3)).Distinct().OrderBy(a => a).ToList();

                    // Sound wood: the boards tile the span end to end inside the width; the supports only over the water,
                    // evenly; the shadow to the lower right; a post at both ends of both sides, and rails both sides.
                    List<RoofPiece> sound = Drawn(false, Condition.Sound);
                    List<(float A0, float A1)> boards = Boards(sound);
                    bool tiled = boards.Count > 0 && Mathf.Abs(boards[0].A0) < 1e-3f && Mathf.Abs(boards[^1].A1 - len) < 1e-3f
                        && boards.Zip(boards.Skip(1)).All(pair => Mathf.Abs(pair.First.A1 - pair.Second.A0) < 1e-3f);
                    bool inside = sound.Where(p => p.Part == "board").SelectMany(p => p.Points).All(p => Mathf.Abs(Across(p)) <= BridgeHalfWidth + 1e-3f);
                    if (!tiled || !inside)
                    {
                        faults.Add($"{where}: {boards.Count} boards, tiling the span {tiled}, inside 0.35 of its middle {inside}");
                    }

                    List<float> bents = Bents(sound, "cap");
                    bool piled = sound.Where(p => p.Part == "pile").All(p => Wet(Along(Centre(p.Points))));
                    List<float> steps = bents.Zip(bents.Skip(1)).Select(pair => pair.Second - pair.First).ToList();
                    bool even = steps.All(d => d <= 2.0f && Mathf.Abs(d - steps[0]) < 0.01f);
                    if (bents.Count < 2 || !piled || !even || sound.Count(p => p.Part == "pile") != 2 * bents.Count)
                    {
                        faults.Add($"{where}: {bents.Count} bents at {string.Join(", ", bents.Select(b => b.ToString("0.00")))}, {sound.Count(p => p.Part == "pile")} piles, all over the water {piled}, evenly {even}");
                    }

                    Vector2 shadowAt = Centre(sound.Where(p => p.Part == "shadow").SelectMany(p => p.Points)) - ((near + far) / 2f);
                    Vector2 wantShadow = new Vector2(0.13f, 0.17f) * BridgeLift;
                    if (shadowAt.DistanceTo(wantShadow) > 0.02f)
                    {
                        faults.Add($"{where}: the shadow sits {shadowAt.X:0.000}, {shadowAt.Y:0.000} from the span (want {wantShadow.X:0.000}, {wantShadow.Y:0.000}, the lower right)");
                    }

                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector2[] posts = sound.Where(p => p.Part == "post").Select(p => Centre(p.Points)).Where(c => Across(c) * side > 0f).ToArray();
                        bool ends = posts.Any(c => Along(c) < 0.15f) && posts.Any(c => Along(c) > len - 0.15f);
                        int rails = sound.Count(p => p.Part == "rail" && Across(Centre(p.Points)) * side > 0f);
                        if (!ends || rails != posts.Length - 1)
                        {
                            faults.Add($"{where}, side {side:+0;-0}: posts at both ends {ends}, {posts.Length} posts and {rails} rails (want one fewer rail than posts)");
                        }
                    }

                    // Failing: boards lost over the water only, some and not too many, the same ones laid twice; a run of rail lost.
                    List<(float A0, float A1)> failing = Boards(Drawn(false, Condition.Failing));
                    var lost = boards.Where(b => !failing.Any(f => Mathf.Abs(f.A0 - b.A0) < 1e-4f)).ToList();
                    bool overWater = lost.All(b => Wet(b.A0) && Wet(b.A1));
                    int overTheRiver = boards.Count(b => Wet(b.A0) && Wet(b.A1));
                    bool again = failing.SequenceEqual(Boards(Drawn(false, Condition.Failing)));
                    bool share = len < 4f ? lost.Count <= overTheRiver / 2 : lost.Count > 0 && lost.Count < overTheRiver * 0.35f;
                    int railsFailing = Drawn(false, Condition.Failing).Count(p => p.Part == "rail");
                    if (!overWater || !again || !share || railsFailing != sound.Count(p => p.Part == "rail") - 1)
                    {
                        faults.Add($"{where}, failing: {lost.Count} of {overTheRiver} boards over the river lost, all over the water {overWater}, the same laid twice {again}; {railsFailing} rails of {sound.Count(p => p.Part == "rail")} (want one run lost)");
                    }

                    // Fallen in: a gap between two bents, the deck still at both banks, no post or rail over the gap.
                    List<RoofPiece> ruinPieces = Drawn(false, Condition.Ruin);
                    List<(float A0, float A1)> ruin = Boards(ruinPieces);
                    (float A0, float A1) gap = ruin.Zip(ruin.Skip(1)).Select(pair => (pair.First.A1, pair.Second.A0)).OrderByDescending(g => g.Item2 - g.Item1).FirstOrDefault();
                    bool between = bents.Zip(bents.Skip(1)).Any(pair => gap.A0 >= pair.First && gap.A1 <= pair.Second);
                    bool banks = ruin.Count > 0 && Mathf.Abs(ruin[0].A0) < 1e-3f && Mathf.Abs(ruin[^1].A1 - len) < 1e-3f;
                    bool railless = ruinPieces.Where(p => p.Part is "post" or "rail").All(p => p.Points.All(q => Along(q) <= gap.A0 + 0.1f || Along(q) >= gap.A1 - 0.1f));
                    if (gap.A1 - gap.A0 < 0.2f || !between || !banks || !railless)
                    {
                        faults.Add($"{where}, fallen in: a gap {gap.A0:0.00}–{gap.A1:0.00}, between two bents {between}, the deck at both banks {banks}, no rail over it {railless}");
                    }

                    // Stone: a pier's cutwater on both sides, only over the water, no rails, posts or stringers; the deck whole.
                    List<RoofPiece> stoneDrawn = Drawn(true, Condition.Sound);
                    List<float> piers = Bents(stoneDrawn, "pier");
                    int noses = stoneDrawn.Count(p => p.Part == "pier");
                    bool twoSided = stoneDrawn.Where(p => p.Part == "pier").GroupBy(p => (float)System.Math.Round(Along(Centre(p.Points)), 2))
                        .All(g => g.Count() == 2 && g.Any(p => Across(Centre(p.Points)) > 0f) && g.Any(p => Across(Centre(p.Points)) < 0f));
                    bool wooden = stoneDrawn.Any(p => p.Part is "rail" or "post" or "stringer" or "pile");
                    List<(float A0, float A1)> deck = Boards(stoneDrawn);
                    bool whole = deck.Count > 0 && Mathf.Abs(deck[0].A0) < 1e-3f && Mathf.Abs(deck[^1].A1 - len) < 1e-3f;
                    bool wantPiers = len > 6f;
                    if ((wantPiers && piers.Count == 0) || !twoSided || wooden || !whole || !piers.All(Wet))
                    {
                        faults.Add($"{where}, stone: {piers.Count} piers ({noses} cutwaters), both sides {twoSided}, all over the water {piers.All(Wet)}, timber in it {wooden}, the deck whole {whole}");
                    }

                    posed++;
                }
            }

            // Laid once and kept until something it depends on changes; the pieces as runs, the deck one textured run.
            var probeSignature = new BridgeSignature(new Vector2(-31.5f, 7.25f), new Vector2(-24.5f, 7.25f), false, Condition.Sound, 7);
            bool Probe(Vector2 p) => p.X > -30.5f && p.X < -25.5f;
            int before = BridgeLayings;
            BridgeCache first = CachedBridge(probeSignature, Probe);
            BridgeCache second = CachedBridge(probeSignature, Probe);
            int afterTwo = BridgeLayings - before;
            CachedBridge(probeSignature with { Drawn = Condition.Worn }, Probe);
            int afterWorn = BridgeLayings - before;
            _bridgeCache.Remove(probeSignature.Near);
            int textured = first.Runs.Count(run => run.Material is not null);
            if (!ReferenceEquals(first, second) || afterTwo != 1 || afterWorn != 2 || textured != 1)
            {
                faults.Add($"the cache laid a bridge asked twice {afterTwo} times and {afterWorn} with its condition changed (want 1, 2); {textured} textured runs (want 1)");
            }

            // With the tick off, nothing is painted.
            bool was = _watercolour;
            _watercolour = false;
            var probeBridge = new Bridge { Near = Point.Origin, Far = Point.Origin, Kind = BuildingKind.Bridge, Name = "probe" };
            bool painted = DrawPaintedBridge(null!, probeBridge, Condition.Sound);
            _watercolour = was;
            if (painted)
            {
                faults.Add("a bridge is painted with the tick off (want today's deck)");
            }

            return faults.Count == 0
                ? $"[widths] painted bridges: ✅ the two decks 1536 × 96, lossless, mipmapped, drawn with mipmaps, each mean tone 0.8; stone never worn, closed fallen in, a ruin kept open drawn failing; {posed} bridges posed at six turns, 3 and 12 tiles: the boards tiling the span inside its width, bents evenly over the water only with a pile each side, the shadow to the lower right, posts at both ends and rails both sides; failing loses boards over the water only, the same ones twice, and one run of rail; fallen in, a gap between two bents with the deck at both banks; stone with a cutwater both sides of every pier and no timber; laid once and kept until it changes, the deck one textured run; painted only with the tick"
                : $"[widths] painted bridges: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] painted bridges: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
