using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Determinism;

/// <summary>
/// FNV-1a (64-bit) over a canonical view of sim state — a cheap witness that two
/// worlds are identical.
/// </summary>
/// <remarks>
/// <para>
/// This is a <em>fingerprint</em>, not a serializer. It exists so the determinism
/// test can compare two 10,000-tick runs with one integer comparison, and so a
/// desync (later, in co-op or replay) can be caught at the exact tick it starts
/// rather than whenever someone notices the towns diverged.
/// </para>
/// <para>
/// <b>Keep this current.</b> Every field added to <see cref="SimWorld"/> that is
/// genuinely part of the simulation must be mixed in here, in a fixed order.
/// A field that is hashed but not simulated is harmless; a field that is
/// simulated but not hashed makes the determinism test quietly weaker. When
/// save/load arrives, this should be reconciled with canonical serialization.
/// </para>
/// </remarks>
public static class StateHash
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>Fingerprint the whole world.</summary>
    public static ulong Compute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        ulong hash = FnvOffsetBasis;

        // Order matters and must never change casually — it is part of the
        // value. Append new fields at the end.
        hash = MixUInt64(hash, world.Tick);
        hash = MixUInt64(hash, world.Rng.State);
        hash = MixUInt64(hash, world.Rng.Inc);

        // ---- Village ----
        // Every villager and every household, in id order. A hash that covered only
        // the first villager would let the rest of the village desync in silence.
        hash = MixUInt32(hash, (uint)world.Villagers.Count);
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            hash = MixVillager(hash, world.Villagers[i]);
        }

        hash = MixUInt32(hash, (uint)world.Households.Count);
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            hash = MixUInt32(hash, (uint)household.Id);
            hash = MixUInt32(hash, (uint)household.Stockpile.Food);
            hash = MixUInt32(hash, (uint)household.Stockpile.LifetimeGathered);
            hash = MixUInt32(hash, (uint)household.LastBirthYear);
            hash = MixUInt32(hash, (uint)household.Stockpile.Wood);
            hash = MixUInt32(hash, (uint)household.Stockpile.LifetimeWoodCut);

            hash = MixUInt32(hash, (uint)household.MemberIds.Count);
            for (int m = 0; m < household.MemberIds.Count; m++)
            {
                hash = MixUInt32(hash, (uint)household.MemberIds[m]);
            }
        }

        hash = MixUInt32(hash, (uint)world.Workplaces.Count);
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            hash = MixUInt32(hash, (uint)workplace.Id);
            hash = MixUInt32(hash, (uint)workplace.WorkerIds.Count);
            for (int k = 0; k < workplace.WorkerIds.Count; k++)
            {
                hash = MixUInt32(hash, (uint)workplace.WorkerIds[k]);
            }
        }

        return hash;
    }

    private static ulong MixVillager(ulong hash, Villager villager)
    {
        hash = MixUInt32(hash, (uint)villager.Id);
        hash = MixUInt32(hash, (uint)villager.HouseholdId);
        hash = MixByte(hash, (byte)villager.LifeStage);
        hash = MixUInt32(hash, (uint)villager.AgeYears);
        hash = MixUInt32(hash, (uint)villager.Hunger);
        hash = MixUInt32(hash, (uint)villager.TicksAtMaxHunger);
        hash = MixByte(hash, (byte)villager.State);
        hash = MixUInt32(hash, (uint)villager.Position.X);
        hash = MixUInt32(hash, (uint)villager.Position.Y);
        hash = MixUInt32(hash, (uint)villager.ActionTicksRemaining);
        hash = MixByte(hash, villager.Alive ? (byte)1 : (byte)0);
        hash = MixByte(hash, (byte)villager.CauseOfDeath);
        hash = MixUInt64(hash, villager.DiedAtTick ?? ulong.MaxValue);
        hash = MixUInt32(hash, (uint)villager.WintersSurvived);
        hash = MixUInt32(hash, (uint)villager.TotalGathers);
        hash = MixUInt32(hash, (uint)villager.LifespanYears);
        hash = MixUInt32(hash, (uint)villager.Vigour);
        hash = MixByte(hash, (byte)villager.Stage);
        hash = MixUInt32(hash, (uint)villager.GathersThisSeason);
        hash = MixUInt32(hash, (uint)villager.BirthYear);
        hash = MixUInt32(hash, (uint)villager.PartnerId);
        hash = MixUInt32(hash, (uint)villager.WorkplaceId);
        return hash;
    }

    /// <summary>Mix eight bytes, low byte first, into the running hash.</summary>
    public static ulong MixUInt64(ulong hash, ulong value)
    {
        for (int i = 0; i < 8; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash = unchecked(hash * FnvPrime);
        }

        return hash;
    }

    /// <summary>Mix four bytes into the running hash.</summary>
    public static ulong MixUInt32(ulong hash, uint value)
    {
        for (int i = 0; i < 4; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash = unchecked(hash * FnvPrime);
        }

        return hash;
    }

    /// <summary>Mix a single byte into the running hash.</summary>
    public static ulong MixByte(ulong hash, byte value)
    {
        hash ^= value;
        return unchecked(hash * FnvPrime);
    }
}
