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
    public static ulong Compute(SimWorld world) => Compute(world, skills: true);

    /// <summary>
    /// Fingerprint the world <b>as it was before anybody could get better at anything</b> —
    /// everything except what each villager has put into a trade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THIS EXISTS BECAUSE THE OBVIOUS NO-OP GUARD CANNOT BE WRITTEN</b>
    /// (`skills-catalog.md §11.2.1`, D181). The spec asks landing 1 to be a **provable no-op:
    /// goldens unmoved** — and the goldens are full state hashes, so the moment proficiency is
    /// hashed and anybody accrues a tick they move by construction. **Hashing new state that
    /// grows and keeping a state-hash golden byte-identical are mutually exclusive**, and the
    /// spec reasoned by analogy from `crops-and-orchards.md`'s terrain values, where the
    /// generator never produced the new values so a valley genuinely hashed the same.
    /// </para>
    /// <para>
    /// <b>So the claim is made in the vocabulary that can be true: *nothing anybody DOES
    /// changed*.</b> Same positions, same stores, same births, same deaths, same tick —
    /// only the counters differ. That is a stronger statement than hash equality anyway,
    /// because it says which half moved.
    /// </para>
    /// <para>
    /// <b>⭐ And it keeps its value into landing 2, pointing the other way:</b> when mastery
    /// starts biting, **this must move** — a skill system that changes nothing is D56's
    /// clothing, and this is the guard that can say so.
    /// </para>
    /// <para>
    /// ⚠️ Precedent: `PerSiteYieldTests.MakingSoilRegionalMovedNoOtherTileInTheValley` computes a
    /// terrain-only fingerprint for exactly this reason — *"including it would make this guard
    /// say only 'the map changed', which is what the map golden already says and is not the
    /// question."*
    /// </para>
    /// </remarks>
    public static ulong ComputeIgnoringSkills(SimWorld world) => Compute(world, skills: false);

    private static ulong Compute(SimWorld world, bool skills)
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

        // Work ground (D86) — which building was given which tile to work. Sparse, in
        // the same style and for the same reason: a village where nobody has painted any
        // mixes nothing at all, so this layer is invisible to a run that does not use it
        // and the goldens do not move for existing.
        //
        // The OWNER is mixed as well as the tile, because two huts given the same forty
        // tiles the other way round is a genuinely different village and must not read as
        // the same one (D51).
        for (int i = 0; i < world.Zones.WorkGround.Count; i++)
        {
            int owner = world.Zones.WorkGround[i];
            if (owner != 0)
            {
                hash = MixUInt32(hash, (uint)i);
                hash = MixUInt32(hash, (uint)owner);
            }
        }

        // What the village means to clear (D87). Sparse, for the third time and the
        // same reason: a village that has painted nothing mixes nothing.
        //
        // NO COUNT ALONGSIDE, unlike residential above — and that is the difference
        // between this layer being invisible when unused and moving both goldens for
        // existing. The residential count is mixed unconditionally and is baked into
        // the hashes already; adding a second such line would mix a fresh zero into
        // every village that has never painted a tree. The indices determine the set
        // by themselves, so the count was only ever belt and braces.
        for (int i = 0; i < world.Zones.Harvest.Count; i++)
        {
            if (world.Zones.Harvest[i])
            {
                hash = MixUInt32(hash, (uint)i);
            }
        }

        // Stock limits (D62), and SILENCE IS THE POINT: a limit nobody has set mixes
        // nothing at all, so a village played without ever opening the control hashes
        // exactly as it did before the control existed. That is what makes
        // "the default is a no-op" a golden test rather than a promise — see
        // StockLimitTests. It is also the same shape the zone loop above already uses:
        // painted tiles are mixed, unpainted ones are not.
        //
        // The good's index goes in with its value, so two different opinions can never
        // collide, and NULL AND ZERO DIVERGE HERE: null mixes nothing, zero mixes a zero.
        // They are different instructions — "no opinion" against "stop, I mean it" — and a
        // hash that conflated them would let a determinism test pass across a real
        // divergence (D51 records the same trap one control over).
        for (int i = 0; i < StockLimits.Kinds.Count; i++)
        {
            int? limit = world.StockLimits.For(StockLimits.Kinds[i]);
            if (limit is not null)
            {
                hash = MixUInt32(hash, (uint)i);
                hash = MixUInt32(hash, (uint)limit.Value);
            }
        }

        // Profession targets (D106), in the same shape and for the same reason: a village
        // played without ever opening the panel mixes nothing at all, so the control is a
        // provable no-op until somebody uses it. Null and zero diverge here too — "no opinion"
        // and "nobody on this, I mean it" are different instructions.
        for (int i = 0; i < JobLimits.Kinds.Count; i++)
        {
            int? target = world.JobLimits.For(JobLimits.Kinds[i]);
            if (target is not null)
            {
                hash = MixUInt32(hash, (uint)i);
                hash = MixUInt32(hash, (uint)target.Value);
            }
        }

        // ---- Village ----
        // Every villager and every household, in id order. A hash that covered only
        // the first villager would let the rest of the village desync in silence.
        hash = MixUInt32(hash, (uint)world.Villagers.Count);
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            hash = MixVillager(hash, world.Villagers[i], skills);
        }

        hash = MixUInt32(hash, (uint)world.Households.Count);
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            hash = MixUInt32(hash, (uint)household.Id);
            hash = MixUInt32(hash, (uint)household.LastBirthYear);

            // The larder and what it has ever produced, every good of it — the same
            // loop MixStore uses, for the same reason.
            hash = MixStore(hash, household.Stockpile);
            for (int g = 0; g < Stockpile.Kinds; g++)
            {
                hash = MixUInt32(hash, (uint)household.Stockpile.Produced((Goods)g));
            }

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

            // Where the player put this site in the build queue (D105). SILENT UNTIL SOMEBODY
            // MOVES SOMETHING, in the same shape and for the same reason as the stock limits
            // below: a village played without ever reordering the queue must hash exactly as
            // it did before the control existed, which is what makes "the default is a no-op"
            // a golden test rather than a promise. Null and a number are different states.
            if (workplace.QueueRank is int rank)
            {
                hash = MixUInt32(hash, (uint)rank);
            }

            // And what the place is set to do (`forests-and-gathering.md`). SILENT UNTIL
            // SOMEBODY SWITCHES ONE, in the same shape and for the same reason as the queue
            // rank above and the stock limits below: a village where nobody has ever touched a
            // mode must hash exactly as it did before modes existed, which is what lets a new
            // control ship without re-taking a single golden.
            // ⚠️ AGAINST THE DEFAULT, NEVER AGAINST A MODE BY NAME (D136). This read
            // `!= WorkMode.Harvest`, which was the same thing right up until Joe made planting
            // the default — at which point every untouched forester's hut in the game would
            // have started mixing, and every golden would have moved for a control nobody had
            // used. The sentinel means *"the player has touched this"*, so it has to follow
            // whatever untouched currently is.
            //
            // ⭐ AND IT IS WHAT MADE D146 FREE. The toggle became FELLING rather than planting,
            // and the value `0` changed meaning from *fell only* to *plant only* — a rewrite of
            // what the enum says, with the numbering untouched. Because this mixes nothing at
            // all for an untouched hut, and no golden in the suite switches one, not a single
            // stored hash depends on what `0` meant.
            if (workplace.Mode != Workplace.DefaultMode)
            {
                hash = MixUInt32(hash, (uint)workplace.Mode);
            }

            hash = MixStore(hash, workplace.Store);
        }

        // ---- Storage buildings (D30) ----
        hash = MixUInt32(hash, (uint)world.StoreBuildings.Count);
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            hash = MixUInt32(hash, (uint)world.StoreBuildings[i].Id);
            hash = MixStore(hash, world.StoreBuildings[i].Store);

            // And which goods the player has told this store to take (D141). SILENT UNTIL
            // SOMEBODY SETS ONE, in the same shape and for the same reason as the queue rank
            // and the work mode above: zero is "they have not said", which is every store in
            // every village that has never used the control, so those hash exactly as they did
            // before filters existed and not one golden moves for the feature landing.
            if (world.StoreBuildings[i].AllowedGoods != 0)
            {
                hash = MixUInt32(hash, (uint)world.StoreBuildings[i].AllowedGoods);
            }
        }

        // ---- Goods on the ground (D96) ----
        // A heap is as much sim state as anything in a store — it is goods in a place, which
        // is the whole reason it can be walked to (D96, against D83's arms).
        //
        // SPARSE AND WITH NO COUNT, exactly like the harvest layer above and for exactly that
        // reason: a village that has never set anything down mixes nothing at all, so this is
        // invisible to every run that does not use it and the goldens do not move for it
        // existing. A count mixed unconditionally would put a fresh zero into every
        // established village — the mistake the residential layer made, and the one D87
        // deliberately declined to repeat.
        for (int i = 0; i < world.GroundStacks.Count; i++)
        {
            GroundStack stack = world.GroundStacks[i];
            hash = MixUInt32(hash, (uint)stack.Position.X);
            hash = MixUInt32(hash, (uint)stack.Position.Y);
            hash = MixByte(hash, (byte)stack.Goods);
            hash = MixUInt32(hash, (uint)stack.Amount);
        }

        // ---- Piles waiting on their ground to be cleared (D100) ----
        // Player intent, so it is sim state: a pile that is coming is a different world from
        // one that is not. Sparse and countless for the third time, so a village that has
        // marked none mixes nothing.
        for (int i = 0; i < world.BuildingsWaitingOnTheGround.Count; i++)
        {
            hash = MixUInt32(hash, (uint)world.BuildingsWaitingOnTheGround[i].X);
            hash = MixUInt32(hash, (uint)world.BuildingsWaitingOnTheGround[i].Y);
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
        // Every good, by index, rather than three lines naming three of them. A good
        // that is not hashed is a good two different worlds can disagree about while
        // reading identical — D51's trap, and the reason this is a loop the day stone
        // and tools arrive rather than the day somebody notices.
        for (int i = 0; i < Stockpile.Kinds; i++)
        {
            hash = MixUInt32(hash, (uint)store[(Goods)i]);
        }

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

        // What is sown where (D161). ⭐ SPARSE, AND WITH NO COUNT ALONGSIDE — the same shape as
        // the harvest zone above and for exactly the reason stated there: a village that has
        // never sown a field mixes *nothing at all*, so this layer is invisible to every run
        // that does not farm and neither golden moves for its existing. A full pass like
        // `Soil` above would mix a fresh zero per tile into every village in the game.
        //
        // The index alone determines the set, so the count was only ever belt and braces —
        // and here it would be the belt that moved the hashes.
        for (int i = 0; i < map.Crops.Count; i++)
        {
            if (map.Crops[i] != 0)
            {
                hash = MixUInt32(hash, (uint)i);
                hash = MixByte(hash, map.Crops[i]);
            }
        }

        // The forage sites and the tree stands were mixed in here, counts and all. They no
        // longer exist (`forests-and-gathering.md` slice 5), so there is nothing to mix —
        // and the woodland that replaced them is in `map.Tiles` above, which is where a fact
        // about the ground belongs. **Every tree in the valley is still hashed**; what has
        // gone is a list of eight positions that used to say which of them mattered.
        hash = MixUInt32(hash, (uint)map.FoundingSite.X);
        return MixUInt32(hash, (uint)map.FoundingSite.Y);
    }

    private static ulong MixVillager(ulong hash, Villager villager, bool skills)
    {
        hash = MixUInt32(hash, (uint)villager.Id);
        hash = MixUInt32(hash, (uint)villager.HouseholdId);
        hash = MixByte(hash, (byte)villager.LifeStage);
        hash = MixUInt32(hash, (uint)villager.AgeYears);
        hash = MixUInt32(hash, (uint)villager.Hunger);
        hash = MixUInt32(hash, (uint)villager.TicksAtMaxHunger);
        hash = MixUInt32(hash, (uint)villager.Cold);
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

        // ⭐ WHAT THEY HAVE PUT INTO EACH TRADE (`specs/skills-catalog.md §8`, Phase 3).
        // Sparse and in id order: `Villager.Skills` is kept sorted by its one door, so this
        // mixes nothing for a villager who has never held a job and cannot depend on the
        // sequence entries happened to be created in (D15).
        //
        // ⚠️ NO COUNT ALONGSIDE, on the same reasoning `Zones.Harvest` records: the ids
        // determine the set by themselves, and mixing a length would put a fresh zero into
        // every villager who has never worked — which is exactly the invisibility this
        // structure is shaped for.
        if (!skills)
        {
            return hash;
        }

        for (int i = 0; i < villager.Skills.Count; i++)
        {
            SkillProgress progress = villager.Skills[i];
            hash = MixUInt32(hash, (uint)progress.SkillId);
            hash = MixUInt32(hash, (uint)progress.Ticks);
            hash = MixUInt32(hash, (uint)progress.Work);
            hash = MixByte(hash, progress.Mastered ? (byte)1 : (byte)0);
        }

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
