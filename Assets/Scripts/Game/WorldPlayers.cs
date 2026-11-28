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
            if (!preview && !p.Downed && DrawForbiddenBeast(p, t))
                return;  // Forbidden Path: a shadow beast in place of the ninja (ForbiddenPathLook.cs)
            float r = p.Def.BodyR;
            Rgba col = p.Def.Color;
            float leapH = 0f;
            bool leaping = !p.Downed && LeapHeight(p, out leapH);  // the Knight's Leap Slam (LeapSlamLook.cs)
            if (p.Downed)
                col = new Rgba(0.45f, 0.45f, 0.5f);
            else if (!leaping && p.InvulnT > 0f && MathX.Fmod(t, 0.12f) < 0.06f)
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
            Rgba role = (p.Recruit ? RecruitRing : p.Def.Color).WithAlpha(col.A);  // (a Training recruit has no role colour: RecruitLook.cs)
            var ds = DownStateOf(p);  // the going-down and getting-up looks (DownedLooks.cs); null with Visual effects off
            float rebuild = RebuildK(ds);
            if (p.Downed)
            {
                if (ds != null)
                    DrawDownedBody(p, r, t, ds);
                else
                {
                    Circle(p.Disp, r, col.Darkened(0.55f), 28);
                    Arc(p.Disp, r, 3f, col, 28);
                }
            }
            else
            {
                if (!SacSpiritShown(p))  // (the blood spirit has no body to ring: SacrificialRitualLook.cs)
                {
                    Circle(p.Disp, r, p.Flash > 0f ? Rgba.White.WithAlpha(0.25f * col.A) : role.WithAlpha(0.14f * col.A), 28);
                    Arc(p.Disp, r, 2.2f, p.Flash > 0f ? Rgba.White : role.WithAlpha(0.6f * col.A), 28);
                }
                V2 groundAt = p.Disp;
                float modelR = r;
                if (leaping)  // up in the air: bigger and higher, the shadow left on the floor
                {
                    GlowDisc(groundAt, r * (2.2f - 0.6f * leapH), new Rgba(0f, 0f, 0f), 0.4f - 0.18f * leapH);
                    modelR = r * (1f + LeapSwell * leapH);
                    p.Disp += new V2(0f, -LeapLiftPx * leapH);
                }
                BeginBodyFeel(p, preview);  // stretched along where it's heading, squashed as it stops (MoveFeel.cs)
                modelR *= JuggSwell(p, t);  // the Juggernaut Suit: bigger while it's on (JuggernautLook.cs)
                V2 emoteOff;
                float emoteSpin;
                int emoteId = preview ? emoteBodyFor : p.Id;  // (a menu preview only moves when the lobby says whose it is)
                bool inTank = rebuild < 0f && DrawLiteralTank(p, t, col.A);  // A Literal Tank: drawn in place of the model (LiteralTankLook.cs)
                inTank |= !inTank && rebuild < 0f && DrawIronArmadillo(p, t, col.A);  // Iron Armadillo: the ball in place of the model (IronArmadilloLook.cs)
                bool golem = !inTank && rebuild < 0f && ColossusStanding(p);  // Stone Colossus: the golem stands in for the model (StoneColossusLook.cs)
                bool bloodGolem = !inTank && !golem && rebuild < 0f && GolemStanding(p);  // Blood Golem: the same (BloodGolemLook.cs)
                golem |= bloodGolem;
                bool sacSpirit = !inTank && !golem && rebuild < 0f && SacSpiritShown(p);  // Sacrificial Ritual: the blood spirit stands in for the model (SacrificialRitualLook.cs)
                golem |= sacSpirit;
                if (inTank)
                {
                }
                else if (bloodGolem)
                    DrawBloodGolem(p, t, col.A);
                else if (sacSpirit)
                    DrawSacSpirit(p, t, col.A);
                else if (golem)
                    DrawColossus(p, t, col.A);
                else if (rebuild >= 0f && !preview)
                    DrawRebuild(p, modelR, t, col.A, rebuild);  // standing back up: flecks, a scan line, a shockwave
                else if (emoteId >= 0 && EmoteBody(emoteId, out emoteOff, out emoteSpin))  // an emote moves the model only (EmoteLooks.cs)
                {
                    V2 keepAt = p.Disp;
                    float keepAim = p.Aim;
                    p.Disp += emoteOff;
                    p.Aim += emoteSpin;
                    DrawPlayerModel(p, modelR, t, col);
                    p.Disp = keepAt;
                    p.Aim = keepAim;
                }
                else
                    DrawPlayerModel(p, modelR, t, col);
                // The class's overlays on top: ability feedback, buffs and the like (ClassLooks.cs)
                var look = p.Recruit ? null : LookOf(p.Kit).Body;  // (a Training recruit has no class look)
                if (look != null && rebuild < 0f && !inTank && !golem)
                    look(this, p, modelR, t, col);
                if (rebuild < 0f)
                {
                    if (!golem)
                        DrawColossus(p, t, col.A);  // the rise: rocks gathering onto you (StoneColossusLook.cs)
                    DrawGolemGather(p, modelR, t, col.A);  // Blood Golem's gather: coats piling onto you (BloodGolemLook.cs)
                    DrawOniMask(p, t, col.A);  // Oni Mask on the head and its timer ring (OniMaskLook.cs)
                    DrawJuggernaut(p, modelR, t, col.A);  // the Juggernaut Suit's plates (JuggernautLook.cs)
                    DrawDebtRing(p, modelR, t, col.A);  // Blood Debt and Arterial Spray: any class can have them (Wildcard)
                    DrawSpray(p, modelR, t, col.A);
                }
                EndBodyFeel();
                p.Disp = groundAt;
            }
            // The real hitbox: a small coloured core dot (a downed player's husk keeps its white ring).
            if (p.Downed)
            {
                Circle(p.Disp, p.Def.HitR, new Rgba(1f, 1f, 1f, 0.85f), 16);
                Circle(p.Disp, p.Def.HitR * 0.5f, col, 12);
                if (ds != null)
                    DrawHuskMark(p, ds);
            }
            else
            {
                Circle(p.Disp, p.Def.HitR * 0.3f, col, 10);  // (the white hitbox ring round it was removed: it hid the heads)
            }

            if (!preview)  // menu previews (LobbyUi.cs) leave out the health bar
            {
                // The overhead stack, bottom up, clear of the body and its rings (OverheadStack.cs); the name goes over it (Hud.cs)
                BeginOverhead(p);
                if (HpBarOn(p))  // (Settings > Gameplay > Health bars)
                {
                    float hy = OverheadRow(4f);
                    GhostBar(p.Disp + new V2(-20f, hy), 40f, 4f, p.Hp / p.MaxHp, new Rgba(0.3f, 1f, 0.4f), PlayerKey(p.Id, false));  // HealthBars.cs
                    DrawWorldWound(p.Disp.X - 20f, p.Disp.Y + hy, 40f, 4f, p);  // the wound: the bar's end that heals can't reach (HealthBars.cs)
                    DrawDebtBar(p, hy, t);  // Blood Debt's ghost segment (BloodDebtLook.cs)
                }
                DrawArmorBar(p);       // an Armor Drop vest's armor, five steel segments (ArmorDropLook.cs)
                DrawJuggBar(p);        // the Juggernaut Suit's armour (JuggernautLook.cs)
                DrawTankBar(p);        // A Literal Tank's armour and time (LiteralTankLook.cs)
                DrawArmBar(p);         // Iron Armadillo's time (IronArmadilloLook.cs)
                EndOverhead(p);
                DrawSallyRing(p, t);   // Sally Forth, out of a Trojan Horse (TrojanHorseLook.cs)
            }
            if (p.Downed)
            {
                if (ds != null)
                    DrawReviveRing(p, r, t, ds);  // the 36-segment ring and the link from whoever is reviving
                else
                    Arc(p.Disp, r + 8f, 4f, new Rgba(0.4f, 1f, 0.5f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(p.Revive, 0.01f));
            }
        }

        /// A knight's buffs and abilities over the model (the body and weapon are in CharacterModels.cs).
        void DrawKnight(Player p, float r, float t)
        {
            // Abilities
            DrawShieldWall(p, r, t);  // ShieldWallLook.cs (also runs when it's down, to notice it going up)
            if (p.UnbreakT > 0f)  // Cavalry Call's guard: a golden octagon of plates, its edges bright
            {
                Polygon(p.Disp, r + 9f, 8, t * 0.5f, new Rgba(0.9f, 0.8f, 0.5f, 0.3f));
                for (int k = 0; k < 8; k++)
                    Line(p.Disp + V2.FromAngle(t * 0.5f + MathX.Tau * k / 8f) * (r + 11f), p.Disp + V2.FromAngle(t * 0.5f + MathX.Tau * (k + 1) / 8f) * (r + 11f), 2.5f,
                         new Rgba(1f, 0.88f, 0.55f, 0.75f + 0.2f * Mathf.Sin(t * 6f + k)));
            }
            DrawLastStand(p, r, t);  // LastStandLook.cs (also runs when it's down, for the collapse)
        }

        static readonly Rgba NinjaCloth = new Rgba(0.1f, 0.1f, 0.14f);

        /// A ninja's buffs and abilities over the model (the body, scarf and weapon are in CharacterModels.cs).
        void DrawNinja(Player p, float r, float t, Rgba col)
        {
            // Abilities
            if (p.PlateN > 0)  // Ceremonial Armor's plates (CeremonialArmorLook.cs; the rings and sigil are under the model)
                DrawCeremonialArmor(p, t);
            DrawCraneRing(p, t);  // Hundred Paper Cranes: the ring swirling round you (HundredPaperCranesLook.cs)
            DrawSenbonRing(p, t);  // Senbon Seekers' needles schooling round you (SenbonSeekersLook.cs)
            DrawSpiritFront(p, t);  // Spirit Avatar's arms and brush over the model (SpiritAvatarLook.cs)
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BlossomStorm)  // petals swirling round
                for (int k = 0; k < 10; k++)
                    Circle(p.Disp + V2.FromAngle(t * 1.5f + MathX.Tau * k / 10f) * (r + 30f + 8f * Mathf.Sin(t * 2f + k)), 3f, new Rgba(1f, 0.7f, 0.85f, 0.8f), 6);
        }
    }
}
