using Kith.Sim.Core;
using Godot;

namespace Kith.Game;

/// <summary>What the probe's <c>title:</c> line asks of the lift (A236, `title-snapshot.md §6a, §8`).</summary>
public sealed partial class TitleScreen
{
    /// <summary>What a green line says the lift does.</summary>
    internal const string WhatTheLiftHeld =
        "; Continue slides the panel off before it opens anything, a refused save brings it back, the paint comes off only over a drawn "
        + "map, the panels stay hidden until it has, and it hands the game over once; a picture written after the save it is of opens the "
        + "map on the picture's own frame, the camera eases to the play framing and a moved camera is the player's; a key hurries it";

    private IEnumerable<string> TheLiftHolds()
    {
        foreach (string fault in ThePictureIsOfItsSave())
        {
            yield return fault;
        }

        // The frame: the heart on the window where the picture shows it, at 16:10, 16:9 and 21:9.
        Vector2 heart = new(31f, 12f);
        foreach (Vector2 window in new[] { new Vector2(1280, 800), new Vector2(1422, 800), new Vector2(2560, 1080) })
        {
            (Vector2 centre, float ppt) = FrameOf(heart, window);
            Vector2 at = ((heart - centre) * ppt) + (window / 2f);
            Vector2 want = WhereTheVillageLands(TitleSnapshot.HeartInPicture, TitleSnapshot.PictureSize, window);
            float east = ((heart.X + 10f - centre.X) * ppt) + (window.X / 2f) - at.X;
            float wantEast = 10f * TitleSnapshot.PixelsPerTile * Mathf.Max(window.X / TitleSnapshot.PictureSize.X, window.Y / TitleSnapshot.PictureSize.Y);
            if (at.DistanceTo(want) > 0.5f || Mathf.Abs(east - wantEast) > 0.5f)
            {
                yield return $"at {window} the live map puts the heart at {at} and ten tiles at {east:F1} px (the picture: {want}, {wantEast:F1})";
            }
        }

        // The way between: its two ends, always nearer in zoom, and every point of it on one line through a still point.
        (Vector2 Centre, float Ppt) from = (new Vector2(10f, 5f), 18f), to = (new Vector2(2f, 9f), 34f);
        float last = 0f;
        for (int i = 0; i <= 10; i++)
        {
            (Vector2 centre, float ppt) = Between(from, to, i / 10f);
            float off = Mathf.Abs((centre - from.Centre).Cross((to.Centre - from.Centre).Normalized()));
            bool end = i == 0 ? centre.DistanceTo(from.Centre) < 0.01f && Mathf.Abs(ppt - 18f) < 0.01f : i < 10 || (centre.DistanceTo(to.Centre) < 0.01f && Mathf.Abs(ppt - 34f) < 0.01f);
            if (off > 0.01f || ppt < last || !end || !centre.IsFinite())
            {
                yield return $"{i * 10}% of the camera's way is {centre} at {ppt:F1} px a tile";
            }

            last = ppt;
        }

        foreach (bool matched in new[] { false, true })
        {
            foreach (string fault in ALiftOver(matched, heart))
            {
                yield return $"{(matched ? "over its own picture" : "over another picture")}: {fault}";
            }
        }
    }

    /// <summary>On a scratch folder it deletes: whose picture it is, by folder, by newest, and by which was written last.</summary>
    private static IEnumerable<string> ThePictureIsOfItsSave()
    {
        string scratch = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://probe-lift"));
        string folder = System.IO.Path.Combine(scratch, "v"), other = System.IO.Path.Combine(scratch, "w");
        string newest = System.IO.Path.Combine(folder, "a.save"), older = System.IO.Path.Combine(folder, "b.save"), elsewhere = System.IO.Path.Combine(other, "a.save");
        string picture = System.IO.Path.Combine(folder, TitleSnapshot.FileName);
        try
        {
            System.IO.Directory.CreateDirectory(folder);
            System.IO.Directory.CreateDirectory(other);
            DateTime then = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach ((string file, int at) in new[] { (older, 0), (newest, 1), (picture, 2), (elsewhere, 1) })
            {
                System.IO.File.WriteAllBytes(file, new byte[] { 1 });
                System.IO.File.SetLastWriteTimeUtc(file, then.AddMinutes(at));
            }

            SaveListing[] saves = { new(newest, "v", null), new(elsewhere, "w", null), new(older, "v", null) };
            foreach ((string what, string? of, string opened, bool want) in new (string, string?, string, bool)[]
            {
                ("the newest save's", picture, newest, true), ("an older save's", picture, older, false),
                ("another village's", picture, elsewhere, false), ("no picture's", null, newest, false),
            })
            {
                if (PictureIsOf(of, opened, saves) != want)
                {
                    yield return $"{what} opening is {(want ? "not " : string.Empty)}taken for the picture's";
                }
            }

            System.IO.File.SetLastWriteTimeUtc(picture, then);
            if (PictureIsOf(picture, newest, saves))
            {
                yield return "a picture older than the newest save is taken for its";
            }
        }
        finally
        {
            if (System.IO.Directory.Exists(scratch))
            {
                System.IO.Directory.Delete(scratch, recursive: true);
            }
        }
    }

    /// <summary>A lift stepped by hand, sixty frames a second, over a posed map and one panel.</summary>
    private List<string> ALiftOver(bool matched, Vector2 heart)
    {
        var faults = new List<string>();
        string scratch = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://probe-lift"));
        string folder = System.IO.Path.Combine(scratch, "v"), save = System.IO.Path.Combine(folder, "a.save");
        var under = new VillageMap { Size = Size };
        under.Present(SimWorld.Create(_config), 0d, -1, null, MapDetail.Off);
        var panel = new Control();
        try
        {
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(save, new byte[] { 1 });
            using Image small = Image.CreateEmpty(64, 36, false, Image.Format.Rgb8);
            if (matched)
            {
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, TitleSnapshot.FileName), small.SaveJpgToBuffer());
            }

            TitleScreen Pose(out Func<int> asked)
            {
                var posed = new TitleScreen(_config, new[] { new SaveListing(save, "v", null) }, _build, _doors);
                AddChild(posed);
                int n = 0;
                posed.LoadAsked += path => n += path == save ? 1 : 100;
                asked = () => n;
                return posed;
            }

            void Step(TitleScreen title, LiftStep until, int most = 400)
            {
                for (int i = 0; i < most && title.Lift != until; i++)
                {
                    title.StepTheLift(1d / 60d);
                }
            }

            // Refused: nothing is opened with the panel there, it is asked for once, and the panel comes back with the reason.
            TitleScreen refused = Pose(out Func<int> refusedAsked);
            refused.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
            for (int i = 0; i < 12; i++)
            {
                refused.StepTheLift(1d / 60d);
            }

            refused.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);

            if (refusedAsked() != 0 || refused.Band.OffsetLeft >= 0f)
            {
                faults.Add($"0.2 s after Continue the save was asked for {refusedAsked()} time(s), the panel at {refused.Band.OffsetLeft:F0} px");
            }

            Step(refused, LiftStep.Asking, most: 8);
            if (refused.Lift != LiftStep.Asking)
            {
                faults.Add($"Continue pressed twice starts the slide again ({refused.Lift} after 0.33 s)");
                Step(refused, LiftStep.Asking);
            }

            refused.StepTheLift(1d / 60d);
            refused.RefuseTheLoad("posed by the probe");
            if (refusedAsked() != 1 || refused.Lift != LiftStep.None || !refused.Band.Visible || refused.Band.OffsetLeft != 0f
                || refused.Band.Modulate.A < 1f || !refused.ContinueButton.HasFocus())
            {
                faults.Add($"a refused save (asked {refusedAsked()} time(s)) leaves the title {refused.Lift}, its panel {(refused.Band.Visible ? "shown" : "hidden")} at {refused.Band.OffsetLeft:F0} px, alpha {refused.Band.Modulate.A:F2}");
            }

            RemoveChild(refused);
            refused.QueueFree();

            // Opened: the paint off, the picture off, the camera and the panels.
            TitleScreen title = Pose(out _);
            title.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
            Step(title, LiftStep.Asking);
            title.LiftOver(under, heart, save, new CanvasItem[] { panel });
            (Vector2 Centre, float Ppt) frame = FrameOf(heart, title.Size);
            if (panel.Modulate.A != 0f || title.Strength != 1f || (under.Camera == frame) != matched)
            {
                faults.Add($"as the village opens its panels are at {panel.Modulate.A:F2}, the paint at {title.Strength:F2}, the map at {under.Camera} (the picture's frame {frame})");
            }

            for (int i = 0; i < 3; i++)
            {
                title.StepTheLift(1d / 60d);
            }

            bool waited = title.Lift == LiftStep.Settling;
            title.StepTheLift(2d);
            if (!waited || title.Lift != LiftStep.Settling)
            {
                faults.Add($"after three frames and one of two seconds over a map it is {title.Lift}{(waited ? string.Empty : ", and did not wait for the three")} (want Settling)");
            }

            Step(title, LiftStep.Lifting);
            title.StepTheLift(2d);
            if (title.Lift != LiftStep.Lifting || title.Strength < 0.9f)
            {
                faults.Add($"a two-second frame as the paint comes off takes it to {title.Strength:F2}, {title.Lift}");
            }

            float strength = title.Strength;
            int paintFrames = 0;
            while (title.Lift == LiftStep.Lifting && paintFrames++ < 400)
            {
                title.StepTheLift(1d / 60d);
                if (title.Strength > strength || panel.Modulate.A != 0f || title.Modulate.A != 1f)
                {
                    faults.Add($"{paintFrames} frames into the paint it is {title.Strength:F2} (was {strength:F2}), the picture {title.Modulate.A:F2}, the panels {panel.Modulate.A:F2}");
                    break;
                }

                strength = title.Strength;
            }

            if (title.Lift != LiftStep.Fading || strength > 0.001f || paintFrames < 55 || paintFrames > 80 || title._floor.Visible)
            {
                faults.Add($"the paint took {paintFrames} frames to {strength:F2}, the title then {title.Lift}, its floor {(title._floor.Visible ? "shown" : "hidden")} (want 64, 0, Fading, hidden)");
            }

            Step(title, LiftStep.Easing);
            if (title.Visible || title.MouseFilter != MouseFilterEnum.Ignore || (matched && under.Camera.Centre.DistanceTo(frame.Centre) > 0.01f))
            {
                faults.Add($"handing over, the title is {(title.Visible ? "shown" : "hidden")}, its mouse {title.MouseFilter}, the map at {under.Camera} (the picture's frame {frame})");
            }

            for (int i = 0; i < 27; i++)
            {
                title.StepTheLift(1d / 60d);
            }

            float half = panel.Modulate.A;
            Step(title, LiftStep.Done);
            if (half < 0.3f || half > 0.7f || panel.Modulate.A != 1f || title.GetParent() is not null)
            {
                faults.Add($"the panels came to {half:F2} half way and {panel.Modulate.A:F2} at the end, the title {(title.GetParent() is null ? "gone" : "still there")}");
            }

            if (matched && under.Camera == frame)
            {
                faults.Add("the camera never left the picture's frame");
            }

            (Vector2 Centre, float Ppt) ended = under.Camera;
            under.CentreOnTheVillage();
            if (matched && (under.Camera.Centre.DistanceTo(ended.Centre) > 0.01f || Mathf.Abs(under.Camera.Ppt - ended.Ppt) > 0.01f))
            {
                faults.Add($"the camera ended at {ended} (the play framing {under.Camera})");
            }

            // Hurried, and moved by the player.
            TitleScreen hurried = Pose(out _);
            hurried.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
            Step(hurried, LiftStep.Asking);
            hurried.LiftOver(under, heart, save, new CanvasItem[] { panel });
            Step(hurried, LiftStep.Lifting);
            hurried._UnhandledKeyInput(new InputEventKey { Keycode = Key.Space, Pressed = true });
            if (hurried.Lift != LiftStep.Done || panel.Modulate.A != 1f || hurried.GetParent() is not null || (matched && under.Camera.Ppt != ended.Ppt))
            {
                faults.Add($"a key left it {hurried.Lift}, the panels {panel.Modulate.A:F2}, the map at {under.Camera}");
            }

            TitleScreen moved = Pose(out _);
            moved.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
            Step(moved, LiftStep.Asking);
            moved.LiftOver(under, heart, save, new CanvasItem[] { panel });
            Step(moved, LiftStep.Easing);
            moved.StepTheLift(1d / 60d);
            under.LookAt(new Vector2(3f, 4f), 20f);
            Step(moved, LiftStep.Done);
            if (under.Camera.Centre != new Vector2(3f, 4f) || under.Camera.Ppt != 20f)
            {
                faults.Add($"a camera the player moved was taken back to {under.Camera}");
            }
        }
        finally
        {
            under.Free();
            panel.Free();
            if (System.IO.Directory.Exists(scratch))
            {
                System.IO.Directory.Delete(scratch, recursive: true);
            }

            PrimaryItem.GrabFocus();
        }

        return faults;
    }
}
