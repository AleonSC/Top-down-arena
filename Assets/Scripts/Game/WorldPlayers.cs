using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// Players in the arena: the base body (DrawPlayer) and the Knight's and Ninja's looks.
    public partial class ArenaApp
    {
        void DrawPlayer(Player p, float t, bool preview = false)
        {
            float r = p.Def.BodyR;
            Rgba col = p.Def.Color;
            if (p.Downed)
                col = new Rgba(0.45f, 0.45f, 0.5f);
            else if (p.InvulnT > 0f && MathX.Fmod(t, 0.12f) < 0.06f)
                col = col.WithAlpha(0.35f);
            if (p.Flash > 0f)
                col = Rgba.White;
            if (p.VanishT > 0f && !p.Downed)  // Vanish: barely there (a little clearer on your own screen)
                col = col.WithAlpha(p.Id == game.MyId ? 0.45f : 0.18f);

            if (p.Id == game.MyId && game.DashT > 0f)
                Circle(p.Disp - game.DashDir * 18f, r * 0.8f, col.WithAlpha(0.25f), 20);
            Circle(p.Disp, r, col.Darkened(0.55f), 28);
            Arc(p.Disp, r, 3f, col, 28);
            V2 dir = V2.FromAngle(p.Aim);
            Line(p.Disp + dir * (r * 0.5f), p.Disp + dir * (r + 10f), 5f, col);
            if (p.Role == Role.Tank)
            {
                float h = r * 0.55f;
                var c = col.WithAlpha(0.5f);
                Line(p.Disp + new V2(-h, -h), p.Disp + new V2(h, -h), 2f, c);
                Line(p.Disp + new V2(h, -h), p.Disp + new V2(h, h), 2f, c);
                Line(p.Disp + new V2(h, h), p.Disp + new V2(-h, h), 2f, c);
                Line(p.Disp + new V2(-h, h), p.Disp + new V2(-h, -h), 2f, c);
            }
            else if (p.Role == Role.Healer)
            {
                Line(p.Disp - new V2(r * 0.6f, 0), p.Disp + new V2(r * 0.6f, 0), 3f, col.WithAlpha(0.6f));
                Line(p.Disp - new V2(0, r * 0.6f), p.Disp + new V2(0, r * 0.6f), 3f, col.WithAlpha(0.6f));
            }
            // The class's look on top: a mage's star, a summoner's motes, a knight's helm and weapon... (ClassLooks.cs)
            var look = LookOf(p.Kit).Body;
            if (look != null && !p.Downed)
                look(this, p, r, t, col);
            // The real hitbox, drawn as a bright core.
            Circle(p.Disp, p.Def.HitR, new Rgba(1f, 1f, 1f, 0.85f), 16);
            Circle(p.Disp, p.Def.HitR * 0.5f, col, 12);

            if (!preview)  // menu previews (LobbyUi.cs) leave out the health bar
                GhostBar(p.Disp + new V2(-20f, -r - 12f), 40f, 4f, p.Hp / p.MaxHp, new Rgba(0.3f, 1f, 0.4f), PlayerKey(p.Id, false));  // HealthBars.cs
            if (p.Downed)
                Arc(p.Disp, r + 8f, 4f, new Rgba(0.4f, 1f, 0.5f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(p.Revive, 0.01f));
        }

        static readonly Rgba Steel = new Rgba(0.82f, 0.88f, 1f);

        /// A knight: a steel helm band, the weapon held toward the aim, and any buff it has up.
        void DrawKnight(Player p, float r, float t)
        {
            V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
            Arc(p.Disp, r * 0.62f, 2.5f, Steel.WithAlpha(0.75f), 16, p.Aim - 0.9f, 1.8f);  // visor
            // The weapon, held out to one side.
            V2 grip = p.Disp + side * (r * 0.7f);
            switch (p.Weapon.Id)
            {
                case WeaponId.Warhammer:
                {
                    // Charging: the hammer is raised up and back over your shoulder, and glows as the charge builds.
                    float c = p.DrawT > 0f ? game.BowCharge(p, p.DrawT) : 0f;
                    V2 hd = V2.FromAngle(p.Aim - 1.9f * c);
                    Line(grip, grip + hd * (r + 12f), 3f, new Rgba(0.6f, 0.45f, 0.3f));
                    if (c > 0f)
                        Circle(grip + hd * (r + 14f), 9f + 5f * c, new Rgba(1f, 0.82f, 0.3f, 0.15f + 0.3f * c), 12);
                    Polygon(grip + hd * (r + 14f), 6f + 2f * c, 4, p.Aim - 1.9f * c, Steel);
                    break;
                }
                case WeaponId.HolyMace:  // the Quarterstaff: a long pole held in both hands, capped in gold at each end
                {
                    V2 a = grip - dir * (r * 0.9f), b = grip + dir * (r + 24f);
                    Line(a, b, 3.5f, new Rgba(0.62f, 0.46f, 0.28f));
                    Circle(a, 3f, new Rgba(1f, 0.88f, 0.45f), 8);
                    Circle(b, 3.5f, new Rgba(1f, 0.88f, 0.45f), 8);
                    break;
                }
                case WeaponId.Halberd: case WeaponId.Rapier:
                    Line(grip - dir * 6f, grip + dir * (r + (p.Weapon.Id == WeaponId.Halberd ? 30f : 22f)), p.Weapon.Id == WeaponId.Halberd ? 3f : 2f, Steel);
                    if (p.Weapon.Id == WeaponId.Halberd)
                        Line(grip + dir * (r + 18f) + side * 6f, grip + dir * (r + 24f) - side * 2f, 4f, Steel);
                    break;
                case WeaponId.Censer:
                {
                    V2 ball = p.Disp + V2.FromAngle(t * 9f) * (r + 14f);
                    Line(grip, ball, 1.5f, Steel.WithAlpha(0.6f));
                    Circle(ball, 5f, new Rgba(1f, 0.85f, 0.45f), 10);
                    break;
                }
                case WeaponId.Lance:  // a long tapering lance with a round guard
                {
                    V2 butt = grip - dir * 8f, tipAt = grip + dir * (r + 40f);
                    Line(butt, grip + dir * (r + 6f), 4f, new Rgba(0.75f, 0.6f, 0.4f));
                    SetColor(Steel);
                    Tri(tipAt, grip + dir * (r + 6f) + side * 4f, grip + dir * (r + 6f) - side * 4f);
                    Arc(grip + dir * 4f, 6f, 2.5f, Steel, 10, p.Aim + MathX.Pi / 2f, MathX.Pi);  // vamplate
                    break;
                }
                case WeaponId.TwinBlades:
                    Line(grip, grip + dir * (r + 12f), 3f, Steel);
                    Line(p.Disp - side * (r * 0.7f), p.Disp - side * (r * 0.7f) + dir * (r + 12f), 3f, Steel);
                    break;
                default:  // swords
                    Line(grip, grip + dir * (r + (p.Weapon.Id == WeaponId.Greatsword ? 26f : 16f)), p.Weapon.Id == WeaponId.Greatsword ? 5f : 3.5f,
                        p.Weapon.Id == WeaponId.Lightbringer ? new Rgba(1f, 0.95f, 0.65f) : Steel);
                    Line(grip + side * 5f, grip - side * 5f, 3f, new Rgba(0.75f, 0.6f, 0.3f));  // crossguard
                    break;
            }
            // Abilities
            if (p.WallT > 0f)
            {
                float half = 1.05f * p.Stats.WallArcMult;
                Arc(p.Disp, r + 34f, 12f, new Rgba(0.75f, 0.85f, 1f, 0.22f), 20, p.Aim - half, 2f * half);
                Arc(p.Disp, r + 40f, 4f, new Rgba(0.85f, 0.92f, 1f, 0.9f), 20, p.Aim - half, 2f * half);
            }
            if (p.WhirlT > 0f)
                for (int k = 0; k < 3; k++)
                {
                    float a = t * 14f + MathX.Tau * k / 3f, reach = 110f * p.Stats.WhirlRadiusMult;
                    Line(p.Disp + V2.FromAngle(a) * r, p.Disp + V2.FromAngle(a) * reach, 3f, Steel.WithAlpha(0.8f));
                    Arc(p.Disp, reach, 2f, Steel.WithAlpha(0.3f), 12, a - 0.6f, 0.6f);
                }
            if (p.FuryT > 0f)
                Arc(p.Disp, r + 6f, 3f, new Rgba(1f, 0.3f, 0.2f, 0.5f + 0.4f * Mathf.Sin(t * 12f)), 24);
            if (p.UnbreakT > 0f)  // Unbreakable: a golden octagon of plates, its edges bright
            {
                Polygon(p.Disp, r + 9f, 8, t * 0.5f, new Rgba(0.9f, 0.8f, 0.5f, 0.3f));
                for (int k = 0; k < 8; k++)
                    Line(p.Disp + V2.FromAngle(t * 0.5f + MathX.Tau * k / 8f) * (r + 11f), p.Disp + V2.FromAngle(t * 0.5f + MathX.Tau * (k + 1) / 8f) * (r + 11f), 2.5f,
                         new Rgba(1f, 0.88f, 0.55f, 0.75f + 0.2f * Mathf.Sin(t * 6f + k)));
            }
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BladeTempest)  // three spectral blades wheeling round
                DrawBladeTempest(p, r, t);  // AbilityLooks.cs
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.LastStand)
                for (int k = 0; k < 8; k++)  // flames licking up around them
                    Circle(p.Disp + V2.FromAngle(MathX.Tau * k / 8f + t) * (r + 10f), 4f + 2f * Mathf.Sin(t * 15f + k), new Rgba(1f, 0.5f, 0.2f, 0.7f), 6);
        }

        static readonly Rgba NinjaCloth = new Rgba(0.1f, 0.1f, 0.14f);
        static readonly Rgba Wood = new Rgba(0.6f, 0.45f, 0.3f);

        /// A ninja: a dark wrap with an eye slit, a scarf in the role's colour trailing behind, the weapon in hand,
        /// and any buff it has up.
        void DrawNinja(Player p, float r, float t, Rgba col)
        {
            float a = col.A;
            V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
            // The scarf: two ribbons waving out behind.
            for (int k = 0; k < 2; k++)
            {
                V2 prev = p.Disp - dir * (r * 0.6f) + side * (k == 0 ? 3f : -3f);
                for (int i = 1; i <= 5; i++)
                {
                    float f = i / 5f;
                    V2 pt = p.Disp - dir * (r * 0.6f + f * (14f + 4f * k)) + side * ((k == 0 ? 3f : -3f) + Mathf.Sin(t * 9f + i * 0.9f + k) * 4f * f);
                    Line(prev, pt, 3f - 1.5f * f, col.WithAlpha(a * (1f - 0.5f * f)));
                    prev = pt;
                }
            }
            Arc(p.Disp, r * 0.62f, 4f, NinjaCloth.WithAlpha(0.9f * a), 16, p.Aim - 1.1f, 2.2f);           // the wrap
            Line(p.Disp + dir * (r * 0.55f) + side * 4f, p.Disp + dir * (r * 0.55f) - side * 4f, 1.5f, new Rgba(1f, 1f, 1f, 0.8f * a));  // eye slit
            V2 grip = p.Disp + side * (r * 0.75f);
            switch (p.Weapon.Id)
            {
                case WeaponId.Kusarigama:  // a sickle, its chain looping round, or run out taut to the enemy it's hooked in
                {
                    Line(grip, grip + dir * (r + 6f), 3f, Wood.WithAlpha(a));
                    Enemy hooked;
                    if (p.HookT > 0f && game.Enemies.TryGetValue(p.HookId, out hooked))
                        DrawChain(grip, hooked.Disp, a);
                    else
                    {
                        Arc(grip + dir * (r + 6f) - side * 5f, 6f, 2.5f, Steel.WithAlpha(a), 8, p.Aim - MathX.Pi / 2f, MathX.Pi);
                        for (int k = 0; k < 5; k++)
                            Circle(p.Disp + V2.FromAngle(t * 6f + k * 0.5f) * (r + 9f), 1.6f, Steel.WithAlpha(0.7f * a), 6);
                    }
                    break;
                }
                case WeaponId.BoStaff:  // a long staff turning in the hands
                {
                    V2 s = V2.FromAngle(p.Aim + 0.6f * Mathf.Sin(t * 3f) + MathX.Pi / 2f);
                    Line(p.Disp - s * (r + 14f), p.Disp + s * (r + 14f), 3.5f, Wood.WithAlpha(a));
                    break;
                }
                case WeaponId.FumaShuriken:
                    DrawStar(grip + dir * 6f, 8f, t * 2f, Steel.WithAlpha(a));
                    break;
                case WeaponId.MedicineDarts:  // three needles between the fingers
                    for (int k = -1; k <= 1; k++)
                        Line(grip, grip + V2.FromAngle(p.Aim + k * 0.25f) * 10f, 1.2f, new Rgba(0.6f, 1f, 0.75f, a));
                    break;
                case WeaponId.SpiritFan:  // an open fan facing forward
                    Arc(grip + dir * 2f, 9f, 6f, new Rgba(0.75f, 1f, 0.85f, 0.85f * a), 10, p.Aim - 0.9f, 1.8f);
                    Line(grip, grip + dir * 4f, 2f, Wood.WithAlpha(a));
                    break;
                case WeaponId.Ofuda:  // a paper charm with a red seal
                {
                    V2 c = grip + dir * 6f;
                    SetColor(new Rgba(1f, 0.93f, 0.78f, a));
                    Quad(c - dir * 6f - side * 3.5f, c + dir * 6f - side * 3.5f, c + dir * 6f + side * 3.5f, c - dir * 6f + side * 3.5f);
                    Line(c - side * 2.5f, c + side * 2.5f, 2f, new Rgba(0.9f, 0.2f, 0.2f, a));
                    break;
                }
                case WeaponId.Shuriken:
                    DrawStar(grip, 5f, t * 4f, Steel.WithAlpha(a));
                    break;
                case WeaponId.Katana:  // a long thin blade with a small square guard
                    Line(grip - dir * 5f, grip, 3f, NinjaCloth.WithAlpha(a));
                    if (p.SheathT >= Game.KatanaSheath)
                    {
                        // Sheathed for a quick-draw: the blade in a dark scabbard, a glint running along it.
                        Line(grip, grip + dir * (r + 20f), 4f, new Rgba(0.16f, 0.13f, 0.2f, a));
                        float gl = MathX.Fmod(t * 1.6f, 1f);
                        V2 at = grip + dir * ((r + 20f) * gl), sd = dir.Orthogonal();
                        var shine = new Rgba(1f, 1f, 1f, a * (0.5f + 0.5f * Mathf.Sin(t * 12f)));
                        Line(at - dir * 4f, at + dir * 4f, 1.5f, shine);
                        Line(at - sd * 4f, at + sd * 4f, 1.5f, shine);
                    }
                    else
                        Line(grip, grip + dir * (r + 20f), 2.5f, Steel.WithAlpha(a));
                    Polygon(grip, 3.5f, 4, p.Aim + MathX.Pi / 4f, new Rgba(0.75f, 0.6f, 0.3f, a));
                    break;
                default:  // Kunai: a knife with a ring pommel
                    Line(grip, grip + dir * (r + 2f), 2.5f, Steel.WithAlpha(a));
                    Arc(grip - dir * 3f, 3f, 1.2f, Steel.WithAlpha(a), 8);
                    break;
            }
            // Abilities
            if (p.IronT > 0f)  // Iron Body: a steel-grey shell
                Polygon(p.Disp, r + 8f, 8, t * 0.6f, new Rgba(0.75f, 0.78f, 0.85f, 0.35f + 0.15f * Mathf.Sin(t * 10f)));
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BlossomStorm)  // petals swirling round
                for (int k = 0; k < 10; k++)
                    Circle(p.Disp + V2.FromAngle(t * 1.5f + MathX.Tau * k / 10f) * (r + 30f + 8f * Mathf.Sin(t * 2f + k)), 3f, new Rgba(1f, 0.7f, 0.85f, 0.8f), 6);
        }
    }
}
