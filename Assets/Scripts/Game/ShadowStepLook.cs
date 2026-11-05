using UnityEngine;

namespace Arena
{
    /// The Ninja's Shadow Step with Visual effects on (Docs/Mockups/shadow-step.html, look 6 "Smoke Reform"). Drawing only; the step
    /// itself (reach, landing spot, 80 damage) is unchanged, and with effects off the plain ring and slash draw as before
    /// (`IsShadowStepPlain`).
    ///
    /// The host sends one FxKind.ShadowStep per step (Pos = where they stood, Pos2 = where they landed, Radius = their id * 1000
    /// + the radius of the enemy they struck, 0 for a short step with no enemy), lasting ShadowStepFxTime. The step is instant in
    /// the game, so the look is a cosmetic cover over it: the ninja is hidden (`StepHides`) while the smoke travels, then shows
    /// again at the landing spot:
    ///   0 s      the ninja bursts into 26 puffs of smoke where they stood, split into a left and a right half;
    ///   0.06 s   the halves stream away behind a pale eye each, bending round the two sides of the enemy;
    ///   0.24 s   they meet behind the enemy, flash and re-form into the ninja, and the strike lands (a crescent, a cut and sparks);
    ///   after    the smoke left at the start thins out.
    public partial class ArenaApp
    {
        const float StepWind = 0.06f, StepTravel = 0.18f, StepArrive = StepWind + StepTravel, StepSlash = 0.2f, StepShow = StepArrive + 0.12f;
        const int StepPuffs = 26;
        static readonly Rgba StepSmoke = new Rgba(0.52f, 0.43f, 0.74f), StepDeep = new Rgba(0.24f, 0.17f, 0.43f), StepViolet = new Rgba(0.72f, 0.58f, 1f), StepPale = new Rgba(0.96f, 0.92f, 1f);

        /// The plain ring and slash that the look replaces (Game.cs and Ninja.cs send them for Visual effects off).
        bool IsShadowStepPlain(Effect fx)
        {
            if (!fxOn)
                return false;
            if (fx.Kind == FxKind.Ring)
                return Mathf.Abs(fx.Radius - 26f) < 0.01f && Mathf.Abs(fx.Dur - 0.3f) < 0.005f && SameRgb(fx.Color, new Rgba(0.45f, 0.4f, 0.65f));
            return fx.Kind == FxKind.Slash && Mathf.Abs(fx.Radius - 0.12f) < 0.001f && Mathf.Abs(fx.Dur - 0.2f) < 0.005f && SameRgb(fx.Color, new Rgba(0.75f, 0.6f, 1f));
        }

        /// The ninja isn't drawn while their smoke is still on its way (the step already moved them to the landing spot).
        bool StepHides(Player p)
        {
            if (!fxOn || !ReferenceEquals(game, session.Game))
                return false;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.ShadowStep && fx.T < StepShow && (int)(fx.Radius / 1000f + 0.001f) == p.Id)
                    return true;
            return false;
        }

        static float StepEase(float x)
        {
            x = Mathf.Clamp01(x);
            return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
        }

        /// How far out to the side, 0 to 1, a puff is at a fraction of the way along the route: it swings out over the 170 px before the
        /// enemy, is level with it at its flank and comes back in over the gap behind it.
        static float StepLateral(float f, float len, float flank)
        {
            float s = f * len, rise = Mathf.Clamp01((s - (flank - 170f)) / 170f), fall = Mathf.Clamp01((s - flank) / Mathf.Max(1f, len - flank));
            return Mathf.Sin(Mathf.PI / 2f * rise) * Mathf.Cos(Mathf.PI / 2f * fall);
        }

        /// A soft puff, stretched along `dir`: three glow discs in a row.
        void StepPuff(V2 c, float r, float stretch, V2 dir, Rgba col, float a)
        {
            if (a <= 0.01f || r <= 0.5f)
                return;
            for (int k = -1; k <= 1; k++)
                GlowDisc(c + dir * (k * r * stretch * 0.45f), r * 1.5f * (k == 0 ? 1f : 0.8f), col, a * (k == 0 ? 1f : 0.7f));
        }

        /// WorldEffects.cs: FxKind.ShadowStep.
        void DrawShadowStepFx(Effect fx)
        {
            if (!fxOn)
                return;
            float a = fx.T;
            V2 dep = fx.Pos, arr = fx.Pos2, off = arr - dep;
            int owner = (int)(fx.Radius / 1000f + 0.001f);
            float er = fx.Radius - owner * 1000f, br = 14f;
            Player pl;
            if (game.Players.TryGetValue(owner, out pl))
                br = pl.Def.BodyR;
            float k = br / 14f, len = Mathf.Max(1f, off.Length);
            V2 dir = off / len, perp = new V2(-dir.Y, dir.X);
            bool hit = er > 0f;
            float gap = er + br + 8f, flank = hit ? len - gap : len * 0.55f, swing = hit ? er + br + 24f : 38f * k;

            // The smoke left at the start, thinning out.
            for (int i = 0; i < 5; i++)
            {
                float age = a - StepWind - 0.02f * i;
                if (age < 0f)
                    continue;
                float life = Mathf.Clamp01(1f - age / 0.8f), grow = EaseOut3(Mathf.Clamp01(age / 0.6f));
                V2 at = dep + new V2((Hash01(i) - 0.5f) * 30f * k, (Hash01(i * 3f) - 0.5f) * 14f * k - age * 16f);
                GlowDisc(at, (12f + 20f * grow) * k, StepSmoke, 0.5f * life);
            }
            // The burst that opens the step: a white puff and a ring.
            float burst = Mathf.Clamp01(1f - a / 0.2f);
            GlowDisc(dep, 60f * k * burst, StepPale, 0.5f * burst);
            if (a < 0.35f)
                Arc(dep, (10f + 52f * EaseOut3(a / 0.35f)) * k, 2.5f * (1f - a / 0.35f) + 0.5f, StepViolet.WithAlpha(0.7f * (1f - a / 0.35f)), 40);

            for (int i = 0; i < StepPuffs; i++)
            {
                float an = Hash01(i * 2.3f) * MathX.Tau, rr = Mathf.Sqrt(Hash01(i * 5.1f)) * (br - 3f);
                V2 o = new V2(Mathf.Cos(an) * rr, Mathf.Sin(an) * rr);
                float side = o.X * perp.X + o.Y * perp.Y >= 0f ? 1f : -1f;
                float j = Hash01(i * 3.1f), scatter = (Hash01(i * 7.7f) - 0.5f) * 26f * k;
                float delay = StepWind * 0.5f + j * 0.05f, dur = StepTravel + 0.04f + j * 0.1f, r0 = (6f + 5f * Hash01(i * 9.1f)) * k;
                float f = StepEase((a - delay) / dur);
                float lateral = side * swing * (0.8f + 0.4f * Hash01(i * 4.4f));
                if (a < delay)  // the burst, drifting apart
                {
                    float u = EaseOut3(Mathf.Clamp01(a / StepWind));
                    StepPuff(dep + o * (1f + 0.7f * u) + perp * (side * 7f * u * k) + new V2(0f, -4f * u), r0 * (0.8f + 0.5f * u), 0f, dir, StepSmoke, 0.95f);
                }
                else if (f < 1f)
                {
                    float sh = Mathf.Sin(Mathf.PI * f);
                    V2 head = StepAt(dep, off, perp, o, f, scatter, lateral, len, flank);
                    for (int t = 4; t >= 1; t--)  // the wispy tail
                    {
                        float u = 1f - t / 5f;
                        StepPuff(StepAt(dep, off, perp, o, Mathf.Max(0f, f - t * 0.06f), scatter, lateral, len, flank), r0 * (0.4f + 0.5f * u), 0f, dir, StepDeep, 0.5f * u * sh);
                    }
                    StepPuff(head, r0 * (1f + 0.8f * sh), 1.6f * sh, dir, StepSmoke, 0.95f);
                }
                else
                {
                    float u = Mathf.Clamp01((a - delay - dur) / 0.2f), pull = 1f - EaseOut3(u);
                    V2 end = StepAt(dep, off, perp, o, 1f, scatter, lateral, len, flank);
                    StepPuff(arr + (end - arr) * (0.4f + 0.6f * pull), r0 * (1f + 0.5f * pull), 0f, dir, StepSmoke, 0.95f * (1f - u * u));
                }
            }

            // The two eyes, one at the head of each half.
            if (a >= StepWind * 0.3f && a < StepShow)
            {
                float f = a < StepWind ? 0f : StepEase((a - StepWind) / StepTravel);
                V2 p = dep + off * f + dir * (5.5f * k);
                float fade = a > StepArrive + 0.04f ? Mathf.Clamp01(1f - (a - StepArrive - 0.04f) / 0.08f) : 1f;
                for (int s = -1; s <= 1; s += 2)
                {
                    V2 e = p + perp * (s * (3.2f * k + swing * StepLateral(f, len, flank)));
                    if (f > 0f && f < 1f)
                        Line(e - dir * 46f, e, 2f, StepPale.WithAlpha(0.7f));
                    GlowDisc(e, 10f, StepViolet, 0.9f * fade);
                    Circle(e, 2f * k, StepPale.WithAlpha(fade), 6);
                }
            }

            // The meeting: a flash and a ring where the ninja re-forms.
            float m = a - StepArrive;
            if (m >= 0f && m < 0.32f)
            {
                float u = m / 0.32f;
                GlowDisc(arr, 55f * k * (1f - u), StepViolet, 0.4f * (1f - u));
                Arc(arr, (12f + 42f * EaseOut3(u)) * k, 3f * (1f - u) + 0.5f, StepViolet.WithAlpha(0.8f * (1f - u)), 40);
            }

            // The strike (replaces the plain slash).
            if (hit && a >= StepArrive && a < StepArrive + 0.45f)
            {
                float age = a - StepArrive, u = Mathf.Clamp01(age / StepSlash);
                V2 e = arr + dir * gap;  // the enemy: the landing spot is `gap` behind it, on the line from where they stood
                float mid = Mathf.Atan2(dir.Y, dir.X);
                if (age < StepSlash)
                {
                    float r = er + 15f + 12f * u, a0 = mid - 1.05f + u * 0.6f;
                    Arc(e, r, 15f * (1f - u) + 2f, StepViolet.WithAlpha(0.55f * (1f - u)), 28, a0, 2.1f);
                    Arc(e, r, 5f * (1f - u) + 1f, StepPale.WithAlpha(0.95f * (1f - u)), 28, a0 + 0.1f, 1.9f);
                    V2 cut = V2.FromAngle(mid + Mathf.PI / 2f + 0.5f) * (46f * EaseOut3(Mathf.Clamp01(u * 2.4f)));
                    Line(e - cut, e + cut, 2f, Rgba.White.WithAlpha(0.9f * (1f - u)));
                }
                for (int i = 0; i < 9; i++)
                {
                    float sa = mid + (Hash01(i) - 0.5f) * 2.6f, sd = (er + 4f + 40f * Hash01(i * 3f + 1f)) * EaseOut3(Mathf.Clamp01(age / 0.25f));
                    float life = Mathf.Clamp01(1f - age / (0.2f + 0.2f * Hash01(i * 5f)));
                    V2 sp = e + V2.FromAngle(sa) * sd;
                    Line(sp, sp + V2.FromAngle(sa) * (7f * life), 1.6f, StepPale.WithAlpha(life));
                }
            }
        }

        /// Where a puff is, a fraction `f` of the way along the route: its place in the body, a little scatter, and its half's swing round the enemy.
        static V2 StepAt(V2 dep, V2 off, V2 perp, V2 o, float f, float scatter, float lateral, float len, float flank)
        {
            float sc = 1f + 1.3f * (1f - f * f) * (1f - Mathf.Max(0f, f - 0.8f) * 3f);
            float s = Mathf.Sin(Mathf.PI * f) * scatter + lateral * StepLateral(f, len, flank);
            return dep + off * f + o * sc + perp * s;
        }
    }
}
