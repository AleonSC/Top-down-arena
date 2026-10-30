using UnityEngine;

namespace Arena
{
    /// How the Storm Herald's fight looks (Herald.cs): rain and wind streaks in the Gale, a darker sky in the
    /// Supercell, lightning rods and the arcs between them, ball lightning and its arcs, the Conductor's ring, the
    /// last stand's spokes, and the storm cloud around the Herald itself. Built from the mockup in
    /// Docs/Mockups/storm-herald-3-stage.html.
    public partial class ArenaApp
    {
        static readonly Rgba StormBlue = new Rgba(0.56f, 0.82f, 1f);
        static readonly Rgba StormWhite = new Rgba(0.85f, 0.93f, 1f);
        static readonly Rgba StormViolet = new Rgba(0.75f, 0.6f, 1f);

        static Enemy FindHerald(Game g)
        {
            foreach (var e in g.Enemies.Values)
                if (e.Type == EnemyType.StormHerald && e.Herald != null && !g.IsSpawning(e))
                    return e;
            return null;
        }

        /// On the floor: the Supercell's dark sky, then rain (slanting with the Gale's wind) and wind streaks.
        void DrawHeraldFloor(float t)
        {
            var e = FindHerald(game);
            if (e == null)
                return;
            var s = e.Herald;
            if (s.Stage == HeraldStage.Supercell && s.TransT <= 0f)
                FillRect(0f, 0f, Game.Width, Game.Height, new Rgba(0.02f, 0.01f, 0.06f, 0.35f));
            if (s.Stage == HeraldStage.Gathering || s.TransT > 0f)
                return;
            float wind = s.Wind;
            float slant = s.Stage == HeraldStage.Gale ? wind * 24f : 6f;
            var rain = new Rgba(0.6f, 0.72f, 0.95f, 0.16f);
            for (int k = 0; k < 90; k++)
            {
                float x = Fmod(k * 197f + t * (s.Stage == HeraldStage.Gale ? wind * 260f : 40f), Game.Width + 200f) - 100f;
                float y = Fmod(k * 131f + t * 700f, Game.Height + 100f) - 50f;
                Line(new V2(x, y), new V2(x + slant, y + 26f), 1.5f, rain);
            }
            if (s.Stage != HeraldStage.Gale || s.Wind == 0)
                return;
            var streak = new Rgba(0.75f, 0.85f, 1f, 0.2f);
            for (int k = 0; k < 24; k++)
            {
                float y = 60f + (k * 37f) % (Game.Height - 120f), sp = 300f + (k % 5) * 60f;
                float x = Fmod(k * 271f + t * sp * wind, Game.Width + 300f) - 150f;
                Line(new V2(x, y), new V2(x + wind * (60f + (k % 3) * 30f), y), 2f, streak);
            }
        }

        static float Fmod(float a, float b) => MathX.Fmod(a, b);

        /// Over the floor, under the enemies: rods and their arcs, ball lightning's arcs, the Conductor, the spokes.
        void DrawHeraldHazards(float t)
        {
            var e = FindHerald(game);
            int seed = (int)(t * 14f);
            // Ball lightning arcs to its neighbours whenever two are close.
            foreach (var a in game.Enemies.Values)
            {
                if (a.Type != EnemyType.BallLightning || game.IsSpawning(a))
                    continue;
                foreach (var b in game.Enemies.Values)
                    if (b.Type == EnemyType.BallLightning && b.Id > a.Id && !game.IsSpawning(b) && a.Disp.DistanceTo(b.Disp) < Game.BallArcRange)
                        DrawBolt(a.Disp, b.Disp, 2.5f, new Rgba(0.7f, 0.86f, 1f), seed + a.Id * 7 + b.Id);
            }
            if (e == null)
                return;
            var s = e.Herald;
            // Rods: a marked spot while one falls, then the rod.
            foreach (var r in s.Rods)
            {
                if (r.Fall > 0f)
                {
                    float k = 1f - Mathf.Clamp01(r.Fall / Game.RodFall);
                    Circle(r.Pos, 26f * (0.4f + 0.6f * k), new Rgba(0f, 0f, 0f, 0.3f + 0.3f * k), 16);
                    Arc(r.Pos, 26f, 2f, StormBlue.WithAlpha(0.8f), 20);
                    Circle(r.Pos - new V2(0f, (1f - k) * 300f), 6f, StormWhite.WithAlpha(k), 10);
                    continue;
                }
                Circle(r.Pos + new V2(0f, 6f), 12f, new Rgba(0f, 0f, 0f, 0.45f), 14);
                FillRect(r.Pos.X - 5f, r.Pos.Y - 30f, 10f, 34f, new Rgba(0.29f, 0.35f, 0.46f));
                Circle(r.Pos - new V2(0f, 32f), 14f, StormBlue.WithAlpha(0.25f), 14);
                Circle(r.Pos - new V2(0f, 32f), 7f + 1.5f * Mathf.Sin(t * 12f + r.Pos.X), StormWhite, 12);
            }
            // Arcs: a dashed flicker first, then the bolt.
            foreach (var a in s.Arcs)
            {
                if (a.A >= s.Rods.Count || a.B >= s.Rods.Count)
                    continue;
                V2 pa = s.Rods[a.A].Pos - new V2(0f, 32f), pb = s.Rods[a.B].Pos - new V2(0f, 32f);
                if (a.T < a.Warn)
                {
                    if (Mathf.Sin(t * 50f) > -0.2f)
                        DashedLine(pa, pb, 2f, new Rgba(0.63f, 0.82f, 1f, 0.25f + 0.35f * a.T / a.Warn));
                }
                else if (Game.ArcLiveNow(a))
                    DrawBolt(pa, pb, 4f, StormWhite, seed + a.A * 11 + a.B);
            }
            // The Conductor's ring follows the marked player.
            Player c;
            if (s.CondId != 0 && game.Players.TryGetValue(s.CondId, out c) && !c.Downed)
            {
                float k = 1f - Mathf.Clamp01(s.CondT / Game.ConductorTime);
                Circle(c.Disp, Game.ConductorRadius, StormViolet.WithAlpha(0.05f + 0.08f * k), 48);
                float blink = 0.6f + 0.4f * Mathf.Sin(t * (10f + 20f * k));
                for (int i = 0; i < 24; i += 2)  // a dashed ring, turning
                    Arc(c.Disp, Game.ConductorRadius, 3f, StormViolet.WithAlpha(blink), 4, t + MathX.Tau * i / 24f, MathX.Tau / 24f);
                for (int i = 0; i < 3; i++)  // sparks crawling over the marked player
                {
                    V2 p0 = c.Disp + V2.FromAngle(t * 9f + i * 2.1f) * 16f;
                    DrawBolt(p0, p0 + V2.FromAngle(t * 23f + i) * 12f, 1.5f, StormViolet, seed + i);
                }
            }
            // The last stand: four spokes turning round the Herald.
            if (s.Stage == HeraldStage.LastStand && s.TransT <= 0f)
                for (int k = 0; k < 4; k++)
                {
                    V2 tip = e.Disp + V2.FromAngle(s.Spin + k * MathX.Pi / 2f) * Game.SpokeLength;
                    DrawBolt(e.Disp, tip, 4f, new Rgba(0.83f, 0.55f, 1f), seed + k * 13, 18);
                }
        }

        /// Jagged lightning from a to b: a soft glow and a bright core. `seed` keeps its shape for a few frames.
        void DrawBolt(V2 a, V2 b, float width, Rgba col, int seed, int segments = 0)
        {
            int n = segments > 0 ? segments : Mathf.Max(4, (int)(a.DistanceTo(b) / 40f));
            V2 side = (b - a).Normalized().Orthogonal(), prev = a;
            for (int k = 1; k <= n; k++)
            {
                float j = k == n ? 0f : (Mathf.Abs(Mathf.Sin(seed * 12.9898f + k * 78.233f) * 43758.5453f) % 1f - 0.5f) * 36f;
                V2 pt = V2.Lerp(a, b, k / (float)n) + side * j;
                Line(prev, pt, width * 3.5f, col.WithAlpha(0.22f));
                Line(prev, pt, width, col);
                prev = pt;
            }
        }

        void DashedLine(V2 a, V2 b, float width, Rgba col)
        {
            float len = a.DistanceTo(b);
            V2 dir = (b - a) / Mathf.Max(1f, len);
            for (float d = 0f; d < len; d += 22f)
                Line(a + dir * d, a + dir * Mathf.Min(len, d + 10f), width, col);
        }

        /// Under the Herald's body: its storm cloud, darker and wider each stage, a glow, and arcs crawling over it.
        void DrawHeraldUnderlay(Enemy e, V2 pos, float r, float t)
        {
            var s = e.Herald;
            int stage = s == null ? 0 : (int)s.Stage;
            var cloud = stage >= 2 ? new Rgba(0.16f, 0.12f, 0.27f, 0.75f) : new Rgba(0.2f, 0.24f, 0.38f, 0.7f);
            for (int k = 0; k < 7; k++)
            {
                float a = t * 0.3f + k * MathX.Tau / 7f, rr = r + 22f + stage * 10f + Mathf.Sin(t * 1.3f + k) * 6f;
                Circle(pos + new V2(Mathf.Cos(a) * rr * 0.8f, Mathf.Sin(a) * rr * 0.45f - 6f), 26f + stage * 4f, cloud, 18);
            }
            Circle(pos, r + 30f, StormBlue.WithAlpha(0.12f + 0.06f * Mathf.Sin(t * 6f)), 28);
            if (Mathf.Sin(t * 7f) > 0.3f)
            {
                float a = t * 3f;
                DrawBolt(pos + V2.FromAngle(a) * r, pos + V2.FromAngle(a + 1.4f) * r, 2f, StormWhite, (int)(t * 14f), 4);
            }
            if (s != null && s.Wind != 0 && s.TransT <= 0f)  // a wind sock of streaks off its body
                for (int k = 0; k < 3; k++)
                    Line(pos + new V2(-s.Wind * r * 0.5f, (k - 1) * 12f), pos + new V2(s.Wind * (r + 30f + 10f * Mathf.Sin(t * 8f + k)), (k - 1) * 12f), 2f, StormWhite.WithAlpha(0.35f));
        }

        /// Ball lightning's body: a bright core that flickers faster as it's about to pop.
        void DrawBallLightning(Enemy e, V2 pos, float r, float t)
        {
            Circle(pos, r + 11f, StormBlue.WithAlpha(0.18f), 18);
            Circle(pos, r * 0.5f, Rgba.White, 12);
            float age = e.T;
            if (age > Game.BallLife - 0.7f)
                Arc(pos, r + 7f, 2f, Rgba.White.WithAlpha(0.5f + 0.5f * Mathf.Sin(t * 40f)), 18);
            DrawBolt(pos + V2.FromAngle(t * 11f + e.Id) * r, pos + V2.FromAngle(t * 11f + e.Id + 2f) * (r + 6f), 1.5f, StormWhite, (int)(t * 20f) + e.Id, 3);
        }
    }
}
