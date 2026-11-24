using System;
using System.Linq;
using Arena;

// The shared attack queries (Core/FoeQuery.cs, Docs/Targeting.md): FoesInCircle, FoesInBox, FoesInCone and AllFoes give exactly the
// Targetable enemies whose body touches the shape, nest, and agree with the plain loops they replaced.
static partial class Program
{
    static System.Collections.Generic.List<Enemy> ToList(FoeQuery q)
    {
        var l = new System.Collections.Generic.List<Enemy>();
        foreach (var e in q) l.Add(e);
        return l;
    }

    static void FoeQueryTest()
    {
        var g = new Session().Game;
        var rng = new Random(7);
        for (int i = 0; i < 200; i++)
        {
            var e = new Enemy { Id = i + 1, Type = EnemyType.Grunt, Def = Defs.Get(EnemyType.Grunt), Radius = 8f + (float)rng.NextDouble() * 30f, Hp = 10f, MaxHp = 10f,
                Pos = new V2((float)rng.NextDouble() * Game.Width, (float)rng.NextDouble() * Game.Height) };
            if (i % 9 == 0) e.SpawnT = 0.5f;  // still spawning
            if (i % 13 == 0) e.Hp = 0f;       // dead this tick
            g.Enemies[e.Id] = e;
        }
        Func<Enemy, bool> ok = e => e.SpawnT <= 0f && e.Hp > 0f;
        var c = new V2(800f, 450f);
        var circle = ToList(g.FoesInCircle(c, 220f)).Select(e => e.Id).OrderBy(x => x).ToArray();
        var want = g.Enemies.Values.Where(e => ok(e) && !e.Pos.FartherThan(c, 220f + e.Radius)).Select(e => e.Id).OrderBy(x => x).ToArray();
        Check(circle.Length > 5 && circle.SequenceEqual(want), "FoesInCircle: the targetable enemies touching the circle (" + circle.Length + ")");
        V2 a = new V2(200f, 200f), b = new V2(1400f, 700f);
        var box = ToList(g.FoesInBox(a, b, 40f)).Select(e => e.Id).OrderBy(x => x).ToArray();
        var wantBox = g.Enemies.Values.Where(e => ok(e) && e.Pos.DistanceToSegment(a, b) <= 40f + e.Radius).Select(e => e.Id).OrderBy(x => x).ToArray();
        Check(box.Length > 3 && box.SequenceEqual(wantBox), "FoesInBox: touching the strip (" + box.Length + ")");
        var cone = ToList(g.FoesInCone(c, 0f, 0.5f, 500f)).Select(e => e.Id).OrderBy(x => x).ToArray();
        var wantCone = g.Enemies.Values.Where(e => ok(e) && !e.Pos.FartherThan(c, 500f + e.Radius) && Math.Abs(MathX.WrapPi((e.Pos - c).Angle)) <= 0.5f).Select(e => e.Id).OrderBy(x => x).ToArray();
        Check(cone.Length > 2 && cone.SequenceEqual(wantCone), "FoesInCone: in reach and inside the angle (" + cone.Length + ")");
        Check(g.AllFoes().Count() == g.Enemies.Values.Count(ok) && ToList(g.AllFoes()).All(ok), "AllFoes: every targetable enemy, none spawning or dead");
        int pairs = 0;
        foreach (var e in g.FoesInCircle(c, 150f))
            foreach (var o in g.FoesInCircle(e.Pos, 60f))
                pairs++;
        Check(pairs > 0, "queries nest (" + pairs + " pairs)");
    }
}
