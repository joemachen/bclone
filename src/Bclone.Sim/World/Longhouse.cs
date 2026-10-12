using System.Text.Json.Nodes;
using Bclone.Sim.Core;
using Bclone.Sim.Persistence;

namespace Bclone.Sim.World;

/// <summary>
/// The longhouse — the nomads' house (`specs/nomads.md §4.5`, D566; Joe: <i>"just repurpose the long house
/// thats already in game. there's no other need for it"</i>). A roof and one larder for families waiting on a
/// house of their own.
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ Its own kind of thing, like a well (D427), not a <see cref="Household"/>.</b> A home is the household
/// itself — its footprint is the house row's, its plot is fenced, its site is chosen by the brush — and the
/// longhouse is none of that: it is placed by a button (D42's one exception), is three tiles long, and shelters
/// several families at once. Threading a second kind of home through every house reader would have been the
/// larger change; a fifth standing thing with its own list is the smaller one, and the well is its template.
/// </para>
/// <para>
/// <b>Lodging is on the family, not here.</b> A roofless household carries <see cref="Household.LodgedIn"/> =
/// this longhouse's <see cref="Id"/>; who lodges here is derived from the households, never a second list.
/// The larder is the longhouse's own (Joe): lodgers eat from it and fetch to it, and its fire burns from it.
/// </para>
/// </remarks>
public sealed class Longhouse
{
    private Point _position;

    /// <summary>One past the highest ever given — a household's <see cref="Household.LodgedIn"/> names it.</summary>
    public required int Id { get; init; }

    public required Point Position { get => _position; init => _position = value; }

    public GridPos Tile => _position.ToTile();

    public Angle Facing { get; init; }

    public int ExtentWidth { get; init; } = 3;

    public int ExtentHeight { get; init; } = 1;

    public Footprint Footprint => new()
    {
        Origin = _position,
        Width = ExtentWidth,
        Height = ExtentHeight,
        Facing = Facing,
    };

    internal void MoveTo(Point to) => _position = to;

    public required string Name { get; init; }

    /// <summary>How many people may lodge here — the row's <c>house_capacity</c> when it was raised.</summary>
    public required int Capacity { get; init; }

    /// <summary>The one larder its lodgers share — food and firewood both.</summary>
    public required Stockpile Larder { get; init; }

    public ulong RaisedAtTick { get; set; }

    public ulong RepairedAtTick { get; set; }

    /// <summary>Fallen in: shelters nobody until the builders mend it (`building-condition.md §4.5`).</summary>
    public bool Closed { get; set; }

    internal JsonObject ToSave() => new()
    {
        ["id"] = Id,
        ["at"] = SaveWriter.Point(Position),
        ["facing"] = SaveWriter.Angle(Facing),
        ["width"] = ExtentWidth,
        ["height"] = ExtentHeight,
        ["name"] = Name,
        ["capacity"] = Capacity,
        ["larder"] = Larder.ToSave(),
        ["raised_at"] = SaveWriter.Hex(RaisedAtTick),
        ["repaired_at"] = SaveWriter.Hex(RepairedAtTick),
        ["closed"] = Closed,
    };

    internal static Longhouse FromSave(SaveReader save) => new()
    {
        Id = save.Int("id"),
        Position = save.Point("at"),
        Facing = save.Angle("facing"),
        ExtentWidth = save.Int("width"),
        ExtentHeight = save.Int("height"),
        Name = save.String("name"),
        Capacity = save.Int("capacity"),
        Larder = Stockpile.FromSave(save.Object("larder")),
        RaisedAtTick = save.ULong("raised_at"),
        RepairedAtTick = save.ULong("repaired_at"),
        Closed = save.Bool("closed"),
    };
}
