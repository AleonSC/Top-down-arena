using UnityEngine;

namespace Arena
{
    /// The Eclipse's faction, the Penumbral Court (Core/Penumbral.cs), drawn as in Docs/Mockups/eclipse-faction.html with the
    /// boss's colours (EclipseLook.cs: sun gold, flare orange, sun white).
    ///   Ember: a flickering teardrop of flame pointing where it goes; its sparks (DrawEmberSpark) glow and cool as they hang.
    ///   Corona Priest: a robed cultist inside a turning halo of spikes that flares and lengthens while it winds up.
    ///   Heliograph: a robed figure holding a sun mirror out front; its line (DrawPenumbralFloor) flickers as a dashed red line
    ///   while it aims, then burns as a 40 px lane of fire that fades out.
    ///   Moonward: a pale knight with a cratered moon circling it (the moon's angle is its Facing).
    ///   Gnomon: a stone slab with a sun lantern on a pole at its back. While it plants, its shadow (a dark wedge in
    ///   front of the slab) shows on the floor and the lantern swells; the flash whites out everything but the wedge.
    ///   Dusk Lobber: a violet-robed cultist with a ball of night; its pool is a violet rim on the floor, and over
    ///   everything a soft grid of dark, cut away DuskSight round each player (DrawDuskPools).
    ///   Sickle: a grey-robed cultist holding a pale crescent blade at its side, glowing as it winds up.
    ///   Nova: a small white-hot star with spikes; lit, it swells and reddens, a fuse ring runs down round it and a
    ///   faint ring shows how far the nova will reach.
    public partial class ArenaApp
    {
        static readonly Rgba PenGold = new Rgba(1f, 0.82f, 0.4f), PenOrange = new Rgba(1f, 0.54f, 0.24f), PenSun = new Rgba(1f, 0.95f, 0.8f),
            PenRed = new Rgba(1f, 0.31f, 0.23f), PenCloth = new Rgba(0.15f, 0.11f, 0.07f), PenTrim = new Rgba(0.55f, 0.42f, 0.24f);

        /// On the floor, under the enemies: a Heliograph's line.
        void DrawPenumbralFloor(Enemy e, V2 pos, float t)
        {
            if (e.Type == EnemyType.Gnomon && (e.Phase == 1 || e.Phase == 2))
            {
                DrawGnomonShadow(e, pos, t);
                return;
            }
            if (e.Type != EnemyType.Heliograph || e.Phase != 1 && e.Phase != 2)
                return;
            V2 u = V2.FromAngle(e.Facing);
            V2 from = pos + u * e.Radius, tip = pos + u * Game.HelioLen;
            if (e.Phase == 1)
            {
                // The warning: a thin dashed line that flickers faster as it runs out, the lane's width faintly under it.
                float k = Mathf.Clamp01(e.SeenT / Game.HelioWarn);
                Line(from, tip, Game.HelioWidth, PenRed.WithAlpha(0.04f + 0.08f * k));
                if (Mathf.Sin(t * (22f + 34f * k)) > -0.2f)
                    DashedLine(from, tip, 2f, PenRed.WithAlpha(0.5f + 0.35f * k));
            }
            else
            {
                float k = Mathf.Clamp01(e.SeenT / Game.HelioBurn), a = 1f - k * k;
                float hum = 0.9f + 0.1f * Mathf.Sin(t * 40f + e.Id);
                Line(from, tip, Game.HelioWidth * 1.5f, PenOrange.WithAlpha(0.18f * a * hum));
                Line(from, tip, Game.HelioWidth, PenOrange.WithAlpha(0.55f * a));
                Line(from, tip, Game.HelioWidth * 0.4f, PenGold.WithAlpha(0.9f * a));
                Line(from, tip, 3f, PenSun.WithAlpha(a));
            }
        }

        /// An Ember's spark: a hot dot with a glow, cooling to red as it runs out.
        void DrawEmberSpark(Bullet b, float t)
        {
            float life = Mathf.Clamp01(b.Life / Mathf.Max(0.01f, b.Life0));
            var col = PenRed.Mix(PenOrange, life);
            float flick = 0.85f + 0.15f * Mathf.Sin(t * 30f + b.Id * 1.7f);
            Circle(b.Pos, b.Radius * 2.4f, col.WithAlpha(0.22f * flick), 10);
            Circle(b.Pos, b.Radius, col.WithAlpha(0.6f + 0.4f * life), 8);
            Circle(b.Pos, b.Radius * 0.45f, PenSun.WithAlpha(0.4f + 0.6f * life), 6);
        }

        void DrawPenumbralBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            switch (e.Type)
            {
                case EnemyType.Ember:
                {
                    float fl = 1f + 0.15f * Mathf.Sin(t * 20f + e.Id * 3f);
                    Circle(pos, r * 2f, PenOrange.WithAlpha(0.12f), 14);
                    PenFlame(pos, f, s, r * 1.25f * fl, r * 1.05f, r * 0.85f, PenOrange);
                    PenFlame(pos + f * (r * 0.15f), f, s, r * 0.75f * fl, r * 0.5f, r * 0.45f, PenSun);
                    break;
                }
                case EnemyType.CoronaPriest:
                {
                    float wind = e.Winding ? 0.5f + 0.5f * Mathf.Sin(t * 16f) : 0f;
                    Circle(pos, r * 2.4f, PenGold.WithAlpha(0.06f + 0.16f * wind), 18);
                    float spin = t * 0.8f + e.Id;
                    for (int k = 0; k < 12; k++)
                    {
                        V2 d = V2.FromAngle(spin + k * MathX.Tau / 12f);
                        float len = r * (0.3f + (k % 2) * 0.2f + 0.45f * wind);
                        Line(pos + d * (r * 1.2f), pos + d * (r * 1.2f + len), 2f, PenGold.WithAlpha(0.6f + 0.4f * wind));
                    }
                    Arc(pos, r * 1.2f, 1.4f, PenGold.WithAlpha(0.6f), 24);
                    PenRobe(pos, f, s, r, col);
                    break;
                }
                case EnemyType.Moonward:
                    DrawMoonward(e, pos, r, col, t);
                    break;
                case EnemyType.Gnomon:
                    DrawGnomon(e, pos, r, col, t);
                    break;
                case EnemyType.DuskLobber:
                    DrawDuskLobber(e, pos, r, col, t);
                    break;
                case EnemyType.Sickle:
                    DrawSickle(e, pos, r, col, t);
                    break;
                case EnemyType.Nova:
                    DrawNova(e, pos, r, col, t);
                    break;
                case EnemyType.Heliograph:
                {
                    bool aiming = e.Phase == 1;
                    float heat = aiming ? Mathf.Clamp01(e.SeenT / Game.HelioWarn) : 0f;
                    Circle(pos, r * 2f, PenGold.WithAlpha(0.07f), 16);
                    PenRobe(pos, f, s, r, col);
                    // The mirror: a gold disc seen edge-on, out front, glowing as it aims.
                    V2 m = pos + f * (r * 1.15f);
                    if (aiming)
                        Circle(m, r * (0.8f + 0.9f * heat), PenSun.WithAlpha(0.25f + 0.45f * heat), 14);
                    Line(m - s * (r * 0.95f), m + s * (r * 0.95f), r * 0.55f, PenGold);
                    Line(m - s * (r * 0.6f), m + s * (r * 0.6f), r * 0.2f, PenSun.WithAlpha(0.6f + 0.4f * heat));
                    break;
                }
            }
        }

        static readonly Rgba PenMoon = new Rgba(0.79f, 0.82f, 0.89f), PenStone = new Rgba(0.23f, 0.2f, 0.19f), PenDusk = new Rgba(0.61f, 0.48f, 1f);

        void DrawMoonward(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            Circle(pos, Game.MoonwardOrbit, PenMoon.WithAlpha(0.05f), 24);
            Arc(pos, Game.MoonwardOrbit, 1f, PenMoon.WithAlpha(0.18f), 32);
            // The knight: a round helm and pauldrons, facing the nearest player.
            V2 f = FacingNearest(pos), s = f.Orthogonal();
            Circle(pos, r * 1.6f, PenDusk.WithAlpha(0.08f), 16);
            Circle(pos - s * (r * 0.75f), r * 0.5f, new Rgba(0.3f, 0.32f, 0.38f), 12);
            Circle(pos + s * (r * 0.75f), r * 0.5f, new Rgba(0.3f, 0.32f, 0.38f), 12);
            Circle(pos, r * 0.85f, new Rgba(0.14f, 0.15f, 0.19f), 18);
            Arc(pos, r * 0.85f, 2f, col, 18);
            Line(pos + f * (r * 0.15f) - s * (r * 0.4f), pos + f * (r * 0.15f) + s * (r * 0.4f), 2.5f, PenMoon.WithAlpha(0.8f));  // the visor slit
            // The moon.
            V2 m = Game.MoonwardMoon(e, pos);
            float mr = Game.MoonwardMoonR;
            Circle(m, mr * 2.2f, PenMoon.WithAlpha(0.18f), 14);
            Circle(m, mr, new Rgba(0.85f, 0.87f, 0.93f), 16);
            Arc(m, mr, 1.2f, Rgba.White, 16);
            Circle(m + new V2(-mr * 0.3f, -mr * 0.25f), mr * 0.25f, new Rgba(0.66f, 0.7f, 0.78f), 8);
            Circle(m + new V2(mr * 0.35f, mr * 0.2f), mr * 0.18f, new Rgba(0.66f, 0.7f, 0.78f), 8);
        }

        /// The way to the nearest player (for bodies whose Facing means something else).
        V2 FacingNearest(V2 pos)
        {
            V2 best = new V2(1f, 0f);
            float bd = float.MaxValue;
            foreach (var p in game.Players.Values)
            {
                float d = p.Disp.DistanceTo(pos);
                if (!p.Downed && d < bd && d > 1f)
                {
                    bd = d;
                    best = (p.Disp - pos) / d;
                }
            }
            return best;
        }

        void DrawGnomon(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            bool planted = e.Phase == 1 || e.Phase == 2;
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            float swell = e.Phase == 1 ? Mathf.Clamp01(e.SeenT / Game.GnomonSwell) : 0f;
            // The lantern on its pole, behind the slab.
            V2 lamp = pos - f * (r * 1.15f);
            Line(lamp, pos - f * (r * 0.3f), 3f, new Rgba(0.42f, 0.36f, 0.3f));
            Circle(lamp, r * (0.5f + 0.9f * swell), PenSun.WithAlpha(0.1f + 0.22f * swell), 18);
            Arc(lamp, r * (0.5f + 0.9f * swell), 1.5f, PenGold.WithAlpha(0.3f + 0.5f * swell), 18);
            Circle(lamp, r * (0.28f + 0.18f * swell), PenSun, 14);
            Arc(lamp, r * (0.28f + 0.18f * swell), 1.5f, PenGold, 14);
            // The slab: a thick stone wedge, wider at the front.
            V2 bl = pos - f * (r * 0.3f) + s * (r * 0.85f), br = pos - f * (r * 0.3f) - s * (r * 0.85f);
            V2 fl = pos + f * (r * 0.45f) + s * (r * 1.0f), fr = pos + f * (r * 0.45f) - s * (r * 1.0f);
            SetColor(PenStone);
            Tri(bl, fl, fr);
            Tri(bl, fr, br);
            var edge = col.Mix(PenTrim, planted ? 0.6f : 0.2f);
            Line(bl, fl, 2f, edge);
            Line(fl, fr, 2.5f, edge);
            Line(fr, br, 2f, edge);
            Line(br, bl, 2f, edge);
            for (int k = -1; k <= 1; k++)  // the dial's hour lines
                Line(pos + s * (k * r * 0.45f), pos + f * (r * 0.4f) + s * (k * r * 0.5f), 1.2f, PenGold.WithAlpha(0.25f));
        }

        /// A planted Gnomon's shadow on the floor; as the lantern flashes, everything but the shadow whites out.
        void DrawGnomonShadow(Enemy e, V2 pos, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            float a0 = e.Radius * 0.4f, a1 = Game.GnomonShadowLen;
            float w0 = Game.GnomonShadowBase + a0 * Game.GnomonShadowFlare, w1 = Game.GnomonShadowBase + a1 * Game.GnomonShadowFlare;
            V2 n0 = pos + f * a0, n1 = pos + f * a1;
            float k = e.Phase == 1 ? Mathf.Clamp01(e.SeenT / Game.GnomonSwell) : 1f;
            SetColor(new Rgba(0.06f, 0.04f, 0.12f, 0.35f + 0.3f * k));
            Tri(n0 + s * w0, n1 + s * w1, n1 - s * w1);
            Tri(n0 + s * w0, n1 - s * w1, n0 - s * w0);
            var rim = PenDusk.WithAlpha(0.45f + 0.4f * k);
            Line(n0 + s * w0, n1 + s * w1, 2f, rim);
            Line(n0 - s * w0, n1 - s * w1, 2f, rim);
            if (e.Phase == 2)
            {
                // The flash: a white-out round it with the shadow left dark, fading over the recovery.
                float fl = 1f - Mathf.Clamp01(e.SeenT / Game.GnomonRecover);
                float R = Game.GnomonReach;
                var white = PenSun.WithAlpha(0.6f * fl * fl);
                const int n = 40;
                V2 lamp = pos - f * (e.Radius * 1.15f);
                for (int i = 0; i < n; i++)
                {
                    float b0 = MathX.Tau * i / n, b1 = MathX.Tau * (i + 1) / n;
                    V2 m = V2.FromAngle((b0 + b1) * 0.5f) * (R * 0.6f) + pos;
                    if (Game.InGnomonShadow(e, m))
                        continue;  // the wedge stays dark
                    SetColor(white);
                    Tri(lamp, pos + V2.FromAngle(b0) * R, pos + V2.FromAngle(b1) * R);
                }
            }
        }

        void DrawDuskLobber(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            Circle(pos, r * 2f, PenDusk.WithAlpha(0.08f + (e.Winding ? 0.12f : 0f)), 16);
            var robeCol = col.Mix(new Rgba(0.35f, 0.28f, 0.55f), 0.5f);
            V2 nose = pos + f * (r * 1.1f), back = pos - f * (r * 0.95f);
            V2 l = pos - f * (r * 0.55f) + s * (r * 0.9f), rgt = pos - f * (r * 0.55f) - s * (r * 0.9f);
            SetColor(new Rgba(0.08f, 0.06f, 0.13f));
            Tri(nose, l, back);
            Tri(nose, back, rgt);
            Line(nose, l, 1.8f, robeCol);
            Line(l, back, 1.8f, robeCol);
            Line(back, rgt, 1.8f, robeCol);
            Line(rgt, nose, 1.8f, robeCol);
            Circle(pos - f * (r * 0.15f), r * 0.4f, new Rgba(0.02f, 0.01f, 0.04f), 14);
            float lift = e.Winding ? r * 0.5f : 0f;
            DuskOrb(pos + f * (r * 0.9f) + s * (r * 0.6f) + new V2(0f, -lift), r * 0.42f, t);
        }

        void DuskOrb(V2 at, float rr, float t)
        {
            Circle(at, rr * 2.6f, PenDusk.WithAlpha(0.25f), 14);
            Circle(at, rr, new Rgba(0.02f, 0.01f, 0.04f), 12);
            Arc(at, rr, 1.5f, PenDusk, 12);
            Arc(at, rr * 0.6f, 1f, PenMoon.WithAlpha(0.7f), 8, t * 3f, 2f);
        }

        /// A Dusk Lobber's throw: a violet ring filling on the landing spot, then the ball of night arcing over to it.
        void DrawDuskLobFx(Enemy e, float age, float throwAt, float flight, V2 land, float R, float t)
        {
            if (age < throwAt + flight)
            {
                float fill = throwAt > 0f ? Mathf.Clamp01(age / throwAt) : 1f;
                Circle(land, R, PenDusk.WithAlpha(0.08f), 32);
                Circle(land, R * fill, PenDusk.WithAlpha(0.16f), 32);
                Arc(land, R, 2.5f, PenDusk.WithAlpha(0.85f), 32);
            }
            if (age >= throwAt && age < throwAt + flight)
            {
                float k = (age - throwAt) / flight;
                V2 from = e.Disp, at = V2.Lerp(from, land, k);
                Circle(at + new V2(0f, 4f), 5f, new Rgba(0f, 0f, 0f, 0.35f), 10);
                DuskOrb(at + new V2(0f, -Mathf.Sin(k * MathX.Pi) * 70f), 6f, t);
            }
        }

        /// A Dusk pool's rim on the floor (the dark itself is drawn over everything, DrawDuskPools).
        void DrawDuskRim(PathPatch pt, float t)
        {
            if (!pt.Active)
                return;
            float life = Mathf.Clamp01((pt.Life - pt.Age) / Mathf.Max(0.01f, pt.Life - pt.Warn));
            Arc(pt.Pos, pt.Radius, 2f, PenDusk.WithAlpha(0.6f), 40);
            Arc(pt.Pos, pt.Radius + 6f, 2f, PenDusk.WithAlpha(0.8f), 40, -MathX.Pi / 2f, MathX.Tau * life);  // time left
        }

        const float DuskCell = 12f;

        /// Over the arena's contents: each Dusk pool as a soft grid of dark, cut away round each player.
        void DrawDuskPools(float t)
        {
            foreach (var pt in game.Patches.Values)
            {
                if (pt.Kind != PatchKind.Dusk || !pt.Active)
                    continue;
                float grow = Mathf.Clamp01((pt.Age - pt.Warn) / 0.3f), fade = Mathf.Clamp01((pt.Life - pt.Age) / 0.4f);
                float R = pt.Radius * (0.4f + 0.6f * grow);
                int n = Mathf.CeilToInt(2f * R / DuskCell);
                V2 o = pt.Pos - new V2(R, R);
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        V2 p00 = o + new V2(i * DuskCell, j * DuskCell), p11 = p00 + new V2(DuskCell, DuskCell);
                        V2 p10 = new V2(p11.X, p00.Y), p01 = new V2(p00.X, p11.Y);
                        float a00 = DuskAlpha(pt.Pos, R, p00), a10 = DuskAlpha(pt.Pos, R, p10), a01 = DuskAlpha(pt.Pos, R, p01), a11 = DuskAlpha(pt.Pos, R, p11);
                        if (a00 + a10 + a01 + a11 <= 0.001f)
                            continue;
                        a00 *= fade; a10 *= fade; a01 *= fade; a11 *= fade;
                        NightVertex(p00, a00); NightVertex(p10, a10); NightVertex(p11, a11);
                        NightVertex(p00, a00); NightVertex(p11, a11); NightVertex(p01, a01);
                    }
            }
        }

        float DuskAlpha(V2 c, float R, V2 p)
        {
            float dark = 1f - Smooth((p.DistanceTo(c) - R + 14f) / 14f);
            foreach (var pl in game.Players.Values)
                dark = Mathf.Min(dark, Smooth((p.DistanceTo(pl.Disp) - Game.DuskSight) / 16f));
            return 0.93f * dark;
        }

        void DrawSickle(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            float wind = e.Winding ? 0.5f + 0.5f * Mathf.Sin(t * 18f) : 0f;
            Circle(pos, r * 1.9f, PenMoon.WithAlpha(0.06f + 0.14f * wind), 16);
            PenRobe(pos, f, s, r, col.Mix(new Rgba(0.45f, 0.47f, 0.55f), 0.5f));
            // The blade at its side: a pale crescent, raised and glowing as it winds up.
            V2 at = pos + f * (r * (0.5f + 0.4f * wind)) + s * (r * 1.1f);
            float br = r * 0.85f, a = e.Facing + MathX.Pi * 0.5f;
            Circle(at, br * 1.5f, PenMoon.WithAlpha(0.12f + 0.3f * wind), 12);
            const int n = 8;
            V2 prevO = at + V2.FromAngle(a - 1.6f) * br, prevI = prevO;
            for (int i = 1; i <= n; i++)
            {
                float k = i / (float)n, ang = a - 1.6f + 3.2f * k, thick = Mathf.Sin(k * MathX.Pi) * br * 0.5f;
                V2 o = at + V2.FromAngle(ang) * br, inner = at + V2.FromAngle(ang) * (br - thick);
                SetColor(new Rgba(0.9f, 0.92f, 0.98f));
                Tri(prevO, o, inner);
                Tri(prevO, inner, prevI);
                prevO = o;
                prevI = inner;
            }
        }

        void DrawNova(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            bool lit = e.Phase == 1;
            float left = lit ? 1f - Mathf.Clamp01(e.SeenT / Game.NovaFuse) : 1f, hot = 1f - left;
            float rr = r * (1f + 0.5f * hot) + (lit ? Mathf.Sin(t * 30f) * hot * 1.5f : 0f);
            var glowCol = hot > 0.7f ? PenRed : PenOrange;
            if (lit)
                Arc(pos, Game.NovaBlastR, 1.5f, PenRed.WithAlpha(0.15f + 0.35f * hot), 48);  // how far it will reach
            Circle(pos, rr * 3f, glowCol.WithAlpha(0.18f + 0.25f * hot), 16);
            float spin = t * (lit ? 9f : 5f) + e.Id;
            for (int k = 0; k < 4; k++)
            {
                V2 d = V2.FromAngle(spin + k * MathX.Pi / 2f);
                Line(pos + d * rr, pos + d * (rr + 5f + 4f * hot), 1.6f, PenSun.WithAlpha(0.8f));
            }
            Circle(pos, rr, PenSun, 14);
            Arc(pos, rr, 1.6f, glowCol, 14);
            if (lit)
                Arc(pos, rr + 7f, 2.5f, (hot > 0.7f ? PenRed : PenGold).WithAlpha(0.95f), 24, -MathX.Pi / 2f, MathX.Tau * left);  // the fuse
        }

        /// A teardrop of flame pointing along f: `len` ahead of `at`, `back` behind it, `wide` across.
        void PenFlame(V2 at, V2 f, V2 s, float len, float back, float wide, Rgba c)
        {
            V2 nose = at + f * len, tail = at - f * back, l = at - f * (back * 0.3f) + s * wide, rgt = at - f * (back * 0.3f) - s * wide;
            SetColor(c);
            Tri(nose, l, tail);
            Tri(nose, tail, rgt);
        }

        /// A Penumbral Court cultist's robe: a teardrop pointing where it faces, dark with a gold trim, and the hood's opening.
        void PenRobe(V2 pos, V2 f, V2 s, float r, Rgba col)
        {
            V2 nose = pos + f * (r * 1.1f), back = pos - f * (r * 0.95f);
            V2 l = pos - f * (r * 0.55f) + s * (r * 0.9f), rgt = pos - f * (r * 0.55f) - s * (r * 0.9f);
            SetColor(PenCloth);
            Tri(nose, l, back);
            Tri(nose, back, rgt);
            var trim = col.Mix(PenTrim, 0.5f);
            Line(nose, l, 1.8f, trim);
            Line(l, back, 1.8f, trim);
            Line(back, rgt, 1.8f, trim);
            Line(rgt, nose, 1.8f, trim);
            Circle(pos - f * (r * 0.15f), r * 0.4f, new Rgba(0.04f, 0.03f, 0.02f), 14);
            Arc(pos - f * (r * 0.15f), r * 0.4f, 1.2f, PenTrim, 14);
        }
    }
}
