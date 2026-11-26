using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Ballista (Core/Ballista.cs), from the mockup (claude.ai artifact "Knight Ballista Ultimate"). Its shadow grows under a
    /// falling frame and a red ring marks the landing's reach; it lands with a gold shock ring, dust and a shake. Then an oak
    /// platform with iron corners, and on it the bow: a long stock, two steel-shod arms, the string drawn back by the windlass and
    /// a giant bolt laid on the stock while loaded. The rider (drawn as a player) sits in the seat behind it. Under it: a gold pip
    /// per bolt left, its time left (steel) and health (gold to red). Each bolt has a recoil kick, a muzzle flash and a wood-and-steel
    /// bolt streaking off with a gold glow. When it breaks, its planks fly apart. Everything here comes from the replicated zone
    /// (Pos, Aim, Radius, T, Duration, GuardFrac, Rider, BoltsLeft, GunCd); only the damage is the host's.
    public partial class ArenaApp
    {
        class BaVis
        {
            public int Bolts = -1, Max, Seen;
            public float Recoil, Flash, HitT, Frac = 1f, LastT = -1f;
            public bool Landed, Broke;
        }

        readonly Dictionary<int, BaVis> baVis = new Dictionary<int, BaVis>();
        readonly List<int> baGone = new List<int>();
        int baFrame;

        static readonly Rgba BaOak = new Rgba(0.54f, 0.35f, 0.2f), BaOakDark = new Rgba(0.36f, 0.23f, 0.12f), BaOakDeep = new Rgba(0.22f, 0.14f, 0.08f),
            BaIron = new Rgba(0.27f, 0.3f, 0.38f), BaSteel = new Rgba(0.72f, 0.76f, 0.85f), BaRope = new Rgba(0.91f, 0.86f, 0.75f),
            BaShaft = new Rgba(0.85f, 0.76f, 0.6f), BaGoldHot = new Rgba(1f, 0.93f, 0.7f), BaRed = new Rgba(0.78f, 0.3f, 0.34f), BaPipOff = new Rgba(0.16f, 0.19f, 0.31f);

        const float BaK = 1f;  // the frame's scale (px per mockup unit)

        BaVis BallistaVis(Zone z, float t)
        {
            BaVis v;
            if (!baVis.TryGetValue(z.Id, out v))
                baVis[z.Id] = v = new BaVis { Bolts = z.BoltsLeft, Max = z.BoltsLeft, LastT = t };
            float dt = Mathf.Clamp(t - v.LastT, 0f, 0.05f);
            if (dt <= 0f)
                return v;  // (a second draw this frame changes nothing)
            v.LastT = t;
            v.Seen = ++baFrame;
            float age = Game.BaAge(z);
            Player rider;
            game.Players.TryGetValue(z.Rider, out rider);
            bool mine = rider != null && rider == game.Me || z.Owner == game.MyId;
            if (!v.Landed && age >= Game.BaFall)
            {
                v.Landed = true;
                if (mine)
                    Shake(0.7f);
                Ring(z.Pos, 150f, 0.5f, BaOak);
                if (Juicy)
                {
                    Sparks(z.Pos, new Rgba(0.68f, 0.55f, 0.4f), 26, 320f, 0.7f, 4f);
                    Sparks(z.Pos, Game.BaGold, 18, 520f, 0.45f, 2f);
                }
            }
            v.Max = Mathf.Max(v.Max, z.BoltsLeft);
            if (z.BoltsLeft < v.Bolts && v.Landed)  // a bolt left the bow
            {
                v.Recoil = 1f;
                v.Flash = 0.09f;
                V2 f = V2.FromAngle(z.Aim);
                if (Juicy)
                    Sparks(z.Pos + f * (Game.BaMuzzle + 30f), Game.BaGold, 8, 420f, 0.25f, 1.6f);
                Ring(z.Pos + f * (Game.BaMuzzle + 30f), 40f, 0.22f, Game.BaGold.WithAlpha(0.7f));
                if (rider != null && rider == game.Me)
                    Shake(0.25f);
                sounds?.Play(new SoundCue { Sfx = Sfx.ShotCannon, Ally = !mine, Quiet = mine ? 0f : game.QuietAt(z.Pos) }, Time.unscaledTime);
            }
            v.Bolts = z.BoltsLeft;
            if (z.GuardFrac < v.Frac - 0.001f)
            {
                v.HitT = 0.12f;
                if (Juicy)
                    Sparks(z.Pos + new V2(Rnd(-30f, 30f), Rnd(-30f, 30f)), BaOak, 4, 240f, 0.3f, 3f);
            }
            v.Frac = z.GuardFrac;
            if (!v.Broke && z.Radius <= 0f && v.Landed)
            {
                v.Broke = true;
                if (mine)
                    Shake(0.4f);
                if (Juicy)
                    Sparks(z.Pos, new Rgba(0.68f, 0.55f, 0.4f), 30, 380f, 0.8f, 4f);
                sounds?.Play(new SoundCue { Sfx = Sfx.CoverBreak, Ally = !mine, Quiet = mine ? 0f : game.QuietAt(z.Pos) }, Time.unscaledTime);
            }
            v.Recoil = Mathf.Max(0f, v.Recoil - dt * 5f);
            v.Flash = Mathf.Max(0f, v.Flash - dt);
            v.HitT = Mathf.Max(0f, v.HitT - dt);
            if (baFrame % 120 == 0)  // forget ballistas that are gone
            {
                foreach (var kv in baVis)
                    if (!game.Zones.ContainsKey(kv.Key))
                        baGone.Add(kv.Key);
                foreach (int id in baGone)
                    baVis.Remove(id);
                baGone.Clear();
            }
            return v;
        }

        /// A ballista on the floor (WorldEnemies' zone pass, under the players: its rider sits on it).
        void DrawBallista(Zone z, float t)
        {
            var v = BallistaVis(z, t);
            V2 c = z.Pos;
            float age = Game.BaAge(z);
            if (age < Game.BaFall)  // falling: its shadow grows, the landing's reach blinks, the frame drops in
            {
                float q = age / Game.BaFall, h = (1f - q) * (1f - q) * 420f;
                Circle(c + new V2(6f, 10f), 40f + 46f * q, new Rgba(0f, 0f, 0f, 0.2f + 0.4f * q), 24);
                float blink = 0.5f + 0.5f * Mathf.Sin(age * 30f);
                for (int i = 0; i < 16; i++)  // a dashed ring
                    Arc(c, Game.BaSlamR, 2f, BaRed.WithAlpha(blink), 4, i * MathX.Tau / 16f, MathX.Tau / 32f);
                DrawBallistaFrame(z, v, c + new V2(0f, -h), 1.3f - 0.3f * q, 0.9f);
                return;
            }
            if (z.Radius <= 0f)  // breaking: the planks fly apart
            {
                DrawBallistaWreck(z, c);
                return;
            }
            Circle(c + new V2(8f, 14f), 72f, new Rgba(0f, 0f, 0f, 0.3f), 28);
            if (Juicy && v.Flash > 0f)
            {
                V2 f = V2.FromAngle(z.Aim);
                FanGradient(c + f * (Game.BaMuzzle + 20f), 160f, new Rgba(1f, 0.85f, 0.5f, 0.16f * v.Flash / 0.09f), new Rgba(1f, 0.6f, 0.2f, 0f), 20);
            }
            DrawBallistaFrame(z, v, c, 1f, 1f);
            DrawBallistaGauges(z, v, c, t);
        }

        /// The platform, the bow, the string, the loaded bolt and the windlass, at this spot and scale.
        void DrawBallistaFrame(Zone z, BaVis v, V2 c, float sc, float alpha)
        {
            float k = BaK * sc, half = 46f * k;
            // The oak platform: four planks, iron corners and rivets.
            SetColor(BaOakDeep.WithAlpha(alpha));
            Quad(c + new V2(-half, -half), c + new V2(half, -half), c + new V2(half, half), c + new V2(-half, half));
            for (int i = 0; i < 4; i++)
            {
                float y0 = -half + 3f * k + i * (2f * half - 6f * k) / 4f, y1 = y0 + (2f * half - 6f * k) / 4f - 3f * k;
                SetColor((i % 2 == 0 ? BaOak : BaOakDark).WithAlpha(alpha));
                Quad(c + new V2(-half + 3f * k, y0), c + new V2(half - 3f * k, y0), c + new V2(half - 3f * k, y1), c + new V2(-half + 3f * k, y1));
            }
            for (int i = 0; i < 4; i++)
            {
                V2 corner = c + new V2(i % 2 == 0 ? -half : half, i < 2 ? -half : half), d = (c - corner).Normalized() * (7f * k);
                SetColor(BaIron.WithAlpha(alpha));
                V2 m = corner + d;
                Quad(m + new V2(-8f * k, -8f * k), m + new V2(8f * k, -8f * k), m + new V2(8f * k, 8f * k), m + new V2(-8f * k, 8f * k));
                Circle(m, 2.5f * k, BaSteel.WithAlpha(alpha), 8);
            }

            // The bow, turning with Aim, kicked back by each shot.
            V2 f = V2.FromAngle(z.Aim), s = f.Orthogonal();
            V2 o = c - f * (v.Recoil * 8f * k);
            float reload = Mathf.Max(0.01f, Game.BaReload), pull = z.BoltsLeft > 0 ? 1f - Mathf.Clamp01(z.GunCd / reload) : 1f;
            float nut = -34f + (1f - pull) * 34f;
            // the stock
            Line(HeliPt(o, f, s, k, -60f, 0f), HeliPt(o, f, s, k, 84f, 0f), 18f * k, BaOakDeep.WithAlpha(alpha));
            Line(HeliPt(o, f, s, k, -58f, 0f), HeliPt(o, f, s, k, 82f, 0f), 12f * k, BaOak.WithAlpha(alpha));
            Line(HeliPt(o, f, s, k, -20f, 0f), HeliPt(o, f, s, k, 82f, 0f), 3f * k, BaOakDeep.WithAlpha(0.6f * alpha));  // the groove
            // the arms: a curve each side, steel over oak
            for (int side = -1; side <= 1; side += 2)
            {
                V2 prev = HeliPt(o, f, s, k, 66f, 0f);
                for (int i = 1; i <= 8; i++)
                {
                    float u = i / 8f, x = Mathf.Lerp(66f, 22f, u) + 20f * u * (1f - u), y = side * 92f * u;
                    V2 at = HeliPt(o, f, s, k, x, y);
                    Line(prev, at, 12f * k * (1f - 0.4f * u), BaOakDeep.WithAlpha(alpha));
                    Line(prev, at, 5f * k * (1f - 0.4f * u), BaSteel.WithAlpha(alpha));
                    prev = at;
                }
            }
            // the string, from each arm's tip to the nut
            V2 tipL = HeliPt(o, f, s, k, 22f, -92f), tipR = HeliPt(o, f, s, k, 22f, 92f), nutAt = HeliPt(o, f, s, k, nut, 0f);
            Line(tipL, nutAt, 2f * k, BaRope.WithAlpha(alpha));
            Line(tipR, nutAt, 2f * k, BaRope.WithAlpha(alpha));
            // the loaded bolt
            if (z.BoltsLeft > 0 && pull > 0.95f)
                DrawBallistaBoltShape(HeliPt(o, f, s, k, nut + 2f, 0f), f, 0.9f * k, alpha);
            // the hub and the windlass
            Circle(o, 13f * k, BaIron.WithAlpha(alpha), 14);
            Circle(o, 6f * k, BaSteel.WithAlpha(alpha), 10);
            Line(HeliPt(o, f, s, k, -64f, -20f), HeliPt(o, f, s, k, -64f, 20f), 9f * k, BaIron.WithAlpha(alpha));
            Line(HeliPt(o, f, s, k, -70f, -24f), HeliPt(o, f, s, k, -70f, 24f), 4f * k, BaSteel.WithAlpha(alpha));
            if (v.HitT > 0f)
                Circle(c, 50f * k, new Rgba(1f, 1f, 1f, 0.35f * v.HitT / 0.12f), 18);
        }

        /// Under it: a pip a bolt, its time left and its health; round it, the reload.
        void DrawBallistaGauges(Zone z, BaVis v, V2 c, float t)
        {
            int n = Mathf.Max(v.Max, z.BoltsLeft);
            float w = 8f, gap = 3f, total = n * (w + gap) - gap, y = 70f;
            for (int i = 0; i < n; i++)
            {
                V2 a = c + new V2(-total * 0.5f + i * (w + gap), y);
                SetColor(new Rgba(0f, 0f, 0f, 0.6f));
                Quad(a + new V2(-1f, -1f), a + new V2(w + 1f, -1f), a + new V2(w + 1f, 13f), a + new V2(-1f, 13f));
                SetColor(i < z.BoltsLeft ? Game.BaGold : BaPipOff);
                Quad(a, a + new V2(w, 0f), a + new V2(w, 12f), a + new V2(0f, 12f));
            }
            float life = Mathf.Clamp01((z.T - Game.BaBreakT) / Game.BaLife), hp = Mathf.Clamp01(z.GuardFrac), bw = Mathf.Max(80f, total);
            V2 l0 = c + new V2(-bw * 0.5f, y + 19f), l1 = c + new V2(bw * 0.5f, y + 19f);
            Line(l0, l1, 4f, new Rgba(0f, 0f, 0f, 0.6f));
            Line(l0, l0 + (l1 - l0) * life, 2.5f, (life < 0.2f && Mathf.Sin(t * 20f) > 0f ? BaRed : BaSteel));
            V2 h0 = c + new V2(-bw * 0.5f, y + 25f), h1 = c + new V2(bw * 0.5f, y + 25f);
            Line(h0, h1, 4f, new Rgba(0f, 0f, 0f, 0.6f));
            if (hp > 0f)
                Line(h0, h0 + (h1 - h0) * hp, 2.5f, v.HitT > 0f ? Rgba.White : Game.BaGold.Mix(BaRed, 1f - hp));
            if (z.BoltsLeft > 0 && z.GunCd > 0f)
            {
                float frac = 1f - Mathf.Clamp01(z.GunCd / Game.BaReload);
                Arc(c, 60f, 3f, Game.BaGold.WithAlpha(0.9f), 36, -MathX.Pi * 0.5f, MathX.Tau * frac);
            }
        }

        /// The last BaBreakT s: the planks, stock and arms thrown apart, spinning and fading.
        void DrawBallistaWreck(Zone z, V2 c)
        {
            float age = Game.BaBreakT - Mathf.Max(0f, z.T), q = Mathf.Clamp01(age / Game.BaBreakT), a = 1f - q;
            int seed = z.Id * 7919;
            for (int i = 0; i < 16; i++)
            {
                float r1 = Hash01(seed + i * 3), r2 = Hash01(seed + i * 3 + 1), r3 = Hash01(seed + i * 3 + 2);
                V2 d = V2.FromAngle(r1 * MathX.Tau);
                float dist = (60f + 160f * r2) * (1f - (1f - q) * (1f - q)), spin = (r3 - 0.5f) * 14f * age + r1 * 6f;
                V2 at = c + d * (12f + dist), pd = V2.FromAngle(spin) * (i < 4 ? 24f : 12f);
                Line(at - pd, at + pd, i < 4 ? 8f : 6f, (i < 4 ? BaOakDark : i % 3 == 0 ? BaIron : BaOak).WithAlpha(a));
            }
            Circle(c, 50f * (1f + q), new Rgba(0.68f, 0.55f, 0.4f, 0.25f * a), 20);
        }

        /// A giant bolt: shaft, red fletching, a steel head with a gold collar.
        void DrawBallistaBoltShape(V2 tail, V2 dir, float sc, float alpha)
        {
            V2 side = dir.Orthogonal();
            float len = 86f * sc;
            V2 head = tail + dir * len;
            Line(tail, head, 7f * sc, BaOakDeep.WithAlpha(alpha));
            Line(tail, head, 4.5f * sc, BaShaft.WithAlpha(alpha));
            SetColor(BaSteel.WithAlpha(alpha));
            Tri(head + side * (10f * sc), head + dir * (24f * sc), head - side * (10f * sc));
            Line(head - dir * (2f * sc), head + dir * (2f * sc), 10f * sc, Game.BaGold.WithAlpha(alpha));
            SetColor(BaRed.WithAlpha(alpha));
            for (int g = -1; g <= 1; g += 2)
                Tri(tail + side * (2f * g * sc), tail + side * (12f * g * sc) - dir * (10f * sc), tail + side * (2f * g * sc) + dir * (14f * sc));
        }

        /// A bolt in flight (WorldShots): a gold glow down its path and the bolt at its head.
        void DrawBallistaBolt(Bullet b)
        {
            V2 dir = b.Vel.Normalized();
            float len = Mathf.Min(160f, Mathf.Max(0f, (b.Life0 - b.Life) * b.Vel.Length));
            Line(b.Pos - dir * len, b.Pos, 18f, Game.BaGold.WithAlpha(0.12f));
            Line(b.Pos - dir * len, b.Pos, 6f, BaGoldHot.WithAlpha(0.35f));
            DrawBallistaBoltShape(b.Pos - dir * 100f, dir, 1.1f, 1f);
        }

        /// DrawHud: by a free ballista, the key to climb on; on one, the key to get off and how many bolts are left.
        void DrawBallistaHint()
        {
            var me = game.Me;
            if (me == null || me.Downed)
                return;
            var ridden = game.BallistaOf(me);
            var z = ridden ?? game.BallistaNear(me.Pos);
            if (z == null)
                return;
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.Interact)).ToUpperInvariant();
            string text = ridden != null ? key + "   LEAVE  ·  " + z.BoltsLeft + (z.BoltsLeft == 1 ? " BOLT" : " BOLTS") : key + "   MOUNT BALLISTA";
            V2 at = ToUi(z.Pos);
            var r = new Rect(at.X - 90f, at.Y - 118f, 180f, 22f);
            Ui.Pill(r, ridden != null ? new Color(0f, 0f, 0f, 0.6f) : CrtStyle.Gold.WithAlpha(0.9f));
            Ui.Label(r, text, 11, ridden != null ? CrtStyle.Gold : new Color(0.06f, 0.05f, 0.03f), TextAnchor.MiddleCenter, true);
        }
    }
}
