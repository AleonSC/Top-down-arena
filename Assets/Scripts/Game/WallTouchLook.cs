using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// A player touching a wall (any wall, pillar or cover of the floor plan or the rooms) charges it: jagged arcs of
    /// electricity crawl along the face from the touch, flickering, with sparks and a little bolt from the body, in the
    /// wall's own colour. Everyone's touches show; the hum (a neon-tube 60 Hz loop, WallHumSoundBank.cs) follows the
    /// strongest, quieter the further it is from you. Local look only: worked out from the players' positions.
    public partial class ArenaApp
    {
        public static bool WallTouchPreview;   // previews: every touch at full strength (WALLTOUCH)
        public static float WallTouchT = -1f;  // previews: the effect's clock (WALLT)
        const float WallTouchReach = 3f, WallHumFar = 700f;
        readonly Dictionary<int, float> wallTouchLvl = new Dictionary<int, float>();

        /// Each frame (ArenaApp.Update): every player's touch eases in (0.1 s) and out (0.2 s); the hum follows.
        void TickWallTouch(float dt)
        {
            var d = game.ActiveLayout;
            var me = game.Me;
            float hum = 0f;
            foreach (var p in game.Players.Values)
            {
                bool on = d != null && !p.Downed && TouchingWall(d, p);
                float l;
                wallTouchLvl.TryGetValue(p.Id, out l);
                l = MathX.MoveTowards(l, on ? 1f : 0f, dt * (on ? 10f : 5f));
                wallTouchLvl[p.Id] = l;
                if (l > 0f && me != null)
                    hum = Mathf.Max(hum, l * Mathf.Clamp01(1f - p.Disp.DistanceTo(me.Disp) / WallHumFar));
            }
            sounds.SetWallHum(hum);
        }

        static bool TouchingWall(LayoutDef d, Player p)
        {
            V2 at, n;
            foreach (var s in d.Solids)
                if (WallContact(s, p.Disp, p.Def.BodyR + WallTouchReach, out at, out n))
                    return true;
            return false;
        }

        /// After the walls (WorldRenderer.cs): the arcs at every touch (a corner can be two).
        void DrawWallTouch(float t)
        {
            var d = game.ActiveLayout;
            if (d == null)
                return;
            if (WallTouchT >= 0f)
                t = WallTouchT;
            foreach (var p in game.Players.Values)
            {
                float l;
                if (WallTouchPreview)
                    l = 1f;
                else if (!wallTouchLvl.TryGetValue(p.Id, out l) || l <= 0.01f)
                    continue;
                if (p.Downed)
                    continue;
                float r = p.Def.BodyR;
                foreach (var s in d.Solids)
                {
                    V2 at, n;
                    if (WallContact(s, p.Disp, r + WallTouchReach + 4f, out at, out n))  // (a little further, so the fade out has somewhere to draw)
                        DrawTouchStatic(at, n, p.Disp, r, t, p.Id, l);
                }
            }
        }

        /// The point on the wall's face nearest the body, and the face's outward normal, when the body is within reach.
        static bool WallContact(Solid s, V2 c, float reach, out V2 at, out V2 n)
        {
            if (s.Round)
            {
                V2 dv = c - s.Pos;
                float dl = dv.Length;
                n = dl > 0.01f ? dv / dl : new V2(0f, -1f);
                at = s.Pos + n * s.R;
                return dl - s.R <= reach;
            }
            at = new V2(Mathf.Clamp(c.X, s.Pos.X - s.HW, s.Pos.X + s.HW), Mathf.Clamp(c.Y, s.Pos.Y - s.HH, s.Pos.Y + s.HH));
            V2 dd = c - at;
            float l = dd.Length;
            n = l > 0.01f ? dd / l : new V2(0f, -1f);
            n = Mathf.Abs(n.X) > Mathf.Abs(n.Y) ? new V2(Mathf.Sign(n.X), 0f) : new V2(0f, Mathf.Sign(n.Y));
            return l <= reach;
        }

        /// A stretch of the wall's outline lit brightest at `at`, fading out along the face over `half` px.
        void LitEdge(V2 at, V2 tan, float half, float width, Rgba col, float a)
        {
            const int steps = 14;
            for (int i = -steps; i < steps; i++)
            {
                float u0 = (float)i / steps, u1 = (float)(i + 1) / steps, um = Mathf.Abs((u0 + u1) * 0.5f);
                float f = (1f - um) * (1f - um);
                Line(at + tan * (u0 * half), at + tan * (u1 * half), width, col.WithAlpha(a * f));
            }
        }

        /// Jagged arcs crawl along the face from the touch (redrawn 20 times a second), with sparks skittering off it
        /// and a short bolt jumping from the body to the wall. `l`: how strong the touch is (fades in and out).
        void DrawTouchStatic(V2 at, V2 n, V2 body, float r, float t, int seed, float l)
        {
            V2 tan = n.Orthogonal();
            int frame = Mathf.FloorToInt(t * 20f);
            float flick = (TouchHash(frame * 7 + seed) > 0.25f ? 1f : 0.5f) * l;
            GlowDisc(at, 70f, Ink, 0.5f * flick);
            LitEdge(at, tan, 90f, 8f, Ink, 0.7f * flick);
            LitEdge(at, tan, 60f, 3f, Rgba.White, flick);
            for (int b = 0; b < 4; b++)
            {
                float sd = b % 2 == 0 ? 1f : -1f, len = (50f + 70f * TouchHash(frame * 13 + b * 5 + seed)) * (0.4f + 0.6f * l);
                V2 prev = at;
                const int segs = 8;
                for (int k = 1; k <= segs; k++)
                {
                    float u = (float)k / segs;
                    V2 q = at + tan * (sd * len * u) + n * ((TouchHash(frame * 31 + b * 17 + k * 3 + seed) - 0.5f) * 16f * (1f - u * 0.5f));
                    Line(prev, q, 7f, Ink.WithAlpha(0.35f * (1f - u * 0.6f) * l));
                    Line(prev, q, 1.8f, Rgba.White.WithAlpha((1f - u * 0.5f) * l));
                    prev = q;
                }
            }
            for (int k = 0; k < 2; k++)  // a short bolt from the body to the wall
            {
                V2 prev = body - n * (r * 0.4f), from = prev;
                for (int j = 1; j <= 3; j++)
                {
                    V2 q = V2.Lerp(from, at, j / 3f) + tan * ((TouchHash(frame * 5 + j * 9 + k * 41 + seed) - 0.5f) * 14f);
                    Line(prev, q, 1.6f, Rgba.White.WithAlpha(l));
                    prev = q;
                }
            }
            for (int s = 0; s < 8; s++)  // sparks skittering off the face
            {
                float u = (TouchHash(frame * 3 + s * 11 + seed) - 0.5f) * 160f;
                Circle(at + tan * u + n * (2f + TouchHash(frame + s * 7 + seed) * 10f), 1.8f, Rgba.White.Mix(Ink, 0.4f).WithAlpha(l), 6);
            }
        }

        static float TouchHash(int i)
        {
            uint x = (uint)i * 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return (x & 0xffffff) / 16777216f;
        }
    }
}
