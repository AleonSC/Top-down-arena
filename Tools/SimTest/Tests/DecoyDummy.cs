using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Decoy Dummy (DecoyDummy.cs), the Healer Soldier's 4th choice: the table (three charges, 4 s), the pick reaching the match,
// the throw to the cursor (280 px at most), up only after it lands and puffs up, regular enemies near it drawn to it (bosses only
// for a glance), enemy shots and bites wearing it, enemies kept off it, a 4th deflating the oldest, the pop's confetti blast
// (shove and stun, bosses untouched), deflating when its time is up and when the wave ends, replicated (health left and the
// throw), mastery (more health, a wider blast) and the Double-Ply card.
static partial class Program
{
    static void DecoyDummyTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var decoyTarget = typeof(Game).GetMethod("DecoyTarget", flags);

        var def = Upgrades.Ability(AbilityId.DecoyDummy);
        Check(def.Name == "Decoy Dummy" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Soldier && def.Role == Role.Healer && def.Cooldown == 4f && def.ExtraCharges == 2,
            "Decoy Dummy: a 4 s, three-charge Healer Soldier 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Healer, Kit.Soldier) == 4 && Upgrades.FirstAbility(Role.Healer, Kit.Soldier, 3) == AbilityId.DecoyDummy
            && Upgrades.FirstAbility(Role.Healer, Kit.Soldier, 2) == AbilityId.Medkit, "the Healer Soldier's 4th key is Decoy Dummy; its first three are unchanged");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Medic", Role.Healer, 0, 3, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.DecoyDummy && me.Slots[0].MaxCharges == 3 && client.Game.Players[1].Slots[0].Id == AbilityId.DecoyDummy,
            "the 4th choice reaches the run as the 1st ability, with three charges");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(400f, 450f), aimAt = new V2(600f, 450f);
        var spots = new Dictionary<Enemy, V2>();
        Func<EnemyType, V2, bool, Enemy> make = (type, at, pinned) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f;
            if (pinned) spots[e] = at;
            return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; }
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                me.Pos = mePos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt });
            }
        };
        Action reset = () =>
        {
            g.Enemies.Clear(); spots.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear();
            me.Pos = mePos; me.Slots[0].Charges = 3; me.Slots[0].Timer = 0f;
            me.MasteryXp.Remove(AbilityId.DecoyDummy);
            make(EnemyType.Grunt, new V2(1500f, 900f), true);  // out of the way, so the wave goes on
        };
        Func<V2, Zone> cast = at =>
        {
            me.Slots[0].Charges = Math.Max(1, me.Slots[0].Charges);
            int top = g.Zones.Count == 0 ? -1 : g.Zones.Keys.Max();
            hostAbility.Invoke(g, new object[] { 1, 0, at, false });
            return g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.DecoyDummy && z.Id > top);
        };
        Func<List<Zone>> dummies = () => g.Zones.Values.Where(z => z.Kind == ZoneKind.DecoyDummy && z.T > 0f).OrderBy(z => z.Id).ToList();
        Action<V2, V2, int> volley = (from, vel, n) => { for (int i = 0; i < n; i++) shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(0f, i * 3f), vel, 0f, 6f, 10f, 0, false, 0f }); };
        Action<float> upFor = secs => step(Game.DecoyFly + Game.DecoyInflate + secs);

        // The throw: at the cursor, with this wave's health; three charges.
        reset();
        var d0 = cast(aimAt);
        float hp = Game.DecoyHealth(g.Wave, false);
        Check(d0 != null && d0.Pos.DistanceTo(aimAt) < 1f && d0.BlockHp == hp && Math.Abs(hp - (Game.DecoyHp + Game.DecoyHpPerWave * (g.Wave - 1))) < 0.01f,
            "the dummy lands at your cursor with 40 health + 4 a wave");
        Check(me.Slots[0].Charges == 2, "it spends one of three charges");
        var far = cast(mePos + new V2(0f, 900f));
        Check(far != null && Math.Abs(far.Pos.DistanceTo(mePos) - Game.DecoyRange) < 1f && Math.Abs(far.Marched - Game.DecoyRange) < 1f, "a far cursor throws it 280 px at most");
        far.T = 0f;
        step(dt);

        // Not up (no pull, no blocking) while it's in the air or puffing up.
        Check(!Game.DecoyUp(d0), "it isn't up the moment it's thrown");
        var near = make(EnemyType.Grunt, d0.Pos + new V2(150f, 0f), true);
        V2 tp = (V2)decoyTarget.Invoke(g, new object[] { near, mePos });
        Check(tp.DistanceTo(mePos) < 1f, "and enemies ignore it until it's up");
        volley(d0.Pos + new V2(60f, 0f), new V2(-600f, 0f), 1);
        step(0.12f);
        Check(d0.BlockHp == hp, "shots fly through while it's in the air");
        g.Bullets.Clear();
        step(Game.DecoyFly + Game.DecoyInflate);
        Check(Game.DecoyUp(d0), "up once it has landed and puffed up (0.7 s)");

        // The pull: regular enemies within 240 px go for it; farther ones and (after a second) bosses don't.
        tp = (V2)decoyTarget.Invoke(g, new object[] { near, mePos });
        Check(tp.DistanceTo(d0.Pos) < 1f, "a regular enemy within 240 px goes for it");
        var away = make(EnemyType.Grunt, d0.Pos + new V2(0f, 300f), true);
        tp = (V2)decoyTarget.Invoke(g, new object[] { away, mePos });
        Check(tp.DistanceTo(mePos) < 1f, "one farther than 240 px keeps to its own target");
        var boss = new Enemy { Type = EnemyType.Warden, Def = Defs.Get(EnemyType.Warden), Pos = d0.Pos + new V2(100f, 0f) };
        tp = (V2)decoyTarget.Invoke(g, new object[] { boss, mePos });
        Check(tp.DistanceTo(d0.Pos) < 1f, "a boss glances at a fresh dummy");
        step(Game.DecoyBossGlance + 0.1f);
        tp = (V2)decoyTarget.Invoke(g, new object[] { boss, mePos });
        Check(tp.DistanceTo(mePos) < 1f, "and after a second ignores it");
        g.Enemies.Remove(near.Id); g.Enemies.Remove(away.Id); spots.Remove(near); spots.Remove(away);

        // A real melee enemy drawn to it walks to it rather than you.
        var walker = make(EnemyType.Brute, d0.Pos + new V2(200f, -40f), false);
        float start = walker.Pos.DistanceTo(d0.Pos);
        step(0.8f);
        Console.WriteLine("  decoy: a Brute 200 px off ends {0:0} px away (you {1:0} px)", walker.Pos.DistanceTo(d0.Pos), walker.Pos.DistanceTo(mePos));
        Check(walker.Pos.DistanceTo(d0.Pos) < start - 20f, "an enemy near it closes in on the dummy");
        g.Enemies.Remove(walker.Id);

        // Enemy shots stop on it and wear it; your own pass.
        g.Bullets.Clear();
        float scale = g.EnemyDamageScale(Math.Max(1, g.Wave));
        volley(d0.Pos + new V2(120f, 0f), new V2(-400f, 0f), 2);
        step(0.5f);
        Check(!g.Bullets.Values.Any(b => b.Owner == 0), "enemy shots that reach it stop there");
        Console.WriteLine("  decoy: 2 shots of 10, health {0:0.0} of {1:0} (scale {2:0.00})", d0.BlockHp, hp, scale);
        Check(Math.Abs(hp - d0.BlockHp - 20f * scale) < 0.5f, "each shot wears it by what it would have hit for");
        Check(g.Effects.Any(e => e.Kind == FxKind.DecoyHit), "each hit is sent as a look");
        var cd = client.Game.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.DecoyDummy);
        Check(cd != null && Math.Abs(client.Game.DecoyFrac(cd) - d0.BlockHp / hp) < 0.01f && Math.Abs(cd.Marched - d0.Marched) < 0.5f && Math.Abs(cd.Aim - d0.Aim) < 0.01f,
            "other screens see the dummy, its throw and its health left");
        g.Bullets.Clear();
        float before = d0.BlockHp;
        shoot.Invoke(g, new object[] { Defs.TankShot, d0.Pos - new V2(40f, 0f), new V2(600f, 0f), 0f, 6f, 10f, 1, false, 0f });
        step(0.2f);
        Check(d0.BlockHp == before && g.Bullets.Values.Any(b => b.Owner == 1 && b.Pos.X > d0.Pos.X + 20f), "your team's shots pass through");
        g.Bullets.Clear();

        // A melee body bites it (every half second) and is kept outside it.
        d0.BlockHp = d0.SoakMax = 1e5f;
        var biter = make(EnemyType.Brute, d0.Pos + new V2(4f, 0f), false);
        step(1.1f);
        Console.WriteLine("  decoy: a Brute leaning on it for 1.1 s took {0:0}", 1e5f - d0.BlockHp);
        Check(d0.BlockHp < 1e5f && biter.Pos.DistanceTo(d0.Pos) >= Game.DecoyR + biter.Radius - 1f, "a melee enemy bites it and can't stand inside it");
        g.Enemies.Remove(biter.Id);

        // Three at most: a fourth deflates the oldest.
        reset();
        var a1 = cast(new V2(600f, 300f)); var a2 = cast(new V2(600f, 450f)); var a3 = cast(new V2(600f, 600f));
        Check(dummies().Count == 3, "three can stand at once");
        cast(new V2(500f, 700f));
        Check(dummies().Count == 3 && !dummies().Any(z => z.Id == a1.Id) && g.Effects.Any(e => e.Kind == FxKind.DecoyFold), "a fourth deflates the oldest");

        // The pop: a confetti blast shoves and stuns regular enemies near it; bosses aren't touched.
        reset();
        var p0 = cast(aimAt);
        upFor(0.05f);
        var grunt = make(EnemyType.Grunt, p0.Pos + new V2(50f, 0f), false);
        var outside = make(EnemyType.Grunt, p0.Pos + new V2(0f, 200f), false);
        V2 gruntFrom = grunt.Pos;
        p0.BlockHp = 5f * scale;
        g.Effects.Clear();
        volley(p0.Pos + new V2(-90f, 0f), new V2(600f, 0f), 1);
        step(0.25f);
        Check(!g.Zones.ContainsKey(p0.Id) && g.Effects.Any(e => e.Kind == FxKind.DecoyPop && Math.Abs(e.Radius - Game.DecoyBlast) < 0.1f), "at 0 health it pops, with a 90 px blast");
        Console.WriteLine("  decoy: the pop shoved a grunt {0:0} px", grunt.Pos.DistanceTo(gruntFrom));
        Check(grunt.Pos.DistanceTo(p0.Pos) > 50f + Game.DecoyShove * 0.5f && grunt.StunT > 0f, "an enemy in the blast is shoved back and stunned");
        Check(outside.StunT <= 0f, "one outside the blast isn't");

        // Its time runs out: it deflates.
        reset();
        var t0 = cast(aimAt);
        t0.BlockHp = t0.SoakMax = 1e6f;  // (the wave's own enemies come for it meanwhile)
        step(Game.DecoyFly + Game.DecoyInflate + Game.DecoyLife + 0.1f);
        Check(!g.Zones.ContainsKey(t0.Id) && g.Effects.Any(e => e.Kind == FxKind.DecoyFold), "after 12 s up it deflates");

        // Mastered: 50% more health and a 130 px blast.
        reset();
        me.MasteryXp[AbilityId.DecoyDummy] = 1e9f;
        var m0 = cast(aimAt);
        Check(m0 != null && Math.Abs(m0.BlockHp - hp * Game.DecoyHpMastered) < 0.01f && m0.Blast == Game.DecoyBlastMastered, "mastered: 50% more health and a 130 px blast");
        me.MasteryXp.Remove(AbilityId.DecoyDummy);

        // Double-Ply (Rare, two stacks): 50% more health each.
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Double-Ply");
        Check(card != null && card.Rarity == Rarity.Rare && card.RequiresAbility == AbilityId.DecoyDummy && card.MaxStacks == 2, "Double-Ply: a Rare card for Decoy Dummy, two stacks");
        reset();
        var st = new Stats();
        card.Apply(st); card.Apply(st);
        me.Stats.DecoyHpMult = st.DecoyHpMult;
        var c0 = cast(aimAt);
        Check(Math.Abs(c0.BlockHp - hp * 2f) < 0.5f, "two stacks double its health");
        me.Stats.DecoyHpMult = 1f;

        // The fight ends: they deflate.
        cast(new V2(600f, 300f));
        step(0.5f);
        int standing = dummies().Count;
        Check(standing == 2, "(dummies standing before the wave ends)");
        g.Effects.Clear();
        typeof(Game).GetMethod("WaveCleared", flags).Invoke(g, null);
        Check(dummies().Count == 0 && g.Effects.Count(e => e.Kind == FxKind.DecoyFold) == standing, "they deflate when the wave is cleared");

        host.Leave(""); client.Leave("");
    }
}
