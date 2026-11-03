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
                case ZoneKind.Meteor:  // warning circle that fills in, then the meteor lands
                    Circle(z.Pos, z.Radius * (1f - timeLeft), c.WithAlpha(0.25f), 40);
                    Arc(z.Pos, z.Radius, 3f, c.WithAlpha(MathX.Fmod(t, 0.2f) < 0.1f ? 0.9f : 0.5f), 48);
                    return;
                case ZoneKind.FlameWall:  // a ring of flickering flames; lower once it's done blocking
                {
                    float wall = z.GuardFrac > 0f ? 1f : 0.45f;
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.12f), 40);
                    for (int k = 0; k < 18; k++)
                        Circle(z.Pos + V2.FromAngle(MathX.Tau * k / 18f) * z.Radius, (7f + 3f * Mathf.Sin(t * 14f + k)) * wall, c.WithAlpha(0.8f * wall), 8);
                    break;
                }
                case ZoneKind.Blizzard:  // swirling snow
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.14f), 40);
                    for (int k = 0; k < 14; k++)
                    {
                        float a = t * 1.5f + k * 2.4f, rr = z.Radius * MathX.Fmod(k * 0.37f + t * 0.2f, 1f);
                        Circle(z.Pos + V2.FromAngle(a) * rr, 2.5f, Rgba.White.WithAlpha(0.8f), 6);
                    }
                    break;
                case ZoneKind.Lantern:
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f + 0.05f * Mathf.Sin(t * 5f)), 40);
                    Polygon(z.Pos, 9f, 4, MathX.Pi / 4f, c);
                    break;
                case ZoneKind.Totem:
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.08f), 40);
                    Polygon(z.Pos, 8f, 6, t, c);
                    if (z.GuardFrac > 0f)  // a thrown Totem charging from seed hits near it
                        Arc(z.Pos, 14f, 3f, new Rgba(1f, 1f, 0.7f, 0.85f), 20, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(z.GuardFrac));
                    break;
                case ZoneKind.Incense:  // the Censer's trail: soft puffs of smoke drifting up and thinning
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
                case ZoneKind.HolySword:  // a warning circle while a giant sword drops toward it
                {
                    Circle(z.Pos, z.Radius * (1f - timeLeft), c.WithAlpha(0.2f), 40);
                    Arc(z.Pos, z.Radius, 3f, c.WithAlpha(MathX.Fmod(t, 0.2f) < 0.1f ? 0.9f : 0.5f), 48);
                    V2 tip = z.Pos - new V2(0f, 420f * timeLeft);
                    Line(tip, tip - new V2(0f, 90f), 10f, c.WithAlpha(0.9f));
                    Line(tip - new V2(0f, 90f) + new V2(-22f, 0f), tip - new V2(0f, 90f) + new V2(22f, 0f), 6f, new Rgba(0.8f, 0.65f, 0.3f));
                    return;
                }
                case ZoneKind.Grove:  // a great tree with roots spreading out
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
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.18f), 40);
                    for (int k = 0; k < 9; k++)
                    {
                        float pa = MathX.Tau * k / 9f + t * 0.25f, pr = z.Radius * (0.35f + 0.45f * MathX.Fmod(k * 0.53f + t * 0.1f, 1f));
                        Circle(z.Pos + V2.FromAngle(pa) * pr, z.Radius * 0.3f, c.WithAlpha(0.12f), 16);
                    }
                    break;
                case ZoneKind.Mist:  // a soft green mist with drifting droplets
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f + 0.04f * Mathf.Sin(t * 3f)), 48);
                    for (int k = 0; k < 8; k++)
                        Circle(z.Pos + V2.FromAngle(t * 0.6f + MathX.Tau * k / 8f) * (z.Radius * 0.7f), 3f, c.WithAlpha(0.6f), 6);
                    break;
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
                case ZoneKind.Siphon:
                case ZoneKind.BloodPool:
                    DrawBloodZone(z, c, t);  // BloodMageLook.cs
                    break;
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
            if (m.Kind == MinionKind.Clone)
            {
                DrawClone(m, t);
                return;
            }
            if (m.Kind == MinionKind.WardenGuard)
            {
                DrawWardenGuard(m, t);  // WardenLook.cs
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
        void DrawClone(Minion m, float t)
        {
            Player owner;
            Rgba col = game.Players.TryGetValue(m.Owner, out owner) ? owner.Def.Color : m.Def.Color;
            float r = owner != null ? owner.Def.BodyR : m.Def.Radius;
            float a = 0.55f + 0.15f * Mathf.Sin(t * 11f + m.Id);
            Circle(m.Disp, r + 8f, m.Def.Color.WithAlpha(0.15f), 20);
            Circle(m.Disp, r, col.Darkened(0.7f).WithAlpha(a), 24);
            Arc(m.Disp, r, 2f, col.WithAlpha(a), 24);
            V2 dir = V2.FromAngle(m.Aim);
            Arc(m.Disp, r * 0.62f, 4f, NinjaCloth.WithAlpha(a), 16, m.Aim - 1.1f, 2.2f);
            Line(m.Disp - dir * (r * 0.6f), m.Disp - dir * (r * 0.6f + 16f) + dir.Orthogonal() * (4f * Mathf.Sin(t * 9f)), 2f, col.WithAlpha(a * 0.8f));
            if (m.T >= 0f && m.Duration > 0f)
                Arc(m.Disp, r + 5f, 2f, col.WithAlpha(0.5f), 24, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
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
            if (e.Def.Melee && !spawning && e.Type != EnemyType.HiveEgg && e.Type != EnemyType.ReaperPillar && !Zombies.Is(e.Type))
                DrawMeleeSpikes(e, pos, r, col, t);
            if (Zombies.Is(e.Type))
                DrawZombieUnder(e, pos, r, t);  // its spore haze, frenzy streaks and the Crawler's hold (ZombieLook.cs)
            col = DrawEnemyUnderlay(e, pos, r, col, spawning, t);
            if (e.Elite)
                Arc(pos, r + 5f, 2.5f, new Rgba(1f, 0.85f, 0.3f, 0.9f), 32);
            col = DrawEnemyStatus(e, pos, r, col, t);
            if (e.Flash > 0f || JuiceFlash(e))
                col = Rgba.White;
            if (Zombies.Is(e.Type))
                DrawZombieBody(e, pos, r, col, t);  // its own shape (ZombieLook.cs)
            else
                Polygon(pos, r, e.Def.Sides, EnemyRotation(e, t), col);
            if (e.Frozen)
                DrawIceBlock(e, pos, r, t);
            DrawEnemyDetails(e, pos, r, col, t);
            DrawVarietyOnEnemy(e, pos, r, t);  // bubbles, trait markers (VarietyLook.cs)
            // Bosses get a bar at the top of the screen instead.
            bool bar = e.Hp < e.MaxHp && !e.Def.IsBoss && !spawning;
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

        void DrawMeleeSpikes(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            int spikes = e.Type == EnemyType.Swarmer ? 6 : e.Type == EnemyType.Brute ? 10 : 8;
            float spin = e.Type == EnemyType.Swarmer ? t * 4f : t * 0.6f;
            for (int k = 0; k < spikes; k++)
            {
                float a = spin + MathX.Tau * k / spikes;
                V2 d = V2.FromAngle(a), side = d.Orthogonal();
                SetColor(col.Mix(new Rgba(1f, 0.95f, 0.85f, col.A), 0.35f));
                Tri(pos + d * (r + 6f), pos + d * (r - 2f) + side * (r * 0.28f), pos + d * (r - 2f) - side * (r * 0.28f));
            }
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
                case EnemyType.Charger:
                    DrawChargerTell(e, pos, r, col, t);
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

        /// Winding up: where it's about to leap. Leaping: a smear behind it.
        void DrawLungerTell(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            if (e.Phase == 1)
            {
                V2 dir = V2.FromAngle(e.Facing);
                float a = MathX.Fmod(t, 0.12f) < 0.06f ? 0.6f : 0.25f;
                Line(pos + dir * r, pos + dir * 230f, 4f, new Rgba(1f, 0.35f, 0.65f, a));
                Arc(pos + dir * 230f, 10f, 2f, new Rgba(1f, 0.35f, 0.65f, a), 12);
            }
            else if (e.Phase == 2)
                for (int k = 1; k <= 3; k++)
                    Circle(pos - V2.FromAngle(e.Facing) * (r * 0.9f * k), r * (1f - 0.2f * k), col.WithAlpha(0.35f / k), 12);
        }

        /// Wind-up: where it's about to dash. Dashing: a smear behind it.
        void DrawChargerTell(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            if (e.Phase == 1)
            {
                V2 dir = V2.FromAngle(e.Facing);
                float a = MathX.Fmod(t, 0.16f) < 0.08f ? 0.55f : 0.25f;
                Line(pos + dir * r, pos + dir * 260f, 3f, new Rgba(1f, 0.3f, 0.3f, a));
            }
            else if (e.Phase == 2)
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
            if (e.Frozen)
                col = col.Mix(new Rgba(0.75f, 0.92f, 1f), 0.85f);  // frosted almost white-blue
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
            if (e.Marked)  // Mark for Death: a rotating red reticle
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
            int n = (e.Frozen ? 1 : 0) + (e.Chilled ? 1 : 0) + (e.Burning ? 1 : 0) + (e.Bleeding ? 1 : 0) + (e.Marked ? 1 : 0) + (e.Suppressed ? 1 : 0) + (e.Poisoned ? 1 : 0);
            if (n == 0)
                return;
            const float size = 8f, step = 21f;
            float x = center.X - (n - 1) * step / 2f;
            var bg = new Rgba(0f, 0f, 0f, 0.55f);
            if (e.Frozen)
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
