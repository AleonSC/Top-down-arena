using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The melee rushers' sounds (MeleeSounds.cs): quieter with distance, one whirr for Buzzsaw contact, distinct deaths that merge when several of a kind fall together.
static partial class Program
{
    static void MeleeSoundsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var s = StartBuffed(dt, 5, 0);
        var g = s[0].Game;
        g.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var upd = typeof(Game).GetMethod("UpdateMeleeSounds", flags);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        var p = g.Players[1];
        g.Players[2].Pos = new V2(60f, 60f);
        p.Pos = new V2(100f, 100f);
        Func<EnemyType, V2, Enemy> put = (t, at) => { var z = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false }); z.SpawnT = 0f; return z; };
        Action<float> tick = d => upd.Invoke(g, new object[] { d });
        Action clear = () => { queue.Clear(); foreach (var e in g.Enemies.Values.ToList()) g.Enemies.Remove(e.Id); tick(0.5f); g.Sounds.Clear(); };
        Func<Sfx, List<SoundCue>> heard = f => g.Sounds.Where(c => c.Sfx == f).ToList();
        clear();

        // Distance: full volume close, fading with distance, never silent, and louder the nearer it is.
        float q0 = g.QuietAt(p.Pos + new V2(150f, 0f)), q1 = g.QuietAt(p.Pos + new V2(500f, 0f)), q2 = g.QuietAt(p.Pos + new V2(800f, 0f)), q3 = g.QuietAt(p.Pos + new V2(1500f, 800f));
        Check(q0 == 0f, "a sound 150 px away is at full volume");
        Check(q0 < q1 && q1 < q2 && q2 < q3, "further sounds are quieter (" + q1.ToString("0.00") + ", " + q2.ToString("0.00") + ", " + q3.ToString("0.00") + ")");
        Check(q3 < 1f - Game.EarFloor + 0.001f && q3 > 0.8f, "the furthest keeps a faint " + (1f - q3).ToString("0.00") + " of its volume");
        Check(new SoundCue { Sfx = Sfx.Hit, Quiet = 0.5f, Ally = true }.Volume < 0.25f, "an ally's far sound stacks both reductions");

        // A Lunger's tell is full volume near you and quiet far away.
        var near = put(EnemyType.Lunger, p.Pos + new V2(220f, 0f));
        var far = put(EnemyType.Lunger, new V2(1500f, 800f));
        tick(dt); g.Sounds.Clear();
        near.Phase = 1; far.Phase = 1;
        tick(dt);
        var tells = heard(Sfx.LungerTell);
        Check(tells.Count == 2, "both Lungers' tells are heard");
        Check(tells.Count == 2 && tells.Min(c => c.Quiet) == 0f && tells.Max(c => c.Quiet) > 0.7f, "the near tell is full, the far one is quiet");
        near.Phase = 2; far.Phase = 2; tick(dt);
        Check(heard(Sfx.LungerLeap).Count == 2, "the leap is heard");
        near.Phase = 3; far.Phase = 3; tick(dt);
        Check(heard(Sfx.LungerLand).Count == 2, "the landing is heard");
        clear();

        // Spike Block: shake, lunge, rest.
        var sb = put(EnemyType.SpikeBlock, p.Pos + new V2(200f, 0f));
        tick(dt); g.Sounds.Clear();
        sb.Phase = 1; tick(dt);
        sb.Phase = 2; tick(dt);
        sb.Phase = 3; tick(dt);
        Check(heard(Sfx.SpikeBlockShake).Count == 1 && heard(Sfx.SpikeBlockLunge).Count == 1 && heard(Sfx.SpikeBlockRest).Count == 1, "a Spike Block's shake, lunge and rest each sound once");
        clear();

        // Footsteps: every walker has a step sound; a crowd makes one a frame, at the nearest one's volume; standing still is silent.
        {
            var walkers = new Dictionary<EnemyType, Sfx> { { EnemyType.Swarmer, Sfx.SwarmerPatter }, { EnemyType.Spiker, Sfx.SpikerSkitter }, { EnemyType.Brute, Sfx.BruteStep }, { EnemyType.Lunger, Sfx.LungerPad }, { EnemyType.SpikeBlock, Sfx.SpikeBlockRock } };
            foreach (var w in walkers)
            {
                var a = put(w.Key, p.Pos + new V2(200f, 0f));
                var b = put(w.Key, new V2(1500f, 800f));
                var c = put(w.Key, p.Pos + new V2(0f, 400f));
                tick(dt); g.Sounds.Clear();
                tick(0.5f);
                Check(heard(w.Value).Count == 0, w.Key + ": standing still makes no steps");
                a.Pos += new V2(3f, 0f); b.Pos += new V2(3f, 0f); c.Pos += new V2(0f, 3f);
                tick(0.5f);
                var steps = heard(w.Value);
                Check(steps.Count == 1 && steps[0].Quiet == 0f, w.Key + ": three walking make one step, at the nearest one's volume (" + steps.Count + ")");
                clear();
            }
        }

        // Buzzsaw: no tick per touch. One whirr that rises while it touches you and fades after.
        var saw = put(EnemyType.Buzzsaw, p.Pos + new V2(300f, 0f));
        tick(dt);
        Check(g.SawWhirrLevel == 0f, "a Buzzsaw that isn't touching anyone is silent");
        saw.Pos = p.Pos;
        for (int i = 0; i < 30; i++) tick(dt);
        Check(g.SawWhirrLevel > 0.95f, "touching you, its whirr is up (" + g.SawWhirrLevel.ToString("0.00") + ")");
        Check(g.Sounds.All(c => c.Sfx.ToString().StartsWith("Saw") == false), "no tick cue for each touch");
        saw.Pos = p.Pos + new V2(300f, 0f);
        for (int i = 0; i < 90; i++) tick(dt);
        Check(g.SawWhirrLevel == 0f, "the whirr fades out when it lets go");
        clear();

        // Deaths: each kind has its own, and several of a kind falling together make one.
        var pack = new List<Enemy>();
        for (int i = 0; i < 4; i++) pack.Add(put(EnemyType.Swarmer, p.Pos + new V2(200f + i * 10f, 0f)));
        var brute = put(EnemyType.Brute, p.Pos + new V2(0f, 250f));
        var spiker = put(EnemyType.Spiker, p.Pos + new V2(0f, 300f));
        var lunger = put(EnemyType.Lunger, p.Pos + new V2(0f, 350f));
        tick(dt); g.Sounds.Clear();
        foreach (var e in pack.Concat(new[] { brute, spiker, lunger })) g.Enemies.Remove(e.Id);
        tick(dt);
        Check(heard(Sfx.SwarmerDie).Count == 1, "four Swarmers dying together make one sound (" + heard(Sfx.SwarmerDie).Count + ")");
        Check(heard(Sfx.BruteDie).Count == 1 && heard(Sfx.SpikerDie).Count == 1 && heard(Sfx.LungerDie).Count == 1, "Brute, Spiker and Lunger each have their own death");
        // A second one a moment later is still the same moment; a third a second later is its own.
        var again = put(EnemyType.Swarmer, p.Pos + new V2(200f, 0f)); tick(dt); g.Sounds.Clear();
        g.Enemies.Remove(again.Id); tick(0.05f);
        Check(heard(Sfx.SwarmerDie).Count == 0, "a Swarmer dying 0.05 s after the pack shares its sound");
        var later = put(EnemyType.Swarmer, p.Pos + new V2(200f, 0f)); tick(dt); g.Sounds.Clear();
        g.Enemies.Remove(later.Id); tick(1f);
        Check(heard(Sfx.SwarmerDie).Count == 1, "a Swarmer dying a second later gets its own");
        clear();

        // The merged death is as loud as the nearest of them.
        var nearPop = put(EnemyType.Spiker, p.Pos + new V2(200f, 0f));
        var farPop = put(EnemyType.Spiker, new V2(1500f, 800f));
        tick(1f); g.Sounds.Clear();
        g.Enemies.Remove(nearPop.Id); g.Enemies.Remove(farPop.Id);
        tick(1f);
        var pops = heard(Sfx.SpikerDie);
        Check(pops.Count == 1 && pops[0].Quiet == 0f, "a near and a far Spiker dying together sound at the near one's volume");

        clear();
        s[0].Leave(""); s[1].Leave("");
    }
}
