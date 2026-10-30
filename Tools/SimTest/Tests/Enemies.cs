using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Enemies: early melee rushers, waves 11-13, Spinners, and waves 25-50.
static partial class Program
{
    // Early waves are melee rushers; shooters take over until the melee are gone by wave 13.
    static void MeleeTest(float dt)
    {
        Check(Game.MeleeShare(1) == 1f && Math.Abs(Game.MeleeShare(7) - 0.5f) < 0.01f && Game.MeleeShare(13) == 0f && Game.MeleeShare(30) == 0f,
            "melee share: all of wave 1, half by wave 7, none from wave 13");
        foreach (int wave in new[] { 1, 7, 14 })
        {
            var s = StartBuffed(dt, wave, 0);
            var types = new HashSet<EnemyType>();
            int melee = 0, ranged = 0, enemyShotsFromMelee = 0;
            bool leapSeen = false, meleeHurt = false;
            var counted = new HashSet<int>();
            Run(s, dt, 25, () => s[0].Game.Wave > wave, () =>
            {
                foreach (var e in s[1].Game.Enemies.Values)
                {
                    types.Add(e.Type);
                    if (counted.Add(e.Id))
                    {
                        if (e.Def.Melee) melee++; else ranged++;
                    }
                    leapSeen |= e.Type == EnemyType.Lunger && e.Phase == 2;
                }
            });
            var names = new List<string>();
            foreach (var t in types) names.Add(t.ToString());
            Console.WriteLine("  wave {0}: {1} melee, {2} shooters ({3})", wave, melee, ranged, string.Join(", ", names.ToArray()));
            if (wave == 1)
                Check(melee > 0 && ranged == 0, "wave 1 is all melee");
            else if (wave == 7)
                Check(melee > 0 && ranged > 0, "wave 7 mixes melee and shooters");
            else
                Check(melee == 0 && ranged > 0, "from wave 13 there are no melee enemies");
            s[0].Leave(""); s[1].Leave("");
        }

        // A brute walks into a player and hurts them; melee enemies never fire.
        {
            var s = StartBuffed(dt, 4, 0);
            s[0].Game.Bot = s[1].Game.Bot = false;
            Run(s, dt, 20, () => { foreach (var e in s[0].Game.Enemies.Values) if (e.Type == EnemyType.Brute && e.SpawnT <= 0f) return true; return false; });
            Enemy brute = null;
            foreach (var e in s[0].Game.Enemies.Values)
                if (e.Type == EnemyType.Brute && e.SpawnT <= 0f)
                    brute = e;
            var p = s[0].Game.Players[1];
            float before = p.Run.DamageTaken;  // health alone hides hits (the test player regenerates fast)
            int meleeShots = 0;
            Run(s, dt, 5, null, () =>
            {
                if (brute != null && s[0].Game.Enemies.ContainsKey(brute.Id))
                    p.Pos = brute.Pos + new V2(brute.Radius, 0f);
                // A melee enemy's fire timer is parked at 99 s the first time it comes up, so it never fires
                foreach (var e in s[0].Game.Enemies.Values)
                    // (it ticks down at most ~2.5 s per second, and is re-parked if it ever runs out)
                    if (e.Def.Melee && e.T > 3f && e.SpawnT <= 0f && e.FireT < 99f - e.T * 2.5f)
                        meleeShots++;
            });
            Console.WriteLine("  brute contact: found {0}, damage taken {1:0}", brute != null, p.Run.DamageTaken - before);
            Check(brute != null && p.Run.DamageTaken - before >= 24f, "melee enemies hurt you when they touch you");
            Check(meleeShots == 0, "melee enemies don't shoot");
            s[0].Leave(""); s[1].Leave("");
        }

        // Lungers wind up and leap (seen on the client).
        {
            var s = StartBuffed(dt, 6, 0);
            s[0].Game.Bot = s[1].Game.Bot = false;  // hold fire so the lungers reach us
            Run(s, dt, 6, () => s[0].Game.State == MatchState.Fighting);
            // Waves are random, so make sure there are lungers to watch.
            var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int k = 0; k < 3; k++)
                spawn.Invoke(s[0].Game, new object[] { EnemyType.Lunger, new V2(300f + k * 400f, 120f), 0.5f, 0, false });
            bool windup = false, leap = false;
            Run(s, dt, 40, () => windup && leap, () =>
            {
                foreach (var e in s[1].Game.Enemies.Values)
                    if (e.Type == EnemyType.Lunger)
                    {
                        windup |= e.Phase == 1;
                        leap |= e.Phase == 2;
                    }
                foreach (var p in s[0].Game.Players.Values)
                    p.InvulnT = 1f;
            });
            Check(windup && leap, "lungers telegraph, then leap (seen on client)");
            s[0].Leave(""); s[1].Leave("");
        }
    }

    // Waves 11-13: the new enemy types and elites show up on the client, in sync.
    static void LateGameEnemies(float dt)
    {
        var s = StartBuffed(dt, 11, 6);
        var seen = new HashSet<EnemyType>();
        bool eliteSeen = false;
        int ghosts = 0, frame = 0;
        Run(s, dt, 120, () => s[0].Game.Wave >= 14 || s[0].Game.State == MatchState.GameOver, () =>
        {
            foreach (var e in s[1].Game.Enemies.Values)
            {
                seen.Add(e.Type);
                eliteSeen |= e.Elite;
            }
            if (++frame % 60 == 0)
            {
                for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                ghosts += Ghosts(s);
            }
        });
        var names = new List<string>();
        foreach (var t in seen)
            names.Add(t.ToString());
        Console.WriteLine("  reached wave {0}; client saw: {1}; elite seen: {2}", s[0].Game.Wave, string.Join(", ", names), eliteSeen);
        foreach (var t in new[] { EnemyType.Charger, EnemyType.Splitter, EnemyType.Shard, EnemyType.Mortar, EnemyType.Shielder, EnemyType.Medic })
            Check(seen.Contains(t), "new enemy shows up on the client: " + t);
        Check(eliteSeen, "elite enemies appear from wave 12");
        Check(ghosts == 0, "late-game waves stay in sync (mortar bursts, splits)");
        s[0].Leave("");
        s[1].Leave("");
    }

    // Spinners spray in bursts with breathers in between (they used to spray nonstop).
    static void SpinnerTest(float dt)
    {
        var s = StartBuffed(dt, 6, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => s[0].Game.State == MatchState.Fighting);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var spinner = (Enemy)spawn.Invoke(s[0].Game, new object[] { EnemyType.Spinner, new V2(800f, 300f), 0.3f, 0, false });
        var seen = new HashSet<int>();
        var shotTimes = new List<float>();
        float t = 0f;
        bool clientBursting = false, clientResting = false;
        Run(s, dt, 10, null, () =>
        {
            t += dt;
            foreach (var e in s[0].Game.Enemies.Values)
                if (e != spinner) e.Hp = 0f;  // only the spinner's bullets
            foreach (var p in s[0].Game.Players.Values) p.InvulnT = 1f;
            foreach (var b in s[0].Game.Bullets.Values)
                if (b.Source == EnemyType.Spinner && seen.Add(b.Id))
                    shotTimes.Add(t);
            var ce = Find(s[1], EnemyType.Spinner);
            if (ce != null && !s[1].Game.IsSpawning(ce))
            {
                clientBursting |= ce.Phase == 1;
                clientResting |= ce.Phase == 0;
            }
        });
        float longestGap = 0f;
        int bursts = shotTimes.Count > 0 ? 1 : 0;
        for (int i = 1; i < shotTimes.Count; i++)
        {
            float gap = shotTimes[i] - shotTimes[i - 1];
            longestGap = Math.Max(longestGap, gap);
            if (gap > 1f) bursts++;
        }
        float perSecond = shotTimes.Count / 10f;
        Console.WriteLine("  spinner: {0} bullets in 10 s ({1:0.0}/s, was ~27/s+), {2} bursts, longest gap {3:0.0}s, client saw burst {4} / rest {5}",
            shotTimes.Count, perSecond, bursts, longestGap, clientBursting, clientResting);
        Check(bursts >= 2 && longestGap > 1.5f && perSecond < 20f, "Spinners fire in bursts with pauses between");
        Check(clientBursting && clientResting, "clients see when a Spinner is bursting");
        s[0].Leave(""); s[1].Leave("");
    }

    // Shielders' shields wear down: blocked hits crack them and they crumble on their own, then shatter.
    static void ShielderTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var s = StartBuffed(dt, 9, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var me = g.Players[1];
        Action Others = () => { foreach (var e in g.Enemies.Values) if (e.Type != EnemyType.Shielder) e.Hp = 0f; foreach (var p in g.Players.Values) p.InvulnT = 1f; };

        // Shoot its shield from the front: it cracks, then shatters, and then shots get through.
        var sh = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shielder, new V2(800f, 300f), 0.1f, 0, false });
        me.Pos = new V2(800f, 600f);
        var phases = new List<byte>();
        float hpBefore = 0f; bool clientBroken = false;
        Run(s, dt, 8f, () => sh.Phase >= Game.ShieldBroken, () =>
        {
            Others();
            me.Pos = new V2(800f, 600f);
            sh.Pos = new V2(800f, 300f);
            if (phases.Count == 0 || phases[phases.Count - 1] != sh.Phase) phases.Add(sh.Phase);
            hpBefore = sh.Hp;
            shoot.Invoke(g, new object[] { Defs.DpsShot, sh.Pos + new V2(0f, 60f), new V2(0f, -900f), 0f, 1f, 12f, 1, false, 0f });
        });
        Run(s, dt, 0.3f, null, () => { Others(); me.Pos = new V2(800f, 600f); sh.Pos = new V2(800f, 300f); });
        var ce = Find(s[1], EnemyType.Shielder);
        clientBroken = ce != null && ce.Phase >= Game.ShieldBroken;
        float hpAt = sh.Hp;
        Run(s, dt, 1f, null, () =>
        {
            Others(); me.Pos = new V2(800f, 600f); sh.Pos = new V2(800f, 300f);
            shoot.Invoke(g, new object[] { Defs.DpsShot, sh.Pos + new V2(0f, 60f), new V2(0f, -900f), 0f, 1f, 12f, 1, false, 0f });
        });
        Console.WriteLine("  shielder: shield steps {0}, health while blocking {1:0} -> after it broke {2:0}, client saw it break {3}",
            string.Join(">", phases), hpBefore, sh.Hp, clientBroken);
        Check(phases.Count >= 4 && phases[0] == 0 && sh.Phase >= Game.ShieldBroken && clientBroken, "blocked shots crack a Shielder's shield until it shatters (seen on client)");
        Check(sh.Hp < hpAt, "once the shield is gone, shots from the front hurt it");

        // Left alone, a shield crumbles by itself.
        sh.Hp = 0f;
        var sh2 = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shielder, new V2(800f, 300f), 0.1f, 0, false });
        Run(s, dt, 10f, null, () => { Others(); me.Pos = new V2(200f, 800f); });
        Console.WriteLine("  shielder left alone 10 s: shield step {0}", sh2.Phase);
        Check(sh2.Phase >= 1 && sh2.Phase < Game.ShieldBroken, "a Shielder's shield cracks over time on its own");
        s[0].Leave(""); s[1].Leave("");
    }

    /// Waves 25-50: late enemies, the six new bosses' mechanics, victory and endless mode.
    static void LateWaves(float dt)
    {
        // Wave 31: all four late-game enemy types are in the mix.
        {
            var s = StartBuffed(dt, 31, 30);
            var seen = new HashSet<EnemyType>();
            int ghosts = 0, frame = 0;
            // 13 enemy types are in the mix by now, so give it a few waves to roll all four.
            Run(s, dt, 160, () => seen.Contains(EnemyType.Phantom) && seen.Contains(EnemyType.Hexer) && seen.Contains(EnemyType.Juggernaut) && seen.Contains(EnemyType.Nest), () =>
            {
                // This only checks which enemies appear, so keep the bots from dying before they've all spawned.
                foreach (var p in s[0].Game.Players.Values)
                    p.InvulnT = 1f;
                foreach (var e in s[1].Game.Enemies.Values)
                    seen.Add(e.Type);
                if (++frame % 60 == 0)
                {
                    for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                    ghosts += Ghosts(s);
                }
            });
            foreach (var t in new[] { EnemyType.Phantom, EnemyType.Hexer, EnemyType.Juggernaut, EnemyType.Nest })
                Check(seen.Contains(t), "late-game enemy shows up on the client: " + t);
            Check(ghosts == 0, "late-game enemies stay in sync");
            s[0].Leave(""); s[1].Leave("");
        }

        foreach (int wave in new[] { 25, 30, 35, 40, 45, 50 })
        {
            var s = StartBuffed(dt, wave, 60);
            Run(s, dt, 10, () => BossAlive(s[0]));
            EnemyType boss = EnemyType.Grunt;
            foreach (var e in s[0].Game.Enemies.Values)
                if (e.Def.IsBoss)
                    boss = e.Type;
            bool mechanic = false, devourerHealed = false;
            float hungryHp = -1f;
            byte eclipsePhase = 0;
            int ghosts = 0, frame = 0;
            Run(s, dt, 400, () => !BossAlive(s[0]) && s[0].Game.State != MatchState.Fighting || s[0].Game.State == MatchState.GameOver, () =>
            {
                var cb = Find(s[1], boss);
                switch (boss)
                {
                    case EnemyType.Colossus: mechanic |= cb?.Colossus != null && cb.Colossus.Rings.Count > 0; break;  // its stomp rings (Colossus.cs)
                    case EnemyType.Duelist: mechanic |= Find(s[1], EnemyType.Decoy) != null; break;
                    case EnemyType.Lich:
                    {
                        var ph = Find(s[0], EnemyType.Phylactery);
                        if (cb != null && cb.Phase == 1 && ph != null)
                        {
                            mechanic = true;
                            ph.Hp = 0f;  // the bots only shoot the nearest enemy, so break it for them
                        }
                        break;
                    }
                    case EnemyType.StormHerald: mechanic |= s[1].Game.Hazards.Count > 0; break;
                    case EnemyType.Devourer:
                    {
                        var hb = Find(s[0], EnemyType.Devourer);
                        if (hb != null && Game.Hungry(hb))
                        {
                            mechanic = true;
                            if (hungryHp < 0f)
                                hungryHp = hb.Hp;
                            else if (hb.Hp > hungryHp + 1f)
                                devourerHealed = true;
                        }
                        else
                            hungryHp = -1f;
                        break;
                    }
                    case EnemyType.Eclipse:
                        if (cb != null)
                            eclipsePhase = Math.Max(eclipsePhase, cb.Phase);
                        mechanic = eclipsePhase == 2;
                        break;
                }
                if (++frame % 60 == 0)
                {
                    for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                    ghosts += Ghosts(s);
                }
            });
            bool dead = !BossAlive(s[0]) && s[0].Game.State != MatchState.GameOver;
            string name = Defs.Get(boss).Name;
            Console.WriteLine("  wave {0}: {1} defeated {2}, mechanic seen {3}, state {4}", wave, name, dead, mechanic, s[0].Game.State);
            Check(dead && mechanic && ghosts == 0 && (boss != EnemyType.Devourer || devourerHealed),
                "wave " + wave + " boss " + name + " works (its mechanic shows on the client) and can be beaten");
            if (wave == 50)
            {
                Run(s, dt, 1);
                Check(s[0].Game.State == MatchState.Victory && s[1].Game.State == MatchState.Victory, "beating The Eclipse at wave 50 wins the game on both screens");
                // A recording that won at wave 50 opens again for endless, so its real end is kept.
                string recFolder = Path.Combine(Path.GetTempPath(), "arena-endless-" + Guid.NewGuid().ToString("N").Substring(0, 6));
                var rec = s[0].Game.Recorder = new SessionRecorder(recFolder);
                rec.Begin(s[0].Game, 50);
                rec.End(s[0].Game, "victory");
                s[0].Game.ContinueEndless();
                Run(s, dt, 12, () => s[1].Game.Wave > 50 && s[1].Game.State == MatchState.Fighting);
                Check(s[1].Game.Endless && s[1].Game.Wave == 51, "endless mode carries on past wave 50");
                s[0].Leave(""); s[1].Leave("");
                string recText = File.Exists(rec.Path) ? File.ReadAllText(rec.Path) : "";
                Check(recText.Contains("\"outcome\": \"left (endless)\"") && recText.Contains("\"endless\": true") && recText.Contains("\"wave\": 51"),
                    "a recorded run that goes on into endless records its real end, not the wave-50 victory");
                try { Directory.Delete(recFolder, true); } catch (IOException) { }
                continue;
            }
            s[0].Leave(""); s[1].Leave("");
        }
    }
}
