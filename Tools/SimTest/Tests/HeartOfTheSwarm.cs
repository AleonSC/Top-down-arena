using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Heart of the Swarm (HeartOfTheSwarm.cs): key 2 of the Healer Hemomancer's Ultimate row reaching the run; six beats (0.35 s,
// then every 1.5 s) from a heart at the cursor that follows it; each beat healing an ally in reach 8% (not one out of reach)
// and pulling enemies in reach toward the heart (heavy ones and one out of reach untouched); no health cost; the end
// on time and on going down; the client seeing it (HsT, HsPos); mastered; the bot's rule.
static partial class Program
{
    static void HeartOfTheSwarmTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // --- The lobby: key 2 of the Healer Hemomancer's Ultimate row -----------------------------------------------------
        var def = Upgrades.Ability(AbilityId.HeartOfTheSwarm);
        Check(def.Name == "Heart of the Swarm" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Healer && def.Kit == Kit.Hemomancer && def.Capstone != null,
            "Heart of the Swarm is the Healer Hemomancer's ultimate on key 2, 80 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Hemomancer, 2) == AbilityId.HeartOfTheSwarm && Upgrades.Ultimate(Role.Healer, Kit.Hemomancer, 1) == AbilityId.Heartbeat
            && Upgrades.Ultimate(Role.Healer, Kit.Hemomancer) == AbilityId.SanguineRain && Upgrades.ClampUlt(Role.Healer, Kit.Hemomancer, 2) == 2,
            "key 2 is Heart of the Swarm; Heartbeat stays on key 1 and Sanguine Rain the default");
        Check(Math.Abs(Game.HsTotal - (0.35f + 5f * 1.5f + 0.6f)) < 1e-4f && Game.HsFired(0.34f) == 0 && Game.HsFired(0.36f) == 1 && Game.HsFired(1.86f) == 2 && Game.HsFired(8f) == 6,
            "the beats come at 0.35 s and every 1.5 s after, six in all; the last wave ends at 8.45 s");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Heart", Role.Healer, 0, 0, Kit.Hemomancer, 0, 2);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 0, 0, Kit.Hemomancer, 0, 2);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 2 && client.Lobby[1].Ult == 2);
        Check(host.Lobby[1].UltId == AbilityId.HeartOfTheSwarm, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.HeartOfTheSwarm && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.HeartOfTheSwarm,
            "in the run both players' ultimate is Heart of the Swarm, on both games");

        var me = g.Players[1];
        var ally = g.Players[cid];
        V2 home = new V2(900f, 700f);
        V2 aim = home + new V2(250f, 0f);  // the heart sits here
        var keep = new HashSet<Enemy>();
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
                        e.FrozenT = 99f;  // (they stand still, so only the pulls move them)
                    }
                g.Bullets.Clear();
                me.Pos = home;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
            }
        };
        Action reset = () =>
        {
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.HsT = 0f; me.InvulnT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
            me.MasteryXp.Remove(AbilityId.HeartOfTheSwarm);
            me.HsBeatCount = me.HsPulls = 0; me.HsHealed = 0f;
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);  // (the wave never runs out: its end would end it)
            g.State = MatchState.Fighting;
            foreach (var e in keep) g.Enemies.Remove(e.Id);
            keep.Clear();
            aim = home + new V2(250f, 0f);
        };
        Func<EnemyType, V2, Enemy> foe = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.Pos = e.Disp = at; e.FireT = 99f; e.FrozenT = 99f;
            keep.Add(e);
            return e;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aim, false });

        // --- The heart: at the cursor, the beats, no cost -----------------------------------------------------------------
        reset();
        ally.Pos = aim + new V2(0f, 2000f);
        step(0.05f);
        cast();
        Check(Game.HsOn(me) && Math.Abs(me.UltT - Game.HsTotal) < 1e-3f && me.HsPos.DistanceTo(aim) < 1f, "casting puts the heart at the cursor, for 8.45 s");
        step(0.25f);
        Check(me.HsBeatCount == 0, "nothing in the first 0.35 s");
        step(0.2f);
        Check(me.HsBeatCount == 1 && me.Hp == me.MaxHp, "the first beat at 0.35 s, costing no health");
        aim = home + new V2(-200f, 150f);
        step(0.8f);
        Check(me.HsPos.DistanceTo(aim) < 1f, "the heart follows the cursor");
        step(Game.HsTotal);
        Check(!Game.HsOn(me) && me.HsT == 0f && me.UltT == 0f && me.HsBeatCount == 6, "six beats, then it ends");

        // --- The heals ------------------------------------------------------------------------------------------------------
        reset();
        ally.Pos = aim + new V2(0f, 200f);
        ally.Hp = ally.MaxHp * 0.3f;
        step(0.05f);
        float before = ally.Hp;
        cast();
        step(0.45f);
        float gain = ally.Hp - before;
        Console.WriteLine("  swarmheart: ally in reach healed {0:0.0}% on the first beat (HsHealed {1:0.0})", 100f * gain / ally.MaxHp, me.HsHealed);
        Check(me.HsBeatCount == 1 && Math.Abs(me.HsHealed - ally.MaxHp * Game.HsHeal) < 0.5f, "a beat heals an ally in reach 8% of their max health");
        step(Game.HsTotal);
        Check(Math.Abs(me.HsHealed - Math.Min(6f * ally.MaxHp * Game.HsHeal, ally.MaxHp * 0.7f)) < ally.MaxHp * 0.05f, "six beats heal six times (up to full)");

        reset();
        ally.Pos = aim + new V2(0f, Game.HsReach + 80f);
        ally.Hp = ally.MaxHp * 0.3f;
        step(0.05f);
        cast();
        step(Game.HsTotal + 0.1f);
        Check(me.HsHealed == 0f, "an ally past 300 px isn't healed");

        // Mastered: 11%.
        reset();
        me.MasteryXp[AbilityId.HeartOfTheSwarm] = Game.MasteryXpFor[Game.MasteryMax - 1];
        ally.Pos = aim + new V2(0f, 200f);
        ally.Hp = ally.MaxHp * 0.3f;
        step(0.05f);
        cast();
        step(0.45f);
        Check(Math.Abs(me.HsHealed - ally.MaxHp * Game.HsHealMastered) < 0.5f, "mastered: a beat heals 11%");

        // --- The pulls ------------------------------------------------------------------------------------------------------
        reset();
        ally.Pos = aim + new V2(0f, 2000f);
        var near = foe(EnemyType.Grunt, aim + new V2(200f, 0f));
        var heavy = foe(EnemyType.Phylactery, aim + new V2(0f, -200f));
        var far = foe(EnemyType.Grunt, aim + new V2(-(Game.HsReach + 80f), 0f));
        step(0.05f);
        float n0 = near.Pos.DistanceTo(aim), h0 = heavy.Pos.DistanceTo(aim), f0 = far.Pos.DistanceTo(aim);
        cast();
        step(0.7f);
        float nIn = n0 - near.Pos.DistanceTo(aim), hIn = h0 - heavy.Pos.DistanceTo(aim), fIn = f0 - far.Pos.DistanceTo(aim);
        Console.WriteLine("  swarmheart: one beat pulled near {0:0} px, heavy {1:0} px (heavy={2}), far {3:0} px", nIn, hIn, (heavy.Def.IsBoss || heavy.Def.Heavy), fIn);
        Check(Math.Abs(nIn - Game.HsPull) < 2f, "a beat pulls an enemy in reach 70 px toward the heart");
        Check(heavy.Def.Heavy && Math.Abs(hIn) < 1f, "a heavy enemy isn't moved");
        Check(Math.Abs(fIn) < 1f, "an enemy past 300 px isn't pulled");
        step(Game.HsTotal);
        Check(near.Pos.DistanceTo(aim) >= near.Radius, "the pulls never drag an enemy past the heart");

        // Going down ends it.
        reset();
        cast();
        step(0.5f);
        me.Downed = true;
        step(0.1f);
        Check(me.HsT == 0f, "going down ends it");
        me.Downed = false;

        // The client sees it (HsT, HsPos), so it draws the heart and the beats.
        reset();
        var cme = cg.Players[cid];
        ally.Pos = home + new V2(0f, 300f);
        ally.Slots[Upgrades.UltSlot].Charges = 1; ally.Slots[Upgrades.UltSlot].Timer = 0f; ally.UltT = 0f;
        ally.AimAt = aim;
        hostAbility.Invoke(g, new object[] { cid, Upgrades.UltSlot, aim, false });
        step(0.5f);
        Run(s, dt, 1, () => cme.HsT > 0.4f);
        Console.WriteLine("  swarmheart: client: host HsT {0:0.00} at {1}, client HsT {2:0.00} at {3}", ally.HsT, ally.HsPos, cme.HsT, cme.HsPos);
        Check(Game.HsOn(cme) && Game.HsFired(cme.HsT) >= 1 && cme.HsPos.DistanceTo(ally.HsPos) < 40f, "the client's game sees the heart and its beats (HsT, HsPos)");

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, NearEnemies = 5 }) && !def.Bot(new BotSense { Me = me, NearEnemies = 1 }), "a bot casts it in a crowd");

        host.Leave(""); client.Leave("");
    }
}
