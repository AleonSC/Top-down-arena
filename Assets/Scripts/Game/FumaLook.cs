using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Fuma Shuriken's look (Docs/Mockups/fuma-shuriken-rework.html): the hooked four-bladed star, thrown or circling you, with its
    /// trail. The Orbit stance's star is placed by Game.FumaPose from a clock: each screen runs its own copy of that clock every frame
    /// and eases it toward the host's (FumaView), so the star moves as smoothly as the mockup however often snapshots come.
    ///   - DrawFumaWorld (once a frame, with the players): the circling stars, the stolen stars, the star going back to your hand;
    ///     without Visual effects their trails too.
    ///   - DrawFumaGlow (the additive glow pass, Visual effects on): the trails as glowing ribbons and a halo round each star.
    ///   - DrawFumaShot (WorldShots.cs): a thrown star, with a trail from where it has been.
    public partial class ArenaApp
    {
        static readonly Rgba FumaBlade = new Rgba(0.79f, 0.8f, 0.88f), FumaEdge = new Rgba(0.96f, 0.96f, 1f), FumaHub = new Rgba(0.23f, 0.24f, 0.32f);
        const int FumaTrailN = 12;
        const float FumaTrailStep = 0.014f;  // seconds between trail samples

        class FumaView
        {
            public float Clock, Seen = -1f, Age, RetT;
            public byte Stance, Minis;
            public V2 RetFrom;
            public readonly float[] MiniBorn = new float[Game.FumaStealCap];
        }

        readonly Dictionary<int, FumaView> fumaViews = new Dictionary<int, FumaView>();
        readonly Dictionary<int, List<V2>> fumaShotTrail = new Dictionary<int, List<V2>>();
        readonly List<int> fumaGone = new List<int>();

        /// This screen's clock for a player's orbit: it runs every frame and is eased toward the host's (snapping when far off).
        FumaView FumaViewOf(Player p, float dt)
        {
            FumaView v;
            if (!fumaViews.TryGetValue(p.Id, out v))
            {
                fumaViews[p.Id] = v = new FumaView();
                v.Minis = p.FumaMinis;  // (stars already circling when this screen first sees them don't fly in again)
                for (int i = 0; i < Game.FumaStealCap; i++)
                    v.MiniBorn[i] = -9f;
            }
            if (p.FumaStance == 1)
            {
                if (p.FumaClock != v.Seen)
                {
                    v.Seen = p.FumaClock;
                    v.Age = 0f;
                }
                else
                    v.Age += dt;
                float est = p.FumaClock + Mathf.Min(v.Age, 0.25f);  // (where the host's clock should be by now)
                if (v.Stance != 1 || Mathf.Abs(est - v.Clock) > 0.25f)
                    v.Clock = est;
                else
                    v.Clock += dt + (est - v.Clock) * Mathf.Min(1f, 8f * dt);
            }
            else if (v.Stance == 1)
            {
                v.RetT = 0.18f;  // back to the hand: from wherever it was drawn last
                v.RetFrom = Game.FumaPose(p, v.Clock, 0, Mathf.Max(1, (int)p.FumaStars));
            }
            v.RetT = Mathf.Max(0f, v.RetT - dt);
            for (int i = v.Minis; i < p.FumaMinis && i < Game.FumaStealCap; i++)
                v.MiniBorn[i] = v.Clock;
            v.Minis = p.FumaMinis;
            v.Stance = p.FumaStance;
            return v;
        }

        /// The star is still flying back to the hand after the Orbit stance ended.
        bool FumaReturning(Player p)
        {
            FumaView v;
            return fumaViews.TryGetValue(p.Id, out v) && v.RetT > 0f;
        }

        /// Is this star part of a Lash or the Cyclone right now (drawn hot)?
        static bool FumaHotNow(Player p, float clock, int i)
        {
            if (p.FumaMove == 0 || i != 0 && p.FumaMove != 3)
                return false;
            float u = (clock - p.FumaMoveAt) / Game.FumaMoveTime(p.FumaMove);
            return u >= 0f && u <= 1f;
        }

        // --- Once a frame -------------------------------------------------------------------------------------------------

        void DrawFumaWorld(float t)
        {
            float dt = ViewDt;
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon == null || p.Weapon.Id != WeaponId.FumaShuriken)
                    continue;
                var v = FumaViewOf(p, dt);
                if (p.Downed)
                    continue;
                float spin = (p.FumaSpin < 0f ? -1f : 1f) * t * 22f;
                if (p.FumaStance == 1)
                {
                    int n = Mathf.Max(1, (int)p.FumaStars);
                    if (!Juicy)
                        for (int i = 0; i < n; i++)
                            DrawFumaTrail(p, v.Clock, i, n, 0.45f);
                    for (int i = 0; i < n; i++)
                        DrawFumaBlades(p.Disp + Game.FumaPose(p, v.Clock, i, n), 1.15f, spin + i, FumaHotNow(p, v.Clock, i) ? 1f : 0f, 1f);
                }
                else if (v.RetT > 0f)
                {
                    float k = 1f - v.RetT / 0.18f, e = k * k * (3f - 2f * k);
                    V2 hand = V2.FromAngle(p.Aim) * 12f;
                    DrawFumaBlades(p.Disp + v.RetFrom + (hand - v.RetFrom) * e, 1f - 0.5f * e, spin, 0f, 1f);
                }
                for (int i = 0; i < p.FumaMinis && i < Game.FumaStealCap; i++)  // stolen shots, circling as small hot stars
                {
                    float k = Mathf.Clamp01((v.Clock - v.MiniBorn[i]) / 0.17f), e = 1f - (1f - k) * (1f - k) * (1f - k);
                    V2 from = V2.FromAngle(p.Aim) * (p.Def.BodyR + Game.ParryReach);
                    V2 at = from + (Game.FumaMiniPose(p, v.Clock, i) - from) * e;
                    DrawFumaBlades(p.Disp + at, 0.45f, -spin * 1.6f + i, 1f, 1f);
                }
            }
            // forget the trails of stars no longer in flight
            fumaGone.Clear();
            foreach (var id in fumaShotTrail.Keys)
                if (!game.Bullets.ContainsKey(id))
                    fumaGone.Add(id);
            foreach (int id in fumaGone)
                fumaShotTrail.Remove(id);
        }

        /// The glow pass: trails as glowing ribbons and a halo round each star.
        void DrawFumaGlow()
        {
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon == null || p.Weapon.Id != WeaponId.FumaShuriken || p.Downed || p.FumaStance != 1)
                    continue;
                var v = FumaViewOf(p, 0f);  // (the clock moves on in DrawFumaWorld)
                int n = Mathf.Max(1, (int)p.FumaStars);
                float r = Game.FumaOrbitR * Mathf.Clamp01(v.Clock / Game.FumaOutTime);
                Arc(p.Disp, r, 2f, Game.FumaInk.WithAlpha(0.07f), 48);  // the circle it keeps
                for (int i = 0; i < n; i++)
                {
                    DrawFumaTrail(p, v.Clock, i, n, 1f);
                    bool hot = FumaHotNow(p, v.Clock, i);
                    GlowDisc(p.Disp + Game.FumaPose(p, v.Clock, i, n), 38f, hot ? Game.FumaHot : Game.FumaInk, hot ? 0.5f : 0.3f);
                }
                for (int i = 0; i < p.FumaMinis && i < Game.FumaStealCap; i++)
                    GlowDisc(p.Disp + Game.FumaMiniPose(p, v.Clock, i), 18f, Game.FumaHot, 0.4f);
            }
            foreach (var kv in fumaShotTrail)
                DrawFumaPathTrail(kv.Value, Game.FumaInk, 1f);
        }

        /// A circling star's trail: the same circle a moment ago, sampled back along the clock, so it bends with the orbit.
        void DrawFumaTrail(Player p, float clock, int i, int n, float alpha)
        {
            bool hot = FumaHotNow(p, clock, i);
            Rgba c = hot ? Game.FumaHot : Game.FumaInk;
            V2 prev = p.Disp + Game.FumaPose(p, clock, i, n);
            for (int k = 1; k <= FumaTrailN; k++)
            {
                float back = clock - k * FumaTrailStep;
                if (back < 0f)
                    break;
                V2 at = p.Disp + Game.FumaPose(p, back, i, n);
                float f = 1f - (k - 1f) / FumaTrailN;
                if ((at - prev).LengthSq > 0.25f)
                    Line(prev, at, 16f * f, c.WithAlpha(0.6f * f * alpha));
                prev = at;
            }
        }

        void DrawFumaPathTrail(List<V2> pts, Rgba c, float alpha)
        {
            for (int k = pts.Count - 1; k > 0; k--)
            {
                float f = k / (float)pts.Count;
                Line(pts[k], pts[k - 1], 14f * f, c.WithAlpha(0.45f * f * alpha));
            }
        }

        /// A thrown Fuma (WorldShots.cs): its trail, then the blades.
        void DrawFumaShot(Bullet b, float t)
        {
            List<V2> trail;
            if (!fumaShotTrail.TryGetValue(b.Id, out trail))
                fumaShotTrail[b.Id] = trail = new List<V2>();
            if (trail.Count == 0 || (trail[trail.Count - 1] - b.Pos).LengthSq > 9f)
            {
                trail.Add(b.Pos);
                if (trail.Count > FumaTrailN)
                    trail.RemoveAt(0);
            }
            if (!Juicy)
                DrawFumaPathTrail(trail, Game.FumaInk, 0.45f);
            DrawFumaBlades(b.Pos, (b.Radius + 4f) / 24f, t * 22f * (b.AngVel < 0f ? -1f : 1f) + b.Id, 0f, 1f);
        }

        // --- The star ---------------------------------------------------------------------------------------------------

        /// The Fuma: four hooked blades round a hub, scale 1 is 24 px to a tip. hot: 0 to 1, how much it glows the Lash's gold.
        void DrawFumaBlades(V2 c, float scale, float rot, float hot, float alpha)
        {
            float R = 24f * scale;
            Rgba fill = FumaBlade.Mix(Game.FumaHot, 0.35f * hot).WithAlpha(alpha), edge = FumaEdge.WithAlpha(alpha);
            Arc(c, R * 0.82f, 2.5f, (hot > 0f ? Game.FumaHot : Game.FumaInk).WithAlpha(0.3f * alpha), 20);  // the blur ring
            for (int k = 0; k < 4; k++)
            {
                float a = rot + k * MathX.Pi / 2f;
                V2 b1 = c + V2.FromAngle(a - 0.55f) * (R * 0.25f), c1 = c + V2.FromAngle(a - 0.5f) * (R * 0.62f), tip = c + V2.FromAngle(a) * R;
                V2 c2 = c + V2.FromAngle(a + 0.2f) * (R * 0.5f), b2 = c + V2.FromAngle(a + 0.6f) * (R * 0.29f);
                SetColor(fill);
                V2 prev = b1;
                for (int s = 1; s <= 4; s++)  // the hooked leading edge
                {
                    V2 q = Bez(b1, c1, tip, s / 4f);
                    Tri(c, prev, q);
                    prev = q;
                }
                for (int s = 1; s <= 3; s++)  // the trailing edge back to the hub
                {
                    V2 q = Bez(tip, c2, b2, s / 3f);
                    Tri(c, prev, q);
                    prev = q;
                }
                prev = b1;
                for (int s = 1; s <= 4; s++)
                {
                    V2 q = Bez(b1, c1, tip, s / 4f);
                    Line(prev, q, 1.2f, edge);
                    prev = q;
                }
            }
            Circle(c, R * 0.22f, FumaHub.WithAlpha(alpha), 10);
            Arc(c, R * 0.22f, 1.4f, edge.WithAlpha(0.8f * alpha), 10);
        }
    }
}
