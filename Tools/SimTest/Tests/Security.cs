using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

static partial class Program
{
    /// The Security Guard (SecurityGuard.cs): it keeps walking while it locks on (only the reload stops it), keeps a strafe
    /// side for at least SecurityStrafeMin s, points straight at you (no lead), and still empties its magazine and reloads.
    static void SecurityTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var s = StartPlain(dt, 19);
        var g = s[0].Game;
        var me = g.Players[1]; var friend = g.Players[2];
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(1500f, 80f), 0f, 0, false });
        foreach (var e0 in g.Enemies.Values) if (e0 != anchor) e0.Hp = 0f;
        Run(s, dt, 0.2f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); });
        g.Bullets.Clear(); g.DevSetGod(1, true);

        V2 home = new V2(800f, 450f);
        me.Pos = home;
        var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.SecurityGuard, home + new V2(-320f, 0f), 0f, 0, false });
        e.SpawnT = 0f; e.Hp = e.MaxHp = 1e6f;
        float still = 0f, windStill = 0f, windT = 0f, sideT = 0f, shortestSide = 99f, worstAim = 0f;
        int shots = 0, reloads = 0, flips = 0; float lastStrafe = e.Strafe; byte lastPhase = e.Phase;
        V2 last = e.Pos; var seen = new HashSet<int>();
        Run(s, dt, 30f, null, () =>
        {
            me.Pos = home; friend.Pos = new V2(150f, 820f); friend.Downed = true; anchor.FrozenT = 1f; anchor.FireT = 99f;
            bool moved = e.Pos.DistanceTo(last) > 0.01f; last = e.Pos;
            if (!moved) still += dt;
            if (e.Winding) { windT += dt; if (!moved) windStill += dt; }
            sideT += dt;
            if (e.Strafe != lastStrafe) { flips++; if (flips > 1) shortestSide = Math.Min(shortestSide, sideT); sideT = 0f; lastStrafe = e.Strafe; }
            if (!e.Winding && e.Phase > 0)
            {
                float off = Math.Abs(MathX.Fmod((me.Pos - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                if (e.T > 1f) worstAim = Math.Max(worstAim, off);
            }
            if (lastPhase > 0 && e.Phase == 0) reloads++;
            lastPhase = e.Phase;
            foreach (var b in g.Bullets.Values) if (b.Kind == Defs.TracerRound && seen.Add(b.Id)) shots++;
        });
        Console.WriteLine("  security guard 30 s: shots {0}, reloads {1}, still {2:0.0} s, still while locking on {3:0.00} of {4:0.0} s, strafe flips {5} (shortest side {6:0.0} s), worst aim off {7:0.00} rad",
            shots, reloads, still, windStill, windT, flips, shortestSide, worstAim);
        Check(shots >= 14 && reloads >= 2, "it fires its rounds and reloads");
        Check(windT > 1f && windStill < windT * 0.2f, "it keeps walking while it locks on");
        Check(still < 30f * 0.2f, "it stands still only around the reload");
        Check(flips >= 3 && shortestSide >= Game.SecurityStrafeMin - 0.05f, "it keeps each strafe side for at least SecurityStrafeMin s");
        Check(worstAim < 0.15f, "its pistol points straight at you (no lead)");
        e.Hp = 0f; Run(s, dt, 0.3f);
    }
}
