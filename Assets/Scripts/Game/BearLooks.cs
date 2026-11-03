using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Bear Totem in style A, "Raked" (Docs/Mockups/bear-totem.html, the owner's pick), ported from the mockup's numbers
    /// (arena px = the mockup's canvas px). Drawing only, on each screen: hits, reach, bleed and timing are the game's own.
    ///   - A claw on each hand: a fur-cuffed gauntlet with three ivory claws that stretches out as the arm swings. The first
    ///     swipe is the left hand, raking across; the second is the right, raking back (DrawBearHands, from DrawPlayerModel).
    ///     Which hand and how far along it is come from the swipe's own FxKind.Claw effect, so nothing is sent for it.
    ///   - The claws' trail: three comet-shaped gouges that follow the claw tips, a red scar behind them and a few flicked
    ///     drops (DrawBearClawFx, from DrawEffect). With Visual effects off the old arcs are drawn (DrawClawFx).
    ///   - The totem itself, a carved bear-head medallion on the back, glowing warm on each rake.
    ///   - The Bear companion: a brown bear with ears, a snout and four striding paws; its jaw opens before each bite and its
    ///     head lunges (DrawBear, from DrawMinion). Each screen guesses the bite: it snaps when the Bear is next to an enemy
    ///     and again each time that enemy is hurt (the host's bite timer isn't sent).
    ///   - Claw scars on a bleeding enemy, one set per two stacked hits (DrawBleedScars, from DrawEnemy).
    public partial class ArenaApp
    {
        const float BearRakeTime = 0.3f;     // how long a claw effect lasts on screen (the mockup's swipe); the hit lands as it starts, as before
        const float BearHandRadius = 30f;    // how far from the body a hand is
        const float BearGuard = 0.9f;        // each hand's resting angle, out to its own side

        static readonly Rgba BearFur = Hx(0x7a5230), BearFurDark = Hx(0x3d2917), BearIvory = Hx(0xf6ecd6), BearClawEdge = Hx(0x7a6a50),
            BearBodyC = Hx(0x6b4a2e), BearBack = Hx(0x573b24), BearHump = Hx(0x755233), BearHead = Hx(0x8a5a36), BearSnout = Hx(0xb88a5a),
            BearPaw = Hx(0x4b3220), BearPawEdge = Hx(0x2a1a0e), BearMouth = Hx(0x3a0d12), BearTooth = Hx(0xfffaf0), BearNose = Hx(0x1b1209),
            BearGouge = new Rgba(1f, 0.78f, 0.51f), BearGougeCore = new Rgba(1f, 0.96f, 0.88f), BearScar = new Rgba(0.82f, 0.16f, 0.16f);

        // --- Whose claws a Claw effect is ------------------------------------------------------------------------------

        class BearSeen
        {
            public int Owner;   // the Summoner who raked (0: nobody found; drawn where it started)
            public int Hand;    // 0 left (the first swipe), 1 right (the second)
        }

        readonly Dictionary<Effect, BearSeen> bearSeen = new Dictionary<Effect, BearSeen>();
        readonly HashSet<Effect> bearLive = new HashSet<Effect>();

        /// A Bear Totem's claws (other FxKind.Claw effects, the Reaver's and the Wolf pack's, keep their old look).
        static bool IsBearClaw(Effect fx)
        {
            if (fx.Kind != FxKind.Claw)
                return false;
            var c = Game.BearClawColor;
            return Math.Abs(fx.Color.R - c.R) < 0.01f && Math.Abs(fx.Color.G - c.G) < 0.01f && Math.Abs(fx.Color.B - c.B) < 0.01f;
        }

        /// Who raked, noted the first time it's seen (the effect stays where it started, the Summoner moves on). The effect is
        /// then kept on screen for BearRakeTime (front end only; nothing reads an effect's life).
        BearSeen BearSeenOf(Effect fx)
        {
            BearSeen seen;
            if (bearSeen.TryGetValue(fx, out seen))
                return seen;
            Player best = null;
            float bd = 44f;
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon == null || p.Weapon.Id != WeaponId.BearTotem)
                    continue;
                float d = Math.Min(p.Disp.DistanceTo(fx.Pos), p.Pos.DistanceTo(fx.Pos));
                if (d < bd)
                {
                    bd = d;
                    best = p;
                }
            }
            if (bearSeen.Count > 48)
            {
                bearLive.Clear();
                foreach (var e in game.Effects)
                    bearLive.Add(e);
                var gone = new List<Effect>();
                foreach (var k in bearSeen.Keys)
                    if (!bearLive.Contains(k))
                        gone.Add(k);
                foreach (var k in gone)
                    bearSeen.Remove(k);
            }
            seen = new BearSeen { Owner = best != null ? best.Id : 0, Hand = fx.Radius >= 0f ? 0 : 1 };
            bearSeen[fx] = seen;
            if (fxOn && fx.Dur < BearRakeTime)
                fx.Dur = BearRakeTime;
            return seen;
        }

        static float BearHeadAt(float f) => Mathf.Clamp01(f * 3f);   // how far along its arc a raking claw tip has got
        static float BearEase(float u) => u * u * (3f - 2f * u);

        /// Claw k (-1, 0, 1): where its tip is in the swipe's own frame (an angle from the swipe's aim) and how far out.
        static float BearTipAngle(float side, float half, float f, int k)
        {
            float from = -side * half, to = -from, lag = (1f - k * side) * 0.035f;
            return Mathf.Lerp(from, to, Mathf.Clamp01(BearHeadAt(f) - lag));
        }

        static float BearTipRadius(float reach, int k) => reach * (0.77f + 0.14f * k);

        // --- The hands -------------------------------------------------------------------------------------------------

        struct BearPose
        {
            public float Phi, Ext, F, Reach, Half, Side, Off;   // the hand's angle from the player's aim, how far its claws are out, ...
            public bool Swing;                                   // raking now (the claws follow the swipe's arc exactly)
        }

        /// Both hands' poses now. At rest each holds a wide guard with its claws drawn in; a claw effect from this player
        /// takes its hand through the rake (left across, then right back) and the claws slide out and in again.
        void BearPoses(Player p, out BearPose left, out BearPose right, out float glow)
        {
            left = new BearPose { Phi = -BearGuard, Side = 1f, Reach = p.Weapon.Reach };
            right = new BearPose { Phi = BearGuard, Side = -1f, Reach = p.Weapon.Reach };
            glow = 0f;
            foreach (var fx in game.Effects)
            {
                if (!IsBearClaw(fx))
                    continue;
                var seen = BearSeenOf(fx);
                if (seen.Owner != p.Id)
                    continue;
                float f = Mathf.Clamp01(fx.T / fx.Dur);
                glow = Math.Max(glow, 1f - f);
                float half = Math.Abs(fx.Radius), side = fx.Radius >= 0f ? 1f : -1f, from = -side * half, to = -from, guard = -side * BearGuard;
                V2 span = fx.Pos2 - fx.Pos;
                float off = MathX.Fmod(span.Angle - p.Aim + MathX.Pi, MathX.Tau) - MathX.Pi;   // the swipe's aim, against where they point now
                var pose = new BearPose { F = f, Reach = span.Length, Half = half, Side = side };
                if (f < 1f / 3f)
                {
                    pose.Swing = true;
                    pose.Ext = 1f;
                    pose.Phi = Mathf.Lerp(from, to, BearHeadAt(f)) + off;
                    pose.Off = off;
                }
                else
                {
                    float u = BearEase(Mathf.Clamp01((f - 1f / 3f) / (2f / 3f)));
                    pose.Ext = 1f - u;
                    pose.Phi = Mathf.Lerp(to, guard, u) + off * (1f - u);
                }
                if (seen.Hand == 0)
                    left = pose;
                else
                    right = pose;
            }
        }

        /// The Summoner's arms, claws and totem, in place of the held weapon (DrawPlayerModel).
        void DrawBearModel(Player p, ModelPal K, float ry)
        {
            BearPose left, right;
            float glow;
            BearPoses(p, out left, out right, out glow);
            MFrame(0f, 0f, 0f, 1f, false);
            DrawBearMedallion(glow);
            DrawBearHand(K, -ry * 0.72f, left);
            DrawBearHand(K, ry * 0.72f, right);
            MFrame(0f, 0f, 0f, 1f, false);
        }

        /// The totem: a carved bear head worn on the back, glowing warm as the claws go out.
        void DrawBearMedallion(float glow)
        {
            if (glow > 0.02f)
                MCirc(-11f, 0f, 9f + 5f * glow, Hx(0xffbe6e, 0.26f * glow), 14);
            MCirc(-13.5f, -4.6f, 2.5f, BearFurDark, 8); MCirc(-13.5f, -4.6f, 1.5f, BearBodyC, 8);
            MCirc(-13.5f, 4.6f, 2.5f, BearFurDark, 8); MCirc(-13.5f, 4.6f, 1.5f, BearBodyC, 8);
            MCirc(-11f, 0f, 6f, BearFurDark, 14); MCirc(-11f, 0f, 4.8f, BearHead, 14);
            MCirc(-8.6f, -2f, 0.9f, BearNose, 6); MCirc(-8.6f, 2f, 0.9f, BearNose, 6); MEll(-6.8f, 0f, 1.8f, 1.4f, BearSnout);
        }

        void DrawBearHand(ModelPal K, float shoulderY, BearPose pose)
        {
            float phi = pose.Phi, cs = Mathf.Cos(phi), sn = Mathf.Sin(phi), hx = cs * BearHandRadius, hy = sn * BearHandRadius;
            float px = -sn, py = cs;                       // across the hand
            float bend = -pose.Side * 6f * pose.Ext;       // the claws curve back against the way they rake
            ModelArm(K, 1f, shoulderY, hx, hy);
            if (pose.Ext > 0.15f)
                MCirc(hx, hy, 11f + 8f * pose.Ext, Hx(0xffbe6e, 0.14f * pose.Ext), 12);
            for (int i = 0; i < 3; i++)
            {
                int k = i - 1;
                float a = pose.Swing ? pose.Off + BearTipAngle(pose.Side, pose.Half, pose.F, k) + 0f : phi + k * 0.1f;
                float r = Mathf.Lerp(40f, BearTipRadius(pose.Reach, k), pose.Ext);
                float tx = Mathf.Cos(a) * r, ty = Mathf.Sin(a) * r;
                float bx = hx + px * k * 3f, by = hy + py * k * 3f;
                BearBlade(bx, by, (bx + tx) / 2f + px * bend, (by + ty) / 2f + py * bend, tx, ty);
            }
            MEll(hx, hy, 8.6f, 7.4f, BearFurDark, phi); MEll(hx, hy, 7.5f, 6.4f, BearFur, phi);   // the fur-cuffed gauntlet
            for (int i = -1; i <= 1; i++)
                MCirc(hx + cs * 5f + px * i * 3.2f, hy + sn * 5f + py * i * 3.2f, 1.9f, BearHead, 6);
        }

        /// A curved ivory claw, thick at its base and sharp at its tip, from (bx,by) through the control point to (tx,ty).
        void BearBlade(float bx, float by, float cx, float cy, float tx, float ty)
        {
            const int n = 6;
            float px = bx, py = by;
            for (int i = 1; i <= n; i++)
            {
                float u = (float)i / n, v = 1f - u;
                float x = v * v * bx + 2f * u * v * cx + u * u * tx, y = v * v * by + 2f * u * v * cy + u * u * ty;
                float w = 4.2f * (1f - u * 0.92f);
                MLine(px, py, x, y, w + 1.4f, BearClawEdge);
                MLine(px, py, x, y, w, BearIvory);
                px = x; py = y;
            }
        }

        // --- The claws' trails -----------------------------------------------------------------------------------------

        /// A blade-shaped stroke along an arc from a0 to a1: thick in the middle, sharp at both ends.
        void BearTaper(V2 o, float r, float a0, float a1, float maxW, Rgba col, float lead)
        {
            const int n = 14;
            var outer = new V2[n + 1];
            var inner = new V2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n, a = Mathf.Lerp(a0, a1, u), w = maxW * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(MathX.Pi * u)), lead) / 2f;
                V2 d = V2.FromAngle(a);
                outer[i] = o + d * (r + w);
                inner[i] = o + d * (r - w);
            }
            SetColor(col);
            for (int i = 0; i < n; i++)
                Quad(outer[i], outer[i + 1], inner[i + 1], inner[i]);
        }

        static float BearRnd(int i)
        {
            float x = (float)Math.Sin(i * 127.1 + 311.7) * 43758.5453f;
            return x - (float)Math.Floor(x);
        }

        /// One of a Bear Totem's two swipes: three gouges follow the claw tips round the arc, a red scar behind them, a few
        /// drops flicked off the tips.
        void DrawBearClawFx(Effect fx)
        {
            var seen = BearSeenOf(fx);
            V2 span = fx.Pos2 - fx.Pos;
            float R = span.Length;
            if (R < 1f)
                return;
            float f = Mathf.Clamp01(fx.T / fx.Dur);
            Player owner;
            V2 o = seen.Owner != 0 && game.Players.TryGetValue(seen.Owner, out owner) && !owner.Downed ? owner.Disp : fx.Pos;   // the claws go with the Summoner
            float aim = span.Angle, half = Math.Abs(fx.Radius), side = fx.Radius >= 0f ? 1f : -1f, from = -side * half, to = -from;
            float fade = 1f - f * f, head = BearHeadAt(f), tail = Mathf.Clamp(Mathf.Clamp01((f - 0.12f) * 1.7f), 0f, head);
            for (int i = 0; i < 3; i++)
            {
                int k = i - 1;
                float r = BearTipRadius(R, k), lag = (1f - k * side) * 0.035f, h = Mathf.Clamp01(head - lag), t0 = Math.Min(Mathf.Clamp01(tail - lag * 0.5f), h);
                float a0 = aim + Mathf.Lerp(from, to, t0), a1 = aim + Mathf.Lerp(from, to, h);
                if (h - t0 > 0.01f)
                {
                    BearTaper(o, r, a0, a1, 12f, BearGouge.WithAlpha(0.22f * fade), 0.7f);
                    BearTaper(o, r, a0, a1, 7.5f, BearGougeCore.WithAlpha(0.95f * fade), 0.8f);
                }
                Arc(o, r, 1.6f, BearScar.WithAlpha(0.7f * (1f - f)), 12, aim + from, a1 - (aim + from));   // the red scar it leaves
            }
            if (f < 0.1f)
                return;
            float tipA = aim + Mathf.Lerp(from, to, head);
            V2 tip = o + V2.FromAngle(tipA) * R;
            int seed = MeleeSeed(fx);
            for (int i = 0; i < 7; i++)                    // droplets flicked off the tip
            {
                float ang = tipA + side * (0.6f + BearRnd(i + seed) * 0.9f) * (BearRnd(i + seed + 9) > 0.5f ? 1f : 0.6f) + BearRnd(i + 3) * 0.4f;
                float d = 8f + f * (22f + BearRnd(i + 5) * 26f);
                Circle(tip + V2.FromAngle(ang) * d, Mathf.Max(0.5f, 1.8f - f), new Rgba(0.87f, 0.15f, 0.19f, 0.85f * (1f - f)), 6);
            }
        }

        // --- The Bear --------------------------------------------------------------------------------------------------

        class BearView
        {
            public V2 Last;
            public bool Has;
            public float LastT, Phase, Run, Cycle, LastFlash;
            public bool Biting;
        }

        readonly Dictionary<int, BearView> bearViews = new Dictionary<int, BearView>();
        V2 bO, bDir, bSide;
        float bS = 1f, bAlpha = 1f;

        V2 BP(float x, float y) => bO + (bDir * x + bSide * y) * bS;
        Rgba Ba(Rgba c) => c.WithAlpha(c.A * bAlpha);

        void BCirc(float x, float y, float r, Rgba c) => Circle(BP(x, y), r * bS, Ba(c), 12);

        void BEll(float x, float y, float rx, float ry, float rot, Rgba c)
        {
            SetColor(Ba(c));
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            V2 mid = BP(x, y), prev = mid;
            for (int i = 0; i <= 14; i++)
            {
                float a = MathX.Tau * i / 14f, ex = rx * Mathf.Cos(a), ey = ry * Mathf.Sin(a);
                V2 q = BP(x + ex * cr - ey * sr, y + ex * sr + ey * cr);
                if (i > 0)
                    Tri(mid, prev, q);
                prev = q;
            }
        }

        void BEllO(float x, float y, float rx, float ry, Rgba fill, Rgba edge)
        {
            BEll(x, y, rx + 1.4f, ry + 1.4f, 0f, edge);
            BEll(x, y, rx, ry, 0f, fill);
        }

        void BLine(float x1, float y1, float x2, float y2, float w, Rgba c) => Line(BP(x1, y1), BP(x2, y2), w * bS, Ba(c));

        void BTri(Rgba c, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            SetColor(Ba(c));
            Tri(BP(x1, y1), BP(x2, y2), BP(x3, y3));
        }

        BearView BearViewOf(Minion m, float t)
        {
            BearView v;
            if (!bearViews.TryGetValue(m.Id, out v))
            {
                if (bearViews.Count > 16)
                {
                    var gone = new List<int>();
                    foreach (var k in bearViews.Keys)
                        if (!game.Minions.ContainsKey(k))
                            gone.Add(k);
                    foreach (var k in gone)
                        bearViews.Remove(k);
                }
                bearViews[m.Id] = v = new BearView { Last = m.Disp, LastT = t, Has = true };
            }
            float dt = t - v.LastT;
            if (dt > 0.0005f && dt < 0.25f)
            {
                float dist = m.Disp.DistanceTo(v.Last);
                v.Phase += dist / 13.3f;
                v.Run += ((dist / dt > 40f ? 1f : 0f) - v.Run) * Mathf.Min(1f, dt * 12f);
                // Biting: next to an enemy, its jaw snaps every 0.6 s (the bite's own interval), and again each time that
                // enemy is hurt, which keeps it close to the host's real bites.
                Enemy near = null;
                float nd = 14f;
                foreach (var e in game.Enemies.Values)
                {
                    if (e.Spawning)
                        continue;
                    float d = e.Disp.DistanceTo(m.Disp) - e.Radius - m.Def.Radius * m.Scale;
                    if (d < nd)
                    {
                        nd = d;
                        near = e;
                    }
                }
                if (near != null)
                {
                    if (!v.Biting)
                    {
                        v.Biting = true;
                        v.Cycle = 0.4f;
                        v.LastFlash = near.Flash;
                    }
                    v.Cycle += dt;
                    if (near.Flash > v.LastFlash + 0.02f)
                        v.Cycle = 0f;   // it was just hurt: bite now
                    v.LastFlash = near.Flash;
                    if (v.Cycle >= 0.6f)
                        v.Cycle -= 0.6f;
                }
                else
                    v.Biting = false;
                v.Last = m.Disp;
                v.LastT = t;
            }
            else if (dt >= 0.25f || dt < 0f)
            {
                v.Last = m.Disp;
                v.LastT = t;
            }
            return v;
        }

        /// The Bear companion (a brown bear from above), instead of the minion polygon.
        void DrawBear(Minion m, float t)
        {
            var v = BearViewOf(m, t);
            bAlpha = m.T >= 0f && m.T < 1.5f && MathX.Fmod(t, 0.2f) < 0.1f ? 0.4f : 1f;
            bS = m.Scale;
            bO = m.Disp;
            bDir = V2.FromAngle(m.Aim);
            bSide = new V2(-bDir.Y, bDir.X);
            float st = Mathf.Sin(v.Phase), run = v.Run, br = 1f + 0.025f * Mathf.Sin(t * 3f);
            float jaw = 0f, lunge = 0f;
            if (v.Biting)
            {
                float age = v.Cycle >= 0.44f ? v.Cycle - 0.6f : v.Cycle;   // the bite lands at 0; the jaw opens in the 0.16 s before it
                if (age >= -0.16f && age < 0f)
                    jaw = (age + 0.16f) / 0.16f;
                else if (age >= 0f && age < 0.14f)
                    jaw = 1f - age / 0.14f;
                if (age >= -0.12f && age < 0.22f)
                    lunge = 10f * Mathf.Sin(MathX.Pi * Mathf.Clamp01((age + 0.12f) / 0.34f));
            }
            BEll(0f, 3f, 17f, 13f, 0f, new Rgba(0f, 0f, 0f, 0.25f));
            float[] pawX = { 9f, 9f, -9f, -9f }, pawY = { -10f, 10f, -10f, 10f }, phase = { 1f, -1f, -1f, 1f };
            for (int i = 0; i < 4; i++)
            {
                float ox = st * 5f * phase[i] * run, px = pawX[i] + ox, py = pawY[i];
                BEll(px, py, 6.6f, 5f, 0f, BearPawEdge); BEll(px, py, 5.2f, 3.6f, 0f, BearPaw);
                for (int c = -1; c <= 1; c++)
                    BLine(px + 4f, py + c * 1.6f, px + 7f, py + c * 2f, 1f, BearIvory);
            }
            BEllO(-1f, 0f, 16f * br, 12.5f * br, BearBodyC, BearFurDark);
            BEll(-4f, 0f, 10f, 6f, 0f, BearBack); BEll(5f, 0f, 8f, 9.5f, 0f, BearHump);
            BCirc(-16f, 0f, 2.6f, BearBack);
            float hx = 14f + lunge;
            BEllO(hx - 4f, -6.8f, 3.4f, 3.4f, BearBodyC, BearFurDark); BEllO(hx - 4f, 6.8f, 3.4f, 3.4f, BearBodyC, BearFurDark);
            BCirc(hx, 0f, 9.8f, BearFurDark); BCirc(hx, 0f, 8.4f, BearHead);
            BEllO(hx + 8f, 0f, 5.8f, 4.2f, BearSnout, BearFurDark);
            if (jaw > 0.05f)
            {
                BEll(hx + 9f, 0f, 5.4f * jaw + 0.5f, 3.8f * jaw, 0f, BearMouth);
                for (int s = -1; s <= 1; s += 2)
                {
                    BTri(BearTooth, hx + 12f, s * 3.2f * jaw, hx + 15f, s * 2.2f * jaw, hx + 12.5f, s * 1.2f * jaw);
                    BTri(BearTooth, hx + 8f, s * 3.6f * jaw, hx + 10.5f, s * 2.6f * jaw, hx + 8.4f, s * 1.6f * jaw);
                }
            }
            BCirc(hx + 13.6f, 0f, 1.9f, BearNose);
            Rgba eye = jaw > 0.5f ? new Rgba(1f, 0.35f, 0.29f) : Hx(0x14100a);
            BCirc(hx + 3f, -3.8f, 1.3f, eye); BCirc(hx + 3f, 3.8f, 1.3f, eye);
            bAlpha = 1f;
            if (m.T >= 0f && m.Duration > 0f)
                Arc(m.Disp, m.Def.Radius * m.Scale + 5f, 2f, m.Def.Color.WithAlpha(0.5f), 24, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
        }

        // --- Bleeding --------------------------------------------------------------------------------------------------

        /// Claw scars on an enemy a Bear Totem's claws have cut: one set of three red slashes per two stacked hits.
        void DrawBleedScars(Enemy e, V2 pos, float r, float t)
        {
            int sets = Math.Min(3, (e.BleedHits + 1) / 2);
            for (int s = 0; s < sets; s++)
            {
                float ang = -0.45f + s * 0.4f;
                V2 along = V2.FromAngle(ang), across = along.Orthogonal();
                for (int i = -1; i <= 1; i++)
                {
                    V2 mid = pos + across * (i * 5f * Mathf.Max(0.6f, r / 16f) + (s - 1) * 1.2f);
                    Line(mid - along * (r * 0.85f), mid + along * (r * 0.85f), 2f, new Rgba(0.47f, 0.04f, 0.08f, 0.85f));
                    Line(mid - along * (r * 0.75f), mid + along * (r * 0.8f), 1f, new Rgba(0.92f, 0.2f, 0.24f, 0.9f));
                }
            }
            int drips = Math.Min(4, e.BleedHits);
            for (int i = 0; i < drips; i++)                // drips running down it
            {
                float ph = MathX.Fmod(t * 0.8f + i * 0.37f + e.Id * 0.13f, 1f), dx = (i - 1.5f) * 6.5f * Mathf.Max(0.6f, r / 16f), dy = r * 0.4f + ph * 22f;
                Line(pos + new V2(dx, r * 0.4f), pos + new V2(dx, dy), 1.5f, new Rgba(0.8f, 0.05f, 0.12f, 0.6f * (1f - ph)));
                Circle(pos + new V2(dx, dy), 2f, new Rgba(0.87f, 0.12f, 0.17f, 0.9f * (1f - ph)), 6);
            }
        }
    }
}
