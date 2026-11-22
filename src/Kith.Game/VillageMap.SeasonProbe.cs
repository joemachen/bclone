using Kith.Sim.Core;
using Kith.Sim.World;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>season shown:</c> line (A189, <c>title-screen-art.md §3</c>): the render-only season override the title's
    /// capture tool draws its four seasons with. Named from the spec's table, never read back from the code's own lists.
    /// </summary>
    /// <remarks>⚠️ Draw-time only beyond this, so the captures are its evidence: the grass, trees and fields as each season draws them.</remarks>
    internal string TheSeasonShownHolds()
    {
        try
        {
            var faults = new List<string>();
            if (_world is not SimWorld world)
            {
                return "[widths] season shown: ⛔ no village to ask";
            }

            int per = world.Config.TicksPerSeason;
            double real = (per * 4 * 7) + (per * 1.5);

            // Nothing shown: the real tick, the tile's own stage, the village's own season — play is untouched.
            if (SeasonTickAt(real, per, null) != real
                || FieldStageShown(Terrain.Sown, null) != Terrain.Sown || FieldStageShown(Terrain.Ripe, null) != Terrain.Ripe
                || SeasonOf(null, Season.Summer) != Season.Summer)
            {
                faults.Add("with no season shown the view does not draw the real one");
            }

            // A season shown: that phase of the same year, the fields by it (spring and summer sown, autumn ripe, winter stubble),
            // the smoke by it.
            foreach ((float shown, Season season, Terrain field) in new[]
            {
                (0.3f, Season.Spring, Terrain.Sown), (1.3f, Season.Summer, Terrain.Sown), (2.3f, Season.Fall, Terrain.Ripe),
                (3.3f, Season.Winter, Terrain.Field), (3.86f, Season.Winter, Terrain.Field),
            })
            {
                double at = SeasonTickAt(real, per, shown);
                bool sameYear = Math.Floor(at / (per * 4.0)) == Math.Floor(real / (per * 4.0));
                bool phase = Math.Abs((at % (per * 4.0)) - (shown * per)) < per * 1e-3;
                if (!sameYear || !phase || SeasonOf(shown, Season.Summer) != season || FieldStageShown(Terrain.Sown, shown) != field
                    || FieldStageShown(Terrain.Field, shown) != field)
                {
                    faults.Add($"shown {shown}: tick {at} (same year {sameYear}, phase {phase}), season {SeasonOf(shown, Season.Summer)}, a field drawn {FieldStageShown(Terrain.Sown, shown)} (want {season}, {field})");
                }
            }

            // Drawn on the village: winter shown lies snow and none falls; summer shown lies none; the world's hash unmoved.
            ulong before = Kith.Sim.Determinism.StateHash.Compute(world);
            float? was = SeasonShown;
            SeasonShown = 3.3f;
            SnowNow winter = TheSnowNow();
            SeasonShown = 1.3f;
            SnowNow summer = TheSnowNow();
            SeasonShown = was;
            ulong after = Kith.Sim.Determinism.StateHash.Compute(world);
            if (winter.Falling != 0f || winter.Lying <= 0f || summer.Lying != 0f || summer.Falling != 0f || before != after)
            {
                faults.Add($"winter shown lies {winter.Lying:F2} and falls {winter.Falling:F2}, summer lies {summer.Lying:F2} (want snow lying and none falling, then none); the world {(before == after ? "unmoved" : "MOVED")}");
            }

            return faults.Count == 0
                ? "[widths] season shown: ✅ nothing shown draws the real season; a season shown draws that phase of the same year, the fields by it (spring and summer sown, autumn ripe, winter stubble) and the smoke by it; winter shown lies snow with none falling, summer none; the world's hash unmoved"
                : $"[widths] season shown: ⛔ {string.Join("; ", faults)}";
        }
        catch (Exception e)
        {
            return $"[widths] season shown: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
