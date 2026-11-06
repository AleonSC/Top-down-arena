using UnityEngine;

namespace Arena
{
    /// Vein Tithe (Core/VeinTithe.cs), from Docs/Mockups/blood-rite-vein-tithe.html. A heart hovers over the Hemomancer and beats once a
    /// second (a ring goes out with each beat). An artery runs from the Hemomancer to every held enemy (`Player.Tithed`, in the order they
    /// reach: `Game.TitheGrown`): thick at the Hemomancer and thinner at the enemy, swelling on the beat, with a bright pulse running out
    /// along it each beat. Each held enemy wears a blood cuff with roots creeping over it and a drip below. When it ends, each artery pulls
    /// back from its enemy as one FxKind.TitheSnap (Pos = the Hemomancer, Pos2 = the enemy), drawn from its age alone.
    public partial class ArenaApp
    {
        static readonly Rgba TvOutline = new Rgba(0.16f, 0.02f, 0.06f), TvArtery = new Rgba(0.55f, 0.06f, 0.15f), TvLight = new Rgba(1f, 0.36f, 0.45f);
        static readonly Rgba TvCuff = new Rgba(0.69f, 0.06f, 0.16f), TvPulse = new Rgba(1f, 0.84f, 0.86f);

        /// The beat, 1 at the start of each second and fading fast.
        static float TvBeat(float el) => Mathf.Exp(-MathX.Fmod(el, 1f) * 5f);

        /// A point `u` of the way along the artery from `a` to `b`: bowed to one side, swaying a little.
        static V2 TvAt(V2 a, V2 b, int id, float t, float u)
        {
            V2 d = b - a;
            float len = Mathf.Max(1f, d.Length), side = id % 2 == 0 ? -1f : 1f, bow = (side * 0.16f + Mathf.Sin(t * 1.7f + id * 1.3f) * 0.05f) * len;
            V2 ctrl = (a + b) * 0.5f + d.Orthogonal() * (bow / len);
            return HvBez(a, ctrl, b, u);
        }

        void TvHeart(V2 c, float s, float beat)
        {
            s *= 1f + 0.2f * beat;
            GlowDisc(c, s * 2.4f, TvLight, 0.18f + 0.25f * beat);
            Circle(c + new V2(-0.5f, -0.3f) * s, s * 0.62f, TvCuff, 14);
            Circle(c + new V2(0.5f, -0.3f) * s, s * 0.62f, TvCuff, 14);
            SetColor(TvCuff);
            Tri(c + new V2(-1.08f, -0.05f) * s, c + new V2(1.08f, -0.05f) * s, c + new V2(0f, 1.1f) * s);
            Circle(c + new V2(-0.55f, -0.45f) * s, s * 0.2f, new Rgba(1f, 1f, 1f, 0.55f), 8);
        }

        /// Called for each player: the heart, and an artery and cuff for each enemy they hold.
        void DrawTithe(Player p, float t)
        {
            if (p.Downed || !Game.TitheActive(p))
                return;
            float el = p.TitheClock, beat = TvBeat(el);
            V2 from = p.Disp;
            foreach (int id in p.Tithed)
                if (game.Enemies.TryGetValue(id, out var e) && e.Hp > 0f)
                    DrawArtery(from, e, p.Tithed.IndexOf(id), el, t, beat);
            Arc(from, 16f + (1f - beat) * 46f, 2f, TvLight.WithAlpha(beat * 0.5f), 32);
            TvHeart(from + new V2(0f, -28f), 8f, beat);
        }

        void DrawArtery(V2 from, Enemy e, int k, float el, float t, float beat)
        {
            float grown = Game.TitheGrown(el, k);
            if (grown <= 0.01f)
                return;
            V2 to = e.Disp;
            float boss = e.Def.IsBoss ? 1.5f : e.Def.Heavy ? 1.2f : 1f;
            const int N = 24;
            int n = Mathf.Max(2, (int)(grown * N));
            V2 prev = from;
            for (int j = 1; j <= n; j++)
            {
                float u = grown * j / n;  // the artery grows from the heart, so its far end is only at the enemy once fully grown
                V2 pt = TvAt(from, to, e.Id, t, u);
                float w = Mathf.Lerp(7f, 3.2f, u) * boss * (1f + 0.28f * beat * (1f - u));
                Line(prev, pt, w + 3f, TvOutline);
                Line(prev, pt, w, TvArtery);
                Line(prev + new V2(0f, -0.9f), pt + new V2(0f, -0.9f), w * 0.3f, TvLight);
                prev = pt;
            }
            if (grown < 0.999f)
                return;
            for (int q = 0; q < 2; q++)  // a pulse leaves the heart on each beat
            {
                float u = MathX.Fmod(el + q * 0.5f, 1f);
                V2 pp = TvAt(from, to, e.Id, t, u);
                GlowDisc(pp, 8f * boss, TvPulse, 0.85f);
            }
            float r = e.Radius;
            Arc(e.Disp, r + 2f, 3f, TvCuff, 20);
            for (int a = 0; a < 7; a++)  // roots creeping over the body
            {
                float an = a / 7f * MathX.Tau + e.Id * 0.7f;
                V2 p0 = e.Disp + V2.FromAngle(an) * (r + 3f);
                for (int s = 1; s <= 4; s++)
                {
                    float aa = an + Mathf.Sin(s * 1.7f + a + t * 0.5f) * 0.25f;
                    V2 p1 = e.Disp + V2.FromAngle(aa) * (r + 3f - r * 0.9f * s / 4f);
                    Line(p0, p1, 1.5f, TvLight.WithAlpha(0.85f));
                    p0 = p1;
                }
            }
            float dr = MathX.Fmod(t * 0.6f + e.Id * 0.37f, 1f);
            Circle(e.Disp + new V2(0f, r + 4f + dr * 6f), 2f, TvCuff, 6);
        }

        /// FxKind.TitheSnap: the artery pulling back off an enemy, a flash of drops as it lets go.
        void DrawTitheSnapFx(Effect fx, float f)
        {
            V2 a = fx.Pos, b = fx.Pos2;
            float pull = 1f - Mathf.Clamp01(f * 1.4f), w = fx.Color.A > 0.9f ? 1.5f : 1f;
            if (pull > 0.01f)
            {
                V2 prev = a;
                for (int j = 1; j <= 16; j++)
                {
                    V2 pt = V2.Lerp(a, b, j / 16f * pull);
                    float th = Mathf.Lerp(7f, 3.2f, j / 16f) * w * pull;
                    Line(prev, pt, th + 3f, TvOutline.WithAlpha(0.8f));
                    Line(prev, pt, th, TvArtery);
                    prev = pt;
                }
            }
            for (int i = 0; i < 6; i++)
            {
                V2 d = V2.FromAngle(CavHash(i * 5 + (int)b.X) * MathX.Tau);
                GlowDisc(b + d * (fx.Radius + 30f * Mathf.Sqrt(f) * (0.5f + CavHash(i))), 3.5f, TvLight, 0.9f * (1f - f));
            }
            Arc(b, fx.Radius + 4f + 16f * f, 2.5f, TvCuff.WithAlpha(1f - f), 20);
        }
    }
}
