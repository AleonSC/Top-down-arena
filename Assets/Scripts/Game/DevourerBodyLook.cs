using UnityEngine;

namespace Arena
{
    /// The Devourer's body, the Lamprey: a ridged worm head with a round disc of tooth rings grinding in turn, gill pores
    /// down its sides and a ringed tail swaying behind. The disc opens wide when it feeds, inhales, lunges, vomits or turns
    /// Ravenous, and whoever it swallowed shows in its throat. Each phase changes it: Appetite raw pink, Gorging swollen
    /// with bile dripping from the disc, Ravenous dark with the skin split red and a third tooth ring in the throat, the
    /// last stand's cracks and throat burning. (The floor tells, shots and morsels: DevourerLook.cs.)
    public partial class ArenaApp
    {
        static readonly Rgba DvThroat = new Rgba(0.09f, 0.01f, 0.03f), DvGum = new Rgba(0.45f, 0.06f, 0.12f),
            DvLip = new Rgba(0.98f, 0.7f, 0.68f);

        /// Its skin in each phase.
        static Rgba DevourFlesh(int stage) => stage == 0 ? new Rgba(0.88f, 0.42f, 0.48f) : stage == 1 ? new Rgba(0.86f, 0.5f, 0.42f)
            : stage == 2 ? new Rgba(0.6f, 0.13f, 0.2f) : new Rgba(0.48f, 0.06f, 0.13f);

        void DrawDevourerBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var s = e.Devour;
            float a = col.A, hit = (e.Flash > 0f || JuiceFlash(e)) ? HitFlashMix(e) : 0f;
            int stage = s == null ? 0 : (int)(s.TransT > 0f ? s.TransTo : s.Stage);
            bool wide = e.Phase == 1 || s != null && (s.InhaleT > 0f || s.Stage == DevourStage.Ravenous || s.Turn == DevourTurn.Vomit || s.Lunging && s.LungeWarn <= 0f);
            float open = s != null && s.SwallowId != 0 ? 0.25f : wide ? 1f : 0.3f + 0.06f * Mathf.Sin(t * 2.5f);
            Rgba? eaten = null;
            Player q;
            if (s != null && s.SwallowId != 0 && game.Players.TryGetValue(s.SwallowId, out q))
                eaten = q.Def.Color;
            V2.Axes(e.Facing, out V2 f, out V2 sd);
            Ellipse(pos + new V2(0f, r * 0.3f), r * 1.1f, r * 0.6f, 0f, new Rgba(0f, 0f, 0f, 0.3f * a), 24);  // shadow
            Rgba flesh = DevourFlesh(stage).Mix(Rgba.White, hit).WithAlpha(a);
            DrawDevourLamprey(pos, r, e.Facing, f, sd, stage, open, flesh, eaten, a, t);
        }

        /// A ring of `n` teeth at radius `rr` round `c`, all pointing in, turned by `rot`.
        void ToothRing(V2 c, float rr, int n, float size, float rot, float alpha)
        {
            for (int k = 0; k < n; k++)
            {
                float ang = rot + MathX.Tau * k / n;
                Tooth(c + V2.FromAngle(ang) * rr, ang + MathX.Pi, size, alpha);
            }
        }

        /// The throat's light from the last stand on (and a hint of it in Ravenous).
        void DevourThroatGlow(V2 c, float rr, int stage, float a, float t)
        {
            if (stage < 2)
                return;
            float k = stage == 3 ? 0.75f + 0.25f * Mathf.Sin(t * 7f) : 0.45f;
            GlowDisc(c, rr * 1.6f, new Rgba(1f, 0.35f, 0.2f), k * a);
            Circle(c, rr * 0.45f, new Rgba(1f, 0.6f, 0.35f, 0.7f * k * a), 14);
        }

        /// Bile running from a spot (Gorging): a few green drops.
        void DevourDrool(V2 at, V2 dir, float r, float a, float t)
        {
            for (int k = 0; k < 3; k++)
            {
                float u = Fmod(t * 0.8f + k * 0.33f, 1f);
                Circle(at + dir * (r * 0.25f * u) + dir.Orthogonal() * (r * 0.12f * (k - 1)), r * (0.07f - 0.03f * u), BileGreen.WithAlpha(a * (1f - u)), 8);
            }
        }

        void DrawDevourLamprey(V2 pos, float r, float face, V2 f, V2 sd, int stage, float open, Rgba flesh, Rgba? eaten, float a, float t)
        {
            float swell = stage == 1 ? 1.08f : 1f;
            // the tail, back to front: four ringed segments swaying behind it
            for (int i = 3; i >= 0; i--)
            {
                float sr = r * (0.8f - 0.15f * i) * swell, back = r * (0.62f + 0.33f * i);
                V2 c = pos - f * back + sd * (Mathf.Sin(t * 3f - i * 0.9f) * r * 0.08f * (i + 1));
                Rgba sc = flesh.Mix(new Rgba(0.2f, 0.03f, 0.07f), 0.15f + 0.12f * i);
                Ellipse(c, sr * 0.82f + 2.5f, sr + 2.5f, face, LarderInk.WithAlpha(a), 18);
                Ellipse(c, sr * 0.82f, sr, face, sc, 18);
                Line(c - sd * sr * 0.85f + f * sr * 0.1f, c + sd * sr * 0.85f + f * sr * 0.1f, 2.5f, sc.Mix(LarderInk, 0.4f));
                Circle(c + f * sr * 0.2f - sd * sr * 0.3f, sr * 0.22f, flesh.Mix(Rgba.White, 0.25f).WithAlpha(0.35f * a), 10);
            }
            // the head
            float hr = r * swell;
            Circle(pos, hr + 3f, LarderInk.WithAlpha(a), 32);
            Circle(pos, hr, flesh, 32);
            Circle(pos - f * hr * 0.2f - sd * hr * 0.25f, hr * 0.5f, flesh.Mix(Rgba.White, 0.18f).WithAlpha(0.5f * a), 20);
            for (int k = 1; k <= 3; k++)  // muscle bands across its back
            {
                float x = -0.15f - 0.22f * k, half = Mathf.Sqrt(1f - x * x) * hr * 0.92f;
                V2 c = pos + f * (x * hr);
                Line(c - sd * half, c + sd * half, 3f, flesh.Mix(LarderInk, 0.45f));
            }
            for (int side = -1; side <= 1; side += 2)  // gill pores down each side
                for (int k = 0; k < 6; k++)
                {
                    float ang = face + side * (1.25f + 0.22f * k);
                    Ellipse(pos + V2.FromAngle(ang) * hr * 0.8f, hr * 0.07f, hr * 0.04f, ang, LarderInk.WithAlpha(a), 8);
                }
            if (stage >= 2)  // Ravenous: the skin splits, raw red showing through
                for (int k = 0; k < 4; k++)
                {
                    float ang = face + MathX.Pi + (k - 1.5f) * 0.55f;
                    V2 p0 = pos + V2.FromAngle(ang) * hr * 0.3f, p1 = pos + V2.FromAngle(ang + 0.2f) * hr * 0.62f, p2 = pos + V2.FromAngle(ang - 0.1f) * hr * 0.88f;
                    Rgba crack = new Rgba(1f, 0.32f, 0.3f, (stage == 3 ? 0.6f + 0.4f * Mathf.Sin(t * 7f + k) : 0.75f) * a);
                    Line(p0, p1, 2.5f, crack);
                    Line(p1, p2, 2f, crack);
                }
            // the oral disc: a lip of papillae, then rings of teeth grinding in turn down to the throat
            V2 mc = pos + f * hr * 0.36f;
            float dr = hr * (0.5f + 0.16f * open);
            Circle(mc, dr + 3f, LarderInk.WithAlpha(a), 28);
            Circle(mc, dr, DvLip.Mix(flesh, 0.35f).WithAlpha(a), 28);
            for (int k = 0; k < 14; k++)
                Circle(mc + V2.FromAngle(face + MathX.Tau * k / 14f) * dr * 0.97f, dr * 0.09f, DvLip.Mix(Rgba.White, 0.3f).WithAlpha(a), 8);
            Circle(mc, dr * 0.86f, DvGum.WithAlpha(a), 26);
            ToothRing(mc, dr * 0.8f, 16, dr * 0.17f, t * 0.35f, a);
            Circle(mc, dr * 0.6f, DvGum.Mix(DvThroat, 0.5f).WithAlpha(a), 22);
            ToothRing(mc, dr * 0.56f, 11, dr * 0.15f, -t * 0.55f, a);
            float tr = dr * (0.25f + 0.17f * open);
            Circle(mc, tr, DvThroat.WithAlpha(a), 18);
            if (stage >= 2)
                ToothRing(mc, tr * 0.95f, 8, dr * 0.1f, t * 0.9f, a);
            if (eaten.HasValue)
                Circle(mc, tr * 0.7f, eaten.Value.WithAlpha((0.45f + 0.3f * Mathf.Sin(t * 8f)) * a), 14);
            DevourThroatGlow(mc, tr, stage, a, t);
            if (stage == 1)
                DevourDrool(mc + f * dr, f, hr, a, t);
        }
    }
}
