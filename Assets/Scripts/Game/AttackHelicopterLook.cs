using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Attack Helicopter (Core/AttackHelicopter.cs), the Orbit look of Docs/Mockups/attack-helicopter.html: an olive gunship seen
    /// from above (tail boom and fin, stub wings with pods, tandem canopy, a spinning four-blade rotor over a blur disc, a tail rotor,
    /// nav lights and a strobe), its shadow on the floor further off the higher it is. It drops in from off-screen over Game.HeliArrive s
    /// (big and high, its shadow tightening), circles its caller (eased toward Game.HeliOrbit), turns its nose onto its target, and
    /// when its time is up climbs away along its heading. Each gun is a three-barrel turret (the nose, or both wing pylons mastered)
    /// that turns onto its target and spins up; amber brackets close on a new target over Game.HeliLock s and turn red; then tracers
    /// fly at Game.HeliRate a second with a muzzle flash, a warm pool of light, brass tumbling down to the floor and sparks on the
    /// target. Replicated state is Player.UltT, HeliGuns, HeliTgt and HeliTgt2; the flight, the lock timing, the tracers and the
    /// brass are local. Sounds (HeliSoundBank.cs): the rotor's loop, a chaingun round per tracer, the barrels spinning up on
    /// a lock from rest. With Visual effects off it draws without the sparks, flash pool and brass.
    public partial class ArenaApp
    {
        class HeliVis
        {
            public bool Out;
            public V2 Pos, Vel;
            public float Ang, Alt = 1.8f, Rotor, LeaveAt = -9f, Spin;
            public V2 From;  // where it came in from
            public readonly int[] Tgt = new int[2];
            public readonly float[] LockAt = { -9f, -9f }, Gun = new float[2], Barrel = new float[2], Next = new float[2], Flash = new float[2];
            public int Rounds;
        }

        struct HeliTracer { public V2 From, To; public float Age, Len; public bool Hit; public int Tgt; }
        struct HeliBrass { public V2 Pos, Vel; public float Z, Vz, Age, Spin, Landed; }

        readonly Dictionary<int, HeliVis> heliVis = new Dictionary<int, HeliVis>();
        readonly List<HeliTracer> heliTracers = new List<HeliTracer>();
        readonly List<HeliBrass> heliBrass = new List<HeliBrass>();
        readonly List<int> heliGone = new List<int>();
        float heliClock = -1f;
        float heliRotorLevel, heliRotorPitch = 1f, heliRotorAt = -9f;  // the loudest rotor this frame (HeliSoundBank.cs), and when it was set

        const float HeliScale = 1.1f, HeliLeaveTime = 1.6f, HeliTracerSpeed = 2600f;
        static readonly Rgba HeliBody = new Rgba(0.31f, 0.35f, 0.23f), HeliDark = new Rgba(0.24f, 0.27f, 0.17f), HeliGlass = new Rgba(0.09f, 0.17f, 0.2f),
            HeliBlade = new Rgba(0.11f, 0.12f, 0.09f), HeliTracerHot = new Rgba(1f, 0.93f, 0.67f), HeliTracerGlow = new Rgba(1f, 0.67f, 0.24f),
            HeliLockRed = new Rgba(1f, 0.28f, 0.24f), HeliLockAmber = new Rgba(1f, 0.75f, 0.35f), HeliBrassCol = new Rgba(0.84f, 0.67f, 0.28f);

        static V2 HeliPt(V2 c, V2 fwd, V2 side, float k, float x, float y) => c + fwd * (x * k) + side * (y * k);

        /// Every gunship and its tracers, over the arena's contents (under the glow and the enemy shots). Called once a frame.
        void DrawHelis(float t)
        {
            UpdateHelis(t);
            DrawHeliBrass(false);
            foreach (var kv in heliVis)
            {
                Player p;
                if (game.Players.TryGetValue(kv.Key, out p))
                    DrawHeliLocks(p, kv.Value, t);
            }
            DrawHeliTracers();
            foreach (var kv in heliVis)
                DrawHeliShadow(kv.Value);
            DrawHeliBrass(true);
            foreach (var kv in heliVis)
                DrawHeli(kv.Value, t);
        }

        /// The flights, guns, tracers and brass catch up with the clock (a second call at the same time does nothing).
        void UpdateHelis(float t)
        {
            float dt = heliClock < 0f ? 0f : Mathf.Clamp(t - heliClock, 0f, 0.05f);
            heliClock = t;
            heliRotorLevel = 0f;
            heliRotorPitch = 1f;
            heliRotorAt = Time.unscaledTime;
            foreach (var p in game.Players.Values)
            {
                bool on = Game.HeliOut(p);
                HeliVis v;
                if (!heliVis.TryGetValue(p.Id, out v))
                {
                    if (!on)
                        continue;
                    heliVis[p.Id] = v = HeliArrival(p);
                }
                if (on && !v.Out)
                    heliVis[p.Id] = v = HeliArrival(p);  // called again before the last one left
                if (!on && v.Out)
                {
                    v.Out = false;
                    v.LeaveAt = t;
                }
                HeliFly(p, v, t, dt);
                // The rotor: swelling in as it comes, fading as it climbs away; quieter higher up, and for someone else's.
                float swell = v.Out ? Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(Game.HeliSince(p) / Game.HeliArrive)) : Mathf.Clamp01(1f - (t - v.LeaveAt) / HeliLeaveTime);
                float level = swell / v.Alt * (p == game.Me ? 1f : 0.55f);
                if (level > heliRotorLevel)
                {
                    heliRotorLevel = level;
                    heliRotorPitch = 1f + 0.06f * (v.Alt - 1f);  // a touch higher while it's working hard (coming in, climbing out)
                }
                if (!v.Out && t - v.LeaveAt > HeliLeaveTime)
                    heliGone.Add(p.Id);
            }
            foreach (var id in heliGone)
                heliVis.Remove(id);
            heliGone.Clear();

            HeliMoveBits(dt);
        }

        HeliVis HeliArrival(Player p)
        {
            float since = Mathf.Max(0f, Game.HeliSince(p));
            V2 orbit = Game.HeliOrbit(p.Disp, Game.HeliArrive), from = orbit + new V2(-0.8f, -0.6f) * 1100f;
            var v = new HeliVis { Out = true, From = from, Pos = since >= Game.HeliArrive ? orbit : from, Ang = 0.64f };
            return v;
        }

        /// The flight: in, round its caller, away; and the guns' turn, spin and fire.
        void HeliFly(Player p, HeliVis v, float t, float dt)
        {
            float since = Game.HeliSince(p);
            v.Rotor += dt * 42f;
            if (v.Out && since < Game.HeliArrive)
            {
                float u = Mathf.Clamp01(since / Game.HeliArrive), e = 1f - (1f - u) * (1f - u) * (1f - u);
                V2 to = Game.HeliOrbit(p.Disp, Game.HeliArrive), prev = v.Pos;
                v.Pos = V2.Lerp(v.From, to, e);
                if (dt > 0f)
                    v.Vel = (v.Pos - prev) / dt;
                v.Alt = 1f + 0.8f * (1f - u) * (1f - u);
            }
            else if (v.Out)
            {
                V2 to = Game.HeliOrbit(p.Disp, since);
                const float w = 3f;
                v.Vel += ((to - v.Pos) * (w * w) - v.Vel * (2f * w)) * dt;
                v.Pos += v.Vel * dt;
                v.Alt = Mathf.Max(1f, v.Alt - dt * 2f);
            }
            else
            {
                float a = t - v.LeaveAt;
                V2 dir = V2.FromAngle(v.Ang);
                v.Vel += dir * (900f * dt);
                v.Pos += v.Vel * dt;
                v.Alt = 1f + (a / 1.2f) * (a / 1.2f) * 0.9f;
            }

            // Who each gun is on, and the nose onto the first one's target.
            int guns = v.Out ? Mathf.Max(1, (int)p.HeliGuns) : v.Tgt[1] != 0 || v.Gun[1] != 0f ? 2 : 1;
            Enemy face = null;
            for (int g = 0; g < 2; g++)
            {
                int id = !v.Out ? 0 : g == 0 ? p.HeliTgt : p.HeliTgt2;
                if (id != v.Tgt[g])
                {
                    if (id != 0 && v.Spin < 0.3f && sounds != null)  // the barrels spinning up from rest
                        sounds.Play(Sfx.HeliSpin, p == game.Me ? 0.55f : 0.28f, Time.unscaledTime);
                    v.Tgt[g] = id;
                    v.LockAt[g] = t;
                    v.Next[g] = 0f;
                }
                Enemy e;
                if (id != 0 && game.Enemies.TryGetValue(id, out e) && face == null)
                    face = e;
            }
            float want = face != null ? (face.Disp - v.Pos).Angle : v.Vel.LengthSq > 1600f ? v.Vel.Angle : v.Ang;
            if (!v.Out)
                want = v.Ang;
            v.Ang = HeliTurn(v.Ang, want, dt * 3.2f);
            v.Spin = face != null && v.Out ? Mathf.Min(1f, v.Spin + dt * 3f) : Mathf.Max(0f, v.Spin - dt * 1.2f);

            for (int g = 0; g < guns; g++)
            {
                v.Flash[g] = Mathf.Max(0f, v.Flash[g] - dt);
                v.Barrel[g] += v.Spin * dt * 45f;
                Enemy e = null;
                if (v.Tgt[g] != 0)
                    game.Enemies.TryGetValue(v.Tgt[g], out e);
                V2 m = HeliMount(v, g, guns);
                float aim = e != null ? (e.Disp - m).Angle : v.Ang;
                v.Gun[g] = HeliTurn(v.Gun[g] == 0f ? v.Ang : v.Gun[g], aim, dt * 9f);
                if (e == null || since < Game.HeliArrive || t - v.LockAt[g] < Game.HeliLock || v.Spin < 0.85f || dt <= 0f)
                    continue;
                v.Next[g] -= dt;
                while (v.Next[g] <= 0f)
                {
                    v.Next[g] += 1f / Game.HeliRate;
                    V2 muzzle = m + V2.FromAngle(v.Gun[g]) * (20f * HeliK(v));
                    V2 to = e.Disp + new V2(Rnd(-0.7f, 0.7f), Rnd(-0.7f, 0.7f)) * e.Radius;
                    heliTracers.Add(new HeliTracer { From = muzzle, To = to, Len = (to - muzzle).Length, Tgt = e.Id });
                    v.Flash[g] = 0.05f;
                    v.Rounds++;
                    if (Juicy)
                    {
                        V2 side = V2.FromAngle(v.Ang).Orthogonal() * (g == 0 && guns == 2 ? -1f : 1f);
                        heliBrass.Add(new HeliBrass { Pos = m, Vel = side * Rnd(90f, 150f) + v.Vel * 0.6f + new V2(Rnd(-30f, 30f), Rnd(-30f, 30f)), Z = 46f * v.Alt, Spin = Rnd(0f, MathX.Tau), Landed = -1f });
                    }
                    if (sounds != null)
                        sounds.Play(Sfx.HeliShot, p == game.Me ? 0.4f : 0.2f, Time.unscaledTime);
                }
            }
        }

        /// The rotor's loop, every frame (ArenaApp.Update): silent once the look stops being drawn (a menu, the lobby).
        void TickHeliSound()
        {
            if (sounds != null)
                sounds.SetHeliRotor(Time.unscaledTime - heliRotorAt < 0.25f ? heliRotorLevel * 0.55f : 0f, heliRotorPitch);
        }

        /// An angle turned toward another by at most `step`, the short way round.
        static float HeliTurn(float a, float to, float step)
        {
            float d = MathX.WrapPi(to - a);
            return a + Mathf.Clamp(d, -step, step);
        }

        float HeliK(HeliVis v) => HeliScale * (1f + (v.Alt - 1f) * 0.38f);

        /// A gun's mount: the nose turret, or a wing pylon each when there are two.
        V2 HeliMount(HeliVis v, int g, int guns)
        {
            V2 fwd = V2.FromAngle(v.Ang), side = fwd.Orthogonal();
            return guns == 1 ? HeliPt(v.Pos, fwd, side, HeliK(v), 46f, 0f) : HeliPt(v.Pos, fwd, side, HeliK(v), 10f, g == 0 ? -34f : 34f);
        }

        void HeliMoveBits(float dt)
        {
            for (int i = heliTracers.Count - 1; i >= 0; i--)
            {
                var s = heliTracers[i];
                s.Age += dt;
                if (s.Age * HeliTracerSpeed >= s.Len && !s.Hit)
                {
                    s.Hit = true;
                    if (Juicy)
                    {
                        Sparks(s.To, HeliTracerHot, 2, 260f, 0.18f, 1.8f);
                        AddPart(new Part { Kind = PSpark, Pos = s.To, Vel = (s.From - s.To).Normalized() * Rnd(120f, 260f), Life = 0.14f, Max = 0.14f, Size = 2.2f, Col = HeliTracerGlow });
                    }
                }
                if (s.Age * HeliTracerSpeed > s.Len + 110f)
                    heliTracers.RemoveAt(i);
                else
                    heliTracers[i] = s;
            }
            for (int i = heliBrass.Count - 1; i >= 0; i--)
            {
                var b = heliBrass[i];
                b.Age += dt;
                if (b.Z > 0f)
                {
                    b.Vz -= 900f * dt;
                    b.Z += b.Vz * dt;
                    b.Pos += b.Vel * dt;
                    if (b.Z <= 0f)
                    {
                        b.Z = 0f;
                        b.Vel *= 0.3f;
                        b.Landed = b.Age;
                    }
                }
                else
                {
                    b.Pos += b.Vel * dt;
                    b.Vel *= Mathf.Exp(-6f * dt);
                }
                if (b.Age > 2.4f)
                    heliBrass.RemoveAt(i);
                else
                    heliBrass[i] = b;
            }
        }

        void DrawHeliTracers()
        {
            foreach (var s in heliTracers)
            {
                if (s.Len < 1f)
                    continue;
                V2 dir = (s.To - s.From) / s.Len;
                float head = Mathf.Min(s.Age * HeliTracerSpeed, s.Len), tail = Mathf.Max(0f, head - 110f);
                float fade = s.Hit ? Mathf.Clamp01(1f - (s.Age * HeliTracerSpeed - s.Len) / 110f) : 1f;
                Line(s.From + dir * tail, s.From + dir * head, 6f, HeliTracerGlow.WithAlpha(0.35f * fade));
                Line(s.From + dir * Mathf.Min(head, tail + 40f), s.From + dir * head, 2.4f, HeliTracerHot.WithAlpha(0.95f * fade));
            }
        }

        void DrawHeliBrass(bool air)
        {
            if (!Juicy)
                return;
            foreach (var b in heliBrass)
            {
                bool landed = b.Z <= 0f;
                if (landed == air)
                    continue;
                float a = landed ? Mathf.Clamp01(1f - (b.Age - b.Landed - 1.2f)) : 1f;
                V2 at = b.Pos + new V2(0f, -b.Z), d = V2.FromAngle(b.Spin + (landed ? 0f : b.Age * 20f)) * 4f;
                Line(at - d, at + d, 3.2f, HeliBrassCol.WithAlpha(a));
                if (landed && b.Age - b.Landed < 0.12f)
                    FanGradient(b.Pos, 8f, new Rgba(1f, 0.86f, 0.55f, 0.5f), new Rgba(1f, 0.86f, 0.55f, 0f), 8);
            }
        }

        /// Brackets closing on each gun's target (amber), turning red with a pulsing ring once it's locked.
        void DrawHeliLocks(Player p, HeliVis v, float t)
        {
            if (!v.Out)
                return;
            float mine = p == game.Me ? 1f : 0.5f;
            for (int g = 0; g < 2; g++)
            {
                Enemy e;
                if (v.Tgt[g] == 0 || !game.Enemies.TryGetValue(v.Tgt[g], out e))
                    continue;
                float u = Mathf.Clamp01((t - v.LockAt[g]) / Game.HeliLock), R = e.Radius + 12f + (1f - u) * 30f;
                float spin = t * 1.5f * (1f - u) + MathX.Pi / 4f * u;
                var col = (u < 1f ? HeliLockAmber : HeliLockRed).WithAlpha(0.95f * mine);
                for (int k = 0; k < 4; k++)
                {
                    float a = spin + k * MathX.Pi / 2f;
                    V2 c = e.Disp + (V2.FromAngle(a) + V2.FromAngle(a + MathX.Pi / 2f)) * R, i1 = V2.FromAngle(a + MathX.Pi), i2 = V2.FromAngle(a + MathX.Pi * 1.5f);
                    Line(c, c + i1 * 10f, 3f, col);
                    Line(c, c + i2 * 10f, 3f, col);
                }
                if (u >= 1f)  // locked: a soft red pulse on it
                    Arc(e.Disp, e.Radius + 6f, 2f, col.WithAlpha(col.A * (0.35f + 0.25f * Mathf.Sin(t * 12f))), 20);
            }
        }

        void DrawHeliShadow(HeliVis v)
        {
            float k = HeliScale * 1.15f, a = Mathf.Clamp01(0.4f / v.Alt);
            V2 c = v.Pos + new V2(40f, 55f) * v.Alt, fwd = V2.FromAngle(v.Ang), side = fwd.Orthogonal();
            var sh = new Rgba(0f, 0f, 0f, a);
            HeliHull(c, fwd, side, k, sh, sh, new Rgba(0f, 0f, 0f, 0f));
            Circle(c, 74f * k, new Rgba(0f, 0f, 0f, a * 0.4f), 24);
        }

        /// The airframe without the rotor: boom and fin, wings and pods, fuselage, engine deck and canopy.
        void HeliHull(V2 c, V2 f, V2 s, float k, Rgba body, Rgba dark, Rgba glass)
        {
            SetColor(body);
            Quad(HeliPt(c, f, s, k, -14f, -7f), HeliPt(c, f, s, k, -96f, -3f), HeliPt(c, f, s, k, -96f, 3f), HeliPt(c, f, s, k, -14f, 7f));
            Quad(HeliPt(c, f, s, k, -84f, -3f), HeliPt(c, f, s, k, -100f, -16f), HeliPt(c, f, s, k, -106f, -16f), HeliPt(c, f, s, k, -96f, 0f));
            Quad(HeliPt(c, f, s, k, -84f, 3f), HeliPt(c, f, s, k, -100f, 16f), HeliPt(c, f, s, k, -106f, 16f), HeliPt(c, f, s, k, -96f, 0f));
            SetColor(dark);
            Quad(HeliPt(c, f, s, k, 14f, -38f), HeliPt(c, f, s, k, -4f, -38f), HeliPt(c, f, s, k, -4f, 38f), HeliPt(c, f, s, k, 14f, 38f));
            float rot = f.Angle;
            Ellipse(HeliPt(c, f, s, k, 4f, -38f), 13f * k, 5f * k, rot, body, 10);
            Ellipse(HeliPt(c, f, s, k, 4f, 38f), 13f * k, 5f * k, rot, body, 10);
            Ellipse(HeliPt(c, f, s, k, 12f, 0f), 40f * k, 17f * k, rot, body, 16);
            SetColor(body);
            Quad(HeliPt(c, f, s, k, -6f, -14f), HeliPt(c, f, s, k, -26f, -9f), HeliPt(c, f, s, k, -26f, 9f), HeliPt(c, f, s, k, -6f, 14f));
            if (glass.A <= 0f)
                return;
            SetColor(dark);
            Quad(HeliPt(c, f, s, k, 4f, -10f), HeliPt(c, f, s, k, -22f, -10f), HeliPt(c, f, s, k, -22f, 10f), HeliPt(c, f, s, k, 4f, 10f));
            Ellipse(HeliPt(c, f, s, k, 30f, 0f), 15f * k, 9f * k, rot, glass, 14);
            Ellipse(HeliPt(c, f, s, k, 12f, 0f), 11f * k, 8f * k, rot, glass, 12);
            Ellipse(HeliPt(c, f, s, k, 33f, -3f), 7f * k, 3f * k, rot - 0.3f, new Rgba(0.78f, 0.94f, 1f, 0.35f), 8);
        }

        void DrawHeli(HeliVis v, float t)
        {
            float k = HeliK(v);
            V2 c = v.Pos, f = V2.FromAngle(v.Ang), s = f.Orthogonal();
            int guns = v.Gun[1] != 0f ? 2 : 1;

            // The warm light the guns throw on the floor under them.
            if (Juicy)
                for (int g = 0; g < guns; g++)
                    if (v.Flash[g] > 0f)
                    {
                        V2 m = HeliMount(v, g, guns);
                        FanGradient(m + V2.FromAngle(v.Gun[g]) * 40f + new V2(0f, 30f), 60f, new Rgba(1f, 0.7f, 0.31f, 0.14f), new Rgba(1f, 0.7f, 0.31f, 0f), 14);
                    }

            HeliHull(c, f, s, k, HeliBody, HeliDark, HeliGlass);

            // Tail rotor, nav lights, strobe.
            V2 tr = HeliPt(c, f, s, k, -98f, 0f), trd = f * (Mathf.Cos(v.Rotor * 2.3f) * 12f * k);
            Line(tr - trd, tr + trd, 3f * k, new Rgba(0.12f, 0.12f, 0.1f, 0.7f));
            Line(tr - f * (12f * k), tr + f * (12f * k), 4f * k, new Rgba(0.78f, 0.78f, 0.75f, 0.12f));
            Circle(HeliPt(c, f, s, k, 4f, -44f), 3f * k, new Rgba(1f, 0.23f, 0.18f), 8);
            Circle(HeliPt(c, f, s, k, 4f, 44f), 3f * k, new Rgba(0.24f, 1f, 0.42f), 8);
            if (Mathf.Repeat(t * 1.6f, 1f) < 0.08f)
                FanGradient(HeliPt(c, f, s, k, -40f, 0f), 26f * k, new Rgba(1f, 1f, 1f, 0.9f), new Rgba(1f, 1f, 1f, 0f), 12);

            // Guns: a turret and three barrels turning as they spin.
            for (int g = 0; g < guns; g++)
            {
                V2 m = HeliMount(v, g, guns), gf = V2.FromAngle(v.Gun[g]), gs = gf.Orthogonal();
                Circle(m, 7f * k, new Rgba(0.17f, 0.18f, 0.15f), 12);
                for (int b = 0; b < 3; b++)
                {
                    float ph = v.Barrel[g] + b * MathX.Tau / 3f, off = Mathf.Sin(ph) * 3.2f;
                    var col = Mathf.Cos(ph) > 0f ? new Rgba(0.42f, 0.45f, 0.4f) : new Rgba(0.23f, 0.24f, 0.21f);
                    Line(HeliPt(m, gf, gs, k, 2f, off), HeliPt(m, gf, gs, k, 24f, off), 2.4f * k, col);
                }
                Line(HeliPt(m, gf, gs, k, 20f, 0f), HeliPt(m, gf, gs, k, 24f, 0f), 8f * k, new Rgba(0.11f, 0.12f, 0.1f));
                if (v.Flash[g] > 0f)
                {
                    float fl = v.Flash[g] / 0.05f;
                    V2 tip = HeliPt(m, gf, gs, k, 24f, 0f);
                    FanGradient(tip + gf * (4f * k), 26f * k, new Rgba(1f, 0.78f, 0.35f, 0.8f * fl), new Rgba(1f, 0.78f, 0.35f, 0f), 12);
                    SetColor(new Rgba(1f, 0.94f, 0.75f, fl));
                    Tri(tip + gs * (5f * k), tip + gf * ((20f + Rnd(0f, 10f)) * k), tip - gs * (5f * k));
                }
            }

            // The rotor: a faint blur disc, ghost blades trailing the real ones, the hub.
            FanGradient(c, 76f * k, new Rgba(0.75f, 0.76f, 0.7f, 0.09f), new Rgba(0.75f, 0.76f, 0.7f, 0f), 24);
            for (int gh = 2; gh >= 0; gh--)
            {
                var col = HeliBlade.WithAlpha(gh == 0 ? 0.85f : 0.25f / gh);
                for (int b = 0; b < 4; b++)
                {
                    V2 d = V2.FromAngle(v.Rotor - gh * 0.13f + b * MathX.Tau / 4f);
                    Line(c + d * (6f * k), c + d * (74f * k), (gh == 0 ? 5f : 4f) * k, col);
                }
            }
            Circle(c, 6f * k, new Rgba(0.14f, 0.15f, 0.12f), 10);
        }
    }
}
