using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// How the Eclipse's fight looks (Eclipse.cs): the moon and its shadow, solar flares, closing rings of shadow,
    /// sunspots and the Umbra's dark, the echoes of earlier bosses, Last Light's burning edge, Totality's nova shell,
    /// crescent blades, and the body: a bright sun the moon crosses a step each phase (look 1 Totality), swelling before
    /// a flash. The fight was built from the mockup in Docs/Mockups/eclipse-5-stage.html.
    public partial class ArenaApp
    {
        static readonly Rgba SunGold = Rgba.Gold;
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
            // The Singularity echo: its disk of orbs spinning out, gold embers like its Accretion disk (SingularityLook.cs),
            // each trailing light back along the spiral it has come out on.
            if (s.DiskT >= 0f)
                for (int k = 0; k < Game.DiskOrbs; k++)
                {
                    for (int j = 4; j >= 1; j--)
                        if (s.DiskT - j * 0.05f >= 0f)
                            Circle(Game.DiskOrbAt(sun, s.DiskT - j * 0.05f, k), Game.EchoDiskOrbR * (1f - 0.17f * j), SingGold.WithAlpha(0.4f - 0.08f * j), 12);
                    DrawEmberOrb(Game.DiskOrbAt(sun, s.DiskT, k), Game.EchoDiskOrbR);
                }
            // An echo: the borrowed boss as a faint ghost above the sun, drawn with its own body, in a halo of its colour.
            V2 ghostAt = sun + new V2(0f, -130f);
            EnemyType echoed;
            Rgba ghost;
            if (s.Turn == EclipseTurn.EchoSingularity)  // its ghost is a lensed black hole, half there (SingularityLook.cs)
            {
                GlowDisc(ghostAt, 90f, SingGold, 0.12f);
                DrawLensedHole(ghostAt, 26f, 1, 0.45f + 0.15f * Mathf.Sin(t * 4f), 0f, t * 1.2f, -0.25f + 0.3f * Mathf.Sin(t * 0.3f), 5);
            }
            else if (EchoLook(s.Turn, out echoed, out ghost))
                DrawEchoGhost(echoed, ghost, ghostAt, t);
            // Totality: the shell of fire grows toward the edges as the nova builds.
            if (s.Stage == EclipseStage.Totality)
            {
                float k = 1f - Mathf.Clamp01(s.NovaT / Game.NovaTime), r = 70f + k * 900f;
                Circle(sun, r, new Rgba(1f, 0.31f, 0.24f, 0.05f + 0.1f * k), 64);
                Arc(sun, r, 5f, new Rgba(1f, 0.47f, 0.31f, 0.5f + 0.4f * Mathf.Sin(t * (6f + 20f * k))), 72);
            }
        }

        /// Which boss an echo turn borrows, and its colour (the ghost's halo).
        static bool EchoLook(EclipseTurn turn, out EnemyType boss, out Rgba col)
        {
            switch (turn)
            {
                case EclipseTurn.EchoColossus: boss = EnemyType.Colossus; col = new Rgba(0.85f, 0.63f, 0.4f); return true;
                case EclipseTurn.EchoLich: boss = EnemyType.Lich; col = new Rgba(0.43f, 1f, 0.69f); return true;
                case EclipseTurn.EchoHerald: boss = EnemyType.StormHerald; col = new Rgba(0.56f, 0.82f, 1f); return true;
                default: boss = EnemyType.Eclipse; col = SunGold; return false;
            }
        }

        readonly Dictionary<EnemyType, Enemy> echoGhosts = new Dictionary<EnemyType, Enemy>();

        /// An echo's ghost: the borrowed boss's own body (no state of its own, so in its first phase's look), shrunk,
        /// see-through and pulsing, bobbing in a soft halo of its colour with a faint gold rim (the Eclipse's light on it).
        void DrawEchoGhost(EnemyType type, Rgba col, V2 at, float t)
        {
            Enemy e;
            if (!echoGhosts.TryGetValue(type, out e))
            {
                var d = Defs.Get(type);
                echoGhosts[type] = e = new Enemy { Id = 870 + (int)type, Type = type, Def = d, Radius = 30f, Hp = 1f, MaxHp = 1f };
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            e.Disp = e.Pos = at + new V2(0f, Mathf.Sin(t * 1.6f) * 3f);
            e.Facing = MathX.Pi / 2f;  // looking down at the sun
            GlowDisc(e.Disp, 80f, col, 0.18f + 0.08f * pulse);
            Arc(e.Disp, 44f, 1.5f, SunGold.WithAlpha(0.25f + 0.15f * pulse), 40);
            float keep = glAlpha;
            glAlpha = keep * (0.4f + 0.15f * pulse);
            menuPreview = true;
            DrawEnemy(e, t);
            menuPreview = false;
            glAlpha = keep;
        }

        /// Over the floor, under the enemies: the moon, in the body's Totality look: the same near-black disc, lit
        /// only along the limb that faces the sun (a white-hot crescent edge with a gold fringe), a faint silver rim
        /// and halo so it reads against the dark floor, and a ring of shadow while the sun swells for a flash.
        void DrawEclipseMoon(float t)
        {
            var e = FindEclipse(game);
            if (e == null || e.Eclipse.MoonD <= 0f || e.Eclipse.TransT > 0f)
                return;
            V2 m = Game.MoonPos(e, e.Disp);
            float r = Game.MoonRadius, sunA = (e.Disp - m).Angle, swell = EclipseSwell(e);
            GlowDisc(m, r * 2.2f, MoonSilver, 0.22f);
            Circle(m, r + 8f, MoonSilver.WithAlpha(0.1f), 32);
            Circle(m, r, new Rgba(0.03f, 0.03f, 0.05f), 32);
            Arc(m, r, 1.5f, MoonSilver.WithAlpha(0.35f), 32);
            float lit = 1.9f + 0.6f * swell;  // the lit limb widens as the sun swells
            Arc(m, r + 3f, 8f, SunGold.WithAlpha(0.55f), 24, sunA - lit / 2f, lit);
            Arc(m, r, 4f, SunWhite, 24, sunA - lit * 0.4f, lit * 0.8f);
            if (swell > 0f)
                Arc(m, r + 12f, 2f, MoonSilver.WithAlpha(0.3f + 0.4f * swell), 32);
        }

        static readonly Rgba TotalityRed = new Rgba(1f, 0.3f, 0.26f);
        static readonly V2 EclipseCoverDir = V2.FromAngle(-2.3f);  // the side the moon comes in from (up and left)

        static int EclipseStageOf(Enemy e) => e.Eclipse == null ? e.Phase : (int)e.Eclipse.Stage;

        /// 0 to 1 through the 3 s before a flash: the sun swells, its streamers stretch.
        static float EclipseSwell(Enemy e)
        {
            var s = e.Eclipse;
            return s != null && s.FlashT > 0f && s.FlashLen > 0f ? 1f - s.FlashT / s.FlashLen : 0f;
        }

        static float CoronaHash(int k) => Mathf.Abs(Mathf.Sin(k * 12.9898f + 4.1414f) * 43758.547f) % 1f;

        /// Under the Eclipse's body (look 1 Totality, the rework renders in Tools/Preview): a soft glow and
        /// the corona's streamers, long uneven petals of light turning slowly; violet in the Umbra, crimson at
        /// Totality, longer each phase and stretching as the sun swells for a flash.
        void DrawEclipseCorona(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            int stage = EclipseStageOf(e);
            float swell = EclipseSwell(e);
            GlowDisc(pos, r * (3.2f + swell * 1.5f), stage >= 5 ? TotalityRed : SunGold, 0.35f + 0.4f * swell);
            var tint = stage == 2 ? UmbraViolet : stage >= 5 ? TotalityRed : SunWhite;
            const int n = 10;
            for (int pass = 0; pass < 2; pass++)
            {
                SetColor(tint.WithAlpha(pass == 0 ? 0.14f : 0.3f));
                float w = r * (pass == 0 ? 0.42f : 0.2f), reach = pass == 0 ? 1f : 0.75f;
                for (int k = 0; k < n; k++)
                {
                    float a = MathX.Tau * k / n + t * 0.06f + CoronaHash(k) * 0.5f;
                    float len = r * (0.7f + 1.3f * CoronaHash(k + 7)) * (1f + 0.12f * Mathf.Sin(t * 1.4f + k * 2.1f)) * (1f + 0.6f * swell) * (0.8f + 0.12f * stage);
                    V2 d = V2.FromAngle(a), side = d.Orthogonal();
                    Tri(pos + d * (r * 0.8f) + side * w, pos + d * (r * 0.8f) - side * w, pos + d * (r + len * reach));
                }
            }
        }

        /// The Eclipse's body: a bright sun with red prominences looping off its rim, and the moon's disc crossing it a
        /// step further each phase (the phase clock), covering it exactly at Totality. From the Umbra on, the thin
        /// burning ring left round the moon shows, with the diamond flare on the far edge. `col` is the body colour
        /// (white on a hit flash), used for the rim.
        void DrawEclipseBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var s = e.Eclipse;
            int stage = EclipseStageOf(e);
            float swell = EclipseSwell(e), cover = Mathf.Clamp01(stage / 5f);
            Circle(pos, r + 4f, col.WithAlpha(0.5f), 40);
            Circle(pos, r, stage >= 5 ? new Rgba(1f, 0.55f, 0.45f) : SunGold, 40);
            Circle(pos, r * 0.86f, SunWhite, 40);
            for (int k = 0; k < 3 + stage; k++)
            {
                float a = MathX.Tau * CoronaHash(k + 30) + t * 0.1f;
                float lr = 6f + 6f * CoronaHash(k + 40) + 2f * Mathf.Sin(t * 3f + k);
                Arc(pos + V2.FromAngle(a) * (r + 2f), lr, 3.5f, new Rgba(1f, 0.3f, 0.25f, 0.9f), 10, a - MathX.Pi / 2f, MathX.Pi);
            }
            V2 m = pos + EclipseCoverDir * (r * 2.05f * (1f - cover) * (1f - cover * 0.15f));
            Circle(m, r * 0.99f, new Rgba(0.03f, 0.03f, 0.05f), 40);
            Arc(m, r * 0.99f, 1.5f, MoonSilver.WithAlpha(0.25f), 40);
            if (cover > 0.5f)
            {
                Arc(pos, r, 3f, (stage >= 5 ? TotalityRed : SunWhite).WithAlpha(0.5f + 0.5f * cover), 48);
                V2 bead = pos - EclipseCoverDir * r;
                float bl = (stage >= 5 ? 26f : 18f) * (1f + 0.25f * Mathf.Sin(t * 7f)) + swell * 30f;
                GlowDisc(bead, 34f, SunWhite, 0.8f);
                Line(bead - new V2(bl, 0f), bead + new V2(bl, 0f), 2.5f, SunWhite);
                Line(bead - new V2(0f, bl), bead + new V2(0f, bl), 2.5f, SunWhite);
                Circle(bead, 6f, Rgba.White, 12);
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
