using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Mender's Bow's Focus bar, Focus arrow, Scatter and its new held look (Core/BowFocus.cs; Docs/Mockups/menders-bow-rework.html).
    ///
    /// Always (it's how you play): the Focus bar beside you at full draw, a gold zone and a marker sweeping up and down
    /// it; Scatter's cooldown as a teal arc under you; the gold Focus arrow wrapped in two wind ribbons and the small teal
    /// Scatter arrows.
    /// With Visual effects on: the bow drawn 30% bigger (dark outline, gold tips, a green grip wrap) glowing gold while
    /// the bar is up; after every shot the hand pulls a fresh arrow from the quiver on your back and swings it over your
    /// shoulder onto the string; the bar pops in, bursts into shards on a Focus release (or shakes and fades on a miss);
    /// a Focus release flashes a gold ring and three wind crescents at the bow, says FOCUS!, and holds and shakes your
    /// own screen; the Focus arrow sheds wind streaks, bursts gold on each enemy it goes through and ends in a gust;
    /// Scatter snaps a fan of light, and its arrows that hit nothing stick in the floor a moment.
    public partial class ArenaApp
    {
        const byte PBowCrescent = 20, PBowFan = 21, PBowWind = 22, PBowGust = 23, PBowStick = 24, PBowImpact = 25;  // particle kinds (Juice.cs draws them through DrawBowFocusPart)
        public const float BowScale = 1.3f;   // the held bow, against the old model
        const float BowSwing = 0.25f;          // a fresh arrow from the quiver to the string (the bow's recovery)
        const float BarW = 9f, BarH = 64f, BarBack = 40f, BarPop = 0.15f, BarFxLife = 0.6f;

        static readonly Rgba BowWind = new Rgba(0.86f, 0.96f, 1f), ScatterTeal = new Rgba(0.5f, 0.9f, 0.85f), ScatterShaft = new Rgba(0.81f, 0.93f, 0.91f);
        static readonly Rgba FocusShaft = new Rgba(1f, 0.95f, 0.77f), BowGoldHot = new Rgba(1f, 0.89f, 0.48f), BowLeaf = new Rgba(0.35f, 1f, 0.55f);

        int barSeen;
        float barFxT = 99f, barFxM, barFxZ;
        bool barFxOk;
        struct FocusCall { public V2 Pos; public float T; }
        readonly List<FocusCall> focusCalls = new List<FocusCall>();
        readonly Dictionary<int, HashSet<int>> focusHit = new Dictionary<int, HashSet<int>>();
        readonly List<int> focusGone = new List<int>();
        int scatterFrame = -1, scatterOwner;

        void ClearBowFocusLook()
        {
            barFxT = 99f;
            focusCalls.Clear();
            focusHit.Clear();
            barSeen = game != null ? game.BowReleases : 0;
        }

        static float BarEaseBack(float x)
        {
            x = Mathf.Clamp01(x);
            const float c1 = 1.9f, c3 = c1 + 1f;
            return 1f + c3 * (x - 1f) * (x - 1f) * (x - 1f) + c1 * (x - 1f) * (x - 1f);
        }

        // --- Each frame (Juice.cs) ---------------------------------------------------------------------------------

        void UpdateBowFocusLooks(float dt)
        {
            barFxT += dt;
            if (game.BowReleases != barSeen)  // your own release: the bar bursts (Focus) or shakes away (missed)
            {
                barSeen = game.BowReleases;
                if (game.BowLastBar)
                {
                    barFxT = 0f; barFxOk = game.BowLastFocus; barFxM = game.BowLastMarker; barFxZ = game.BowZone;
                }
            }
            for (int i = focusCalls.Count - 1; i >= 0; i--)
            {
                var f = focusCalls[i];
                f.T += dt;
                if (f.T >= 0.9f) focusCalls.RemoveAt(i); else focusCalls[i] = f;
            }
            // Focus arrows: wind streaks peel off, and each enemy they go through bursts gold once.
            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind != Defs.FocusArrow)
                    continue;
                V2 dir = b.Vel.Normalized(), side = dir.Orthogonal();
                for (int n = Mathf.Max(0, Mathf.RoundToInt(Rnd(0.2f, 1.2f) * 54f * dt)); n > 0; n--)
                {
                    float life = Rnd(0.25f, 0.4f);
                    AddPart(new Part { Kind = PBowWind, Pos = b.Pos + side * Rnd(-25f, 25f), Vel = dir * 260f, Rot = b.Vel.Angle, Life = life, Max = life, Col = BowWind });
                }
                HashSet<int> hit;
                if (!focusHit.TryGetValue(b.Id, out hit))
                    focusHit[b.Id] = hit = new HashSet<int>();
                foreach (var e in game.Enemies.Values)
                    if (!hit.Contains(e.Id) && e.Disp.CloserThan(b.Pos, e.Radius + b.Radius + 4f))
                    {
                        if (hit.Count == 0 && b.Owner == game.MyId)
                            FocusHitHold();  // your own Focus arrow's first hit holds a frame or two (ChargeCameraFx.cs)
                        hit.Add(e.Id);
                        AddPart(new Part { Kind = PBowImpact, Pos = e.Disp - dir * 12f, Life = 0.35f, Max = 0.35f, Size = 16f, Col = BowGoldHot });
                    }
            }
            focusGone.Clear();
            foreach (var id in focusHit.Keys)
                if (!game.Bullets.ContainsKey(id))
                    focusGone.Add(id);
            foreach (var id in focusGone)
                focusHit.Remove(id);
        }

        /// A new Focus arrow at the bow (Juice.cs SeeShot): the gold ring and wind crescents, FOCUS!, and for your own a hold and a shake.
        void OnFocusArrow(Player owner, Bullet b)
        {
            BowSeen s;
            if (!bowSeen.TryGetValue(owner.Id, out s))
                bowSeen[owner.Id] = s = new BowSeen();
            s.Focus = true;
            float aim = b.Vel.Angle;
            for (int i = 0; i < 3; i++)
            {
                float life = 0.35f + i * 0.08f;
                AddPart(new Part { Kind = PBowCrescent, Pos = b.Pos, Rot = aim, Life = life, Max = life, A0 = i * 0.09f, Col = BowWind });
            }
            Ring(b.Pos, 52f, 0.4f, BowGold);
            Sparks(b.Pos, BowGoldHot, 8, 260f, 0.3f, 2.2f);
            focusCalls.Add(new FocusCall { Pos = owner.Disp + new V2(0f, -owner.Def.BodyR - 20f), T = 0f });
            if (owner.Id == game.MyId)
            {
                Hold(0.07f, 0.3f);
                Shake(0.35f);
            }
        }

        /// A Scatter arrow at the bow: the fan of light (once a volley), and a fresh arrow swung onto the string.
        void OnScatterArrow(Player owner, Bullet b)
        {
            if (scatterFrame == juiceFrame && scatterOwner == owner.Id)
                return;
            scatterFrame = juiceFrame; scatterOwner = owner.Id;
            BowSeen s;
            if (!bowSeen.TryGetValue(owner.Id, out s))
                bowSeen[owner.Id] = s = new BowSeen();
            s.SnapT = juiceClock; s.Focus = false;
            float aim = (b.Pos - owner.Pos).Angle;
            AddPart(new Part { Kind = PBowFan, Pos = owner.Disp, Rot = aim, Life = 0.3f, Max = 0.3f, Col = ScatterTeal });
        }

        /// A Focus or Scatter arrow is gone (Juice.cs ShotsGone): a gust where a Focus arrow ends, a Scatter arrow that missed sticks in the floor.
        void OnBowArrowGone(ShotSeen s)
        {
            if (s.Kind == Defs.FocusArrow)
                AddPart(new Part { Kind = PBowGust, Pos = s.Pos, Rot = s.Vel.Angle, Life = 0.4f, Max = 0.4f, Col = BowWind });
            else if (s.Kind == Defs.ScatterArrow && WhyGone(s.Pos, s.Vel, s.Life, s.Radius, s.Owner) == Ending.RanOut)
                AddPart(new Part { Kind = PBowStick, Pos = s.Pos, Rot = s.Vel.Angle, Life = 0.7f, Max = 0.7f, Col = ScatterTeal });
        }

        // --- The arrows (WorldShots.cs) ------------------------------------------------------------------------------

        /// An arrow in the world: shaft from `tail` along `dir`, `len` long at `sc`, its head the last 5, two pairs of fletching at the tail.
        void ArrowShape(V2 tail, V2 dir, float sc, float len, Rgba shaft, Rgba head, float lw)
        {
            V2 side = dir.Orthogonal();
            V2 neck = tail + dir * ((len - 5f) * sc);
            Line(tail, neck, lw * sc, shaft);
            SetColor(head);
            Tri(tail + dir * (len * sc), neck + side * (2f * sc), neck - side * (2f * sc));
            var fl = new Rgba(0.91f, 0.94f, 0.91f);
            Line(tail, tail + (-dir * 3f + side * 2.2f) * sc, sc, fl); Line(tail, tail + (-dir * 3f - side * 2.2f) * sc, sc, fl);
            V2 t2 = tail + dir * (2f * sc);
            Line(t2, t2 + (-dir * 3f + side * 2.2f) * sc, sc, fl); Line(t2, t2 + (-dir * 3f - side * 2.2f) * sc, sc, fl);
        }

        /// A Focus arrow: a gold streak with a glow, two wind ribbons twisting round its path, and the gold arrow at its head.
        void DrawFocusArrow(Bullet b, float t)
        {
            V2 dir = b.Vel.Normalized(), side = dir.Orthogonal();
            float speed = b.Vel.Length, flown = Mathf.Max(0f, (b.Life0 - b.Life) * speed);
            float step = speed / 60f;
            // The ribbons: the path as it was flown, each point pushed out to the side on a turning sine.
            for (int r = 0; r < 2; r++)
            {
                float ph = r * Mathf.PI;
                V2 prev = b.Pos;
                for (int i = 1; i < 16 && step * i <= flown + step; i++)
                {
                    float amp = 4f + i * 1.3f, off = Mathf.Sin(i * 0.8f - t * 40f + ph) * amp;
                    V2 at = b.Pos - dir * (step * i + i * 2f) + side * off;
                    Line(prev, at, 1.6f, BowWind.WithAlpha(0.55f));
                    prev = at;
                }
            }
            float len = Mathf.Min(110f, flown);
            Line(b.Pos - dir * len, b.Pos, 12f, BowGold.WithAlpha(0.12f));  // the glow
            for (int i = 0; i < 6; i++)
                Line(b.Pos - dir * (len * (i + 1) / 6f), b.Pos - dir * (len * i / 6f), 5f, BowGoldHot.WithAlpha(0.9f * (1f - (i + 0.5f) / 6f)));
            Circle(b.Pos, 9f, BowGold.WithAlpha(0.25f), 12);
            ArrowShape(b.Pos - dir * 30f, dir, 1.25f, 26f, FocusShaft, BowGold, 2f);
        }

        /// A Scatter arrow: small and teal, with a short streak.
        void DrawScatterArrow(Bullet b)
        {
            V2 dir = b.Vel.Normalized();
            float len = Mathf.Min(26f, Mathf.Max(0f, (b.Life0 - b.Life) * b.Vel.Length));
            Line(b.Pos - dir * len, b.Pos, 1.6f, ScatterTeal.WithAlpha(0.35f));
            ArrowShape(b.Pos - dir * 18f, dir, 0.7f, 26f, ScatterShaft, ScatterTeal, 1.4f);
        }

        // --- The Focus bar and Scatter's cooldown (WorldRenderer.cs, always) -------------------------------------------

        void DrawBowFocusUi(float t)
        {
            var me = game.Me;
            if (me == null || me.Weapon == null || me.Weapon.Id != WeaponId.MendersBow || me.Downed)
                return;
            float cd = game.ScatterCooldown;
            if (cd > 0f)
                Arc(me.Disp, me.Def.BodyR + 7f, 1.6f, ScatterTeal.WithAlpha(0.75f), 24, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(cd / Game.ScatterCd));
            bool on = me.DrawT > 0f && game.BowFocusBar(me, me.DrawT);
            float pop = 1f, m, z;
            int state = 0;  // 0 live, 1 burst (Focus), 2 missed
            if (on)
            {
                float since = me.DrawT - game.BowFull(me);
                pop = Juicy ? BarEaseBack(since / BarPop) : 1f;
                m = Game.BowMarker(since); z = game.BowZone;
            }
            else if (Juicy && barFxT < BarFxLife)
            {
                m = barFxM; z = barFxZ; state = barFxOk ? 1 : 2;
            }
            else
                return;
            V2 aim = V2.FromAngle(me.Aim);
            V2 at = me.Disp - new V2(aim.X * BarBack, aim.Y * 10f);
            float s = pop, alpha = 1f;
            if (state == 2) { at += new V2(Mathf.Sin(barFxT * 90f) * 3f * (1f - barFxT / BarFxLife), 0f); alpha = 1f - barFxT / BarFxLife; }
            if (state == 1) { float k = Mathf.Clamp01(barFxT / 0.35f); s = 1f + 0.6f * EaseOut3(k); alpha = 1f - k; }
            if (alpha > 0.01f && s > 0.01f)
            {
                System.Func<float, float, V2> P = (x, y) => at + new V2(x, y) * s;
                System.Func<float, float> Y = v => BarH / 2f - v * BarH;  // 0 bottom .. 1 top
                float hw = BarW / 2f, hh = BarH / 2f;
                SetColor(new Rgba(0.043f, 0.059f, 0.075f, 0.92f * alpha));
                Quad(P(-hw - 2f, -hh - 2f), P(hw + 2f, -hh - 2f), P(hw + 2f, hh + 2f), P(-hw - 2f, hh + 2f));
                var edge = (state == 2 ? new Rgba(0.88f, 0.33f, 0.37f) : BowGold).WithAlpha(0.7f * alpha);
                Line(P(-hw - 2f, -hh - 2f), P(hw + 2f, -hh - 2f), s, edge); Line(P(hw + 2f, -hh - 2f), P(hw + 2f, hh + 2f), s, edge);
                Line(P(hw + 2f, hh + 2f), P(-hw - 2f, hh + 2f), s, edge); Line(P(-hw - 2f, hh + 2f), P(-hw - 2f, -hh - 2f), s, edge);
                for (int i = 1; i < 8; i++)
                    Line(P(-hw, -hh + i * BarH / 8f), P(-hw + 2f, -hh + i * BarH / 8f), 0.5f * s, new Rgba(1f, 1f, 1f, 0.18f * alpha));
                bool hot = state == 0 && m >= z && m <= z + Game.BowFocusZone;
                float y0 = Y(z + Game.BowFocusZone), y1 = Y(z);
                if (hot)
                {
                    SetColor(BowGold.WithAlpha(0.3f * alpha));
                    Quad(P(-hw - 4f, y0 - 4f), P(hw + 4f, y0 - 4f), P(hw + 4f, y1 + 4f), P(-hw - 4f, y1 + 4f));
                }
                SetColor((state == 2 ? new Rgba(0.55f, 0.55f, 0.55f, 0.5f) : hot ? BowGoldHot : BowGold.WithAlpha(0.55f)).WithAlpha((state == 2 ? 0.5f : hot ? 1f : 0.55f) * alpha));
                Quad(P(-hw, y0), P(hw, y0), P(hw, y1), P(-hw, y1));
                float my = Y(m);
                var mc = (state == 1 ? Rgba.White : state == 2 ? new Rgba(0.88f, 0.33f, 0.37f) : hot ? Rgba.White : new Rgba(0.87f, 0.91f, 0.94f)).WithAlpha(alpha);
                Line(P(-hw - 3f, my), P(hw + 3f, my), 1.6f * s, mc);
                SetColor(mc);
                Tri(P(-hw - 6f, my - 3f), P(-hw - 2f, my), P(-hw - 6f, my + 3f));
                Tri(P(hw + 6f, my - 3f), P(hw + 2f, my), P(hw + 6f, my + 3f));
            }
            if (state == 1 && barFxT < 0.4f)  // the shards
            {
                float k = barFxT / 0.4f;
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * MathX.Tau + 0.3f, r = 6f + 40f * EaseOut3(k);
                    Star4(at + new V2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 1.4f), 3f * (1f - k), BowGoldHot.WithAlpha(0.9f));
                }
            }
        }

        /// The HUD pass (Juice.cs, with the damage numbers): FOCUS! over whoever let a Focus shot go.
        void DrawBowFocusHud()
        {
            foreach (var f in focusCalls)
            {
                float a = 1f - Mathf.Clamp01((f.T - 0.45f) / 0.45f);
                V2 at = ToUi(f.Pos + new V2(0f, -24f * EaseOut3(f.T / 0.9f)));
                int size = f.T < 0.12f ? 22 : 18;
                Ui.Shadowed(new Rect(at.X - 70f, at.Y - 13f, 140f, 26f), "FOCUS!", size, new Color(BowGold.R, BowGold.G, BowGold.B, a));
            }
        }

        // --- The held bow, the quiver and the arrow swung from it (CharacterModels.cs) ---------------------------------

        /// The quiver on the back, in the body frame: three arrows' fletching out of its mouth.
        void DrawBowQuiver(Player p)
        {
            V2 c = new V2(-16.7f, -2.6f);
            float ca = Mathf.Cos(-0.45f), sa = Mathf.Sin(-0.45f);
            System.Func<float, float, V2> Q = (x, y) => c + new V2(x * ca - y * sa, x * sa + y * ca) * 1.286f;
            System.Action<Rgba, float, float, float, float> R = (col, x, y, w, h) =>
            {
                V2 a = Q(x, y), b = Q(x + w, y), d = Q(x + w, y + h), e = Q(x, y + h);
                MPoly(col, a.X, a.Y, b.X, b.Y, d.X, d.Y, e.X, e.Y);
            };
            foreach (float x in new[] { -2f, 0.2f, 2.2f })
            {
                V2 a = Q(x, -10f), b = Q(x - 0.4f, -14f), g = Q(x + 0.6f, -13.5f);
                MLine(a.X, a.Y, b.X, b.Y, 1.8f, MBone);
                MLine(a.X, a.Y, g.X, g.Y, 0.9f, BowLeaf.WithAlpha(0.8f));
            }
            R(Hx(0x4a3626), -3.6f, -10f, 7.2f, 19f);
            R(Hx(0x2a1d12), -3.6f, -10f, 7.2f, 2f);
            R(Hx(0x8a6a42), -3.6f, 5f, 7.2f, 1.5f);
        }

        /// How far back the string (and the hand on it) is pulled, in the bow's units: 3 at rest, -6 at full draw.
        float BowNock(Player p)
        {
            float c = p.DrawT > 0f ? game.BowCharge(p, p.DrawT) : 0f;
            float k = p.DrawT > 0f ? 0.15f + 0.85f * c : 0.1f;
            return 4f - 10f * k;
        }

        float BowSince(Player p)
        {
            BowSeen s;
            return bowSeen.TryGetValue(p.Id, out s) ? juiceClock - s.SnapT : 99f;
        }

        /// The arrow's tail on its swing from the quiver to the string (u 0..1), in the body frame, and its heading.
        static V2 SwingTail(float u, V2 nock, out float ang)
        {
            V2 p0 = new V2(-18f, -15.4f), p1 = new V2(-7.7f, -43.7f);
            float a = (1f - u) * (1f - u), b = 2f * u * (1f - u), d = u * u;
            ang = Mathf.Lerp(-2.2f, 0f, EaseOut3(u));
            return p0 * a + p1 * b + nock * d;
        }

        /// With Visual effects on, the hands go where the bow wants them: the right one on the string (or pulling a fresh arrow
        /// from the quiver), the left on the grip. `ox, oy`: where the bow's frame sits (the old right-hand spot).
        bool BowHandPose(Player p, float wk, ref float rhx, ref float rhy, ref float lhx, ref float lhy, out float ox, out float oy)
        {
            ox = rhx; oy = rhy;
            if (!Juicy || p.Downed)
                return false;
            float sc = wk * BowScale, c = p.DrawT > 0f ? game.BowCharge(p, p.DrawT) : 0f;
            V2 nock = new V2(ox + 3f * sc, oy);
            float since = BowSince(p);
            if (since >= 0f && since < BowSwing)
            {
                float ang;
                V2 tail = SwingTail(EaseInOut3(since / BowSwing), nock, out ang);
                rhx = tail.X; rhy = tail.Y;
            }
            else
                rhx = ox + BowNock(p) * sc;
            lhx = ox + (18f - 2f * c) * sc; lhy = oy;
            return true;
        }

        /// The fresh arrow on its swing (in the body frame, after the bow; `ox, oy`: the bow's frame): it and two faint ghosts behind it.
        void DrawBowSwing(Player p, float wk, float ox, float oy)
        {
            float since = BowSince(p);
            if (!Juicy || p.Downed || since < 0f || since >= BowSwing)
                return;
            BowSeen s;
            bool gold = bowSeen.TryGetValue(p.Id, out s) && s.Focus;
            float sc = wk * BowScale, u = EaseInOut3(since / BowSwing);
            V2 nock = new V2(ox + 3f * sc, oy);
            for (int g = 2; g >= 0; g--)
            {
                float uu = Mathf.Max(0f, u - g * 0.1f), ang;
                V2 tail = SwingTail(uu, nock, out ang);
                float keep = mAlpha;
                mAlpha *= g == 0 ? 1f : 0.18f / g;
                HeldArrow(tail.X, tail.Y, ang, sc, 28f, gold ? FocusShaft : Hx(0xcfe9d6), gold ? BowGold : BowLeaf);
                mAlpha = keep;
            }
        }

        /// An arrow in the body frame: its tail at (x,y), heading `ang`, `len` long at scale `sc`.
        void HeldArrow(float x, float y, float ang, float sc, float len, Rgba shaft, Rgba head)
        {
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            System.Func<float, float, V2> A = (ax, ay) => new V2(x + (ax * ca - ay * sa) * sc, y + (ax * sa + ay * ca) * sc);
            V2 t0 = A(0f, 0f), n = A(len - 5f, 0f), h = A(len, 0f), h1 = A(len - 5f, -2f), h2 = A(len - 5f, 2f);
            MLine(t0.X, t0.Y, n.X, n.Y, 1.4f * sc, shaft);
            MPoly(head, h.X, h.Y, h1.X, h1.Y, h2.X, h2.Y);
            foreach (float off in new[] { 0f, 2f })
            {
                V2 a = A(off, 0f), b = A(off - 3f, -2.2f), d = A(off - 3f, 2.2f);
                MLine(a.X, a.Y, b.X, b.Y, sc, MBone); MLine(a.X, a.Y, d.X, d.Y, sc, MBone);
            }
        }

        /// The held bow with the effects on (the hand's frame, scaled up by BowScale): a dark-outlined limb that flexes with
        /// the draw, gold tips, a green grip wrap, the string, and the arrow on it once the swing has brought it there. While
        /// the Focus bar is up the bow glows gold and trembles.
        void DrawBowFlex(Player p)
        {
            float keepX = mOx, keepY = mOy, keepS = mWs;
            bool bar = game.BowFocusBar(p, p.DrawT) && p.DrawT > 0f;
            float gold = bar ? Mathf.Min(1f, (p.DrawT - game.BowFull(p)) / 0.15f) * (0.7f + 0.3f * Mathf.Sin(mT * 18f)) : 0f;
            MFrame(mOx, mOy + (bar ? Mathf.Sin(mT * 55f) * 0.35f : 0f), mAng, mWs * BowScale, mMir);
            bool drawing = p.DrawT > 0f;
            float c = drawing ? game.BowCharge(p, p.DrawT) : 0f;
            float since = BowSince(p);
            float wobble = since > 0f && since < 0.5f ? Mathf.Sin(since * 70f) * 4f * Mathf.Exp(-since * 9f) : 0f;
            float tipX = 9.9f - 5f * c, tipY = 13.9f - 1.8f * c, ctrlX = 2f * (18f - 2f * c) - tipX;
            System.Action<float, Rgba> Limb = (w, col) =>
            {
                float px = tipX, py = -tipY;
                for (int i = 1; i <= 10; i++)  // one curve from tip to tip, bulging forward
                {
                    float u = i / 10f, a = (1f - u) * (1f - u), b = 2f * u * (1f - u), d = u * u;
                    float x = a * tipX + b * ctrlX + d * tipX, y = a * -tipY + d * tipY;
                    MLine(px, py, x, y, w, col);
                    px = x; py = y;
                }
            };
            if (gold > 0f)
                Limb(8f, BowGold.WithAlpha(0.3f * gold));
            Limb(4.2f, Hx(0x2a1d12));
            Limb(2.8f, Hx(0x8a6a42));
            Limb(0.8f, new Rgba(0.84f, 0.7f, 0.47f, 0.6f));
            MCirc(tipX, -tipY, 1.5f, BowGold, 8); MCirc(tipX, tipY, 1.5f, BowGold, 8);
            float gx = 18f - 2f * c - 1.6f;
            MRect(gx - 1.6f, -3.4f, 3.4f, 6.8f, Hx(0x2f6b45));
            MLine(gx - 1.6f, -1.5f, gx + 1.8f, -2.4f, 0.6f, BowLeaf); MLine(gx - 1.6f, 1.2f, gx + 1.8f, 0.3f, 0.6f, BowLeaf);
            float nock = BowNock(p) + wobble;
            var str = gold > 0f ? new Rgba(1f, (235f - 40f * gold) / 255f, (200f - 110f * gold) / 255f) : Hx(0xeaeaea);
            MLine(tipX, -tipY, nock, 0f, 0.8f, str); MLine(nock, 0f, tipX, tipY, 0.8f, str);
            if (since >= BowSwing)  // on the string once the swing has brought it there
            {
                bool hotArrow = gold > 0.3f;
                float len = 31f - nock;
                MLine(nock, 0f, nock + len - 5f, 0f, 1.4f, hotArrow ? FocusShaft : Hx(0xcfe9d6));
                MPoly(hotArrow ? BowGold : BowLeaf, nock + len, 0f, nock + len - 5f, -2f, nock + len - 5f, 2f);
                foreach (float off in new[] { 0f, 2f })
                {
                    MLine(nock + off, 0f, nock + off - 3f, -2.2f, 1f, MBone); MLine(nock + off, 0f, nock + off - 3f, 2.2f, 1f, MBone);
                }
                if (c > 0.7f)
                {
                    float g = (c - 0.7f) / 0.3f;
                    MStar(31f, 0f, 3f + 5f * g, 0.9f + 0.6f * g, 4, 0f, new Rgba(1f, 1f, 1f, 0.4f + 0.6f * g));
                }
            }
            MFrame(keepX, keepY, mAng, keepS, mMir);
        }

        // --- The particles (Juice.cs hands them here) ------------------------------------------------------------------

        void DrawBowFocusPart(Part p, float f)
        {
            float k = 1f - f;  // 0 when made, 1 when gone
            switch (p.Kind)
            {
                case PBowCrescent:  // a wind crescent flying out of the bow (each a little later than the one before)
                {
                    float el = p.Max - p.Life, tt = Mathf.Clamp01((el - p.A0) / Mathf.Max(0.01f, p.Max - p.A0));
                    if (tt <= 0f)
                        break;
                    float r = 10f + 46f * EaseOut3(tt);
                    V2 c = p.Pos + V2.FromAngle(p.Rot) * (r * 0.6f - 4f);
                    Arc(c, r * 0.5f, 2.2f * (1f - tt) + 0.4f, p.Col.WithAlpha(0.85f * (1f - tt)), 14, p.Rot - 1.1f, 2.2f);
                    break;
                }
                case PBowFan:  // Scatter's fan: five short arcs flying out along the arrows, and a white sweep
                {
                    float e = EaseOut3(k);
                    for (int i = -2; i <= 2; i++)
                    {
                        float a = p.Rot + i * Game.ScatterFan / 4f;
                        Arc(p.Pos, 20f + 40f * e, 2f, p.Col.WithAlpha(f), 6, a - 0.06f, 0.12f);
                    }
                    Arc(p.Pos, 18f + 30f * e, 1.2f, Rgba.White.WithAlpha(0.5f * f), 12, p.Rot - 0.45f, 0.9f);
                    break;
                }
                case PBowWind:  // a streak of wind peeling off a Focus arrow
                    Line(p.Pos, p.Pos - V2.FromAngle(p.Rot) * (14f * f), 1f, p.Col.WithAlpha(0.6f * f));
                    break;
                case PBowGust:  // where a Focus arrow ends: three arcs of wind blowing on
                {
                    float e = EaseOut3(k);
                    for (int i = 0; i < 3; i++)
                        Arc(p.Pos, 8f + 30f * e + i * 5f, 1.4f, p.Col.WithAlpha(0.5f * f), 12, p.Rot - 0.9f, 1.8f);
                    break;
                }
                case PBowStick:  // a Scatter arrow that hit nothing, stuck in the floor
                {
                    V2 dir = V2.FromAngle(p.Rot);
                    ArrowShape(p.Pos - dir * 18f, dir, 0.7f, 26f, ScatterShaft.WithAlpha(f), p.Col.WithAlpha(f), 1.4f);
                    break;
                }
                case PBowImpact:  // a Focus arrow going through an enemy: a gold star and a ring
                    Star4(p.Pos, p.Size * (0.5f + k), p.Col.WithAlpha(f));
                    Arc(p.Pos, 6f + 26f * EaseOut3(k), 2f, BowGold.WithAlpha(f), 20);
                    break;
            }
        }
    }
}
