using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Hemomancer tank's Blood Shell in look B, "Heartbeat" (Docs/Mockups/blood-shell.html): a big bubble of blood with a
    /// cell wall and veins that beat faster as your health runs down to the third where it bursts. A ring round it shows the
    /// health you can still spend. A stopped bullet dents the skin and sends a bead of blood from you to the hit and to each
    /// ally inside; the burst flings drops and a shockwave. With Visual effects off it is a plain red ring at the real size.
    /// Drawing only: it reads the replicated ShellOn, health and the effects this screen has.
    public partial class ArenaApp
    {
        /// How much health the shell has left to spend before it bursts, 0..1.
        static float ShellReserve(Player p) => p.MaxHp > 0f ? Mathf.Clamp01((p.Hp - p.MaxHp * Game.ShellPopFrac) / (p.MaxHp * (1f - Game.ShellPopFrac))) : 0f;

        void DrawBloodShell(Player p, float t)
        {
            float R = Game.ShellRadius;
            V2 c = p.Disp;
            float reserve = ShellReserve(p);
            if (!fxOn)
            {
                Arc(c, R, 3f, new Rgba(0.95f, 0.2f, 0.3f, 0.5f + 0.4f * reserve), 48);
                return;
            }
            float age = 1f;  // it swells over the forming effect (ShellPop mode 2), so a screen that comes in late sees it whole
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.ShellPop && fx.Pos2.Y > 1.5f && fx.Pos.DistanceTo(p.Pos) < 40f)
                    age = fx.T;
            float grow = EaseOut3(Mathf.Clamp01(age / 0.5f));
            float r0 = Mathf.Max(14f, R * grow), al = 0.45f + 0.55f * reserve;
            float bpm = Mathf.Lerp(150f, 60f, reserve);
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * bpm / 60f * MathX.Pi)), 6f);

            // dents where bullets have just hit
            var dents = new List<Vector2>();  // x = angle, y = strength
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.ShellHit && fx.Pos2.DistanceTo(p.Pos) < 40f)
                {
                    V2 off = fx.Pos - c;
                    dents.Add(new Vector2(Mathf.Atan2(off.Y, off.X), 1f - fx.T / fx.Dur));
                }

            // the skin: filled in layers, dark at the heart and bright at the wall
            for (int k = 0; k < 6; k++)
                Circle(c, r0 * (1f - k * 0.06f), BloodRed.WithAlpha((0.05f + 0.05f * k) * al), 48);
            Circle(c, r0 * 0.7f, BloodDark.WithAlpha(0.18f * al), 40);

            // the cell wall
            for (int gy = -3; gy <= 3; gy++)
                for (int gx = -3; gx <= 3; gx++)
                {
                    V2 at = c + new V2(gx * 34f + ((gy & 1) != 0 ? 17f : 0f), gy * 29f) * grow;
                    if (at.DistanceTo(c) > r0 - 14f)
                        continue;
                    Arc(at, 13f + beat * 2f, 1.2f, new Rgba(1f, 0.35f, 0.43f, 0.14f * al), 10);
                }

            // veins, with a pulse running down each
            for (int v = 0; v < 7; v++)
            {
                float a = v / 7f * MathX.Tau + 0.3f;
                V2 prev = c + V2.FromAngle(a) * r0;
                for (int s = 1; s <= 6; s++)
                {
                    float u = s / 6f;
                    V2 pt = c + V2.FromAngle(a + 0.35f * Mathf.Sin(v * 2f + s * 1.3f)) * (r0 * (1f - u * 0.7f));
                    Line(prev, pt, 2f, new Rgba(1f, 0.27f, 0.37f, 0.3f * al));
                    prev = pt;
                }
                float ph = (t * 0.9f + v * 0.13f) % 1f;
                float pr = r0 * (1f - ph * 0.7f);
                GlowDisc(c + V2.FromAngle(a + 0.35f * Mathf.Sin(v * 2f + ph * 6f * 1.3f)) * pr, 3f + beat * 2f, new Rgba(1f, 0.75f, 0.8f), 0.6f * al);
            }

            // the rim: dented where bullets hit, thumping with the heartbeat, flickering when thin
            float flick = reserve < 0.35f ? 0.65f + 0.35f * Mathf.Sin(t * 38f) : 1f;
            V2 last = V2.Zero;
            int n = 72;
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * MathX.Tau;
                float rr = r0 + beat * 2.4f + 2f * Mathf.Sin(3f * a + t * 2f);
                foreach (var d in dents)
                {
                    float da = Mathf.Abs(Mathf.Repeat(a - d.x + MathX.Pi, MathX.Tau) - MathX.Pi);
                    rr -= 7f * d.y * Mathf.Exp(-da * da / 0.12f) * Mathf.Cos((1f - d.y) * 18f);
                }
                V2 pt = c + V2.FromAngle(a) * rr;
                if (i > 0)
                {
                    Line(last, pt, 3f + beat * 3f, new Rgba(1f, 0.36f, 0.44f, 0.95f * al * flick));
                    Line(last, pt, 1f, new Rgba(1f, 0.85f, 0.87f, 0.4f * al));
                }
                last = pt;
            }
            Arc(c, r0 - 12f, 4f, Rgba.White.WithAlpha(0.8f * al), 8, MathX.Pi * 1.1f, MathX.Pi * 0.28f);  // a glint
            Arc(c, r0 - 12f, 2.5f, Rgba.White.WithAlpha(0.8f * al), 4, MathX.Pi * 1.45f, MathX.Pi * 0.05f);

            // the health you can still spend before it bursts: a ring from the top, with the wall's sigil ticks inside it
            for (int i = 0; i < 36; i++)
            {
                float a = i / 36f * MathX.Tau + t * 0.25f;
                Line(c + V2.FromAngle(a) * (R + 8f), c + V2.FromAngle(a) * (R + (i % 3 != 0 ? 12f : 17f)), 1.5f, new Rgba(1f, 0.43f, 0.49f, 0.35f * grow));
            }
            Arc(c, R + 24f, 4f, new Rgba(1f, 1f, 1f, 0.08f * grow), 64);
            Arc(c, R + 24f, 4f, new Rgba(1f, 0.29f, 0.39f, 0.95f * grow), 64, -MathX.Pi / 2f, MathX.Tau * reserve);

            // blood running to each ally inside
            foreach (var q in game.Players.Values)
                if (q != p && !q.Downed && q.Disp.DistanceTo(c) < R)
                    for (int k = 0; k < 4; k++)
                    {
                        float u = (t * 0.8f + k * 0.25f) % 1f;
                        GlowDisc(V2.Lerp(c, q.Disp, u), 2.4f, new Rgba(1f, 0.45f, 0.55f), 0.45f);
                    }
        }

        /// A bullet stopped by the shell (FxKind.ShellHit; Pos = where, Pos2 = the owner): a burst at the skin, a bead of blood
        /// from the owner out to it, and a bead from the owner to each ally inside.
        void DrawShellHitFx(Effect fx, float f)
        {
            V2 at = fx.Pos, from = fx.Pos2;
            Player owner = null;
            float bestD = 60f;
            foreach (var q in game.Players.Values)
            {
                float d = q.Pos.DistanceTo(fx.Pos2);
                if (d < bestD) { bestD = d; owner = q; }
            }
            if (owner != null)
                from = owner.Disp;
            V2 out0 = at - from, dir = out0.IsZero ? new V2(1f, 0f) : out0.Normalized();
            Arc(at, 4f + 20f * f, 2.5f * (1f - f) + 0.5f, new Rgba(1f, 0.5f, 0.58f, 1f - f), 16);
            if (f < 0.4f)
                GlowDisc(at, 16f * (1f - f / 0.4f), new Rgba(1f, 0.9f, 0.9f), 0.9f);
            for (int k = 0; k < 7; k++)  // blood sprayed off the skin
            {
                float ang = Mathf.Atan2(dir.Y, dir.X) + (k - 3f) * 0.22f, sp = 40f + 22f * (k % 3);
                V2 d = V2.FromAngle(ang);
                DrawDrop(at + d * sp * f, 2.6f * (1f - 0.5f * f), d, BloodRed.WithAlpha(1f - f));
            }
            if (f < 0.35f)  // what it cost you
                BloodBead(from, at, f / 0.35f, new Rgba(0.55f, 0.05f, 0.14f), 3f);
            if (f > 0.15f && f < 0.7f)  // what the allies inside get back
                foreach (var q in game.Players.Values)
                    if (q != owner && !q.Downed && q.Disp.DistanceTo(from) < Game.ShellRadius)
                        BloodBead(from, q.Disp, (f - 0.15f) / 0.55f, new Rgba(1f, 0.56f, 0.63f), 3.6f);
        }

        void BloodBead(V2 a, V2 b, float u, Rgba col, float r)
        {
            u = u * u * (3f - 2f * u);
            V2 mid = (a + b) * 0.5f + (b - a).Orthogonal() * 0.2f;
            V2 p = a * ((1f - u) * (1f - u)) + mid * (2f * (1f - u) * u) + b * (u * u);
            GlowDisc(p, r * 2.2f, col, 0.45f);
            Circle(p, r, col, 8);
        }

        /// The shell forming, letting go, or bursting (FxKind.ShellPop; Pos2.X = the owner, Pos2.Y = 2 forming, 0 let go, 1 burst).
        void DrawShellPopFx(Effect fx, float f)
        {
            V2 c = fx.Pos;
            float R = fx.Radius;
            int mode = Mathf.RoundToInt(fx.Pos2.Y);
            if (mode == 2)  // drops rush in and set into the skin
            {
                float g = 1f - (1f - f) * (1f - f);
                for (int k = 0; k < 14; k++)
                {
                    V2 d = V2.FromAngle(k * MathX.Tau / 14f + 0.6f * g);
                    DrawDrop(c + d * (R + 60f * (1f - g)), 3.2f, -d, BloodRed.WithAlpha(0.95f * (1f - f * f)));
                }
                return;
            }
            if (mode == 0)  // let go: the skin sinks back to the ground
            {
                Arc(c, R * (1f - f), 4f * (1f - f) + 1f, new Rgba(1f, 0.36f, 0.44f, 0.9f * (1f - f)), 48);
                return;
            }
            Arc(c, R * (0.9f + 1.4f * f), 7f * (1f - f) + 1f, new Rgba(1f, 0.5f, 0.57f, 0.9f * (1f - f)), 64);  // the shockwave
            Circle(c, R * (0.9f + 0.5f * f), BloodRed.WithAlpha(0.3f * (1f - f)), 48);
            for (int k = 0; k < 36; k++)  // the skin, flung out as drops
            {
                float ang = k * 2.39996f;
                V2 d = V2.FromAngle(ang);
                float dist = R * 0.9f + (60f + 90f * (k % 4)) * (1f - (1f - f) * (1f - f));
                DrawDrop(c + d * dist, 3.4f * (1f - 0.4f * f), d, BloodRed.WithAlpha(1f - f));
            }
        }
    }
}
