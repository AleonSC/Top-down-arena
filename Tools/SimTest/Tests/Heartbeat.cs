using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Heartbeat (Heartbeat.cs): key 1 of the Healer Hemomancer's Ultimate row reaching the run; three beats (0.35 s, then every
// 0.9 s), each paying 8% of max health but never below 15%; each wave hitting an enemy in reach once, missing one out of
// reach; a wave's kill always dropping a blood orb; the end on time and on going down; the client seeing it (HbT);
// mastered; the bot's rule.
static partial class Program
{
    static void HeartbeatTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // --- The lobby: key 1 of the Healer Hemomancer's Ultimate row -----------------------------------------------------
        var def = Upgrades.Ability(AbilityId.Heartbeat);
        Check(def.Name == "Heartbeat" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Healer && def.Kit == Kit.Hemomancer && def.Capstone != null,
            "Heartbeat is the Healer Hemomancer's ultimate on key 1, 80 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Hemomancer, 1) == AbilityId.Heartbeat && Upgrades.Ultimate(Role.Healer, Kit.Hemomancer) == AbilityId.SanguineRain
            && Upgrades.ClampUlt(Role.Healer, Kit.Hemomancer, 1) == 1 && Upgrades.ClampUlt(Role.Healer, Kit.Hemomancer, 2) == 0,
            "key 1 is Heartbeat; Sanguine Rain stays the default; key 2 falls back to it");
        Check(Math.Abs(Game.HbTotal - (0.35f + 2f * 0.9f + 0.6f)) < 1e-4f && Game.HbFired(0.34f) == 0 && Game.HbFired(0.36f) == 1 && Game.HbFired(2.2f) == 3,
            "the beats come at 0.35 s, 1.25 s and 2.15 s; the last wave ends at 2.75 s");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Heart", Role.Healer, 0, 0, Kit.Hemomancer, 0, 1);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 0, 0, Kit.Hemomancer, 0, 1);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 1 && client.Lobby[1].Ult == 1);
        Check(host.Lobby[1].UltId == AbilityId.Heartbeat, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.Heartbeat && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.Heartbeat,
            "in the run both players' ultimate is Heartbeat, on both games");

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        me.Pos = new V2(900f, 700f);
        ally.Pos = me.Pos + new V2(0f, 600f);
        var keep = new HashSet<Enemy>();
        var spots = new Dictionary<Enemy, V2>();
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.ToList())
                    if (!keep.Contains(g.Enemies[id]))
                        g.Enemies.Remove(id);
                foreach (var e in keep)
                    if (g.Enemies.ContainsKey(e.Id))
                    {
                        e.FireT = 99f;
                        e.Pos = spots[e];
                    }
                g.Bullets.Clear();
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = me.Pos + new V2(200f, 0f) });
            }
        };
        Action reset = () =>
        {
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.HbT = 0f; me.InvulnT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
            me.MasteryXp.Remove(AbilityId.Heartbeat);
            me.HbBeatCount = me.HbHits = me.HbOrbs = 0; me.HbPaid = 0f;
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);  // (the wave never runs out: its end would end it)
            g.State = MatchState.Fighting;
            foreach (var e in keep) g.Enemies.Remove(e.Id);
            keep.Clear(); spots.Clear();
            g.Orbs.Clear();
        };
        Func<float, float, Enemy> foe = (dist, hp) =>
        {
            var at = me.Pos + new V2(dist, 0f);
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.Pos = e.Disp = at; e.FireT = 99f;
            keep.Add(e); spots[e] = at;
            return e;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, false });

        // --- The beats and their price (no enemies, so no lifesteal) --------------------------------------------------------
        reset();
        cast();
        Check(Game.HbOn(me) && Math.Abs(me.UltT - Game.HbTotal) < 1e-3f, "casting starts the heart, for 2.75 s");
        step(0.25f);
        Check(me.HbBeatCount == 0 && me.Hp == me.MaxHp, "nothing in the first 0.35 s");
        step(0.2f);
        Check(me.HbBeatCount == 1 && Math.Abs(me.HbPaid - me.MaxHp * 0.08f) < 0.1f, "the first beat costs 8% of max health");
        step(1.85f);
        Console.WriteLine("  heartbeat: beats {0}, paid {1:0.0}% (health {2:0.0}%, with regen)", me.HbBeatCount, 100f * me.HbPaid / me.MaxHp, 100f * me.Hp / me.MaxHp);
        Check(me.HbBeatCount == 3 && Math.Abs(me.HbPaid - me.MaxHp * 0.24f) < 0.1f, "three beats, 24% of max health in all");
        step(0.6f);
        Check(!Game.HbOn(me) && me.HbT == 0f && me.UltT == 0f && me.HbBeatCount == 3, "it ends after the last wave, with no more beats");

        // The floor: never below the blood floor (20%).
        reset();
        me.Hp = me.MaxHp * 0.23f;
        cast();
        float low = me.Hp;
        for (float tt = 0f; tt < Game.HbTotal + 0.1f; tt += dt) { step(dt); low = Math.Min(low, me.Hp); }
        Console.WriteLine("  heartbeat: from 23%: lowest {0:0.0}%, paid {1:0.0}%", 100f * low / me.MaxHp, 100f * me.HbPaid / me.MaxHp);
        Check(low >= me.MaxHp * Game.HbFloor - 0.01f && me.HbPaid < me.MaxHp * 0.24f - 1f && !me.Downed && me.HbBeatCount == 3, "the price stops at the blood floor (20%), and the beats still come");

        // --- The waves ------------------------------------------------------------------------------------------------------
        reset();
        var near = foe(200f, 1e6f);
        var far = foe(Game.HbReach + 80f, 1e6f);
        cast();
        step(Game.HbTotal + 0.1f);
        float dealt = near.MaxHp - near.Hp;
        Console.WriteLine("  heartbeat: near took {0:0} in {1} hits, far took {2:0}", dealt, me.HbHits, far.MaxHp - far.Hp);
        Check(me.HbHits == 3 && dealt >= 3f * Game.HbDamage * me.Stats.DmgMult - 1f, "each wave hits an enemy in reach once: three hits of 45");
        Check(far.Hp == far.MaxHp, "an enemy past 320 px is untouched");

        // A wave's kill drops a blood orb, every time.
        reset();
        for (int i = 0; i < 4; i++)
            foe(120f + 40f * i, 10f);
        cast();
        step(0.8f);
        int bloods = g.Orbs.Values.Count(o => o.Blood);
        Console.WriteLine("  heartbeat: kills {0}, blood orbs {1}", 4 - keep.Count(e => g.Enemies.ContainsKey(e.Id)), bloods);
        Check(keep.All(e => !g.Enemies.ContainsKey(e.Id)) && bloods == 4 && me.HbOrbs == 4, "every enemy a wave kills drops a blood orb");

        // Mastered: 40% harder.
        reset();
        var plain = foe(150f, 1e6f);
        cast();
        step(Game.HbTotal + 0.1f);
        float plainDealt = plain.MaxHp - plain.Hp;
        reset();
        me.MasteryXp[AbilityId.Heartbeat] = Game.MasteryXpFor[Game.MasteryMax - 1];
        var hard = foe(150f, 1e6f);
        cast();
        step(Game.HbTotal + 0.1f);
        float hardDealt = hard.MaxHp - hard.Hp;
        Check(Math.Abs(hardDealt / plainDealt - Game.HbDamageMastered / Game.HbDamage) < 0.02f, "mastered: the waves hit 40% harder");

        // Going down ends it.
        reset();
        cast();
        step(0.5f);
        me.Downed = true;
        step(0.1f);
        Check(me.HbT == 0f, "going down ends it");
        me.Downed = false;

        // The client sees it (HbT), so it draws the beats and waves.
        reset();
        var cme = cg.Players[cid];
        ally.Slots[Upgrades.UltSlot].Charges = 1; ally.Slots[Upgrades.UltSlot].Timer = 0f; ally.UltT = 0f;
        hostAbility.Invoke(g, new object[] { cid, Upgrades.UltSlot, ally.Pos, false });
        step(0.5f);
        Run(s, dt, 1, () => cme.HbT > 0.4f);
        Console.WriteLine("  heartbeat: client: host HbT {0:0.00}, client HbT {1:0.00}", ally.HbT, cme.HbT);
        Check(Game.HbOn(cme) && Game.HbFired(cme.HbT) >= 1, "the client's game sees the beats (HbT)");

        // The bot's rule.
        me.Hp = me.MaxHp;
        Check(def.Bot(new BotSense { Me = me, NearEnemies = 5 }) && !def.Bot(new BotSense { Me = me, NearEnemies = 1 }), "a bot casts it in a crowd with its health high");

        host.Leave(""); client.Leave("");
    }
}
