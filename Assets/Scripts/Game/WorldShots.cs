using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// Bullets and charges in the arena: the player weapons' shots (fireballs, boulders, Storm Hammers, small and
    /// Ninja shots, shot mods) and the in-world charge overlays (bow draw, hammer and boulder charge, revolver cylinder).
    public partial class ArenaApp
    {
        /// The Fireball: a big ball of flame, white-hot at its heart, with fire streaming off behind it.
        void DrawFireball(Bullet b, Rgba c, float t)
        {
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), side = dir.Orthogonal();
            float r = b.Radius;
            var ember = new Rgba(0.85f, 0.2f, 0.05f);
            float soft = FireballSoftness();  // the haze and trail thin out as more fireballs fly together
            for (int k = 0; k < 6; k++)  // the trail: puffs of flame falling behind and cooling
            {
                float f = MathX.Fmod(t * 3.5f + k / 6f + b.Id * 0.13f, 1f);
                V2 at = b.Pos - dir * (r * 0.4f + f * r * 2.6f) + side * (Mathf.Sin(t * 13f + k * 2.1f) * r * 0.35f * f);
                Circle(at, r * (0.75f - 0.45f * f), c.Mix(ember, f).WithAlpha(0.75f * soft * (1f - f)), 10);
            }
            Circle(b.Pos, r * (1.6f + 0.1f * Mathf.Sin(t * 25f + b.Id)), c.WithAlpha(0.22f * soft), 16);  // heat haze
            Circle(b.Pos, r, c, 16);
            Circle(b.Pos + dir * (r * 0.15f), r * 0.65f, new Rgba(1f, 0.78f, 0.3f), 14);
            Circle(b.Pos + dir * (r * 0.25f), r * 0.32f, new Rgba(1f, 0.97f, 0.8f), 10);
        }

        int fireballsFrame = -1;
        float fireballsSoft = 1f;

        /// 1 for a lone fireball, falling toward 0.25 as more fly at once (every +1 adds one), so their haze and trails don't
        /// pile into a white-out. The ball and its core stay solid. Counted once a frame.
        float FireballSoftness()
        {
            if (fireballsFrame != Time.frameCount)
            {
                fireballsFrame = Time.frameCount;
                int n = 0;
                foreach (var b in game.Bullets.Values)
                    if (b.Kind == Defs.FireballShot)
                        n++;
                fireballsSoft = Mathf.Clamp(1.6f / Mathf.Max(1, n), 0.25f, 1f);
            }
            return fireballsSoft;
        }

        /// The Golem Staff's boulder in flight: its shadow on the ground where it is, and the rock itself lifted
        /// above it along its arc (higher on longer throws), tumbling as it goes.
        void DrawBoulder(Bullet b, Rgba c, float t)
        {
            float flight = Mathf.Max(0.01f, b.Life0), k = 1f - MathX.Clamp01(b.Life / flight);
            float lift = Mathf.Sin(k * MathX.Pi) * (40f + 90f * (flight - 0.4f) / 0.3f);
            V2 at = b.Pos + new V2(0f, -lift);  // the arena's y points down the screen
            float r = b.Radius;
            Circle(b.Pos, r * (0.9f - 0.3f * lift / 130f), new Rgba(0f, 0f, 0f, 0.35f), 16);  // its shadow, smaller the higher it is
            float spin = t * 6f + b.Id;
            Polygon(at, r, 7, spin, c);
            Polygon(at, r * 0.72f, 7, spin + 0.3f, c.Mix(new Rgba(0.85f, 0.78f, 0.68f), 0.35f));
            V2 crack = V2.FromAngle(spin + 1f);
            Line(at - crack * r * 0.6f, at + crack * r * 0.2f, 1.5f, new Rgba(0.3f, 0.25f, 0.2f, 0.8f));
            Circle(at + V2.FromAngle(spin + 2.4f) * r * 0.35f, r * 0.18f, new Rgba(0.4f, 0.34f, 0.28f), 6);
        }

        /// The Storm Hammer: a war hammer tumbling end over end, its head crackling with sparks.
        void DrawStormHammer(Bullet b, Rgba c, float t)
        {
            DrawStormHammerThunder(b, c, t);  // StormHammerLook.cs
            return;
        }

        /// Small shots (enemies', arrows, sword waves...), drawn over everything else.
        void DrawSmallShot(Bullet b, float t)
        {
            if (PacSkin)
            {
                DrawPacDot(b, t);  // PacmanLook.cs
                return;
            }
            float keep = glAlpha;
            glAlpha = keep * TeammateShotAlpha(b);  // a teammate's shot, as faded in Settings > Gameplay
            DrawSmallShotLook(b, t);
            glAlpha = keep;
        }

        /// How opaque a shot is drawn: 1 for yours and the enemies', the Settings > Gameplay "Teammate shots" value for another player's.
        float TeammateShotAlpha(Bullet b) => mateShotAlpha < 1f && b.Owner != game.MyId && !Defs.IsEnemyShot(b.Kind) && game.Players.ContainsKey(b.Owner) ? mateShotAlpha : 1f;

        void DrawSmallShotLook(Bullet b, float t)
        {
            if (!Defs.IsEnemyShot(b.Kind))
                DrawShotMods(b, t);
            Rgba c = Defs.BulletColor(b.Kind);
            if (b.Kind == Defs.Caltrop)
            {
                DrawCaltrop(b, t);  // KatanaLook.cs
                return;
            }
            if (b.Kind == Defs.ParryNeedle)
            {
                DrawParryNeedle(b);  // ParryLook.cs
                return;
            }
            if (DrawWeaponShot(b, t))
                return;  // its weapon's new look (ShotLooks.cs)
            foreach (var look in KitLooks)  // a class's own shots (KitLook.Shot in ClassLooks.cs: the Ninja's, the Hemomancer's)
                if (look.Shot != null && look.Shot(this, b, c, t))
                    return;
            if (b.Kind == Defs.SteelShard)
            {
                DrawSteelShardShot(b);  // KnightDpsLook.cs
                return;
            }
            if (b.Kind == Defs.HammerShot)
            {
                DrawStormHammer(b, c, t);
                return;
            }
            if (b.Kind == Defs.TreantSeed)
            {
                DrawThornDart(b);  // SeedLook.cs
                return;
            }
            if (b.Kind == Defs.MedkitShot)
            {
                DrawMedkitShot(b, t);  // HealerKitLooks.cs
                return;
            }
            if (b.Kind == Defs.BoulderShot)
            {
                DrawBoulder(b, c, t);
                return;
            }
            if (b.Kind == Defs.FireballShot)
            {
                DrawFireball(b, c, t);
                return;
            }
            if (b.Kind == Defs.FlameShot)
            {
                // A Flamethrower flame: small and white-hot at the nozzle, swelling and reddening as it goes, then gone.
                float age = 1f - Mathf.Clamp01(b.Life / Mathf.Max(0.01f, b.Life0)), r = 3.5f + 11f * age;
                var hot = new Rgba(1f, 0.95f, 0.65f);
                var col = age < 0.5f ? hot.Mix(Defs.BulletColor(b.Kind), age * 2f) : Defs.BulletColor(b.Kind).Mix(new Rgba(0.75f, 0.15f, 0.08f), (age - 0.5f) * 2f);
                float a = 0.85f * (1f - age * age);
                Circle(b.Pos, r * 1.7f, col.WithAlpha(0.18f * a), 12);
                Circle(b.Pos, r, col.WithAlpha(a), 10);
                return;
            }
            if (b.Kind == Defs.StunShot || b.Kind == Defs.StunBolt)  // the Stun Gun fires the Warden's own stun shots
            {
                DrawStunShot(b, t);  // WardenLook.cs
                return;
            }
            if (b.Kind == Defs.TaserBolt)
            {
                DrawTaserBolt(b, t);  // TaserLook.cs
                return;
            }
            if (b.Kind == Defs.TracerRound)
            {
                DrawTracerRound(b, t);  // SecurityLook.cs
                return;
            }
            if (b.Kind == Defs.HoneyShot)
            {
                DrawHoneyShot(b);  // QueenLook.cs
                return;
            }
            if (b.Kind == Defs.BileShot || b.Kind == Defs.ToothShot)
            {
                DrawDevourerShot(b, t);  // DevourerLook.cs
                return;
            }
            if (b.Kind == Defs.HollowShot)
            {
                DrawHollowShot(b);  // DuelistLook.cs
                return;
            }
            if (b.Kind == Defs.CrescentShot)
            {
                DrawCrescentShot(b, t);  // EclipseLook.cs
                return;
            }
            if (b.Kind == Defs.PauseShot || b.Kind == Defs.StitchShot)
            {
                DrawPatternShot(b, c, t);  // VarietyLook.cs
                return;
            }
            if (b.Kind == Defs.ArrowShot)  // a Mender's Bow arrow: wooden shaft, steel head, green fletching
            {
                V2 dir = b.Vel.Normalized(), side = dir.Orthogonal();
                V2 tip = b.Pos + dir * 9f, neck = b.Pos + dir * 3f, tail = b.Pos - dir * 15f;
                if (Juicy)
                    DrawArrowStreak(b, dir, tail, c);                                     // a longer streak, by how hard it was drawn (GunBowLooks.cs)
                else
                    Line(tail - dir * 10f, tail, 2f, c.WithAlpha(0.25f));                // faint green streak behind
                Line(tail, neck, 2f, new Rgba(0.78f, 0.6f, 0.38f));                      // shaft
                SetColor(new Rgba(0.9f, 0.95f, 1f));                                      // head
                Tri(tip, neck + side * 3.5f, neck - side * 3.5f);
                for (int k = 0; k < 2; k++)                                               // fletching
                {
                    V2 at = tail + dir * (2f + k * 3f);
                    Line(at, at - dir * 4f + side * 3.5f, 1.5f, c);
                    Line(at, at - dir * 4f - side * 3.5f, 1.5f, c);
                }
                return;
            }
            if (IsLightCrescent(b))  // a Lightbringer's Dawn or Mercy crescent (LightbringerFxLook.cs; in the glow pass when Visual effects are on)
            {
                if (!Juicy)
                    DrawSunriseCrescent(b);
                return;
            }
            if (b.Kind == Defs.KnightWave)  // a crescent of steel (or light) flying edge-first
            {
                float a = b.Vel.Angle;
                Player bo;
                if (fxOn && game.Players.TryGetValue(b.Owner, out bo) && bo.Weapon != null && bo.Weapon.Id == WeaponId.Greatsword)
                {
                    // Moonlit Crescent's sword beam: a trail of fading crescents behind a bright one.
                    V2 bd = V2.FromAngle(a);
                    var moon = new Rgba(0.78f, 0.88f, 1f);
                    for (int k = 9; k >= 1; k--)
                        Arc(b.Pos - bd * (8f + 12f * k), b.Radius + 6f, 3f * (1f - k / 12f), moon.WithAlpha((1f - k / 10f) * 0.5f), 12, a - 0.95f, 1.9f);
                    GlowDisc(b.Pos, 30f, moon, 0.3f);
                    Arc(b.Pos - bd * 8f, b.Radius + 6f, 7f, moon.WithAlpha(0.8f), 12, a - 0.95f, 1.9f);
                    Arc(b.Pos - bd * 8f, b.Radius + 6f, 2.4f, Rgba.White, 12, a - 0.95f, 1.9f);
                    return;
                }
                Arc(b.Pos - V2.FromAngle(a) * 8f, b.Radius + 6f, 5f, c.WithAlpha(0.45f), 12, a - 0.9f, 1.8f);
                Arc(b.Pos - V2.FromAngle(a) * 8f, b.Radius + 6f, 2f, Rgba.White.WithAlpha(0.95f), 12, a - 0.8f, 1.6f);
                return;
            }
            if (b.Kind == Defs.PierceShot)  // a boss's unblockable shot: red with a dark core
            {
                Circle(b.Pos, b.Radius + 4f, c.WithAlpha(0.35f), 12);
                Circle(b.Pos, b.Radius, c, 12);
                Circle(b.Pos, b.Radius * 0.5f, new Rgba(0.1f, 0f, 0f, 1f), 8);
                return;
            }
            if (b.FromBoss)  // other boss shots: violet glow, spinning spikes, dark core (they wear blockers down)
            {
                Circle(b.Pos, b.Radius + 5f, BossShotGlow.WithAlpha(0.3f), 12);
                Polygon(b.Pos, b.Radius + 4f, 4, t * 5f + b.Id, BossShotGlow);
                Circle(b.Pos, b.Radius, c, 12);
                Circle(b.Pos, b.Radius * 0.5f, new Rgba(0.08f, 0f, 0.12f, 1f), 8);
                return;
            }
            if (b.Look != ShotLook.Round && b.Owner == 0)
            {
                DrawShapedShot(b, c);  // an aimed dart or a curving crescent (VarietyLook.cs)
                return;
            }
            if (b.Kind == Defs.MineShot)  // mortar bomb: pulses faster as it's about to burst
                Arc(b.Pos, b.Radius + 5f + 3f * Mathf.Sin(t * (10f + 20f * (1.3f - b.Life))), 2f, c.WithAlpha(0.8f), 16);
            Circle(b.Pos, b.Radius + 2.5f, c.WithAlpha(0.3f), 12);
            Circle(b.Pos, b.Radius, c, 12);
            Circle(b.Pos, b.Radius * 0.45f, new Rgba(1f, 1f, 1f, 0.9f), 8);
        }

        /// The Ninja's thrown things: spinning stars, needles, knives, paper charms and gusts of wind.
        /// The Ninja's shots (KitLook.Shot): false for any other kind.
        bool DrawNinjaShot(Bullet b, Rgba c, float t)
        {
            if (b.Kind < Defs.StarShot || b.Kind > Defs.FumaStar)
                return false;
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), side = dir.Orthogonal();
            switch (b.Kind)
            {
                case Defs.StarShot:
                    if (Juicy)  // motion-blur arcs as it spins (ShotLooks.cs)
                        for (int k = 0; k < 4; k++)
                        {
                            float a = t * 20f + b.Id + k * MathX.Tau / 4f;
                            Arc(b.Pos, b.Radius + 2f, 3.5f, c.WithAlpha(0.22f), 6, a - 0.9f, 0.9f);
                        }
                    DrawStar(b.Pos, b.Radius + 2f, t * 20f + b.Id, c);
                    break;
                case Defs.FumaStar:  // the hooked four-bladed star with its trail (FumaLook.cs)
                    DrawFumaShot(b, t);
                    break;
                case Defs.NeedleShot:
                    Line(b.Pos - dir * 9f, b.Pos + dir * 4f, 1.5f, c);
                    Circle(b.Pos + dir * 4f, 1.6f, Rgba.White, 6);
                    break;
                case Defs.KunaiShot:
                    Line(b.Pos - dir * 8f, b.Pos, 2f, NinjaCloth.Mix(c, 0.4f));
                    SetColor(c);
                    Tri(b.Pos + dir * 7f, b.Pos + side * 3f, b.Pos - side * 3f);
                    break;
                case Defs.OfudaShot:  // a paper charm fluttering along, glowing faintly
                {
                    Circle(b.Pos, b.Radius + 5f, c.WithAlpha(0.2f), 12);
                    V2 s = side * (3.5f + 1.5f * Mathf.Sin(t * 14f + b.Id));
                    SetColor(c);
                    Quad(b.Pos - dir * 7f - s, b.Pos + dir * 7f - s, b.Pos + dir * 7f + s, b.Pos - dir * 7f + s);
                    Line(b.Pos - side * 2.5f, b.Pos + side * 2.5f, 2f, new Rgba(0.9f, 0.2f, 0.2f));
                    break;
                }
                default:  // GustShot: soft puffs of air
                    Circle(b.Pos, b.Radius, c.WithAlpha(0.18f), 12);
                    Arc(b.Pos, b.Radius * 0.7f, 1.5f, c.WithAlpha(0.45f), 10, b.Vel.Angle + t * 8f, 2f);
                    break;
            }
            return true;
        }

        /// Weapon mods show on the shots themselves: a trail for homing, embers for fire,
        /// frost rings, sparks for shock and a hot rim for explosive rounds.
        void DrawShotMods(Bullet b, float t)
        {
            Player p;
            if (b.Owner == 0 || b.Kind == Defs.GrenadeShot || b.Kind == Defs.SwarmShot || !game.Players.TryGetValue(b.Owner, out p))
                return;
            var s = p.Stats;
            V2 back = b.Vel.Normalized();
            if (b.Homing > 0f)
                Line(b.Pos - back * (b.Radius * 4f), b.Pos, b.Radius * 0.9f, Defs.BulletColor(b.Kind).WithAlpha(0.3f));
            if (s.BurnFrac > 0f)
                Circle(b.Pos - back * (b.Radius * 1.8f) + V2.FromAngle(t * 20f + b.Id) * 1.5f, b.Radius * 0.7f, new Rgba(1f, 0.45f, 0.1f, 0.7f), 8);
            if (s.CryoSlow > 0f)
                Arc(b.Pos, b.Radius + 3f, 1.5f, new Rgba(0.65f, 0.92f, 1f, 0.8f), 10);
            if (s.ShockChance > 0f && MathX.Fmod(t * 7f + b.Id * 0.37f, 1f) < 0.5f)
            {
                V2 side = back.Orthogonal();
                Line(b.Pos + side * (b.Radius + 2f), b.Pos + side * (b.Radius + 6f) - back * 3f, 1.5f, new Rgba(0.75f, 0.85f, 1f, 0.9f));
                Line(b.Pos - side * (b.Radius + 2f), b.Pos - side * (b.Radius + 6f) + back * 3f, 1.5f, new Rgba(0.75f, 0.85f, 1f, 0.9f));
            }
            if (s.ExplosiveRadius > 0f)
                Arc(b.Pos, b.Radius + 1.5f, 1.5f, new Rgba(1f, 0.3f, 0.15f, 0.9f), 10);
        }

        static readonly Rgba BossShotGlow = new Rgba(0.85f, 0.3f, 1f);

        /// The aim line: a soft glow from your gun along your aim in your role's color, out to your weapon's
        /// range (Game.WeaponRange) with a small cap there, fading with distance, motes drifting outward.
        /// Mender's Bow: a ring that fills as you draw (gold at full draw, pulsing while a release would be
        /// perfect), and for your own bow an aim cone that narrows to a line.
        void DrawBowDraw(Player p, bool mine, float t)
        {
            float c = game.BowCharge(p, p.DrawT);
            bool full = c >= 1f, perfect = game.BowPerfect(p, p.DrawT);
            float r = p.Def.BodyR + 11f;
            var role = p.Def.Color;
            var gold = new Rgba(1f, 0.82f, 0.3f);
            Arc(p.Disp, r, 2.5f, new Rgba(1f, 1f, 1f, 0.12f), 32);
            Arc(p.Disp, r, perfect ? 4f : 2.5f, (full ? gold : role).WithAlpha(perfect ? 0.75f + 0.25f * Mathf.Sin(t * 30f) : 0.85f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(0.02f, c));
            DrawBowFullPop(p);  // a gold ring as the draw reaches full (GunBowLooks.cs)
            if (!mine)
                return;
            float cone = 0.2f * (1f - c);  // radians either side: 11 degrees at the start of a draw, a line at full
            float len = 120f + 160f * c;
            V2 from = p.Disp + V2.FromAngle(p.Aim) * (p.Def.BodyR + 6f);
            var lc = (full ? gold : role.Mix(Rgba.White, 0.4f)).WithAlpha(perfect ? 0.8f : 0.45f);
            if (cone > 0.01f)
            {
                Line(from, from + V2.FromAngle(p.Aim - cone) * len, 1.5f, lc);
                Line(from, from + V2.FromAngle(p.Aim + cone) * len, 1.5f, lc);
            }
            else
                Line(from, from + V2.FromAngle(p.Aim) * len, perfect ? 3f : 2f, lc);
        }

        /// The Warhammer's charge: a ring filling round you (gold when full) and, in front, the circle it will slam: always the
        /// same size, a faint outline with a disc growing inside it as the charge builds.
        void DrawHammerCharge(Player p, bool mine, float t)
        {
            float c = game.BowCharge(p, p.DrawT);
            bool full = c >= 1f;
            DrawHammerRaised(p, c);  // raised over the shoulder, with Visual effects on (MeleeLooks.cs)
            var gold = new Rgba(1f, 0.82f, 0.3f);
            float r = p.Def.BodyR + 11f;
            Arc(p.Disp, r, 2.5f, new Rgba(1f, 1f, 1f, 0.12f), 32);
            Arc(p.Disp, r + (fxOn && c > 0.2f ? Mathf.Sin(t * 70f) * 1.2f * c : 0f), full ? 4f : 2.5f, (full ? gold : p.Def.Color).WithAlpha(full ? 0.75f + 0.25f * Mathf.Sin(t * 20f) : 0.85f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(0.02f, c));  // (Quake: the ring trembles as it fills)
            // Where the slam will land: always the same circle. A faint outline marks the area; the charge grows an
            // inner disc from a small core out to the outline, gold and pulsing when it's full.
            float mult = Game.MeleeReach(p, p.Weapon.Reach) / p.Weapon.Reach;
            V2 at = p.Disp + V2.FromAngle(p.Aim) * Game.HammerCentre(Game.MeleeReach(p, p.Weapon.Reach));
            float R = Game.HammerRadius * mult;
            float a = mine ? 1f : 0.6f;
            Circle(at, R, new Rgba(0.9f, 0.85f, 0.75f, 0.05f * a), 40);
            Arc(at, R, 2f, new Rgba(0.9f, 0.85f, 0.75f, 0.3f * a), 48);
            float inner = Mathf.Lerp(8f, R, c);
            var fill = full ? gold : new Rgba(1f, 0.7f, 0.4f);
            Circle(at, inner, fill.WithAlpha((0.14f + 0.3f * c) * a), 40);
            Arc(at, inner, full ? 4f : 2.5f, fill.WithAlpha((full ? 0.75f + 0.25f * Mathf.Sin(t * 20f) : 0.55f + 0.3f * c) * a), 40);
            if (fxOn && c > 0f)  // Quake: dust is pulled in toward the circle as the charge builds
                for (int i = 0; i < 14; i++)
                {
                    float ph = Mathf.Repeat(t * 0.9f + i * 0.137f, 1f), an = i * 2.399f;
                    float rr = R * (1.9f - 1.5f * ph);
                    Circle(at + new V2(Mathf.Cos(an) * rr, Mathf.Sin(an) * rr * 0.9f), 2f + 2f * (1f - ph), new Rgba(0.69f, 0.64f, 0.57f, (1f - ph) * 0.5f * Mathf.Clamp01(c * 2f) * a), 8);
                }
        }

        /// The Golem Staff's charge: a ring filling round you (gold when full) and, for your own, where the boulder
        /// will land and how much it will crush, with a dotted arc out to it.
        void DrawBoulderCharge(Player p, bool mine, float t)
        {
            float c = game.BowCharge(p, p.DrawT);
            bool full = c >= 1f;
            var gold = new Rgba(1f, 0.82f, 0.3f);
            var stone = new Rgba(0.85f, 0.78f, 0.65f);
            float r = p.Def.BodyR + 11f;
            Arc(p.Disp, r, 2.5f, new Rgba(1f, 1f, 1f, 0.12f), 32);
            Arc(p.Disp, r, full ? 4f : 2.5f, (full ? gold : stone).WithAlpha(full ? 0.75f + 0.25f * Mathf.Sin(t * 20f) : 0.85f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(0.02f, c));
            if (!mine)
                return;
            V2 land = p.Disp + V2.FromAngle(p.Aim) * (Game.BoulderDistance(c) * p.Stats.ShotSpeedMult);
            float splash = Game.BoulderSplash * Game.AreaMult(p);
            var col = (full ? gold : stone);
            for (int k = 1; k < 10; k++)  // the throw's arc, as dots
            {
                float f = k / 10f;
                V2 dot = V2.Lerp(p.Disp, land, f) + new V2(0f, -Mathf.Sin(f * MathX.Pi) * 30f);
                Circle(dot, 2f, col.WithAlpha(0.5f), 6);
            }
            Circle(land, splash, col.WithAlpha(0.06f + 0.08f * c), 32);
            Arc(land, splash, 2f, col.WithAlpha(0.45f + 0.35f * c), 40);
        }

        /// Revolver: the cylinder as pips in an arc under you (gold = heavy rounds), and a ring while reloading.
        void DrawCylinder(Player p, float t)
        {
            int cap = game.RevolverCapacity(p), heavy = game.RevolverHeavy(p);
            int left = Math.Max(0, cap - p.AmmoUsed);
            float r = p.Def.BodyR + 13f;
            var gold = new Rgba(1f, 0.8f, 0.3f);
            if (p.ReloadT > 0f)
            {
                float f = 1f - MathX.Clamp01(p.ReloadT / Game.RevolverReload);
                Arc(p.Disp, r, 2.5f, new Rgba(1f, 1f, 1f, 0.15f), 32);
                Arc(p.Disp, r, 2.5f, gold.WithAlpha(0.85f), 32, -MathX.Pi / 2f, MathX.Tau * f);
                return;
            }
            // Pips spread across the bottom half, filled from the right as the cylinder empties.
            float span = Mathf.Min(MathX.Pi * 0.9f, cap * 0.2f);
            for (int i = 0; i < cap; i++)
            {
                float a = MathX.Pi / 2f + span / 2f - span * (cap == 1 ? 0.5f : (float)i / (cap - 1));
                V2 at = p.Disp + V2.FromAngle(a) * r;
                bool loaded = i < left;
                bool isHeavy = i < heavy;  // the heavy rounds are the last ones fired
                var c = isHeavy ? gold : new Rgba(0.85f, 0.95f, 1f);
                Circle(at, isHeavy ? 3.2f : 2.6f, loaded ? c.WithAlpha(0.95f) : c.WithAlpha(0.15f), 8);
            }
        }
    }
}
