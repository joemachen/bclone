namespace Bclone.Sim.World;

/// <summary>
/// Where the player has said the village may build homes (D42).
/// </summary>
/// <remarks>
/// <para>
/// <b>Intent painted over ground, which the village acts on when it has a reason
/// to.</b> A residential area with no housing shortage produces nothing, and that is
/// not the brush failing — it is the brush working. The player decides the
/// neighbourhood; the sim still decides which tile, because
/// <see cref="Household.ChooseSite"/> knows about the walk to work and the walk to the
/// store and a cursor does not.
/// </para>
/// <para>
/// This is what let placement be handed over without giving up the guarantee the food
/// economy rests on. Per-house placement would have broken
/// <see cref="VillageEconomy.MaxHomeToWorkTiles"/> — a bound the <em>sim</em> keeps —
/// whereas a zone merely narrows where `ChooseSite` may look. The bound survives, and
/// the warning about a bad neighbourhood happens once, when the area is painted,
/// instead of on every house.
/// </para>
/// <para>
/// <b>Sim state, therefore hashed and deterministic.</b> A zone is a decision the
/// player made; two runs given the same decisions must produce the same village.
/// </para>
/// </remarks>
public sealed class ZoneMap
{
    private readonly bool[] _residential;
    private readonly int _width;
    private readonly int _height;
    private readonly int _minX;
    private readonly int _minY;

    public ZoneMap(GeneratedMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _width = map.Width;
        _height = map.Height;
        _minX = map.MinX;
        _minY = map.MinY;
        _residential = new bool[_width * _height];
    }

    /// <summary>How many tiles are painted for housing.</summary>
    public int ResidentialTiles { get; private set; }

    /// <summary>Whether the village may put a home on this tile.</summary>
    public bool IsResidential(GridPos position)
    {
        int index = IndexOf(position);
        return index >= 0 && _residential[index];
    }

    /// <summary>Paint or erase one tile. Returns true if it changed anything.</summary>
    public bool SetResidential(GridPos position, bool painted)
    {
        int index = IndexOf(position);
        if (index < 0 || _residential[index] == painted)
        {
            return false;
        }

        _residential[index] = painted;
        ResidentialTiles += painted ? 1 : -1;
        return true;
    }

    /// <summary>Every painted tile, in a fixed order — for hashing and for drawing.</summary>
    public IReadOnlyList<bool> Residential => _residential;

    private int IndexOf(GridPos position)
    {
        int x = position.X - _minX;
        int y = position.Y - _minY;

        return x < 0 || x >= _width || y < 0 || y >= _height ? -1 : (y * _width) + x;
    }
}
