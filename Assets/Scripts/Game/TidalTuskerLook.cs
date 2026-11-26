using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Tidal Tusker (the Tank Summoner's fourth ultimate, Core/TidalTusker.cs), from the Tidal Tusker mockup. Everything is drawn
    /// from the replicated zone (`ZoneKind.Tusker`: Pos, Aim, Radius, GuardFrac, Origin/Origin2 and Side/Rider, T, Duration):
    ///   the floor (DrawTuskerFloor, under everyone) the puddle it rises from, its shadow, and the water its streams leave on
    ///             the ground: puddles that spread and dry, and the stomp's flood;
    ///   over      (DrawTuskers, over the enemies) the teal spirit elephant: stepping feet, shaded body and spirit rim, saddle
    ///             blanket, swishing tail, flapping ears (they flap hard when it's bitten), tusks and a ringed trunk that swings
    ///             after what it's spraying; then each stream as a curving, rippling hose of water (spray, core and white centre,
    ///             widening as it goes) with pressure pulses racing down it, mist at the trunk and churning foam where it hits;
    ///             a health bar and a time bar over it. It rises out of its puddle, rears for the stomp and fades in spirit motes.
    /// Its place, facing and trunk are eased toward the snapshots so it moves smoothly; the splash droplets and motes are
    /// particles (Juice.cs). The stomp shakes the screen once. Visual effects off: a plain ellipse, a line for each stream.
    public partial class ArenaApp
    {
        static readonly Rgba TkSkin = new Rgba(0.56f, 0.7f, 0.74f), TkSkinLight = new Rgba(0.81f, 0.9f, 0.93f), TkSkinDark = new Rgba(0.33f, 0.47f, 0.53f),
            TkInk = new Rgba(0.17f, 0.28f, 0.33f), TkSpirit = new Rgba(0.39f, 0.89f, 0.82f), TkWater = new Rgba(0.36f, 0.79f, 1f),
            TkFoam = new Rgba(0.91f, 0.97f, 1f), TkIvory = new Rgba(0.96f, 0.94f, 0.85f), TkPink = new Rgba(0.88f, 0.59f, 0.63f), TkEye = new Rgba(0.04f, 0.09f, 0.13f);

        /// The mockup's elephant is drawn this much bigger than its numbers.
        const float TkSize = 1.3f;

        class TkView
        {
            public V2 Disp;
            public float Face, Trunk, Trunk2, Step, Flap, Frac = 1f, Hurt, PuddleT;
            public bool Stomped;
        }

        struct TkPuddle
        {
            public V2 Pos;
            public float R, Life, Max, Ph;
        }

        readonly Dictionary<int, TkView> tkViews = new Dictionary<int, TkView>();
        readonly List<int> tkGone = new List<int>();
        readonly List<TkPuddle> tkPuddles = new List<TkPuddle>();

        /// Under everyone (WorldRenderer.DrawArenaContents): eases each elephant's view, then the puddles and its shadow.
        void DrawTuskerFloor(float t)
        {
            TickTuskerViews(Mathf.Min(0.1f, Time.unscaledDeltaTime));
            if (fxOn)
                foreach (var pd in tkPuddles)
                {
                    float a = Mathf.Clamp01(pd.Life / pd.Max * 1.5f), grow = 0.75f + 0.25f * Mathf.Clamp01((pd.Max - pd.Life) * 4f);
                    Ellipse(pd.Pos, pd.R * grow, pd.R * grow * 0.72f, pd.Ph, TkWater.WithAlpha(0.13f * a), 14);
                    Ellipse(pd.Pos, pd.R * grow * 0.55f, pd.R * grow * 0.4f, pd.Ph, TkWater.WithAlpha(0.12f * a), 12);
                    Arc(pd.Pos + V2.FromAngle(pd.Ph + 2.2f) * (pd.R * 0.3f), pd.R * 0.25f, 1f, TkFoam.WithAlpha(0.18f * a), 8, pd.Ph, 1.6f);
                }
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.Tusker)
                    continue;
                TkView v;
                if (!tkViews.TryGetValue(z.Id, out v))
                    continue;
                float age = Game.TkAge(z), rise = Mathf.Clamp01(age / Game.TkRise);
                if (!fxOn)
                {
                    Arc(v.Disp, Game.TkBody, 1.5f, TkSpirit.WithAlpha(0.5f), 20);
                    continue;
                }
                // the puddle it stands in: wide while it rises out of it, a pool under its feet after
                float pr = Game.TkBody * TkSize * (age < Game.TkRise ? 0.6f + 0.9f * KEo(rise) : 1.25f);
                Ellipse(v.Disp + new V2(0f, 6f), pr, pr * 0.62f, 0f, TkWater.WithAlpha(0.16f), 20);
                if (age < Game.TkRise + 0.4f)  // the ripples it rises through
                    for (int k = 0; k < 3; k++)
                    {
                        float f = (age * 1.4f + k / 3f) % 1f;
                        RingEllipse(v.Disp + new V2(0f, 6f), pr * (0.4f + f), pr * (0.4f + f) * 0.62f, 1.5f, TkFoam.WithAlpha(0.45f * (1f - f) * (1f - Mathf.Clamp01(age - Game.TkRise) * 2.5f)));
                    }
                float alpha = TuskerAlpha(z);
                Ellipse(v.Disp + new V2(-2f, 9f) * TkSize, 40f * TkSize * rise, 25f * TkSize * rise, 0f, new Rgba(0f, 0f, 0f, 0.3f * alpha), 18);
                if (z.Owner == game.MyId && Game.TuskerUp(z))  // where it draws enemies from (your own only: a faint dashed ring)
                    for (int d = 0; d < 36; d++)
                        Arc(v.Disp, Game.TkPull, 1f, TkSpirit.WithAlpha(0.12f), 3, d * MathX.Tau / 36f + t * 0.12f, MathX.Tau / 36f * 0.45f);
            }
        }

        /// How solid it is: fading in as it rises, out after its stomp.
        static float TuskerAlpha(Zone z)
        {
            float age = Game.TkAge(z);
            if (age < Game.TkRise)
                return Mathf.Clamp01(age / Game.TkRise);
            float after = Game.TkStomp - Game.TkStompLand;
            return z.T < after ? Mathf.Clamp01(z.T / after * 1.6f) : 1f;
        }

        /// An ellipse's outline (no rotation).
        void RingEllipse(V2 c, float rx, float ry, float w, Rgba col)
        {
            const int n = 24;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * MathX.Tau / n, a1 = (i + 1) * MathX.Tau / n;
                Line(c + new V2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry), c + new V2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry), w, col);
            }
        }

        void TickTuskerViews(float dt)
        {
            tkGone.Clear();
            foreach (var id in tkViews.Keys)
                if (!game.Zones.ContainsKey(id))
                    tkGone.Add(id);
            foreach (var id in tkGone)
                tkViews.Remove(id);
            for (int i = tkPuddles.Count - 1; i >= 0; i--)
            {
                var pd = tkPuddles[i];
                pd.Life -= dt;
                if (pd.Life <= 0f)
                    tkPuddles.RemoveAt(i);
                else
                    tkPuddles[i] = pd;
            }
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.Tusker)
                    continue;
                TkView v;
                if (!tkViews.TryGetValue(z.Id, out v) || (v.Disp - z.Pos).Length > 160f)
                {
                    if (v == null)
                    {
                        v = new TkView { Face = z.Aim, Trunk = z.Aim, Trunk2 = z.Aim, Frac = z.GuardFrac };
                        tkViews[z.Id] = v;
                    }
                    v.Disp = z.Pos;
                }
                V2 was = v.Disp;
                v.Disp = V2.Lerp(v.Disp, z.Pos, 1f - Mathf.Exp(-14f * dt));
                v.Step += (v.Disp - was).Length;
                v.Face = MathX.WrapPi(v.Face + MathX.WrapPi(z.Aim - v.Face) * (1f - Mathf.Exp(-10f * dt)));
                float frac = z.GuardFrac;
                if (frac < v.Frac - 0.001f)
                {
                    v.Flap = 1f;  // bitten or shot: the ears flap
                    v.Hurt = 0.12f;
                }
                v.Frac = frac;
                v.Flap = Mathf.Max(0f, v.Flap - dt * 2.5f);
                v.Hurt = Mathf.Max(0f, v.Hurt - dt);
                V2 head = TkHeadAt(v, 1f);
                float t = Time.time;
                float want = z.Side != 0 ? (z.Origin - head).Angle : v.Face + Mathf.Sin(t * 2f) * 0.35f;
                v.Trunk = MathX.WrapPi(v.Trunk + MathX.WrapPi(want - v.Trunk) * (1f - Mathf.Exp(-9f * dt)));
                float want2 = z.Rider != 0 ? (z.Origin2 - head).Angle : v.Trunk;
                v.Trunk2 = MathX.WrapPi(v.Trunk2 + MathX.WrapPi(want2 - v.Trunk2) * (1f - Mathf.Exp(-9f * dt)));
                if (!fxOn)
                    continue;
                float age = Game.TkAge(z);
                if (age < Game.TkRise && Rnd(0f, 1f) < dt * 40f)
                    Sparks(v.Disp + new V2(Rnd(-30f, 30f), Rnd(-18f, 18f)), TkSpirit, 1, 30f, 0.7f, 2.2f);  // spirit motes as it forms
                else if (Game.TuskerUp(z) && Rnd(0f, 1f) < dt * 5f)
                    Sparks(v.Disp + new V2(Rnd(-20f, 20f), Rnd(-14f, 14f)), TkSpirit, 1, 14f, 0.9f, 2f);
                v.PuddleT -= dt;
                for (int s = 0; s < 2; s++)
                {
                    int hit = s == 0 ? z.Side : z.Rider;
                    if (hit == 0)
                        continue;
                    V2 end = s == 0 ? z.Origin : z.Origin2;
                    if (Rnd(0f, 1f) < dt * 30f)  // spray thrown back off what it hits
                        Sparks(end, Rnd(0f, 1f) < 0.3f ? TkFoam : TkWater, 2, 130f, 0.35f, 2.2f);
                    if (v.PuddleT <= 0f)
                        AddTkPuddle(end + new V2(Rnd(-12f, 12f), Rnd(-10f, 10f)), Rnd(12f, 22f), 2.5f);
                }
                if (v.PuddleT <= 0f)
                    v.PuddleT = 0.08f;
                if (!v.Stomped && Game.TkStomping(z) && z.T <= Game.TkStomp - Game.TkStompLand)
                {
                    v.Stomped = true;  // the stomp lands: a flood round it, a burst of spray, one shake
                    AddTkPuddle(z.Pos, Game.TkSplashR * 0.9f, 3.5f);
                    Sparks(z.Pos, TkWater, 46, 330f, 0.6f, 3f);
                    Sparks(z.Pos, TkFoam, 18, 220f, 0.5f, 2.5f);
                    Ring(z.Pos, Game.TkSplashR, 0.6f, TkWater);
                    Shake(0.28f);
                    Hold(0.05f);
                }
            }
        }

        void AddTkPuddle(V2 at, float r, float life)
        {
            if (tkPuddles.Count >= 160)
                tkPuddles.RemoveAt(0);
            tkPuddles.Add(new TkPuddle { Pos = at, R = r, Life = life, Max = life, Ph = Rnd(0f, MathX.Tau) });
        }

        /// Where the trunk leaves its head, for an elephant drawn `k` times its size.
        static V2 TkHeadAt(TkView v, float k) => v.Disp + V2.FromAngle(v.Face) * (31f * TkSize * k);

        /// Over the enemies (WorldRenderer.DrawArenaOver): each elephant and its streams.
        void DrawTuskers(float t)
        {
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.Tusker)
                    continue;
                TkView v;
                if (tkViews.TryGetValue(z.Id, out v))
                    DrawTusker(z, v, t);
            }
        }

        void DrawTusker(Zone z, TkView v, float t)
        {
            float age = Game.TkAge(z), alpha = TuskerAlpha(z);
            float k = 1f, lift = 0f;
            if (age < Game.TkRise)
                k = 0.4f + 0.6f * KEo(age / Game.TkRise);
            else if (Game.TkStomping(z))
            {
                float s = (Game.TkStomp - z.T) / Game.TkStompLand;  // 0 to 1 as it rears, past 1 after it lands
                k = s < 1f ? 1f + 0.12f * s : 1.12f - 0.12f * Mathf.Clamp01((s - 1f) * 3f);
                lift = s < 1f ? 16f * Mathf.Sin(s * Mathf.PI * 0.5f) : 0f;
            }
            V2 c = v.Disp + new V2(0f, -lift + (age < Game.TkRise ? (1f - age / Game.TkRise) * 12f : 0f));
            if (!fxOn)
            {
                Ellipse(c, 31f * TkSize, 23f * TkSize, v.Face, TkSkin.WithAlpha(alpha), 14);
                if (z.Side != 0)
                    Line(TkHeadAt(v, 1f), z.Origin, 3f, TkWater);
                if (z.Rider != 0)
                    Line(TkHeadAt(v, 1f), z.Origin2, 3f, TkWater);
                return;
            }
            float sk = TkSize * k;
            System.Func<float, float, V2> L = (x, y) => c + new V2(x, y).Rotated(v.Face) * sk;
            System.Func<Rgba, Rgba> Hurt = col => (v.Hurt > 0f ? col.Mix(Rgba.White, 0.7f) : col).WithAlpha(alpha);
            GlowDisc(c, 62f * sk, TkSpirit, (0.16f + 0.06f * Mathf.Sin(t * 4f)) * alpha);
            // feet, stepping as it walks
            float st = Mathf.Sin(v.Step * 0.14f);
            for (int i = 0; i < 4; i++)
            {
                float lx = i < 2 ? 13f : -17f, ly = (i % 2 == 0 ? -1f : 1f) * (i < 2 ? 17f : 16f), ph = i == 0 || i == 3 ? 1f : -1f;
                float fx = lx + st * 3.5f * ph;
                Ellipse(L(fx, ly), 7f * sk, 6f * sk, v.Face, TkSkinDark.WithAlpha(alpha), 10);
                for (int o = -1; o <= 1; o++)
                    Circle(L(fx + 5f, ly + o * 3f), 1.1f * sk, TkIvory.WithAlpha(alpha), 6);
            }
            // tail
            float sw = Mathf.Sin(t * 5f);
            Line(L(-33f, 0f), L(-39f, sw * 4f), 2f * sk, TkSkinDark.WithAlpha(alpha));
            Line(L(-39f, sw * 4f), L(-42f, sw * 7f), 2f * sk, TkSkinDark.WithAlpha(alpha));
            Circle(L(-42f, sw * 7f), 2.2f * sk, TkInk.WithAlpha(alpha), 6);
            // body: ink rim, skin, belly shade, back light, spirit rim
            Ellipse(L(-3f, 0f), 32.6f * sk, 24.6f * sk, v.Face, TkInk.WithAlpha(alpha), 22);
            Ellipse(L(-3f, 0f), 31f * sk, 23f * sk, v.Face, Hurt(TkSkin), 22);
            Ellipse(L(-1f, 7f), 26f * sk, 13f * sk, v.Face, TkSkinDark.WithAlpha(0.3f * alpha), 18);
            Ellipse(L(-8f, -6f), 20f * sk, 12f * sk, v.Face, TkSkinLight.WithAlpha(0.5f * alpha), 18);
            Ellipse(L(-11f, -8f), 9f * sk, 5f * sk, v.Face, TkSkinLight.WithAlpha(0.45f * alpha), 12);
            TkRim(c, v.Face, -3f, 33.5f, 25.5f, sk, 1.4f, TkSpirit.WithAlpha((0.5f + 0.25f * Mathf.Sin(t * 4f)) * alpha));
            for (int i = 0; i < 8; i++)  // the spine
                Line(L(-28f + i * 5.25f, -1f - 3f * Mathf.Sin(i / 8f * Mathf.PI)), L(-28f + (i + 1) * 5.25f, -1f - 3f * Mathf.Sin((i + 1) / 8f * Mathf.PI)), 2f * sk, TkSkinLight.WithAlpha(0.35f * alpha));
            for (int f = 0; f < 3; f++)  // skin folds
            {
                float fx = -18f + f * 8f;
                for (int i = 0; i < 4; i++)
                {
                    float a0 = -0.5f + i * 0.25f, a1 = a0 + 0.25f;
                    Line(L(fx - 18f + 18f * Mathf.Cos(a0), 18f * Mathf.Sin(a0)), L(fx - 18f + 18f * Mathf.Cos(a1), 18f * Mathf.Sin(a1)), 1f * sk, TkInk.WithAlpha(0.22f * alpha));
                }
            }
            // the spirit blanket and its gold clasp
            Ellipse(L(-6f, 0f), 11f * sk, 15f * sk, v.Face, TkSpirit.WithAlpha(0.22f * alpha), 14);
            TkRim(c, v.Face, -6f, 11f, 15f, sk, 1.2f, TkSpirit.WithAlpha(0.7f * alpha));
            Circle(L(-6f, 0f), 2.2f * sk, Rgba.Gold.WithAlpha(alpha), 8);
            // ears: flapping in the breeze, hard when it's hurt
            float fl = v.Flap * 0.6f + Mathf.Sin(t * 3f) * 0.1f;
            for (int s = -1; s <= 1; s += 2)
            {
                float ea = s * (0.45f + fl);
                V2 pivot = new V2(19f, s * 11f), mid = pivot + new V2(-5f, s * 11f).Rotated(ea);
                V2 ec = L(mid.X, mid.Y);
                Ellipse(ec, 14.4f * sk, 17.4f * sk, v.Face + ea, TkInk.WithAlpha(alpha), 16);
                Ellipse(ec, 13f * sk, 16f * sk, v.Face + ea, Hurt(TkSkin.Mix(TkSkinLight, 0.35f)), 16);
                V2 inner = pivot + new V2(-4f, s * 11f).Rotated(ea);
                Ellipse(L(inner.X, inner.Y), 7.5f * sk, 10f * sk, v.Face + ea, TkPink.WithAlpha(0.35f * alpha), 12);
                for (int vn = -1; vn <= 1; vn++)  // veins
                {
                    V2 a = pivot + new V2(0f, s * 3f).Rotated(ea), b = pivot + new V2(-4f + vn * 3f, s * 10f).Rotated(ea), e = pivot + new V2(-7f + vn * 5f, s * 20f).Rotated(ea);
                    Line(L(a.X, a.Y), L(b.X, b.Y), 0.8f * sk, TkPink.Mix(TkInk, 0.4f).WithAlpha(0.35f * alpha));
                    Line(L(b.X, b.Y), L(e.X, e.Y), 0.8f * sk, TkPink.Mix(TkInk, 0.4f).WithAlpha(0.3f * alpha));
                }
            }
            // head
            Ellipse(L(28f, 0f), 14.4f * sk, 15.4f * sk, v.Face, TkInk.WithAlpha(alpha), 16);
            Ellipse(L(28f, 0f), 13f * sk, 14f * sk, v.Face, Hurt(TkSkin.Mix(TkSkinLight, 0.25f)), 16);
            Ellipse(L(24f, -4f), 7f * sk, 6f * sk, v.Face, TkSkinLight.WithAlpha(0.55f * alpha), 10);
            for (int s = -1; s <= 1; s += 2)  // the two domes of its brow
                Arc(L(25f, s * 5f), 5f * sk, 1.4f * sk, TkSkinLight.WithAlpha(0.4f * alpha), 8, v.Face + (s > 0 ? 0f : MathX.Pi), MathX.Pi * 0.8f);
            // tusks and eyes
            for (int s = -1; s <= 1; s += 2)
            {
                V2 a = L(34f, s * 7f), m = L(43f, s * 10f), e = L(48f, s * 5f);
                Line(a, m, 4f * sk, TkInk.WithAlpha(alpha));
                Line(m, e, 4f * sk, TkInk.WithAlpha(alpha));
                Line(a, m, 2.4f * sk, TkIvory.WithAlpha(alpha));
                Line(m, e, 2.4f * sk, TkIvory.WithAlpha(alpha));
                Circle(L(31f, s * 7.5f), 1.7f * sk, TkEye.WithAlpha(alpha), 8);
                Circle(L(31.4f, s * 7.5f - 0.6f), 0.6f * sk, Rgba.White.WithAlpha(0.8f * alpha), 6);
            }
            // the trunk: from its brow, swinging after what it sprays (curled up as it rears)
            bool stomping = Game.TkStomping(z);
            DrawTkTrunk(c, v, sk, stomping ? v.Face - 1.3f : v.Trunk, alpha, z.Side != 0 && !stomping);
            if (z.Rider != 0 && !stomping)  // mastered: the second stream leaves the same trunk
                DrawTkStream(TkTip(c, v, sk, v.Trunk2), v.Trunk2, z.Origin2, t);
            if (z.Side != 0 && !stomping)
                DrawTkStream(TkTip(c, v, sk, v.Trunk), v.Trunk, z.Origin, t);
            // health and time
            if (Game.TuskerUp(z))
            {
                float w = 64f, by = -50f * TkSize;
                V2 b0 = v.Disp + new V2(-w / 2f, by), b1 = v.Disp + new V2(w / 2f, by);
                float hp = Mathf.Clamp01(z.GuardFrac), left = Mathf.Clamp01((z.T - Game.TkStomp) / (z.Duration - Game.TkRise - Game.TkStomp));
                Line(b0 - new V2(1f, 0f), b1 + new V2(1f, 0f), 7f, new Rgba(0.02f, 0.05f, 0.07f, 0.85f));
                Line(b0, V2.Lerp(b0, b1, hp), 4f, hp > 0.5f ? TkSpirit : hp > 0.25f ? Rgba.Gold : new Rgba(0.89f, 0.34f, 0.3f));
                Line(b0 + new V2(0f, -1.2f), V2.Lerp(b0, b1, hp) + new V2(0f, -1.2f), 1.2f, Rgba.White.WithAlpha(0.3f));
                Line(b0 + new V2(0f, 5f), V2.Lerp(b0, b1, left) + new V2(0f, 5f), 2f, TkWater.WithAlpha(0.85f));
            }
        }

        /// A rotated ellipse's outline round the body point (x, 0).
        void TkRim(V2 c, float face, float x, float rx, float ry, float sk, float w, Rgba col)
        {
            const int n = 28;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * MathX.Tau / n, a1 = (i + 1) * MathX.Tau / n;
                V2 p0 = c + new V2(x + Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry).Rotated(face) * sk;
                V2 p1 = c + new V2(x + Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry).Rotated(face) * sk;
                Line(p0, p1, w * sk, col);
            }
        }

        static V2 TkTip(V2 c, TkView v, float sk, float trunk) => c + V2.FromAngle(v.Face) * (31f * sk) + V2.FromAngle(trunk) * (25f * sk);

        void DrawTkTrunk(V2 c, TkView v, float sk, float trunk, float alpha, bool spraying)
        {
            V2 h = c + V2.FromAngle(v.Face) * (31f * sk), m = h + V2.FromAngle(v.Face) * (25f * sk * 0.55f), e = h + V2.FromAngle(trunk) * (25f * sk);
            const int n = 10;
            for (int i = 0; i < n; i++)
            {
                V2 a = TkQuad(h, m, e, i / (float)n), b = TkQuad(h, m, e, (i + 1) / (float)n);
                Line(a, b, (8.5f - i * 0.35f) * sk, TkInk.WithAlpha(alpha));
            }
            for (int i = 0; i < n; i++)
            {
                V2 a = TkQuad(h, m, e, i / (float)n), b = TkQuad(h, m, e, (i + 1) / (float)n);
                Line(a, b, (6.5f - i * 0.35f) * sk, (i % 2 == 0 ? TkSkin.Mix(TkSkinLight, 0.3f) : TkSkin.Mix(TkSkinLight, 0.15f)).WithAlpha(alpha));
            }
            for (int i = 2; i < n; i += 2)  // its rings
            {
                V2 p = TkQuad(h, m, e, i / (float)n);
                Arc(p, 2.6f * sk, 0.8f, TkInk.WithAlpha(0.45f * alpha), 5, trunk + 1.2f, 0.75f);
            }
            Circle(e, 3.4f * sk, TkInk.WithAlpha(alpha), 10);
            Circle(e, 2.6f * sk, (spraying ? TkFoam : TkSkinLight).WithAlpha(alpha), 10);
        }

        static V2 TkQuad(V2 a, V2 m, V2 b, float u)
        {
            float w = 1f - u;
            return a * (w * w) + m * (2f * w * u) + b * (u * u);
        }

        /// One stream: from the trunk's tip (leaving along `nozzle`) curving to `end`, rippling, widening, with pulses and foam.
        void DrawTkStream(V2 tip, float nozzle, V2 end, float t)
        {
            float len = (end - tip).Length;
            if (len < 2f)
                return;
            V2 ctrl = tip + V2.FromAngle(nozzle) * (len * 0.45f);
            int n = Mathf.Clamp((int)(len / 7f), 6, 48);
            System.Func<float, V2> Pt = u =>
            {
                V2 p0 = TkQuad(tip, ctrl, end, u), q0 = TkQuad(tip, ctrl, end, Mathf.Min(1f, u + 0.02f));
                V2 dir = (q0 - p0).Normalized();
                float wob = Mathf.Sin(u * 16f - t * 34f) * (0.4f + u * 1.6f);
                return p0 + new V2(-dir.Y, dir.X) * wob;
            };
            GlowDisc(V2.Lerp(tip, end, 0.5f), len * 0.5f + 10f, TkWater, 0.08f);
            for (int layer = 0; layer < 4; layer++)
            {
                Rgba col = layer == 0 ? TkWater.WithAlpha(0.12f) : layer == 1 ? TkWater.WithAlpha(0.3f) : layer == 2 ? TkWater.Mix(TkFoam, 0.25f).WithAlpha(0.92f) : TkFoam.WithAlpha(0.95f);
                V2 prev = Pt(0f);
                for (int i = 1; i <= n; i++)
                {
                    float u = i / (float)n;
                    V2 p = Pt(u);
                    float w = layer == 0 ? 7f + u * 10f : layer == 1 ? 4f + u * 6f : layer == 2 ? 3f + u * 3f : 1.2f + u * 1.2f;
                    Line(prev, p, w, col);
                    prev = p;
                }
            }
            for (int k = 0; k < 4; k++)  // pressure pulses racing down it
            {
                float u = (t * 2.6f + k / 4f) % 1f;
                V2 p = Pt(u), q = Pt(Mathf.Min(1f, u + 0.03f));
                Ellipse(p, 3f + u * 3f, 1.6f + u, (q - p).Angle, Rgba.White.WithAlpha(0.75f * (1f - u)), 8);
            }
            Circle(tip, 3.5f + Mathf.Sin(t * 50f), TkFoam.WithAlpha(0.35f), 8);  // mist at the trunk
            GlowDisc(end, 18f, TkFoam, 0.55f);  // foam churning where it hits
            float back = (end - ctrl).Angle + MathX.Pi;
            for (int k = 0; k < 6; k++)
            {
                float j = t * 40f, a = back + Mathf.Sin(j + k * 2.1f) * 1.2f, r = 3f + (k * 7f + j) % 6f;
                Circle(end + V2.FromAngle(a) * r, 2f + k % 3, TkFoam.WithAlpha(0.4f + 0.1f * (k % 3)), 8);
            }
        }
    }
}
