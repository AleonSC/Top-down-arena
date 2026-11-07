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
        const float BootRowH = 26f, BootTop = 84f, BootX = 70f, BootAdv = 16f;
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
        void BootText(string s, float x, float y, Rgba col)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                float gx = x + i * BootAdv;
                if (ch == ' ')
                    continue;
                if (ch == '.')
                {
                    FillRect(gx + 3f, y + 12f, 4f, 4f, col);
                    continue;
                }
                float hs = Hash(ch * 13 + 5);
                if (hs > 0.45f)
                    FillRect(gx, y, 3f, 15f, col);
                if (hs < 0.7f)
                    FillRect(gx + 7f, y, 3f, 15f, col);
                for (int r = 0; r < 3; r++)
                    if (Hash(ch * 13 + r) > 0.35f)
                        FillRect(gx, y + r * 6f, 10f, 3f, col);
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
                    FillRect(BootX - 14f, y - 5f, BootAdv * 38f, BootRowH - 3f, ink.WithAlpha(0.85f));
                    col = new Rgba(0.01f, 0.04f, 0.02f);
                }
                BootText(ln.Text, BootX, y, col);
            }
            FillRect(0, 0, W, 66f, new Rgba(0.01f, 0.03f, 0.02f));
            BootText("TOP-DOWN ARENA BIOS V1.07", BootX, 22f, ink.WithAlpha(0.9f));
            FillRect(BootX - 14f, 58f, BootAdv * 38f, 2f, ink.WithAlpha(0.6f));
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
                    Line(p0, p1, 2.5f, ink.WithAlpha(0.9f));
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
            Line(tip, l, 2.5f, hull);
            Line(l, tail, 2.5f, hull);
            Line(tail, rr, 2.5f, hull);
            Line(rr, tip, 2.5f, hull);
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
    }
}
