using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

static partial class Program
{
    /// The Twin Reapers' cult (ReaperFaction.cs): the faction table, the Bound Wraiths' pair, orbit, tether and revive, the
    /// Scythe Wraith's locked mark and long reap, the Lamplighter's lines then cross, the Mourner's spiral then rest.
    static void ReaperCultTest(float dt)
    {
        var f = Approaches.For(EnemyType.TwinA);
        Check(f != null && f.Members.Length == 8 && f.Squads.Length == 4 && f.Finale != null && f.Lessons.Length == 10, "the Twin Reapers have a faction: 8 members, 4 squads, a finale, 10 lessons");
        Check(Game.IsBrute(EnemyType.Gravedigger) && Game.IsLunger(EnemyType.ScytheWraith) && Game.IsSwordsman(EnemyType.Acolyte), "the Gravedigger, Scythe Wraith and Acolyte share their bases' behaviour");

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var s = StartPlain(dt, 13);
        var g = s[0].Game;
        var me = g.Players[1]; var friend = g.Players[2];
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        Func<EnemyType, V2, Enemy> add = (t, at) => { var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; return e; };
        // An anchor far off keeps the wave from ending while we work.
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        var anchor = add(EnemyType.Grunt, new V2(1500f, 80f));
        anchor.Hp = anchor.MaxHp = 1e6f;
        Action clear = () =>
        {
            ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
            foreach (var e0 in g.Enemies.Values) if (e0 != anchor) { e0.LastHitBy = 0; e0.Hp = 0f; }
            Run(s, dt, 0.2f);
            g.Bullets.Clear();
        };
        clear();
        g.DevSetGod(1, true);
        V2 home = new V2(800f, 450f);
        Action hold = () => { ((List<EnemyType>)queueField.GetValue(g)).Clear(); me.Pos = home; friend.Pos = new V2(150f, 820f); friend.Downed = true; anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); };

        // --- Bound Wraiths: a pair, circling near you, the tether sweeping over you ---
        int before = g.Enemies.Values.Count(e => e.Type == EnemyType.BoundWraith && e.Hp > 0f);
        var a = add(EnemyType.BoundWraith, home + new V2(-300f, 0f));
        Enemy b; g.Enemies.TryGetValue(a.LinkId, out b);
        int pairs = g.Enemies.Values.Count(e => e.Type == EnemyType.BoundWraith && e.Hp > 0f) - before;
        Check(pairs == 2 && b != null && b.LinkId == a.Id && (a.Id & 1) != (b.Id & 1), "one Bound Wraith brings its partner, linked both ways, one Dusk and one Dawn");
        a.Hp = a.MaxHp = 1e5f; b.Hp = b.MaxHp = 1e5f;
        float turned = 0f, lastAng = (a.Pos - b.Pos).Angle, closest = 999f; int crossings = 0; bool on = false;
        Run(s, dt, 8f, null, () =>
        {
            hold();
            float ang = (a.Pos - b.Pos).Angle, d = MathX.Fmod(ang - lastAng + MathX.Pi, MathX.Tau) - MathX.Pi;
            turned += Math.Abs(d); lastAng = ang;
            V2 mid = (a.Pos + b.Pos) * 0.5f; closest = Math.Min(closest, mid.DistanceTo(home));
            V2 dir = (b.Pos - a.Pos).Normalized(); float along = (home - a.Pos).Dot(dir);
            bool hit = along > 0f && along < a.Pos.DistanceTo(b.Pos) && home.DistanceTo(a.Pos + dir * along) < Game.BoundTetherWidth / 2f + me.Def.HitR;
            if (hit && !on) crossings++;
            on = hit;
        });
        Console.WriteLine("  bound pair 8 s: turned {0:0.0} rad, middle came within {1:0} px, tether crossed you {2} times, gap {3:0} px", turned, closest, crossings, a.Pos.DistanceTo(b.Pos));
        Check(turned > 5f && closest < Game.BoundHover + 30f, "the pair circles a point that comes to you");
        Check(crossings >= 2, "the tether sweeps over you");

        // Kill one: the other stops and channels it back at 60%.
        a.Hp = a.MaxHp = 100f; b.Hp = b.MaxHp = 100f;
        a.Hp = 0f;
        Run(s, dt, 0.1f, null, hold);
        V2 bStill = b.Pos;
        Check(b.Phase == Game.BoundChanneling, "kill one Bound Wraith and its partner channels");
        Run(s, dt, Game.BoundRevive - 0.3f, null, hold);
        Check(b.Pos.DistanceTo(bStill) < 2f, "it stands still while it channels");
        Run(s, dt, 0.6f, null, hold);
        Enemy back; g.Enemies.TryGetValue(b.LinkId, out back);
        Check(b.Phase == 0 && back != null && back.Hp > 0f && Math.Abs(back.Hp - back.MaxHp * Game.BoundReviveHp) < 1f && back.LinkId == b.Id, "after BoundRevive s the fallen one is back at 60%, linked again");
        // Kill one, then the channeller: neither comes back.
        back.Hp = 0f;
        Run(s, dt, 0.5f, null, hold);
        b.Hp = 0f;
        Run(s, dt, Game.BoundRevive + 1f, null, hold);
        Check(!g.Enemies.Values.Any(e => e.Type == EnemyType.BoundWraith && e.Hp > 0f && (e.LinkId == b.Id || e.Pos.DistanceTo(back.Pos) < 30f)), "kill the channeller too and the pair is gone for good");

        // --- Scythe Wraith: locks its mark, then a long reap ---
        clear();
        var w = add(EnemyType.ScytheWraith, home + new V2(-240f, 0f));
        w.Hp = w.MaxHp = 1e5f; w.PhaseT = 0f;
        float markFacing = 0f, maxDrift = 0f, dashFrom = 0f; V2 dashStart = default(V2), dashEnd = default(V2); bool marked = false, dashed = false;
        Run(s, dt, 5f, () => dashed && w.Phase == 3, () =>
        {
            hold();
            if (w.Phase == 1 && w.PhaseT < Game.ScytheMark - Game.ScytheTrack - 0.02f)
            {
                if (!marked) { marked = true; markFacing = w.Facing; me.Pos = home + new V2(0f, 60f); }
                maxDrift = Math.Max(maxDrift, Math.Abs(w.Facing - markFacing));
            }
            if (w.Phase == 2 && !dashed) { dashed = true; dashStart = w.Pos; dashFrom = w.Facing; }
            if (w.Phase == 2) dashEnd = w.Pos;
        });
        float dash = dashStart.DistanceTo(dashEnd);
        Console.WriteLine("  scythe wraith: mark drift {0:0.000} rad, dash {1:0} px", maxDrift, dash);
        Check(marked && maxDrift < 0.001f, "the Scythe Wraith's mark stays put after ScytheTrack s");
        Check(dashed && dash > 220f, "it reaps along a long line (about 270 px)");

        // --- Lamplighter: three lines, then a cross ---
        clear();
        var lamp = add(EnemyType.Lamplighter, home + new V2(-280f, 0f));
        lamp.Hp = lamp.MaxHp = 1e5f;
        var volleys = new List<int>(); var seenB = new HashSet<int>(); float windStill = 0f; V2 lastL = lamp.Pos;
        Run(s, dt, 14f, () => volleys.Count >= 2, () =>
        {
            hold();
            if (lamp.Winding && lamp.Pos.DistanceTo(lastL) < 0.01f) windStill += dt;
            lastL = lamp.Pos;
            int n = 0;
            foreach (var bl in g.Bullets.Values) if (bl.Shooter == lamp.Id && seenB.Add(bl.Id)) n++;
            if (n > 0) volleys.Add(n);
        });
        Console.WriteLine("  lamplighter volleys: {0}; still while winding {1:0.00} s", string.Join(", ", volleys), windStill);
        Check(volleys.Count >= 2 && volleys[0] == Game.LampLines * Game.LampStreak && volleys[1] == 4 * Game.LampStreak, "the Lamplighter fires three lines, then a cross");
        Check(windStill > Game.LampWind * 0.5f, "it plants its feet while its lantern flares");

        // --- Mourner: a twin spiral, then a rest ---
        clear();
        var mo = add(EnemyType.Mourner, home + new V2(-260f, 0f));
        mo.Hp = mo.MaxHp = 1e5f;
        int spiralShots = 0; float lastShot = -1f, longestQuiet = 0f, clock = 0f; seenB.Clear();
        Run(s, dt, 12f, null, () =>
        {
            hold(); clock += dt;
            foreach (var bl in g.Bullets.Values)
                if (bl.Shooter == mo.Id && seenB.Add(bl.Id))
                {
                    spiralShots++;
                    if (lastShot >= 0f) longestQuiet = Math.Max(longestQuiet, clock - lastShot);
                    lastShot = clock;
                }
        });
        Console.WriteLine("  mourner 12 s: {0} shots, longest quiet {1:0.0} s", spiralShots, longestQuiet);
        Check(spiralShots >= 20, "the Mourner weeps a spiral");
        Check(longestQuiet > Game.MournRest * 0.8f, "then rests");
        foreach (var e0 in g.Enemies.Values) e0.Hp = 0f;
        Run(s, dt, 0.3f);
    }
}
