using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Tank Ninja's Bamboo Grove (Core/BambooGrove.cs), take B "Ripple" of Docs/Mockups/bamboo-grove.html, drawn from
    /// the one `FxKind.BambooGrove` effect. The wind-up: a pale green wash over the circle, a ring closing in on its edge,
    /// fourteen dark cracks running out from the caster's feet and a gold ring under them. Then six rings of bamboo burst up
    /// one after another behind a bright front racing outward: each stalk pops up past its height and settles, throwing
    /// dirt, a shaded green cane with pale nodes and a slanted pale cut at its tip. The inner rings sink after a moment; the
    /// outer one stands as the fence for its life, then sinks too.
    ///
    /// Stalks are laid out from a hash of the grove's middle, so every screen grows the same grove. They stand up the
    /// screen (turned with the camera), so they're split for depth: the far half is drawn before the enemies, the near half
    /// after them (`DrawBambooGroveBack` / `DrawBambooGroveFront`); the wash, cracks, front and shadows go under everything
    /// (`DrawBambooGroveFloor`).
    public partial class ArenaApp
    {
        static readonly Rgba BgWash = Rgb(127, 176, 74), BgRingCol = Rgb(160, 210, 100), BgCrack = Rgb(70, 55, 35),
            BgFront = Rgb(200, 230, 150), BgDirt = Rgb(110, 90, 60), BgGold = Rgb(217, 164, 65),
            BgCaneDark = Rgb(47, 85, 32), BgCane = Rgb(127, 176, 74), BgFenceCane = Rgb(140, 191, 85), BgShade = Rgb(60, 106, 38),
            BgNode = Rgb(214, 228, 160), BgCut = Rgb(228, 220, 174);

        static Rgba Rgb(int r, int g, int b) => new Rgba(r / 255f, g / 255f, b / 255f);

        static float BgHash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f) * 43758.5453f, 1f);
        static float BgRnd(float seed, float a, float b) => a + (b - a) * BgHash(seed);
        static float BgSeed(Effect fx) => Mathf.Repeat(fx.Pos.X * 0.37f + fx.Pos.Y * 1.91f, 1000f);

        struct BgStalk { public V2 Base; public float H, Born, Life, Lean, W; public bool Fence; }

        /// One stalk of a grove: ring `ri`, `i` of `n` round it.
        static BgStalk BgStalkOf(Effect fx, int ri, int i, int n, float seed)
        {
            float rr = fx.Radius * Game.BgRingAt[ri], k = seed + ri * 101.3f + i * 7.77f;
            bool last = ri == Game.BgRings - 1;
            float ang = (float)i / n * MathX.Tau + ri * 0.3f + BgRnd(k, -0.04f, 0.04f);
            return new BgStalk
            {
                Base = fx.Pos + V2.FromAngle(ang) * rr,
                H = last ? BgRnd(k + 1.1f, 34f, 42f) : BgRnd(k + 1.1f, 20f, 32f),
                Born = Game.BgWindup + ri * Game.BgRingGap,
                Life = last ? fx.Pos2.Y : 0.9f + ri * Game.BgRingGap,
                Lean = last ? 0f : BgRnd(k + 2.3f, -0.18f, 0.18f),
                W = last ? 5.5f : BgRnd(k + 3.7f, 3.5f, 6f),
                Fence = last,
            };
        }

        static int BgRingCount(Effect fx, int ri) => Mathf.Max(3, Mathf.RoundToInt(fx.Radius * Game.BgRingAt[ri] / 5.5f));

        /// A stalk's height at `age` since it was born: up past its height, settle, stand, sink.
        static float BgStalkH(BgStalk s, float age)
        {
            const float up = 0.09f;
            if (age < 0f)
                return 0f;
            if (age < up)
                return s.H * (age / up) * 1.15f;
            if (age < up + 0.08f)
                return s.H * (1.15f - 0.15f * (age - up) / 0.08f);
            if (age > s.Life)
                return s.H * Mathf.Max(0f, 1f - (age - s.Life) / Game.BgRetract);
            return s.H;
        }

        /// Up the screen, in arena units (the camera may be turned: CameraRotate.cs).
        V2 BgUp => UnrotateView(new V2(0f, -1f));

        /// Under everything: the wash, closing ring, cracks, the front, the caster's gold ring, dirt and shadows.
        void DrawBambooGroveFloor(float t)
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.BambooGrove)
                    DrawBgFloor(fx);
        }

        void DrawBgFloor(Effect fx)
        {
            float since = fx.T, R = fx.Radius, seed = BgSeed(fx);
            V2 c = fx.Pos, up = BgUp, right = new V2(-up.Y, up.X);
            float k = Mathf.Min(1f, since / Game.BgWindup);
            if (since < Game.BgWindup + 1.4f)
            {
                if (since < Game.BgWindup)
                {
                    Circle(c, R, BgWash.WithAlpha(0.07f + 0.1f * k), 48);
                    Arc(c, R * (1.25f - 0.25f * k), 2.5f, BgRingCol.WithAlpha(0.5f + 0.5f * k), 48);
                }
                float fade = since > Game.BgWindup ? Mathf.Max(0f, 1f - (since - Game.BgWindup) / 1.4f) : 1f;
                for (int i = 0; i < 14; i++)  // cracks running out from the middle
                {
                    float a = i / 14f * MathX.Tau + BgRnd(seed + i * 3.1f, -0.15f, 0.15f), r = 0f;
                    V2 prev = c;
                    int steps = 0;
                    while (r < R && steps < 16)
                    {
                        steps++;
                        r = Mathf.Min(R, r + BgRnd(seed + i * 17f + steps * 1.3f, 14f, 26f));
                        a += BgRnd(seed + i * 29f + steps * 2.7f, -0.3f, 0.3f);
                        if (r / R > k + 0.001f)
                            break;
                        V2 next = c + V2.FromAngle(a) * r;
                        Line(prev, next, 2f, BgCrack.WithAlpha(0.9f * fade));
                        Line(prev - up, next - up, 0.8f, BgRingCol.WithAlpha(0.35f * fade));
                        prev = next;
                    }
                }
            }
            if (since > Game.BgWindup && since < Game.BgWindup + 0.55f)  // the wave's front
            {
                float f = (since - Game.BgWindup) / 0.42f * R;
                if (f < R + 6f)
                    Arc(c, f, 3f, BgFront.WithAlpha(0.6f), 48);
            }
            Player p;
            if (since < Game.BgWindup + 0.2f && game.Players.TryGetValue((int)fx.Pos2.X, out p))  // palm to the ground: a gold ring at the caster's feet
            {
                V2 feet = p.Disp - up * 8f;
                float a = 0.8f * (1f - Mathf.Max(0f, since - Game.BgWindup) / 0.2f);
                EllipseLine(feet, 10f + k * 6f, 4f + k * 2f, 2f, BgGold.WithAlpha(a), 20);
            }
            for (int ri = 0; ri < Game.BgRings; ri++)  // shadows and dirt under the stalks
            {
                int n = BgRingCount(fx, ri);
                for (int i = 0; i < n; i++)
                {
                    var s = BgStalkOf(fx, ri, i, n, seed);
                    float age = since - s.Born, h = BgStalkH(s, age);
                    if (h <= 0.5f)
                        continue;
                    if (age < 0.3f)
                        for (int j = 0; j < 4; j++)
                        {
                            float an = j * 1.7f + s.Base.X;
                            V2 at = s.Base + right * (Mathf.Cos(an) * age * 40f) - up * (Mathf.Sin(an) * age * 18f) + up * (age * 20f);
                            Circle(at, 1.6f, BgDirt.WithAlpha(0.6f * (1f - age / 0.3f)), 6);
                        }
                    Ellipse(s.Base + right * 2f - up, s.W * 0.9f, s.W * 0.4f, Mathf.Atan2(right.Y, right.X), new Rgba(0f, 0f, 0f, 0.35f), 10);
                }
            }
        }

        /// The far half of the stalks, before the enemies.
        void DrawBambooGroveBack(float t) => DrawBgStalks(true);

        /// The near half of the stalks, after the enemies.
        void DrawBambooGroveFront(float t) => DrawBgStalks(false);

        readonly List<KeyValuePair<float, BgStalk>> bgDraw = new List<KeyValuePair<float, BgStalk>>();

        void DrawBgStalks(bool back)
        {
            V2 up = BgUp;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.BambooGrove)
                    continue;
                float seed = BgSeed(fx);
                bgDraw.Clear();
                for (int ri = 0; ri < Game.BgRings; ri++)
                {
                    int n = BgRingCount(fx, ri);
                    for (int i = 0; i < n; i++)
                    {
                        var s = BgStalkOf(fx, ri, i, n, seed);
                        float along = (s.Base - fx.Pos).Dot(up);  // how far up the screen from the middle
                        if ((along > 0f) == back)
                            bgDraw.Add(new KeyValuePair<float, BgStalk>(along, s));
                    }
                }
                bgDraw.Sort((a, b) => b.Key.CompareTo(a.Key));  // top of the screen first, so nearer canes cover farther ones
                foreach (var kv in bgDraw)
                    DrawBgStalk(kv.Value, fx.T - kv.Value.Born, up);
            }
        }

        void DrawBgStalk(BgStalk s, float age, V2 up)
        {
            float h = BgStalkH(s, age);
            if (h <= 0.5f)
                return;
            V2 right = new V2(-up.Y, up.X);
            V2 b = s.Base, tip = b + right * (s.Lean * h) + up * h, axis = tip - b;
            float len = axis.Length;
            if (len < 0.01f)
                return;
            V2 side = new V2(-axis.Y, axis.X) / len;  // across the cane
            if (side.Dot(right) < 0f)
                side = -side;
            float w = s.W;
            V2 cutDrop = -up * (w * 0.6f);  // the slanted cut: one side of the tip lower than the other
            Rgba mid = s.Fence ? BgFenceCane : BgCane;
            const int strips = 4;  // the cane's shading across, dark edge to bright to shade
            for (int k = 0; k < strips; k++)
            {
                float u0 = (float)k / strips, u1 = (float)(k + 1) / strips, uc = (u0 + u1) * 0.5f;
                Rgba col = uc < 0.45f ? BgCaneDark.Mix(mid, uc / 0.45f) : mid.Mix(BgShade, (uc - 0.45f) / 0.55f);
                V2 o0 = side * (w * (u0 - 0.5f)), o1 = side * (w * (u1 - 0.5f));
                V2 t0 = tip + o0 + cutDrop * (1f - 2f * u0), t1 = tip + o1 + cutDrop * (1f - 2f * u1);
                Quad(b + o0, t0, t1, b + o1, col);
            }
            for (float d = 9f; d < h - 4f; d += 10f)  // the nodes
            {
                V2 at = b + axis * (d / h);
                Line(at - side * (w * 0.575f), at + side * (w * 0.575f), 1f, BgNode.WithAlpha(0.55f));
            }
            Ellipse(tip, w * 0.55f, w * 0.32f, Mathf.Atan2(side.Y, side.X), BgCut, 10);
        }
    }
}
