using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Blood Boil (BloodBoil.cs): key 2 of the DPS Hemomancer's Ultimate row reaching the run; the cast (10 s); the drain (7% a
// second, never under 5%); speed, fire rate and damage growing as health falls and back to normal at the end, the health
// kept; pressing again to end it; going down; the client seeing it (BoilT, BoilLen); mastered; the bot's rule.
static partial class Program
{
    static void BloodBoilTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var fireRate = typeof(Game).GetMethod("FireRate", flags);

        // --- The lobby: key 2 of the DPS Hemomancer's Ultimate row --------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.BloodBoil);
        Check(def.Name == "Blood Boil" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Dps && def.Kit == Kit.Hemomancer,
            "Blood Boil is the DPS Hemomancer's ultimate on key 2, 70 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Hemomancer, 2) == AbilityId.BloodBoil && Upgrades.Ultimate(Role.Dps, Kit.Hemomancer, 1) == AbilityId.BloodRavager
            && Upgrades.Ultimate(Role.Dps, Kit.Hemomancer) == AbilityId.CrimsonHarvest,
            "key 2 is Blood Boil; key 1 is still Blood Ravager and Crimson Harvest the default");
        Check(Upgrades.ClampUlt(Role.Dps, Kit.Hemomancer, 2) == 2 && Upgrades.Ultimate(Role.Healer, Kit.Hemomancer, 2) != AbilityId.BloodBoil && Content.UltFallsBack(2),
            "key 2 is the DPS's; a class without a key 2 falls back to the default");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Boil", Role.Dps, 0, 0, Kit.Hemomancer, 0, 2);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0, Kit.Hemomancer, 0, 2);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 2 && client.Lobby[1].Ult == 2);
        Check(host.Lobby[1].UltId == AbilityId.BloodBoil && host.Lobby[cid].Ult == 2, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.BloodBoil && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.BloodBoil,
            "in the run both players' ultimate is Blood Boil, on both games");

        // --- On the host's game -------------------------------------------------------------------------------------------
        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                g.Enemies.Clear();
                g.Bullets.Clear();
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = me.Pos + new V2(200f, 0f) });
            }
        };
        Action reset = () =>
        {
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.BoilT = 0f; me.InvulnT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
            me.MasteryXp.Remove(AbilityId.BloodBoil);
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);  // (the wave never runs out: its end would end the boil)
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, false });
        Func<float> rate = () => (float)fireRate.Invoke(g, new object[] { me });
        Func<float> hit = () =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, me.Pos + new V2(300f, 0f), 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f;
            float hp0 = me.Hp;
            hurt.Invoke(g, new object[] { e, 100f, 1 });
            me.Hp = hp0;  // (the 3% lifesteal back out)
            float d = e.MaxHp - e.Hp;
            g.Enemies.Remove(e.Id);
            return d;
        };

        // The cast: at full health nothing is faster yet.
        reset();
        float baseSpeed = me.Speed, baseRate = rate(), baseHit = hit();
        cast();
        Check(Game.BoilOn(me) && me.BoilLen == Game.BoilTime && Math.Abs(me.UltT - Game.BoilTime) < 1e-3f, "casting: it boils for 10 s");
        Check(Game.BoilLevel(me) < 0.01f && Math.Abs(me.Speed - baseSpeed) < 0.5f, "at full health it doesn't boil yet");

        // The drain: 7% a second.
        step(2f);
        float lost = 1f - me.Hp / me.MaxHp;
        Console.WriteLine("  boil: 2 s in, health {0:0}% (lost {1:0.0}%), boil {2:0.00}", 100f * me.Hp / me.MaxHp, 100f * lost, Game.BoilLevel(me));
        Check(Math.Abs(lost - 2f * Game.BoilDrain) < 0.02f, "it drains 7% of max health a second");

        // At 10% health: full boil.
        me.Hp = me.MaxHp * Game.BoilFull;
        float b = Game.BoilLevel(me);
        Check(Math.Abs(b - 1f) < 1e-3f, "at 10% health it boils fully");
        Console.WriteLine("  boil: full: speed {0:0} -> {1:0}, fire rate x{2:0.00} -> x{3:0.00}, a 100 hit {4:0} -> {5:0}", baseSpeed, me.Speed, baseRate, rate(), baseHit, hit());
        Check(Math.Abs(me.Speed - baseSpeed * (1f + Game.BoilSpeed)) < 1f, "you move 1.7 times as fast");
        Check(Math.Abs(rate() - baseRate * (1f + Game.BoilFire)) < 0.01f, "you fire twice as fast");
        float priceMult = 1.4f;  // Blood Price at 10% health
        Check(Math.Abs(hit() - baseHit / 1f * priceMult * (1f + Game.BoilDmg)) < 1f, "you hit 60% harder, on top of Blood Price");

        // Halfway down: half the boil.
        me.Hp = me.MaxHp * 0.55f;
        Check(Math.Abs(Game.BoilLevel(me) - 0.5f) < 1e-3f && Math.Abs(me.Speed - baseSpeed * (1f + Game.BoilSpeed * 0.5f)) < 1f, "at 55% health it boils halfway");

        // The floor: never below 5%.
        me.Hp = me.MaxHp * 0.07f;
        step(1f);
        Check(Math.Abs(me.Hp - me.MaxHp * Game.BoilFloor) < 0.5f && !me.Downed && Game.BoilOn(me), "the drain stops at 5%: it can't down you");

        // The end, on time: the health is kept, everything back to normal, a burst of steam.
        me.Hp = me.MaxHp * 0.3f;
        g.Effects.Clear();
        float hpEnd = me.Hp;
        for (int i = 0; i < 2000 && Game.BoilOn(me); i++) { hpEnd = me.Hp; step(dt); }
        Check(!Game.BoilOn(me) && me.BoilT == 0f && me.UltT == 0f, "it ends when the time is up");
        step(0.5f);
        Check(Math.Abs(me.Hp - hpEnd) < 2f, "and the drain stops there");
        Console.WriteLine("  boil: ended at {0:0}% health; speed {1:0} (was {2:0}), fire x{3:0.00}", 100f * me.Hp / me.MaxHp, me.Speed, baseSpeed, rate());
        Check(me.Hp < me.MaxHp * 0.3f && Math.Abs(me.Speed - baseSpeed) < 0.5f && Math.Abs(rate() - baseRate) < 0.01f, "you keep the health you're at; speed and fire rate go back to normal");
        Check(Math.Abs(hit() - baseHit * (1f + 0.4f * Math.Min(1f, (1f - me.Hp / me.MaxHp) / 0.9f))) < 1f, "damage is back to Blood Price alone");
        Check(g.Effects.Any(e => e.Kind == FxKind.BoilEnd), "a burst of steam at the end");

        // Pressing again ends it, with no cooldown spent.
        reset();
        cast();
        step(1f);
        float hpAt = me.Hp;
        me.Slots[Upgrades.UltSlot].Charges = 0;
        cast();
        Check(!Game.BoilOn(me) && Math.Abs(me.Hp - hpAt) < 0.5f, "pressing it again ends it and the health stays");
        step(0.5f);
        Check(Math.Abs(me.Hp - hpAt) < 2f, "and the drain stops");

        // Going down ends it.
        reset();
        cast();
        step(0.2f);
        me.Downed = true;
        step(0.1f);
        Check(me.BoilT == 0f, "going down ends it");
        me.Downed = false;

        // Mastered: +90% at full boil.
        reset();
        me.MasteryXp[AbilityId.BloodBoil] = Game.MasteryXpFor[Game.MasteryMax - 1];
        cast();
        me.Hp = me.MaxHp * Game.BoilFull;
        Check(Math.Abs(hit() - baseHit * priceMult * (1f + Game.BoilDmgMastered)) < 1f, "mastered: 90% harder at full boil");
        me.MasteryXp.Remove(AbilityId.BloodBoil);

        // The client sees its own boil (BoilT, BoilLen), so it moves and fires as fast.
        reset();
        var cme = cg.Players[cid];
        ally.Hp = ally.MaxHp;
        ally.Slots[Upgrades.UltSlot].Charges = 1; ally.Slots[Upgrades.UltSlot].Timer = 0f;
        hostAbility.Invoke(g, new object[] { cid, Upgrades.UltSlot, ally.Pos, false });
        g.DevSetGod(cid, false);
        step(1.5f);
        Run(s, dt, 1, () => cme.BoilT > 0.5f);
        Console.WriteLine("  boil: client: host BoilT {0:0.00} hp {1:0}%, client BoilT {2:0.00} len {3:0} hp {4:0}%", ally.BoilT, 100f * ally.Hp / ally.MaxHp, cme.BoilT, cme.BoilLen, 100f * cme.Hp / cme.MaxHp);
        Check(Game.BoilOn(cme) && cme.BoilLen == Game.BoilTime && Game.BoilLevel(cme) > 0.05f, "the client's game sees its own boil (BoilT, BoilLen) and how hard it boils");
        g.DevSetGod(cid, true);

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, HaveTarget = true, TargetD = 200f }) && !def.Bot(new BotSense { Me = me, HaveTarget = false }), "a bot casts it with a target near and its health high");

        host.Leave(""); client.Leave("");
    }
}
