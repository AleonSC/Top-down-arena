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
                case WeaponId.LeechBeam:  // a knot of three little tendrils curling from the palm
                    Circle(grip + dir * 3f, 3f, BloodDark.WithAlpha(a), 8);
                    for (int k = -1; k <= 1; k++)
                    {
                        V2 prev = grip + dir * 3f;
                        for (int i = 1; i <= 4; i++)
                        {
                            V2 pt = grip + dir * (3f + i * 2.6f) + side * (k * i * 1.6f + Mathf.Sin(t * 7f + i * 1.1f + k * 2f) * 1.2f * i / 4f);
                            Line(prev, pt, 2.2f - 0.35f * i, BloodRed.WithAlpha(a));
                            prev = pt;
                        }
                    }
                    break;
                case WeaponId.VeinTether:  // a fanged crescent
                    Arc(grip + dir * 4f, 6f, 2.5f, Defs.BulletColor(Defs.VeinShot).WithAlpha(a), 10, p.Aim - 1f, 2f);
                    break;
                case WeaponId.TransfusionDarts:  // a syringe
                    Line(grip - dir * 3f, grip + dir * 7f, 3.5f, new Rgba(0.9f, 0.9f, 0.95f, 0.8f * a));
                    Line(grip - dir * 1f, grip + dir * 5f, 2f, BloodRed.WithAlpha(a));
                    Line(grip + dir * 7f, grip + dir * 11f, 1f, Steel.WithAlpha(a));
                    break;
                case WeaponId.SanguineOrb:
                {
                    Circle(grip + dir * 5f, 6f, BloodRed.WithAlpha(0.3f * a), 10);
                    Circle(grip + dir * 5f, 3.5f, BloodRed.WithAlpha(a), 10);
                    // The heal reserve: an arc under you filling as you coat enemies, glowing once there's enough to coat an ally.
                    float full = MathX.Clamp01(p.BloodReserve / Game.ReserveMax);
                    bool ready = p.BloodReserve >= Game.AllyCoatCost;
                    Arc(p.Disp, r + 5f, 2.5f, BloodDark.WithAlpha(0.6f * a), 20, MathX.Pi * 0.2f, MathX.Pi * 0.6f);
                    if (full > 0f)
                        Arc(p.Disp, r + 5f, 2.5f, (ready ? new Rgba(1f, 0.35f, 0.45f) : BloodRed).WithAlpha((ready ? 0.8f + 0.2f * Mathf.Sin(t * 8f) : 0.8f) * a), 20,
                            MathX.Pi * 0.8f - MathX.Pi * 0.6f * full, MathX.Pi * 0.6f * full);
                    break;
                }
                case WeaponId.Implosion:  // a clenched knot of blood, drops circling into it
                    Circle(grip + dir * 5f, 3.5f, BloodDark.WithAlpha(a), 8);
                    for (int k = 0; k < 3; k++)
                        Circle(grip + dir * 5f + V2.FromAngle(-t * 7f + k * MathX.Tau / 3f) * 5f, 1.5f, BloodRed.WithAlpha(a), 6);
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
            if (p.Clot > 0 && p.ClotMax > 0)  // Blood Lash's Clot: dark scabs plating you, glowing when a Clot Burst is ready
            {
                bool ready = p.Clot >= p.ClotMax;
                float seg = MathX.Tau / p.ClotMax;
                var plate = ready ? BloodRed.WithAlpha((0.7f + 0.3f * Mathf.Sin(t * 12f)) * a) : BloodDark.WithAlpha(0.9f * a);
                for (int k = 0; k < p.Clot; k++)
                    Arc(p.Disp, r + 3f, 3f, plate, 6, p.Aim + seg * k + 0.08f, seg - 0.16f);
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

        /// Blood Bolts stuck in an enemy: a drop for each, circling it and drawing in closer as they build toward the
        /// burst; one short of it, they swell and throb.
        void DrawHemorrhage(Enemy e, V2 pos, float r, float t)
        {
            int n = e.Hemorrhage, burstAt = Game.HemorrhageHits;
            float k = (float)n / burstAt, ring = r + 10f - 6f * k;
            bool primed = n >= burstAt - 1;
            float swell = primed ? 1f + 0.35f * Mathf.Abs(Mathf.Sin(t * 12f)) : 1f;
            if (primed)
                Circle(pos, r + 4f, BloodRed.WithAlpha(0.15f + 0.1f * Mathf.Sin(t * 12f)), 20);
            for (int i = 0; i < n; i++)
            {
                V2 d = V2.FromAngle(t * (2f + 3f * k) + MathX.Tau * i / burstAt + e.Id);
                DrawDrop(pos + d * ring, 4.8f * swell, d, BloodDark);  // a dark edge, so it reads on red enemies
                DrawDrop(pos + d * ring, 3.6f * swell, d, BloodRed);
            }
        }

        /// A Sanguine Orb circling in on the enemy it caught (FxKind.BloodCoat): drops spiral in and close over it,
        /// following the enemy (its id rides in Pos2.X), until it's covered in blood.
        void DrawBloodCoatFx(Effect fx, float f, float t)
        {
            Enemy e;
            V2 at = game.Enemies.TryGetValue((int)fx.Pos2.X, out e) ? e.Disp : fx.Pos;
            float r = fx.Radius, ring = (r + 24f) * (1f - f) + r * 0.6f * f;
            for (int k = 0; k < 4; k++)
            {
                V2 d = V2.FromAngle(f * MathX.Tau * 1.6f + k * MathX.Tau / 4f);
                DrawDrop(at + d * ring, 3f + 1.5f * f, d.Orthogonal(), BloodRed.WithAlpha(0.9f));
            }
            Arc(at, ring, 2f, BloodRed.WithAlpha(0.5f * f), 24);  // the blood joining up round it
            Circle(at, r * (0.4f + 0.6f * f), BloodRed.WithAlpha(0.3f * f), 16);
        }

        /// A player coated in a Sanguine Orb's healing blood: a red sheen with drops running off.
        void DrawHealingCoat(Player p, float t)
        {
            float r = p.Def.BodyR;
            Circle(p.Disp, r + 5f, new Rgba(1f, 0.3f, 0.4f, 0.18f + 0.06f * Mathf.Sin(t * 6f)), 20);
            Arc(p.Disp, r + 5f, 2f, new Rgba(1f, 0.4f, 0.5f, 0.6f), 20);
            for (int k = 0; k < 3; k++)
            {
                float f = MathX.Fmod(t * 1.2f + k * 0.33f + p.Id * 0.2f, 1f);
                DrawDrop(p.Disp + new V2((k - 1f) * r * 0.6f, r * 0.3f + f * r), 2.2f, new V2(0f, 1f), BloodRed.WithAlpha(0.8f * (1f - f)));
            }
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

        /// Implosion: the enemy your click would hit gets four blood ticks pointing in at it.
        void DrawImplodeMark(float t)
        {
            var me = game.Me;
            if (me == null || me.Downed || me.Weapon.Id != WeaponId.Implosion || me.StunShots > 0)
                return;
            var e = game.ImplodeTarget(me, me.AimAt, true);
            if (e == null)
                return;
            float r = e.Radius + 9f + 2f * Mathf.Sin(t * 10f);
            for (int k = 0; k < 4; k++)
            {
                V2 d = V2.FromAngle(t * 1.5f + k * MathX.Pi / 2f);
                Line(e.Disp + d * (r + 7f), e.Disp + d * r, 2.5f, BloodRed.WithAlpha(0.85f));
            }
            Arc(e.Disp, r, 1.2f, BloodRed.WithAlpha(0.35f), 24);
        }

        /// An Implosion going off: a ring and drops of blood rushing in, the core darkening, then a small pop.
        void DrawImplodeFx(Effect fx, float f)
        {
            const float inward = 0.7f;  // the collapse; the rest of the time is the pop
            if (f < inward)
            {
                float g = f / inward, ease = g * g;
                float r = fx.Radius * (1f - ease);
                Arc(fx.Pos, Mathf.Max(1f, r), 3f, fx.Color.WithAlpha(0.4f + 0.6f * g), 32);
                for (int k = 0; k < 8; k++)
                {
                    V2 d = V2.FromAngle(k * MathX.Tau / 8f + g * 1.2f);
                    DrawDrop(fx.Pos + d * (r + 6f), 2.5f, -d, fx.Color.WithAlpha(0.9f));
                }
                Circle(fx.Pos, 4f + 8f * g, BloodDark.WithAlpha(0.8f * g), 16);
            }
            else
            {
                float g = (f - inward) / (1f - inward);
                Circle(fx.Pos, 12f * (1f - g) + 2f, BloodDark.WithAlpha(1f - g), 16);
                Arc(fx.Pos, 6f + 18f * g, 2f, fx.Color.WithAlpha(1f - g), 20);
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
                float range = p.Weapon.Reach * (1f + p.Weapon.Grow * game.ExtraShots(p)) * p.Stats.ShotSpeedMult;
                for (int v = 0; v <= p.Stats.Fan; v++)  // Barrage: a beam per stack, fanned like the shots
                {
                    float aim = p.Aim + (v == 0 ? 0f : (v % 2 == 1 ? 1f : -1f) * 0.14f * ((v + 1) / 2));
                    V2 dir = V2.FromAngle(aim), side = dir.Orthogonal(), from = p.Disp + dir * p.Def.BodyR;
                    float wide = Game.AreaMult(p);  // Homing Rounds widen it
                    V2 end = game.BeamEnd(from, aim, range, p.Stats.Pierce, null, true, Game.VeinBeamHalfWidth * wide);
                    float len = from.DistanceTo(end);
                    bool hit = len < range - 1f;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 18f + p.Id);
                    float tipFade = hit ? 1f : 0.55f;
                    Line(from, end, (15f + 3f * pulse) * wide, c.WithAlpha(0.1f * tipFade));
                    Line(from, end, (6f + 1.5f * pulse) * wide, c.WithAlpha(0.55f * tipFade));
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

        /// Leech Tendrils (each screen draws them from the held enemies in the snapshot): a tendril of blood curls
        /// out to each held enemy and coils round it, drops riding it back into the caster; free tendrils writhe
        /// out across the arc, feeling for something to grab.
        void DrawLeechTendrils(float t)
        {
            var c = Defs.BulletColor(Defs.LeechShot);
            foreach (var p in game.Players.Values)
            {
                if (!game.GraspOn(p))
                    continue;
                float reach = game.GraspReach(p), arc = Game.GraspArcFor(p), half = arc / 2f;
                int max = Game.GraspTendrils + game.ExtraShots(p) + p.Stats.Fan, held = 0;
                Arc(p.Disp, p.Def.BodyR + reach, 1.5f, c.WithAlpha(0.1f), 20, p.Aim - half, arc);  // how far they reach
                for (int g = 0; g < p.Grasp.Count; g++)
                {
                    int id = p.Grasp[g];
                    Enemy e;
                    if (!game.Enemies.TryGetValue(id, out e))
                        continue;
                    int on = Game.TendrilsOn(max, p.Grasp.Count, g);  // spare tendrils pile onto what's held
                    for (int j = 0; j < on; j++, held++)
                    {
                        // Roots spread across your front, so the tendrils fan out before they bend to their prey.
                        float rootAng = p.Aim + (max <= 1 ? 0f : (held / (max - 1f) - 0.5f) * half);
                        V2 root = p.Disp + V2.FromAngle(rootAng) * p.Def.BodyR;
                        V2 toE = e.Disp - root;
                        float len = toE.Length;
                        V2 end = len > e.Radius ? e.Disp - toE / len * (e.Radius * 0.7f) : e.Disp;
                        end += toE.Orthogonal() / Mathf.Max(1f, len) * (e.Radius * 0.5f * (on <= 1 ? 0f : j / (on - 1f) - 0.5f));  // several bite in side by side
                        V2 bend = root + V2.FromAngle(rootAng) * (len * 0.45f);
                        DrawTendril(root, bend, end, 6.5f, 3f, t * 9f + id + j * 2.3f, 1f);
                        // The drain: drops riding the tendril home.
                        for (int k = 0; k < 3; k++)
                        {
                            float f = MathX.Fmod(t * 0.9f + k / 3f + id * 0.17f + j * 0.29f, 1f), u = 1f - f;
                            V2 at = Bezier(root, bend, end, u), ahead = Bezier(root, bend, end, Mathf.Max(0f, u - 0.05f)) - at;
                            DrawDrop(at, 2.4f, ahead.IsZero ? -toE.Normalized() : ahead.Normalized(), new Rgba(1f, 0.55f, 0.6f, 0.95f * Mathf.Sin(f * MathX.Pi)));
                        }
                    }
                    // Coiled round the enemy, tightening and loosening.
                    float squeeze = 0.5f + 0.5f * Mathf.Sin(t * 6f + id);
                    float coil = e.Radius + 3f - 1.5f * squeeze, spin = t * 1.8f + id;
                    Arc(e.Disp, coil, 5.5f, BloodDark.WithAlpha(0.9f), 18, spin, 4.4f);
                    Arc(e.Disp, coil, 3f, c.WithAlpha(0.95f), 18, spin, 4.4f);
                    Arc(e.Disp, coil + 4f, 4f, BloodDark.WithAlpha(0.85f), 14, spin + 3.6f, 2.6f);
                    Arc(e.Disp, coil + 4f, 2f, c.WithAlpha(0.9f), 14, spin + 3.6f, 2.6f);
                    for (int k = 0; k < 4; k++)  // thorns biting in
                    {
                        V2 d = V2.FromAngle(spin + 0.4f + k * 1.1f);
                        Line(e.Disp + d * (coil + 2f), e.Disp + d * (coil - 3f - squeeze * 2f), 1.5f, Bone.WithAlpha(0.8f));
                    }
                }
                // Free tendrils feel about the arc for something to grab.
                int free = Mathf.Min(3, max - held);
                for (int k = 0; k < free; k++)
                {
                    float sway = Mathf.Sin(t * 2.3f + k * 2.1f + p.Id);
                    float spread = free == 1 ? 0f : k / (free - 1f) - 0.5f;
                    float ang = p.Aim + spread * arc + 0.1f * sway;
                    float l = reach * (held > 0 ? 0.35f : 0.6f + 0.25f * Mathf.Sin(t * 3.1f + k * 1.7f));
                    V2 dir = V2.FromAngle(ang), side = dir.Orthogonal();
                    V2 root = p.Disp + V2.FromAngle(p.Aim + spread * 0.8f) * p.Def.BodyR;
                    V2 tip = root + dir * l + side * (l * 0.12f * Mathf.Sin(t * 4f + k));
                    DrawTendril(root, root + dir * (l * 0.5f) - side * (l * 0.1f * sway), tip, 5f, 1.2f, t * 11f + k * 3f, held > 0 ? 0.55f : 0.85f);
                }
                Circle(p.Disp + V2.FromAngle(p.Aim) * p.Def.BodyR, 5f + Mathf.Sin(t * 10f), c.WithAlpha(0.5f), 12);
            }
        }

        static V2 Bezier(V2 a, V2 b, V2 c, float u) => a * ((1f - u) * (1f - u)) + b * (2f * u * (1f - u)) + c * (u * u);

        /// One tendril along a curve from root to tip: a dark rim, a crimson core and a pale sheen, thinning from
        /// `w0` to `w1`, rippling along its length (`phase`) but fixed at both ends.
        void DrawTendril(V2 root, V2 bend, V2 tip, float w0, float w1, float phase, float alpha)
        {
            const int n = 14;
            var c = Defs.BulletColor(Defs.LeechShot);
            V2 prev = root;
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n;
                V2 at = Bezier(root, bend, tip, u), along = at - Bezier(root, bend, tip, u - 1f / n);
                V2 side = along.IsZero ? new V2(0f, 0f) : along.Normalized().Orthogonal();
                V2 pt = at + side * (3f * Mathf.Sin(u * 9f - phase) * Mathf.Sin(u * MathX.Pi));
                float w = w0 + (w1 - w0) * u;
                Line(prev, pt, w + 2.5f, BloodDark.WithAlpha(0.9f * alpha));
                Circle(pt, (w + 2.5f) / 2f, BloodDark.WithAlpha(0.9f * alpha), 6);
                Line(prev, pt, w, c.WithAlpha(alpha));
                Circle(pt, w / 2f, c.WithAlpha(alpha), 6);
                if (u < 0.7f)
                    Line(prev, pt, w * 0.3f, new Rgba(1f, 0.7f, 0.72f, 0.6f * alpha));
                if (i % 4 == 2)  // a thorn
                    Line(pt, pt + side * (w * 0.5f + 3f) + (pt - prev) * 0.6f, 1.2f, Bone.WithAlpha(0.7f * alpha));
                prev = pt;
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
                default:  // BloodShot: drops of blood
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
                if (z.Duration - z.T >= Game.PoolCongeal)  // congealed: a dark crust, cracked
                {
                    var crust = new Rgba(0.3f, 0.02f, 0.06f, 0.75f * fade);
                    Arc(z.Pos, z.Radius * 0.85f, 3f, crust, 32);
                    for (int k = 0; k < 4; k++)
                    {
                        V2 d0 = V2.FromAngle(k * 1.6f + z.Id);
                        Line(z.Pos + d0 * (z.Radius * 0.2f), z.Pos + d0.Rotated(0.3f) * (z.Radius * 0.7f), 1.5f, crust);
                    }
                }
                else
                    Circle(z.Pos + new V2(-z.Radius * 0.3f, -z.Radius * 0.3f), z.Radius * 0.15f, new Rgba(1f, 0.6f, 0.6f, 0.25f), 10);  // still wet: a glint
            }
        }
    }
}
