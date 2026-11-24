using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ The title's picture of a village (A234, `title-snapshot.md §3–§4`; Joe: <i>"the menu shows the newest"</i>): the live world
/// drawn once, offscreen, by a second <see cref="VillageMap"/>, and written as <see cref="FileName"/> in the village's save folder.
/// </summary>
/// <remarks>
/// <para>
/// <b>The player's screen never changes.</b> The second map lives in a <see cref="SubViewport"/> of its own, borrows what the live
/// map has already baked of the same ground (<see cref="VillageMap.BorrowTheGroundOf"/>), is drawn, read back, and freed.
/// </para>
/// <para>
/// ⚠️ <b>A picture takes frames</b>: the figures are baked a few a frame, so the map is drawn until every one is (or
/// <see cref="MostFrames"/>), then read back, and encoded and written off the main thread. A caller that is about to close the game waits for <c>done</c>; it is always called, whether the picture was
/// written or not. <b>A picture that fails never costs the save</b>: it is said loudly and the last picture stands.
/// </para>
/// </remarks>
public sealed partial class TitleSnapshot : Node
{
    /// <summary>The picture's name in a village's save folder: one a village, rewritten at each save (call 3).</summary>
    internal const string FileName = "title.jpg";

    /// <summary>The picture: 16:9, the size of Joe's screen.</summary>
    internal static readonly Vector2I PictureSize = new(2560, 1440);

    /// <summary>Where the village's heart sits in it: the middle of the area right of the title's panel (A190).</summary>
    internal static readonly Vector2 HeartInPicture = new(0.64f, 0.50f);

    /// <summary>Pixels a tile: the four seasons' zoom, as a 2560 window shows it.</summary>
    internal const float PixelsPerTile = 32f;

    /// <summary>The most frames the map is drawn for; a village's figures are nearly all borrowed, so it is two or three.</summary>
    private const int MostFrames = 60;

    private const float JpgQuality = 0.9f;

    private readonly string _path = string.Empty;
    private readonly Action? _done;
    private SubViewport? _view;
    private VillageMap _map = null!;
    private VillageMap _live = null!;
    private SimWorld _world = null!;
    private Vector2 _heart;
    private int _frames;
    private Task? _writing;

    /// <summary>For Godot, which may construct a script's node itself; the game uses <see cref="Take"/>.</summary>
    public TitleSnapshot()
    {
    }

    private TitleSnapshot(string path, Action? done)
    {
        _path = path;
        _done = done;
    }

    /// <summary>
    /// Take the village's picture to <paramref name="path"/>, under <paramref name="parent"/>: <paramref name="done"/> is called once
    /// it is written, or has failed. With no home standing there is no picture (call 2) and <paramref name="done"/> is called now.
    /// </summary>
    internal static void Take(Node parent, VillageMap live, SimWorld world, string path, Action? done = null)
    {
        if (HeartOf(world) is not Vector2 heart)
        {
            done?.Invoke();
            return;
        }

        parent.AddChild(new TitleSnapshot(path, done) { _live = live, _world = world, _heart = heart });
    }

    /// <summary>
    /// The village's heart: the middle of the box round its lived-in homes, the map's own framing rule
    /// (<see cref="VillageMap.CentreOnTheVillage"/>) — or null with none standing.
    /// </summary>
    internal static Vector2? HeartOf(SimWorld world)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        bool any = false;
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (household.HomeTile is GridPos home && world.LivingMembersOf(household) > 0)
            {
                min = min.Min(new Vector2(home.X, home.Y));
                max = max.Max(new Vector2(home.X, home.Y));
                any = true;
            }
        }

        return any ? (min + max) / 2f : null;
    }

    /// <summary>Pure: the tile at the middle of a picture whose heart sits at <see cref="HeartInPicture"/>.</summary>
    internal static Vector2 CentreFor(Vector2 heart) =>
        heart + (((Vector2.One / 2f) - HeartInPicture) * PictureSize / PixelsPerTile);

    /// <summary>
    /// One step a frame: the map made and drawn, drawn again until every figure is painted, read back, written, freed. The first
    /// is here and not in <see cref="Take"/>, so the live map has drawn this moment before it is borrowed from.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_writing is not null)
        {
            if (_writing.IsCompleted)
            {
                SayIfItFailed();
                QueueFree();
                SetProcess(false);
                _done?.Invoke();
            }

            return;
        }

        if (_view is null)
        {
            Draw();
            return;
        }

        if (++_frames < MostFrames && !_map.EveryoneIsPainted)
        {
            Present();
            return;
        }

        Image? picture = _view.GetTexture().GetImage();
        _view.QueueFree();
        _writing = Task.Run(() => Write(picture));
    }

    private void Draw()
    {
        _view = new SubViewport
        {
            Size = PictureSize,
            Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        AddChild(_view);

        _map = new VillageMap { Paused = true };
        _view.AddChild(_map);
        _map.Position = Vector2.Zero;
        _map.Size = PictureSize;
        _map.MouseFilter = Control.MouseFilterEnum.Ignore; // after its _Ready, which asks for the mouse
        _map.ShowIdleMarkers(false);
        _map.ShowFullMarkers(false);
        _map.ShowResidentialLand(false);
        _map.ShowWorkGround(false);
        _map.ShowHarvestLand(false);
        _map.ShowWatercolour(true);
        _map.BorrowTheGroundOf(_live, _world);
        _map.LookAt(CentreFor(_heart), PixelsPerTile);
        Present();
    }

    /// <summary>The map drawn once more, and rendered once: a frame that only reads renders nothing.</summary>
    private void Present()
    {
        _map.Present(_world, 0d, -1, null, MapDetail.Off);
        _view!.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    /// <summary>Off the main thread: encoded, then put in place whole, so a picture is never half written.</summary>
    private void Write(Image? picture)
    {
        byte[] jpg = picture is null || picture.IsEmpty() ? Array.Empty<byte>() : picture.SaveJpgToBuffer(JpgQuality);
        if (jpg.Length == 0)
        {
            throw new System.IO.IOException("the map drew nothing, or the picture would not encode");
        }

        string aside = _path + ".part";
        System.IO.File.WriteAllBytes(aside, jpg);
        System.IO.File.Move(aside, _path, overwrite: true);
    }

    private void SayIfItFailed()
    {
        if (_writing?.Exception is { } failed)
        {
            GD.PushError($"[Kith] the village's picture for the title was not written: {_path} — the last one stands. {failed.GetBaseException()}");
        }
    }
}
