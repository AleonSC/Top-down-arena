using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Lightning Rod (LightningRod.cs), the Tank Elementalist's second ultimate, and the lobby's ultimate choice (LobbyEntry.Ult):
// the choice reaching the host, the other player and the match (and clamped where there's no second), saved runs keeping it;
// then the 80% cut, the taunt, a bolt per hit on the shooter and its 2 arcs, the per-enemy cap, a dead shooter's bolt, wet
// arcs, grounding in the hit grace, contact hits, the end on time and on going down, the mastered capstone, and replication.
static partial class Program
{
    static void RodTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var damage = typeof(Game).GetMethod("Damage", flags, null, new[] { typeof(Player), typeof(float), typeof(string) }, null);
        var pickTarget = typeof(Game).GetMethod("PickTarget", flags);
        var acting = typeof(Game).GetField("actingEnemy", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby choice --------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.LightningRod);
        Check(def.Name == "Lightning Rod" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 1
            && Upgrades.Ultimate(Role.Tank, Kit.Elementalist) == AbilityId.GlacialTomb && Upgrades.Ultimate(Role.Tank, Kit.Elementalist, 0) == AbilityId.GlacialTomb
            && Upgrades.Ultimate(Role.Tank, Kit.Elementalist, 1) == AbilityId.LightningRod, "Lightning Rod is the tank Elementalist's second ultimate (70 s); Glacial Tomb stays the default");
        Check(Upgrades.ClampUlt(Role.Tank, Kit.Elementalist, 1) == 1 && Upgrades.ClampUlt(Role.Dps, Kit.Elementalist, 1) == 0 && Upgrades.ClampUlt(Role.Wildcard, Kit.Elementalist, 1) == 0
            && Upgrades.Ultimate(Role.Dps, Kit.Elementalist, 1) == AbilityId.BulletRain, "key 1 is the tank Elementalist's; a DPS Elementalist (and a Wildcard) fall back to their default");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };  // (3: it never starts on its own with two, but StartMatch needs no Ready)
        host.Host(port, "Rod", Role.Tank, 0, 0, Kit.Elementalist, 0, 1);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0, Kit.Elementalist, 0, 1);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        LobbyEntry hostMe = host.Lobby[1], friend = host.Lobby.Values.First(e => e.Id != 1);
        Check(hostMe.Ult == 1 && friend.Ult == 0, "the host keeps its pick; a DPS Elementalist asking for a second ultimate is clamped to its default");
        Check(client.Lobby[1].Ult == 1, "the other player's lobby sees the host's pick");
        client.RequestRole(Role.Tank);
        Run(s, dt, 2, () => host.Lobby[friend.Id].Role == Role.Tank);
        client.RequestUlt(1);
        Run(s, dt, 2, () => host.Lobby[friend.Id].Ult == 1 && client.Lobby[friend.Id].Ult == 1);
        Check(host.Lobby[friend.Id].Ult == 1 && client.Lobby[friend.Id].Ult == 1, "a player's pick reaches the host and comes back to everyone");
        client.RequestKit(Kit.Soldier);
        Run(s, dt, 2, () => host.Lobby[friend.Id].Kit == Kit.Soldier);
        Check(host.Lobby[friend.Id].Ult == 0, "changing class resets the pick");
        client.RequestKit(Kit.Elementalist);
        Run(s, dt, 2, () => host.Lobby[friend.Id].Kit == Kit.Elementalist);
        client.RequestUlt(1);
        Run(s, dt, 2, () => host.Lobby[friend.Id].Ult == 1);
        client.RequestUlt(7);
        Run(s, dt, 1, () => false);
        Check(host.Lobby[friend.Id].Ult == 0, "a pick past what the class has is clamped to the default");
        client.RequestUlt(1);
        Run(s, dt, 2, () => host.Lobby[friend.Id].Ult == 1);

        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1]; var ally = g.Players[friend.Id];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.LightningRod && ally.Slots[Upgrades.UltSlot].Id == AbilityId.LightningRod
            && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.LightningRod && client.Game.Players[friend.Id].Slots[Upgrades.UltSlot].Id == AbilityId.LightningRod,
            "the picks reach the match: both players' F key is Lightning Rod, on both screens");

        // A saved run keeps it.
        var save = new RunSave { Wave = 4 };
        save.Seats.Add(new SeatSave { Name = "Rod", Role = Role.Tank, Kit = Kit.Elementalist, Ult = 1 });
        save.Seats.Add(new SeatSave { Name = "Old", Role = Role.Dps, Kit = Kit.Elementalist, Ult = 1 });
        var back = RunSave.Parse(save.ToText());
        Check(back.Seats[0].Ult == 1 && back.Seats[1].Ult == 0, "a saved run keeps each seat's ultimate (clamped to what the seat's class has)");
        var oldLine = RunSave.Parse(save.ToText().Replace("Tank Elementalist 0 0 0 1", "Tank Elementalist 0 0 0"));
        Check(oldLine.Seats[0].Ult == 0, "a save from before the choice gets the default");

        // --- The ultimate -------------------------------------------------------------------------------------------
        g.DevSetGod(friend.Id, true);
        V2 mePos = new V2(500f, 450f);
        var mine = new HashSet<int>();
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.CageT = 999f; mine.Add(e.Id); return e;  // caged: no moving or shooting of their own
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < Math.Max(1, (int)Math.Round(secs / dt)); f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                me.Pos = mePos; ally.Pos = new V2(200f, 800f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.RodT = 0f; me.InvulnT = 0f; me.TauntT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Pos = mePos; masteredField.SetValue(g, false);
        };
        Func<Enemy, Bullet> fire = e =>  // e fires a small shot that lands on you next tick
        {
            acting.SetValue(g, e);
            V2 from = mePos + new V2(6f, 0f);  // (already on you: it lands on the next tick)
            var b = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, from, new V2(-300f, 0f), 0f, 3f, Defs.BulletDamage(Defs.SmallShot), 0, false, 0f });
            acting.SetValue(g, null);
            return b;
        };
        Func<float, float> hpLoss = amount => { me.Hp = me.MaxHp; me.InvulnT = 0f; damage.Invoke(g, new object[] { me, amount, "test" }); return me.MaxHp - me.Hp; };
        Func<int> bolts = () => g.Effects.Count(x => x.Kind == FxKind.RodBolt);

        reset();
        float normal = hpLoss(50f);
        var shooter = make(EnemyType.Grunt, new V2(900f, 450f));
        var near1 = make(EnemyType.Grunt, new V2(980f, 450f));
        var near2 = make(EnemyType.Grunt, new V2(900f, 560f));
        var far = make(EnemyType.Grunt, new V2(900f, 450f + Game.RodArcReach + 200f));  // out of the arcs' reach
        var boss = make(EnemyType.Boss, new V2(1400f, 300f));
        foreach (var e in g.Enemies.Values) e.TargetId = friend.Id;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        Check(me.RodT == Game.RodTime && me.UltT == Game.RodTime && me.RodArcs == Game.RodArcCount, "casting charges you for 6 s");
        Check(g.Enemies.Values.All(e => e.TargetId == 1) && (int)pickTarget.Invoke(g, null) == 1 && boss.TargetId == 1, "every enemy, the boss too, turns on you (the taunt path)");
        float cut = hpLoss(50f);
        Console.WriteLine("  rod: a 50 hit takes {0:0.0} -> {1:0.0}", normal, cut);
        Check(Math.Abs(cut - normal * (1f - Game.RodCut)) < 0.05f, "you take 80% less damage while it's up");

        // A bolt per hit, on the shooter, then 2 arcs to the nearest.
        me.InvulnT = 0f; g.Effects.Clear();
        float[] h0 = { shooter.Hp, near1.Hp, near2.Hp, far.Hp };
        float hp0 = me.Hp;
        fire(shooter);
        step(dt * 2);
        float bolt = Game.RodBoltDamage * me.Stats.DmgMult, arc = Game.RodArcDamage * me.Stats.DmgMult;
        float[] took = { h0[0] - shooter.Hp, h0[1] - near1.Hp, h0[2] - near2.Hp, h0[3] - far.Hp };
        Console.WriteLine("  rod: bolt {0:0.0} arcs {1:0.0} {2:0.0} far {3:0.0} (expect {4:0.0} / {5:0.0}); you took {6:0.0}", took[0], took[1], took[2], took[3], bolt, arc, hp0 - me.Hp);
        Check(bolts() == 1 && g.Effects.Any(x => x.Kind == FxKind.RodBolt && x.Pos.DistanceTo(shooter.Pos) < 1f), "the hit calls one bolt, on the enemy that fired it");
        Check(Math.Abs(took[0] - bolt) < 0.5f && Math.Abs(took[1] - arc) < 0.5f && Math.Abs(took[2] - arc) < 0.5f && took[3] < 0.01f,
            "the bolt does 30 (by your damage) and arcs on to the 2 nearest for 15 each, not further");
        Check(g.Effects.Count(x => x.Kind == FxKind.RodArc) == 2 && g.Effects.Any(x => x.Kind == FxKind.RodGround && (int)x.Pos2.X == 1 && x.Pos2.Y > 0f), "two arcs drawn, and the grey number for what you took");

        // The cap: a volley of five from one enemy calls one bolt; 0.25 s later it can call another.
        step(0.3f);
        me.InvulnT = 0f; g.Effects.Clear();
        for (int i = 0; i < 5; i++) fire(shooter);
        step(dt * 2);
        Check(bolts() == 1, "five pellets at once from one enemy call one bolt (one per enemy every 0.25 s)");
        step(0.1f);
        me.InvulnT = 0f;
        fire(shooter);
        step(dt * 2);
        Check(bolts() == 1, "and none again before 0.25 s");
        step(0.25f);
        me.InvulnT = 0f; g.Effects.Clear();
        fire(shooter);
        step(dt * 2);
        Check(bolts() == 1, "after 0.25 s it can call the next");

        // Grounded in the hit grace: no damage, but the bolt still falls.
        step(0.3f);
        g.Effects.Clear();
        me.InvulnT = 1f; me.Hp = me.MaxHp * 0.5f;
        float graceHp = me.Hp;
        int bullets = g.Bullets.Count;
        fire(near1);
        step(dt * 2);
        Check(bolts() == 1 && me.Hp >= graceHp && g.Bullets.Count == bullets, "a shot in the hit grace is grounded: used up, no damage, and its bolt falls");

        // A dead shooter: the bolt still lands where it stood, and arcs from there.
        step(0.3f);
        me.InvulnT = 0f; g.Effects.Clear();
        var ghost = make(EnemyType.Grunt, new V2(1000f, 520f));
        V2 stood = ghost.Pos;
        fire(ghost);
        g.Enemies.Remove(ghost.Id); mine.Remove(ghost.Id);
        float n1 = near1.Hp;
        step(dt * 2);
        Check(g.Effects.Any(x => x.Kind == FxKind.RodBolt && x.Pos.DistanceTo(stood) < 1f && x.Radius == 0f) && n1 - near1.Hp > arc * 0.9f,
            "if the shooter has died the bolt lands where it stood and still arcs");

        // Wet enemies take the usual extra arcs (Tsunami.cs).
        step(0.3f);
        me.InvulnT = 0f; g.Effects.Clear();
        far.WetT = 3f; far.WetArcCd = 0f;
        float wf = far.Hp;
        fire(shooter);
        step(dt * 2);
        Console.WriteLine("  rod: a wet enemy out of reach took {0:0.0} (expect {1:0.0})", wf - far.Hp, bolt * Game.WetArcShare);
        Check(Math.Abs(wf - far.Hp - bolt * Game.WetArcShare) < 0.5f, "the bolt counts as lightning: a wet enemy anywhere takes its extra arc");
        far.WetT = 0f;

        // A contact hit calls a bolt on the enemy that hit.
        step(0.3f);
        me.InvulnT = 0f; g.Effects.Clear();
        var brute = make(EnemyType.Brute, mePos + new V2(12f, 0f));
        brute.CageT = 0f;
        step(dt);
        Check(g.Effects.Any(x => x.Kind == FxKind.RodBolt && x.Radius > 0f && x.Pos.DistanceTo(brute.Pos) < 40f), "a contact hit calls a bolt on the enemy that touched you");
        g.Enemies.Remove(brute.Id); mine.Remove(brute.Id);

        // Replicated, and it ends on time; the enemies pick their own targets again.
        Check(client.Game.Players[1].RodT > 0f, "the other screen sees the charge");
        step(Game.RodTime);
        Check(me.RodT <= 0f && me.UltT <= 0f && Math.Abs(hpLoss(50f) - normal) < 0.05f, "it ends after 6 s, and damage is back to normal");
        Check(g.Enemies.Values.All(e => e.RetargetT <= 3.01f), "the enemies are let go to pick their own targets");
        g.Effects.Clear();
        me.InvulnT = 0f;
        fire(shooter);
        step(dt * 2);
        Check(bolts() == 0, "no bolts once it's over");

        // Going down ends it at once.
        reset();
        shooter = make(EnemyType.Grunt, new V2(900f, 450f));
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        me.Hp = 1f; me.InvulnT = 0f;
        damage.Invoke(g, new object[] { me, 500f, "test" });
        step(dt);
        Check(me.Downed && me.RodT <= 0f, "it ends early if you go down");

        // Mastered: 8 s and 3 arcs.
        reset();
        shooter = make(EnemyType.Grunt, new V2(900f, 450f));
        foreach (var at in new[] { new V2(980f, 450f), new V2(900f, 540f), new V2(820f, 450f) }) make(EnemyType.Grunt, at);
        me.MasteryXp[AbilityId.LightningRod] = 1e6f;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        Check(me.RodT == Game.RodTimeMastered && me.RodArcs == Game.RodArcCountMastered, "mastered it lasts 8 s and arcs to 3");
        g.Effects.Clear(); me.InvulnT = 0f;
        fire(shooter);
        step(dt * 2);
        Check(g.Effects.Count(x => x.Kind == FxKind.RodArc) == 3, "a mastered bolt arcs to 3 enemies");
        me.MasteryXp.Remove(AbilityId.LightningRod);
        reset();
        host.Leave(""); client.Leave("");

        // A bot that has it casts it when several enemies are shooting at it.
        var rule = def.Bot;
        Check(rule != null && !rule(new BotSense { Shooters = 1 }) && rule(new BotSense { Shooters = Game.RodBotShooters }), "a bot casts it when several enemies are shooting at it");
    }
}
