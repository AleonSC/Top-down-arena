using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The DPS meter and the target dummy (DpsTools.cs).
static partial class Program
{
    static void DpsToolsTest(float dt)
    {
        var host = new Session { AutoStart = 1, StartWave = 3 };
        host.Host(nextPort++, "A", Role.Dps, 0, 0, Kit.Soldier);
        host.StartMatch();
        var g = host.Game;
        for (int i = 0; i < 300 && !g.Running; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
        var me = g.Players[1];
        Func<Enemy> dummyOf = () => g.Enemies.Values.FirstOrDefault(e => e.Dummy);
        // Step with the wave's own enemies cleared (so only the dummy takes damage), firing at the dummy or not.
        Action<float, bool> steps = (seconds, fire) =>
        {
            for (int f = 0; f < (int)(seconds / dt); f++)
            {
                foreach (var e in g.Enemies.Values) if (!e.Dummy) e.Hp = 0f;
                foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
                var d = dummyOf();
                host.Poll();
                g.FixedStep(dt, new PlayerInput { Fire = fire && d != null, AimAt = d != null ? d.Pos : new V2(800f, 450f) });
            }
        };
        g.DevSetGod(1, true);
        steps(0.2f, false);

        var dummy = g.DevSpawnDummy(1);
        Check(dummy != null && dummy.Dummy && g.DummyCount() == 1 && g.Foes() == g.Enemies.Count - 1, "a dummy spawns, and isn't counted as a foe");
        V2 home = dummy.DummyHome;
        me.Pos = new V2(Math.Max(60f, home.X - 220f), home.Y);

        // Damage: the meter and the dummy agree, and it never dies.
        steps(4f, true);
        DpsTrack t = g.Dps[1];
        float taken = g.DummyDamage();
        Console.WriteLine("  meter: total {0:0} now {1:0} 5s {2:0} avg {3:0} peak {4:0}; dummy took {5:0}; weapon sim {6:0}",
            t.Total, t.Window(1f), t.Window(5f), t.Average, t.Peak, taken, Defs.WeaponDps(me.Weapon.Id));
        Check(t.Total > 100f && t.Window(1f) > 0f && t.Peak >= t.Window(1f) * 0.99f && t.Average > 0f, "the DPS meter counts a player's damage (now, peak and average)");
        Check(taken > 0f && Math.Abs(taken - t.Total) <= t.Total * 0.1f, "the dummy's damage taken matches the meter");
        Check(dummy.Hp >= dummy.MaxHp * 0.99f && g.Enemies.ContainsKey(dummy.Id), "a dummy never dies");
        Check(t.Average > Defs.WeaponDps(me.Weapon.Id) * 0.3f, "the meter reads in the same range as the sim's weapon DPS");

        // Idle: the combat clock stops, so the average doesn't sink while you stand about.
        float avgBefore = t.Average, combat = t.CombatTime;
        steps(4f, false);
        Console.WriteLine("  idle: avg {0:0.0} -> {1:0.0}, combat {2:0.00} -> {3:0.00}, idle {4}", avgBefore, t.Average, combat, t.CombatTime, t.Idle);
        Check(t.Idle && t.CombatTime < combat + 0.6f && Math.Abs(t.Average - avgBefore) < avgBefore * 0.05f, "standing about doesn't drag the average down");
        Check(t.Window(1f) == 0f, "the meter's 'now' falls to zero when nothing is hit");

        // Damage over time is counted and split out.
        g.DevResetDps();
        Check(g.Dps[1].Total == 0f && g.Dps[1].Peak == 0f && g.Dps[1].CombatTime == 0f, "reset clears the meter");
        Check(g.DummyDamage() == 0f, "reset clears what the dummy has taken");
        dummy.BurnT = 2f; dummy.BurnDps = 50f; dummy.BurnBy = 1;
        steps(2.5f, false);
        var after = g.Dps[1];
        Check(after.OverTime > 60f && after.OverTime <= after.Total + 0.01f, "burning is counted as damage over time (" + after.OverTime.ToString("0") + " of " + after.Total.ToString("0") + ")");

        // Kill all leaves the dummy, and its damage isn't counted.
        float totalBefore = g.Dps[1].Total;
        int killed = g.DevKillAll(1);
        steps(0.1f, false);
        Check(killed == 0 && g.Enemies.ContainsKey(dummy.Id) && Math.Abs(g.Dps[1].Total - totalBefore) < 1f, "Kill all leaves the dummy alone");

        // Standing on it is safe: no contact damage, no shots.
        me.Pos = dummy.Pos;
        g.DevSetGod(1, false);
        float hp0 = me.Hp;
        steps(1.5f, false);
        Check(me.Hp >= hp0 - 0.01f, "a dummy doesn't hurt to touch");
        g.DevSetGod(1, true);

        // Patrol: moves about its home; back to standing still when switched off.
        g.DummyMoves = true;
        float minX = 1e9f, maxX = -1e9f;
        for (int i = 0; i < 8; i++) { steps(0.8f, false); minX = Math.Min(minX, dummy.Pos.X); maxX = Math.Max(maxX, dummy.Pos.X); }
        Check(maxX - minX > 100f, "a patrolling dummy sweeps side to side (" + (maxX - minX).ToString("0") + " px)");
        g.DummyMoves = false;
        steps(0.2f, false);
        Check(dummy.Pos.DistanceTo(home) < 0.5f, "switched off, it goes back and stands still");

        // It doesn't hold up the wave.
        Check(g.State == MatchState.Fighting || g.State == MatchState.Intermission, "(match state is live)");
        for (int i = 0; i < 60 * 40 && g.State == MatchState.Fighting; i++) steps(0.1f, false);
        Check(g.State == MatchState.Intermission && g.DummyCount() == 1, "a wave still ends with a dummy standing");

        // Limits and removal.
        int made = 1;
        for (int i = 0; i < 6; i++) if (g.DevSpawnDummy(1) != null) made++;
        Check(made == Game.MaxDummies && g.DummyCount() == Game.MaxDummies, "at most " + Game.MaxDummies + " dummies");
        g.DevClearDummies();
        Check(g.DummyCount() == 0, "the dummies can be removed");
        host.Leave("");
    }
}
