using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Consecrate's Holy Cross (HolyCross.cs): the flight, heal and Swiftness for allies, damage, slow and knockback for enemies,
// once each, what it misses, and the mastered three-way fan.
static partial class Program
{
    static void HolyCrossTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1]; var ally = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Slots[0].Id == AbilityId.Consecrate, "the Healer Knight's first ability is Consecrate");
        g.Enemies.Clear(); g.Bullets.Clear();
        Func<V2, Enemy> make = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; };
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> pin = at => { var e = make(at); spots[e] = at; return e; };
        V2 mePos = new V2(200f, 450f), allyPos = new V2(520f, 450f), offPos = new V2(520f, 900f);
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = mePos; ally.Pos = allyPos; me.Hp = me.MaxHp; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); } };
        me.Pos = mePos; ally.Pos = allyPos; me.Aim = 0f;
        ally.Hp = ally.MaxHp * 0.3f;
        var near = pin(new V2(560f, 450f)); var far = pin(new V2(760f, 520f)); var off = pin(offPos); var behind = pin(new V2(100f, 450f));
        var shoved = make(new V2(480f, 420f));  // not pinned, so its knockback shows
        masteredField.SetValue(g, false);
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        bool fx = g.Effects.Any(e => e.Kind == FxKind.HolyCross);
        float hp0 = ally.Hp;
        float lateStart = 0f;
        for (int f = 0; f < 3f / dt && shoved.Hp == shoved.MaxHp; f++) { step(dt); lateStart += dt; }
        bool knocked = shoved.KnockSpeed > 0f || shoved.KnockLeft.Length > 1f;
        step(2.5f);
        float nearHit = near.MaxHp - near.Hp, farHit = far.MaxHp - far.Hp;
        Console.WriteLine("  holy cross: ally {0:0.0} -> {1:0.0} (swift {2:0.0}), near {3:0.0}, far {4:0.0}, off {5:0.0}, behind {6:0.0}", hp0, ally.Hp, ally.SwiftT, nearHit, farHit, off.MaxHp - off.Hp, behind.MaxHp - behind.Hp);
        Check(fx, "casting Consecrate broadcasts the cross");
        Check(Math.Abs(ally.Hp - Math.Min(ally.MaxHp, hp0 + Game.HolyHeal)) < 1f, "an ally the cross crosses is healed 35, once");
        Check(ally.SwiftT > 0f && ally.SwiftT <= Game.HolySwiftTime, "...and gets Swiftness for 4 s");
        Check(Math.Abs(ally.Speed / (ally.Def.Speed * ally.Stats.SpeedMult * Defs.KitSpeedMult(ally.Kit)) - Game.HolySwiftMult) < 0.01f, "Swiftness is +30% speed");
        Check(me.SwiftT == 0f, "the caster isn't blessed by their own cross");
        Check(Math.Abs(nearHit - Game.HolyDamage * me.Stats.DmgMult) < 0.5f && Math.Abs(farHit - nearHit) < 0.5f, "enemies it crosses take 25, once each, near or far");
        Check(near.SlowT > 0f || near.Hp < near.MaxHp, "enemies are hit");
        Check(off.Hp == off.MaxHp && behind.Hp == behind.MaxHp, "what the cross misses (off the line, or behind you) is untouched");
        Check(knocked, "an enemy it hits is knocked back");

        // The slow, measured right after a hit.
        g.Enemies.Clear(); spots.Clear(); g.Effects.Clear();
        var slowed = pin(new V2(560f, 450f));
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        for (int f = 0; f < 2f / dt && slowed.Hp == slowed.MaxHp; f++) step(dt);
        Check(slowed.Hp < slowed.MaxHp && Math.Abs(slowed.SlowMult - (1f - Game.HolySlow)) < 0.01f && slowed.SlowT > 2f, "a hit enemy is slowed 40% for 3 s");
        step(4f);
        Check(slowed.SlowMult == 1f, "...and the slow wears off");

        // It grows: a target 150 px off the line is missed early and caught late.
        g.Enemies.Clear(); spots.Clear();
        ally.Hp = ally.MaxHp; ally.SwiftT = 0f;
        var side = pin(new V2(260f, 600f));   // 150 px off the line, near me: the cross is still small there
        var lateSide = pin(new V2(780f, 600f));  // the same distance off, far out where the arms are long
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        step(2f);
        Check(side.Hp == side.MaxHp && lateSide.Hp < lateSide.MaxHp, "the cross grows as it flies: a target off to the side is missed early and caught late");

        // Mastered: three crosses, each target still hit once.
        g.Enemies.Clear(); spots.Clear();
        var wide = pin(new V2(700f, 120f));   // well off the straight line, on the +22 degree arm's path
        var mid = pin(new V2(560f, 450f));
        masteredField.SetValue(g, true);
        g.Effects.Clear();
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        int crosses = g.Effects.Count(e => e.Kind == FxKind.HolyCross);
        step(2.5f);
        masteredField.SetValue(g, false);
        Check(crosses == 3, "a mastered Consecrate throws three crosses");
        Check(Math.Abs((mid.MaxHp - mid.Hp) - Game.HolyDamage * me.Stats.DmgMult) < 0.5f, "...and a target all three cross is hit once, not three times");
        Check(wide.Hp < wide.MaxHp, "...so the fan reaches targets the single cross missed");
        Check(g.Players[1].Stats.HolyRangeMult == 1f, "no upgrade, no extra range");
    }
}
