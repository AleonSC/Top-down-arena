using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Balance reports (not tests): boss time-to-kill, bullets on screen, weapon DPS.
static partial class Program
{
    // Balance probe: a full run with invulnerable bots that level up and pick upgrades as they go,
    // timing how long each boss survives. (Not part of the suite; run with: run.sh 1 1 ttk [last wave])
    static void BossTtk(float dt, int lastWave)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Game.Bot = true;
        host.Host(port, "Host", Role.Tank, 0, 0, Kit.Soldier);
        var client = new Session();
        client.Game.Bot = true;
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); }
        var born = new Dictionary<int, float>();
        float t = 0f;
        for (long frame = 0; frame < 60L * 60 * 90; frame++)
        {
            Step(host, dt);
            Step(client, dt);
            t += dt;
            foreach (var p in host.Game.Players.Values)
            {
                p.InvulnT = 1f;
                p.Downed = false;
                p.Hp = p.MaxHp;
            }
            foreach (var e in host.Game.Enemies.Values)
                if (e.Def.IsBoss && e.SpawnT <= 0f && !born.ContainsKey(e.Id))
                {
                    born[e.Id] = t;
                    Console.WriteLine("  wave {0}: {1} appears with {2:0} HP (players level {3}/{4}, {5}/{6} picks)", host.Game.Wave, e.Def.Name, e.MaxHp,
                        host.Game.Players[1].Level, host.Game.Players[2].Level, host.Game.Players[1].Taken.Count, host.Game.Players[2].Taken.Count);
                }
            foreach (var kv in new List<KeyValuePair<int, float>>(born))
                if (!host.Game.Enemies.ContainsKey(kv.Key))
                {
                    Console.WriteLine("      dead after {0:0.0} s", t - kv.Value);
                    born.Remove(kv.Key);
                }
            if (host.Game.Wave > lastWave || host.Game.State == MatchState.Victory)
                break;
            if (frame % 600 == 0)
                Thread.Sleep(1);
        }
        host.Leave(""); client.Leave("");
    }

    // How crowded the screen gets: enemy bullets on screen through a wave (bots playing, buffed so they survive).
    static void BulletCountReport(float dt)
    {
        foreach (int wave in new[] { 3, 6, 9, 12, 15, 18, 21, 24, 27 })
        {
            var s = StartBuffed(dt, wave, 6);
            foreach (var g in new[] { s[0].Game, s[1].Game }) foreach (var p in g.Players.Values) p.DevGod = true;
            s[0].Game.DevSetGod(1, true); s[0].Game.DevSetGod(2, true);
            Run(s, dt, 8, () => s[0].Game.State == MatchState.Fighting);
            var counts = new List<int>();
            bool boss = false;
            Run(s, dt, 40, () => s[0].Game.State != MatchState.Fighting, () =>
            {
                counts.Add(s[0].Game.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind)));
                boss |= s[0].Game.Enemies.Values.Any(e => e.Def.IsBoss);
            });
            counts.Sort();
            Console.WriteLine("  wave {0,2}{1}: enemy bullets avg {2,4:0} median {3,4} p90 {4,4} max {5,4}", wave, boss ? " (boss)" : "       ",
                counts.Count > 0 ? counts.Average() : 0, counts.Count > 0 ? counts[counts.Count / 2] : 0, counts.Count > 0 ? counts[counts.Count * 9 / 10] : 0, counts.Count > 0 ? counts.Last() : 0);
            s[0].Leave(""); s[1].Leave("");
        }
    }

    // Damage per second of every weapon on one big target (melee from up close, the rest from 250 px).
    static void WeaponDpsReport(float dt)
    {
        var host = new Session { AutoStart = 1, StartWave = 3 };
        int port = nextPort++;
        host.Host(port, "A", Role.Tank, 0);
        host.StartMatch();
        var g = host.Game;
        for (int i = 0; i < 300 && !g.Running; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var me = g.Players[1];
        g.DevSetGod(1, true);
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
            foreach (Kit kit in new[] { Kit.Soldier, Kit.Mage, Kit.Summoner, Kit.Knight })
            {
                var line = new List<string>();
                for (int w = 0; w < 3; w++)
                {
                    var weapon = Defs.Weapon(role, kit, w);
                    foreach (var e in g.Enemies.Values) e.Hp = 0f;
                    for (int k = 0; k < 3; k++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
                    foreach (var id in g.Minions.Keys.ToList()) g.Minions.Remove(id);
                    me.Weapon = weapon;
                    var target = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Juggernaut, new V2(800f, 450f), 0f, 0, false });
                    target.Hp = target.MaxHp = 1e7f;
                    float range = weapon.Melee ? Game.MeleeRange(weapon.Id) * 0.7f + target.Radius : 250f;
                    me.Pos = new V2(800f - range, 450f);
                    float t = 0f, startHp = target.Hp;
                    for (int f = 0; f < 60 * 8; f++)
                    {
                        target.Pos = new V2(800f, 450f); target.FrozenT = 1f;
                        me.Pos = new V2(800f - range, 450f);
                        foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
                        host.Poll();
                        g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = target.Pos });
                        t += dt;
                    }
                    line.Add(string.Format("{0} {1:0}", weapon.Name, (startHp - target.Hp) / t));
                }
                Console.WriteLine("  {0,-6} {1,-8} {2}", role, kit, string.Join(" | ", line));
            }
        host.Leave("");
    }
}
