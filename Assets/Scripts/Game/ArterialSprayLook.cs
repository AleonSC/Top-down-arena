using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The DPS Hemomancer's Arterial Spray (Core/ArterialSpray.cs), look A "Jet" of Docs/Mockups/arterial-spray.html,
    /// ported from the mockup's particle system. A bright, tight stream leaves the hand and holds together for about 56% of
    /// the reach, then 320 drops a second burst into a fan that fills the cone; each flies its own path from where the hand
    /// was when it left (so the jet sweeps as you turn) and 40% of them leave a stain where they land, which fades over
    /// about 12 s. Enemies in the jet get wetter the longer they're in it: heavy red flecks, a drip, a dashed ring while
    /// slowed, and a burst of blood with stains when one dies soaked. Enemy bullets that vanish in the jet fizz (the
    /// mastered spray washes them away), and the jet sputters out in a spray of drops.
    /// Everything comes from the replicated Player.SprayOn and aim; the particles, stains and wetness are this screen's own.
    public partial class ArenaApp
    {
        struct SprayDrop
        {
            public int Owner;
            public bool Path;             // a jet drop following its path (else a free drop: velocity, drag, life)
            public V2 O, Pos, Prev, Vel;  // origin (path), position now and last frame, velocity (free)
            public float A0, Af, Spd, S, Len, Fan, Size, Drag, Life, Max, Dec;
            public bool Hi, Fizz, Seen;
        }
        struct Spatter { public V2 Pos; public float Born, Rx, Ry, Rot, Alpha; }
        class Soak { public float Wet, Seed; public V2 Last; public float R; }

        readonly List<SprayDrop> sprayDrops = new List<SprayDrop>();
        readonly List<Spatter> spatters = new List<Spatter>();
        readonly Dictionary<int, Soak> soaks = new Dictionary<int, Soak>();
        readonly Dictionary<int, float> sprayEmit = new Dictionary<int, float>();
        readonly HashSet<int> sprayWasOn = new HashSet<int>();
        readonly Dictionary<int, V2> shotsInJet = new Dictionary<int, V2>();
        readonly List<int> sprayScratch = new List<int>();
        float sprayAt = -1f;
        int spatterSeed;
        const float SpatterLife = 12f, SprayRate = 420f;
        const float SprayLook = 1.6f;  // the mockup's sizes, scaled up: its canvas shows a world unit about 1.7x bigger than the arena does
        const int SpatterMax = 700, SprayDropMax = 1600;

        static readonly Rgba SprayCrimson = new Rgba(0.77f, 0.07f, 0.18f), SprayBright = new Rgba(1f, 0.42f, 0.5f);
        static readonly Rgba StainInk = new Rgba(0.43f, 0.03f, 0.09f), FleckDark = new Rgba(0.64f, 0.06f, 0.16f), FleckLight = new Rgba(1f, 0.35f, 0.44f);

        static float SprayHash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f) * 43758.5453f, 1f);
        float SprayRoll() => SprayHash(++spatterSeed * 0.731f + 0.17f);  // the look's own dice (deterministic, so previews repeat)

        static V2 SprayHand(Player p) => p.Disp + V2.FromAngle(p.Aim) * (p.Def.BodyR + 2f) + V2.FromAngle(p.Aim + MathX.Pi / 2f) * 3f;

        void AddSpatter(V2 at, float size, float alpha)
        {
            spatters.Add(new Spatter { Pos = at, Born = sprayAt, Rx = size, Ry = size * (0.6f + 0.4f * SprayRoll()), Rot = SprayRoll() * MathX.Tau, Alpha = alpha });
            if (spatters.Count > SpatterMax)
                spatters.RemoveAt(0);
        }

        void AddFreeDrop(int owner, V2 at, float ang, float speed, float drag, float life, float size, float dec, bool fizz = false)
        {
            sprayDrops.Add(new SprayDrop { Owner = owner, Pos = at, Prev = at, Vel = V2.FromAngle(ang) * speed, Drag = drag, Life = life, Max = life, Size = size, Dec = dec, Fizz = fizz });
        }

        /// Once a frame (from the floor pass): emit, move and land the drops; soak, dry and burst the enemies; fizz the shots.
        void StepSpray(float t)
        {
            if (game == null)
                return;
            float dt = sprayAt < 0f || t < sprayAt ? 0f : Mathf.Min(0.05f, t - sprayAt);
            sprayAt = t;
            foreach (var p in game.Players.Values)
            {
                bool on = p.SprayOn && !p.Downed;
                bool was = sprayWasOn.Contains(p.Id);
                if (on && !was)
                {
                    sprayWasOn.Add(p.Id);
                    for (int k = 0; k < 18; k++)
                        StepSprayFor(p, 1f / 60f, true);  // a pressurized jet is out at once: warm it up a little (0.3 s)
                }
                else if (!on && was)
                {
                    sprayWasOn.Remove(p.Id);
                    if (p.Hp <= p.MaxHp * (Game.SprayFloor + 0.02f))  // it sputtered out at the floor: a last spray of drops
                        for (int k = 0; k < 10; k++)
                            AddFreeDrop(p.Id, SprayHand(p), p.Aim + (SprayRoll() - 0.5f) * 0.8f, 40f + SprayRoll() * 80f, 3f, 0.6f, (1.5f + SprayRoll() * 2f) * SprayLook, 0.3f);
                }
                if (on && dt > 0f)
                    StepSprayFor(p, dt, false);
            }
            if (dt <= 0f)
                return;
            // Drops
            for (int i = sprayDrops.Count - 1; i >= 0; i--)
            {
                var d = sprayDrops[i];
                bool gone = false;
                d.Prev = d.Seen ? d.Pos : (d.Path ? d.O : d.Pos);
                d.Seen = true;
                if (d.Path)
                {
                    d.S += d.Spd * dt;
                    float s = Mathf.Max(0f, d.S);
                    V2 pos = s < d.Fan ? d.O + V2.FromAngle(d.A0) * s : d.O + V2.FromAngle(d.A0) * d.Fan + V2.FromAngle(d.Af) * (s - d.Fan);
                    d.Pos = pos;
                    if (d.S >= d.Len)
                    {
                        gone = true;
                        if (SprayRoll() < d.Dec)
                            AddSpatter(d.Pos, d.Size * (0.9f + 0.8f * SprayRoll()), 0.7f);
                    }
                }
                else
                {
                    float k = Mathf.Exp(-d.Drag * dt);
                    d.Vel = d.Vel * k;
                    d.Pos = d.Pos + d.Vel * dt;
                    d.Life -= dt;
                    if (d.Life <= 0f)
                    {
                        gone = true;
                        if (d.Dec > 0f && SprayRoll() < d.Dec)
                            AddSpatter(d.Pos, d.Size, 0.7f);
                    }
                }
                if (gone)
                    sprayDrops.RemoveAt(i);
                else
                    sprayDrops[i] = d;
            }
            if (sprayDrops.Count > SprayDropMax)
                sprayDrops.RemoveRange(0, sprayDrops.Count - SprayDropMax);
            // Soaked enemies dry slowly; one that's gone while soaked died wet: it bursts.
            sprayScratch.Clear();
            foreach (var kv in soaks)
            {
                Enemy e;
                if (game.Enemies.TryGetValue(kv.Key, out e))
                {
                    kv.Value.Last = e.Disp;
                    kv.Value.R = e.Radius;
                    kv.Value.Wet -= dt * 0.06f;
                    if (kv.Value.Wet <= 0f)
                        sprayScratch.Add(kv.Key);
                    continue;
                }
                sprayScratch.Add(kv.Key);
                if (kv.Value.Wet > 0.15f)
                {
                    var sk = kv.Value;
                    for (int j = 0; j < 18; j++)
                        AddFreeDrop(-1, sk.Last, SprayRoll() * MathX.Tau, 40f + SprayRoll() * 140f, 3f, 0.7f, (1.5f + SprayRoll() * 2.5f) * SprayLook, 0.6f);
                    for (int j = 0; j < 5; j++)
                        AddSpatter(sk.Last + new V2((SprayRoll() - 0.5f) * sk.R * 2f, (SprayRoll() - 0.5f) * sk.R * 2f), sk.R * 0.35f + SprayRoll() * sk.R * 0.4f, 0.85f);
                }
            }
            foreach (int id in sprayScratch)
                soaks.Remove(id);
            // Enemy shots that were in a jet last frame and are gone now, away from the sprayer: washed away (mastered).
            sprayScratch.Clear();
            foreach (var kv in shotsInJet)
                if (!game.Bullets.ContainsKey(kv.Key))
                    sprayScratch.Add(kv.Key);
            foreach (int id in sprayScratch)
            {
                V2 at = shotsInJet[id];
                Player near = null;
                foreach (var p in game.Players.Values)
                    if (p.SprayOn && (near == null || p.Disp.DistanceSq(at) < near.Disp.DistanceSq(at)))
                        near = p;
                if (near != null && at.DistanceTo(near.Disp) > near.Def.BodyR + 10f)
                    for (int j = 0; j < 5; j++)
                        AddFreeDrop(near.Id, at, near.Aim + (SprayRoll() - 0.5f) * 1.2f, 50f + SprayRoll() * 90f, 3f, 0.45f, (1.2f + SprayRoll()) * SprayLook, 0f, true);
            }
            shotsInJet.Clear();
            foreach (var b in game.Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind))
                    continue;
                foreach (var p in game.Players.Values)
                    if (p.SprayOn && Game.InSpray(p.Disp, p.Aim, Game.SprayReachOf(p), b.Pos, b.Radius))
                    {
                        shotsInJet[b.Id] = b.Pos;
                        break;
                    }
            }
        }

        /// One player's jet for dt: emit drops at 320 a second, and soak what's in it.
        void StepSprayFor(Player p, float dt, bool warm)
        {
            float reach = Game.SprayReachOf(p), half = Game.SprayHalfArc;
            float emit;
            sprayEmit.TryGetValue(p.Id, out emit);
            emit += dt * SprayRate;
            int n = (int)emit;
            emit -= n;
            sprayEmit[p.Id] = emit;
            V2 hand = SprayHand(p);
            for (int j = 0; j < n; j++)
            {
                float r = SprayRoll();
                var d = new SprayDrop
                {
                    Owner = p.Id, Path = true, O = hand, Pos = hand, Prev = hand,
                    A0 = p.Aim + (SprayRoll() - 0.5f) * 0.07f, Af = p.Aim + (SprayRoll() - 0.5f) * 2f * half * 0.95f,
                    Spd = 560f + SprayRoll() * 120f, Len = reach * (0.78f + SprayRoll() * 0.28f), Fan = reach * (0.52f + SprayRoll() * 0.15f),
                    Size = (1.2f + r * 1.9f) * SprayLook, Hi = SprayRoll() < 0.25f, Dec = 0.4f,
                };
                d.S = warm ? 0f : -SprayRoll() * d.Spd * dt;  // spread over the frame, so they don't leave in clumps
                sprayDrops.Add(d);
            }
            if (warm)
            {
                for (int i = 0; i < sprayDrops.Count; i++)  // move the warm-up's drops along with it
                {
                    var d = sprayDrops[i];
                    if (!d.Path || d.Owner != p.Id)
                        continue;
                    d.Prev = d.Pos;
                    d.S += d.Spd * dt;
                    float s = Mathf.Max(0f, d.S);
                    d.Pos = s < d.Fan ? d.O + V2.FromAngle(d.A0) * s : d.O + V2.FromAngle(d.A0) * d.Fan + V2.FromAngle(d.Af) * (s - d.Fan);
                    d.Seen = true;
                    if (d.S >= d.Len)
                    {
                        sprayDrops.RemoveAt(i--);
                        continue;
                    }
                    sprayDrops[i] = d;
                }
            }
            foreach (var e in game.Enemies.Values)
            {
                if (!Game.InSpray(p.Disp, p.Aim, reach, e.Disp, e.Radius))
                    continue;
                Soak sk;
                if (!soaks.TryGetValue(e.Id, out sk))
                    soaks[e.Id] = sk = new Soak { Seed = SprayRoll() * 233280f };
                sk.Wet = Mathf.Min(1f, sk.Wet + dt * 0.9f);
                sk.Last = e.Disp;
                sk.R = e.Radius;
            }
        }

        /// World, on the floor under everything (WorldRenderer.cs): steps the spray, then the stains it left.
        void DrawSprayStains(float t)
        {
            StepSpray(t);
            for (int i = spatters.Count - 1; i >= 0; i--)
            {
                var s = spatters[i];
                float age = t - s.Born;
                if (age > SpatterLife || age < -1f)
                {
                    spatters.RemoveAt(i);
                    continue;
                }
                float fade = Mathf.Clamp01(1f - age / SpatterLife);
                Ellipse(s.Pos, s.Rx, s.Ry, s.Rot, StainInk.WithAlpha(s.Alpha * fade * (0.6f + 0.4f * fade)), 10);
            }
        }

        /// World, over the enemies (WorldRenderer.cs): soaked enemies' flecks, drips and slow rings, then every drop in flight.
        void DrawSprayOver(float t)
        {
            foreach (var kv in soaks)
            {
                Enemy e;
                if (!game.Enemies.TryGetValue(kv.Key, out e) || kv.Value.Wet <= 0f)
                    continue;
                float wet = kv.Value.Wet, r = e.Radius;
                V2 c = e.Disp;
                int n = Mathf.CeilToInt(wet * 14f);
                float a = kv.Value.Seed;
                for (int j = 0; j < n; j++)
                {
                    a = (a * 9301f + 49297f) % 233280f; float u = a / 233280f;
                    a = (a * 9301f + 49297f) % 233280f; float v = a / 233280f;
                    V2 off = new V2((u - 0.5f) * r * 1.8f, (v - 0.5f) * r * 1.8f);
                    if (off.Length > r * 0.85f)
                        off = off.Normalized() * r * 0.85f;
                    Circle(c + off, (1.4f + u * 2.2f) * 1.3f, j % 3 != 0 ? FleckDark : FleckLight, 7);
                }
                if (wet > 0.3f)  // a drip running off it
                {
                    float d = Mathf.Repeat(t * 0.8f + kv.Value.Seed * 0.001f, 1f);
                    Ellipse(c + new V2(-r * 0.3f, r + 2f + d * 8f), 2.2f, 3.4f, 0f, new Rgba(0.69f, 0.06f, 0.16f, 1f - 0.5f * d), 8);
                }
                if (e.Chilled)  // slowed: a dashed ring
                    for (int k = 0; k < 14; k++)
                        Arc(c, r + 5f, 1.2f, new Rgba(1f, 0.35f, 0.43f, 0.5f), 3, MathX.Tau * k / 14f + t * 0.5f, MathX.Tau / 28f);
            }
            foreach (var d in sprayDrops)
            {
                if (!d.Seen || d.Path && d.S < 0f)
                    continue;
                float al = d.Path ? 1f - Mathf.Clamp01((d.S - d.Len * 0.85f) / (d.Len * 0.15f)) * 0.5f : Mathf.Clamp01(d.Life / d.Max * 1.4f);
                var col = d.Fizz ? new Rgba(1f, 0.75f, 0.47f) : d.Hi ? SprayBright : SprayCrimson;
                V2 from = d.Prev;
                if (from.DistanceSq(d.Pos) < 1f)
                    from = d.Pos - (d.Path ? V2.FromAngle(d.Af) : d.Vel.Normalized()) * 2f;
                Line(from, d.Pos, d.Size, col.WithAlpha(al));
                Circle(d.Pos, d.Size * 0.5f, col.WithAlpha(al), 6);  // round caps
            }
        }

        /// World, over the model: the stream itself (bright and tight, fading where it bursts), its white core, the glow at
        /// the hand and the cone's dashed outline.
        void DrawSpray(Player p, float r, float t, float a)
        {
            if (!p.SprayOn)
                return;
            float reach = Game.SprayReachOf(p), half = Game.SprayHalfArc;
            V2 dir = V2.FromAngle(p.Aim), hand = SprayHand(p);
            // The cone's outline, dashed and faint.
            var edge = new Rgba(1f, 0.25f, 0.33f, 0.18f * a);
            for (int s = -1; s <= 1; s += 2)
            {
                V2 ed = V2.FromAngle(p.Aim + s * half);
                for (float x = 0f; x < reach; x += 10f)
                    Line(hand + ed * x, hand + ed * Mathf.Min(reach, x + 4f), 1f, edge);
            }
            int dashes = Mathf.Max(4, Mathf.RoundToInt(reach * 2f * half / 10f));
            for (int k = 0; k < dashes; k++)
                Arc(hand, reach, 1f, edge, 2, p.Aim - half + 2f * half * k / dashes, 2f * half / dashes * 0.4f);
            // The glow where it leaves the hand.
            for (int k = 4; k >= 1; k--)
                Circle(hand, 5f * k, new Rgba(1f, 0.31f, 0.39f, 0.15f * a), 14);
            // The stream: a gradient from hot pink to crimson that fades out where it bursts into the fan.
            float core = reach * 0.56f, w = (4.5f + Mathf.Sin(t * 37f) * 1.2f) * 1.5f;
            const int segs = 8;
            for (int k = 0; k < segs; k++)
            {
                float u0 = (float)k / segs, u1 = (float)(k + 1) / segs, um = (u0 + u1) * 0.5f;
                var col = um < 0.7f ? new Rgba(1f, 0.35f, 0.43f).Mix(SprayCrimson, um / 0.7f) : SprayCrimson;
                float al = um < 0.7f ? 1f : 0.95f * (1f - (um - 0.7f) / 0.3f);
                Line(hand + dir * (core * u0), hand + dir * (core * u1), w, col.WithAlpha(al * a));
            }
            Line(hand, hand + dir * (core * 0.75f), 1.8f, new Rgba(1f, 0.82f, 0.84f, 0.8f * a));
        }
    }
}
