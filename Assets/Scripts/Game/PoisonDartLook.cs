using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Poison Dart's look (Docs/Mockups/poison-dart.html, look A "Toxic Drip"), drawing only:
    ///   - FxKind.VenomDart (Pos = you, Pos2 = the enemy, Radius = its id): a green puff as you throw, a steel needle with green
    ///     fletching racing in on a glowing green trail, and a splash of droplets and a ring where it lands. The dart then
    ///     stays stuck in that enemy while it's poisoned;
    ///   - a poisoned enemy (Enemy.Venomed): a toxic glow under it, the body turned sickly green, teardrops forming on its
    ///     underside and falling, a lime rim, bubbles rising off it and popping, a green health bar, and a drop with a dart
    ///     through it in the status row;
    ///   - FxKind.VenomSpread (Pos = the corpse, Pos2 = an enemy it passed to, Radius = the glob's number): the first one
    ///     also draws the death: a lime flash, a toxic burst flinging droplets, a ring, and a bubbling puddle left on the
    ///     floor for a couple of seconds. Each one lobs a wobbling glob onto its victim, trailing drops, that splats there.
    /// With Visual effects off: a plain green line for the dart and the spread, and the tint and the icon.
    public partial class ArenaApp
    {
        static readonly Rgba VenomGreen = new Rgba(0.56f, 0.94f, 0.35f), VenomLime = new Rgba(0.82f, 1f, 0.59f),
            VenomGlow = new Rgba(0.45f, 1f, 0.3f), VenomDeep = new Rgba(0.16f, 0.47f, 0.16f), VenomDark = new Rgba(0.07f, 0.2f, 0.07f),
            VenomBody = new Rgba(0.42f, 0.92f, 0.28f), VenomBar = new Rgba(0.56f, 0.94f, 0.35f),
            DartSteel = new Rgba(0.84f, 0.88f, 0.9f), DartFletch = new Rgba(0.37f, 0.75f, 0.23f);

        readonly HashSet<int> dartStuck = new HashSet<int>();  // enemies a dart hit (not ones the poison spread to)

        static float VenomHash(float s) => Hash01(s);  // (SoldierLooks.cs)

        /// A teardrop, point up.
        void Teardrop(V2 c, float r, Rgba col)
        {
            Circle(c, r, col, 10);
            SetColor(col);
            Quad(c + new V2(-r * 0.93f, -r * 0.37f), c + new V2(0f, -r * 2.2f), c + new V2(0f, -r * 2.2f), c + new V2(r * 0.93f, -r * 0.37f));
        }

        /// A needle pointing along dir with its tip at tip: a steel shaft, a green-wet tip and green fletching.
        void DrawNeedle(V2 tip, V2 dir, float len, float a)
        {
            V2 n = dir.Orthogonal(), tail = tip - dir * len;
            Line(tail, tip - dir * 2f, 3.6f, new Rgba(0f, 0f, 0f, 0.35f * a));  // a dark edge so it reads on anything
            Line(tail, tip - dir * 2f, 2.2f, DartSteel.WithAlpha(a));
            Line(tail + n * 0.5f, tip - dir * 3f + n * 0.5f, 0.8f, Rgba.White.WithAlpha(0.7f * a));  // the glint
            SetColor(VenomGreen.WithAlpha(a));
            Quad(tip + dir * 3.5f, tip - dir * 2.5f + n * 2.4f, tip - dir * 2.5f - n * 2.4f, tip + dir * 3.5f);
            SetColor(DartFletch.WithAlpha(a));
            Quad(tail + dir * 1f, tail - dir * 5f + n * 4.5f, tail - dir * 2f, tail - dir * 5f - n * 4.5f);
            SetColor(VenomLime.WithAlpha(0.8f * a));
            Quad(tail + dir * 1f, tail - dir * 3.5f + n * 2.6f, tail - dir * 1.5f, tail + dir * 1f);
        }

        /// Droplets thrown out from c, falling as they go (k: 0 to 1 over their flight).
        void FlingDrops(V2 c, int count, float seed, float speed, float k, float size, Rgba col, float spread = MathX.Tau, float toward = 0f)
        {
            if (k >= 1f)
                return;
            for (int i = 0; i < count; i++)
            {
                float an = toward + (spread >= MathX.Tau ? i / (float)count * MathX.Tau + VenomHash(seed + i) * 0.6f : (VenomHash(seed + i) - 0.5f) * spread);
                float sp = speed * (0.55f + 0.45f * VenomHash(seed + i * 3.1f));
                V2 at = c + V2.FromAngle(an) * (sp * EaseOut3(k)) + new V2(0f, 60f * k * k);
                Circle(at, size * (1f - 0.6f * k) * (0.7f + 0.5f * VenomHash(seed + i * 7.7f)), col.WithAlpha(1f - k), 8);
            }
        }

        /// FxKind.VenomDart.
        void DrawVenomDartFx(Effect fx)
        {
            V2 d = fx.Pos2 - fx.Pos;
            float len = d.Length;
            if (len < 1f)
                return;
            V2 dir = d / len;
            float fly = Mathf.Clamp01(fx.T / Game.VenomDartFly);
            V2 head = fx.Pos + d * fly;
            if (!fxOn)
            {
                if (fly < 1f)
                    Line(head - dir * 14f, head, 2f, VenomGreen);
                return;
            }
            if (fx.T < 0.18f)  // the throw: a small green puff off your hand
            {
                float k = fx.T / 0.18f;
                V2 hand = fx.Pos + dir * 16f;
                GlowDisc(hand, 8f + 10f * k, VenomGlow, 0.5f * (1f - k));
                FlingDrops(hand, 4, fx.Radius + 1f, 22f, k, 1.8f, VenomLime, 1.6f, dir.Angle);
            }
            if (fly < 1f)
            {
                float tail = Mathf.Min(90f, len * fly);
                const int Segs = 8;
                for (int i = 0; i < Segs; i++)  // a glowing trail, fading out behind the needle
                {
                    float a = (float)i / Segs, b = (float)(i + 1) / Segs;
                    V2 p0 = head - dir * tail * (1f - a), p1 = head - dir * tail * (1f - b);
                    Line(p0, p1, 7f * b, VenomGlow.WithAlpha(0.18f * b));
                    Line(p0, p1, 2.6f * b + 0.6f, VenomGreen.WithAlpha(0.8f * b));
                }
                GlowDisc(head, 9f, VenomGlow, 0.35f);
                DrawNeedle(head, dir, 14f, 1f);
                return;
            }
            float t = fx.T - Game.VenomDartFly, kk = Mathf.Clamp01(t / 0.45f);
            if (kk >= 1f)
                return;  // (the effect lives on as long as the venom, for the stuck dart: PruneDarts)
            if (t < 0.08f)
                Circle(fx.Pos2, 10f * (1f - t / 0.08f) + 4f, VenomLime.WithAlpha(0.9f * (1f - t / 0.08f)), 14);  // the sting
            GlowDisc(fx.Pos2, 18f + 16f * EaseOut3(kk), VenomGlow, 0.55f * (1f - kk));
            Arc(fx.Pos2, 12f + 22f * EaseOut3(kk), 2.5f * (1f - kk) + 0.5f, VenomLime.WithAlpha(1f - kk), 28);
            FlingDrops(fx.Pos2, 7, fx.Radius * 1.37f, 38f, kk, 2.6f, VenomGreen, 2.4f, dir.Angle);  // splashed on, the way it came
        }

        /// FxKind.VenomSpread.
        void DrawVenomSpreadFx(Effect fx)
        {
            V2 from = fx.Pos, to = fx.Pos2;
            V2 d = to - from;
            float len = d.Length;
            bool glob = len > 2f, first = fx.Radius < 0.5f;
            float fly = Mathf.Clamp01(fx.T / Game.VenomGlobFly);
            if (!fxOn)
            {
                if (glob && fly < 1f)
                    Line(V2.Lerp(from, to, Mathf.Max(0f, fly - 0.2f)), V2.Lerp(from, to, fly), 2f, VenomGreen);
                return;
            }
            if (first)
                DrawVenomBurst(fx, from);
            if (!glob)
                return;
            float h = 26f + len * 0.2f;  // lobbed: up and over
            if (fly < 1f)
            {
                for (int g = 6; g >= 1; g--)  // drops trailing it
                {
                    float v = Mathf.Max(0f, fly - g * 0.05f);
                    V2 at = V2.Lerp(from, to, v) + new V2(0f, -h * Mathf.Sin(Mathf.PI * v) + g * g * 0.35f);
                    Circle(at, 3.6f - g * 0.45f, VenomGreen.WithAlpha(0.6f * (1f - g / 7f)), 8);
                }
                V2 p = V2.Lerp(from, to, fly) + new V2(0f, -h * Mathf.Sin(Mathf.PI * fly));
                V2 ahead = V2.Lerp(from, to, Mathf.Min(1f, fly + 0.04f)) + new V2(0f, -h * Mathf.Sin(Mathf.PI * Mathf.Min(1f, fly + 0.04f)));
                V2 vdir = (ahead - p).Normalized();
                float wob = 1f + 0.15f * Mathf.Sin(fx.T * 40f + fx.Radius);
                GlowDisc(p, 14f, VenomGlow, 0.35f);
                Circle(p - vdir * 2.5f, 5.4f / wob, VenomDark.WithAlpha(0.8f), 12);  // a dark edge, stretched back along its path
                Circle(p, 5.2f * wob * 0.95f, VenomGreen, 12);
                Circle(p + vdir * 1.5f, 3.6f, VenomLime, 10);
                Circle(p + new V2(-1.6f, -1.8f), 1.4f, Rgba.White.WithAlpha(0.85f), 6);  // the wet highlight
            }
            else
            {
                float t = fx.T - Game.VenomGlobFly, k = Mathf.Clamp01(t / 0.45f);  // it splats on the victim
                if (k < 1f)
                {
                    GlowDisc(to, 12f + 16f * EaseOut3(k), VenomGlow, 0.6f * (1f - k));
                    Arc(to, 8f + 20f * EaseOut3(k), 2f * (1f - k) + 0.5f, VenomLime.WithAlpha(0.9f * (1f - k)), 24);
                    FlingDrops(to, 6, fx.Radius * 2.9f + from.X, 30f, k, 2.2f, VenomGreen);
                }
            }
        }

        /// The poisoned death (drawn by its first glob's effect): a lime flash, the toxic burst with droplets flung out,
        /// a ring, and a bubbling puddle that fades out over the effect's life.
        void DrawVenomBurst(Effect fx, V2 c)
        {
            float t = fx.T, seed = c.X * 0.13f + c.Y * 0.07f;
            if (t < 0.12f)
                Circle(c, 18f + 22f * (t / 0.12f), VenomLime.Mix(Rgba.White, 0.4f).WithAlpha(0.85f * (1f - t / 0.12f)), 24);
            float b = Mathf.Clamp01(t / 0.55f);
            if (b < 1f)
            {
                GlowDisc(c, 30f + 40f * EaseOut3(b), VenomGlow, 0.7f * (1f - b));
                Arc(c, 14f + 56f * EaseOut3(b), 4f * (1f - b) + 0.6f, VenomGreen.WithAlpha(0.95f * (1f - b)), 36);
                FlingDrops(c, 14, seed, 70f, Mathf.Clamp01(t / 0.7f), 3.4f, VenomLime);
                FlingDrops(c, 8, seed + 50f, 44f, Mathf.Clamp01(t / 0.6f), 2.4f, VenomGreen);
            }
            // The puddle: spreads out fast, sits there bubbling, then dries away.
            float grow = EaseOut3(Mathf.Clamp01((t - 0.05f) / 0.35f));
            float fade = Mathf.Clamp01((fx.Dur - t) / 1.2f);
            if (grow <= 0f || fade <= 0f)
                return;
            for (int i = 0; i < 6; i++)
            {
                V2 at = c + PuddleSpot(seed, i, grow);
                float rr = (7f + 6f * VenomHash(seed + i * 5.1f)) * grow;
                Circle(at, rr + 1.5f, VenomDark.WithAlpha(0.3f * fade), 14);
            }
            for (int i = 0; i < 6; i++)
            {
                V2 at = c + PuddleSpot(seed, i, grow);
                float rr = (7f + 6f * VenomHash(seed + i * 5.1f)) * grow;
                Circle(at, rr, VenomDeep.WithAlpha(0.26f * fade), 14);
                Arc(at, rr, 1f, VenomGreen.WithAlpha(0.12f * fade), 14);
            }
            for (int i = 0; i < 3; i++)  // bubbles welling up in it and popping
            {
                float cyc = MathX.Fmod(t * 1.4f + i * 0.37f + VenomHash(seed + i * 9f), 1f);
                V2 at = c + new V2((VenomHash(seed + i * 4.4f) - 0.5f) * 26f * grow, 5f + (VenomHash(seed + i * 6.6f) - 0.5f) * 14f * grow);
                if (cyc < 0.8f)
                    Arc(at, 1f + 3f * (cyc / 0.8f), 1.2f, VenomLime.WithAlpha(0.8f * fade), 10);
                else
                    Arc(at, 4f + 4f * ((cyc - 0.8f) / 0.2f), 1f, VenomLime.WithAlpha(0.6f * fade * (1f - (cyc - 0.8f) / 0.2f)), 10);
            }
        }

        /// A blob of the puddle: spread wide and flat, the way a splash lands on a floor seen from above at a slant.
        static V2 PuddleSpot(float seed, int i, float grow)
        {
            V2 d = V2.FromAngle(VenomHash(seed + i) * MathX.Tau) * (8f + 14f * VenomHash(seed + i * 2.3f)) * grow;
            return new V2(d.X * 1.3f, d.Y * 0.5f + 5f);
        }

        /// A poisoned enemy, under its body (from DrawEnemyStatus): the glow and the falling drips. Returns the tinted colour.
        Rgba DrawVenomed(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4.5f + e.Id);
            col = col.Mix(VenomBody, 0.7f + 0.1f * pulse);
            if (!fxOn)
                return col;
            GlowDisc(pos, r * 1.9f + 6f * pulse, VenomGlow, 0.3f + 0.12f * pulse);
            for (int k = 0; k < 3; k++)  // teardrops forming on its underside, then falling and splashing
            {
                float cyc = MathX.Fmod(t * 0.9f + k * 0.33f + e.Id * 0.37f, 1f);
                V2 at = pos + new V2((k - 1f) * r * 0.5f, r * 0.55f);
                float fall = r * 1.5f + 6f;
                if (cyc < 0.35f)  // swelling
                    Teardrop(at + new V2(0f, 2f * cyc), 1f + 2.4f * (cyc / 0.35f), VenomGreen.WithAlpha(0.95f));
                else if (cyc < 0.8f)  // falling
                {
                    float u = (cyc - 0.35f) / 0.45f;
                    V2 p = at + new V2(0f, 1f + fall * u * u);
                    Line(p + new V2(0f, -3f - 6f * u), p, 1.2f, VenomGreen.WithAlpha(0.35f));
                    Teardrop(p, 3.2f, VenomGreen.WithAlpha(0.95f - 0.25f * u));
                    Circle(p + new V2(-0.9f, 0.2f), 0.9f, Rgba.White.WithAlpha(0.6f), 5);
                }
                else  // the splash
                {
                    float u = (cyc - 0.8f) / 0.2f;
                    V2 p = at + new V2(0f, 1f + fall);
                    Arc(p, 2f + 5f * u, 1.2f, VenomGreen.WithAlpha(0.8f * (1f - u)), 10);
                    Circle(p + new V2(-3f - 3f * u, -3f * u + 4f * u * u), 1f, VenomLime.WithAlpha(1f - u), 5);
                    Circle(p + new V2(3f + 3f * u, -3f * u + 4f * u * u), 1f, VenomLime.WithAlpha(1f - u), 5);
                }
            }
            return col;
        }

        /// A poisoned enemy, over its body (DrawEnemy): the lime rim, bubbles rising off it and the stuck dart.
        void DrawVenomOver(Enemy e, V2 pos, float r, float t)
        {
            if (!fxOn)
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4.5f + e.Id);
            Arc(pos, r + 3f, 1.6f, VenomLime.WithAlpha(0.35f + 0.35f * pulse), 32);
            for (int k = 0; k < 4; k++)  // bubbles welling off the top, swaying, popping
            {
                float cyc = MathX.Fmod(t * 0.75f + k * 0.25f + e.Id * 0.21f, 1f);
                float sway = Mathf.Sin(t * 5f + k * 1.7f + e.Id) * 3f;
                V2 at = pos + new V2((VenomHash(e.Id + k * 3.3f) - 0.5f) * r * 1.2f + sway, -r * 0.3f - cyc * (r + 16f));
                if (cyc < 0.85f)
                {
                    float br = 1.2f + 2.6f * cyc;
                    Circle(at, br, VenomGreen.WithAlpha(0.22f), 10);
                    Arc(at, br, 1f, VenomLime.WithAlpha(0.85f), 10);
                    Circle(at + new V2(-br * 0.35f, -br * 0.35f), br * 0.28f, Rgba.White.WithAlpha(0.7f), 5);
                }
                else
                {
                    float u = (cyc - 0.85f) / 0.15f;
                    for (int s = 0; s < 4; s++)
                    {
                        V2 dd = V2.FromAngle(s * MathX.Pi / 2f + 0.78f);
                        Line(at + dd * (3f + 2f * u), at + dd * (5f + 3f * u), 1f, VenomLime.WithAlpha(1f - u));
                    }
                }
            }
            if (dartStuck.Contains(e.Id))  // the dart that started it, still in it, quivering for a moment
            {
                float an = -2.4f + VenomHash(e.Id + 4f) * 0.9f;  // sticking out of its upper left
                V2 dir = V2.FromAngle(an + MathX.Pi + Mathf.Sin(t * 30f) * 0.03f);
                DrawNeedle(pos - dir * (r * 0.45f), dir, 13f, 1f);
            }
        }

        /// The status-row icon: a green drop with a dart through it.
        void DrawVenomIcon(V2 c, float size)
        {
            Teardrop(c + new V2(0f, 2.5f), size * 0.55f, VenomGreen);
            Circle(c + new V2(-1.5f, 2.5f), size * 0.16f, Rgba.White.WithAlpha(0.7f), 5);
            Line(c + new V2(-size * 0.75f, size * 0.7f), c + new V2(size * 0.75f, -size * 0.7f), 1.5f, DartSteel);
        }

        /// Which enemies have a dart in them: a landed FxKind.VenomDart (it lives as long as the venom) on an enemy that is
        /// still poisoned (once a frame, before the enemies draw).
        void PruneDarts()
        {
            dartStuck.Clear();
            foreach (var fx in game.Effects)
            {
                Enemy e;
                if (fx.Kind == FxKind.VenomDart && fx.T >= Game.VenomDartFly && game.Enemies.TryGetValue((int)fx.Radius, out e) && e.Venomed)
                    dartStuck.Add(e.Id);
            }
        }
    }
}
