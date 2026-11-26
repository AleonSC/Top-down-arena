using UnityEngine;

namespace Arena
{
    /// How Honey Raid looks (the Healer Summoner's second ultimate, Core/HoneyRaid.cs), from the Honey Raid mockup. Everything is
    /// drawn from the replicated zone (`ZoneKind.HoneyRaid`: Pos the pool, Origin where the bee came in, Radius, T, Duration):
    ///   - on the floor (DrawHoneyRaidFloor): a dashed gold ring where it will land and the bee's shadow sweeping in; then the pool:
    ///     a lumpy glossy honey pool with wax-comb cells, a sliding shine, bubbles, the splash's shockwave and droplets, the enemies
    ///     wading in it collared and dripping, and its time as a gold arc round the rim; in its last HrSet s it sets, paling to wax
    ///     with cracks.
    ///   - over the arena (DrawHoneyRaids): the giant bee flying over with its honey pot, and the glob falling to the spot.
    /// (The drips on the way are ordinary honey puddles: HoneybeeLook.cs.)
    public partial class ArenaApp
    {
        const float HrHover = 90f;  // how high the bee flies over its shadow
        static readonly Rgba HrDeep = new Rgba(0.67f, 0.37f, 0.03f), HrWax = new Rgba(1f, 0.92f, 0.68f);

        /// The pool's lumpy edge at angle a.
        static float HrEdge(float a, float seed, float t) => 0.9f + 0.07f * Mathf.Sin(a * 5f + seed) + 0.04f * Mathf.Sin(a * 3f - t * 1.2f + seed);

        void HrFan(V2 c, float r, float seed, float t, Rgba col)
        {
            const int n = 32;
            V2 prev = default(V2);
            SetColor(col);
            for (int i = 0; i <= n; i++)
            {
                float a = MathX.Tau * i / n, rr = r * HrEdge(a, seed, t);
                V2 p = c + new V2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.92f);
                if (i > 0)
                    Tri(c, prev, p);
                prev = p;
            }
        }

        void DrawHoneyRaidFloor(Zone z, float t)
        {
            float age = Game.HrAge(z), R = z.Radius, seed = z.Id * 1.37f;
            if (!Game.HrLanded(z))
            {
                float k = Mathf.Clamp01(age / Game.HrFly);
                Rgba gold = HoneyLight.WithAlpha(0.35f + 0.45f * k);
                for (int i = 0; i < 28; i++)  // where it will land
                    Arc(z.Pos, R, 2f, gold, 3, i * MathX.Tau / 28f + t * 0.3f, MathX.Tau / 56f);
                Circle(z.Pos, 4f, gold, 8);
                if (age < Game.HrFly + Game.HrLeave)  // the bee's shadow
                {
                    V2 g = Game.HrBeeAt(z, age);
                    Ellipse(g, 70f, 40f, Game.HrHeading(z), new Rgba(0f, 0f, 0f, 0.28f), 16);
                }
                if (age >= Game.HrFly)  // the glob's shadow, growing as it falls
                {
                    float f = Mathf.Clamp01((age - Game.HrFly) / Game.HrDrop);
                    Ellipse(z.Pos, 24f + 40f * f, 16f + 28f * f, 0f, new Rgba(0f, 0f, 0f, 0.3f), 16);
                }
                return;
            }
            float la = age - Game.HrLandAt, reach = Game.HrReach(z), fade = Mathf.Clamp01(z.T / 0.6f);
            float set = Game.HrSetting(z) ? 1f - z.T / Game.HrSet : 0f;
            if (!fxOn)
            {
                Circle(z.Pos, reach, HoneyAmber.WithAlpha(0.5f * fade), 28);
                Arc(z.Pos, reach + 8f, 3f, BeeGold.WithAlpha(0.9f * fade), 40, -MathX.Pi * 0.5f, MathX.Tau * Mathf.Clamp01(z.T / Game.HrPool));
                return;
            }
            Rgba deep = HrDeep.Mix(HrWax, set * 0.6f), mid = HoneyAmber.Mix(HrWax, set * 0.7f), light = HoneyLight.Mix(HrWax, set);
            HrFan(z.Pos + new V2(0f, 6f), reach * 1.02f, seed, t, new Rgba(0f, 0f, 0f, 0.25f * fade));  // its shadow lip
            HrFan(z.Pos, reach, seed, t, deep.WithAlpha(0.85f * fade));
            HrFan(z.Pos + new V2(-reach * 0.05f, -reach * 0.05f), reach * 0.86f, seed + 1f, t, mid.WithAlpha(0.85f * fade));
            HrFan(z.Pos + new V2(-reach * 0.16f, -reach * 0.16f), reach * 0.5f, seed + 2f, t, light.WithAlpha(0.45f * fade));
            float cell = 22f;  // wax comb in the honey
            for (int row = -12; row <= 12; row++)
                for (int col = -12; col <= 12; col++)
                {
                    V2 c = z.Pos + new V2(col * cell * 1.5f, row * cell * 1.732f + (col & 1) * cell * 0.866f);
                    if (c.FartherThan(z.Pos, reach * 0.7f))
                        continue;
                    HexOutline(c, cell * 0.92f, 1.5f, HoneyShine.WithAlpha((0.14f + 0.2f * set) * fade));
                }
            float sh = Mathf.Sin(t * 1.6f + seed) * reach * 0.08f;  // the shine, sliding a little
            Ellipse(z.Pos + new V2(-reach * 0.32f + sh, -reach * 0.36f), reach * 0.3f, reach * 0.08f, -0.45f, HoneyShine.WithAlpha(0.55f * fade * (1f - set * 0.6f)), 14);
            if (set <= 0f)
                for (int i = 0; i < 6; i++)  // bubbles rising and popping
                {
                    float ph = (t * 0.6f + i * 0.29f + seed * 0.11f) % 1f, a = BeeHash(seed + i + Mathf.Floor(t * 0.6f + i * 0.29f + seed * 0.11f)) * MathX.Tau;
                    V2 at = z.Pos + V2.FromAngle(a) * (reach * 0.7f * BeeHash(a + 3f));
                    Arc(at, 2f + 6f * ph, 1.5f, HoneyShine.WithAlpha(0.8f * (1f - ph) * fade), 12);
                }
            else
                for (int i = 0; i < 7; i++)  // setting: cracks running in from the rim
                {
                    float a = seed + i * 0.9f, len = reach * 0.45f * Mathf.Clamp01(set * 1.6f);
                    V2 o = z.Pos + V2.FromAngle(a) * reach * 0.9f, m = o - V2.FromAngle(a + 0.25f) * len * 0.5f, e = o - V2.FromAngle(a - 0.15f) * len;
                    Line(o, m, 2f, HrDeep.WithAlpha(0.7f * fade));
                    Line(m, e, 1.5f, HrDeep.WithAlpha(0.6f * fade));
                }
            Arc(z.Pos, reach * 0.98f, 3f, HrDeep.Mix(HoneyShine, set).WithAlpha(0.8f * fade), 48);
            if (la < 0.5f)  // the splash: a shockwave and droplets thrown out
            {
                float k = la / 0.5f;
                Arc(z.Pos, R * (0.5f + 0.9f * k), 8f * (1f - k), HoneyLight.WithAlpha(1f - k), 48);
            }
            for (int i = 0; i < 12; i++)  // the droplets that landed round the rim
            {
                float a = seed * 3f + i * 0.53f + BeeHash(i + seed) * 0.4f, d = R * (1.02f + 0.18f * BeeHash(i * 2f + seed)) * Mathf.Clamp01(la / 0.25f);
                Circle(z.Pos + V2.FromAngle(a) * d, 4f + 5f * BeeHash(i + 9f), mid.WithAlpha(0.8f * fade), 10);
            }
            foreach (var e in game.Enemies.Values)  // stuck in it: a honey collar and drips
            {
                if (e.Disp.FartherThan(z.Pos, reach + e.Radius * 0.5f))
                    continue;
                Arc(e.Disp + new V2(0f, e.Radius * 0.3f), e.Radius * 0.95f, 3f, mid.WithAlpha(0.85f * fade), 16, 0.2f, MathX.Pi - 0.4f);
                float d = (t * 1.6f + e.Id * 0.31f) % 1f;
                Circle(e.Disp + new V2(e.Radius * 0.45f, e.Radius * 0.55f + 10f * d), 2.2f * (1f - d * 0.5f), mid.WithAlpha(fade * (1f - d)), 6);
            }
            Arc(z.Pos, R + 12f, 3f, BeeGold.WithAlpha(0.85f * fade), 64, -MathX.Pi * 0.5f, MathX.Tau * Mathf.Clamp01(z.T / Game.HrPool));  // time left
        }

        /// Over the arena: each Honey Raid's bee flying over, and its glob falling.
        void DrawHoneyRaids(float t)
        {
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.HoneyRaid)
                    continue;
                float age = Game.HrAge(z);
                if (age < Game.HrFly + Game.HrLeave)
                    DrawGiantBee(z, age, t);
                if (age >= Game.HrFly && age < Game.HrLandAt)
                {
                    float f = Mathf.Clamp01((age - Game.HrFly) / Game.HrDrop), e = f * f;
                    V2 at = z.Pos - new V2(0f, HrHover * (1f - e));
                    Ellipse(at, 26f - 6f * e, 30f + 8f * e, 0f, HoneyAmber, 16);
                    Ellipse(at + new V2(-7f, -9f), 7f, 3f, -0.5f, HoneyShine.WithAlpha(0.8f), 8);
                }
            }
        }

        void DrawGiantBee(Zone z, float age, float t)
        {
            float aim = Game.HrHeading(z), alpha = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((Game.HrFly + Game.HrLeave - age) / 0.3f);
            V2 at = Game.HrBeeAt(z, age) - new V2(0f, HrHover + Mathf.Sin(t * 6f) * 4f);
            bool full = age < Game.HrFly;
            if (full && fxOn)  // the honey pot it carries, under its body
            {
                Circle(at + new V2(0f, 18f), 20f, HrDeep.WithAlpha(alpha), 16);
                Circle(at + new V2(-5f, 12f), 8f, HoneyLight.WithAlpha(0.7f * alpha), 10);
            }
            DrawBeeBody(at, aim, 11f, t, z.Id, alpha);
            if (fxOn)
            {
                V2.Axes(aim, out V2 f, out V2 s);
                Circle(at + f * 58f + s * 11f, 4f, new Rgba(1f, 0.54f, 0.16f, alpha), 8);  // its eyes
                Circle(at + f * 58f - s * 11f, 4f, new Rgba(1f, 0.54f, 0.16f, alpha), 8);
            }
            if (full && fxOn && (t * 5f + z.Id) % 1f < 0.5f)  // a drip on its way down
                Circle(at + new V2(0f, 44f + 30f * ((t * 5f) % 0.5f)), 5f, HoneyAmber.WithAlpha(alpha), 8);
        }
    }
}
