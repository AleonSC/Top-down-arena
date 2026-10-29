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
}
