using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Deployable Turret (Core/DeployableTurret.cs), from Docs/Mockups/deployable-turret.html. A crate drops on the spot
    /// (Game.DtLand s, its shadow closing, a gold ring marking the landing), lands with a shock ring, sparks and a shake, unfolds
    /// its four feet (to Game.DtDeploy) and the owner sits in it. While they hold Fire it turns onto their aim and fires twin
    /// barrels in turn, the rate spooling up from Game.DtRateLow to DtRateHigh over Game.DtSpool s. Every round has a recoil kick,
    /// a muzzle star, a flash of warm light on the floor, a gold tracer, sparks where it lands and a casing flicked out sideways;
    /// the barrels go from steel to orange to white as they heat, and at full spool shock rings leave the turret. The last 3 s of
    /// fire the timer ring blinks red; then it shakes, glows white and blows (the host does the damage). Everything here is drawn
    /// from the replicated zone (Pos, Aim, T, Duration) and its owner's aim and trigger, on each screen; the rounds' damage and
    /// where they land are the host's, so the tracers and sparks are the look's own guess at the lane.
    public partial class ArenaApp
    {
        class DtVis
        {
            public Zone Z; public int Seen;
            public float Aim, Spool, Heat, Next, Flash, Ring, Age, FireEnd;
            public readonly float[] Rec = new float[2];
            public int Side; public bool Landed, Mine;
        }

        struct TurretTracer { public V2 From, To; public float Age, Len; }
        struct TurretBrass { public V2 Pos, Vel; public float Spin, Age; }
        readonly Dictionary<int, DtVis> dtVis = new Dictionary<int, DtVis>();
        readonly List<int> dtGone = new List<int>();
        readonly List<TurretTracer> turretTracers = new List<TurretTracer>();
        readonly List<TurretBrass> turretBrass = new List<TurretBrass>();
        float dtClock = -1f;
        int dtFrame;

        const float DtTracerSpeed = 3200f;
        static readonly Rgba DtSteel = new Rgba(0.54f, 0.6f, 0.67f), DtOrange = new Rgba(1f, 0.48f, 0.1f), DtWhiteHot = new Rgba(1f, 0.96f, 0.78f),
            DtPlate = new Rgba(0.15f, 0.18f, 0.22f), DtPlateHi = new Rgba(0.33f, 0.38f, 0.45f), DtDark = new Rgba(0.08f, 0.1f, 0.13f),
            DtGold = new Rgba(1f, 0.78f, 0.29f), DtDanger = new Rgba(1f, 0.3f, 0.37f), DtBrass = new Rgba(0.85f, 0.66f, 0.23f), DtGlow = new Rgba(1f, 0.69f, 0.27f);

        static Rgba DtHeat(float h)
        {
            h = Mathf.Clamp01(h);
            return h < 0.5f ? DtSteel.Mix(DtOrange, h / 0.5f) : DtOrange.Mix(DtWhiteHot, (h - 0.5f) / 0.5f);
        }

        /// Every turret, its tracers and brass, over the arena's contents (after the helicopter). Called once a frame.
        void DrawDeployTurrets(float t)
        {
            UpdateDeployTurrets(t);
            if (dtVis.Count == 0 && turretTracers.Count == 0 && turretBrass.Count == 0)
                return;
            DrawTurretBrass();
            DrawTurretTracers();
            foreach (var v in dtVis.Values)
                DrawDeployTurretBody(v, t);
        }

        /// The drop, the aim, the guns and the end catch up with the zones (a second call at the same time does nothing).
        void UpdateDeployTurrets(float t)
        {
            float dt = dtClock < 0f ? 0f : Mathf.Clamp(t - dtClock, 0f, 0.05f);
            dtClock = t;
            if (dt <= 0f)
                return;
            dtFrame++;
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.DeployTurret)
                    continue;
                DtVis v;
                if (!dtVis.TryGetValue(z.Id, out v))
                    dtVis[z.Id] = v = new DtVis { Aim = z.Aim };
                v.Z = z;
                v.Seen = dtFrame;
                v.Age = Game.DtAge(z);
                v.FireEnd = Game.DtFireEnd(z);
                Player p;
                game.Players.TryGetValue(z.Owner, out p);
                v.Mine = p != null && p == game.Me;
                if (!v.Landed && v.Age >= Game.DtLand)
                {
                    v.Landed = true;
                    DeployTurretLand(z, v);
                }
                for (int i = 0; i < 2; i++)
                    v.Rec[i] = Mathf.Max(0f, v.Rec[i] - dt * 14f);
                v.Flash = Mathf.Max(0f, v.Flash - dt);
                if (v.Age < Game.DtDeploy)
                    continue;
                if (p != null)
                    v.Aim += Mathf.Clamp(MathX.WrapPi(p.Aim - v.Aim), -dt * Game.DtTurn, dt * Game.DtTurn);
                bool fire = p != null && !p.Downed && !p.ParryActive && game.HoldingFire(p) && v.Age < v.FireEnd;
                v.Spool = fire ? Mathf.Min(1f, v.Spool + dt / Game.DtSpool) : Mathf.Max(0f, v.Spool - dt / 0.6f);
                if (fire)
                {
                    float sm = v.Spool * v.Spool * (3f - 2f * v.Spool);
                    v.Next -= dt * Mathf.Lerp(Game.DtRateLow, Game.DtRateHigh, sm);
                    int guard = 0;
                    while (v.Next <= 0f && guard++ < 4)
                    {
                        v.Next += 1f;
                        DeployTurretFire(z, v);
                    }
                    if (v.Spool > 0.9f)
                    {
                        v.Ring -= dt;
                        if (v.Ring <= 0f)
                        {
                            v.Ring = 0.22f;
                            if (Juicy)
                                Ring(z.Pos, 120f, 0.4f, DtGlow.WithAlpha(0.8f));
                        }
                    }
                }
                else
                {
                    v.Next = 0f;
                    v.Heat = Mathf.Max(0f, v.Heat - dt * 0.05f);
                }
                if (v.Age >= v.FireEnd)  // overheating: it shakes and throws white sparks
                {
                    v.Heat = 1.1f;
                    if (v.Mine)
                        Shake(dt * 2.5f);
                    if (Juicy && Rnd(0f, 1f) < 0.7f)
                        Sparks(z.Pos + new V2(Rnd(-20f, 20f), Rnd(-20f, 20f)), Rgba.White, 3, 260f, 0.35f, 2f);
                }
            }
            foreach (var kv in dtVis)
                if (kv.Value.Seen != dtFrame)
                {
                    if (kv.Value.Age >= kv.Value.FireEnd && kv.Value.Z != null && !kv.Value.Z.Quiet && game.Battle)
                        DeployTurretBlow(kv.Value);
                    dtGone.Add(kv.Key);
                }
            foreach (int id in dtGone)
                dtVis.Remove(id);
            dtGone.Clear();
            DeployTurretMoveBits(dt);
        }

        void DeployTurretLand(Zone z, DtVis v)
        {
            if (v.Mine)
                Shake(0.8f);
            Ring(z.Pos, 190f, 0.6f, DtGold);
            Ring(z.Pos, 120f, 0.5f, DtWhiteHot);
            if (Juicy)
            {
                Sparks(z.Pos, DtGold, 40, 600f, 0.6f, 2.2f);
                Sparks(z.Pos, new Rgba(0.6f, 0.65f, 0.7f), 14, 260f, 0.9f, 4f);
                FanGradient(z.Pos, 90f, new Rgba(1f, 0.94f, 0.8f, 0.5f), new Rgba(1f, 0.94f, 0.8f, 0f), 16);
            }
        }

        void DeployTurretBlow(DtVis v)
        {
            V2 at = v.Z.Pos;
            Shake(v.Mine ? 1f : 0.5f);
            Ring(at, Game.DtBlast * 1.6f, 0.7f, Rgba.White);
            Ring(at, Game.DtBlast * 1.2f, 0.6f, DtOrange);
            Ring(at, Game.DtBlast * 0.8f, 0.5f, DtDanger);
            if (Juicy)
            {
                Sparks(at, DtGold, 70, 900f, 0.9f, 2.4f);
                Sparks(at, Rgba.White, 30, 600f, 0.5f, 2f);
                FanGradient(at, Game.DtBlast, new Rgba(1f, 0.9f, 0.7f, 0.8f), new Rgba(1f, 0.55f, 0.2f, 0f), 24);
            }
            Scorch(at, 44f);
        }

        void DeployTurretFire(Zone z, DtVis v)
        {
            V2 f = V2.FromAngle(v.Aim), s = f.Orthogonal();
            int g = v.Side ^= 1;
            V2 muzzle = HeliPt(z.Pos, f, s, 1f, 56f - v.Rec[g] * 6f, g == 0 ? -8f : 8f);
            v.Rec[g] = 1f;
            v.Flash = 0.07f;
            V2 d = V2.FromAngle(v.Aim + Rnd(-1f, 1f) * (0.025f + (1f - v.Spool) * 0.025f)), ds = d.Orthogonal();

            // Where the lane ends for the eye: the second enemy it crosses (the first when it can only pierce one more).
            int pierce = z.Cap ? Game.DtPierceMastered : Game.DtPierce, n = 0;
            float len = Game.DtRange;
            hitProbe.Clear();
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f || e.SpawnT > 0f)
                    continue;
                V2 rel = e.Disp - muzzle;
                float u = rel.Dot(d), w = Mathf.Abs(rel.Dot(ds));
                if (u < 0f || u > Game.DtRange || w > e.Radius + 3f)
                    continue;
                hitProbe.Add(u);
                if (Juicy && n++ < 2)
                    Sparks(e.Disp, DtWhiteHot, 3, 300f, 0.22f, 2f);
            }
            if (hitProbe.Count > 0)
            {
                hitProbe.Sort();
                len = hitProbe[Mathf.Min(pierce, hitProbe.Count) - 1];
            }
            turretTracers.Add(new TurretTracer { From = muzzle, To = muzzle + d * len, Len = len });
            if (Juicy)
            {
                Sparks(muzzle, DtGold, 3, 380f, 0.25f, 1.8f);
                turretBrass.Add(new TurretBrass { Pos = z.Pos + s * 10f, Vel = s * Rnd(120f, 240f) - f * 20f, Spin = Rnd(0f, MathX.Tau) });
            }
            if (v.Mine)
                Shake(0.02f + 0.03f * v.Spool);
            v.Heat = Mathf.Min(1.1f, v.Heat + 0.0042f);
            if (sounds != null && (v.Next > 0.5f || v.Spool > 0f))
                sounds.Play(Sfx.HeliShot, v.Mine ? 0.3f : 0.15f, Time.unscaledTime);
        }

        readonly List<float> hitProbe = new List<float>();

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
                Line(s.From + dir * tail, s.From + dir * head, 9f, DtGlow.WithAlpha(0.35f * fade));
                Line(s.From + dir * Mathf.Min(head, tail + 50f), s.From + dir * head, 2.4f, DtWhiteHot.WithAlpha(0.95f * fade));
            }
        }

        void DrawTurretBrass()
        {
            if (!Juicy)
                return;
            foreach (var b in turretBrass)
            {
                float a = Mathf.Clamp01((1.8f - b.Age) * 1.2f);
                V2 d = V2.FromAngle(b.Spin + b.Age * 18f * Mathf.Exp(-b.Age * 2f)) * 3.5f;
                Line(b.Pos - d, b.Pos + d, 3f, DtBrass.WithAlpha(a));
            }
        }

        void DrawDeployTurretBody(DtVis m, float t)
        {
            V2 c = m.Z.Pos;
            float end = m.FireEnd, age = m.Age;
            if (age < Game.DtLand)  // the crate coming down, its shadow and landing ring closing in
            {
                float q = age / Game.DtLand, h = (1f - q * q) * 200f, sc = Mathf.Lerp(2.4f, 1f, q * q);
                Circle(c + new V2(3f, 5f), 22f + 26f * q, new Rgba(0f, 0f, 0f, 0.25f + 0.35f * q), 18);
                float blink = 0.4f + 0.4f * Mathf.Sin(age * 40f);
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
            float u = Mathf.Clamp01((age - Game.DtLand) / (Game.DtDeploy - Game.DtLand)), shake = age >= end ? 2.5f : 0f;
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
            bool low = age >= Game.DtDeploy && age < end && end - age < 3f && Mathf.Sin(t * 22f) > 0f;
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
            if (age >= Game.DtDeploy)
            {
                float frac = Mathf.Clamp01((end - age) / (end - Game.DtDeploy));
                Arc(c, 58f, 4f, new Rgba(1f, 1f, 1f, 0.08f), 40);
                if (frac > 0f)
                    Arc(c, 58f, 4f, low ? DtDanger : DtGold, 40, -MathX.Pi * 0.5f, MathX.Tau * frac);
            }
        }
    }
}
