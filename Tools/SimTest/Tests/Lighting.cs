using System;
using Arena;

// Dynamic lighting's shadows (Core/LightCast.cs; drawn by Game/LightingLook.cs): which floor a light reaches past walls and bodies.
static partial class Program
{
    static void LightingTest()
    {
        var s = new ShadowScene();
        var fan = new LightFan();
        V2 at = new V2(500f, 500f);

        // Nothing in reach: every ray runs the light's full reach, no shadow.
        LightCast.Cast(s, at, 200f, fan);
        bool full = fan.N == LightCast.RimRays && !fan.Shadowed;
        for (int i = 0; i < fan.N; i++) full &= Math.Abs(fan.Dist[i] - 200f) < 0.01f;
        Check(full, "an empty floor: " + fan.N + " rays, all at full reach, nothing shadowed");
        Check(LightCast.Lit(fan, new V2(650f, 500f)) && !LightCast.Lit(fan, new V2(720f, 500f)), "lit inside its reach, dark past it");

        // A wall 100 px to the right: the floor behind it is dark, the floor beside it lit, its edge sharp at the corner.
        s.AddBox(new V2(610f, 500f), 10f, 40f);
        LightCast.Cast(s, at, 300f, fan);
        Check(fan.Shadowed, "a wall in reach casts a shadow");
        Check(!LightCast.Lit(fan, new V2(700f, 500f)) && !LightCast.Lit(fan, new V2(760f, 540f)), "the floor behind the wall is dark (" + LightCast.Reach(fan, new V2(700f, 500f)).ToString("0") + " px reach that way)");
        Check(LightCast.Lit(fan, new V2(590f, 500f)) && LightCast.Lit(fan, new V2(500f, 650f)) && LightCast.Lit(fan, new V2(400f, 500f)), "in front of it, beside it and behind the light: lit");
        // the shadow's edge runs straight out from the corner (600, 460): just inside it dark, just outside it lit
        V2 corner = new V2(600f, 460f), d = (corner - at).Normalized();
        V2 past = at + d * 220f, side = new V2(-d.Y, d.X) * 6f;
        Check(!LightCast.Lit(fan, past + side) && LightCast.Lit(fan, past - side), "the shadow's edge runs straight out from the wall's corner");

        // A body (disc) casts a shadow as wide as it is; the light's own body (the light inside it) casts none.
        s.Clear();
        s.AddDisc(new V2(600f, 500f), 20f);
        LightCast.Cast(s, at, 300f, fan);
        Check(!LightCast.Lit(fan, new V2(700f, 500f)) && !LightCast.Lit(fan, new V2(700f, 530f)) && LightCast.Lit(fan, new V2(700f, 560f)), "a body's shadow behind it, about its width (wider with distance)");
        s.Clear();
        s.AddDisc(at + new V2(5f, 0f), 18f);
        LightCast.Cast(s, at, 300f, fan);
        Check(!fan.Shadowed && LightCast.Lit(fan, new V2(700f, 500f)), "a light inside a body (its shooter, the enemy a blast is on) isn't shadowed by it");

        // Casters out of reach are skipped; a long wall crossing the light's rim is clipped to it.
        s.Clear();
        s.AddSeg(new V2(900f, 0f), new V2(900f, 1000f));
        LightCast.Cast(s, at, 300f, fan);
        Check(!fan.Shadowed && fan.N == LightCast.RimRays, "a wall out of reach: no extra rays, no shadow");
        s.Clear();
        s.AddSeg(new V2(650f, -1000f), new V2(650f, 2000f));
        LightCast.Cast(s, at, 300f, fan);
        Check(LightCast.Lit(fan, new V2(640f, 520f)) && !LightCast.Lit(fan, new V2(660f, 520f)) && !LightCast.Lit(fan, new V2(700f, 300f)), "a long wall through the rim stops the light all along it");

        // A scene of many casters: the fan stays sorted and every ray inside its reach.
        s.Clear();
        var rng = new Random(7);
        for (int i = 0; i < 60; i++) s.AddDisc(new V2(300f + (float)rng.NextDouble() * 400f, 300f + (float)rng.NextDouble() * 400f), 6f + (float)rng.NextDouble() * 14f);
        for (int i = 0; i < 20; i++) s.AddBox(new V2(200f + (float)rng.NextDouble() * 600f, 200f + (float)rng.NextDouble() * 600f), 10f + (float)rng.NextDouble() * 30f, 10f + (float)rng.NextDouble() * 30f);
        LightCast.Cast(s, at, 250f, fan);
        bool sorted = true, inside = true;
        for (int i = 0; i < fan.N; i++)
        {
            if (i > 0 && fan.Ang[i] < fan.Ang[i - 1]) sorted = false;
            if (fan.Dist[i] < 0f || fan.Dist[i] > 250.01f) inside = false;
        }
        Check(sorted && inside && fan.N > LightCast.RimRays, "a crowded scene: " + fan.N + " rays, sorted by angle, each within reach");
    }
}
