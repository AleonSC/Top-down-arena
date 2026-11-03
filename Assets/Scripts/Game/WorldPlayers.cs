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

            if (p.Id == game.MyId && fxOn)
                DrawDashStreak(p, t);  // Comet Streak (SoldierLooks.cs)
            else if (p.Id == game.MyId && game.DashT > 0f)
                Circle(p.Disp - game.DashDir * 18f, r * 0.8f, col.WithAlpha(0.25f), 20);
            // The footprint: a faint disc and a ring in the role's colour, the size of what the body can be hit by.
            // The model (CharacterModels.cs) is drawn over it in the class's own colours, and a downed player is a grey disc.
            Rgba role = p.Def.Color.WithAlpha(col.A);
            if (p.Downed)
            {
                Circle(p.Disp, r, col.Darkened(0.55f), 28);
                Arc(p.Disp, r, 3f, col, 28);
            }
            else
            {
                Circle(p.Disp, r, p.Flash > 0f ? Rgba.White.WithAlpha(0.25f * col.A) : role.WithAlpha(0.14f * col.A), 28);
                Arc(p.Disp, r, 2.2f, p.Flash > 0f ? Rgba.White : role.WithAlpha(0.6f * col.A), 28);
                DrawPlayerModel(p, r, t, col);
                // The class's overlays on top: ability feedback, buffs and the like (ClassLooks.cs)
                var look = LookOf(p.Kit).Body;
                if (look != null)
                    look(this, p, r, t, col);
            }
            // The real hitbox, drawn as a bright ring round a core dot (a solid disc would hide the head).
            if (p.Downed)
            {
                Circle(p.Disp, p.Def.HitR, new Rgba(1f, 1f, 1f, 0.85f), 16);
                Circle(p.Disp, p.Def.HitR * 0.5f, col, 12);
            }
            else
            {
                Arc(p.Disp, p.Def.HitR - 0.75f, 2.2f, new Rgba(1f, 1f, 1f, 0.9f), 20);
                Circle(p.Disp, p.Def.HitR * 0.42f, Rgba.White.WithAlpha(0.9f), 10);
                Circle(p.Disp, p.Def.HitR * 0.3f, col, 10);
            }

            if (!preview)  // menu previews (LobbyUi.cs) leave out the health bar
                GhostBar(p.Disp + new V2(-20f, -r - 12f), 40f, 4f, p.Hp / p.MaxHp, new Rgba(0.3f, 1f, 0.4f), PlayerKey(p.Id, false));  // HealthBars.cs
            if (p.Downed)
                Arc(p.Disp, r + 8f, 4f, new Rgba(0.4f, 1f, 0.5f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(p.Revive, 0.01f));
        }

        static readonly Rgba Steel = new Rgba(0.82f, 0.88f, 1f);

        /// A knight's buffs and abilities over the model (the body and weapon are in CharacterModels.cs).
        void DrawKnight(Player p, float r, float t)
        {
            V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
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

        /// A ninja's buffs and abilities over the model (the body, scarf and weapon are in CharacterModels.cs).
        void DrawNinja(Player p, float r, float t, Rgba col)
        {
            // Abilities
            if (p.IronT > 0f)  // Iron Body: a steel-grey shell
                Polygon(p.Disp, r + 8f, 8, t * 0.6f, new Rgba(0.75f, 0.78f, 0.85f, 0.35f + 0.15f * Mathf.Sin(t * 10f)));
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BlossomStorm)  // petals swirling round
                for (int k = 0; k < 10; k++)
                    Circle(p.Disp + V2.FromAngle(t * 1.5f + MathX.Tau * k / 10f) * (r + 30f + 8f * Mathf.Sin(t * 2f + k)), 3f, new Rgba(1f, 0.7f, 0.85f, 0.8f), 6);
        }
    }
}
