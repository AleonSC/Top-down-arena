using UnityEngine;

namespace Arena
{
    /// How the Eclipse's fight looks (Eclipse.cs): the moon and its shadow, solar flares, closing rings of shadow,
    /// sunspots and the Umbra's dark, the echoes of earlier bosses, Last Light's burning edge, Totality's nova shell,
    /// crescent blades, and the sun itself swelling before a flash. Built from the mockup in
    /// Docs/Mockups/eclipse-5-stage.html.
    public partial class ArenaApp
    {
        static readonly Rgba SunGold = new Rgba(1f, 0.82f, 0.4f);
        static readonly Rgba SunWhite = new Rgba(1f, 0.95f, 0.8f);
        static readonly Rgba FlareOrange = new Rgba(1f, 0.54f, 0.24f);
        static readonly Rgba MoonSilver = new Rgba(0.79f, 0.82f, 0.89f);
        static readonly Rgba UmbraViolet = new Rgba(0.61f, 0.48f, 1f);

        static Enemy FindEclipse(Game g)
        {
            foreach (var e in g.Enemies.Values)
                if (e.Type == EnemyType.Eclipse && e.Eclipse != null && !g.IsSpawning(e) && e.Hp > 0f)
                    return e;
            return null;
        }

        /// On the floor, under everything: the moon's shadow, sunspots, flare lanes, closing rings, the echoes' beams
        /// and disk, and Totality's growing shell.
        void DrawEclipseFloor(float t)
        {
            var e = FindEclipse(game);
            if (e == null)
                return;
            var s = e.Eclipse;
            if (s.TransT > 0f)
                return;
            V2 sun = e.Disp;
            // The moon's shadow: a cone from behind the moon, darker while the sun swells for a flash.
            if (s.MoonD > 0f)
            {
                V2 u = V2.FromAngle(s.MoonA), n = u.Orthogonal(), m = Game.MoonPos(e, sun);
                float far = 2200f, w0 = Game.MoonRadius * 0.9f, w1 = w0 + far * Game.ShadowSpread;
                bool charging = s.FlashT > 0f;
                SetColor(new Rgba(0f, 0f, 0.02f, charging ? 0.5f + 0.12f * Mathf.Sin(t * 8f) : 0.26f));
                Quad(m + n * w0, m + u * far + n * w1, m + u * far - n * w1, m - n * w0);
                if (charging)
                {
                    DashedLine(m + n * w0, m + u * far + n * w1, 2f, MoonSilver.WithAlpha(0.55f));
                    DashedLine(m - n * w0, m + u * far - n * w1, 2f, MoonSilver.WithAlpha(0.55f));
                }
            }
            // Sunspots: warm pools of light.
            foreach (var sp in s.Spots)
            {
                Circle(sp.Pos, Game.SunspotRadius, SunGold.WithAlpha(0.12f), 40);
                Circle(sp.Pos, Game.SunspotRadius * 0.6f, SunGold.WithAlpha(0.08f), 32);
                Arc(sp.Pos, Game.SunspotRadius, 2f, SunGold.WithAlpha(0.45f), 48);
            }
            // Flares: a line that thickens over its warning, then a lane of fire.
            foreach (var f in s.Flares)
            {
                V2 tip = f.From + V2.FromAngle(f.Ang) * Game.FlareLen;
                if (f.T < Game.FlareWarn)
                {
                    float k = f.T / Game.FlareWarn;
                    Line(f.From, tip, 2f + 6f * k, FlareOrange.WithAlpha(0.25f + 0.5f * k));
                }
                else if (Game.FlareLiveNow(f))
                {
                    Line(f.From, tip, Game.FlareWidth * 1.75f, FlareOrange.WithAlpha(0.3f));
                    Line(f.From, tip, Game.FlareWidth, new Rgba(1f, 0.69f, 0.36f));
                    Line(f.From, tip, Game.FlareWidth * 0.35f, SunWhite);
                }
            }
            // Closing umbra: a band of shadow with two gaps, rimmed in violet.
            foreach (var r in s.Rings)
            {
                if (r.R <= 0f)
                    continue;
                const int segs = 96;
                for (int i = 0; i < segs; i++)
                {
                    float a0 = MathX.Tau * i / segs, a1 = MathX.Tau * (i + 1) / segs;
                    V2 mid = r.Center + V2.FromAngle((a0 + a1) / 2f) * r.R;
                    if (Game.InUmbraGap(r, mid))
                        continue;
                    Arc(r.Center, r.R, Game.UmbraRingWidth, new Rgba(0.18f, 0.1f, 0.36f, 0.88f), 1, a0, a1 - a0);
                    Arc(r.Center, r.R - Game.UmbraRingWidth / 2f, 2.5f, UmbraViolet.WithAlpha(0.9f), 1, a0, a1 - a0);
                    Arc(r.Center, r.R + Game.UmbraRingWidth / 2f, 2.5f, UmbraViolet.WithAlpha(0.9f), 1, a0, a1 - a0);
                }
            }
            // The Lich echo: dashed guides, then two soul beams.
            if (s.SweepOn > 0)
                for (int k = 0; k < 2; k++)
                {
                    V2 tip = sun + V2.FromAngle(s.SweepA + k * MathX.Pi) * Game.EchoSweepLen;
                    var soul = new Rgba(0.43f, 1f, 0.69f);
                    if (s.SweepOn == 1)
                        DashedLine(sun, tip, 3f, soul.WithAlpha(0.5f));
                    else
                    {
                        Line(sun, tip, 34f, soul.WithAlpha(0.28f));
                        Line(sun, tip, 10f, new Rgba(0.79f, 1f, 0.89f));
                    }
                }
            // The Singularity echo: its disk of orbs spinning out.
            if (s.DiskT >= 0f)
                for (int k = 0; k < Game.DiskOrbs; k++)
                {
                    V2 o = Game.DiskOrbAt(sun, s.DiskT, k);
                    Circle(o, Game.EchoDiskOrbR + 11f, new Rgba(0.75f, 0.47f, 1f, 0.2f), 16);
                    Circle(o, Game.EchoDiskOrbR, new Rgba(0.84f, 0.69f, 1f), 16);
                    Circle(o, Game.EchoDiskOrbR * 0.45f, new Rgba(0.1f, 0.02f, 0.15f), 10);
                }
            // An echo: the borrowed boss as a faint gold-rimmed ghost above the sun.
            int sides;
            Rgba ghost;
            if (EchoLook(s.Turn, out sides, out ghost))
            {
                float a = 0.35f + 0.15f * Mathf.Sin(t * 4f);
                V2 at = sun + new V2(0f, -130f);
                Circle(at, 46f, ghost.WithAlpha(0.06f), 24);
                for (int i = 0; i < sides; i++)
                    Line(at + V2.FromAngle(t * 0.4f + MathX.Tau * i / sides) * 46f, at + V2.FromAngle(t * 0.4f + MathX.Tau * (i + 1) / sides) * 46f, 2.5f, ghost.WithAlpha(a));
            }
            // Totality: the shell of fire grows toward the edges as the nova builds.
            if (s.Stage == EclipseStage.Totality)
            {
                float k = 1f - Mathf.Clamp01(s.NovaT / Game.NovaTime), r = 70f + k * 900f;
                Circle(sun, r, new Rgba(1f, 0.31f, 0.24f, 0.05f + 0.1f * k), 64);
                Arc(sun, r, 5f, new Rgba(1f, 0.47f, 0.31f, 0.5f + 0.4f * Mathf.Sin(t * (6f + 20f * k))), 72);
            }
        }

        static bool EchoLook(EclipseTurn turn, out int sides, out Rgba col)
        {
            switch (turn)
            {
                case EclipseTurn.EchoColossus: sides = 8; col = new Rgba(0.85f, 0.63f, 0.4f); return true;
                case EclipseTurn.EchoLich: sides = 7; col = new Rgba(0.43f, 1f, 0.69f); return true;
                case EclipseTurn.EchoHerald: sides = 9; col = new Rgba(0.56f, 0.82f, 1f); return true;
                case EclipseTurn.EchoSingularity: sides = 12; col = new Rgba(0.71f, 0.55f, 1f); return true;
                default: sides = 0; col = SunGold; return false;
            }
        }

        /// Over the floor, under the enemies: the moon.
        void DrawEclipseMoon(float t)
        {
            var e = FindEclipse(game);
            if (e == null || e.Eclipse.MoonD <= 0f || e.Eclipse.TransT > 0f)
                return;
            V2 m = Game.MoonPos(e, e.Disp);
            float r = Game.MoonRadius;
            Circle(m, r + 10f, MoonSilver.WithAlpha(0.12f), 32);
            Circle(m, r, new Rgba(0.16f, 0.18f, 0.22f), 32);
            Circle(m + new V2(-12f, -8f), 8f, new Rgba(0.23f, 0.25f, 0.3f), 12);
            Circle(m + new V2(14f, 10f), 6f, new Rgba(0.23f, 0.25f, 0.3f), 12);
            Circle(m + new V2(4f, -18f), 4f, new Rgba(0.23f, 0.25f, 0.3f), 10);
            Arc(m, r, 3f, MoonSilver, 32);
        }

        /// Under the Eclipse's body: its glow (swelling before a flash) and a corona that grows wilder each stage.
        void DrawEclipseCorona(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var s = e.Eclipse;
            int stage = s == null ? e.Phase : (int)s.Stage;
            float swell = s != null && s.FlashT > 0f && s.FlashLen > 0f ? 1f - s.FlashT / s.FlashLen : 0f;
            Circle(pos, r + 34f + stage * 6f + swell * 70f, SunGold.WithAlpha(0.07f + 0.12f * swell), 40);
            Circle(pos, r + 14f + swell * 26f, SunWhite.WithAlpha(0.08f + 0.22f * swell), 32);
            int rays = 12 + stage * 2;
            var rayCol = stage >= 5 ? new Rgba(1f, 0.3f, 0.3f) : stage == 2 ? UmbraViolet : col;
            for (int k = 0; k < rays; k++)
            {
                float a = MathX.Tau * k / rays + t * (0.3f + 0.3f * stage);
                Line(pos + V2.FromAngle(a) * (r + 2f), pos + V2.FromAngle(a) * (r + 14f + 6f * stage + 4f * Mathf.Sin(t * 6f + k) + swell * 40f), 3f, rayCol.WithAlpha(0.8f));
            }
            if (s != null && s.TransT > 0f)
                Arc(pos, r + 18f, 4f, Rgba.White.WithAlpha(0.5f + 0.4f * Mathf.Sin(t * 16f)), 40);
        }

        /// Over the arena's contents: the Umbra's dark (lit round each player, the sunspots and the sun), Last
        /// Light's burning edge, and the white-out as a flash goes off.
        void DrawEclipseSky(float t)
        {
            var e = FindEclipse(game);
            if (e == null)
                return;
            var s = e.Eclipse;
            if (s.Stage == EclipseStage.Umbra && s.TransT <= 0f)
                DrawUmbraDark(e, s);
            if (s.Inset > 0f && s.TransT <= 0f)
            {
                float i = s.Inset;
                var burnt = new Rgba(0.47f, 0.12f, 0.02f, 0.6f);
                FillRect(0f, 0f, AW, i, burnt);
                FillRect(0f, AH - i, AW, i, burnt);
                FillRect(0f, i, i, AH - 2f * i, burnt);
                FillRect(AW - i, i, i, AH - 2f * i, burnt);
                var edge = new Rgba(1f, 0.55f + 0.15f * Mathf.Sin(t * 9f), 0.24f, 0.95f);
                Line(new V2(i, i), new V2(AW - i, i), 5f, edge);
                Line(new V2(i, AH - i), new V2(AW - i, AH - i), 5f, edge);
                Line(new V2(i, i), new V2(i, AH - i), 5f, edge);
                Line(new V2(AW - i, i), new V2(AW - i, AH - i), 5f, edge);
                var lick = new Rgba(1f, 0.7f, 0.35f, 0.4f);
                for (float x = i; x < AW - i; x += 38f)
                {
                    float h = 10f + 8f * Mathf.Sin(t * 7f + x);
                    Line(new V2(x, i), new V2(x + 8f, i + h), 2f, lick);
                    Line(new V2(x, AH - i), new V2(x + 8f, AH - i - h), 2f, lick);
                }
                for (float y = i; y < AH - i; y += 38f)
                {
                    float h = 10f + 8f * Mathf.Sin(t * 7f + y);
                    Line(new V2(i, y), new V2(i + h, y + 8f), 2f, lick);
                    Line(new V2(AW - i, y), new V2(AW - i - h, y + 8f), 2f, lick);
                }
            }
        }

        const float UmbraSight = 140f, UmbraEdge = 60f, UmbraCell = 30f;
        float[] umbraAlpha;

        /// The Umbra's dark, like Nightfall's (PathsUi.cs): a grid of shade, cut away round each player, each
        /// sunspot and the sun's rim.
        void DrawUmbraDark(Enemy e, EclipseState s)
        {
            int nx = Mathf.CeilToInt(AW / UmbraCell), ny = Mathf.CeilToInt(AH / UmbraCell);
            if (umbraAlpha == null || umbraAlpha.Length < (nx + 1) * (ny + 1))
                umbraAlpha = new float[(nx + 1) * (ny + 1)];
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    V2 p = new V2(Mathf.Min(AW, i * UmbraCell), Mathf.Min(AH, j * UmbraCell));
                    float dark = Smooth((p.DistanceTo(e.Disp) - e.Radius - 50f) / UmbraEdge);
                    foreach (var pl in game.Players.Values)
                        dark = Mathf.Min(dark, Smooth((p.DistanceTo(pl.Disp) - UmbraSight) / UmbraEdge));
                    foreach (var sp in s.Spots)
                        dark = Mathf.Min(dark, Smooth((p.DistanceTo(sp.Pos) - Game.SunspotRadius + 10f) / UmbraEdge));
                    umbraAlpha[j * (nx + 1) + i] = 0.9f * dark;
                }
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * (nx + 1) + i;
                    float a00 = umbraAlpha[k], a10 = umbraAlpha[k + 1], a01 = umbraAlpha[k + nx + 1], a11 = umbraAlpha[k + nx + 2];
                    if (a00 + a10 + a01 + a11 <= 0.001f)
                        continue;
                    V2 p00 = new V2(i * UmbraCell, j * UmbraCell), p11 = new V2(Mathf.Min(AW, p00.X + UmbraCell), Mathf.Min(AH, p00.Y + UmbraCell));
                    V2 p10 = new V2(p11.X, p00.Y), p01 = new V2(p00.X, p11.Y);
                    NightVertex(p00, a00); NightVertex(p10, a10); NightVertex(p11, a11);
                    NightVertex(p00, a00); NightVertex(p11, a11); NightVertex(p01, a01);
                }
            // The rings stay faintly visible through the dark.
            foreach (var r in s.Rings)
                if (r.R > 0f)
                    Arc(r.Center, r.R, 2f, UmbraViolet.WithAlpha(0.35f), 96);
        }

        /// The Eclipse's crescent blade: a spinning pale-violet crescent with a soft halo.
        void DrawCrescentShot(Bullet b, float t)
        {
            float a = t * 14f + b.Id;
            Circle(b.Pos, b.Radius + 8f, UmbraViolet.WithAlpha(0.15f), 16);
            const int n = 10;
            V2 prevO = b.Pos + V2.FromAngle(a) * b.Radius, prevI = prevO;
            for (int i = 1; i <= n; i++)
            {
                float f = i / (float)n, ang = a + f * MathX.Pi * 1.3f;
                float thick = Mathf.Sin(f * MathX.Pi) * b.Radius * 0.55f;
                V2 o = b.Pos + V2.FromAngle(ang) * b.Radius, inner = b.Pos + V2.FromAngle(ang) * (b.Radius - thick);
                SetColor(Defs.BulletColor(b.Kind));
                Quad(prevO, o, inner, prevI);
                prevO = o;
                prevI = inner;
            }
        }
    }
}
