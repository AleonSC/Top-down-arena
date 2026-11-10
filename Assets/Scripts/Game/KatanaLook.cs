using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Katana's two stance combos (Core/Katana.cs; mockup Docs/Mockups/katana-stance-cancels.html), drawn as the mockup draws
    /// them, with the same shapes and numbers: Hayate moon steel, Senbon rose.
    ///   - The katana itself, in both hands: a curved blade with a white edge and a temper line, a gold habaki and tsuba, a
    ///     dark wrapped grip, held low and trailing in Hayate and forward with its edge lit in Senbon, following each step (the
    ///     dash's line, the sweep's head, a slash) and stabbed into the ground for the Momentum Burst.
    ///   - Each dash (FxKind.KatanaDash): a spear of light from where it began, afterimages along it.
    ///   - Each sweep or spin (KatanaSweep): a crescent thick at the blade, eaten from its tail, with a white edge.
    ///   - Senbon's slashes (KatanaSlash): thin rose lenses.
    ///   - Every enemy cut (KatanaCut): a white line across it, sparks; if it died, its two halves slide apart in its own colour
    ///     and shape.
    ///   - The Momentum Burst (KatanaBurst): rings out to its reach, cracks from the blade, a flash of light; the enemies it
    ///     marked carry a turning dashed steel ring.
    ///   - Caltrops (Defs.Caltrop): small three-spiked stars with a rose glint, arcing out when thrown, trailing when they fly.
    ///   - The combo pips over the swordsman: Hayate's four diamonds, Senbon's five ticks, the next step white, the stance you're
    ///     in bright and the stored one dim.
    /// Lights go in the additive pass (DrawKatanaGlow, from Juice.cs); solid things in the world pass.
    public partial class ArenaApp
    {
        static readonly Rgba KatanaBladeSteel = new Rgba(0.87f, 0.91f, 0.96f), KatanaBack = new Rgba(0.55f, 0.6f, 0.68f), KatanaGoldC = new Rgba(0.91f, 0.76f, 0.48f);
        static readonly Rgba KatanaWrap = new Rgba(0.13f, 0.1f, 0.14f), KatanaWrapTie = new Rgba(1f, 0.44f, 0.57f);
        readonly Dictionary<int, float> katanaBladeAng = new Dictionary<int, float>();

        static Rgba KatanaStanceCol(int stance) => stance == 0 ? Game.KatanaSteel : Game.KatanaRose;
        static V2 Vector2Lerp(V2 a, V2 b, float t) => a + (b - a) * t;  // (JuggernautLook.cs uses it too)

        // --- The katana, in hand ---------------------------------------------------------------------------------------

        /// Where this swordsman's blade points now: along a dash, at a sweep's head, along a slash, planted for the Burst, or
        /// easing back to the stance's guard. `len` is how far it reaches.
        bool KatanaBladeNow(Player p, out float ang, out float len, out bool lit, out bool planted)
        {
            planted = false;
            lit = p.KatanaStance == 1;
            len = 40f;
            float rest = p.Aim + (p.KatanaStance == 0 ? 2.35f : 0.3f), target = rest, ease = 14f;
            foreach (var fx in game.Effects)
            {
                switch (fx.Kind)
                {
                    case FxKind.KatanaDash:
                        if ((int)fx.Radius == p.Id && fx.T < Game.KatanaDashTime + 0.06f && fx.Pos.DistanceTo(fx.Pos2) > 1f)
                        {
                            target = (fx.Pos2 - fx.Pos).Angle;
                            ease = 60f; lit = true; len = 46f;
                        }
                        break;
                    case FxKind.KatanaSweep:
                        if ((int)(fx.Radius / 1000f) == p.Id && fx.T < fx.Dur + 0.05f)
                        {
                            float k = EaseOut3(Mathf.Clamp01(fx.T / fx.Dur));
                            target = fx.Pos2.X + fx.Pos2.Y * k;
                            ease = 80f; lit = true; len = 46f;
                        }
                        break;
                    case FxKind.KatanaSlash:
                        if ((int)fx.Radius == p.Id && fx.T < 0.06f)
                        {
                            target = (Vector2Lerp(fx.Pos, fx.Pos2, 0.5f) - p.Disp).Angle;
                            ease = 60f; lit = true; len = 46f;
                        }
                        break;
                    case FxKind.KatanaBurst:
                        if ((int)fx.Pos2.X == p.Id && fx.T < Game.KatanaPlant + 0.1f)
                            planted = true;
                        break;
                }
            }
            float cur;
            if (!katanaBladeAng.TryGetValue(p.Id, out cur))
                cur = rest;
            cur += (MathX.Fmod(target - cur + MathX.Pi, MathX.Tau) - MathX.Pi) * Mathf.Min(1f, Time.deltaTime * ease);
            katanaBladeAng[p.Id] = cur;
            ang = cur;
            return true;
        }

        /// The Katana in hand, in place of the model's own blade (CharacterModels.cs leaves its arms out).
        void DrawKatanaIdle(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.Katana || !fxOn || p.Downed)
                return;
            float ang, len;
            bool lit, planted;
            KatanaBladeNow(p, out ang, out len, out lit, out planted);
            var col = KatanaStanceCol(p.KatanaStance);
            if (planted)
            {
                // Stabbed into the ground in front of you: the blade seen from above, short, and a crack at its point.
                V2 at = p.Disp + V2.FromAngle(p.Aim) * 8f;
                DrawKatanaArms(p, at - V2.FromAngle(p.Aim) * 2f, p.Aim);
                DrawKatanaModel(at - V2.FromAngle(p.Aim) * 10f, p.Aim, 18f, col, true, 0f);
                Circle(at + V2.FromAngle(p.Aim) * 12f, 3f, Rgba.White, 10);
                return;
            }
            V2 dir = V2.FromAngle(ang);
            V2 grip = p.Disp + dir * 4f;
            DrawKatanaArms(p, grip + dir * 8f, ang);
            DrawKatanaModel(grip, ang, len, col, lit, p.KatanaStance == 0 ? 0.4f : -0.4f);
        }

        /// Both arms from the shoulders to the grip, the lead hand ahead of the rear one.
        void DrawKatanaArms(Player p, V2 hand, float ang)
        {
            V2 o = p.Disp, side = V2.FromAngle(p.Aim).Orthogonal();
            V2 dir = V2.FromAngle(ang);
            Rgba arm = ArmColor(p.Id);
            DrawMeleeArms(o, side, hand, hand - dir * 7f, arm, 1f);
            DrawMeleeGloves(hand, hand - dir * 7f, new Rgba(0.04f, 0.04f, 0.06f), new Rgba(0.16f, 0.17f, 0.22f));
        }

        /// The katana: kashira, a wrapped grip with its rose ties, the tsuba, a gold habaki, and a curved blade (sori) with its
        /// back in shadow, a white cutting edge, a soft temper line and a sharp kissaki. `bend` curves it toward one side; `lit`
        /// lays the stance's light along the edge.
        void DrawKatanaModel(V2 o, float ang, float len, Rgba col, bool lit, float bend)
        {
            V2 dir = V2.FromAngle(ang), side = dir.Orthogonal();
            // Grip: 14 px, dark, with diamond ties.
            V2 g0 = o - dir * 6f, g1 = o + dir * 8f;
            Line(g0, g1, 5f, KatanaWrap);
            for (int i = 0; i < 4; i++)
            {
                V2 c = Vector2Lerp(g0, g1, (i + 0.5f) / 4f);
                SetColor(KatanaWrapTie);
                Quad(c + dir * 1.6f, c + side * 1.6f, c - dir * 1.6f, c - side * 1.6f);
            }
            Circle(g0 - dir * 1f, 2.4f, KatanaGoldC, 8);
            // Tsuba: a dark guard with a gold rim; habaki: a gold collar.
            Line(g1 - side * 5.5f, g1 + side * 5.5f, 3.6f, KatanaGoldC);
            Line(g1 - side * 4.2f, g1 + side * 4.2f, 1.8f, new Rgba(0.12f, 0.1f, 0.08f));
            SetColor(KatanaGoldC);
            Quad(g1 + dir * 1.5f + side * 1.8f, g1 + dir * 4.5f + side * 1.6f, g1 + dir * 4.5f - side * 1.6f, g1 + dir * 1.5f - side * 1.8f);
            // Blade: from the habaki to the point, curved by `bend`.
            const int n = 9;
            float bl = Mathf.Max(8f, len - 4f);
            V2 b0 = g1 + dir * 4.5f;
            var spine = new V2[n + 1];
            var edgeP = new V2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n, sori = bend * 4.5f * u * u, w = Mathf.Lerp(3.3f, 2.2f, u);
                V2 c = b0 + dir * (bl * u) + side * sori;
                if (i == n)
                    w = 0f;
                spine[i] = c - side * (w * 0.5f);
                edgeP[i] = c + side * (w * 0.5f);
            }
            V2 tip = b0 + dir * (bl + 5f) + side * (bend * 4.5f);
            for (int i = 0; i < n; i++)
            {
                SetColor(KatanaBladeSteel);
                Quad(spine[i], spine[i + 1], edgeP[i + 1], edgeP[i]);
                Line(spine[i], spine[i + 1], 1.1f, KatanaBack);  // the back in shadow
            }
            SetColor(KatanaBladeSteel);
            Tri(spine[n], edgeP[n], tip);  // kissaki
            for (int i = 0; i < n; i++)
                Line(edgeP[i], edgeP[i + 1], 1.1f, Rgba.White);  // the cutting edge
            Line(edgeP[n], tip, 1.1f, Rgba.White);
            for (int i = 1; i < n - 1; i++)  // the temper line, a soft wave just inside the edge
            {
                V2 a = Vector2Lerp(spine[i], edgeP[i], 0.62f + 0.12f * Mathf.Sin(i * 2.1f)), b = Vector2Lerp(spine[i + 1], edgeP[i + 1], 0.62f + 0.12f * Mathf.Sin((i + 1) * 2.1f));
                Line(a, b, 0.7f, new Rgba(0.75f, 0.86f, 1f, 0.8f));
            }
            if (lit)
                SoftLine(edgeP[0], tip, 8f, col.WithAlpha(0.45f));
        }

        // --- The combo pips --------------------------------------------------------------------------------------------

        /// Over the swordsman: Hayate's four diamonds (higher) and Senbon's five ticks; the next step white, the ones done in the
        /// stance's colour; the stance you're in bright, the stored one dim.
        void DrawKatanaHud(Player p, float t)
        {
            if (p.Downed)
                return;
            for (int c = 0; c <= 1; c++)
            {
                int steps = Game.KatanaSteps(c), next = c == 0 ? p.KatanaHay : p.KatanaSen;
                bool on = p.KatanaStance == c;
                float y = p.Disp.Y - p.Def.BodyR - (c == 0 ? 30f : 22f), a = on ? 1f : 0.35f;
                var col = KatanaStanceCol(c);
                for (int i = 0; i < steps; i++)
                {
                    float x = p.Disp.X + (i - (steps - 1) / 2f) * 9f;
                    Rgba fill = i == next ? Rgba.White : i < next ? col : new Rgba(0.51f, 0.55f, 0.65f, 0.4f);
                    SetColor(fill.WithAlpha(fill.A * a));
                    V2 m = new V2(x, y);
                    if (c == 0)
                        Quad(m + new V2(0f, -3.5f), m + new V2(3.5f, 0f), m + new V2(0f, 3.5f), m + new V2(-3.5f, 0f));
                    else
                        Quad(m + new V2(-1.2f, -4f), m + new V2(1.2f, -4f), m + new V2(1.2f, 4f), m + new V2(-1.2f, 4f));
                }
            }
        }

        // --- The world pass ------------------------------------------------------------------------------------------------

        /// The enemies a Momentum Burst marked (a turning dashed steel ring), and each Burst's cracks in the ground.
        void DrawKatanaMarks(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (!e.KatanaShocked || e.Hp <= 0f)
                    continue;
                for (int k = 0; k < 8; k++)
                    Arc(e.Disp, e.Radius + 6f, 1.6f, Game.KatanaSteel.WithAlpha(0.75f), 4, t * 2f + k * MathX.Tau / 8f, MathX.Tau / 16f);
            }
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.KatanaBurst)
                    continue;
                float a = 1f - Mathf.Clamp01(fx.T / fx.Dur);
                for (int k = 0; k < 7; k++)
                {
                    float an = fx.Pos2.Y + k * MathX.Tau / 7f + Mathf.Sin(k * 3.1f) * 0.3f;
                    V2 prev = fx.Pos;
                    for (int j = 1; j <= 4; j++)
                    {
                        float aj = an + Mathf.Sin(j * 1.7f + k) * 0.25f;
                        V2 pt = fx.Pos + V2.FromAngle(aj) * (j * 16f);
                        Line(prev, pt, 2f * a + 0.5f, Game.KatanaSteel.WithAlpha(0.8f * a));
                        prev = pt;
                    }
                }
            }
        }

        /// One step's afterimages along its dash (the world pass): rings fading behind the head.
        void DrawKatanaDashFx(Effect fx)
        {
            float dur = Game.KatanaDashTime, age = fx.T, head = EaseOut3(Mathf.Clamp01(age / dur));
            if (age > 0.3f)
                return;
            for (int i = 1; i <= 4; i++)
            {
                float u = head - i * 0.2f;
                if (u < 0f)
                    continue;
                float a = 0.45f * (1f - age / 0.3f) * (1f - i * 0.18f);
                V2 c = Vector2Lerp(fx.Pos, fx.Pos2, u);
                Circle(c, 13f, fx.Color.WithAlpha(a * 0.25f), 16);
                Arc(c, 13f, 1.4f, fx.Color.WithAlpha(a), 18);
            }
        }

        /// One enemy cut (the world pass): if it died, its two halves sliding apart in its own colour and shape.
        void DrawKatanaCutFx(Effect fx)
        {
            if (fx.Color.A < 0.75f)
                return;
            float age = fx.T, q = Mathf.Clamp01(age / fx.Dur);
            int sides = Mathf.Max(3, Mathf.RoundToInt(fx.Pos2.Length));
            V2 dir = fx.Pos2.Normalized(), n = dir.Orthogonal();
            float off = 80f * (1f - Mathf.Pow(0.05f, age)) / 3f, alpha = Mathf.Clamp01((1f - q) * 1.6f);
            var col = fx.Color.WithAlpha(1f);
            for (int s = -1; s <= 1; s += 2)
            {
                float spin = s * (1.2f + 1.4f * Hash01(fx.Pos.X * 0.13f + s)) * (1f - Mathf.Pow(0.05f, age)) / 3f;
                V2 c = fx.Pos + n * (off * s) + dir * (14f * age);
                KatanaHalfPoly(c, fx.Radius, sides, dir.Angle + spin, s, col, alpha);
            }
        }

        /// Half an enemy polygon (the game's Polygon: darkened fill, a 2.5 px outline), cut along `cutAng` through its middle,
        /// the side `s` keeps, with the cut edge white.
        void KatanaHalfPoly(V2 c, float r, int sides, float cutAng, int s, Rgba col, float alpha)
        {
            V2 d = V2.FromAngle(cutAng), nrm = d.Orthogonal() * s;
            var pts = new List<V2>();
            var poly = new V2[sides];
            for (int i = 0; i < sides; i++)
                poly[i] = c + V2.FromAngle(cutAng * 0.5f + MathX.Tau * i / sides) * r;
            for (int i = 0; i < sides; i++)
            {
                V2 a = poly[i], b = poly[(i + 1) % sides];
                float da = Vector2DotK(a - c, nrm), db = Vector2DotK(b - c, nrm);
                if (da >= 0f)
                    pts.Add(a);
                if (da >= 0f != db >= 0f)
                    pts.Add(a + (b - a) * (da / (da - db)));
            }
            if (pts.Count < 3)
                return;
            SetColor(col.Darkened(0.45f).WithAlpha(alpha));
            for (int i = 1; i < pts.Count - 1; i++)
                Tri(pts[0], pts[i], pts[i + 1]);
            for (int i = 0; i < pts.Count; i++)
                Line(pts[i], pts[(i + 1) % pts.Count], 2.5f, col.WithAlpha(alpha));
            Line(c - d * r, c + d * r, 2f, Rgba.White.WithAlpha(0.9f * alpha));
        }

        static float Vector2DotK(V2 a, V2 b) => a.X * b.X + a.Y * b.Y;

        /// The switch: a small ring in the new stance's colour.
        void DrawKatanaStanceFx(Effect fx)
        {
            float q = Mathf.Clamp01(fx.T / fx.Dur);
            Player p;
            V2 c = game.Players.TryGetValue((int)fx.Pos2.X, out p) ? p.Disp : fx.Pos;
            Arc(c, 12f + (fx.Radius - 12f) * EaseOut3(q), 2.2f * (1f - q) + 0.5f, fx.Color.WithAlpha(1f - q), 28);
        }

        /// A caltrop: three spikes and a rose glint; thrown ones arc up and down on the way out, flying ones trail.
        void DrawCaltrop(Bullet b, float t)
        {
            float age = b.Life0 - b.Life;
            bool thrown = b.Life0 > 4f && age < Game.KatanaToss + 0.02f && !b.Vel.IsZero, moving = !b.Vel.IsZero;
            float h = thrown ? Mathf.Sin(Mathf.Clamp01(age / Game.KatanaToss) * MathX.Pi) * 16f : 0f;
            V2 at = b.Pos + new V2(0f, -h);
            float rot = moving ? t * 14f + b.Id : b.Id * 1.7f;
            if (moving && !thrown)
                Line(at, at - b.Vel * 0.04f, 2f, Game.KatanaRose.WithAlpha(0.55f));
            if (h > 0.5f)
                Circle(b.Pos, 3.5f, new Rgba(0f, 0f, 0f, 0.3f), 8);
            for (int k = 0; k < 3; k++)
                Line(at, at + V2.FromAngle(rot + k * MathX.Tau / 3f) * 4.5f, 1.8f, new Rgba(0.81f, 0.84f, 0.9f));
            Circle(at, 1.6f, Game.KatanaRose.WithAlpha(0.9f), 8);
        }

        // --- The light pass (additive) -------------------------------------------------------------------------------------

        /// The lights: dashes' spears, sweeps' crescents, Senbon's slashes, the cuts' lines and sparks, the Burst's rings and
        /// flash, and the switch.
        void DrawKatanaGlow()
        {
            foreach (var fx in game.Effects)
            {
                switch (fx.Kind)
                {
                    case FxKind.KatanaDash:
                        KatanaSpearGlow(fx.Pos, fx.Pos2, fx.T, Game.KatanaDashTime, fx.Dur, fx.Color, 11f, false);
                        break;
                    case FxKind.KatanaSlash:
                        KatanaSpearGlow(fx.Pos, fx.Pos2, fx.T, 0.04f, fx.Dur, fx.Color, 7f, true);
                        break;
                    case FxKind.KatanaSweep:
                    {
                        int owner = (int)(fx.Radius / 1000f);
                        float reach = fx.Radius - owner * 1000f;
                        Player p;
                        V2 c = game.Players.TryGetValue(owner, out p) ? p.Disp : fx.Pos;
                        float life = fx.Dur + 0.2f, T = fx.T;
                        if (T > life)
                            break;
                        float hf = EaseOut3(Mathf.Clamp01(T / fx.Dur)), tf = EaseOut3(Mathf.Clamp01((T - fx.Dur * 0.45f) / (life - fx.Dur * 0.45f)));
                        float al = 1f - Mathf.Clamp01((T - fx.Dur) / (life - fx.Dur)) * 0.5f, arc = fx.Pos2.Y, a0 = fx.Pos2.X;
                        float w = reach * (Mathf.Abs(arc) > 6f ? 0.23f : 0.2f);
                        KatanaArcFill(c, reach, a0 + arc * tf, a0 + arc * hf, w * (1f - tf * 0.6f), al, fx.Color.WithAlpha(1f));
                        break;
                    }
                    case FxKind.KatanaCut:
                    {
                        float age = fx.T, life = fx.Color.A > 0.75f ? 0.3f : 0.22f;
                        if (age > 0.32f)
                            break;
                        V2 dir = fx.Pos2.Normalized();
                        bool died = fx.Color.A > 0.75f;
                        float a = Mathf.Clamp01(1f - age / life), L = fx.Radius * (died ? 6.5f : 2.8f) * (0.6f + 0.4f * EaseOut3(Mathf.Clamp01(age / 0.05f)));
                        if (a > 0f)
                        {
                            SoftLine(fx.Pos - dir * (L / 2f), fx.Pos + dir * (L / 2f), 10f * a + 2f, Game.KatanaRose.WithAlpha(0.6f * a));
                            Line(fx.Pos - dir * (L / 2f), fx.Pos + dir * (L / 2f), (died ? 3f : 2.4f) * a + 0.5f, Rgba.White.WithAlpha(a));
                        }
                        float sk = Mathf.Clamp01(1f - age / 0.3f);
                        for (int i = 0; i < (died ? 10 : 6); i++)  // sparks thrown along the cut
                        {
                            float side = (i & 1) == 0 ? 1f : -1f, an = dir.Angle + (side < 0f ? MathX.Pi : 0f) + (Hash01(fx.Pos.X + i * 3.7f) - 0.5f) * 1.1f;
                            float d = (140f + 280f * Hash01(fx.Pos.Y + i * 1.9f)) * (1f - Mathf.Pow(0.02f, age)) / 3.9f;
                            V2 at = fx.Pos + V2.FromAngle(an) * d;
                            if (sk > 0f)
                                Line(at, at - V2.FromAngle(an) * (10f * sk), 1.6f, ((i % 2) == 0 ? Rgba.White : Game.KatanaRose).WithAlpha(sk));
                        }
                        if (died && age < 0.3f)
                        {
                            float k = age / 0.3f;
                            Arc(fx.Pos, fx.Radius * (1f + 2f * EaseOut3(k)), 2f * (1f - k) + 0.5f, fx.Color.WithAlpha(1f - k), 20);
                            GlowDisc(fx.Pos, 46f, fx.Color.WithAlpha(1f), 0.7f * (1f - age / 0.25f));
                        }
                        break;
                    }
                    case FxKind.KatanaBurst:
                    {
                        float age = fx.T, R = fx.Radius;
                        if (age < 0.42f)
                        {
                            float k = EaseOut3(age / 0.42f);
                            Arc(fx.Pos, 16f + (R - 16f) * k, 5f * (1f - age / 0.42f) + 0.5f, fx.Color.WithAlpha(0.9f * (1f - age / 0.42f)), 48);
                        }
                        if (age < 0.3f)
                        {
                            float k = EaseOut3(age / 0.3f);
                            Arc(fx.Pos, 10f + (R * 0.6f - 10f) * k, 2.5f * (1f - age / 0.3f) + 0.5f, Rgba.White.WithAlpha(0.9f * (1f - age / 0.3f)), 40);
                            GlowDisc(fx.Pos, 90f, fx.Color.WithAlpha(1f), 0.7f * (1f - age / 0.3f));
                        }
                        break;
                    }
                    case FxKind.KatanaStanceFx:
                        if (fx.T < 0.2f)
                            GlowDisc(fx.Pos, 30f, fx.Color.WithAlpha(1f), 0.35f * (1f - fx.T / 0.2f));
                        break;
                }
            }
        }

        /// A spear of light from `a` toward `b`: its head runs out over `grow`, a soft glow, a tapered body (or a lens for a
        /// slash) and a white core, fading out by `life`.
        void KatanaSpearGlow(V2 a, V2 b, float T, float grow, float life, Rgba col, float w, bool lens)
        {
            if (T > life)
                return;
            float hf = EaseOut3(Mathf.Clamp01(T / grow)), al = 1f - Mathf.Clamp01((T - grow) / Mathf.Max(0.01f, life - grow));
            V2 head = Vector2Lerp(a, b, hf), d = head - a;
            float L = d.Length;
            if (L < 1f || al <= 0f)
                return;
            V2 u = d / L, n = u.Orthogonal();
            col = col.WithAlpha(1f);
            if (lens)
            {
                V2 m = Vector2Lerp(a, head, 0.5f);
                SetColor(col.WithAlpha(0.5f * al));
                Quad(a, m + n * (w * 0.5f), head, m - n * (w * 0.5f));
            }
            else
            {
                SoftLine(a, head, w * 2.2f, col.WithAlpha(0.45f * al));
                SetColor(col.WithAlpha(0.5f * al));
                Tri(a, head + n * (w * 0.5f), head - n * (w * 0.5f));
                Tri(head + n * (w * 0.5f), head + u * 6f, head - n * (w * 0.5f));
            }
            Line(a, head, 1.5f, Rgba.White.WithAlpha(0.95f * al));
        }

        /// A crescent from `tail` to `head` at radius `r`: thick (`w`) at the head, nothing at the tail, its outer edge white
        /// over the leading three quarters.
        void KatanaArcFill(V2 c, float r, float tail, float head, float w, float a, Rgba col)
        {
            float span = head - tail;
            if (Mathf.Abs(span) < 0.01f || a <= 0f)
                return;
            int n = Mathf.Max(10, Mathf.CeilToInt(Mathf.Abs(span) * 14f));
            SetColor(col.WithAlpha(0.55f * a));
            for (int i = 0; i < n; i++)
            {
                float t0 = (float)i / n, t1 = (float)(i + 1) / n;
                float q0 = tail + span * t0, q1 = tail + span * t1;
                float w0 = w * Mathf.Pow(t0, 1.4f), w1 = w * Mathf.Pow(t1, 1.4f);
                V2 o0 = c + V2.FromAngle(q0) * r, o1 = c + V2.FromAngle(q1) * r, i0 = c + V2.FromAngle(q0) * (r - w0), i1 = c + V2.FromAngle(q1) * (r - w1);
                Quad(o0, o1, i1, i0);
            }
            for (int i = n / 4; i < n; i++)
            {
                float q0 = tail + span * i / n, q1 = tail + span * (i + 1) / n;
                Line(c + V2.FromAngle(q0) * r, c + V2.FromAngle(q1) * r, 2f, Rgba.White.WithAlpha(0.95f * a));
            }
        }
    }
}
