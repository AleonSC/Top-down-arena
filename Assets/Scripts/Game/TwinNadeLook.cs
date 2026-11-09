using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Twin Rifle's grenade tubes with Visual effects on: Rocket assist (Docs/Mockups/twin-rifle-grenade-launch.html).
    /// Drawing only; the grenades themselves are Core/TwinGrenades.cs and nothing here changes their path, timing or damage.
    ///   Launch   a hot backblast cone and a jet of smoke out of the back of the tube, a dull olive-white flash and a ring of
    ///            smoke at its mouth; the firing gun alone kicks back and climbs outward (DpsGunPose), the breech drops open
    ///            and a fat 40 mm casing tumbles out behind you, then it snaps shut.
    ///   Flight   the grenade flies nose first with a flickering flame off its tail and leaves a puffy smoke trail; it is drawn
    ///            leaving the drawn tube and eases onto its real line over its first 0.12 s.
    ///   Reload   when your hits load a tube (a TwinNades rise), its breech drops open, a fresh shell slides in, and it snaps
    ///            shut with a spark and a green ring.
    /// The launch is seen as the new grenade shot (Juice's new-shots pass), so other players' launches animate too.
    public partial class ArenaApp
    {
        class NadeSeen { public V2 Off; public float T, Puff; }

        const float NadeKickTime = 0.2f, NadeSettle = 0.12f, NadeLead = 0.12f, NadeShellIn = 0.17f, NadeSnap = 0.2f;
        static readonly Rgba NadeFlame = new Rgba(1f, 0.63f, 0.27f), NadeFlameCore = new Rgba(1f, 0.94f, 0.78f), NadeFlashCol = new Rgba(0.92f, 1f, 0.76f);

        readonly Dictionary<int, NadeSeen> nadeSeen = new Dictionary<int, NadeSeen>();
        readonly List<int> nadeGone = new List<int>();

        // the pose the model reads: each gun's extra kick (back along the aim) and climb (turned outward), set by DpsGunPose
        float mNadeKick0, mNadeKick1, mNadeClimb0, mNadeClimb1;

        /// A gun's kick `age` seconds after its tube fired: 6 back and 0.17 rad outward, easing out over 0.2 s, then a small
        /// spring past rest.
        static void NadeKick(float age, out float kick, out float climb)
        {
            kick = climb = 0f;
            if (age < 0f || age >= NadeKickTime + NadeSettle)
                return;
            if (age < NadeKickTime)
            {
                float f = 1f - age / NadeKickTime;
                kick = 6f * f * f;
                climb = 0.17f * Mathf.Pow(f, 1.6f);
            }
            else
            {
                float k = Mathf.Sin(MathX.Pi * (age - NadeKickTime) / NadeSettle);
                kick = -1.2f * k;
                climb = -0.03f * k;
            }
        }

        /// How far the tube's breech is open (radians): after a launch (0.08 to 0.32 s) and while a shell goes in (0 to 0.2 s).
        float TubeOpen(DpsSeen s, int tube)
        {
            const float Open = 0.4f;
            float l = juiceClock - s.Launch[tube], r = juiceClock - s.Load[tube];
            if (l >= 0f && l < 0.32f)
                return l < 0.08f ? 0f : l < 0.14f ? Open * (l - 0.08f) / 0.06f : l < 0.28f ? Open : Open * (1f - (l - 0.28f) / 0.04f);
            if (r >= 0f && r < NadeSnap)
                return r < 0.06f ? Open * r / 0.06f : r < 0.18f ? Open : Open * (1f - (r - 0.18f) / 0.02f);
            return 0f;
        }

        /// A rectangle (x0,y0)-(x1,y1) of the gun's frame turned `open` about the tube's front (22, 4.6), its back end swinging out.
        void MTubeQuad(float open, float x0, float y0, float x1, float y1, Rgba c)
        {
            float cs = Mathf.Cos(-open), sn = Mathf.Sin(-open);
            float ax = x0 - 22f, bx = x1 - 22f, ay = y0 - 4.6f, by = y1 - 4.6f;
            MPoly(c, 22f + ax * cs - ay * sn, 4.6f + ax * sn + ay * cs, 22f + bx * cs - ay * sn, 4.6f + bx * sn + ay * cs,
                22f + bx * cs - by * sn, 4.6f + bx * sn + by * cs, 22f + ax * cs - by * sn, 4.6f + ax * sn + by * cs);
        }

        /// A grenade has just left a Twin Rifle's tube (Juice's new-shots pass).
        void OnTwinNadeShot(Player owner, Bullet b)
        {
            var s = DpsOf(owner.Id);
            V2 dir = V2.FromAngle(owner.Aim), left = dir.Orthogonal();
            int tube = (b.Pos - owner.Pos).Dot(left) >= 0f ? 0 : 1;  // TwinTubeMuzzle: tube 0 sits to your left
            s.Launch[tube] = juiceClock;
            V2 mouth = s.Mouth[tube], back = s.Back[tube];
            if (mouth.IsZero || mouth.DistanceTo(owner.Disp) > 80f)  // never drawn (off screen): use the real start
            {
                mouth = b.Pos;
                back = b.Pos - dir * 12f;
            }
            V2 gd = (mouth - back).IsZero ? dir : (mouth - back).Normalized(), outer = tube == 0 ? left : left * -1f;
            float ga = gd.Angle;
            // at the mouth: a dull olive-white flash and a ring of smoke rolling out
            AddPart(new Part { Kind = PMuzzle, Pos = mouth, Rot = ga, Life = 0.06f, Max = 0.06f, Size = 12f, Col = NadeFlashCol });
            AddPart(new Part { Kind = PRing, Pos = mouth + gd * 3f, Life = 0.4f, Max = 0.4f, Size = 15f, Col = SmokeCol });
            for (int i = 0; i < 4; i++)
                AddPart(new Part { Kind = PDust, Pos = mouth, Vel = gd * Rnd(15f, 45f) + gd.Orthogonal() * Rnd(-25f, 25f), Life = Rnd(0.5f, 0.85f), Max = 0.85f, Size = 3.2f, Col = SmokeCol });
            // out of the back: the backblast
            AddPart(new Part { Kind = PMuzzle, Pos = back, Rot = ga + MathX.Pi, Life = 0.08f, Max = 0.08f, Size = 20f, Col = NadeFlame });
            AddPart(new Part { Kind = PFlash, Pos = back, Life = 0.08f, Max = 0.08f, Size = 9f, Col = NadeFlame });
            for (int i = 0; i < 8; i++)
                AddPart(new Part { Kind = PDust, Pos = back, Vel = gd * -Rnd(60f, 160f) + gd.Orthogonal() * Rnd(-30f, 30f), Life = Rnd(0.4f, 0.8f), Max = 0.8f, Size = Rnd(3f, 4.5f), Col = SmokeCol });
            // the spent shell tumbles out behind, to the outer side
            AddPart(new Part { Kind = PCasing, Pos = back, Vel = gd * -Rnd(40f, 70f) + outer * Rnd(30f, 55f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-12f, 12f), Life = 1.6f, Max = 1.6f, Size = 3.4f, Col = BrassCol });
            // the grenade is drawn from the drawn tube and eases onto its real line
            NadeSeen n;
            if (!nadeSeen.TryGetValue(b.Id, out n))
                nadeSeen[b.Id] = n = new NadeSeen();
            n.Off = mouth - b.Pos;
            n.T = juiceClock;
            n.Puff = 0f;
        }

        /// Where a grenade is drawn: its real position plus what is left of its lead-in from the tube.
        V2 NadeDrawPos(Bullet b)
        {
            NadeSeen n;
            if (!nadeSeen.TryGetValue(b.Id, out n))
                return b.Pos;
            float k = Mathf.Clamp01((juiceClock - n.T) / NadeLead), e = 1f - k;
            return b.Pos + n.Off * (e * e);
        }

        /// Each frame: a smoke trail behind each rocket; forget the ones that burst.
        void UpdateNadeTrails(float dt)
        {
            nadeGone.Clear();
            foreach (var kv in nadeSeen)
            {
                Bullet b;
                if (!game.Bullets.TryGetValue(kv.Key, out b) || b.Kind != Defs.GrenadeShot)
                {
                    nadeGone.Add(kv.Key);
                    continue;
                }
                var n = kv.Value;
                n.Puff -= dt;
                if (n.Puff <= 0f)
                {
                    n.Puff = 0.018f;
                    AddPart(new Part { Kind = PDust, Pos = NadeDrawPos(b), Vel = new V2(Rnd(-8f, 8f), Rnd(-8f, 8f)), Life = 0.6f, Max = 0.6f, Size = Rnd(2.6f, 3.6f), Col = SmokeCol });
                }
            }
            foreach (int id in nadeGone)
                nadeSeen.Remove(id);
        }

        /// A Twin Rifle each frame: a tube that has just loaded drops its breech, takes a shell and snaps shut.
        void UpdateTwinTubes(Player p, DpsSeen s)
        {
            int mask = (Game.TwinTubeLoaded(p, 0) ? 1 : 0) | (Game.TwinTubeLoaded(p, 1) ? 2 : 0);
            if (s.Mask >= 0)
                for (int tube = 0; tube < 2; tube++)
                    if ((mask & ~s.Mask & (1 << tube)) != 0)
                    {
                        s.Load[tube] = juiceClock;
                        s.Snapped[tube] = false;
                    }
            s.Mask = mask;
            for (int tube = 0; tube < 2; tube++)
                if (!s.Snapped[tube] && juiceClock - s.Load[tube] >= NadeSnap)
                {
                    s.Snapped[tube] = true;
                    V2 at = s.Mouth[tube].IsZero ? p.Disp : s.Mouth[tube];
                    AddPart(new Part { Kind = PRing, Pos = at, Life = 0.35f, Max = 0.35f, Size = 15f, Col = TubeLit });
                    Sparks(at, NadeFlameCore, 4, 120f, 0.08f, 1.2f);
                }
        }

        /// The grenade in flight, nose first, with its flame (SoldierLooks.cs DrawGrenadeFlight draws the landing ring first).
        void DrawRocketNade(Bullet b, float t)
        {
            V2 at = NadeDrawPos(b), d = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), sd = d.Orthogonal();
            float r = b.Radius, fl = 7f + 5f * Mathf.Abs(Mathf.Sin(t * 47f + b.Id));
            V2 tail = at - d * (r * 0.8f);
            SetColor(NadeFlame.WithAlpha(0.85f));
            Tri(tail + sd * 2.6f, tail - sd * 2.6f, tail - d * fl);
            SetColor(NadeFlameCore);
            Tri(tail + sd * 1.2f, tail - sd * 1.2f, tail - d * (fl * 0.5f));
            SetColor(NadeOlive);
            Quad(at + d * r + sd * (r * 0.55f), at + d * r - sd * (r * 0.55f), at - d * r - sd * (r * 0.55f), at - d * r + sd * (r * 0.55f));
            Circle(at + d * r, r * 0.55f, NadeOlive, 10);
            Line(at + sd * (r * 0.55f), at - sd * (r * 0.55f), 1.6f, new Rgba(0.17f, 0.2f, 0.12f));
            if (MathX.Fmod(t, 0.15f) < 0.075f)
                Circle(at + d * r, 2.2f, new Rgba(1f, 0.3f, 0.2f), 8);
        }
    }
}
