using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The trails a rank unlocks (Rank.cs: Ember 8, Frost 14, Storm 18, Void 22, Gold stardust 25, Prism 35, Light Cycle 45), drawn behind a player as they move. Drawing
    /// only: each screen keeps the last second or so of every player's path itself, and the lobby entry (Session.Lobby)
    /// says which trail each player has, so everyone sees them.
    public partial class ArenaApp
    {
        class TrailPoint
        {
            public V2 At;
            public float T;
        }

        const float TrailLife = 0.9f;
        readonly Dictionary<int, List<TrailPoint>> trails = new Dictionary<int, List<TrailPoint>>();

        void DrawRankTrails(float t)
        {
            if (game.ShowcaseMode)
                return;
            foreach (var p in game.Players.Values)
            {
                List<TrailPoint> pts;
                if (!trails.TryGetValue(p.Id, out pts))
                    trails[p.Id] = pts = new List<TrailPoint>();
                LobbyEntry e;
                int kind = session.Lobby.TryGetValue(p.Id, out e) ? e.Trail : 0;
                if (kind != 0 && Rank.TrailId(kind) == CycleId)  // the Light Cycle keeps one point per corner (LightCycleTrail.cs)
                {
                    CycleRider cr;
                    if (!cycles.TryGetValue(p.Id, out cr))
                        cycles[p.Id] = cr = new CycleRider();
                    pts.Clear();
                    if (!p.Downed)
                        cr.Step(p.Disp, t);
                    DrawCycleTrail(cr, t, V2.Zero);
                    continue;
                }
                cycles.Remove(p.Id);
                // Keep the path while they have a trail: a point every few pixels, the oldest dropped as it fades.
                if (kind != 0 && !p.Downed && (pts.Count == 0 || (pts[pts.Count - 1].At - p.Disp).Length > 7f))
                    pts.Add(new TrailPoint { At = p.Disp, T = t });
                while (pts.Count > 0 && t - pts[0].T > TrailLife)
                    pts.RemoveAt(0);
                if (kind == 0 || pts.Count < 2)
                    continue;
                string id = Rank.TrailId(kind);
                for (int i = 1; i < pts.Count; i++)
                {
                    if ((pts[i].At - pts[i - 1].At).Length > 120f)
                        continue;  // wrapped round the edge: no line across the arena
                    TrailPiece(id, pts[i].At, pts[i - 1].At, i, Mathf.Clamp01((t - pts[i].T) / TrailLife), t, p.Disp);
                }
            }
        }

        /// A pastel rainbow: h goes round once every 1.0.
        static Rgba Spectrum(float h)
        {
            h -= (float)System.Math.Floor(h);
            return new Rgba(0.675f + 0.325f * Mathf.Cos(6.2832f * h), 0.675f + 0.325f * Mathf.Cos(6.2832f * (h - 0.3333f)), 0.675f + 0.325f * Mathf.Cos(6.2832f * (h - 0.6667f)));
        }

        static float TrailHash(int n)
        {
            float x = Mathf.Sin(n * 12.9898f) * 43758.5453f;
            return x - (float)System.Math.Floor(x);
        }

        /// One step of a trail: the piece from q (older) to p (newer), the i-th point of the path, age 0 (new) to 1 (gone).
        /// head is where the player is now. Shared with the cosmetics window's samples (CosmeticsUi.DrawTrailSample).
        void TrailPiece(string id, V2 p, V2 q, int i, float age, float t, V2 head)
        {
            float a = 1f - age;
            V2 d = p - q;
            float len = d.Length;
            V2 n = len > 0.01f ? new V2(-d.Y / len, d.X / len) : V2.Zero;  // sideways
            switch (id)
            {
                case "trail-gold":
                    Line(q, p, 3f * a + 0.5f, new Rgba(1f, 0.85f, 0.4f, 0.22f * a));
                    if (i % 2 == 0)  // sparkles that twinkle as they fade
                    {
                        float tw = 0.5f + 0.5f * Mathf.Sin(t * 14f + i * 1.7f);
                        Circle(p + V2.FromAngle(i * 2.4f) * (4f + 6f * age), 1.2f + 1.4f * a * tw, new Rgba(1f, 0.95f, 0.75f, 0.85f * a * tw), 6);
                    }
                    break;
                case "trail-frost":  // a chill line, with ice crystals that stay where they formed, glint and melt
                    Line(q, p, 3.5f * a + 0.5f, new Rgba(0.55f, 0.85f, 1f, 0.2f * a));
                    if (i % 2 == 0)
                    {
                        float g = 0.55f + 0.45f * Mathf.Sin(t * 9f + i * 1.9f);
                        V2 c = p + n * ((i % 4 == 0 ? 1f : -1f) * (5f + 3f * age));
                        Circle(c, 2.2f + 2.4f * a, new Rgba(0.75f, 0.93f, 1f, 0.8f * a * g), 4);
                        Circle(c, 0.9f * a + 0.2f, new Rgba(1f, 1f, 1f, a), 6);
                    }
                    break;
                case "trail-storm":  // static: a jagged thread that re-rolls every few frames, with the odd fork
                {
                    float j = (TrailHash(i + Mathf.FloorToInt(t * 18f) * 7) - 0.5f);
                    V2 m = (p + q) * 0.5f + n * (j * 16f * a);
                    Line(q, m, 3.2f * a + 0.4f, new Rgba(0.6f, 0.75f, 1f, 0.22f * a));
                    Line(m, p, 3.2f * a + 0.4f, new Rgba(0.6f, 0.75f, 1f, 0.22f * a));
                    Line(q, m, 1.3f * a + 0.3f, new Rgba(1f, 0.97f, 0.7f, 0.9f * a));
                    Line(m, p, 1.3f * a + 0.3f, new Rgba(1f, 0.97f, 0.7f, 0.9f * a));
                    if (i % 5 == 0 && j > 0f)
                        Line(m, m + n * (14f * a), 0.9f, new Rgba(1f, 1f, 1f, 0.8f * a));
                    break;
                }
                case "trail-void":  // a dark ribbon with a violet edge; motes are pulled back toward the player
                    Line(q, p, 8f * a + 1f, new Rgba(0.45f, 0.15f, 0.85f, 0.2f * a));
                    Line(q, p, 5f * a + 0.5f, new Rgba(0.05f, 0.02f, 0.12f, 0.75f * a));
                    if (i % 2 == 0)
                    {
                        float ph = i * 2.1f + t * 3f;
                        V2 m = head + (p - head) * (1f - 0.55f * age) + new V2(Mathf.Cos(ph), Mathf.Sin(ph)) * (6f * a);
                        Circle(m, 1.1f + 1.6f * a, new Rgba(0.78f, 0.5f, 1f, 0.8f * a), 6);
                    }
                    break;
                case "trail-prism":  // three ribbons that cycle through the spectrum along their length, with glints
                    for (int k = -1; k <= 1; k++)
                    {
                        Rgba c = Spectrum(t * 0.33f + i * 0.04f + k * 0.14f);
                        Line(q + n * (k * 5f * a), p + n * (k * 5f * a), 2.4f * a + 0.4f, c.WithAlpha(0.55f * a));
                    }
                    if (i % 3 == 0)
                    {
                        float tw = 0.5f + 0.5f * Mathf.Sin(t * 16f + i);
                        Circle(p + n * (9f * Mathf.Sin(i)), 1f + 1.3f * a * tw, new Rgba(1f, 1f, 1f, 0.9f * a * tw), 6);
                    }
                    break;
                default:  // Ember
                {
                    Line(q, p, 5f * a + 0.5f, new Rgba(1f, 0.4f, 0.1f, 0.16f * a));
                    float flick = 0.6f + 0.4f * Mathf.Sin(t * 20f + i * 2.3f);
                    Circle(p + new V2(0f, -10f * age), 1.6f + 2.2f * a * flick, new Rgba(1f, 0.55f + 0.3f * a, 0.15f, 0.75f * a), 7);  // embers drift up as they die
                    break;
                }
            }
        }
    }
}
