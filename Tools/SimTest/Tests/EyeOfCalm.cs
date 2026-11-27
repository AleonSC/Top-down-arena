using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Eye of Calm (EyeOfCalm.cs): key 2 of the Healer Elementalist's Ultimate row reaching the run; the spin-up; allies in the eye
// healing 12 a second; enemies put out of the eye, drifting round the wall, slowed and hurt 22 a second (a heavy hurt but not
// moved); shots swept by the wall, red aimed shots only in the eye; your speed; the final gust on time and when you go down;
// mastered; the bot's rule; other screens seeing it.
static partial class Program
{
    static void EyeOfCalmTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 2 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.EyeOfCalm);
        Check(def.Name == "Eye of Calm" && def.Cooldown == 85f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Healer && def.Kit == Kit.Elementalist,
            "Eye of Calm is the Healer Elementalist's ultimate on key 2, 85 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Elementalist, 2) == AbilityId.EyeOfCalm && Upgrades.Ultimate(Role.Healer, Kit.Elementalist, 1) == AbilityId.StaticBubble
            && Upgrades.Ultimate(Role.Healer, Kit.Elementalist) == AbilityId.Sanctum,
            "key 2 is Eye of Calm; Static Bubble stays key 1 and Sanctum the default");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Calm", Role.Healer, 0, 0, Kit.Elementalist, 0, 2);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 0, 0, Kit.Elementalist, 0, 2);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 2 && client.Lobby[1].Ult == 2);
        Check(host.Lobby[1].UltId == AbilityId.EyeOfCalm && host.Lobby[cid].Ult == 2 && client.Lobby[1].Ult == 2, "the pick reaches the host and the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.EyeOfCalm && client.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.EyeOfCalm,
            "in the run both players' ultimate is Eye of Calm, on both games");

        // --- The storm on the host's game -------------------------------------------------------------------------------
        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(1, true);
        V2 mePos = new V2(700f, 450f), allyPos = new V2(150f, 150f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; mine.Add(e.Id); return e;
        };
        bool keepShots = false;
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                if (!keepShots)
                    foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                me.Pos = mePos; ally.Pos = allyPos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mePos + new V2(300f, 0f) });
            }
        };
        Action reset = () =>
        {
            foreach (var id in mine) g.Enemies.Remove(id);
            mine.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.EyeT = 0f; me.EyeUp = false; me.EyeHits = me.EyeSwept = me.EyeFlung = 0; me.EyeHealed = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            allyPos = new V2(150f, 150f);
            keepShots = false;
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        float dmg = me.Stats.DmgMult;
        Func<Enemy, float> dist = e => e.Pos.DistanceTo(me.Pos);
        Func<Enemy, float> ang = e => (e.Pos - me.Pos).Angle;

        // Casting and the spin-up.
        reset();
        float baseSpeed = me.Speed;
        cast();
        Check(Math.Abs(me.EyeT - Game.EyeTime) < 1e-3f && me.UltT == me.EyeT && me.EyeWallR == Game.EyeWall && Game.EyeOn(me), "casting: 10 s, a 280 px wall");
        Check(Math.Abs(me.Speed - baseSpeed * Game.EyeSelfSpeed) < 0.01f, "you move at 80% while it blows");
        Check(Game.EyeReach(me.EyeWallR, 0f) == Game.EyeRadius && Math.Abs(Game.EyeReach(me.EyeWallR, Game.EyeSpin * 0.5f) - (Game.EyeRadius + Game.EyeWall) / 2f) < 0.5f
            && Game.EyeReach(me.EyeWallR, 5f) == Game.EyeWall, "the wall grows out from the eye over the 0.8 s spin-up");
        step(0.1f);
        Check(client.Game.Players[1].EyeT > 0f && client.Game.Players[1].EyeWallR == Game.EyeWall && client.Game.Players[1].EyeAge > 0f, "other screens see the storm");

        // The wall: enemies drift round it, are slowed and hurt; one in the eye is put out; one past the edge is left alone.
        reset();
        var inner = make(mePos + new V2(180f, 0f), EnemyType.Grunt);
        var inEye = make(mePos + new V2(0f, 30f), EnemyType.Grunt);
        var far = make(mePos + new V2(0f, Game.EyeWall + 120f), EnemyType.Grunt);
        V2 farAt = far.Pos;
        cast();
        step(Game.EyeSpin + 0.2f);
        Check(dist(inEye) >= Game.EyeRadius + inEye.Radius - 0.5f, "an enemy in the eye is put out at its edge");
        float hp0 = inner.Hp, a0 = ang(inner);
        step(1f);
        float perSec = hp0 - inner.Hp, turned = ang(inner) - a0;
        Console.WriteLine("  eye of calm: {0:0} damage a second in the wall (damage x{1:0.00}); a grunt 180 px out drifted {2:0.00} rad", perSec, dmg, turned);
        Check(Math.Abs(perSec - Game.EyeDps * dmg) <= Game.EyeDps * Game.EyeTick * dmg + 0.5f, "about 22 damage a second in the wall");
        Check(Math.Abs(turned) > 0.3f && Math.Abs(dist(inner) - 180f) < 2f, "it drifts round the wall at the same distance");
        Check(inner.SlowT > 0f && Math.Abs(inner.SlowMult - Game.EyeSlow) < 1e-3f, "and is slowed 45%");
        Check(far.Pos.DistanceTo(farAt) < 0.5f && far.Hp == far.MaxHp, "one past the edge is left alone");

        // A heavy is hurt but not moved.
        reset();
        var heavyType = HeavyFoe(moving: true);
        var heavy = make(mePos + new V2(0f, 160f), heavyType);
        V2 heavyAt = heavy.Pos;
        cast();
        step(1.5f);
        Check(heavy.Pos.DistanceTo(heavyAt) < 0.5f && heavy.Hp < heavy.MaxHp, "a heavy (" + heavyType + ") stays put but is hurt");

        // The calm eye heals the allies in it.
        reset();
        allyPos = mePos + new V2(40f, 0f);
        ally.Hp = ally.MaxHp * 0.3f;
        cast();
        step(0.05f);
        float ahp = me.EyeHealed;  // (the eye's own share: the healer's aura heals them too)
        step(1f);
        float healed = me.EyeHealed - ahp;
        Console.WriteLine("  eye of calm: an ally in the eye healed {0:0.0} in a second from it", healed);
        Check(Math.Abs(healed - Game.EyeHeal) < 1.5f, "an ally in the eye heals about 12 a second");
        allyPos = mePos + new V2(150f, 0f);
        ally.Hp = ally.MaxHp * 0.3f;
        step(0.05f);
        ahp = me.EyeHealed;
        step(0.5f);
        Check(me.EyeHealed <= ahp + 0.01f, "not out in the wall");

        // Shots: the wall sweeps ordinary ones, red aimed shots go on to the eye, where they're gone; outside they fly on.
        reset();
        cast();
        step(Game.EyeSpin + 0.1f);
        keepShots = true;
        var small = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, mePos + new V2(200f, 0f), new V2(-200f, 0f), 0f, 3f, 5f, 0, false, 0f });
        var red = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.PierceShot, mePos + new V2(0f, 200f), new V2(0f, -150f), 0f, 3f, 5f, 0, false, 0f });
        var outside = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, mePos + new V2(0f, -(Game.EyeWall + 200f)), new V2(200f, 0f), 0f, 3f, 5f, 0, false, 0f });
        step(dt);
        Check(!g.Bullets.ContainsKey(small.Id) && g.Bullets.ContainsKey(outside.Id), "an ordinary shot in the wall is swept away, one outside flies on");
        Check(g.Bullets.ContainsKey(red.Id), "a red aimed shot flies through the wall");
        step(0.8f);
        Check(!g.Bullets.ContainsKey(red.Id) && me.EyeSwept == 2, "and is gone when it reaches the eye");
        Check(g.Effects.Any(e => e.Kind == FxKind.VortexWisp), "with a wisp of wind");
        keepShots = false;

        // The final gust when it ends on time.
        reset();
        var ring = new List<Enemy>();
        for (int i = 0; i < 8; i++)
            ring.Add(make(mePos + V2.FromAngle(MathX.Tau * i / 8f) * 150f, EnemyType.Grunt));
        cast();
        step(1f);
        float before = ring.Average(e => dist(e));
        var hpBefore = ring.Select(e => e.Hp).ToList();
        me.EyeT = 0.02f;
        step(0.05f);
        Check(me.EyeT == 0f && me.UltT == 0f && !Game.EyeOn(me) && !me.EyeUp && Math.Abs(me.Speed - baseSpeed) < 0.01f, "it ends on time, and your speed comes back");
        Check(g.Effects.Any(e => e.Kind == FxKind.EyeGust), "in a gust");
        step(0.4f);
        float after = ring.Average(e => dist(e));
        Console.WriteLine("  eye of calm: the gust threw the ring from {0:0} px out to {1:0} px", before, after);
        Check(after - before > Game.EyeFling * 0.8f, "everyone in the wall is flung about 200 px out");
        Check(me.EyeFlung == 8 && ring.Select((e, i) => hpBefore[i] - e.Hp).All(x => x >= Game.EyeGust * dmg - 0.5f), "and takes 40");

        // Going down ends it.
        reset();
        cast();
        step(1f);
        me.Downed = true;
        step(dt);
        Check(me.EyeT == 0f && !me.EyeUp, "going down ends it at once");
        me.Downed = false; me.Hp = me.MaxHp;

        // Mastered.
        reset();
        me.MasteryXp[AbilityId.EyeOfCalm] = 1e6f;
        var wide = make(mePos + new V2(310f, 0f), EnemyType.Grunt);
        cast();
        Check(me.EyeWallR == Game.EyeWallMastered, "mastered: the wall reaches 335 px");
        step(1f);
        Check(wide.Hp < wide.MaxHp, "mastered: an enemy 310 px out is in the wall");
        float whp = wide.Hp;
        me.EyeT = 0.02f;
        step(0.05f);
        Check(whp - wide.Hp >= Game.EyeGustMastered * dmg - 0.5f, "mastered: the gust hits for 80");
        me.MasteryXp.Remove(AbilityId.EyeOfCalm);
        reset();

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, Crowd = Game.JuggBotCrowd }) && !def.Bot(new BotSense { Me = me, Crowd = 1 }), "a bot casts it in a crowd");

        host.Leave(""); client.Leave("");
    }
}
