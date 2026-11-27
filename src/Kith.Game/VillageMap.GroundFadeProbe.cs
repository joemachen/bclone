using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>ground fade:</c> line (A300, <c>ground-fade.md §3</c>) — printed with the people's lines. The spec's numbers:
    /// a fade over 2.25 ticks, eased, half-way at half; the "from" side the floor as it showed; a change mid-fade baked once.
    /// </summary>
    /// <remarks>
    /// The probe's valley is changed with <c>map.SetTerrain</c> and put back, as <c>tree motion:</c> does, the bake refreshed with a
    /// generation of its own. ⚠️ Nothing is drawn headless (D340): what the layer shows is the uniform and the textures, read here.
    /// </remarks>
    private string TheGroundFadeHolds()
    {
        if (_world is not SimWorld world)
        {
            return "[widths] ground fade: ⛔ no world to draw";
        }

        GeneratedMap map = world.Map;
        var faults = new List<string>();
        var changed = new Dictionary<GridPos, Terrain>();
        bool wasOn = _watercolour;
        int generation = world.TerrainGeneration + 1_000_000;
        try
        {
            ShowWatercolour(true);
            if (_watercolourMaterial is not ShaderMaterial material)
            {
                return "[widths] ground fade: ⛔ no watercolour layer";
            }

            // The shader declares what the map sets.
            var names = material.Shader.GetShaderUniformList().Select(u => u.AsGodotDictionary()["name"].AsString()).ToHashSet();
            foreach (string uniform in new[] { "ground_fade", "ground_map_from", "ground_map_shares_from" })
            {
                if (!names.Contains(uniform))
                {
                    faults.Add($"valley_terrain.gdshader declares no {uniform}");
                }
            }

            // The curve: 0 at the change, a half at half, 1 at 2.25 ticks and after.
            if (GroundFadeAfter(0) != 0f || Mathf.Abs(GroundFadeAfter(1.125) - 0.5f) > 1e-4f || GroundFadeAfter(2.25) != 1f || GroundFadeAfter(double.PositiveInfinity) != 1f)
            {
                faults.Add($"the fade {GroundFadeAfter(0):F2}, {GroundFadeAfter(1.125):F2}, {GroundFadeAfter(2.25):F2} at 0, 1.125 and 2.25 ticks (want 0, 0.5, 1)");
            }

            _valley.Refresh(world, generation: generation++);
            FadeTheGround(material, 1000.0);
            FadeTheGround(material, 1000.0);
            int fades = GroundFades, mids = GroundFadeMidBakes;
            if (Seen() != 1f)
            {
                faults.Add($"with nothing changed the ground shows {Seen():F2} of the new (want 1)");
            }

            byte[] before = (byte[])_valley.WeightsBytes!.Clone();
            GridPos first = FindForest(null);
            Set(first, Terrain.Grass);
            _valley.Refresh(world, generation: generation++);
            byte[] afterFirst = (byte[])_valley.WeightsBytes!.Clone();
            FadeTheGround(material, 1000.0);
            if (GroundFades != fades + 1 || Seen() != 0f || !_groundWas!.AsSpan().SequenceEqual(before) || !_groundShown!.AsSpan().SequenceEqual(afterFirst)
                || before.AsSpan().SequenceEqual(afterFirst))
            {
                faults.Add($"a fell: {GroundFades - fades} fades, showing {Seen():F2} of the new, from the floor as it was {_groundWas!.AsSpan().SequenceEqual(before)}, to the new {_groundShown!.AsSpan().SequenceEqual(afterFirst)} (want 1, 0, true, true)");
            }

            FadeTheGround(material, 1001.125);
            float half = Seen();
            FadeTheGround(material, 1002.25);
            float whole = Seen();
            if (Mathf.Abs(half - 0.5f) > 1e-4f || whole != 1f || GroundFades != fades + 1)
            {
                faults.Add($"the fell's fade {half:F2} at half, {whole:F2} at its end, {GroundFades - fades} started (want 0.5, 1, 1)");
            }

            // A second change half-way through a fade: what was on screen is baked into the "from" side, once.
            FadeTheGround(material, 2000.0);
            GridPos second = FindForest(first);
            Set(second, Terrain.Grass);
            _valley.Refresh(world, generation: generation++);
            int startAt = GroundFades;
            FadeTheGround(material, 2000.0);
            Set(first, Terrain.Forest);
            _valley.Refresh(world, generation: generation++);
            byte[] onScreen = LerpPathField(_groundWas!, _groundShown!, 0.5f);
            FadeTheGround(material, 2001.125);
            if (GroundFadeMidBakes != mids + 1 || GroundFades != startAt + 2 || !_groundWas!.AsSpan().SequenceEqual(onScreen) || Seen() != 0f)
            {
                faults.Add($"a change mid-fade: {GroundFadeMidBakes - mids} baked, {GroundFades - startAt} started, from what showed {_groundWas!.AsSpan().SequenceEqual(onScreen)}, showing {Seen():F2} (want 1, 2, true, 0)");
            }

            // Off and on again: the floor is drawn as it is, no fade from a stale one.
            ShowWatercolour(false);
            Set(second, Terrain.Forest);
            _valley.Refresh(world, generation: generation++);
            ShowWatercolour(true);
            int offAt = GroundFades;
            FadeTheGround(material, 3000.0);
            if (GroundFades != offAt || Seen() != 1f)
            {
                faults.Add($"on again after a change while off: {GroundFades - offAt} fades, showing {Seen():F2} (want 0, 1)");
            }

            return faults.Count == 0
                ? $"[widths] ground fade: ✅ a fell fades the floor from as it was to the new over 2.25 ticks, eased, half at half; nothing changed, nothing fades; a change mid-fade bakes what showed into the \"from\" side once; on again after a change while off, no fade; the shader declares its three (a start {LastGroundFadeMs:F1} ms)"
                : $"[widths] ground fade: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] ground fade: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
        finally
        {
            foreach ((GridPos tile, Terrain was) in changed)
            {
                map.SetTerrain(tile, was);
            }

            _valley.Refresh(world, generation: generation);
            _groundOf = null;
            ShowWatercolour(wasOn);
        }

        float Seen() => _watercolourMaterial!.GetShaderParameter("ground_fade").AsSingle();

        void Set(GridPos tile, Terrain to)
        {
            changed.TryAdd(tile, map.TerrainAt(tile));
            map.SetTerrain(tile, to);
        }

        GridPos FindForest(GridPos? awayFrom)
        {
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                var tile = new GridPos(map.MinX + (i % map.Width), map.MinY + (i / map.Width));
                if (map.Tiles[i] == Terrain.Forest && (awayFrom is not GridPos far || System.Math.Abs(tile.X - far.X) + System.Math.Abs(tile.Y - far.Y) > 8))
                {
                    return tile;
                }
            }

            throw new System.InvalidOperationException("the probe's valley has no wood to fell");
        }
    }
}
