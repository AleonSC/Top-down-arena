using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Tortoise Oracle (TortoiseOracle.cs): the Healer Summoner's key 2 on the lobby's Ultimate row (the pick reaching the run), the spot
// kept within range, the client seeing the circle, enemies inside at half time (bosses 0.75), enemy shots over it at half speed
// (and aging as slowly), allies inside healed twice as much, firing twice as fast and recharging their abilities (not the
// ultimate) twice as fast, nothing outside touched, the end on time, mastered wider.
static partial class Program
{
    static void TortoiseOracleTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        var enemyTime = typeof(Game).GetMethod("OracleEnemyTime", flags);
        var fireRate = typeof(Game).GetMethod("FireRate", flags);
        var heal = typeof(Game).GetMethod("Heal", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var moveBullets = typeof(Game).GetMethod("MoveBullets", flags);

        // --- The lobby -----------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.TortoiseOracle);
        Check(def.Name == "Tortoise Oracle" && def.Cooldown == 90f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Kit == Kit.Summoner && def.Role == Role.Healer && def.Capstone != null,
            "Tortoise Oracle is the Healer Summoner's ultimate on key 2, 90 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Summoner) == AbilityId.WorldTree && Upgrades.Ultimate(Role.Healer, Kit.Summoner, 1) == AbilityId.HoneyRaid
            && Upgrades.Ultimate(Role.Healer, Kit.Summoner, 2) == AbilityId.TortoiseOracle && Upgrades.ClampUlt(Role.Healer, Kit.Summoner, 2) == 2,
            "World Tree stays the default, Honey Raid key 1, Tortoise Oracle key 2");

        // --- Into a run -----------------------------------------------------------------------------------------------------
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Keeper", Role.Healer, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Summoner);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(2);
        settle();
        Check(host.Lobby[1].Ult == 2 && client.Lobby[1].Ult == 2 && host.Lobby[1].UltId == AbilityId.TortoiseOracle, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var mate = g.Players[2];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.TortoiseOracle && client.Game.Players[1].Slots[U].Id == AbilityId.TortoiseOracle, "in the match the host's F is Tortoise Oracle, on every game");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(300f, 450f), aim = new V2(600f, 450f), matePos = new V2(650f, 480f);
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                g.Enemies.Clear(); g.Bullets.Clear(); g.Minions.Clear();
                me.Pos = mePos; mate.Pos = matePos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
            }
        };
        Func<Zone> oracle = () => g.Zones.Values.FirstOrDefault(zz => zz.Kind == ZoneKind.TortoiseOracle);
        Action reset = () =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            foreach (var id in g.Zones.Keys.ToList()) g.Zones.Remove(id);
            me.Downed = false; me.Pos = mePos; me.Aim = 0f; me.Hp = me.MaxHp;
            me.Slots[U].Charges = 1; me.Slots[U].Timer = 0f;
            masteredField.SetValue(g, false);
            aim = new V2(600f, 450f); matePos = new V2(650f, 480f);
            g.State = MatchState.Fighting;
        };

        // --- The cast -------------------------------------------------------------------------------------------------------
        reset();
        aim = new V2(1500f, 450f);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        var z = oracle();
        Check(z != null && z.Pos.CloserThan(mePos, Game.ToRange + 1f) && Math.Abs(z.Radius - Game.ToRadius) < 0.01f, "a far aim is kept within 500 px; the circle is 240 px");
        reset();
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = oracle();
        Check(z != null && z.Pos.CloserThan(aim, 1f), "it lands on the cursor");
        step(Game.ToGrow + 0.1f);
        var cz = client.Game.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.TortoiseOracle);
        Check(cz != null && cz.Pos.CloserThan(z.Pos, 2f) && Math.Abs(Game.ToReach(cz) - z.Radius) < 1f, "the client sees the circle, grown in");

        // --- Enemies and their shots ----------------------------------------------------------------------------------------
        var inside = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, aim + new V2(60f, 0f), 0f, 0, false });
        var outside = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, aim + new V2(400f, 0f), 0f, 0, false });
        var boss = (Enemy)spawn.Invoke(g, new object[] { FirstEnemy(d => d.IsBoss), aim + new V2(-80f, 0f), 0f, 0, false });
        float tIn = (float)enemyTime.Invoke(g, new object[] { inside }), tOut = (float)enemyTime.Invoke(g, new object[] { outside }), tBoss = (float)enemyTime.Invoke(g, new object[] { boss });
        Console.WriteLine("  tortoise oracle: enemy time inside {0:0.00}, outside {1:0.00}, boss {2:0.00}", tIn, tOut, tBoss);
        Check(tIn == Game.ToEnemyTime && tOut == 1f && tBoss == Game.ToBossTime, "enemies inside run at half time, bosses at 0.75, outside untouched");
        g.Enemies.Clear();
        V2 v = new V2(200f, 0f);
        var bIn = (Bullet)spawnBullet.Invoke(g, new object[] { (byte)10, aim + new V2(0f, -120f), v, 0f, 3f, 1f, 0, false, 0f });
        var bOut = (Bullet)spawnBullet.Invoke(g, new object[] { (byte)10, aim + new V2(0f, 500f), v, 0f, 3f, 1f, 0, false, 0f });
        V2 pIn = bIn.Pos, pOut = bOut.Pos;
        float lIn = bIn.Life, lOut = bOut.Life;
        moveBullets.Invoke(g, new object[] { 0.1f });
        float dIn = bIn.Pos.DistanceTo(pIn), dOut = bOut.Pos.DistanceTo(pOut);
        Console.WriteLine("  tortoise oracle: an enemy shot moves {0:0.0} px inside, {1:0.0} outside in 0.1 s", dIn, dOut);
        Check(Math.Abs(dIn - 10f) < 0.5f && Math.Abs(dOut - 20f) < 0.5f, "enemy shots over the circle fly at half speed");
        Check(Math.Abs((lIn - bIn.Life) - 0.05f) < 0.001f && Math.Abs((lOut - bOut.Life) - 0.1f) < 0.001f, "and age half as fast, keeping their reach");
        g.Bullets.Clear();

        // --- Allies inside --------------------------------------------------------------------------------------------------
        Check(g.OracleTempo(mate) == 2f && g.OracleTempo(me) == 1f, "the ally in it runs at double tempo, the caster outside at 1");
        float fIn = (float)fireRate.Invoke(g, new object[] { mate });
        matePos = new V2(650f, 1100f); mate.Pos = matePos;
        float fOut = (float)fireRate.Invoke(g, new object[] { mate });
        Check(Math.Abs(fIn / fOut - 2f) < 0.01f, "it fires twice as fast inside");
        matePos = new V2(650f, 480f); mate.Pos = matePos;
        mate.Hp = mate.MaxHp * 0.3f;
        float healed = (float)heal.Invoke(g, new object[] { mate, 10f, me, null });
        me.Hp = me.MaxHp * 0.3f;
        float healedMe = (float)heal.Invoke(g, new object[] { me, 10f, me, null });
        Console.WriteLine("  tortoise oracle: a 10 heal gives {0:0.0} inside, {1:0.0} outside", healed, healedMe);
        Check(Math.Abs(healed - 20f) < 0.01f && Math.Abs(healedMe - 10f) < 0.01f, "heals on allies inside count double");
        int slot1 = 0;
        for (int i = 0; i < U; i++) if (mate.Slots[i].Id != AbilityId.None) { slot1 = i; break; }
        mate.Slots[slot1].Charges = 0; mate.Slots[slot1].MaxCharges = 1; mate.Slots[slot1].Timer = 5f;
        mate.Slots[U].Charges = 0; mate.Slots[U].Timer = 50f;
        step(1f);
        Console.WriteLine("  tortoise oracle: in 1 s an ability's cooldown drops {0:0.00} s, the ultimate's {1:0.00} s", 5f - mate.Slots[slot1].Timer, 50f - mate.Slots[U].Timer);
        Check(Math.Abs(5f - mate.Slots[slot1].Timer - 2f) < 0.05f && Math.Abs(50f - mate.Slots[U].Timer - 1f) < 0.05f, "abilities recharge twice as fast inside, the ultimate doesn't");

        // --- The end; mastered is wider -------------------------------------------------------------------------------------
        step(Game.ToTime);
        Check(oracle() == null && g.OracleTempo(mate) == 1f, "it ends after 10 s");
        reset();
        masteredField.SetValue(g, true);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = oracle();
        Check(z != null && Math.Abs(z.Radius - Game.ToRadiusMastered) < 0.01f, "mastered, the circle is 300 px");
    }
}
