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
    ///   FxKind.BulwarkBlock: a shot stopped on the plate (sparks and a ping), soaked (a mote spiralling into the boss), a
    ///     melee blow on the braced plate (a clang star), or the thrown shield cutting an enemy (a slash);
    ///   FxKind.BulwarkThrow: the Shield Throw spinning out and back (a ribbon of light, afterimages, a glint, the catch).
    /// The held shield runs on springs (BbAnim): it eases after your aim, swells out with a bounce when you brace, kicks back
    /// on a block, and the cuts cock back, whip across past their end (smeared, with afterimages) and settle.
    public partial class ArenaApp
    {
        static readonly Rgba BbEnamel = new Rgba(0.11f, 0.23f, 0.43f), BbEnamelHi = new Rgba(0.2f, 0.37f, 0.66f), BbSteelHi = new Rgba(0.72f, 0.8f, 0.92f);
        static readonly Rgba BbRim = new Rgba(0.9f, 0.94f, 0.98f), BbInner = new Rgba(0.03f, 0.06f, 0.12f), BbTrim = new Rgba(0.87f, 0.71f, 0.38f);
        static readonly Rgba BbBossCol = new Rgba(0.86f, 0.66f, 0.27f), BbBossDark = new Rgba(0.43f, 0.29f, 0.08f), BbBossHi = new Rgba(1f, 0.96f, 0.81f);

        static float BbEaseIO(float x) { x = Mathf.Clamp01(x); return x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f; }
        static float BbEaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
        static float BbBack(float x) { x = Mathf.Clamp01(x); const float c = 1.9f; float u = x - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }
        static float BbHash(float x) { float s = Mathf.Sin(x * 12.9898f) * 43758.5453f; return s - Mathf.Floor(s); }

        /// Where the shield is now: its middle angle, its distance out, half its width and how thick it is; Smear (0-1) how
        /// fast it is moving (stretched thin and wide), Flash (0-1) a white flash on its face, Driven while a step or bash
        /// poses it (followed exactly; otherwise the shield springs after its rest pose).
        struct BbPose { public float Ang, Rad, Half, Th, Smear, Flash; public bool Braced, Driven, Hidden; }

        /// Each knight's shield as drawn: springs on its width, distance and thickness (the Brace swells out with a thunk, a
        /// caught shield pops back on), its angle easing after the aim, and where its boss was last drawn (soaked motes fly
        /// there).
        class BbAnim { public float Ang, Half, HalfV, Rad, RadV, Th, ThV, Last = -1f; public bool Init, WasHidden; public V2 Boss; }
        readonly System.Collections.Generic.Dictionary<int, BbAnim> bbAnims = new System.Collections.Generic.Dictionary<int, BbAnim>();

        static float BbDeltaAngle(float from, float to) => Mathf.Repeat(to - from + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;

        static void BbSpring(ref float x, ref float v, float target, float dt)
        {
            const float K = 420f, C = 25f;  // (a little bounce: about 10% past the mark)
            for (float left = dt; left > 0f; left -= 1f / 120f)
            {
                float h = Mathf.Min(left, 1f / 120f);
                v += ((target - x) * K - v * C) * h;
                x += v * h;
            }
        }

        /// A cut's swing (steps 1 and 2) `u` of the way through: how far the shield is off the aim. It cocks back past where
        /// the cut starts (anticipation), whips across fast and past its end, holds a beat, and settles back to the aim.
        public static float BbCutOffset(int step, float u)
        {
            float A = step == 1 ? 1.05f : 1.25f, sgn = step == 1 ? 1f : -1f;
            float cock = -A * 1.22f, end = A * 1.14f;
            float off;
            if (u < 0.16f)
                off = Mathf.Lerp(0f, cock, BbEaseOut(u / 0.16f));
            else if (u < 0.5f)
            {
                float k = (u - 0.16f) / 0.34f;
                off = Mathf.Lerp(cock, end, 1f - Mathf.Pow(1f - k, 4f));
            }
            else if (u < 0.72f)
                off = Mathf.Lerp(end, A * 1.04f, BbEaseIO((u - 0.5f) / 0.22f));
            else
                off = Mathf.Lerp(A * 1.04f, 0f, BbEaseIO((u - 0.72f) / 0.28f));
            return off * sgn;
        }

        /// How fast a cut is moving at `u` (0-1: the whip's peak is 1).
        static float BbCutSmear(float u) => u > 0.16f && u < 0.5f ? Mathf.Sin(Mathf.PI * Mathf.Pow((u - 0.16f) / 0.34f, 0.6f)) : 0f;

        /// The throw's wind-up: the shield swings round behind you, edge on, then whips forward and leaves the hand.
        public static float BbThrowRelease => Game.BbThrowWind / Game.BbDur[3];

        /// The newest live effect of `kind` for player `id` (Radius / div is the id), or null.
        Effect BbLatest(FxKind kind, int id, int div)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
                if (fx.Kind == kind && fx.T < fx.Dur && Mathf.FloorToInt(fx.Radius / div + 0.001f) == id && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// A thrown shield of player `id` still in the air, or null.
        Effect BbThrown(int id)
        {
            foreach (var fx in FxOf(FxKind.BulwarkThrow))
                if (fx.Kind == FxKind.BulwarkThrow && fx.T < Game.BbThrowFlight && Mathf.RoundToInt(fx.Radius) == id)
                    return fx;
            return null;
        }

        /// Where the shield wants to be now (no springs: the steps and the bash say exactly).
        BbPose BulwarkPose(Player p)
        {
            float r0 = Game.BulwarkRadius(p) - 4f;
            var o = new BbPose { Ang = p.Aim, Rad = r0, Half = p.BbBrace ? Game.BbBraceHalf : Game.BbGuardHalf, Th = p.BbBrace ? 12f : 9.5f, Braced = p.BbBrace };
            if (BbThrown(p.Id) != null)
            {
                o.Hidden = true;
                return o;
            }
            var bash = BbLatest(FxKind.BulwarkBash, p.Id, 1000);
            if (bash != null && bash.T < Game.BashTime(Mathf.Repeat(bash.Radius, 1000f)) + 0.08f)
            {
                o.Ang = (bash.Pos2 - bash.Pos).Angle;
                o.Rad = r0 + 2f;
                o.Half = Game.BbBraceHalf;
                o.Th = 13f;
                o.Braced = o.Driven = true;
                o.Smear = 0.6f;
                return o;
            }
            var st = p.Parrying ? null : BbLatest(FxKind.BulwarkStep, p.Id, 10);  // (the parry takes a step back: the shield drops it)
            if (st != null)
            {
                int step = Mathf.Clamp(Mathf.RoundToInt(st.Radius) % 10, 1, 4);
                float dur = Game.BbDur[step - 1], u = Mathf.Clamp01(st.T / dur);
                if (u < 1f)
                {
                    float aim = (st.Pos2 - st.Pos).Angle;
                    o.Driven = true;
                    o.Half = Game.BbSwingHalf;
                    o.Ang = aim;
                    if (step < 3)
                    {
                        o.Ang = aim + BbCutOffset(step, u);
                        float pull = u < 0.16f ? -3f * Mathf.Sin(Mathf.PI * u / 0.16f) : 0f;
                        o.Rad = r0 + pull + (step == 1 ? 12f : 14f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01((u - 0.1f) / 0.75f));
                        o.Smear = BbCutSmear(u);
                    }
                    else if (step == 3)
                    {
                        if (u < 0.3f)  // drawn in, gathering
                        {
                            float k = BbEaseOut(u / 0.3f);
                            o.Rad = Mathf.Lerp(r0, r0 - 8f, k);
                            o.Th = Mathf.Lerp(9.5f, 13f, k);
                            o.Half = Mathf.Lerp(Game.BbSwingHalf, Game.BbSwingHalf * 0.8f, k);
                        }
                        else if (u < 0.48f)  // the thrust, past its mark
                        {
                            float k = (u - 0.3f) / 0.18f;
                            o.Rad = Mathf.Lerp(r0 - 8f, r0 + 28f, BbBack(k));
                            o.Th = Mathf.Lerp(13f, 10f, k);
                            o.Smear = Mathf.Sin(Mathf.PI * k);
                        }
                        else
                            o.Rad = Mathf.Lerp(r0 + 28f, r0, BbEaseIO((u - 0.48f) / 0.52f));
                        float hit = (u - 0.4f) / 0.22f;
                        if (hit > 0f && hit < 1f)
                            o.Flash = 1f - hit;
                    }
                    else  // the throw's wind-up, then gone from the hand
                    {
                        float rel = BbThrowRelease;
                        if (u >= rel)
                        {
                            o.Hidden = true;
                            return o;
                        }
                        float k = u / rel;
                        float back = BbEaseOut(Mathf.Clamp01(k / 0.7f));
                        float whip = k > 0.7f ? BbEaseIO((k - 0.7f) / 0.3f) : 0f;
                        o.Ang = aim + Mathf.Lerp(0f, -2.1f, back) * (1f - whip);
                        o.Rad = r0 - 6f * back + 10f * whip;
                        o.Half = Mathf.Lerp(Game.BbSwingHalf, 0.55f, back);
                        o.Th = Mathf.Lerp(9.5f, 12f, back);
                        o.Smear = whip;
                        o.Flash = 0.6f * whip;
                    }
                    return o;
                }
            }
            return o;
        }

        /// The shield as it is drawn this frame: the pose, run through the knight's springs, plus a kick back from a block.
        BbPose BulwarkDrawn(Player p)
        {
            var o = BulwarkPose(p);
            BbAnim a;
            if (!bbAnims.TryGetValue(p.Id, out a))
                bbAnims[p.Id] = a = new BbAnim();
            float now = Time.unscaledTime, dt = a.Last < 0f ? 0f : Mathf.Clamp(now - a.Last, 0f, 0.05f);
            if (a.Last != now)
            {
                a.Last = now;
                if (!a.Init)
                {
                    a.Init = true;
                    a.Ang = o.Ang; a.Half = o.Half; a.Rad = o.Rad; a.Th = o.Th;
                }
                if (o.Hidden)
                    a.WasHidden = true;
                else if (a.WasHidden)  // caught: it pops back on small and springs out
                {
                    a.WasHidden = false;
                    a.Ang = o.Ang; a.Half = o.Half * 0.4f; a.Rad = o.Rad - 9f; a.Th = o.Th * 0.6f;
                    a.HalfV = a.RadV = a.ThV = 0f;
                }
                if (o.Driven)
                    a.Ang = o.Ang;
                else
                    a.Ang += BbDeltaAngle(a.Ang, o.Ang) * (1f - Mathf.Exp(-16f * dt));
                BbSpring(ref a.Half, ref a.HalfV, o.Half, dt);
                BbSpring(ref a.Th, ref a.ThV, o.Th, dt);
                if (o.Driven)
                {
                    a.Rad = o.Rad;
                    a.RadV = 0f;
                }
                else
                    BbSpring(ref a.Rad, ref a.RadV, o.Rad, dt);
            }
            var d = o;
            d.Ang = a.Ang;
            d.Half = a.Half * (1f + 0.14f * o.Smear);
            d.Th = a.Th * (1f - 0.22f * o.Smear);
            d.Rad = a.Rad;
            foreach (var fx in FxOf(FxKind.BulwarkBlock))  // the newest block knocks it back a little and flashes where it struck
                if (fx.Kind == FxKind.BulwarkBlock && fx.T < 0.14f && fx.Pos2.Y < 2.5f && Mathf.RoundToInt(fx.Pos2.X) == p.Id)
                {
                    float k = 1f - fx.T / 0.14f;
                    d.Rad -= (fx.Pos2.Y > 1.5f ? 5f : 2.5f) * k;
                    d.Flash = Mathf.Max(d.Flash, 0.7f * k);
                }
            a.Boss = p.Disp + V2.FromAngle(d.Ang) * (d.Rad + 1f);
            return d;
        }

        /// Where `p`'s shield boss was last drawn (soaked motes fly there).
        V2 BbBossAt(Player p)
        {
            BbAnim a;
            return bbAnims.TryGetValue(p.Id, out a) && a.Init ? a.Boss : p.Disp + V2.FromAngle(p.Aim) * Game.BulwarkRadius(p);
        }

        /// The shield itself at `c`, posed `o`; `ghost` > 0 draws a flat blue afterimage of that strength instead.
        void DrawBulwarkPlate(Player p, V2 c, BbPose o, float t, float ghost = 0f)
        {
            float a0 = o.Ang - o.Half, sw = 2f * o.Half, r = o.Rad, th = o.Th;
            const int N = 30;
            if (ghost > 0f)
            {
                Arc(c, r, th, Game.BbGlow.WithAlpha(0.3f * ghost), N, a0, sw);
                Arc(c, r + th / 2f - 0.8f, 1.4f, Rgba.White.WithAlpha(0.35f * ghost), N, a0, sw);
                return;
            }
            Arc(c + new V2(2f, 3f), r, th + 2f, new Rgba(0f, 0f, 0f, 0.35f), N, a0, sw);  // its shadow on the floor
            V2 e0 = c + V2.FromAngle(a0) * r, e1 = c + V2.FromAngle(a0 + sw) * r;     // the rounded ends
            Circle(e0, th / 2f, BbEnamel, 10);
            Circle(e1, th / 2f, BbEnamel, 10);
            Arc(c, r, th, BbEnamel, N, a0, sw);
            Arc(c, r + th * 0.12f, th * 0.5f, BbEnamelHi, N, a0, sw);
            Arc(c, r + th * 0.36f, th * 0.28f, BbSteelHi, N, a0, sw);
            // a sheen sliding slowly across the enamel (quicker and brighter as it swings)
            float sheen = Mathf.Sin(t * 0.9f + p.Id) * 0.55f + o.Smear * 0.35f;
            float sa = o.Ang + o.Half * Mathf.Clamp(sheen, -0.8f, 0.8f);
            Arc(c, r + th * 0.15f, th * 0.42f, Rgba.White.WithAlpha(0.16f + 0.3f * o.Smear), 6, sa - 0.14f, 0.28f);
            Arc(c, r + th * 0.15f, th * 0.42f, Rgba.White.WithAlpha(0.08f + 0.15f * o.Smear), 6, sa - 0.3f, 0.6f);
            Arc(c, r + th / 2f - 0.8f, 1.6f, BbRim, N, a0, sw);
            Arc(c, r - th / 2f + 0.7f, 1.4f, BbInner, N, a0, sw);
            Arc(c, r + 0.5f, 1f, BbTrim.WithAlpha(0.85f), N / 2, a0 + 0.1f, o.Half - 0.3f);  // the gold trim, broken at the boss
            Arc(c, r + 0.5f, 1f, BbTrim.WithAlpha(0.85f), N / 2, o.Ang + 0.2f, o.Half - 0.3f);
            for (float a = -o.Half + 0.16f; a <= o.Half - 0.12f; a += 0.36f)  // rivets
                if (Mathf.Abs(a) > 0.2f)
                    Circle(c + V2.FromAngle(o.Ang + a) * (r + 0.5f), 1.15f, BbRim, 6);
            if (o.Flash > 0f)  // struck or striking: the face flares white
            {
                Arc(c, r, th + 2f, Rgba.White.WithAlpha(0.55f * o.Flash), N, a0, sw);
                if (fxOn)
                    GlowDisc(c + V2.FromAngle(o.Ang) * r, 30f, Rgba.White, 0.5f * o.Flash);
            }
            float k = o.Braced ? 1f : 0f, ram = Mathf.Clamp01(p.BbRam / Game.BbRamMax);
            bool full = p.BbRam >= Game.BbRamMax - 0.01f;
            Rgba film = full ? Game.BbGold : Game.BbGlow;
            if (k > 0f)  // braced: a film of light along the face, bright motes running round it
            {
                Arc(c, r + th / 2f + 5f, 7f, film.WithAlpha(0.14f), N, a0, sw);
                Arc(c, r + th / 2f + 3.5f, 1.2f, film.WithAlpha(0.5f + 0.2f * Mathf.Sin(t * 6f)), N, a0, sw);
                for (int i = 0; i < 6; i++)
                {
                    float f = Mathf.Repeat(t * 0.9f + i / 6f, 1f);
                    Arc(c, r + th / 2f + 5f, 1.6f, film.WithAlpha(0.7f * Mathf.Sin(Mathf.PI * f)), 4, a0 + sw * f, 0.12f);
                }
                if (fxOn)
                    GlowDisc(c + V2.FromAngle(a0 + sw * Mathf.Repeat(t * 0.6f, 1f)) * (r + th / 2f + 5f), 14f, film, 0.45f);
            }
            V2 boss = c + V2.FromAngle(o.Ang) * (r + 1f);  // the boss
            float br = (o.Braced ? 6.6f : 5.4f) + (full ? 0.6f * Mathf.Sin(t * 10f) : 0f) + 1.4f * o.Flash;
            Circle(boss, br + 1f, BbBossDark, 14);
            Circle(boss, br, BbBossCol.Mix(BbBossHi, 0.6f * o.Flash), 14);
            Circle(boss + new V2(-1.4f, -1.4f), br * 0.45f, BbBossHi, 10);
            if (ram > 0f)
                Arc(boss, br + 2.4f, 1.2f, film.WithAlpha(0.35f + 0.5f * ram), 14, -Mathf.PI / 2f, Mathf.PI * 2f * ram);  // Ram, round the boss
            if (fxOn && ram > 0f)
                GlowDisc(boss, br * 3.4f, film, 0.7f * ram);
        }

        /// The kite shield seen flat from above, spinning (thrown): `spin` its turn, `ghost` > 0 a faded blue copy.
        void DrawBbKite(V2 c, float spin, float scale, float ghost)
        {
            var pts = BbKitePts;
            int n = pts.Length;
            var w = new V2[n];
            for (int i = 0; i < n; i++)
                w[i] = c + pts[i].Rotated(spin) * scale;
            if (ghost > 0f)
            {
                SetColor(Game.BbGlow.WithAlpha(0.3f * ghost));
                for (int i = 0; i < n; i++)
                    Tri(c, w[i], w[(i + 1) % n]);
                return;
            }
            SetColor(new Rgba(0f, 0f, 0f, 0.3f));  // its shadow
            for (int i = 0; i < n; i++)
                Tri(c + new V2(3f, 5f), w[i] + new V2(3f, 5f), w[(i + 1) % n] + new V2(3f, 5f));
            SetColor(BbEnamel);
            for (int i = 0; i < n; i++)
                Tri(c, w[i], w[(i + 1) % n]);
            SetColor(BbEnamelHi);
            for (int i = 0; i < n; i++)
                Tri(c, c + (w[i] - c) * 0.66f, c + (w[(i + 1) % n] - c) * 0.66f);
            for (int i = 0; i < n; i++)
                Line(w[i], w[(i + 1) % n], 1.8f, BbRim);
            V2.Axes(spin - Mathf.PI / 2f, out V2 up, out V2 side);
            Line(c - up * 9f * scale, c + up * 10f * scale, 1.1f, BbTrim.WithAlpha(0.85f));  // the gold cross
            Line(c - side * 6f * scale, c + side * 6f * scale, 1.1f, BbTrim.WithAlpha(0.85f));
            Circle(c, 4.2f * scale, BbBossDark, 12);
            Circle(c, 3.4f * scale, BbBossCol, 12);
            Circle(c + new V2(-1f, -1f), 1.4f * scale, BbBossHi, 8);
        }

        static readonly V2[] BbKitePts = { new V2(0f, -13f), new V2(8f, -11f), new V2(11f, -4f), new V2(8f, 5f), new V2(0f, 15f), new V2(-8f, 5f), new V2(-11f, -4f), new V2(-8f, -11f) };

        /// FxKind.BulwarkThrow: the thrown shield spinning out and back along BbThrowAt, a ribbon of light behind it, a glint
        /// at the far end, and a ring as it's caught.
        void DrawBulwarkThrowFx(Effect fx, float t)
        {
            Player owner;
            int id = Mathf.RoundToInt(fx.Radius);
            bool has = game.Players.TryGetValue(id, out owner);
            V2 home = has ? owner.Disp : fx.Pos;
            float T = fx.T;
            if (T >= Game.BbThrowFlight)  // caught: a ring and a flash at the knight
            {
                float q = Mathf.Clamp01((T - Game.BbThrowFlight) / Game.BbThrowCatchFx);
                float aim = has ? owner.Aim : (fx.Pos2 - fx.Pos).Angle;
                Arc(home, Mathf.Lerp(18f, 40f, BbEaseOut(q)), 3f * (1f - q), Game.BbGlow.WithAlpha(1f - q), 28);
                Arc(home + V2.FromAngle(aim) * 20f, Mathf.Lerp(6f, 22f, BbEaseOut(q)), 2f * (1f - q), Rgba.White.WithAlpha(0.9f * (1f - q)), 14, aim - 1.2f, 2.4f);
                return;
            }
            bool back;
            V2 at = Game.BbThrowAt(fx.Pos, fx.Pos2, home, T, out back);
            float spin = T * 24f;
            // the ribbon: where it has been, thinning to nothing
            const int R = 14;
            V2 prev = at;
            for (int i = 1; i <= R; i++)
            {
                float ti = Mathf.Max(0f, T - i * 0.012f);
                bool b2;
                V2 pt = Game.BbThrowAt(fx.Pos, fx.Pos2, home, ti, out b2);
                V2 d = pt - prev;
                if (d.LengthSq > 0.25f)
                {
                    V2 n = d.Normalized().Orthogonal();
                    float w0 = 11f * (1f - (i - 1) / (float)R), w1 = 11f * (1f - i / (float)R);
                    SetColor(Game.BbGlow.WithAlpha(0.32f * (1f - i / (float)R)));
                    Quad(prev + n * w0, pt + n * w1, pt - n * w1, prev - n * w0);
                    Line(prev, pt, 1.6f * (1f - i / (float)R), Rgba.White.WithAlpha(0.7f * (1f - i / (float)R)));
                }
                prev = pt;
                if (ti <= 0f)
                    break;
            }
            for (int i = 3; i >= 1; i--)  // afterimages a beat behind
            {
                bool b2;
                V2 g = Game.BbThrowAt(fx.Pos, fx.Pos2, home, Mathf.Max(0f, T - i * 0.022f), out b2);
                DrawBbKite(g, spin - i * 0.5f, 1f, 0.6f * (1f - i / 4f));
            }
            Arc(at, 21f, 1.6f, Rgba.White.WithAlpha(0.45f), 10, spin * 0.7f, 1.6f);  // the air it cuts, whirling
            Arc(at, 21f, 1.6f, Game.BbGlow.WithAlpha(0.45f), 10, spin * 0.7f + Mathf.PI, 1.6f);
            DrawBbKite(at, spin, 1f, 0f);
            if (fxOn)
                GlowDisc(at, 30f, Game.BbGlow, 0.45f);
            float hang = (T - Game.BbThrowOut) / Game.BbThrowHang;  // the glint at the far end
            if (hang > -0.5f && hang < 1.6f)
            {
                float g = 1f - Mathf.Abs(hang - 0.5f) / 1.1f;
                if (g > 0f)
                {
                    float L = 16f * g;
                    Line(at - new V2(L, 0f), at + new V2(L, 0f), 1.6f, Rgba.White.WithAlpha(g));
                    Line(at - new V2(0f, L * 1.4f), at + new V2(0f, L * 1.4f), 1.6f, Rgba.White.WithAlpha(g));
                }
            }
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
            var o = BulwarkDrawn(p);
            if (!o.Hidden)
            {
                var st = p.Parrying ? null : BbLatest(FxKind.BulwarkStep, p.Id, 10);
                if (st != null && o.Smear > 0.05f)  // afterimages behind a fast swing
                {
                    int step = Mathf.Clamp(Mathf.RoundToInt(st.Radius) % 10, 1, 4);
                    float dur = Game.BbDur[step - 1], aim = (st.Pos2 - st.Pos).Angle;
                    for (int i = 1; i <= 3 && step < 3; i++)
                    {
                        float ui = Mathf.Clamp01(st.T / dur - i * 0.035f);
                        var g = o;
                        g.Ang = aim + BbCutOffset(step, ui);
                        DrawBulwarkPlate(p, p.Disp, g, t, o.Smear * (1f - i / 4f));
                    }
                }
                DrawBulwarkPlate(p, p.Disp, o, t);
            }
            DrawBulwarkPips(p);
        }

        /// A crescent band from `tail` to `head` at radius `r`, growing to `w` thick at its head (a cut's trail): a soft wide
        /// glow under a bright band, a white leading edge and a hot cap at the head, and motes shed along it.
        void BbCrescent(V2 c, float r, float tail, float head, float w, Rgba col, float alpha, float seed)
        {
            const int N = 18;
            for (int pass = 0; pass < 2; pass++)
            {
                float ww = pass == 0 ? w * 1.45f : w, aa = pass == 0 ? 0.3f : 1f, ro = pass == 0 ? r + 3f : r;
                for (int i = 0; i < N; i++)
                {
                    float u0 = i / (float)N, u1 = (i + 1) / (float)N;
                    float b0 = Mathf.Lerp(tail, head, u0), b1 = Mathf.Lerp(tail, head, u1);
                    float w0 = ww * Mathf.Pow(u0, 1.3f), w1 = ww * Mathf.Pow(u1, 1.3f);
                    Rgba cc = pass == 0 ? col : col.Mix(Rgba.White, 0.35f * u1);
                    SetColor(cc.WithAlpha(aa * alpha * Mathf.Pow(u1, 1.6f)));
                    Quad(c + V2.FromAngle(b0) * ro, c + V2.FromAngle(b1) * ro, c + V2.FromAngle(b1) * (ro - w1), c + V2.FromAngle(b0) * (ro - w0));
                }
            }
            float lo = Mathf.Min(tail, head), span = Mathf.Abs(head - tail);
            float hs = Mathf.Min(span, 0.35f);
            Arc(c, r, 1.2f, Rgba.White.WithAlpha(0.5f * alpha), N, lo, span);
            Arc(c, r, 2.6f, Rgba.White.WithAlpha(0.95f * alpha), 6, head > tail ? head - hs : head, hs);
            Line(c + V2.FromAngle(head) * (r - w * 0.9f), c + V2.FromAngle(head) * (r + 2f), 2.4f, Rgba.White.WithAlpha(0.9f * alpha));
            for (int i = 0; i < 7; i++)  // motes shed off the trail
            {
                float h = BbHash(seed + i * 3.3f), f = Mathf.Lerp(0.15f, 0.9f, h);
                float a = Mathf.Lerp(tail, head, f), rr = r + 2f + 9f * BbHash(seed + i * 7.1f) * (1f - f);
                Circle(c + V2.FromAngle(a) * rr, 1.1f + 0.8f * h, Rgba.White.Mix(col, 0.4f).WithAlpha(alpha * f), 6);
            }
        }

        /// FxKind.BulwarkStep: a cut's crescent trailing the rim, the Boss Slam's shock arcs, star and ring, or the throw's
        /// whip as it leaves the hand.
        void DrawBulwarkStepFx(Effect fx)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(fx.Radius) % 10, 1, 4);
            float dur = Game.BbDur[step - 1], aim = (fx.Pos2 - fx.Pos).Angle, reach = (fx.Pos2 - fx.Pos).Length;
            Player owner;
            V2 c = game.Players.TryGetValue(Mathf.FloorToInt(fx.Radius / 10f + 0.001f), out owner) ? owner.Disp : fx.Pos;
            if (owner != null && owner.Parrying)
                return;  // (the parry took it back)
            float u = Mathf.Clamp01(fx.T / dur);
            if (step < 3)
            {
                float uh = u, ut = Mathf.Max(0.16f, u - 0.17f);
                if (uh <= 0.17f || uh - ut < 0.005f)
                    return;
                float head = aim + BbCutOffset(step, uh), tail = aim + BbCutOffset(step, ut);
                float fade = fx.T > dur ? 1f - Mathf.Clamp01((fx.T - dur) / 0.2f) : 1f;
                if (Mathf.Abs(head - tail) < 0.03f || fade <= 0f)
                    return;
                BbCrescent(c, reach - 4f, tail, head, step == 1 ? 22f : 26f, Game.BbGlow, 0.62f * fade, fx.Pos.X * 0.1f + step);
                return;
            }
            if (step == 4)  // the throw: a whoosh arc as the arm whips through
            {
                float k = (u / BbThrowRelease - 0.7f) / 0.45f;
                if (k <= 0f || k >= 1f)
                    return;
                float head = aim + Mathf.Lerp(-1.6f, 0.2f, BbEaseOut(k)), tail = aim - 1.9f;
                BbCrescent(c, (owner != null ? Game.BulwarkRadius(owner) : 26f) + 10f, tail, head, 16f, Game.BbGlow, 0.55f * (1f - k), fx.Pos.Y * 0.1f);
                return;
            }
            float s = (fx.T - dur * 0.3f) / 0.42f;  // the slam lands as the boss thrusts out
            if (s < 0f || s >= 1f)
                return;
            V2 at = c + V2.FromAngle(aim) * ((owner != null ? Game.BulwarkRadius(owner) : 26f) + 26f);
            for (int i = 0; i < 3; i++)
            {
                float q = Mathf.Clamp01(s * 1.3f - i * 0.12f);
                if (q <= 0f || q >= 1f)
                    continue;
                float rr = Mathf.Lerp(8f, reach + 10f, BbEaseOut(q));
                Arc(at - V2.FromAngle(aim) * 20f, rr, Mathf.Lerp(7f, 1f, q), (i == 0 ? Game.BbGold : Game.BbGlow).WithAlpha(0.85f * (1f - q)), 18, aim - 0.62f, 1.24f);
            }
            float g = BbEaseOut(Mathf.Clamp01(s / 0.6f));  // a ring on the floor round the strike
            if (s < 0.6f)
                Arc(at, Mathf.Lerp(6f, 48f, g), 2.5f * (1f - g), Game.BbGold.WithAlpha(0.8f * (1f - g)), 24);
            if (s < 0.45f)  // a star of rays off the boss
            {
                float q = s / 0.45f;
                for (int i = 0; i < 10; i++)
                {
                    float a = aim + (i / 10f - 0.5f) * 2.4f + (BbHash(fx.Pos.X + i) - 0.5f) * 0.2f;
                    float L0 = 6f + 30f * BbEaseOut(q), L1 = L0 + (10f + 12f * BbHash(i * 5.1f + fx.Pos.Y)) * (1f - q);
                    Line(at + V2.FromAngle(a) * L0, at + V2.FromAngle(a) * L1, 2f * (1f - q), (i % 3 == 0 ? Game.BbGold : Rgba.White).WithAlpha(1f - q));
                }
            }
            if (fxOn && s < 0.35f)
                GlowDisc(at, 30f, BbBossHi, 1f - s / 0.35f);
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

        /// FxKind.BulwarkBlock: a shot stopped on the plate (Pos2.Y 0: sparks off it and a ping), soaked (1: a mote spiralling
        /// into the boss), a melee blow stopped on the braced plate (2: a clang star, a burst of sparks and a mote), or a
        /// thrown shield cutting an enemy (3: a bright slash across it). Pos2.X is the knight.
        void DrawBulwarkBlockFx(Effect fx)
        {
            Player owner;
            bool has = game.Players.TryGetValue(Mathf.RoundToInt(fx.Pos2.X), out owner);
            V2 c = has ? owner.Disp : fx.Pos;
            float q = Mathf.Clamp01(fx.T / fx.Dur);
            int kind = Mathf.RoundToInt(fx.Pos2.Y);
            if (kind == 3)
            {
                float a = BbHash(fx.Pos.X * 0.37f + fx.Pos.Y) * Mathf.PI, L = fx.Radius * 1.5f + 8f;
                V2 d = V2.FromAngle(a), mid = fx.Pos;
                float grow = BbEaseOut(Mathf.Clamp01(q / 0.3f));
                Line(mid - d * L * grow, mid + d * L * grow, 5f * (1f - q), Game.BbGlow.WithAlpha(0.5f * (1f - q)));
                Line(mid - d * L * grow, mid + d * L * grow, 2f * (1f - q), Rgba.White.WithAlpha(1f - q));
                for (int i = 0; i < 5; i++)
                {
                    float sa = a + Mathf.PI / 2f + (BbHash(i * 2.2f + fx.Pos.X) - 0.5f) * 2f, sp = 12f + 22f * BbHash(i * 4.4f + fx.Pos.Y);
                    V2 s0 = mid + V2.FromAngle(sa) * sp * BbEaseOut(q);
                    Line(s0, s0 - V2.FromAngle(sa) * 5f * (1f - q), 1.3f, Game.BbSpark.WithAlpha(1f - q));
                }
                return;
            }
            V2 nrm = (fx.Pos - c).IsZero ? new V2(1f, 0f) : (fx.Pos - c).Normalized();
            if (kind == 0 || kind == 2)
            {
                int n = kind == 2 ? 11 : 6;
                Rgba sc = kind == 2 ? new Rgba(0.75f, 0.9f, 1f) : Game.BbSpark;
                for (int i = 0; i < n; i++)
                {
                    float a = nrm.Angle + (BbHash(i * 2.9f + fx.Pos.X) - 0.5f) * (kind == 2 ? 2.4f : 1.8f), sp = (kind == 2 ? 26f : 18f) + 26f * BbHash(i * 4.1f + fx.Pos.Y);
                    V2 s0 = fx.Pos + V2.FromAngle(a) * sp * BbEaseOut(q);
                    Line(s0, s0 - V2.FromAngle(a) * 6f * (1f - q), 1.4f, sc.WithAlpha(1f - q));
                }
                Arc(fx.Pos, Mathf.Lerp(3f, kind == 2 ? 24f : 13f, BbEaseOut(q)), (kind == 2 ? 3f : 2f) * (1f - q), new Rgba(1f, 0.86f, 0.63f, 1f - q), 14);
                if (kind == 0)
                    return;
                if (q < 0.4f)  // the clang: a four-point star where the blow met the plate
                {
                    float k = q / 0.4f, L = 18f * (1f - k * 0.5f);
                    for (int i = 0; i < 4; i++)
                    {
                        V2 d = V2.FromAngle(nrm.Angle + Mathf.PI / 4f + i * Mathf.PI / 2f);
                        Line(fx.Pos - d * 2f, fx.Pos + d * L, 2.2f * (1f - k), Rgba.White.WithAlpha(1f - k));
                    }
                    if (fxOn)
                        GlowDisc(fx.Pos, 26f, Game.BbGlow, 0.8f * (1f - k));
                }
            }
            // soaked (and a melee blow): a mote spiralling into the boss
            V2 boss = has ? BbBossAt(owner) : fx.Pos;
            float e = q * q, sw = Mathf.Sin(Mathf.PI * q) * 10f * (BbHash(fx.Pos.X) < 0.5f ? 1f : -1f);
            V2 dd = boss - fx.Pos;
            V2 side = dd.IsZero ? V2.Zero : dd.Normalized().Orthogonal();
            V2 at = fx.Pos + dd * e + side * sw;
            var col = new Rgba(Mathf.Lerp(1f, 0.5f, q), Mathf.Lerp(0.78f, 0.84f, q), Mathf.Lerp(0.47f, 1f, q));
            V2 at2 = fx.Pos + dd * (e * 0.8f) + side * sw * 0.9f;  // a short tail behind it
            Line(at2, at, 1.6f * (1f - 0.4f * q), col.WithAlpha(0.6f));
            Circle(at, 2.6f * (1f - 0.4f * q), col, 8);
            if (fxOn)
                GlowDisc(at, 9f, col, 0.9f);
            if (kind == 1)
                Arc(fx.Pos, Mathf.Lerp(2f, 12f, BbEaseOut(q)), 2f * (1f - q), Game.BbGlow.WithAlpha(1f - q), 10);
        }
    }
}
