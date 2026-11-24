using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ The village and its picture (A236, `title-snapshot.md §3, §6a`): a picture taken at every save (Joe: <i>"Yes, every save"</i>),
/// the ways out that wait for it, and <i>Continue</i>'s paint lifting into the village it opens.
/// </summary>
public partial class Main
{
    /// <summary>A picture is being taken: the next save's waits for it, so no two write one file at once.</summary>
    private bool _pictureUnderway;

    /// <summary>A save came while a picture was being taken: one more, after it, however many came.</summary>
    private bool _pictureAgain;

    /// <summary>The way out of the village that is waiting for the picture, or null.</summary>
    private Action? _leave;

    /// <summary>For the probe: how many pictures the saves have asked for, and where the last was to go.</summary>
    private int _picturesAsked;

    /// <inheritdoc cref="_picturesAsked"/>
    private string _pictureAskedFor = string.Empty;

    /// <summary>
    /// The village's picture, after a save that was written. ⚠️ <b>With the watercolour off it is taken only on the way out</b>: the
    /// live map then holds no painted ground to borrow, and painting one from nothing holds a frame for 1.7 s (A234).
    /// </summary>
    private void PictureTheVillage(bool leaving)
    {
        if (_saveFolder is null || _map is null)
        {
            return;
        }

        _picturesAsked++;
        _pictureAskedFor = System.IO.Path.Combine(_saveFolder, TitleSnapshot.FileName);
        if (NothingIsWritten || !(leaving || _map.WatercolourShown))
        {
            return;
        }

        if (_pictureUnderway)
        {
            _pictureAgain = true;
            return;
        }

        _pictureUnderway = true;
        TitleSnapshot.Take(this, _map, _loop.World, _pictureAskedFor, () =>
        {
            _pictureUnderway = false;
            if (_pictureAgain)
            {
                _pictureAgain = false;
                PictureTheVillage(_leave is not null);
            }

            LeaveIfReady();
        });
    }

    /// <summary>Out of the village by <paramref name="way"/>, once its picture is written — at once when none is being taken. Asked twice, the first stands.</summary>
    private void LeaveOncePictured(Action way)
    {
        if (_leave is not null)
        {
            return;
        }

        _leave = way;
        if (!_halted)
        {
            SetSpeed(0.0); // what was saved is what is pictured
        }

        LeaveIfReady();
    }

    private void LeaveIfReady()
    {
        if (_leave is null || _pictureUnderway)
        {
            return;
        }

        Action way = _leave;
        _leave = null;
        way();
    }

    /// <summary>
    /// ⭐ The window's ✕. Godot's own answer to it is turned off (<see cref="_Notification"/>), because a picture takes frames: a
    /// village is saved, pictured and closed; anything else closes at once, and so does a second ✕ while the first waits.
    /// </summary>
    private void TheWindowIsClosing()
    {
        if (_title is not null || _newGame is not null || _loop is null || _leave is not null)
        {
            GetTree().Quit();
            return;
        }

        Autosave("on quit", leaving: true);
        LeaveOncePictured(() => GetTree().Quit());
    }

    /// <summary>
    /// ⭐ The title, kept over the village it has just opened until its paint has lifted (§6a): it is Main's last child, so it is over
    /// the map and every panel, and it takes itself away.
    /// </summary>
    private void LiftTheTitle(TitleScreen title, string opened)
    {
        MoveChild(title, -1);
        var hud = new List<CanvasItem>();
        foreach (Node child in GetChildren())
        {
            if (child is CanvasItem item && child != _map && child != title)
            {
                hud.Add(item);
            }
        }

        title.LiftOver(_map, TitleSnapshot.HeartOf(_loop.World), opened, hud);
    }

    /// <summary>
    /// ⭐ The picture at every save and the ways out (A236) — <b>a probe line</b>. Nothing is drawn or written under the probe, so it
    /// asks what was asked for: an autosave, a named save and F5's each ask for one picture, in the save's own folder; a refused
    /// save none; a way out goes at once with no picture underway, waits for one that is, and goes once. Through a scratch folder it deletes.
    /// </summary>
    private string EverySaveAsksForThePicture()
    {
        var faults = new List<string>();
        string? folderWas = _saveFolder;
        bool haltedWas = _halted;
        string scratch = ProjectSettings.GlobalizePath("user://probe-picture");
        string want = System.IO.Path.Combine(scratch, TitleSnapshot.FileName);
        try
        {
            _saveFolder = scratch;
            foreach ((string what, Action save, int asks) in new (string, Action, int)[]
            {
                ("an autosave", () => Autosave("probe"), 1),
                ("a named save", () => SaveUnderAName("probe"), 1),
                ("F5", QuickSave, 1),
            })
            {
                int before = _picturesAsked;
                _pictureAskedFor = string.Empty;
                save();
                if (_picturesAsked - before != asks || _pictureAskedFor != want)
                {
                    faults.Add($"{what} asked for {_picturesAsked - before} picture(s) at '{_pictureAskedFor}' (want {asks} at {want})");
                }
            }

            int asked = _picturesAsked;
            _halted = true;
            Autosave("probe");
            SaveUnderAName("probe");
            _halted = haltedWas;
            if (_picturesAsked != asked)
            {
                faults.Add("a village stopped on an error was pictured");
            }

            int left = 0;
            LeaveOncePictured(() => left++);
            if (left != 1 || _leave is not null)
            {
                faults.Add($"with no picture underway a way out went {left} time(s)");
            }

            left = 0;
            _pictureUnderway = true;
            LeaveOncePictured(() => left++);
            LeaveOncePictured(() => left += 10);
            bool waited = left == 0;
            _pictureUnderway = false;
            LeaveIfReady();
            LeaveIfReady();
            if (!waited || left != 1)
            {
                faults.Add($"with a picture underway a way out {(waited ? "waited" : "did not wait")} and went {left} time(s) (want waited, once)");
            }

            if (GetTree().AutoAcceptQuit)
            {
                faults.Add("Godot closes the window itself at the ✕, before the picture is written");
            }
        }
        catch (Exception e)
        {
            faults.Add($"threw {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            _saveFolder = folderWas;
            _halted = haltedWas;
            _pictureUnderway = false;
            _leave = null;
            if (System.IO.Directory.Exists(scratch))
            {
                System.IO.Directory.Delete(scratch, recursive: true);
            }
        }

        return faults.Count == 0
            ? "[widths] title picture: ✅ an autosave, a named save and F5 each ask for one picture beside the save, a village stopped on an error none; a way out waits for the picture and goes once; the ✕ waits too"
            : $"[widths] title picture: ❌ {string.Join("; ", faults)}";
    }
}
