using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Attack Helicopter (AttackHelicopter.cs): the DPS Soldier's key 1 on the lobby's Ultimate row (and a Wildcard's pick), the pick
// reaching the run; no fire while it flies in; then each gun locks the enemy nearest its caller within the leash and fires
// 20 rounds a second of 8 (times the damage multiplier) until it dies, then the next; nothing beyond the leash; two guns on two
// targets mastered; the other screen sees the guns and targets; it ends on time, on going down and between waves.
static partial class Program
{
    static void HelicopterTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var tickHelis = typeof(Game).GetMethod("TickHelis", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby ---------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.AttackHelicopter);
        Check(def.Name == "Attack Helicopter" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Dps && def.Kit == Kit.Soldier && def.Capstone != null,
            "the Attack Helicopter is the DPS Soldier's ultimate on key 1, 70 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Soldier) == AbilityId.ArtilleryAssault && Upgrades.Ultimate(Role.Dps, Kit.Soldier, 1) == AbilityId.AttackHelicopter
            && Upgrades.AltUltimate(Role.Dps, Kit.Soldier, 2) == AbilityId.OrbitalLaser && Upgrades.AltUltimate(Role.Tank, Kit.Soldier, 1) == AbilityId.JuggernautSuit,
            "Artillery Assault stays the default; key 1 is the Attack Helicopter (key 2 Orbital Laser); the Tank Soldier keeps the Juggernaut Suit");
        int wild = Upgrades.WildUlt(Kit.Soldier, 1);
        Check(Upgrades.ClampUlt(Role.Wildcard, Kit.Soldier, wild) == wild && Upgrades.Ultimate(Role.Wildcard, Kit.Soldier, wild) == AbilityId.AttackHelicopter,
            "a Wildcard can pick it too");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9 }, port, false, "Gunner", Role.Dps, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(1); } };
        host.RequestUlt(1);
        settle();
        Check(host.Lobby[1].Ult == 1 && client.Lobby[1].Ult == 1 && host.Lobby[1].UltId == AbilityId.AttackHelicopter, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        int cid = client.MyId;
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.AttackHelicopter && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.AttackHelicopter,
            "in the match the host's F is the Attack Helicopter, on every game");

        // --- The gunship on the host's game -------------------------------------------------------------------------------
        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        V2 mePos = new V2(800f, 450f);
        var mine = new HashSet<int>();
        Func<V2, float, Enemy> make = (at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FrozenT = 999f; e.Pos = at; mine.Add(e.Id); return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                g.Bullets.Clear();
                me.Pos = mePos; me.InvulnT = 1f; ally.Pos = new V2(200f, 800f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = new V2(200f, 100f) });  // (aimed off at nothing: your own gun hits nobody)
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.HeliGuns = 0; me.HeliRounds = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });

        reset();
        var near = make(mePos + new V2(150f, 0f), 100f);
        var next = make(mePos + new V2(0f, 300f), 1e6f);
        var far = make(mePos + new V2(-700f, 0f), 1e6f);
        cast();
        Check(me.UltT == Game.HeliTime && me.HeliGuns == 1 && me.HeliTgt == 0, "calling it: 10.9 s out (0.9 s flying in), one gun");
        step(Game.HeliArrive - 0.1f);
        Check(near.Hp == 100f && me.HeliRounds == 0, "no fire while it flies in");
        step(0.3f);
        Check(me.HeliTgt == near.Id, "it locks the enemy nearest you");
        step(1.2f);
        Console.WriteLine("  heli: rounds {0}, near {1:0}, next hp lost {2:0}, far hp lost {3:0}, target {4}", me.HeliRounds, near.Hp, 1e6f - next.Hp, 1e6f - far.Hp, me.HeliTgt);
        Check(near.Hp <= 0f || !g.Enemies.ContainsKey(near.Id), "it shoots its target dead");
        Check(me.HeliTgt == next.Id && next.Hp < 1e6f, "then turns on the next nearest");
        Check(far.Hp == 1e6f, "nothing beyond the 560 px leash is touched");
        step(0.1f);
        Check(client.Game.Players[1].HeliGuns == 1 && client.Game.Players[1].HeliTgt == next.Id, "the other screen sees the gun and its target");
        float before = next.Hp;
        int r0 = me.HeliRounds;
        step(1f);
        float per = (before - next.Hp) / Math.Max(1, me.HeliRounds - r0);
        Check(Math.Abs(me.HeliRounds - r0 - Game.HeliRate) <= 1 && Math.Abs(per - Game.HeliDamage * me.Stats.DmgMult) < 0.5f,
            "20 rounds a second, 8 each times your damage multiplier (" + (me.HeliRounds - r0) + " rounds, " + per.ToString("0.0") + " each)");
        step(Game.HeliTime);
        Check(me.HeliGuns == 0 && me.UltT == 0f && client.Game.Players[1].HeliGuns == 0, "it leaves when its time runs out");

        // Mastered: two guns on two targets.
        reset();
        me.MasteryXp[AbilityId.AttackHelicopter] = 1e6f;
        var a = make(mePos + new V2(120f, 0f), 1e6f);
        var b = make(mePos + new V2(-160f, 0f), 1e6f);
        cast();
        step(Game.HeliArrive + 1f);
        Check(me.HeliGuns == 2 && me.HeliTgt != 0 && me.HeliTgt2 != 0 && me.HeliTgt != me.HeliTgt2 && a.Hp < 1e6f && b.Hp < 1e6f, "mastered: two guns on two targets");
        me.MasteryXp.Remove(AbilityId.AttackHelicopter);

        // Going down, and between waves.
        reset();
        make(mePos + new V2(150f, 0f), 1e6f);
        cast();
        step(0.2f);
        me.InvulnT = 0f; me.Hp = 1f;
        damage.Invoke(g, new object[] { me, 500f, "test" });
        tickHelis.Invoke(g, new object[] { dt });
        Check(me.Downed && me.HeliGuns == 0, "it leaves if you go down");
        reset();
        cast();
        g.State = MatchState.Intermission;
        tickHelis.Invoke(g, new object[] { dt });
        Check(me.HeliGuns == 0 && me.UltT == 0f, "it leaves when the wave is over");
        g.State = MatchState.Fighting;
        host.Leave(""); client.Leave("");
    }
}
