using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Warhorn Volley (WarhornVolley.cs): the DPS Knight's 4th lobby ability (pick 3 reaching the match), the arrows' front
// crossing the lane, damage, burning and the pin once per enemy, what it misses, shots knocked down (red aimed ones fly on),
// bosses not pinned, the mastered wider lane and longer fire.
static partial class Program
{
    static void WarhornVolleyTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        Check(Upgrades.AbilityLimit(Role.Dps, Kit.Knight) == 4 && Upgrades.RoleAbility(Role.Dps, Kit.Knight, 3) == AbilityId.WarhornVolley
            && Upgrades.AbilityLimit(Role.Tank, Kit.Knight) == 3 && Upgrades.AbilityLimit(Role.Healer, Kit.Knight) == 3,
            "the DPS Knight has a 4th ability key, Warhorn Volley; the Tank and Healer Knights keep three");
        var def = Upgrades.Ability(AbilityId.WarhornVolley);
        Check(def.Name == "Warhorn Volley" && def.Cooldown == 14f && def.Kit == Kit.Knight && def.Role == Role.Dps, "Warhorn Volley: DPS Knight, 14 s");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Knight", Role.Dps, 0, 3, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.RoleAbility == AbilityId.WarhornVolley && me.Slots[0].Id == AbilityId.WarhornVolley, "picking the 4th key brings Warhorn Volley into the match on Space");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; spots[e] = at; return e;
        };
        V2 mePos = new V2(200f, 450f), aim = new V2(900f, 450f);
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = mePos; me.Hp = me.MaxHp; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aim }); } };
        me.Pos = mePos; me.Aim = 0f;
        g.Players[2].Pos = new V2(200f, 900f);

        var near = pin(new V2(500f, 450f), EnemyType.Brute);
        var far = pin(new V2(1350f, 520f), EnemyType.Brute);
        var behind = pin(new V2(100f, 450f), EnemyType.Brute);       // 100 px behind: the front starts 150 px behind you
        var wideOut = pin(new V2(700f, 450f + 420f), EnemyType.Brute);  // 420 px off the aim: outside the 350 px half-width
        var tooFar = pin(new V2(200f + 1600f, 450f), EnemyType.Brute);  // past the 1,400 px end
        masteredField.SetValue(g, false);
        cast.Invoke(g, new object[] { me, 0, aim });
        var fx = g.Effects.FirstOrDefault(e => e.Kind == FxKind.WarhornVolley);
        Check(fx != null && (int)fx.Radius == me.Id && fx.Color.A < 0.75f && Math.Abs(fx.Dur - Game.VolFxDur) < 0.01f, "casting it sends the look to every screen (one effect, the knight's id, not mastered)");
        Check(g.Sounds.Any(c => c.Sfx == Sfx.Warhorn), "the horn sounds (Sfx.Warhorn, sent with the cast)");

        step(Game.VolLandT - 0.1f);
        Check(near.Hp == near.MaxHp && behind.Hp == behind.MaxHp, "nothing is hit while the horn is still blowing");
        step(0.25f);
        Check(behind.Hp < behind.MaxHp && near.Hp == near.MaxHp, "the first arrows land behind you, then the front moves out along the aim");
        step(Game.VolEndT + 0.5f);
        float dmg = Game.VolDamage * me.Stats.DmgMult;
        Console.WriteLine("  volley: near {0:0.0}, far {1:0.0}, behind {2:0.0}, wide {3:0.0}, too far {4:0.0}", near.MaxHp - near.Hp, far.MaxHp - far.Hp, behind.MaxHp - behind.Hp, wideOut.MaxHp - wideOut.Hp, tooFar.MaxHp - tooFar.Hp);
        float burnMax = Game.VolBurnDps * me.Stats.DmgMult * Game.VolBurnTime;
        Func<Enemy, float> taken = e => e.MaxHp - e.Hp;
        Check(taken(near) >= dmg - 0.5f && taken(near) <= dmg + burnMax + 0.5f && taken(far) >= dmg - 0.5f, "enemies in the lane take 36 once, near or 1,150 px out (plus their burning)");
        Check(wideOut.Hp == wideOut.MaxHp && tooFar.Hp == tooFar.MaxHp, "what the lane misses (off to the side, past its end) is untouched");

        // The fire and the pin, measured right after a hit.
        g.Enemies.Clear(); spots.Clear(); g.Effects.Clear();
        var hit = pin(new V2(400f, 450f), EnemyType.Brute);
        me.Slots[0].Timer = 0f; me.Slots[0].Charges = 1;
        cast.Invoke(g, new object[] { me, 0, aim });
        for (int f = 0; f < 3f / dt && hit.Hp == hit.MaxHp; f++) step(dt);
        Check(hit.BurnT > Game.VolBurnTime - 0.1f && Math.Abs(hit.BurnDps - Game.VolBurnDps * me.Stats.DmgMult) < 0.01f && hit.BurnBy == me.Id, "every enemy it hits burns, 8 a second for 4 s, credited to the knight");
        Check(Math.Abs(hit.SlowMult - (1f - Game.VolPin)) < 0.01f && hit.SlowT > Game.VolPinTime - 0.1f, "...and is pinned: 40% slower for 2 s");
        float before = hit.Hp;
        step(1f);
        Check(hit.Hp < before - Game.VolBurnDps * me.Stats.DmgMult * 0.8f, "the fire burns it down over time");
        step(Game.VolEndT);

        // Shots in the front's path are knocked out of the air; red aimed (piercing) shots fly on.
        g.Enemies.Clear(); spots.Clear(); g.Effects.Clear(); g.Bullets.Clear();
        me.Slots[0].Timer = 0f; me.Slots[0].Charges = 1;
        cast.Invoke(g, new object[] { me, 0, aim });
        step(Game.VolLandT + 0.2f);
        var small = new List<Bullet>(); var red = new List<Bullet>();
        for (int i = 0; i < 6; i++)
        {
            small.Add((Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, new V2(800f, 300f + i * 50f), new V2(0f, 0.01f), 0f, 6f, 5f, 0, false, 0f }));
            red.Add((Bullet)shoot.Invoke(g, new object[] { Defs.PierceShot, new V2(820f, 300f + i * 50f), new V2(0f, 0.01f), 0f, 6f, 5f, 0, false, 0f }));
        }
        step(1.4f);
        Check(small.All(b => !g.Bullets.ContainsKey(b.Id)), "ordinary enemy shots the front passes over are knocked down");
        Check(red.All(b => g.Bullets.ContainsKey(b.Id)), "red aimed shots fly on");
        step(Game.VolEndT);

        // Bosses burn but aren't pinned.
        g.Enemies.Clear(); spots.Clear(); g.Effects.Clear(); g.Bullets.Clear();
        var boss = pin(new V2(600f, 450f), EnemyType.Brute);
        boss.Def = Defs.Get(EnemyType.Colossus);
        me.Slots[0].Timer = 0f; me.Slots[0].Charges = 1;
        cast.Invoke(g, new object[] { me, 0, aim });
        for (int f = 0; f < 3f / dt && boss.BurnT <= 0f; f++) step(dt);
        Check(boss.BurnT > 0f && boss.SlowT <= 0f, "a boss is set burning but not pinned");
        step(Game.VolEndT);

        // Mastered: a wider lane and longer fire.
        g.Enemies.Clear(); spots.Clear(); g.Effects.Clear();
        var edge = pin(new V2(700f, 450f + 420f), EnemyType.Brute);
        me.Slots[0].Timer = 0f; me.Slots[0].Charges = 1;
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, 0, aim });
        masteredField.SetValue(g, false);
        Check(g.Effects.Any(e => e.Kind == FxKind.WarhornVolley && e.Color.A > 0.75f), "mastered, the look knows (the wider lane)");
        for (int f = 0; f < 3f / dt && edge.Hp == edge.MaxHp; f++) step(dt);
        Check(edge.Hp < edge.MaxHp && edge.BurnT > Game.VolBurnTimeMastered - 0.2f, "mastered, the lane is 40% wider and the fire burns 8 s");
        step(Game.VolEndT);
    }
}
