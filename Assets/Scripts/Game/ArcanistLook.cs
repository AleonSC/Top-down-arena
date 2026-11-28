using UnityEngine;

namespace Arena
{
    /// The Arcanist's looks (Core/Arcanist.cs): violet telekinesis and gold runes. Over the model: the Kinetic Field's faint ring,
    /// Shard Guard, Kinetic Grasp's hold and Runic Array's rift (KitLook.Body; the lock ring at your cursor, DrawArrayLock); its rune bolts and spectral blades (KitLook.Shot);
    /// the Orbit Shards and Dancing Blades (DrawMinion); the Grand Sigil, Sanctum Sigil and Telekinetic Crush's grip (DrawZone); the held
    /// stylus and casting glyphs (WeaponLook.Held, WeaponLooks.cs). Preview: `ARCANIST=tank|healer|dps shot.sh x hud` (Tools/Preview/ShotArcanist.cs).
    public partial class ArenaApp
    {
        public static readonly Rgba ArcViolet = new Rgba(0.72f, 0.55f, 1f), ArcGold = new Rgba(1f, 0.85f, 0.45f), ArcInk = new Rgba(0.16f, 0.12f, 0.32f);
        static readonly Rgba ArcPale = new Rgba(0.93f, 0.9f, 1f);
        static readonly Rgba ArcanistLookViolet = new Rgba(0.72f, 0.55f, 1f);  // (the model's eyes and hem studs: CharacterModels.cs)

        /// A small gold rune drifting round the Arcanist's head (the model's motes, CharacterModels.cs ModelHead).
        void DrawRuneMote(V2 at, float t) => DrawRune(at, 2.6f, t * 2f, Mc(ArcGold.WithAlpha(0.85f)));

        /// A small rune: a diamond with a cross through it, turned by `rot`.
        void DrawRune(V2 at, float r, float rot, Rgba c)
        {
            Polygon(at, r, 4, rot, c.WithAlpha(c.A * 0.25f));
            for (int k = 0; k < 4; k++)
                Line(at + V2.FromAngle(rot + MathX.Tau * k / 4f) * r, at + V2.FromAngle(rot + MathX.Tau * (k + 1) / 4f) * r, 1.4f, c);
            Line(at + V2.FromAngle(rot) * r * 0.55f, at - V2.FromAngle(rot) * r * 0.55f, 1.2f, c);
        }

        // --- KitLook.Body ---------------------------------------------------------------------

        void DrawArcanistBody(Player p, float r, float t, Rgba col)
        {
            // Kinetic Field: a faint dashed ring where enemy shots slow down.
            for (int k = 0; k < 16; k++)
            {
                float a = t * 0.4f + MathX.Tau * k / 16f;
                Arc(p.Disp, Game.FieldRadius, 1.2f, ArcViolet.WithAlpha(0.12f * col.A), 4, a, MathX.Tau / 32f);
            }
            if (p.ShardGuard)  // Shard Guard: a bright warded ring
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 10f);
                Circle(p.Disp, Game.ShardGuardR, ArcViolet.WithAlpha(0.10f + 0.05f * pulse), 32);
                Arc(p.Disp, Game.ShardGuardR, 2.2f, ArcViolet.WithAlpha(0.7f + 0.3f * pulse), 32);
                Arc(p.Disp, Game.ShardGuardR - 5f, 1f, ArcPale.WithAlpha(0.5f), 32, t * 3f, MathX.Tau * 0.6f);
            }
            Enemy held;
            if (p.KgHeld != 0 && game != null && game.Enemies.TryGetValue(p.KgHeld, out held))  // Kinetic Grasp: the held enemy wrapped in force
            {
                V2 hand = p.Disp + V2.FromAngle(p.Aim) * (r + 4f);
                for (int k = 0; k < 3; k++)
                {
                    V2 side = V2.FromAngle(p.Aim + MathX.Pi / 2f) * (Mathf.Sin(t * 9f + k * 2.1f) * 4f);
                    Line(hand, held.Disp + side, 1.6f, ArcViolet.WithAlpha(0.5f));
                }
                Circle(held.Disp, held.Radius + 7f, ArcViolet.WithAlpha(0.16f), 20);
                Arc(held.Disp, held.Radius + 7f, 2f, ArcViolet.WithAlpha(0.85f), 20, t * 4f, MathX.Tau * 0.75f);
                for (int k = 0; k < 3; k++)
                    DrawRune(held.Disp + V2.FromAngle(t * 2f + MathX.Tau * k / 3f) * (held.Radius + 12f), 3.5f, t * 3f, ArcGold.WithAlpha(0.8f));
            }
            if (p.CrushOn || p.CrushMeter < 0.995f)  // Telekinetic Crush's force meter, round your feet (TelekineticCrush.cs)
            {
                bool low = p.CrushMeter < Game.CrushMinOn;
                Rgba mc = p.CrushOn ? (low ? ArcViolet.Mix(CrushRed, 0.65f) : ArcViolet) : low ? CrushRed : ArcGold;
                Arc(p.Disp, r + 9f, 2.5f, mc.WithAlpha(0.15f * col.A), 32);
                Arc(p.Disp, r + 9f, 2.5f, mc.WithAlpha(0.9f * col.A), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(p.CrushMeter));
            }
            if (p.ArrayT > 0f)  // Runic Array: the rift behind you
                DrawArrayRift(p, t);
            else
                arraySeen.Remove(p.Id);  // (the next one opens from shut)
        }

        // --- Runic Array -------------------------------------------------------------------

        readonly System.Collections.Generic.Dictionary<int, float> arraySeen = new System.Collections.Generic.Dictionary<int, float>();  // when this client first saw each player's rift

        public static float ArrayOpenPreview = -1f;  // Tools/Preview: how open the rift is (-1: timed as in the game)

        static float ArrayEase(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

        /// How open a player's rift is: eases open over Game.ArrayOpen from when this client first sees it, shuts over its last 0.4 s.
        float ArrayOpenness(Player p, float t)
        {
            if (ArrayOpenPreview >= 0f)
                return ArrayOpenPreview * Mathf.Clamp01(p.ArrayT / 0.4f);
            float seen;
            if (!arraySeen.TryGetValue(p.Id, out seen) || seen > t)
                arraySeen[p.Id] = seen = t;
            return ArrayEase((t - seen) / Game.ArrayOpen) * Mathf.Clamp01(p.ArrayT / 0.4f);
        }

        /// Runic Array's rift: an upright tear behind you, dark inside with light rippling up it, a gold rim and eight runes round it,
        /// a faint thread back to you.
        void DrawArrayRift(Player p, float t)
        {
            float open = ArrayOpenness(p, t), tall = ArrayEase(open * 1.5f);
            if (open <= 0.01f)
                return;
            V2 c = Game.ArrayRift(p.Disp, p.Aim);
            float rx = 15f * open, ry = Game.ArrayRiftH * tall;
            const int segs = 28;
            System.Func<int, float, float, V2> at = (i, sx, sy) =>
            {
                float a = MathX.Tau * i / segs, pinch = 1f - 0.35f * Mathf.Abs(Mathf.Cos(a));
                return c + new V2(Mathf.Sin(a) * sx * pinch, -Mathf.Cos(a) * sy);
            };
            SetColor(ArcViolet.WithAlpha(0.10f * open));
            for (int i = 0; i < segs; i++)
                Tri(c, at(i, rx + 14f, ry + 14f), at(i + 1, rx + 14f, ry + 14f));
            SetColor(ArcInk.WithAlpha(0.95f * open));
            for (int i = 0; i < segs; i++)
                Tri(c, at(i, rx, ry), at(i + 1, rx, ry));
            for (int k = 0; k < 4; k++)  // light rippling up the middle
            {
                float y = ((t * 0.8f + k * 0.25f) % 1f) * 2f - 1f;
                Line(c + new V2(-rx * 0.5f, y * ry * 0.8f), c + new V2(rx * 0.5f, y * ry * 0.8f - 4f), 1.2f, ArcViolet.WithAlpha(0.6f * open));
            }
            for (int i = 0; i < segs; i++)
                Line(at(i, rx, ry), at(i + 1, rx, ry), 2.2f, ArcGold.WithAlpha(0.95f * open));
            for (int k = 0; k < 8; k++)
                DrawRune(at(k * segs / 8 + 1, rx + 11f, ry + 9f), 3.8f, t * 1.4f + k, ArcPale.WithAlpha(0.95f * open));
            Line(p.Disp, c + new V2(0f, ry), 1f, ArcViolet.WithAlpha(0.35f * open));  // tethered to you
        }

        /// Your own Runic Array: a slow gold rune ring at the cursor (Game.ArrayLock: the blades go for enemies inside it) and a lock
        /// bracket on each of them.
        void DrawArrayLock(Player p, V2 cursor, float t)
        {
            float open = ArrayOpenness(p, t);
            if (open <= 0.01f || game == null)
                return;
            float R = Game.ArrayLock;
            Circle(cursor, R, ArcViolet.WithAlpha(0.05f * open), 48);
            for (int k = 0; k < 24; k++)
                Arc(cursor, R, 1.4f, ArcGold.WithAlpha(0.45f * open), 3, t * 0.3f + MathX.Tau * k / 24f, MathX.Tau / 60f);
            for (int k = 0; k < 4; k++)
                DrawRune(cursor + V2.FromAngle(-t * 0.5f + MathX.Tau * k / 4f) * R, 4.5f, t, ArcGold.WithAlpha(0.8f * open));
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f || e.SpawnT > 0f || !e.Disp.CloserThan(cursor, R + e.Radius))
                    continue;
                float r = e.Radius + 9f;
                for (int k = 0; k < 4; k++)
                    Arc(e.Disp, r, 2f, ArcGold.WithAlpha(0.85f * open), 6, t * 1.5f + MathX.Tau * k / 4f - 0.3f, 0.6f);
            }
        }

        /// A spectral sword: pale blade, gold guard and grip, a violet ghost round it, pointing along `d`.
        void DrawSpectralSword(V2 at, V2 d, float len, float a)
        {
            V2 n = new V2(-d.Y, d.X);
            SetColor(ArcViolet.WithAlpha(0.28f * a));
            Tri(at + d * (len * 0.62f), at - d * (len * 0.5f) + n * 5f, at - d * (len * 0.5f) - n * 5f);
            SetColor(ArcPale.WithAlpha(0.95f * a));
            Tri(at + d * (len * 0.55f), at - d * (len * 0.12f) + n * 2.4f, at - d * (len * 0.12f) - n * 2.4f);
            Line(at, at + d * (len * 0.4f), 0.8f, ArcViolet.WithAlpha(0.6f * a));  // the fuller
            Line(at - d * (len * 0.14f) + n * 5.5f, at - d * (len * 0.14f) - n * 5.5f, 2f, ArcGold.WithAlpha(0.95f * a));
            Line(at - d * (len * 0.14f), at - d * (len * 0.38f), 2f, ArcGold.WithAlpha(0.7f * a));
            Circle(at - d * (len * 0.4f), 1.8f, ArcGold.WithAlpha(0.9f * a), 6);
        }

        // --- KitLook.Shot ---------------------------------------------------------------------

        bool DrawArcanistShot(Bullet b, Rgba c, float t)
        {
            if (b.Kind == Defs.GlyphShot)  // a gold rune spinning along, a short trail behind
            {
                V2 back = b.Vel.LengthSq > 1f ? b.Vel.Normalized() : V2.FromAngle(0f);
                for (int k = 1; k <= 3; k++)
                    Circle(b.Pos - back * (k * 6f), 3.2f - k * 0.7f, ArcGold.WithAlpha(0.45f - k * 0.12f), 8);
                Circle(b.Pos, 7f, ArcGold.WithAlpha(0.18f), 12);
                DrawRune(b.Pos, 5.5f, t * 8f + b.Id, ArcGold);
                return true;
            }
            if (b.Kind == Defs.SpectralBlade)  // a spectral sword along its flight, a fading violet trail behind
            {
                V2 f = b.Vel.LengthSq > 1f ? b.Vel.Normalized() : V2.FromAngle(0f);
                float trail = Mathf.Clamp(b.Vel.Length * 0.035f, 8f, 34f);
                for (int k = 6; k >= 1; k--)
                {
                    float w = 1f - k / 7f;
                    Line(b.Pos - f * (trail * k / 6f + 8f), b.Pos - f * (trail * (k - 1) / 6f + 8f), 3.2f * w, ArcViolet.WithAlpha(0.45f * w));
                }
                DrawSpectralSword(b.Pos, f, 30f, 1f);
                return true;
            }
            return false;
        }

        // --- The Orbit Shards and Dancing Blades (DrawMinion) --------------------------------

        /// An Orbit Shard: a violet crystal pointing along its Aim, brighter while it flies (Wind 0.5).
        void DrawArcShard(Minion m, float t)
        {
            V2 f = V2.FromAngle(m.Aim), s = new V2(-f.Y, f.X);
            float fly = m.Wind > 0f ? 1f : 0f;
            Circle(m.Disp, 9f + 3f * fly, ArcViolet.WithAlpha(0.14f + 0.12f * fly), 12);
            V2 tip = m.Disp + f * 9f, tail = m.Disp - f * 7f;
            SetColor(ArcInk);
            Tri(tip + f * 1.5f, m.Disp + s * 5f, tail - f * 1.5f);
            Tri(tip + f * 1.5f, m.Disp - s * 5f, tail - f * 1.5f);
            SetColor(ArcViolet);
            Tri(tip, m.Disp + s * 3.8f, tail);
            Tri(tip, m.Disp - s * 3.8f, tail);
            SetColor(ArcPale.WithAlpha(0.85f));
            Tri(tip, m.Disp + s * 1.6f, m.Disp);
            if (fly > 0f)
                Line(m.Disp - f * 8f, m.Disp - f * 22f, 2.2f, ArcViolet.WithAlpha(0.35f));
        }

        /// A Dancing Blade: a gold-hilted sword pointing along its Aim; flying it leaves a short streak. Dancing (Wind 1) it is drawn
        /// over the enemies instead (DrawArcBladesOver).
        void DrawArcBlade(Minion m, float t)
        {
            if (BladeDanceU(m, t) >= 0f)
                return;
            V2 f = V2.FromAngle(m.Aim);
            if (m.Wind > 0f)  // flying out or home: a fading streak
                for (int k = 0; k < 3; k++)
                    Line(m.Disp - f * (10f + k * 12f), m.Disp - f * (22f + k * 12f), 3f - k * 0.8f, ArcGold.WithAlpha(0.3f - k * 0.09f));
            else
                Circle(m.Disp, 10f, ArcGold.WithAlpha(0.08f), 12);
            DrawBladeShape(m.Disp, m.Aim, 1f);
        }

        /// The dancing blades, over the enemies they cut through (WorldRenderer, after the enemies).
        void DrawArcBladesOver(float t)
        {
            foreach (var m in game.Minions.Values)
                if (m.Kind == MinionKind.ArcBlade && m.Wind >= 1f)
                    DrawBladeDance(m, BladeDanceU(m, t));
        }

        /// A blade's dance, the Crossing Cuts (Core BladeDanceAt): curved dashes through the enemy with a crescent trail, afterimages,
        /// a flip at each end and gold sparks on each cut. Timed from when this client first sees it dance (no snapshot field).
        void DrawBladeDance(Minion m, float u)
        {
            V2 at = m.Disp;
            float d0 = m.Aim, h;
            Circle(at, Game.BladeSlashR, ArcGold.WithAlpha(0.05f), 28);
            V2 pos = Game.BladeDanceAt(at, d0, u, out h);
            // the crescent trail: the blade's edge over the last BladeTrail seconds, fading to violet
            V2 prevTip = default, prevIn = default;
            for (int k = 0; k <= BladeTrailN; k++)
            {
                float back = BladeTrail * k / BladeTrailN, uk = Mathf.Max(0f, u - back / Game.BladeSlashTime), hk;
                V2 pk = Game.BladeDanceAt(at, d0, uk, out hk), tip = pk + V2.FromAngle(hk) * 22f, inner = pk + V2.FromAngle(hk) * 10f;
                if (k > 0)
                {
                    float age = (float)k / BladeTrailN, a = 1f - age;
                    SetColor(ArcPale.Mix(ArcViolet, age).WithAlpha(0.85f * a));
                    Tri(prevTip, tip, inner);
                    Tri(prevTip, inner, prevIn);
                    Line(prevTip, tip, 0.4f + 1.6f * (1f - age), ArcGold.WithAlpha(0.9f * (1f - age) * (1f - age)));
                }
                prevTip = tip;
                prevIn = inner;
            }
            // afterimages while it dashes, and gold motes shed from the tip
            float hb, speed = (pos - Game.BladeDanceAt(at, d0, Mathf.Max(0f, u - 0.01f), out hb)).Length / (0.01f * Game.BladeSlashTime);
            if (speed > 250f)
                for (int k = 1; k <= 3; k++)
                {
                    float hg;
                    V2 pg = Game.BladeDanceAt(at, d0, Mathf.Max(0f, u - 0.03f * k / Game.BladeSlashTime), out hg);
                    DrawBladeShape(pg, hg, 0.32f - k * 0.08f);
                }
            for (int k = 0; k < 8; k++)  // one mote every 0.04 s of the dance, drifting off for 0.3 s
            {
                float born = (Mathf.Floor(u * Game.BladeSlashTime / 0.04f) - k) * 0.04f, age = u * Game.BladeSlashTime - born;
                if (born < 0f || age > 0.3f)
                    continue;
                float hm, hm2;
                V2 pm = Game.BladeDanceAt(at, d0, born / Game.BladeSlashTime, out hm);
                if ((pm - Game.BladeDanceAt(at, d0, Mathf.Max(0f, born / Game.BladeSlashTime - 0.01f), out hm2)).Length < 2.5f)
                    continue;  // only where it was going fast
                float spin = (m.Id * 7.3f + born * 91f) % MathX.Tau, q = age / 0.3f;
                Circle(pm + V2.FromAngle(hm) * 20f + V2.FromAngle(spin) * (8f + 22f * q), 0.4f + 1.4f * (1f - q), ArcGold.WithAlpha(1f - q), 6);
            }
            // each slash tick (u = 0.25 ... 1): gold sparks thrown along the cut for 0.2 s
            for (int k = 1; k <= 4; k++)
            {
                float q = (u - k * 0.25f) * Game.BladeSlashTime / 0.2f;
                if (q < 0f || q > 1f)
                    continue;
                float dir = d0 + k * Game.BladeCutTurn;
                for (int i = 0; i < 6; i++)
                {
                    float a = dir + (i % 2 == 0 ? 1f : -1f) * (0.25f + 0.18f * i), d = 8f + 26f * Mathf.Sqrt(q);
                    Line(at + V2.FromAngle(a) * d, at + V2.FromAngle(a) * (d + 7f * (1f - q)), 1.6f, ArcGold.WithAlpha(1f - q));
                }
            }
            DrawBladeShape(pos, h, 1f);
        }

        const float BladeTrail = 0.14f;  // seconds of trail behind a dancing blade
        const int BladeTrailN = 12;
        readonly System.Collections.Generic.Dictionary<int, float> bladeDanceFrom = new System.Collections.Generic.Dictionary<int, float>();
        /// Preview: a fixed time into every dance (Tools/Preview ShotArcanist.cs, `DANCE=<s>`); -1 off.
        public static float BladeDancePreview = -1f;

        /// How far into its dance (0..1) blade m is, from when this client first saw it dancing; -1 when it isn't.
        float BladeDanceU(Minion m, float t)
        {
            if (m.Wind < 1f)
            {
                bladeDanceFrom.Remove(m.Id);
                return -1f;
            }
            if (BladeDancePreview >= 0f)
                return Mathf.Clamp01(BladeDancePreview / Game.BladeSlashTime);
            float from;
            if (!bladeDanceFrom.TryGetValue(m.Id, out from))
            {
                if (bladeDanceFrom.Count > 32)
                    bladeDanceFrom.Clear();  // blades gone mid-dance
                bladeDanceFrom[m.Id] = from = t;
            }
            return Mathf.Clamp01((t - from) / Game.BladeSlashTime);
        }

        /// The blade itself at `p` pointing along `ang`, at `a` opacity (afterimages are fainter).
        void DrawBladeShape(V2 p, float ang, float a)
        {
            V2 f = V2.FromAngle(ang), s = new V2(-f.Y, f.X);
            V2 tip = p + f * 22f, guard = p - f * 6f;
            Line(guard, tip, 7f, ArcGold.WithAlpha(0.14f * a));                      // its glow
            SetColor(ArcInk.WithAlpha(a));
            Tri(tip + f * 2f, guard + s * 4.6f, guard - s * 4.6f);
            SetColor(ArcPale.WithAlpha(a));
            Tri(tip, guard + s * 3.3f, guard - s * 3.3f);
            Line(p + f * 16f, guard, 1.2f, ArcViolet.WithAlpha(0.85f * a));          // the fuller glows
            Line(guard + s * 7.5f, guard - s * 7.5f, 3.2f, ArcGold.WithAlpha(a));     // crossguard
            Line(guard, guard - f * 8f, 3f, new Rgba(0.42f, 0.28f, 0.16f, a));        // grip
            Circle(guard - f * 9.5f, 2.4f, ArcGold.WithAlpha(a), 8);                  // pommel
        }

        // --- The zones (DrawZone) -------------------------------------------------------------

        /// Grand Sigil: a violet circle with a gold rune rim and a slowly turning star; it draws in and fades out.
        void DrawGrandSigil(Zone z, float t)
        {
            float age = z.Duration - z.T, grow = Mathf.Clamp01(age / 0.35f), fade = Mathf.Clamp01(z.T / 0.5f);
            float r = z.Radius * (0.6f + 0.4f * grow), a = grow * fade;
            Circle(z.Pos, r, ArcViolet.WithAlpha(0.08f * a), 64);
            Arc(z.Pos, r, 3f, ArcViolet.WithAlpha(0.85f * a), 64);
            Arc(z.Pos, r - Game.SigilBand, 1.2f, ArcViolet.WithAlpha(0.45f * a), 64);
            for (int k = 0; k < 18; k++)  // runes in the rim band
                DrawRune(z.Pos + V2.FromAngle(t * 0.15f + MathX.Tau * k / 18f) * (r - Game.SigilBand * 0.5f), 4.2f, t * 0.15f + MathX.Tau * k / 18f, ArcGold.WithAlpha(0.75f * a));
            float rot = -t * 0.25f, ir = r * 0.62f;
            for (int k = 0; k < 6; k++)  // a six-point star of two triangles
            {
                V2 p0 = z.Pos + V2.FromAngle(rot + MathX.Tau * k / 6f) * ir, p2 = z.Pos + V2.FromAngle(rot + MathX.Tau * (k + 2) / 6f) * ir;
                Line(p0, p2, 1.6f, ArcGold.WithAlpha(0.5f * a));
            }
            Arc(z.Pos, ir, 1.4f, ArcGold.WithAlpha(0.4f * a), 48);
            Arc(z.Pos, ir * 0.35f, 1.4f, ArcViolet.WithAlpha(0.6f * a), 24, t, MathX.Tau * 0.7f);
            Arc(z.Pos, r + 6f, 2f, ArcGold.WithAlpha(0.7f * a), 64, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration)));  // its time left
        }

        /// Sanctum Sigil: a soft gold circle with the runes still up turning on its rim.
        void DrawSanctumSigil(Zone z, float t)
        {
            float age = z.Duration - z.T, grow = Mathf.Clamp01(age / 0.3f), fade = Mathf.Clamp01(z.T / 0.5f), a = grow * fade;
            float r = z.Radius * (0.7f + 0.3f * grow);
            Circle(z.Pos, r, ArcGold.WithAlpha((0.09f + 0.03f * Mathf.Sin(t * 3f)) * a), 56);
            Arc(z.Pos, r, 2f, ArcGold.WithAlpha(0.6f * a), 56);
            Arc(z.Pos, r * 0.55f, 1.2f, ArcGold.WithAlpha(0.35f * a), 40, -t * 0.4f, MathX.Tau * 0.8f);
            for (int i = 0; i < 4; i++)  // motes rising
            {
                float ph = Mathf.Repeat(t * 0.6f + i * 0.25f, 1f);
                V2 c = z.Pos + V2.FromAngle(i * 1.7f + 0.4f) * (r * 0.5f) + new V2(0f, -22f * ph);
                Circle(c, 2.2f, ArcGold.WithAlpha(Mathf.Sin(ph * MathX.Pi) * 0.8f * a), 8);
            }
            int left = (int)z.Aim;
            for (int k = 0; k < Game.SanctumRunes; k++)
                if ((left & (1 << k)) != 0)
                {
                    V2 at = Game.SanctumRune(z, k);
                    Circle(at, Game.SanctumRuneR, ArcGold.WithAlpha(0.12f * a), 16);
                    DrawRune(at, 8f, t * 1.5f + k, ArcGold.WithAlpha(a));
                }
            Arc(z.Pos, r + 5f, 2f, ArcGold.WithAlpha(0.6f * a), 56, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration)));
        }

        static readonly Rgba CrushRed = new Rgba(1f, 0.33f, 0.41f);
        readonly System.Collections.Generic.Dictionary<int, V2> crushShown = new System.Collections.Generic.Dictionary<int, V2>();
        readonly System.Collections.Generic.Dictionary<int, V2> crushShownVel = new System.Collections.Generic.Dictionary<int, V2>();
        int crushShownFrame = -1;

        /// Where a Telekinetic Crush grip is drawn: its replicated place, smoothed (it moves every tick on the host but arrives in
        /// snapshots), and how fast it's going (for its afterimages). Once a frame for each grip.
        V2 CrushShown(Zone z, out V2 vel)
        {
            V2 at;
            float dt = Time.deltaTime;
            if (crushShownFrame != Time.frameCount)
            {
                crushShownFrame = Time.frameCount;
                if (crushShown.Count > 8)
                {
                    crushShown.Clear();
                    crushShownVel.Clear();
                }
            }
            if (dt <= 0f || !crushShown.TryGetValue(z.Id, out at) || at.FartherThan(z.Pos, 300f))
                at = z.Pos;  // (the preview, or a fresh grip: straight there)
            V2 next = at + (z.Pos - at) * Mathf.Clamp01(dt * 24f);
            V2 v = dt > 0f ? (next - at) / dt : V2.Zero, pv;
            vel = crushShownVel.TryGetValue(z.Id, out pv) ? pv + (v - pv) * Mathf.Clamp01(dt * 10f) : v;
            crushShown[z.Id] = next;
            crushShownVel[z.Id] = vel;
            return next;
        }

        /// Telekinetic Crush's grip (TelekineticCrush.cs; mockup in its header): a violet ring with a turning gold halo of runes,
        /// ripples closing on a swirling core, the caster's force meter round it, the shots it has caught orbiting inside and a tether
        /// of motes back to the caster's hand. The enemies it holds glow in force. Under a quarter of the meter it flickers red.
        void DrawArcCrush(Zone z, float t)
        {
            V2 vel;
            V2 c = CrushShown(z, out vel);
            float R = z.Radius;
            Player owner = null;
            if (game != null)
                game.Players.TryGetValue(z.Owner, out owner);
            float meter = owner != null ? owner.CrushMeter : 1f;
            bool low = meter < Game.CrushMinOn;
            float flick = low ? (Mathf.Sin(t * 38f) > 0f ? 1f : 0.55f) : 1f;
            Rgba main = low ? ArcViolet.Mix(CrushRed, 0.65f) : ArcViolet;
            if (owner != null && !owner.Downed)
                DrawCrushTether(owner, c, R, t, main);
            float sp = vel.Length;
            if (sp > 150f)  // afterimages trailing a fast sweep
                for (int k = 1; k <= 3; k++)
                    Arc(c - vel * (0.02f * k), R, 2f, main.WithAlpha(0.2f * (1f - k / 4f) * flick * Mathf.Clamp01((sp - 150f) / 400f)), 40);
            GlowDisc(c, R * 1.7f, main, 0.3f * flick);
            Circle(c, R, main.WithAlpha(0.08f), 40);
            for (int i = 0; i < 3; i++)  // ripples closing on the middle (the old crush rings, now running all the time)
            {
                float ph = Mathf.Repeat(t * 0.8f + i / 3f, 1f);
                Arc(c, R * (1f - ph), 1.5f, main.WithAlpha(0.45f * ph * flick), 36);
            }
            for (int k = 0; k < 3; k++)  // three swirling arms
            {
                float a0 = t * 2.5f + MathX.Tau * k / 3f;
                V2 prev = c + V2.FromAngle(a0) * (R * 0.15f);
                for (int s = 1; s <= 12; s++)
                {
                    float u = s / 12f;
                    V2 at = c + V2.FromAngle(a0 + u * 1.9f) * (R * (0.15f + 0.6f * u));
                    Line(prev, at, 2f, ArcPale.WithAlpha(0.22f * flick * (0.4f + 0.6f * u)));
                    prev = at;
                }
            }
            Arc(c, R, 3f, main.WithAlpha(0.95f * flick), 48);
            Arc(c, R - 4f, 1f, ArcPale.WithAlpha(0.35f * flick), 48);
            for (int k = 0; k < 16; k++)  // the gold halo's dashes, turning the other way
                Arc(c, R + 9f, 1.6f, ArcGold.WithAlpha(0.6f * flick), 4, -t * 0.6f + MathX.Tau * k / 16f, MathX.Tau / 30f);
            for (int k = 0; k < 6; k++)  // six runes riding the halo
            {
                float a = -t * 0.7f + MathX.Tau * k / 6f;
                DrawRune(c + V2.FromAngle(a) * (R + 20f), 5f * (1f + 0.15f * Mathf.Sin(t * 5f + k)), a + t, ArcGold.WithAlpha(0.9f * flick));
            }
            Arc(c, R + 30f, 2f, main.WithAlpha(0.15f), 48);  // the force meter, hugging the grip
            Arc(c, R + 30f, 2.5f, (low ? CrushRed : ArcGold).WithAlpha(0.85f), 48, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(meter));
            DrawRune(c, 7f + 1.5f * Mathf.Sin(t * 6f), t * 3f, ArcGold.WithAlpha(flick));
            for (int k = 0; k < 10; k++)  // motes drifting round
            {
                float a = t * (1.2f + k * 0.13f) + k, rr = R * (0.3f + 0.06f * ((k * 37) % 10));
                Circle(c + V2.FromAngle(a) * rr, 1.4f, ArcPale.WithAlpha(0.7f * flick), 6);
            }
            int shots = (int)z.Aim;
            for (int k = 0; k < shots; k++)  // the shots it has caught, orbiting inside
            {
                V2 at = c + V2.FromAngle(t * 4f + MathX.Tau * k / shots) * (R * (0.25f + 0.035f * ((k * 37) % 10)));
                GlowDisc(at, 9f, ArcGold, 0.7f);
                Circle(at, 3f, ArcGold, 8);
            }
            if (game == null)
                return;
            foreach (var e in game.Enemies.Values)  // what it holds, wrapped in force
            {
                if (e.Def.IsBoss || e.Def.Heavy || e.Disp.FartherThan(c, R + e.Radius * 0.4f + 6f))
                    continue;
                GlowDisc(e.Disp, e.Radius * 2.2f, ArcViolet, 0.35f);
                for (int k = 0; k < 8; k++)
                    Arc(e.Disp, e.Radius + 5f, 1.2f, ArcViolet.WithAlpha(0.8f), 3, t * 3f + MathX.Tau * k / 8f, MathX.Tau / 20f);
            }
        }

        /// The tether from the caster's hand to the grip: a soft curve that sways, and motes running down it.
        void DrawCrushTether(Player p, V2 c, float R, float t, Rgba main)
        {
            V2 hand = p.Disp + V2.FromAngle(p.Aim) * (p.Def.BodyR + 4f);
            V2 d = c - hand;
            float len = d.Length;
            if (len < R + 8f)
                return;
            V2 dir = d / len, n = new V2(-dir.Y, dir.X), end = c - dir * R;
            V2 ctl = (hand + end) * 0.5f + n * (Mathf.Sin(t * 2.7f) * 26f);
            V2 prev = hand;
            for (int s = 1; s <= 16; s++)
            {
                float u = s / 16f;
                V2 at = hand * ((1f - u) * (1f - u)) + ctl * (2f * (1f - u) * u) + end * (u * u);
                Line(prev, at, 5f, main.WithAlpha(0.16f));
                Line(prev, at, 1.2f, ArcPale.WithAlpha(0.35f));
                prev = at;
            }
            for (int i = 0; i < 18; i++)
            {
                float u = Mathf.Repeat(i / 18f + t * 0.9f, 1f), bulge = Mathf.Sin(u * MathX.Pi);
                V2 at = hand * ((1f - u) * (1f - u)) + ctl * (2f * (1f - u) * u) + end * (u * u);
                Circle(at, 1.2f + 2f * bulge, (i % 4 == 0 ? ArcGold : ArcViolet).WithAlpha(0.85f * bulge), 6);
            }
        }

        // --- The model's held things (BuildHeldDraws) ------------------------------------------

        /// Orbit Shards and Dancing Blades: an open palm with a turning glyph over it.
        void DrawHeldArcGlyph(Player p, float t, int hold)
        {
            float rot = t * 1.8f;
            Rgba c = p.Weapon.Id == WeaponId.DancingBlades ? ArcGold : ArcViolet;
            MCirc(6f, 0f, 7f, c.WithAlpha(0.14f), 14);
            for (int k = 0; k < 4; k++)  // a turning rune diamond with a bar through it
            {
                float a0 = rot + MathX.Tau * k / 4f, a1 = a0 + MathX.Tau / 4f;
                MLine(6f + Mathf.Cos(a0) * 5.5f, Mathf.Sin(a0) * 5.5f, 6f + Mathf.Cos(a1) * 5.5f, Mathf.Sin(a1) * 5.5f, 1.2f, c);
            }
            MLine(6f + Mathf.Cos(rot) * 3f, Mathf.Sin(rot) * 3f, 6f - Mathf.Cos(rot) * 3f, -Mathf.Sin(rot) * 3f, 1f, ArcPale);
            for (int k = 0; k < 6; k++)  // motes on a ring
            {
                float a = -rot * 0.7f + MathX.Tau * k / 6f;
                MCirc(6f + Mathf.Cos(a) * 9f, Mathf.Sin(a) * 9f, 0.9f, c.WithAlpha(0.8f), 6);
            }
        }

        /// Glyph Stylus: a long gold stylus with a glowing nib, writing a rune ahead of it.
        void DrawHeldGlyphStylus(Player p, float t, int hold)
        {
            MLine(-4f, 0f, 18f, 0f, 3.2f, Hx(0x5a3e8a));
            MLine(-3f, 0f, 16f, 0f, 1.8f, ArcGold);
            MPoly(ArcGold, 18f, -1.8f, 24f, 0f, 18f, 1.8f);
            MCirc(25f, 0f, 2.2f, ArcPale, 8);
            MArc(25f, 0f, 4.5f + Mathf.Sin(t * 6f), 0f, MathX.Tau, 0.9f, ArcGold.WithAlpha(0.6f));
            MCirc(-3f, 0f, 2f, ArcViolet, 8);
        }
    }
}
