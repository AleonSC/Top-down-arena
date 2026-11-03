using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Every music track renders cleanly.
static partial class Program
{
    // Every music track renders: a sensible length, no clipping or NaNs, and a seamless loop.
    static void MusicTest()
    {
        var recipes = new List<KeyValuePair<string, Func<float[]>>>();
        recipes.Add(new KeyValuePair<string, Func<float[]>>("menu", MenuMusic.Render));
        recipes.Add(new KeyValuePair<string, Func<float[]>>("battle", () => GameMusic.Render(GameMusic.Battle)));
        var bosses = new HashSet<EnemyType>();
        for (int wave = 5; wave <= Game.MaxWave; wave += 5)
            bosses.Add(Game.BossForWave(wave));
        foreach (var boss in bosses)
        {
            var b = boss;
            recipes.Add(new KeyValuePair<string, Func<float[]>>(b.ToString(), () => GameMusic.Render(GameMusic.ForBoss(b))));
        }
        bool ok = true;
        var lengths = new List<string>();
        foreach (var kv in recipes)
        {
            var s = kv.Value();
            float peak = 0f;
            bool nan = false;
            foreach (var v in s)
            {
                nan |= float.IsNaN(v) || float.IsInfinity(v);
                peak = Math.Max(peak, Math.Abs(v));
            }
            float secs = s.Length / (float)MenuMusic.Rate;
            bool good = !nan && peak <= 0.81f && peak > 0.2f && secs > 20f && secs < 60f && Math.Abs(s[0] - s[s.Length - 1]) < 0.1f;
            lengths.Add(kv.Key + " " + secs.ToString("0") + "s");
            if (!good) Console.WriteLine("  bad track: " + kv.Key + " peak " + peak + " length " + secs + " nan " + nan);
            ok &= good;
        }
        Console.WriteLine("  music: " + string.Join(", ", lengths.ToArray()));
        Check(ok && bosses.Count == 10, "menu, battle and all 10 boss tracks render cleanly and loop");
    }

    // The music player's library: unique ids, every extra mood renders cleanly and loops, and the Jukebox steps sensibly.
    static void MusicLibraryTest()
    {
        var ids = new HashSet<string>();
        bool unique = true;
        foreach (var e in MusicLibrary.All)
            unique &= ids.Add(e.Id) && e.Title.Length > 0 && Array.IndexOf(MusicLibrary.Moods, e.Mood) >= 0;
        Check(unique && MusicLibrary.All.Count == 22, "music library: 22 tracks, unique ids, known moods");

        bool ok = true;
        foreach (var e in MusicLibrary.All)
        {
            if (!e.Id.StartsWith("juke:"))
                continue;  // (menu, battle and the bosses are rendered by MusicTest)
            var s = e.Render();
            float peak = 0f;
            bool nan = false;
            foreach (var v in s)
            {
                nan |= float.IsNaN(v) || float.IsInfinity(v);
                peak = Math.Max(peak, Math.Abs(v));
            }
            float secs = s.Length / (float)MenuMusic.Rate;
            bool good = !nan && peak <= 0.81f && peak > 0.2f && secs > 20f && secs < 60f && Math.Abs(s[0] - s[s.Length - 1]) < 0.1f;
            if (!good) Console.WriteLine("  bad track: " + e.Id + " peak " + peak + " length " + secs);
            ok &= good;
        }
        Check(ok, "music library: every extra mood renders cleanly and loops");

        var j = new Jukebox();
        j.Select(MusicLibrary.All[0].Id);
        j.Next();
        string second = j.Selected;
        j.Next();
        j.Previous();
        bool order = second == MusicLibrary.All[1].Id && j.Selected == second;
        j.Select(MusicLibrary.All[MusicLibrary.All.Count - 1].Id);
        j.Next();
        order &= j.Selected == MusicLibrary.All[0].Id;  // wraps round
        j.Shuffle = true;
        bool shuffled = true;
        for (int i = 0; i < 40; i++)
        {
            string before = j.Selected;
            j.Next();
            shuffled &= j.Selected != before && MusicLibrary.IndexOf(j.Selected) >= 0;
        }
        j.Select("no-such-track");
        Check(order && shuffled && j.Selected == null, "jukebox: next / previous / wrap, shuffle never repeats a track, bad ids fall back to automatic");
        Check(MusicLibrary.IsTransient("juke:neon") && MusicLibrary.IsTransient("boss:Lich") && !MusicLibrary.IsTransient("menu"), "jukebox: extra and boss tracks are released when unused");
    }
}
