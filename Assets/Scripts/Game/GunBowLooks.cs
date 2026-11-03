using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Healer's Revolver and Mender's Bow with Visual effects on (Docs/Mockups/revolver-bow-animations.html, look A for both).
    /// Revolver, "Smoke & brass": the gun kicks back on every shot and its cylinder clicks round, a flash cone and drifting
    /// smoke leave the muzzle, a brass casing flips out; a heavy round is a bigger, orange flash with a few sparks.
    /// Bow, "Flex & snap": the limbs bend as you draw, a glint shows on the arrowhead near full draw, a gold ring pops at
    /// full draw, and on release the string snaps and rings out (with a ring of light, gold for a perfect shot), and the
    /// arrow flies with a longer streak. Drawing only: rounds, damage and timing are in Core/Weapons.cs. With the effects
    /// off, nothing here runs and the models draw exactly as before.
    public partial class ArenaApp
    {
        class GunSeen { public float T = -99f; public bool Heavy; public int Shots; }
        class BowSeen { public float Prev, SnapT = -99f; public bool Perfect; }

        const byte PMuzzle = 8, PCasing = 9, PSnap = 10;  // particle kinds (Juice.cs draws them through DrawGunPart)
        const float GunKickRate = 22f, GunClick = 0.1f, BowSnapHide = 0.3f, BowSnapRing = 0.4f, BowRingLife = 0.2f;

        readonly Dictionary<int, GunSeen> gunSeen = new Dictionary<int, GunSeen>();
        readonly Dictionary<int, BowSeen> bowSeen = new Dictionary<int, BowSeen>();

        static readonly Rgba BrassCol = new Rgba(0.88f, 0.69f, 0.29f), SmokeCol = new Rgba(0.78f, 0.8f, 0.85f), BowGold = new Rgba(1f, 0.82f, 0.3f);

        void ClearGunBowLooks()
        {
            gunSeen.Clear();
            bowSeen.Clear();
        }

        // --- Revolver ---------------------------------------------------------------------------------------------------

        /// A Revolver round has just appeared at its muzzle: flash, smoke, brass, and the gun's kick.
        void OnRevolverShot(Player owner, Bullet b)
        {
            GunSeen g;
            if (!gunSeen.TryGetValue(owner.Id, out g))
                gunSeen[owner.Id] = g = new GunSeen();
            // The host sends a heavy round fatter (6 against 4.5, times the shooter's shot size).
            float size = Mathf.Max(0.1f, owner.Stats.ShotSizeMult);
            bool heavy = b.Radius / size >= 5.25f;
            g.T = juiceClock; g.Heavy = heavy; g.Shots++;

            V2 dir = b.Vel.Normalized(), side = dir.Orthogonal();
            float k = heavy ? 1.4f : 1f;
            AddPart(new Part { Kind = PMuzzle, Pos = b.Pos, Vel = V2.Zero, Rot = b.Vel.Angle, Life = 0.09f, Max = 0.09f, Size = 22f * k, Col = heavy ? new Rgba(1f, 0.74f, 0.36f) : new Rgba(1f, 0.88f, 0.56f) });
            for (int i = 0; i < 6; i++)  // smoke drifting out and up
            {
                float life = Rnd(0.55f, 0.85f);
                AddPart(new Part { Kind = PDust, Pos = b.Pos + dir * Rnd(3f, 9f), Vel = dir * Rnd(60f, 150f) + side * Rnd(-35f, 35f), Life = life, Max = life, Size = Rnd(5f, 9f) * k, Col = SmokeCol });
            }
            // A casing flips out of the right side and tumbles.
            AddPart(new Part { Kind = PCasing, Pos = owner.Disp + dir * 8f + side * 8f, Vel = side * Rnd(60f, 100f) - dir * Rnd(0f, 30f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-22f, 22f), Life = 0.6f, Max = 0.6f, Size = 3f, Col = BrassCol });
            if (heavy)
                Sparks(b.Pos, new Rgba(1f, 0.7f, 0.3f), 6, 240f, 0.18f, 2f);
        }

        /// How far the gun is kicked back (0..1.6) and how far its cylinder has turned, for the model.
        bool GunLook(Player p, out float kick, out float spin)
        {
            kick = spin = 0f;
            GunSeen g;
            if (!Juicy || !gunSeen.TryGetValue(p.Id, out g))
                return Juicy;
            float age = juiceClock - g.T;
            kick = age > 0.6f ? 0f : (float)System.Math.Exp(-GunKickRate * age) * (g.Heavy ? 1.6f : 1f);
            spin = g.Shots * MathX.Tau / 6f - (1f - EaseOut3(Mathf.Clamp01(age / GunClick))) * MathX.Tau / 6f;
            return true;
        }

        // --- Mender's Bow -----------------------------------------------------------------------------------------------

        /// Each frame: notice a release (the draw ending) so the string can snap and a ring can fly off.
        void UpdateBowLooks()
        {
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon.Id != WeaponId.MendersBow)
                    continue;
                BowSeen s;
                if (!bowSeen.TryGetValue(p.Id, out s))
                    bowSeen[p.Id] = s = new BowSeen();
                if (s.Prev > 0f && p.DrawT <= 0f && !p.Downed)
                {
                    float c = game.BowCharge(p, s.Prev);
                    bool perfect = game.BowPerfect(p, s.Prev);
                    s.SnapT = juiceClock; s.Perfect = perfect;
                    V2 at = p.Disp + V2.FromAngle(p.Aim) * (p.Def.BodyR + 14f);
                    var col = perfect ? BowGold : new Rgba(0.7f, 1f, 0.8f);
                    float reach = 26f + 30f * c;
                    AddPart(new Part { Kind = PSnap, Pos = at, Rot = p.Aim, Life = BowSnapRing, Max = BowSnapRing, Size = reach, Col = col, A0 = 0f, A1 = perfect ? 4f : 2.5f });
                    if (perfect)
                        AddPart(new Part { Kind = PSnap, Pos = at, Rot = p.Aim, Life = BowSnapRing, Max = BowSnapRing, Size = reach * 0.6f, Col = Rgba.White, A0 = 1f, A1 = 2f });
                }
                s.Prev = p.DrawT;
            }
        }

        /// The held bow with the effects on: limbs that flex with the draw, the string, the arrow (gone just after a shot)
        /// and a glint on its head near full draw.
        void DrawBowFlex(Player p)
        {
            bool drawing = p.DrawT > 0f;
            float c = drawing ? game.BowCharge(p, p.DrawT) : 0f;
            float k = drawing ? 0.15f + 0.85f * c : 0.1f;
            BowSeen s;
            float since = bowSeen.TryGetValue(p.Id, out s) ? juiceClock - s.SnapT : 99f;
            float wobble = since > 0f && since < 0.5f ? (float)System.Math.Sin(since * 70f) * 4f * (float)System.Math.Exp(-since * 9f) : 0f;
            float tipX = 9.9f - 5f * c, tipY = 13.9f - 1.8f * c, ctrlX = 2f * (18f - 2f * c) - tipX;
            var wood = Hx(0x7a5a3a);
            float px = tipX, py = -tipY;
            for (int i = 1; i <= 10; i++)  // the limbs: one curve from tip to tip, bulging forward
            {
                float u = i / 10f, a = (1f - u) * (1f - u), b = 2f * u * (1f - u), d = u * u;
                float x = a * tipX + b * ctrlX + d * tipX, y = a * -tipY + d * tipY;
                MLine(px, py, x, y, 2.6f, wood);
                px = x; py = y;
            }
            float nock = 4f - 10f * k;
            MLine(tipX, -tipY, nock + wobble, 0f, 0.8f, Hx(0xeaeaea)); MLine(nock + wobble, 0f, tipX, tipY, 0.8f, Hx(0xeaeaea));
            if (since > BowSnapHide)  // the arrow is away for a moment after the shot
            {
                MLine(nock, 0f, 26f, 0f, 1.4f, Hx(0xcfe9d6)); MPoly(Hx(0x59ff8c), 26f, 0f, 26f, -2f, 31f, 0f, 26f, 2f); MCirc(nock, 0f, 1.6f, Hx(0x59ff8c), 8);
                if (c > 0.7f)
                {
                    float g = (c - 0.7f) / 0.3f;
                    MStar(31f, 0f, 3f + 5f * g, 0.9f + 0.6f * g, 4, 0f, new Rgba(1f, 1f, 1f, 0.4f + 0.6f * g));
                }
            }
            MCirc(18f, 0f, 2f, Hx(0x59ff8c), 8);
        }

        /// The ring that pops out of the bow at the moment a draw reaches full (called while drawing).
        void DrawBowFullPop(Player p)
        {
            float d = p.DrawT - game.BowFull(p);
            if (!Juicy || d < 0f || d >= BowRingLife)
                return;
            float k = d / BowRingLife;
            V2 at = p.Disp + V2.FromAngle(p.Aim) * (p.Def.BodyR + 18f);
            Arc(at, 12f + 26f * EaseOut3(Mathf.Clamp01(k)), 3f, BowGold.WithAlpha(1f - k), 16, p.Aim - 0.9f, 1.8f);
        }

        /// A longer, fading streak behind an arrow (its speed is how hard it was drawn).
        void DrawArrowStreak(Bullet b, V2 dir, V2 tail, Rgba c)
        {
            float len = 14f + 40f * Mathf.Clamp01((b.Vel.Length - 450f) / 500f);
            for (int i = 0; i < 4; i++)
                Line(tail - dir * (len * (i + 1) / 4f), tail - dir * (len * i / 4f), 1.5f + 2.5f * (1f - i / 4f) * Mathf.Clamp01((b.Vel.Length - 450f) / 500f), c.WithAlpha(0.5f * (1f - i / 4f)));
        }

        // --- The particles themselves (Juice.cs hands them here) ---------------------------------------------------------

        void DrawGunPart(Part p, float f)
        {
            switch (p.Kind)
            {
                case PMuzzle:  // a flash cone and a four-pointed star
                {
                    V2 dir = V2.FromAngle(p.Rot), side = dir.Orthogonal();
                    float len = p.Size;
                    SetColor(p.Col.WithAlpha(f));
                    Tri(p.Pos + dir * len, p.Pos + dir * (len * 0.45f) + side * (len * 0.2f), p.Pos + dir * (len * 0.45f) - side * (len * 0.2f));
                    Tri(p.Pos, p.Pos + dir * (len * 0.45f) + side * (len * 0.2f), p.Pos + dir * (len * 0.45f) - side * (len * 0.2f));
                    Star4(p.Pos + dir * (len * 0.25f), len * 0.45f * f + 2f, Rgba.White.WithAlpha(f));
                    break;
                }
                case PCasing:  // a short brass bar, tumbling
                {
                    V2 d = V2.FromAngle(p.Rot) * p.Size;
                    float a = Mathf.Clamp01((f - 0.2f) * 2f);
                    Line(p.Pos - d, p.Pos + d, 2.6f, p.Col.WithAlpha(a));
                    Line(p.Pos - d, p.Pos - d * 0.4f, 2.6f, new Rgba(1f, 0.94f, 0.69f, a));
                    break;
                }
                case PSnap:  // an arc of light flying out in front of the bow
                {
                    float k = 1f - f;
                    Arc(p.Pos, 10f + (p.Size - 10f) * EaseOut3(Mathf.Clamp01(k)), p.A1, p.Col.WithAlpha(f * (p.A0 > 0.5f ? 0.8f : 1f)), 16, p.Rot - (p.A0 > 0.5f ? 0.9f : 0.7f), p.A0 > 0.5f ? 1.8f : 1.4f);
                    break;
                }
            }
        }
    }
}
