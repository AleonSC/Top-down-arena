using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// Enemies, minions and zones in the arena: the Singularity's hazards, ground zones, minions, clones, every
    /// enemy's body, tells, underlay and details, status effects and their icons.
    public partial class ArenaApp
    {
        float laserAngle;  // the Singularity's laser sweep, smoothed between snapshots

        /// The Singularity's three lasers (a flickering warning first, then the beams) and the cage rings.
        void DrawSingularityHazards(float t)
        {
            Enemy sing = null;
            foreach (var e in game.Enemies.Values)
                if (e.Type == EnemyType.Singularity && !game.IsSpawning(e))
                    sing = e;
            if (sing != null)
                DrawSingularityDisk(sing);  // SingularityLook.cs
            if (sing != null && (sing.Phase == Game.SingLaserWarn || sing.Phase == Game.SingLasers))
            {
                // Sweep smoothly at the known speed and ease toward the host's angle.
                int lasers = Game.SingLaserCount(sing);
                if (sing.Phase == Game.SingLasers)
                    laserAngle += Game.SingLaserSpin(sing) * ViewDt;
                float diff = MathX.Fmod(sing.Facing - laserAngle + MathX.Pi, MathX.Tau) - MathX.Pi;
                laserAngle += Mathf.Abs(diff) > 0.5f ? diff : diff * Mathf.Min(1f, ViewDt * 8f);
                for (int k = 0; k < lasers; k++)
                {
                    V2 dir = V2.FromAngle(laserAngle + MathX.Tau * k / lasers);
                    V2 from = sing.Disp + dir * sing.Def.Radius, to = sing.Disp + dir * 2000f;
                    if (sing.Phase == Game.SingLaserWarn)
                        Line(from, to, 2f, new Rgba(1f, 0.25f, 0.25f, 0.35f + 0.3f * Mathf.Sin(t * 30f)));
                    else
                    {
                        Line(from, to, Game.LaserWidth + 12f, new Rgba(1f, 0.15f, 0.2f, 0.18f));
                        Line(from, to, Game.LaserWidth, new Rgba(1f, 0.2f, 0.25f, 0.9f));
                        Line(from, to, 2.5f, new Rgba(1f, 0.85f, 0.85f, 0.95f));
                    }
                }
            }
            else if (sing != null)
                laserAngle = sing.Facing;

            var cageCol = Defs.Get(EnemyType.CageWell).Color;
            foreach (var c in game.Cages.Values)
            {
                if (c.Radius < 1f)
                    continue;
                bool mine = c.PlayerId == game.MyId;
                Arc(c.Center, c.Radius, Game.CageWallWidth + 10f, new Rgba(cageCol.R, cageCol.G, cageCol.B, 0.15f), 64);
                Arc(c.Center, c.Radius, Game.CageWallWidth, new Rgba(cageCol.R, cageCol.G, cageCol.B, mine ? 0.85f : 0.6f), 64);
                Arc(c.Center, c.Radius, 2f, new Rgba(1f, 0.9f, 1f, 0.8f), 64, t * 2f, MathX.Pi * 1.4f);  // crackle
            }
        }

        void DrawZone(Zone z, float t)
        {
            var d = z.Def;
            var c = d.Color;
            float timeLeft = MathX.Clamp01(z.T / Mathf.Max(0.01f, z.Duration));
            switch (z.Kind)
            {
                case ZoneKind.Meteor:  // warning circle that fills in while the meteor falls (ElementalistLooks.cs)
                    DrawMeteorWarning(z, t);
                    return;
                case ZoneKind.FlameWall:  // a ring of flickering flames; lower once it's done blocking
                {
                    if (fxOn)
                    {
                        DrawMagmaWall(z, t);  // FlameWallLook.cs
                        break;
                    }
                    float wall = z.GuardFrac > 0f ? 1f : 0.45f;
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.12f), 40);
                    for (int k = 0; k < 18; k++)
                        Circle(z.Pos + V2.FromAngle(MathX.Tau * k / 18f) * z.Radius, (7f + 3f * Mathf.Sin(t * 14f + k)) * wall, c.WithAlpha(0.8f * wall), 8);
                    break;
                }
                case ZoneKind.BulletRain:  // Downpour: the arena rains, the zone's drops harden into needles (BulletRainLook.cs)
                    DrawBulletRain(z, t);
                    break;
                case ZoneKind.LumenWard:  // a cone of light, god rays and drifting dust (LumenWardLook.cs)
                    DrawLumenWard(z, t);
                    return;
                case ZoneKind.Blizzard:  // a whiteout of wind-driven snow (ElementalistLooks.cs)
                    DrawBlizzard(z, t);
                    break;
                case ZoneKind.HealingRain:  // Verdant Bloom: grass and flowers sprout as it rains; motes rise, drops splash (Docs/HealingRain.md)
                {
                    float age = z.Duration - z.T;
                    float fade = MathX.Clamp01(z.T);                 // the last second
                    float lush = MathX.Clamp01(age / 4f);
                    float grow = MathX.Clamp01(age / 0.4f);
                    float rad = z.Radius * (0.3f + 0.7f * grow);
                    Circle(z.Pos, rad, c.WithAlpha(0.14f * fade), 40);
                    Arc(z.Pos, rad - 1f, 2f, c.WithAlpha(0.7f * fade), 48);
                    for (int k = 0; k < 36; k++)  // plants, each born at its own moment
                    {
                        float born = MathX.Fmod(k * 0.173f, 0.8f);
                        if (lush < born)
                            continue;
                        float sp = MathX.Clamp01((lush - born) / 0.15f);
                        float pa = MathX.Fmod(z.Id * 1.3f + k * 2.399f, MathX.Tau), pr = z.Radius * 0.92f * Mathf.Sqrt(MathX.Fmod(k * 0.381f + z.Id * 0.17f, 1f));
                        V2 at = z.Pos + V2.FromAngle(pa) * pr;
                        for (int b = -1; b <= 1; b++)
                            Line(at, at + new V2(b * 3.5f, -8f * sp), 1.6f, new Rgba(0.35f, 0.9f, 0.43f, 0.9f * fade));
                        if (k % 4 == 0 && sp > 0.7f)
                            Circle(at + new V2(0f, -9f), 2.4f, (k % 8 == 0 ? new Rgba(1f, 0.94f, 0.6f) : Rgba.White).WithAlpha(fade), 6);
                    }
                    for (int k = 0; k < 12; k++)  // motes drifting up
                    {
                        float f = MathX.Fmod(t * 0.35f + k * 0.37f + z.Id * 0.11f, 1f);
                        float ma = MathX.Fmod(k * 2.1f + z.Id, MathX.Tau), mr = z.Radius * 0.85f * Mathf.Sqrt(MathX.Fmod(k * 0.29f + z.Id * 0.13f, 1f));
                        Circle(z.Pos + V2.FromAngle(ma) * mr + new V2(Mathf.Sin(t * 2f + k) * 3f, -26f * f), 2f, new Rgba(0.35f, 1f, 0.55f, 0.8f * (1f - f) * fade), 6);
                    }
                    int drops = (int)(20f * fade);
                    for (int k = 0; k < drops; k++)  // falling drops, splashing at the end of the fall
                    {
                        float f = MathX.Fmod(t * 1.2f + k * 0.173f, 1f);
                        float da = MathX.Fmod(k * 2.7f + z.Id, MathX.Tau), dr = z.Radius * 0.9f * Mathf.Sqrt(MathX.Fmod(k * 0.331f + z.Id * 0.19f, 1f));
                        V2 ground = z.Pos + V2.FromAngle(da) * dr;
                        if (f < 0.86f)
                        {
                            V2 top = ground + new V2(0f, -46f + 46f * f / 0.86f);
                            Line(top + new V2(-3f, -8f), top, 1.6f, new Rgba(0.67f, 1f, 0.78f, 0.85f * fade));
                        }
                        else
                            Arc(ground, 3f + 9f * (f - 0.86f) / 0.14f, 1.2f, new Rgba(0.67f, 1f, 0.78f, 0.6f * fade * (1f - (f - 0.86f) / 0.14f)), 12);
                    }
                    break;
                }
                case ZoneKind.Lantern:
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f + 0.05f * Mathf.Sin(t * 5f)), 40);
                    Polygon(z.Pos, 9f, 4, MathX.Pi / 4f, c);
                    break;
                case ZoneKind.Incense:  // the Censer's incense: soft puffs of smoke drifting up and thinning
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float f = MathX.Fmod(t * 0.4f + k * 0.33f + z.Id * 0.21f, 1f);
                        V2 at = z.Pos + new V2(Mathf.Sin(z.Id * 2.1f + k * 2.3f) * z.Radius * 0.4f, -f * 18f);
                        Circle(at, z.Radius * (0.45f + 0.3f * f), c.WithAlpha(0.13f * timeLeft * (1f - 0.5f * f)), 14);
                    }
                    return;  // no timer ring: it's a trail
                }
                case ZoneKind.OfudaWard:  // an Ofuda charm lying on the ground: a paper strip with a red seal, glowing
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f + z.Id);
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.08f + 0.06f * pulse), 20);
                    V2 u = V2.FromAngle(0.3f + z.Id), s2 = u.Orthogonal();
                    SetColor(new Rgba(0.98f, 0.95f, 0.85f));
                    Quad(z.Pos - u * 9f - s2 * 4f, z.Pos + u * 9f - s2 * 4f, z.Pos + u * 9f + s2 * 4f, z.Pos - u * 9f + s2 * 4f);
                    Circle(z.Pos, 2.5f, new Rgba(0.85f, 0.15f, 0.15f), 8);
                    break;
                }
                case ZoneKind.Sanctum:  // a shining dome
                    if (fxOn)
                    {
                        DrawSanctum(z, t);  // SanctumLook.cs
                        return;
                    }
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.12f + 0.04f * Mathf.Sin(t * 3f)), 56);
                    Arc(z.Pos, z.Radius - 6f, 2f, c.WithAlpha(0.5f), 56);
                    for (int k = 0; k < 8; k++)
                        Line(z.Pos, z.Pos + V2.FromAngle(MathX.Tau * k / 8f + t * 0.3f) * 26f, 3f, c.WithAlpha(0.8f));
                    break;
                case ZoneKind.Consecrate:  // blessed ground: a golden circle with a slowly turning rune ring
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f + 0.04f * Mathf.Sin(t * 4f)), 48);
                    Arc(z.Pos, z.Radius - 4f, 2f, c.WithAlpha(0.6f), 48);
                    for (int k = 0; k < 6; k++)
                        Polygon(z.Pos + V2.FromAngle(t * 0.4f + MathX.Tau * k / 6f) * (z.Radius - 14f), 4f, 4, t, c.WithAlpha(0.8f));
                    break;
                case ZoneKind.OathWard:  // Guardian's Oath's ward: gold ground, a rune ring and two turning triangles; it fades out over its last second
                {
                    float fade = Mathf.Clamp01(z.T / 0.9f), grow = Mathf.Clamp01((z.Duration - z.T) / 0.4f), r = z.Radius * grow;
                    Circle(z.Pos, r, c.WithAlpha((0.14f + 0.04f * Mathf.Sin(t * 3f)) * fade), 56);
                    Circle(z.Pos, r * 0.6f, c.WithAlpha(0.10f * fade), 40);
                    Arc(z.Pos, r - 2f, 3f, c.WithAlpha(0.9f * fade), 56);
                    Arc(z.Pos, Mathf.Max(1f, r - 10f), 1.5f, c.WithAlpha(0.5f * fade), 56);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = t * 0.5f + MathX.Tau * k / 3f;
                        Line(z.Pos + V2.FromAngle(a) * r * 0.7f, z.Pos + V2.FromAngle(a + MathX.Tau / 3f) * r * 0.7f, 1.5f, c.WithAlpha(0.5f * fade));
                        a = -t * 0.5f + MathX.Tau * k / 3f + 1.57f;
                        Line(z.Pos + V2.FromAngle(a) * r * 0.7f, z.Pos + V2.FromAngle(a + MathX.Tau / 3f) * r * 0.7f, 1.5f, c.WithAlpha(0.5f * fade));
                    }
                    break;
                }
                case ZoneKind.HolySword:  // a gold circle and a beam while a giant sword drops down it (HallowedBladeLook.cs)
                    DrawHolySwordFall(z, t);
                    return;
                case ZoneKind.HallowedBlade:  // the planted sword, its ring and its arena-wide pulses (HallowedBladeLook.cs)
                    DrawHallowedBlade(z, t);
                    return;
                case ZoneKind.Grove:  // a great tree with roots spreading out
                    if (fxOn)
                    {
                        DrawWorldTree(z, t);  // WorldTreeLook.cs
                        return;
                    }
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f), 48);
                    for (int k = 0; k < 7; k++)
                    {
                        float a = MathX.Tau * k / 7f + 0.3f * Mathf.Sin(t + k);
                        Line(z.Pos, z.Pos + V2.FromAngle(a) * z.Radius * 0.85f, 3f, new Rgba(0.45f, 0.32f, 0.2f, 0.7f));
                    }
                    Circle(z.Pos, 34f, c.WithAlpha(0.85f), 20);
                    Circle(z.Pos, 12f, new Rgba(0.45f, 0.32f, 0.2f), 12);
                    break;
                case ZoneKind.Smoke:  // billowing grey puffs
                    if (fxOn)
                    {
                        DrawSmokeCloud(z, t);  // SmokeBombLook.cs
                        break;
                    }
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.18f), 40);
                    for (int k = 0; k < 9; k++)
                    {
                        float pa = MathX.Tau * k / 9f + t * 0.25f, pr = z.Radius * (0.35f + 0.45f * MathX.Fmod(k * 0.53f + t * 0.1f, 1f));
                        Circle(z.Pos + V2.FromAngle(pa) * pr, z.Radius * 0.3f, c.WithAlpha(0.12f), 16);
                    }
                    break;
                case ZoneKind.Mist:  // a Healing Dash footprint (NinjaLooks.cs)
                    DrawFootprint(z, c, t);
                    return;
                case ZoneKind.Ward:  // a paper talisman with a slowly turning seal ring; a bright rim for the damage it can still take
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.08f), 48);
                    Arc(z.Pos, z.Radius - 2f, 3f, c.WithAlpha(0.15f), 48);
                    Arc(z.Pos, z.Radius - 2f, 3f, c.WithAlpha(0.85f), 48, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(z.GuardFrac));
                    Arc(z.Pos, z.Radius * 0.55f, 2f, new Rgba(0.9f, 0.25f, 0.25f, 0.5f), 32, t * 0.5f, MathX.Tau * 0.8f);
                    SetColor(new Rgba(1f, 0.93f, 0.78f));
                    Quad(z.Pos + new V2(-7f, -12f), z.Pos + new V2(7f, -12f), z.Pos + new V2(7f, 12f), z.Pos + new V2(-7f, 12f));
                    Line(z.Pos + new V2(0f, -8f), z.Pos + new V2(0f, 8f), 2.5f, new Rgba(0.9f, 0.2f, 0.2f));
                    break;
                case ZoneKind.BoneSpear:  // a Bone Spear stuck in the ground, glowing to be picked up
                {
                    V2 sd = V2.FromAngle(z.Id * 1.7f);
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f + 0.06f * Mathf.Sin(t * 5f + z.Id)), 16);
                    Line(z.Pos - sd * 14f, z.Pos + sd * 10f, 3f, c);
                    SetColor(c);
                    Tri(z.Pos + sd * 16f, z.Pos + sd * 9f + sd.Orthogonal() * 3.5f, z.Pos + sd * 9f - sd.Orthogonal() * 3.5f);
                    break;
                }
                case ZoneKind.BloodPool:
                    DrawBloodZone(z, c, t);  // BloodMageLook.cs
                    break;
                case ZoneKind.VeinTrail:
                    DrawVeinTrail(z, t);  // VeinRushLook.cs
                    return;  // no timer ring: it's a trail
                case ZoneKind.Drawbridge:
                    DrawDrawbridge(z, t);  // DrawbridgeLook.cs
                    return;
                case ZoneKind.Bloodbath:
                    DrawBloodbath(z, t);  // BloodbathLook.cs (its own time ring when Visual effects are off)
                    return;
                case ZoneKind.HoneyPool:
                    DrawHoneyPool(z, c, t);  // QueenLootLook.cs
                    break;
                default:  // Haven
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f + 0.04f * Mathf.Sin(t * 4f)), 48);
                    break;
            }
            Arc(z.Pos, z.Radius, 2.5f, c.WithAlpha(0.7f), 48, -MathX.Pi / 2f, MathX.Tau * timeLeft);
        }

        void DrawMinion(Minion m, float t)
        {
            var d = m.Def;
            var c = d.Color;
            float rad = d.Radius * m.Scale, block = d.BlockRadius * m.Scale;
            if (m.T >= 0f && m.T < 1.5f && MathX.Fmod(t, 0.2f) < 0.1f)
                c = c.WithAlpha(0.4f);  // about to vanish
            bool staggered = d.BlockRadius > 0f && m.GuardFrac <= 0f;
            if (staggered)  // took too much fire: dazed, not blocking for a moment
            {
                c = c.Darkened(0.4f);
                for (int k = 0; k < 3; k++)
                    Circle(m.Disp + V2.FromAngle(t * 5f + MathX.Tau * k / 3f) * (rad + 6f), 2.5f, new Rgba(1f, 0.9f, 0.4f, 0.9f), 6);
            }
            if (block > rad && m.Kind != MinionKind.HiveDrone)  // the guardian spirit's bullet-eating field, fading as it soaks (a drone just guards its own little patch)
            {
                float g = MathX.Clamp01(m.GuardFrac);
                Circle(m.Disp, block, c.WithAlpha(0.04f + 0.1f * g), 40);
                Arc(m.Disp, block, 1f + 2f * g, c.WithAlpha(0.25f + 0.45f * g), 40);
            }
            if (Game.IsTitanLike(m.Kind))
            {
                DrawTitan(m, t);  // TitanLook.cs
                return;
            }
            if (m.Kind == MinionKind.Hydra)
            {
                DrawHydra(m, t);  // HydraLook.cs
                return;
            }
            if (m.Kind == MinionKind.Burrower)
            {
                DrawBurrower(m, t);  // BurrowerLook.cs
                return;
            }
            if (m.Kind == MinionKind.Wall)
            {
                DrawGolemWall(m, t);  // GolemWallLook.cs
                return;
            }
            if (m.Kind == MinionKind.CloneWall)
            {
                DrawCloneWall(m, t);  // CloneWallLook.cs
                return;
            }
            if (m.Kind == MinionKind.Clone)
            {
                DrawClone(m, t);
                return;
            }
            if (m.Kind == MinionKind.Nimbus)
            {
                DrawNimbus(m, t);  // NimbusLook.cs
                return;
            }
            if (m.Kind == MinionKind.Treant)
            {
                DrawTreant(m, t, staggered);  // RootLook.cs
                return;
            }
            if (m.Kind == MinionKind.Thrall)
            {
                DrawThrall(m, t);  // ForcePactLook.cs
                return;
            }
            if (m.Kind == MinionKind.WardenGuard)
            {
                DrawWardenGuard(m, t);  // WardenLook.cs
                return;
            }
            if (m.Kind == MinionKind.SoulLantern)
            {
                DrawSoulLantern(m, t);  // HealerKitLooks.cs
                return;
            }
            if (m.Kind == MinionKind.SoulGhost)
            {
                DrawSoulGhost(m, t);
                return;
            }
            if (m.Kind == MinionKind.HiveDrone)
            {
                DrawHiveDrone(m, t);  // QueenLootLook.cs
                return;
            }
            if (m.Kind == MinionKind.HiveBomber)
            {
                DrawHiveBomber(m, t);  // QueenLootLook.cs
                return;
            }
            if (m.Kind == MinionKind.Scythe)
            {
                DrawTwinScythe(m, t);  // ReaperLootLook.cs
                return;
            }
            if (m.Kind == MinionKind.ThrownScythe)
            {
                DrawThrownScythe(m, t);  // ReaperLootLook.cs
                return;
            }
            if (m.Kind == MinionKind.Bear)
            {
                DrawBear(m, t);  // BearLooks.cs
                return;
            }
            if (m.Kind == MinionKind.Butterfly)
            {
                DrawButterfly(m, t);  // WorldTreeLook.cs
                return;
            }
            if (m.Kind == MinionKind.Bat)
            {
                DrawBat(m, t);  // BatLook.cs
                return;
            }
            float rot = d.Move == MinionMove.Static ? 0f : m.Aim;
            if (m.Kind == MinionKind.Wisp || m.Kind == MinionKind.Sprite)
                Circle(m.Disp, d.Radius * 2.2f, c.WithAlpha(0.2f), 12);
            if (m.Kind == MinionKind.LightOrb)  // a soft halo that pulses
                Circle(m.Disp, rad * (2.4f + 0.3f * Mathf.Sin(t * 6f + m.Id)), c.WithAlpha(0.22f), 16);
            if (m.Kind == MinionKind.Phoenix)
                for (int k = 0; k < 2; k++)  // flapping wings
                    Line(m.Disp, m.Disp + V2.FromAngle(m.Aim + MathX.Pi * (0.6f + k * 0.8f) + 0.4f * Mathf.Sin(t * 12f)) * 20f, 4f, c);
            Polygon(m.Disp, rad, d.Sides, rot, c);
            if (d.Attack == MinionAttack.Shoot)
                Line(m.Disp, m.Disp + V2.FromAngle(m.Aim) * (rad + 7f), 4f, c);
            if (m.Kind == MinionKind.Sprite)
                Line(m.Disp - new V2(3, 0), m.Disp + new V2(3, 0), 1.5f, Rgba.White);
            if (m.T >= 0f && m.Duration > 0f)
                Arc(m.Disp, rad + 5f, 2f, c.WithAlpha(0.5f), 24, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
        }

        /// A Soul Ghost: a pale, see-through copy of its healer with a gun toward what it's shooting, wisps trailing
        /// off it, its time running out around it, and a health bar once it's hurt.
        void DrawSoulGhost(Minion m, float t)
        {
            Player owner;
            Rgba col = game.Players.TryGetValue(m.Owner, out owner) ? owner.Def.Color.Mix(m.Def.Color, 0.5f) : m.Def.Color;
            float r = owner != null ? owner.Def.BodyR : m.Def.Radius;
            float a = 0.4f + 0.12f * Mathf.Sin(t * 7f + m.Id);
            if (m.T >= 0f && m.T < 1f)
                a *= m.T;  // fading out as the link ends
            Circle(m.Disp, r + 9f, m.Def.Color.WithAlpha(0.12f * a / 0.4f), 20);
            for (int k = 0; k < 3; k++)  // wisps drifting off behind it
            {
                V2 w = m.Disp - V2.FromAngle(m.Aim + 0.5f * (k - 1)) * (r + 6f + 4f * Mathf.Sin(t * 5f + k));
                Circle(w, 3f - k * 0.5f, m.Def.Color.WithAlpha(a * 0.8f), 8);
            }
            Circle(m.Disp, r, col.WithAlpha(a), 24);
            Arc(m.Disp, r, 2f, m.Def.Color.WithAlpha(a + 0.3f), 24);
            V2 dir = V2.FromAngle(m.Aim);
            Line(m.Disp + dir * (r * 0.5f), m.Disp + dir * (r + 8f), 4f, col.WithAlpha(a + 0.2f));
            if (m.T >= 0f && m.Duration > 0f)
                Arc(m.Disp, r + 5f, 2f, m.Def.Color.WithAlpha(0.5f), 24, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
            if (m.GuardFrac < 0.999f)
                Bar(m.Disp + new V2(-15f, -r - 12f), 30f, 3.5f, Mathf.Clamp01(m.GuardFrac), new Rgba(0.4f, 1f, 0.55f));
        }

        /// A Shadow Clone: a dark copy of its ninja, flickering, with its time running out around it.
        static readonly Rgba InkEcho = new Rgba(0.65f, 0.55f, 1f);

        /// Thousand Clones, "Ink Echo": a violet brush-stroke silhouette standing in a pool of ink, with a ring that
        /// breathes and a thin health bar (Minion.GuardFrac) that turns red when it is nearly gone.
        void DrawClone(Minion m, float t)
        {
            float a = 0.9f + 0.08f * Mathf.Sin(t * 5f + m.Id);
            V2 c = m.Disp, dir = V2.FromAngle(m.Aim);
            float breathe = Mathf.Sin(t * 3f + m.Id * 1.7f);
            Circle(c, 30f, InkEcho.WithAlpha(0.10f), 28);  // the pool
            Circle(c, 22f, InkEcho.WithAlpha(0.18f), 24);
            Arc(c, 23f + breathe * 1.5f, 2f, InkEcho.WithAlpha(0.55f), 32);
            Circle(c, 13f, InkEcho.WithAlpha(a), 20);  // robe
            Circle(c + dir * 4f, 8f, InkEcho.Darkened(0.55f).WithAlpha(a), 16);  // head
            Arc(c, 13f, 3f, NinjaCloth.WithAlpha(a), 16, m.Aim - 1.0f, 2.0f);  // the headband
            Line(c + dir * 8f, c + dir * 24f, 2f, InkEcho.WithAlpha(a));  // blade
            float f = Mathf.Clamp01(m.GuardFrac);
            V2 bar = c + new V2(-15f, 26f);
            Line(bar, bar + new V2(30f, 0f), 4f, new Rgba(0f, 0f, 0f, 0.6f));
            if (f > 0f)
                Line(bar, bar + new V2(30f * f, 0f), 4f, f > 0.4f ? InkEcho : new Rgba(1f, 0.38f, 0.44f));
        }

        /// An enemy, in layers: spawn ring, melee spikes, its type's effects behind the body, elite ring and
        /// status (tint, flames, mark), the body, ice, its type's details on top, then the health bar and icons.
        void DrawEnemy(Enemy e, float t)
        {
            Rgba col = e.Def.Color;
            V2 pos = e.Disp;
            float r = (e.Type == EnemyType.Singularity ? e.Def.Radius : e.Radius) * JuiceScale(e);  // its Radius grows with the event horizon (SingularityLook.cs); JuiceScale: a hit's bulge (Juice.cs)
            bool spawning = game.IsSpawning(e);
            if (spawning)
            {
                Arc(pos, r + 6f + 4f * Mathf.Sin(t * 12f), 2f, col.WithAlpha(0.6f), 32);
                col = col.WithAlpha(0.35f);
            }
            if (e.Type == EnemyType.Gatekeeper || e.Type == EnemyType.Shadow)
            {
                bool hit = e.Flash > 0f || JuiceFlash(e);
                if (e.Type == EnemyType.Gatekeeper)
                    DrawGatekeeper(e, pos, r, hit, spawning, t);  // ChallengeLook.cs
                else
                    DrawShadow(e, pos, r, hit, spawning, t);
                return;
            }
            if (e.Type == EnemyType.HiveBomber && e.Phase >= 1)
            {
                DrawFlankBomber(e, pos, r, e.Flash > 0f || JuiceFlash(e), spawning, t);  // Swarm's flanking bombers (QueenLook.cs)
                return;
            }
            // Melee enemies are spiky, so you can tell rushers from shooters at a glance.
            if (e.Def.Melee && !spawning && e.Type != EnemyType.Swarmer && e.Type != EnemyType.HiveEgg && e.Type != EnemyType.ReaperPillar && !Zombies.Is(e.Type) && !Game.IsNonShooter(e.Type))
                DrawMeleeSpikes(e, pos, r, col, t);
            if (Zombies.Is(e.Type))
                DrawZombieUnder(e, pos, r, t);  // its spore haze, frenzy streaks and the Crawler's hold (ZombieLook.cs)
            DrawEnemyShadow(e, pos, r);  // EnemyPresenceLook.cs
            col = DrawEnemyUnderlay(e, pos, r, col, spawning, t);
            if (Game.IsNonShooter(e.Type) && !spawning)
                DrawNonShooterUnder(e, pos, r, t);  // the fuse, the aim line and harpoon, a pod's lasers (NonShooterLooks.cs)
            if (e.Elite)
                Arc(pos, r + 5f, 2.5f, new Rgba(1f, 0.85f, 0.3f, 0.9f), 32);
            col = DrawEnemyStatus(e, pos, r, col, t);
            if (e.Flash > 0f || JuiceFlash(e))
                col = col.Mix(Rgba.White, HitFlashMix(e));  // softer under steady fire (Juice.cs)
            if (Zombies.Is(e.Type))
                DrawZombieBody(e, pos, r, col, t);  // its own shape (ZombieLook.cs)
            else if (e.Type == EnemyType.Swarmer)
                DrawChomper(e, pos, r, col, t);
            else if (Game.IsNonShooter(e.Type))
                DrawNonShooterBody(e, pos, r, col, t);  // NonShooterLooks.cs
            else
                Polygon(pos, r, e.Def.Sides, EnemyRotation(e, t), col);
            if (e.Frozen && !LeapCaged(e))  // (a Leap Slam's stun draws its own cage: LeapSlamLook.cs)
            {
                if (fxOn)
                {
                    if (!DrawBloodIce(e, pos, r, t) && !DrawTombSpires(e, pos, r, t))  // a Blood Freeze's crystal (BloodFreezeLook.cs), a Glacial Tomb's spires (GlacialTombLook.cs)
                        DrawIceBlockFacets(e, pos, r, t);  // FrostNovaLook.cs
                }
                else
                    DrawIceBlock(e, pos, r, t);
            }
            if (bfStates.Count > 0)
                DrawBloodShatter(e, pos, r, t);  // the crystal breaking as the freeze ends (BloodFreezeLook.cs)
            if (tombStates.Count > 0)
                DrawTombShatter(e, pos, r, t);  // the spires breaking as the freeze ends (GlacialTombLook.cs)
            DrawEnemyDetails(e, pos, r, col, t);
            DrawJobMarker(e, pos, r);  // JobMarkerLook.cs
            DrawVarietyOnEnemy(e, pos, r, t);  // bubbles, trait markers (VarietyLook.cs)
            // Bosses get a bar at the top of the screen instead.
            bool bar = e.Hp < e.MaxHp && !e.Def.IsBoss && !spawning && e.Type != EnemyType.SapperMine;
            if (bar)
            {
                float w = Mathf.Max(30f, r * 2f);
                GhostBar(pos + new V2(-w / 2f, -r - 10f), w, 4f, e.Hp / e.MaxHp, new Rgba(1f, 0.3f, 0.3f), EnemyKey(e.Id));  // HealthBars.cs
            }
            if (e.Caged)
                DrawBoneCage(pos, r, t);
            if (e.Hemorrhage > 0)
                DrawHemorrhage(e, pos, r, t);  // BloodMageLook.cs
            for (int k = 0; k < e.StarsStuck && k < 8; k++)  // Shuriken stars stuck in it, waiting to be recalled
                DrawStar(pos + V2.FromAngle(k * 2.4f + e.Id) * (r * 0.75f), 4f, k * 0.7f, Defs.BulletColor(Defs.StarShot));
            foreach (var hunter in game.Players.Values)
                if (hunter.PreyId == e.Id && hunter.Weapon.Id == WeaponId.WolfWhistle)
                {
                    DrawPreyMark(pos, r, t);  // the Wolf Whistle's pack is hunting it
                    break;
                }
            DrawStatusIcons(e, pos + new V2(0f, -r - (bar ? 25f : 17f)), t);
        }

        /// A Swarmer: a white disc with a wedge for a mouth that points where it runs. No eyes. The mouth gapes wider as it
        /// closes in on you, and chews quickly while it walks.
        void DrawChomper(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float nearest = 9999f;
            foreach (var p in game.Players.Values)
                if (!p.Downed)
                    nearest = Mathf.Min(nearest, p.Disp.DistanceTo(pos));
            float near = MathX.Clamp01(1f - nearest / 120f);
            float chew = 0.5f + 0.5f * Mathf.Sin(t * 11f + e.Id * 1.7f);
            float open = 0.18f + 0.5f * chew * (0.5f + 0.5f * near);
            float face = e.Facing;
            SetColor(col.Darkened(0.45f));
            const int segs = 16;
            V2 prev = pos + V2.FromAngle(face + open) * r;
            for (int i = 1; i <= segs; i++)
            {
                V2 next = pos + V2.FromAngle(face + open + (MathX.Tau - 2f * open) * i / segs) * r;
                Tri(pos, prev, next);
                prev = next;
            }
            V2 lip0 = pos + V2.FromAngle(face + open) * r, lip1 = pos + V2.FromAngle(face - open) * r;
            Arc(pos, r, 2.5f, col, 20, face + open, MathX.Tau - 2f * open);
            Line(lip0, pos, 2.5f, col);
            Line(lip1, pos, 2.5f, col);
        }

        void DrawMeleeSpikes(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            if (e.Type == EnemyType.Spiker || e.Type == EnemyType.SpikeBlock)
            {
                DrawFaceSpikes(e, pos, r, col);  // MeleeGrunts: a spike on each face
                return;
            }
            int spikes = e.Type == EnemyType.Brute ? 10 : 8;
            float spin = e.Type == EnemyType.Buzzsaw ? t * 7f + e.Id : t * 0.6f;  // a Buzzsaw's turn about once a second
            for (int k = 0; k < spikes; k++)
            {
                float a = spin + MathX.Tau * k / spikes;
                V2 d = V2.FromAngle(a), side = d.Orthogonal();
                SetColor(col.Mix(new Rgba(1f, 0.95f, 0.85f, col.A), 0.35f));
                Tri(pos + d * (r + 6f), pos + d * (r - 2f) + side * (r * 0.28f), pos + d * (r - 2f) - side * (r * 0.28f));
            }
        }

        /// A Swarmer's three long thin spikes (one per face of its triangle) and a Spike Block's four big ones (one per face of its
        /// square). The body is turned so the faces line up with the spikes (EnemyRotation). A Spike Block's spikes glow while it winds up and leaps.
        void DrawFaceSpikes(Enemy e, V2 pos, float r, Rgba col)
        {
            bool block = e.Type == EnemyType.SpikeBlock;
            int faces = block ? 4 : 3;
            bool hot = block && (e.Phase == 1 || e.Phase == 2);
            Rgba c = hot ? new Rgba(1f, 0.92f, 0.9f, col.A) : col.Mix(new Rgba(1f, 0.95f, 0.85f, col.A), 0.45f);
            float apothem = block ? r * 0.7071f : r * 0.5f;       // centre to the middle of a face
            float len = block ? r * 1.05f : r * 1.4f;              // how far a spike sticks out of its face
            float half = block ? r * 0.4f : r * 0.16f;             // half the spike's width at its base
            float first = block ? e.Facing : e.Facing + MathX.Pi / 3f;  // a Spiker's faces lie between its corners
            for (int k = 0; k < faces; k++)
            {
                V2 d = V2.FromAngle(first + MathX.Tau * k / faces), side = d.Orthogonal();
                SetColor(c);
                Tri(pos + d * (apothem + len), pos + d * (apothem - 1f) + side * half, pos + d * (apothem - 1f) - side * half);
            }
        }

        /// Winding up: the shared lane. Leaping: a smear behind it. Resting: dim.
        void DrawSpikeBlockTell(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            DrawLungeLane(e, pos, r, t);  // the wind-up: the shared lane (LungeLaneLook.cs)
            if (e.Phase == 2)
                for (int k = 1; k <= 3; k++)
                    Circle(pos - V2.FromAngle(e.Facing) * (r * 0.9f * k), r * (1f - 0.2f * k), col.WithAlpha(0.35f / k), 12);
        }

        /// Each type's effects behind its body. Returns the body colour (a Phantom flickers).
        Rgba DrawEnemyUnderlay(Enemy e, V2 pos, float r, Rgba col, bool spawning, float t)
        {
            switch (e.Type)
            {
                case EnemyType.Spinner:
                    if (e.Phase == 1 && !spawning)  // bursting: a hot glow
                        Circle(pos, r + 8f + 3f * Mathf.Sin(t * 20f), new Rgba(1f, 0.6f, 0.2f, 0.25f), 24);
                    break;
                case EnemyType.Boss:
                    DrawWardenUnderlay(e, pos, r, t);  // WardenLook.cs
                    break;
                case EnemyType.Lunger:
                    DrawLungerTell(e, pos, r, col, t);
                    break;
                case EnemyType.SpikeBlock:
                    DrawSpikeBlockTell(e, pos, r, col, t);
                    break;
                case EnemyType.Charger:
                    DrawChargerTell(e, pos, r, col, t);
                    break;
                case EnemyType.RiotShield:
                    DrawLungeLane(e, pos, r, t);  // its bash wind-up, same lane as the others (LungeLaneLook.cs)
                    break;
                case EnemyType.HiveBomber:  // a hot glow and a faint streak along its flight line
                    if (!spawning)
                    {
                        Circle(pos, r + 5f + 2f * Mathf.Sin(t * 30f), new Rgba(1f, 0.5f, 0.15f, 0.3f), 16);
                        V2 ahead = V2.FromAngle(e.Facing);
                        Line(pos, pos + ahead * 90f, 3f, new Rgba(1f, 0.5f, 0.2f, 0.18f));
                    }
                    break;
                case EnemyType.HiveQueen:
                    DrawQueenUnderlay(e, pos, r, t);  // QueenLook.cs
                    break;
                case EnemyType.TwinA:
                case EnemyType.TwinB:
                    DrawTwinShell(e, pos, r, t);
                    DrawStageShiftBoss(e, pos, r, t);  // changing stage (StageShiftLook.cs)
                    break;
                case EnemyType.DualReaper:
                    DrawDualUnderlay(e, pos, r, t);  // ReaperLook.cs
                    break;
                case EnemyType.Duelist:
                    DrawStageShiftBoss(e, pos, r, t);  // changing stage (StageShiftLook.cs)
                    break;
                case EnemyType.Singularity:
                    DrawSingularityUnderlay(e, pos, r, col, t);  // SingularityLook.cs
                    break;
                case EnemyType.Colossus:
                    DrawColossusUnderlay(e, pos, r, t);  // ColossusLook.cs
                    if (e.Colossus != null && e.Colossus.Stage >= ColStage.MoltenCore)
                        col = new Rgba(0.36f, 0.25f, 0.2f);  // its shell burnt dark over the molten core
                    break;
                case EnemyType.SingShard:
                    DrawSingShardUnderlay(e, pos, r, col, t);
                    break;
                case EnemyType.Phantom:
                    col = col.WithAlpha((menuPreview ? 0.75f : 0.45f) + (menuPreview ? 0.2f : 0.35f) * Mathf.Sin(t * 9f + e.Id));  // flickers in and out
                    break;
                case EnemyType.Juggernaut:
                    Arc(pos, r + 4f, 5f, new Rgba(0.75f, 0.75f, 0.8f, 0.9f), 8);  // armor plating
                    break;
                case EnemyType.Nest:
                    Circle(pos, r + 6f + 3f * Mathf.Sin(t * 3f), col.WithAlpha(0.2f), 24);
                    break;
                case EnemyType.Lich:
                    col = DrawLichUnderlay(e, pos, r, col, t);  // LichLook.cs
                    break;
                case EnemyType.Phylactery:
                    Circle(pos, r * 2.2f, col.WithAlpha(0.15f + 0.1f * Mathf.Sin(t * 6f)), 24);
                    break;
                case EnemyType.GravityWell:
                    if (!spawning)
                        DrawWellPull(e, pos, r, col, t);
                    break;
                case EnemyType.StormHerald:
                    DrawHeraldUnderlay(e, pos, r, t);  // HeraldLook.cs
                    break;
                case EnemyType.Devourer:
                    DrawDevourerUnderlay(e, pos, r, t);  // DevourerLook.cs
                    break;
                case EnemyType.Eclipse:
                    DrawEclipseCorona(e, pos, r, col, t);
                    break;
            }
            return col;
        }

        /// The Warden's cross streams: faint guides along the four arms (hidden while it moves).
        void DrawWardenCrossGuides(Enemy e, V2 pos, float r)
        {
            for (int k = 0; k < 4; k++)
            {
                V2 d = V2.FromAngle(e.Facing + MathX.Pi / 2f * k);
                Line(pos + d * r, pos + d * (r + 420f), 10f, new Rgba(1f, 0.35f, 0.45f, 0.07f));
                Line(pos + d * r, pos + d * (r + 420f), 2f, new Rgba(1f, 0.45f, 0.55f, 0.18f));
            }
        }

        /// Winding up: the shared lane. Leaping: a smear behind it.
        void DrawLungerTell(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            DrawLungeLane(e, pos, r, t);  // the wind-up: the shared lane (LungeLaneLook.cs)
            if (e.Phase == 2)
                for (int k = 1; k <= 3; k++)
                    Circle(pos - V2.FromAngle(e.Facing) * (r * 0.9f * k), r * (1f - 0.2f * k), col.WithAlpha(0.35f / k), 12);
        }

        /// Wind-up: the shared lane. Dashing: a smear behind it.
        void DrawChargerTell(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            DrawLungeLane(e, pos, r, t);  // the wind-up: the shared lane (LungeLaneLook.cs)
            if (e.Phase == 2)
                Circle(pos - V2.FromAngle(e.Facing) * r, r * 0.9f, col.WithAlpha(0.3f), 16);
        }

        /// The Twin Reapers: an enraged glow, or Soul Bond's immune shell with orbiting runes.
        void DrawTwinShell(Enemy e, V2 pos, float r, float t)
        {
            if (e.Phase == 1)  // enraged
                Circle(pos, r + 10f + 4f * Mathf.Sin(t * 14f), new Rgba(1f, 0.2f, 0.2f, 0.25f), 32);
            else if (e.Phase == 2)  // Soul Bond: immune, wrapped in a pale shell with orbiting runes
            {
                Circle(pos, r + 14f, new Rgba(0.9f, 0.9f, 1f, 0.18f), 40);
                Arc(pos, r + 14f, 3f, new Rgba(0.95f, 0.95f, 1f, 0.85f), 40);
                for (int k = 0; k < 5; k++)
                    Polygon(pos + V2.FromAngle(t * 1.5f + MathX.Tau * k / 5f) * (r + 14f), 4f, 4, t * 3f, new Rgba(1f, 1f, 1f, 0.9f));
            }
        }

        /// A gravity well's pull range, with rings falling inward.
        void DrawWellPull(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            Circle(pos, Game.WellPullRange, col.WithAlpha(0.05f), 40);
            for (int k = 0; k < 3; k++)
            {
                float f = 1f - MathX.Fmod(t * 0.9f + k / 3f + e.Id * 0.13f, 1f);
                Arc(pos, r + 6f + f * (Game.WellPullRange - r - 6f), 1.5f, col.WithAlpha(0.4f * (1f - f)), 40);
            }
        }

        /// Status effects tint the body (frozen, chilled, burning with flames licking up) and Mark for Death
        /// adds a reticle; icons above it say which ones (see DrawStatusIcons). Returns the tinted colour.
        Rgba DrawEnemyStatus(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            if (e.Frozen && !LeapCaged(e))
            {
                if (!(fxOn && bfStates.ContainsKey(e.Id)))  // (a Blood Freeze's crystal shows the enemy through it: BloodFreezeLook.cs)
                    col = col.Mix(new Rgba(0.75f, 0.92f, 1f), 0.85f);  // frosted almost white-blue
            }  // frosted almost white-blue
            else if (e.Chilled)
                col = col.Mix(IceColor, 0.35f);
            if (e.Burning)
            {
                col = col.Mix(FireColor, 0.25f + 0.15f * Mathf.Sin(t * 18f + e.Id));
                // Flames licking up off the top of it.
                for (int k = 0; k < 4; k++)
                {
                    float f = MathX.Fmod(t * 1.6f + k * 0.25f + e.Id * 0.17f, 1f);
                    V2 at = pos + new V2((k - 1.5f) * r * 0.45f + 2f * Mathf.Sin(t * 9f + k), -r * 0.4f - f * r * 0.9f);
                    Circle(at, (1f - f) * (4f + r * 0.28f), FireColor.Mix(new Rgba(1f, 0.85f, 0.3f), f).WithAlpha(0.9f * (1f - f)), 8);
                }
            }
            if (e.Wet)
                col = DrawWet(e, pos, r, col, t);  // Tsunami (ElementalistLooks.cs)
            if (e.Bleeding)
            {
                col = col.Mix(BleedColor, 0.18f);
                // Drops falling off it.
                for (int k = 0; k < 3; k++)
                {
                    float f = MathX.Fmod(t * 1.3f + k * 0.33f + e.Id * 0.21f, 1f);
                    V2 at = pos + new V2((k - 1f) * r * 0.5f, r * 0.2f + f * r * 1.1f);
                    Circle(at, 2.2f + r * 0.08f, BleedColor.WithAlpha(0.85f * (1f - f)), 6);
                }
                if (fxOn && e.BleedHits > 0)
                    DrawBleedScars(e, pos, r, t);  // claw scars, one set per two stacked hits (BearLooks.cs)
            }
            if (e.Marked && fxOn)  // Mark for Death: the reticle lock and skull (SoldierLooks.cs)
            {
                col = col.Mix(new Rgba(1f, 0.42f, 0.35f), 0.2f);
                DrawMarkLook(e, pos, r, t);
            }
            else if (e.Marked)  // Mark for Death: a rotating red reticle
            {
                var mc = new Rgba(1f, 0.2f, 0.2f, 0.9f);
                for (int k = 0; k < 4; k++)
                    Arc(pos, r + 12f, 2.5f, mc, 6, t * 2f + k * MathX.Pi / 2f, 0.9f);
            }
            return col;
        }

        /// How the body is turned: toward its aim, spinning, or slowly drifting round.
        static float EnemyRotation(Enemy e, float t)
        {
            switch (e.Type)
            {
                case EnemyType.SpikeBlock:
                    return e.Facing + MathX.Pi / 4f;  // a corner is 45 degrees off a face, and its spikes point out of the faces
                case EnemyType.Spiker:
                case EnemyType.Swarmer:
                case EnemyType.Charger:
                case EnemyType.Lunger:
                case EnemyType.Brute:
                case EnemyType.Shard:
                case EnemyType.Mortar:
                case EnemyType.Shielder:
                case EnemyType.HiveBomber:
                case EnemyType.RiotShield:
                case EnemyType.GasGrenadier:
                case EnemyType.Shambler:
                case EnemyType.Runner:
                case EnemyType.Spitter:
                case EnemyType.Crawler:
                case EnemyType.Bloater:
                case EnemyType.Shrieker:
                case EnemyType.Mender:
                case EnemyType.FuseRunner:
                case EnemyType.Hookman:
                case EnemyType.Sapper:
                    return e.Facing;  // point where they're aiming (zombies: where they're going)
                case EnemyType.Spinner:
                    return t * (e.Phase == 1 ? 7f : 1.5f) + e.Id;  // revs up while it's spraying
                case EnemyType.Singularity:
                    return t * (e.Phase == 1 ? -3f : 1f);
                case EnemyType.Duelist:
                case EnemyType.Decoy:
                    return e.Facing;  // its point is its blade (DuelistLook.cs); the decoys copy it
                case EnemyType.Medic:
                    return MathX.Pi / 4f;
                default:
                    return t * 0.4f + e.Id;
            }
        }

        /// Frozen: encased in an angular block of ice with a glint.
        void DrawIceBlock(Enemy e, V2 pos, float r, float t)
        {
            Arc(pos, r + 5f, 3f, IceColor.WithAlpha(0.95f), 6, MathX.Pi / 6f, MathX.Tau);
            Polygon(pos, r + 5f, 6, MathX.Pi / 6f, IceColor.WithAlpha(0.35f));
            float g = MathX.Fmod(t * 0.7f + e.Id * 0.3f, 1f);
            Line(pos + new V2(-r * 0.6f, -r * 0.2f + g * r * 0.5f), pos + new V2(-r * 0.1f, -r * 0.7f + g * r * 0.5f), 2f, Rgba.White.WithAlpha(0.8f));
        }

        /// Each type's details on top of its body.
        /// A Shielder's shield: dimmer and more cracked as it wears (Phase 1-3); gone once it shatters (Phase 4).
        void DrawShielderShield(Enemy e, V2 pos, float r)
        {
            if (e.Phase >= Game.ShieldBroken)
                return;
            float from = e.Facing - 1.13f, span = 2.26f;
            Arc(pos, r + 7f, 4f, new Rgba(0.7f, 0.85f, 1f, 0.9f - 0.15f * e.Phase), 20, from, span);
            // Cracks: fixed for each Shielder (from its id), more of them at each step.
            for (int k = 0; k < e.Phase * 2; k++)
            {
                float u = 0.12f + 0.76f * (((e.Id * 37 + k * 61) % 97) / 96f), a = from + span * u;
                V2 p0 = pos + V2.FromAngle(a) * (r + 9.5f), p1 = pos + V2.FromAngle(a + (k % 2 == 0 ? 0.06f : -0.06f)) * (r + 7f), p2 = pos + V2.FromAngle(a + (k % 2 == 0 ? 0.02f : -0.02f)) * (r + 4.5f);
                Line(p0, p1, 1.4f, new Rgba(0.15f, 0.2f, 0.35f, 0.95f));
                Line(p1, p2, 1.4f, new Rgba(0.15f, 0.2f, 0.35f, 0.95f));
            }
        }

        void DrawEnemyDetails(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.Duelist:
                case EnemyType.Decoy:
                    DrawDuelistBlade(e, pos, r, t);  // DuelistLook.cs
                    break;
                case EnemyType.Colossus:
                    DrawColossusDetails(e, pos, r, t);  // ColossusLook.cs
                    break;
                case EnemyType.Brute:
                    DrawBruteFace(e, pos, r, col);
                    break;
                case EnemyType.Rioter:
                    DrawRioterDetails(e, pos, r, col);  // WardenLook.cs
                    break;
                case EnemyType.Guard:
                    DrawGuardDetails(e, pos, r, t);  // WardenLook.cs
                    break;
                case EnemyType.RiotShield:
                    DrawRiotShield(e, pos, r, t);  // WardenLook.cs
                    break;
                case EnemyType.GasGrenadier:
                    DrawGrenadier(e, pos, r, t);  // WardenLook.cs
                    break;
                case EnemyType.HiveEgg:
                    DrawEggDetails(e, pos, r, t);  // QueenLook.cs
                    break;
                case EnemyType.DualReaper:
                    DrawDualDetails(e, pos, r, t);  // ReaperLook.cs
                    break;
                case EnemyType.ReaperPillar:
                    DrawPillarDetails(e, pos, r, t);  // ReaperLook.cs
                    break;
                case EnemyType.Shielder:
                    DrawShielderShield(e, pos, r);
                    break;
                case EnemyType.Medic:
                    Line(pos - new V2(r * 0.5f, 0), pos + new V2(r * 0.5f, 0), 3f, Rgba.White);
                    Line(pos - new V2(0, r * 0.5f), pos + new V2(0, r * 0.5f), 3f, Rgba.White);
                    break;
                case EnemyType.Mortar:
                    Line(pos, pos + V2.FromAngle(e.Facing) * (r + 8f), 6f, col);
                    break;
                case EnemyType.Splitter:
                    Line(pos - new V2(r * 0.6f, 0), pos + new V2(r * 0.6f, 0), 2f, col.WithAlpha(0.6f));
                    Line(pos - new V2(0, r * 0.6f), pos + new V2(0, r * 0.6f), 2f, col.WithAlpha(0.6f));
                    break;
                case EnemyType.Singularity:
                    Circle(pos, r * 0.45f, new Rgba(0f, 0f, 0f, 1f), 24);
                    Arc(pos, r * 0.45f, 2f, Rgba.White.WithAlpha(0.6f), 24);
                    break;
                case EnemyType.Devourer:
                    DrawDevourerMouth(e, pos, r, t);  // DevourerLook.cs: its mouth opens when it feeds
                    break;
                case EnemyType.Morsel:
                    DrawMorsel(e, pos, r, t);  // DevourerLook.cs
                    break;
                case EnemyType.Eclipse:
                    Circle(pos, r * 0.8f, new Rgba(0.02f, 0.02f, 0.04f, 1f), 32);
                    break;
                case EnemyType.Phylactery:
                    Polygon(pos, r * 0.5f, 4, -t * 2f, Rgba.White.WithAlpha(0.8f));
                    break;
                case EnemyType.BallLightning:
                    DrawBallLightning(e, pos, r, t);  // HeraldLook.cs
                    break;
                case EnemyType.GravityWell:  // a spinning black hole
                case EnemyType.CageWell:
                case EnemyType.SingShard:
                    Circle(pos, r * 0.6f, new Rgba(0f, 0f, 0f, 1f), 16);
                    Arc(pos, r * 0.6f, 1.5f, Rgba.White.WithAlpha(0.7f), 16, t * 6f, MathX.Pi * 1.2f);
                    break;
            }
        }

        /// The Brute: a heavy brow and fists toward its target.
        void DrawBruteFace(Enemy e, V2 pos, float r, Rgba col)
        {
            V2 f = V2.FromAngle(e.Facing), side = f.Orthogonal();
            Circle(pos + f * r * 0.95f + side * r * 0.6f, r * 0.34f, col.Mix(new Rgba(1f, 0.9f, 0.8f), 0.3f), 10);
            Circle(pos + f * r * 0.95f - side * r * 0.6f, r * 0.34f, col.Mix(new Rgba(1f, 0.9f, 0.8f), 0.3f), 10);
            Line(pos + f * r * 0.35f - side * r * 0.4f, pos + f * r * 0.35f + side * r * 0.4f, 3f, new Rgba(0.15f, 0.05f, 0.05f, 0.9f));
        }

        static readonly Rgba IceColor = new Rgba(0.55f, 0.85f, 1f);
        static readonly Rgba FireColor = new Rgba(1f, 0.42f, 0.1f);
        static readonly Rgba SlowColor = new Rgba(0.45f, 0.7f, 1f);
        static readonly Rgba MarkColor = new Rgba(1f, 0.25f, 0.25f);
        static readonly Rgba BleedColor = new Rgba(0.8f, 0.05f, 0.12f);

        /// The Wolf Whistle's prey: three claw marks circling it, pointing in.
        void DrawPreyMark(V2 pos, float r, float t)
        {
            var c = new Rgba(0.85f, 0.85f, 0.95f);
            for (int k = 0; k < 3; k++)
            {
                V2 d = V2.FromAngle(t * 1.5f + MathX.Tau * k / 3f), s = d.Orthogonal();
                V2 at = pos + d * (r + 12f);
                for (int j = -1; j <= 1; j++)
                    Line(at + s * (j * 3f) + d * 4f, at + s * (j * 3f) - d * 3f, 1.6f, c.WithAlpha(0.85f));
            }
        }

        /// A Bone Guard cage round a caged enemy: a ring of bone bars closing in on it.
        void DrawBoneCage(V2 pos, float r, float t)
        {
            var bone = Defs.BulletColor(Defs.BoneShot);
            float cr = r + 6f;
            Circle(pos, cr, bone.WithAlpha(0.08f), 20);
            for (int k = 0; k < 7; k++)  // the bars, seen from above: short ribs pointing in
            {
                V2 d = V2.FromAngle(MathX.Tau * k / 7f + 0.2f);
                Line(pos + d * (cr - 4f), pos + d * (cr + 3f), 3f, bone);
                Circle(pos + d * (cr + 3f), 2.2f, bone, 6);
            }
            Arc(pos, cr, 2f, bone.WithAlpha(0.9f), 28);
            Arc(pos, cr + 5f, 1.2f, bone.WithAlpha(0.5f + 0.2f * Mathf.Sin(t * 6f)), 28);
        }

        /// A row of small icons over an enemy, one per status: snowflake = frozen, blue chevrons =
        /// slowed, flame = burning, blood drop = bleeding, green bubbles = poisoned, amber pause = suppressed (shoots slower), red crosshair = marked (takes extra damage).
        void DrawStatusIcons(Enemy e, V2 center, float t)
        {
            bool iced = e.Frozen && !LeapCaged(e);
            int n = (iced ? 1 : 0) + (e.Chilled ? 1 : 0) + (e.Burning ? 1 : 0) + (e.Bleeding ? 1 : 0) + (e.Marked ? 1 : 0) + (e.Suppressed ? 1 : 0) + (e.Poisoned ? 1 : 0);
            if (n == 0)
                return;
            const float size = 8f, step = 21f;
            float x = center.X - (n - 1) * step / 2f;
            var bg = new Rgba(0f, 0f, 0f, 0.55f);
            if (iced)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                for (int k = 0; k < 3; k++)  // snowflake: three crossing spokes with tips
                {
                    V2 d = V2.FromAngle(MathX.Pi / 2f + k * MathX.Pi / 3f) * size;
                    Line(c - d, c + d, 2f, IceColor);
                }
                Circle(c, 1.6f, Rgba.White, 6);
                x += step;
            }
            if (e.Chilled)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                for (int k = 0; k < 2; k++)  // two downward chevrons: slowed
                {
                    float y = c.Y - 3f + k * 4.5f;
                    Line(new V2(c.X - size * 0.8f, y - 2f), new V2(c.X, y + 2f), 2f, SlowColor);
                    Line(new V2(c.X, y + 2f), new V2(c.X + size * 0.8f, y - 2f), 2f, SlowColor);
                }
                x += step;
            }
            if (e.Burning)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                float flick = 0.8f * Mathf.Sin(t * 16f + e.Id);
                Circle(c + new V2(0f, 1.5f), size * 0.6f, FireColor, 10);       // flame: round base...
                Polygon(c + new V2(0f, -2f + flick * 0.3f), size * 0.62f, 3, -MathX.Pi / 2f, FireColor);  // ...pointed top
                Circle(c + new V2(0f, 2f), size * 0.3f, new Rgba(1f, 0.9f, 0.4f), 8);
                x += step;
            }
            if (e.Bleeding)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                Circle(c + new V2(0f, 2f), size * 0.55f, BleedColor, 10);  // a blood drop: round base...
                Polygon(c + new V2(0f, -2f), size * 0.55f, 3, -MathX.Pi / 2f, BleedColor);  // ...pointed top
                Circle(c + new V2(-1.5f, 2f), size * 0.15f, new Rgba(1f, 0.6f, 0.6f), 6);
                x += step;
            }
            if (e.Poisoned)  // Medicine Darts' poison: green bubbles
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                var green = new Rgba(0.45f, 0.95f, 0.35f);
                Circle(c + new V2(-2.5f, 2f), 3f, green, 8);
                Circle(c + new V2(3f, -1f), 2.2f, green, 8);
                Circle(c + new V2(0f, -4.5f), 1.6f, green, 6);
                x += step;
            }
            if (e.Suppressed)  // the Riot Repeater's suppression: a pause sign, its shooting slowed
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                var amber = new Rgba(1f, 0.75f, 0.25f);
                Line(c + new V2(-2.5f, -size * 0.6f), c + new V2(-2.5f, size * 0.6f), 2.5f, amber);
                Line(c + new V2(2.5f, -size * 0.6f), c + new V2(2.5f, size * 0.6f), 2.5f, amber);
                x += step;
            }
            if (e.Marked)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                Arc(c, size * 0.75f, 1.6f, MarkColor, 12);
                Line(c - new V2(size, 0f), c + new V2(size, 0f), 1.5f, MarkColor);
                Line(c - new V2(0f, size), c + new V2(0f, size), 1.5f, MarkColor);
            }
        }
    }
}
