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

    static byte[] MakeWav(int format, int bits, int channels, int rate, double seconds, double hz)
    {
        int frames = (int)(rate * seconds), bytes = bits / 8;
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + frames * channels * bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)format); w.Write((short)channels); w.Write(rate);
        w.Write(rate * channels * bytes); w.Write((short)(channels * bytes)); w.Write((short)bits);
        w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(frames * channels * bytes);
        for (int f = 0; f < frames; f++)
            for (int c = 0; c < channels; c++)
            {
                double v = 0.5 * Math.Sin(2 * Math.PI * hz * f / rate);
                if (bits == 8) w.Write((byte)(128 + v * 127));
                else if (bits == 16) w.Write((short)(v * 32767));
                else if (bits == 24) { int x = (int)(v * 8388607); w.Write((byte)x); w.Write((byte)(x >> 8)); w.Write((byte)(x >> 16)); }
                else if (format == 3) w.Write((float)v);
                else w.Write((int)(v * 2147483647));
            }
        return ms.ToArray();
    }

    // Your own tracks: WAV decoding (every common format, any rate), the music folder scan, and the library/jukebox picking them up.
    static void CustomMusicTest()
    {
        bool ok = true;
        foreach (var spec in new[] { new[] { 1, 8, 1, 44100 }, new[] { 1, 16, 2, 22050 }, new[] { 1, 24, 2, 48000 }, new[] { 1, 32, 1, 44100 }, new[] { 3, 32, 2, 96000 } })
        {
            var s = CustomMusic.DecodeWav(MakeWav(spec[0], spec[1], spec[2], spec[3], 2.0, 440));
            float peak = 0f;
            if (s != null)
                foreach (var v in s) peak = Math.Max(peak, Math.Abs(v));
            bool good = s != null && Math.Abs(s.Length - 2 * MenuMusic.Rate) <= 2 && Math.Abs(peak - 0.8f) < 0.02f && Math.Abs(s[0]) < 0.05f && Math.Abs(s[s.Length - 1]) < 0.05f;
            if (!good) Console.WriteLine("  bad wav: format " + spec[0] + " bits " + spec[1] + " ch " + spec[2] + " rate " + spec[3] + (s == null ? " (null)" : " len " + s.Length + " peak " + peak));
            ok &= good;
        }
        Check(ok, "custom music: 8 / 16 / 24 / 32-bit and float WAVs, mono or stereo, any rate, become mono at the game's rate, peak 0.8, quiet ends");
        Check(CustomMusic.DecodeWav(new byte[100]) == null && CustomMusic.DecodeWav(null) == null && CustomMusic.DecodeWav(MakeWav(2, 16, 1, 44100, 1.0, 440)) == null,
            "custom music: a file that isn't a plain WAV is refused (not a crash)");

        string dir = Path.Combine(Path.GetTempPath(), "arena-music-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Check(CustomMusic.Scan(dir).Count == 0 && Directory.Exists(dir), "custom music: the folder is made if it's missing");
            File.WriteAllBytes(Path.Combine(dir, "b_song.wav"), MakeWav(1, 16, 1, 44100, 1.5, 330));
            File.WriteAllBytes(Path.Combine(dir, "A Song.MP3"), new byte[10]);
            File.WriteAllText(Path.Combine(dir, "notes.txt"), "not music");
            var files = CustomMusic.Scan(dir);
            Check(files.Count == 2 && CustomMusic.TitleOf(files[0]) == "A Song" && CustomMusic.TitleOf(files[1]) == "b song", "custom music: scan finds .wav / .mp3 / .ogg (any case), skips the rest, sorted by name");

            int builtIn = MusicLibrary.All.Count;
            MusicLibrary.SetCustom(CustomMusic.Entries(files));
            var wav = MusicLibrary.Find("file:b_song.wav");
            var mp3 = MusicLibrary.Find("file:A Song.MP3");
            var played = wav == null ? null : wav.Render();
            bool lib = MusicLibrary.All.Count == builtIn + 2 && wav != null && mp3 != null && !wav.NeedsUnity && mp3.NeedsUnity && wav.Mood == "Custom"
                && played != null && Math.Abs(played.Length - (int)(1.5 * MenuMusic.Rate)) <= 2 && MusicLibrary.IsTransient("file:x");
            Check(lib, "custom music: files appear in the music player's list after the built-in tracks and a WAV renders");

            var j = new Jukebox();
            j.Select(MusicLibrary.All[builtIn - 1].Id);
            j.Next();
            bool reaches = j.Selected == "file:A Song.MP3";
            CustomMusic.Give("file:A Song.MP3", new float[1000]);
            reaches &= mp3.Render().Length == 1000 && !CustomMusic.HasDecoded("file:A Song.MP3");  // handed over once, then freed
            MusicLibrary.SetCustom(new System.Collections.Generic.List<MusicLibrary.Entry>());
            Check(reaches && MusicLibrary.All.Count == builtIn && MusicLibrary.Find("file:b_song.wav") == null, "custom music: the jukebox steps into your tracks, Unity-decoded audio is handed over once, and a rescan drops removed files");
        }
        finally
        {
            MusicLibrary.SetCustom(new System.Collections.Generic.List<MusicLibrary.Entry>());
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }
    }
}
