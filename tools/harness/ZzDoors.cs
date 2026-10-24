using Bclone.Sim.Core;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D619's MEASUREMENT — the doors spec's premise (`specs/doors.md §3`). Kept in tools/harness/, COPIED into
// tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit (D420).
// For every standing building on a folder of saves: the tile in front of its facing (the house's own door rule,
// `PlotShape`: centre + (0, −(H/2 + ½)) turned by the facing) — is it walkable, unbuilt, unwalled, reached from the
// village; if not, is another side open; and how far is it against today's walk to the building itself (from the
// first home or the founding site)? A building that is both a workplace and a store is counted once.
// Run: copy the saves to a scratch folder (never read Joe's own folder in place), cp this in, then
//   ZZ_SAVES=<folder> dotnet test tests/Bclone.Sim.Tests -c Release --filter "FullyQualifiedName~ZzDoors" --logger "console;verbosity=detailed"
// One ZZD line a save, one ZZD-B line a blocked front.
public sealed class ZzDoors
{
    private readonly ITestOutputHelper _o;

    public ZzDoors(ITestOutputHelper o) => _o = o;

    public static IEnumerable<object[]> Saves() =>
        Directory.GetFiles(Environment.GetEnvironmentVariable("ZZ_SAVES")!, "*.save").Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Saves))]
    public void Measure(string path)
    {
        SaveOpened opened = SaveFile.Open(path, ShippedConfig.Load());
        Assert.True(opened.Loop is not null, opened.Refusal);
        SimWorld world = opened.Loop!.World;

        var shapes = new List<(string Name, Footprint Shape)>();
        foreach (Household h in world.Households)
        {
            if (world.HomeFootprintOf(h) is Footprint home)
            {
                shapes.Add(("home", home));
            }
        }

        foreach (Workplace w in world.Workplaces)
        {
            if (!SimWorld.IsABridgeSite(w) && !SimWorld.IsARepairSite(w))
            {
                shapes.Add((w.Kind.ToString(), w.Footprint));
            }
        }

        shapes.AddRange(world.Libraries.Select(l => ("library", l.Footprint)));
        if (world.TownHall is not null)
        {
            shapes.Add(("hall", world.TownHall.Footprint));
        }

        shapes.AddRange(world.Wells.Select(w => ("well", w.Footprint)));
        shapes.AddRange(world.StoreBuildings.Select(s => (s.Kind.ToString(), s.Footprint)));

        shapes = shapes.GroupBy(s => (s.Shape.Origin, s.Shape.Width, s.Shape.Height)).Select(g => g.First()).ToList();
        int sideOpen = 0, noSide = 0;
        GridPos from = world.FirstHomeOrFoundingSite();
        int water = 0, built = 0, walled = 0, unreached = 0, ok = 0, zero = 0, diagonal = 0;
        long costAnchor = 0, costDoor = 0;
        int priced = 0;
        foreach ((string name, Footprint shape) in shapes)
        {
            if (shape.Facing == Angle.Zero)
            {
                zero++;
            }

            Fixed reach = Fixed.FromRatio(shape.Height + 1, 2);
            Fixed inside = Fixed.FromRatio(shape.Height - 1, 2);
            GridPos step = (shape.Origin + new Point(Fixed.Zero, -reach).RotatedBy(shape.Facing)).ToTile();
            GridPos door = (shape.Origin + new Point(Fixed.Zero, -inside).RotatedBy(shape.Facing)).ToTile();
            int dx = step.X - door.X, dy = step.Y - door.Y;
            bool adjacent = Math.Abs(dx) + Math.Abs(dy) == 1;
            if (!adjacent)
            {
                diagonal++;
            }

            string why;
            if (!world.IsWalkable(step))
            {
                water++;
                why = "not walkable";
            }
            else if (world.StandsOn(step))
            {
                built++;
                why = "built on";
            }
            else if (adjacent && (world.WallsOn(door) & WallBit(dx, dy)) != 0)
            {
                walled++;
                why = "walled";
            }
            else if (!world.TravelCost.CanReach(from, step))
            {
                unreached++;
                why = "unreached";
            }
            else
            {
                ok++;
                why = "ok";
                int a = world.TravelCost.Cost(from, shape.Origin.ToTile());
                int d = world.TravelCost.Cost(from, step);
                if (a != TravelCostField.Unreachable)
                {
                    costAnchor += a;
                    costDoor += d;
                    priced++;
                }
            }

            if (why != "ok")
            {
                bool any = false;
                foreach (int quarter in new[] { 1, 3, 2 })
                {
                    Angle turned = shape.Facing + Angle.FromTurnFraction(quarter, 4);
                    Fixed half = Fixed.FromRatio((quarter == 2 ? shape.Height : shape.Width) + 1, 2);
                    GridPos s2 = (shape.Origin + new Point(Fixed.Zero, -half).RotatedBy(turned)).ToTile();
                    if (world.IsWalkable(s2) && !world.StandsOn(s2) && world.TravelCost.CanReach(from, s2))
                    {
                        any = true;
                        break;
                    }
                }

                if (any) { sideOpen++; } else { noSide++; }
                why += any ? " (another side open)" : " (NO side open)";
                _o.WriteLine($"ZZD-B {Path.GetFileName(path)} {name} {shape.Width}x{shape.Height} at {shape.Origin.ToTile()} step {step}: {why}");
            }
        }

        _o.WriteLine($"ZZD {Path.GetFileName(path)} buildings {shapes.Count} ok {ok} water {water} built {built} walled {walled} " +
                     $"unreached {unreached} side-open {sideOpen} no-side {noSide} facing-zero {zero} non-adjacent {diagonal} cost anchor {costAnchor} door {costDoor} over {priced}");
    }

    // N=1 E=2 S=4 W=8, −y is north (ZoneMap.EdgeBit).
    private static byte WallBit(int dx, int dy) => (dx, dy) switch
    {
        (0, -1) => 1,
        (1, 0) => 2,
        (0, 1) => 4,
        _ => 8,
    };
}
