using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Soldier DPS abilities' looks (mockup: Docs/Mockups/soldier-dps-abilities.html), all drawing only. With Visual
    /// effects off, none of this runs and the old looks draw as before.
    ///   Dashes          B, Comet Streak (made for the old Dash, which Poison Dart replaced; PoisonDartLook.cs): a tapered cyan
    ///                   ribbon behind your own Game.DashT movement (Leap Slam, the Royal Dive...), cooling off over 0.4 s, sparks off it,
    ///                   and a ring of sparks where you land;
    ///   Grenade (the Twin Rifle's tubes; Airstrike's bombs use the blast) A, Frag Burst: an olive grenade with a faint ring growing to its blast radius, then a white flash,
    ///                   an orange blast out to the radius, eight streaking shards and smoke (the scorch is Juice.cs's);
    ///   Mark for Death  A, Reticle Lock, with B's skull floating over it: the reticle snaps down and circles the enemy, a
    ///                   timer arc unwinds round it, and both blink for the last second.
    public partial class ArenaApp
    {
        const byte PFrag = 11;  // particle kind: a grenade's shrapnel streak (Juice.cs draws it through DrawFragPart)
        const float DashStreakFade = 0.4f, MarkLife = 6f, MarkSnap = 0.25f;

        static readonly Rgba DashTail = new Rgba(0.31f, 0.78f, 1f), DashHead = new Rgba(0.78f, 0.98f, 1f);
        static readonly Rgba FragFlash = new Rgba(1f, 0.94f, 0.75f), FragOrange = new Rgba(1f, 0.55f, 0.16f), FragGold = new Rgba(1f, 0.78f, 0.31f),
            FragSmoke = new Rgba(0.35f, 0.31f, 0.29f), NadeOlive = new Rgba(0.36f, 0.42f, 0.24f);
        static readonly Rgba MarkRed = new Rgba(1f, 0.25f, 0.25f), MarkBone = new Rgba(0.93f, 0.9f, 0.86f);

        class MarkSeen { public float Start, Last; }

        bool dashWas;
        V2 dashFrom, dashTo;
        float dashEndT = -99f;
        readonly Dictionary<int, MarkSeen> markSeen = new Dictionary<int, MarkSeen>();

        void ClearSoldierLooks()
        {
            dashWas = false;
            dashEndT = -99f;
            markSeen.Clear();
        }

        static float Hash01(float s)
        {
            float x = Mathf.Sin(s * 127.1f + 311.7f) * 43758.5453f;
            return x - (float)System.Math.Floor(x);
        }

        // --- Dash: Comet Streak ----------------------------------------------------------------------------------------

        /// Your dash's ribbon, under your model (DrawPlayer). Remembers where the dash began and where it is now.
        void DrawDashStreak(Player p, float t)
        {
            if (t < dashEndT - 0.5f)  // the clock went back (a new match)
                ClearSoldierLooks();
            if (game.DashT > 0f)
            {
                if (!dashWas)
                {
                    dashWas = true;
                    dashFrom = p.Disp;
                }
                dashTo = p.Disp;
                dashEndT = t;
            }
            else if (dashWas)  // it just ended: sparks fly out where you landed
            {
                dashWas = false;
                dashEndT = t;
                for (int i = 0; i < 6; i++)
                {
                    V2 d = V2.FromAngle(MathX.Tau * i / 6f);
                    AddPart(new Part { Kind = PSpark, Pos = dashTo + d * 14f, Vel = d * 90f, Life = 0.25f, Max = 0.25f, Size = 2f, Col = DashHead });
                }
            }
            float fade = dashWas ? 1f : 1f - (t - dashEndT) / DashStreakFade;
            V2 delta = dashTo - dashFrom;
            float len = delta.Length;
            if (fade <= 0f || len < 6f)
                return;
            V2 dir = delta / len, n = dir.Orthogonal();
            const int Segs = 8;
            for (int i = 0; i < Segs; i++)  // the tail is thin and clear; the head is wide and bright
            {
                float a = (float)i / Segs, b = (float)(i + 1) / Segs, w0 = 11f * fade * a, w1 = 11f * fade * b;
                SetColor(DashTail.Mix(DashHead, (a + b) * 0.5f).WithAlpha(0.85f * fade * (a + b) * 0.5f));
                Quad(dashFrom + delta * a + n * w0, dashFrom + delta * b + n * w1, dashFrom + delta * b - n * w1, dashFrom + delta * a - n * w0);
            }
            if (len > 8f)
                for (int k = 0; k < 9; k++)  // sparks left along it
                {
                    float u = Hash01(k + 3f);
                    V2 at = dashFrom + delta * u + n * ((Hash01(k + 9f) - 0.5f) * 30f);
                    Line(at, at + dir * 5f, 1.5f, DashHead.WithAlpha(0.8f * fade * (1f - u * 0.4f)));
                }
        }

        // --- Grenades (Twin Rifle tubes) and Airstrike bombs: Frag Burst -------------------------------------------------------------------------------

        /// A grenade's blast: Combat.cs sends it as a Boom in the grenade shot's colour (nothing else uses that colour).
        bool IsGrenadeBoom(Effect fx)
        {
            if (!fxOn || fx.Kind != FxKind.Boom)
                return false;
            var c = Defs.BulletColor(Defs.GrenadeShot);
            return fx.Color.R == c.R && fx.Color.G == c.G && fx.Color.B == c.B;
        }

        /// The blast itself, over the arena for its 0.35 s: a flash, then the orange disc and ring out to the radius.
        void DrawFragBlast(Effect fx, float f)
        {
            float s = fx.Radius / 110f, k = EaseOut3(Mathf.Clamp01(f / 0.7f));
            if (f < 0.3f)
                Circle(fx.Pos, (34f + 30f * f / 0.3f) * s, FragFlash.WithAlpha(1f - f / 0.3f), 20);
            Circle(fx.Pos, fx.Radius * k, FragOrange.WithAlpha(0.22f * (1f - f)), 32);
            Arc(fx.Pos, fx.Radius * k, 3f, FragGold.WithAlpha(0.9f * (1f - f)), 40);
        }

        /// What the blast leaves flying: eight shards and a few puffs of smoke (once, from Juice's new-effects pass).
        void FragBurst(Effect fx)
        {
            float off = Rnd(0f, MathX.Tau);
            for (int i = 0; i < 8; i++)  // (the real shrapnel's angles aren't known here, so these are for looks)
            {
                V2 d = V2.FromAngle(off + MathX.Tau * i / 8f);
                AddPart(new Part { Kind = PFrag, Pos = fx.Pos + d * 8f, Vel = d * 620f, Life = 0.4f, Max = 0.4f, Size = 16f, Col = FragGold });
            }
            for (int i = 0; i < 6; i++)
            {
                V2 d = V2.FromAngle(MathX.Tau * i / 6f + off);
                AddPart(new Part { Kind = PDust, Pos = fx.Pos, Vel = d * 80f + new V2(0f, -18f), Life = 1.1f, Max = 1.1f, Size = 12f + Rnd(0f, 8f), Col = FragSmoke });
            }
        }

        void DrawFragPart(Part p, float f)
        {
            V2 d = p.Vel.Normalized();
            Line(p.Pos - d * p.Size, p.Pos, 2.5f, p.Col.WithAlpha(f));
            Circle(p.Pos, 1.8f, FragFlash.WithAlpha(f), 6);
        }

        /// A grenade in the air (DrawBigShot): a faint ring growing to the blast radius where it will land, and the grenade
        /// itself, olive with a dark band, spinning, its fuse blinking red.
        void DrawGrenadeFlight(Bullet b, float t)
        {
            float u = b.Life0 > 0f ? Mathf.Clamp01(1f - b.Life / b.Life0) : 0.5f;
            float R = (b.Splash > 0f ? b.Splash : Game.GrenadeRadius) * EaseOut3(u);  // (only the host knows a mastered radius)
            V2 land = b.Pos + b.Vel * b.Life;
            int dashes = Mathf.Max(6, (int)(MathX.Tau * R / 12f));
            for (int i = 0; i < dashes; i++)
                Arc(land, R, 1.5f, FragOrange.WithAlpha(0.28f), 2, MathX.Tau * i / dashes, MathX.Tau / dashes * 0.5f);
            DrawRocketNade(b, t);  // Rocket assist: nose first, with its flame (TwinNadeLook.cs)
        }

        // --- Mark for Death: Reticle Lock, with the skull ---------------------------------------------------------------

        /// A marked enemy's reticle and skull (DrawEnemyStatus). Only a "marked" flag reaches clients, so each draws its own
        /// 6 s clock from when it first sees the mark (a re-cast, which refills it, restarts it).
        void DrawMarkLook(Enemy e, V2 pos, float r, float t)
        {
            MarkSeen m;
            if (!markSeen.TryGetValue(e.Id, out m) || t - m.Last > 0.5f || t < m.Start)
                markSeen[e.Id] = m = new MarkSeen { Start = t };
            if (t - m.Start >= MarkLife)
                m.Start = t;  // still marked after its 6 s: it was cast again
            m.Last = t;
            if (markSeen.Count > 48)
                PruneMarks(t);
            float age = t - m.Start, rem = Mathf.Clamp01(1f - age / MarkLife);
            float al = rem > 0.17f || MathX.Fmod(t, 0.2f) < 0.1f ? 1f : 0.25f;  // the last second blinks
            float pulse = e.Flash > 0f ? 0.18f : 0f;
            float sc = (age < MarkSnap ? 2.4f - 1.4f * EaseOut3(age / MarkSnap) : 1f) + pulse, rr = (r + 14f) * sc;
            for (int k = 0; k < 4; k++)  // four turning arcs, each with a tick pointing out
            {
                float a0 = t * 0.9f + k * MathX.Pi / 2f;
                Arc(pos, rr, 2.5f, MarkRed.WithAlpha(al), 6, a0 + 0.25f, MathX.Pi / 2f - 0.5f);
                V2 d = V2.FromAngle(a0);
                Line(pos + d * (rr - 6f), pos + d * (rr + 7f), 2.5f, MarkRed.WithAlpha(al));
            }
            Arc(pos, (r + 24f) * Mathf.Min(1f, sc * 0.9f + 0.1f), 2f, new Rgba(1f, 0.59f, 0.51f, 0.85f * al), 32, -MathX.Pi / 2f, MathX.Tau * rem);  // time left
            DrawMarkSkull(pos, r, t, age, al, pulse > 0f);
        }

        /// A small skull bobbing over the enemy; its eyes flare when the enemy is hit.
        void DrawMarkSkull(V2 pos, float r, float t, float age, float al, bool hit)
        {
            float s = age < 0.3f ? EaseOut3(age / 0.3f) : 1f;
            V2 c = pos + new V2(0f, -(r + 26f) + Mathf.Sin(t * 3f) * 2.5f - (1f - s) * 14f);
            float u = 0.8f * s;  // the mockup's skull is 9 px wide; this one is a little smaller
            Circle(c + new V2(0f, -u), 9f * u, MarkBone.WithAlpha(al), 14);
            SetColor(MarkBone.WithAlpha(al));
            Quad(c + new V2(-5f * u, 4f * u), c + new V2(5f * u, 4f * u), c + new V2(5f * u, 10f * u), c + new V2(-5f * u, 10f * u));
            SetColor(new Rgba(0.07f, 0.09f, 0.11f, al));
            Quad(c + new V2(-2.5f * u, 7f * u), c + new V2(-1f * u, 7f * u), c + new V2(-1f * u, 10f * u), c + new V2(-2.5f * u, 10f * u));
            Quad(c + new V2(1f * u, 7f * u), c + new V2(2.5f * u, 7f * u), c + new V2(2.5f * u, 10f * u), c + new V2(1f * u, 10f * u));
            if (hit)
                Circle(c + new V2(0f, -2f * u), 7f * u, MarkRed.WithAlpha(0.3f * al), 10);
            Circle(c + new V2(-3.5f * u, -2f * u), 2.4f * u, MarkRed.WithAlpha(al), 8);
            Circle(c + new V2(3.5f * u, -2f * u), 2.4f * u, MarkRed.WithAlpha(al), 8);
        }

        void PruneMarks(float t)
        {
            var gone = new List<int>();
            foreach (var kv in markSeen)
                if (t - kv.Value.Last > 0.5f)
                    gone.Add(kv.Key);
            foreach (int id in gone)
                markSeen.Remove(id);
        }
    }
}
