using UnityEngine;

namespace Arena
{
    /// Stone Colossus (Core/StoneColossus.cs; Docs/Mockups/stone-colossus.html): the Titan's stone body (TitanLook.cs) in earth and
    /// lava, worn by the tank. Drawn from what the snapshot carries: Player.ColT (on), ColAge (the rise) and ColWind (the slam), with
    /// the feet turning over by how far you walked. Its floor marks are FxKind.TitanSlam's earth kinds: a slam, the rise, the rubble,
    /// the crumble's rolling boulders and each footfall.
    public partial class ArenaApp
    {
        static readonly Rgba EarthBase = new Rgba(0.47f, 0.39f, 0.31f), EarthLite = new Rgba(0.71f, 0.6f, 0.47f), EarthDark = new Rgba(0.19f, 0.15f, 0.12f),
            EarthGlow = new Rgba(1f, 0.6f, 0.2f), EarthDust = new Rgba(0.62f, 0.54f, 0.43f);

        /// Swap the Titan's stone colours for earth and lava (true) or back (false).
        static void TitanPaint(bool earth)
        {
            TitanBase = earth ? EarthBase : RuneBase;
            TitanLite = earth ? EarthLite : RuneLite;
            TitanDark = earth ? EarthDark : RuneDark;
            TitanGlow = earth ? EarthGlow : RuneGlow;
            TitanDust = earth ? EarthDust : RuneDust;
        }

        /// Up as the golem (the rise is over): it is drawn instead of the player's model.
        static bool ColossusStanding(Player p) => Game.ColOn(p) && p.ColAge >= Game.ColRise;

        readonly System.Collections.Generic.Dictionary<int, V2> colStep = new System.Collections.Generic.Dictionary<int, V2>();
        readonly System.Collections.Generic.Dictionary<int, float> colStepPhase = new System.Collections.Generic.Dictionary<int, float>();

        void DrawColossus(Player p, float t, float alpha)
        {
            if (!Game.ColOn(p))
            {
                colStep.Remove(p.Id);
                return;
            }
            V2 pos = p.Disp;
            float sc = Game.ColScale * TitanUnit, age = p.ColAge;
            colStep.TryGetValue(p.Id, out V2 last);
            float walked = last.IsZero ? 0f : Mathf.Min(30f, pos.DistanceTo(last));
            float step = (colStepPhase.TryGetValue(p.Id, out float ph) ? ph : 0f) + walked * (MathX.Pi / Game.ColStride);  // a half stride a step
            colStep[p.Id] = pos;
            colStepPhase[p.Id] = step;
            TitanPaint(true);
            try
            {
                if (age < Game.ColRise)  // the earth piles onto you
                {
                    float k = Mathf.Clamp01(age / Game.ColRise);
                    GlowDisc(pos, 70f, EarthGlow, 0.35f * k * alpha);
                    for (int i = 0; i < 14; i++)
                    {
                        float dly = TH(i * 4.7f) * 0.4f, ck = Mathf.Clamp01((age - dly) / (Game.ColRise * 0.55f)), ek = TEase(ck);
                        if (ck <= 0f || ck >= 1f && k > 0.75f)
                            continue;
                        float a0 = i / 14f * MathX.Tau + TH(i) * 0.5f, d0 = 110f + TH(i + 9f) * 60f;
                        float an = a0 + (TH(i + 21f) < 0.5f ? -1f : 1f) * 0.9f * (1f - ek), dd = Mathf.Lerp(d0, 14f, ek);
                        V2 cp = pos + V2.FromAngle(an) * dd + new V2(0f, -Mathf.Sin(ck * MathX.Pi) * 40f);
                        Circle(pos + V2.FromAngle(an) * dd + new V2(3f, 4f), 7f, new Rgba(0f, 0f, 0f, 0.3f * alpha), 10);
                        TitanStone(cp, 6f + TH(i + 3f) * 6f, i * 7.1f + 50f, 6, 0f, Mathf.Clamp01(ck * 3f) * alpha);
                    }
                    float al = Mathf.Clamp01(k * 1.8f - 0.55f);
                    if (al > 0f)
                        TitanBody(pos, sc * (0.55f + 0.45f * TEase(k)), p.Aim, 0f, 0f, k * 0.7f, al * alpha);
                    return;
                }
                float glow = 0.78f + 0.22f * Mathf.Sin(t * 5f);
                if (p.ColT < 1.5f)  // the last moments: it shakes and its veins flicker before it breaks
                {
                    float end = 1f - p.ColT / 1.5f;
                    pos += new V2(Mathf.Sin(t * 70f), Mathf.Cos(t * 83f)) * (3f * end);
                    glow *= 0.7f + 0.3f * Mathf.Sin(t * 40f);
                }
                GlowDisc(pos, 56f, EarthGlow, 0.12f * glow * alpha);
                TitanBody(pos, sc, p.Aim, step, p.ColWind, glow, alpha);
                // where the fists land: a hint ring on your own golem, the wind-up for everyone
                V2 at = pos + V2.FromAngle(p.Aim) * Game.ColSlamAhead;
                if (p.ColWind > 0f && p.ColWind < 0.62f)
                {
                    float wk = Mathf.Clamp01(p.ColWind / 0.61f);
                    Arc(at, Game.ColSlamRadius, 2f + 3f * wk, EarthGlow.WithAlpha((0.25f + 0.5f * wk) * alpha), 48);
                    Arc(at, Game.ColSlamRadius * (1f - wk), 3f, new Rgba(1f, 0.95f, 0.8f, 0.5f * wk * alpha), 48);
                }
                else if (p.Id == game.MyId)
                    Arc(at, Game.ColSlamRadius, 1.5f, EarthGlow.WithAlpha(0.14f * alpha), 40);
            }
            finally
            {
                TitanPaint(false);
            }
        }

        /// FxKind.TitanSlam's earth kinds (Game.TitanEarth + 0 slam, + 1 the rise, + 2 rubble; ColFxBoulders; ColFxStomp).
        void DrawColossusFx(Effect fx, bool floor, int kind)
        {
            TitanPaint(true);
            try
            {
                if (kind <= Game.TitanEarth + 2)
                    TitanFx(fx, floor, kind - Game.TitanEarth);
                else if (kind == Game.ColFxBoulders)
                    DrawBouldersFx(fx, floor);
                else if (kind == Game.ColFxStomp)
                    DrawStompFx(fx, floor);
            }
            finally
            {
                TitanPaint(false);
            }
        }

        /// The crumble: ColBoulders boulders rolling out on the same path the host hurts with (Game.BoulderAt).
        void DrawBouldersFx(Effect fx, bool floor)
        {
            if (floor)
                return;
            float age = fx.T, roll = Mathf.Min(age, Game.ColRollTime);
            float fade = age > Game.ColRollTime ? 1f - Mathf.Clamp01((age - Game.ColRollTime) / (fx.Dur - Game.ColRollTime)) : 1f;
            for (int i = 0; i < Game.ColBoulders; i++)
            {
                float ang = fx.Pos2.Y + MathX.Tau * i / Game.ColBoulders;
                V2 at = Game.BoulderAt(fx.Pos, ang, roll);
                for (int j = 1; j <= 3; j++)  // a dust trail behind
                {
                    V2 back = Game.BoulderAt(fx.Pos, ang, Mathf.Max(0f, roll - 0.07f * j));
                    Circle(back, 10f + 4f * j, EarthDust.WithAlpha(0.16f * fade * (1f - roll / Game.ColRollTime)), 12);
                }
                Circle(at + new V2(4f, 6f), Game.ColBoulderRadius * 1.05f, new Rgba(0f, 0f, 0f, 0.32f * fade), 14);
                TitanStone(at, Game.ColBoulderRadius, 70f + i * 3.3f, 7, 0.5f * (1f - roll / Game.ColRollTime), fade);
                float spin = (at - fx.Pos).Length / Game.ColBoulderRadius;  // a seam turning over as it rolls
                V2 seam = V2.FromAngle(ang + MathX.Pi / 2f) * (Game.ColBoulderRadius * 0.7f * Mathf.Cos(spin));
                Line(at - seam, at + seam, 2f, EarthDark.WithAlpha(0.8f * fade));
            }
        }

        /// A footfall: a puff of dust and a few short cracks that cool and fade.
        void DrawStompFx(Effect fx, bool floor)
        {
            float age = fx.T, seed = fx.Pos.X * 0.017f + fx.Pos.Y * 0.031f;
            if (floor)
            {
                float fade = Mathf.Clamp01(1f - (age - fx.Dur * 0.4f) / (fx.Dur * 0.6f)), hot = Mathf.Clamp01(1f - age / 0.6f);
                Circle(fx.Pos, fx.Radius * 0.45f, new Rgba(0f, 0f, 0f, 0.22f * fade), 16);
                for (int i = 0; i < 5; i++)
                {
                    float a = seed + i * MathX.Tau / 5f + (TH(seed + i) - 0.5f) * 0.6f;
                    V2 mid = fx.Pos + V2.FromAngle(a) * (fx.Radius * 0.35f), end = mid + V2.FromAngle(a + (TH(seed + i * 3f) - 0.5f) * 0.8f) * (fx.Radius * 0.35f);
                    Line(fx.Pos, mid, 4f, new Rgba(0f, 0f, 0f, 0.55f * fade));
                    Line(mid, end, 3f, new Rgba(0f, 0f, 0f, 0.45f * fade));
                    Line(fx.Pos, mid, 1.6f, EarthDark.Mix(EarthGlow, hot).WithAlpha((0.3f + 0.6f * hot) * fade));
                }
                return;
            }
            float k = age / 0.4f;
            if (k >= 1f)
                return;
            Circle(fx.Pos, fx.Radius * TEase(k), EarthDust.WithAlpha(0.22f * (1f - k)), 20);
            Arc(fx.Pos, fx.Radius * TEase(k), 2f + 3f * (1f - k), new Rgba(1f, 0.93f, 0.8f, 0.45f * (1f - k)), 24);
        }
    }
}
