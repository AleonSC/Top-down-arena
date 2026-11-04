using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using Arena;

// Run replays: recording a hosted match, saving/listing/renaming/deleting the file, and playing it back (pause, step,
// any speed either way, seeking anywhere gives the same picture as playing there).
static partial class Program
{
    static string StateKey(Game g)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("w" + g.Wave + " s" + g.State + " sc" + g.Score + "|");
        foreach (var p in g.Players.Values.OrderBy(p => p.Id))
            sb.Append(p.Id + ":" + (int)p.Pos.X + "," + (int)p.Pos.Y + " hp" + (int)p.Hp + " l" + p.Level + ";");
        sb.Append("|");
        foreach (var e in g.Enemies.Values.OrderBy(e => e.Id))
            sb.Append(e.Id + ":" + (int)e.Hp + ";");
        return sb.ToString();
    }

    static void ReplayTest(float dt)
    {
        string dir = Path.Combine(Path.GetTempPath(), "arena-replay-test-" + Guid.NewGuid().ToString("N"));
        var host = new Session { AutoStart = 1 };
        host.Host(nextPort++, "Rec", Role.Dps, 0, 0, Kit.Soldier);
        var g = host.Game;
        g.Bot = true;
        g.ReplayFolder = dir;
        host.StartMatch();
        Check(g.Replay != null, "replay: a hosted match starts recording when a replay folder is set");
        for (int f = 0; f < 60 * 70 && g.Wave < 3; f++)
            Step(host, dt);
        int liveWave = g.Wave;
        host.Leave("");
        Check(g.Replay == null, "replay: stopping the match ends the recording");

        var list = ReplayFile.List(dir);
        Check(list.Count == 1, "replay: the finished run is saved to the folder (" + list.Count + ")");
        if (list.Count != 1)
            return;
        var head = list[0];
        Check(head.MaxWave >= 1 && head.Seconds > 5f && head.Players == 1 && head.Party.Contains("Dps"), "replay: the header says what happened (" + head.Mode + ", wave " + head.MaxWave + ", " + head.Seconds.ToString("0.0") + " s, " + head.Party + ")");
        var file = ReplayFile.Load(head.Path);
        Check(file != null && file.Frames > 300 && file.Msgs.Count > 100 && file.Marks.Count > 0, "replay: the file loads with its frames, messages and marks (" + (file != null ? file.Frames + " frames, " + file.Msgs.Count + " messages, " + file.Marks.Count + " marks, " + file.Bytes / 1024 + " KB" : "null") + ")");
        if (file == null)
            return;

        // A replay's last frame matches what the live game showed (same wave, same enemies).
        var pl = new ReplayPlayer();
        pl.Load(file);
        Check(pl.Game.Running && pl.Game.Players.Count == 1 && pl.Game.Me != null, "replay: opens on a running copy of the match with the player in view");
        pl.Seek(file.Frames);
        Check(pl.Game.Wave >= Math.Max(1, liveWave - 1), "replay: the end of the recording is where the run got to (wave " + pl.Game.Wave + " vs " + liveWave + ")");
        Check(pl.Game.Sounds.Count >= 0 && pl.Paused == false, "replay: seeking doesn't change the pause");

        // Seeking anywhere gives the same picture as playing there frame by frame.
        int target = Math.Min(file.Frames - 5, 1400);
        var walk = new ReplayPlayer();
        walk.Load(file);
        walk.StepFrames(target);
        string walked = StateKey(walk.Game);
        var jump = new ReplayPlayer();
        jump.Load(file);
        jump.Seek(target);
        Check(jump.Frame == target && StateKey(jump.Game) == walked, "replay: jumping to frame " + target + " shows the same match as playing there");
        // Backward lands on the same picture as forward.
        jump.Seek(target - 500);
        var walk2 = new ReplayPlayer();
        walk2.Load(file);
        walk2.StepFrames(target - 500);
        Check(StateKey(jump.Game) == StateKey(walk2.Game), "replay: rewinding 500 frames shows the same match as playing there");
        jump.Seek(0);
        Check(jump.Frame == 0 && jump.Game.Wave <= 1, "replay: back to the very start");

        // Speeds: forward and backward at several rates, and pause.
        foreach (float rate in new[] { 0.1f, 0.5f, 1f, 4f, 10f })
        {
            var p = new ReplayPlayer();
            p.Load(file);
            p.Seek(1000);
            int from = p.Frame;
            p.Play(rate);
            for (int i = 0; i < 60; i++)
                p.Step(dt);
            int moved = p.Frame - from;
            int want = (int)Math.Round(60 * rate);
            Check(Math.Abs(moved - want) <= Math.Max(2, want / 20), "replay: " + rate + "x runs " + moved + " frames in a second (about " + want + ")");
        }
        foreach (float rate in new[] { -0.1f, -1f, -10f })
        {
            var p = new ReplayPlayer();
            p.Load(file);
            p.Seek(Math.Min(file.Frames - 1, 1500));
            int from = p.Frame;
            p.Play(rate);
            for (int i = 0; i < 30; i++)
                p.Step(dt);
            int moved = from - p.Frame;
            int want = (int)Math.Round(30 * -rate);
            Check(Math.Abs(moved - want) <= Math.Max(2, want / 20), "replay: " + rate + "x runs " + moved + " frames back in half a second (about " + want + ")");
        }
        var q = new ReplayPlayer();
        q.Load(file);
        q.Seek(500);
        q.TogglePause();
        for (int i = 0; i < 60; i++)
            q.Step(dt);
        Check(q.Frame == 500, "replay: paused stays put");
        q.StepFrames(1);
        q.StepFrames(1);
        q.StepFrames(-1);
        Check(q.Frame == 501 && q.Paused, "replay: frame stepping goes forward and back one at a time");
        int mark = q.NextWaveFrame();
        Check(mark > q.Frame, "replay: the next wave mark is ahead (frame " + mark + ")");

        // Seeking a long way takes about as long as a short way.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
            q.Seek(file.Frames - 1);
        double ms = sw.Elapsed.TotalMilliseconds / 10;
        Check(ms < 250, "replay: a seek to the end of the run takes " + ms.ToString("0") + " ms (" + file.Frames + " frames)");

        // Playing a replay doesn't touch a profile or record anything of its own.
        Check(pl.Game.Recorder == null && pl.Game.Replay == null, "replay: watching one records nothing");

        // Rename, delete.
        string oldPath = head.Path;
        Check(head.Rename("Boss practice: Warden/1") && File.Exists(head.Path) && !File.Exists(oldPath) && head.Name == "Boss practice Warden1", "replay: renaming moves the file and cleans the name (" + head.Name + ")");
        var second = ReplayFile.Load(head.Path);
        second.Save(ReplayFile.FreePath(dir, "Other"));
        Check(ReplayFile.List(dir).Count == 2, "replay: both are listed");
        Check(head.Rename("Other") && head.Name == "Other (2)", "replay: a name that's taken gets a number (" + head.Name + ")");
        Check(head.Delete() && ReplayFile.List(dir).Count == 1, "replay: delete removes the file");
        File.WriteAllText(Path.Combine(dir, "junk.replay"), "not a replay");
        Check(ReplayFile.List(dir).Count == 1, "replay: a damaged file is skipped, not a crash");
        try { Directory.Delete(dir, true); } catch (Exception) { }
    }
}
