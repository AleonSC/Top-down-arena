using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The arena in GL immediate mode: the floor, players, enemies, minions, bullets, zones and effects, the
    /// in-world overlays (aim line, bow draw, revolver cylinder), and the GL primitives everything is built from.
    public partial class ArenaApp
    {
        // --- World drawing (GL immediate mode, arena coordinates) -------------

        void OnPostRender()
        {
            if (glMaterial == null)
            {
                glMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                glMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                glMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                glMaterial.SetInt("_Cull", (int)CullMode.Off);
                glMaterial.SetInt("_ZWrite", 0);
                glMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            UpdateLayout();
            glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            drawScale = game.Running ? scale * WorldToUi : scale;
            if (game.Running)
                DrawWorld();
            else
                DrawMenuBackground(Time.time);
            GL.End();
            GL.PopMatrix();
        }

        void DrawWorld()
        {
            float t = game.AnimT;
            // Behind the arena: the background this player picked in the lobby, dimmed so the fight stays easy to read.
            float worldScale = drawScale;
            drawScale = worldScale / WorldToUi;  // the backgrounds draw in UI canvas coordinates
            DrawMenuBackground(Time.time);
            drawScale = worldScale;
            FillRect(0, 0, AW, AH, new Rgba(0.05f, 0.06f, 0.09f, ArenaBgDim));
            var grid = new Rgba(1f, 1f, 1f, 0.035f);
            for (float x = 0; x <= AW; x += 64f)
                Line(new V2(x, 0), new V2(x, AH), 1f, grid);
            for (float y = 0; y <= AH; y += 64f)
                Line(new V2(0, y), new V2(AW, y), 1f, grid);
            var border = new Rgba(0.3f, 0.35f, 0.5f, 0.6f);
            Line(new V2(2, 2), new V2(AW - 2, 2), 3f, border);
            Line(new V2(2, AH - 2), new V2(AW - 2, AH - 2), 3f, border);
            Line(new V2(2, 2), new V2(2, AH - 2), 3f, border);
            Line(new V2(AW - 2, 2), new V2(AW - 2, AH - 2), 3f, border);

            DrawArenaContents(t);

            if (game.State == MatchState.GameOver || game.State == MatchState.Victory)
                FillRect(0, 0, AW, AH, new Rgba(0f, 0f, 0f, 0.45f));

            if (aimLine && game.Me != null && !game.Me.Downed && !game.Me.ParryActive)
                DrawAimLine(game.Me, MouseArena(), t);
            if (game.Me != null && !game.Me.Downed && game.Me.Weapon.Id == WeaponId.Pistol)
                DrawCylinder(game.Me, t);
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.MendersBow && p.DrawT > 0f && !p.Downed)
                    DrawBowDraw(p, p.Id == game.MyId, t);

            if (aimDebug && game.Me != null)
            {
                // Cyan: from you through the crosshair. Yellow: along your last shot as the host fired it.
                var me = game.Me;
                Line(me.Disp, me.Disp + V2.FromAngle(me.Aim) * 1100f, 1.5f, new Rgba(0.3f, 0.9f, 1f, 0.7f));
                if (game.LastShotId != 0)
                {
                    Line(game.LastShotFrom, game.LastShotFrom + V2.FromAngle(game.LastShotAngle) * 1100f, 1.5f, new Rgba(1f, 0.85f, 0.2f, 0.75f));
                    Bullet shot;
                    if (game.Bullets.TryGetValue(game.LastShotId, out shot))
                        Arc(shot.Pos, shot.Radius + 5f, 1.5f, new Rgba(1f, 0.85f, 0.2f, 0.9f), 12);
                }
            }

            // Crosshair
            V2 m = MouseArena();
            var ch = new Rgba(1f, 1f, 1f, 0.7f);
            Arc(m, 8f, 1.5f, ch, 16);
            Line(m - new V2(13, 0), m - new V2(5, 0), 1.5f, ch);
            Line(m + new V2(5, 0), m + new V2(13, 0), 1.5f, ch);
            Line(m - new V2(0, 13), m - new V2(0, 5), 1.5f, ch);
            Line(m + new V2(0, 5), m + new V2(0, 13), 1.5f, ch);
        }

        /// Everything on the arena floor: players, bullets, enemies, minions, zones and effects. Also draws the
        /// lobby's loadout preview (a Showcase game swapped in as `game`).
        void DrawArenaContents(float t)
        {
            foreach (var p in game.Players.Values)
                DrawPlayerAuras(p, t);
            foreach (var b in game.Bullets.Values)
                if (b.Kind < Defs.SmallShot)
                    DrawBigShot(b, t);
            foreach (var z in game.Zones.Values)
                DrawZone(z, t);
            foreach (var h in game.Hazards.Values)
                DrawHazard(h, t);
            foreach (var p in game.Players.Values)
                DrawPlayerLinks(p, t);
            foreach (var minion in game.Minions.Values)
                DrawMinion(minion, t);
            foreach (var o in game.Orbs.Values)
                DrawOrb(o, t);
            DrawReaperTether(t);
            DrawSingularityHazards(t);
            foreach (var e in game.Enemies.Values)
                DrawEnemy(e, t);
            foreach (var p in game.Players.Values)
                DrawPlayerWrapped(p, t);
            foreach (var b in game.Bullets.Values)
                if (b.Kind >= Defs.SmallShot)
                    DrawSmallShot(b, t);
            foreach (var fx in game.Effects)
                DrawEffect(fx, t);
        }

        /// Under a player: the healer's aura, Barrier, buffs, the parry shield and ultimates' looks.
        void DrawPlayerAuras(Player p, float t)
        {
            if (p.Downed)
                return;
            if (p.Role == Role.Healer)
            {
                Circle(p.Disp, p.AuraRange, new Rgba(0.3f, 1f, 0.5f, 0.03f), 64);
                Arc(p.Disp, p.AuraRange, 2f, new Rgba(0.3f, 1f, 0.5f, 0.18f), 64);
            }
            if (p.BarrierT > 0f)
            {
                // Fades as it soaks damage; flickers when nearly broken or about to expire.
                float strength = MathX.Clamp01(p.BarrierFrac);
                bool failing = p.BarrierT < 1f || strength < 0.25f;
                float a = !failing || MathX.Fmod(t, 0.2f) < 0.1f ? 0.3f + 0.5f * strength : 0.15f;
                Circle(p.Disp, p.BarrierRadius, new Rgba(0.4f, 0.7f, 1f, 0.04f + 0.1f * strength), 64);
                Arc(p.Disp, p.BarrierRadius, 1.5f + 2.5f * strength, new Rgba(0.5f, 0.8f, 1f, a), 64);
            }
            if (p.OverdriveT > 0f)
                Arc(p.Disp, p.Def.BodyR + 7f + 2f * Mathf.Sin(t * 20f), 2.5f, new Rgba(1f, 0.4f, 0.3f, 0.8f), 32);
            if (p.AegisT > 0f)  // Arcane Aegis: a spinning hexagon
                for (int k = 0; k < 6; k++)
                    Line(p.Disp + V2.FromAngle(t * 3f + MathX.Tau * k / 6f) * (p.Def.BodyR + 22f),
                         p.Disp + V2.FromAngle(t * 3f + MathX.Tau * (k + 1) / 6f) * (p.Def.BodyR + 22f), 3f, new Rgba(0.8f, 0.5f, 1f, 0.85f));
            if (p.ShieldHp > 0f)  // Arcane Ward
                Arc(p.Disp, p.Def.BodyR + 5f, 3f, new Rgba(0.5f, 0.7f, 1f, 0.4f + 0.5f * Mathf.Min(1f, p.ShieldHp / 60f)), 32);
            if (p.SurgeT > 0f)  // Arcane Surge
                Circle(p.Disp, p.Def.BodyR + 10f + 3f * Mathf.Sin(t * 12f), new Rgba(0.75f, 0.45f, 1f, 0.18f), 32);
            if (p.FrenzyT > 0f)  // Alpha Call
                Arc(p.Disp, p.Def.BodyR + 9f, 2f, new Rgba(1f, 0.5f, 0.3f, 0.7f), 16, t * 6f, MathX.Pi);
            if (p.ParryActive)
                DrawParryShield(p, t);
            else if (p.ParryBroken > 0f && !p.Downed)  // broken: cracked pieces until it's back
                for (int k = 0; k < 3; k++)
                    Arc(p.Disp, p.Def.BodyR + 9f, 2f, new Rgba(1f, 0.45f, 0.45f, 0.35f), 4, p.Aim - 0.8f + k * 0.6f, 0.35f);
            else if (p.Id == game.MyId && p.ParryHp < Game.ParryMax(p))  // lowered and refilling: your own gauge, faintly
                DrawParryGauge(p, t, 0.4f);
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.Fortress)  // golden bulwark
            {
                Circle(p.Disp, p.Def.BodyR + 16f, new Rgba(1f, 0.8f, 0.35f, 0.15f), 32);
                Polygon(p.Disp, p.Def.BodyR + 14f, 6, t * 0.8f, new Rgba(1f, 0.8f, 0.35f, 0.12f));
                Arc(p.Disp, p.Def.BodyR + 16f, 3f, new Rgba(1f, 0.85f, 0.4f, 0.9f), 6, t * 0.8f + MathX.Pi / 6f, MathX.Tau);
            }
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BulletStorm)  // spinning muzzle sparks
                for (int k = 0; k < 3; k++)
                    Arc(p.Disp, p.Def.BodyR + 7f + k * 4f, 2f, new Rgba(1f, 0.6f, 0.25f, 0.8f - k * 0.2f), 12, t * (8f + k * 3f), 1.2f);
        }

        /// The parry shield: an arc toward the cursor, with its durability gauge around the outside.
        void DrawParryShield(Player p, float t)
        {
            float rr = p.Def.BodyR + Game.ParryReach * 0.55f;
            var sc = new Rgba(0.55f, 0.9f, 1f);
            float from = p.Aim - Game.ParryArc(p), span = Game.ParryArc(p) * 2f;
            Arc(p.Disp, rr, 14f, sc.WithAlpha(0.45f), 20, from, span);                          // the shield's face
            Arc(p.Disp, rr + 7f, 4f, sc.Mix(Rgba.White, 0.6f).WithAlpha(0.95f), 20, from, span);  // bright rim
            for (int k = 0; k <= 1; k++)  // end caps
                Circle(p.Disp + V2.FromAngle(from + span * k) * (rr + 2f), 4f, sc.Mix(Rgba.White, 0.4f), 8);
            DrawParryGauge(p, t, 1f);
        }

        /// The parry's durability: a thin blue bar just outside the shield's rim that shrinks toward your aim
        /// as it wears, blinking red when nearly broken. Faint (alpha below 1) while the shield is lowered.
        void DrawParryGauge(Player p, float t, float alpha)
        {
            float hp = Mathf.Clamp01(p.ParryHp / Game.ParryMax(p));
            float rad = p.Def.BodyR + Game.ParryReach * 0.55f + 12.5f, half = Game.ParryArc(p);
            var gc = new Rgba(0.37f, 0.71f, 1f);
            var warn = new Rgba(1f, 0.42f, 0.42f);
            Arc(p.Disp, rad, 4f, new Rgba(0.02f, 0.03f, 0.07f, 0.55f * alpha), 20, p.Aim - half, half * 2f);  // dark backing
            Arc(p.Disp, rad, 2.5f, gc.WithAlpha(0.18f * alpha), 20, p.Aim - half, half * 2f);                // empty track
            if (hp <= 0f)
                return;
            bool low = hp < 0.35f && alpha >= 1f;
            Rgba c = low ? (MathX.Fmod(t, 0.2f) < 0.1f ? warn : gc.Mix(warn, 0.35f)) : gc;
            Arc(p.Disp, rad, 2.5f, c.WithAlpha(0.95f * alpha), 20, p.Aim - half * hp, half * 2f * hp);
        }

        /// Players' big shots (kinds below Defs.SmallShot), drawn under enemies.
        void DrawBigShot(Bullet b, float t)
        {
            DrawShotMods(b, t);
            if (b.Kind == Defs.GrenadeShot)  // spinning grenade with a blinking fuse
            {
                Polygon(b.Pos, b.Radius, 6, t * 12f, Defs.BulletColor(b.Kind));
                if (MathX.Fmod(t, 0.15f) < 0.075f)
                    Circle(b.Pos, 3f, new Rgba(1f, 0.3f, 0.2f), 8);
                return;
            }
            if (b.Kind == Defs.FireShot || b.Kind == Defs.HolyShot || b.Kind == Defs.ArcaneShot)
                Circle(b.Pos, b.Radius * (1.8f + 0.3f * Mathf.Sin(t * 30f + b.Id)), Defs.BulletColor(b.Kind).WithAlpha(0.25f), 12);
            if (b.Kind == Defs.RailShot)
                Line(b.Pos - b.Vel * 0.03f, b.Pos, 3f, Defs.BulletColor(b.Kind).WithAlpha(0.6f));
            Circle(b.Pos, b.Radius, Defs.BulletColor(b.Kind).WithAlpha(0.8f), 10);
        }

        /// An incoming lightning strike: a filling warning circle.
        void DrawHazard(Hazard h, float t)
        {
            var hc = new Rgba(0.7f, 0.85f, 1f);
            float f = 1f - MathX.Clamp01(h.T / Mathf.Max(0.01f, h.Duration));
            Circle(h.Pos, h.Radius * f, hc.WithAlpha(0.25f), 32);
            Arc(h.Pos, h.Radius, 2.5f, (f > 0.7f && MathX.Fmod(t, 0.1f) < 0.05f ? Rgba.White : hc).WithAlpha(0.85f), 40);
        }

        /// Soul Link's tether, and Roar's ring.
        void DrawPlayerLinks(Player p, float t)
        {
            Player q;
            if (p.LinkT > 0f && p.LinkTo != p.Id && game.Players.TryGetValue(p.LinkTo, out q))
            {
                // Soul Link: a wavy tether between healer and ally.
                V2 a = p.Disp, b = q.Disp, n = (b - a).Normalized().Orthogonal();
                var c = new Rgba(0.6f, 1f, 0.9f, 0.8f);
                V2 prev = a;
                for (int k = 1; k <= 16; k++)
                {
                    float f = k / 16f;
                    V2 pt = V2.Lerp(a, b, f) + n * (Mathf.Sin(f * 12f - t * 10f) * 5f * Mathf.Sin(f * MathX.Pi));
                    Line(prev, pt, 2.5f, c);
                    prev = pt;
                }
            }
            if (p.TauntT > 0f && !p.Downed)  // Roar
                Arc(p.Disp, p.Def.BodyR + 12f + 3f * Mathf.Sin(t * 16f), 3f, new Rgba(1f, 0.4f, 0.25f, 0.85f), 32);
        }

        /// A health orb: green with a white cross; big ones get a gold ring.
        void DrawOrb(Orb o, float t)
        {
            // Blink during the last 3 seconds before the orb disappears.
            if (o.Life < 3f && MathX.Fmod(t, 0.25f) < 0.1f)
                return;
            float pulse = 1f + 0.12f * Mathf.Sin(t * (o.Big ? 6f : 8f) + o.Id);
            float r = o.Radius * pulse;
            var green = new Rgba(0.35f, 1f, 0.5f);
            Circle(o.Disp, r * 2.2f, green.WithAlpha(o.Big ? 0.18f : 0.12f), 20);
            Circle(o.Disp, r, green, 16);
            Line(o.Disp - new V2(r * 0.55f, 0), o.Disp + new V2(r * 0.55f, 0), o.Big ? 3f : 2f, Rgba.White);
            Line(o.Disp - new V2(0, r * 0.55f), o.Disp + new V2(0, r * 0.55f), o.Big ? 3f : 2f, Rgba.White);
            if (o.Big)
                Arc(o.Disp, r + 5f, 2f, new Rgba(1f, 0.95f, 0.5f, 0.8f), 24);
        }

        /// A player, plus, near an edge, the part of them already coming in on the far side.
        void DrawPlayerWrapped(Player p, float t)
        {
            DrawPlayer(p, t);
            V2 real = p.Disp;
            float edge = p.Def.BodyR + 12f;
            float dx = real.X < edge ? AW : real.X > AW - edge ? -AW : 0f;
            float dy = real.Y < edge ? AH : real.Y > AH - edge ? -AH : 0f;
            var ghosts = new List<V2>();
            if (dx != 0f) ghosts.Add(new V2(dx, 0f));
            if (dy != 0f) ghosts.Add(new V2(0f, dy));
            if (dx != 0f && dy != 0f) ghosts.Add(new V2(dx, dy));  // corners
            foreach (var off in ghosts)
            {
                p.Disp = real + off;
                DrawPlayer(p, t);
            }
            p.Disp = real;
        }

        /// The Frost Lance: a long ice spear flying point-first. The spearhead sits on the hitbox (the bullet's
        /// radius), the shaft trails behind it with a frosty streak and a few glinting motes.
        void DrawFrostLance(Bullet b, Rgba c, float t)
        {
            V2 dir = b.Vel.Normalized(), side = dir.Orthogonal();
            float r = b.Radius;
            V2 tip = b.Pos + dir * (r * 1.4f), back = b.Pos - dir * (r * 0.8f), neck = back - dir * 2f, tail = b.Pos - dir * (r * 3.6f);
            var ice = new Rgba(0.78f, 0.94f, 1f);
            Circle(b.Pos, r * 1.5f, c.WithAlpha(0.16f), 14);                         // cold glow round the head
            Line(tail - dir * 16f, tail, 7f, c.WithAlpha(0.14f));                     // frost streak behind
            Line(tail, neck, 3.5f, ice.WithAlpha(0.95f));                               // shaft
            Line(tail, neck, 1.2f, Rgba.White.WithAlpha(0.7f));                         //   with a bright core
            Line(neck + side * (r * 0.55f), neck - side * (r * 0.55f), 2.5f, ice);      // crossguard
            V2 l = b.Pos + side * (r * 0.62f), rgt = b.Pos - side * (r * 0.62f);
            SetColor(c.Mix(ice, 0.5f));                                                 // the faceted spearhead
            Tri(tip, l, rgt);
            Tri(l, back, rgt);
            SetColor(Rgba.White.WithAlpha(0.85f));                                      // light catching one facet
            Tri(tip, l, b.Pos);
            Line(back, tip, 1.2f, Rgba.White.WithAlpha(0.8f));
            for (int k = 0; k < 3; k++)                                                 // glinting frost motes
            {
                float f = MathX.Fmod(t * 3f + k * 0.33f + b.Id * 0.17f, 1f);
                V2 at = tail + dir * (f * r * 2.4f) + side * ((k - 1) * 4f * Mathf.Sin(t * 9f + k));
                Circle(at, 1.6f * (1f - f) + 0.6f, Rgba.White.WithAlpha(0.8f * (1f - f)), 6);
            }
        }

        /// Small shots (enemies', arrows, sword waves...), drawn over everything else.
        void DrawSmallShot(Bullet b, float t)
        {
            if (!Defs.IsEnemyShot(b.Kind))
                DrawShotMods(b, t);
            Rgba c = Defs.BulletColor(b.Kind);
            if (b.Kind >= Defs.StarShot && b.Kind <= Defs.ChainStar)
            {
                DrawNinjaShot(b, c, t);
                return;
            }
            if (b.Kind == Defs.FrostLance)
            {
                DrawFrostLance(b, c, t);
                return;
            }
            if (b.Kind == Defs.ArrowShot)  // a Mender's Bow arrow: wooden shaft, steel head, green fletching
            {
                V2 dir = b.Vel.Normalized(), side = dir.Orthogonal();
                V2 tip = b.Pos + dir * 9f, neck = b.Pos + dir * 3f, tail = b.Pos - dir * 15f;
                Line(tail - dir * 10f, tail, 2f, c.WithAlpha(0.25f));                    // faint green streak behind
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
            if (b.Kind == Defs.KnightWave)  // a crescent of steel (or light) flying edge-first
            {
                float a = b.Vel.Angle;
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
            if (b.Kind == Defs.MineShot)  // mortar bomb: pulses faster as it's about to burst
                Arc(b.Pos, b.Radius + 5f + 3f * Mathf.Sin(t * (10f + 20f * (1.3f - b.Life))), 2f, c.WithAlpha(0.8f), 16);
            Circle(b.Pos, b.Radius + 2.5f, c.WithAlpha(0.3f), 12);
            Circle(b.Pos, b.Radius, c, 12);
            Circle(b.Pos, b.Radius * 0.45f, new Rgba(1f, 1f, 1f, 0.9f), 8);
        }

        /// The Ninja's thrown things: spinning stars, needles, knives, paper charms and gusts of wind.
        void DrawNinjaShot(Bullet b, Rgba c, float t)
        {
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), side = dir.Orthogonal();
            switch (b.Kind)
            {
                case Defs.StarShot:
                    DrawStar(b.Pos, b.Radius + 2f, t * 20f + b.Id, c);
                    break;
                case Defs.ChainStar:  // big, with a blur ring as it spins
                    Arc(b.Pos, b.Radius + 3f, 2f, c.WithAlpha(0.35f), 16);
                    DrawStar(b.Pos, b.Radius + 2f, t * 16f + b.Id, c);
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
        }

        /// A short-lived effect, by kind.
        void DrawEffect(Effect fx, float t)
        {
            float f = fx.T / fx.Dur;
            switch (fx.Kind)
            {
                case FxKind.Boom:
                    Circle(fx.Pos, fx.Radius * (0.4f + 0.6f * f), fx.Color.WithAlpha(0.35f * (1f - f)), 24);
                    Arc(fx.Pos, fx.Radius * (0.5f + f), 3f, fx.Color.WithAlpha(1f - f), 32);
                    break;
                case FxKind.Spark:
                    Arc(fx.Pos, Mathf.Max(1f, fx.Radius * f), 2f, fx.Color.WithAlpha(1f - f), 12);
                    break;
                case FxKind.Ring:
                    Arc(fx.Pos, fx.Radius * (0.2f + 0.8f * f), 4f, fx.Color.WithAlpha(1f - f), 64);
                    break;
                case FxKind.Target:  // where a bomber will blow up: a faint zone with a ring closing in on it
                    Circle(fx.Pos, fx.Radius, fx.Color.WithAlpha(0.08f + 0.12f * f), 32);
                    Arc(fx.Pos, fx.Radius, 2f, fx.Color.WithAlpha(0.5f), 32);
                    Arc(fx.Pos, fx.Radius * (1.8f - 0.8f * f), 2f, fx.Color.WithAlpha(0.3f + 0.5f * f), 32);
                    break;
                case FxKind.Slash:
                    DrawSlashFx(fx, f, t);
                    break;
                case FxKind.Jab:
                    DrawJabFx(fx, f);
                    break;
                case FxKind.Bolt:
                    DrawBoltFx(fx, f);
                    break;
                case FxKind.Shatter:
                    DrawShatterFx(fx, f);
                    break;
            }
        }

        /// A parry shield breaking: cracks flash across it, then it bursts into jagged pieces with sparks.
        /// The pieces come from a generator seeded by where it broke, so everyone sees the same burst.
        void DrawShatterFx(Effect fx, float f)
        {
            V2 c = fx.Pos, mid = fx.Pos2 - fx.Pos;
            float aim = mid.Angle, rr = mid.Length, half = fx.Radius, from = aim - half, span = half * 2f;
            float r0 = rr - 7f, r1 = rr + 9f;
            uint seed = (uint)((int)fx.Pos.X * 73856093 ^ (int)fx.Pos.Y * 19349663) | 1u;
            Func<float> rnd = () => { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216f; };
            const float crackEnd = 0.18f;
            if (f < crackEnd)  // the shield whitens and cracks run across it
            {
                float k = f / crackEnd;
                Arc(c, rr, 14f, fx.Color.Mix(Rgba.White, 0.5f).WithAlpha(0.6f), 20, from, span);
                Arc(c, rr + 7f, 4f, Rgba.White.WithAlpha(0.95f), 20, from, span);
                for (int n = 0; n < 5; n++)
                {
                    float a = from + span * (0.1f + 0.8f * rnd()), rad = r1 + 1f;
                    V2 prev = c + V2.FromAngle(a) * rad;
                    for (int s = 1; s <= 3; s++)
                    {
                        rad = r1 + 1f - (r1 - r0 + 2f) * s / 3f * Mathf.Min(1f, k * 1.4f);
                        a += (rnd() - 0.5f) * 0.25f;
                        V2 next = c + V2.FromAngle(a) * rad;
                        Line(prev, next, 1.6f, Rgba.White.WithAlpha(0.6f + 0.4f * k));
                        prev = next;
                    }
                }
                return;
            }
            float g = (f - crackEnd) / (1f - crackEnd);
            float e = 1f - (float)Math.Pow(1f - g, 2.6f), fade = Mathf.Max(0f, 1f - (float)Math.Pow(g, 1.4f));
            Circle(c + V2.FromAngle(aim) * rr, (r1 - r0) * (0.4f + 0.5f * e), new Rgba(0.67f, 0.9f, 1f, 0.3f * (1f - g) * (1f - g)), 20);  // burst glow
            var bright = fx.Color.Mix(Rgba.White, 0.7f).WithAlpha(fade);
            for (int n = 0; n < 10; n++)  // sparks
            {
                V2 d = V2.FromAngle(aim + (rnd() - 0.5f) * span * 1.3f);
                V2 tip = c + d * (rr + (40f + 50f * rnd()) * e);
                Line(tip - d * 7f * (1f - g), tip, 2f, bright);
            }
            const int pieces = 9;
            var edges = new float[pieces + 1];
            for (int i = 1; i < pieces; i++)
                edges[i] = (i + (rnd() - 0.5f) * 0.6f) / pieces;
            edges[pieces] = 1f;
            for (int i = 0; i < pieces; i++)
            {
                float a0 = from + span * edges[i] + 0.02f, a1 = from + span * edges[i + 1] - 0.02f;
                float speed = 40f + 25f * rnd(), spin = (rnd() - 0.5f) * 5f;
                V2 off = V2.FromAngle((a0 + a1) / 2f + (rnd() - 0.5f) * 0.4f) * (speed * e);
                DrawShieldPiece(c, a0, a1, r0 + 2f * rnd(), r1 - 2f * rnd(), off, spin * e, (a1 - a0) * 0.25f, fx.Color.WithAlpha(0.55f * fade), bright);
            }
        }

        /// One curved piece of a broken shield (angles a0..a1, radii r0..r1, pointed sides), moved by off and
        /// turned by rot around its own middle.
        void DrawShieldPiece(V2 c, float a0, float a1, float r0, float r1, V2 off, float rot, float jag, Rgba fill, Rgba edge)
        {
            const int n = 4;
            float rm = (r0 + r1) / 2f;
            V2 mid = c + V2.FromAngle((a0 + a1) / 2f) * rm;
            var pts = new V2[2 * n + 4];
            int m = 0;
            for (int i = 0; i <= n; i++)
                pts[m++] = c + V2.FromAngle(a0 + (a1 - a0) * i / n) * r1;
            pts[m++] = c + V2.FromAngle(a1 + jag) * rm;
            for (int i = n; i >= 0; i--)
                pts[m++] = c + V2.FromAngle(a0 + (a1 - a0) * i / n) * r0;
            pts[m++] = c + V2.FromAngle(a0 - jag) * rm;
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            for (int i = 0; i < m; i++)
            {
                V2 d = pts[i] - mid;
                pts[i] = mid + off + new V2(d.X * cs - d.Y * sn, d.X * sn + d.Y * cs);
            }
            SetColor(fill);
            for (int i = 0; i < m; i++)
                Tri(mid + off, pts[i], pts[(i + 1) % m]);
            for (int i = 0; i < n; i++)  // bright outer edge
                Line(pts[i], pts[i + 1], 2.5f, edge);
        }

        /// A melee swing: a bright crescent sweeping across the arc, fading out (a jab or thrust is a streak).
        void DrawSlashFx(Effect fx, float f, float t)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = fx.Radius;
            if (reach < 1f)
                return;
            if (half < 0f)  // a jab: the blade shoots forward, with speed lines along the cone
            {
                float grow = Mathf.Min(1f, f * 4f), len = reach * (0.3f + 0.7f * grow), fade = 1f - f;
                V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
                V2 tipAt = fx.Pos + dir * len;
                for (int k = -1; k <= 1; k += 2)  // the cone it covers, faintly
                    Line(fx.Pos, fx.Pos + V2.FromAngle(aim + k * -half) * len * 0.9f, 1.5f, fx.Color.WithAlpha(0.25f * fade));
                for (int k = 0; k < 4; k++)  // speed lines
                {
                    float o = (k % 2 == 0 ? 1f : -1f) * (0.35f + 0.15f * k) * (float)Math.Tan(-half) * len * 0.5f;
                    V2 a0 = fx.Pos + dir * len * (0.35f + 0.12f * k) + side * o;
                    Line(a0, a0 + dir * len * 0.25f, 1.5f, Rgba.White.WithAlpha(0.5f * fade));
                }
                Line(fx.Pos, tipAt, 10f * fade + 2f, fx.Color.WithAlpha(0.35f * fade));
                Line(fx.Pos, tipAt - dir * 6f, 3f, Rgba.White.WithAlpha(0.9f * fade));
                SetColor(Rgba.White.WithAlpha(0.95f * fade));
                Tri(tipAt + dir * 8f, tipAt - dir * 4f + side * 6f, tipAt - dir * 4f - side * 6f);
                return;
            }
            if (half < 0.2f)  // a thrust or a lunge: a streak along the line
            {
                Line(fx.Pos, fx.Pos2, 8f * (1f - f), fx.Color.WithAlpha(0.35f * (1f - f)));
                Line(fx.Pos, fx.Pos2, 2.5f, Rgba.White.WithAlpha(0.9f * (1f - f)));
                return;
            }
            float sweep = 2f * half * Mathf.Min(1f, f * 3f);
            float from = half >= MathX.Pi - 0.01f ? aim + t * 6f : aim - half;
            Arc(fx.Pos, reach * 0.78f, reach * 0.4f, fx.Color.WithAlpha(0.28f * (1f - f)), 24, from, sweep);
            Arc(fx.Pos, reach, 3f, Rgba.White.WithAlpha(0.9f * (1f - f)), 24, from, sweep);
        }

        /// The Lance: its rectangular hitbox slides out from the Knight, lance tip leading.
        void DrawJabFx(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, w = fx.Radius;
            if (reach < 1f)
                return;
            V2 dir = to / reach, side = dir.Orthogonal();
            float g = Mathf.Min(1f, f / 0.35f);
            g = 1f - (1f - g) * (1f - g) * (1f - g);  // fast out, easing in to full reach
            float fade = f < 0.35f ? 1f : 1f - (f - 0.35f) / 0.65f;
            float front = reach * g;
            // The box, from your body to its sliding front edge.
            V2 a0 = fx.Pos + side * w, a1 = fx.Pos - side * w, b0 = a0 + dir * front, b1 = a1 + dir * front;
            SetColor(fx.Color.WithAlpha(0.2f * fade));
            Tri(a0, b0, b1);
            Tri(a0, b1, a1);
            Line(a0, b0, 1.5f, fx.Color.WithAlpha(0.6f * fade));
            Line(a1, b1, 1.5f, fx.Color.WithAlpha(0.6f * fade));
            Line(b0, b1, 4f, Rgba.White.WithAlpha(0.9f * fade));  // the leading edge
            // Speed streaks riding along inside it.
            for (int k = 0; k < 4; k++)
            {
                float o = (k - 1.5f) / 1.5f * w * 0.7f, at = front * (0.25f + 0.15f * (k % 2));
                V2 s0 = fx.Pos + dir * at + side * o;
                Line(s0, s0 + dir * front * 0.35f, 1.5f, Rgba.White.WithAlpha(0.45f * fade));
            }
            // The lance itself, slid out with its tip at the front edge.
            V2 tipAt = fx.Pos + dir * front, tail = fx.Pos + dir * Mathf.Max(0f, front - reach * 0.8f);
            Line(tail, tipAt - dir * 10f, 4f, Rgba.White.WithAlpha(0.85f * fade));
            SetColor(Rgba.White.WithAlpha(0.95f * fade));
            Tri(tipAt + dir * 6f, tipAt - dir * 14f + side * 7f, tipAt - dir * 14f - side * 7f);
        }

        /// Jagged lightning between two points.
        void DrawBoltFx(Effect fx, float f)
        {
            V2 a = fx.Pos, b = fx.Pos2, n = (b - a).Normalized().Orthogonal();
            V2 prev = a;
            for (int k = 1; k <= 6; k++)
            {
                V2 pt = k == 6 ? b : V2.Lerp(a, b, k / 6f) + n * (((k * 37 + fx.Pos.X) % 17f) - 8.5f);
                Line(prev, pt, 3f, fx.Color.WithAlpha(1f - f));
                prev = pt;
            }
        }

        bool ImCaged()
        {
            foreach (var c in game.Cages.Values)
                if (c.PlayerId == game.MyId)
                    return true;
            return false;
        }

        bool AnyWells()
        {
            foreach (var o in game.Enemies.Values)
                if (o.Type == EnemyType.GravityWell)
                    return true;
            return false;
        }

        float laserAngle;  // the Singularity's laser sweep, smoothed between snapshots

        /// The Singularity's three lasers (a flickering warning first, then the beams) and the cage rings.
        void DrawSingularityHazards(float t)
        {
            Enemy sing = null;
            foreach (var e in game.Enemies.Values)
                if (e.Type == EnemyType.Singularity && !game.IsSpawning(e))
                    sing = e;
            if (sing != null && (sing.Phase == Game.SingLaserWarn || sing.Phase == Game.SingLasers))
            {
                // Sweep smoothly at the known speed and ease toward the host's angle.
                if (sing.Phase == Game.SingLasers)
                    laserAngle += Game.LaserSpin * Time.deltaTime;
                float diff = MathX.Fmod(sing.Facing - laserAngle + MathX.Pi, MathX.Tau) - MathX.Pi;
                laserAngle += Mathf.Abs(diff) > 0.5f ? diff : diff * Mathf.Min(1f, Time.deltaTime * 8f);
                for (int k = 0; k < Game.LaserCount; k++)
                {
                    V2 dir = V2.FromAngle(laserAngle + MathX.Tau * k / Game.LaserCount);
                    V2 from = sing.Disp + dir * sing.Radius, to = sing.Disp + dir * 2000f;
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

        /// The beam between the Twin Reapers: crackling dusk-to-dawn colors. It hurts to touch.
        void DrawReaperTether(float t)
        {
            Enemy a = null, b = null;
            foreach (var e in game.Enemies.Values)
                if (Game.IsTwin(e.Type) && !game.IsSpawning(e))
                {
                    if (a == null) a = e; else b = e;
                }
            if (a == null || b == null)
                return;
            V2 from = a.Disp, to = b.Disp, n = (to - from).Normalized().Orthogonal();
            Line(from, to, Game.TetherWidth + 8f, new Rgba(0.75f, 0.4f, 1f, 0.18f));
            Line(from, to, Game.TetherWidth, new Rgba(0.85f, 0.5f, 1f, 0.85f));
            // A jagged core so it reads as dangerous.
            V2 prev = from;
            for (int k = 1; k <= 16; k++)
            {
                V2 pt = k == 16 ? to : V2.Lerp(from, to, k / 16f) + n * (3f * Mathf.Sin(t * 30f + k * 1.7f));
                Line(prev, pt, 2f, new Rgba(1f, 0.95f, 1f, 0.9f));
                prev = pt;
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
                    break;
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
                case ZoneKind.Ward:  // a paper talisman with a slowly turning seal ring
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.08f), 48);
                    Arc(z.Pos, z.Radius * 0.55f, 2f, new Rgba(0.9f, 0.25f, 0.25f, 0.5f), 32, t * 0.5f, MathX.Tau * 0.8f);
                    SetColor(new Rgba(1f, 0.93f, 0.78f));
                    Quad(z.Pos + new V2(-7f, -12f), z.Pos + new V2(7f, -12f), z.Pos + new V2(7f, 12f), z.Pos + new V2(-7f, 12f));
                    Line(z.Pos + new V2(0f, -8f), z.Pos + new V2(0f, 8f), 2.5f, new Rgba(0.9f, 0.2f, 0.2f));
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
            if (block > rad)  // the guardian spirit's bullet-eating field, fading as it soaks
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
            float r = e.Radius;
            bool spawning = game.IsSpawning(e);
            if (spawning)
            {
                Arc(pos, r + 6f + 4f * Mathf.Sin(t * 12f), 2f, col.WithAlpha(0.6f), 32);
                col = col.WithAlpha(0.35f);
            }
            // Melee enemies are spiky, so you can tell rushers from shooters at a glance.
            if (e.Def.Melee && !spawning)
                DrawMeleeSpikes(e, pos, r, col, t);
            col = DrawEnemyUnderlay(e, pos, r, col, spawning, t);
            if (e.Elite)
                Arc(pos, r + 5f, 2.5f, new Rgba(1f, 0.85f, 0.3f, 0.9f), 32);
            col = DrawEnemyStatus(e, pos, r, col, t);
            if (e.Flash > 0f)
                col = Rgba.White;
            Polygon(pos, r, e.Def.Sides, EnemyRotation(e, t), col);
            if (e.Frozen)
                DrawIceBlock(e, pos, r, t);
            DrawEnemyDetails(e, pos, r, col, t);
            // Bosses get a bar at the top of the screen instead.
            bool bar = e.Hp < e.MaxHp && !e.Def.IsBoss && !spawning;
            if (bar)
            {
                float w = Mathf.Max(30f, r * 2f);
                Bar(pos + new V2(-w / 2f, -r - 10f), w, 4f, e.Hp / e.MaxHp, new Rgba(1f, 0.3f, 0.3f));
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
                    if (e.Phase == 3)
                        DrawWardenCrossGuides(e, pos, r);
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
                    if (e.Phase == 1)  // shielded by her swarm
                    {
                        Circle(pos, r + 16f, new Rgba(1f, 0.85f, 0.3f, 0.15f), 40);
                        Arc(pos, r + 16f, 3f, new Rgba(1f, 0.9f, 0.4f, 0.8f), 40);
                    }
                    break;
                case EnemyType.TwinA:
                case EnemyType.TwinB:
                    DrawTwinShell(e, pos, r, t);
                    break;
                case EnemyType.Singularity:
                    if (e.Phase == 1)  // gravity well: rings shrinking inward
                        for (int k = 0; k < 4; k++)
                        {
                            float f = 1f - MathX.Fmod(t * 0.8f + k * 0.25f, 1f);
                            Arc(pos, r + 40f + f * 360f, 2f, col.WithAlpha(0.35f * (1f - f)), 64);
                        }
                    Circle(pos, r * 1.6f, new Rgba(0.05f, 0f, 0.1f, 0.9f), 40);  // dark core halo
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
                    if (e.Phase == 1)
                        DrawLichTether(pos, r, t);
                    break;
                case EnemyType.Phylactery:
                    Circle(pos, r * 2.2f, col.WithAlpha(0.15f + 0.1f * Mathf.Sin(t * 6f)), 24);
                    break;
                case EnemyType.GravityWell:
                    if (!spawning)
                        DrawWellPull(e, pos, r, col, t);
                    break;
                case EnemyType.StormHerald:
                    for (int k = 0; k < 3; k++)  // crackling sparks
                    {
                        V2 a = pos + V2.FromAngle(t * 4f + k * 2.1f) * (r + 6f);
                        Line(a, a + V2.FromAngle(t * 17f + k) * 10f, 2f, new Rgba(0.8f, 0.9f, 1f, 0.8f));
                    }
                    break;
                case EnemyType.Devourer:
                    if (e.Phase == 1)  // hungry: a pulsing warning glow
                        Circle(pos, r + 18f + 6f * Mathf.Sin(t * 10f), new Rgba(1f, 0.2f, 0.6f, 0.25f), 40);
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

        /// The Lich, protected: a tether to its phylactery and a green shell.
        void DrawLichTether(V2 pos, float r, float t)
        {
            foreach (var o in game.Enemies.Values)
                if (o.Type == EnemyType.Phylactery)
                    Line(pos, o.Disp, 2f, new Rgba(0.4f, 1f, 0.6f, 0.5f + 0.3f * Mathf.Sin(t * 8f)));
            Circle(pos, r + 14f, new Rgba(0.4f, 1f, 0.6f, 0.15f), 32);
            Arc(pos, r + 14f, 3f, new Rgba(0.4f, 1f, 0.6f, 0.8f), 32);
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

        /// The Eclipse: a dark disk with a corona that grows wilder each phase.
        void DrawEclipseCorona(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            for (int k = 0; k < 12; k++)
            {
                float a = MathX.Tau * k / 12f + t * (0.3f + 0.3f * e.Phase);
                Line(pos + V2.FromAngle(a) * (r + 2f), pos + V2.FromAngle(a) * (r + 14f + 6f * e.Phase + 4f * Mathf.Sin(t * 6f + k)), 3f, col.WithAlpha(0.8f));
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
                    return e.Facing;  // point where they're aiming
                case EnemyType.Spinner:
                    return t * (e.Phase == 1 ? 7f : 1.5f) + e.Id;  // revs up while it's spraying
                case EnemyType.Singularity:
                    return t * (e.Phase == 1 ? -3f : 1f);
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
        void DrawEnemyDetails(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.Brute:
                    DrawBruteFace(e, pos, r, col);
                    break;
                case EnemyType.Shielder:
                    Arc(pos, r + 7f, 4f, new Rgba(0.7f, 0.85f, 1f, 0.9f), 20, e.Facing - 1.13f, 2.26f);
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
                    Circle(pos, r * (e.Phase == 1 ? 0.7f : 0.4f), new Rgba(0.1f, 0f, 0.05f, 1f), 24);  // the mouth opens when hungry
                    break;
                case EnemyType.Eclipse:
                    Circle(pos, r * 0.8f, new Rgba(0.02f, 0.02f, 0.04f, 1f), 32);
                    break;
                case EnemyType.Phylactery:
                    Polygon(pos, r * 0.5f, 4, -t * 2f, Rgba.White.WithAlpha(0.8f));
                    break;
                case EnemyType.GravityWell:  // a spinning black hole
                case EnemyType.CageWell:
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

        /// A row of small icons over an enemy, one per status: snowflake = frozen, blue chevrons =
        /// slowed, flame = burning, red crosshair = marked (takes extra damage).
        void DrawStatusIcons(Enemy e, V2 center, float t)
        {
            int n = (e.Frozen ? 1 : 0) + (e.Chilled ? 1 : 0) + (e.Burning ? 1 : 0) + (e.Marked ? 1 : 0);
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
            if (e.Marked)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                Arc(c, size * 0.75f, 1.6f, MarkColor, 12);
                Line(c - new V2(size, 0f), c + new V2(size, 0f), 1.5f, MarkColor);
                Line(c - new V2(0f, size), c + new V2(0f, size), 1.5f, MarkColor);
            }
        }

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
            // Class marker: a mage's four-point star, a summoner's three orbiting motes.
            if (p.Kit == Kit.Mage && !p.Downed)
                for (int k = 0; k < 4; k++)
                    Line(p.Disp + V2.FromAngle(MathX.Pi / 4f + k * MathX.Pi / 2f) * (r * 0.35f),
                         p.Disp + V2.FromAngle(MathX.Pi / 4f + k * MathX.Pi / 2f) * (r + 6f), 2f, new Rgba(0.8f, 0.55f, 1f, 0.8f));
            else if (p.Kit == Kit.Summoner && !p.Downed)
                for (int k = 0; k < 3; k++)
                    Circle(p.Disp + V2.FromAngle(t * 2f + MathX.Tau * k / 3f) * (r + 7f), 2.5f, new Rgba(0.6f, 1f, 0.7f, 0.9f), 8);
            else if (p.Kit == Kit.Knight && !p.Downed)
                DrawKnight(p, r, t);
            else if (p.Kit == Kit.Ninja && !p.Downed)
                DrawNinja(p, r, t, col);
            // The real hitbox, drawn as a bright core.
            Circle(p.Disp, p.Def.HitR, new Rgba(1f, 1f, 1f, 0.85f), 16);
            Circle(p.Disp, p.Def.HitR * 0.5f, col, 12);

            if (!preview)  // menu previews (LobbyUi.cs) leave out the health bar
                Bar(p.Disp + new V2(-20f, -r - 12f), 40f, 4f, p.Hp / p.MaxHp, new Rgba(0.3f, 1f, 0.4f));
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
                case WeaponId.Warhammer: case WeaponId.HolyMace:
                    Line(grip, grip + dir * (r + 12f), 3f, new Rgba(0.6f, 0.45f, 0.3f));
                    Polygon(grip + dir * (r + 14f), 6f, p.Weapon.Id == WeaponId.HolyMace ? 6 : 4, p.Aim, p.Weapon.Id == WeaponId.HolyMace ? new Rgba(1f, 0.9f, 0.5f) : Steel);
                    break;
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
            if (p.UnbreakT > 0f)
                Polygon(p.Disp, r + 9f, 8, t * 0.5f, new Rgba(0.9f, 0.8f, 0.5f, 0.35f));
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
                case WeaponId.Kusarigama:  // a sickle, its chain looping round
                    Line(grip, grip + dir * (r + 6f), 3f, Wood.WithAlpha(a));
                    Arc(grip + dir * (r + 6f) - side * 5f, 6f, 2.5f, Steel.WithAlpha(a), 8, p.Aim - MathX.Pi / 2f, MathX.Pi);
                    for (int k = 0; k < 5; k++)
                        Circle(p.Disp + V2.FromAngle(t * 6f + k * 0.5f) * (r + 9f), 1.6f, Steel.WithAlpha(0.7f * a), 6);
                    break;
                case WeaponId.BoStaff:  // a long staff turning in the hands
                {
                    V2 s = V2.FromAngle(p.Aim + 0.6f * Mathf.Sin(t * 3f) + MathX.Pi / 2f);
                    Line(p.Disp - s * (r + 14f), p.Disp + s * (r + 14f), 3.5f, Wood.WithAlpha(a));
                    break;
                }
                case WeaponId.ChainShuriken:
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

        /// A four-pointed throwing star.
        void DrawStar(V2 c, float r, float rot, Rgba col)
        {
            SetColor(col);
            for (int k = 0; k < 4; k++)
            {
                V2 d = V2.FromAngle(rot + MathX.Pi / 2f * k), s = d.Orthogonal();
                Tri(c + d * r, c + s * (r * 0.3f), c - s * (r * 0.3f));
            }
            Circle(c, r * 0.25f, NinjaCloth.WithAlpha(col.A), 6);
        }

        // --- GL primitives (arena coordinates; everything is triangles) -------

        Vector3 Px(V2 p) => CanvasPx(glShift + p * glZoom);

        /// A point on the UI canvas (or, while the world is drawn, in the arena) in screen pixels.
        Vector3 CanvasPx(V2 u) => new Vector3(offX + u.X * drawScale, UnityEngine.Screen.height - (offY + u.Y * drawScale), 0f);

        /// Start drawing game models (GL) on top of the menus, in UI canvas coordinates. IMGUI draws in order
        /// on Repaint, so models land above the panels drawn before and below those drawn after. Returns
        /// false (draw nothing) on other events. Always pair with EndUiGl.
        bool BeginUiGl()
        {
            if (Event.current.type != EventType.Repaint || glMaterial == null)
                return false;
            glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            drawScale = scale;
            return true;
        }

        void EndUiGl()
        {
            GL.End();
            GL.PopMatrix();
            glZoom = 1f;
            glShift = V2.Zero;
            glClipOn = false;
        }

        // Clipping for menu previews (the loadout's mini-arena): triangles are cut to this UI canvas rect.
        bool glClipOn;
        Rect glClip;
        readonly V2[] clipIn = new V2[9], clipOut = new V2[9];

        /// Until EndUiGl (or UiNoClip), cut everything drawn to this rect of the UI canvas.
        void UiClip(Rect r)
        {
            glClip = r;
            glClipOn = true;
        }

        void UiNoClip() => glClipOn = false;

        /// Models drawn next sit around (0, 0), scaled by zoom and centred on `at` (UI canvas).
        void UiModelAt(V2 at, float zoom)
        {
            glShift = at;
            glZoom = zoom;
        }

        static void SetColor(Rgba c) => GL.Color(new Color(c.R, c.G, c.B, c.A));

        void Tri(V2 a, V2 b, V2 c)
        {
            if (!glClipOn)
            {
                GL.Vertex(Px(a));
                GL.Vertex(Px(b));
                GL.Vertex(Px(c));
                return;
            }
            V2 ua = glShift + a * glZoom, ub = glShift + b * glZoom, uc = glShift + c * glZoom;
            float x0 = glClip.x, x1 = glClip.xMax, y0 = glClip.y, y1 = glClip.yMax;
            if ((ua.X < x0 && ub.X < x0 && uc.X < x0) || (ua.X > x1 && ub.X > x1 && uc.X > x1) ||
                (ua.Y < y0 && ub.Y < y0 && uc.Y < y0) || (ua.Y > y1 && ub.Y > y1 && uc.Y > y1))
                return;  // all outside
            if (InClip(ua) && InClip(ub) && InClip(uc))
            {
                GL.Vertex(CanvasPx(ua));
                GL.Vertex(CanvasPx(ub));
                GL.Vertex(CanvasPx(uc));
                return;
            }
            // Cut the triangle against each edge in turn (Sutherland-Hodgman), then fan it back into triangles.
            clipIn[0] = ua; clipIn[1] = ub; clipIn[2] = uc;
            int n = 3;
            n = ClipEdge(clipIn, n, clipOut, 0, x0, false);
            n = ClipEdge(clipOut, n, clipIn, 0, x1, true);
            n = ClipEdge(clipIn, n, clipOut, 1, y0, false);
            n = ClipEdge(clipOut, n, clipIn, 1, y1, true);
            for (int i = 1; i + 1 < n; i++)
            {
                GL.Vertex(CanvasPx(clipIn[0]));
                GL.Vertex(CanvasPx(clipIn[i]));
                GL.Vertex(CanvasPx(clipIn[i + 1]));
            }
        }

        bool InClip(V2 u) => u.X >= glClip.x && u.X <= glClip.xMax && u.Y >= glClip.y && u.Y <= glClip.yMax;

        /// Keeps the part of polygon src on one side of the line axis = at (max: keep below it), into dst.
        static int ClipEdge(V2[] src, int n, V2[] dst, int axis, float at, bool max)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                V2 p = src[i], q = src[(i + 1) % n];
                float pv = axis == 0 ? p.X : p.Y, qv = axis == 0 ? q.X : q.Y;
                bool pin = max ? pv <= at : pv >= at, qin = max ? qv <= at : qv >= at;
                if (pin)
                    dst[m++] = p;
                if (pin != qin)
                    dst[m++] = V2.Lerp(p, q, (at - pv) / (qv - pv));
            }
            return m;
        }

        void Quad(V2 a, V2 b, V2 c, V2 d)
        {
            Tri(a, b, c);
            Tri(a, c, d);
        }

        void FillRect(float x, float y, float w, float h, Rgba c)
        {
            SetColor(c);
            Quad(new V2(x, y), new V2(x + w, y), new V2(x + w, y + h), new V2(x, y + h));
        }

        void Line(V2 a, V2 b, float width, Rgba c)
        {
            V2 n = (b - a).Normalized().Orthogonal() * (width / 2f);
            SetColor(c);
            Quad(a + n, b + n, b - n, a - n);
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

        void Circle(V2 center, float r, Rgba c, int segs)
        {
            SetColor(c);
            V2 prev = center + new V2(r, 0);
            for (int i = 1; i <= segs; i++)
            {
                V2 next = center + V2.FromAngle(MathX.Tau * i / segs) * r;
                Tri(center, prev, next);
                prev = next;
            }
        }

        void Arc(V2 center, float r, float width, Rgba c, int segs, float start = 0f, float sweep = MathX.Tau)
        {
            SetColor(c);
            float r0 = Mathf.Max(0f, r - width / 2f), r1 = r + width / 2f;
            for (int i = 0; i < segs; i++)
            {
                V2 d0 = V2.FromAngle(start + sweep * i / segs), d1 = V2.FromAngle(start + sweep * (i + 1) / segs);
                Quad(center + d0 * r0, center + d0 * r1, center + d1 * r1, center + d1 * r0);
            }
        }

        void Polygon(V2 center, float r, int sides, float rot, Rgba c)
        {
            var pts = new V2[sides];
            for (int i = 0; i < sides; i++)
                pts[i] = center + V2.FromAngle(rot + MathX.Tau * i / sides) * r;
            SetColor(c.Darkened(0.45f));
            for (int i = 0; i < sides; i++)
                Tri(center, pts[i], pts[(i + 1) % sides]);
            for (int i = 0; i < sides; i++)
                Line(pts[i], pts[(i + 1) % sides], 2.5f, c);
        }

        void Bar(V2 pos, float w, float h, float frac, Rgba c)
        {
            FillRect(pos.X, pos.Y, w, h, new Rgba(0f, 0f, 0f, 0.6f));
            FillRect(pos.X, pos.Y, w * MathX.Clamp01(frac), h, c);
        }

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

        void DrawAimLine(Player me, V2 cursor, float t)
        {
            V2 dir = V2.FromAngle(me.Aim);
            V2 from = me.Disp + dir * (me.Def.BodyR + 8f);
            // Out to the crosshair, but never past where your shots (or swings) stop.
            float range = Mathf.Max(game.WeaponRange(me) - me.Def.BodyR - 8f, 20f);
            float toCursor = cursor.DistanceTo(me.Disp) - me.Def.BodyR - 8f;
            float reach = Mathf.Min(range, toCursor);
            if (reach < 4f)
                return;  // crosshair on top of you
            var c = me.Def.Color.Mix(Rgba.White, 0.35f);
            const int Steps = 24;
            for (int i = 0; i < Steps; i++)
            {
                float a = (float)i / Steps, b = (float)(i + 1) / Steps;
                float fade = 1f - 0.8f * a;  // strongest at the gun, still visible at the crosshair
                V2 p0 = from + dir * (reach * a), p1 = from + dir * (reach * b);
                Line(p0, p1, 8f, c.WithAlpha(0.07f * fade));   // soft glow
                Line(p0, p1, 1.5f, c.WithAlpha(0.32f * fade));  // thin core
            }
            // A small cap where the weapon's reach ends (when the crosshair is beyond it).
            if (toCursor >= range)
            {
                V2 end = from + dir * reach, side = dir.Orthogonal();
                Line(end - side * 6f, end + side * 6f, 2f, c.WithAlpha(0.45f));
            }
            // Motes drifting outward.
            const float Spacing = 34f;
            float offset = MathX.Fmod(t * 140f, Spacing);
            for (float d = offset; d < reach; d += Spacing)
            {
                float f = 1f - 0.75f * d / reach;
                Circle(from + dir * d, 1.3f + 1.2f * f, c.WithAlpha(0.5f * f), 8);
            }
        }
    }
}
