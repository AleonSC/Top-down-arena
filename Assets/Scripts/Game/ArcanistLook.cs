using UnityEngine;

namespace Arena
{
    /// The Arcanist's looks (Core/Arcanist.cs): violet telekinesis and gold runes. Over the model: the Kinetic Field's faint ring,
    /// Shard Guard, Kinetic Grasp's hold and Runic Array's sigils (KitLook.Body); its rune bolts and spectral blades (KitLook.Shot);
    /// the Orbit Shards and Dancing Blades (DrawMinion); the Grand Sigil, Sanctum Sigil and Telekinetic Crush (DrawZone); the held
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
            if (p.ArrayT > 0f)  // Runic Array: four sigils turning behind you
            {
                float fade = Mathf.Clamp01(p.ArrayT / 0.4f);
                for (int k = 0; k < 4; k++)
                {
                    V2 at = Game.ArraySigil(p.Disp, p.Aim, k);
                    float spin = t * (k % 2 == 0 ? 1.6f : -1.6f);
                    Circle(at, 12f, ArcViolet.WithAlpha(0.12f * fade), 18);
                    Arc(at, 10f, 1.4f, ArcGold.WithAlpha(0.85f * fade), 18);
                    for (int s = 0; s < 8; s++)  // tick marks round the ring
                    {
                        V2 d = V2.FromAngle(spin + MathX.Tau * s / 8f);
                        Line(at + d * 10f, at + d * 13f, 1f, ArcGold.WithAlpha(0.6f * fade));
                    }
                    DrawRune(at, 5.5f, -spin * 1.5f, ArcPale.WithAlpha(0.95f * fade));
                    Line(at, at + V2.FromAngle(p.Aim) * 15f, 1.2f, ArcViolet.WithAlpha(0.5f * fade));  // the way its blades fly
                }
            }
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
            if (b.Kind == Defs.SpectralBlade)  // a ghostly violet blade along its flight
            {
                V2 f = b.Vel.LengthSq > 1f ? b.Vel.Normalized() : V2.FromAngle(0f), s = new V2(-f.Y, f.X);
                SetColor(ArcViolet.WithAlpha(0.35f));
                Tri(b.Pos + f * 10f, b.Pos - f * 12f + s * 4f, b.Pos - f * 12f - s * 4f);
                SetColor(ArcPale.WithAlpha(0.9f));
                Tri(b.Pos + f * 9f, b.Pos - f * 6f + s * 2f, b.Pos - f * 6f - s * 2f);
                Line(b.Pos - f * 7f + s * 4f, b.Pos - f * 7f - s * 4f, 1.6f, ArcGold.WithAlpha(0.8f));
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

        /// A Dancing Blade: a gold-hilted sword pointing along its Aim; dancing (Wind 1) it spins and trails slash arcs.
        void DrawArcBlade(Minion m, float t)
        {
            V2 f = V2.FromAngle(m.Aim), s = new V2(-f.Y, f.X);
            if (m.Wind >= 1f)
            {
                Circle(m.Disp, Game.BladeSlashR, ArcGold.WithAlpha(0.06f), 24);
                for (int k = 0; k < 2; k++)
                    Arc(m.Disp, Game.BladeSlashR - 6f - k * 8f, 2.2f - k * 0.8f, ArcGold.WithAlpha(0.55f - k * 0.2f), 16, m.Aim - 1.6f + k * MathX.Pi, 1.4f);
            }
            else
                Circle(m.Disp, 10f, ArcGold.WithAlpha(m.Wind > 0f ? 0.16f : 0.08f), 12);
            V2 tip = m.Disp + f * 22f, guard = m.Disp - f * 6f;
            Line(guard, tip, 7f, ArcGold.WithAlpha(0.14f));                      // its glow
            SetColor(ArcInk);
            Tri(tip + f * 2f, guard + s * 4.6f, guard - s * 4.6f);
            SetColor(ArcPale);
            Tri(tip, guard + s * 3.3f, guard - s * 3.3f);
            Line(m.Disp + f * 16f, guard, 1.2f, ArcViolet.WithAlpha(0.85f));     // the fuller glows
            Line(guard + s * 7.5f, guard - s * 7.5f, 3.2f, ArcGold);              // crossguard
            Line(guard, guard - f * 8f, 3f, new Rgba(0.42f, 0.28f, 0.16f));       // grip
            Circle(guard - f * 9.5f, 2.4f, ArcGold, 8);                           // pommel
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

        /// Telekinetic Crush: violet rings closing on the middle, tighter as the slam nears.
        void DrawArcCrush(Zone z, float t)
        {
            float k01 = 1f - Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration));
            Circle(z.Pos, z.Radius, ArcViolet.WithAlpha(0.06f + 0.1f * k01), 40);
            for (int i = 0; i < 3; i++)
            {
                float ph = Mathf.Repeat(k01 * 1.5f + i / 3f, 1f);
                Arc(z.Pos, z.Radius * (1f - ph), 2.4f, ArcViolet.WithAlpha(0.7f * (1f - ph * 0.5f)), 36);
            }
            for (int k = 0; k < 6; k++)
            {
                V2 d = V2.FromAngle(MathX.Tau * k / 6f + t);
                Line(z.Pos + d * z.Radius, z.Pos + d * (z.Radius * (1f - k01) * 0.8f + 8f), 1.4f, ArcGold.WithAlpha(0.6f));
            }
            DrawRune(z.Pos, 9f + 6f * k01, t * 4f, ArcGold);
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
