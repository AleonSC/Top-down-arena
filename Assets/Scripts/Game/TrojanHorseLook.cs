using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Trojan Horse (the Tank Knight's second ultimate, Core/TrojanHorse.cs), from the claude.ai artifact "Trojan Horse". Drawn
    /// from the replicated zone (`ZoneKind.TrojanHorse`: Pos, Aim, Marched, Quiet = parked, GuardFrac its health) and each
    /// player's replicated HorseId:
    ///   the horse seen from above: a shadow, four wheels whose spokes turn with how far it has rolled, the platform, a planked
    ///     barrel body with three iron bands and three arrow slits a side, the neck and head (ears, eyes, muzzle, mane) and a tail
    ///     that sways; it bobs while it rolls and flashes pale when it is hit (GuardFrac dropping);
    ///   a slit glows gold while a rider on that side holds Fire;
    ///   over it: the health bar (a pale drain behind), one pip per seat lit in each rider's colour, and its time arc;
    ///   while everyone up is inside, enemies go for it: a red pulse round the body;
    ///   your Interact hint by it or in it (DrawHorseHint, from the HUD), and a pulsing ring of its boarding reach when you're by it;
    ///   FxKind.TrojanEnd: planks tumbling out (it broke) or its sides swinging open with a gold ring (Sally Forth).
    /// Riders aren't drawn (WorldPlayers.cs); Sally Forth puts a gold ring round anyone with it on. Visual effects off: no bob,
    /// flash, pulses or planks.
    public partial class ArenaApp
    {
        static readonly Rgba HorseWoodC = new Rgba(0.69f, 0.48f, 0.27f), HorseWoodDark = new Rgba(0.43f, 0.27f, 0.13f);
        static readonly Rgba HorseWoodLight = new Rgba(0.85f, 0.64f, 0.42f), HorseIron = new Rgba(0.29f, 0.17f, 0.07f);
        static readonly Rgba HorseSlitDark = new Rgba(0.1f, 0.06f, 0.02f), HorseGoldC = Rgba.Gold;
        static readonly Rgba HorseHunt = new Rgba(0.78f, 0.3f, 0.34f);

        readonly Dictionary<int, float> horseFrac = new Dictionary<int, float>(), horseFlash = new Dictionary<int, float>(), horseGhost = new Dictionary<int, float>();

        // The frame being drawn: its middle, forward (along its heading) and across.
        V2 hsO, hsF, hsS;
        V2 HsP(float lx, float ly) => hsO + hsF * lx + hsS * ly;

        void HorseQuad(float x0, float y0, float x1, float y1, Rgba c)
        {
            SetColor(c);
            Quad(HsP(x0, y0), HsP(x1, y0), HsP(x1, y1), HsP(x0, y1));
        }

        /// The horse, from its zone (WorldEnemies.cs's zone pass).
        void DrawTrojanHorse(Zone z, float t)
        {
            float age = Game.HorseAge(z), frac = Mathf.Clamp01(z.GuardFrac);
            float last, flash;
            if (horseFrac.TryGetValue(z.Id, out last) && frac < last - 0.001f)
                horseFlash[z.Id] = 0.1f;
            horseFrac[z.Id] = frac;
            horseFlash.TryGetValue(z.Id, out flash);
            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime);
            horseFlash[z.Id] = flash;
            float rise = Mathf.Clamp01(age / 0.3f);  // it pops up out of the ground at the cast
            float bob = fxOn && !z.Quiet ? Mathf.Sin(z.Marched * 0.11f) * 1.2f : 0f;
            hsO = z.Pos;
            hsF = V2.FromAngle(z.Aim);
            hsS = new V2(-hsF.Y, hsF.X);
            float k = 0.6f + 0.4f * rise;  // (scale while it pops up)
            V2 c = z.Pos;

            if (fxOn && game.HorseHunted(z))
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f);
                GlowDisc(c, 70f + 8f * pulse, HorseHunt, 0.18f + 0.12f * pulse);
            }
            Ellipse(c + new V2(-3f, 5f), 58f * k, 31f * k, z.Aim, new Rgba(0f, 0f, 0f, 0.35f), 20);  // shadow

            // wheels: dark blocks with spokes that turn as it rolls
            float spin = Mathf.Repeat(z.Marched * 0.5f, 6f);
            for (int w = 0; w < 4; w++)
            {
                float wx = (w < 2 ? -30f : 22f) * k, wy = (w % 2 == 0 ? -26f : 26f) * k;
                HorseQuad(wx - 9f, wy - 4f, wx + 9f, wy + 4f, new Rgba(0.16f, 0.1f, 0.05f));
                for (float sx = -9f + spin; sx < 9f; sx += 6f)
                    Line(HsP(wx + sx, wy - 4f), HsP(wx + sx, wy + 4f), 1.2f, HorseWoodLight);
            }
            HorseQuad(-46f * k, -22f * k, 36f * k, 22f * k, HorseWoodDark);  // the platform

            // tail
            float sway = fxOn ? Mathf.Sin(t * 3f) * 3f : 0f;
            V2 t0 = HsP(-46f * k, 0f), t1 = HsP(-58f * k, 3f + sway), t2 = HsP(-70f * k, -2f + sway * 1.3f);
            Line(t0, t1, 5f, HorseWoodDark);
            Line(t1, t2, 4f, HorseWoodDark);

            // the body: a planked barrel
            Rgba wood = flash > 0f && fxOn ? HorseWoodC.Mix(new Rgba(1f, 0.95f, 0.84f), flash / 0.1f) : HorseWoodC;
            Ellipse(HsP(-6f, bob), 44f * k, 20f * k, z.Aim, wood, 24);
            for (int i = -2; i <= 2; i++)  // the planks, along its length
            {
                float y = i * 7f, half = 44f * Mathf.Sqrt(Mathf.Max(0f, 1f - (y / 20f) * (y / 20f)));
                Line(HsP((-6f - half) * k, y * k + bob), HsP((-6f + half) * k, y * k + bob), 1.2f, HorseWoodDark.WithAlpha(0.7f));
            }
            foreach (float bx in new[] { -28f, -6f, 16f })  // iron bands
            {
                float half = 19f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((bx + 6f) / 44f, 2f))) * k;
                Line(HsP(bx * k, -half + bob), HsP(bx * k, half + bob), 3f, HorseIron);
            }
            // the slits: lit on the side a rider is shooting from
            bool litL = false, litR = false;
            foreach (var p in game.Players.Values)
                if (p.HorseId == z.Id && !p.Downed && game.HoldingFire(p))
                {
                    if (V2.FromAngle(p.Aim).Dot(hsS) >= 0f) litR = true; else litL = true;
                }
            foreach (float sx in new[] { -24f, -6f, 12f })
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    bool lit = sd > 0 ? litR : litL;
                    float sy = sd * 17f * k;
                    HorseQuad(sx * k - 4f, sy - 1.5f + bob, sx * k + 4f, sy + 1.5f + bob, lit ? HorseGoldC : HorseSlitDark);
                    if (lit && fxOn)
                        GlowDisc(HsP(sx * k, sy + sd * 3f + bob), 7f, HorseGoldC, 0.35f);
                }
            // neck and head
            SetColor(wood);
            Quad(HsP(26f * k, -10f + bob), HsP(50f * k, -8f), HsP(50f * k, 8f), HsP(26f * k, 10f + bob));
            Ellipse(HsP(58f * k, 0f), 14f * k, 9f * k, z.Aim, wood, 14);
            Ellipse(HsP(70f * k, 0f), 6f * k, 6f * k, z.Aim, HorseWoodDark, 10);  // muzzle
            HorseQuad(55f * k, -7f * k, 58f * k, -4f * k, HorseSlitDark);  // eyes
            HorseQuad(55f * k, 4f * k, 58f * k, 7f * k, HorseSlitDark);
            SetColor(HorseWoodLight);  // ears
            Tri(HsP(50f * k, -8f * k), HsP(54f * k, -15f * k), HsP(57f * k, -8f * k));
            Tri(HsP(50f * k, 8f * k), HsP(54f * k, 15f * k), HsP(57f * k, 8f * k));
            for (float mx = 28f; mx < 50f; mx += 5f)  // mane
                Line(HsP(mx * k, 0f), HsP((mx - 3f) * k, 0f), 3f, HorseIron);

            DrawHorseBar(z, age, frac);
            var me = game.Me;
            if (fxOn && me != null && !me.Downed && !Game.Riding(me) && me.Pos.CloserThan(z.Pos, Game.HorseBoardR))
                Arc(z.Pos, Game.HorseBoardR, 1.5f, HorseGoldC.WithAlpha(0.35f + 0.35f * Mathf.Sin(t * 6f)), 40);
        }

        /// Over the horse: its health (with a pale drain), the seats and its time.
        void DrawHorseBar(Zone z, float age, float frac)
        {
            float ghost;
            if (!horseGhost.TryGetValue(z.Id, out ghost) || ghost < frac)
                ghost = frac;
            ghost = Mathf.Max(frac, ghost - Time.unscaledDeltaTime * 0.6f);
            horseGhost[z.Id] = ghost;
            const float w = 90f, h = 5f;
            V2 at = z.Pos + new V2(-w * 0.5f, -60f);
            FillRect(at.X - 1.5f, at.Y - 1.5f, w + 3f, h + 3f, new Rgba(0f, 0f, 0f, 0.65f));
            FillRect(at.X, at.Y, w * ghost, h, new Rgba(1f, 0.93f, 0.78f, 0.5f));
            FillRect(at.X, at.Y, w * frac, h, frac > 0.35f ? HorseWoodLight : HorseHunt);
            FillRect(at.X, at.Y, w * frac, 1.5f, Rgba.White.WithAlpha(0.4f));
            // seats: one pip per player in the run, lit in each rider's colour
            int seats = Mathf.Max(1, game.Players.Count), n = 0;
            float x0 = z.Pos.X - (seats - 1) * 6f;
            foreach (var p in game.Players.Values)
                if (p.HorseId == z.Id && !p.Downed && n < seats)
                    Circle(new V2(x0 + 12f * n++, at.Y - 9f), 4f, p.Def.Color, 10);
            for (int i = n; i < seats; i++)
                Arc(new V2(x0 + 12f * i, at.Y - 9f), 4f, 1.2f, new Rgba(0.45f, 0.5f, 0.6f, 0.8f), 12);
            float left = Mathf.Clamp01(1f - age / Game.HorseTime);
            Arc(new V2(at.X + w + 11f, at.Y + h * 0.5f), 5.5f, 2f, HorseGoldC.WithAlpha(0.2f), 16);
            Arc(new V2(at.X + w + 11f, at.Y + h * 0.5f), 5.5f, 2f, HorseGoldC.WithAlpha(0.8f), 16, -Mathf.PI / 2f, MathX.Tau * left);
        }

        /// Sally Forth on a player: a gold ring round them while the boost lasts (WorldPlayers.cs).
        void DrawSallyRing(Player p, float t)
        {
            if (p.SallyT <= 0f || p.Downed)
                return;
            float a = Mathf.Clamp01(p.SallyT / 0.5f);
            Arc(p.Disp, p.Def.BodyR + 5f + (fxOn ? Mathf.Sin(t * 12f) : 0f), 2f, HorseGoldC.WithAlpha(0.8f * a), 24);
        }

        /// FxKind.TrojanEnd: planks tumbling out (it broke), or its sides swinging open and a gold ring (Sally Forth).
        void DrawTrojanEndFx(Effect fx, float f)
        {
            if (!fxOn)
                return;
            V2 c = fx.Pos, fwd = (fx.Pos2 - fx.Pos).Normalized(), side = new V2(-fwd.Y, fwd.X);
            bool broke = fx.Radius > 0.5f;
            float v = fx.T, fade = 1f - Mathf.Clamp01((f - 0.6f) / 0.4f);
            if (broke)
            {
                GlowDisc(c, 60f * (1f - Mathf.Clamp01(v / 0.3f)), new Rgba(1f, 0.9f, 0.7f), 0.5f);
                for (int i = 0; i < 18; i++)
                {
                    float h = CoverHash(c.X * 0.13f + c.Y + i * 7.3f), h2 = CoverHash(i * 3.1f + c.X);
                    float ang = h * MathX.Tau, dist = (40f + 90f * h2) * (1f - Mathf.Exp(-4f * v));
                    V2 at = c + V2.FromAngle(ang) * dist;
                    V2 d = V2.FromAngle(ang + v * (3f + 5f * h2)) * (6f + 4f * h);
                    Line(at - d, at + d, 3.5f, (i % 2 == 0 ? HorseWoodC : HorseWoodDark).WithAlpha(fade));
                }
                return;
            }
            float open = Mathf.Clamp01(v / 0.35f);
            for (int sd = -1; sd <= 1; sd += 2)  // the two side doors swinging down and out
            {
                V2 hinge = c + side * (sd * 20f);
                V2 out1 = hinge + side * (sd * 26f * open);
                SetColor(HorseWoodC.WithAlpha(fade));
                Quad(hinge - fwd * 30f, hinge + fwd * 18f, out1 + fwd * 18f, out1 - fwd * 30f);
                Line(hinge - fwd * 30f, hinge + fwd * 18f, 2f, HorseIron.WithAlpha(fade));
            }
            float ring = Game.HorseSallyR * (1f - Mathf.Exp(-5f * v));
            Arc(c, ring, 3f, HorseGoldC.WithAlpha(0.8f * (1f - f)), 48);
            GlowDisc(c, ring * 0.6f, HorseGoldC, 0.25f * (1f - f));
        }

        /// DrawHud: in a horse, or by one, the Interact key to drop out or climb in.
        void DrawHorseHint()
        {
            var me = game.Me;
            if (me == null || me.Downed || !game.Battle)
                return;
            Zone z = game.HorseOf(me);
            bool inside = z != null;
            if (!inside)
                z = game.HorseNear(me.Pos);
            if (z == null)
                return;
            V2 at = ToUi(z.Pos);
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.Interact)).ToUpperInvariant();
            var r = new Rect(at.X - 70f, at.Y + 46f, 140f, 22f);
            Ui.Pill(r, inside ? new Color(0f, 0f, 0f, 0.6f) : CrtStyle.Gold.WithAlpha(0.9f));
            Ui.Label(r, key + (inside ? "   LEAVE" : "   CLIMB IN"), 11, inside ? CrtStyle.Gold : new Color(0.06f, 0.04f, 0.02f), TextAnchor.MiddleCenter, true);
        }
    }
}
