using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The reactive menu backgrounds after the Ripples reference (BackgroundReact.cs): Boot Log, Vector Rocks and Life Grid, all in the
    /// CRT look (mockup Docs/Mockups/crt-backgrounds.html). Unlike Ripples they keep a little state, stepped once a frame with
    /// ReactStepOnce, and they read the pointer, its speed (ReactEnergy) and the click (ReactClick).
    public partial class ArenaApp
    {
        // --- Boot Log: a BIOS terminal that logs as you move and click ----------------------------

        struct BootLine { public string Text; public byte Kind; }  // Kind: 0 log, 1 your click, 2 warning

        static readonly string[] BootWords =
        {
            "MEM CHECK", "LOAD SPRITES", "AUDIO INIT", "NET LINK", "HUD MOUNT", "WAVE TABLE", "SHADER BAKE", "PAD DETECT",
            "SEED RNG", "MOUNT ARENA", "TICK SYNC", "CRT DEGAUSS",
        };

        const int BootRows = 22;
        const float BootRowH = 26f, BootTop = 84f, BootX = 250f, BootAdv = 18f;  // (the block sits in the middle so a thumbnail that crops the sides keeps it)
        readonly List<BootLine> bootLines = new List<BootLine>();
        float bootAcc, bootShift;
        int bootFrame = -1;

        BootLine BootMake()
        {
            bool warn = ReactRand() < 0.06f;
            string word = BootWords[(int)(ReactRand() * BootWords.Length) % BootWords.Length];
            string s = ((warn ? "[WARN] " : "[ OK ] ") + word + " ").PadRight(30, '.') + " 0x" + ((int)(ReactRand() * 65535f)).ToString("X4");
            return new BootLine { Text = s, Kind = (byte)(warn ? 2 : 0) };
        }

        void BootPush(BootLine l)
        {
            bootLines.Add(l);
            if (bootLines.Count > BootRows)
                bootLines.RemoveAt(0);
            bootShift = BootRowH;
        }

        /// Pixel-ish text with no font: each character is a few strips and stems, chosen by the character, so the same letter always looks alike.
        void BootText(string s, float x, float y, Rgba col, float k = 1f)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                float gx = x + i * BootAdv * k;
                if (ch == ' ')
                    continue;
                if (ch == '.')
                {
                    FillRect(gx + 3f * k, y + 12f * k, 4f * k, 4f * k, col);
                    continue;
                }
                float hs = Hash(ch * 13 + 5);
                if (hs > 0.45f)
                    FillRect(gx, y, 3f * k, 15f * k, col);
                if (hs < 0.7f)
                    FillRect(gx + 7f * k, y, 3f * k, 15f * k, col);
                for (int r = 0; r < 3; r++)
                    if (Hash(ch * 13 + r) > 0.35f)
                        FillRect(gx, y + r * 6f * k, 10f * k, 3f * k, col);
            }
        }

        void BgBootLog(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.01f, 0.03f, 0.02f));
            var ink = new Rgba(0.35f, 1f, 0.6f);
            if (bootLines.Count == 0)
                for (int i = 0; i < BootRows; i++)
                    bootLines.Add(BootMake());
            if (ReactStepOnce(ref bootFrame, out float dt))
            {
                bootAcc += dt * (4f + 26f * ReactEnergy);
                while (bootAcc >= 1f)
                {
                    bootAcc -= 1f;
                    BootPush(BootMake());
                }
                bootShift *= Mathf.Exp(-14f * dt);
                if (ReactClick(out var c))
                    BootPush(new BootLine { Text = "> INPUT ACCEPTED   X=" + (int)c.X + " Y=" + (int)c.Y, Kind = 1 });
            }
            int hover = ReactPointer(out var p) ? Mathf.FloorToInt((p.Y - BootTop - bootShift) / BootRowH) : -1;
            int n = bootLines.Count;
            for (int i = 0; i < n; i++)
            {
                var ln = bootLines[i];
                float y = BootTop + i * BootRowH + bootShift, dim = 0.3f + 0.7f * i / Mathf.Max(1, n - 1);
                Rgba col = ln.Kind == 1 ? new Rgba(0.8f, 1f, 0.9f) : ln.Kind == 2 ? new Rgba(1f, 0.75f, 0.3f, 0.95f) : ink.WithAlpha(dim);
                if (i == hover)
                {
                    FillRect(BootX - 14f, y - 5f, BootAdv * 40f, BootRowH - 3f, ink.WithAlpha(0.85f));
                    col = new Rgba(0.01f, 0.04f, 0.02f);
                }
                BootText(ln.Text, BootX, y, col);
            }
            FillRect(0, 0, W, 66f, new Rgba(0.01f, 0.03f, 0.02f));
            BootText("TOP-DOWN ARENA BIOS V1.07", BootX, 22f, ink.WithAlpha(0.9f));
            FillRect(BootX - 14f, 58f, BootAdv * 40f, 2f, ink.WithAlpha(0.6f));
            if ((int)(t * 2f) % 2 == 0 && n > 0)
                FillRect(BootX + bootLines[n - 1].Text.Length * BootAdv + 6f, BootTop + (n - 1) * BootRowH + bootShift - 1f, 12f, 17f, ink.WithAlpha(0.9f));
            if (ReactPointer(out var g))
                SoftGlow(g, 220f, ink.WithAlpha(0.05f + 0.08f * ReactEnergy));
        }

        // --- Vector Rocks: an asteroid field with a little ship on your pointer --------------------

        class BgRock { public float X, Y, Vx, Vy, R, A, Va; public float[] V = new float[9]; public bool Dead; }
        struct BgShot { public float X, Y, Vx, Vy, Life; }

        readonly List<BgRock> rockList = new List<BgRock>();
        readonly List<BgShot> rockShots = new List<BgShot>();
        readonly List<BgShot> rockSparks = new List<BgShot>();  // (the same shape: a position, a velocity, a life)
        float rockSx = W / 2f, rockSy = H / 2f, rockAng = -Mathf.PI / 2f, rockWait;
        int rockFrame = -1;
        bool rockMoving;

        BgRock RockMake(float r, float x, float y)
        {
            var k = new BgRock { X = x, Y = y, R = r, A = ReactRand() * MathX.Tau, Va = ReactRange(-1f, 1f) };
            for (int i = 0; i < k.V.Length; i++)
                k.V[i] = r * ReactRange(0.72f, 1.15f);
            float a = ReactRand() * MathX.Tau, s = ReactRange(28f, 66f) * (r > 30f ? 1f : 1.4f);
            k.Vx = Mathf.Cos(a) * s;
            k.Vy = Mathf.Sin(a) * s;
            return k;
        }

        void RockSparks(float x, float y, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                float a = ReactRand() * MathX.Tau, s = ReactRange(0.3f, 1f) * speed;
                rockSparks.Add(new BgShot { X = x, Y = y, Vx = Mathf.Cos(a) * s, Vy = Mathf.Sin(a) * s, Life = ReactRange(0.3f, 0.8f) });
            }
        }

        void RockStep(float t, float dt)
        {
            if (rockList.Count == 0 && rockWait <= 0f)
                foreach (float r in new[] { 44f, 44f, 34f, 34f, 24f, 24f, 44f })
                    rockList.Add(RockMake(r, ReactRand() * W, ReactRand() * H));
            V2 target = ReactPointer(out var p) ? p : new V2(W / 2f + Mathf.Cos(t * 0.5f) * W * 0.3f, H / 2f + Mathf.Sin(t * 0.7f) * H * 0.28f);
            float ox = rockSx, oy = rockSy;
            rockSx += (target.X - rockSx) * Mathf.Min(1f, dt * 5f);
            rockSy += (target.Y - rockSy) * Mathf.Min(1f, dt * 5f);
            float vx = rockSx - ox, vy = rockSy - oy;
            rockMoving = vx * vx + vy * vy > 2.5f;
            if (rockMoving)
            {
                float df = Mathf.Atan2(vy, vx) - rockAng;
                df = Mathf.Atan2(Mathf.Sin(df), Mathf.Cos(df));
                rockAng += df * Mathf.Min(1f, dt * 10f);
            }
            if (ReactClick(out var c))
                for (int i = 0; i < 10; i++)
                {
                    float a = rockAng + i * MathX.Tau / 10f;
                    rockShots.Add(new BgShot { X = rockSx, Y = rockSy, Vx = Mathf.Cos(a) * 480f, Vy = Mathf.Sin(a) * 480f, Life = 1.1f });
                }
            rockWait -= dt;
            if (rockList.Count < 5 && rockWait <= 0f)
            {
                rockList.Add(RockMake(40f, ReactRand() < 0.5f ? 0f : W, ReactRand() * H));
                rockWait = 0.9f;
            }
            float sp = 1f + 0.6f * ReactEnergy;
            foreach (var r in rockList)
            {
                float dx = r.X - rockSx, dy = r.Y - rockSy, dist = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
                if (dist < r.R + 100f)
                {
                    r.Vx += dx / dist * 350f * dt;
                    r.Vy += dy / dist * 350f * dt;
                }
                float s = Mathf.Sqrt(r.Vx * r.Vx + r.Vy * r.Vy);
                if (s > 170f)
                {
                    r.Vx *= 170f / s;
                    r.Vy *= 170f / s;
                }
                r.X = MathX.Fmod(r.X + r.Vx * dt * sp + W, W);
                r.Y = MathX.Fmod(r.Y + r.Vy * dt * sp + H, H);
                r.A += r.Va * dt;
            }
            for (int i = rockShots.Count - 1; i >= 0; i--)
            {
                var b = rockShots[i];
                b.X += b.Vx * dt;
                b.Y += b.Vy * dt;
                b.Life -= dt;
                for (int k = 0; k < rockList.Count && b.Life > 0f; k++)
                {
                    var r = rockList[k];
                    float dx = b.X - r.X, dy = b.Y - r.Y;
                    if (r.Dead || dx * dx + dy * dy > r.R * r.R)
                        continue;
                    r.Dead = true;
                    b.Life = 0f;
                    RockSparks(r.X, r.Y, 7, 150f);
                    if (r.R > 24f)
                        for (int j = 0; j < 2; j++)
                        {
                            var n = RockMake(r.R * 0.6f, r.X, r.Y);
                            n.Vx += r.Vx;
                            n.Vy += r.Vy;
                            rockList.Add(n);
                        }
                }
                if (b.Life <= 0f)
                    rockShots.RemoveAt(i);
                else
                    rockShots[i] = b;
            }
            rockList.RemoveAll(r => r.Dead);
            if (rockList.Count > 16)
                rockList.RemoveRange(16, rockList.Count - 16);
            for (int i = rockSparks.Count - 1; i >= 0; i--)
            {
                var s = rockSparks[i];
                s.X += s.Vx * dt;
                s.Y += s.Vy * dt;
                s.Life -= dt;
                if (s.Life <= 0f)
                    rockSparks.RemoveAt(i);
                else
                    rockSparks[i] = s;
            }
        }

        /// A line width that stays visible in a small thumbnail (about 1.3 px on screen at the least).
        float ThumbLine(float w) => Mathf.Max(w, 1.3f / Mathf.Max(0.01f, bgViewScale));

        void BgVectorRocks(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.01f, 0.02f, 0.045f));
            var ink = new Rgba(0.55f, 0.8f, 1f);
            if (ReactStepOnce(ref rockFrame, out float dt))
                RockStep(t, dt);
            foreach (var r in rockList)
            {
                int n = r.V.Length;
                for (int i = 0; i < n; i++)
                {
                    float a0 = r.A + i * MathX.Tau / n, a1 = r.A + (i + 1) * MathX.Tau / n;
                    var p0 = new V2(r.X + Mathf.Cos(a0) * r.V[i], r.Y + Mathf.Sin(a0) * r.V[i]);
                    var p1 = new V2(r.X + Mathf.Cos(a1) * r.V[(i + 1) % n], r.Y + Mathf.Sin(a1) * r.V[(i + 1) % n]);
                    Line(p0, p1, 8f, ink.WithAlpha(0.1f));
                    Line(p0, p1, ThumbLine(2.5f), ink.WithAlpha(0.9f));
                }
            }
            foreach (var s in rockSparks)
                Line(new V2(s.X, s.Y), new V2(s.X - s.Vx * 0.05f, s.Y - s.Vy * 0.05f), 2f, new Rgba(0.8f, 0.95f, 1f, Mathf.Clamp01(s.Life * 2f)));
            foreach (var b in rockShots)
            {
                Circle(new V2(b.X, b.Y), 9f, ink.WithAlpha(0.18f), 10);
                Circle(new V2(b.X, b.Y), 3.5f, new Rgba(0.85f, 0.97f, 1f), 8);
            }
            var ship = new V2(rockSx, rockSy);
            V2 tip = ship + V2.FromAngle(rockAng) * 22f, l = ship + V2.FromAngle(rockAng + 2.55f) * 18f, rr = ship + V2.FromAngle(rockAng - 2.55f) * 18f, tail = ship + V2.FromAngle(rockAng + Mathf.PI) * 9f;
            var hull = new Rgba(0.8f, 0.95f, 1f);
            Line(tip, l, ThumbLine(2.5f), hull);
            Line(l, tail, ThumbLine(2.5f), hull);
            Line(tail, rr, ThumbLine(2.5f), hull);
            Line(rr, tip, ThumbLine(2.5f), hull);
            if (rockMoving && menuBgAnimate)
            {
                var back = rockAng + Mathf.PI;
                float fl = 20f + 8f * Hash((int)(t * 30f));
                Line(ship + V2.FromAngle(back + 0.3f) * 12f, ship + V2.FromAngle(back) * fl, 2.5f, new Rgba(0.55f, 0.8f, 1f, 0.8f));
                Line(ship + V2.FromAngle(back - 0.3f) * 12f, ship + V2.FromAngle(back) * fl, 2.5f, new Rgba(0.55f, 0.8f, 1f, 0.8f));
            }
        }

        // --- Life Grid: Conway's Life in phosphor, seeded where you go ------------------------------

        const int LifeW = 80, LifeH = 45;
        const float LifeCell = 16f;
        readonly byte[] lifeA = new byte[LifeW * LifeH], lifeB = new byte[LifeW * LifeH];
        readonly float[] lifeG = new float[LifeW * LifeH];  // how lit each cell still is: 1 while alive, then fading
        byte[] lifeNow, lifeNext;
        float lifeAcc;
        int lifeFrame = -1;

        void LifeBlob(int gx, int gy, int r, float chance)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r && ReactRand() < chance)
                        lifeNow[((gy + y + LifeH) % LifeH) * LifeW + (gx + x + LifeW) % LifeW] = 1;
        }

        void LifeGlider(int gx, int gy)
        {
            int sx = ReactRand() < 0.5f ? 1 : -1, sy = ReactRand() < 0.5f ? 1 : -1;
            int[] cells = { 1, 0, 2, 1, 0, 2, 1, 2, 2, 2 };
            for (int i = 0; i < cells.Length; i += 2)
                lifeNow[((gy + cells[i + 1] * sy + LifeH) % LifeH) * LifeW + (gx + cells[i] * sx + LifeW) % LifeW] = 1;
        }

        void LifeStep(float dt)
        {
            var a = lifeNow;
            var b = lifeNext;
            int pop = 0;
            for (int y = 0; y < LifeH; y++)
            {
                int ym = ((y + LifeH - 1) % LifeH) * LifeW, y0 = y * LifeW, yp = ((y + 1) % LifeH) * LifeW;
                for (int x = 0; x < LifeW; x++)
                {
                    int xm = (x + LifeW - 1) % LifeW, xp = (x + 1) % LifeW;
                    int n = a[ym + xm] + a[ym + x] + a[ym + xp] + a[y0 + xm] + a[y0 + xp] + a[yp + xm] + a[yp + x] + a[yp + xp];
                    byte v = (byte)(a[y0 + x] == 1 ? (n == 2 || n == 3 ? 1 : 0) : (n == 3 ? 1 : 0));
                    b[y0 + x] = v;
                    pop += v;
                }
            }
            lifeNow = b;
            lifeNext = a;
            if (pop < 160)
            {
                LifeBlob((int)(ReactRand() * LifeW), (int)(ReactRand() * LifeH), 5, 0.45f);
                LifeGlider((int)(ReactRand() * LifeW), (int)(ReactRand() * LifeH));
            }
        }

        void BgLifeGrid(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.03f, 0.02f, 0.01f));
            if (lifeNow == null)
            {
                lifeNow = lifeA;
                lifeNext = lifeB;
                for (int i = 0; i < lifeNow.Length; i++)
                    lifeNow[i] = (byte)(ReactRand() < 0.2f ? 1 : 0);
            }
            if (ReactStepOnce(ref lifeFrame, out float dt))
            {
                if (ReactPointer(out var p) && ReactRand() < dt * 9f)
                    LifeBlob((int)(p.X / LifeCell), (int)(p.Y / LifeCell), 2, 0.6f);
                if (ReactClick(out var c))
                {
                    int gx = (int)(c.X / LifeCell), gy = (int)(c.Y / LifeCell);
                    for (int i = 0; i < 3; i++)
                        LifeGlider(gx + (int)ReactRange(-5f, 5f), gy + (int)ReactRange(-5f, 5f));
                    LifeBlob(gx, gy, 5, 0.5f);
                }
                lifeAcc += dt;
                float iv = Mathf.Max(0.035f, 0.11f - 0.05f * ReactEnergy);
                while (lifeAcc > iv)
                {
                    lifeAcc -= iv;
                    LifeStep(dt);
                }
                float fall = Mathf.Exp(-4.5f * dt);
                for (int i = 0; i < lifeG.Length; i++)
                    lifeG[i] = lifeNow[i] == 1 ? 1f : lifeG[i] * fall;
            }
            for (int i = 0; i < lifeG.Length; i++)
            {
                float g = lifeG[i];
                if (g < 0.05f)
                    continue;
                var col = lifeNow[i] == 1 ? new Rgba(1f, 0.84f, 0.5f, 0.95f) : new Rgba(1f, 0.62f, 0.2f, 0.12f + 0.6f * g);
                FillRect((i % LifeW) * LifeCell + 1f, (i / LifeW) * LifeCell + 1f, LifeCell - 3f, LifeCell - 3f, col);
            }
        }

        // --- Phosphor Pond: a dot grid that ripples where you go and click -----------------------------

        const int PondW = 64, PondH = 36;
        const float PondCell = 20f;
        readonly float[] pondH = new float[PondW * PondH], pondV = new float[PondW * PondH];
        int pondFrame = -1;

        void PondPoke(int gx, int gy, float amp, int r)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int X = gx + x, Y = gy + y;
                    float f = 1f - Mathf.Sqrt(x * x + y * y) / (r + 0.5f);
                    if (X >= 1 && Y >= 1 && X < PondW - 1 && Y < PondH - 1 && f > 0f)
                        pondH[Y * PondW + X] += amp * f;
                }
        }

        void BgPhosphorPond(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.01f, 0.02f, 0.012f));
            if (ReactStepOnce(ref pondFrame, out float dt))
            {
                if (ReactPointer(out var p))
                    PondPoke((int)(p.X / PondCell), (int)(p.Y / PondCell), 0.22f + 0.2f * ReactEnergy, 2);
                if (ReactClick(out var c))
                    PondPoke((int)(c.X / PondCell), (int)(c.Y / PondCell), 2.4f, 6);
                if (ReactRand() < dt * 0.9f)
                    PondPoke(5 + (int)(ReactRand() * (PondW - 10)), 4 + (int)(ReactRand() * (PondH - 8)), 1.6f, 3);
                for (int step = 0; step < 2; step++)
                {
                    for (int y = 1; y < PondH - 1; y++)
                        for (int x = 1; x < PondW - 1; x++)
                        {
                            int i = y * PondW + x;
                            pondV[i] += ((pondH[i - 1] + pondH[i + 1] + pondH[i - PondW] + pondH[i + PondW]) * 0.25f - pondH[i]) * 0.9f;
                        }
                    for (int i = 0; i < pondH.Length; i++)
                    {
                        pondV[i] *= 0.988f;
                        pondH[i] += pondV[i];
                    }
                }
            }
            for (int y = 0; y < PondH; y++)
                for (int x = 0; x < PondW; x++)
                {
                    float v = pondH[y * PondW + x], a = Mathf.Min(1f, Mathf.Abs(v) * 1.2f);
                    var col = new Rgba(0.2f + 0.7f * a, 0.9f + 0.1f * a, 0.45f + 0.5f * a, 0.3f + 0.7f * a);
                    float size = v > 0.35f ? 8f : 5f;
                    FillRect(x * PondCell + PondCell / 2f - size / 2f, y * PondCell + PondCell / 2f - size / 2f - Mathf.Clamp(v, -1.5f, 1.5f) * 5f, size, size, col);
                }
        }

        // --- Vector Tunnel: neon gates rushing past, steered by the pointer -----------------------------

        float tunZ, tunBoost, tunLit, tunVx = W / 2f, tunVy = H / 2f;
        int tunN, tunFrame = -1;

        void BgVectorTunnel(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.01f, 0.012f, 0.03f));
            var ink = new Rgba(0.85f, 0.45f, 1f);
            if (ReactStepOnce(ref tunFrame, out float dt))
            {
                tunBoost *= Mathf.Exp(-1.8f * dt);
                tunLit *= Mathf.Exp(-1.4f * dt);
                if (ReactClick(out var c))
                {
                    tunBoost = 1.3f;
                    tunLit = 1f;
                }
                V2 target = ReactPointer(out var p) ? p : new V2(W / 2f + Mathf.Cos(t * 0.4f) * W * 0.12f, H / 2f + Mathf.Sin(t * 0.55f) * H * 0.12f);
                tunVx += ((W / 2f + (target.X - W / 2f) * 0.45f) - tunVx) * Mathf.Min(1f, dt * 4f);
                tunVy += ((H / 2f + (target.Y - H / 2f) * 0.45f) - tunVy) * Mathf.Min(1f, dt * 4f);
                tunZ += dt * (0.55f + tunBoost * 1.8f + ReactEnergy * 0.6f);
                while (tunZ >= 1f)
                {
                    tunZ -= 1f;
                    tunN++;
                }
            }
            const int N = 14;
            var vp = new V2(tunVx, tunVy);
            for (int i = 0; i < N; i++)
            {
                float f = (i + tunZ) / N, size = 24f + Mathf.Pow(f, 2.3f) * W * 1.7f, hw = size / 2f, hh = size * 0.5625f / 2f;
                bool gate = ((i - tunN) % 5 + 5) % 5 == 0;
                float a = Mathf.Clamp01(0.2f + f * 0.8f + tunLit * 0.3f) * (gate ? 1f : 0.7f), w = gate ? 4f : 2f;
                var col = gate || tunLit > 0.3f ? new Rgba(0.95f, 0.75f, 1f, a) : ink.WithAlpha(a);
                var tl = new V2(vp.X - hw, vp.Y - hh); var tr = new V2(vp.X + hw, vp.Y - hh); var br = new V2(vp.X + hw, vp.Y + hh); var bl = new V2(vp.X - hw, vp.Y + hh);
                Line(tl, tr, ThumbLine(w), col); Line(tr, br, ThumbLine(w), col); Line(br, bl, ThumbLine(w), col); Line(bl, tl, ThumbLine(w), col);
                if (gate)
                {
                    Line(tl, tr, w + 8f, col.WithAlpha(a * 0.12f)); Line(tr, br, w + 8f, col.WithAlpha(a * 0.12f)); Line(br, bl, w + 8f, col.WithAlpha(a * 0.12f)); Line(bl, tl, w + 8f, col.WithAlpha(a * 0.12f));
                }
            }
            float[] dx = { -1f, 1f, -1f, 1f, 0f, 0f, -1f, 1f }, dy = { -1f, -1f, 1f, 1f, -1f, 1f, 0f, 0f };
            for (int k = 0; k < 8; k++)
                Line(vp, vp + new V2(dx[k] * W * 0.95f, dy[k] * W * 0.95f * 0.5625f), ThumbLine(1.5f), ink.WithAlpha(0.35f));
            Line(vp - new V2(10f, 0f), vp + new V2(10f, 0f), ThumbLine(2f), new Rgba(0.95f, 0.8f, 1f, 0.9f));
            Line(vp - new V2(0f, 8f), vp + new V2(0f, 8f), ThumbLine(2f), new Rgba(0.95f, 0.8f, 1f, 0.9f));
        }

        // --- Signal Lost: a phosphor test card that tears where you point and rolls when you click -------

        float sigRoll, sigRv, sigMsg;
        int sigFrame = -1;
        struct SigBlock { public float Y, Height, Slip, Life; }  // a band of rows that slips sideways for a moment
        readonly List<SigBlock> sigBlocks = new List<SigBlock>();

        void BgSignalLost(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0f, 0f, 0f));
            if (ReactStepOnce(ref sigFrame, out float dt))
            {
                sigRoll = (sigRoll + sigRv * dt) * Mathf.Exp(-2.2f * dt);
                sigRv *= Mathf.Exp(-3.2f * dt);
                sigMsg = Mathf.Max(0f, sigMsg - dt);
                if (ReactClick(out var c))
                {
                    sigRv = (ReactRand() < 0.5f ? -1f : 1f) * 1700f;
                    sigMsg = 1.4f;
                    for (int i = 0; i < 5; i++)
                        sigBlocks.Add(new SigBlock { Y = ReactRand() * H, Height = ReactRange(30f, 100f), Slip = ReactRange(-200f, 200f), Life = ReactRange(0.15f, 0.4f) });
                }
                for (int i = sigBlocks.Count - 1; i >= 0; i--)
                {
                    var b = sigBlocks[i];
                    b.Life -= dt;
                    if (b.Life <= 0f)
                        sigBlocks.RemoveAt(i);
                    else
                        sigBlocks[i] = b;
                }
            }
            float g = 0.25f + ReactEnergy * 0.9f;
            bool has = ReactPointer(out var p);
            float[] lv = { 0.92f, 0.7f, 0.54f, 0.4f, 0.28f, 0.18f, 0.09f };
            int rows = (int)(H / 8f);
            for (int r = 0; r < rows; r++)
            {
                float y = r * 8f, src = MathX.Fmod(y - sigRoll + H * 4f, H);
                float dx = (Hash(r * 7 + (int)(t * 24f)) - 0.5f) * 24f * g + Mathf.Sin(y * 0.09f + t * 2f) * 6f;
                if (has || menuBgAnimate)
                    dx += 100f * Mathf.Exp(-Mathf.Pow((y - (has ? p.Y : H / 2f + Mathf.Sin(t * 0.7f) * H * 0.3f)) / 44f, 2f)) * Mathf.Sin(y * 0.15f + t * 26f);
                foreach (var b in sigBlocks)
                    if (y >= b.Y && y < b.Y + b.Height)
                        dx += b.Slip;
                for (int k = 0; k < 7; k++)
                {
                    float l = src < H * 0.7f ? lv[k] : src < H * 0.8f ? lv[6 - k] * 0.8f : (k % 2 == 0 ? 0.06f : 0.16f);
                    FillRect(k * (W / 7f) + dx, y, W / 7f + 1f, 8f, new Rgba(l, l * 0.78f, l * 0.35f, 0.4f));
                }
            }
            var ring = new Rgba(0f, 0f, 0f, 0.9f);
            var cc = new V2(W / 2f, MathX.Fmod(H * 0.38f + sigRoll, H));
            Arc(cc, 130f, 5f, ring, 48);
            Line(cc - new V2(170f, 0f), cc + new V2(170f, 0f), 5f, ring);
            Line(cc - new V2(0f, 170f), cc + new V2(0f, 170f), 5f, ring);
            for (int i = 0; i < 30 + (int)(g * 200f); i++)
                FillRect(ReactRand() * W, ReactRand() * H, 2f + ReactRand() * 4f, 3f, new Rgba(1f, 0.85f, 0.55f, 0.7f));
            var txt = new Rgba(1f, 0.82f, 0.5f);
            if (sigMsg > 0f || (int)(t * 1.6f) % 2 == 0)
            {
                string m = sigMsg > 0f ? "TUNING" : "NO SIGNAL";
                float wd = m.Length * BootAdv * 1.8f;
                FillRect(W / 2f - wd / 2f - 16f, H * 0.38f - 34f, wd + 26f, 68f, new Rgba(0f, 0f, 0f, 0.75f));
                BootText(m, W / 2f - wd / 2f, H * 0.38f - 24f, txt, 1.8f);
            }
            int fr = (int)(t * 25f);
            BootText("CH 03   " + (fr / 1500).ToString("00") + ":" + (fr / 25 % 60).ToString("00") + ":" + (fr % 25).ToString("00"), 40f, 30f, txt);
        }
    }
}
