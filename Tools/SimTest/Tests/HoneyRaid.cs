using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Honey Raid (HoneyRaid.cs): the Healer Summoner's key 1 on the lobby's Ultimate row (the pick reaching the run), the spot kept
// within range, the client seeing where the bee came in, the drips on the way, nothing hurt before the glob lands, the splash,
// the pool's slow (Heavy enemies wading), burn and heal, the set sticking grunts but not bosses, the end on time, mastered wider.
static partial class Program
{
    static void HoneyRaidTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby -----------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.HoneyRaid);
        Check(def.Name == "Honey Raid" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Kit == Kit.Summoner && def.Role == Role.Healer && def.Capstone != null,
            "Honey Raid is the Healer Summoner's ultimate on key 1, 80 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Summoner) == AbilityId.WorldTree && Upgrades.Ultimate(Role.Healer, Kit.Summoner, 1) == AbilityId.HoneyRaid
            && Upgrades.ClampUlt(Role.Healer, Kit.Summoner, 1) == 1 && Content.UltFallsBack(2),
            "World Tree stays the default, key 1 Honey Raid; a key a class lacks falls back");

        // --- Into a run -----------------------------------------------------------------------------------------------------
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Keeper", Role.Healer, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Summoner);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(1);
        settle();
        Check(host.Lobby[1].Ult == 1 && client.Lobby[1].Ult == 1 && host.Lobby[1].UltId == AbilityId.HoneyRaid, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var mate = g.Players[2];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.HoneyRaid && client.Game.Players[1].Slots[U].Id == AbilityId.HoneyRaid, "in the match the host's F is Honey Raid, on every game");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (where, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, where, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; spots[e] = where; return e;
        };
        V2 mePos = new V2(300f, 450f), aim = new V2(700f, 450f), matePos = new V2(300f, 900f);
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !spots.Keys.Any(e => e.Id == k)).ToList()) g.Enemies.Remove(id);
                foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.KnockLeft = V2.Zero; kv.Key.FireT = 99f; }
                g.Minions.Clear();
                me.Pos = mePos; mate.Pos = matePos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
            }
        };
        Func<Zone> raid = () => g.Zones.Values.FirstOrDefault(zz => zz.Kind == ZoneKind.HoneyRaid);
        Func<int> drips = () => g.Zones.Values.Count(zz => zz.Kind == ZoneKind.HoneyPuddle);
        Action reset = () =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); spots.Clear();
            foreach (var id in g.Zones.Keys.ToList()) g.Zones.Remove(id);
            me.Downed = false; me.Pos = mePos; me.Aim = 0f; me.Hp = me.MaxHp;
            me.Slots[U].Charges = 1; me.Slots[U].Timer = 0f;
            masteredField.SetValue(g, false);
            aim = new V2(700f, 450f); matePos = new V2(300f, 900f);
            g.State = MatchState.Fighting;
        };

        // --- The flight ------------------------------------------------------------------------------------------------------
        reset();
        aim = new V2(1500f, 450f);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        var z = raid();
        Check(z != null && Math.Abs(z.Duration - Game.HrTotal) < 1e-3f && Math.Abs(z.Radius - Game.HrRadius) < 0.01f, "casting starts it: 1.6 + 0.4 + 8 s, a 240 px pool");
        Check(z != null && Math.Abs((z.Pos - mePos).Length - Game.HrRange) < 2f, "aiming 1200 px away puts the spot 600 px out");
        Check(z != null && Math.Abs((z.Pos - z.Origin).Length - Game.HrRun) < 1f && z.Origin.X < mePos.X, "the bee comes in from 900 px behind you");
        step(0.1f);
        var cz = client.Game.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.HoneyRaid);
        Check(cz != null && cz.Origin.CloserThan(z.Origin, 2f) && Game.HrBeeAt(cz, 0.8f).CloserThan(Game.HrBeeAt(z, 0.8f), 2f), "the client sees where it came in, so it flies the bee the same");
        step(Game.HrFly);
        int n = drips();
        Console.WriteLine("  honey raid: {0} drips on the way in", n);
        Check(n >= 5 && g.Zones.Values.Where(zz => zz.Kind == ZoneKind.HoneyPuddle).All(zz => zz.Pos.FartherThan(z.Pos, Game.HrDripStop - 1f)), "it drips honey puddles on the way, none on the spot itself");
        Check(Game.HrBeeAt(z, Game.HrFly).CloserThan(z.Pos, 1f), "it is over the spot at 1.6 s");

        // --- The splash and the pool -----------------------------------------------------------------------------------------
        reset();
        V2 at = new V2(700f, 450f);
        var under = pin(at + new V2(40f, 0f), EnemyType.Grunt);
        var inPool = pin(at + new V2(0f, 190f), EnemyType.Grunt);
        var outside = pin(at + new V2(0f, -320f), EnemyType.Grunt);
        var heavyType = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().First(t => (int)t < 250 && Defs.Get(t).Heavy && !Defs.Get(t).IsBoss && Defs.Get(t).Speed > 0f);
        var heavy = pin(at + new V2(-150f, 0f), heavyType);
        matePos = at + new V2(60f, 60f);
        mate.Hp = mate.MaxHp * 0.3f;
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = raid();
        step(Game.HrLandAt - 0.1f);
        Check(under.Hp == under.MaxHp && inPool.Hp == inPool.MaxHp, "nothing is hurt before the glob lands");
        float mateBefore = mate.Hp;
        step(0.1f + dt * 2);
        float lostUnder = under.MaxHp - under.Hp;
        Console.WriteLine("  honey raid: the splash took {0:0} under it (x{1:0.00})", lostUnder, z.DmgMult);
        Check(Math.Abs(lostUnder - Game.HrSplashDmg * z.DmgMult) < 0.25f * Game.HrSplashDmg * z.DmgMult, "the glob splashes 40 on what's under it");
        Check(inPool.Hp > inPool.MaxHp - 1f, "the splash doesn't reach 190 px out");
        step(Game.HrSpread + 0.1f);
        Check(inPool.SlowT > 0f && Math.Abs(inPool.SlowMult - (1f - Game.HrSlow)) < 0.01f, "a grunt in the pool is slowed 55%");
        Check(heavy.WadeT > 0f && Math.Abs(heavy.WadeMult - (1f - Game.HrSlowHeavy)) < 0.01f, "a heavy enemy wades: 35%");
        Check(outside.SlowT <= 0f && outside.Hp == outside.MaxHp, "nothing outside it is touched");
        float hp0 = inPool.Hp, mate0 = mate.Hp;
        step(2f);
        float burn = (hp0 - inPool.Hp) / 2f, heal = (mate.Hp - mate0) / 2f;
        Console.WriteLine("  honey raid: the pool burns {0:0.0}/s, heals {1:0.0}/s", burn, heal);
        Check(Math.Abs(burn - Game.HrDps * z.DmgMult) < 0.25f * Game.HrDps * z.DmgMult, "the pool burns 12 a second");
        Check(heal > Game.HrHeal * 0.7f && mate.Hp > mateBefore, "an ally in it heals about 10 a second");

        // --- The set and the end ----------------------------------------------------------------------------------------------
        step(Game.HrPool - Game.HrSet - Game.HrSpread - 0.2f - 2f - 0.1f);
        Check(inPool.StunT <= 0f, "nothing is stuck before it sets");
        step(0.3f);
        Check(inPool.StunT > Game.HrStuck - 0.35f && heavy.StunT > 0f, "as it sets, enemies in it are stuck 1 s");
        Check(outside.StunT <= 0f, "not the ones outside");
        step(Game.HrSet + 0.2f);
        Check(raid() == null, "it ends on time");

        // --- A boss isn't stuck; mastered is wider ----------------------------------------------------------------------------
        reset();
        var bossType = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().First(t => (int)t < 250 && Defs.Get(t).IsBoss);
        var boss = pin(at + new V2(0f, 100f), bossType);
        masteredField.SetValue(g, true);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = raid();
        Check(z != null && Math.Abs(z.Radius - Game.HrRadiusMastered) < 0.01f, "mastered, the pool is 300 px");
        step(Game.HrTotal - Game.HrSet + 0.2f);
        Console.WriteLine("  honey raid: a boss in the setting honey wades x{0:0.00}, stun {1:0.00}", boss.WadeMult, boss.StunT);
        Check(boss.StunT <= 0f && boss.WadeT > 0f && Math.Abs(boss.WadeMult - (1f - Game.HrSlowBoss)) < 0.01f, "a boss is slowed 20% and never stuck");
    }
}
