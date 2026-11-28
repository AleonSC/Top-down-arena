using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Excalibur (Excalibur.cs): key 3 of the DPS Knight's Ultimate row; the draw (slowed, no swings); a swing cuts the 150° arc in
// front out to 230 px (nothing behind) and throws a shockwave that goes through everything in its crescent (once each) and cuts
// small shots; the 0.38 s rate; every 4th swing a Judgement (harder blade, a wider, longer wave that shoves and cuts red shots);
// the weapon put away; the fields reaching a client; the burst on time; going down (no burst); mastered; the bot's rule.
static partial class Program
{
    static void ExcaliburTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnBullet = typeof(Game).GetMethods(flags).First(m => m.Name == "SpawnBullet" && m.GetParameters().Length == 9);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby -----------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.Excalibur);
        Check(def.Name == "Excalibur" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Dps && def.Kit == Kit.Knight,
            "Excalibur is the DPS Knight's ultimate on key 3, 70 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Knight, 3) == AbilityId.Excalibur && Upgrades.Ultimate(Role.Dps, Kit.Knight) == AbilityId.WarhornVolley
            && Upgrades.Ultimate(Role.Dps, Kit.Knight, 1) == AbilityId.Ballista && Upgrades.Ultimate(Role.Dps, Kit.Knight, 2) == AbilityId.WarStandard,
            "key 3 is Excalibur; Warhorn Volley, Ballista and War Standard keep keys 0-2");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Arthur", Role.Dps, 0, 0, Kit.Knight, 0, 3);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Tank, 0, 0, Kit.Soldier, 0, 0);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        Run(s, dt, 2, () => client.Lobby[1].Ult == 3);
        Check(host.Lobby[1].UltId == AbilityId.Excalibur && client.Lobby[1].Ult == 3, "the pick reaches the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.Excalibur, "in the run the ultimate is Excalibur");
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        g.DevSetGod(1, true);
        V2 home = new V2(400f, 450f);
        var mine = new HashSet<int>();
        var pinned = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.Pos = at; mine.Add(e.Id); pinned[e] = at; return e;
        };
        bool fire = false;
        V2 aim = home + new V2(300f, 0f);
        bool keepShots = false;
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                if (!keepShots)
                    foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                foreach (var kv in pinned) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; }  // (they stay put and hold fire)
                ally.Pos = new V2(150f, 150f);
                me.Pos = home;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Action reset = () =>
        {
            foreach (var id in mine) g.Enemies.Remove(id);
            mine.Clear(); pinned.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.ExcT = 0f; me.ExcUp = false; me.ExcCd = 0f;
            me.ExcSwings = me.ExcJudges = me.ExcHits = me.ExcCuts = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = home; masteredField.SetValue(g, false);
            fire = false; keepShots = false; aim = home + new V2(300f, 0f);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, false });
        Func<Enemy, float> took = e => e.MaxHp - e.Hp;
        float dmg = me.Stats.DmgMult;

        // The draw: slowed, and Fire does nothing yet.
        reset();
        cast();
        Check(Math.Abs(me.ExcT - (Game.ExcDraw + Game.ExcTime)) < 1e-3f && me.UltT == me.ExcT && Game.ExcOn(me) && !Game.ExcReady(me),
            "casting: a 0.5 s draw, then 10 s");
        Check(Game.ExcSpeed(me) == Game.ExcDrawMove, "you walk at half speed while the sword is drawn");
        fire = true; step(0.25f); fire = false;
        Check(me.ExcSwings == 0, "Fire in the draw doesn't swing");
        step(Game.ExcDraw);
        Check(Game.ExcReady(me) && Game.ExcSpeed(me) == Game.ExcMove, "past the draw the sword is out and you move at 90%");

        // A swing: the blade's arc in front, then the wave through everything in its crescent, once each.
        var front = pin(home + new V2(150f, 20f), EnemyType.Grunt);
        var behind = pin(home + new V2(-120f, 0f), EnemyType.Grunt);
        var far = pin(home + new V2(500f, 40f), EnemyType.Grunt);
        var past = pin(home + new V2(Game.ExcWaveRange + 120f, 0f), EnemyType.Grunt);
        var wide = pin(home + new V2(300f, 300f), EnemyType.Grunt);  // 45° off the aim: in the blade's arc reach? no (424 px), outside the wave's 36°
        fire = true; step(dt); fire = false;
        Check(me.ExcSwings == 1 && Math.Abs(took(front) - Game.ExcBladeDamage * dmg) < 1f && took(far) == 0f,
            "a swing's blade hits what's within 230 px in front (60), not further");
        step(1f);
        Console.WriteLine("  excalibur: one swing: in front took {0:0}, 500 px out {1:0}, behind {2:0}, past the wave {3:0}, 45° off {4:0}",
            took(front), took(far), took(behind), took(past), took(wide));
        Check(Math.Abs(took(front) - (Game.ExcBladeDamage + Game.ExcWaveDamage) * dmg) < 1f, "the wave then goes through the near one too (60 + 90)");
        Check(Math.Abs(took(far) - Game.ExcWaveDamage * dmg) < 1f, "and reaches 500 px out (90, once)");
        Check(took(behind) == 0f && took(past) == 0f && took(wide) == 0f, "nothing behind, past 700 px or outside the crescent");

        // Small shots in the wave's path are cut; red aimed ones aren't (until a Judgement).
        reset();
        cast();
        step(Game.ExcDraw + dt);
        keepShots = true;
        var small = (Bullet)spawnBullet.Invoke(g, new object[] { (byte)10, home + new V2(300f, 0f), new V2(0f, 0f), 0f, 5f, 5f, 0, false, 0f });
        int smallId = small.Id;
        fire = true; step(dt); fire = false;
        step(0.5f);
        Check(!g.Bullets.ContainsKey(smallId) && me.ExcCuts >= 1, "the wave cuts the small enemy shots it passes");

        // Held Fire: a swing every 0.38 s, the 4th a Judgement.
        reset();
        cast();
        step(Game.ExcDraw + dt);
        var target = pin(home + new V2(120f, 0f), EnemyType.Grunt);
        var heavyType = HeavyFoe(moving: true);
        var heavy = pin(home + new V2(160f, 60f), heavyType);
        fire = true;
        float t0 = 0f;
        while (me.ExcSwings < 3 && t0 < 3f) { step(dt); t0 += dt; }
        Console.WriteLine("  excalibur: 3 swings in {0:0.00} s (one every {1:0.000} s)", t0, t0 / 2f);
        Check(t0 <= 2f * Game.ExcSwingEvery + 3f * dt + 0.02f, "held Fire swings about every 0.38 s");
        Check(Game.ExcSwingNo(me) == 3 && me.ExcJudges == 0, "the swing number is replicated through ExcSwing");
        while (me.ExcJudges < 1 && t0 < 4f) { step(dt); t0 += dt; }
        Check(me.ExcJudges == 1 && Game.ExcIsJudge(Game.ExcSwingNo(me)), "the 4th swing is the Judgement");
        int stepAt = me.ExcStep;
        step(Game.ExcJudgeRecover - 2f * dt);
        Check(me.ExcStep == stepAt, "the next swing waits 0.5 s after a Judgement");
        int shots = g.Bullets.Values.Count(b => b.Owner == 1);
        step(0.4f);
        fire = false;
        Check(me.ExcStep > stepAt && g.Bullets.Values.Count(b => b.Owner == 1) == shots, "then the swings go on, and your weapon fires nothing: it's put away");
        Check(took(heavy) > 0f, "the swings hurt heavies too (" + heavyType + ")");

        // The Judgement: harder, a wider and longer wave.
        reset();
        cast();
        step(Game.ExcDraw + dt);
        me.ExcStep = Game.ExcJudgeEvery - 1;
        var nearJ = pin(home + new V2(150f, 0f), EnemyType.Grunt);
        var farJ = pin(home + new V2(900f, 0f), EnemyType.Grunt);
        var wideJ = pin(home + V2.FromAngle(0.85f) * 400f, EnemyType.Grunt);  // outside a plain wave's crescent, inside a Judgement's
        fire = true; step(dt); fire = false;
        Check(me.ExcJudges == 1 && Math.Abs(took(nearJ) - Game.ExcJudgeDamage * dmg) < 1f, "the Judgement's blade hits for 140");
        step(1.3f);
        Console.WriteLine("  excalibur: Judgement: near {0:0}, 900 px out {1:0}, 49° off at 400 px {2:0}", took(nearJ), took(farJ), took(wideJ));
        Check(Math.Abs(took(farJ) - Game.ExcJudgeWaveDamage * dmg) < 1f && Math.Abs(took(wideJ) - Game.ExcJudgeWaveDamage * dmg) < 1f,
            "its wave flies 1,000 px and wider, for 180");

        // The fields reach the client.
        reset();
        cast();
        step(Game.ExcDraw + dt);
        fire = true; step(dt); fire = false;
        for (int i = 0; i < 4; i++) { host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aim }); cg.FixedStep(dt, new PlayerInput()); Nap(1); }
        var seen = cg.Players[1];
        Check(seen.ExcT > 0f && Math.Abs(seen.ExcAge - me.ExcAge) < 0.2f && Game.ExcSwingNo(seen) == 1 && Game.ExcOn(seen), "the client sees the sword, its age and the swing");

        // On time: the sword bursts into light.
        reset();
        cast();
        step(Game.ExcDraw + dt);
        var ringIn = pin(home + new V2(0f, 150f), EnemyType.Grunt);
        var ringOut = pin(home + new V2(0f, -320f), EnemyType.Grunt);
        me.ExcT = 0.02f;
        step(0.05f);
        Check(me.ExcT == 0f && me.UltT == 0f && !me.ExcUp && !Game.ExcOn(me), "it ends on time");
        Check(Math.Abs(took(ringIn) - Game.ExcBurstDamage * dmg) < 1f && took(ringOut) == 0f, "and bursts: 120 to everything within 200 px");

        // Going down ends it with no burst.
        reset();
        cast();
        step(Game.ExcDraw + dt);
        var nearD = pin(home + new V2(0f, 150f), EnemyType.Grunt);
        me.Downed = true;
        step(dt);
        me.Downed = false; me.Hp = me.MaxHp;
        step(0.2f);
        Check(me.ExcT == 0f && !me.ExcUp && took(nearD) == 0f, "going down ends it at once, with no burst");

        // Mastered.
        reset();
        me.MasteryXp[AbilityId.Excalibur] = 1e6f;
        cast();
        Check(Math.Abs(me.ExcT - (Game.ExcDraw + Game.ExcTimeMastered)) < 1e-3f && me.ExcMastered && Game.ExcRange(false, true) > Game.ExcWaveRange * 1.25f,
            "mastered: 13 s and the waves fly 30% farther");
        me.MasteryXp.Remove(AbilityId.Excalibur);

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, HaveTarget = true, TargetD = 300f }) && !def.Bot(new BotSense { Me = me, HaveTarget = true, TargetD = 600f }),
            "a bot casts it with a target within 420 px");

        host.Leave(""); client.Leave("");
    }
}
