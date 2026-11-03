using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The trails a rank unlocks (Rank.cs: Ember at rank 8, Gold stardust at 25), drawn behind a player as they move. Drawing
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
                // Keep the path while they have a trail: a point every few pixels, the oldest dropped as it fades.
                if (kind != 0 && !p.Downed && (pts.Count == 0 || (pts[pts.Count - 1].At - p.Disp).Length > 7f))
                    pts.Add(new TrailPoint { At = p.Disp, T = t });
                while (pts.Count > 0 && t - pts[0].T > TrailLife)
                    pts.RemoveAt(0);
                if (kind == 0 || pts.Count < 2)
                    continue;
                bool gold = Rank.TrailId(kind) == "trail-gold";
                for (int i = 1; i < pts.Count; i++)
                {
                    float age = Mathf.Clamp01((t - pts[i].T) / TrailLife), a = 1f - age;
                    if ((pts[i].At - pts[i - 1].At).Length > 120f)
                        continue;  // wrapped round the edge: no line across the arena
                    if (gold)
                    {
                        Line(pts[i - 1].At, pts[i].At, 3f * a + 0.5f, new Rgba(1f, 0.85f, 0.4f, 0.22f * a));
                        if (i % 2 == 0)  // sparkles that twinkle as they fade
                        {
                            float tw = 0.5f + 0.5f * Mathf.Sin(t * 14f + i * 1.7f);
                            V2 s = pts[i].At + V2.FromAngle(i * 2.4f) * (4f + 6f * age);
                            Circle(s, 1.2f + 1.4f * a * tw, new Rgba(1f, 0.95f, 0.75f, 0.85f * a * tw), 6);
                        }
                    }
                    else
                    {
                        Line(pts[i - 1].At, pts[i].At, 5f * a + 0.5f, new Rgba(1f, 0.4f, 0.1f, 0.16f * a));
                        float flick = 0.6f + 0.4f * Mathf.Sin(t * 20f + i * 2.3f);
                        V2 s = pts[i].At + new V2(0f, -10f * age);  // embers drift up as they die
                        Circle(s, 1.6f + 2.2f * a * flick, new Rgba(1f, 0.55f + 0.3f * a, 0.15f, 0.75f * a), 7);
                    }
                }
            }
        }
    }
}
