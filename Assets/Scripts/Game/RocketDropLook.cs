using UnityEngine;

namespace Arena
{
    /// Rocket Drop (Core/RocketDrop.cs; mockup Docs/Mockups/rocket-drop.html, look B with the Thermobaric warhead), all worked out
    /// from replicated state:
    ///   the pod     (ZoneKind.RocketDrop, its timer the phase) a red lock-on ring with four gold brackets turning round it, the
    ///               steel pod streaking in on a flame trail with its shadow growing, the slam (orange shockwave, dust, a crater),
    ///               its four petals opening and the launcher rising on its rack, a pulsing gold ring (a gold chevron over it for its owner);
    ///               the petals close and fade as it folds.
    ///   the soldier (Player.RocketT) the olive launcher on the right shoulder, both hands on it, kicking back with each rocket
    ///               and showing a fresh warhead once reloaded; a gold time ring at the feet, a gold ∞ chip over the head, and for
    ///               your own the blast's reach dashed round the cursor.
    ///   a rocket    (Defs.RocketShell) an olive body with fins and an orange nose, a flame and a grey smoke trail.
    ///   the blast   (FxKind.RocketBlast) a white flash, a fireball swelling to the reach, a shockwave ring, debris and smoke.
    ///   the fire    (ZoneKind.RocketFire) a scorched floor under flickering flames and embers, a dashed orange rim, dying down.
    public partial class ArenaApp
    {
        static readonly Rgba RdOlive = new Rgba(0.33f, 0.38f, 0.18f), RdOliveDark = new Rgba(0.2f, 0.24f, 0.11f), RdOliveLight = new Rgba(0.49f, 0.52f, 0.34f);
        static readonly Rgba RdOrange = Game.RocketOrange, RdRed = new Rgba(1f, 0.3f, 0.25f), RdHot = new Rgba(1f, 0.86f, 0.5f);
        static readonly Rgba RdSteel = new Rgba(0.36f, 0.38f, 0.41f), RdSteelDark = new Rgba(0.15f, 0.16f, 0.17f), RdSmoke = new Rgba(0.6f, 0.58f, 0.54f);
        const float RdRecoil = 0.2f, RdReload = 0.35f;
        static readonly V2 RdFall = new V2(-260f, -620f);  // where the pod comes in from, relative to its landing spot

        // ---------------- the pod ----------------

        void DrawRocketDrop(Zone z, float t)
        {
            float age = Game.RocketAge(z);
            V2 c = z.Pos;
            if (age < Game.RocketCallIn)
            {
                float f = age / Game.RocketCallIn, pulse = 0.5f + 0.5f * Mathf.Sin(t * 16f);
                Circle(c, Game.RocketSlamR, RdRed.WithAlpha(0.07f + 0.12f * f), 40);
                Arc(c, Game.RocketSlamR, 2.5f, RdRed.WithAlpha(0.4f + 0.45f * pulse), 48);
                Arc(c, Game.RocketSlamR * (1f - f) + 8f, 1.5f, RdRed.WithAlpha(0.6f * f), 32);  // closing in on the spot
                for (int i = 0; i < 4; i++)  // gold brackets turning round the rim
                    Arc(c, Game.RocketSlamR - 12f * f, 3f, Rgba.Gold.WithAlpha(0.9f), 6, t * 2f + i * MathX.Pi / 2f - 0.25f, 0.5f);
                Line(c + new V2(-7f, 0f), c + new V2(7f, 0f), 1.5f, RdRed.WithAlpha(0.8f));
                Line(c + new V2(0f, -7f), c + new V2(0f, 7f), 1.5f, RdRed.WithAlpha(0.8f));
                float q = Mathf.Clamp01((age - 0.3f) / (Game.RocketCallIn - 0.3f));
                if (age > 0.3f)
                {
                    Circle(c, 10f + 18f * q, new Rgba(0f, 0f, 0f, 0.2f + 0.4f * q), 20);  // the shadow tightening
                    V2 dir = (-RdFall).Normalized();
                    V2 at = c + RdFall * (1f - q);
                    float s = 1f + 0.8f * (1f - q);
                    if (fxOn)
                        for (int i = 1; i <= 6; i++)  // the re-entry trail
                            GlowDisc(at - dir * (i * 20f * s), (18f - i * 2f) * s, RdOrange, 0.5f * (1f - i / 7f));
                    PodHex(at, 22f * s, 0.3f, RdSteel, 1f);
                    Circle(at, 7f * s, RdOrange, 12);
                }
                return;
            }

            float since = age - Game.RocketCallIn, fold = Mathf.Clamp01(z.T / Game.RocketFold), alpha = fold;
            if (since < 0.5f)  // the slam: an orange shockwave and dust
            {
                float k = since / 0.5f;
                Arc(c, 30f + k * (Game.RocketSlamR + 30f), 2f + 7f * (1f - k), RdOrange.WithAlpha(1f - k), 48);
                if (fxOn && since < 0.12f)
                    GlowDisc(c, 70f * (1f - since / 0.12f) + 20f, RdHot, 0.8f);
                if (!fxReduced)
                    for (int i = 0; i < 18; i++)
                    {
                        float a = MathX.Tau * i / 18f + i * 0.37f;
                        Circle(c + V2.FromAngle(a) * (24f + k * (60f + 30f * Mathf.Sin(i * 2.1f))), 5f * (1f - k) + 1f, new Rgba(0.55f, 0.5f, 0.42f, 0.7f * (1f - k)), 8);
                    }
            }
            Circle(c, 44f, new Rgba(0.05f, 0.04f, 0.03f, 0.55f * alpha), 28);  // the crater
            float open = Mathf.Clamp01(since / Game.RocketOpen);
            open = 1f - (1f - open) * (1f - open);
            if (z.T < Game.RocketFold)
                open *= fold;
            for (int i = 0; i < 4; i++)  // the petals
            {
                float a = MathX.Pi / 4f + i * MathX.Pi / 2f;
                V2 fw = V2.FromAngle(a), side = new V2(-fw.Y, fw.X);
                V2 b = c + fw * (6f + 16f * open);
                Quad(b + side * -20f, b + side * 20f, b + fw * 18f + side * 13f, b + fw * 18f + side * -13f, RdSteel.WithAlpha(alpha));
                Line(b + side * -20f, b + side * 20f, 2f, RdSteelDark.WithAlpha(alpha));
                Quad(b + fw * 6f + side * -5f, b + fw * 6f + side * 5f, b + fw * 12f, b + fw * 12f, RdOrange.WithAlpha(alpha));  // the chevron
            }
            Circle(c, 20f, RdSteelDark.WithAlpha(alpha), 20);
            if (open < 0.3f)
                PodHex(c, 22f * (1f - open), 0.3f, RdSteel, alpha);
            if (Game.RocketReady(z) || since < Game.RocketOpen && open > 0.3f && z.T > Game.RocketFold)
            {
                float rise = Mathf.Clamp01((open - 0.3f) / 0.7f);
                DrawLauncher(c + new V2(0f, 6f * (1f - rise)), 0f, 0.85f, true, 1f);
            }
            if (Game.RocketReady(z))
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f);
                Arc(c, Game.RocketTakeR, 2f, Rgba.Gold.WithAlpha(0.5f + 0.4f * pulse), 40);
                if (game != null && z.Owner == game.MyId)  // a gold chevron bobbing over it: yours, step on it
                {
                    V2 tip = c + new V2(0f, -Game.RocketTakeR - 8f - 4f * pulse);
                    SetColor(Rgba.Gold.WithAlpha(0.95f));
                    Tri(tip + new V2(-9f, -9f), tip + new V2(0f, 0f), tip + new V2(0f, -4f));
                    Tri(tip + new V2(9f, -9f), tip + new V2(0f, -4f), tip + new V2(0f, 0f));
                }
            }
        }

        /// A hexagonal steel pod seen from above, with a dark rim.
        void PodHex(V2 c, float r, float rot, Rgba col, float alpha)
        {
            SetColor(RdSteelDark.WithAlpha(alpha));
            for (int i = 0; i < 6; i++)
                Tri(c, c + V2.FromAngle(rot + MathX.Tau * i / 6f) * (r + 2.5f), c + V2.FromAngle(rot + MathX.Tau * (i + 1) / 6f) * (r + 2.5f));
            SetColor(col.WithAlpha(alpha));
            for (int i = 0; i < 6; i++)
                Tri(c, c + V2.FromAngle(rot + MathX.Tau * i / 6f) * r, c + V2.FromAngle(rot + MathX.Tau * (i + 1) / 6f) * r);
        }

        /// The launcher in world space (on its rack, or anywhere outside the model): a tube along `ang`, `s` its size.
        void DrawLauncher(V2 c, float ang, float s, bool loaded, float alpha)
        {
            V2 fw = V2.FromAngle(ang) * s, side = new V2(-fw.Y, fw.X);
            RdBox(c + new V2(3f, 4f), fw, side, -26f, 37f, 9f, new Rgba(0f, 0f, 0f, 0.3f * alpha));
            Quad(c + fw * -30f + side * -9f, c + fw * -24f + side * -7f, c + fw * -24f + side * 7f, c + fw * -30f + side * 9f, RdOliveDark.WithAlpha(alpha));  // the rear flare
            RdBox(c, fw, side, -24f, 34f, 7f, RdOlive.WithAlpha(alpha));
            RdBox(c + side * 4f, fw, side, -24f, 34f, 2.5f, RdOliveDark.WithAlpha(alpha));
            RdBox(c, fw, side, 18f, 23f, 7f, RdOrange.WithAlpha(alpha));  // the hazard bands
            RdBox(c, fw, side, -14f, -11f, 7f, RdOrange.WithAlpha(alpha));
            RdBox(c - side * 9f, fw, side, -4f, 8f, 2.5f, RdSteelDark.WithAlpha(alpha));  // the sight
            RdBox(c, fw, side, 32f, 37f, 8f, new Rgba(0.08f, 0.09f, 0.06f, alpha));  // the muzzle ring
            if (loaded)
                Quad(c + fw * 37f + side * -5f, c + fw * 37f + side * 5f, c + fw * 44f, c + fw * 44f, RdOrange.WithAlpha(alpha));  // a warhead in the tube
        }

        /// A box from x0 to x1 along fw, `hw` either side.
        void RdBox(V2 c, V2 fw, V2 side, float x0, float x1, float hw, Rgba col) =>
            Quad(c + fw * x0 - side * hw, c + fw * x1 - side * hw, c + fw * x1 + side * hw, c + fw * x0 + side * hw, col);

        // ---------------- the soldier carrying it ----------------

        /// Seconds since this player's newest rocket left the tube (large when none is in the air).
        float RocketSince(Player p)
        {
            float best = 99f;
            if (game == null)
                return best;
            foreach (var b in game.Bullets.Values)
                if (b.Kind == Defs.RocketShell && b.Owner == p.Id)
                    best = Mathf.Min(best, b.Life0 - b.Life);
            return best;
        }

        /// From DrawPlayerModel, in place of the weapon: both arms on the launcher (the tube itself is drawn over the head).
        void DrawRocketArms(Player p, ModelPal K, float ry)
        {
            float since = RocketSince(p), kick = since < RdRecoil ? (1f - since / RdRecoil) * 5f : 0f;
            MFrame(0f, 0f, 0f, 1f, false);
            ModelArm(K, 1f, -ry * 0.72f, 18f - kick, 5f);   // the front hand across on the grip
            ModelArm(K, 1f, ry * 0.72f, 4f - kick, 15f);    // the trigger hand
            MCirc(18f - kick, 5f, 2.8f, K.Sleeve, 8);
            MCirc(4f - kick, 15f, 2.8f, K.Sleeve, 8);
        }

        /// The launcher on the right shoulder, kicked back after a shot, a fresh warhead in it once reloaded.
        void DrawRocketTube(Player p)
        {
            float since = RocketSince(p), kick = since < RdRecoil ? (1f - since / RdRecoil) * 5f : 0f;
            MFrame(0f, 0f, 0f, 1f, false);
            float y = 10f, x0 = -30f - kick, x1 = 40f - kick, h = 6.5f;
            MPoly(RdOliveDark, x0 - 7f, y - h - 3f, x0, y - h, x0, y + h, x0 - 7f, y + h + 3f);  // the rear flare
            MRect(x0, y - h, x1 - x0, h * 2f, RdOliveDark);
            MRect(x0 + 1f, y - h + 1f, x1 - x0 - 2f, h * 2f - 2f, RdOlive);
            MRect(x0 + 1f, y + 2f, x1 - x0 - 2f, h - 3f, RdOliveDark);
            MRect(x1 - 13f, y - h + 1f, 4.5f, h * 2f - 2f, RdOrange);  // the hazard bands
            MRect(x0 + 11f, y - h + 1f, 3f, h * 2f - 2f, RdOrange);
            MRect(x0 + 24f, y - h - 4f, 12f, 4f, RdSteelDark);  // the sight
            MRect(x1, y - h - 1f, 5f, h * 2f + 2f, new Rgba(0.08f, 0.09f, 0.06f));  // the muzzle ring
            if (since > RdReload)
                MPoly(RdOrange, x1 + 5f, y - h + 1.5f, x1 + 12f, y, x1 + 5f, y + h - 1.5f);  // a fresh warhead
        }

        readonly System.Collections.Generic.Dictionary<int, float> rdFull = new System.Collections.Generic.Dictionary<int, float>();  // the launcher time each player started with (10 or 13 s)

        /// Under the soldier: the time ring; over the head the ∞ chip; for your own, the blast's reach round the cursor.
        void DrawRocketAura(Player p, float t)
        {
            if (!Game.RocketOn(p))
            {
                rdFull.Remove(p.Id);
                return;
            }
            float full;
            if (!rdFull.TryGetValue(p.Id, out full) || p.RocketT > full)
                full = rdFull[p.Id] = Mathf.Max(p.RocketT, Game.RocketTime > p.RocketT ? Game.RocketTime : Game.RocketTimeMastered);
            float r = p.Def.BodyR + 11f, left = Mathf.Clamp01(p.RocketT / full);
            bool warn = p.RocketT < 2.5f && Mathf.Repeat(t * 6f, 1f) < 0.5f;
            Arc(p.Disp, r, 3f, Rgba.Gold.WithAlpha(0.18f), 32);
            Arc(p.Disp, r, 3f, (warn ? RdRed : Rgba.Gold).WithAlpha(0.9f), 32, -MathX.Pi / 2f, MathX.Tau * left);
            V2 chip = p.Disp + new V2(0f, -p.Def.BodyR - 34f);  // the ∞ chip: infinite ammo
            Quad(chip + new V2(-15f, -9f), chip + new V2(15f, -9f), chip + new V2(15f, 9f), chip + new V2(-15f, 9f), new Rgba(0.08f, 0.07f, 0.04f, 0.75f));
            Arc(chip + new V2(-5f, 0f), 4.5f, 2f, Rgba.Gold, 16);
            Arc(chip + new V2(5f, 0f), 4.5f, 2f, Rgba.Gold, 16);
            if (game == null || p.Id != game.MyId || p.AimAt.IsZero)
                return;
            float d = Mathf.Clamp(p.AimAt.DistanceTo(p.Pos), Game.RocketMinRange, Game.RocketRange);
            V2 at = p.Disp + V2.FromAngle(p.Aim) * d;
            for (int i = 0; i < 24; i++)  // dashed: the blast's reach where this rocket would burst
                Arc(at, Game.RocketBlastR, 1.5f, RdOrange.WithAlpha(0.35f), 3, i * MathX.Tau / 24f + t * 0.4f, MathX.Tau / 48f);
        }

        // ---------------- a rocket in flight ----------------

        void DrawRocketShell(Bullet b, float t)
        {
            V2 dir = b.Vel.IsZero ? V2.FromAngle(0f) : b.Vel.Normalized();
            V2 side = new V2(-dir.Y, dir.X);
            if (fxOn)
            {
                TaperTrail(b, 12, 9f, RdSmoke, 0.45f);
                float flick = 0.8f + 0.2f * Mathf.Sin(t * 60f + b.Id);
                GlowDisc(b.Pos - dir * 18f, 16f * flick, RdOrange, 0.7f);
                GlowDisc(b.Pos - dir * 15f, 7f, RdHot, 0.9f);
            }
            Quad(b.Pos - dir * 16f - side * 10f, b.Pos - dir * 8f - side * 5f, b.Pos - dir * 8f + side * 5f, b.Pos - dir * 16f + side * 10f, RdOliveDark);  // the fins
            RdBox(b.Pos, dir, side, -14f, 10f, 5.5f, RdOliveLight);
            RdBox(b.Pos + side * 3f, dir, side, -14f, 10f, 1.8f, RdOliveDark);
            RdBox(b.Pos, dir, side, 2f, 4f, 5.5f, RdHot);
            Quad(b.Pos + dir * 10f - side * 5.5f, b.Pos + dir * 10f + side * 5.5f, b.Pos + dir * 21f, b.Pos + dir * 21f, RdOrange);  // the nose
        }

        // ---------------- the blast ----------------

        /// FxKind.RocketBlast: Pos the burst, Radius its reach.
        void DrawRocketBlastFx(Effect fx)
        {
            float v = fx.T, R = fx.Radius, k = Mathf.Clamp01(v / Mathf.Max(0.1f, fx.Dur));
            V2 c = fx.Pos;
            if (!fxOn)
            {
                Circle(c, R * (0.4f + 0.6f * Mathf.Clamp01(v / 0.2f)), RdOrange.WithAlpha(0.35f * (1f - k)), 32);
                return;
            }
            if (v < 0.12f)
                GlowDisc(c, R * 0.75f * (1f - v / 0.12f) + 24f, Rgba.White, 0.9f);
            float grow = Mathf.Clamp01(v / 0.22f), fr = R * (0.3f + 0.7f * (1f - (1f - grow) * (1f - grow) * (1f - grow)));
            float fa = v < 0.22f ? 1f : Mathf.Clamp01(1f - (v - 0.22f) / 0.55f);
            GlowDisc(c, fr, RdOrange, 0.65f * fa);
            GlowDisc(c, fr * 0.62f, new Rgba(1f, 0.75f, 0.3f), 0.75f * fa);
            GlowDisc(c, fr * 0.3f, RdHot, 0.85f * fa * fa);
            float sw = Mathf.Clamp01(v / 0.4f), sr = R * 1.1f * (1f - (1f - sw) * (1f - sw)), sa = Mathf.Clamp01(1f - v / 0.45f);
            if (sa > 0f)
                Arc(c, sr, 1.5f + 6f * sa, new Rgba(1f, 0.93f, 0.8f, 0.7f * sa), 56);
            if (v < 0.5f)
                for (int i = 0; i < 24; i++)  // the reach, dashed, for a moment
                    Arc(c, R, 2f, RdOrange.WithAlpha(0.7f * (1f - v / 0.5f)), 3, i * MathX.Tau / 24f, MathX.Tau / 40f);
            int seed = Mathf.RoundToInt(c.X * 3.1f + c.Y * 1.7f);
            if (!fxReduced)
            {
                for (int i = 0; i < 20; i++)  // debris flung out
                {
                    float h1 = Mathf.Repeat(Mathf.Sin((seed + i) * 12.9898f) * 43758.55f, 1f), h2 = Mathf.Repeat(Mathf.Sin((seed + i) * 78.233f) * 12543.7f, 1f);
                    V2 d = V2.FromAngle(h1 * MathX.Tau);
                    float out1 = R * (0.4f + 0.7f * h2) * (1f - (1f - Mathf.Clamp01(v / 0.5f)) * (1f - Mathf.Clamp01(v / 0.5f)));
                    Line(c + d * out1, c + d * (out1 + 6f), 3f, new Rgba(0.16f, 0.15f, 0.13f, 1f - k));
                }
                for (int i = 0; i < 10; i++)  // smoke rolling out after the fire
                {
                    float h = Mathf.Repeat(Mathf.Sin((seed + i * 7) * 3.733f) * 9631.1f, 1f);
                    float sk = Mathf.Clamp01((v - 0.15f) / 0.75f);
                    if (sk <= 0f)
                        continue;
                    V2 d = V2.FromAngle(MathX.Tau * i / 10f + h);
                    Circle(c + d * R * (0.3f + 0.45f * sk), 14f + 20f * sk, RdSmoke.WithAlpha(0.35f * (1f - sk)), 14);
                }
            }
        }

        // ---------------- the burning ground ----------------

        void DrawRocketFire(Zone z, float t)
        {
            V2 c = z.Pos;
            float R = z.Radius, fade = Mathf.Clamp01(z.T / 0.6f) * Mathf.Clamp01((z.Duration - z.T) / 0.15f + 0.3f);
            Circle(c, R * 0.85f, new Rgba(0.05f, 0.03f, 0.02f, 0.45f * Mathf.Clamp01(z.T / 0.6f)), 32);  // the scorch
            if (fxOn)
                GlowDisc(c, R, RdOrange, 0.4f * fade);
            int seed = z.Id * 7;
            for (int i = 0; i < 16; i++)  // flames licking up
            {
                float h1 = Mathf.Repeat(Mathf.Sin((seed + i) * 12.9898f) * 43758.55f, 1f), h2 = Mathf.Repeat(Mathf.Sin((seed + i) * 78.233f) * 12543.7f, 1f);
                V2 at = c + V2.FromAngle(h1 * MathX.Tau) * (Mathf.Sqrt(h2) * R * 0.85f);
                float flick = 0.6f + 0.4f * Mathf.Sin(t * (9f + i) + i * 1.7f);
                float h = (8f + 10f * h2) * flick * fade;
                SetColor(new Rgba(1f, 0.42f + 0.2f * h2, 0.1f, 0.75f * fade));
                Tri(at + new V2(-5f, 0f), at + new V2(5f, 0f), at + new V2(0f, -h - 6f));
                SetColor(RdHot.WithAlpha(0.8f * fade));
                Tri(at + new V2(-2.5f, 0f), at + new V2(2.5f, 0f), at + new V2(0f, -h * 0.55f - 2f));
                if (!fxReduced)
                {
                    float e = Mathf.Repeat(t * 0.9f + h1, 1f);  // an ember rising
                    Circle(at + new V2(Mathf.Sin(t * 3f + i) * 4f, -e * 30f), 1.4f, RdHot.WithAlpha(fade * (1f - e)), 6);
                }
            }
            for (int i = 0; i < 20; i++)
                Arc(c, R, 2f, RdOrange.WithAlpha(0.55f * fade), 3, i * MathX.Tau / 20f - t * 0.3f, MathX.Tau / 40f);
        }
    }
}
