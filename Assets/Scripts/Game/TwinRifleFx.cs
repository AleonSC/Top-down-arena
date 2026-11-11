using UnityEngine;

namespace Arena
{
    /// The Twin Rifle's firing feedback past the guns themselves (Docs/Mockups/twin-rifle-firing.html, the beats table), with
    /// Visual effects on. Drawing only. The guns, their pose and flash are DpsGunLooks.cs; this adds:
    ///   - a warm pool of light on the floor at each muzzle for a frame or two;
    ///   - a bright streak joining the gun to its round on the round's first frame;
    ///   - brass that flips out of the port, lands, bounces once, slides and fades after about 1.4 s, so a long burst leaves
    ///     the floor littered;
    ///   - recoil through the body: the elbows fold with each gun's kick (TwinArm) and the whole body jolts back.
    public partial class ArenaApp
    {
        const byte PBrass = 15, PStreak = 16, PPool = 17;  // particle kinds (Juice.cs ages PBrass through BrassStep, draws all three through DrawTwinPart)
        const float BrassGravity = 420f, TwinJoltPx = 1.1f, TwinJoltTime = 0.06f;
        static readonly Rgba BrassShine = new Rgba(1f, 0.95f, 0.75f), TwinPoolCol = new Rgba(1f, 0.67f, 0.31f);

        /// One Twin Rifle round seen leaving `muzzle` (now at `round`, flying along `bd`); `port` is the gun's ejection port and
        /// `outer` points out of the gun's outer side.
        void TwinShotFx(V2 muzzle, V2 round, V2 bd, V2 port, V2 outer)
        {
            AddPart(new Part { Kind = PPool, Pos = muzzle + bd * 4f, Life = 0.06f, Max = 0.06f, Size = 26f, Col = TwinPoolCol });
            V2 run = round - muzzle;
            if (run.Length > 2f)
                AddPart(new Part { Kind = PStreak, Pos = muzzle, Rot = run.Angle, A0 = run.Length, Life = 0.035f, Max = 0.035f, Size = 2.4f, Col = TwinFlashOrange });
            AddPart(new Part { Kind = PSpark, Pos = port, Vel = outer * 40f, Life = 0.035f, Max = 0.035f, Size = 1.6f, Col = Rgba.White });  // a glint at the port
            AddPart(new Part { Kind = PBrass, Pos = port, Vel = outer * Rnd(70f, 115f) + bd * Rnd(-25f, 5f), A0 = 3f, A1 = Rnd(55f, 80f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-22f, 22f), Life = 1.4f, Max = 1.4f, Size = 1.7f, Col = BrassCol });
        }

        /// A casing: A0 is its height off the floor, A1 how fast that is rising. It bounces once or twice, then slides to a stop.
        static void BrassStep(ref Part p, float dt)
        {
            p.Pos += p.Vel * dt;
            p.Rot += p.Spin * dt;
            if (p.A0 > 0f || p.A1 > 0f)
            {
                p.A1 -= BrassGravity * dt;
                p.A0 += p.A1 * dt;
                if (p.A0 <= 0f)
                {
                    p.A0 = 0f;
                    if (p.A1 < -20f)  // a bounce: up a third as fast, sideways about half
                    {
                        p.A1 = -p.A1 * 0.35f;
                        p.Vel = p.Vel * 0.55f;
                        p.Spin *= 0.5f;
                    }
                    else
                        p.A1 = 0f;
                }
            }
            else  // on the floor: sliding to a stop
            {
                float k = Mathf.Exp(-4f * dt);
                p.Vel = p.Vel * k;
                p.Spin *= k;
            }
        }

        void DrawTwinPart(Part p, float f)
        {
            switch (p.Kind)
            {
                case PBrass:
                {
                    float a = Mathf.Clamp01(p.Life / 0.35f);  // fades over its last 0.35 s
                    Circle(p.Pos + new V2(1f, 1f), 1.6f, new Rgba(0f, 0f, 0f, 0.35f * a), 6);  // its shadow on the floor
                    V2 at = p.Pos + new V2(0f, -p.A0 * 0.6f), d = V2.FromAngle(p.Rot) * p.Size;
                    Line(at - d, at + d, 1.7f, p.Col.WithAlpha(a));
                    Line(at + d * 0.3f, at + d, 1.7f, BrassShine.WithAlpha(a));
                    break;
                }
                case PStreak:
                {
                    V2 d = V2.FromAngle(p.Rot);
                    Line(p.Pos, p.Pos + d * p.A0, p.Size * f + 0.6f, p.Col.WithAlpha(0.9f * f));
                    Line(p.Pos, p.Pos + d * p.A0, 0.9f, Rgba.White.WithAlpha(f));
                    break;
                }
                // PPool is light only: the glow pass draws it
            }
        }

        /// How far the body is jolted back by the Twin Rifle right now (world px): each volley, and harder for a grenade.
        float TwinJolt(Player p)
        {
            DpsSeen s;
            if (!Juicy || !Game.IsTwinRifle(p.Weapon.Id) || !dpsSeen.TryGetValue(p.Id, out s))
                return 0f;
            float age = juiceClock - s.T, jolt = age >= 0f && age < TwinJoltTime ? TwinJoltPx * (1f - age / TwinJoltTime) : 0f;
            for (int tube = 0; tube < 2; tube++)
            {
                float l = juiceClock - s.Launch[tube];
                if (l >= 0f && l < 0.15f)
                    jolt += 2f * (1f - l / 0.15f);
            }
            return jolt;
        }

        /// How far gun `gun`'s elbow folds back (model units) with its recoil (0 left, 1 right; set by DpsGunPose).
        float TwinArmFold(int gun) => Mathf.Max(0f, (mTwinKick + (gun == 0 ? mNadeKick0 : mNadeKick1)) * 0.6f);
    }
}
