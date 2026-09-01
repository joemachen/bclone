using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// The village's civic life — <b>what it does about itself rather than about the valley</b>
/// (`specs/town-hall.md`, D252).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ ONE THING TODAY: the last founder dies, and the village raises a hall in their name.</b>
/// It is its own system rather than a branch of <see cref="KnowledgeSystem"/> because D251 is
/// emphatic that <em>the town hall is not a knowledge building with extras</em> — knowledge is
/// about a fifth of what it is — and because <b>nomads land here next</b> (`DESIGN.md §5`: the
/// town hall is what triggers them). *A system named for what it does keeps the next feature from
/// being smuggled into a system named for something else.*
/// </para>
/// <para>
/// <b>⛔ IT RUNS AFTER <see cref="MortalitySystem"/>, AND THE ORDER IS THE CONTRACT.</b> The
/// trigger is a death, so it has to read a world in which that death has already happened —
/// exactly as <see cref="KnowledgeSystem"/> does for a technique whose last knower has just died.
/// Moving it earlier delays every founding moment by one tick, which is a behavioural change and
/// must be treated as one (<see cref="ISimSystem"/>).
/// </para>
/// <para>
/// <b>⚠️ It touches no RNG and reads no clock but the sim's.</b> Nothing here can shift the draw
/// order of anything else, which is what keeps a village that never reaches the trigger
/// byte-identical to a village from before this system existed.
/// </para>
/// </remarks>
public sealed class CivicSystem : ISimSystem
{
    public string Name => "civic";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        NoticeIfTheFoundersAreAllGone(world);
    }

    /// <summary>
    /// Say it, once, on the tick the last of the founders stops being alive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THE TRIGGER IS A FUNERAL, AND THAT IS THE DESIGN RATHER THAN A CONVENIENCE</b>
    /// (D252, Joe: *"town hall trigger should be when the last founder dies. the gift is given as
    /// a tribute/monument to founding members"*). The alternative on the table was *the founders
    /// being outnumbered by the native-born* — <b>a ratio that crosses in silence on an arbitrary
    /// tick</b>, so the game would have had to invent a notice for it. A death is already
    /// narrated, already has a name and an age on it, and is the most-read line in the village
    /// log. <em>Generational time as the trigger for a generational-management building</em>
    /// (`DESIGN.md §1`) needs a generational moment, and a funeral is the only one this game has.
    /// </para>
    /// <para>
    /// <b>⛔ SOMEBODY HAS TO BE LEFT ALIVE, AND THIS IS NOT A TIDY-UP.</b> The last founder dying
    /// in an empty valley is <em>the village ending</em>, not the village outgrowing anybody —
    /// D143 rules that an unattended valley is supposed to die out. <b>A monument raised for a
    /// village that no longer exists is a message to a corpse.</b>
    /// </para>
    /// <para>
    /// ⚠️ <b>An unlucky founding fires it early and that was accepted on purpose</b>
    /// (`specs/town-hall.md §3.1`). Four founders lost to one hard winter in year 6 is a valid
    /// trigger: the building is <em>for the founders</em>, so it arriving when they are gone is
    /// correct whether the village is thriving or reeling.
    /// </para>
    /// </remarks>
    private static void NoticeIfTheFoundersAreAllGone(SimWorld world)
    {
        if (world.SaidTheFoundersAreGone)
        {
            return;
        }

        // ⚠️ Asked of the roster rather than of a counter, for the reason `KnowledgeSystem`'s
        // header gives one system over: a derived answer has no code path that can forget to fire.
        bool anyFounderAlive = false;
        bool anybodyAlive = false;
        bool anyFounderAtAll = false;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            anyFounderAtAll |= villager.Founder;

            if (!villager.Alive)
            {
                continue;
            }

            anybodyAlive = true;
            anyFounderAlive |= villager.Founder;
        }

        // ⛔ A WORLD WITH NO FOUNDERS AT ALL MUST NOT TRIP THIS, AND IT IS THE FIRST THING A
        // FIXTURE GETS WRONG. A test world posed with hand-built villagers has nobody marked, so
        // "no founder is alive" is vacuously true from tick 1 — the town hall would arrive in an
        // empty valley before anybody had done anything. *An empty predicate is D157's
        // green-and-blind, one system over.*
        if (!anyFounderAtAll || anyFounderAlive || !anybodyAlive)
        {
            return;
        }

        world.SaidTheFoundersAreGone = true;
        world.ATownHallIsOwed = true;

        world.RaiseMoment(
            "The last of the founders is gone",
            TributeTo(world),

            // ⭐ IT WAITS, LIKE THE LIBRARY'S GIFT AND UNLIKE A DISCOVERY (`Moment.WaitsToBeDismissed`). The
            // test is *"is there anything to do about it?"* and there is: somewhere to put a
            // building. At 4× an unpaused panel slides past unread (D232).
            stops: true,

            // The death itself is already a `Death` line from `MortalitySystem`. This one is the
            // village's memory of itself, which is what `Discovery` is for — and it is where the
            // library's gift went, for the same reason.
            category: LogCategory.Discovery);
    }

    /// <summary>
    /// The sentence the village says over its founders — <b>every one of them, by name</b>.
    /// </summary>
    /// <remarks>
    /// <b>⭐⭐ THIS IS WHY THE BUILDING IS FREE, AND IT IS NOT DECORATION.</b> The library is a gift
    /// because fifteen years of granary-keeping bought it; <b>a town hall is a gift because nobody
    /// sells you a monument to your own dead</b> (D252). ⛔ <b>A tribute that cannot name who it is
    /// for is a stats screen with a plaque on it</b> — which is the whole reason
    /// <see cref="Villager.Founder"/> is marked rather than derived, and why the dead stay in
    /// <c>world.Villagers</c> where this can still reach them.
    /// </remarks>
    private static string TributeTo(SimWorld world)
    {
        Villager? last = null;
        var others = new List<string>();

        // ⚠️ In villager id order, which is founding order — an unordered tie in a player-facing
        // sentence is a desync waiting to happen (D15), and this one is read by a golden.
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Founder)
            {
                continue;
            }

            if (last is null || villager.DiedAtTick > last.DiedAtTick)
            {
                if (last is not null)
                {
                    others.Add(last.Name);
                }

                last = villager;
                continue;
            }

            others.Add(villager.Name);
        }

        if (last is null)
        {
            return string.Empty;
        }

        int living = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Alive)
            {
                living++;
            }
        }

        string beside = others.Count switch
        {
            0 => string.Empty,
            1 => $" {others[0]} went before them.",
            _ => $" {string.Join(", ", others.Take(others.Count - 1))} and "
                + $"{others[^1]} went before them.",
        };

        // ⭐ THE CLAIM THAT MAKES IT A MOMENT RATHER THAN AN OBITUARY: everybody left was born
        // here. That is true by construction the tick the last founder dies, and saying it out
        // loud is what turns four deaths into *the village outgrowing its founders* (D251).
        string whoIsLeft = living == 1
            ? "The one soul still here was born in this valley."
            : $"All {living} souls still here were born in this valley.";

        return $"{last.Name} was the last of the four who walked into this valley with a cart "
            + $"and no roof, and is dead at {last.AgeYears}.{beside} {whoIsLeft} "
            + "The village has gathered timber and stone for a hall in their name — put it "
            + $"wherever you like, and it will cost you nothing. {world.Clock.SeasonAndYear()}.";
    }
}
