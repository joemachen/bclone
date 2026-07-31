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

        // ---- The valley ----
        // The map is immutable once generated, so this can never drift mid-run — but
        // it absolutely must be here, because two runs on the same seed that generated
        // DIFFERENT worlds would otherwise agree on the hash right up until somebody
        // walked somewhere. It is also what makes the golden map test possible: a
        // known seed hashes to a known valley, so a refactor that reorders the
        // generator's draws fails the build instead of silently invalidating every
        // seed anyone has written down.
        hash = MixMap(hash, world.Map);

        // ---- What the player has asked for ----
        // Zones are a decision somebody made, so they are sim state (D42): two runs
        // given the same decisions must produce the same village. Left out, a village
        // painted differently would agree on the hash right up until it built a house.
        for (int i = 0; i < world.Zones.Residential.Count; i++)
        {
            if (world.Zones.Residential[i])
            {
                hash = MixUInt32(hash, (uint)i);
            }
        }

        hash = MixUInt32(hash, (uint)world.Zones.ResidentialTiles);

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
            hash = MixUInt32(hash, (uint)household.Stockpile.Logs);
            hash = MixUInt32(hash, (uint)household.Stockpile.LifetimeLogsFelled);
            hash = MixUInt32(hash, (uint)household.Stockpile.Firewood);
            hash = MixUInt32(hash, (uint)household.Stockpile.LifetimeFirewoodCut);

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

            // Player intent is sim state (D42's rule, D51's case): an override changes
            // who works where, so two runs of one seed that differ in it are different
            // runs and must hash differently. Null hashes distinctly from any real
            // count, so "let the village decide" is not the same state as "0".
            hash = MixUInt32(hash, workplace.StaffingOverride is int places
                ? (uint)places + 1u
                : 0u);
            hash = MixUInt32(hash, (uint)workplace.WorkerIds.Count);
            for (int k = 0; k < workplace.WorkerIds.Count; k++)
            {
                hash = MixUInt32(hash, (uint)workplace.WorkerIds[k]);
            }

            hash = MixStore(hash, workplace.Store);
        }

        // ---- Storage buildings (D30) ----
        hash = MixUInt32(hash, (uint)world.StoreBuildings.Count);
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            hash = MixUInt32(hash, (uint)world.StoreBuildings[i].Id);
            hash = MixStore(hash, world.StoreBuildings[i].Store);
        }

        return hash;
    }

    /// <summary>Mix the contents of one store.</summary>
    /// <remarks>
    /// Shared by every place that holds things, so a store added to a new kind of
    /// building cannot be left out of the hash by being written in a different style.
    /// </remarks>
    private static ulong MixStore(ulong hash, Stockpile store)
    {
        hash = MixUInt32(hash, (uint)store.Food);
        hash = MixUInt32(hash, (uint)store.Logs);
        hash = MixUInt32(hash, (uint)store.Firewood);
        return hash;
    }

    /// <summary>Fingerprint a generated valley — terrain, soil, and everything on it.</summary>
    public static ulong MixMap(ulong hash, GeneratedMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        hash = MixUInt32(hash, (uint)map.Width);
        hash = MixUInt32(hash, (uint)map.Height);
        hash = MixUInt32(hash, (uint)map.MinX);
        hash = MixUInt32(hash, (uint)map.MinY);

        for (int i = 0; i < map.Tiles.Count; i++)
        {
            hash = MixByte(hash, (byte)map.Tiles[i]);
        }

        for (int i = 0; i < map.Soil.Count; i++)
        {
            hash = MixByte(hash, map.Soil[i]);
        }

        hash = MixUInt32(hash, (uint)map.ForageSites.Count);
        for (int i = 0; i < map.ForageSites.Count; i++)
        {
            hash = MixUInt32(hash, (uint)map.ForageSites[i].X);
            hash = MixUInt32(hash, (uint)map.ForageSites[i].Y);
        }

        hash = MixUInt32(hash, (uint)map.TreeStands.Count);
        for (int i = 0; i < map.TreeStands.Count; i++)
        {
            hash = MixUInt32(hash, (uint)map.TreeStands[i].X);
            hash = MixUInt32(hash, (uint)map.TreeStands[i].Y);
        }

        hash = MixUInt32(hash, (uint)map.FoundingSite.X);
        return MixUInt32(hash, (uint)map.FoundingSite.Y);
    }

    private static ulong MixVillager(ulong hash, Villager villager)
    {
        hash = MixUInt32(hash, (uint)villager.Id);
        hash = MixUInt32(hash, (uint)villager.HouseholdId);
        hash = MixByte(hash, (byte)villager.LifeStage);
        hash = MixUInt32(hash, (uint)villager.AgeYears);
        hash = MixUInt32(hash, (uint)villager.Hunger);
        hash = MixUInt32(hash, (uint)villager.TicksAtMaxHunger);
        hash = MixUInt32(hash, (uint)villager.TicksCold);
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

        // What is in their arms, which this had been missing. Carried goods are as much
        // sim state as anything in a store — they are the goods that exist between two
        // buildings — and a village could have desynced in exactly the amount somebody
        // was holding without the determinism test noticing. Appended at the end, per
        // the note at the top of this file.
        hash = MixUInt32(hash, (uint)villager.CarriedFood);
        hash = MixUInt32(hash, (uint)villager.CarriedLogs);
        hash = MixUInt32(hash, (uint)villager.CarriedFirewood);
        hash = MixUInt32(hash, (uint)villager.ErrandHouseholdId);
        hash = MixUInt32(hash, (uint)villager.ErrandX);
        hash = MixUInt32(hash, (uint)villager.ErrandY);
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
