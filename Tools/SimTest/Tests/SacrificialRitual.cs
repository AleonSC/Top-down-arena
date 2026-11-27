using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Sacrificial Ritual (SacrificialRitual.cs): key 3 of the Tank Hemomancer's Ultimate row reaching the run; the rite (rooted, can't be
// hurt); the death (1 HP, the body left where you stood, other screens see it); the spirit (faster, never picked by enemies, which go
// for the body; can't be hurt or go down; enemy shots pass through); the scythe (40 in a 150° arc to 70 px, the weapon put away, one
// every 0.33 s); kills storing 10% each (a boss 5); the return to the body with the stored health, or 1 HP with no kills; ending in
// the rite does nothing; mastered; the bot's rule.
static partial class Program
{
    static void SacrificialRitualTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var pickTarget = typeof(Game).GetMethod("PickTarget", flags);
        var sacTarget = typeof(Game).GetMethod("SacTarget", flags);
        var sacKill = typeof(Game).GetMethod("SacKill", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 3 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.SacrificialRitual);
        Check(def.Name == "Sacrificial Ritual" && def.Cooldown == 90f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Tank && def.Kit == Kit.Hemomancer,
            "Sacrificial Ritual is the Tank Hemomancer's ultimate on key 3, 90 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Hemomancer) == AbilityId.VeinTithe && Upgrades.Ultimate(Role.Tank, Kit.Hemomancer, 1) == AbilityId.Bloodbath
            && Upgrades.Ultimate(Role.Tank, Kit.Hemomancer, 2) == AbilityId.BloodGolem && Upgrades.Ultimate(Role.Tank, Kit.Hemomancer, 3) == AbilityId.SacrificialRitual,
            "Vein Tithe stays the default, Bloodbath key 1, Blood Golem key 2, Sacrificial Ritual key 3");
        Check(Upgrades.ClampUlt(Role.Tank, Kit.Hemomancer, 3) == 3, "key 3 is a real pick for the tank Hemomancer");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Rite", Role.Tank, 0, 0, Kit.Hemomancer, 0, 3);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Tank, 0, 0, Kit.Hemomancer, 0, 3);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 3 && client.Lobby[1].Ult == 3);
        Check(host.Lobby[1].UltId == AbilityId.SacrificialRitual, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.SacrificialRitual && client.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.SacrificialRitual,
            "in the run both players' ultimate is Sacrificial Ritual, on both games");

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        V2 mePos = new V2(500f, 450f), aimAt = new V2(900f, 450f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, float, Enemy> make = (at, type, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FrozenT = 999f; e.Pos = at; mine.Add(e.Id); return e;
        };
        bool fire = false;
        V2 move = V2.Zero;
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1 && x.Id < 90000).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                ally.Pos = new V2(200f, 200f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt, Fire = fire, Move = move });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.InvulnT = 0f; me.UltT = 0f; me.SacT = 0f; me.SacUp = false; me.SacKills = 0; me.SacClaws = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = me.Disp = mePos; masteredField.SetValue(g, false);
            fire = false; move = V2.Zero;
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        float dmg = me.Stats.DmgMult;

        // The rite: rooted, nothing hurts you.
        reset();
        float speed0 = me.Speed, hp0 = me.Hp;
        cast();
        Check(Math.Abs(me.SacT - (Game.SacRite + Game.SacTime)) < 1e-3f && me.UltT == me.SacT && Game.SacOn(me) && !Game.SacSpirit(me), "casting: a 1 s rite, then 10 s as the spirit");
        Check(me.Speed == 0f, "you're rooted through the rite");
        damage.Invoke(g, new object[] { me, 40f, "test" });
        Check(me.Hp == hp0, "nothing hurts you in the rite");
        step(Game.SacRite * 0.5f);
        Check(me.Hp == hp0 && !Game.SacSpirit(me), "you're still alive halfway through the rite");

        // The death: 1 HP, the body stays, other screens see it.
        step(Game.SacRite * 0.5f + 0.05f);
        Check(Game.SacSpirit(me) && me.Hp == 1f && me.SacBody.DistanceTo(mePos) < 1f, "the spikes kill you: 1 HP, your body left where you stood");
        Run(s, dt, 1, () => client.Game.Players[1].SacT > 0f && client.Game.Players[1].SacAge >= Game.SacRite);
        var seen = client.Game.Players[1];
        Check(seen.SacT > 0f && seen.SacBody.DistanceTo(mePos) < 1f && Game.SacSpirit(seen), "other screens see the spirit and the body");
        Check(Math.Abs(me.Speed / speed0 - Game.SacSpeed) < 1e-3f, "the spirit moves 40% faster");

        // Enemies can't see the spirit.
        bool picked = false;
        for (int i = 0; i < 40; i++) picked |= (int)pickTarget.Invoke(g, null) == 1;
        Check(!picked, "no enemy picks the spirit while someone else is up");
        V2 there = new V2(900f, 300f);
        Check(((V2)sacTarget.Invoke(g, new object[] { me, there })).DistanceTo(mePos) < 1f && ((V2)sacTarget.Invoke(g, new object[] { ally, there })).DistanceTo(there) < 1f,
            "one already on the spirit goes for the body instead; others are left alone");

        // Nothing can hurt the spirit, nor make it go down; shots pass through.
        damage.Invoke(g, new object[] { me, 40f, "test" });
        damage.Invoke(g, new object[] { me, 1e6f, "test" });
        Check(me.Hp == 1f && !me.Downed && Game.SacSpirit(me), "the spirit can't be hurt or go down");
        g.Bullets[95001] = new Bullet { Id = 95001, Kind = Defs.SmallShot, Pos = me.Pos + new V2(0f, -6f), Radius = 4f, Life = 5f, Vel = new V2(0f, 30f), Dmg = 10f };
        step(dt * 2f);
        Check(g.Bullets.ContainsKey(95001) && me.Hp == 1f, "an enemy shot passes through the spirit");
        g.Bullets.Remove(95001);

        // The scythe: Fire, 40 in a 150° arc out to 70 px; the weapon is put away.
        var ahead = make(me.Pos + new V2(50f, 0f), EnemyType.Grunt, 1e6f);
        var side = make(me.Pos + V2.FromAngle(1.1f) * 50f, EnemyType.Grunt, 1e6f);  // 63°: inside the arc
        var behind = make(me.Pos + new V2(-60f, 0f), EnemyType.Grunt, 1e6f);
        var past = make(me.Pos + new V2(Game.SacClawReach + 60f, 0f), EnemyType.Grunt, 1e6f);
        g.Bullets.Clear();
        fire = true;
        step(dt * 2f);
        fire = false;
        float hit = Game.SacClawDamage * dmg;
        Console.WriteLine("  sacrifice: swing {0}, ahead {1:0.0}, side {2:0.0}, expected {3:0.0}", me.SacClaws, ahead.MaxHp - ahead.Hp, side.MaxHp - side.Hp, hit);
        Check(me.SacClaws == 1 && ahead.MaxHp - ahead.Hp >= hit - 0.5f && Math.Abs((ahead.MaxHp - ahead.Hp) - (side.MaxHp - side.Hp)) < 0.5f, "a swing hits everything in its arc for 40 (more at 1 HP: the Hemomancer's blood bonus)");
        Check(behind.Hp == behind.MaxHp && past.Hp == past.MaxHp, "nothing behind you or beyond 70 px");
        Check(g.Effects.Any(e => e.Kind == FxKind.SacScythe), "with the scythe's crescent");
        Check(!g.Bullets.Values.Any(b => b.Owner == 1), "the weapon doesn't fire while you're the spirit");
        fire = true;
        step(1f);
        fire = false;
        Console.WriteLine("  sacrifice: {0} swings in a second of Fire", me.SacClaws - 1);
        Check(me.SacClaws - 1 >= 2 && me.SacClaws - 1 <= 4, "holding Fire swings about three times a second");

        // Kills store health; a boss counts 5.
        foreach (var id in mine.ToList()) g.Enemies.Remove(id);
        mine.Clear();
        make(me.Pos + new V2(40f, 0f), EnemyType.Grunt, 5f);
        make(me.Pos + new V2(45f, 20f), EnemyType.Grunt, 5f);
        me.SacClawCd = 0f;
        fire = true;
        step(dt * 3f);
        fire = false;
        Check(me.SacKills == 2 && g.Effects.Any(e => e.Kind == FxKind.Drain && e.Pos2.DistanceTo(mePos) < 1f), "each kill is stored, its blood streaming to the body");
        var boss = make(me.Pos + new V2(300f, 0f), EnemyType.Brute, 1e6f);
        boss.Def = Defs.Enemies.First(d => d.IsBoss);
        boss.LastHitBy = 1;
        sacKill.Invoke(g, new object[] { boss });
        Check(me.SacKills == 2 + Game.SacBossKills, "a boss counts as five kills");
        me.SacKills = 2;

        // The return: back to the body with the stored health.
        me.Pos = me.Disp = mePos + new V2(300f, 120f);  // the spirit wandered off
        me.SacT = 0.02f;
        step(0.05f);
        Check(!Game.SacOn(me) && me.UltT == 0f && me.Pos.DistanceTo(mePos) < 1f, "time up: you stand up at your body");
        Check(Math.Abs(me.Hp - me.MaxHp * Game.SacPerKill * 2f) < 0.5f && me.SacReturns >= 1, "with 10% of your max health a kill (2 kills: 20%)");
        Check(Math.Abs(me.Speed / speed0 - 1f) < 1e-4f, "your speed is back");
        damage.Invoke(g, new object[] { me, 10f, "test" });
        Check(me.Hp < me.MaxHp * Game.SacPerKill * 2f, "and you can be hurt again");

        // No kills: back with 1 HP.
        reset();
        cast();
        step(Game.SacRite + 0.1f);
        me.SacT = 0.02f;
        step(0.05f);
        Check(!Game.SacOn(me) && me.Hp == 1f && !me.Downed, "no kills: you come back with 1 HP");

        // Health is capped at full.
        reset();
        cast();
        step(Game.SacRite + 0.1f);
        me.SacKills = 40;
        me.SacT = 0.02f;
        step(0.05f);
        Check(Math.Abs(me.Hp - me.MaxHp) < 0.5f, "the stored health stops at full");

        // Ending in the rite does nothing: you never died.
        reset();
        cast();
        step(Game.SacRite * 0.4f);
        me.SacT = 0.01f;
        step(0.05f);
        Check(!Game.SacOn(me) && me.Hp == me.MaxHp && me.Pos.DistanceTo(mePos) < 1f, "ended in the rite: nothing happens");

        // Mastered: 12 s, 15% a kill.
        reset();
        me.MasteryXp[AbilityId.SacrificialRitual] = 1e6f;
        cast();
        Check(Math.Abs(me.SacT - (Game.SacRite + Game.SacTimeMastered)) < 1e-3f, "mastered: the spirit lasts 12 s");
        step(Game.SacRite + 0.1f);
        me.SacKills = 3;
        me.SacT = 0.02f;
        step(0.05f);
        Check(Math.Abs(me.Hp - me.MaxHp * Game.SacPerKillMastered * 3f) < 0.5f, "mastered: 15% a kill");
        me.MasteryXp.Remove(AbilityId.SacrificialRitual);
        reset();

        // The bot's rule.
        me.Hp = me.MaxHp * 0.3f;
        bool low = def.Bot(new BotSense { Me = me, NearEnemies = Game.SacBotCrowd });
        me.Hp = me.MaxHp;
        bool healthy = def.Bot(new BotSense { Me = me, NearEnemies = Game.SacBotCrowd });
        Check(low && !healthy, "a bot sacrifices itself only when low in a crowd");

        host.Leave(""); client.Leave("");
    }
}
