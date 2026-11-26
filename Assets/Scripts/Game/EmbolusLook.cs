using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The DPS Hemomancer's Embolus Spears, look A Clot Javelin (Docs/Mockups/embolus-spears.html). Drawing only: the throws,
    /// recalls and bursts are Core/EmbolusSpears.cs.
    ///   Held (CharacterModels.cs DrawHeld): a short javelin in each hand, arms out; an empty hand drips; a new one hardens in
    ///     (EsHeldGrow).
    ///   Out (Player.EsSt/EsPos/EsDir): a dark congealed shaft with a faceted ruby head, a dotted blood thread back to its hand.
    ///     Flying and returning it leaves a drip trail; stuck it shudders, cracks the wall where it went in, and its butt pulses.
    ///     Other players' spears are moved on between snapshots (EsSpeed, EsBackSpeed) so they fly smoothly.
    ///   The burst (FxKind.EmbolusBlast): a flash, one heavy ring and a spray of clots.
    public partial class ArenaApp
    {
        static readonly Rgba EsShaft = Hx(0x7a1020), EsShine = Hx(0xe0303f), EsHead = Hx(0xb0142a), EsEdge = Hx(0xff6b7d), EsDrip = Hx(0xa3101f);
        const float EsWobble = 0.35f;

        class EsSpearDraw { public V2 Pos, Last; public int St = -1; public float Since; public readonly List<V2> Trail = new List<V2>(); public V2 Crack; public float Seed; }
        class EsMote { public V2 Pos, Vel; public float Age, Life, Size; }

        readonly Dictionary<int, EsSpearDraw[]> esPlayers = new Dictionary<int, EsSpearDraw[]>();
        readonly List<EsMote> esMotes = new List<EsMote>();
        readonly HashSet<Effect> esBlastsSeen = new HashSet<Effect>();
        Game esGame;
        float esClock = -1f;
        int esFrame = -1;
        readonly System.Random esRng = new System.Random(7);  // (the drips and clots only: not the game's dice)
        float EsRand() => (float)esRng.NextDouble();
        float EsRand(float a, float b) => a + (b - a) * EsRand();

        EsSpearDraw[] EsOf(Player p)
        {
            EsSpearDraw[] d;
            if (!esPlayers.TryGetValue(p.Id, out d))
                esPlayers[p.Id] = d = new[] { new EsSpearDraw(), new EsSpearDraw() };
            return d;
        }

        /// How much of hand i's spear is in the hand, 0..1 (1 held, rising while one hardens, 0 while it's out): CharacterModels.cs.
        float EsHeldGrow(Player p, int i)
        {
            int st = p.EsSt[i];
            if (st == Game.EsInHand)
                return 1f;
            if (st != Game.EsHardening)
                return 0f;
            EsUpdate();
            return Mathf.Clamp01(EsOf(p)[i].Since / Game.EsRegrow);
        }

        /// Once a frame: each spear's drawn place and trail, what changed since the last frame, the bursts' clots and the drips.
        void EsUpdate()
        {
            if (esFrame == Time.frameCount && esGame == game && esClock == game.AnimT)
                return;
            esFrame = Time.frameCount;
            if (esGame != game)
            {
                esGame = game;
                esPlayers.Clear(); esMotes.Clear(); esBlastsSeen.Clear();
                esClock = -1f;
            }
            float now = game.AnimT, dt = esClock < 0f ? 0f : Mathf.Clamp(now - esClock, 0f, 0.1f);
            esClock = now;
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon.Id != WeaponId.EmbolusSpears)
                    continue;
                var d = EsOf(p);
                for (int i = 0; i < 2; i++)
                    EsTrack(p, i, d[i], dt);
            }
            foreach (var fx in FxOf(FxKind.EmbolusBlast))
                if (fx.Kind == FxKind.EmbolusBlast && esBlastsSeen.Add(fx))
                    EsSpray(fx.Pos, 26, 420f, 3.2f);
            esBlastsSeen.RemoveWhere(fx => !game.Effects.Contains(fx));
            for (int i = esMotes.Count - 1; i >= 0; i--)
            {
                var m = esMotes[i];
                m.Age += dt;
                m.Pos += m.Vel * dt;
                m.Vel *= Mathf.Max(0f, 1f - dt * 5f);
                if (m.Age >= m.Life)
                    esMotes.RemoveAt(i);
            }
        }

        void EsTrack(Player p, int i, EsSpearDraw s, float dt)
        {
            int st = p.EsSt[i];
            bool changed = st != s.St;
            V2 hand = Game.EsHand(p.Disp, p.Aim, i);
            if (changed)
            {
                if (s.St == Game.EsReturning && st == Game.EsInHand)
                    EsSpray(hand, 8, 120f, 1.8f);  // caught
                if (st == Game.EsFlying)
                    EsSpray(hand, 7, 150f, 1.6f);  // thrown: blood off the hand
                if (st == Game.EsStuck)
                {
                    s.Crack = p.EsPos[i] + p.EsDir[i] * (Game.EsLen / 2f - Game.EsSink);
                    s.Seed = EsRand() * 99f;
                    EsSpray(s.Crack, 10, 200f, 2f);
                }
                s.St = st;
                s.Since = 0f;
                s.Pos = p.EsPos[i];
                s.Trail.Clear();
            }
            else
                s.Since += dt;
            bool fresh = changed || p.EsPos[i].X != s.Last.X || p.EsPos[i].Y != s.Last.Y;
            s.Last = p.EsPos[i];
            if (fresh || st != Game.EsFlying && st != Game.EsReturning)
                s.Pos = p.EsPos[i];  // (the host moves it every tick; another screen gets it 20 times a second and moves it on between)
            else if (st == Game.EsFlying)
                s.Pos += p.EsDir[i] * (Game.EsSpeed * dt);
            else
            {
                V2 to = hand - s.Pos;
                float len = to.Length, move = Game.EsBackSpeed * dt;
                s.Pos = len > move ? s.Pos + to / len * move : hand;
            }
            if ((st == Game.EsFlying || st == Game.EsReturning) && dt > 0f)
            {
                s.Trail.Add(s.Pos);
                if (s.Trail.Count > 10)
                    s.Trail.RemoveAt(0);
                if (EsRand() < dt * 40f)  // drips off it as it goes
                    esMotes.Add(new EsMote { Pos = s.Pos, Vel = new V2(EsRand(-40f, 40f), EsRand(-40f, 40f)), Life = EsRand(0.25f, 0.5f), Size = EsRand(1.2f, 2.2f) });
            }
        }

        void EsSpray(V2 at, int n, float speed, float size)
        {
            for (int k = 0; k < n; k++)
            {
                float a = EsRand() * MathX.Tau, v = speed * EsRand(0.3f, 1f);
                esMotes.Add(new EsMote { Pos = at, Vel = V2.FromAngle(a) * v, Life = EsRand(0.3f, 0.7f), Size = size * EsRand(0.6f, 1.2f) });
            }
        }

        /// A player's spears out of their hands, their threads and trails, the cracks they made and the drips (DrawPlayerWrapped).
        void DrawEmbolus(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.EmbolusSpears)
                return;
            EsUpdate();
            var d = EsOf(p);
            for (int i = 0; i < 2; i++)
            {
                var s = d[i];
                int st = p.EsSt[i];
                if (!Game.EsOut(st))
                    continue;
                V2 dir = p.EsDir[i].IsZero ? V2.FromAngle(p.Aim) : p.EsDir[i];
                V2 hand = Game.EsHand(p.Disp, p.Aim, i), butt = s.Pos - dir * (Game.EsLen / 2f);
                // the thread back to the hand: dotted, crawling toward you, brighter while it reels the spear in
                V2 span = butt - hand;
                float len = span.Length;
                if (len > 8f)
                {
                    var tc = st == Game.EsReturning ? EsEdge.WithAlpha(0.7f) : EsHead.WithAlpha(0.45f);
                    float off = (t * 40f) % 10f;
                    for (float u = off; u < len; u += 10f)
                        Line(hand + span * (u / len), hand + span * (Mathf.Min(len, u + 3f) / len), 1.2f, tc);
                }
                for (int k = 1; k < s.Trail.Count; k++)
                {
                    float a = (float)k / s.Trail.Count;
                    Line(s.Trail[k - 1], s.Trail[k], 4f * a, EsDrip.WithAlpha(0.55f * a));
                }
                float ang = dir.Angle;
                if (st == Game.EsStuck)
                {
                    float w = Mathf.Max(0f, EsWobble - s.Since);
                    ang += Mathf.Sin(w * 60f) * w * 0.5f;  // it shudders as it bites
                    EsCrack(s.Crack, dir.Angle, s.Seed);
                }
                EsJavelin(s.Pos, ang, 1f, 1f, st != Game.EsStuck);
                if (st == Game.EsStuck)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f + i);  // ready to be called back
                    Circle(butt, 3f + 2f * pulse, EsEdge.WithAlpha(0.25f + 0.3f * pulse), 10);
                }
            }
            foreach (var m in esMotes)
                Circle(m.Pos, m.Size * (1f - 0.4f * m.Age / m.Life), EsDrip.WithAlpha(1f - m.Age / m.Life), 6);
        }

        /// The wall split round where a spear went in: five short cracks and a dark splat.
        void EsCrack(V2 at, float ang, float seed)
        {
            for (int k = 0; k < 5; k++)
            {
                float a = ang + MathX.Pi + (k - 2) * 0.45f + Mathf.Sin(seed + k) * 0.2f, r = 8f + (k % 2) * 6f;
                Line(at, at + V2.FromAngle(a) * r, 1.1f, EsEdge.WithAlpha(0.55f));
            }
            Circle(at, 3.5f, EsShaft, 10);
        }

        /// One Clot Javelin, its middle at `at`, point along `ang`: a dark shaft with a red shine, a faceted ruby head, a clot at the butt.
        void EsJavelin(V2 at, float ang, float scale, float alpha, bool moving)
        {
            V2 d = V2.FromAngle(ang) * scale, s = new V2(-d.Y, d.X);
            float h = Game.EsLen / 2f;
            System.Func<float, float, V2> P = (x, y) => at + d * x + s * y;
            Line(P(-h - 2f, 0f), P(h + 6f, 0f), (moving ? 12f : 9f) * scale, EsEdge.WithAlpha((moving ? 0.22f : 0.14f) * alpha));  // a wet red glow round it
            Line(P(-h, 0f), P(h + 3f, 0f), 7f * scale, Hx(0x2a0408, 0.6f * alpha));  // a dark rim so it reads on any floor
            Line(P(-h, 0f), P(h - 12f, 0f), 4.5f * scale, EsShaft.WithAlpha(alpha));
            Line(P(-h + 3f, -1.2f), P(h - 14f, -1.2f), 1.3f * scale, EsShine.WithAlpha(alpha));
            SetColor(EsHead.WithAlpha(alpha));
            Tri(P(h - 17f, 0f), P(h - 8f, -5.5f), P(h + 5f, 0f));
            Tri(P(h - 17f, 0f), P(h + 5f, 0f), P(h - 8f, 5.5f));
            var e = EsEdge.WithAlpha(alpha);
            Line(P(h - 17f, 0f), P(h - 8f, -5.5f), 1f, e);
            Line(P(h - 8f, -5.5f), P(h + 5f, 0f), 1f, e);
            Line(P(h + 5f, 0f), P(h - 8f, 5.5f), 1f, e);
            Line(P(h - 8f, 5.5f), P(h - 17f, 0f), 1f, e);
            Line(P(h - 17f, 0f), P(h + 5f, 0f), 0.8f * scale, e);
            Circle(P(-h, 0f), 3f * scale, EsShaft.WithAlpha(alpha), 8);
        }

        /// FxKind.EmbolusBlast: a flash, a dark burst filling out, one heavy ring (thick, thinning) and an inner one.
        void DrawEmbolusBlastFx(Effect fx)
        {
            float k = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), R = fx.Radius, r = R * Mathf.Min(1f, k * 2.2f);
            if (fx.T < 0.08f)
                Circle(fx.Pos, R * 0.6f, Hx(0xff3d55, 0.8f * (1f - fx.T / 0.08f)), 24);
            Circle(fx.Pos, r, Hx(0x5a0610, 0.55f * (1f - k)), 28);
            Arc(fx.Pos, r, 6f * (1f - k) + 1.5f, EsShine.WithAlpha(1f - k), 32);
            Arc(fx.Pos, r * 0.65f, 1.5f, EsEdge.WithAlpha(0.8f * (1f - k)), 24);
        }

        /// Hand i's spear in the model's frame (grip at the origin, +x along the aim): CharacterModels.cs DrawHeld.
        void DrawEsHeld(Player p, int i, float t)
        {
            float g = EsHeldGrow(p, i);
            if (g <= 0.01f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 10f + i);  // an empty hand: blood beading on the palm
                MCirc(2f, 0f, 2f + pulse, Hx(0xc3142a), 8);
                return;
            }
            float L = 40f * g, x0 = -8f;
            MLine(x0, 0f, x0 + L, 0f, 6f, EsEdge.WithAlpha(0.2f));
            MLine(x0, 0f, x0 + L - 7f, 0f, 3.4f, EsShaft);
            MLine(x0 + 2f, -0.8f, x0 + L - 8f, -0.8f, 0.9f, EsShine);
            MPoly(EsHead, x0 + L - 11f, 0f, x0 + L - 5f, -4f * g, x0 + L + 4f, 0f, x0 + L - 5f, 4f * g);
            MLine(x0 + L - 11f, 0f, x0 + L + 4f, 0f, 0.8f, EsEdge);
        }
    }
}
