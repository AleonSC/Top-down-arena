using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The new shooting patterns (Variety.cs): the Pauser, the Leader and the Stitcher.
static partial class Program
{
    static void PatternsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var lead = typeof(Game).GetMethod("LeadAngle", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var squadField = typeof(Game).GetField("squadQueue", flags);
        var buildWave = typeof(Game).GetMethod("BuildWave", flags);
        var s = StartBuffed(dt, 22, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        Run(s, dt, 15, () => g.State == MatchState.Fighting);

        // They join the mix at their waves.
        var seen = new HashSet<EnemyType>();
        for (int k = 0; k < 30; k++)
        {
            g.Wave = 22;
            buildWave.Invoke(g, new object[] { 22 });
            foreach (var t in (List<EnemyType>)queueField.GetValue(g)) seen.Add(t);
            foreach (var q in (List<SquadDef>)squadField.GetValue(g)) foreach (var t in q.Members) seen.Add(t);
        }
        Check(seen.Contains(EnemyType.Pauser) && seen.Contains(EnemyType.Leader) && seen.Contains(EnemyType.Stitcher), "the Pauser, Leader and Stitcher show up by wave 22");
        g.Wave = 13;
        bool early = false;
        for (int k = 0; k < 12; k++)
        {
            buildWave.Invoke(g, new object[] { 13 });
            early |= ((List<EnemyType>)queueField.GetValue(g)).Contains(EnemyType.Pauser) || ((List<SquadDef>)squadField.GetValue(g)).Any(q => q.Members.Contains(EnemyType.Pauser));
        }
        Check(!early, "no Pausers before wave 14");
        g.Wave = 22;

        // Clear the field; a frozen Grunt in a corner keeps the wave going while we test.
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        ((List<SquadDef>)squadField.GetValue(g)).Clear();
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(40f, 40f), 0f, 0, false });
        anchor.Hp = anchor.MaxHp = 1e6f;
        foreach (var e in g.Enemies.Values) if (e != anchor) e.Hp = 0f;
        Run(s, dt, 0.2f);
        var me = g.Players[1];
        g.DevSetGod(2, true);
        Action<V2, V2> Place = (a, b) => { g.Players[1].Pos = c.Players[1].Pos = a; g.Players[2].Pos = c.Players[2].Pos = b; };
        Action hold = () => { anchor.FrozenT = 9f; anchor.FireT = 9f; };
        Func<EnemyType, V2, Enemy> Spawn = (t, where) => (Enemy)spawn.Invoke(g, new object[] { t, where, 0f, 0, false });
        Action clear = () => { foreach (var e in g.Enemies.Values) if (e != anchor) e.Hp = 0f; foreach (var b in g.Bullets.Values) if (Defs.IsEnemyShot(b.Kind)) b.Life = 0f; g.Hazards.Clear(); g.Patches.Clear(); };
        clear(); Run(s, dt, 0.2f);

        // The Pauser: 8 shots that stop and hang (on the client too), then rush at you.
        var pauser = Spawn(EnemyType.Pauser, new V2(500f, 450f));
        pauser.Hp = pauser.MaxHp = 1e5f;
        pauser.Speed = 0f;  // stays where it fired from, so we can measure how far out its shots stop
        pauser.FireT = 0.05f;
        Run(s, dt, 2f, () => g.Bullets.Values.Count(b => b.Kind == Defs.PauseShot) >= Game.PauseShots, () => { hold(); Place(new V2(900f, 450f), new V2(100f, 800f)); me.InvulnT = 1f; });
        var held = g.Bullets.Values.Where(b => b.Kind == Defs.PauseShot).ToList();
        Run(s, dt, Game.PauseFly + 0.1f, null, () => { hold(); pauser.FireT = 9f; me.InvulnT = 1f; });
        var at = held.Select(b => b.Pos).ToList();
        int onClient = c.Bullets.Values.Count(b => b.Kind == Defs.PauseShot);
        Run(s, dt, 0.25f, null, () => { hold(); pauser.FireT = 9f; me.InvulnT = 1f; });
        float drift = held.Select((b, i) => b.Pos.DistanceTo(at[i])).Max();
        float clientDrift = held.Max(b => { Bullet cb; return c.Bullets.TryGetValue(b.Id, out cb) ? cb.Pos.DistanceTo(b.Pos) : 999f; });
        float out0 = held.Average(b => b.Pos.DistanceTo(pauser.Pos));
        Run(s, dt, Game.PauseHold, null, () => { hold(); pauser.FireT = 9f; me.InvulnT = 1f; });
        var rush = g.Bullets.Values.Where(b => b.Source == EnemyType.Pauser && b.Kind == Defs.SmallShot).ToList();
        int aimed = rush.Count(b => Math.Abs(MathX.Fmod((me.Pos - b.Pos).Angle - b.Vel.Angle + MathX.Pi, MathX.Tau) - MathX.Pi) < 0.15f);
        Console.WriteLine("  pauser: {0} shots, hung {1:0} px out (moved {2:0.0} while hanging; client {3}, off by {4:0.0}); then {5} rush shots, {6} aimed at you",
            held.Count, out0, drift, onClient, clientDrift, rush.Count, aimed);
        Check(held.Count == Game.PauseShots && drift < 0.5f && out0 > 80f && out0 < 140f, "a Pauser fans 8 shots that stop about 100 px out and hang still");
        Check(onClient == Game.PauseShots && clientDrift < 3f, "the client sees them stop in the same places");
        Check(rush.Count == Game.PauseShots && aimed == rush.Count, "then each one rushes at the nearest player from where it hung");
        clear(); Run(s, dt, 0.2f);

        // The Leader: leads a moving target, with an aim line (Phase 1) first.
        float lt = Game.LeadTime(new V2(300f, 0f), new V2(0f, 100f), 260f);
        V2 meet = new V2(300f, 100f * lt);
        Check(Math.Abs(meet.Length - 260f * lt) < 0.5f && Game.LeadTime(new V2(300f, 0f), V2.Zero, 260f) > 1.1f, "the lead maths: the shot and the target arrive together");
        var leader = Spawn(EnemyType.Leader, new V2(500f, 450f));
        leader.Hp = leader.MaxHp = 1e5f;
        leader.SpawnT = 0f;
        leader.TargetId = 1;
        me.Pos = new V2(800f, 450f);
        me.Motion = new V2(0f, 200f);
        float aimStill = (me.Pos - leader.Pos).Angle;
        float aimLead = (float)lead.Invoke(g, new object[] { leader, me.Pos });
        float expect = (me.Pos - leader.Pos + me.Motion * Game.LeadTime(me.Pos - leader.Pos, me.Motion, Game.LeaderSpeed)).Angle;
        Console.WriteLine("  leader: aims {0:0.00} rad ahead of a player moving 200 px/s across (straight at them: {1:0.00})", aimLead - aimStill, aimStill);
        Check(aimLead - aimStill > 0.3f && Math.Abs(aimLead - expect) < 1e-3f, "a Leader aims where a moving player will be");
        me.Motion = V2.Zero;
        Check(Math.Abs((float)lead.Invoke(g, new object[] { leader, me.Pos }) - aimStill) < 1e-3f, "and straight at one standing still");
        bool aiming = false, clientAiming = false;
        int fired0 = g.Bullets.Values.Count(b => b.Source == EnemyType.Leader);
        leader.FireT = 0.05f;
        Run(s, dt, 3f, () => g.Bullets.Values.Count(b => b.Source == EnemyType.Leader) > fired0, () =>
        {
            hold(); Place(new V2(800f, 450f), new V2(100f, 800f)); me.InvulnT = 1f;
            aiming |= leader.Phase == 1; Enemy cl; clientAiming |= c.Enemies.TryGetValue(leader.Id, out cl) && cl.Phase == 1;
        });
        Check(aiming && clientAiming && g.Bullets.Values.Count(b => b.Source == EnemyType.Leader) - fired0 == 2, "it shows an aim line (seen on the client), then fires a pair");
        clear(); Run(s, dt, 0.2f);

        // The Stitcher: orbs tied by threads that hurt; too far apart, no thread.
        Place(new V2(800f, 450f), new V2(100f, 800f));
        var o1 = (Bullet)shoot.Invoke(g, new object[] { Defs.StitchShot, new V2(700f, 300f), V2.Zero, 0f, 3f, 12f, 0, false, 0f });
        var o2 = (Bullet)shoot.Invoke(g, new object[] { Defs.StitchShot, new V2(900f, 300f), V2.Zero, 0f, 3f, 12f, 0, false, 0f });
        var o3 = (Bullet)shoot.Invoke(g, new object[] { Defs.StitchShot, new V2(1500f, 300f), V2.Zero, 0f, 3f, 12f, 0, false, 0f });
        Run(s, dt, 0.2f, null, () => { hold(); me.InvulnT = 1f; });
        var links = new List<Game.StitchThread>(); var clientLinks = new List<Game.StitchThread>();
        Game.StitchLinks(g.Bullets.Values, links);
        Game.StitchLinks(c.Bullets.Values, clientLinks);
        Check(links.Count == 1 && clientLinks.Count == 1, "two orbs 200 px apart are tied by a thread, a third 600 px off isn't (the same on the client)");
        // 40 px off it first (letting the client's position catch up), then on it; hit grace spaces the hits out.
        Run(s, dt, 0.3f, null, () => { hold(); me.InvulnT = 1f; g.Players[1].Pos = c.Players[1].Pos = new V2(800f, 340f); });
        float taken = me.Run.DamageTaken;
        Run(s, dt, 0.3f, null, () => { hold(); g.Players[1].Pos = c.Players[1].Pos = new V2(800f, 340f); if (me.InvulnT > 0.9f) me.InvulnT = 0f; });
        float offThread = me.Run.DamageTaken - taken;
        Run(s, dt, 0.3f, null, () => { hold(); me.InvulnT = 1f; g.Players[1].Pos = c.Players[1].Pos = new V2(800f, 300f); });
        me.InvulnT = 0f;
        taken = me.Run.DamageTaken;
        Run(s, dt, 0.1f, null, () => { hold(); g.Players[1].Pos = c.Players[1].Pos = new V2(800f, 300f); });
        float onThread = me.Run.DamageTaken - taken;
        Console.WriteLine("  stitcher: standing on the thread took {0:0} (one hit, then hit grace), 40 px off it {1:0}", onThread, offThread);
        Check(onThread > 0f && onThread < 40f && offThread == 0f, "the thread hurts whoever stands on it, and only on it");
        clear(); Run(s, dt, 0.2f);

        // A pair stands side by side and fires parallel orbs: a fence about as wide as the gap between them.
        var pair = Game.Squads.First(q => q.Name == "Seamstresses");
        typeof(Game).GetMethod("SpawnSquad", flags).Invoke(g, new object[] { pair });
        List<Bullet> orbs = null;
        Run(s, dt, 14f, () => (orbs = g.Bullets.Values.Where(b => b.Kind == Defs.StitchShot).ToList()).Count >= 2 && g.Enemies.Values.Count(e => e.Type == EnemyType.Stitcher && e.T > 6f) == 2,
            () => { hold(); Place(new V2(800f, 450f), new V2(800f, 450f)); me.InvulnT = 1f; foreach (var b in g.Bullets.Values) if (b.Kind == Defs.StitchShot && b.Life < 6.8f) b.Life = 0f; });
        orbs = g.Bullets.Values.Where(b => b.Kind == Defs.StitchShot).OrderByDescending(b => b.Life).Take(2).ToList();
        float gap = orbs.Count == 2 ? orbs[0].Pos.DistanceTo(orbs[1].Pos) : 0f;
        float parallel = orbs.Count == 2 ? Math.Abs(MathX.Fmod(orbs[0].Vel.Angle - orbs[1].Vel.Angle + MathX.Pi, MathX.Tau) - MathX.Pi) : 9f;
        Console.WriteLine("  stitcher pair: orbs {0:0} px apart, courses {1:0.000} rad apart", gap, parallel);
        Check(gap > 150f && gap < Game.StitchLink && parallel < 0.01f, "a Stitcher pair fires parallel orbs, a fence about as wide as the pair");
        clear(); Run(s, dt, 0.2f);

        // The bots see the hanging shots for what they are, and keep off the threads.
        var still = (Bullet)shoot.Invoke(g, new object[] { Defs.PauseShot, new V2(300f, 300f), new V2(340f, 0f), 0f, Game.PauseFly + Game.PauseHold, 12f, 0, false, 0f });
        still.Life = Game.PauseHold * 0.5f;
        Check(Game.PauseTravel(still, 0.5f) == 0f && Math.Abs(Game.PauseTravel(new Bullet { Life0 = 1.3f, Life = 1.3f }, Game.PauseFly) - Game.PauseFly / 2f) < 1e-4f,
            "the bots know a hanging Pauser shot stays put (and how far a fresh one gets)");
        s[0].Leave(""); s[1].Leave("");
    }
}
