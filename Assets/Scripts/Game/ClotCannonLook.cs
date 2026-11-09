using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The tank Hemomancer's Clot Cannon (WeaponId.ClotBolt, Core/BloodMage.cs), look A "Gore Mortar" of
    /// Docs/Mockups/clot-cannon.html. While you hold, the clot in your hand swells and your arc grows out to where it
    /// will land: a trail of beads up and over to a dashed outline of the pool and the splash inside it. The clot flies
    /// lifted along its arc over its shadow, raining drops; it lands in a crown of drops and a big pool (DrawBloodZone)
    /// that wobbles while it's wet (healing motes rise off it), with a rim that closes over the first second, then congeals: a brown crust creeps
    /// in from the edge and cracks. Drops fall from the hand as you walk (more while charging) and stain the floor.
    /// Everything comes from replicated state (Player.DrawT and Aim, the clot's Life and Life0, the pool zone's age);
    /// the drops and stains are this screen's own.
    public partial class ArenaApp
    {
        struct ClotDrop { public V2 Pos, Vel; public float Z, Vz, R; }
        struct ClotStain { public V2 Pos, S1, S2; public float Born, R, Sx, Rot, S1r, S2r; }
        struct ClotRipple { public V2 Pos; public float Born, R; }
        struct ClotSeen { public V2 Pos; public float Life; }

        readonly List<ClotDrop> clotDrops = new List<ClotDrop>();
        readonly List<ClotStain> clotStains = new List<ClotStain>();
        readonly List<ClotRipple> clotRipples = new List<ClotRipple>();
        readonly Dictionary<int, ClotSeen> clotsFlying = new Dictionary<int, ClotSeen>();  // last frame's clots: one that's gone landed
        readonly Dictionary<int, V2> clotHandAt = new Dictionary<int, V2>();
        readonly Dictionary<int, float> clotWalked = new Dictionary<int, float>(), clotIdle = new Dictionary<int, float>(), clotShownD = new Dictionary<int, float>();
        readonly Dictionary<int, float> clotFullAt = new Dictionary<int, float>();
        readonly List<int> clotScratch = new List<int>();
        float clotAt = -1f, clotEmit, clotDt;
        int clotSeed;
        const float ClotHandZ = 14f, ClotGravity = 1150f, ClotStainLife = 8f, ClotBody = 15f;
        const int ClotDropMax = 800, ClotStainMax = 600;

        static readonly Rgba ClotBright = new Rgba(0.88f, 0.19f, 0.29f), ClotMid = new Rgba(0.54f, 0.05f, 0.13f), ClotDeep = new Rgba(0.17f, 0.01f, 0.04f);
        static readonly Rgba ClotDrip = new Rgba(0.77f, 0.07f, 0.18f), ClotStainFresh = new Rgba(0.63f, 0.06f, 0.16f), ClotStainDry = new Rgba(0.35f, 0.05f, 0.07f);
        static readonly Rgba PoolHi = new Rgba(0.63f, 0.07f, 0.16f), PoolBody = new Rgba(0.42f, 0.03f, 0.09f), PoolRim = new Rgba(0.23f, 0.01f, 0.05f);
        static readonly Rgba CrustHi = new Rgba(0.35f, 0.13f, 0.09f), CrustBody = new Rgba(0.29f, 0.1f, 0.06f), CrustDark = new Rgba(0.15f, 0.05f, 0.03f);

        float ClotRoll() => SprayHash(++clotSeed * 0.613f + 0.29f);  // the look's own dice (deterministic, so previews repeat)
        static float ClotEase(float x) => 1f - (1f - x) * (1f - x) * (1f - x);
        static float ClotBack(float x) { x -= 1f; return 1f + 2.9f * x * x * x + 1.9f * x * x; }

        static V2 ClotHand(Player p)
        {
            V2 a = V2.FromAngle(p.Aim);
            return p.Disp + a * (p.Def.BodyR + 8f) + a.Orthogonal() * 6f;
        }

        /// How high a flying clot is above its shadow (b.Pos), from how far through its flight it is.
        static float ClotLift(Bullet b)
        {
            float flight = Mathf.Max(0.01f, b.Life0), k = 1f - MathX.Clamp01(b.Life / flight);
            float c = MathX.Clamp01((flight - 0.45f) / 0.3f);
            return Mathf.Lerp(ClotHandZ, 0f, k) + 4f * Game.ClotApex(c) * k * (1f - k);
        }

        /// A point on the throw's arc, lifted: k from the hand (0) to where it lands (1).
        static V2 ClotArcPoint(V2 hand, V2 land, float apex, float k) =>
            V2.Lerp(hand, land, k) + new V2(0f, -(Mathf.Lerp(ClotHandZ, 0f, k) + 4f * apex * k * (1f - k)));

        void AddClotDrop(V2 at, float z, V2 vel, float vz, float r)
        {
            clotDrops.Add(new ClotDrop { Pos = at, Z = z, Vel = vel, Vz = vz, R = r });
            if (clotDrops.Count > ClotDropMax)
                clotDrops.RemoveAt(0);
        }

        /// Is this spot in a Clot Cannon pool that's still wet?
        bool InWetClotPool(V2 at)
        {
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.BloodPool && z.Duration - z.T < Game.PoolCongeal && at.DistanceTo(z.Pos) < z.Radius * 0.9f)
                    return true;
            return false;
        }

        /// Once a frame (from the floor pass): drips from every Clot Cannon's hand, drops off every clot in flight, the
        /// crown where one landed, then the drops falling and staining.
        void StepClots(float t)
        {
            if (game == null)
                return;
            float dt = clotAt < 0f || t < clotAt ? 0f : Mathf.Min(0.05f, t - clotAt);
            clotAt = t;
            clotDt = dt;
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon.Id != WeaponId.ClotBolt || p.Downed)
                {
                    clotHandAt.Remove(p.Id);
                    continue;
                }
                V2 hand = ClotHand(p), was;
                float moved = clotHandAt.TryGetValue(p.Id, out was) ? Mathf.Min(40f, hand.DistanceTo(was)) : 0f;
                clotHandAt[p.Id] = hand;
                if (dt <= 0f)
                    continue;
                bool charging = p.DrawT > 0f;
                float c = charging ? game.BowCharge(p, p.DrawT) : 0f;
                float walked, idle;
                clotWalked.TryGetValue(p.Id, out walked);
                clotIdle.TryGetValue(p.Id, out idle);
                walked += moved;
                float every = charging ? 22f - 10f * c : 70f;  // a drop every so far you walk, closer as it swells
                V2 drift = moved > 0f ? (hand - was) / dt * 0.5f : V2.Zero;
                while (walked > every)
                {
                    walked -= every;
                    AddClotDrop(hand + new V2(ClotRoll() * 6f - 3f, ClotRoll() * 6f - 3f), ClotHandZ, drift + new V2(ClotRoll() * 28f - 14f, ClotRoll() * 28f - 14f), ClotRoll() * 50f - 20f, 1.6f + ClotRoll() * 0.9f + c * 1.6f);
                }
                if (charging)  // and it drips standing still, faster as it fills
                {
                    idle += dt * (1f + 3.5f * c);
                    while (idle > 1f)
                    {
                        idle -= 1f;
                        AddClotDrop(hand + new V2(ClotRoll() * 6f - 3f, ClotRoll() * 6f - 3f), ClotHandZ, new V2(ClotRoll() * 28f - 14f, ClotRoll() * 28f - 14f), ClotRoll() * 50f - 20f, 1.5f + ClotRoll() * 0.9f + c * 1.6f);
                    }
                }
                clotWalked[p.Id] = walked;
                clotIdle[p.Id] = idle;
            }
            if (dt <= 0f)
                return;
            // Clots in flight shed drops; one that's gone near the end of its flight has landed: a crown of drops.
            clotEmit += dt * 60f;
            int emit = (int)clotEmit;
            clotEmit -= emit;
            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind != Defs.ClotShot)
                    continue;
                float z = ClotLift(b);
                for (int k = 0; k < emit; k++)
                    AddClotDrop(b.Pos + new V2(ClotRoll() * 14f - 7f, ClotRoll() * 14f - 7f), z + ClotRoll() * 10f - 5f,
                        b.Vel * (0.1f + ClotRoll() * 0.25f) + new V2(ClotRoll() * 60f - 30f, ClotRoll() * 60f - 30f), ClotRoll() * 90f - 50f, 1.8f + ClotRoll() * 2f);
                clotsFlying[b.Id] = new ClotSeen { Pos = b.Pos, Life = b.Life };
            }
            clotScratch.Clear();
            foreach (var kv in clotsFlying)
                if (!game.Bullets.ContainsKey(kv.Key))
                    clotScratch.Add(kv.Key);
            foreach (int id in clotScratch)
            {
                var seen = clotsFlying[id];
                clotsFlying.Remove(id);
                if (seen.Life > 0.1f)
                    continue;  // cleared, not landed
                for (int k = 0; k < 34; k++)
                {
                    V2 d = V2.FromAngle(ClotRoll() * MathX.Tau);
                    AddClotDrop(seen.Pos + d * 8f, 3f, d * (110f + ClotRoll() * 310f), 140f + ClotRoll() * 280f, 2f + ClotRoll() * 3.5f);
                }
            }
            // The drops fall; where they land they stain, or ripple a wet pool.
            float drag = Mathf.Exp(-1.2f * dt);
            for (int i = clotDrops.Count - 1; i >= 0; i--)
            {
                var d = clotDrops[i];
                d.Vz -= ClotGravity * dt;
                d.Z += d.Vz * dt;
                d.Pos = d.Pos + d.Vel * dt;
                d.Vel = d.Vel * drag;
                if (d.Z > 0f)
                {
                    clotDrops[i] = d;
                    continue;
                }
                clotDrops.RemoveAt(i);
                if (InWetClotPool(d.Pos))
                {
                    clotRipples.Add(new ClotRipple { Pos = d.Pos, Born = t, R = d.R });
                    continue;
                }
                float rot = d.Vel.IsZero ? ClotRoll() * MathX.Tau : d.Vel.Angle;
                V2 a1 = V2.FromAngle(rot + ClotRoll() * 1.2f - 0.6f), a2 = V2.FromAngle(rot + ClotRoll() * 1.2f - 0.6f);
                clotStains.Add(new ClotStain
                {
                    Pos = d.Pos, Born = t, R = d.R * (1.1f + ClotRoll() * 0.5f), Rot = rot, Sx = 1f + Mathf.Min(1f, d.Vel.Length / 300f),
                    S1 = a1 * d.R * (1.6f + ClotRoll() * 1.2f), S1r = d.R * (0.25f + ClotRoll() * 0.2f),
                    S2 = d.R > 3f ? a2 * d.R * (1.6f + ClotRoll() * 1.2f) : V2.Zero, S2r = d.R > 3f ? d.R * (0.25f + ClotRoll() * 0.2f) : 0f,
                });
                if (d.R > 3.2f)
                    clotRipples.Add(new ClotRipple { Pos = d.Pos, Born = t, R = -d.R });  // a splat on dry floor
            }
            if (clotStains.Count > ClotStainMax)
                clotStains.RemoveRange(0, clotStains.Count - ClotStainMax);
        }

        /// World, on the floor under the zones (WorldRenderer.cs): steps the drops, then the stains and ripples they left.
        void DrawClotStains(float t)
        {
            StepClots(t);
            for (int i = clotStains.Count - 1; i >= 0; i--)
            {
                var s = clotStains[i];
                float age = t - s.Born;
                if (age > ClotStainLife || age < -1f)
                {
                    clotStains.RemoveAt(i);
                    continue;
                }
                float fresh = MathX.Clamp01(1f - age / 2.2f), fade = MathX.Clamp01(1f - age / ClotStainLife);
                var col = ClotStainDry.Mix(ClotStainFresh, fresh).WithAlpha(0.85f * fade);
                Ellipse(s.Pos, s.R * s.Sx, s.R, s.Rot, col, 10);
                Circle(s.Pos + s.S1, s.S1r, col, 6);
                if (s.S2r > 0f)
                    Circle(s.Pos + s.S2, s.S2r, col, 6);
            }
            for (int i = clotRipples.Count - 1; i >= 0; i--)
            {
                var r = clotRipples[i];
                float f = (t - r.Born) / 0.45f;
                if (f > 1f || f < 0f)
                {
                    clotRipples.RemoveAt(i);
                    continue;
                }
                bool dry = r.R < 0f;
                float rr = Mathf.Abs(r.R);
                Arc(r.Pos, rr * (1f + 3f * f), 1.3f, (dry ? ClotDrip : new Rgba(1f, 0.59f, 0.65f)).WithAlpha((dry ? 0.5f : 0.55f) * (1f - f)), 14);
            }
        }

        /// World, over the enemies (WorldRenderer.cs): every drop still in the air, over its shadow.
        void DrawClotAir(float t)
        {
            if (clotDrops.Count == 0)
                return;
            foreach (var d in clotDrops)
                Circle(d.Pos, d.R * 0.8f, new Rgba(0f, 0f, 0f, Mathf.Clamp(0.35f - d.Z / 400f, 0.05f, 0.35f)), 6);
            foreach (var d in clotDrops)
            {
                float len = Mathf.Clamp(-d.Vz / 160f, 0f, 3f) * d.R;  // stretched by its fall
                V2 at = d.Pos + new V2(0f, -d.Z - len * 0.5f);
                Ellipse(at, d.R, d.R + len * 0.5f, 0f, ClotDrip, 8);
                Circle(at + new V2(-d.R * 0.3f, -d.R * 0.3f), d.R * 0.3f, new Rgba(1f, 0.78f, 0.82f, 0.6f), 5);
            }
        }

        /// A wobbling clot of blood: a dark rim, a lighter body, a bright lobe toward the light, strands across, a glint.
        void DrawClotBlob(V2 c, float r, float t, float s, float glow)
        {
            if (glow > 0f)
                Circle(c, r * 2f, new Rgba(1f, 0.2f, 0.31f, 0.18f * glow), 20);
            ClotFan(c, r, t, s, ClotDeep);
            ClotFan(c + new V2(-r * 0.08f, -r * 0.1f), r * 0.82f, t, s + 1f, ClotMid);
            ClotFan(c + new V2(-r * 0.3f, -r * 0.34f), r * 0.42f, t, s + 2f, ClotBright.WithAlpha(0.9f));
            for (int k = 0; k < 3; k++)  // fibrous strands
            {
                float a = s + k * 2.1f + t * 1.5f;
                V2 p0 = c + V2.FromAngle(a) * (r * 0.78f), p1 = c + V2.FromAngle(a + 1.3f) * (r * 0.25f), p2 = c + V2.FromAngle(a + 2.5f) * (r * 0.72f);
                var sc = new Rgba(0.12f, 0.01f, 0.02f, 0.55f);
                Line(p0, p1, 1.2f, sc);
                Line(p1, p2, 1.2f, sc);
            }
            Ellipse(c + new V2(-r * 0.38f, -r * 0.42f), r * 0.22f, r * 0.12f, -0.7f, new Rgba(1f, 0.88f, 0.9f, 0.75f), 8);
        }

        void ClotFan(V2 c, float r, float t, float s, Rgba col)
        {
            SetColor(col);
            const int n = 22;
            V2 prev = V2.Zero;
            for (int i = 0; i <= n; i++)
            {
                float a = MathX.Tau * i / n;
                float rr = r * (1f + 0.11f * Mathf.Sin(3f * a + t * 9f + s) + 0.07f * Mathf.Sin(5f * a - t * 7f) + 0.04f * Mathf.Sin(9f * a + t * 13f));
                V2 next = c + V2.FromAngle(a) * rr;
                if (i > 0)
                    Tri(c, prev, next);
                prev = next;
            }
        }

        /// A clot in flight: its shadow on the ground (smaller and fainter the higher it is), a smear behind, the clot above.
        void DrawClotLob(Bullet b, float t)
        {
            float z = ClotLift(b), r = b.Radius > 0f ? b.Radius + 1f : ClotBody;
            float hi = Mathf.Clamp01(z / 170f);
            Ellipse(b.Pos, r * 1.1f * (1f - 0.35f * hi), r * 0.8f * (1f - 0.35f * hi), 0f, new Rgba(0f, 0f, 0f, 0.5f - 0.25f * hi), 16);
            V2 at = b.Pos + new V2(0f, -z), back = b.Vel.IsZero ? V2.Zero : -b.Vel.Normalized();
            for (int g = 3; g >= 1; g--)
                ClotFan(at + back * (g * 9f), r * (1f - g * 0.12f), t, b.Id + g, ClotMid.WithAlpha(0.18f * (4 - g) / 3f));
            DrawClotBlob(at, r, t * 1.6f, b.Id * 0.7f, 0.6f);
        }

        /// The Clot Cannon's charge: a ring filling round you and the clot swelling in your hand (everyone's), and for
        /// your own, the arc of beads growing out to where it will land, with the pool's outline and the splash there.
        void DrawClotCharge(Player p, bool mine, float t)
        {
            float c = game.BowCharge(p, p.DrawT);
            bool full = c >= 1f;
            float fullAt;
            if (!full)
                clotFullAt.Remove(p.Id);
            else if (!clotFullAt.TryGetValue(p.Id, out fullAt))
                clotFullAt[p.Id] = fullAt = t;
            float pulse = full ? 0.5f + 0.5f * Mathf.Sin(t * 18f) : 0f;
            var red = new Rgba(0.91f, 0.15f, 0.25f);
            var pink = new Rgba(1f, 0.63f + 0.31f * pulse, 0.67f + 0.27f * pulse);
            float ringR = p.Def.BodyR + 12f;
            Arc(p.Disp, ringR, 3f, new Rgba(1f, 1f, 1f, 0.08f), 32);
            Arc(p.Disp, ringR, full ? 4.5f : 3f, full ? pink : red.WithAlpha(0.9f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(0.02f, c));
            V2 hand = ClotHand(p), aim = V2.FromAngle(p.Aim);
            float clotR = 6f + 9f * ClotEase(c);
            DrawClotBlob(hand + aim * (clotR * 0.5f), clotR, t, 1.3f, 0.4f + 0.6f * c * (full ? 1f : 0.5f));
            if (full && clotFullAt.TryGetValue(p.Id, out fullAt) && t - fullAt < 0.35f)  // it's full: a snap of light
            {
                float f = (t - fullAt) / 0.35f;
                Arc(hand, 10f + 30f * f, 3f * (1f - f) + 0.5f, new Rgba(1f, 0.78f, 0.82f, 1f - f), 24);
            }
            if (!mine)
                return;
            float want = Game.ClotDistance(c) * p.Stats.ShotSpeedMult, shown;
            if (!clotShownD.TryGetValue(p.Id, out shown) || p.DrawT < 0.05f)
                shown = Game.ClotMin * p.Stats.ShotSpeedMult;  // a new charge starts short
            shown += (want - shown) * Mathf.Min(1f, clotDt * 16f);  // the arc slides out as you hold
            clotShownD[p.Id] = shown;
            V2 land = p.Disp + aim * shown;
            float apex = Game.ClotApex(MathX.Clamp01((shown / p.Stats.ShotSpeedMult - Game.ClotMin) / (Game.ClotMax - Game.ClotMin)));
            float splash = Game.ClotSplash * Game.AreaMult(p), pool = Game.ClotPoolRadius * Game.AreaMult(p);
            // the ground track under the arc
            for (float s = 10f; s < shown; s += 12f)
                Circle(p.Disp + aim * s, 1.2f, new Rgba(1f, 0.47f, 0.51f, 0.16f), 4);
            // where it lands: the pool's ragged outline, dashed and turning slowly, and the splash
            const int segs = 48;
            for (int i = 0; i < segs; i += 2)
            {
                float a0 = MathX.Tau * i / segs + t * 0.25f, a1 = MathX.Tau * (i + 1) / segs + t * 0.25f;
                float r0 = pool * (1f + 0.05f * Mathf.Sin(3f * (a0 - t * 0.25f)) + 0.035f * Mathf.Sin(5f * (a0 - t * 0.25f) + 1f));
                float r1 = pool * (1f + 0.05f * Mathf.Sin(3f * (a1 - t * 0.25f)) + 0.035f * Mathf.Sin(5f * (a1 - t * 0.25f) + 1f));
                Line(land + V2.FromAngle(a0) * r0, land + V2.FromAngle(a1) * r1, 2f, red.WithAlpha(0.35f + 0.3f * c + 0.3f * pulse));
            }
            Circle(land, splash, red.WithAlpha(0.08f + 0.1f * c), 32);
            Arc(land, splash, 1.5f, new Rgba(1f, 0.55f, 0.59f, 0.4f + 0.3f * c), 40);
            // the beads, swelling as a beat runs out along them
            float per = 0.8f - 0.4f * c, f0 = (t % per) / per;
            float beat = Mathf.Exp(-f0 * 14f) + (f0 > 0.18f ? 0.6f * Mathf.Exp(-(f0 - 0.18f) * 16f) : 0f);
            const int n = 18;
            for (int i = 1; i <= n; i++)
            {
                float k = i / (n + 1f);
                V2 at = ClotArcPoint(hand, land, apex, k) + new V2(0f, Mathf.Sin(t * 7f - i * 0.7f) * 1.3f);
                float r = Mathf.Lerp(1.8f, 4.6f, k) * (1f + 0.35f * beat * Mathf.Exp(-Mathf.Abs(k - (t * 1.4f) % 1f) * 8f));
                Circle(at, r, full ? pink.WithAlpha(0.5f + 0.45f * k) : new Rgba(0.86f, 0.12f, 0.22f, 0.35f + 0.55f * k), 10);
                Circle(at + new V2(-r * 0.3f, -r * 0.35f), r * 0.3f, new Rgba(1f, 0.86f, 0.88f, 0.5f), 5);
            }
            // a drop dangling over the landing
            V2 tear = land + new V2(0f, -22f + Mathf.Sin(t * 5f) * 3f);
            var tc = full ? new Rgba(1f, 0.5f, 0.58f) : new Rgba(0.85f, 0.12f, 0.23f);
            SetColor(tc);
            Tri(tear + new V2(0f, -12f), tear + new V2(4.5f, -2f), tear + new V2(-4.5f, -2f));
            Circle(tear, 5f, tc, 12);
        }

        // --- The pool (ZoneKind.BloodPool) ------------------------------------------------------------------------

        static float PoolHash(int id, int i) => SprayHash(id * 13.17f + i * 1.731f + 0.5f);

        /// The pool's ragged edge at angle `a` (1 = its radius).
        static float PoolEdge(int id, float a, bool wet, float t)
        {
            float s = PoolHash(id, 0) * 90f;
            float r = 1f + 0.06f * Mathf.Sin(3f * a + s) + 0.045f * Mathf.Sin(5f * a + s * 1.7f) + 0.03f * Mathf.Sin(8f * a + s * 2.3f) + 0.018f * Mathf.Sin(13f * a + s * 3.1f);
            if (wet)
                r += 0.012f * Mathf.Sin(a * 6f + t * 3f + s);
            return r;
        }

        void PoolFan(V2 c, float r, int id, bool wet, float t, Rgba col)
        {
            SetColor(col);
            const int n = 48;
            V2 prev = V2.Zero;
            for (int i = 0; i <= n; i++)
            {
                float a = MathX.Tau * i / n;
                V2 next = c + V2.FromAngle(a) * (r * PoolEdge(id, a, wet, t));
                if (i > 0)
                    Tri(c, prev, next);
                prev = next;
            }
        }

        void PoolOutline(V2 c, float r, int id, bool wet, float t, float width, Rgba col)
        {
            const int n = 48;
            V2 prev = V2.Zero;
            for (int i = 0; i <= n; i++)
            {
                float a = MathX.Tau * i / n;
                V2 next = c + V2.FromAngle(a) * (r * PoolEdge(id, a, wet, t));
                if (i > 0)
                    Line(prev, next, width, col);
                prev = next;
            }
        }

        /// The Clot Cannon's pool: it splashes out past its size and settles, wobbling while wet, with a rim arc that
        /// closes over the first second; then it congeals with a thump: a brown crust creeps in from the edge and cracks.
        void DrawBloodZone(Zone z, Rgba c, float t)
        {
            float age = z.Duration - z.T;
            bool wet = age < Game.PoolCongeal;
            float spread = ClotBack(MathX.Clamp01(age / 0.42f)), end = MathX.Clamp01(z.T / 0.7f);
            float thump = age > Game.PoolCongeal && age < Game.PoolCongeal + 0.22f ? 1f - 0.05f * Mathf.Sin(MathX.Pi * (age - Game.PoolCongeal) / 0.22f) : 1f;
            float R = z.Radius * spread * (0.86f + 0.14f * end) * thump;
            float ck = MathX.Clamp01((age - Game.PoolCongeal) / 0.38f), crust = ClotEase(ck);
            V2 p = z.Pos, lit = new V2(-0.25f, -0.3f);
            // The crust first, under the wet middle that shrinks as it sets.
            if (ck > 0f)
            {
                PoolFan(p, R, z.Id, false, t, CrustDark.WithAlpha(end));
                PoolFan(p + lit * (R * 0.2f), R * 0.86f, z.Id, false, t, CrustBody.WithAlpha(end));
                PoolFan(p + lit * (R * 0.4f), R * 0.45f, z.Id, false, t, CrustHi.WithAlpha(0.7f * end));
            }
            float w = 1f - 0.97f * crust;
            if (w > 0.03f)
            {
                PoolFan(p, R * w, z.Id, wet, t, PoolRim.WithAlpha(end));
                PoolFan(p + lit * (R * w * 0.15f), R * w * 0.9f, z.Id, wet, t, PoolBody.WithAlpha(end));
                PoolFan(p + lit * (R * w * 0.35f), R * w * 0.55f, z.Id, wet, t, PoolHi.WithAlpha(0.75f * end));
                PoolFan(p + lit * (R * w * 0.5f), R * w * 0.28f, z.Id, wet, t, PoolHi.Mix(new Rgba(0.85f, 0.25f, 0.33f), 0.5f).WithAlpha(0.5f * end));
            }
            if (ck > 0f)  // the cracks, running out from the middle as it sets
            {
                var dark = new Rgba(0.06f, 0.01f, 0.01f, 0.9f * end);
                var edge = new Rgba(0.75f, 0.43f, 0.31f, 0.28f * end);
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * MathX.Tau + PoolHash(z.Id, 10 + i) * 0.6f - 0.3f, r = 0.12f + PoolHash(z.Id, 20 + i) * 0.18f;
                    int segsN = 4 + (int)(PoolHash(z.Id, 30 + i) * 3f);
                    float upto = ck * segsN;
                    V2 prev = p + V2.FromAngle(a) * (r * R);
                    for (int k = 0; k < segsN && k < upto; k++)
                    {
                        float a2 = a + PoolHash(z.Id, 40 + i * 8 + k) * 0.7f - 0.35f, r2 = Mathf.Min(0.97f, r + 0.1f + PoolHash(z.Id, 100 + i * 8 + k) * 0.08f);
                        V2 next = p + V2.FromAngle(a2) * (r2 * R);
                        float f = Mathf.Min(1f, upto - k);
                        V2 tip = V2.Lerp(prev, next, f);
                        Line(prev, tip, 2.6f, dark);
                        Line(prev + new V2(0.8f, 0.8f), tip + new V2(0.8f, 0.8f), 1f, edge);
                        prev = next;
                        a = a2;
                        r = r2;
                    }
                }
            }
            if (ck < 1f)  // still wet: a shine that drifts, and the splash's ripples
            {
                float wetA = (1f - ck) * end;
                V2 glint = p + new V2(-R * (0.32f + 0.04f * Mathf.Sin(t * 0.9f + z.Id)), -R * (0.36f + 0.03f * Mathf.Cos(t * 0.7f)));
                Ellipse(glint, R * 0.3f, R * 0.12f, -0.6f, new Rgba(1f, 0.75f, 0.78f, 0.16f * wetA), 14);
                Ellipse(glint + new V2(6f, 3f), 5f, 2.2f, -0.6f, new Rgba(1f, 0.9f, 0.92f, 0.5f * wetA), 8);
                for (int k = 0; k < 2; k++)
                {
                    float f = MathX.Clamp01((age - k * 0.16f) / 0.7f);
                    if (f > 0f && f < 1f)
                        Arc(p, f * R * 0.9f, 3f * (1f - f) + 1f, new Rgba(1f, 0.47f, 0.55f, 0.4f * (1f - f) * wetA), 40);
                }
            }
            for (int k = 0; k < 7; k++)  // it heals whoever stands in it: pale motes rising off it
            {
                float ph = (t * 0.55f + k / 7f + PoolHash(z.Id, 200 + k)) % 1f;
                V2 at = p + V2.FromAngle(PoolHash(z.Id, 210 + k) * MathX.Tau) * (R * (0.15f + 0.6f * PoolHash(z.Id, 220 + k))) + new V2(0f, -26f * ph);
                Circle(at, 1.6f + 1.2f * (1f - ph), new Rgba(1f, 0.72f, 0.76f, 0.55f * Mathf.Sin(ph * MathX.Pi) * end), 6);
            }
            PoolOutline(p, R, z.Id, wet, t, 3f, (ck > 0f ? new Rgba(0.11f, 0.035f, 0.025f) : new Rgba(0.13f, 0.01f, 0.03f)).WithAlpha(0.95f * end));
            if (wet && age > 0.15f)  // the rim arc: when it closes, the pool sets
            {
                float f = MathX.Clamp01((age - 0.15f) / (Game.PoolCongeal - 0.15f));
                Arc(p, R * 1.07f, 2f, new Rgba(1f, 0.59f, 0.63f, 0.25f + 0.35f * f), 48, -MathX.Pi / 2f, MathX.Tau * f);
            }
        }
    }
}
