using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Duelist in three stages (Duelist.cs): En Garde, Masquerade, To the Death, and its last stand.
static partial class Program
{
    static void DuelistTest(float dt)
    {
        var s = StartBuffed(dt, 30, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var me = g.Players[1];
        var friend = g.Players[2];
        Action<V2> PutFriend = pos => { friend.Pos = pos; c.Players[2].Pos = pos; };
        Action safe = () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; };
        Run(s, dt, 15, () => g.State == MatchState.Fighting && Find(s[0], EnemyType.Duelist) != null && !g.IsSpawning(Find(s[0], EnemyType.Duelist)));
        var duel = Find(s[0], EnemyType.Duelist);
        Run(s, dt, 0.5f, null, safe);
        var ds = duel.Duel;
        Check(ds != null && ds.Stage == DuelStage.EnGarde, "the fight opens En Garde");

        // En Garde's loop: Volleys, Lunge, Guard, Blink, with breathers between.
        var turns = new List<DuelTurn>();
        bool warned = false, red = false;
        int blades = 0, bladeShots = 0;
        float fastest = 0f;
        V2 last = duel.Pos;
        Run(s, dt, 16.5f, null, () =>
        {
            safe();
            if (turns.Count == 0 || turns[turns.Count - 1] != ds.Turn) turns.Add(ds.Turn);
            warned |= ds.Dash == DuelDash.Lunge && ds.DashWarn > 0f;
            if (ds.Dash == DuelDash.Lunge && ds.DashWarn <= 0f) fastest = Math.Max(fastest, duel.Pos.DistanceTo(last) / dt);
            last = duel.Pos;
            blades = Math.Max(blades, ds.Blades.Count);
            red |= g.Bullets.Values.Any(b => b.Source == EnemyType.Duelist && b.Kind == Defs.PierceShot);
            if (ds.Turn == DuelTurn.Lunge) bladeShots = Math.Max(bladeShots, g.Bullets.Values.Count(b => b.Source == EnemyType.Duelist && b.Kind == Defs.SmallShot));
        });
        string order = string.Join(" > ", turns);
        Console.WriteLine("  en garde: {0}", order);
        Check(order.Contains("Volleys > Rest > Lunge > Rest > Guard > Rest > Blink"), "En Garde: Volleys, Lunge, Guard and Blink, breathers between");
        Console.WriteLine("  lunge: warning line {0}, dash {1:0} px/s, blades {2}, blade shots {3}; red volleys {4}", warned, fastest, blades, bladeShots, red);
        Check(red, "the Volleys fire its red aimed shots");
        Check(warned && fastest > 1000f && blades >= 3 && bladeShots >= 4, "the Lunge flashes a warning line, dashes fast and leaves blades that fire sideways");

        // The guard: shots on its front bounce back; from behind they hurt.
        Run(s, dt, 20f, () => ds.Turn == DuelTurn.Guard && ds.TurnT > 0.4f, safe);
        Func<V2, float> ShotFrom = side =>
        {
            float hp = duel.Hp;
            V2 at = duel.Pos + side * 70f;
            shoot.Invoke(g, new object[] { Defs.TankShot, at, -side * 600f, 0f, 1f, 40f, 1, false, 0f });
            Run(s, dt, 0.15f, null, safe);
            return hp - duel.Hp;
        };
        var before = new HashSet<int>(g.Bullets.Keys);
        float front = ShotFrom(V2.FromAngle(duel.Facing));
        int bounced = g.Bullets.Values.Count(b => !before.Contains(b.Id) && b.Source == EnemyType.Duelist && b.Kind == Defs.SmallShot);
        float back = ShotFrom(-V2.FromAngle(duel.Facing));
        Console.WriteLine("  guard: a shot on its front did {0:0} and {1} came back; one from behind did {2:0}", front, bounced, back);
        Check(front == 0f && bounced >= 1 && back > 20f, "Guard: shots on its front bounce back at you; from behind they hurt");

        // 65%: it changes stage (can't be hurt), then the Masquerade: three decoys round you; only its shots are solid.
        duel.Hp = duel.MaxHp * 0.6f;
        Run(s, dt, 1f, () => ds.TransT > 0f, safe);
        float h0 = duel.Hp; hurt.Invoke(g, new object[] { duel, 300f, 1 });
        bool immune = ds.TransT > 0f && duel.Hp == h0;
        Run(s, dt, 3f, () => ds.Stage == DuelStage.Masquerade && ds.TransT <= 0f, safe);
        Run(s, dt, 2f, () => ds.Turn == DuelTurn.Masquerade && ds.TurnT > 1.6f, safe);
        var decoys = g.Enemies.Values.Where(e => e.Type == EnemyType.Decoy && e.ParentId == duel.Id).ToList();
        var cd = c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Duelist);
        Console.WriteLine("  masquerade: immune while changing {0}; client sees {1}; decoys {2} (client {3})", immune, cd?.Duel?.Stage, decoys.Count, c.Enemies.Values.Count(e => e.Type == EnemyType.Decoy));
        Check(immune && cd?.Duel?.Stage == DuelStage.Masquerade, "at 65% it changes stage (can't be hurt), then the Masquerade (seen on the client)");
        Check(decoys.Count == 3 && decoys.All(d => d.Def.Color.R == duel.Def.Color.R), "three decoys that look like it");
        bool hollow = false, solid = false;
        Run(s, dt, 2f, null, () => { safe(); hollow |= g.Bullets.Values.Any(b => b.Kind == Defs.HollowShot); solid |= g.Bullets.Values.Any(b => b.Kind == Defs.PierceShot && b.Source == EnemyType.Duelist); });
        // A hollow shot right on you does nothing.
        me.InvulnT = 0f;
        float taken = me.Run.DamageTaken;
        shoot.Invoke(g, new object[] { Defs.HollowShot, me.Pos, new V2(1f, 0f), 0f, 1f, 0f, 0, true, 0f });
        s[0].Poll(); g.FixedStep(dt, new PlayerInput { AimAt = me.Pos + new V2(100f, 0f) });
        bool harmless = me.Run.DamageTaken == taken;
        Console.WriteLine("  masquerade shots: hollow {0}, solid red {1}, a hollow shot on you hurt {2}", hollow, solid, !harmless);
        Check(hollow && solid && harmless, "the decoys' shots are hollow and harmless; only the real one's are solid");
        decoys = g.Enemies.Values.Where(e => e.Type == EnemyType.Decoy && e.ParentId == duel.Id).ToList();
        if (decoys.Count > 0)
        {
            var d0 = decoys[0];
            V2 at = d0.Pos;
            hurt.Invoke(g, new object[] { d0, 5f, 1 });
            Run(s, dt, 0.1f, null, safe);
            int ring = g.Bullets.Values.Count(b => b.Kind == Defs.SmallShot && b.Pos.DistanceTo(at) < 60f);
            Console.WriteLine("  a decoy shot down: {0} shots burst from it", ring);
            Check(!g.Enemies.ContainsKey(d0.Id) && ring >= 10, "a decoy shot down shatters into a ring");
        }
        else Check(false, "a decoy shot down shatters into a ring (no decoys left to test)");

        // The Crossing: corner-to-corner dashes leaving blades.
        int crossBlades = 0;
        bool crossed = false;
        Run(s, dt, 14f, () => ds.Turn == DuelTurn.LungeChain, () => { safe(); crossed |= ds.Dash == DuelDash.Cross; crossBlades = Math.Max(crossBlades, ds.Blades.Count); });
        Console.WriteLine("  crossing: dashed {0}, blades {1}", crossed, crossBlades);
        Check(crossed && crossBlades >= 10, "the Crossing dashes corner to corner, leaving a line of blades");

        // 30%: To the Death. The dueling circle closes in; you can't leave it, and leaning on it hurts.
        duel.Hp = duel.MaxHp * 0.25f;
        Run(s, dt, 12f, () => ds.Stage == DuelStage.ToTheDeath && ds.TransT <= 0f && ds.Ring > 0f && ds.Ring <= Game.DuelRingRadius + 1f, safe);
        Run(s, dt, 0.3f, null, safe);
        cd = c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Duelist);
        V2 middle = new V2(Game.Width / 2f, Game.Height / 2f);
        me.Pos = new V2(80f, 80f);
        Run(s, dt, 0.05f, null, safe);
        float inside = me.Pos.DistanceTo(middle);
        me.InvulnT = 0f; foreach (var p in g.Players.Values) if (p != me) p.InvulnT = 1f;
        taken = me.Run.DamageTaken;
        s[0].Poll(); g.FixedStep(dt, new PlayerInput { Move = (me.Pos - middle).Normalized(), AimAt = middle }); s[1].Poll();
        Console.WriteLine("  circle: {0:0} px (client {1:0}); pushed out to the corner you're held {2:0} px from the middle; leaning on it hurt {3:0}", ds.Ring, cd?.Duel?.Ring, inside, me.Run.DamageTaken - taken);
        Check(ds.Ring == Game.DuelRingRadius && cd != null && Math.Abs(cd.Duel.Ring - ds.Ring) < 2f, "To the Death: a dueling circle 360 px round the middle (on the client too)");
        Check(inside <= Game.DuelRingRadius + 0.5f && me.Run.DamageTaken > taken, "you can't leave the circle, and leaning on it hurts");

        // The Salute: a perfect parry as it strikes knocks it off balance (open: 50% more damage).
        me.Pos = middle + new V2(-150f, 0f);
        PutFriend(middle + new V2(150f, 120f));
        Run(s, dt, 20f, () => ds.Turn == DuelTurn.Salute && ds.SaluteT > 0f, safe);
        bool parried = false;
        for (int i = 0; i < 120 && !parried; i++)
        {
            // When the strike starts, stand in its path and raise the parry just as it comes.
            bool striking = ds.Dash == DuelDash.Salute && ds.DashWarn <= 0f;
            if (striking && ds.DashDone < 10f)
                me.Pos = duel.Pos + V2.FromAngle(ds.DashAng) * 160f;
            s[0].Poll();
            g.FixedStep(dt, new PlayerInput { AimAt = duel.Pos, Parry = striking });
            g.Update(dt);
            Step(s[1], dt);
            foreach (var p in g.Players.Values) if (p != me) p.InvulnT = 1f;
            parried = ds.StaggerT > 0f;
        }
        float hb = duel.Hp; hurt.Invoke(g, new object[] { duel, 100f, 1 });
        float openHit = hb - duel.Hp;
        Run(s, dt, 0.2f, null, safe);
        cd = c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Duelist);
        Console.WriteLine("  salute: parried {0} (stagger {1:0.0} s, client {2:0.0}); a 100 hit while open did {3:0}", parried, ds.StaggerT, cd?.Duel?.StaggerT, openHit);
        Check(parried && Game.DuelOpen(duel) && cd != null && cd.Duel.StaggerT > 0f, "a perfect parry of the Salute's strike knocks it off balance (seen on the client)");
        Check(Math.Abs(openHit - 100f * Game.DuelOpenMult) < 1f, "while it's open it takes 50% more damage");
        g.HoldParry = false;

        // Last stand: the circle closes to 280, a spiral, and salutes.
        duel.Hp = duel.MaxHp * 0.08f;
        bool spiral = false, saluted = false;
        Run(s, dt, 7f, null, () => { safe(); spiral |= ds.Stage == DuelStage.LastStand && g.Bullets.Values.Count(b => b.Source == EnemyType.Duelist) > 20; saluted |= ds.Stage == DuelStage.LastStand && ds.SaluteT > 0f; });
        Console.WriteLine("  last stand: stage {0}, circle {1:0}, spiral {2}, salutes {3}", ds.Stage, ds.Ring, spiral, saluted);
        Check(ds.Stage == DuelStage.LastStand && ds.Ring == Game.DuelRingLast && spiral && saluted, "below 10%: the circle closes to 280, a spiral, and salutes");
        s[0].Leave(""); s[1].Leave("");
    }
}
