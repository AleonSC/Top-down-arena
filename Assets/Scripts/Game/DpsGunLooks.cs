using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Soldier DPS's three guns with Visual effects on (Docs/Mockups/soldier-dps-gun-animations.html). Drawing only:
    /// rounds, damage and timing stay in Core/Weapons.cs. With the effects off nothing here runs and the old models
    /// draw as before. The glowing shots are ShotLooks.cs's (tracers, the Railgun's slug and ion trail), unchanged. The
    /// lobby's loadout preview runs the same Juice pass, so it shows all of this too.
    ///   Twin Rifle   both guns angle in a little, kick 0.07 s, flick their charging handles back, flash at each muzzle and
    ///                drop brass out of the outer side; the barrels warm the longer you hold fire (+0.045 a volley, cooling
    ///                0.28 a second).
    ///   Railgun      three coils light one after another over the gap between shots, a ring closes on the muzzle when it is
    ///                ready; firing is a white flash, arcs between the rails, a 0.2 s kick that shoves the rails apart and
    ///                steam off the coils.
    ///   Burst SMG    three barrel tubes that turn faster as it spins up and heat from steel to white-hot (the spin ring is
    ///                WorldRenderer's), a flash that grows with the spin, brass every other round, smoke once it is hot, and
    ///                the real spray cone drawn while it is spinning (WorldRenderer.cs).
    public partial class ArenaApp
    {
        class DpsSeen { public float T = -99f, Heat, Phase; public int N; }

        const float TwinKick = 0.07f, RailKick = 0.2f, RailFlash = 0.1f, RailVent = 0.45f, RailCharge = 0.62f, SmgKick = 0.05f, TwinHeatUp = 0.045f, TwinHeatDown = 0.28f;

        static readonly Rgba TwinFlashCol = new Rgba(1f, 0.95f, 0.82f), TwinFlashOrange = new Rgba(1f, 0.62f, 0.23f), RailCyan = new Rgba(0.32f, 0.79f, 1f),
            RailFlashCol = new Rgba(0.75f, 0.95f, 1f), SteamCol = new Rgba(0.81f, 0.91f, 0.96f), CoilOff = Hx(0x2c3a46), SmgHot = Hx(0xff9a40), SmgWhite = Hx(0xfff1cc);

        readonly Dictionary<int, DpsSeen> dpsSeen = new Dictionary<int, DpsSeen>();
        static readonly DpsSeen dpsIdle = new DpsSeen();

        // the pose the model reads while it draws (set by DpsGunPose)
        bool mDpsOn;
        float mBolt, mDHeat, mCharge = 1f, mDAge = 99f, mSpinPhase, mSpinUp;

        void ClearDpsGunLooks() => dpsSeen.Clear();

        static bool IsDpsGun(WeaponId id) => id == WeaponId.TwinRifle || id == WeaponId.Railgun || id == WeaponId.BurstSmg;

        DpsSeen DpsOf(int id)
        {
            DpsSeen s;
            if (!dpsSeen.TryGetValue(id, out s))
                dpsSeen[id] = s = new DpsSeen();
            return s;
        }

        /// A Twin Rifle, Railgun or Burst SMG round has just appeared at its muzzle (Juice's new-shots pass).
        void OnDpsGunShot(Player owner, Bullet b)
        {
            var s = DpsOf(owner.Id);
            bool volley = juiceClock - s.T >= 0.04f;  // a volley's rounds appear together (both Twin barrels, an SMG's extra bullet)
            V2 dir = V2.FromAngle(owner.Aim), side = dir.Orthogonal(), bd = b.Vel.IsZero ? dir : b.Vel.Normalized(), at = b.Pos;
            switch (owner.Weapon.Id)
            {
                case WeaponId.TwinRifle:
                {
                    float sd = (b.Pos - owner.Pos).Dot(side) >= 0f ? 1f : -1f;
                    AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = bd.Angle, Life = 0.04f, Max = 0.04f, Size = 20f, Col = TwinFlashCol });
                    AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = bd.Angle, Life = 0.06f, Max = 0.06f, Size = 11f, Col = TwinFlashOrange });
                    AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.05f, Max = 0.05f, Size = 7f, Col = TwinFlashOrange });
                    if (Rnd(0f, 1f) < 0.5f)
                        AddPart(new Part { Kind = PDust, Pos = at, Vel = bd * 28f, Life = 0.4f, Max = 0.4f, Size = 2.6f, Col = SmokeCol });
                    AddPart(new Part { Kind = PCasing, Pos = owner.Disp + dir * 14f + side * (sd * 12f), Vel = side * (sd * Rnd(90f, 140f)) + dir * Rnd(-15f, 15f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-16f, 16f), Life = 0.7f, Max = 0.7f, Size = 3f, Col = BrassCol });
                    if (volley)
                    {
                        s.Heat = Mathf.Min(1f, Mathf.Max(0f, s.Heat - TwinHeatDown * (juiceClock - s.T)) + TwinHeatUp);
                        s.T = juiceClock;
                    }
                    break;
                }
                case WeaponId.Railgun:
                    s.T = juiceClock;
                    AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.1f, Max = 0.1f, Size = 34f, Col = Rgba.White });
                    AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = bd.Angle, Life = 0.09f, Max = 0.09f, Size = 46f, Col = RailFlashCol });
                    AddPart(new Part { Kind = PRing, Pos = at, Life = 0.2f, Max = 0.2f, Size = 30f, Col = RailCyan });
                    Sparks(at, RailFlashCol, 10, 360f, 0.16f, 1.6f);
                    break;
                case WeaponId.BurstSmg:
                {
                    if (!volley)
                        break;
                    s.T = juiceClock;
                    s.N++;
                    float spin = owner.SpinUp;
                    AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = bd.Angle + Rnd(-0.08f, 0.08f), Life = 0.035f, Max = 0.035f, Size = 12f + 8f * spin, Col = spin > 0.9f ? Rgba.White : TwinFlashCol });
                    AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.04f, Max = 0.04f, Size = 5.5f, Col = TwinFlashOrange });
                    if (s.N % 2 == 0)
                        AddPart(new Part { Kind = PCasing, Pos = owner.Disp + dir * 10f + side * 6f, Vel = side * Rnd(100f, 170f) + dir * Rnd(-20f, 20f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-22f, 22f), Life = 0.6f, Max = 0.6f, Size = 2.6f, Col = BrassCol });
                    if (spin > 0.5f && s.N % 3 == 0)
                        AddPart(new Part { Kind = PDust, Pos = at, Vel = bd * 25f, Life = 0.5f, Max = 0.5f, Size = 2.6f, Col = SmokeCol });
                    break;
                }
            }
        }

        /// Each frame: the SMG's barrels turn (faster as it spins up) and a fired Railgun's coils steam.
        void UpdateDpsGunLooks(float dt)
        {
            foreach (var p in game.Players.Values)
            {
                if (!IsDpsGun(p.Weapon.Id))
                    continue;
                var s = DpsOf(p.Id);
                if (p.Weapon.Id == WeaponId.BurstSmg)
                    s.Phase = MathX.Fmod(s.Phase + dt * (6f + 60f * p.SpinUp), MathX.Tau);
                else if (p.Weapon.Id == WeaponId.Railgun && juiceClock - s.T < RailVent && Rnd(0f, 1f) < dt * 50f)
                {
                    V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
                    float sg = Rnd(0f, 1f) < 0.5f ? -1f : 1f;
                    AddPart(new Part { Kind = PDust, Pos = p.Disp + dir * Rnd(16f, 38f) + side * (sg * 6f), Vel = side * (sg * Rnd(10f, 30f)) + dir * Rnd(-15f, 15f), Life = Rnd(0.35f, 0.6f), Max = 0.6f, Size = 3.2f, Col = SteamCol });
                }
            }
        }

        /// How the model's gun sits with the effects on: `kick` slides the hands (and gun) back along the aim; the rest is
        /// left in the m* fields for DrawHeld (the Twin Rifle's handle and barrel heat, the Railgun's coil charge and
        /// discharge, the SMG's spin phase and spin).
        void DpsGunPose(Player p, out float kick)
        {
            kick = 0f;
            mDpsOn = Juicy && IsDpsGun(p.Weapon.Id);
            if (!mDpsOn)
                return;
            DpsSeen s;
            if (!dpsSeen.TryGetValue(p.Id, out s))
                s = dpsIdle;
            float age = juiceClock - s.T;
            mDAge = age; mBolt = 0f; mDHeat = 0f; mCharge = 1f; mSpinUp = 0f; mSpinPhase = s.Phase;
            switch (p.Weapon.Id)
            {
                case WeaponId.TwinRifle:
                    if (age < TwinKick)
                    {
                        kick = 4f * Sq(1f - age / TwinKick);
                        mBolt = 5f * Mathf.Sin(MathX.Pi * age / TwinKick);
                    }
                    mDHeat = Mathf.Max(0f, s.Heat - TwinHeatDown * age);
                    break;
                case WeaponId.Railgun:
                    if (age < RailKick)
                        kick = 10f * Sq(1f - age / RailKick);
                    mCharge = Mathf.Clamp01((age - 0.08f) / RailCharge);
                    break;
                case WeaponId.BurstSmg:
                    mSpinUp = p.SpinUp;
                    if (age < SmgKick)
                        kick = (1.6f + 1.4f * p.SpinUp) * Sq(1f - age / SmgKick);
                    break;
            }
        }

        // --- the models (CharacterModels.cs DrawHeld hands over here when the effects are on) ---------------------------

        void DrawTwinRifleFx()
        {
            MRect(-6f, -2f, 8f, 4f, MWood); MRect(0f, -2.6f, 14f, 5.2f, MDark);
            MLine(12f, 0f, 26f, 0f, 2.4f, MSteel.Mix(new Rgba(1f, 0.55f, 0.2f), mDHeat));  // the barrel warms with sustained fire
            MRect(5f, -5f, 8f, 2.6f, Hx(0x7bd0ff));
            MRect(5f - mBolt, 2.6f, 5f, 1.4f, MSteel);                                      // the charging handle flicks back
            DrawTwinTube();
        }

        // --- the Twin Rifle's grenade tubes (Core/TwinGrenades.cs) -----------------------------------------------------

        static readonly Rgba TubeOlive = Hx(0x7c8c45), TubeEmpty = Hx(0x3f4728), TubeLit = Hx(0xd6ec8a);
        int mHeldTube;        // which tube the rifle being drawn carries: 0 left hand, 1 right (set by the model, CharacterModels.cs)
        Player mHeldOwner;

        /// A launcher tube under the barrel: olive and glowing when a grenade is in it, dark when empty, with a light at the
        /// muzzle on the tube that fires next.
        void DrawTwinTube()
        {
            var p = mHeldOwner;
            if (p == null || p.Weapon.Id != WeaponId.TwinRifle)
                return;
            bool loaded = Game.TwinTubeLoaded(p, mHeldTube), next = loaded && p.TwinNext == mHeldTube;
            MRect(11f, 2.9f, 12f, 3.4f, loaded ? TubeOlive : TubeEmpty);
            MRect(22f, 2.6f, 2.4f, 4f, Hx(0x2a301c));
            if (next)
                MCirc(24.8f, 4.6f, 1.4f, TubeLit.WithAlpha(0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f)), 8);
        }

        /// Two diamonds behind you, one a tube: olive when loaded, the next one to fire ringed, and the empty tube that loads next
        /// ringed by its progress as your hits load it.
        void DrawTwinNadePips(Player p)
        {
            if (!fxOn || p.Downed || p.TwinNades == 0 && p.TwinCharge == 0)
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            int filling = p.TwinNades == 0 ? p.TwinNext : p.TwinNades == 1 ? 1 - p.TwinNext : -1;
            for (int tube = 0; tube < Game.TwinNadeMax; tube++)
            {
                // tube 0 (left) sits to your left behind you: arena y runs down the screen, so Orthogonal is your left
                V2 back = V2.FromAngle(p.Aim + MathX.Pi), left = V2.FromAngle(p.Aim).Orthogonal();
                V2 c = p.Disp + back * 28f + left * (tube == 0 ? 11f : -11f);
                bool on = Game.TwinTubeLoaded(p, tube);
                if (on)
                    Circle(c, 8f, TubeLit.WithAlpha(0.12f + 0.1f * pulse), 12);
                Polygon(c, 5f, 4, 0f, on ? TubeOlive.Mix(TubeLit, 0.35f) : new Rgba(1f, 1f, 1f, 0.14f));
                if (tube == filling && p.TwinCharge > 0)  // a ring closing round it as your hits load it
                    Arc(c, 8.5f, 1.8f, TubeLit.WithAlpha(0.75f), 16, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(p.TwinCharge / (float)Game.TwinNadeHits));
                if (on && p.TwinNext == tube && p.TwinNades == 2)
                    Arc(c, 8.5f, 1.4f, TubeLit.WithAlpha(0.8f), 14);
            }
        }

        void DrawRailgunFx()
        {
            float flash = mDAge < RailFlash ? 1f - mDAge / RailFlash : 0f, kick = mDAge < RailKick ? 10f * Sq(1f - mDAge / RailKick) : 0f, sp = 3f + 0.12f * kick;
            MRect(-8f, -3f, 10f, 6f, MDark);
            MLine(2f, -sp, 38f, -sp, 2.2f, MSteel); MLine(2f, sp, 38f, sp, 2.2f, MSteel);  // the rails shove apart on a shot
            for (int i = 0; i < 3; i++)                                                      // three coils light up one after another
            {
                float lit = Mathf.Clamp01(mCharge * 3f - i);
                MRect(10f + i * 8f, -4.6f, 3f, 9.2f, CoilOff.Mix(RailCyan.Mix(Rgba.White, 0.4f * lit), lit));
            }
            MLine(4f, 0f, 36f, 0f, 1.5f, new Rgba(0.75f, 0.95f, 1f, 0.25f + 0.75f * Mathf.Max(flash, mCharge)));
            if (flash > 0f)                                                                  // arcs jumping between the rails
                for (int i = 0; i < 5; i++)
                {
                    float x = 6f + i * 7f, j = Mathf.Sin(mDAge * 400f + i * 7f) * 2f;
                    MLine(x, -sp, x + 2f, j, 1.4f, Rgba.White.WithAlpha(flash));
                    MLine(x + 2f, j, x + 4f, sp, 1.4f, Rgba.White.WithAlpha(flash));
                }
            if (mDAge > 0.35f)                                                               // ready: a ring closing on the muzzle
            {
                float f = Mathf.Clamp01((mDAge - 0.35f) / 0.35f);
                MArc(40f, 0f, 15f - 9f * f, 0f, MathX.Tau, 1.4f, RailCyan.WithAlpha(0.2f + 0.5f * f));
            }
        }

        void DrawBurstSmgFx()
        {
            float sp = mSpinUp, hot = Mathf.Clamp01(sp * 2f), white = Mathf.Clamp01(sp * 2f - 1f);
            var tube = MSteel.Mix(SmgHot, hot).Mix(SmgWhite, white);  // steel to white-hot as it spins up
            MRect(-6f, -2.5f, 8f, 5f, MDark); MRect(0f, -3.4f, 14f, 6.8f, Hx(0x4a5560)); MRect(4f, 2f, 5f, 8f, Hx(0x22272e));
            for (int i = 0; i < 3; i++)
            {
                float a = mSpinPhase + i * 2.094f;
                MLine(14f, Mathf.Sin(a) * 2.2f, 24f, Mathf.Sin(a) * 2.2f, 1.8f + 0.4f * Mathf.Cos(a), tube);
            }
            MRect(14f, -3.6f, 2.4f, 7.2f, MDark);
            if (sp > 0.3f)
                MCirc(25f, 0f, 1.6f + 2f * sp, SmgHot.WithAlpha(0.6f * sp), 8);
        }
    }
}
