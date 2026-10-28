using UnityEngine;

namespace Arena
{
    /// How the Blood Mage looks: a crimson hood, blood drops circling it that glow brighter the more health
    /// it's missing (Blood Price), its weapon in hand, its shots and its zones.
    public partial class ArenaApp
    {
        static readonly Rgba BloodRed = new Rgba(0.85f, 0.1f, 0.18f);
        static readonly Rgba BloodDark = new Rgba(0.35f, 0.03f, 0.08f);
        static readonly Rgba Bone = new Rgba(0.95f, 0.9f, 0.82f);

        /// A blood mage: a dark crimson hood, three blood drops circling (brighter as its health drops), the
        /// weapon in hand, and any buff it has up.
        void DrawBloodMage(Player p, float r, float t, Rgba col)
        {
            float a = col.A;
            V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
            float missing = p.MaxHp > 0f ? MathX.Clamp01(1f - p.Hp / p.MaxHp) : 0f;
            if (missing > 0.05f)  // Blood Price: a red glow that grows as health falls
                Circle(p.Disp, r + 6f + 3f * Mathf.Sin(t * (4f + 6f * missing)), BloodRed.WithAlpha(0.25f * missing * a), 24);
            Arc(p.Disp, r * 0.7f, 5f, BloodDark.WithAlpha(0.95f * a), 18, p.Aim + MathX.Pi - 1.3f, 2.6f);  // the hood
            Arc(p.Disp, r * 0.7f + 2.5f, 1.5f, BloodRed.WithAlpha(0.8f * a), 18, p.Aim + MathX.Pi - 1.3f, 2.6f);
            for (int k = 0; k < 3; k++)
            {
                V2 at = p.Disp + V2.FromAngle(t * 1.6f + MathX.Tau * k / 3f) * (r + 8f);
                DrawDrop(at, 2.6f, V2.FromAngle(t * 1.6f + MathX.Tau * k / 3f + MathX.Pi / 2f), BloodRed.WithAlpha((0.55f + 0.45f * missing) * a));
            }
            V2 grip = p.Disp + side * (r * 0.75f);
            switch (p.Weapon.Id)
            {
                case WeaponId.BloodLash:  // a whip of blood, swaying
                {
                    V2 prev = grip;
                    for (int i = 1; i <= 6; i++)
                    {
                        V2 pt = grip + dir * (i * 4f) + side * (Mathf.Sin(t * 8f + i * 0.9f) * 2.5f * i / 6f);
                        Line(prev, pt, 2.5f - 0.25f * i, BloodRed.WithAlpha(a));
                        prev = pt;
                    }
                    break;
                }
                case WeaponId.ClotBolt:  // a dark clot held up
                    Circle(grip + dir * 5f, 5f, BloodDark.WithAlpha(a), 10);
                    Circle(grip + dir * 6f + side * 1.5f, 2.5f, BloodRed.WithAlpha(a), 8);
                    break;
                case WeaponId.LeechBeam:  // a fanged crescent
                case WeaponId.VeinTether:
                    Arc(grip + dir * 4f, 6f, 2.5f, (p.Weapon.Id == WeaponId.VeinTether ? Defs.BulletColor(Defs.VeinShot) : BloodRed).WithAlpha(a), 10, p.Aim - 1f, 2f);
                    break;
                case WeaponId.TransfusionDarts:  // a syringe
                    Line(grip - dir * 3f, grip + dir * 7f, 3.5f, new Rgba(0.9f, 0.9f, 0.95f, 0.8f * a));
                    Line(grip - dir * 1f, grip + dir * 5f, 2f, BloodRed.WithAlpha(a));
                    Line(grip + dir * 7f, grip + dir * 11f, 1f, Steel.WithAlpha(a));
                    break;
                case WeaponId.SanguineOrb:
                    Circle(grip + dir * 5f, 6f, BloodRed.WithAlpha(0.3f * a), 10);
                    Circle(grip + dir * 5f, 3.5f, BloodRed.WithAlpha(a), 10);
                    break;
                case WeaponId.HemoSpray:  // droplets gathered in the hand
                    for (int k = -1; k <= 1; k++)
                        Circle(grip + dir * 5f + side * (k * 3f), 2f, BloodRed.WithAlpha(a), 6);
                    break;
                case WeaponId.BoneSpear:  // a bone spear held back, ready to throw
                    Line(grip - dir * 10f, grip + dir * (r + 8f), 3f, Bone.WithAlpha(a));
                    SetColor(Bone.WithAlpha(a));
                    Tri(grip + dir * (r + 15f), grip + dir * (r + 7f) + side * 3.5f, grip + dir * (r + 7f) - side * 3.5f);
                    break;
                default:  // Blood Bolts: a drop hovering over the palm
                    DrawDrop(grip + dir * 5f, 3.5f, dir, BloodRed.WithAlpha(a));
                    break;
            }
            // Abilities
            if (p.PactT > 0f)  // Crimson Pact: a ring of thorns, pulsing as it stores damage
                for (int k = 0; k < 10; k++)
                {
                    V2 d = V2.FromAngle(MathX.Tau * k / 10f + t * 0.8f);
                    Line(p.Disp + d * (r + 8f), p.Disp + d * (r + 14f + 2f * Mathf.Sin(t * 12f + k)), 2f, BloodRed.WithAlpha(0.8f * a));
                }
            if (p.RushT > 0f)  // Frenzy Pact: red streaks flickering off
                for (int k = 0; k < 4; k++)
                {
                    V2 d = V2.FromAngle(p.Aim + MathX.Pi + (k - 1.5f) * 0.4f);
                    float f = MathX.Fmod(t * 4f + k * 0.27f, 1f);
                    Line(p.Disp + d * (r + 4f + 10f * f), p.Disp + d * (r + 10f + 10f * f), 2f, BloodRed.WithAlpha((1f - f) * a));
                }
            var ult = p.UltT > 0f ? p.Slots[Upgrades.UltSlot].Id : AbilityId.None;
            if (ult == AbilityId.CrimsonApotheosis)  // a crimson halo of flame
                for (int k = 0; k < 9; k++)
                    Circle(p.Disp + V2.FromAngle(MathX.Tau * k / 9f - t) * (r + 11f), 3.5f + 1.5f * Mathf.Sin(t * 14f + k), BloodRed.WithAlpha(0.75f), 6);
        }

        /// Under a blood mage: Blood Rite's great red moon and Sanguine Rain's falling drops (DrawPlayerAuras).
        void DrawBloodAuras(Player p, float t)
        {
            if (p.UltT <= 0f || p.Downed)
                return;
            var ult = p.Slots[Upgrades.UltSlot].Id;
            if (ult == AbilityId.BloodRite)
            {
                Circle(p.Disp, 120f, BloodRed.WithAlpha(0.08f + 0.03f * Mathf.Sin(t * 3f)), 48);
                Arc(p.Disp, 120f, 3f, BloodRed.WithAlpha(0.6f), 48);
                Circle(p.Disp, p.Def.BodyR + 14f, BloodDark.WithAlpha(0.45f), 24);
            }
            else if (ult == AbilityId.SanguineRain)
                foreach (var q in game.Players.Values)
                {
                    if (q.Downed)
                        continue;
                    for (int k = 0; k < 6; k++)
                    {
                        float f = MathX.Fmod(t * 1.3f + k * 0.37f, 1f);
                        V2 at = q.Disp + new V2(Mathf.Sin(k * 2.3f) * 26f, -40f + 50f * f);
                        DrawDrop(at, 2.2f, new V2(0f, 1f), BloodRed.WithAlpha(0.8f * (1f - f)));
                    }
                }
        }

        /// Vein Tether's beams (each screen works out where they stop from where the enemies are drawn): a pulsing
        /// blood beam with two veins twisting along it. On an enemy, drops of blood flow back along it into the
        /// caster and the hit point throbs; on nothing, it thins out at the tip.
        void DrawVeinBeams(float t)
        {
            var c = Defs.BulletColor(Defs.VeinShot);
            foreach (var p in game.Players.Values)
            {
                if (!game.BeamOn(p))
                    continue;
                float range = Game.VeinBeamRange * (1f + 0.25f * game.ExtraShots(p));
                for (int v = 0; v <= p.Stats.Fan; v++)  // Barrage: a beam per stack, fanned like the shots
                {
                    float aim = p.Aim + (v == 0 ? 0f : (v % 2 == 1 ? 1f : -1f) * 0.14f * ((v + 1) / 2));
                    V2 dir = V2.FromAngle(aim), side = dir.Orthogonal(), from = p.Disp + dir * (p.Def.BodyR + 4f);
                    V2 end = game.BeamEnd(from, aim, range, p.Stats.Pierce, null, true);
                    float len = from.DistanceTo(end);
                    bool hit = len < range - 1f;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 18f + p.Id);
                    float tipFade = hit ? 1f : 0.55f;
                    Line(from, end, 15f + 3f * pulse, c.WithAlpha(0.1f * tipFade));
                    Line(from, end, 6f + 1.5f * pulse, c.WithAlpha(0.55f * tipFade));
                    Line(from, end, 2f, new Rgba(1f, 0.8f, 0.85f, 0.9f * tipFade));
                    // Two veins twisting around the beam.
                    for (int s = 0; s < 2; s++)
                    {
                        V2 prev = from;
                        for (float u = 10f; u <= len; u += 10f)
                        {
                            float amp = 4.5f * Mathf.Min(1f, u / 30f);
                            V2 q = from + dir * u + side * (amp * Mathf.Sin(u * 0.09f - t * 14f + s * MathX.Pi));
                            Line(prev, q, 1.4f, BloodDark.WithAlpha(0.85f * tipFade));
                            prev = q;
                        }
                    }
                    Circle(from, 5f + 1.5f * pulse, c.WithAlpha(0.6f), 12);
                    if (hit)
                    {
                        // The drain: drops riding the beam back home.
                        for (int k = 0; k < 5; k++)
                        {
                            float f = MathX.Fmod(t * 1.8f + k / 5f, 1f);
                            DrawDrop(V2.Lerp(end, from, f) + side * (2.5f * Mathf.Sin(f * 9f + k)), 2.3f, -dir, BloodRed.WithAlpha(0.95f * Mathf.Sin(f * MathX.Pi)));
                        }
                        Circle(end, 9f + 3f * pulse, c.WithAlpha(0.35f), 16);
                        Arc(end, 13f + 4f * pulse, 1.8f, BloodRed.WithAlpha(0.7f), 16);
                        Circle(end, 4f, new Rgba(1f, 0.8f, 0.85f, 0.9f), 10);
                    }
                    else
                        Circle(end, 3f + pulse, c.WithAlpha(0.4f), 10);
                }
            }
        }

        /// A drop of blood: round, with a point trailing behind its direction of travel.
        void DrawDrop(V2 c, float r, V2 dir, Rgba col)
        {
            V2 side = dir.Orthogonal();
            Circle(c, r, col, 10);
            SetColor(col);
            Tri(c - dir * (r * 2.4f), c + side * (r * 0.9f), c - side * (r * 0.9f));
        }

        /// The Blood Mage's shots.
        void DrawBloodShot(Bullet b, Rgba c, float t)
        {
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), side = dir.Orthogonal();
            switch (b.Kind)
            {
                case Defs.ClotShot:  // a lumpy dark clot
                    Circle(b.Pos, b.Radius + 3f, c.WithAlpha(0.3f), 12);
                    Circle(b.Pos, b.Radius, c, 12);
                    Circle(b.Pos + side * 3f - dir * 2f, b.Radius * 0.6f, c, 10);
                    Circle(b.Pos + dir * 2f - side * 2f, b.Radius * 0.35f, BloodRed, 8);
                    break;
                case Defs.LeechShot:  // a streak of the beam
                case Defs.VeinShot:
                    Line(b.Pos - dir * 14f, b.Pos + dir * 4f, 5f, c.WithAlpha(0.3f));
                    Line(b.Pos - dir * 12f, b.Pos + dir * 3f, 2f, c);
                    break;
                case Defs.TransfuseShot:
                    Line(b.Pos - dir * 9f, b.Pos + dir * 4f, 1.5f, c);
                    Circle(b.Pos + dir * 4f, 1.8f, BloodRed, 6);
                    break;
                case Defs.SanguineShot:  // a glowing orb with a drop circling it
                    Circle(b.Pos, b.Radius + 4f, c.WithAlpha(0.25f), 12);
                    Circle(b.Pos, b.Radius, c, 12);
                    Circle(b.Pos, b.Radius * 0.45f, BloodDark, 8);
                    Circle(b.Pos + V2.FromAngle(t * 9f + b.Id) * (b.Radius + 4f), 1.8f, BloodRed, 6);
                    break;
                case Defs.BoneSpearShot:  // a long bone shaft, a red streak behind
                    Line(b.Pos - dir * 30f, b.Pos - dir * 14f, 4f, BloodRed.WithAlpha(0.35f));
                    Line(b.Pos - dir * 16f, b.Pos + dir * 2f, 3f, c);
                    SetColor(c);
                    Tri(b.Pos + dir * 10f, b.Pos + side * 4f, b.Pos - side * 4f);
                    break;
                default:  // BloodShot, DropletShot: drops of blood
                    Circle(b.Pos, b.Radius + 2.5f, c.WithAlpha(0.25f), 10);
                    DrawDrop(b.Pos, b.Radius, dir, c);
                    break;
            }
        }

        /// The Blood Mage's zones: the Siphon Field drawing motes in, and a Clot Bolt's glossy pool.
        void DrawBloodZone(Zone z, Rgba c, float t)
        {
            if (z.Kind == ZoneKind.Siphon)
            {
                Circle(z.Pos, z.Radius, c.WithAlpha(0.1f + 0.04f * Mathf.Sin(t * 3f)), 48);
                for (int k = 0; k < 10; k++)  // motes drifting in to the centre
                {
                    float f = MathX.Fmod(t * 0.5f + k * 0.37f, 1f);
                    Circle(z.Pos + V2.FromAngle(MathX.Tau * k / 10f + 0.6f * f) * (z.Radius * (1f - f)), 2.5f, c.WithAlpha(0.8f * f), 6);
                }
                Circle(z.Pos, 6f, c.WithAlpha(0.7f), 12);
            }
            else  // BloodPool
            {
                float fade = MathX.Clamp01(z.T / 0.5f);  // dries up at the end
                Circle(z.Pos, z.Radius * 0.85f, c.WithAlpha(0.2f * fade), 32);
                for (int k = 0; k < 5; k++)
                    Circle(z.Pos + V2.FromAngle(k * 1.3f + z.Id) * (z.Radius * 0.6f), z.Radius * 0.3f, c.WithAlpha(0.12f * fade), 16);
                Circle(z.Pos + new V2(-z.Radius * 0.3f, -z.Radius * 0.3f), z.Radius * 0.15f, new Rgba(1f, 0.6f, 0.6f, 0.25f), 10);  // a glint
            }
        }
    }
}
