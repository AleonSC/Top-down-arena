using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Sleepy Cat (SleepyCat.cs): the Healer Summoner's key 3 on the lobby's Ultimate row (the pick reaching the run); the cat running
// after the cursor (no faster than 560 px/s, seen by the client); Fire put away while it prowls; a click meows and marks every
// enemy in reach (held Fire doesn't repeat, a click in the gap waits for it), four meows; the nap after the last meow or the prowl's
// 5 s: marked enemies asleep for 10 s (bosses 3 s), the rest untouched, hits don't wake them, the client sees the marks and the
// sleep; the purr heals allies near the cat only; the end on time; going down ends the prowl; mastered five meows and a wider reach.
static partial class Program
{
    static void SleepyCatTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Keeper", Role.Healer, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Summoner);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        var def = Upgrades.Ability(AbilityId.SleepyCat);
        Check(def.Name == "Sleepy Cat" && def.Cooldown == Game.ScCooldown && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Kit == Kit.Summoner && def.Role == Role.Healer && def.Capstone != null,
            "Sleepy Cat is the Healer Summoner's ultimate on key 3");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Summoner) == AbilityId.WorldTree && Upgrades.Ultimate(Role.Healer, Kit.Summoner, 3) == AbilityId.SleepyCat && Upgrades.ClampUlt(Role.Healer, Kit.Summoner, 3) == 3,
            "World Tree stays the default, Sleepy Cat on key 3");
        host.RequestUlt(3);
        settle();
        Check(host.Lobby[1].Ult == 3 && client.Lobby[1].Ult == 3 && host.Lobby[1].UltId == AbilityId.SleepyCat, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var mate = g.Players[2];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.SleepyCat && client.Game.Players[1].Slots[U].Id == AbilityId.SleepyCat, "in the match the host's ultimate is Sleepy Cat, on every game");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(300f, 450f), aim = new V2(600f, 450f), matePos = new V2(300f, 1000f);
        bool fire = false;
        var pinned = new Dictionary<Enemy, V2>();
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                g.Bullets.Clear(); g.Minions.Clear();
                foreach (var id in g.Enemies.Keys.ToList()) if (!pinned.Keys.Any(e => e.Id == id)) g.Enemies.Remove(id);
                foreach (var kv in pinned) kv.Key.Pos = kv.Value;
                me.Pos = mePos; mate.Pos = matePos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Func<Zone> catZone = () => g.Zones.Values.FirstOrDefault(zz => zz.Kind == ZoneKind.SleepyCat);
        Func<V2, EnemyType, Enemy> enemyAt = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.SpawnT = 0f; e.Pos = at; e.Hp = e.MaxHp = 1e5f;
            pinned[e] = at;
            return e;
        };
        Action reset = () =>
        {
            pinned.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            foreach (var id in g.Zones.Keys.ToList()) g.Zones.Remove(id);
            me.Downed = false; me.Pos = mePos; me.Aim = 0f; me.Hp = me.MaxHp;
            me.Slots[U].Charges = 1; me.Slots[U].Timer = 0f;
            masteredField.SetValue(g, false);
            fire = false; aim = new V2(600f, 450f); matePos = new V2(300f, 1000f);
            g.State = MatchState.Fighting;
        };
        Action click = () => { fire = true; step(dt); fire = false; step(dt); };

        // --- The cast and the run -------------------------------------------------------------------------------------------
        reset();
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        var z = catZone();
        Check(z != null && !z.Quiet && z.BoltsLeft == Game.ScMeows && Math.Abs(z.Radius - Game.ScRadius) < 0.01f && g.CatOn(me), "the cat comes out prowling: 4 meows, a 95 px reach, Fire put away");
        V2 before = z.Pos;
        step(0.25f);
        float ran = z.Pos.DistanceTo(before);
        Console.WriteLine("  sleepy cat: ran {0:0} px in 0.25 s, speed {1:0}", ran, z.Marched);
        Check(ran > 40f && ran <= Game.ScSpeed * 0.25f + 1f && z.Pos.DistanceTo(aim) < before.DistanceTo(aim), "it runs toward the cursor, no faster than 560 px/s");
        step(1.2f);
        var cz = client.Game.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.SleepyCat);
        Check(z.Pos.DistanceTo(aim) < 12f && cz != null && cz.Pos.DistanceTo(z.Pos) < 30f && !cz.Quiet && cz.BoltsLeft == Game.ScMeows, "it settles on the cursor; the client sees it, prowling, 4 meows");

        // --- The meows ------------------------------------------------------------------------------------------------------
        var a1 = enemyAt(aim + new V2(50f, 0f), EnemyType.Grunt);
        var a2 = enemyAt(aim + new V2(-30f, 40f), EnemyType.Grunt);
        var far = enemyAt(aim + new V2(0f, 300f), EnemyType.Grunt);
        click();
        Check(a1.CatMark == z.Id && a2.CatMark == z.Id && far.CatMark == 0 && z.BoltsLeft == Game.ScMeows - 1, "a click meows: both enemies in reach are marked, the far one isn't");
        fire = true;
        step(1f);
        fire = false;
        Check(z.BoltsLeft == Game.ScMeows - 2, "holding Fire meows once, not again and again");
        step(dt * 2);
        aim = far.Pos;
        var boss = enemyAt(far.Pos + new V2(-40f, 0f), FirstEnemy(d => d.IsBoss));
        pinned[boss] = boss.Pos;
        step(0.8f);
        click();
        click();  // (in the gap: it waits for it)
        step(Game.ScGap + 0.05f);
        var ca1 = client.Game.Enemies.Values.FirstOrDefault(q => q.Id == a1.Id);
        Check(far.CatMark == z.Id && boss.CatMark == z.Id && z.BoltsLeft == 0 && !z.Quiet, "the cursor's new spot: a meow marks the far one and a boss; a click in the gap waits and meows after it (the last)");
        Check(ca1 != null && Game.CatMarked(ca1), "the client sees the paw mark");
        var unmarked = enemyAt(mePos + new V2(0f, -300f), EnemyType.Grunt);

        // --- The nap --------------------------------------------------------------------------------------------------------
        step(Game.ScLastHold);
        Check(z.Quiet && !g.CatOn(me), "it naps soon after the last meow; Fire is back");
        Console.WriteLine("  sleepy cat: a grunt sleeps {0:0.0} s, the boss {1:0.0} s, an unmarked one {2:0.0} s", a1.CatNapT, boss.CatNapT, unmarked.StunT);
        Check(a1.CatNapT > Game.ScNap - 0.5f && a1.CatNapT <= Game.ScNap && a1.StunT > Game.ScNap - 0.5f && a2.StunT > 9f && far.StunT > 9f, "every marked enemy sleeps 10 s");
        Check(boss.CatNapT > Game.ScBossNap - 0.5f && boss.CatNapT <= Game.ScBossNap && unmarked.StunT <= 0f && unmarked.CatNapT <= 0f, "a boss dozes 3 s; an unmarked enemy is untouched");
        hurt.Invoke(g, new object[] { a1, 500f, me.Id });
        step(1f);
        var ca2 = client.Game.Enemies.Values.FirstOrDefault(q => q.Id == a2.Id);
        Check(a1.StunT > 8f && a1.CatNapT > 8f, "a hit doesn't wake it");
        Check(ca2 != null && Game.CatAsleep(ca2) && Math.Abs(Game.CatSleepLeft(ca2) - a2.CatNapT) < 0.3f && !ca2.Dazed, "the client sees it asleep, with its time left (Zs, not daze stars)");
        matePos = z.Pos + new V2(60f, 0f); mate.Pos = matePos; mate.Hp = mate.MaxHp * 0.3f;
        me.Hp = me.MaxHp * 0.3f;
        float mateHp = mate.Hp, meHp = me.Hp;
        step(1f);
        Console.WriteLine("  sleepy cat: in 1 s the purr heals the ally beside it {0:0.0}, the caster {1:0} px off {2:0.0}", mate.Hp - mateHp, me.Pos.DistanceTo(z.Pos), me.Hp - meHp);
        Check(mate.Hp - mateHp > Game.ScPurr * 0.7f && me.Hp - meHp < 3f, "the purr heals an ally near the cat, not one far off (only the caster's own regen)");
        step(Game.ScNap - 2f + 0.1f);
        Check(a1.CatNapT <= 0f && a1.StunT <= 0f && boss.StunT <= 0f, "they wake after 10 s");
        step(Game.ScFade + 0.1f);
        Check(catZone() == null, "the cat goes after its nap");

        // --- The prowl's time runs out; going down ends it ------------------------------------------------------------------
        reset();
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = catZone();
        step(Game.ScProwl + 0.05f);
        Check(z.Quiet && z.BoltsLeft == Game.ScMeows, "unspent, the prowl ends after 5 s");
        reset();
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = catZone();
        step(0.5f);
        me.Downed = true;
        step(dt);
        Check(z.Quiet, "going down ends the prowl: the cat naps");
        me.Downed = false;

        // --- Mastered -------------------------------------------------------------------------------------------------------
        reset();
        masteredField.SetValue(g, true);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = catZone();
        Check(z != null && z.BoltsLeft == Game.ScMeowsMastered && Math.Abs(z.Radius - Game.ScRadiusMastered) < 0.01f, "mastered: five meows and a 120 px reach");
    }
}
