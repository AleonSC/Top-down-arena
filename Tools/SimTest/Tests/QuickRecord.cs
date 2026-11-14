using System;
using System.IO;
using Arena;

static partial class Program
{
    // Session recording in Quick Play: a solo Quick Play writes its session file; a Boss Lab fight on the same session
    // doesn't, and the match played after the lab is recorded again.
    static void QuickRecordTest(float dt)
    {
        string wasFolder = Profile.Folder, dir = Path.Combine(Path.GetTempPath(), "arena-qrec-" + Guid.NewGuid().ToString("N"));
        string rec = Path.Combine(dir, "sessions");
        Profile.Folder = dir;
        Session.QuickDefault = true;
        Func<int> Files = () => Directory.Exists(rec) ? Directory.GetFiles(rec, "*.json").Length : 0;
        try
        {
            var solo = new Session();
            solo.Game.ProfileOverride = new Profile();
            solo.Game.RecordFolder = rec;
            solo.Host(nextPort++, "Host", Role.Tank);
            solo.RequestReady(true);
            for (int i = 0; i < 40; i++) solo.Poll();
            solo.StartMatch();
            Run(new[] { solo }, dt, 3f);
            Check(solo.Game.Quick && solo.Game.Recorder != null && Files() == 1, "a solo Quick Play writes a session file");
            solo.Leave("");
            System.Threading.Thread.Sleep(1100);  // (session files are named to the second)

            var lab = new BossLab { Wave = 10, PlayerCount = 1, Seed = 3, Quick = true };
            lab.Seats[0].Human = true;
            lab.Start(solo, nextPort++);
            Run(new[] { solo }, dt, 2f);
            bool labUnrecorded = solo.Game.Running && solo.Game.Recorder == null && Files() == 1;
            lab.Stop();
            System.Threading.Thread.Sleep(1100);
            solo.Host(nextPort++, "Host", Role.Tank);
            solo.QuickMode = true;
            solo.RequestReady(true);
            for (int i = 0; i < 40; i++) solo.Poll();
            solo.StartMatch();
            Run(new[] { solo }, dt, 3f);
            Console.WriteLine("  lab fight recorded: {0}; Quick Play after it recorded: {1} ({2} files)", !labUnrecorded, solo.Game.Recorder != null, Files());
            Check(labUnrecorded && solo.Game.Quick && solo.Game.Recorder != null && Files() == 2, "a Boss Lab fight isn't recorded, and the Quick Play after it is");
            solo.Leave("");
        }
        finally
        {
            Session.QuickDefault = false;
            Profile.Folder = wasFolder;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
