using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Static Shock's look (Core/StaticShock.cs), ported from the owner's pick, the "Static Shock" mockup artifact, with
    /// its numbers. Drawing only.
    ///   - the cone: a fan of pale blue light from your hand (bright there, gone at its reach, a faint rim), seven crackling
    ///     filaments re-struck 18 times a second, and a flare on your hand;
    ///   - the circuit: a jagged bolt along each link (soft halo, blue glow, white core), worked out on this screen from
    ///     where the enemies are drawn (Game.StaticCircuit, the host's own rule); gold bolts to teammates in the cone;
    ///     each wired enemy glows, flickers and throws sparks;
    ///   - Overcharge: a gold glow pulsing round the teammate, gold arcs jumping off them, a trail of gold light behind,
    ///     a gold ring draining over its 10 s with the seconds under it, and an "OVERCHARGE" pop with a burst of sparks;
    ///   - the Arc Cannon (FxKind.ArcBall): a violet ball with a white core, a pulsing rim and arcs inside, a trail of light
    ///     and sparks; its burst (FxKind.ArcBurst): a flash, an easing ring, ten radial bolts, a violet scorch and sparks,
    ///     and small arcs crackling on the enemies it stunned.
    /// With Visual effects off: the cone's outline, straight links, a plain ball, a ring for the burst and the gold ring.
    public partial class ArenaApp
    {
        static readonly Rgba ShkFanIn = new Rgba(0.745f, 0.92f, 1f), ShkFanMid = new Rgba(0.43f, 0.745f, 1f), ShkRim = new Rgba(0.63f, 0.86f, 1f);
        static readonly Rgba ShkCrackGlow = new Rgba(0.49f, 0.78f, 1f), ShkCrackCore = new Rgba(0.91f, 0.97f, 1f);
        static readonly Rgba ShkLinkGlow = new Rgba(0.435f, 0.816f, 1f), ShkHand = new Rgba(0.78f, 0.94f, 1f), ShkEnemyGlow = new Rgba(0.55f, 0.86f, 1f);
        static readonly Rgba ShkSparkCol = new Rgba(0.75f, 0.93f, 1f);
        static readonly Rgba OcGlow = new Rgba(1f, 0.745f, 0.275f), OcLinkGlow = new Rgba(1f, 0.79f, 0.3f), OcCore = new Rgba(1f, 0.965f, 0.847f), OcSpark = new Rgba(1f, 0.843f, 0.455f);
        static readonly Rgba AcHalo = new Rgba(0.55f, 0.43f, 1f), AcInner = new Rgba(0.745f, 0.86f, 1f), AcRim = new Rgba(0.784f, 0.706f, 1f), AcBolt = new Rgba(0.71f, 0.61f, 1f);
        static readonly Rgba AcTrail = new Rgba(0.59f, 0.47f, 1f), AcSpark = new Rgba(0.85f, 0.78f, 1f), AcRing = new Rgba(0.85f, 0.8f, 1f), AcBurstBolt = new Rgba(0.65f, 0.55f, 1f);
        static readonly Rgba AcFlash = new Rgba(0.92f, 0.9f, 1f), AcBurstSpark = new Rgba(0.9f, 0.86f, 1f), AcInnerRing = new Rgba(0.62f, 0.89f, 1f);
        const float ShkStrike = 18f;
        static readonly Rgba ShkHotLink = new Rgba(0.82f, 0.95f, 1f);  // a fully ramped link's glow: nearly white
        static Rgba ShkHotGlow(float heat) => new Rgba(ShkLinkGlow.R + (ShkHotLink.R - ShkLinkGlow.R) * heat, ShkLinkGlow.G + (ShkHotLink.G - ShkLinkGlow.G) * heat, ShkLinkGlow.B + (ShkHotLink.B - ShkLinkGlow.B) * heat);  // the bolts are re-struck this many times a second

        // ---- lightning geometry: midpoint displacement, five levels (33 points), seeded so a seed holds its shape

        readonly V2[] shkBolt = new V2[33], shkBoltTmp = new V2[33];

        int ShkBoltPts(V2 a, V2 b, float disp, uint seed)
        {
            var pts = shkBolt;
            pts[0] = a;
            pts[1] = b;
            int n = 2;
            for (int k = 0; k < 5; k++)
            {
                int m = 0;
                for (int i = 0; i < n - 1; i++)
                {
                    V2 p = pts[i], q = pts[i + 1], d = q - p;
                    float l = Mathf.Max(0.001f, d.Length), o = (Rand01(ref seed) * 2f - 1f) * disp;
                    shkBoltTmp[m++] = p;
                    shkBoltTmp[m++] = (p + q) * 0.5f + new V2(-d.Y / l, d.X / l) * o;
                }
                shkBoltTmp[m++] = pts[n - 1];
                System.Array.Copy(shkBoltTmp, pts, m);
                n = m;
                disp *= 0.55f;
            }
            return n;
        }

        /// A bolt: a soft halo along it, a glowing jagged band and a hot core (the mockup's three strokes).
        void ShkDrawBolt(V2 a, V2 b, float disp, uint seed, Rgba glow, Rgba core, float w, float alpha)
        {
            if (alpha <= 0.01f || (b - a).Length < 1f)
                return;
            int n = ShkBoltPts(a, b, disp, seed);
            SoftLine(a, b, w * 9f, glow.WithAlpha(0.16f * alpha));
            for (int i = 0; i < n - 1; i++)
                Line(shkBolt[i], shkBolt[i + 1], w * 3.5f, glow.WithAlpha(0.28f * alpha));
            for (int i = 0; i < n - 1; i++)
                Line(shkBolt[i], shkBolt[i + 1], w, core.WithAlpha(alpha));
        }

        static uint ShkSeed(int a, int b, int c) => (uint)(a * 73856093 ^ b * 19349663 ^ c * 83492791) | 1u;
        int ShkFrame(float t) => Mathf.FloorToInt(t * ShkStrike);

        // ---- the circuit, as this screen sees it

        readonly List<Enemy> shkIn = new List<Enemy>();
        readonly List<int> shkFrom = new List<int>();
        readonly HashSet<int> shkWired = new HashSet<int>();
        // How long each enemy (by id) and room turret (by where it stands) has been wired on this screen, the shock's
        // ramp as this screen sees it: the bolts to it thicken and run hotter as the shock grows (Game.StaticRamp).
        readonly Dictionary<long, float> shkHeat = new Dictionary<long, float>(), shkHeatSeen = new Dictionary<long, float>();
        readonly List<long> shkHeatGone = new List<long>();
        float shkClock;

        static long ShkTurretKey(V2 p) => long.MinValue + ((long)(int)Mathf.Round(p.X) << 24) + (int)Mathf.Round(p.Y) + 10000000;

        /// 0 to 1: how far along its ramp the shock on this target is.
        float ShkHeatOf(long key)
        {
            float h;
            return shkHeat.TryGetValue(key, out h) ? Mathf.Clamp01(h / Game.StaticRamp) : 0f;
        }

        void ShkWarm(long key, float dt)
        {
            float h;
            shkHeat.TryGetValue(key, out h);
            shkHeat[key] = h + dt;
            shkHeatSeen[key] = shkClock;
        }

        bool ShkShowable(Enemy e) => e.Hp > 0f && !game.IsSpawning(e);

        /// The circuit of player p's cone on this screen (into shkIn / shkFrom).
        void ShkCircuit(Player p)
        {
            Game.StaticCircuit(Game.StaticHand(p, p.Disp), p.Aim, Game.StaticRangeOf(p), game.Enemies.Values, e => e.Disp, ShkShowable,
                Game.StaticLinks + game.ExtraShots(p), shkIn, shkFrom);
        }

        static bool ShkConeUp(Player p) => p.StaticOn && !p.Downed && p.Weapon.Id == WeaponId.StaticShock;

        // ---- per frame: sparks, the gold trails, new effects

        readonly Dictionary<int, List<V2>> ocTrail = new Dictionary<int, List<V2>>();
        readonly HashSet<Effect> shkSeen = new HashSet<Effect>();
        readonly Dictionary<Effect, HashSet<int>> acPassed = new Dictionary<Effect, HashSet<int>>();
        readonly List<Effect> shkGone = new List<Effect>();

        /// Juice.UpdateJuice (Visual effects on): sparks off wired enemies and the ball, bursts of sparks for new effects,
        /// and each Overcharged teammate's trail.
        void UpdateStaticShockLook(float dt)
        {
            if (dt <= 0f)
                return;
            shkWired.Clear();
            shkClock += dt;
            foreach (var p in game.Players.Values)
            {
                if (!ShkConeUp(p))
                    continue;
                ShkCircuit(p);
                foreach (var tr in game.RoomTrapList)
                    if (tr.Kind == TrapKind.Turret && Game.InStaticCone(Game.StaticHand(p, p.Disp), p.Aim, Game.StaticRangeOf(p), tr.Pos, RoomTraps.TurretHitR))
                        ShkWarm(ShkTurretKey(tr.Pos), dt);
                foreach (var e in shkIn)
                {
                    shkWired.Add(e.Id);
                    ShkWarm(e.Id, dt);
                    if (Rnd(0f, 1f) < dt * (14f + 20f * ShkHeatOf(e.Id)))
                        AddPart(new Part { Kind = PSpark, Pos = e.Disp + new V2(Rnd(-e.Radius, e.Radius), Rnd(-e.Radius, e.Radius)),
                            Vel = V2.FromAngle(Rnd(0f, MathX.Tau)) * Rnd(40f, 140f), Life = Rnd(0.25f, 0.6f), Max = 0.6f, Size = 1.8f, Col = ShkSparkCol });
                }
            }
            shkHeatGone.Clear();
            foreach (var kv in shkHeatSeen)
                if (shkClock - kv.Value > Game.StaticForget)
                    shkHeatGone.Add(kv.Key);
            foreach (long k in shkHeatGone)
            {
                shkHeat.Remove(k);
                shkHeatSeen.Remove(k);
            }
            foreach (var p in game.Players.Values)
            {
                List<V2> tr;
                if (!ocTrail.TryGetValue(p.Id, out tr))
                    ocTrail[p.Id] = tr = new List<V2>();
                if (p.OverchargeT > 0f && !p.Downed)
                {
                    tr.Insert(0, p.Disp);
                    if (tr.Count > 14)
                        tr.RemoveAt(14);
                }
                else
                    tr.Clear();
            }
            foreach (var fx in game.Effects)
            {
                bool fresh = shkSeen.Add(fx);
                if (fx.Kind == FxKind.ArcBall)
                {
                    float dur = Mathf.Max(0.05f, fx.Dur);
                    V2 at = Game.ArcBallAt(fx.Pos, fx.Pos2, fx.T, dur), dir = (fx.Pos2 - fx.Pos).Normalized();
                    if (fresh)
                        for (int i = 0; i < 14; i++)
                            ShkSpark(fx.Pos, dir.Angle + Rnd(-0.6f, 0.6f), Rnd(150f, 380f), AcSpark);
                    if (fx.T < dur && Rnd(0f, 1f) < dt * 40f)
                        ShkSpark(at, dir.Angle + MathX.Pi + Rnd(-0.8f, 0.8f), Rnd(60f, 160f), new Rgba(0.8f, 0.72f, 1f));
                    HashSet<int> passed;
                    if (!acPassed.TryGetValue(fx, out passed))
                        acPassed[fx] = passed = new HashSet<int>();
                    foreach (var e in game.Enemies.Values)
                        if (fx.T < dur && ShkShowable(e) && !passed.Contains(e.Id) && e.Disp.WithinReach(at, Game.CannonBallR + e.Radius))
                        {
                            passed.Add(e.Id);
                            for (int i = 0; i < 8; i++)
                                ShkSpark(e.Disp, Rnd(0f, MathX.Tau), Rnd(80f, 220f), AcSpark);
                        }
                }
                else if (fresh && fx.Kind == FxKind.ArcBurst)
                {
                    for (int i = 0; i < 40; i++)
                        ShkSpark(fx.Pos, Rnd(0f, MathX.Tau), Rnd(120f, 520f), i % 2 == 0 ? AcBurstSpark : ShkFanIn);
                    var me = game.Me;
                    if (me != null && me.Disp.WithinReach(fx.Pos, 520f))
                        Shake(0.18f);
                }
                else if (fresh && fx.Kind == FxKind.Overcharge)
                {
                    Player q;
                    V2 at = game.Players.TryGetValue((int)fx.Pos2.X, out q) ? q.Disp : fx.Pos;
                    for (int i = 0; i < 16; i++)
                        ShkSpark(at, Rnd(0f, MathX.Tau), Rnd(80f, 260f), OcSpark);
                }
            }
            shkGone.Clear();
            foreach (var fx in shkSeen)
                if (!game.Effects.Contains(fx))
                    shkGone.Add(fx);
            foreach (var fx in shkGone)
            {
                shkSeen.Remove(fx);
                acPassed.Remove(fx);
            }
        }

        void ShkSpark(V2 at, float angle, float speed, Rgba col) =>
            AddPart(new Part { Kind = PSpark, Pos = at, Vel = V2.FromAngle(angle) * speed, Life = Rnd(0.25f, 0.6f), Max = 0.6f, Size = 1.8f, Col = col });

        // ---- the glow pass

        /// Juice.DrawGlowPass: the cones and their circuits, Overcharge's light, the balls and bursts.
        void DrawStaticShockGlow()
        {
            float t = game.AnimT;
            int fr = ShkFrame(t);
            foreach (var p in game.Players.Values)
                if (p.OverchargeT > 0f && !p.Downed && !LaunchHides(p))
                    DrawOverchargeGlow(p, t, fr);
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.ArcBall)
                    DrawArcBallGlow(fx, fr);
                else if (fx.Kind == FxKind.ArcBurst)
                    DrawArcBurstGlow(fx, fr);
            }
            foreach (var p in game.Players.Values)
                if (ShkConeUp(p) && !LaunchHides(p))
                    DrawStaticCone(p, t, fr);
        }

        void DrawStaticCone(Player p, float t, int fr)
        {
            V2 h = Game.StaticHand(p, p.Disp);
            float R = Game.StaticRangeOf(p), aim = p.Aim, half = Game.StaticHalf;
            uint fs = ShkSeed(fr, p.Id, 7);
            float fl = 0.75f + 0.25f * Rand01(ref fs);
            // The fan: three rings (the hand, 55% out, the reach), hard-edged like the mockup's wedge.
            int n = 16;
            for (int i = 0; i < n; i++)
            {
                V2 d0 = V2.FromAngle(aim - half + 2f * half * i / n), d1 = V2.FromAngle(aim - half + 2f * half * (i + 1) / n);
                Rgba c0 = ShkFanIn.WithAlpha(0.42f * fl), c1 = ShkFanMid.WithAlpha(0.14f * fl), c2 = ShkFanMid.WithAlpha(0f);
                VTri(h + d0 * 4f, c0, h + d0 * (R * 0.55f), c1, h + d1 * (R * 0.55f), c1);
                VTri(h + d0 * 4f, c0, h + d1 * (R * 0.55f), c1, h + d1 * 4f, c0);
                VTri(h + d0 * (R * 0.55f), c1, h + d0 * R, c2, h + d1 * R, c2);
                VTri(h + d0 * (R * 0.55f), c1, h + d1 * R, c2, h + d1 * (R * 0.55f), c1);
            }
            Arc(h, R, 1f, ShkRim.WithAlpha(0.22f * fl), 24, aim - half, 2f * half);
            // Seven crackling filaments inside it.
            for (int i = 0; i < 7; i++)
            {
                uint s = ShkSeed(fr, p.Id * 16 + i, 11);
                float a = aim + (Rand01(ref s) * 2f - 1f) * half, L = (0.35f + 0.65f * Rand01(ref s)) * R;
                ShkDrawBolt(h, h + V2.FromAngle(a) * L, L * 0.12f, s, ShkCrackGlow, ShkCrackCore, 1f, 0.55f);
            }
            // The circuit: hand to enemy, enemy to enemy.
            ShkCircuit(p);
            for (int i = 0; i < shkIn.Count; i++)
            {
                V2 from = shkFrom[i] < 0 ? h : shkIn[shkFrom[i]].Disp, to = shkIn[i].Disp;
                float heat = ShkHeatOf(shkIn[i].Id);  // the bolt thickens and whitens as the shock grows
                ShkDrawBolt(from, to, Mathf.Min(28f, (to - from).Length * 0.18f), ShkSeed(fr, shkIn[i].Id, 3), ShkHotGlow(heat), Rgba.White, 1.8f + 1.0f * heat, 1f);
                uint es = ShkSeed(fr, shkIn[i].Id, 5);
                GlowDisc(to, shkIn[i].Radius * (2.4f + 1.2f * heat), ShkEnemyGlow, (0.4f + 0.3f * heat) * (0.6f + 0.4f * Rand01(ref es)));
                if (Rand01(ref es) < 0.5f)
                    Arc(to, shkIn[i].Radius, 2f, new Rgba(0.74f, 0.94f, 1f, 0.8f), 20);
            }
            // Room turrets in it take the shock too: a bolt from the hand.
            foreach (var tr in game.RoomTrapList)
            {
                if (tr.Kind != TrapKind.Turret || !Game.InStaticCone(h, aim, R, tr.Pos, RoomTraps.TurretHitR))
                    continue;
                long key = ShkTurretKey(tr.Pos);
                float heat = ShkHeatOf(key);
                ShkDrawBolt(h, tr.Pos, Mathf.Min(28f, (tr.Pos - h).Length * 0.18f), ShkSeed(fr, (int)(key & 0xffffff), 23), ShkHotGlow(heat), Rgba.White, 1.8f + 1.0f * heat, 1f);
                GlowDisc(tr.Pos, RoomTraps.TurretHitR * (1.8f + 0.8f * heat), ShkEnemyGlow, 0.4f + 0.3f * heat);
            }
            // Gold bolts to teammates in it.
            foreach (var q in game.Players.Values)
                if (q != p && !q.Downed && Game.InStaticCone(h, aim, R, q.Disp, q.Def.BodyR))
                    ShkDrawBolt(h, q.Disp, 14f, ShkSeed(fr, q.Id, 9), OcLinkGlow, OcCore, 1.8f, 1f);
            GlowDisc(h, 26f, ShkHand, 0.9f * fl);
        }

        void DrawOverchargeGlow(Player p, float t, int fr)
        {
            float r = p.Def.BodyR, fade = p.OverchargeT < 1.5f && Mathf.Sin(t * 30f) > 0.3f ? 0.5f : 1f;
            List<V2> tr;
            if (ocTrail.TryGetValue(p.Id, out tr))
                for (int i = 0; i < tr.Count; i += 2)
                    GlowDisc(tr[i], r * 1.3f, OcGlow, 0.35f * (1f - i / 14f) * 0.7f * fade);
            GlowDisc(p.Disp, r * 3.2f, OcGlow, 0.45f * (0.75f + 0.25f * Mathf.Sin(t * 12f)) * fade);
            uint s = ShkSeed(fr, p.Id, 21);
            if (Rand01(ref s) < 0.5f)
            {
                float a = Rand01(ref s) * MathX.Tau;
                V2 d = V2.FromAngle(a);
                ShkDrawBolt(p.Disp + d * r, p.Disp + d * (r * 2.1f), 4f, s, OcLinkGlow, OcCore, 1.1f, fade);
            }
        }

        void DrawArcBallGlow(Effect fx, int fr)
        {
            float dur = Mathf.Max(0.05f, fx.Dur), age = fx.T, R = Game.CannonBallR;
            if (age >= dur)
                return;
            V2 at = Game.ArcBallAt(fx.Pos, fx.Pos2, age, dur);
            for (int i = 17; i >= 0; i--)  // the trail: where it was the last 18 frames
            {
                float back = age - i / 60f;
                if (back < 0f)
                    continue;
                GlowDisc(Game.ArcBallAt(fx.Pos, fx.Pos2, back, dur), R * (1.3f - i / 18f), AcTrail, 0.22f * (1f - i / 18f));
            }
            GlowDisc(at, R * 3.4f, AcHalo, 0.35f);
            GlowDisc(at, R * 1.6f, AcInner, 0.9f);
            float pulse = 1f + 0.08f * Mathf.Sin(age * 30f);
            Circle(at, R * 0.45f * pulse, Rgba.White.WithAlpha(0.95f), 20);
            Arc(at, R * pulse, 1.5f, AcRim.WithAlpha(0.6f), 28);
            int bf = Mathf.FloorToInt(age * 20f);
            for (int i = 0; i < 5; i++)
            {
                uint s = ShkSeed(bf, (int)(fx.Pos.X * 3f) + i, 13);
                float a = Rand01(ref s) * MathX.Tau, L = (0.6f + 0.65f * Rand01(ref s)) * R;
                ShkDrawBolt(at, at + V2.FromAngle(a) * L, 6f, s, AcBolt, Rgba.White, 1.4f, 1f);
            }
        }

        void DrawArcBurstGlow(Effect fx, int fr)
        {
            float T = fx.T, R = fx.Radius, k = Mathf.Clamp01(T / 0.35f), e = 1f - (1f - k) * (1f - k) * (1f - k);
            GlowDisc(fx.Pos, R * 1.2f, AcHalo, 0.35f * (1f - T / Game.CannonBurstFx));
            if (T < 0.12f)
                GlowDisc(fx.Pos, R * 1.5f, AcFlash, 0.9f * (1f - T / 0.12f));
            float ra = Mathf.Max(0f, 1f - T / 0.6f);
            if (ra > 0f)
            {
                Arc(fx.Pos, R * e, 10f * (1f - k) + 2f, AcRing.WithAlpha(ra), 48);
                Arc(fx.Pos, R * e * 0.7f, 2f, AcInnerRing.WithAlpha(ra * 0.6f), 40);
            }
            if (T < 0.45f)
            {
                int bf = Mathf.FloorToInt(T * 20f);
                for (int i = 0; i < 10; i++)
                {
                    uint s = ShkSeed(bf, (int)(fx.Pos.X * 3f + fx.Pos.Y) + i, 17);
                    float a = i / 10f * MathX.Tau + (Rand01(ref s) - 0.5f) * 0.4f, L = R * (0.7f + 0.35f * Rand01(ref s));
                    ShkDrawBolt(fx.Pos, fx.Pos + V2.FromAngle(a) * L, 16f, s, AcBurstBolt, Rgba.White, 1.8f, 1f - T / 0.45f);
                }
            }
            // The enemies it stunned crackle while the stun lasts.
            if (T < Game.CannonStun)
                foreach (var en in game.Enemies.Values)
                {
                    if (!ShkShowable(en) || en.Def.IsBoss || en.Def.Heavy || !en.Disp.WithinReach(fx.Pos, R + en.Radius))
                        continue;
                    GlowDisc(en.Disp, en.Radius * 2.6f, ShkEnemyGlow, 0.35f);
                    for (int j = 0; j < 2; j++)
                    {
                        uint s = ShkSeed(ShkFrame(game.AnimT), en.Id * 4 + j, 19);
                        V2 d = V2.FromAngle(Rand01(ref s) * MathX.Tau);
                        ShkDrawBolt(en.Disp, en.Disp + d * (en.Radius * 1.6f), 5f, s, ShkLinkGlow, Rgba.White, 1.2f, 1f);
                    }
                }
        }

        // ---- the normal pass

        /// WorldRenderer.DrawPlayerAuras: Overcharge's draining gold ring; with Visual effects off, the cone's outline and links.
        void DrawStaticShockUnder(Player p, float t)
        {
            if (p.OverchargeT > 0f)
            {
                float k = Mathf.Clamp01(p.OverchargeT / Game.OverchargeTime), R = p.Def.BodyR + 9f;
                float fade = p.OverchargeT < 1.5f && Mathf.Sin(t * 30f) > 0.3f ? 0.5f : 1f;
                Arc(p.Disp, R, 3f, Game.OverchargeGold.WithAlpha(0.18f * fade), 32);
                Arc(p.Disp, R, 3f, Game.OverchargeGold.WithAlpha(fade), 32, -MathX.Pi / 2f, MathX.Tau * k);
            }
            if (fxOn || !ShkConeUp(p))
                return;
            V2 h = Game.StaticHand(p, p.Disp);
            float Rr = Game.StaticRangeOf(p), half = Game.StaticHalf;
            Line(h, h + V2.FromAngle(p.Aim - half) * Rr, 1.5f, Game.StaticBlue.WithAlpha(0.5f));
            Line(h, h + V2.FromAngle(p.Aim + half) * Rr, 1.5f, Game.StaticBlue.WithAlpha(0.5f));
            Arc(h, Rr, 1.5f, Game.StaticBlue.WithAlpha(0.5f), 24, p.Aim - half, 2f * half);
            ShkCircuit(p);
            for (int i = 0; i < shkIn.Count; i++)
                Line(shkFrom[i] < 0 ? h : shkIn[shkFrom[i]].Disp, shkIn[i].Disp, 2f, Game.StaticBlue);
            foreach (var q in game.Players.Values)
                if (q != p && !q.Downed && Game.InStaticCone(h, p.Aim, Rr, q.Disp, q.Def.BodyR))
                    Line(h, q.Disp, 2f, Game.OverchargeGold);
        }

        /// WorldEffects, Visual effects off only (on, the glow pass draws them): a plain ball and burst ring.
        void DrawArcBallPlain(Effect fx)
        {
            if (fxOn || fx.T >= fx.Dur)
                return;
            V2 at = Game.ArcBallAt(fx.Pos, fx.Pos2, fx.T, Mathf.Max(0.05f, fx.Dur));
            Circle(at, Game.CannonBallR, Game.CannonViolet.WithAlpha(0.5f), 24);
            Circle(at, Game.CannonBallR * 0.45f, Rgba.White, 16);
        }

        void DrawArcBurstPlain(Effect fx)
        {
            if (fxOn)
                return;
            float k = Mathf.Clamp01(fx.T / 0.35f);
            Arc(fx.Pos, fx.Radius * k, 3f, Game.CannonViolet.WithAlpha(1f - Mathf.Clamp01(fx.T / 0.6f)), 40);
        }

        // ---- HUD

        /// Hud: the seconds of Overcharge under each charged player, and the "OVERCHARGE" pop over them.
        void DrawOverchargeHud()
        {
            foreach (var p in game.Players.Values)
            {
                if (p.OverchargeT <= 0f || p.Downed || LaunchHides(p))
                    continue;
                V2 at = ToUi(p.Disp + new V2(0f, p.Def.BodyR + 24f));
                ShkLabel(at, p.OverchargeT.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s", 11, 1f);
            }
            foreach (var fx in FxOf(FxKind.Overcharge))
            {
                if (fx.Kind != FxKind.Overcharge)
                    continue;
                Player q;
                V2 w = game.Players.TryGetValue((int)fx.Pos2.X, out q) ? q.Disp : fx.Pos;
                float k = Mathf.Clamp01(fx.T / Mathf.Max(0.05f, fx.Dur));
                ShkLabel(ToUi(w) + new V2(0f, -(q != null ? q.Def.BodyR : 14f) * WorldToUi - 12f - 26f * k), "OVERCHARGE", 13, 1f - k * k);
            }
        }

        void ShkLabel(V2 at, string text, int size, float alpha)
        {
            if (alpha <= 0f)
                return;
            var r = new Rect(at.X - 100f, at.Y - 12f, 200f, 24f);
            Ui.Label(new Rect(r.x + 1f, r.y + 1.5f, r.width, r.height), text, size, new Color(0.05f, 0.03f, 0f, 0.8f * alpha), TextAnchor.MiddleCenter, true);
            Ui.Label(r, text, size, CrtStyle.Gold.WithAlpha(alpha), TextAnchor.MiddleCenter, true);
        }
    }
}
