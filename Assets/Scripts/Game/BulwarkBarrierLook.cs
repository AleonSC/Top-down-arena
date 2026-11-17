using UnityEngine;

namespace Arena
{
    /// The Bulwark Barrier's look (Docs/Mockups/bastion-shield.html): a kite shield bent round your front half, blue enamel
    /// between a bright steel rim and a dark inner edge, a gold trim line, rivets and a gold boss at its middle that glows
    /// with Ram. Braced, a shimmering film of light runs along its face (gold at full Ram), and five Ram pips sit in an arc
    /// under the knight. Drawing only, from the replicated aim, BbBrace and BbRam and three effects:
    ///   FxKind.BulwarkStep: a chain step from its age (the shield cuts across or thrusts out; a crescent trails the cuts, the
    ///     Boss Slam throws shock arcs and a flash);
    ///   FxKind.BulwarkBash: the dash (afterimages, streaks, a bow wave), then the impact (rings, sparks, cracks in the floor);
    ///   FxKind.BulwarkBlock: a shot stopped on the plate (sparks and a ping), or soaked (a mote spiralling into the boss).
    public partial class ArenaApp
    {
        static readonly Rgba BbEnamel = new Rgba(0.11f, 0.23f, 0.43f), BbEnamelHi = new Rgba(0.2f, 0.37f, 0.66f), BbSteelHi = new Rgba(0.72f, 0.8f, 0.92f);
        static readonly Rgba BbRim = new Rgba(0.9f, 0.94f, 0.98f), BbInner = new Rgba(0.03f, 0.06f, 0.12f), BbTrim = new Rgba(0.87f, 0.71f, 0.38f);
        static readonly Rgba BbBossCol = new Rgba(0.86f, 0.66f, 0.27f), BbBossDark = new Rgba(0.43f, 0.29f, 0.08f), BbBossHi = new Rgba(1f, 0.96f, 0.81f);

        static float BbEaseIO(float x) { x = Mathf.Clamp01(x); return x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f; }
        static float BbEaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
        static float BbBack(float x) { x = Mathf.Clamp01(x); const float c = 1.9f; float u = x - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }
        static float BbHash(float x) { float s = Mathf.Sin(x * 12.9898f) * 43758.5453f; return s - Mathf.Floor(s); }

        /// Where the shield is now: its middle angle, its distance out, half its width and how thick it is.
        struct BbPose { public float Ang, Rad, Half, Th; public bool Braced; }

        /// The newest live effect of `kind` for player `id` (Radius / div is the id), or null.
        Effect BbLatest(FxKind kind, int id, int div)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
                if (fx.Kind == kind && fx.T < fx.Dur && Mathf.FloorToInt(fx.Radius / div + 0.001f) == id && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        BbPose BulwarkPose(Player p)
        {
            float r0 = Game.BulwarkRadius(p) - 4f;
            var o = new BbPose { Ang = p.Aim, Rad = r0, Half = p.BbBrace ? Game.BbBraceHalf : Game.BbGuardHalf, Th = p.BbBrace ? 12f : 9.5f, Braced = p.BbBrace };
            var bash = BbLatest(FxKind.BulwarkBash, p.Id, 1000);
            if (bash != null && bash.T < Game.BashTime(Mathf.Repeat(bash.Radius, 1000f)) + 0.08f)
            {
                o.Ang = (bash.Pos2 - bash.Pos).Angle;
                o.Rad = r0 + 2f;
                o.Half = Game.BbBraceHalf;
                o.Th = 12f;
                o.Braced = true;
                return o;
            }
            var st = BbLatest(FxKind.BulwarkStep, p.Id, 10);
            if (st == null)
                return o;
            int step = Mathf.Clamp(Mathf.RoundToInt(st.Radius) % 10, 1, 3);
            float dur = Game.BbDur[step - 1], u = Mathf.Clamp01(st.T / dur);
            if (u >= 1f)
                return o;
            float aim = (st.Pos2 - st.Pos).Angle;
            o.Half = Game.BbSwingHalf;
            o.Ang = aim;
            if (step < 3)
            {
                float e = BbEaseIO((u - 0.08f) / 0.62f);
                o.Ang = aim + (step == 1 ? Mathf.Lerp(-1.05f, 1.05f, e) : Mathf.Lerp(1.25f, -1.25f, e));
                o.Rad = r0 + (step == 1 ? 12f : 14f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u / 0.85f));
            }
            else if (u < 0.3f)
                o.Rad = Mathf.Lerp(r0, r0 - 7f, BbEaseOut(u / 0.3f));
            else if (u < 0.48f)
                o.Rad = Mathf.Lerp(r0 - 7f, r0 + 26f, BbBack((u - 0.3f) / 0.18f));
            else
                o.Rad = Mathf.Lerp(r0 + 26f, r0, BbEaseIO((u - 0.48f) / 0.52f));
            return o;
        }

        /// The shield itself at `c`, posed `o`; `ghost` > 0 draws a flat blue afterimage of that strength instead.
        void DrawBulwarkPlate(Player p, V2 c, BbPose o, float t, float ghost = 0f)
        {
            float a0 = o.Ang - o.Half, sw = 2f * o.Half, r = o.Rad, th = o.Th;
            const int N = 30;
            if (ghost > 0f)
            {
                Arc(c, r, th, Game.BbGlow.WithAlpha(0.3f * ghost), N, a0, sw);
                return;
            }
            Arc(c + new V2(2f, 3f), r, th + 2f, new Rgba(0f, 0f, 0f, 0.35f), N, a0, sw);  // its shadow on the floor
            V2 e0 = c + V2.FromAngle(a0) * r, e1 = c + V2.FromAngle(a0 + sw) * r;     // the rounded ends
            Circle(e0, th / 2f, BbEnamel, 10);
            Circle(e1, th / 2f, BbEnamel, 10);
            Arc(c, r, th, BbEnamel, N, a0, sw);
            Arc(c, r + th * 0.12f, th * 0.5f, BbEnamelHi, N, a0, sw);
            Arc(c, r + th * 0.36f, th * 0.28f, BbSteelHi, N, a0, sw);
            Arc(c, r + th / 2f - 0.8f, 1.6f, BbRim, N, a0, sw);
            Arc(c, r - th / 2f + 0.7f, 1.4f, BbInner, N, a0, sw);
            Arc(c, r + 0.5f, 1f, BbTrim.WithAlpha(0.85f), N / 2, a0 + 0.1f, o.Half - 0.3f);  // the gold trim, broken at the boss
            Arc(c, r + 0.5f, 1f, BbTrim.WithAlpha(0.85f), N / 2, o.Ang + 0.2f, o.Half - 0.3f);
            for (float a = -o.Half + 0.16f; a <= o.Half - 0.12f; a += 0.36f)  // rivets
                if (Mathf.Abs(a) > 0.2f)
                    Circle(c + V2.FromAngle(o.Ang + a) * (r + 0.5f), 1.15f, BbRim, 6);
            float k = o.Braced ? 1f : 0f, ram = Mathf.Clamp01(p.BbRam / Game.BbRamMax);
            bool full = p.BbRam >= Game.BbRamMax - 0.01f;
            Rgba film = full ? Game.BbGold : Game.BbGlow;
            if (k > 0f)  // braced: a film of light along the face, a bright mote running round it
            {
                Arc(c, r + th / 2f + 5f, 7f, film.WithAlpha(0.2f), N, a0, sw);
                for (int i = 0; i < 6; i++)
                {
                    float f = Mathf.Repeat(t * 0.9f + i / 6f, 1f);
                    Arc(c, r + th / 2f + 5f, 1.6f, film.WithAlpha(0.7f * Mathf.Sin(Mathf.PI * f)), 4, a0 + sw * f, 0.12f);
                }
                if (fxOn)
                    GlowDisc(c + V2.FromAngle(a0 + sw * Mathf.Repeat(t * 0.6f, 1f)) * (r + th / 2f + 5f), 14f, film, 0.45f);
            }
            V2 boss = c + V2.FromAngle(o.Ang) * (r + 1f);  // the boss
            float br = (o.Braced ? 6.6f : 5.4f) + (full ? 0.6f * Mathf.Sin(t * 10f) : 0f);
            Circle(boss, br + 1f, BbBossDark, 14);
            Circle(boss, br, BbBossCol, 14);
            Circle(boss + new V2(-1.4f, -1.4f), br * 0.45f, BbBossHi, 10);
            if (fxOn && ram > 0f)
                GlowDisc(boss, br * 3.4f, film, 0.7f * ram);
        }

        /// Five Ram pips in an arc under the knight: blue as they fill, all gold at full Ram.
        void DrawBulwarkPips(Player p)
        {
            if (p.BbRam <= 0f && !p.BbBrace)
                return;
            bool full = p.BbRam >= Game.BbRamMax - 0.01f;
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.PI / 2f + Mathf.Lerp(0.55f, -0.55f, i / 4f);
                V2 at = p.Disp + V2.FromAngle(a) * (p.Def.BodyR + 15f);
                float f = Mathf.Clamp01(p.BbRam / 20f - i);
                Circle(at, 3.6f, new Rgba(0.03f, 0.05f, 0.1f, 0.85f), 10);
                Arc(at, 3.6f, 1f, new Rgba(0.6f, 0.75f, 0.94f, 0.5f), 10);
                if (f > 0f)
                    Circle(at, 2.6f * f, full ? Game.BbGold : Game.BbGlow, 10);
            }
        }

        /// From the player loop: the shield held (posed by any step or bash playing), and the Ram pips.
        void DrawBulwarkHeld(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.BulwarkBarrier || p.Downed)
                return;
            DrawBulwarkPlate(p, p.Disp, BulwarkPose(p), t);
            DrawBulwarkPips(p);
        }

        /// A crescent band from `tail` to `head` at radius `r`, growing to `w` thick at its head (a cut's trail).
        void BbCrescent(V2 c, float r, float tail, float head, float w, Rgba col, float alpha)
        {
            const int N = 16;
            for (int i = 0; i < N; i++)
            {
                float u0 = i / (float)N, u1 = (i + 1) / (float)N;
                float b0 = Mathf.Lerp(tail, head, u0), b1 = Mathf.Lerp(tail, head, u1);
                float w0 = w * Mathf.Pow(u0, 1.3f), w1 = w * Mathf.Pow(u1, 1.3f);
                SetColor(col.WithAlpha(alpha * Mathf.Pow(u1, 1.6f)));
                Quad(c + V2.FromAngle(b0) * r, c + V2.FromAngle(b1) * r, c + V2.FromAngle(b1) * (r - w1), c + V2.FromAngle(b0) * (r - w0));
            }
            Arc(c, r, 2f, Rgba.White.WithAlpha(0.85f * alpha), N, Mathf.Min(tail, head), Mathf.Abs(head - tail));
        }

        /// FxKind.BulwarkStep: a cut's crescent trailing the rim, or the Boss Slam's shock arcs and flash.
        void DrawBulwarkStepFx(Effect fx)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(fx.Radius) % 10, 1, 3);
            float dur = Game.BbDur[step - 1], aim = (fx.Pos2 - fx.Pos).Angle, reach = (fx.Pos2 - fx.Pos).Length;
            Player owner;
            V2 c = game.Players.TryGetValue(Mathf.FloorToInt(fx.Radius / 10f + 0.001f), out owner) ? owner.Disp : fx.Pos;
            if (step < 3)
            {
                float u = Mathf.Clamp01(fx.T / dur), e = BbEaseIO((u - 0.08f) / 0.62f);
                float fade = fx.T > dur ? 1f - Mathf.Clamp01((fx.T - dur) / 0.2f) : 1f;
                float a0 = step == 1 ? -1.05f : 1.25f, a1 = -a0;
                float head = aim + Mathf.Lerp(a0, a1, e), span = (a1 - a0) * 0.6f * Mathf.Min(1f, e * 1.6f);
                if (Mathf.Abs(span) < 0.02f || fade <= 0f)
                    return;
                BbCrescent(c, reach - 4f, head - span, head, step == 1 ? 22f : 26f, Game.BbGlow, 0.55f * fade);
                return;
            }
            float s = (fx.T - dur * 0.3f) / 0.42f;  // the slam lands as the boss thrusts out
            if (s < 0f || s >= 1f)
                return;
            V2 at = c + V2.FromAngle(aim) * (Game.BulwarkRadius(owner ?? game.Me) + 26f);
            for (int i = 0; i < 3; i++)
            {
                float q = Mathf.Clamp01(s * 1.3f - i * 0.12f);
                if (q <= 0f || q >= 1f)
                    continue;
                float rr = Mathf.Lerp(8f, reach + 10f, BbEaseOut(q));
                Arc(at - V2.FromAngle(aim) * 20f, rr, Mathf.Lerp(6f, 1f, q), (i == 0 ? Game.BbGold : Game.BbGlow).WithAlpha(0.85f * (1f - q)), 16, aim - 0.62f, 1.24f);
            }
            if (fxOn && s < 0.35f)
                GlowDisc(at, 26f, BbBossHi, 1f - s / 0.35f);
        }

        /// FxKind.BulwarkBash: afterimages, streaks and a bow wave while the knight dashes, then the impact.
        void DrawBulwarkBashFx(Effect fx, float t)
        {
            int id = Mathf.FloorToInt(fx.Radius / 1000f + 0.001f);
            float ram = Mathf.Repeat(fx.Radius, 1000f), time = Game.BashTime(ram);
            V2 d = fx.Pos2 - fx.Pos;
            float len = d.Length;
            if (len < 1f)
                return;
            V2 dir = d / len, nrm = dir.Orthogonal();
            bool full = fx.Color.G < 0.83f && fx.Color.R > 0.99f;  // (BbGold)
            Player owner;
            game.Players.TryGetValue(id, out owner);
            if (fx.T < time)
            {
                float u = BbEaseOut(fx.T / time);
                V2 head = fx.Pos + d * u;
                var pose = new BbPose { Ang = dir.Angle, Rad = owner != null ? Game.BulwarkRadius(owner) - 2f : 26f, Half = Game.BbBraceHalf, Th = 12f, Braced = true };
                for (int i = 1; i <= 6; i++)  // afterimages behind the knight
                {
                    float g = u - i * 0.07f;
                    if (g < 0f)
                        break;
                    V2 at = fx.Pos + d * g;
                    Circle(at, 15f, Game.BbGlow.WithAlpha(0.22f * (1f - i / 7f)), 16);
                    if (owner != null)
                        DrawBulwarkPlate(owner, at, pose, t, 1f - i / 7f);
                }
                for (int i = 0; i < 7; i++)  // speed streaks
                {
                    float side = (BbHash(i * 3.1f + fx.Pos.X) - 0.5f) * 30f, back = 14f + 40f * BbHash(i * 7.7f + fx.Pos.Y);
                    V2 s0 = head - dir * back + nrm * side;
                    Line(s0, s0 - dir * (16f + 10f * BbHash(i)), 1.4f, new Rgba(0.6f, 0.8f, 1f, 0.5f));
                }
                Arc(head, 38f, 3f, Rgba.White.WithAlpha(0.85f), 14, dir.Angle - 0.9f, 1.8f);  // the bow wave
                if (fxOn)
                    GlowDisc(head + dir * 30f, 40f, Game.BbGlow, 0.5f);
                return;
            }
            float q = (fx.T - time) / Mathf.Max(0.05f, fx.Dur - time);
            V2 hit = fx.Pos2 + dir * 26f;
            float reach = 50f + 0.9f * ram;
            float ring = BbEaseOut(Mathf.Clamp01(q / 0.45f));
            if (q < 0.45f)
            {
                Arc(hit, Mathf.Lerp(10f, reach, ring), Mathf.Max(0.5f, 7f * (1f - ring)), Game.BbGlow.WithAlpha(1f - ring), 28);
                if (ram > 40f)
                    Arc(hit, Mathf.Lerp(6f, 30f + 0.5f * ram, ring), Mathf.Max(0.5f, 3f * (1f - ring)), Game.BbGold.WithAlpha(1f - ring), 24);
                int n = 10 + Mathf.RoundToInt(ram * 0.12f);
                for (int i = 0; i < n; i++)  // sparks thrown forward
                {
                    float a = dir.Angle + (BbHash(i * 1.7f + fx.Pos.X) - 0.5f) * 2f, sp = 30f + 70f * BbHash(i * 5.3f + fx.Pos.Y);
                    V2 s0 = hit + V2.FromAngle(a) * sp * ring * 1.5f;
                    Line(s0, s0 - V2.FromAngle(a) * 8f * (1f - ring), 1.6f, new Rgba(0.85f, 0.94f, 1f, 1f - ring));
                }
            }
            if (fxOn && q < 0.16f)
                GlowDisc(hit, 22f + 0.18f * ram, Rgba.White, 1f - q / 0.16f);
            if (ram > 15f)  // cracks in the floor, fading
            {
                int lines = 5 + Mathf.RoundToInt(ram / 25f);
                var crack = new Rgba(0.59f, 0.78f, 1f, 0.55f * (1f - q));
                for (int i = 0; i < lines; i++)
                {
                    float a = dir.Angle + (BbHash(i * 2.3f + fx.Pos2.X) - 0.5f) * 2.2f, L = (20f + 0.45f * ram) * (0.6f + 0.4f * BbHash(i * 9.1f));
                    V2 pt = hit;
                    for (int j = 1; j <= 4; j++)
                    {
                        float aa = a + (BbHash(i * 13f + j * 3.7f + fx.Pos2.Y) - 0.5f) * 0.7f;
                        V2 nx = pt + V2.FromAngle(aa) * (L / 4f);
                        Line(pt, nx, 1.5f, crack);
                        pt = nx;
                    }
                }
            }
            if (full && q < 0.3f)  // the soaked shots bursting out
                Arc(hit, Mathf.Lerp(20f, 120f, q / 0.3f), 2f, Game.BbGold.WithAlpha(1f - q / 0.3f), 18, dir.Angle - 0.62f, 1.24f);
        }

        /// FxKind.BulwarkBlock: a shot stopped on the plate (Pos2.Y 0: sparks off it and a ping) or soaked (1: a mote spiralling
        /// into the boss).
        void DrawBulwarkBlockFx(Effect fx)
        {
            Player owner;
            bool has = game.Players.TryGetValue(Mathf.RoundToInt(fx.Pos2.X), out owner);
            V2 c = has ? owner.Disp : fx.Pos;
            float q = Mathf.Clamp01(fx.T / fx.Dur);
            if (fx.Pos2.Y < 0.5f)
            {
                V2 nrm = (fx.Pos - c).IsZero ? new V2(1f, 0f) : (fx.Pos - c).Normalized();
                for (int i = 0; i < 6; i++)
                {
                    float a = nrm.Angle + (BbHash(i * 2.9f + fx.Pos.X) - 0.5f) * 1.8f, sp = 18f + 26f * BbHash(i * 4.1f + fx.Pos.Y);
                    V2 s0 = fx.Pos + V2.FromAngle(a) * sp * BbEaseOut(q);
                    Line(s0, s0 - V2.FromAngle(a) * 6f * (1f - q), 1.4f, Game.BbSpark.WithAlpha(1f - q));
                }
                Arc(fx.Pos, Mathf.Lerp(3f, 13f, BbEaseOut(q)), 2f * (1f - q), new Rgba(1f, 0.86f, 0.63f, 1f - q), 12);
                return;
            }
            float r = has ? BulwarkPose(owner).Rad : 24f;
            V2 boss = has ? c + V2.FromAngle(BulwarkPose(owner).Ang) * (r + 1f) : fx.Pos;
            float e = q * q, sw = Mathf.Sin(Mathf.PI * q) * 10f * (BbHash(fx.Pos.X) < 0.5f ? 1f : -1f);
            V2 dd = boss - fx.Pos;
            V2 side = dd.IsZero ? V2.Zero : dd.Normalized().Orthogonal();
            V2 at = fx.Pos + dd * e + side * sw;
            var col = new Rgba(Mathf.Lerp(1f, 0.5f, q), Mathf.Lerp(0.78f, 0.84f, q), Mathf.Lerp(0.47f, 1f, q));
            Circle(at, 2.6f * (1f - 0.4f * q), col, 8);
            if (fxOn)
                GlowDisc(at, 9f, col, 0.9f);
            Arc(fx.Pos, Mathf.Lerp(2f, 12f, BbEaseOut(q)), 2f * (1f - q), Game.BbGlow.WithAlpha(1f - q), 10);
        }
    }
}
