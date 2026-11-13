using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Eclipse's faction, steps 1-2 (Core/Penumbral.cs, Docs/EclipseFaction.md): an Ember weaves in and leaves sparks hanging
// behind it, a Corona Priest's ring has one shot at you and a gap beside it, a Heliograph plants, flickers its line,
// burns a lane and rests; a Moonward's moon soaks shots, a Gnomon's flash spares its shadow, a Dusk Lobber's pool drains;
// and they come in the waves before the Eclipse.
static partial class Program
{
    static void PenumbralTest(float dt)
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Check(Game.IsPenumbral(EnemyType.Ember) && Game.IsPenumbral(EnemyType.CoronaPriest) && Game.IsPenumbral(EnemyType.Heliograph) && !Game.IsPenumbral(EnemyType.RiotCaptain),
            "Ember, Corona Priest and Heliograph are the Penumbral Court");
        Check(Defs.Get(EnemyType.Ember).Melee && !Defs.Get(EnemyType.CoronaPriest).Melee && Game.JobOf(EnemyType.Heliograph) == EnemyJob.Zoner,
            "the Ember is melee, the Corona Priest shoots, the Heliograph is a Zoner");
        Check(Defs.IsEnemyShot(Defs.EmberSpark), "an Ember's spark is an enemy shot");

        Game.SeedForTests(4422);
        var host = new Session { AutoStart = 1, StartWave = 1 };
        host.Host(nextPort++, "Host", Role.Tank);
        host.Diff.Set(DiffKnob.Campaign, 0f);
        host.StartMatch();
        var g = host.Game;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", np);
        var queues = new[] { (IList)typeof(Game).GetField("spawnQueue", np).GetValue(g), (IList)typeof(Game).GetField("squadQueue", np).GetValue(g) };
        Action Quiet = () => { foreach (var q in queues) q.Clear(); };
        for (int f = 0; f < 60 * 20 && g.State != MatchState.Fighting; f++)
            Step(host, dt);
        Check(g.State == MatchState.Fighting, "the match started");
        var me = g.Players[1];
        Func<EnemyType, V2, Enemy> put = (t, at) =>
        {
            Quiet();
            g.Enemies.Clear(); g.Bullets.Clear(); g.Patches.Clear();
            me.Hp = me.MaxHp;
            var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false });
            e.SpawnT = 0f; e.Spawning = false;
            return e;
        };
        Action<float, Action> run = (secs, each) => { for (int f = 0; f < secs * 60; f++) { Quiet(); me.Pos = me.Disp = new V2(900f, 400f); Step(host, dt); each?.Invoke(); } };
        me.Pos = me.Disp = new V2(900f, 400f);
        g.DevSetGod(1, true);

        // Ember: closes in on a weave and leaves sparks that hang (slow, short-lived).
        var em = put(EnemyType.Ember, new V2(300f, 400f));
        float d0 = em.Pos.DistanceTo(me.Pos), off = 0f;
        int sparks = 0;
        float fastest = 0f, longest = 0f;
        run(1.6f, () =>
        {
            off = Math.Max(off, Math.Abs(em.Pos.Y - 400f));
            foreach (var b in g.Bullets.Values.Where(b => b.Kind == Defs.EmberSpark))
            {
                fastest = Math.Max(fastest, b.Vel.Length);
                longest = Math.Max(longest, b.Life0);
            }
            sparks = Math.Max(sparks, g.Bullets.Values.Count(b => b.Kind == Defs.EmberSpark));
        });
        Console.WriteLine("  Ember: {0:0} px closer, {1:0} px off the line, {2} sparks at once (drift {3:0} px/s, {4:0.0} s)", d0 - em.Pos.DistanceTo(me.Pos), off, sparks, fastest, longest);
        Check(em.Pos.DistanceTo(me.Pos) < d0 - 150f && off > 15f, "an Ember closes in on a weave");
        Check(sparks >= 4 && fastest <= Game.EmberSparkDrift + 0.1f && Math.Abs(longest - Game.EmberSparkLife) < 0.01f, "it leaves slow sparks hanging behind it");

        // Corona Priest: a ring with one shot straight at you and one open slot beside it.
        var pr = put(EnemyType.CoronaPriest, new V2(640f, 400f));
        List<Bullet> ring = null;
        float aimAt = 0f;
        bool wound = false;
        run(6f, () =>
        {
            wound |= pr.Winding;
            if (ring != null) return;
            var bs = g.Bullets.Values.Where(b => b.Shooter == pr.Id).ToList();
            if (bs.Count > 0) { ring = bs; aimAt = (me.Pos - pr.Pos).Angle; }
        });
        Check(wound, "a Corona Priest winds up before it fires (its halo flares)");
        Check(ring != null && ring.Count == Game.PriestSlots - 1, "it fires a ring of " + (Game.PriestSlots - 1) + " shots");
        if (ring != null && ring.Count > 0)
        {
            float slot = MathX.Tau / Game.PriestSlots;
            Func<float, float> near = a => ring.Min(b => Math.Abs(MathX.Fmod(b.Vel.Angle - a + MathX.Pi, MathX.Tau) - MathX.Pi));
            bool atYou = near(aimAt) < 0.12f;
            bool gapBeside = near(aimAt + slot) > slot * 0.6f || near(aimAt - slot) > slot * 0.6f;
            Console.WriteLine("  Corona Priest: {0} shots, one at you {1}, a gap beside it {2}", ring.Count, atYou, gapBeside);
            Check(atYou && gapBeside, "one shot comes straight at you, the gap is one slot beside it");
        }

        // Heliograph: plants, flickers, burns a lane where you stood, rests.
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp = 1e6f;
        var he = put(EnemyType.Heliograph, new V2(620f, 400f));
        bool warned = false, burned = false, rested = false, hurt = false, still = true, onYou = false;
        V2 planted = V2.Zero;
        byte last = 0;
        run(9f, () =>
        {
            if (he.Phase == 1 && last != 1) { warned = true; planted = he.Pos; onYou = Game.HelioLaneDistance(he, me.Pos) < 1f; }
            if (he.Phase == 1 || he.Phase == 2) still &= he.Pos.DistanceTo(planted) < 1f;
            if (he.Phase == 2) { burned = true; hurt |= me.Hp < me.MaxHp; }
            if (he.Phase == 3) rested = true;
            last = he.Phase;
        });
        Console.WriteLine("  Heliograph: warned {0}, burned {1}, rested {2}, hurt you {3}", warned, burned, rested, hurt);
        Check(warned && burned && rested && still, "a Heliograph plants, flickers its line, burns and rests, standing still");
        Check(onYou && hurt, "its line is aimed where you stand, and the lane hurts");
        // ...and stepping off the line is safe.
        var he2 = put(EnemyType.Heliograph, new V2(620f, 400f));
        bool hurt2 = false;
        last = 0;
        for (int f = 0; f < 9 * 60; f++)
        {
            Quiet();
            if (he2.Phase == 1 && last != 1) me.Pos = me.Disp = new V2(900f, 400f) + new V2(0f, 120f);  // step off once it aims
            if (he2.Phase == 2) hurt2 |= me.Hp < me.MaxHp;
            if (he2.Phase == 3) break;
            last = he2.Phase;
            Step(host, dt);
            if (he2.Phase == 0) me.Pos = me.Disp = new V2(900f, 400f);
        }
        Check(!hurt2, "stepping off the line before it burns is safe");

        // Step 2. Moonward: its moon soaks players' shots; the far side of it doesn't.
        var mw = put(EnemyType.Moonward, new V2(600f, 400f));
        float f0 = mw.Facing;
        run(0.5f, null);
        Check(Math.Abs(MathX.Fmod(mw.Facing - f0 - MathX.Tau / Game.MoonwardTurn * 0.5f + MathX.Pi, MathX.Tau) - MathX.Pi) < 0.1f, "a Moonward's moon goes round once every " + Game.MoonwardTurn + " s");
        typeof(Game).GetMethod("FindMoonwards", np).Invoke(g, null);
        var stops = typeof(Game).GetMethod("MoonwardStops", np);
        V2 moon = Game.MoonwardMoon(mw, mw.Pos);
        var onMoon = new Bullet { Kind = Defs.DpsShot, Pos = moon, Radius = 3.5f };
        var farSide = new Bullet { Kind = Defs.DpsShot, Pos = mw.Pos - (moon - mw.Pos), Radius = 3.5f };
        var theirs = new Bullet { Kind = Defs.SmallShot, Pos = moon, Radius = 6f };
        Check((bool)stops.Invoke(g, new object[] { onMoon }) && !(bool)stops.Invoke(g, new object[] { farSide }) && !(bool)stops.Invoke(g, new object[] { theirs }),
            "the moon soaks a player's shot that touches it, not one on the far side, and not enemy shots");

        // Gnomon: plants with its slab a little to one side of you, swells, flashes; its shadow is safe.
        Func<bool, bool> flash = hide =>
        {
            var gn = put(EnemyType.Gnomon, new V2(640f, 400f));
            me.Hp = me.MaxHp;
            bool hit = false, gPlanted = false, offYou = false;
            byte lastP = 0;
            V2 at = new V2(900f, 400f);
            for (int f = 0; f < 10 * 60; f++)
            {
                Quiet();
                me.Pos = me.Disp = at;
                lastP = gn.Phase;
                Step(host, dt);
                if (gn.Phase == 2) { hit = me.Hp < me.MaxHp; break; }
                if (gn.Phase == 1 && lastP != 1)
                {
                    gPlanted = true;
                    offYou = !Game.InGnomonShadow(gn, at);
                    if (hide) at = gn.Pos + V2.FromAngle(gn.Facing) * 170f;  // step into the shadow
                }
            }
            Check(gPlanted && offYou, "a Gnomon plants with its shadow a step to the side of you");
            return hit;
        };
        bool hitOut = flash(false), hitIn = flash(true);
        Console.WriteLine("  Gnomon: flash hurt you outside the shadow {0}, inside {1}", hitOut, hitIn);
        Check(hitOut && !hitIn, "its flash hurts you outside its shadow, not in it");

        // Dusk Lobber: lobs a pool of dark that drains you.
        var dl = put(EnemyType.DuskLobber, new V2(560f, 400f));
        bool pool = false;
        float before = me.Hp;
        run(9f, () => pool |= g.Patches.Values.Any(pt => pt.Kind == PatchKind.Dusk && pt.Active && pt.Pos.DistanceTo(me.Pos) < Game.DuskPoolR));
        Console.WriteLine("  Dusk Lobber: a pool on you {0}, health {1:0} -> {2:0}", pool, before, me.Hp);
        Check(dl.Kin == EnemyType.GasGrenadier && pool, "a Dusk Lobber plays like a Gas Grenadier and lobs a pool of dark on you");
        Check(me.Hp < before, "the pool drains you");
        host.Leave("");

        // The faction: the six come in the waves before the Eclipse (50-wave track: 46-49).
                var fe = Approaches.For(EnemyType.Eclipse);
        Check(fe != null && fe.Members.Length == 6 && fe.Members.All(m => Game.IsPenumbral(m.Type)) && fe.Squads.Select(q => q.Name).SequenceEqual(new[] { "Dawn", "Mirror pair", "Night watch" }),
            "the Eclipse's faction: six members so far; squads Dawn, Mirror pair and Night watch");
        var build = typeof(Game).GetMethod("BuildWave", np);
        var spawnQ = typeof(Game).GetField("spawnQueue", np);
        var squadQ = typeof(Game).GetField("squadQueue", np);
        var seen = new HashSet<EnemyType>();
        var early = new HashSet<EnemyType>();
        int mostHelio = 0;
        for (int w = 46; w <= 49; w++)
            for (int seed = 0; seed < 4; seed++)
            {
                Game.SeedForTests(9500 + w * 10 + seed);
                var cg = new Session().Game;
                cg.Wave = w;
                build.Invoke(cg, new object[] { w });
                typeof(Game).GetMethod("CapSentries", np).Invoke(cg, null);
                var q = ((List<EnemyType>)spawnQ.GetValue(cg)).Concat(((List<SquadDef>)squadQ.GetValue(cg)).SelectMany(x => x.Members)).ToList();
                foreach (var t in q.Where(Game.IsPenumbral))
                    seen.Add(t);
                mostHelio = Math.Max(mostHelio, q.Count(t => t == EnemyType.Heliograph));
            }
        for (int w = 2; w <= 44; w += 3)
        {
            Game.SeedForTests(9700 + w);
            var cg = new Session().Game;
            cg.Wave = w;
            build.Invoke(cg, new object[] { w });
            foreach (var t in ((List<EnemyType>)spawnQ.GetValue(cg)).Concat(((List<SquadDef>)squadQ.GetValue(cg)).SelectMany(x => x.Members)).Where(Game.IsPenumbral))
                early.Add(t);
        }
        Console.WriteLine("  waves 46-49 bring: " + string.Join(", ", seen) + "; at most " + mostHelio + " Heliographs a wave");
        Check(seen.Count == 6, "waves 46-49 bring all six");
        Check(mostHelio <= Game.MaxHeliographs, "at most " + Game.MaxHeliographs + " Heliographs a wave");
        Check(early.Count == 0, "none come before the Eclipse's approach");
    }
}
