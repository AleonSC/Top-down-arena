using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Tank Soldier's Barrier in style C ("Bulwark Plates", Docs/Mockups/barrier-animations.html, the owner's pick),
    /// ported from the mockup's numbers. Eight armour plates slam down in a ring when it's cast (a short stagger, a flash as
    /// each lands, a ring of light). A blocked bullet makes the nearest plate recoil and flash, with a white cross at the
    /// impact; as the soak runs down (BarrierFrac) plates are knocked off, the one nearest the last hit first, spinning away
    /// with sparks; a faint energy disc and a slowly turning dashed ring fill the space between them. When it runs out the
    /// plates slide outward and fade one after another; when it's shattered the rest blow out. Under a second or a quarter
    /// strength left, the plates flicker as the old ring did.
    ///
    /// Drawing only, with Visual effects on (off, WorldRenderer draws the old disc and ring). Everything is worked out on
    /// this screen from BarrierT and BarrierFrac, which are already replicated, and the blocks Juice.cs sees, so nothing
    /// here is synced and the plates a client loses may differ in order from another's, never in count.
    /// (The mockup's mirror tint for the reflect capstone isn't drawn: BarrierReflect is host-only, and showing it
    /// everywhere would mean replicating it, a protocol bump.)
    public partial class ArenaApp
    {
        const float BulwarkBaseTime = 4f;  // Balance.cs BarrierTime (private there); only used to guess a cast's age on first sight
        const int BulwarkPlates = 8;
        const float BulwarkSpan = MathX.Tau / BulwarkPlates * 0.78f;

        class BulwarkState
        {
            public float Cast = -9f, Seen = -9f, PrevT, End = -9f;
            public bool Broke;
            public int Alive = BulwarkPlates;
            public float[] LostAt = new float[BulwarkPlates];
            public List<BulwarkHit> Hits = new List<BulwarkHit>();
        }

        struct BulwarkHit { public float At, Ang; }

        readonly Dictionary<int, BulwarkState> bulwark = new Dictionary<int, BulwarkState>();

        static float BulwarkCenter(int k) => k * MathX.Tau / BulwarkPlates + MathX.Tau / (2 * BulwarkPlates);
        static float BulwarkEase(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
        /// a - b, wrapped to -pi..pi.
        static float BulwarkDiff(float a, float b)
        {
            float d = MathX.Fmod(a - b + MathX.Pi, MathX.Tau);
            return d - MathX.Pi;
        }
        static float BulwarkHash(int n) { float s = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return s - (float)System.Math.Floor(s); }

        BulwarkState BulwarkOf(int id)
        {
            BulwarkState st;
            if (!bulwark.TryGetValue(id, out st))
                bulwark[id] = st = new BulwarkState();
            return st;
        }

        /// Juice.cs, for a bullet an enemy fired that a blocker stopped: if it was a Barrier, remember where it hit.
        void NoteBarrierBlock(V2 pos)
        {
            if (!fxOn || game == null)
                return;
            foreach (var p in game.Players.Values)
            {
                if (p.BarrierT <= 0f || p.Downed || p.Disp.DistanceTo(pos) > p.BarrierRadius + 25f)
                    continue;
                var st = BulwarkOf(p.Id);
                if (st.Hits.Count >= 8)
                    st.Hits.RemoveAt(0);
                st.Hits.Add(new BulwarkHit { At = game.AnimT, Ang = (pos - p.Disp).Angle });
            }
        }

        /// The plate (still on at time `at`) nearest an angle, or -1.
        static int BulwarkNearest(BulwarkState st, float ang, float at)
        {
            int best = -1;
            float bd = 9f;
            for (int k = 0; k < BulwarkPlates; k++)
            {
                if (st.LostAt[k] < at - 0.05f)
                    continue;
                float d = Mathf.Abs(BulwarkDiff(BulwarkCenter(k), ang));
                if (d < bd)
                {
                    bd = d;
                    best = k;
                }
            }
            return best;
        }

        /// Keeps the cosmetic state in step with the replicated Barrier: a fresh cast, plates lost as it soaks, how it ended.
        void BulwarkUpdate(Player p, BulwarkState st, float t)
        {
            if (p.BarrierT > 0f)
            {
                if (st.PrevT <= 0f || t - st.Seen > 0.5f || t < st.Cast)
                {
                    st.Cast = t - Mathf.Max(0f, BulwarkBaseTime - p.BarrierT);  // first seen part-way through (a late join): already landed
                    st.End = -9f;
                    st.Broke = false;
                    st.Alive = BulwarkPlates;
                    st.Hits.Clear();
                    for (int k = 0; k < BulwarkPlates; k++)
                        st.LostAt[k] = float.PositiveInfinity;
                }
                int left = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(p.BarrierFrac) * BulwarkPlates - 0.02f), 1, BulwarkPlates);
                while (st.Alive > left)
                {
                    int pick = -1;
                    if (st.Hits.Count > 0 && t - st.Hits[st.Hits.Count - 1].At < 0.35f)
                        pick = BulwarkNearest(st, st.Hits[st.Hits.Count - 1].Ang, t + 1f);
                    for (int k = 0; pick < 0 && k < BulwarkPlates; k++)
                        if (st.LostAt[k] == float.PositiveInfinity)
                            pick = k;
                    if (pick < 0)
                        break;
                    st.LostAt[pick] = t;
                    st.Alive--;
                }
                st.Seen = t;
                st.PrevT = p.BarrierT;
            }
            else if (st.PrevT > 0f)
            {
                // Just ended. Down to its last few frames it ran out; with more time left it was shattered. Not seen for a
                // moment (they were down, or this screen wasn't drawing them): no ending to play.
                if (t - st.Seen < 0.25f)
                {
                    st.Broke = st.PrevT > 0.12f;
                    st.End = t;
                }
                else
                    st.End = -9f;
                st.PrevT = 0f;
            }
        }

        /// Under a player, from DrawPlayerAuras: the plates while the Barrier is up, and the way they leave.
        void DrawBulwark(Player p, float t)
        {
            var st = BulwarkOf(p.Id);
            BulwarkUpdate(p, st, t);
            bool alive = p.BarrierT > 0f;
            float exitAge = alive || st.End < 0f ? -1f : t - st.End;
            if (!alive && (exitAge < 0f || exitAge > 0.75f))
                return;
            float age = t - st.Cast, str = alive ? Mathf.Clamp01(p.BarrierFrac) : 0f;
            float rem = alive ? p.BarrierT : 0f;
            float k = Mathf.Min(1.4f, p.BarrierRadius / Game.BarrierRadius), R = p.BarrierRadius;
            bool failing = alive && (rem < 1f || str < 0.25f);
            float flick = failing && MathX.Fmod(t, 0.2f) >= 0.1f ? 0.45f : 1f;
            V2 o = p.Disp;

            if (alive)
            {
                Circle(o, R, new Rgba(0.4f, 0.7f, 1f, (0.03f + 0.06f * str) * Mathf.Clamp01(age / 0.3f)), 48);
                // a dashed inner ring, turning slowly
                float r2 = R - 15f * k, off = -t * 18f / r2;
                for (int i = 0; i < 24; i++)
                    Arc(o, r2, 1.4f, new Rgba(0.59f, 0.82f, 1f, 0.16f * str), 1, off + MathX.Tau * i / 24f, MathX.Tau / 24f * 0.33f);
            }

            for (int i = 0; i < BulwarkPlates; i++)
            {
                float ca = BulwarkCenter(i);
                float lostAge = st.LostAt[i] == float.PositiveInfinity ? -1f : t - st.LostAt[i];
                float offr = 0f, alpha = 1f, rot = 0f, flash = 0f;
                if (lostAge >= 0f)
                {
                    // knocked off: spins away outward
                    float q = lostAge / 0.6f;
                    if (q > 1f)
                        continue;
                    offr = 90f * BulwarkEase(q);
                    rot = (BulwarkHash(i + 3) - 0.5f) * 4f * q;
                    alpha = 1f - q;
                }
                else
                {
                    float delay = i * 0.035f, pr = BulwarkEase((age - delay) / 0.3f);
                    offr = (1f - pr) * 70f;
                    alpha = Mathf.Clamp01((age - delay) / 0.12f);
                    float landed = age - delay - 0.3f;
                    if (alive && landed > 0f && landed < 0.12f)
                        flash += 0.6f * (1f - landed / 0.12f);
                    foreach (var h in st.Hits)
                    {
                        float ha = t - h.At;
                        if (ha < 0f || ha > 0.4f || BulwarkNearest(st, h.Ang, h.At) != i)
                            continue;
                        offr -= 10f * (1f - ha / 0.4f) * (1f - ha / 0.4f);
                        if (ha < 0.22f)
                            flash += 1f - ha / 0.22f;
                    }
                    if (!alive)
                    {
                        if (st.Broke)
                        {
                            float q = exitAge / 0.7f;
                            if (q > 1f)
                                continue;
                            offr += 110f * BulwarkEase(q) * (0.7f + BulwarkHash(i) * 0.6f);
                            rot = (BulwarkHash(i + 9) - 0.5f) * 5f * q;
                            alpha *= 1f - q;
                        }
                        else
                        {
                            float q = Mathf.Clamp01((exitAge - i * 0.02f) / 0.4f);
                            offr += 38f * BulwarkEase(q);
                            alpha *= 1f - q;
                        }
                    }
                    else
                        alpha *= flick;
                }
                if (alpha <= 0.01f)
                    continue;
                flash = Mathf.Clamp01(flash);
                float a0 = ca - BulwarkSpan / 2f + rot, sweep = BulwarkSpan, rr = R - 4f * k + offr;
                var mid = new Rgba(0.26f, 0.49f, 0.78f).Mix(Rgba.White, flash);
                Arc(o, rr, 13f * k, new Rgba(0.12f, 0.26f, 0.46f, alpha), 8, a0, sweep);
                Arc(o, rr, 8f * k, mid.WithAlpha(alpha), 8, a0 + 0.02f, sweep - 0.04f);
                Arc(o, rr + 5.5f * k, 2f, new Rgba(0.78f, 0.89f, 1f, alpha * 0.9f), 8, a0 + 0.02f, sweep - 0.04f);
                if (alive)
                {
                    float gl = MathX.Fmod(t * 0.9f, MathX.Tau), gd = Mathf.Abs(BulwarkDiff(gl, ca));
                    if (gd < BulwarkSpan / 2f)
                    {
                        float ga = ca + BulwarkDiff(gl, ca);
                        Arc(o, rr + 5.5f * k, 3f, new Rgba(1f, 1f, 1f, alpha * 0.9f), 1, ga - 0.05f, 0.1f);
                    }
                }
                for (int s = -1; s <= 1; s += 2)
                    Circle(o + V2.FromAngle(ca + s * BulwarkSpan * 0.36f + rot) * rr, 1.6f, new Rgba(0.86f, 0.93f, 1f, alpha * 0.9f), 6);
                if (lostAge >= 0f && lostAge < 0.25f)
                {
                    V2 at = o + V2.FromAngle(ca) * (R - 4f * k);
                    float f = lostAge / 0.25f;
                    for (int j = 0; j < 5; j++)
                    {
                        V2 d = V2.FromAngle(ca + (j - 2) * 0.5f);
                        Line(at, at + d * (8f + 26f * f), 2f, new Rgba(1f, 0.86f, 0.63f, 1f - f));
                    }
                }
            }

            if (alive)
            {
                foreach (var h in st.Hits)
                {
                    float ha = t - h.At;
                    if (ha < 0f || ha >= 0.2f)
                        continue;
                    V2 at = o + V2.FromAngle(h.Ang) * R;
                    float sz = 12f * (1f - ha / 0.2f);
                    Line(at - new V2(sz, 0f), at + new V2(sz, 0f), 2.4f, new Rgba(1f, 1f, 1f, 0.95f));
                    Line(at - new V2(0f, sz), at + new V2(0f, sz), 2.4f, new Rgba(1f, 1f, 1f, 0.95f));
                }
                if (age < 0.45f && age > 0.15f)
                {
                    float f = (age - 0.15f) / 0.3f;
                    Arc(o, R * (0.4f + 0.75f * f), 4f, new Rgba(0.59f, 0.82f, 1f, 0.5f * (1f - f)), 40);
                }
            }
        }
    }
}
