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
        var host = HostAs(new Session { AutoStart = 2, StartWave = 1 }, port, true, "Host", Role.Tank, 0, 0, Kit.Soldier);
        var client = JoinAs(port, true, "Friend", Role.Dps, 0, 0, Kit.Soldier);
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

    // Damage per second of every weapon on one big target (melee from up close, the rest from 250 px, or 3/4 of their reach if that's shorter).
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
            foreach (Kit kit in new[] { Kit.Soldier, Kit.Elementalist, Kit.Summoner, Kit.Knight })
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
                    me.Weapon = weapon;
                    float range = weapon.Melee ? Game.MeleeRange(weapon.Id) * 0.7f + target.Radius : Math.Min(250f, g.WeaponRange(me) * 0.75f);  // short-range guns stand in reach
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

    // How much damage each DPS ultimate adds against one boss-sized dummy: 12 s of firing with the
    // ultimate cast at the start, minus 12 s of firing without it (no upgrades, first weapon).
    static void UltimateDpsReport(float dt)
    {
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        for (int k = 0; k < Defs.KitCount; k++)
        {
            var kit = (Kit)k;
            float[] dealt = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                var host = new Session { AutoStart = 1, StartWave = 3 };
                host.Host(nextPort++, "A", Role.Dps, 0, 0, kit);
                host.StartMatch();
                var g = host.Game;
                for (int i = 0; i < 300 && !g.Running; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
                var me = g.Players[1];
                g.DevSetGod(1, true);
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
                for (int i = 0; i < 3; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
                var target = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Juggernaut, new V2(800f, 450f), 0f, 0, false });
                target.Hp = target.MaxHp = 1e7f;
                me.Slots[Upgrades.UltSlot].Charges = 1;
                me.Slots[Upgrades.UltSlot].Timer = 0f;
                float range = me.Weapon.Melee ? Game.MeleeRange(me.Weapon.Id) * 0.7f + target.Radius : Math.Min(250f, g.WeaponRange(me) * 0.75f);
                float start = target.Hp;
                for (int f = 0; f < 60 * 12; f++)
                {
                    target.Pos = new V2(800f, 450f); target.FrozenT = 1f;
                    me.Pos = new V2(800f - range, 450f);
                    foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
                    host.Poll();
                    g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = target.Pos, Ultimate = pass == 1 && f < 10 });
                }
                dealt[pass] = start - target.Hp;
                host.Leave("");
            }
            Console.WriteLine("  {0,-10} {1,-20} weapon {2,6:0} in 12 s, with ultimate {3,6:0}: ultimate adds {4,6:0}",
                kit, Upgrades.Ability(Upgrades.Ultimate(Role.Dps, kit)).Name, dealt[0], dealt[1], dealt[1] - dealt[0]);
        }
    }

    // Which weapons ignore which upgrades: every weapon fires at a pinned cluster of enemies (one at the cursor, one
    // behind it, two to the sides, all at 30% health for Executioner) for 6 s with no upgrades, then with 3 stacks of
    // each weapon upgrade. An upgrade that changes neither the damage, the slow on enemies nor your healing is listed.
    // Homing Rounds are also tried with the cursor 45 px beside the enemy, where they should fix the miss.
    static void UpgradeCoverageReport(float dt)
    {
        string[] ups = { "Sharpened Rounds", "Quick Trigger", "Multishot", "Barrage", "Piercing Rounds", "Swarm Rounds", "Royal Jelly",
            "Executioner", "Twin Fang", "Collapse Rounds", "Graviton Rounds", "Dark Matter", "Homing Rounds", "Ricochet",
            "Incendiary Rounds", "Cryo Rounds", "Shock Rounds", "Explosive Rounds", "Velocity Rounds" };
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var missing = new Dictionary<string, List<string>>();
        var gains = new Dictionary<string, List<float>>(); var gainNames = new Dictionary<string, List<string>>();
        foreach (var u in ups) { missing[u] = new List<string>(); gains[u] = new List<float>(); gainNames[u] = new List<string>(); }
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
            for (int k = 0; k < Defs.KitCount; k++)
            {
                var kit = (Kit)k;
                var host = new Session { AutoStart = 1, StartWave = 3 };
                host.Host(nextPort++, "A", role, 0, 0, kit);
                host.StartMatch();
                var g = host.Game;
                for (int i = 0; i < 300 && !g.Running; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
                var me = g.Players[1];
                g.DevSetGod(1, true);
                for (int w = 0; w < Defs.WeaponsPerRole; w++)
                {
                    var weapon = Defs.Weapon(role, kit, w);
                    // Returns damage dealt, the strongest slow seen (1 = none) and the health you gained.
                    Func<string, bool, float[]> trial = (up, offset) =>
                    {
                        foreach (var e in g.Enemies.Values) e.Hp = 0f;
                        for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
                        g.Minions.Clear(); g.Bullets.Clear(); g.Zones.Clear();
                        me.Taken.Clear();
                        int idx = up == null ? -1 : Array.FindIndex(Upgrades.All, x => x.Name == up);
                        for (int n = 0; up != null && n < Math.Min(3, Upgrades.All[idx].MaxStacks <= 0 ? 3 : Upgrades.All[idx].MaxStacks); n++) me.Taken.Add(idx);
                        g.GrantUpgrade(1, Array.FindIndex(Upgrades.All, x => x.Name == "Vitality"));  // recalc (and a harmless card)
                        me.Weapon = weapon;
                        me.BowShotDraw = 0f;
                        float range = weapon.Melee ? Game.MeleeRange(weapon.Id) * 0.7f + 14f : Math.Min(220f, g.WeaponRange(me) * 0.7f);
                        V2 at = new V2(800f, 450f), from = new V2(800f - range, 450f);
                        var spots = new[] { at, at + new V2(60f, 0f), at + new V2(15f, 70f), at + new V2(15f, -70f) };
                        var foes = spots.Select(pt => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, pt, 0f, 0, false }); e.MaxHp = 1e6f; e.Hp = 3e5f; e.SpawnT = 0f; return e; }).ToList();
                        float start = foes.Sum(e => e.Hp), slow = 1f, healed = 0f;
                        V2 aim = offset ? at + new V2(0f, 45f) : at;
                        for (int f = 0; f < 60 * 6; f++)
                        {
                            for (int j = 0; j < foes.Count; j++) { foes[j].Pos = spots[j]; foes[j].FireT = 99f; if (foes[j].Hp < 1e5f) foes[j].Hp = 3e5f - 1e4f; }
                            if (offset) foes.Skip(1).ToList().ForEach(e => e.Pos = new V2(100f, 100f + 60f * foes.IndexOf(e)));
                            me.Pos = from;
                            me.Hp = Math.Min(me.Hp, me.MaxHp * 0.5f);
                            float hp0 = me.Hp;
                            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
                            host.Poll();
                            bool fire = !Game.ChargeWeapon(weapon.Id) || (f % 72) < 60;
                            g.FixedStep(dt, new PlayerInput { Fire = fire, AimAt = aim });
                            healed += Math.Max(0f, me.Hp - hp0);
                            foreach (var e in foes) slow = Math.Min(slow, e.SlowT > 0f ? e.SlowMult : 1f);
                        }
                        return new[] { start - foes.Sum(e => e.Hp), slow, healed };
                    };
                    var b0 = trial(null, false);
                    var bo = trial(null, true);
                    var effects = new List<string>();
                    foreach (var up in ups)
                    {
                        var r = trial(up, false);
                        gains[up].Add(r[0] / Math.Max(1f, b0[0]) - 1f); gainNames[up].Add(weapon.Name);
                        bool hit = Math.Abs(r[0] - b0[0]) > 0.03f * Math.Max(1f, b0[0]) || r[1] < b0[1] - 0.01f || r[2] > b0[2] + 2f;
                        if (!hit && up == "Homing Rounds")
                        {
                            var ro = trial(up, true);
                            hit = Math.Abs(ro[0] - bo[0]) > 0.03f * Math.Max(1f, bo[0]);
                        }
                        if (!hit)
                            missing[up].Add(weapon.Name);
                        else
                            effects.Add(up);
                    }
                    Console.WriteLine("  {0,-6} {1,-10} {2,-18} base {3,7:0}  ignores: {4}", role, kit, weapon.Name, b0[0],
                        string.Join(", ", ups.Where(u => !effects.Contains(u))));
                }
                host.Leave("");
            }
        Console.WriteLine();
        foreach (var kv in missing)
            Console.WriteLine("  {0,-18} no effect on {1,2}: {2}", kv.Key, kv.Value.Count, string.Join(", ", kv.Value));
        Console.WriteLine();
        Console.WriteLine("  Damage change at 3 stacks (or max), across all weapons: median, lowest, highest");
        foreach (var kv in gains.OrderByDescending(x => x.Value.OrderBy(v => v).ElementAt(x.Value.Count / 2)))
        {
            var v = kv.Value.OrderBy(x => x).ToList();
            var names = gainNames[kv.Key];
            string lo = names[kv.Value.IndexOf(v[0])], hi = names[kv.Value.IndexOf(v[v.Count - 1])];
            var worse = kv.Value.Select((x, j) => new { x, n = names[j] }).Where(o => o.x < -0.05f).Select(o => o.n + string.Format(" {0:0}%", o.x * 100f));
            Console.WriteLine("  {0,-18} {1,5:+0;-0}%  {2,5:+0;-0}% ({4})  {3,6:+0;-0}% ({5})", kv.Key, v[v.Count / 2] * 100f, v[0] * 100f, v[v.Count - 1] * 100f, lo, hi);
            if (worse.Any()) Console.WriteLine("      makes worse: " + string.Join(", ", worse));
        }
    }

    // Upgrade balance: what one pick (and a full stack) of each damage upgrade is worth, for every weapon, in three
    // fights: a boss (one big target counted as a boss, with as much health as the weapon deals in 10 s, so the run is
    // a whole kill), a pack (4 enemies close together) and a spread wave (8 enemies over a wide area), whose enemies
    // have about 2.5 s of the weapon's damage in health. Enemies come back when killed, so Executioner and on-kill
    // upgrades count fairly;
    // the measure is health taken off in 10 s. Prints the median gain over all weapons and the 10th-90th percentile.
    static void UpgradeBalanceReport(float dt)
    {
        string[] ups = { "Sharpened Rounds", "Quick Trigger", "Magnum Rounds", "Hollow Points", "Hair Trigger", "Multishot", "Barrage", "Piercing Rounds",
            "Swarm Rounds", "Brood Mother", "Hive Drones", "Bomber Escort", "Twin Scythes", "Final Reap", "Executioner", "Twin Fang", "Collapse Rounds", "Graviton Rounds", "Dark Matter",
            "Homing Rounds", "Ricochet", "Incendiary Rounds", "Cryo Rounds", "Shock Rounds", "Explosive Rounds", "Velocity Rounds" };
        string[] fights = { "boss", "pack", "spread" };
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        // gains[fight][upgrade][0 = one pick, 1 = full stack] -> one gain per weapon
        var gains = fights.ToDictionary(f => f, f => ups.ToDictionary(u => u, u => new[] { new List<float>(), new List<float>() }));
        var weaponNames = new List<string>();
        var bossDef = (EnemyDef)typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(Defs.Get(EnemyType.Juggernaut), null);
        bossDef.IsBoss = true;
        var perWeapon = new Dictionary<string, Dictionary<string, float>>();  // weapon -> "fight/upgrade" -> one-pick gain
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
            for (int k = 0; k < Defs.KitCount; k++)
            {
                var kit = (Kit)k;
                var host = new Session { AutoStart = 1, StartWave = 10 };
                host.Host(nextPort++, "A", role, 0, 0, kit);
                host.StartMatch();
                var g = host.Game;
                for (int i = 0; i < 300 && !g.Running; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
                var me = g.Players[1];
                g.DevSetGod(1, true);
                for (int w = 0; w < Defs.WeaponsPerRole; w++)
                {
                    var weapon = Defs.Weapon(role, kit, w);
                    weaponNames.Add(weapon.Name);
                    perWeapon[weapon.Name] = new Dictionary<string, float>();
                    me.Taken.Clear();
                    g.GrantUpgrade(1, Array.FindIndex(Upgrades.All, x => x.Name == "Vitality"));
                    me.Weapon = weapon;
                    float baseRange = weapon.Melee ? Game.MeleeRange(weapon.Id) * 0.7f + 14f : Math.Min(240f, g.WeaponRange(me) * 0.7f);
                    float bossHp = 30000f;  // set per weapon below: what the weapon deals in 10 s, so a run is one whole boss kill
                    Func<string, int, string, float> trial = (up, stacks, fight) =>
                    {
                        foreach (var e in g.Enemies.Values) e.Hp = 0f;
                        for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
                        g.Minions.Clear(); g.Bullets.Clear(); g.Zones.Clear();
                        me.Taken.Clear();
                        if (up != null) { int idx = Array.FindIndex(Upgrades.All, x => x.Name == up); for (int n = 0; n < stacks; n++) me.Taken.Add(idx); }
                        g.GrantUpgrade(1, Array.FindIndex(Upgrades.All, x => x.Name == "Vitality"));
                        me.Weapon = weapon;
                        me.BowShotDraw = 0f;
                        me.BoltStore = 0f; me.BoltCd = 0f; me.BroodCd = 0f;
                        float range = baseRange;  // where enemies stand doesn't move with reach upgrades
                        V2 at = new V2(800f, 450f), from = new V2(800f - range, 450f);
                        V2[] spots; float hp; EnemyType type = EnemyType.Brute;
                        if (fight == "boss") { spots = new[] { at }; hp = bossHp; type = EnemyType.Juggernaut; }
                        else if (fight == "pack") { spots = new[] { at, at + new V2(40f, 25f), at + new V2(40f, -25f), at + new V2(75f, 0f) }; hp = Math.Max(100f, bossHp / 4f); }
                        else  // 8 enemies 100-130 px apart in a wide arc in front of you; you switch target every half second
                        {
                            spots = Enumerable.Range(0, 8).Select(j => from + V2.FromAngle(-0.9f + 1.8f * j / 7f) * (range + (j % 2) * 60f)).ToArray();
                            hp = Math.Max(100f, bossHp / 4f);  // a regular enemy takes about 2.5 s of your damage
                        }
                        var foes = new Enemy[spots.Length];
                        float removed = 0f;
                        Func<int, Enemy> make = j =>
                        {
                            var e = (Enemy)spawn.Invoke(g, new object[] { type, spots[j], 0f, 0, false });
                            e.MaxHp = e.Hp = hp; e.SpawnT = 0f; e.FireT = 99f;
                            if (fight == "boss") e.Def = bossDef;  // a stand-in boss: counts as one (Brood Mother, boss damage) without a boss's fight
                            return e;
                        };
                        for (int j = 0; j < spots.Length; j++) foes[j] = make(j);
                        float[] last = foes.Select(e => e.Hp).ToArray();
                        for (int f = 0; f < 60 * 10; f++)
                        {
                            for (int j = 0; j < foes.Length; j++)
                            {
                                var e = foes[j];
                                removed += Math.Max(0f, last[j] - Math.Max(0f, e.Hp));
                                if (e.Hp <= 0f || !g.Enemies.ContainsKey(e.Id)) { foes[j] = e = make(j); }
                                e.Pos = spots[j]; e.FireT = 99f;
                                last[j] = e.Hp;
                            }
                            foreach (var e in g.Enemies.Values.ToList()) if (!foes.Contains(e)) e.Hp = 0f;  // nothing else in the arena
                            me.Pos = from;
                            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
                            host.Poll();
                            bool fire = !Game.ChargeWeapon(weapon.Id) || (f % 72) < 60;
                            V2 aim = fight == "spread" ? foes[(f / 30) % foes.Length].Pos : at;
                            g.State = MatchState.Fighting;  // a fight in progress (turrets and minions only shoot then), whatever the wave is doing
                            g.FixedStep(dt, new PlayerInput { Fire = fire, AimAt = aim });
                        }
                        return removed;
                    };
                    bossHp = 1e7f;
                    bossHp = Math.Max(200f, trial(null, 0, "boss"));
                    foreach (var fight in fights)
                    {
                        float b0 = Math.Max(1f, trial(null, 0, fight));
                        foreach (var up in ups)
                        {
                            var def = Upgrades.All[Array.FindIndex(Upgrades.All, x => x.Name == up)];
                            int max = def.MaxStacks <= 0 || def.MaxStacks > 3 ? 3 : def.MaxStacks;  // uncapped cards: compared at 3
                            float one = trial(up, 1, fight) / b0 - 1f, full = trial(up, max, fight) / b0 - 1f;
                            gains[fight][up][0].Add(one);
                            gains[fight][up][1].Add(full);
                            perWeapon[weapon.Name][fight + "/" + up] = one;
                        }
                    }
                    Console.Error.Write(".");
                }
                host.Leave("");
            }
        Console.Error.WriteLine();
        Func<List<float>, float, float> pct = (v, q) => { var o = v.OrderBy(x => x).ToList(); return o[Math.Min(o.Count - 1, (int)(q * o.Count))] * 100f; };
        foreach (var fight in fights)
        {
            Console.WriteLine();
            Console.WriteLine("  {0}: damage gain over {1} weapons (median, then 10th-90th percentile)", fight.ToUpper(), weaponNames.Count);
            Console.WriteLine("  {0,-18} {1,-10} {2,22}   {3,22}", "upgrade", "rarity", "one pick", "full stack");
            foreach (var up in ups)
            {
                var def = Upgrades.All[Array.FindIndex(Upgrades.All, x => x.Name == up)];
                int max = def.MaxStacks <= 0 || def.MaxStacks > 3 ? 3 : def.MaxStacks;  // uncapped cards: compared at 3
                var a = gains[fight][up][0]; var b = gains[fight][up][1];
                Console.WriteLine("  {0,-18} {1,-10} {2,6:+0;-0}% ({3,5:+0;-0} to {4,5:+0;-0})   x{8} {5,6:+0;-0}% ({6,5:+0;-0} to {7,5:+0;-0})",
                    up, def.Rarity, pct(a, 0.5f), pct(a, 0.1f), pct(a, 0.9f), pct(b, 0.5f), pct(b, 0.1f), pct(b, 0.9f), max);
            }
        }
        Console.WriteLine();
        Console.WriteLine("  Weapons where one pick is worth under 5% in every fight:");
        foreach (var up in ups)
        {
            var weak = weaponNames.Distinct().Where(n => fights.Max(f => perWeapon[n][f + "/" + up]) < 0.05f)
                .Select(n => string.Format("{0} ({1})", n, string.Join("/", fights.Select(f => (perWeapon[n][f + "/" + up] * 100f).ToString("+0;-0")))));
            if (weak.Any())
                Console.WriteLine("  {0,-18} {1}", up, string.Join(", ", weak));
        }
        Console.WriteLine();
        Console.WriteLine("  Weapons where one pick does least / most (boss and spread, median of the upgrades):");
        foreach (var fight in new[] { "boss", "spread" })
        {
            var med = weaponNames.Distinct().ToDictionary(n => n, n => perWeapon[n].Where(kv => kv.Key.StartsWith(fight + "/")).Select(kv => kv.Value).OrderBy(x => x).ElementAt(ups.Length / 2) * 100f);
            Console.WriteLine("  {0,-7} least: {1}", fight, string.Join(", ", med.OrderBy(kv => kv.Value).Take(5).Select(kv => string.Format("{0} {1:+0;-0}%", kv.Key, kv.Value))));
            Console.WriteLine("  {0,-7} most:  {1}", fight, string.Join(", ", med.OrderByDescending(kv => kv.Value).Take(5).Select(kv => string.Format("{0} {1:+0;-0}%", kv.Key, kv.Value))));
        }
    }

    // Bosses that keep up with strong builds (BuildPower.cs): a typical build's power at each boss wave, and how much
    // more health a boss would get for the uploaded Hemomancer run's build (picks in order, cut at each boss).
    static void BossPowerReport(float dt)
    {
        foreach (var role in new[] { Role.Dps, Role.Tank, Role.Healer })
            Console.WriteLine("  typical {0,-6} (Dash / Barrier / Mend Pulse): {1}", role,
                string.Join("  ", new[] { 5, 10, 15, 20, 25, 30, 40, 50 }.Select(w => string.Format("w{0} x{1:0.0}", w,
                    Game.TypicalPower(role, role == Role.Dps ? AbilityId.Dash : role == Role.Tank ? AbilityId.Barrier : AbilityId.MendPulse, w)))));
        string[] picks = { "Magnum Rounds", "Hair Trigger", "Bloodrush", "Warden's Guard", "Homing Rounds", "Magnum Rounds", "Hair Trigger", "Light Boots", "Sprinter", "Reinforced Guard", "Hair Trigger", "Warden's Guard", "Sharpened Rounds", "Hive Drones", "Homing Rounds", "Sharpened Rounds", "Multishot", "Blink", "Magnum Rounds", "Barrage", "Overcharge", "Fortitude", "Executioner", "Homing Rounds", "Barrage", "Hair Trigger", "Nova", "Thick Skin", "Hemo Nova", "Sharpened Rounds", "Thick Skin", "Hair Trigger", "Magnum Rounds", "Twin Fang", "Second Charge", "Multishot", "Dark Matter", "Velocity Rounds", "Light Guard", "Light Boots", "Crimson Tide", "Quick Trigger", "Barrage", "Quick Trigger", "Collapse Rounds", "Ricochet", "Light Boots" };
        int[] cutAt = { 3, 13, 22, 36, 44, 47 };  // the picks made before each boss (from the session file), and all of them
        int[] waves = { 5, 10, 15, 20, 25, 26 };
        for (int k = 0; k < cutAt.Length; k++)
        {
            var taken = picks.Take(cutAt[k]).Select(n => Array.FindIndex(Upgrades.All, u => u.Name == n)).Where(i => i >= 0).ToList();
            float power = Game.BuildPower(Upgrades.Compute(taken)), typical = Game.TypicalPower(Role.Dps, AbilityId.FrenzyPact, waves[k]);
            Console.WriteLine("  the Hemomancer run before wave {0,2}: power x{1,5:0.0}, typical x{2,4:0.0} -> boss health x{3:0.00}",
                waves[k], power, typical, Game.BossScaleFor(power / typical));
        }
    }
}

static partial class Program
{
    // Every weapon's damage a second, as the lobby's hero card shows it (Defs.WeaponDps): each role and class with each
    // weapon, no upgrades, firing for 10 s at a frozen boss-sized dummy (its companions and burning included). Weapons
    // you charge and release try a few hold times, ranged ones a few distances, and each keeps its best. "weapondps write" also writes
    // Assets/Scripts/Core/WeaponDps.Generated.cs.
    static void MeasuredWeaponDps(float dt, bool write)
    {
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rows = new List<string>();
        for (int k = 0; k < Defs.KitCount; k++)
            for (int r = 0; r < 3; r++)
            {
                var line = new List<string>();
                for (int wi = 0; wi < Defs.WeaponsPerRole; wi++)
                {
                    var weapon = Defs.Weapon((Role)r, (Kit)k, wi);
                    float best = 0f;
                    float[] holds = Game.ChargeWeapon(weapon.Id) ? new[] { 0.3f, 0.6f, 0.9f, 1.2f } : new[] { 0f };
                    float[] dists = weapon.Melee ? new[] { 1f } : new[] { 0.3f, 0.55f, 0.8f, 0.97f };
                    foreach (float hold in holds)
                    foreach (float dist in dists)
                    {
                        var host = new Session { AutoStart = 1, StartWave = 3 };
                        host.Host(nextPort++, "A", (Role)r, wi, 0, (Kit)k);
                        host.StartMatch();
                        var g = host.Game;
                        for (int i = 0; i < 300 && !g.Running; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
                        var me = g.Players[1];
                        g.DevSetGod(1, true);
                        foreach (var e in g.Enemies.Values) e.Hp = 0f;
                        for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
                        var target = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Juggernaut, new V2(800f, 450f), 0f, 0, false });
                        target.Hp = target.MaxHp = 1e7f;
                        float range = weapon.Melee ? Game.MeleeRange(weapon.Id) * 0.7f + target.Radius : Math.Max(60f, Math.Min(440f, g.WeaponRange(me) * dist));
                        float held = 0f, measured = 0f, startHp = 0f;
                        for (int f = 0; f < 60 * 12; f++)
                        {
                            if (f == 60 * 2) startHp = target.Hp;  // 2 s to settle (companions arrive), then 10 s measured
                            target.Pos = new V2(800f, 450f); target.FrozenT = 1f;
                            if (!weapon.Melee || f % 30 == 0) me.Pos = new V2(800f - range, 450f);
                            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
                            foreach (var e in g.Enemies.Values) if (e != target) e.Hp = 0f;
                            bool fire = true, recall = false;
                            if (hold > 0f) { held += dt; if (held > hold) { fire = false; if (held > hold + 0.05f) held = 0f; } }
                            if (weapon.Id == WeaponId.Shuriken)
                            {
                                // Throw for a while, then press the weapon-action key to recall the stuck stars for their second hit.
                                int c = f % 72;
                                fire = c < 54;
                                recall = c >= 58 && c < 62;
                            }
                            host.Poll();
                            g.FixedStep(dt, new PlayerInput { Fire = fire, WeaponAction = recall, AimAt = target.Pos });
                            if (f >= 60 * 2) measured += dt;
                        }
                        best = Math.Max(best, (startHp - target.Hp) / measured);
                        host.Leave("");
                    }
                    line.Add(string.Format("{0} {1:0}", weapon.Name, best));
                    rows.Add(string.Format("            {{ WeaponId.{0}, {1:0}f }},", weapon.Id, best));
                }
                Console.WriteLine("  {0,-6} {1,-12} {2}", (Role)r, Defs.KitName((Kit)k), string.Join(" | ", line));
            }
        if (!write)
            return;
        var src = "// Generated by  Tools/SimTest/run.sh 1 1 weapondps write  (MeasuredWeaponDps in Tests/Reports.cs): each weapon's\n"
            + "// damage a second with no upgrades, measured against one frozen boss-sized target. Re-run it after weapon changes.\n"
            + "using System.Collections.Generic;\n\nnamespace Arena\n{\n    public static partial class Defs\n    {\n"
            + "        static readonly Dictionary<WeaponId, float> MeasuredDps = new Dictionary<WeaponId, float>\n        {\n"
            + string.Join("\n", rows) + "\n        };\n    }\n}\n";
        System.IO.File.WriteAllText("../../Assets/Scripts/Core/WeaponDps.Generated.cs", src);
        Console.WriteLine("  wrote Assets/Scripts/Core/WeaponDps.Generated.cs");
    }
}
