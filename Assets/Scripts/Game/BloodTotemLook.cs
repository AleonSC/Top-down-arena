using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Blood Totem (Core/BloodTotem.cs), from the claude.ai artifact "Blood Totem". Each `ZoneKind.BloodTotem` zone draws in two
    /// parts. On the floor (`DrawBloodTotemFloor`, from DrawZone): a faint blood wash out to its reach, a slowly turning dashed
    /// ring and seven roots creeping out from its foot. Over the enemies (`DrawBloodTotemsOver`): the totem, a spike of clot with
    /// crimson ribs rising out of the floor, a beating heart on top (Vein Tithe's `TvHeart`) and a thin arc of the time it has
    /// left; then a vein from that heart to every enemy it holds (`Game.TotemGrips`, worked out here the same way as on the
    /// host): a wavy cord with a dark outline and a bright core, a pulse of blood running down it, growing out over 0.25 s and
    /// wrapping the enemy in three turning coils. In its last second the totem sinks back and everything fades.
    public partial class ArenaApp
    {
        static readonly Rgba BtClot = new Rgba(0.23f, 0.05f, 0.09f), BtRim = new Rgba(0.54f, 0.11f, 0.19f), BtRib = new Rgba(0.78f, 0.2f, 0.29f);
        static readonly Rgba BtVein = new Rgba(0.78f, 0.2f, 0.29f), BtDark = new Rgba(0.35f, 0.03f, 0.09f), BtCore = new Rgba(1f, 0.59f, 0.63f);
        const float BtHeartY = 34f, BtGrowTime = 0.25f;

        readonly List<Game.TotemGrip> btGrips = new List<Game.TotemGrip>();
        readonly Dictionary<int, float> btSeen = new Dictionary<int, float>();  // enemy id -> when its vein started growing (the look's clock)
        readonly List<int> btGone = new List<int>();
        float btLastT = -99f;  // when the veins were last drawn: a gap means we weren't watching, so veins already there show grown

        /// 0 to 1: how far the totem has risen out of the floor, and how much of it is left as it sinks at the end.
        static float BtUp(Zone z) => Mathf.Clamp01((z.Duration - z.T) / Game.BtRise) * Mathf.Clamp01(z.T / 1f);

        /// The floor under a totem: the wash, the dashed reach ring and the roots (DrawZone, WorldEnemies.cs).
        void DrawBloodTotemFloor(Zone z, float t)
        {
            float up = BtUp(z), grow = Mathf.Clamp01((z.Duration - z.T) / 0.35f), r = z.Radius * (1f - (1f - grow) * (1f - grow));
            float fade = Mathf.Clamp01(z.T / 1f);
            Circle(z.Pos, r, BtVein.WithAlpha(0.07f * fade), 48);
            Circle(z.Pos, r * 0.6f, BtVein.WithAlpha(0.05f * fade), 40);
            const int N = 56;
            for (int i = 0; i < N; i += 2)  // the dashed reach, turning slowly
            {
                float a0 = (i + t * 0.8f) / N * MathX.Tau, a1 = (i + 1.1f + t * 0.8f) / N * MathX.Tau;
                Line(z.Pos + V2.FromAngle(a0) * r, z.Pos + V2.FromAngle(a1) * r, 1.6f, BtVein.WithAlpha(0.45f * fade));
            }
            for (int k = 0; k < 7; k++)  // roots creeping out from the foot
            {
                float a = k / 7f * MathX.Tau + z.Id * 0.9f, len = (40f + (k % 3) * 14f) * up;
                V2 prev = z.Pos;
                for (int s = 1; s <= 5; s++)
                {
                    float u = s / 5f, bend = Mathf.Sin(u * 3f + k) * 0.35f;
                    V2 pt = z.Pos + V2.FromAngle(a + bend * u) * (len * u);
                    pt = new V2(pt.X, z.Pos.Y + (pt.Y - z.Pos.Y) * 0.62f);  // flattened: lying on the floor
                    Line(prev, pt, Mathf.Lerp(3f, 1f, u), BtDark.WithAlpha(0.85f * fade));
                    prev = pt;
                }
            }
        }

        /// Every totem's body and veins, over the enemies (WorldRenderer.cs).
        void DrawBloodTotemsOver(float t)
        {
            bool any = false;
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.BloodTotem)
                {
                    any = true;
                    break;
                }
            if (!any)
            {
                btSeen.Clear();
                return;
            }
            game.TotemGrips(btGrips);
            bool fresh = t - btLastT > 0.5f;
            btLastT = t;
            btGone.Clear();
            foreach (var id in btSeen.Keys)
                if (!btGrips.Exists(g => g.Enemy.Id == id))
                    btGone.Add(id);
            foreach (int id in btGone)
                btSeen.Remove(id);
            foreach (var g in btGrips)
            {
                if (!btSeen.ContainsKey(g.Enemy.Id))
                    btSeen[g.Enemy.Id] = fresh ? t - BtGrowTime : t;
                DrawTotemVein(g.Totem, g.Enemy, Mathf.Clamp01((t - btSeen[g.Enemy.Id]) / BtGrowTime), t);
            }
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.BloodTotem)
                    DrawTotemBody(z, t);
        }

        /// The beat of a totem's heart: a quick double thump, offset per totem.
        static float BtBeat(Zone z, float t)
        {
            float ph = MathX.Fmod(t * 1.1f + z.Id * 0.37f, 1f);
            return Mathf.Exp(-ph * 9f) + 0.6f * Mathf.Exp(-Mathf.Abs(ph - 0.22f) * 14f);
        }

        void DrawTotemBody(Zone z, float t)
        {
            float up = BtUp(z), h = 58f * up, beat = BtBeat(z, t);
            if (up <= 0.01f)
                return;
            V2 p = z.Pos;
            Ellipse(p + new V2(0f, 3f), 22f, 8f, new Rgba(0f, 0f, 0f, 0.45f));
            // the spike: a wide foot narrowing to a point
            V2 fl = p + new V2(-15f, 2f), fr = p + new V2(15f, 2f), sl = p + new V2(-9f, -h * 0.78f), sr = p + new V2(9f, -h * 0.78f), tip = p + new V2(0f, -h);
            SetColor(BtRim);
            Quad(fl + new V2(-2f, 1f), fr + new V2(2f, 1f), sr + new V2(2f, 0f), sl + new V2(-2f, 0f));
            Tri(sl + new V2(-2f, 0f), sr + new V2(2f, 0f), tip + new V2(0f, -3f));
            SetColor(BtClot);
            Quad(fl, fr, sr, sl);
            Tri(sl, sr, tip);
            for (int k = 0; k < 4; k++)  // crimson ribs bound round it
            {
                float y = -8f - k * 11f * up, w = 11f - k * 1.6f;
                V2 prev = p + new V2(-w, y);
                for (int s = 1; s <= 6; s++)
                {
                    float u = s / 6f;
                    V2 pt = p + new V2(Mathf.Lerp(-w, w, u), y - Mathf.Sin(u * MathX.Pi) * 5f);
                    Line(prev, pt, 1.8f, BtRib);
                    prev = pt;
                }
            }
            Line(p + new V2(-3f, -4f), p + new V2(-2f, -h * 0.85f), 1.2f, BtRib.WithAlpha(0.5f));  // a highlight down its face
            // the heart on top, and the time it has left round it
            V2 heart = p + new V2(0f, -BtHeartY * up);
            TvHeart(heart, 7f, beat);
            float left = Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration));
            Arc(p + new V2(0f, 14f), 9f, 2f, BtDark.WithAlpha(0.7f), 20);
            Arc(p + new V2(0f, 14f), 9f, 2f, BtVein, 20, -MathX.Pi / 2f, MathX.Tau * left);
        }

        /// A point `u` of the way along a vein from `a` to `b`: a wave that swells in the middle and runs toward the enemy.
        static V2 BtAt(V2 a, V2 b, int id, float t, float u, float amp)
        {
            V2 d = b - a;
            float len = Mathf.Max(1f, d.Length);
            V2 n = d.Orthogonal() * (1f / len);
            float wv = Mathf.Sin(u * 9f - t * 5f + id) * Mathf.Sin(u * MathX.Pi) * amp;
            return a + d * u + n * wv;
        }

        void DrawTotemVein(Zone z, Enemy e, float grown, float t)
        {
            if (grown <= 0.01f)
                return;
            float fade = Mathf.Clamp01(z.T / 1f), up = BtUp(z);
            V2 from = z.Pos + new V2(0f, -BtHeartY * up), to = e.Disp;
            float boss = e.Def.IsBoss ? 1.4f : e.Def.Heavy ? 1.2f : 1f, amp = 11f * boss;
            const int N = 22;
            int n = Mathf.Max(2, (int)(grown * N));
            for (int pass = 0; pass < 3; pass++)
            {
                V2 prev = from;
                for (int j = 1; j <= n; j++)
                {
                    float u = grown * j / n;
                    V2 pt = BtAt(from, to, e.Id, t, u, amp);
                    float w = Mathf.Lerp(5f, 3f, u) * boss;
                    if (pass == 0)
                        Line(prev, pt, w + 3.5f, BtDark.WithAlpha(0.9f * fade));
                    else if (pass == 1)
                        Line(prev, pt, w, BtVein.WithAlpha(fade));
                    else
                        Line(prev + new V2(0f, -0.9f), pt + new V2(0f, -0.9f), w * 0.3f, BtCore.WithAlpha(0.8f * fade));
                    prev = pt;
                }
            }
            if (grown < 0.999f)
                return;
            float pu = MathX.Fmod(t * 1.2f + e.Id * 0.31f, 1f);  // a pulse of blood running down to the enemy
            GlowDisc(BtAt(from, to, e.Id, t, pu, amp), 6f * boss, TvPulse, 0.8f * fade);
            float r = e.Radius;
            for (int c = 0; c < 3; c++)  // the vein's coils round the enemy, turning
            {
                float rr = r + 3f + c * 2.4f, a0 = t * (c % 2 == 0 ? 1.5f : -1.3f) + c * 2.1f + e.Id;
                Arc(to, rr, c == 1 ? 2.2f : 3f, (c == 1 ? BtVein : BtDark).WithAlpha(fade), 20, a0, MathX.Tau * 0.62f);
            }
            float dr = MathX.Fmod(t * 0.7f + e.Id * 0.41f, 1f);  // a drip
            Circle(to + new V2(0f, r + 4f + dr * 6f), 1.8f, BtVein.WithAlpha(fade * (1f - dr)), 6);
        }
    }
}
