using UnityEngine;

namespace Arena
{
    /// Forbidden Path's look (Core/ForbiddenPath.cs; from its mockup). Drawing only, from the replicated FpT, FpAge, FpStage,
    /// FpStageT, FpTarget, FpAt, FpFrom, Enemy.FpBrands and FxKind.ForbiddenPath (Radius = Game.FpFx*).
    ///   - the beast (DrawForbiddenBeast, in place of the body): a black quadruped seen from above, legs splayed in a gallop,
    ///     a red rim, a spine ridge, red claws, a smoking mane and tail, two red eyes with trails; it leaps (Pounce), circles
    ///     the target (Spiral), fades to a shadow while its shades run (Rend) and rises and drops (Gate);
    ///   - under it (DrawForbiddenAura): the time left draining in red, a dashed chain to the locked target; the Spiral's red
    ///     trail and the Gate's rune circle while they run;
    ///   - on top (DrawForbiddenTop): the lock's spinning fang brackets and the Blood Brand pips under each branded enemy;
    ///   - on the floor (DrawForbiddenFloor): the Rake's black crescents, the Pounce's claw marks, the Gate's black sun with
    ///     its red rim and cracks;
    ///   - moments (DrawForbiddenFx): the shed's seal and smoke, the Pounce's shockwave, the bites' jaws, the Rend's shades and
    ///     tears, the brands bursting, the smoke of the revert.
    /// With Visual effects off: the beast without smoke or glow, plain rings and lines.
    public partial class ArenaApp
    {
        static readonly Rgba BeastRim = new Rgba(0.75f, 0.07f, 0.14f), BeastSpine = new Rgba(0.43f, 0.04f, 0.08f), BeastEye = new Rgba(1f, 0.2f, 0.27f);

        readonly System.Collections.Generic.Dictionary<int, V2> fpLastDisp = new System.Collections.Generic.Dictionary<int, V2>();
        readonly System.Collections.Generic.Dictionary<int, float> fpGait = new System.Collections.Generic.Dictionary<int, float>();  // each beast's stride, run on while it moves

        static float FpHash(float n)
        {
            float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        static float FpEase(float k) => k < 0.5f ? 2f * k * k : 1f - (-2f * k + 2f) * (-2f * k + 2f) / 2f;
        static float FpOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        /// Where the beast is drawn this frame, which way it faces, how high it is (0..1.6) and how solid (0..1).
        void FpPose(Player p, out V2 pos, out float ang, out float z, out float alpha)
        {
            pos = p.Disp;
            ang = p.Aim;
            z = 0f;
            alpha = Mathf.Clamp01((p.FpAge - 0.4f) / 0.2f) * Mathf.Clamp01(p.FpT / 0.25f);
            float st = p.FpStageT;
            V2 toAt = p.FpAt - p.Disp;
            switch (p.FpStage)
            {
                case 1:
                {
                    float k = Mathf.Clamp01(st / Game.FpLeap);
                    pos = V2.Lerp(p.FpFrom, p.Disp, FpEase(k));
                    z = Mathf.Sin(Mathf.PI * k) * 1.1f;
                    if (!(p.FpAt - p.FpFrom).IsZero)
                        ang = (p.FpAt - p.FpFrom).Angle;
                    break;
                }
                case 2:
                    ang = (toAt.IsZero ? p.Aim : toAt.Angle) + Mathf.Sin(st * Mathf.PI / 0.3f) * 0.45f;
                    break;
                case 3:
                {
                    float u = Mathf.Clamp01(st / Game.FpSpiralTime);
                    pos = Game.FpSpiralPos(p.FpAt, p.FpFrom, u);
                    V2 next = Game.FpSpiralPos(p.FpAt, p.FpFrom, Mathf.Min(1f, u + 0.02f)) - pos;
                    ang = next.IsZero ? ang : next.Angle + 0.35f;
                    break;
                }
                case 4:
                    ang = toAt.IsZero ? p.Aim : toAt.Angle;
                    alpha *= 1f - 0.85f * Mathf.Clamp01(st / 0.15f) + 0.85f * Mathf.Clamp01((st - 0.8f) / 0.2f);
                    break;
                case 5:
                    if (st < Game.FpGateSlam)
                        pos = V2.Lerp(p.Disp, p.FpAt, FpEase(Mathf.Clamp01(st / 0.6f)));
                    z = st < Game.FpGateRise ? FpOut(st / Game.FpGateRise) * 1.6f
                        : st < 0.6f ? 1.6f
                        : st < Game.FpGateSlam ? 1.6f * (1f - Mathf.Pow((st - 0.6f) / (Game.FpGateSlam - 0.6f), 2f)) : 0f;
                    ang = -Mathf.PI / 2f + Mathf.Clamp01(st / Game.FpGateRise) * Mathf.PI * 0.5f;
                    break;
            }
        }

        /// WorldPlayers.DrawPlayer: the beast in place of the body (true: drawn, skip the ninja).
        bool DrawForbiddenBeast(Player p, float t)
        {
            if (!Game.FpOn(p) || p.FpAge < 0.45f)
                return false;
            V2 pos;
            float ang, z, alpha;
            FpPose(p, out pos, out ang, out z, out alpha);
            V2 last;
            bool moving = p.FpStage == 3 || p.FpStage == 1 || fpLastDisp.TryGetValue(p.Id, out last) && (p.Disp - last).Length > 0.3f;
            fpLastDisp[p.Id] = p.Disp;
            float run;
            fpGait.TryGetValue(p.Id, out run);
            if (moving)
                run += Time.unscaledDeltaTime * 22f;
            fpGait[p.Id] = run;
            float ph = run;
            DrawBeastBody(pos, ang, z, ph, alpha, t, false, p.Id);
            return true;
        }

        /// A beast seen from above, facing `ang`; `shade`: Shadow Rend's see-through copy.
        void DrawBeastBody(V2 c, float ang, float z, float ph, float alpha, float t, bool shade, int seed)
        {
            if (alpha <= 0.01f)
                return;
            float sz = 0.85f;
            float sh = 1f / (1f + z * 0.6f);
            Ellipse(c + new V2(0f, 5f), 40f * sz * sh, 20f * sz * sh, ang, new Rgba(0f, 0f, 0f, 0.35f * alpha), 16);
            V2 o = c + new V2(0f, -z * 55f);
            float s = sz * (1f + z * 0.28f);
            V2 fw = V2.FromAngle(ang), sd = new V2(-fw.Y, fw.X);
            System.Func<float, float, V2> W = (x, y) => o + fw * (x * s) + sd * (y * s);
            Rgba body = shade ? new Rgba(0.055f, 0f, 0.016f, 0.82f * alpha) : Game.FpInk.WithAlpha(alpha);
            Rgba rim = shade ? Game.FpRed.WithAlpha(0.9f * alpha) : BeastRim.WithAlpha(0.85f * alpha);
            // the smoking tail
            if (fxOn)
                for (int i = 0; i < 12; i++)
                {
                    float k = i / 11f;
                    Circle(W(-30f - k * 46f, Mathf.Sin(ph * 0.5f + i * 0.7f) * 7f * k), (8f - k * 5f) * s, Game.FpInk.WithAlpha((0.7f - k * 0.45f) * alpha), 10);
                }
            // legs: hips and shoulders fixed, feet swinging fore and aft
            for (int l = 0; l < 4; l++)
            {
                float fx = l < 2 ? 18f : -22f, side = l % 2 == 0 ? -1f : 1f, off = (l < 2 ? 0f : Mathf.PI) + (l % 2) * 0.6f;
                float sw = Mathf.Sin(ph + off) * 13f;
                V2 hip = W(fx, side * 9f), foot = W(fx + sw, side * 25f), knee = W(fx + sw * 0.5f - 4f, side * 20f);
                Line(hip, knee, 8.5f * s, rim);
                Line(knee, foot, 8.5f * s, rim);
                Line(hip, knee, 6f * s, body);
                Line(knee, foot, 6f * s, body);
                for (int j = -1; j <= 1; j++)
                    Line(foot + fw * (2f * s) + sd * (j * 2.5f * s), foot + fw * (8f * s) + sd * (j * 3.2f * s), 1.4f * s, Game.FpRed.WithAlpha(alpha));
            }
            // body: the rim first, a little bigger, then the black over it
            for (int pass = 0; pass < 2; pass++)
            {
                float g = pass == 0 ? 2.2f : 0f;
                Rgba col = pass == 0 ? rim : body;
                Ellipse(W(-4f, 0f), (34f + g) * s, (13f + g) * s, ang, col, 18);
                Ellipse(W(14f, 0f), (17f + g) * s, (15f + g) * s, ang, col, 16);
                Ellipse(W(34f, 0f), (12f + g) * s, (10f + g) * s, ang, col, 14);
                Ellipse(W(46f, 0f), (8f + g) * s, (6f + g) * s, ang, col, 12);
                SetColor(col);
                for (int e = -1; e <= 1; e += 2)
                    Tri(W(31f, e * (6f - g)), W(19f - g, e * (19f + g)), W(37f, e * (8f - g)));  // the ears
            }
            if (shade)
                SoftGlow(o, 40f * s, Game.FpRed, 0.25f * alpha);
            // the spine ridge
            Line(W(-30f, 0f), W(26f, 0f), 1.6f * s, BeastSpine.WithAlpha(alpha));
            for (int i = 0; i < 9; i++)
            {
                float x = -26f + i * 6f, d = i % 2 == 0 ? 1f : -1f;
                Line(W(x, 0f), W(x - 5f, d * 5f), 1.4f * s, BeastSpine.WithAlpha(alpha));
            }
            // the mane's smoke
            if (fxOn)
                for (int i = 0; i < 7; i++)
                {
                    float a = FpHash(i + seed * 3) * MathX.Tau + t * 2f, r = 12f + FpHash(i + 9) * 6f;
                    Circle(W(12f + Mathf.Cos(a) * r * 0.6f, Mathf.Sin(a) * r), (5f + FpHash(i + 3) * 4f) * s, Game.FpInk.WithAlpha(0.55f * alpha), 10);
                }
            // the eyes and their trails
            for (int e = -1; e <= 1; e += 2)
            {
                V2 eye = W(41f, e * 4.6f);
                if (fxOn)
                {
                    SoftGlow(eye, 9f * s, Game.FpRed, 0.5f * alpha);
                    Line(W(39f, e * 4.6f), W(27f, e * 7f), 1.2f * s, BeastEye.WithAlpha(0.55f * alpha));
                }
                Ellipse(eye, 2.8f * s, 1.5f * s, ang + e * 0.3f, BeastEye.WithAlpha(alpha), 8);
            }
        }

        /// WorldRenderer.DrawPlayerAuras: the time left, the chain to the lock, the Spiral's trail, the Gate's runes.
        void DrawForbiddenAura(Player p, float t)
        {
            if (!Game.FpOn(p))
                return;
            float a = Mathf.Clamp01((p.FpAge - Game.FpShed) / 0.2f);
            if (a <= 0f)
                return;
            float total = p.FpAge + p.FpT - Game.FpShed, left = total > 0.01f ? Mathf.Clamp01(p.FpT / total) : 0f;
            Arc(p.Disp, p.Def.BodyR + 16f, 2.5f, Game.FpRed.WithAlpha(0.85f * a), 40, -MathX.Pi / 2f, MathX.Tau * left);
            Enemy target;
            if (p.FpTarget >= 0 && game.Enemies.TryGetValue(p.FpTarget, out target) && p.FpStage != 3)
            {
                V2 d = target.Disp - p.Disp;
                float len = d.Length;
                if (len > 1f)
                {
                    V2 u = d / len;
                    float dash = 12f, shift = Mathf.Repeat(t * 60f, dash);
                    for (float s = p.Def.BodyR + 18f - shift; s < len - target.Radius - 10f; s += dash)
                        if (s > p.Def.BodyR + 14f)
                            Line(p.Disp + u * s, p.Disp + u * Mathf.Min(s + 6f, len), 1.5f, Game.FpRed.WithAlpha(0.6f * a));
                }
            }
            if (p.FpStage == 3)  // the Spiral's trail
            {
                float end = Mathf.Min(p.FpStageT, Game.FpSpiralTime);
                for (float s = 0f; s < end; s += 0.03f)
                {
                    float age = (end - s) / Game.FpSpiralTime;
                    V2 a1 = Game.FpSpiralPos(p.FpAt, p.FpFrom, s / Game.FpSpiralTime), a2 = Game.FpSpiralPos(p.FpAt, p.FpFrom, Mathf.Min(end, s + 0.03f) / Game.FpSpiralTime);
                    Line(a1, a2, 10f * (1f - age) + 2f, Game.FpRed.WithAlpha((1f - age) * 0.7f));
                }
            }
            if (p.FpStage == 5 && p.FpStageT < Game.FpGateSlam)  // the Gate's rune circle gathering
            {
                float k = Mathf.Clamp01(p.FpStageT / Game.FpGateRise);
                Circle(p.FpAt, Game.FpGateR, new Rgba(0f, 0f, 0f, 0.3f * k), 48);
                float rot = -t * 0.8f;
                for (int i = 0; i < 24; i++)  // the dashed rim
                    Arc(p.FpAt, Game.FpGateR, 2f, Game.FpRed.WithAlpha(0.9f * k), 6, rot + i * MathX.Tau / 24f, MathX.Tau / 24f * 0.68f);
                for (int i = 0; i < 12; i++)  // the runes
                {
                    V2 d = V2.FromAngle(t + i * MathX.Tau / 12f), n = new V2(-d.Y, d.X);
                    V2 m = p.FpAt + d * 205f;
                    Line(m - d * 9f, m + d * 9f, 2f, Game.FpRed.WithAlpha(0.8f * k));
                    Line(m - n * 7f, m + n * 7f, 2f, Game.FpRed.WithAlpha(0.8f * k));
                }
                if (fxOn)
                    for (int i = 0; i < 24; i++)  // motes drawn in
                    {
                        V2 d = V2.FromAngle(FpHash(i) * MathX.Tau);
                        float r = Game.FpGateR - Mathf.Repeat(t * 180f + FpHash(i + 5) * 260f, 240f);
                        Circle(p.FpAt + d * r, 1.6f, Game.FpEmber.WithAlpha(0.8f * k), 6);
                    }
            }
        }

        /// Over the bodies: the lock's fang brackets and the brands.
        void DrawForbiddenTop(float t)
        {
            foreach (var e in game.Enemies.Values)
                if (e.FpBrands > 0)
                    for (int k = 0; k < e.FpBrands; k++)
                    {
                        V2 b = e.Disp + new V2(-(e.FpBrands - 1) * 4.5f + k * 9f, e.Radius + 9f);
                        SetColor(Game.FpRed);
                        Quad(b + new V2(0f, -4f), b + new V2(3f, 0f), b + new V2(0f, 4f), b + new V2(-3f, 0f));
                    }
            foreach (var p in game.Players.Values)
            {
                Enemy e;
                if (!Game.FpOn(p) || p.FpAge < Game.FpShed || p.FpTarget < 0 || !game.Enemies.TryGetValue(p.FpTarget, out e))
                    continue;
                float R = e.Radius + 10f + Mathf.Sin(t * 9f) * 2f, rot = t * 1.6f;
                for (int k = 0; k < 4; k++)
                {
                    V2 d = V2.FromAngle(rot + k * MathX.Pi / 2f), n = new V2(-d.Y, d.X);
                    SetColor(Game.FpRed);
                    Tri(e.Disp + d * R + n * 7f, e.Disp + d * (R - 6f), e.Disp + d * (R + 3f) - n * 7f);
                    if (fxOn)
                        SoftGlow(e.Disp + d * R, 8f, Game.FpRed, 0.3f);
                }
            }
        }

        /// Under the enemies: the Rake's crescents, the Pounce's claw marks, the Gate's black sun.
        void DrawForbiddenFloor(float t)
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.ForbiddenPath)
                    continue;
                int kind = (int)(fx.Radius + 0.5f);
                float k = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), fade = 1f - k;
                if (kind == Game.FpFxRake || kind == Game.FpFxRake2)
                {
                    float aim = (fx.Pos2 - fx.Pos).Angle, dir = kind == Game.FpFxRake ? 1f : -1f;
                    float grow = FpOut(Mathf.Clamp01(fx.T / 0.12f)), sweep = Game.FpRakeHalf * 2f * grow;
                    float start = aim - Game.FpRakeHalf * dir;
                    float th = 34f * fade + 6f;
                    Arc(fx.Pos, Game.FpRakeR - th / 2f, th, Game.FpInk.WithAlpha(0.9f * fade), 24, start, sweep * dir);
                    Arc(fx.Pos, Game.FpRakeR, 2.5f, Game.FpRed.WithAlpha(fade), 24, start, sweep * dir);
                    if (fxOn)
                        Arc(fx.Pos, Game.FpRakeR, 8f, Game.FpRed.WithAlpha(0.2f * fade), 24, start, sweep * dir);
                }
                else if (kind == Game.FpFxPounce)
                {
                    V2 d = (fx.Pos2 - fx.Pos).IsZero ? new V2(1f, 0f) : (fx.Pos2 - fx.Pos).Normalized();
                    V2 across = V2.FromAngle(d.Angle - 0.7f), n = new V2(-across.Y, across.X);
                    float g = FpOut(Mathf.Clamp01(fx.T / 0.08f));
                    for (int j = -1; j <= 1; j++)
                    {
                        V2 a = fx.Pos2 - across * 55f + n * (j * 16f), b = a + across * (110f * g);
                        Line(a, b, 9f, Game.FpInk.WithAlpha(fade));
                        Line(a, b, 2.5f, Game.FpRed.WithAlpha(fade));
                    }
                }
                else if (kind == Game.FpFxGate)
                {
                    float grow = FpOut(Mathf.Clamp01(fx.T / 0.18f)), R = Game.FpGateR * grow, f2 = 1f - Mathf.Clamp01((fx.T - 0.15f) / (fx.Dur - 0.15f));
                    Circle(fx.Pos, R, Game.FpInk.WithAlpha(0.88f * f2), 48);
                    Arc(fx.Pos, R, 18f * (1f - grow) + 3f, Game.FpRed.WithAlpha(f2), 48);
                    if (fxOn)
                    {
                        Arc(fx.Pos, R + 8f, 14f, Game.FpRed.WithAlpha(0.18f * f2), 48);
                        for (int j = 0; j < 16; j++)
                        {
                            V2 d = V2.FromAngle(j / 16f * MathX.Tau + FpHash(j) * 0.3f);
                            Jag(fx.Pos + d * 20f, fx.Pos + d * Mathf.Max(21f, R - 10f), 2f, Game.FpRed.WithAlpha(0.9f * f2), j * 31, 8, 9f, true);
                        }
                    }
                }
            }
        }

        /// FxKind.ForbiddenPath (WorldEffects): the moments over the bodies.
        void DrawForbiddenFx(Effect fx, float t)
        {
            int kind = (int)(fx.Radius + 0.5f);
            float k = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), fade = 1f - k;
            switch (kind)
            {
                case Game.FpFxShed:
                case Game.FpFxRevert:
                {
                    bool shed = kind == Game.FpFxShed;
                    if (shed)
                    {
                        float ring = 30f + FpOut(Mathf.Clamp01(fx.T / 0.7f)) * 45f;
                        for (int w = 0; w < 3; w++)
                            Arc(fx.Pos, ring + w * 3f, 5f - w * 1.5f, Game.FpRed.WithAlpha(fade), 32, k * 1.2f + w * 0.4f, MathX.Pi * 1.8f + w * 0.3f);
                        for (int j = 0; j < 8; j++)
                        {
                            V2 d = V2.FromAngle(k * 1.2f + j * MathX.Pi / 4f);
                            Line(fx.Pos + d * (ring + 10f), fx.Pos + d * (ring + 26f), 2f, Game.FpRed.WithAlpha(fade));
                        }
                    }
                    if (fxOn)
                        for (int j = 0; j < (shed ? 16 : 12); j++)
                        {
                            V2 d = V2.FromAngle(FpHash(j + (shed ? 0 : 40)) * MathX.Tau);
                            float r = FpOut(k) * (shed ? 30f + FpHash(j + 2) * 50f : 40f);
                            Circle(fx.Pos + d * r, (14f + FpHash(j + 4) * 10f) * (1f - k * 0.6f), Game.FpInk.WithAlpha(0.7f * fade), 12);
                        }
                    break;
                }
                case Game.FpFxPounce:
                {
                    float g = FpOut(Mathf.Clamp01(fx.T / 0.25f));
                    Arc(fx.Pos, 20f + (Game.FpPounceR - 20f) * g, 6f * (1f - g) + 1f, Game.FpRed.WithAlpha(fade), 40);
                    break;
                }
                case Game.FpFxBite:
                {
                    float gap = 4f + 14f * (1f - Mathf.Clamp01(fx.T / 0.08f));
                    float rot = FpHash(fx.Pos.X * 0.1f + fx.Pos.Y) * MathX.Tau;
                    V2 ax = V2.FromAngle(rot), n = new V2(-ax.Y, ax.X);
                    SetColor(Game.FpRed.WithAlpha(fade));
                    for (int sd = -1; sd <= 1; sd += 2)
                        for (int j = -2; j <= 2; j++)
                        {
                            V2 b = fx.Pos + ax * (j * 7f) + n * (sd * gap);
                            Tri(b - ax * 3.5f, b + ax * 3.5f, b - n * (sd * 7f));
                        }
                    if (fxOn)
                        SoftGlow(fx.Pos, 22f, Game.FpRed, 0.35f * fade);
                    break;
                }
                case Game.FpFxShade:
                {
                    V2 d = fx.Pos2 - fx.Pos;
                    float len = d.Length;
                    if (len < 1f)
                        break;
                    V2 u = d / len;
                    float run = Game.FpShadeRun, m = Mathf.Clamp01(fx.T / (run + 0.12f));
                    V2 head = fx.Pos + u * (FpOut(m) * (len + 50f));
                    float w = fx.T > run ? 6f + 10f * Mathf.Clamp01((fx.T - run) / 0.3f) : 6f;
                    float tf = 1f - Mathf.Clamp01((fx.T - run - 0.3f) / 0.45f);
                    Line(fx.Pos, head, w + 4f, Game.FpRed.WithAlpha(tf));
                    Line(fx.Pos, head, w, Game.FpInk.WithAlpha(tf));
                    if (fx.T < run + 0.35f)
                        DrawBeastBody(head, u.Angle, 0f, t * 30f, 0.85f * (1f - Mathf.Clamp01((fx.T - run - 0.1f) / 0.25f)), t, true, (int)fx.Pos.X);
                    break;
                }
                case Game.FpFxBrand:
                {
                    float g = FpOut(Mathf.Clamp01(fx.T / 0.2f));
                    SetColor(Game.FpRed.WithAlpha(fade));
                    for (int j = 0; j < 6; j++)
                    {
                        V2 d = V2.FromAngle(j * MathX.Pi / 3f), n = new V2(-d.Y, d.X);
                        Tri(fx.Pos + n * 5f, fx.Pos + d * (50f * g), fx.Pos - n * 5f);
                    }
                    if (fxOn)
                        SoftGlow(fx.Pos, 40f, Game.FpRed, 0.4f * fade);
                    break;
                }
            }
        }
    }
}
