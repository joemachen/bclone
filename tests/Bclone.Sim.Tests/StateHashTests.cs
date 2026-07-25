using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Xunit;

namespace Bclone.Sim.Tests;

public sealed class StateHashTests
{
    private static SimWorld NewWorld(ulong seed = 1UL) =>
        SimWorld.Create(new SimConfig { Seed = seed }, new InMemoryLogSink());

    [Fact]
    public void IdenticalWorlds_HashEqual()
    {
        Assert.Equal(StateHash.Compute(NewWorld()), StateHash.Compute(NewWorld()));
    }

    [Fact]
    public void TickDifference_ChangesTheHash()
    {
        var a = NewWorld();
        var b = NewWorld();
        new SimLoop(b).StepOnce();

        Assert.NotEqual(StateHash.Compute(a), StateHash.Compute(b));
    }

    [Fact]
    public void RngDifference_ChangesTheHash()
    {
        var a = NewWorld();
        var b = NewWorld();
        b.Rng.NextUInt();

        Assert.NotEqual(StateHash.Compute(a), StateHash.Compute(b));
    }

    [Fact]
    public void SeedDifference_ChangesTheHash()
    {
        Assert.NotEqual(StateHash.Compute(NewWorld(1UL)), StateHash.Compute(NewWorld(2UL)));
    }

    [Fact]
    public void Hash_IsNotTheEmptyBasis()
    {
        // Catches the failure where Compute forgets to mix anything in and every
        // world hashes to the same constant — which would make the determinism
        // suite pass forever while proving nothing.
        const ulong FnvOffsetBasis = 14695981039346656037UL;
        Assert.NotEqual(FnvOffsetBasis, StateHash.Compute(NewWorld()));
    }

    [Fact]
    public void Hash_IsStableAcrossRepeatedCalls()
    {
        var world = NewWorld();
        ulong first = StateHash.Compute(world);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(first, StateHash.Compute(world));
        }
    }

    [Fact]
    public void Mixers_AreOrderSensitive()
    {
        const ulong Basis = 14695981039346656037UL;

        ulong ab = StateHash.MixUInt64(StateHash.MixUInt64(Basis, 1UL), 2UL);
        ulong ba = StateHash.MixUInt64(StateHash.MixUInt64(Basis, 2UL), 1UL);

        Assert.NotEqual(ab, ba);
    }

    [Fact]
    public void Mixers_DistinguishAdjacentValues()
    {
        const ulong Basis = 14695981039346656037UL;

        Assert.NotEqual(StateHash.MixUInt64(Basis, 0UL), StateHash.MixUInt64(Basis, 1UL));
        Assert.NotEqual(StateHash.MixUInt32(Basis, 0U), StateHash.MixUInt32(Basis, 1U));
        Assert.NotEqual(StateHash.MixByte(Basis, 0), StateHash.MixByte(Basis, 1));
    }

    [Fact]
    public void Compute_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => StateHash.Compute(null!));
    }
}
