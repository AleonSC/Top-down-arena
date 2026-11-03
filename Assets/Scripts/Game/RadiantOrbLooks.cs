using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Radiant Orb's trail with Visual effects on: style B, "Stardust" (Docs/Mockups/radiant-orb-trails.html, the
    /// owner's pick). The orb sheds glittering motes and four-point sparkles that drift off and twinkle out, over a thin
    /// faint tail of where it has just been; it keeps shedding a little when it sits still. The orb's Light Orbs (Multishot,
    /// Barrage) get the same at half size. All light, so it's drawn in the additive glow pass (DrawRadiantStardust, from
    /// DrawGlowPass), which the lobby's loadout preview runs too. The front end replays where each orb has been
    /// (UpdateRadiantStardust, from UpdateJuice): nothing is sent over the network. Off, the orb draws as before.
    public partial class ArenaApp
    {
        const float StardustTail = 0.55f;                      // seconds of path the faint tail keeps
        const float StardustPerPx = 1f / 5f, StardustPerSec = 26f;  // motes shed: one every 5 px it travels, and 26 a second

        struct Mote { public V2 Pos, Vel; public float Age, Life, Size, Tw, Warm; public bool Star; public float Rot; }
        struct TailPt { public V2 Pos; public float T; }

        class RadiantTrail
        {
            public V2 Last; public bool Has, Seen; public float Acc, K;
            public readonly List<Mote> Motes = new List<Mote>();
            public readonly List<TailPt> Tail = new List<TailPt>();
        }

        readonly Dictionary<int, RadiantTrail> radiantTrails = new Dictionary<int, RadiantTrail>();
        const int LightOrbKey = 1 << 20;  // Light Orbs' trails are keyed after the players'

        /// Each frame (UpdateJuice): follow every Radiant Orb and its Light Orbs, shed motes, age what's shed.
        void UpdateRadiantStardust(float dt)
        {
            foreach (var tr in radiantTrails.Values)
                tr.Seen = false;
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.RadiantOrb && !p.Downed)
                {
                    if (p.RadiantOn)
                        FeedStardust(p.Id, p.RadiantDisp, Game.RadiantSize(p) / Game.RadiantRadius, dt);
                }
            foreach (var m in game.Minions.Values)
            {
                Player owner;
                if (m.Kind == MinionKind.LightOrb && game.Players.TryGetValue(m.Owner, out owner) && owner.Weapon.Id == WeaponId.RadiantOrb && !owner.Downed)
                    FeedStardust(LightOrbKey + m.Id, m.Disp, 0.5f, dt);
            }
            List<int> gone = null;
            foreach (var kv in radiantTrails)
            {
                var tr = kv.Value;
                if (!tr.Seen)
                    tr.Has = false;  // the orb let go: what it shed fades out, then the trail goes
                for (int i = tr.Motes.Count - 1; i >= 0; i--)
                {
                    var m = tr.Motes[i];
                    m.Age += dt / m.Life;
                    if (m.Age >= 1f) { tr.Motes.RemoveAt(i); continue; }
                    m.Pos += m.Vel * dt;
                    m.Vel *= Mathf.Pow(0.9f, dt * 60f);
                    tr.Motes[i] = m;
                }
                while (tr.Tail.Count > 0 && juiceClock - tr.Tail[0].T > StardustTail)
                    tr.Tail.RemoveAt(0);
                if (!tr.Seen && tr.Motes.Count == 0 && tr.Tail.Count == 0)
                    (gone ?? (gone = new List<int>())).Add(kv.Key);
            }
            if (gone != null)
                foreach (var k in gone)
                    radiantTrails.Remove(k);
        }

        void FeedStardust(int key, V2 at, float k, float dt)
        {
            RadiantTrail tr;
            if (!radiantTrails.TryGetValue(key, out tr))
                radiantTrails[key] = tr = new RadiantTrail();
            tr.Seen = true;
            tr.K = k;
            float dist = tr.Has ? at.DistanceTo(tr.Last) : 0f;
            if (dist > 260f)  // a jump (a new cast, a respawn): no streak across it
            {
                tr.Tail.Clear();
                dist = 0f;
            }
            V2 from = tr.Has && dist > 0f ? tr.Last : at;
            tr.Acc += dist * StardustPerPx + dt * StardustPerSec;
            while (tr.Acc >= 1f)
            {
                tr.Acc -= 1f;
                float a = Rnd(0f, MathX.Tau), sp = 14f + Rnd(0f, 1f) * 44f * k, f = Rnd(0f, 1f);
                V2 at0 = from + (at - from) * f + V2.FromAngle(Rnd(0f, MathX.Tau)) * (Rnd(0f, 1f) * RadiantRadius0 * 0.55f * k);
                tr.Motes.Add(new Mote
                {
                    Pos = at0, Vel = V2.FromAngle(a) * sp + new V2(0f, -6f), Life = Rnd(0.45f, 1f), Size = (1.2f + Rnd(0f, 2.6f)) * k,
                    Star = Rnd(0f, 1f) < 0.35f, Rot = Rnd(0f, 3f), Tw = Rnd(0f, MathX.Tau), Warm = Rnd(0f, 1f),
                });
            }
            tr.Tail.Add(new TailPt { Pos = at, T = juiceClock });
            tr.Last = at;
            tr.Has = true;
        }

        const float RadiantRadius0 = Game.RadiantRadius;

        /// The glow pass: every orb's faint tail, then its motes and sparkles.
        void DrawRadiantStardust()
        {
            var light = Defs.BulletColor(Defs.HolyShot);
            var warm = new Rgba(1f, 0.74f, 0.3f);
            foreach (var tr in radiantTrails.Values)
            {
                float k = tr.K;
                for (int i = 1; i < tr.Tail.Count; i++)
                {
                    float f0 = Mathf.Clamp01(1f - (juiceClock - tr.Tail[i - 1].T) / StardustTail), f1 = Mathf.Clamp01(1f - (juiceClock - tr.Tail[i].T) / StardustTail);
                    float p0 = Mathf.Pow(f0, 1.25f), p1 = Mathf.Pow(f1, 1.25f);
                    Ribbon(tr.Tail[i - 1].Pos, tr.Tail[i].Pos, RadiantRadius0 * 0.75f * k * 0.5f * p0, RadiantRadius0 * 0.75f * k * 0.5f * p1,
                        warm.WithAlpha(0.2f * p0), warm.WithAlpha(0.2f * p1));
                    Line(tr.Tail[i - 1].Pos, tr.Tail[i].Pos, Mathf.Max(0.6f, 1.5f * k * p1), Rgba.White.WithAlpha(0.3f * p1));
                }
                foreach (var m in tr.Motes)
                {
                    float f = 1f - m.Age, tw = 0.65f + 0.35f * Mathf.Sin(juiceClock * 22f + m.Tw), s = m.Size * (0.4f + 0.6f * f);
                    Rgba c = StardustMix(Rgba.White, StardustMix(light, warm, m.Warm), m.Age);
                    GlowDisc(m.Pos, s * 4.5f, c, 0.55f * f);
                    if (m.Star)
                    {
                        float r = s * 3.2f * tw, a = m.Rot + m.Age * 2f, w = Mathf.Max(0.8f, s * 0.35f);
                        V2 d0 = V2.FromAngle(a) * r, d1 = V2.FromAngle(a + MathX.Pi / 2f) * r;
                        Line(m.Pos - d0, m.Pos + d0, w, c.WithAlpha(0.9f * f));
                        Line(m.Pos - d1, m.Pos + d1, w, c.WithAlpha(0.9f * f));
                    }
                    else
                        Square(m.Pos, Mathf.Max(1.2f, s * 1.2f), Rgba.White.WithAlpha(0.9f * f));
                }
            }
        }

        static Rgba StardustMix(Rgba a, Rgba b, float f) => new Rgba(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f, 1f);
    }
}
