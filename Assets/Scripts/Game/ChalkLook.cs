using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Spirit Chalk's ink (Docs/Mockups/totem-staff-rework.html, concept 5): a glowing line of pale ink with a bright head where
    /// it is being drawn; a closed loop turns gold and fills faintly; enemies held in by one wear a ring. Drawing only; what the
    /// ink does is in Chalk.cs (the host sends the dots in every snapshot).
    public partial class ArenaApp
    {
        static readonly Rgba InkCore = new Rgba(0.82f, 0.93f, 1f), InkGlow = new Rgba(0.45f, 0.7f, 1f), InkSealed = new Rgba(1f, 0.9f, 0.6f);
        readonly Dictionary<int, List<V2>> inkRings = new Dictionary<int, List<V2>>();

        void DrawInk(float t)
        {
            var ink = game.Ink;
            if (ink.Count == 0)
                return;
            inkRings.Clear();
            foreach (var d in ink)
                if (d.Pen != 0)
                {
                    List<V2> ring;
                    if (!inkRings.TryGetValue(d.Pen, out ring))
                        inkRings[d.Pen] = ring = new List<V2>();
                    ring.Add(d.Pos);
                }
            // A loop's inside, faintly, so you can see what it holds.
            foreach (var ring in inkRings.Values)
            {
                if (ring.Count < 3)
                    continue;
                V2 c = V2.Zero;
                foreach (var q in ring)
                    c += q;
                c = c / ring.Count;
                SetColor(InkSealed.WithAlpha(0.09f + 0.03f * Mathf.Sin(t * 4f)));
                for (int i = 0; i < ring.Count; i++)
                    Tri(c, ring[i], ring[(i + 1) % ring.Count]);
            }
            for (int i = 0; i < ink.Count; i++)
            {
                var d = ink[i];
                Player owner;
                game.Players.TryGetValue(d.Owner, out owner);
                float width = Game.ChalkDotR * 2f * (owner != null ? Game.AreaMult(owner) : 1f);
                float fade = Mathf.Clamp01(d.T / 0.8f) * (0.92f + 0.08f * Mathf.Sin(t * 7f + i * 0.5f));
                Rgba core = d.Pen != 0 ? InkSealed : InkCore, glow = d.Pen != 0 ? new Rgba(1f, 0.78f, 0.35f) : InkGlow;
                if (i > 0 && ink[i - 1].Owner == d.Owner && ink[i - 1].Stroke == d.Stroke && ink[i - 1].Pos.DistanceTo(d.Pos) < 40f)
                {
                    V2 a = ink[i - 1].Pos, b = d.Pos;
                    Circle(b, width * 0.5f + 5f, glow.WithAlpha(0.07f * fade), 12);  // (a round glow: butt-ended line pieces left notches at the bends)
                    Line(a, b, width * 0.7f, core.WithAlpha(0.55f * fade));
                    Line(a, b, 2.2f, Rgba.White.WithAlpha(0.9f * fade));
                }
                else
                {
                    Circle(d.Pos, width * 0.5f + 4f, glow.WithAlpha(0.12f * fade), 12);
                    Circle(d.Pos, width * 0.35f, core.WithAlpha(0.55f * fade), 10);
                }
                bool head = d.T > Game.ChalkLife - 0.22f;  // the newest dab: where the chalk is
                if (head)
                {
                    Circle(d.Pos, 8f, new Rgba(0.85f, 0.95f, 1f, 0.35f), 12);
                    Circle(d.Pos, 3.5f, Rgba.White, 8);
                }
            }
        }

        /// A ring and four turning ticks round each enemy a loop is holding.
        void DrawPennedMarks(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (!e.Penned || e.Hp <= 0f)
                    continue;
                float r = e.Radius + 8f;
                Arc(e.Disp, r, 2f, InkSealed.WithAlpha(0.8f), 24);
                for (int k = 0; k < 4; k++)
                {
                    float a = t * 1.5f + k * MathX.Pi / 2f;
                    V2 u = V2.FromAngle(a);
                    Line(e.Disp + u * (r - 3f), e.Disp + u * (r + 4f), 2f, InkSealed.WithAlpha(0.9f));
                }
            }
        }
    }
}
