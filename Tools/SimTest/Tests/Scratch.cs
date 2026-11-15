using System;
using System.Collections.Generic;
using Arena;

// V2's square-root-free distance tests (CloserThan, WithinReach, FartherThan, FartherOrAt) agree with comparing
// DistanceTo, and the reusable lists (Scratch.cs ListPool) hand out a different list while another is in use.
static partial class Program
{
    static void ScratchTest()
    {
        var rng = new Random(7);
        int bad = 0;
        for (int i = 0; i < 20000; i++)
        {
            var a = new V2((float)(rng.NextDouble() * 1600 - 200), (float)(rng.NextDouble() * 900 - 200));
            var b = new V2((float)(rng.NextDouble() * 1600 - 200), (float)(rng.NextDouble() * 900 - 200));
            float r = (float)(rng.NextDouble() * 600 - 40);  // some negative
            float d = a.DistanceTo(b);
            if (Math.Abs(d - r) < 1e-3f)
                continue;  // a rounding hair apart is allowed
            if (a.CloserThan(b, r) != (d < r) || a.WithinReach(b, r) != (d <= r) || a.FartherThan(b, r) != (d > r) || a.FartherOrAt(b, r) != (d >= r))
                bad++;
        }
        Check(bad == 0, "CloserThan / WithinReach / FartherThan / FartherOrAt agree with DistanceTo");
        var o = V2.Zero;
        Check(!o.CloserThan(o, 0f) && o.WithinReach(o, 0f) && !o.FartherThan(o, 0f) && o.FartherOrAt(o, 0f), "a point at distance 0 from itself: not < 0, is <= 0");
        var nan = new V2(float.NaN, 0f);
        Check(!nan.CloserThan(o, 5f) && !nan.WithinReach(o, 5f) && !nan.FartherThan(o, 5f) && !nan.FartherOrAt(o, 5f), "a NaN position is neither near nor far, as before");
        Check(!o.CloserThan(new V2(1f, 0f), -5f) && o.FartherThan(new V2(1f, 0f), -5f), "a negative reach is never reached (squaring it must not flip that)");

        var pool = new ListPool<int>();
        var x = pool.Rent();
        x.Add(1);
        var y = pool.Rent();
        Check(!ReferenceEquals(x, y), "a second Rent while the first is out is a different list");
        pool.Return(x);
        var z = pool.Rent();
        Check(ReferenceEquals(x, z) && z.Count == 0, "a returned list comes back cleared");
    }
}
