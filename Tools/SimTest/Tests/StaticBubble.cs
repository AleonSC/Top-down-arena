using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Static Bubble (StaticBubble.cs): key 1 of the Healer Elementalist's Ultimate row reaching the run; the cast (44 px, 10 s);
// enemies touching it put out at the edge, shoved, hurt, stunned and feeding it (+3, once per 0.6 s each; heavies too);
// enemy shots eaten (+1); allies inside can't be hurt; it grows as it fills; full, it bursts (damage, stun, shots erased)
// and gives Static Rush (speed and fire rate) to you and the allies in the blast; time running out pops it smaller;
// going down ends it quietly; mastered; the client sees it.
static partial class Program
{
    static void StaticBubbleTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var fireRate = typeof(Game).GetMethod("FireRate", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 1 of the Healer Elementalist's Ultimate row --------------------------------------------------
        var def = Upgrades.Ability(AbilityId.StaticBubble);
        Check(def.Name == "Static Bubble" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Healer && def.Kit == Kit.Elementalist,
            "Static Bubble is the Healer Elementalist's ultimate on key 1, 80 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Elementalist, 1) == AbilityId.StaticBubble && Upgrades.Ultimate(Role.Healer, Kit.Elementalist) == AbilityId.Sanctum,
            "key 1 is Static Bubble; Sanctum stays the default");
        Check(Upgrades.ClampUlt(Role.Healer, Kit.Elementalist, 1) == 1 && Content.UltFallsBack(2),
            "key 1 is the Healer's; a class without a key 2 falls back to the default");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Spark", Role.Healer, 0, 0, Kit.Elementalist, 0, 1);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 0, 0, Kit.Elementalist, 0, 1);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 1 && client.Lobby[1].Ult == 1);
        Check(host.Lobby[1].UltId == AbilityId.StaticBubble && client.Lobby[1].Ult == 1, "the pick reaches the host and the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.StaticBubble && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.StaticBubble,
            "in the run both players' ultimate is Static Bubble, on both games");

        // --- On the host's game -------------------------------------------------------------------------------------------
        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        g.DevSetGod(1, true);
        V2 home = new V2(600f, 450f);
        V2 allyAt = new V2(150f, 150f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; e.Pos = at; mine.Add(e.Id); return e;
        };
        Func<V2, Bullet> shot = at => (Bullet)spawnBullet.Invoke(g, new object[] { (byte)10, at, new V2(0f, 0f), 0f, 5f, 10f, 0, false, 0f });
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                ally.Pos = allyAt;
                cg.Players[cid].Pos = allyAt;  // (the client moves itself)
                host.Poll(); client.Poll();
                ally.Pos = allyAt;
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(200f, 0f) });
                cg.FixedStep(dt, new PlayerInput { AimAt = allyAt });
            }
        };
        Action reset = () =>
        {
            foreach (var id in mine) g.Enemies.Remove(id);
            mine.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.BubT = 0f; me.BubUp = false; me.RushT = 0f; ally.RushT = 0f;
            me.BubEaten = me.BubFed = me.BubBursts = me.BubFizzles = me.BubRushed = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = home; masteredField.SetValue(g, false);
            allyAt = new V2(150f, 150f);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, false });
        float dmg = me.Stats.DmgMult;

        // The cast.
        reset();
        cast();
        Check(me.BubT == Game.BubTime && me.BubR == Game.BubRadius && me.UltT == me.BubT && Game.BubOn(me), "casting: a 44 px bubble for 10 s");

        // An enemy touching it: put out at the edge, shoved, hurt, stunned, and it feeds the bubble (once per 0.6 s).
        var grunt = make(home + new V2(20f, 0f), EnemyType.Grunt);
        grunt.FrozenT = 0f;
        step(dt);
        float edge = me.Pos.DistanceTo(grunt.Pos) - grunt.Radius;
        Console.WriteLine("  staticbubble: the grunt was put out to {0:0} px (bubble {1:0}), took {2:0}, charge {3:0}", edge, me.BubR, grunt.MaxHp - grunt.Hp, me.BubCharge);
        Check(edge >= me.BubR - 0.5f && me.BubFed == 1 && me.BubCharge == Game.BubTouchFeed, "an enemy touching it is put out at its edge and feeds it 3");
        Check(Math.Abs(grunt.MaxHp - grunt.Hp - Game.BubTouchDamage * dmg) < 0.5f && grunt.StunT > 0f, "the touch hurts it (4) and stuns it");
        step(0.3f);
        Check(me.Pos.DistanceTo(grunt.Pos) - grunt.Radius > me.BubR + 20f, "it is shoved on, away from the bubble");
        grunt.Pos = home + new V2(10f, 0f);
        step(dt);
        Check(me.BubFed == 1 && me.Pos.DistanceTo(grunt.Pos) >= me.BubR, "pushed back in within 0.6 s: put out again, but no second feed yet");
        step(Game.BubFeedGap);
        grunt.Pos = home + new V2(10f, 0f);
        step(dt);
        Check(me.BubFed == 2, "after 0.6 s it feeds again");

        // Heavies are put out and shoved like everyone else.
        var heavyType = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().First(t => (int)t < 250 && Defs.Get(t).Heavy && !Defs.Get(t).IsBoss && Defs.Get(t).Speed > 0f);
        var heavy = make(home + new V2(0f, 25f), heavyType);
        step(dt);
        Check(me.Pos.DistanceTo(heavy.Pos) - heavy.Radius >= me.BubR - 0.5f && me.BubFed == 3, "a heavy is put out at the edge too, and feeds it");
        foreach (var id in mine) g.Enemies.Remove(id);
        mine.Clear();

        // Enemy shots touching it are eaten.
        float before = me.BubCharge;
        var b1 = shot(home + new V2(30f, 0f));
        var b2 = shot(home + new V2(0f, -40f));
        var far = shot(home + new V2(300f, 0f));
        step(dt);
        Check(!g.Bullets.ContainsKey(b1.Id) && !g.Bullets.ContainsKey(b2.Id) && g.Bullets.ContainsKey(far.Id) && me.BubEaten == 2 && me.BubCharge == before + 2f * Game.BubShotFeed,
            "enemy shots touching it are eaten (+1 each); one far off isn't");

        // Allies inside can't be hurt; outside they can.
        g.DevSetGod(cid, false);
        allyAt = home + new V2(15f, 0f);
        step(dt);
        float hp = ally.Hp;
        damage.Invoke(g, new object[] { ally, 30f, "Test" });
        Check(ally.Hp == hp, "an ally inside the bubble isn't hurt");
        allyAt = home + new V2(400f, 0f);
        step(dt);
        hp = ally.Hp;
        damage.Invoke(g, new object[] { ally, 30f, "Test" });
        Check(ally.Hp < hp, "outside it they are");
        ally.Hp = ally.MaxHp;
        g.DevSetGod(cid, true);

        // It grows as it fills.
        me.BubCharge = Game.BubFull * 0.5f;
        step(1f);
        Console.WriteLine("  staticbubble: half full it is {0:0} px (full: {1:0})", me.BubR, Game.BubRadiusFull);
        Check(Math.Abs(me.BubR - Game.BubSize(0.5f)) < 1f && me.BubR > Game.BubRadius + 20f, "half full it has grown toward its full size");
        Check(cg.Players[1].BubT > 0f && Math.Abs(cg.Players[1].BubR - me.BubR) < 3f && cg.Players[1].BubCharge > 0f, "the client sees the bubble, its size and charge");

        // Full: it bursts.
        var near = make(home + new V2(150f, 0f), EnemyType.Grunt);
        var out1 = make(home + new V2(350f, 0f), EnemyType.Grunt);
        var nearShot = shot(home + new V2(-160f, 0f));
        allyAt = home + new V2(0f, 140f);
        step(dt);
        me.BubCharge = Game.BubFull - 0.5f;
        shot(home + new V2(10f, 0f));
        float sp0 = me.Speed, fr0 = (float)fireRate.Invoke(g, new object[] { me });
        step(dt);
        Console.WriteLine("  staticbubble: the burst: the grunt 150 px off took {0:0}, the one 350 px off {1:0}; Static Rush {2:0.0} s on you, {3:0.0} s on the ally",
            near.MaxHp - near.Hp, out1.MaxHp - out1.Hp, me.RushT, ally.RushT);
        Check(me.BubBursts == 1 && me.BubT == 0f && !me.BubUp && me.UltT == 0f, "full, it bursts and ends");
        Check(near.MaxHp - near.Hp >= Game.BubBlastDamage * dmg - 0.5f && near.StunT > 0f && out1.Hp == out1.MaxHp, "the burst hits (70) and stuns what's in reach, not what's beyond");
        Check(!g.Bullets.ContainsKey(nearShot.Id) && g.Bullets.ContainsKey(far.Id), "the burst erases shots in reach");
        Check(Math.Abs(me.RushT - Game.StaticRushTime) < 0.05f && Math.Abs(ally.RushT - Game.StaticRushTime) < 0.05f && me.BubRushed == 2,
            "you and the ally in the blast get 6 s of Static Rush");
        float fr1 = (float)fireRate.Invoke(g, new object[] { me });
        Check(Math.Abs(me.Speed / sp0 - Game.StaticRushMove) < 0.01f && Math.Abs(fr1 / fr0 - Game.StaticRushFire) < 0.01f, "Static Rush: +40% speed, +60% fire rate");
        step(0.2f);
        Check(cg.Players[1].RushT > 0f && cg.Players[cid].RushT > 0f && cg.Effects.Any(fx => fx.Kind == FxKind.StaticBurst), "the client sees Static Rush on both and the burst");
        step(Game.StaticRushTime);
        Check(me.RushT == 0f && Math.Abs(me.Speed / sp0 - 1f) < 0.01f, "Static Rush wears off");

        // An ally outside the blast doesn't get it.
        reset();
        allyAt = home + new V2(500f, 0f);
        cast();
        me.BubCharge = Game.BubFull;
        step(dt);
        Check(me.BubBursts == 1 && me.RushT > 0f && ally.RushT == 0f, "an ally beyond the blast gets no Static Rush");

        // Time runs out first: a smaller pop, Static Rush scaled by the fill.
        reset();
        cast();
        me.BubCharge = Game.BubFull * 0.5f;
        step(Game.BubTime + 0.1f);
        Check(me.BubFizzles == 1 && me.BubBursts == 0 && !me.BubUp && Math.Abs(me.RushT - Game.StaticRushTime * 0.5f) < 0.2f,
            "time running out pops it smaller: half full, half the Static Rush");
        reset();
        cast();
        step(Game.BubTime + 0.1f);
        Check(me.BubFizzles == 1 && Math.Abs(me.RushT - Game.StaticRushTime * Game.BubFizzleMin) < 0.2f, "empty, it still gives a quarter");

        // Going down ends it quietly.
        reset();
        cast();
        step(0.2f);
        me.Downed = true;
        step(dt);
        Check(!me.BubUp && me.BubT == 0f && me.BubBursts + me.BubFizzles == 0 && me.RushT == 0f, "going down ends it with no burst");
        me.Downed = false; me.Hp = me.MaxHp;

        // Mastered: a wider burst and a longer Static Rush.
        reset();
        me.MasteryXp[AbilityId.StaticBubble] = 1e6f;
        var wide = make(home + new V2(215f, 0f), EnemyType.Grunt);
        cast();
        me.MasteryXp.Remove(AbilityId.StaticBubble);
        me.BubCharge = Game.BubFull;
        step(dt);
        Check(wide.Hp < wide.MaxHp && Math.Abs(me.RushT - Game.StaticRushTimeMastered) < 0.05f, "mastered: the burst reaches 240 px and Static Rush lasts 8 s");

        Check(def.Bot(new BotSense { Me = me, Danger = true }) && !def.Bot(new BotSense { Me = me }), "a bot casts it in danger");
        host.Leave(""); client.Leave("");
    }
}
