using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Mammoth Fall (the DPS Summoner's third ultimate, Core/MammothFall.cs), from the Mammoth Fall mockup. Everything is drawn
    /// from the replicated zone (`ZoneKind.Mammoth`: Pos, Origin, Aim, Radius, Side, T, Duration):
    ///   the floor  (DrawMammothFloor, under the enemies) a violet rune circle under you while it forms; the gold aim ring at
    ///              Origin (the shockwave's dashed edge, the crush ring and an arc filling as the summon completes); the shadows
    ///              of the hovering and diving mammoth; then the crater with glowing cracks, the shockwave running out, the
    ///              rampage's faint leash ring and gold brackets on the enemy it's charging;
    ///   the sky    (DrawMammoths, over the enemies) the rift over you with motes spiralling in and the mammoth forming in it
    ///              (a violet outline fading as the wool fills in), rearing at the end; the dive; the squash on landing; the
    ///              stomping rampage; and the charge away, fading into violet motes.
    /// The landing shakes the screen once per mammoth. Its rampage position is eased toward the snapshots so it runs smoothly.
    /// Visual effects off: plain rings and an ellipse for the mammoth.
    public partial class ArenaApp
    {
        static readonly Rgba MfWool = new Rgba(0.48f, 0.31f, 0.18f), MfWoolLight = new Rgba(0.66f, 0.46f, 0.29f), MfWoolDark = new Rgba(0.29f, 0.18f, 0.1f),
            MfTusk = new Rgba(0.95f, 0.9f, 0.8f), MfViolet = new Rgba(0.65f, 0.49f, 1f), MfRiftCore = new Rgba(0.94f, 0.88f, 1f),
            MfGold = new Rgba(1f, 0.8f, 0.35f), MfCrack = new Rgba(0.95f, 0.62f, 0.25f), MfEye = new Rgba(0.1f, 0.06f, 0.03f);

        /// How high it hangs over you while it forms.
        const float MfHover = 92f;

        readonly Dictionary<int, V2> mfDisp = new Dictionary<int, V2>();
        readonly HashSet<int> mfShook = new HashSet<int>();
        readonly List<int> mfGone = new List<int>();

        V2 MfOwnerAt(Zone z)
        {
            Player p;
            return game.Players.TryGetValue(z.Owner, out p) ? p.Disp : z.Pos;
        }

        /// Under the enemies (WorldEnemies.cs): the rune, the aim ring, the shadows, the crater, the shockwave, the prey marker.
        void DrawMammothFloor(Zone z, float t)
        {
            float age = Game.MfAge(z);
            if (age < Game.MfImpactAt)
            {
                bool flying = age >= Game.MfSummon;
                float f = Mathf.Clamp01(age / Game.MfSummon);
                V2 me = MfOwnerAt(z);
                if (!fxOn)
                {
                    Arc(z.Origin, z.Radius, 2f, MfGold.WithAlpha(0.5f), 32);
                    Arc(z.Origin, Game.MfCrush, 2.5f, MfGold, 24);
                    return;
                }
                if (!flying)  // the rune under you
                {
                    float rr = 40f + 10f * KEo(f);
                    Arc(me, rr, 2f, MfViolet.WithAlpha(0.8f), 32);
                    Arc(me, rr * 0.78f, 1.5f, MfViolet.WithAlpha(0.6f), 28);
                    for (int i = 0; i < 5; i++)  // the star
                    {
                        float a0 = t * 0.9f + i * MathX.Tau * 2f / 5f, a1 = a0 + MathX.Tau * 2f / 5f;
                        Line(me + V2.FromAngle(a0) * (rr * 0.75f), me + V2.FromAngle(a1) * (rr * 0.75f), 1.5f, MfViolet.WithAlpha(0.55f));
                    }
                    for (int i = 0; i < 12; i++)  // the ticks round the rim
                    {
                        float a = -t * 0.6f + i * MathX.Tau / 12f;
                        Line(me + V2.FromAngle(a) * (rr * 0.82f), me + V2.FromAngle(a) * (rr * 0.98f), 2f, MfViolet.WithAlpha(0.7f));
                    }
                    Ellipse(me + new V2(0f, 6f), 46f * f, 14f * f, 0f, new Rgba(0f, 0f, 0f, 0.22f * f), 18);  // the shadow of what's forming overhead
                }
                // the aim ring: the shockwave's dashed edge, the crush ring, the summon filling round it
                float a2 = flying ? 0.75f : 0.3f + 0.35f * f;
                for (int d = 0; d < 28; d++)
                    Arc(z.Origin, z.Radius, 1.6f, MfGold.WithAlpha(a2), 4, d * MathX.Tau / 28f + t * 0.5f, MathX.Tau / 28f * 0.55f);
                Circle(z.Origin, Game.MfCrush, MfGold.WithAlpha(0.07f + 0.1f * f), 28);
                Arc(z.Origin, Game.MfCrush, 2.5f, MfGold.WithAlpha(0.9f), 28);
                Arc(z.Origin, Game.MfCrush + 8f, 4f, MfGold, 32, -Mathf.PI / 2f, MathX.Tau * f);
                if (!flying)  // a faint tether from you to it
                    for (int d = 0; d < 12; d++)
                    {
                        float a = d / 12f, b = a + 0.04f;
                        Line(V2.Lerp(me, z.Origin, a), V2.Lerp(me, z.Origin, b), 1.5f, MfViolet.WithAlpha(0.35f));
                    }
                else  // the dive's shadow, growing as it comes down
                {
                    float k = (age - Game.MfSummon) / Game.MfFly, e = k * k;
                    V2 g = V2.Lerp(z.Pos, z.Origin, e);
                    Ellipse(g, 32f + 26f * k, 12f + 14f * k, 0f, new Rgba(0f, 0f, 0f, 0.25f + 0.4f * k), 18);
                }
                return;
            }
            float since = age - Game.MfImpactAt, fade = Mathf.Clamp01(z.T / 1.2f);
            if (!fxOn)
            {
                if (since < 0.35f)
                    Arc(z.Origin, z.Radius * (since / 0.35f), 3f, MfGold.WithAlpha(1f - since / 0.35f), 32);
                Circle(z.Origin, 50f, new Rgba(0f, 0f, 0f, 0.35f * fade), 18);
                return;
            }
            // the crater and its cracks
            Ellipse(z.Origin, 60f, 50f, 0f, new Rgba(0f, 0f, 0f, 0.42f * fade), 22);
            Ellipse(z.Origin, 36f, 28f, 0f, new Rgba(0f, 0f, 0f, 0.3f * fade), 18);
            float glow = Mathf.Clamp01(1f - since / 3f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * MathX.Tau / 9f + KHash(z.Id * 7f + i) * 0.5f;
                V2 at = z.Origin;
                for (int j = 0; j < 5; j++)
                {
                    a += (KHash(z.Id * 13f + i * 5f + j) - 0.5f) * 0.9f;
                    float len = 14f + 22f * KHash(z.Id * 3f + i * 7f + j + 0.5f);
                    V2 next = at + new V2(Mathf.Cos(a) * len, Mathf.Sin(a) * len * 0.85f);
                    Line(at, next, 3.4f - j * 0.45f, MfWoolDark.WithAlpha(0.8f * fade));
                    Line(at, next, 1.8f - j * 0.2f, MfCrack.WithAlpha((0.25f + 0.75f * glow) * fade));
                    at = next;
                }
            }
            if (since < 0.5f)  // the shockwave running out
            {
                float k = Mathf.Clamp01(since / 0.35f), r = z.Radius * KEo(k), a = 1f - since / 0.5f;
                Arc(z.Origin, r, 10f * (1f - 0.7f * k), MfGold.WithAlpha(a), 48);
                Arc(z.Origin, r * 0.72f, 2f, MfTusk.WithAlpha(a), 40);
                for (int d = 0; d < 14; d++)  // dust thrown out
                {
                    float da = d * MathX.Tau / 14f + KHash(z.Id + d) * 0.4f;
                    GlowDisc(z.Origin + V2.FromAngle(da) * (r * (0.55f + 0.35f * KHash(z.Id * 2f + d))), 6f + 4f * KHash(d + 0.3f), new Rgba(0.62f, 0.55f, 0.46f), 0.55f * a);
                }
            }
            float leaveAt = Game.MfLeaveAt(z);
            if (age >= Game.MfImpactAt + Game.MfSettle && age < leaveAt)
            {
                for (int d = 0; d < 36; d++)  // the leash ring, faint
                    Arc(z.Origin, Game.MfLeash, 1.2f, MfGold.WithAlpha(0.18f), 3, d * MathX.Tau / 36f, MathX.Tau / 36f * 0.3f);
                Enemy prey;
                if (z.Side != 0 && game.Enemies.TryGetValue(z.Side, out prey))
                {
                    float s = prey.Radius + 9f + Mathf.Sin(t * 11f) * 2f;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                        V2 c = prey.Disp + V2.FromAngle(a) * s;
                        Line(c, c + V2.FromAngle(a + Mathf.PI * 0.75f) * 7f, 2f, MfGold);
                        Line(c, c + V2.FromAngle(a - Mathf.PI * 0.75f) * 7f, 2f, MfGold);
                    }
                }
            }
        }

        /// Over the enemies (WorldRenderer.cs): each mammoth in the sky, landing, rampaging or leaving.
        void DrawMammoths(float t)
        {
            mfGone.Clear();
            foreach (var id in mfDisp.Keys)
                if (!game.Zones.ContainsKey(id))
                    mfGone.Add(id);
            foreach (var id in mfGone)
            {
                mfDisp.Remove(id);
                mfShook.Remove(id);
            }
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.Mammoth)
                    DrawMammoth(z, t);
        }

        void DrawMammoth(Zone z, float t)
        {
            float age = Game.MfAge(z);
            if (age < Game.MfSummon)
            {
                float f = age / Game.MfSummon, ease = KEo(f);
                V2 at = MfOwnerAt(z) + new V2(0f, -MfHover + Mathf.Sin(t * 5.5f) * 3f * f);
                if (!fxOn)
                {
                    Ellipse(at, 50f * ease, 34f * ease, z.Aim, MfWool.WithAlpha(f), 14);
                    return;
                }
                float rw = 110f * Mathf.Clamp01(f * 2.2f), rh = (40f + 20f * f) * Mathf.Clamp01(f * 2.2f);
                Ellipse(at, rw, rh, 0f, MfViolet.WithAlpha(0.22f), 24);  // the rift
                Ellipse(at, rw * 0.62f, rh * 0.62f, 0f, MfViolet.WithAlpha(0.3f), 22);
                GlowDisc(at, rw * 0.35f, MfRiftCore, 0.55f);
                MfEllipseLine(at, rw * 0.8f, rh * 0.8f, 0f, 2f, MfRiftCore.WithAlpha(0.7f));
                for (int i = 0; i < 14; i++)  // motes spiralling in
                {
                    float k = (t * 1.6f + i / 14f) % 1f, a = i * 2.4f + k * 3f, r = 150f * (1f - k);
                    GlowDisc(at + new V2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f), 3f, i % 2 == 0 ? MfViolet : MfWoolLight, 0.9f * k);
                }
                float rear = f > 0.85f ? Mathf.Sin((f - 0.85f) / 0.15f * Mathf.PI) * 0.12f : 0f;
                DrawMammothBody(at, z.Aim, 0.55f + 0.5f * ease + rear, Mathf.Clamp01(f * 1.4f), f, t * 3.3f);
                return;
            }
            if (age < Game.MfImpactAt)
            {
                float k = (age - Game.MfSummon) / Game.MfFly, e = k * k;
                V2 g = V2.Lerp(z.Pos, z.Origin, e), at = g - new V2(0f, MfHover * (1f - e));
                if (fxOn)
                    for (int i = 1; i <= 5; i++)  // the streak behind it
                    {
                        float kb = Mathf.Max(0f, k - i * 0.05f), eb = kb * kb;
                        V2 back = V2.Lerp(z.Pos, z.Origin, eb) - new V2(0f, MfHover * (1f - eb));
                        GlowDisc(back, 26f - i * 3f, MfViolet, 0.35f - i * 0.05f);
                    }
                DrawMammothBody(at, z.Aim, 1.05f + 0.2f * (1f - k), 1f, 1f, 0f);
                return;
            }
            if (!mfShook.Contains(z.Id))
            {
                mfShook.Add(z.Id);
                Shake(0.32f);
                Hold(0.05f);
            }
            V2 disp;
            if (!mfDisp.TryGetValue(z.Id, out disp) || (disp - z.Pos).Length > 140f)
                disp = z.Pos;
            disp = V2.Lerp(disp, z.Pos, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            mfDisp[z.Id] = disp;
            float since = age - Game.MfImpactAt, leaveAt = Game.MfLeaveAt(z);
            float alpha = age < leaveAt ? 1f : Mathf.Clamp01(1f - (age - leaveAt) / Game.MfLeave);
            float squash = since < Game.MfSettle ? 1f - 0.12f * Mathf.Sin(since / Game.MfSettle * Mathf.PI) : 1f;
            float stomp = since < Game.MfSettle ? 0f : t * 18f;
            if (fxOn)
            {
                Ellipse(disp + new V2(0f, 8f), 50f, 22f, z.Aim, new Rgba(0f, 0f, 0f, 0.3f * alpha), 18);  // its shadow
                if (since >= Game.MfSettle)
                    for (int i = 0; i < 3; i++)  // dust kicked up behind
                    {
                        float k = (t * 2.2f + i / 3f) % 1f;
                        GlowDisc(disp - V2.FromAngle(z.Aim) * (34f + 30f * k) + V2.FromAngle(z.Aim + Mathf.PI / 2f) * ((i - 1) * 14f), 7f + 8f * k, new Rgba(0.5f, 0.43f, 0.36f), 0.45f * (1f - k) * alpha);
                    }
                if (age >= leaveAt)
                    for (int i = 0; i < 10; i++)  // fading back into motes
                    {
                        float k = (t * 1.3f + i / 10f) % 1f;
                        V2 m = disp + new V2((KHash(z.Id + i) - 0.5f) * 90f, (KHash(z.Id * 2f + i) - 0.5f) * 60f - 50f * k);
                        GlowDisc(m, 3.5f, MfViolet, (1f - alpha) * (1f - k));
                    }
            }
            DrawMammothBody(disp, z.Aim, 0.95f * squash, alpha, 1f, stomp);
        }

        /// The mammoth from above, facing `ang`: `s` its scale, `a` its alpha, `form` 0 to 1 how far it has formed (a violet
        /// outline until 1), `stomp` the legs' swing.
        void DrawMammothBody(V2 c, float ang, float s, float a, float form, float stomp)
        {
            if (a <= 0.01f)
                return;
            V2 fwd = V2.FromAngle(ang), side = new V2(-fwd.Y, fwd.X);
            System.Func<float, float, V2> P = (x, y) => c + fwd * (x * s) + side * (y * s);
            if (!fxOn)
            {
                Ellipse(P(-4f, 0f), 44f * s, 31f * s, ang, MfWool.WithAlpha(a), 14);
                Ellipse(P(40f, 0f), 17f * s, 19f * s, ang, MfWoolDark.WithAlpha(a), 10);
                return;
            }
            float[,] legs = { { -26f, -24f }, { -26f, 24f }, { 20f, -24f }, { 20f, 24f } };
            for (int i = 0; i < 4; i++)
            {
                float o = Mathf.Sin(stomp + (i % 2 == 1 ? Mathf.PI : 0f) + (i > 1 ? Mathf.PI / 2f : 0f)) * 5f;
                Ellipse(P(legs[i, 0] + o, legs[i, 1]), 9f * s, 8f * s, ang, MfWoolDark.WithAlpha(a), 10);
            }
            for (int i = 0; i < 22; i++)  // the shaggy fringe round the body
            {
                float q = i * MathX.Tau / 22f, w = 1f + 0.06f * Mathf.Sin(q * 11f + stomp * 0.3f);
                Ellipse(P(-4f + Mathf.Cos(q) * 42f * w, Mathf.Sin(q) * 29f * w), 7f * s, 5f * s, ang + q, MfWoolDark.WithAlpha(a), 8);
            }
            Ellipse(P(-4f, 0f), 43f * s, 30f * s, ang, MfWool.WithAlpha(a), 22);
            Ellipse(P(-8f, -4f), 30f * s, 18f * s, ang, MfWoolLight.WithAlpha(a), 18);
            for (int i = 0; i < 14; i++)  // fur strokes
            {
                float x = -38f + i * 5.5f, y = Mathf.Sin(i * 2.3f) * 16f;
                Line(P(x, y - 6f), P(x + 3f, y), 1.6f * s, MfWoolDark.WithAlpha(0.7f * a));
                Line(P(x + 3f, y), P(x, y + 6f), 1.6f * s, MfWoolDark.WithAlpha(0.7f * a));
            }
            Ellipse(P(18f, 0f), 13f * s, 17f * s, ang, MfWoolDark.WithAlpha(a), 14);  // the hump
            Ellipse(P(34f, -19f), 7f * s, 5f * s, ang - 0.4f, MfWoolDark.WithAlpha(a), 8);  // ears
            Ellipse(P(34f, 19f), 7f * s, 5f * s, ang + 0.4f, MfWoolDark.WithAlpha(a), 8);
            Ellipse(P(40f, 0f), 17f * s, 19f * s, ang, MfWool.WithAlpha(a), 16);  // the head
            for (int sd = -1; sd <= 1; sd += 2)  // the tusks, curling forward and in
            {
                V2 prev = P(50f, sd * 9f);
                for (int i = 1; i <= 10; i++)
                {
                    float u = i / 10f, iu = 1f - u;
                    float x = iu * iu * iu * 50f + 3f * iu * iu * u * 74f + 3f * iu * u * u * 86f + u * u * u * 76f;
                    float y = iu * iu * iu * 9f + 3f * iu * iu * u * 22f + 3f * iu * u * u * 6f + u * u * u * -6f;
                    V2 next = P(x, sd * y);
                    Line(prev, next, (5f - 2.5f * u) * s, MfTusk.WithAlpha(a));
                    prev = next;
                }
            }
            float sway = Mathf.Sin(stomp * 0.8f);
            V2 t0 = P(55f, 0f), t1 = P(66f, sway * 6f), t2 = P(62f, sway * 12f + 6f);  // the trunk
            Line(t0, t1, 7f * s, MfWoolDark.WithAlpha(a));
            Line(t1, t2, 6f * s, MfWoolDark.WithAlpha(a));
            Circle(P(46f, -8f), 2.2f * s, MfEye.WithAlpha(a), 8);
            Circle(P(46f, 8f), 2.2f * s, MfEye.WithAlpha(a), 8);
            if (form < 1f)  // the summoning outline, fading as the wool fills in
            {
                var col = MfViolet.WithAlpha((1f - form) * 0.95f);
                MfEllipseLine(P(-4f, 0f), 46f * s, 33f * s, ang, 1.8f, col);
                MfEllipseLine(P(40f, 0f), 19f * s, 21f * s, ang, 1.8f, col);
                GlowDisc(c, 60f * s, MfViolet, 0.35f * (1f - form));
            }
        }

        /// An ellipse's outline.
        void MfEllipseLine(V2 c, float rx, float ry, float rot, float w, Rgba col)
        {
            V2 u = V2.FromAngle(rot), v = new V2(-u.Y, u.X), prev = c + u * rx;
            for (int i = 1; i <= 28; i++)
            {
                float q = i * MathX.Tau / 28f;
                V2 next = c + u * (Mathf.Cos(q) * rx) + v * (Mathf.Sin(q) * ry);
                Line(prev, next, w, col);
                prev = next;
            }
        }
    }
}
