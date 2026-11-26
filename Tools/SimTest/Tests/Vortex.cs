using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Vortex (Vortex.cs): key 3 of the Tank Elementalist's Ultimate row reaching the run and a save; the wind-up (nothing moved or
// hurt); the swirl and pull (never into the eye), the fringe drawn in, heavies and far enemies left alone; 45 damage a second
// inside; enemy shots swept away; the fling when it ends on time and when you go down; mastered; the bot's rule; other screens
// seeing it.
static partial class Program
{
    static void VortexTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 3 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.Vortex);
        Check(def.Name == "Vortex" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Tank && def.Kit == Kit.Elementalist,
            "Vortex is the Tank Elementalist's ultimate on key 3, 80 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Elementalist, 3) == AbilityId.Vortex && Upgrades.Ultimate(Role.Tank, Kit.Elementalist) == AbilityId.GlacialTomb,
            "key 3 is Vortex; Glacial Tomb stays the default");
        Check(Upgrades.ClampUlt(Role.Tank, Kit.Elementalist, 3) == 3 && Upgrades.Ultimate(Role.Dps, Kit.Elementalist, 3) != AbilityId.Vortex && Upgrades.Ultimate(Role.Healer, Kit.Elementalist, 3) != AbilityId.Vortex && Content.UltFallsBack(3),
            "key 3 is only the tank's; a role and class without a key 3 falls back to its default");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Storm", Role.Tank, 0, 0, Kit.Elementalist, 0, 3);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Tank, 0, 0, Kit.Elementalist, 0, 3);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 3 && client.Lobby[1].Ult == 3);
        Check(host.Lobby[1].Ult == 3 && host.Lobby[cid].Ult == 3 && client.Lobby[1].Ult == 3 && host.Lobby[1].UltId == AbilityId.Vortex, "the pick reaches the host and the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.Vortex && client.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.Vortex,
            "in the run both players' ultimate is Vortex, on both games");
        var save = new RunSave { Wave = 4 };
        save.Seats.Add(new SeatSave { Name = "Storm", Role = Role.Tank, Kit = Kit.Elementalist, Ult = 3 });
        var bare = Content.RoleWithoutUlt(3);  // (a role and class with no key 3)
        save.Seats.Add(new SeatSave { Name = "Bare", Role = bare.Role, Kit = bare.Kit, Ult = 3 });
        var back = RunSave.Parse(save.ToText());
        Check(back.Seats[0].Ult == 3 && back.Seats[1].Ult == 0, "a saved run keeps key 3 (clamped for a class without it)");

        // --- The storm on the host's game -------------------------------------------------------------------------------
        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        g.DevSetGod(1, true);
        V2 mePos = new V2(700f, 450f);
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
                me.Pos = mePos; ally.Pos = new V2(150f, 150f);
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
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.VortexT = 0f; me.VortexUp = false; me.VortexHits = me.VortexSwept = me.VortexFlung = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            keepShots = false;
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        float dmg = me.Stats.DmgMult;
        Func<Enemy, float> dist = e => e.Pos.DistanceTo(me.Pos);
        Func<Enemy, float> ang = e => (e.Pos - me.Pos).Angle;

        // The wind-up: nothing moves or is hurt yet.
        reset();
        var inner = make(mePos + new V2(150f, 0f), EnemyType.Grunt);
        var fringe = make(mePos + new V2(-(Game.VortexRadius + 40f), 0f), EnemyType.Grunt);
        var far = make(mePos + new V2(0f, Game.VortexRadius + 200f), EnemyType.Grunt);
        cast();
        Check(Math.Abs(me.VortexT - (Game.VortexWindup + Game.VortexTime)) < 1e-3f && me.UltT == me.VortexT && me.VortexR == Game.VortexRadius,
            "casting: 0.4 s wind-up + 5 s, 240 px");
        step(0.3f);
        Check(inner.Hp == inner.MaxHp && Math.Abs(dist(inner) - 150f) < 0.5f && Math.Abs(ang(inner)) < 1e-3f, "the wind-up moves and hurts nothing");

        // The swirl, the pull, the fringe.
        V2 farAt = far.Pos;
        step(1.1f);
        float d1 = dist(inner), turned = ang(inner);
        Console.WriteLine("  vortex: after 0.8 s of storm the grunt 150 px out is {0:0} px out, {1:0.00} rad round; the one 40 px past the edge is {2:0} px out",
            d1, turned, dist(fringe));
        Check(Math.Abs(turned) > 0.5f && d1 < 150f && d1 >= Game.VortexEye + inner.Radius - 0.5f, "an enemy inside is swept round and pulled in");
        Check(dist(fringe) < Game.VortexRadius + 40f - 10f, "one just outside the edge is drawn in");
        Check(far.Pos.DistanceTo(farAt) < 0.5f && far.Hp == far.MaxHp, "one far outside is left alone");
        Check(client.Game.Players[1].VortexT > 0f && client.Game.Players[1].VortexR == Game.VortexRadius && client.Game.Players[1].VortexAge > Game.VortexWindup,
            "other screens see the storm");
        step(2f);
        Check(dist(inner) >= Game.VortexEye + inner.Radius - 0.5f, "never pulled into the calm eye");

        // The damage: 9 every 0.2 s, at full power.
        float hp0 = inner.Hp;
        step(1f);
        float perSec = hp0 - inner.Hp;
        Console.WriteLine("  vortex: {0:0} damage a second inside (damage x{1:0.00})", perSec, dmg);
        Check(Math.Abs(perSec - Game.VortexTickDamage * 5f * dmg) <= Game.VortexTickDamage * dmg + 0.5f, "about 45 damage a second to everything inside");

        // A heavy is hurt but not moved.
        reset();
        var heavyType = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().First(t => (int)t < 250 && Defs.Get(t).Heavy && !Defs.Get(t).IsBoss && Defs.Get(t).Speed > 0f);
        var heavy = make(mePos + new V2(0f, 140f), heavyType);
        V2 heavyAt = heavy.Pos;
        cast();
        step(1.5f);
        Check(heavy.Pos.DistanceTo(heavyAt) < 0.5f && heavy.Hp < heavy.MaxHp, "a heavy (" + heavyType + ") stays put but is hurt");

        // Enemy shots inside are swept away; ones outside fly on.
        keepShots = true;
        var shotIn = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, mePos + new V2(120f, 0f), new V2(-200f, 0f), 0f, 3f, 5f, 0, false, 0f });
        var shotOut = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, mePos + new V2(0f, -(Game.VortexRadius + 200f)), new V2(200f, 0f), 0f, 3f, 5f, 0, false, 0f });
        step(dt);
        Check(!g.Bullets.ContainsKey(shotIn.Id) && g.Bullets.ContainsKey(shotOut.Id) && me.VortexSwept == 1, "an enemy shot inside is swept away, one outside flies on");
        Check(g.Effects.Any(e => e.Kind == FxKind.VortexWisp), "with a wisp of wind");
        keepShots = false;

        // The fling when it ends on time.
        reset();
        var ring = new List<Enemy>();
        for (int i = 0; i < 8; i++)
            ring.Add(make(mePos + V2.FromAngle(MathX.Tau * i / 8f) * 130f, EnemyType.Grunt));
        cast();
        step(1f);
        float before = ring.Average(e => dist(e));
        var hpBefore = ring.Select(e => e.Hp).ToList();
        me.VortexT = 0.02f;
        step(0.05f);
        Check(me.VortexT == 0f && me.UltT == 0f && !Game.VortexOn(me) && !me.VortexUp, "it ends on time");
        step(0.4f);
        float after = ring.Average(e => dist(e));
        Console.WriteLine("  vortex: the fling threw the ring from {0:0} px out to {1:0} px", before, after);
        Check(after - before > Game.VortexFling * 0.8f, "everyone inside is flung about 160 px out");
        Check(me.VortexFlung == 8 && ring.Select((e, i) => hpBefore[i] - e.Hp).All(x => x >= Game.VortexFlingDamage * dmg - 0.5f), "and takes 30");

        // Going down ends it.
        reset();
        cast();
        step(1f);
        me.Downed = true;
        step(dt);
        Check(me.VortexT == 0f && !me.VortexUp, "going down ends it at once");
        me.Downed = false; me.Hp = me.MaxHp;

        // Mastered.
        reset();
        me.MasteryXp[AbilityId.Vortex] = 1e6f;
        var wide = make(mePos + new V2(270f, 0f), EnemyType.Grunt);
        wide.FrozenT = 999f;
        cast();
        Check(me.VortexR == Game.VortexRadiusMastered, "mastered: 300 px");
        step(0.4f + 0.4f + 0.05f);
        hp0 = wide.Hp;
        step(1f);
        Check(Math.Abs((hp0 - wide.Hp) - Game.VortexTickDamageMastered * 5f * dmg) <= Game.VortexTickDamageMastered * dmg + 0.5f, "mastered: about 55 a second");
        me.MasteryXp.Remove(AbilityId.Vortex);
        reset();

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, Crowd = Game.JuggBotCrowd }) && !def.Bot(new BotSense { Me = me, Crowd = 1 }), "a bot casts it in a crowd");

        host.Leave(""); client.Leave("");
    }
}
