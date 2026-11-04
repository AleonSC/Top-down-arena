using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Tank Soldier's three guns with Visual effects on (Docs/Mockups/tank-gun-animations.html). Drawing only: rounds,
    /// damage and timing stay in Core/Weapons.cs. With the effects off nothing here runs and the old models and shots draw
    /// as before. The lobby's loadout preview runs the same Juice pass, so it shows all of this too.
    ///   Scattergun      a flash cone and sparks, a 0.14 s recoil kick, then a pump: the front hand slides back and forward
    ///                   (0.14 to 0.34 s) and a red shell drops out of the side. The flechette shots are ShotLooks.cs's.
    ///   Flak Cannon     a big muzzle flash and smoke, a 0.3 s breech recoil, the orange bands glowing hot and cooling over
    ///                   0.8 s. The shell is lifted off the ground on an arc with a shadow (it flies over enemies), and its
    ///                   airburst is a flash, an orange disc, two rings, shrapnel and smoke (the glow is Juice.cs's).
    ///   Riot Repeater   a small flash, a 0.09 s bolt kick, a brass casing every round and one magazine rib lighting up; its
    ///                   rounds are glowing orbs with a white core and a tapered tracer.
    public partial class ArenaApp
    {
        class TankSeen { public float T = -99f; public bool Ejected = true; }

        const float ScatterKick = 0.14f, ScatterPump = 0.2f, ScatterEject = 0.24f, FlakKick = 0.3f, FlakHeat = 0.8f, RiotKick = 0.09f, FlakLiftMax = 34f;

        static readonly Rgba FlakHotOrange = new Rgba(1f, 0.7f, 0.36f), FlakWhiteHot = new Rgba(1f, 0.95f, 0.77f), FlakGrey = new Rgba(0.54f, 0.57f, 0.61f),
            ShellRed = new Rgba(0.78f, 0.25f, 0.18f), RiotBlue = new Rgba(0.5f, 0.75f, 1f), RiotFlash = new Rgba(0.85f, 0.95f, 1f);

        readonly Dictionary<int, TankSeen> tankSeen = new Dictionary<int, TankSeen>();

        void ClearTankGunLooks() => tankSeen.Clear();

        /// A Scattergun, Flak Cannon or Riot Repeater shot has just appeared at its muzzle (Juice's new-shots pass).
        void OnTankGunShot(Player owner, Bullet b)
        {
            TankSeen s;
            if (!tankSeen.TryGetValue(owner.Id, out s))
                tankSeen[owner.Id] = s = new TankSeen();
            if (juiceClock - s.T < 0.04f)
                return;  // one volley (a Scattergun's pellets all appear together)
            s.T = juiceClock;
            V2 dir = V2.FromAngle(owner.Aim), side = dir.Orthogonal(), at = b.Pos;
            switch (owner.Weapon.Id)
            {
                case WeaponId.Scattergun:
                {
                    s.Ejected = false;
                    for (int k = -1; k <= 1; k++)
                        AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = owner.Aim + k * 0.22f, Life = 0.07f, Max = 0.07f, Size = k == 0 ? 34f : 24f, Col = k == 0 ? new Rgba(1f, 0.92f, 0.66f) : new Rgba(1f, 0.62f, 0.23f) });
                    Sparks(at, new Rgba(1f, 0.85f, 0.55f), 10, 300f, 0.14f, 1.6f);
                    for (int i = 0; i < 6; i++)
                    {
                        float life = Rnd(0.4f, 0.7f);
                        AddPart(new Part { Kind = PDust, Pos = at + dir * Rnd(0f, 8f), Vel = dir * Rnd(40f, 140f) + side * Rnd(-45f, 45f), Life = life, Max = life, Size = Rnd(4f, 8f), Col = SmokeCol });
                    }
                    break;
                }
                case WeaponId.FlakCannon:
                    AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = owner.Aim, Life = 0.08f, Max = 0.08f, Size = 44f, Col = FlakHotOrange });
                    AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.09f, Max = 0.09f, Size = 18f, Col = FlakWhiteHot });
                    Sparks(at, FlakHotOrange, 8, 260f, 0.16f, 2f);
                    for (int i = 0; i < 8; i++)
                    {
                        float life = Rnd(0.5f, 0.9f);
                        AddPart(new Part { Kind = PDust, Pos = at, Vel = dir * Rnd(40f, 140f) + side * Rnd(-60f, 60f), Life = life, Max = life, Size = Rnd(6f, 10f), Col = SmokeCol });
                    }
                    break;
                case WeaponId.RiotRepeater:
                    AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = owner.Aim, Life = 0.04f, Max = 0.04f, Size = 16f, Col = RiotFlash });
                    AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.05f, Max = 0.05f, Size = 6f, Col = RiotBlue });
                    AddPart(new Part { Kind = PDust, Pos = at, Vel = dir * 30f, Life = 0.4f, Max = 0.4f, Size = 3f, Col = SmokeCol });
                    AddPart(new Part { Kind = PCasing, Pos = owner.Disp + dir * 8f + side * 8f, Vel = side * Rnd(90f, 140f) + dir * Rnd(-15f, 15f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-16f, 16f), Life = 0.7f, Max = 0.7f, Size = 3f, Col = BrassCol });
                    break;
            }
        }

        /// Each frame: a pumped Scattergun's shell drops out at the back of the stroke.
        void UpdateTankGunLooks()
        {
            foreach (var kv in tankSeen)
            {
                var s = kv.Value;
                Player p;
                if (s.Ejected || juiceClock - s.T < ScatterEject || !game.Players.TryGetValue(kv.Key, out p))
                    continue;
                s.Ejected = true;
                V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
                AddPart(new Part { Kind = PCasing, Pos = p.Disp + dir * 14f + side * 8f, Vel = side * Rnd(90f, 110f), Rot = p.Aim, Spin = 11f, Life = 0.9f, Max = 0.9f, Size = 4f, Col = ShellRed });
            }
        }

        /// How the model's gun and hands sit with the effects on: `kick` slides both hands (and the gun) back along the aim,
        /// `pump` slides the front hand back (Scattergun), `heat` is the Flak Cannon's glow (0..1) and `lit` the Riot
        /// Repeater's lit magazine rib (-1 for none).
        void TankGunPose(Player p, out float kick, out float pump, out float heat, out int lit)
        {
            kick = pump = heat = 0f;
            lit = -1;
            TankSeen s;
            if (!Juicy || !tankSeen.TryGetValue(p.Id, out s))
                return;
            float age = juiceClock - s.T;
            switch (p.Weapon.Id)
            {
                case WeaponId.Scattergun:
                {
                    if (age < ScatterKick)
                        kick = 6f * Sq(1f - age / ScatterKick);
                    float tp = (age - ScatterKick) / ScatterPump;
                    if (tp > 0f && tp < 1f)
                        pump = 11f * Mathf.Sin(MathX.Pi * tp);
                    break;
                }
                case WeaponId.FlakCannon:
                    if (age < FlakKick)
                        kick = 8f * Sq(1f - age / FlakKick);
                    heat = Mathf.Clamp01(1f - age / FlakHeat);
                    break;
                case WeaponId.RiotRepeater:
                    if (age < RiotKick)
                        kick = 3.4f * Sq(1f - age / RiotKick);
                    if (age < 0.13f)
                        lit = Mathf.Min(3, (int)(age / 0.03f));
                    break;
            }
        }

        static float Sq(float x) => x * x;

        // --- the shells and rounds in flight --------------------------------------------------------------------------

        /// How high a Flak shell is above the ground (0 at the muzzle and where it bursts, FlakLiftMax halfway).
        float FlakLift(Bullet b)
        {
            float flight = Mathf.Max(0.01f, b.Life0), k = 1f - MathX.Clamp01(b.Life / flight);
            return FlakLiftMax * Mathf.Sin(k * MathX.Pi);
        }

        /// The Flak shell: its shadow on the ground, and the glowing shell itself lifted along its arc, swelling as it climbs.
        void DrawFlakShell(Bullet b, Rgba c)
        {
            float lift = FlakLift(b), sc = 1f + lift / 70f;
            V2 at = b.Pos + new V2(0f, -lift);
            Circle(b.Pos + new V2(0f, 3f), b.Radius * 0.75f, new Rgba(0f, 0f, 0f, 0.35f), 12);
            Circle(at, b.Radius * sc, c, 14);
            Circle(at, b.Radius * sc * 0.5f, Rgba.White.WithAlpha(0.9f), 10);
        }

        /// A Riot Repeater round: a glowing orb with a white core and a tracer tapering away behind it.
        void DrawRiotRound(Bullet b, Rgba c)
        {
            TaperTrail(b, 8, 5f, c, 0.8f);
            Circle(b.Pos, b.Radius, c, 12);
            Circle(b.Pos, b.Radius * 0.5f, Rgba.White.WithAlpha(0.9f), 10);
        }

        // --- the Flak airburst ----------------------------------------------------------------------------------------

        /// A Flak shell's burst: the Boom Combat.cs sends in the shell's colour (nothing else uses it).
        bool IsFlakBoom(Effect fx)
        {
            if (!fxOn || fx.Kind != FxKind.Boom)
                return false;
            var c = Defs.BulletColor(Defs.FlakShell);
            return fx.Color.R == c.R && fx.Color.G == c.G && fx.Color.B == c.B;
        }

        /// The burst over the arena for its 0.25 to 0.3 s: a white flash, an orange disc, a hard ring and a softer inner one.
        void DrawFlakBlast(Effect fx, float f)
        {
            float R = fx.Radius, k = EaseOut3(Mathf.Clamp01(f / 0.7f));
            if (f < 0.33f)
                Circle(fx.Pos, R * 0.9f * EaseOut3(f / 0.33f), FlakWhiteHot.WithAlpha(0.85f * (1f - f / 0.33f)), 28);
            Circle(fx.Pos, R * 0.7f * k, FlakHotOrange.WithAlpha(0.3f * (1f - f)), 28);
            Arc(fx.Pos, R * k, 2f + 4f * (1f - f), FlakHotOrange.WithAlpha(1f - f), 40);
            Arc(fx.Pos, R * 0.6f * k, 2f, FlakWhiteHot.WithAlpha(0.8f * (1f - f)), 32);
        }

        /// What the burst leaves flying (once, from Juice's new-effects pass): shrapnel streaks and smoke.
        void FlakBurstParts(Effect fx)
        {
            float R = fx.Radius, off = Rnd(0f, MathX.Tau);
            const int n = 16;
            for (int i = 0; i < n; i++)
            {
                V2 d = V2.FromAngle(off + MathX.Tau * i / n + Rnd(-0.1f, 0.1f));
                AddPart(new Part { Kind = PFrag, Pos = fx.Pos + d * 6f, Vel = d * Rnd(R * 2.4f, R * 4f), Life = 0.28f, Max = 0.28f, Size = 14f, Col = FlakHotOrange });
            }
            for (int i = 0; i < 6; i++)
            {
                V2 d = V2.FromAngle(Rnd(0f, MathX.Tau));
                float life = Rnd(0.6f, 1f);
                AddPart(new Part { Kind = PDust, Pos = fx.Pos + d * R * 0.4f, Vel = d * Rnd(10f, 60f), Life = life, Max = life, Size = R * 0.16f, Col = FlakGrey });
            }
        }
    }
}
