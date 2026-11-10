using UnityEngine;

namespace Arena
{
    /// The Arc Knuckles, worn and thrown (Docs/Mockups/gauntlets.html, the Arc Coil look; the hits are timed in Core/ArcKnuckles.cs).
    /// The Knight's model leaves its arms out with them; this draws both arms and a coiled blue gauntlet on each fist:
    ///   - held up in a guard, breathing, the coils pulsing (DrawKnuckleIdle, from the player loop);
    ///   - each punch (FxKind.KnuckleStep, its Radius = the puncher's id * 10 + the step), played from its age off the same
    ///     keyframes as the mockup, with a blue streak behind the fist that's moving fast and small arcs where it lands
    ///     (DrawKnuckleStepFx); the Twin Hammer's and the Counter Hammer's shockwave, cracks and dust;
    ///   - the Rocket Dash (FxKind.KnuckleDash): the lead fist thrown forward, the rear gauntlet's thruster, afterimages and speed
    ///     lines along the path (DrawKnuckleDashFx); a ring under your own feet fills as it comes back;
    ///   - five diamonds over the head for the chain, gold while a perfect parry has the Counter Hammer armed (DrawKnucklePips).
    public partial class ArenaApp
    {
        static readonly Rgba KnPlate = new Rgba(0.204f, 0.267f, 0.361f), KnPlateD = new Rgba(0.129f, 0.169f, 0.227f), KnCuff = new Rgba(0.11f, 0.141f, 0.196f),
            KnBand = new Rgba(0.635f, 0.894f, 1f), KnStud = new Rgba(0.933f, 0.98f, 1f), KnInk = new Rgba(0.03f, 0.035f, 0.04f),
            KnSleeve = new Rgba(0.29f, 0.32f, 0.38f), KnArc = new Rgba(0.48f, 0.83f, 1f), KnArcHi = new Rgba(0.93f, 0.98f, 1f), KnGold = new Rgba(1f, 0.82f, 0.29f);

        /// A pose: each fist's place in front of the body (x along the aim, y across, z raised), the body's lunge and twist, the coils' glow.
        struct KnPose
        {
            public float Lx, Ly, Lz, Rx, Ry, Rz, Dx, Tw, G;
            public KnPose(float lx, float ly, float lz, float rx, float ry, float rz, float dx, float tw, float g)
            { Lx = lx; Ly = ly; Lz = lz; Rx = rx; Ry = ry; Rz = rz; Dx = dx; Tw = tw; G = g; }
            public static KnPose Lerp(KnPose a, KnPose b, float k) => new KnPose(
                a.Lx + (b.Lx - a.Lx) * k, a.Ly + (b.Ly - a.Ly) * k, a.Lz + (b.Lz - a.Lz) * k, a.Rx + (b.Rx - a.Rx) * k, a.Ry + (b.Ry - a.Ry) * k,
                a.Rz + (b.Rz - a.Rz) * k, a.Dx + (b.Dx - a.Dx) * k, a.Tw + (b.Tw - a.Tw) * k, a.G + (b.G - a.G) * k);
        }

        static readonly KnPose KnRest = new KnPose(17f, -13f, 0f, 17f, 13f, 0f, 0f, 0f, 0f);

        // One key: its time and the easing into it (0 in-out, 1 out, 2 in, 3 linear).
        struct KnKey { public float T; public int E; public KnPose P; }

        static KnKey K(float t, int e, float lx, float ly, float lz, float rx, float ry, float rz, float dx, float tw, float g) =>
            new KnKey { T = t, E = e, P = new KnPose(lx, ly, lz, rx, ry, rz, dx, tw, g) };

        // The keyframes of each step (Jab, Cross, Hook, Uppercut, Twin Hammer; the Counter Hammer moves as the Twin Hammer).
        static readonly KnKey[][] KnSteps =
        {
            new[] { K(0f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0), K(0.05f, 0, 11, -12, 0, 17, 13, 0, 0, -0.08f, 0), K(0.10f, 1, 64, -5, 0, 17, 13, 0, 4, 0.15f, 0.3f),
                    K(0.15f, 0, 60, -5, 0, 17, 13, 0, 4, 0.15f, 0.2f), K(0.28f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0) },
            new[] { K(0f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0), K(0.06f, 0, 20, -10, 0, 9, 15, 0, 0, -0.25f, 0), K(0.12f, 1, 14, -15, 0, 72, 3, 0, 8, 0.35f, 0.3f),
                    K(0.17f, 0, 14, -15, 0, 68, 3, 0, 8, 0.3f, 0.2f), K(0.32f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0) },
            new[] { K(0f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0), K(0.07f, 0, 4, -34, 0, 17, 13, 0, 0, -0.35f, 0), K(0.12f, 2, 38, -44, 0, 17, 13, 0, 0, -0.05f, 0.2f),
                    K(0.16f, 3, 64, -12, 0, 17, 13, 0, 6, 0.3f, 0.4f), K(0.20f, 1, 54, 22, 0, 17, 13, 0, 6, 0.55f, 0.3f), K(0.36f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0) },
            new[] { K(0f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0), K(0.08f, 0, 18, -12, 0, 12, 18, 0, -2, -0.2f, 0), K(0.15f, 1, 18, -12, 0, 56, 4, 0.9f, 8, 0.3f, 0.5f),
                    K(0.22f, 0, 18, -12, 0, 52, 2, 1f, 8, 0.3f, 0.4f), K(0.42f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0) },
            new[] { K(0f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0), K(0.18f, 0, 8, -7, 1.25f, 8, 7, 1.25f, -4, 0, 0.6f), K(0.26f, 2, 44, -9, 0, 44, 9, 0, 10, 0, 1f),
                    K(0.40f, 0, 42, -9, 0, 42, 9, 0, 10, 0, 0.3f), K(0.62f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0) },
        };

        // The Rocket Dash: a flick back, the lead fist out in front for the flight, the hit, then back to the guard.
        static readonly KnKey[] KnDash =
        {
            K(0f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0), K(0.04f, 0, 18, -12, 0, 6, 16, 0, 0, -0.3f, 0.4f), K(0.08f, 1, 12, -14, 0, 40, 2, 0, 0, 0.2f, 0.8f),
            K(0.2f, 3, 12, -14, 0, 42, 2, 0, 0, 0.2f, 0.8f), K(0.24f, 1, 14, -14, 0, 46, 2, 0, 0, 0.25f, 1f), K(0.55f, 0, 17, -13, 0, 17, 13, 0, 0, 0, 0),
        };
        const float KnDashPose = 0.55f;

        static KnPose KnuckleDashAt(float c)
        {
            if (c <= 0f)
                return KnDash[0].P;
            for (int i = 1; i < KnDash.Length; i++)
                if (c <= KnDash[i].T)
                    return KnPose.Lerp(KnDash[i - 1].P, KnDash[i].P, KnEase(KnDash[i].E, (c - KnDash[i - 1].T) / (KnDash[i].T - KnDash[i - 1].T)));
            return KnDash[KnDash.Length - 1].P;
        }

        /// The Rocket Dash effect of this player still playing its pose (the newest), or null.
        Effect KnuckleDashFxOf(int id)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.KnuckleDash && Mathf.RoundToInt(fx.Radius) == id && fx.T < KnDashPose && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// The Rocket Dash, from its effect's age: the pose, the thruster, the afterimages along the path.
        void DrawKnuckleDashFx(Effect fx)
        {
            int id = Mathf.RoundToInt(fx.Radius);
            Player owner;
            game.Players.TryGetValue(id, out owner);
            float c = fx.T, t = Time.time, aim = (fx.Pos2 - fx.Pos).Angle, fly = Mathf.Clamp01(c / Game.KnuckleDashTime);
            V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
            V2 o = owner != null ? owner.Disp : fx.Pos + (fx.Pos2 - fx.Pos) * fly;
            // afterimages and speed lines along the path while it flies (and a moment after)
            if (c < Game.KnuckleDashTime + 0.2f)
            {
                float fade = Mathf.Clamp01(1f - (c - Game.KnuckleDashTime) / 0.2f);
                for (int j = 1; j <= 5; j++)
                {
                    float k = fly - j * 0.16f;
                    if (k < 0f)
                        break;
                    V2 at = fx.Pos + (fx.Pos2 - fx.Pos) * k;
                    float a = 0.22f * (6 - j) / 5f * fade;
                    Circle(at, 16f, KnArc.WithAlpha(a * 0.5f), 18);
                    Arc(at, 16f, 1.4f, KnArc.WithAlpha(a), 18);
                }
                for (int i = 0; i < 6; i++)
                {
                    float y = (KnHash(i + Mathf.FloorToInt(t * 30f)) - 0.5f) * 36f, x = -20f - KnHash(i * 3) * 60f;
                    V2 a0 = o + dir * x + side * y;
                    Line(a0, a0 - dir * 22f, 1f, new Rgba(0.9f, 0.9f, 0.9f, 0.25f * fade));
                }
            }
            if (owner != null && KnuckleDashFxOf(id) != fx)
                return;
            if (c >= KnDashPose)
                return;
            var m = KnuckleDashAt(c);
            DrawKnucklePose(o, aim, m, owner != null ? owner.Def.Color : new Rgba(0.45f, 0.5f, 0.6f), t, false);
            if (c > 0.03f && c < Game.KnuckleDashTime + 0.04f)  // the lead gauntlet's thruster, firing back along the arm (over it)
            {
                V2 f = KnuckleFist(o, aim, m, true) - dir * 9f;
                float fl = 24f + 8f * Mathf.Sin(t * 95f);
                SetColor(KnArc.WithAlpha(0.85f));
                Tri(f + side * 7f, f - dir * fl, f - side * 7f);
                SetColor(KnArcHi);
                Tri(f + side * 2.5f, f - dir * fl * 0.55f, f - side * 2.5f);
            }
            if (owner != null)
                DrawKnucklePips(owner, t);
        }

        static float KnEase(int e, float k)
        {
            switch (e)
            {
                case 1: return 1f - (1f - k) * (1f - k) * (1f - k);
                case 2: return k * k * k;
                case 3: return k;
                default: return k < 0.5f ? 2f * k * k : 1f - (-2f * k + 2f) * (-2f * k + 2f) / 2f;
            }
        }

        /// The pose `c` seconds into a step (1-6).
        static KnPose KnuckleAt(int step, float c)
        {
            var keys = KnSteps[Mathf.Min(step, 5) - 1];
            if (c <= 0f)
                return keys[0].P;
            for (int i = 1; i < keys.Length; i++)
                if (c <= keys[i].T)
                    return KnPose.Lerp(keys[i - 1].P, keys[i].P, KnEase(keys[i].E, (c - keys[i - 1].T) / (keys[i].T - keys[i - 1].T)));
            return keys[keys.Length - 1].P;
        }

        /// The punch effect of this player whose punch is still moving (the newest), or null.
        Effect KnuckleFxOf(int id)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.KnuckleStep || Mathf.RoundToInt(fx.Radius) / 10 != id)
                    continue;
                int step = Mathf.RoundToInt(fx.Radius) % 10;
                if (step < 1 || step > 6 || fx.T >= Game.KnuckleDur[step - 1])
                    continue;
                if (best == null || fx.T < best.T)
                    best = fx;
            }
            return best;
        }

        /// The fists up in a guard, when no punch is moving.
        void DrawKnuckleIdle(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.ArcKnuckles || KnuckleFxOf(p.Id) != null || KnuckleDashFxOf(p.Id) != null)
                return;
            float b = Mathf.Sin(t * 2.6f);
            var m = KnRest;
            m.Lx += 0.8f * b; m.Rx -= 0.8f * b; m.Ly -= 0.5f * b; m.Ry += 0.5f * b;
            m.G = p.KnuckleArmed ? 0.6f + 0.3f * Mathf.Sin(t * 12f) : 0f;
            DrawKnucklePose(p.Disp, p.Aim, m, p.Def.Color, t, p.KnuckleArmed);
            DrawKnucklePips(p, t);
        }

        /// One punch, from its effect's age: the pose while it moves; a hammer's shockwave for as long as the effect lasts.
        void DrawKnuckleStepFx(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius), id = code / 10, step = code % 10;
            if (step < 1 || step > 6)
                return;
            Player owner;
            game.Players.TryGetValue(id, out owner);
            V2 o = owner != null ? owner.Disp : fx.Pos;
            float aim = (fx.Pos2 - fx.Pos).Angle, c = fx.T, t = Time.time;
            bool gold = step == 6;
            if (step >= 5)
                DrawKnuckleQuake(fx.Pos + V2.FromAngle(aim) * Game.KnuckleHammerAt, c - Game.KnuckleLand[step - 1], gold ? Game.KnuckleCounterR : Game.KnuckleHammerR, gold, id);
            if (c >= Game.KnuckleDur[step - 1] || (owner != null && (KnuckleFxOf(id) != fx || KnuckleDashFxOf(id) != null)))
                return;  // (a dash takes over from a punch)  // the punch is over, or a newer one by the same player has taken over
            // the streak behind a fist that moves fast
            for (int side = 0; side < 2; side++)
            {
                V2 prev = default(V2);
                float run = 0f;
                for (int i = 0; i <= 7; i++)
                {
                    float ci = c - i * 0.011f;
                    if (ci < 0f)
                        break;
                    V2 at = KnuckleFist(o, aim, KnuckleAt(step, ci), side == 1);
                    if (i > 0)
                        run += at.DistanceTo(prev);
                    prev = at;
                }
                if (run < 26f)
                    continue;
                float a = Mathf.Clamp01((run - 26f) / 50f);
                for (int i = 1; i <= 7; i++)
                {
                    float c0 = c - (i - 1) * 0.011f, c1 = c - i * 0.011f;
                    if (c1 < 0f)
                        break;
                    V2 p0 = KnuckleFist(o, aim, KnuckleAt(step, c0), side == 1), p1 = KnuckleFist(o, aim, KnuckleAt(step, c1), side == 1);
                    float k = 1f - i / 8f;
                    Line(p0, p1, 11f * k + 1f, (gold ? KnGold : KnArc).WithAlpha(0.3f * a * k));
                    Line(p0, p1, 4f * k + 0.5f, KnArcHi.WithAlpha(0.5f * a * k));
                }
            }
            var m = KnuckleAt(step, c);
            DrawKnucklePose(o, aim, m, owner != null ? owner.Def.Color : new Rgba(0.45f, 0.5f, 0.6f), t, gold);
            // small arcs off the fist that lands
            float u = c - Game.KnuckleLand[step - 1];
            if (u >= 0f && u < 0.16f && step < 5)
            {
                bool right = step == 2 || step == 4;
                V2 f = KnuckleFist(o, aim, m, right);
                KnuckleSparks(f, u / 0.16f, code * 7 + (int)(fx.Pos.X * 0.1f), 1f);
            }
            if (owner != null)
                DrawKnucklePips(owner, t);
        }

        /// Where a fist is drawn (raised fists rise up the screen).
        static V2 KnuckleFist(V2 o, float aim, KnPose m, bool right)
        {
            float x = right ? m.Rx : m.Lx, y = right ? m.Ry : m.Ly, z = right ? m.Rz : m.Lz;
            V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
            return o + dir * (x + m.Dx) + side * y + new V2(0f, -z * 12f);
        }

        /// Both arms and gauntlets in a pose (the body and head are the model's).
        void DrawKnucklePose(V2 o, float aim, KnPose m, Rgba armCol, float t, bool gold)
        {
            V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
            float body = aim + m.Tw;
            for (int k = 0; k < 2; k++)
            {
                bool right = k == 1;
                float z = right ? m.Rz : m.Lz;
                V2 ground = o + dir * ((right ? m.Rx : m.Lx) + m.Dx) + side * (right ? m.Ry : m.Ly);
                if (z > 0.04f)
                    Circle(ground, 9f * (1f + z * 0.4f), new Rgba(0f, 0f, 0f, 0.3f - 0.08f * z), 14);  // its shadow on the floor
            }
            // the lower fist first, so a raised one is on top
            bool rightFirst = m.Rz <= m.Lz;
            for (int n = 0; n < 2; n++)
            {
                bool right = n == 0 ? rightFirst : !rightFirst;
                V2 s = o + V2.FromAngle(body + (right ? 1.48f : -1.48f)) * 12f, f = KnuckleFist(o, aim, m, right);
                Line(s, f, 7.5f, KnInk);
                Line(s, f, 5f, armCol.Mix(KnSleeve, 0.35f));
                Circle(s, 6.2f, KnInk, 12);
                Circle(s, 5f, KnSleeve.Mix(armCol, 0.25f), 12);
                V2 d = f - s;
                float ang = d.IsZero ? aim : d.Angle;
                DrawGauntlet(f, ang, right ? m.Rz : m.Lz, m.G, t, right ? 1 : -1, gold);
            }
        }

        /// One coiled gauntlet: the cuff with its three coils, the plated fist, the glowing knuckle band and its studs.
        void DrawGauntlet(V2 at, float ang, float z, float glow, float t, int side, bool gold)
        {
            float s = 1f + z * 0.42f;
            Rgba band = gold ? KnGold : KnBand;
            if (glow > 0.01f)
            {
                Circle(at, 20f * s, band.WithAlpha(0.12f * glow), 18);
                Circle(at, 13f * s, band.WithAlpha(0.18f * glow), 16);
            }
            V2 dir = V2.FromAngle(ang) * s, nrm = V2.FromAngle(ang).Orthogonal() * s;
            KnBox(at, dir, nrm, -14f, -5f, -5.5f, 5.5f, KnInk, 1.2f);            // the cuff's outline
            KnBox(at, dir, nrm, -14f, -5f, -5.5f, 5.5f, KnCuff, 0f);
            for (int i = 0; i < 3; i++)  // the coils, pulsing
            {
                float x = -12f + i * 2.5f, a = 0.55f + 0.45f * Mathf.Sin(t * 20f + x + side);
                Line(at + dir * x - nrm * 5.5f, at + dir * x + nrm * 5.5f, 1.1f * s, band.WithAlpha(a));
            }
            // the fist: a round plated knuckle, darker at the back, the glowing band of knuckles round its front, three studs
            V2 fc = at + dir * 1.5f;
            float fr = 6.8f * s;
            Circle(fc, fr + 1.3f, KnInk, 16);
            Circle(fc, fr, KnPlate, 16);
            Circle(fc - dir * 2.2f, fr * 0.62f, KnPlateD, 12);
            Arc(fc, fr - 1.2f * s, 4.2f * s, band.WithAlpha(0.3f + 0.3f * glow), 14, ang - 1.25f, 2.5f);  // the band's glow
            Arc(fc, fr - 1.6f * s, 2.6f * s, band, 14, ang - 1.1f, 2.2f);
            for (int i = -1; i <= 1; i++)
                Circle(fc + V2.FromAngle(ang + i * 0.62f) * (fr - 1.6f * s), 1.05f * s, KnStud, 6);
        }

        /// A box along `dir` (x0-x1) and across it (y0-y1) from `at`, grown by `grow` all round.
        void KnBox(V2 at, V2 dir, V2 nrm, float x0, float x1, float y0, float y1, Rgba col, float grow)
        {
            SetColor(col);
            x0 -= grow; x1 += grow; y0 -= grow; y1 += grow;
            Quad(at + dir * x0 + nrm * y0, at + dir * x1 + nrm * y0, at + dir * x1 + nrm * y1, at + dir * x0 + nrm * y1);
        }

        /// Small lightning arcs, a ring and a flash where a fist lands (`u` 0-1 over the burst).
        void KnuckleSparks(V2 at, float u, int seed, float size)
        {
            float a = 1f - u;
            Circle(at, (9f * a + 2f) * size, KnArcHi.WithAlpha(a), 14);
            Arc(at, (8f + 20f * (1f - a * a * a)) * size, (3f * a + 0.5f) * size, KnArc.WithAlpha(0.9f * a), 22);
            for (int i = 0; i < 3; i++)
            {
                float ang = KnHash(seed * 3 + i) * MathX.Tau;
                V2 p = at;
                for (int k = 0; k < 4; k++)
                {
                    ang += (KnHash(seed + i * 7 + k) - 0.5f) * 1.4f;
                    V2 q = p + V2.FromAngle(ang) * 7f * size;
                    Line(p, q, 1.3f, KnArcHi.WithAlpha(a));
                    p = q;
                }
            }
        }

        static float KnHash(int i)
        {
            float s = Mathf.Sin(i * 127.1f + 311.7f) * 43758.547f;
            return s - Mathf.Floor(s);
        }

        /// The Twin Hammer's shockwave (`u` seconds after it landed): rings, cracks in the floor, dust; gold for the Counter Hammer.
        void DrawKnuckleQuake(V2 c, float u, float r, bool gold, int seed)
        {
            if (u < 0f || u > 1.2f)
                return;
            Rgba col = gold ? KnGold : KnArc, hi = gold ? new Rgba(1f, 0.96f, 0.84f) : KnArcHi;
            float fade = Mathf.Clamp01(1f - (u - 0.4f) / 0.8f);
            for (int i = 0; i < 10; i++)  // the cracks
            {
                float a = i / 10f * MathX.Tau + KnHash(seed * 10 + i) * 0.5f, len = r * (0.35f + 0.25f * KnHash(i + 3)) * Mathf.Clamp01(u / 0.12f);
                V2 p = c;
                for (int k = 0; k < 4; k++)
                {
                    a += (KnHash(i * 9 + k) - 0.5f) * 0.6f;
                    V2 q = p + V2.FromAngle(a) * len / 4f;
                    Line(p, q, 1.6f, new Rgba(0.03f, 0.035f, 0.05f, 0.9f * fade));
                    Line(p, q, 0.8f, col.WithAlpha(0.6f * fade));
                    p = q;
                }
            }
            float v = Mathf.Clamp01(u / 0.32f), e = 1f - (1f - v) * (1f - v) * (1f - v);
            if (v < 1f)
            {
                Arc(c, r * e, 7f * (1f - v) + 1f, col.WithAlpha((1f - v) * 0.9f), 48);
                Arc(c, Mathf.Max(0f, r * e - 4f), 2f, hi.WithAlpha((1f - v) * 0.8f), 48);
                Circle(c, r * e, col.WithAlpha(0.07f * (1f - v)), 48);
            }
            float v2 = Mathf.Clamp01((u - 0.08f) / 0.4f);
            if (v2 > 0f && v2 < 1f)
                Arc(c, r * 0.75f * (1f - (1f - v2) * (1f - v2) * (1f - v2)), 1.5f, col.WithAlpha((1f - v2) * 0.5f), 40);
            if (u < 0.6f)
                for (int i = 0; i < 14; i++)  // dust at the ring's edge
                {
                    float a = i / 14f * MathX.Tau, d = r * (0.75f + 0.3f * Mathf.Clamp01(u / 0.6f));
                    Circle(c + V2.FromAngle(a) * d, (4f + 6f * u) * (1f - u / 0.6f), new Rgba(0.59f, 0.55f, 0.51f, 0.18f * (1f - u / 0.6f)), 8);
                }
            if (u < 0.1f)
                Circle(c, 26f * (1f - u / 0.1f), hi.WithAlpha(0.9f * (1f - u / 0.1f)), 18);
            if (u < 0.22f)
                for (int i = 0; i < 4; i++)
                    KnuckleSparks(c + V2.FromAngle(i * 1.57f + seed) * r * 0.4f * e, u / 0.22f, seed * 13 + i, 0.7f);
        }

        /// Five diamonds over the head: lit for each punch thrown in this chain; all gold while the Counter Hammer is armed or landing.
        void DrawKnucklePips(Player p, float t)
        {
            int step = Game.KnuckleStepOf(p.Combo);
            bool gold = p.KnuckleArmed || step == 6;
            if (p.Id == game.MyId && game.KnuckleDashCd > 0f)  // the Rocket Dash coming back: a ring filling under your feet
            {
                float k = 1f - game.KnuckleDashCd / Game.KnuckleDashCooldown;
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, KnArc.WithAlpha(0.6f), 28, -MathX.Pi / 2f, MathX.Tau * k);
            }
            if (step == 0 && !gold)
                return;
            V2 c = p.Disp + new V2(0f, -p.Def.BodyR - 16f);
            for (int i = 0; i < 5; i++)
            {
                V2 at = c + new V2((i - 2f) * 9f, 0f);
                float r = i == 4 ? 3.8f : 3f;
                bool lit = gold || i < step;
                Rgba col = gold ? KnGold.WithAlpha(p.KnuckleArmed ? 0.65f + 0.35f * Mathf.Sin(t * 14f) : 1f) : lit ? KnArc : new Rgba(1f, 1f, 1f, 0.15f);
                Polygon(at, r + 1f, 4, 0f, new Rgba(0f, 0f, 0f, 0.55f));
                Polygon(at, r, 4, 0f, col);
            }
        }
    }
}
