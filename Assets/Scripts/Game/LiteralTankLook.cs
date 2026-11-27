using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// A Literal Tank (Core/LiteralTank.cs), the "Olive steel" look of Docs/Mockups/a-literal-tank.html: an olive tank drawn in
    /// place of the player. It falls onto you over Game.TankDrop s (its shadow grows under you, a dust ring as it lands); the hull
    /// turns toward where it drives (eased here, from how it moves: it reverses rather than spinning round) and leaves tread
    /// marks; the turret follows your aim and kicks back as each shell leaves (Defs.TankShell, drawn as a fat glowing round with
    /// a smoke trail). The last 3 s the hull blinks red; when it ends a charred wreck is left a moment where it blew up.
    /// A steel armour bar with the time as a hairline under it sits over the health bar. Replicated state is Player.TankT,
    /// TankArmor and TankMax; the hull's heading, the marks, the recoil and the wreck are local. Visual effects off: no glow,
    /// sparks, smoke or marks.
    public partial class ArenaApp
    {
        class TankVis
        {
            public bool Up;
            public float OnAt = -9f, OffAt = -9f;
            public float Hull;                         // the hull's heading (radians), eased toward where it drives
            public V2 Last, WreckAt;
            public float WreckHull;
            public float MarkT;                        // until the next pair of tread marks
            public bool Landed;                        // the landing's crash played
            public float WarnAt;                       // the next low-time beep (your own tank)
            public float Rev;                          // the engine's revs (0 idle, 1 driving), eased
            public readonly List<TankMark> Marks = new List<TankMark>();
        }

        struct TankMark { public V2 At; public float Ang, Born; }

        readonly Dictionary<int, TankVis> tankVis = new Dictionary<int, TankVis>();
        const float TankHullTurn = 5f, TankMarkLife = 3.5f, TankMarkGap = 0.05f, TankWreckLife = 1.6f, TankFallPx = 420f, TankRecoil = 0.18f;
        static readonly Rgba TankHull = new Rgba(0.36f, 0.42f, 0.2f), TankHullDark = new Rgba(0.24f, 0.28f, 0.13f),
            TankHullLite = new Rgba(0.49f, 0.56f, 0.29f), TankTread = new Rgba(0.16f, 0.18f, 0.14f), TankTurret = new Rgba(0.4f, 0.48f, 0.23f),
            TankBarrel = new Rgba(0.28f, 0.33f, 0.16f), TankMarkCol = new Rgba(0.91f, 0.89f, 0.78f), TankWarn = new Rgba(0.82f, 0.29f, 0.23f),
            TankFlash = new Rgba(1f, 0.9f, 0.63f), TankBarFill = new Rgba(0.62f, 0.71f, 0.78f);

        static float TankWrap(float r) => Mathf.Repeat(r + MathX.Pi, MathX.Tau) - MathX.Pi;

        static float TankEase(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k); }

        TankVis TankState(Player p, float t)
        {
            bool on = Game.TankOn(p);
            if (!tankVis.TryGetValue(p.Id, out var v))
            {
                if (!on)
                    return null;
                tankVis[p.Id] = v = new TankVis { Up = true, OnAt = t - Game.TankSince(p), Hull = p.Aim, Last = p.Disp, Landed = Game.TankSince(p) >= Game.TankDrop };  // a late joiner sees it already down
            }
            if (on && !v.Up)
            {
                v.OnAt = t - Mathf.Clamp(Game.TankSince(p), 0f, 0.2f);
                v.Hull = p.Aim;
                v.Last = p.Disp;
                v.Landed = false;
                v.Rev = 0f;
            }
            else if (!on && v.Up)
            {
                v.OffAt = t;
                v.WreckAt = p.Disp;
                v.WreckHull = v.Hull;
                if (Juicy)
                    Sparks(p.Disp, Game.TankFire, 18, 260f, 0.5f, 2.6f);
                sounds?.Play(Sfx.LitTankHatch, p == game.Me ? 0.6f : 0.3f, Time.unscaledTime);  // you pop out (the wreck's boom is sent)
            }
            v.Up = on;
            float dt = Mathf.Max(0f, Time.deltaTime);
            if (on && dt > 0f)
            {
                V2 moved = p.Disp - v.Last;
                if (moved.Length > 18f * dt)  // driving: the hull turns toward it, or backs up if that's nearer
                {
                    float want = Mathf.Atan2(moved.Y, moved.X);
                    float d = TankWrap(want - v.Hull);
                    if (Mathf.Abs(d) > MathX.Pi * 0.6f)
                        d = TankWrap(want + MathX.Pi - v.Hull);
                    v.Hull += Mathf.Clamp(d, -TankHullTurn * dt, TankHullTurn * dt);
                    if (fxOn && (v.MarkT -= dt) <= 0f)
                    {
                        v.MarkT = TankMarkGap;
                        v.Marks.Add(new TankMark { At = p.Disp, Ang = v.Hull, Born = t });
                    }
                }
            }
            if (on)
                TankSounds(p, v, t, dt);
            v.Last = p.Disp;
            v.Marks.RemoveAll(m => t - m.Born > TankMarkLife);
            return v;
        }

        float tankEngineLevel, tankEnginePitch = 1f, tankEngineAt = -9f;  // the loudest tank's engine this frame (TickLitTankSound)
        const float TankRevSpeed = 150f;  // px/s: driving this fast is full revs

        /// The landing's crash, your own tank's low-time beeps, and the engine's level and pitch (LitTankSoundBank.cs).
        void TankSounds(Player p, TankVis v, float t, float dt)
        {
            bool mine = p == game.Me;
            float since = t - v.OnAt;
            if (!v.Landed && since >= Game.TankDrop)
            {
                v.Landed = true;
                sounds?.Play(Sfx.LitTankLand, mine ? 0.9f : 0.5f, Time.unscaledTime);
            }
            if (mine && p.TankT < 3f && t >= v.WarnAt)
            {
                v.WarnAt = t + 0.5f;
                sounds?.Play(Sfx.LitTankWarn, 0.45f, Time.unscaledTime);
            }
            if (!v.Landed)
                return;
            float speed = dt > 0f ? (p.Disp - v.Last).Length / dt : 0f;
            v.Rev = Mathf.MoveTowards(v.Rev, Mathf.Clamp01(speed / TankRevSpeed), dt * 2.5f);
            float level = (mine ? 1f : 0.5f) * (0.4f + 0.6f * v.Rev);
            if (Time.unscaledTime - tankEngineAt > 0.05f || level >= tankEngineLevel)
            {
                tankEngineLevel = level;
                tankEnginePitch = 0.82f + 0.35f * v.Rev;
                tankEngineAt = Time.unscaledTime;
            }
        }

        void TickLitTankSound()
        {
            if (sounds != null)
                sounds.SetLitTankEngine(Time.unscaledTime - tankEngineAt < 0.25f ? tankEngineLevel * 0.5f : 0f, tankEnginePitch);
        }

        /// From DrawPlayer, before the model: the tank (true: it is drawn in place of the model), or while it falls, the tank
        /// coming down over you (false: you are still drawn), or after, its wreck (false).
        bool DrawLiteralTank(Player p, float t, float alpha)
        {
            if (p.TankT <= 0f && !tankVis.ContainsKey(p.Id))
                return false;
            var v = TankState(p, t);
            if (v == null)
                return false;
            foreach (var m in v.Marks)  // the tread marks, fading
            {
                float a = 0.32f * (1f - (t - m.Born) / TankMarkLife) * alpha;
                V2.Axes(m.Ang, out V2 fw, out V2 side);
                TankBox(m.At + side * 30f, fw, side, 3f, 6f, new Rgba(0f, 0f, 0f, a));
                TankBox(m.At - side * 30f, fw, side, 3f, 6f, new Rgba(0f, 0f, 0f, a));
            }
            if (!v.Up)
            {
                float k = (t - v.OffAt) / TankWreckLife;
                if (k < 1f)  // the charred wreck, smoking, then gone
                {
                    V2.Axes(v.WreckHull, out V2 fw, out V2 side);
                    float a = alpha * (1f - k * k);
                    TankBox(v.WreckAt, fw, side, 50f, 30f, new Rgba(0.09f, 0.09f, 0.08f, 0.9f * a));
                    Circle(v.WreckAt, 18f, new Rgba(0.13f, 0.12f, 0.11f, a), 18);
                    if (fxOn)
                        GlowDisc(v.WreckAt + new V2(0f, -10f - 30f * k), 26f + 20f * k, new Rgba(0.35f, 0.33f, 0.3f), 0.35f * (1f - k));
                }
                else if (v.Marks.Count == 0)
                    tankVis.Remove(p.Id);
                return false;
            }
            float since = t - v.OnAt;
            if (since < Game.TankDrop)  // falling onto you: a growing shadow, the tank dropping out of the sky
            {
                float k = since / Game.TankDrop;
                GlowDisc(p.Disp, 30f + 34f * k, new Rgba(0f, 0f, 0f), 0.25f + 0.35f * k);
                DrawTankBody(p, v, p.Disp + new V2(0f, -TankFallPx * (1f - k) * (1f - k)), 1f + 0.35f * (1f - k), alpha, 0f, false);
                return false;
            }
            if (since < Game.TankDrop + 0.35f && fxOn)  // the dust as it lands
            {
                float k = (since - Game.TankDrop) / 0.35f;
                Arc(p.Disp, 50f + 80f * TankEase(k), 6f * (1f - k) + 1f, new Rgba(0.75f, 0.7f, 0.58f, 0.6f * (1f - k) * alpha), 36);
            }
            bool warn = p.TankT < 3f && Mathf.Repeat(t * 6f, 1f) < 0.5f;
            DrawTankBody(p, v, p.Disp, 1f, alpha, TankKick(p), warn);
            return true;
        }

        /// How far the barrel is kicked back (0-1): from the newest shell this player fired.
        float TankKick(Player p)
        {
            float best = 0f;
            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind != Defs.TankShell || b.Owner != p.Id)
                    continue;
                float age = b.Life0 - b.Life;
                if (age < TankRecoil)
                    best = Mathf.Max(best, 1f - age / TankRecoil);
            }
            return best;
        }

        /// A box of half-length hl along fw and half-width hw along side.
        void TankBox(V2 c, V2 fw, V2 side, float hl, float hw, Rgba col) =>
            Quad(c - fw * hl - side * hw, c + fw * hl - side * hw, c + fw * hl + side * hw, c - fw * hl + side * hw, col);

        static Rgba TankA(float alpha, Rgba c) => c.WithAlpha(c.A * alpha);

        void DrawTankBody(Player p, TankVis v, V2 at, float sc, float alpha, float kick, bool warn)
        {
            V2.Axes(v.Hull, out V2 fw, out V2 side);
            fw *= sc;
            side *= sc;
            TankBox(at + new V2(4f, 6f), fw, side, 52f, 40f, new Rgba(0f, 0f, 0f, 0.3f * alpha));  // the shadow
            // treads, with links rolling as it drives
            TankBox(at + side * 30f, fw, side, 52f, 10f, TankA(alpha, TankTread));
            TankBox(at - side * 30f, fw, side, 52f, 10f, TankA(alpha, TankTread));
            float roll = Mathf.Repeat((p.Disp.X * fw.X + p.Disp.Y * fw.Y) / sc, 12f);
            for (float x = -50f + roll; x < 50f; x += 12f)
            {
                TankBox(at + side * 30f + fw * x, fw, side, 2f, 10f, TankA(alpha, new Rgba(1f, 1f, 1f, 0.1f)));
                TankBox(at - side * 30f + fw * x, fw, side, 2f, 10f, TankA(alpha, new Rgba(1f, 1f, 1f, 0.1f)));
            }
            // the hull: body, darker skirt, lit top edge, front plate
            Rgba hull = warn ? TankWarn : TankHull;
            TankBox(at, fw, side, 46f, 26f, TankA(alpha, hull));
            TankBox(at + side * 20f, fw, side, 46f, 6f, TankA(alpha, warn ? TankWarn.Darkened(0.3f) : TankHullDark));
            TankBox(at - side * 21f, fw, side, 46f, 5f, TankA(alpha, warn ? TankWarn.Mix(Rgba.White, 0.25f) : TankHullLite));
            TankBox(at + fw * 37f, fw, side, 3f, 20f, TankA(alpha, TankHullDark));
            // the turret, on your aim, with the barrel kicked back
            V2.Axes(p.Aim, out V2 aim, out V2 aSide);
            aim *= sc;
            aSide *= sc;
            float back = kick * 10f;
            TankBox(at + aim * (40f - back), aim, aSide, 26f, 6f, TankA(alpha, TankBarrel));
            TankBox(at + aim * (64f - back), aim, aSide, 6f, 8f, TankA(alpha, TankHullDark));  // the muzzle brake
            Circle(at, 24f * sc, TankA(alpha, TankTurret), 24);
            Circle(at - (aim + aSide) * 4f, 15f * sc, TankA(alpha, TankHullLite), 20);
            Circle(at - aim * 6f + aSide * 6f, 7f * sc, TankA(alpha, TankHullDark), 14);  // the hatch
            Circle(at + aim * 8f - aSide * 2f, 4f * sc, TankA(alpha, TankMarkCol), 10);   // the star
            if (kick > 0.5f && fxOn)  // the muzzle flash
            {
                float f = (kick - 0.5f) * 2f;
                GlowDisc(at + aim * 72f, 26f * f + 6f, TankFlash, 0.9f * f * alpha);
                Circle(at + aim * 72f, 12f * f, TankFlash.WithAlpha(f * alpha), 12);
            }
        }

        /// The armour bar over the health bar, with the time left as a hairline under it.
        void DrawTankBar(Player p)
        {
            if (!Game.TankOn(p) || p.TankMax <= 0f)
                return;
            float frac = Mathf.Clamp01(p.TankArmor / p.TankMax);
            float w = 56f, h = 5f;
            V2 at = p.Disp + new V2(-w / 2f, OverheadRow(h + 3f));  // (and the time under it: OverheadStack.cs)
            FillRect(at.X - 1f, at.Y - 1f, w + 2f, h + 2f, new Rgba(0f, 0f, 0f, 0.65f));
            FillRect(at.X, at.Y, w * frac, h, TankBarFill);
            FillRect(at.X, at.Y, w * frac, 1f, new Rgba(1f, 1f, 1f, 0.5f));
            float left = Mathf.Clamp01(p.TankT / Game.TankTime);
            FillRect(at.X, at.Y + h + 1f, w * left, 2f, (p.TankT < 3f ? TankWarn : Game.TankFire).WithAlpha(0.85f));
        }

        /// A shell (Defs.TankShell): a fat brass-and-orange round with a glow and a grey smoke trail.
        void DrawTankShell(Bullet b, float t)
        {
            var c = Defs.BulletColor(Defs.TankShell);
            V2 dir = b.Vel.IsZero ? V2.FromAngle(0f) : b.Vel.Normalized();
            V2 side = new V2(-dir.Y, dir.X);
            if (fxOn)
            {
                TaperTrail(b, 8, 7f, new Rgba(0.75f, 0.74f, 0.7f), 0.45f);
                GlowDisc(b.Pos, 18f, c, 0.55f);
            }
            TankBox(b.Pos, dir, side, 10f, 5f, new Rgba(1f, 0.82f, 0.48f));
            TankBox(b.Pos - dir * 10f, dir, side, 3f, 4f, c);
            Circle(b.Pos + dir * 8f, 4.5f, new Rgba(1f, 0.93f, 0.75f), 10);
        }
    }
}
