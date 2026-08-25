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

    /// <summary>
    /// ⭐⭐ And a cold start's cleared stone reaches somewhere the village can spend it (D217).
    /// </summary>
    /// <remarks>
    /// <b>Joe, playing:</b> <em>"while villagers harvest stone on the map, they didn'''t put any
    /// stone in the storage pile, and the UI always showed 0 stone."</em> He was playing
    /// <c>main</c>, where D211 had not landed and a cleared seam was simply destroyed — but the
    /// report named a store the guards above never used, so it gets its own.
    /// <para>
    /// The tests above run a warm-start village, which has a shed. <b>A founding has a stockpile
    /// and a cart</b>, and a stockpile holds a good by <em>being</em> a stockpile
    /// (<c>KindAccepts</c>: <c>Kind == StoreKind.Pile || …</c>) rather than by the catalogue
    /// listing it — a different question, asked here.
    /// </para>
    /// <para>
    /// ⚠️ <b>IT ASSERTS "a store", NOT "the stockpile", AND THE MEASUREMENT IS WHY.</b> Three
    /// years into a played opening the stockpile is <b>91/91 — completely full of timber</b> — so
    /// the 42 stone the village cleared is correctly in the cart instead. **A stockpile showing
    /// zero stone is not the bug Joe saw; a village showing zero stone was.** Pinning this guard
    /// to one building would make it a test about which store happened to have room.
    /// </para>
    /// </remarks>
    [Fact]
    public void ClearedStoneReachesTheStockpileOfAColdStart()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 3);

        StoreBuilding? pile = null;
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            if (world.StoreBuildings[i].Kind == StoreKind.Pile)
            {
                pile = world.StoreBuildings[i];
                break;
            }
        }

        Assert.NotNull(pile);
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding st = world.StoreBuildings[i];
            _output.WriteLine(
                $"  {st.Name} ({st.Kind}) at {st.Position}: {st.Store[Goods.Stone]} stone, "
                + $"{st.Store.Held}/{st.Store.Capacity} held, accepts={st.Accepts(Goods.Stone)}");
        }

        _output.WriteLine(
            $"{pile!.Name}: {pile.Store[Goods.Stone]} stone; "
            + $"village total {world.InStores(Goods.Stone)}");

        Assert.True(pile.Accepts(Goods.Stone), "A stockpile is meant to take anything.");
        Assert.True(
            world.InStores(Goods.Stone) > 0,
            "A played opening cleared its seam and no stone reached any store.");
        Assert.Equal(0, world.OnTheGround(Goods.Stone));
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
