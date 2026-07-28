using Bclone.Sim.Config;

namespace Bclone.Sim.World;

/// <summary>What the player can put up.</summary>
/// <remarks>
/// Deliberately a small closed set rather than "any building". Forage sites and tree
/// stands are terrain — you find them, you do not build them — and homes are not here
/// either, because the player paints a residential zone and the village builds inside
/// it (D42) rather than placing houses one at a time.
/// </remarks>
public enum BuildingKind
{
    Granary = 0,
    Shed = 1,
    Market = 2,
    WoodcutterHut = 3,
}

/// <summary>What a building costs to raise.</summary>
/// <remarks>
/// <b>Data, not code</b> — the recipe comes out of config so a modder can change what a
/// granary costs without touching the sim (DESIGN.md §3). The two numbers are separate
/// on purpose: logs are what the village must *have*, and work is what it must *spend*,
/// and a building that is expensive in one and cheap in the other is a different
/// decision from one that is expensive in both.
/// </remarks>
public readonly record struct BuildingRecipe(int Logs, int WorkTicks)
{
    /// <summary>The recipe for a kind, read from config.</summary>
    public static BuildingRecipe For(BuildingKind kind, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return kind switch
        {
            BuildingKind.Granary => new BuildingRecipe(config.GranaryLogs, config.GranaryWorkTicks),
            BuildingKind.Shed => new BuildingRecipe(config.ShedLogs, config.ShedWorkTicks),
            BuildingKind.Market => new BuildingRecipe(config.MarketLogs, config.MarketWorkTicks),
            _ => new BuildingRecipe(config.HutLogs, config.HutWorkTicks),
        };
    }
}

/// <summary>
/// A building that has been decided on but not yet raised.
/// </summary>
/// <remarks>
/// <para>
/// <b>A building does not appear the instant it is paid for</b> (D43). The player marks
/// a site, materials are carried to it, somebody builds it, and only then is it a
/// building. That is the same claim D14 makes about distribution and D29 about
/// processing: things that happen in this village are work somebody does.
/// </para>
/// <para>
/// It buys three things. A half-built granary is legible on the map — you can see what
/// the village is spending itself on. Building competes for hands with eating, so
/// placement is a real trade rather than a purchase. And §2.6's desire paths get a
/// burst of traffic to a spot the player chose.
/// </para>
/// <para>
/// The site is carried by a <see cref="Workplace"/> of kind
/// <see cref="JobKind.Builder"/>, so it inherits labour allocation, catchment and
/// refusal reasons rather than growing a second system that does the same things
/// slightly differently.
/// </para>
/// </remarks>
public sealed class ConstructionSite
{
    public required BuildingKind Kind { get; init; }

    /// <summary>What it will be called once it stands.</summary>
    public required string Name { get; init; }

    public required BuildingRecipe Recipe { get; init; }

    /// <summary>Logs carried here so far.</summary>
    public int LogsDelivered { get; private set; }

    /// <summary>Ticks of work put in so far.</summary>
    public int WorkDone { get; private set; }

    /// <summary>Whether everything it needs has been carried here.</summary>
    public bool HasMaterials => LogsDelivered >= Recipe.Logs;

    /// <summary>Whether it is finished and ready to become a building.</summary>
    public bool IsFinished => HasMaterials && WorkDone >= Recipe.WorkTicks;

    /// <summary>Logs still wanted here.</summary>
    public int LogsStillNeeded
    {
        get
        {
            int short_ = Recipe.Logs - LogsDelivered;
            return short_ < 0 ? 0 : short_;
        }
    }

    /// <summary>Take in logs somebody carried over. Returns how many were accepted.</summary>
    public int Deliver(int logs)
    {
        if (logs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logs), $"Cannot deliver {logs} logs.");
        }

        int accepted = logs < LogsStillNeeded ? logs : LogsStillNeeded;
        LogsDelivered += accepted;
        return accepted;
    }

    /// <summary>Put a tick of work in. Does nothing until the materials are here.</summary>
    /// <remarks>
    /// Materials first, deliberately: a builder cannot make progress on a site with
    /// nothing delivered to it, which is what makes the hauling a real leg of the job
    /// rather than a formality that happens to precede it.
    /// </remarks>
    public void Work()
    {
        if (HasMaterials && WorkDone < Recipe.WorkTicks)
        {
            WorkDone++;
        }
    }

    /// <summary>Logs handed back if the site is abandoned before it is finished.</summary>
    public int Abandon()
    {
        int back = LogsDelivered;
        LogsDelivered = 0;
        WorkDone = 0;
        return back;
    }
}
