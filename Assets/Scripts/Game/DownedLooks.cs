using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Going down and getting back up (Docs/Mockups/crt-downed-revived.html: option B for the message, D, E and F for the bodies).
    /// All of it is drawn on this screen only; nothing is sent over the network (Player.Downed and Player.Revive were already replicated).
    ///   tracker   UpdateDownedLooks notices each player going down or standing back up and who is standing by them
    ///   D         the real model jitters, splits into red and cyan copies and flickers out while flecks of its own costume colours
    ///             shed and fall; what stays is the downed disc, dim red, with an X and a slow ping (DrawDownedBody, DrawHuskMark)
    ///   E         flecks fly back in, the model returns mint-tinted with a reverse split under a scan line, then a shockwave (DrawRebuild)
    ///   F         a 36-segment ring charges round the husk and a data link runs to whoever is reviving it (DrawReviveRing)
    ///   B         the red DOWNED scan band, with a vitals trace behind the word, and the mint REVIVED band (DrawDownedBand)
    /// Visual effects off draws the old grey disc and green arc; Reduced effects keeps the fades, flecks and ring but drops the
    /// split copies, flicker, jitter and glow. The model is tinted through mTintA, the same mix the hit flash uses (CharacterModels.cs).
    public partial class ArenaApp
    {
        const float DownShedTime = 1.3f, RebuildTime = 1.2f, RebuildTail = 1.35f;  // the shed; the rebuild and its shockwave tail (a multiple of RebuildTime)
        const float BandInDelay = 0.5f, BandInTime = 0.38f, ReviveBandFrom = 0.25f, ReviveBandHold = 2.2f, BandOutTime = 0.25f;

        static readonly Rgba DownRed = new Rgba(1f, 0.42f, 0.48f), DownRedDim = new Rgba(0.66f, 0.25f, 0.3f), DownRedHi = new Rgba(1f, 0.84f, 0.85f);
        static readonly Rgba DownCyan = new Rgba(0.35f, 0.85f, 0.91f), ReviveMint = new Rgba(0.43f, 1f, 0.72f), ReviveMintHi = new Rgba(0.88f, 1f, 0.94f);

        class DownSeen
        {
            public bool Seen, Downed;
            public float DownAt = -99f, UpAt = -99f;  // -99: it was already so when we first saw them (no show)
            public float LastRevive;
            public int Rescuer;                       // who stood by them last (client guess: the nearest standing player)
            public float RescuerAt = -99f;
        }

        struct Fleck { public float X, Y, S1, S2, S3, Sz; public int Ci; }

        readonly Dictionary<int, DownSeen> downSeen = new Dictionary<int, DownSeen>();
        Game downGame;
        static readonly Fleck[] Flecks = MakeFlecks();

        static Fleck[] MakeFlecks()
        {
            var rnd = new System.Random(11);
            var a = new Fleck[110];
            for (int i = 0; i < a.Length; i++)
            {
                float ang = (float)rnd.NextDouble() * MathX.Tau, d = Mathf.Sqrt((float)rnd.NextDouble());
                a[i] = new Fleck { X = Mathf.Cos(ang) * d, Y = Mathf.Sin(ang) * d, S1 = (float)rnd.NextDouble(), S2 = (float)rnd.NextDouble(), S3 = (float)rnd.NextDouble(), Ci = rnd.Next(5), Sz = (float)rnd.NextDouble() };
            }
            return a;
        }

        static float DHash(float n)
        {
            float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        // --- The tracker -----------------------------------------------------------------------------------------------

        /// Called each frame at the start of DrawWorld: who went down or got back up since the last frame, and who is reviving whom.
        void UpdateDownedLooks()
        {
            if (downGame != game)
            {
                downSeen.Clear();
                downGame = game;
            }
            float now = Time.unscaledTime;
            foreach (var p in game.Players.Values)
            {
                DownSeen s;
                if (!downSeen.TryGetValue(p.Id, out s))
                    downSeen[p.Id] = s = new DownSeen();
                if (!s.Seen)
                {
                    s.Seen = true;
                    s.Downed = p.Downed;
                    s.LastRevive = p.Revive;
                    continue;
                }
                if (p.Downed && !s.Downed)
                    s.DownAt = now;
                else if (!p.Downed && s.Downed)
                    s.UpAt = now;
                s.Downed = p.Downed;
                if (p.Downed && p.Revive > s.LastRevive + 0.0005f)
                {
                    Player near = null;
                    float best = 130f * 130f;
                    foreach (var q in game.Players.Values)
                        if (q != p && !q.Downed && q.Disp.DistanceSq(p.Disp) < best)
                        {
                            best = q.Disp.DistanceSq(p.Disp);
                            near = q;
                        }
                    if (near != null)
                    {
                        s.Rescuer = near.Id;
                        s.RescuerAt = now;
                    }
                }
                s.LastRevive = p.Revive;
            }
        }

        DownSeen DownStateOf(Player p)
        {
            DownSeen s;
            return downGame == game && fxOn && downSeen.TryGetValue(p.Id, out s) && s.Seen ? s : null;
        }

        Player RescuerOf(DownSeen s)
        {
            Player q;
            return s != null && Time.unscaledTime - s.RescuerAt < 0.35f && game.Players.TryGetValue(s.Rescuer, out q) && !q.Downed ? q : null;
        }

        // --- The model with a tint, alpha and offset ---------------------------------------------------------------------

        /// The real model drawn mixed toward `tint` (tintA 0 = as is, 1 = a flat silhouette), at `alpha`, shifted by `off`.
        void DrawModelTinted(Player p, float r, float t, V2 off, float alpha, Rgba tint, float tintA)
        {
            V2 keep = p.Disp;
            p.Disp += off;
            mTintCol = tint;
            mTintA = tintA;
            DrawPlayerModel(p, r, t, new Rgba(1f, 1f, 1f, alpha));
            mTintA = 0f;
            p.Disp = keep;
        }

        Rgba FleckColor(Player p, int ci)
        {
            int kit = Mathf.Clamp((int)p.Kit, 0, ModelPals.Length - 1);
            ModelPal K = ModelPals[kit];
            switch (ci)
            {
                case 0: return K.Base;
                case 1: return K.Sleeve;
                case 2: return K.Dark;
                case 3: return K.Glove;
                default: return p.Def.Color;
            }
        }

        void FleckQuad(V2 at, float size, Rgba c)
        {
            if (c.A > 0.02f)
                FillRect(at.X - size / 2f, at.Y - size / 2f, size, size, c);
        }

        // --- D: going down ------------------------------------------------------------------------------------------------

        /// 0-1 through the shed, and how visible the husk is by then.
        float DownShed(DownSeen s) => Mathf.Clamp01((Time.unscaledTime - s.DownAt) / DownShedTime);

        float HuskAlpha(DownSeen s) => Mathf.Clamp01((DownShed(s) - 0.35f) / 0.4f);

        /// The downed body: the footprint fading, the model glitching out, flecks shedding, the husk coming up.
        void DrawDownedBody(Player p, float r, float t, DownSeen s)
        {
            float k = DownShed(s);
            bool calm = fxReduced;
            V2 c = p.Disp;
            Rgba role = p.Def.Color;
            int step = Mathf.FloorToInt(t * 30f);
            if (k < 1f)
            {
                float fa = 1f - Mathf.Clamp01(k * 3f);
                Circle(c, r, role.WithAlpha(0.14f * fa), 28);
                Arc(c, r, 2.2f, role.WithAlpha(0.6f * fa), 28);
            }
            DrawHusk(c, r, t, HuskAlpha(s));
            if (k >= 0.65f + 0.35f)
                return;
            float fade = Mathf.Clamp01((k - 0.15f) / 0.5f), red = Mathf.Clamp01(k * 2.5f);
            float ma = 1f - fade;
            if (!calm && DHash(step * 3.3f) > 0.75f - k * 0.4f)
                ma *= 0.45f;
            float jx = !calm && k < 0.6f ? (DHash(step) - 0.5f) * r * 0.14f : 0f;
            if (!calm && k < 0.5f)
            {
                float d = r * 0.25f * (1f - k * 2f);
                DrawModelTinted(p, r, t, new V2(d, 0f), 0.4f, DownRed, 1f);
                DrawModelTinted(p, r, t, new V2(-d, 0f), 0.34f, DownCyan, 1f);
            }
            if (ma > 0.02f)
                DrawModelTinted(p, r, t, new V2(jx, 0f), ma, DownRed, red * 0.7f);
            for (int i = 0; i < Flecks.Length; i++)
            {
                var f = Flecks[i];
                float td = 0.12f + 0.55f * f.S1;
                if (k <= td)
                    continue;
                float u = (k - td) / (1f - td);
                var at = new V2(c.X + f.X * r * 0.9f + (f.S2 - 0.5f) * r * 1.8f * u, c.Y + f.Y * r * 0.9f + u * u * r * 1.5f + f.S3 * r * 0.9f * u);
                FleckQuad(at, r * 0.14f * (0.6f + f.Sz * 0.8f) + 0.8f, FleckColor(p, f.Ci).Mix(DownRedDim, u * 0.7f).WithAlpha(1f - Mathf.Clamp01(u * 1.1f)));
            }
        }

        /// The downed disc, restyled: dark with a dim red rim, and a slow ping ring out from it.
        void DrawHusk(V2 c, float r, float t, float a)
        {
            if (a <= 0.01f)
                return;
            Circle(c, r, new Rgba(0.12f, 0.08f, 0.1f, 0.92f * a), 28);
            float step = Mathf.Floor(t * 14f);
            Arc(c, r, 3f, DownRedDim.WithAlpha((DHash(step * 3f) > 0.9f ? 0.35f : 0.9f) * a), 28);
            float u = Mathf.Repeat(t, 2.4f) / 2.4f;
            Arc(c, r * (1.05f + u * 0.9f), 2f, DownRed.WithAlpha(0.35f * (1f - u) * a), 40);
        }

        /// The X over the hit marker (the marker is drawn last, so this goes after it).
        void DrawHuskMark(Player p, DownSeen s)
        {
            float a = HuskAlpha(s);
            if (a <= 0.01f)
                return;
            float m = Mathf.Max(p.Def.HitR * 0.55f, p.Def.BodyR * 0.3f);
            V2 c = p.Disp;
            Rgba x = DownRed.WithAlpha(0.95f * a);
            Line(c + new V2(-m, -m), c + new V2(m, m), 2.4f, x);
            Line(c + new V2(m, -m), c + new V2(-m, m), 2.4f, x);
        }

        // --- E: getting back up --------------------------------------------------------------------------------------------

        /// 0-RebuildTail while they come back (then it's the normal model); -1 when it isn't running.
        float RebuildK(DownSeen s)
        {
            if (s == null || s.UpAt < 0f)
                return -1f;
            float k = (Time.unscaledTime - s.UpAt) / RebuildTime;
            return k < RebuildTail ? k : -1f;
        }

        void DrawRebuild(Player p, float r, float t, float baseAlpha, float k)
        {
            bool calm = fxReduced;
            V2 c = p.Disp;
            int step = Mathf.FloorToInt(t * 30f);
            float m = Mathf.Clamp01((k - 0.35f) / 0.4f), sweep = Mathf.Clamp01((k - 0.3f) / 0.5f);
            if (k < 0.3f)
                DrawHusk(c, r, t, 1f - k / 0.3f);
            GlowDisc(c, r * 2.2f, ReviveMint, 0.2f * Mathf.Clamp01(k * 2f));
            for (int i = 0; i < Flecks.Length; i++)
            {
                var f = Flecks[i];
                float a0 = 0.05f + 0.5f * f.S1, u = Mathf.Clamp01((k - a0) / 0.32f);
                if (u <= 0f)
                    continue;
                float e = 1f - (1f - u) * (1f - u) * (1f - u), ang = f.S2 * MathX.Tau, dist = r * (1.2f + f.S3 * 2.4f);
                var home = new V2(c.X + f.X * r * 0.9f, c.Y + f.Y * r * 0.9f);
                var at = new V2(home.X + Mathf.Cos(ang) * dist * (1f - e), home.Y + Mathf.Sin(ang) * dist * (1f - e) - (1f - e) * r * 0.4f);
                float keep = 1f - Mathf.Clamp01((k - 0.55f) / 0.25f), size = r * 0.14f * (0.6f + f.Sz * 0.8f) + 0.8f;
                if (u < 1f && !calm)
                    FleckQuad(new V2(home.X + (at.X - home.X) * 0.6f, home.Y + (at.Y - home.Y) * 0.6f), size * 0.7f, ReviveMint.WithAlpha(0.2f));
                FleckQuad(at, size, FleckColor(p, f.Ci).Mix(ReviveMint, 0.7f * (1f - u)).WithAlpha((u < 1f ? 0.4f + 0.6f * u : 1f) * keep));
            }
            if (m > 0f)
            {
                float e = 1f - (1f - m) * (1f - m) * (1f - m), ma = baseAlpha * e;
                if (m < 1f && !calm && DHash(step * 2.1f) > 0.7f)
                    ma *= 0.5f;
                if (m < 0.9f && !calm)
                {
                    float d = r * 0.2f * (1f - m);
                    DrawModelTinted(p, r, t, new V2(d, 0f), 0.35f * baseAlpha * m, ReviveMint, 1f);
                    DrawModelTinted(p, r, t, new V2(-d, 0f), 0.3f * baseAlpha * m, DownCyan, 1f);
                }
                DrawModelTinted(p, r, t, V2.Zero, ma, ReviveMint, (1f - Mathf.Clamp01((k - 0.4f) / 0.5f)) * 0.85f);
            }
            if (k > 0.3f && k < 0.85f)
            {
                float y = c.Y + Mathf.Lerp(-r, r, sweep);
                FillRect(c.X - r * 1.3f, y - r * 0.5f, r * 2.6f, r * 0.5f, ReviveMint.WithAlpha(0.14f));
                Line(new V2(c.X - r * 1.3f, y), new V2(c.X + r * 1.3f, y), 1.6f, ReviveMintHi.WithAlpha(0.95f));
            }
            float v = (k - 0.78f) / 0.45f;
            if (v > 0f && v < 1f)
            {
                Arc(c, r * (1.1f + v * 3.4f), 2f + 4f * (1f - v), ReviveMint.WithAlpha(1f - v), 48);
                Arc(c, r * (0.9f + v * 2f), 1.4f, ReviveMintHi.WithAlpha(0.7f * (1f - v)), 40);
                GlowDisc(c, r * 2.4f, ReviveMint, 0.45f * (1f - v) * (1f - v));
                for (int i = 0; i < 14; i++)
                {
                    float a = DHash(i * 3.1f) * MathX.Tau, d = r * (0.9f + v * (1.4f + DHash(i) * 1.8f)), s = r * 0.07f * (1f - v * 0.5f) + 1.2f;
                    var at = new V2(c.X + Mathf.Cos(a) * d, c.Y + Mathf.Sin(a) * d);
                    Rgba pc = ReviveMintHi.WithAlpha(1f - v);
                    Line(at + new V2(-s, 0f), at + new V2(s, 0f), 1.2f, pc);
                    Line(at + new V2(0f, -s), at + new V2(0f, s), 1.2f, pc);
                }
            }
        }

        // --- F: the revive ring and link ------------------------------------------------------------------------------------

        /// A 36-segment ring charges round the downed player (the old plain green arc when effects are off), and a dashed link with
        /// moving packets runs from whoever is standing by them.
        void DrawReviveRing(Player p, float r, float t, DownSeen s)
        {
            float prog = Mathf.Clamp01(Mathf.Max(p.Revive, 0.01f));
            V2 c = p.Disp;
            float R = r * 1.5f + 8f;
            const int N = 36;
            for (int i = 0; i < N; i++)
            {
                float a0 = -MathX.Pi / 2f + (float)i / N * MathX.Tau + 0.025f;
                bool lit = (i + 0.5f) / N <= prog;
                Arc(c, R, 3.4f, lit ? ReviveMint.WithAlpha(0.95f) : DownRedDim.WithAlpha(0.4f), 4, a0, MathX.Tau / N - 0.05f);
            }
            if (prog < 1f)
            {
                float ha = -MathX.Pi / 2f + prog * MathX.Tau;
                Circle(c + V2.FromAngle(ha) * R, 3.2f, ReviveMintHi.WithAlpha(0.9f), 8);
            }
            if (!fxReduced)
                for (int i = 0; i < 28; i++)
                    Arc(c, R + 7f, 1.3f, DownRed.WithAlpha(0.45f), 2, (float)i / 28 * MathX.Tau + t * 0.3f, MathX.Tau / 28f * 0.25f);
            var q = RescuerOf(s);
            if (q == null)
                return;
            V2 dir = q.Disp - c;
            float len = dir.Length;
            if (len < 1f)
                return;
            dir = dir * (1f / len);
            V2 a = q.Disp - dir * q.Def.BodyR, b = c + dir * (r + 3f);
            float span = (a - b).Length, dash = 4f, gap = 5.6f, off = Mathf.Repeat(t * 36f, dash + gap);
            for (float d = -off; d < span; d += dash + gap)
            {
                float d0 = Mathf.Max(0f, d), d1 = Mathf.Min(span, d + dash);
                if (d1 > d0)
                    Line(b + (a - b) * (d0 / span), b + (a - b) * (d1 / span), 1.6f, ReviveMint.WithAlpha(0.85f));
            }
            for (int i = 0; i < 3; i++)
            {
                float u = Mathf.Repeat(t * 0.9f + i / 3f, 1f);
                FleckQuad(a + (b - a) * u, 3.2f, ReviveMintHi);
            }
        }

        // --- B: the band ----------------------------------------------------------------------------------------------------

        static float Beat(float u)
        {
            u -= Mathf.Floor(u);
            return 0.12f * Gauss(u, 0.12f, 0.035f) - 0.14f * Gauss(u, 0.27f, 0.012f) + Gauss(u, 0.31f, 0.011f) - 0.28f * Gauss(u, 0.355f, 0.013f) + 0.26f * Gauss(u, 0.6f, 0.05f);
        }

        static float Gauss(float u, float m, float s) => Mathf.Exp(-((u - m) * (u - m)) / (2f * s * s));

        /// The vitals trace behind the word: columns of light, brighter toward the right end.
        void DrawVitals(float cy, Color ink, float amp, float speed, float noise, float t, float alpha)
        {
            const float step = 4f, half = 36f;
            float prev = cy;
            for (float x = 0f; x < W; x += step)
            {
                float u = x / W, v = amp * Beat(u * 3f - t * speed) + noise * (DHash(x * 0.37f + Mathf.Floor(t * 18f) * 7f) - 0.5f);
                float y = cy - v * half, lo = Mathf.Min(prev, y), h = Mathf.Max(3f, Mathf.Abs(y - prev) + 2f);
                Ui.Fill(new Rect(x, lo - 1f, step, h), new Color(ink.r, ink.g, ink.b, alpha * Mathf.Lerp(0.15f, 1f, u)));
                prev = y;
            }
        }

        /// Your own DOWNED band (red, vitals dying to a flatline, a revive bar when an ally is on you) and, as you stand back
        /// up, the REVIVED band (a solid mint flash, then settled). Opens like the wave banners do (WaveBand.cs).
        void DrawDownedBand(Player me)
        {
            if (me == null || game.State != MatchState.Fighting)
                return;
            var s = DownStateOf(me);
            if (s == null)
                return;
            float now = Time.unscaledTime;
            bool calm = CabCalm;
            const float top = 118f;
            if (me.Downed)
            {
                if (game.Practice != null)
                    return;  // the Practice Range has its own DOWNED window with Respawn (PracticeDownedUi.cs)
                float age = s.DownAt < -50f ? 99f : now - s.DownAt - BandInDelay;
                float open = age / BandInTime;
                if (open <= 0f)
                    return;
                float prog = me.Revive, a = age - BandInTime;
                var q = RescuerOf(s);
                string by = q != null ? q.Name.ToUpperInvariant() : "AN ALLY";
                string sub = prog > 0.005f ? by + " IS REVIVING YOU" : "AN ALLY CAN REVIVE YOU";
                float amp = Mathf.Max(prog * 0.8f, Mathf.Clamp01(1f - a * 0.9f)), speed = 0.3f + prog * 0.5f, noise = prog <= 0.005f && a > 1.1f ? 0.05f : 0f;
                DrawScanBand("DOWNED", sub, me.Name.ToUpperInvariant() + " · NO PULSE", "HP 0", prog > 0.005f ? prog : -1f, DownRedUi, Mathf.Clamp01(open), false, top, rc =>
                {
                    DrawVitals(rc.center.y, DownRedUi, amp, speed, noise, now, 0.3f);
                    if (!calm && a < 0.6f)
                    {
                        float j = (DHash(Mathf.Floor(now * 30f)) - 0.5f) * 16f;
                        bool was = Ui.CrtFont;
                        Ui.CrtFont = true;
                        var tr = new Rect(0f, rc.y + 22f, W, 100f);
                        Ui.Label(new Rect(tr.x - 8f + j, tr.y, tr.width, tr.height), "DOWNED", 88, new Color(DownRedUi.r, DownRedUi.g, DownRedUi.b, 0.45f), TextAnchor.MiddleCenter, false);
                        Ui.Label(new Rect(tr.x + 8f + j, tr.y, tr.width, tr.height), "DOWNED", 88, new Color(DownCyanUi.r, DownCyanUi.g, DownCyanUi.b, 0.35f), TextAnchor.MiddleCenter, false);
                        Ui.CrtFont = was;
                    }
                }, new Color(DownRedHi.R, DownRedHi.G, DownRedHi.B), ReviveMintUi);
                return;
            }
            if (s.UpAt < 0f)
                return;
            float up = now - s.UpAt;
            if (s.DownAt > -50f && s.UpAt - s.DownAt > BandInDelay + BandInTime && up < BandOutTime)  // the red band closing as you stand
            {
                DrawScanBand("DOWNED", "", "", "", -1f, DownRedUi, 1f - up / BandOutTime, false, top, null, new Color(DownRedHi.R, DownRedHi.G, DownRedHi.B));
                return;
            }
            float inT = (up - ReviveBandFrom) / BandInTime, outT = (ReviveBandHold - up) / BandOutTime;
            float o = Mathf.Min(inT, outT);
            if (o <= 0f)
                return;
            bool flash = up - ReviveBandFrom - BandInTime < 0.35f && inT >= 1f;
            float a2 = up - ReviveBandFrom - BandInTime;
            string hp = "HP " + Mathf.RoundToInt(100f * me.Hp / Mathf.Max(1f, me.MaxHp)) + "%";
            Player rp, q2 = game.Players.TryGetValue(s.Rescuer, out rp) && now - s.RescuerAt < 1.5f ? rp : null;
            string sub2 = q2 != null && q2 != me ? "REVIVED BY " + q2.Name.ToUpperInvariant() + " · BACK ONLINE" : "BACK ONLINE";
            DrawScanBand("REVIVED", sub2, me.Name.ToUpperInvariant() + " · PULSE FOUND", hp, -1f, ReviveMintUi, Mathf.Clamp01(o), flash, top, rc =>
            {
                if (!flash)
                    DrawVitals(rc.center.y, ReviveMintUi, 0.9f, 0.9f, 0f, now, 0.3f);
            }, new Color(0.88f, 1f, 0.94f));
        }

        static Color DownRedUi => new Color(DownRed.R, DownRed.G, DownRed.B);
        static Color DownCyanUi => new Color(DownCyan.R, DownCyan.G, DownCyan.B);
        static Color ReviveMintUi => new Color(ReviveMint.R, ReviveMint.G, ReviveMint.B);
    }
}
