using System.Collections.Generic;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// Draws the village: the ground, the homes, the places people work, and the people
/// walking between them.
/// </summary>
/// <remarks>
/// <para>
/// Until this existed the game was a readout — the sim had positions, homes sat in a
/// spiral, the berry patch and tree stand had coordinates, and none of it was on
/// screen. "Watching twelve villagers is still legible" is close to unanswerable when
/// you cannot see them.
/// </para>
/// <para>
/// <b>Villagers are interpolated between ticks.</b> The sim moves them a whole tile at
/// a time, once per tick, and at one tick per second that would be a slideshow. So
/// this keeps each villager's previous tile — <em>view-side only</em>, never in sim
/// state — and lerps toward the current one using
/// <see cref="FixedTimestepDriver.Alpha"/>. That property has existed since the very
/// first commit for exactly this purpose, and this is the first thing to use it.
/// </para>
/// <para>
/// Reads sim state, never writes it (DESIGN.md §3).
/// </para>
/// </remarks>
public partial class VillageMap : Control
{
    private static readonly Color Ground = new("#2b3332");
    private static readonly Color GridLine = new("#343d3d");
    private static readonly Color HomeColour = new("#b98a52");
    private static readonly Color BerryColour = new("#5aa04a");
    private static readonly Color TreeColour = new("#2f6b3a");
    private static readonly Color AdultColour = new("#e8e2d4");
    private static readonly Color ChildColour = new("#8fc7e8");
    private static readonly Color ElderColour = new("#d9a05b");
    private static readonly Color SelectedRing = new("#f2c14e");

    private readonly Dictionary<int, Vector2> _previousTiles = new();

    private SimWorld? _world;
    private double _alpha;
    private int _selectedVillagerId;

    /// <summary>Hand the map the state to draw. Called every frame by <see cref="Main"/>.</summary>
    public void Present(SimWorld world, double alpha, int selectedVillagerId)
    {
        _world = world;
        _alpha = alpha;
        _selectedVillagerId = selectedVillagerId;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_world is null)
        {
            return;
        }

        Rect2 bounds = new(Vector2.Zero, Size);
        DrawRect(bounds, Ground);

        // Fit the whole village on screen, so a spreading settlement zooms out
        // rather than walking off the edge.
        GetWorldExtent(_world, out Vector2 min, out Vector2 max);
        float scale = FitScale(min, max, Size);
        Vector2 origin = (Size / 2f) - ((min + max) / 2f * scale);

        DrawGrid(min, max, scale, origin);

        // Workplaces first, then homes, then people on top - the things that move
        // should never be hidden behind the things that do not.
        DrawWorkplaces(_world, scale, origin);
        DrawHomes(_world, scale, origin);
        DrawVillagers(_world, scale, origin);
    }

    private void DrawGrid(Vector2 min, Vector2 max, float scale, Vector2 origin)
    {
        // Only worth drawing while tiles are big enough to read.
        if (scale < 6f)
        {
            return;
        }

        for (float x = Mathf.Floor(min.X); x <= max.X; x++)
        {
            Vector2 a = (new Vector2(x, min.Y) * scale) + origin;
            Vector2 b = (new Vector2(x, max.Y) * scale) + origin;
            DrawLine(a, b, GridLine, 1f);
        }

        for (float y = Mathf.Floor(min.Y); y <= max.Y; y++)
        {
            Vector2 a = (new Vector2(min.X, y) * scale) + origin;
            Vector2 b = (new Vector2(max.X, y) * scale) + origin;
            DrawLine(a, b, GridLine, 1f);
        }
    }

    private void DrawWorkplaces(SimWorld world, float scale, Vector2 origin)
    {
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            Vector2 centre = ToScreen(workplace.Position, scale, origin);
            Color colour = workplace.Kind == JobKind.Forager ? BerryColour : TreeColour;

            // Catchment as a faint ring: this is the "does not walk across the map"
            // rule made visible rather than merely enforced.
            //
            // Only drawn when the ring actually fits the panel. A catchment far wider
            // than the village produces an arc bigger than the window, which reads as
            // a stray line across the UI rather than as a boundary - and a boundary
            // you cannot see the shape of tells you nothing anyway.
            float radiusTiles = workplace.CatchmentRadius / (float)TravelCostField.BaseTileCost;
            float radiusPixels = radiusTiles * scale;
            if (radiusPixels <= Mathf.Max(Size.X, Size.Y))
            {
                DrawArc(centre, radiusPixels, 0f, Mathf.Tau, 64, colour with { A = 0.18f }, 1f);
            }

            DrawCircle(centre, Mathf.Max(5f, scale * 0.45f), colour);
        }
    }

    private void DrawHomes(SimWorld world, float scale, Vector2 origin)
    {
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            bool occupied = world.LivingMembersOf(household) > 0;

            Vector2 centre = ToScreen(household.HomePosition, scale, origin);
            float size = Mathf.Max(6f, scale * 0.6f);
            var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);

            // A house whose family has died still stands, and reads as abandoned.
            DrawRect(rect, occupied ? HomeColour : HomeColour with { A = 0.25f });
        }
    }

    private void DrawVillagers(SimWorld world, float scale, Vector2 origin)
    {
        var stillAlive = new HashSet<int>();

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            stillAlive.Add(villager.Id);

            var current = new Vector2(villager.Position.X, villager.Position.Y);
            Vector2 previous = _previousTiles.TryGetValue(villager.Id, out Vector2 known) ? known : current;

            // Lerp from where they were to where they are. If they moved more than a
            // tile - being born, or moving house - snap instead, or they would glide
            // across the map.
            Vector2 drawTile = previous.DistanceSquaredTo(current) > 2f
                ? current
                : previous.Lerp(current, (float)_alpha);

            if (current != previous && _alpha >= 0.999)
            {
                _previousTiles[villager.Id] = current;
            }
            else if (!_previousTiles.ContainsKey(villager.Id))
            {
                _previousTiles[villager.Id] = current;
            }

            Vector2 centre = ToScreen(drawTile, scale, origin);
            float radius = Mathf.Max(3f, scale * 0.22f);

            Color colour = villager.LifeStage switch
            {
                LifeStage.Child => ChildColour,
                LifeStage.Elder => ElderColour,
                _ => AdultColour,
            };

            DrawCircle(centre, radius, colour);

            if (villager.Id == _selectedVillagerId)
            {
                DrawArc(centre, radius + 4f, 0f, Mathf.Tau, 24, SelectedRing, 2f);
            }
        }

        PruneTheDead(stillAlive);
    }

    /// <summary>Forget interpolation state for the dead, so ids can never be confused
    /// and the dictionary does not grow for the whole run.</summary>
    private void PruneTheDead(HashSet<int> stillAlive)
    {
        if (_previousTiles.Count <= stillAlive.Count)
        {
            return;
        }

        var gone = new List<int>();
        foreach (KeyValuePair<int, Vector2> entry in _previousTiles)
        {
            if (!stillAlive.Contains(entry.Key))
            {
                gone.Add(entry.Key);
            }
        }

        foreach (int id in gone)
        {
            _previousTiles.Remove(id);
        }
    }

    // ---------------------------------------------------------------

    private static Vector2 ToScreen(GridPos tile, float scale, Vector2 origin) =>
        (new Vector2(tile.X, tile.Y) * scale) + origin;

    private static Vector2 ToScreen(Vector2 tile, float scale, Vector2 origin) =>
        (tile * scale) + origin;

    /// <summary>Bounding box of everything worth showing, with a tile of margin.</summary>
    private static void GetWorldExtent(SimWorld world, out Vector2 min, out Vector2 max)
    {
        float minX = 0f, minY = 0f, maxX = 0f, maxY = 0f;

        void Include(GridPos p)
        {
            minX = Mathf.Min(minX, p.X);
            minY = Mathf.Min(minY, p.Y);
            maxX = Mathf.Max(maxX, p.X);
            maxY = Mathf.Max(maxY, p.Y);
        }

        for (int i = 0; i < world.Households.Count; i++)
        {
            Include(world.Households[i].HomePosition);
        }

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Include(world.Workplaces[i].Position);
        }

        min = new Vector2(minX - 2f, minY - 2f);
        max = new Vector2(maxX + 2f, maxY + 2f);
    }

    private static float FitScale(Vector2 min, Vector2 max, Vector2 viewport)
    {
        Vector2 span = max - min;
        if (span.X <= 0f || span.Y <= 0f || viewport.X <= 0f || viewport.Y <= 0f)
        {
            return 16f;
        }

        return Mathf.Min(viewport.X / span.X, viewport.Y / span.Y);
    }
}
