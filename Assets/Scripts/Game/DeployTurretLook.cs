using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The DPS Soldier's Deployable Turret (mockup only: no ability or game logic yet; nothing in a run sets `deployTurretMock`).
    /// A crate drops on the spot (0.55 s, its shadow closing, a gold ring marking the landing), lands with a shock ring, dust-grey
    /// sparks and a shake, unfolds its four feet (0.5 s) and you sit in it. For 10 s it turns onto the nearest enemy and fires
    /// twin barrels in turn (22 damage, pierces 2): the rate spools up from 7 to 30 rounds a second over 1.2 s. Every round has a
    /// recoil kick, a muzzle star, a flash of warm light on the floor, a gold tracer, sparks where it lands, and a casing flicked
    /// out sideways; the barrels go from steel to orange to white as they heat, and at full spool shock rings leave the turret.
    /// The last 3 s the timer ring blinks red; at 0 it shakes, glows white and blows up (160 px, 80 damage, a scorch).
    /// Tools/Preview draws it through `DeployTurretMock` (ShotDeployTurret.cs), set beside Docs/Mockups/deployable-turret.html.
    public class DeployTurretMock
    {
        public V2 Pos; public float T;  // T: seconds since the cast
        public float Aim, Spool, Heat, Next, Flash, Ring, Clock = -1f;
        public readonly float[] Rec = new float[2];
        public int Side; public bool Blown;
    }

    public partial class ArenaApp
    {
        internal static DeployTurretMock deployTurretMock;  // Tools/Preview only (ShotDeployTurret.cs)

        struct TurretTracer { public V2 From, To; public float Age, Len; }
        struct TurretBrass { public V2 Pos, Vel; public float Spin, Age; }
        readonly List<TurretTracer> turretTracers = new List<TurretTracer>();
        readonly List<TurretBrass> turretBrass = new List<TurretBrass>();

        const float DtDrop = 0.55f, DtDeploy = 1.1f, DtDuration = 10f, DtDying = 0.8f, DtRange = 560f, DtDamage = 22f, DtBlast = 160f;
        const float DtSpoolTime = 1.2f, DtRateLow = 7f, DtRateHigh = 30f, DtTracerSpeed = 3200f;
        static readonly Rgba DtSteel = new Rgba(0.54f, 0.6f, 0.67f), DtOrange = new Rgba(1f, 0.48f, 0.1f), DtWhiteHot = new Rgba(1f, 0.96f, 0.78f),
            DtPlate = new Rgba(0.15f, 0.18f, 0.22f), DtPlateHi = new Rgba(0.33f, 0.38f, 0.45f), DtDark = new Rgba(0.08f, 0.1f, 0.13f),
            DtGold = new Rgba(1f, 0.78f, 0.29f), DtDanger = new Rgba(1f, 0.3f, 0.37f), DtBrass = new Rgba(0.85f, 0.66f, 0.23f), DtGlow = new Rgba(1f, 0.69f, 0.27f);

        static Rgba DtHeat(float h)
        {
            h = Mathf.Clamp01(h);
            return h < 0.5f ? DtSteel.Mix(DtOrange, h / 0.5f) : DtOrange.Mix(DtWhiteHot, (h - 0.5f) / 0.5f);
        }

        /// The turret, its tracers and brass, over the arena's contents (after the helicopter). Called once a frame.
        void DrawDeployTurret(float t)
        {
            var m = deployTurretMock;
            if (m == null)
                return;
            UpdateDeployTurret(t);
            DrawTurretBrass(false);
            DrawTurretTracers();
            if (m.T < DtDeploy + DtDuration + DtDying)
                DrawDeployTurretBody(m, t);
            DrawTurretBrass(true);
        }

        /// The drop, the aim, the guns and the end catch up with the clock (a second call at the same time does nothing).
        void UpdateDeployTurret(float t)
        {
            var m = deployTurretMock;
            if (m == null)
                return;
            float dt = m.Clock < 0f ? 0f : Mathf.Clamp(t - m.Clock, 0f, 0.05f);
            m.Clock = t;
            if (dt <= 0f)
                return;
            float before = m.T;
            m.T += dt;
            if (before < DtDrop && m.T >= DtDrop)
                DeployTurretLand(m);
            for (int i = 0; i < 2; i++)
                m.Rec[i] = Mathf.Max(0f, m.Rec[i] - dt * 14f);
            m.Flash = Mathf.Max(0f, m.Flash - dt);
            float end = DtDeploy + DtDuration;
            if (m.T >= DtDeploy && m.T < end + DtDying)
            {
                // The nearest enemy in range, and the turn onto it.
                Enemy tgt = null;
                float best = DtRange;
                foreach (var e in game.Enemies.Values)
                {
                    float d = (e.Disp - m.Pos).Length;
                    if (e.Hp > 0f && d < best)
                    {
                        best = d;
                        tgt = e;
                    }
                }
                float diff = 0f;
                if (tgt != null)
                {
                    diff = MathX.WrapPi((tgt.Disp - m.Pos).Angle - m.Aim);
                    m.Aim += Mathf.Clamp(diff, -dt * 7f, dt * 7f);
                }
                bool firing = tgt != null && m.T < end && Mathf.Abs(diff) < 1f;
                m.Spool = firing ? Mathf.Min(1f, m.Spool + dt / DtSpoolTime) : Mathf.Max(0f, m.Spool - dt / 0.6f);
                if (firing)
                {
                    float sm = m.Spool * m.Spool * (3f - 2f * m.Spool);
                    m.Next -= dt * Mathf.Lerp(DtRateLow, DtRateHigh, sm);
                    int guard = 0;
                    while (m.Next <= 0f && guard++ < 4)
                    {
                        m.Next += 1f;
                        DeployTurretFire(m);
                    }
                    if (m.Spool > 0.9f)
                    {
                        m.Ring -= dt;
                        if (m.Ring <= 0f)
                        {
                            m.Ring = 0.22f;
                            if (Juicy)
                                Ring(m.Pos, 120f, 0.4f, DtGlow.WithAlpha(0.8f));
                        }
                    }
                }
                else
                {
                    m.Next = 0f;
                    m.Heat = Mathf.Max(0f, m.Heat - dt * 0.05f);
                }
                if (m.T >= end && m.T < end + DtDying)  // overheating: it shakes, throws white sparks, then blows
                {
                    m.Heat = 1.1f;
                    Shake(dt * 2.5f);
                    if (Juicy && Rnd(0f, 1f) < 0.7f)
                        Sparks(m.Pos + new V2(Rnd(-20f, 20f), Rnd(-20f, 20f)), Rgba.White, 3, 260f, 0.35f, 2f);
                }
            }
            if (!m.Blown && m.T >= end + DtDying)
                DeployTurretBlow(m);
            DeployTurretMoveBits(dt);
        }

        void DeployTurretLand(DeployTurretMock m)
        {
            Shake(0.8f);
            Ring(m.Pos, 190f, 0.6f, DtGold);
            Ring(m.Pos, 120f, 0.5f, DtWhiteHot);
            if (Juicy)
            {
                Sparks(m.Pos, DtGold, 40, 600f, 0.6f, 2.2f);
                Sparks(m.Pos, new Rgba(0.6f, 0.65f, 0.7f), 14, 260f, 0.9f, 4f);
                FanGradient(m.Pos, 90f, new Rgba(1f, 0.94f, 0.8f, 0.5f), new Rgba(1f, 0.94f, 0.8f, 0f), 16);
            }
        }

        void DeployTurretBlow(DeployTurretMock m)
        {
            m.Blown = true;
            Shake(1f);
            Ring(m.Pos, DtBlast * 1.6f, 0.7f, Rgba.White);
            Ring(m.Pos, DtBlast * 1.2f, 0.6f, DtOrange);
            Ring(m.Pos, DtBlast * 0.8f, 0.5f, DtDanger);
            if (Juicy)
            {
                Sparks(m.Pos, DtGold, 70, 900f, 0.9f, 2.4f);
                Sparks(m.Pos, Rgba.White, 30, 600f, 0.5f, 2f);
                FanGradient(m.Pos, DtBlast, new Rgba(1f, 0.9f, 0.7f, 0.8f), new Rgba(1f, 0.55f, 0.2f, 0f), 24);
            }
            foreach (var e in game.Enemies.Values)
                if (e.Hp > 0f && (e.Disp - m.Pos).Length < DtBlast)
                    e.Hp -= 80f;
            Scorch(m.Pos, 44f);
        }

        void DeployTurretFire(DeployTurretMock m)
        {
            V2 f = V2.FromAngle(m.Aim), s = f.Orthogonal();
            int g = m.Side ^= 1;
            V2 muzzle = HeliPt(m.Pos, f, s, 1f, 56f - m.Rec[g] * 6f, g == 0 ? -8f : 8f);
            m.Rec[g] = 1f;
            m.Flash = 0.07f;
            V2 d = V2.FromAngle(m.Aim + Rnd(-1f, 1f) * (0.025f + (1f - m.Spool) * 0.025f));

            // Hitscan down the lane: the first two enemies it crosses.
            Enemy a = null, b = null;
            float au = 0f, bu = 0f;
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f)
                    continue;
                V2 rel = e.Disp - muzzle;
                float u = rel.Dot(d), v = Mathf.Abs(rel.Dot(d.Orthogonal()));
                if (u < 0f || u > DtRange || v > e.Radius + 3f)
                    continue;
                if (a == null || u < au) { b = a; bu = au; a = e; au = u; }
                else if (b == null || u < bu) { b = e; bu = u; }
            }
            float len = b != null ? bu : a != null ? au : DtRange;
            foreach (var e in new[] { a, b })
            {
                if (e == null)
                    continue;
                e.Hp -= DtDamage;
                if (Juicy)
                    Sparks(e.Disp, DtWhiteHot, 3, 300f, 0.22f, 2f);
            }
            turretTracers.Add(new TurretTracer { From = muzzle, To = muzzle + d * len, Len = len });
            if (Juicy)
            {
                Sparks(muzzle, DtGold, 3, 380f, 0.25f, 1.8f);
                turretBrass.Add(new TurretBrass { Pos = m.Pos + s * 10f, Vel = s * Rnd(120f, 240f) - f * 20f, Spin = Rnd(0f, MathX.Tau) });
            }
            Shake(0.02f + 0.03f * m.Spool);
            m.Heat = Mathf.Min(1.1f, m.Heat + 0.0042f);
        }

        void DeployTurretMoveBits(float dt)
        {
            for (int i = turretTracers.Count - 1; i >= 0; i--)
            {
                var s = turretTracers[i];
                s.Age += dt;
                if (s.Age * DtTracerSpeed > s.Len + 140f)
                    turretTracers.RemoveAt(i);
                else
                    turretTracers[i] = s;
            }
            for (int i = turretBrass.Count - 1; i >= 0; i--)
            {
                var b = turretBrass[i];
                b.Age += dt;
                b.Pos += b.Vel * dt;
                b.Vel *= Mathf.Exp(-5f * dt);
                if (b.Age > 1.8f)
                    turretBrass.RemoveAt(i);
                else
                    turretBrass[i] = b;
            }
        }

        void DrawTurretTracers()
        {
            foreach (var s in turretTracers)
            {
                if (s.Len < 1f)
                    continue;
                V2 dir = (s.To - s.From) / s.Len;
                float head = Mathf.Min(s.Age * DtTracerSpeed, s.Len), tail = Mathf.Max(0f, head - 140f);
                float fade = Mathf.Clamp01(1f - (s.Age * DtTracerSpeed - s.Len) / 140f);
                float hot = deployTurretMock != null ? deployTurretMock.Spool : 0f;
                Line(s.From + dir * tail, s.From + dir * head, 7f + hot * 4f, DtGlow.WithAlpha(0.35f * fade));
                Line(s.From + dir * Mathf.Min(head, tail + 50f), s.From + dir * head, 2.4f, DtWhiteHot.WithAlpha(0.95f * fade));
            }
        }

        void DrawTurretBrass(bool air)
        {
            if (!Juicy || air)
                return;
            foreach (var b in turretBrass)
            {
                float a = Mathf.Clamp01(1.8f - b.Age) * 1.2f;
                V2 d = V2.FromAngle(b.Spin + b.Age * 18f * Mathf.Exp(-b.Age * 2f)) * 3.5f;
                Line(b.Pos - d, b.Pos + d, 3f, DtBrass.WithAlpha(Mathf.Clamp01(a)));
            }
        }

        void DrawDeployTurretBody(DeployTurretMock m, float t)
        {
            V2 c = m.Pos;
            float end = DtDeploy + DtDuration;
            if (m.T < DtDrop)  // the crate coming down, its shadow and landing ring closing in
            {
                float q = m.T / DtDrop, h = (1f - q * q) * 200f, sc = Mathf.Lerp(2.4f, 1f, q * q);
                Circle(c + new V2(3f, 5f), 22f + 26f * q, new Rgba(0f, 0f, 0f, 0.25f + 0.35f * q), 18);
                float blink = 0.4f + 0.4f * Mathf.Sin(m.T * 40f);
                Arc(c, 46f * (1.3f - q * 0.3f), 2f, DtGold.WithAlpha(blink), 28);
                V2 at = c + new V2(0f, -h);
                float r = 24f * sc;
                SetColor(DtPlate);
                Quad(at + new V2(-r, -r), at + new V2(r, -r), at + new V2(r, r), at + new V2(-r, r));
                Line(at + new V2(-r, -r), at + new V2(r, r), 2f, DtGold);
                Line(at + new V2(r, -r), at + new V2(-r, r), 2f, DtGold);
                Arc(at, r * 1.41f, 2f, DtGold, 4, MathX.Pi * 0.25f, MathX.Tau * 0.999f);
                return;
            }
            float u = Mathf.Clamp01((m.T - DtDrop) / (DtDeploy - DtDrop)), shake = m.T >= end ? 2.5f : 0f;
            if (shake > 0f)
                c += new V2(Rnd(-shake, shake), Rnd(-shake, shake));

            // Light the barrels throw on the floor, behind the body.
            V2 f = V2.FromAngle(m.Aim), s = f.Orthogonal();
            if (Juicy && m.Flash > 0f)
            {
                float r = 230f + m.Spool * 140f, fl = m.Flash / 0.07f;
                FanGradient(c + f * 90f, r, new Rgba(1f, 0.74f, 0.35f, 0.3f * fl), new Rgba(1f, 0.47f, 0.12f, 0f), 20);
            }
            Circle(c + new V2(3f, 5f), 44f, new Rgba(0f, 0f, 0f, 0.35f), 18);

            // Four feet unfolding from the corners, then the eight-sided plate.
            for (int i = 0; i < 4; i++)
            {
                V2 d = V2.FromAngle(MathX.Pi * 0.25f + i * MathX.Pi * 0.5f), p0 = c + d * 18f, p1 = c + d * (18f + 30f * u);
                Line(p0, p1, 8f, DtDark);
                Line(p0, p1, 3f, DtPlateHi);
                SetColor(DtPlate);
                Quad(p1 + new V2(-6f, -6f), p1 + new V2(6f, -6f), p1 + new V2(6f, 6f), p1 + new V2(-6f, 6f));
                Arc(p1, 7f, 1f, DtGold.WithAlpha(0.8f), 4, MathX.Pi * 0.25f, MathX.Tau * 0.999f);
            }
            Polygon(c, 27f, 8, MathX.Pi / 8f, DtPlateHi);
            Polygon(c, 25f, 8, MathX.Pi / 8f, DtPlate);
            bool low = m.T >= DtDeploy && m.T < end && DtDeploy + DtDuration - m.T < 3f && Mathf.Sin(t * 22f) > 0f;
            Arc(c, 19f, 2f, (low ? DtDanger : DtGold), 24);

            // The gun: ammo box with its belt, twin barrels (recoil, heat), shield and the gunner behind it.
            float k = Mathf.Lerp(0.6f, 1f, u);
            SetColor(DtPlate);
            Quad(HeliPt(c, f, s, k, -30f, -12f), HeliPt(c, f, s, k, -14f, -12f), HeliPt(c, f, s, k, -14f, 12f), HeliPt(c, f, s, k, -30f, 12f));
            for (int i = 0; i < 4; i++)
                Line(HeliPt(c, f, s, k, -27f + i * 3.5f, -9f), HeliPt(c, f, s, k, -27f + i * 3.5f, 9f), 2f * k, DtBrass);
            Rgba hc = DtHeat(m.Heat);
            for (int g = 0; g < 2; g++)
            {
                float y = g == 0 ? -8f : 8f, len = 44f - m.Rec[g] * 7f;
                Line(HeliPt(c, f, s, k, 8f, y), HeliPt(c, f, s, k, 8f + len - 6f, y), 9f * k, DtDark);
                Line(HeliPt(c, f, s, k, 10f, y), HeliPt(c, f, s, k, 6f + len, y), 6f * k, hc);
                Line(HeliPt(c, f, s, k, len, y), HeliPt(c, f, s, k, len + 9f, y), 11f * k, DtDark);
            }
            SetColor(DtPlateHi);
            Quad(HeliPt(c, f, s, k, 6f, -22f), HeliPt(c, f, s, k, 18f, -17f), HeliPt(c, f, s, k, 18f, 17f), HeliPt(c, f, s, k, 6f, 22f));
            SetColor(DtPlate);
            Quad(HeliPt(c, f, s, k, 0f, -14f), HeliPt(c, f, s, k, 7f, -20f), HeliPt(c, f, s, k, 7f, 20f), HeliPt(c, f, s, k, 0f, 14f));
            Line(HeliPt(c, f, s, k, 10f, 0f), HeliPt(c, f, s, k, 18f, 0f), 3f * k, DtGold);
            Circle(HeliPt(c, f, s, k, -10f, 0f), 7f * k, new Rgba(0.3f, 0.88f, 1f), 12);
            Circle(HeliPt(c, f, s, k, -8f, 0f), 3.5f * k, new Rgba(0.04f, 0.16f, 0.2f), 8);

            // Heat on the barrels, the muzzle star, the timer ring.
            if (Juicy && m.Heat > 0.35f)
            {
                float hk = (m.Heat - 0.35f) * 0.5f;
                V2 tip = c + f * 46f;
                FanGradient(tip, 26f, DtOrange.WithAlpha(0.35f * hk), DtOrange.WithAlpha(0f), 14);
                FanGradient(tip, 12f, DtWhiteHot.WithAlpha(0.4f * hk), DtWhiteHot.WithAlpha(0f), 10);
            }
            if (m.Flash > 0f)
            {
                float fl = m.Flash / 0.07f;
                V2 tip = HeliPt(c, f, s, 1f, 56f - m.Rec[m.Side] * 6f, m.Side == 0 ? -8f : 8f);
                FanGradient(tip, 54f, new Rgba(1f, 0.97f, 0.82f, 0.95f * fl), new Rgba(1f, 0.55f, 0.15f, 0f), 14);
                for (int i = 0; i < 7; i++)
                {
                    float ang = m.Aim + Rnd(-1f, 1f) * (i < 3 ? 0.12f : 0.8f), L = (i < 3 ? 70f : 34f) * Rnd(0.5f, 1.3f) * (0.7f + m.Spool * 0.6f);
                    V2 dd = V2.FromAngle(ang), ss = dd.Orthogonal();
                    SetColor((i % 2 == 0 ? DtWhiteHot : DtGold).WithAlpha(0.9f * fl));
                    Tri(tip + ss * 5f, tip + dd * L, tip - ss * 5f);
                }
            }
            if (m.T >= DtDeploy)
            {
                float frac = Mathf.Clamp01((DtDeploy + DtDuration - m.T) / DtDuration);
                Arc(c, 58f, 4f, new Rgba(1f, 1f, 1f, 0.08f), 40);
                if (frac > 0f)
                    Arc(c, 58f, 4f, low ? DtDanger : DtGold, 40, -MathX.Pi * 0.5f, MathX.Tau * frac);
            }
        }
    }
}
