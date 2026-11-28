using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Oni Mask (OniMask.cs): key 3 of the DPS Ninja's Ultimate row reaching the run; the cast (10 s, the drop untouchable);
// +30% speed, +40% damage on your hits only, +50% max health given as health; kills adding 0.5 s (an elite 2 s, others'
// kills nothing) up to 25 s; the end giving the max health back (health kept up to it); going down; mastered (0.75 s a kill,
// up to 30 s); the client's view (OmT, OmLen, max health); the bot's rule.
static partial class Program
{
    static void OniMaskTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);

        var def = Upgrades.Ability(AbilityId.OniMask);
        Check(def.Name == "Oni Mask" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Dps && def.Kit == Kit.Ninja && def.Capstone != null,
            "Oni Mask is the DPS Ninja's ultimate on key 3, 75 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Ninja, 3) == AbilityId.OniMask && Upgrades.Ultimate(Role.Dps, Kit.Ninja, 2) == AbilityId.HundredPaperCranes
            && Upgrades.Ultimate(Role.Dps, Kit.Ninja) == AbilityId.DeathLotus && Upgrades.ClampUlt(Role.Dps, Kit.Ninja, 3) == 3,
            "key 3 is Oni Mask; Hundred Paper Cranes stays on key 2 and Death Lotus the default");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Oni", Role.Dps, 0, 0, Kit.Ninja, 0, 3);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0, Kit.Ninja, 0, 3);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 3 && client.Lobby[1].Ult == 3);
        Check(host.Lobby[1].UltId == AbilityId.OniMask, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.OniMask && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.OniMask,
            "in the run both players' ultimate is Oni Mask, on both games");

        var me = g.Players[1];
        var ally = g.Players[cid];
        V2 home = new V2(900f, 700f);
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
                    if (g.Enemies.ContainsKey(e.Id)) { e.FireT = 99f; e.FrozenT = 99f; }
                g.Bullets.Clear();
                me.Pos = home;
                ally.Pos = home + new V2(0f, 1500f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(300f, 0f) });
            }
        };
        Action reset = () =>
        {
            step(dt);
            if (me.OmT > 0f) { me.OmT = 0.0001f; step(dt); }
            me.Downed = false; me.UltT = 0f; me.InvulnT = 0f; me.Hp = me.MaxHp;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
            me.MasteryXp.Remove(AbilityId.OniMask);
            me.OmAdded = 0f;
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);  // (the wave never runs out: its end would end it)
            g.State = MatchState.Fighting;
            foreach (var e in keep) g.Enemies.Remove(e.Id);
            keep.Clear();
        };
        Func<float, Enemy> foe = hp =>
        {
            V2 at = home + new V2(200f, 40f * keep.Count);
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.Pos = e.Disp = at; e.FireT = 99f; e.FrozenT = 99f; e.Elite = false;
            keep.Add(e);
            return e;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home + new V2(300f, 0f), false });
        Action<Enemy, int> kill = (e, by) => { e.Hp = 1f; hurt.Invoke(g, new object[] { e, 50f, by }); step(dt); };

        // --- The cast: the three buffs --------------------------------------------------------------------------------------
        reset();
        float speed0 = me.Speed, max0 = me.MaxHp;
        me.Hp = max0 - 20f;
        cast();
        Console.WriteLine("  oni: speed {0:0} -> {1:0}, max health {2:0} -> {3:0}, health {4:0}", speed0, me.Speed, max0, me.MaxHp, me.Hp);
        Check(Game.OmOn(me) && me.OmT == Game.OmTime && me.OmLen == Game.OmTime && me.UltT == Game.OmTime && me.InvulnT >= Game.OmCastT,
            "casting puts the mask on for 10 s, untouchable while it drops on");
        Check(Math.Abs(me.Speed - speed0 * Game.OmMove) < 0.5f, "30% faster");
        Check(Math.Abs(me.MaxHp - max0 * Game.OmHp) < 0.01f && Math.Abs(me.Hp - (max0 - 20f + max0 * 0.5f)) < 0.01f, "50% more max health, the gain given as health");
        var target = foe(1e5f);
        hurt.Invoke(g, new object[] { target, 10f, cid });
        float byAlly = 1e5f - target.Hp;
        hurt.Invoke(g, new object[] { target, 10f, 1 });
        float byMe = 1e5f - byAlly - target.Hp;
        Console.WriteLine("  oni: a 10 hit lands for {0:0.0} from the ally, {1:0.0} masked", byAlly, byMe);
        Check(byAlly > 0f && Math.Abs(byMe - byAlly * Game.OmDmg) < 0.01f, "your hits land 40% harder; an ally's don't");

        // --- Kills add time, up to the cap ----------------------------------------------------------------------------------
        step(1f);
        float t0 = me.OmT;
        kill(foe(10f), 1);
        Check(Math.Abs(me.OmT - (t0 - dt + Game.OmPerKill)) < 0.01f && Math.Abs(me.OmLen - (Game.OmTime + Game.OmPerKill)) < 0.001f && me.OmKills == 1,
            "a kill adds 0.5 s");
        var elite = foe(10f); elite.Elite = true;
        kill(elite, 1);
        Check(Math.Abs(me.OmLen - (Game.OmTime + Game.OmPerKill + Game.OmPerHeavy)) < 0.001f, "an elite adds 2 s");
        float len = me.OmLen;
        kill(foe(10f), cid);
        Check(me.OmLen == len, "an ally's kill adds nothing");
        me.OmLen = Game.OmMax - 0.2f;
        kill(foe(10f), 1);
        kill(foe(10f), 1);
        Check(Math.Abs(me.OmLen - Game.OmMax) < 0.001f, "no more than 25 s in all");

        // --- The client sees it ---------------------------------------------------------------------------------------------
        var cme = cg.Players[1];
        step(0.2f);
        Run(s, dt, 1, () => cme.OmT > 0f && Math.Abs(cme.OmLen - Game.OmMax) < 0.01f);
        Check(Game.OmOn(cme) && Math.Abs(cme.OmLen - Game.OmMax) < 0.01f && Math.Abs(cme.MaxHp - max0 * Game.OmHp) < 0.01f,
            "the client's game sees the mask (OmT, OmLen) and the higher max health");

        // --- The end --------------------------------------------------------------------------------------------------------
        me.OmT = 0.05f;
        me.Hp = me.MaxHp;
        float speedOn = me.Speed;  // (the kills gave Momentum stacks too)
        step(0.1f);
        Check(!Game.OmOn(me) && me.OmT == 0f && me.UltT == 0f && Math.Abs(me.MaxHp - max0) < 0.01f && Math.Abs(me.Hp - max0) < 0.01f,
            "it ends on time: max health drops back, health kept up to it");
        Check(Math.Abs(me.Speed - speedOn / Game.OmMove) < 0.5f, "and the speed with it");
        Run(s, dt, 1, () => cme.OmT == 0f);
        Check(cme.OmT == 0f && Math.Abs(cme.MaxHp - max0) < 0.01f, "the client's max health drops back too");

        // --- Going down ends it ---------------------------------------------------------------------------------------------
        reset();
        cast();
        step(0.5f);
        me.Downed = true;
        step(0.1f);
        Check(me.OmT == 0f && Math.Abs(me.MaxHp - max0) < 0.01f, "going down ends it");
        me.Downed = false;

        // --- Mastered -------------------------------------------------------------------------------------------------------
        reset();
        me.MasteryXp[AbilityId.OniMask] = Game.MasteryXpFor[Game.MasteryMax - 1];
        cast();
        kill(foe(10f), 1);
        Check(Math.Abs(me.OmLen - (Game.OmTime + Game.OmPerKillMastered)) < 0.001f, "mastered: a kill adds 0.75 s");
        me.OmLen = Game.OmMaxMastered - 0.2f;
        kill(foe(10f), 1);
        Check(Math.Abs(me.OmLen - Game.OmMaxMastered) < 0.001f, "mastered: up to 30 s");
        reset();

        Check(def.Bot(new BotSense { Me = me, NearEnemies = 5 }) && !def.Bot(new BotSense { Me = me, NearEnemies = 1 }), "a bot casts it in a crowd");

        host.Leave(""); client.Leave("");
    }
}
