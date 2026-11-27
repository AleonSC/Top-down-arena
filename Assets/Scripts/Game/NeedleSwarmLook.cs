using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The DPS Hemomancer's Needle Swarm (Core/NeedleSwarm.cs), the mockup Docs/Mockups/needle-swarm.html made real: every needle is
    /// flown on each screen with the mockup's springs, from what the host sends (Player.NsCount, NsTen, NsLen, NsBound).
    ///   The ring: each needle has its own lane (NsInner..NsOuter), speed and wobble and springs toward its spot on it, so the swarm
    ///     swirls, breathes and trails behind you as you move. A needle siphoned out of you spirals out from your middle.
    ///   A tendril: needles on the enemy's side of the ring peel off and stream along a swaying curve to the tendril's tip, thick in
    ///     the middle; once it arrives they pour round the enemy as a fast spinning ring. When it lets go they glide back.
    ///   A shot stopping on the ring (FxKind.NeedleSwarm, code 0) breaks the needle nearest it in a spark; the Alt's burst (code 1)
    ///     takes the needles nearest the aim (the needles in flight are Defs.BloodShot shots, ShotLooks.cs).
    ///   Drawn under the player: a dark red tendril underglow, a faint halo in the ring, then each needle as a glow, a blood-red
    ///     body and a pale point along the way it flies.
    public partial class ArenaApp
    {
        static readonly Rgba NsGlow = new Rgba(0.59f, 0.04f, 0.11f, 0.22f), NsBody = new Rgba(0.73f, 0.06f, 0.16f, 0.95f),
            NsTip = new Rgba(1f, 0.47f, 0.51f, 0.9f), NsHalo = new Rgba(0.55f, 0.03f, 0.09f), NsUnder = new Rgba(0.47f, 0.02f, 0.08f, 0.35f),
            NsBright = new Rgba(1f, 0.27f, 0.35f);
        const int NsOrbit = 0, NsInTendril = 1, NsBind = 2;

        class NsNeedle { public V2 Pos, Vel, Dir = new V2(1f, 0f); public float OrbR, W, Phase, Seed, Age, U, J; public int Mode, Ten = -1; }
        class NsTendrilDraw { public int EnemyId, Count; public float Tip; public V2 S, C, E, Perp; public float D, Bend; public bool On; }
        class NsSwarmDraw
        {
            public readonly List<NsNeedle> N = new List<NsNeedle>();
            public readonly NsTendrilDraw[] T = { new NsTendrilDraw(), new NsTendrilDraw(), new NsTendrilDraw() };
            public float SpawnAcc, ExcessT;
            public readonly float[] Recruit = new float[3];
            public bool Seeded;
        }
        class NsMote { public V2 Pos, Vel; public float Life, Max, Size; public Rgba Col; }

        readonly Dictionary<int, NsSwarmDraw> nsPlayers = new Dictionary<int, NsSwarmDraw>();
        readonly List<NsMote> nsMotes = new List<NsMote>();
        readonly HashSet<Effect> nsSeen = new HashSet<Effect>();
        Game nsGame;
        float nsClock = -1f, nsTime;
        readonly System.Random nsRng = new System.Random(23);  // (the look's own dice: not the game's)
        float NsRand() => (float)nsRng.NextDouble();
        float NsRand(float a, float b) => a + (b - a) * NsRand();

        NsSwarmDraw NsOf(Player p)
        {
            NsSwarmDraw d;
            if (!nsPlayers.TryGetValue(p.Id, out d))
                nsPlayers[p.Id] = d = new NsSwarmDraw();
            return d;
        }

        float NsOrbitW(float r) => 2.4f * Mathf.Sqrt(48f / r) * NsRand(0.88f, 1.12f);

        NsNeedle NsMake(V2 at)
        {
            float a = NsRand(0f, MathX.Tau);
            var n = new NsNeedle
            {
                Pos = at + V2.FromAngle(a) * 3f,
                Vel = V2.FromAngle(a) * 90f + V2.FromAngle(a + MathX.Pi / 2f) * 140f,  // out and round: it spirals into the ring
                OrbR = 22f + 50f * (NsRand() + NsRand() + NsRand()) / 3f,
                Seed = NsRand(),
            };
            n.W = NsOrbitW(n.OrbR);
            n.Phase = a - nsTime * n.W;
            return n;
        }

        /// Back into the ring from wherever it is now, gliding in (a short soft spring).
        void NsToOrbit(NsNeedle n, V2 center)
        {
            n.Mode = NsOrbit;
            n.Ten = -1;
            n.Phase = (n.Pos - center).Angle - nsTime * n.W;
            n.Age = 0.15f;
        }

        void NsSpray(V2 at, int n, Rgba col, float speed, float life, float size)
        {
            for (int i = 0; i < n && nsMotes.Count < 600; i++)
                nsMotes.Add(new NsMote { Pos = at, Vel = V2.FromAngle(NsRand(0f, MathX.Tau)) * speed * NsRand(0.2f, 1f), Life = life * NsRand(0.5f, 1f), Max = life, Col = col, Size = size * NsRand(0.6f, 1.2f) });
        }

        /// Once a frame (or once a step in the preview): every swarm follows what the host sent and flies its needles.
        void NsUpdate()
        {
            if (nsGame != game)
            {
                nsGame = game;
                nsPlayers.Clear(); nsMotes.Clear(); nsSeen.Clear();
                nsClock = -1f;
            }
            float now = game.AnimT;
            if (now == nsClock)
                return;
            float dt = nsClock < 0f ? 0f : Mathf.Clamp(now - nsClock, 0f, 0.05f);
            nsClock = now;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.NeedleSwarm && nsSeen.Add(fx))
                    NsTakeFx(fx);
            nsSeen.RemoveWhere(fx => !game.Effects.Contains(fx));
            for (int step = 0; step < 2; step++)  // two half steps, as the mockup does, so the springs stay smooth
            {
                float h = dt / 2f;
                nsTime += h;
                foreach (var p in game.Players.Values)
                    NsStep(p, h);
            }
            for (int i = nsMotes.Count - 1; i >= 0; i--)
            {
                var m = nsMotes[i];
                m.Pos += m.Vel * dt;
                m.Vel *= 1f - Mathf.Min(1f, dt * 3f);
                if ((m.Life -= dt) <= 0f)
                    nsMotes.RemoveAt(i);
            }
            var gone = DrawScratch.FreshIds();
            foreach (var id in nsPlayers.Keys)
                if (!game.Players.ContainsKey(id))
                    gone.Add(id);
            foreach (var id in gone)
                nsPlayers.Remove(id);
        }

        /// A needle broken by a shot, or the Alt's burst leaving the ring.
        void NsTakeFx(Effect fx)
        {
            Player p;
            if (!game.Players.TryGetValue((int)fx.Pos2.X, out p))
                return;
            var d = NsOf(p);
            bool burst = fx.Pos2.Y > 0.5f;
            int n = burst ? Mathf.Max(1, Mathf.RoundToInt(fx.Radius)) : 1;
            for (int k = 0; k < n && d.N.Count > 0; k++)
            {
                int best = -1;
                float bd = float.MaxValue;
                for (int i = 0; i < d.N.Count; i++)
                {
                    if (burst && d.N[i].Mode == NsBind)
                        continue;
                    float dd = d.N[i].Pos.DistanceSq(fx.Pos);
                    if (dd < bd) { bd = dd; best = i; }
                }
                if (best < 0)
                    break;
                NsRemove(d, best);
            }
            if (!burst)
            {
                NsSpray(fx.Pos, 6, new Rgba(1f, 0.82f, 0.48f), 140f, 0.3f, 1.6f);
                NsSpray(fx.Pos, 4, NsBright, 90f, 0.4f, 1.8f);
            }
            d.SpawnAcc = 0f;
        }

        void NsRemove(NsSwarmDraw d, int i)
        {
            var n = d.N[i];
            if (n.Ten >= 0)
                d.T[n.Ten].Count--;
            d.N.RemoveAt(i);
        }

        static V2 NsBez(NsTendrilDraw t, float s)
        {
            float a = (1f - s) * (1f - s), b = 2f * (1f - s) * s, c = s * s;
            return t.S * a + t.C * b + t.E * c;
        }

        void NsStep(Player p, float dt)
        {
            bool has = p.Weapon.Id == WeaponId.NeedleSwarm;
            NsSwarmDraw d;
            if (!nsPlayers.TryGetValue(p.Id, out d))
            {
                if (!has || p.NsCount <= 0)
                    return;
                d = NsOf(p);
            }
            V2 P = p.Disp;
            int target = has && !p.Downed ? p.NsCount : 0;
            if (!d.Seeded && target > 0)
            {
                d.Seeded = true;  // first sight: a full ring at once, already turning
                for (int i = 0; i < target; i++)
                {
                    var n = NsMake(P);
                    float a = NsRand(0f, MathX.Tau);
                    n.Pos = P + V2.FromAngle(a) * n.OrbR;
                    n.Vel = V2.FromAngle(a + MathX.Pi / 2f) * n.OrbR * n.W;
                    n.Dir = V2.FromAngle(a + MathX.Pi / 2f);
                    n.Phase = a - nsTime * n.W;
                    n.Age = 1f;
                    d.N.Add(n);
                }
            }
            // Siphoned in: new needles spiral out of you; any left over once the host agrees fall away.
            if (d.N.Count < target)
            {
                d.SpawnAcc += 48f * dt;
                while (d.SpawnAcc >= 1f && d.N.Count < target)
                {
                    d.SpawnAcc -= 1f;
                    d.N.Add(NsMake(P));
                    if (NsRand() < 0.5f)
                        NsSpray(P, 1, NsBright, 60f, 0.35f, 2f);
                }
            }
            else
                d.SpawnAcc = 0f;
            d.ExcessT = d.N.Count > target ? d.ExcessT + dt : 0f;
            if (d.ExcessT > 0.15f || target == 0)
                while (d.N.Count > target)
                {
                    int i = d.N.Count - 1;
                    for (int j = d.N.Count - 1; j >= 0; j--)
                        if (d.N[j].Mode == NsOrbit) { i = j; break; }
                    NsSpray(d.N[i].Pos, 1, NsBody, 40f, 0.5f, 2.2f);
                    NsRemove(d, i);
                }
            // The tendrils, following the host's.
            int outNow = 0;
            for (int k = 0; k < Game.NsTendrils; k++)
                if (has && p.NsTen[k] != 0)
                    outNow++;
            for (int k = 0; k < Game.NsTendrils; k++)
            {
                var t = d.T[k];
                int id = has && !p.Downed ? p.NsTen[k] : 0;
                Enemy e = null;
                if (id != 0 && (!game.Enemies.TryGetValue(id, out e) || e.Hp <= 0f))
                    id = 0;
                if (t.On && t.EnemyId != id)
                {
                    foreach (var n in d.N)
                        if (n.Ten == k)
                            NsToOrbit(n, P);
                    t.On = false;
                    t.Count = 0;
                    if (e == null && game.Enemies.TryGetValue(t.EnemyId, out var dead) && dead.Hp <= 0f)
                        NsSpray(t.E, 18, NsBody, 200f, 0.8f, 3f);
                }
                if (id == 0)
                    continue;
                if (!t.On)
                {
                    t.On = true;
                    t.EnemyId = id;
                    t.Tip = 0f;
                    t.Bend = id * 1.7f;
                    d.Recruit[k] = 0f;
                }
                V2 E = e.Disp, to = E - P;
                float dist = Mathf.Max(1f, to.Length);
                V2 u = to / dist;
                t.S = P + u * Game.NsEdge;
                t.E = E;
                t.Perp = new V2(-u.Y, u.X);
                t.D = Mathf.Max(1f, t.S.DistanceTo(E));
                float bend = Mathf.Sin(nsTime * 1.4f + t.Bend) * 0.22f * t.D;
                t.C = (t.S + E) * 0.5f + t.Perp * bend;
                float goal = p.NsBound[k] > 0 ? 1f : Mathf.Clamp01(p.NsLen[k] / Game.NsSpan(P, E));
                float rate = Game.NsGrow / t.D;
                t.Tip = goal > t.Tip ? Mathf.Min(goal, t.Tip + rate * 1.5f * dt) : Mathf.Max(goal, t.Tip - rate * Game.NsRetract * dt);
                int want = Mathf.Max(Mathf.Max(10, p.NsBound[k]), Game.NsWrapCap(p.NsCount, outNow));
                d.Recruit[k] += 200f * dt;
                float ea = to.Angle;
                while (t.Count < want && d.Recruit[k] >= 1f)
                {
                    d.Recruit[k] -= 1f;
                    NsNeedle best = null;
                    float bd = 9f;
                    for (int s = 0; s < 24 && d.N.Count > 0; s++)
                    {
                        var n = d.N[nsRng.Next(d.N.Count)];
                        if (n.Mode != NsOrbit || n.Age < 0.6f)
                            continue;
                        float da = Mathf.Abs(MathX.Fmod((n.Pos - P).Angle - ea + MathX.Pi * 3f, MathX.Tau) - MathX.Pi);
                        if (da < bd) { bd = da; best = n; }
                    }
                    if (best == null)
                        break;
                    best.Mode = NsInTendril;
                    best.Ten = k;
                    best.U = 0f;
                    best.J = NsRand();
                    t.Count++;
                }
                d.Recruit[k] = Mathf.Min(d.Recruit[k], 2f);
            }
            if (d.N.Count == 0 && target == 0)
            {
                nsPlayers.Remove(p.Id);
                return;
            }
            // Every needle springs toward its spot.
            float breathe = 1f + 0.06f * Mathf.Sin(nsTime * 0.9f);
            foreach (var n in d.N)
            {
                n.Age += dt;
                V2 aim;
                float k = 55f;
                if (n.Mode == NsInTendril && !d.T[n.Ten].On || n.Mode == NsBind && !d.T[n.Ten].On)
                    NsToOrbit(n, P);
                if (n.Mode == NsOrbit)
                {
                    float a = n.Phase + nsTime * n.W, R = n.OrbR * breathe + 5f * Mathf.Sin(nsTime * 1.7f + n.Seed * 40f);
                    aim = P + V2.FromAngle(a) * R;
                    if (n.Age < 0.6f)
                        k *= 0.25f + 1.25f * n.Age;
                }
                else if (n.Mode == NsInTendril)
                {
                    var t = d.T[n.Ten];
                    n.U += dt * 240f / t.D;
                    if (t.Tip >= 1f)
                        n.J = Mathf.Max(0f, n.J - dt * 0.45f);
                    float s = Mathf.Min(n.U, t.Tip * (1f - n.J * 0.75f));
                    V2 b = NsBez(t, s);
                    float thick = 3f + 7f * Mathf.Sin(MathX.Pi * Mathf.Min(1f, s / Mathf.Max(0.05f, t.Tip)));
                    aim = b + t.Perp * (Mathf.Sin(nsTime * 6f + n.Seed * 60f) * thick);
                    k = 90f;
                    if (t.Tip >= 1f && s > 0.97f)
                        n.Mode = NsBind;
                }
                else
                {
                    var t = d.T[n.Ten];
                    Enemy e;
                    V2 at = game.Enemies.TryGetValue(t.EnemyId, out e) ? e.Disp : t.E;
                    float er = e != null ? e.Radius : 12f;
                    float a = n.Seed * MathX.Tau + nsTime * (5.5f + n.Seed * 2f), R = er + 5f + n.Seed * 9f + 2f * Mathf.Sin(nsTime * 7f + n.Seed * 30f);
                    aim = at + V2.FromAngle(a) * R;
                    k = 160f;
                }
                float c = 2f * Mathf.Sqrt(k) * 0.75f;
                n.Vel += ((aim - n.Pos) * k - n.Vel * c) * dt;
                n.Pos += n.Vel * dt;
                float v = n.Vel.Length;
                if (v > 15f)
                {
                    n.Dir += (n.Vel / v - n.Dir) * Mathf.Min(1f, dt * 14f);
                    float l = n.Dir.Length;
                    n.Dir = l > 1e-4f ? n.Dir / l : new V2(1f, 0f);
                }
            }
            if (p.Downed && d.N.Count > 0)
                d.N.Clear();
        }

        /// A player's swarm and tendrils, under the player (DrawPlayerWrapped).
        void DrawNeedleSwarm(Player p, float t)
        {
            NsUpdate();
            NsSwarmDraw d;
            if (!nsPlayers.TryGetValue(p.Id, out d))
                return;
            foreach (var tt in d.T)  // the tendrils' dark underglow and their glowing heads
            {
                if (!tt.On || tt.Tip <= 0f)
                    continue;
                V2 last = tt.S;
                for (int i = 1; i <= 20; i++)
                {
                    V2 q = NsBez(tt, tt.Tip * i / 20f);
                    Line(last, q, 9f, NsUnder);
                    last = q;
                }
                GlowDisc(last, 16f, NsBright, 0.45f);
            }
            if (d.N.Count > 0)  // a faint red halo in the ring
            {
                V2 c = p.Disp;
                float r0 = 10f, r1 = 52f, r2 = 95f;
                var clear = NsHalo.WithAlpha(0f);
                var mid = NsHalo.WithAlpha(0.16f * Mathf.Min(1f, d.N.Count / 60f));
                const int segs = 24;
                for (int i = 0; i < segs; i++)
                {
                    V2 a0 = V2.FromAngle(MathX.Tau * i / segs), a1 = V2.FromAngle(MathX.Tau * (i + 1) / segs);
                    VTri(c + a0 * r0, clear, c + a1 * r0, clear, c + a1 * r1, mid);
                    VTri(c + a0 * r0, clear, c + a1 * r1, mid, c + a0 * r1, mid);
                    VTri(c + a0 * r2, clear, c + a1 * r2, clear, c + a1 * r1, mid);
                    VTri(c + a0 * r2, clear, c + a1 * r1, mid, c + a0 * r1, mid);
                }
            }
            foreach (var n in d.N)
                Line(n.Pos - n.Dir * 9f, n.Pos + n.Dir * 3f, 5f, NsGlow);
            foreach (var n in d.N)
                Line(n.Pos - n.Dir * 9f, n.Pos + n.Dir * 2f, 2f, NsBody);
            foreach (var n in d.N)
                Line(n.Pos, n.Pos + n.Dir * 3.5f, 1.1f, NsTip);
            DrawNsMotes();
        }

        int nsMotesFrame = -1;

        void DrawNsMotes()
        {
            if (nsMotesFrame == Time.frameCount)
                return;  // (once a frame, whoever's swarm is drawn first)
            nsMotesFrame = Time.frameCount;
            foreach (var m in nsMotes)
            {
                float a = Mathf.Clamp01(m.Life / m.Max), s = m.Size / 2f;
                SetColor(m.Col.WithAlpha(m.Col.A * a));
                Quad(m.Pos + new V2(-s, -s), m.Pos + new V2(s, -s), m.Pos + new V2(s, s), m.Pos + new V2(-s, s));
            }
        }

        /// FxKind.NeedleSwarm: a shot breaking a needle (a little gold star), or the Alt's burst leaving the ring (a red flash).
        void DrawNeedleSwarmFx(Effect fx, float f)
        {
            if (fx.Pos2.Y > 0.5f)
            {
                GlowDisc(fx.Pos, 26f, NsBright, 0.5f * (1f - f));
                return;
            }
            float r = 3f + 9f * f;
            var c = new Rgba(1f, 0.85f, 0.55f, 1f - f);
            for (int i = 0; i < 4; i++)
            {
                V2 dd = V2.FromAngle(i * MathX.Pi / 2f + fx.Pos.X);
                Line(fx.Pos + dd * (r * 0.4f), fx.Pos + dd * r, 1.4f, c);
            }
        }
    }
}
