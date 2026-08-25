using System;
using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// What a laborer clears has to end up somewhere the village can spend it —
/// <b>for every good, not for the three a villager happens to have a field for</b>
/// (`goods-catalog.md §4.0`).
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <see cref="LaborerHarvestTests"/>, which asks the same question of timber
/// and has been green since D87. Stone and iron come off the same brush, through the same
/// <c>VillagerState.Clearing</c> branch, into the same stores — and the load in between was
/// three named integers.
/// </para>
/// <para>
/// <b>⭐ TWO ASSERTIONS, AND THE FIRST ONE IS WHY.</b> The seam has to be <em>cleared</em>
/// before its yield can be lost, so the guard proves it walked the path before it complains
/// about the end of it. Without that, an unreachable seam and a destroyed yield look
/// identical from the store.
/// </para>
/// </remarks>
public sealed class CarryingSeamGoodsTests
{
    private readonly ITestOutputHelper _output;

    public CarryingSeamGoodsTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    private static int Count(SimWorld world, Terrain terrain)
    {
        int n = 0;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] == terrain)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Paint the nearest reachable tiles of one kind of ground, cheapest first.</summary>
    /// <remarks>
    /// <b>Nearest by travel cost, because that is what <c>NearestHarvest</c> sorts by.</b>
    /// A seam on the far bank is not a long walk, it is no walk at all (D40), so painting one
    /// would test the river rather than the load.
    /// </remarks>
    private static int PaintNearestSeams(SimWorld world, Terrain terrain, int howMany)
    {
        GridPos site = world.Map.FoundingSite;
        var found = new List<(int Cost, GridPos At)>();

        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) != terrain)
                {
                    continue;
                }

                int cost = world.TravelCost.Cost(site, at);
                if (cost != TerrainCostField.Unreachable)
                {
                    found.Add((cost, at));
                }
            }
        }

        found.Sort(static (a, b) =>
            a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost)
            : a.At.Y != b.At.Y ? a.At.Y.CompareTo(b.At.Y)
            : a.At.X.CompareTo(b.At.X));

        int painted = 0;
        for (int i = 0; i < found.Count && painted < howMany; i++)
        {
            if (world.PaintHarvest(found[i].At).Allowed)
            {
                painted++;
            }
        }

        return painted;
    }

    [Theory]
    [InlineData(Terrain.Rock, Goods.Stone)]
    [InlineData(Terrain.IronDeposit, Goods.Iron)]
    public void WhatALaborerClearsReachesAStore(Terrain terrain, Goods goods)
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        int painted = PaintNearestSeams(world, terrain, 8);
        Assert.True(painted > 0, $"The valley has no reachable {terrain} to paint.");

        int seamsBefore = Count(world, terrain);
        int inStoresBefore = world.InStores(goods);

        loop.Step(config.TicksPerYear * 2);

        int seamsAfter = Count(world, terrain);
        int inStoresAfter = world.InStores(goods);

        _output.WriteLine(
            $"painted {painted} {terrain}; seams {seamsBefore} -> {seamsAfter}; "
            + $"{goods} in stores {inStoresBefore} -> {inStoresAfter}, "
            + $"on the ground {world.OnTheGround(goods)}");

        // Did the village walk the path at all? If not, the bug below is latent, not live.
        Assert.True(
            seamsAfter < seamsBefore,
            $"Two years passed with {terrain} painted for harvest and not one tile was cleared.");

        Assert.True(
            inStoresAfter > inStoresBefore,
            $"A {terrain} tile was cleared and no {goods} reached a store — "
            + $"{world.OnTheGround(goods)} is lying on the ground.");
    }
}
