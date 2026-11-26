using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Soldier DPS's three guns with Visual effects on (Docs/Mockups/soldier-dps-gun-animations.html). Drawing only:
    /// rounds, damage and timing stay in Core/Weapons.cs. With the effects off nothing here runs and the old models
    /// draw as before. The glowing shots are ShotLooks.cs's (tracers, the Railgun's slug and ion trail), unchanged. The
    /// lobby's loadout preview runs the same Juice pass, so it shows all of this too.
    ///   Twin Rifle   held out wide (Spread arms, Docs/Mockups/twin-rifle-firing.html): each muzzle sits where its stream starts
    ///                and turns toward the crossing; each gun kicks 0.07 s back along its own barrel and climbs outward, flicks
    ///                its charging handle, flashes at its muzzle with side-port puffs and flips brass out of its port; the barrels
    ///                heat from the tip the longer you hold fire (+0.045 a volley, cooling 0.28 a second), shimmer when hot and
    ///                smoke after a burst. Its grenade tubes' launch (Rocket assist), breech and reload are TwinNadeLook.cs.
    ///   Railgun      three coils light one after another as you hold fire to charge, a ring closes on the muzzle (gold in
    ///                the perfect window); the rails heat from steel to white-hot with the heat meter and shimmer when hot;
    ///                firing is a white flash, arcs between the rails, a kick that shoves the rails apart (all bigger with the
    ///                charge) and steam off the coils. The beam, the fire cone and the heat gauge are RailBeamLook.cs.
    ///   Burst SMG    three barrel tubes that turn faster as it spins up and heat from steel to white-hot (the spin ring is
    ///                WorldRenderer's), a flash that grows with the spin, brass every other round, smoke once it is hot, and
    ///                the real spray cone drawn while it is spinning (WorldRenderer.cs).
    public partial class ArenaApp
    {
        internal class DpsSeen
        {
            public float T = -99f, Heat, Phase; public int N;
            // the grenade tubes (TwinNadeLook.cs): when each last fired and loaded, which were loaded, where they were drawn
            public readonly float[] Launch = { -99f, -99f }, Load = { -99f, -99f };
            public readonly bool[] Snapped = { true, true };
            public int Mask = -1;
            public readonly V2[] Mouth = new V2[2], Back = new V2[2];
            // the Twin Rifle's two muzzles as last drawn (smoke after a burst), and where its streams cross as seen from its rounds
            public readonly V2[] Muzzle = new V2[2];
            public float Cross = 300f;
            // the Double Barrel (DoubleBarrelLook.cs): when each barrel last fired, and the reload time last seen
            public readonly float[] Bar = { -99f, -99f };
            public float RelT = -1f;
        }

        const float TwinKick = 0.07f, RailKick = 0.2f, RailFlash = 0.1f, RailVent = 0.45f, RailCharge = 0.62f, SmgKick = 0.05f, TwinHeatUp = 0.045f, TwinHeatDown = 0.28f;

        static readonly Rgba TwinFlashCol = new Rgba(1f, 0.95f, 0.82f), TwinFlashOrange = new Rgba(1f, 0.62f, 0.23f), RailCyan = new Rgba(0.32f, 0.79f, 1f),
            RailFlashCol = new Rgba(0.75f, 0.95f, 1f), SteamCol = new Rgba(0.81f, 0.91f, 0.96f), CoilOff = Hx(0x2c3a46), SmgHot = Hx(0xff9a40), SmgWhite = Hx(0xfff1cc);

        readonly Dictionary<int, DpsSeen> dpsSeen = new Dictionary<int, DpsSeen>();
        static readonly DpsSeen dpsIdle = new DpsSeen();

        // the pose the model reads while it draws (set by DpsGunPose)
        bool mDpsOn;
        float mTwinKick, mTwinClimb;  // the Twin Rifle's recoil, back along each barrel and turned outward (TwinSpreadPose)
        float mBolt, mDHeat, mCharge = 1f, mDAge = 99f, mSpinPhase, mSpinUp, mRailHeat, mRailShotC = 1f;
        bool mRailPerfect;

        /// A gun's shot as Juice saw it appear, for WeaponLook.DpsShot and TankShot: the aim, its left, the round's heading
        /// and where it is (the muzzle), and whether it starts a volley (DPS guns only).
        internal struct GunShot { public V2 Dir, Side, Bd, At; public bool Volley; }

        void ClearDpsGunLooks() { dpsSeen.Clear(); nadeSeen.Clear(); }

        static bool IsDpsGun(WeaponId id) => LookOf(id)?.DpsPose != null;  // (WeaponLook.DpsPose: the Twin Rifles, Railgun, Burst SMG and Double Barrel)

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
            LookOf(owner.Weapon.Id)?.DpsShot?.Invoke(this, owner, b, s, new GunShot { Dir = dir, Side = side, Bd = bd, At = at, Volley = volley });  // the weapon's own (WeaponLook.DpsShot)
        }

        /// WeaponLook.DpsShot of the Twin Rifle and its Auto: a round has just appeared at a gun's muzzle.
        void TwinRifleShot(Player owner, Bullet b, DpsSeen s, GunShot g)
        {
            V2 dir = g.Dir, side = g.Side, bd = g.Bd, at = g.At;
            bool volley = g.Volley;
            // The round starts at the gun's muzzle (Spread arms), so the flash sits on the gun: a star whose size varies a
            // little shot to shot, two puffs out of the muzzle brake's side ports, and brass flipping out of the port.
            // (side is your left: sd +1 is the left gun, tube 0). The round has already flown a little by the time it is
            // seen, so the flash goes on the gun as last drawn, and a bright streak joins the gun to the round.
            float sd = (b.Pos - owner.Pos).Dot(side) >= 0f ? 1f : -1f;
            int gun = sd > 0f ? 0 : 1;
            V2 drawn = s.Muzzle[gun];
            at = !drawn.IsZero && drawn.DistanceTo(owner.Disp) < 90f ? drawn : owner.Disp + dir * TwinOut(owner) + side * (b.Pos - owner.Pos).Dot(side);
            V2 bs = bd.Orthogonal(), port = at - bd * 17f + side * (sd * 1.5f);
            float fl = Rnd(0.75f, 1.15f);
            TwinShotFx(at, b.Pos, bd, port, side * sd);  // TwinRifleFx.cs: the light on the floor, the streak, the brass
            AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = bd.Angle, Life = 0.04f, Max = 0.04f, Size = 18f * fl, Col = TwinFlashCol });
            AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = bd.Angle, Life = 0.06f, Max = 0.06f, Size = 10f * fl, Col = TwinFlashOrange });
            AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.05f, Max = 0.05f, Size = 7f, Col = TwinFlashOrange });
            for (int k = -1; k <= 1; k += 2)
                AddPart(new Part { Kind = PDust, Pos = at - bd * 1.5f, Vel = bs * (k * Rnd(40f, 60f)), Life = 0.07f, Max = 0.07f, Size = 1.8f, Col = TwinFlashOrange.Mix(Rgba.White, 0.5f) });
            if (Rnd(0f, 1f) < 0.5f)
                AddPart(new Part { Kind = PDust, Pos = at, Vel = bd * 28f, Life = 0.4f, Max = 0.4f, Size = 2.6f, Col = SmokeCol });
            // where the streams cross, from how far this round turns in (for drawing other players' guns, TwinSpreadPose)
            float turn = Mathf.Abs(MathX.WrapPi(bd.Angle - owner.Aim)), lat = Mathf.Abs((b.Pos - owner.Pos).Dot(side));
            if (turn > 0.01f && lat > 4f && owner.Weapon.Id == WeaponId.TwinRifle)
                s.Cross += (Mathf.Clamp(TwinOut(owner) + lat / Mathf.Tan(turn), Game.TwinCrossMin, 600f) - s.Cross) * 0.3f;
            if (volley)
            {
                s.Heat = Mathf.Min(1f, Mathf.Max(0f, s.Heat - TwinHeatDown * (juiceClock - s.T)) + TwinHeatUp);
                s.T = juiceClock;
                if (owner == game.Me)  // (the Twin Rifle and its Auto)
                    TwinScreenKick(dir);  // TwinRifleFx.cs
            }
        }

        /// WeaponLook.DpsShot of the Burst SMG: a volley's flash, brass every other round and smoke once it is hot.
        void BurstSmgShot(Player owner, DpsSeen s, GunShot g)
        {
            V2 dir = g.Dir, side = g.Side, bd = g.Bd, at = g.At;
            bool volley = g.Volley;
            if (!volley)
                return;
            s.T = juiceClock;
            s.N++;
            float spin = owner.SpinUp;
            AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = bd.Angle + Rnd(-0.08f, 0.08f), Life = 0.035f, Max = 0.035f, Size = 12f + 8f * spin, Col = spin > 0.9f ? Rgba.White : TwinFlashCol });
            AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.04f, Max = 0.04f, Size = 5.5f, Col = TwinFlashOrange });
            if (s.N % 2 == 0)
                AddPart(new Part { Kind = PCasing, Pos = owner.Disp + dir * 10f + side * 6f, Vel = side * Rnd(100f, 170f) + dir * Rnd(-20f, 20f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-22f, 22f), Life = 0.6f, Max = 0.6f, Size = 2.6f, Col = BrassCol });
            if (spin > 0.5f && s.N % 3 == 0)
                AddPart(new Part { Kind = PDust, Pos = at, Vel = bd * 25f, Life = 0.5f, Max = 0.5f, Size = 2.6f, Col = SmokeCol });
        }

        /// Each frame: the SMG's barrels turn (faster as it spins up) and a fired Railgun's coils steam.
        void UpdateDpsGunLooks(float dt)
        {
            UpdateNadeTrails(dt);  // TwinNadeLook.cs: the rockets' smoke
            UpdateDoubleBarrelLooks(dt);  // DoubleBarrelLook.cs: the hulls, the snap, the wisps
            foreach (var p in game.Players.Values)
            {
                if (!IsDpsGun(p.Weapon.Id))
                    continue;
                var s = DpsOf(p.Id);
                LookOf(p.Weapon.Id).DpsTick?.Invoke(this, p, s, dt);  // the weapon's own (WeaponLook.DpsTick)
            }
        }

        /// WeaponLook.DpsTick of the Twin Rifles: a tube loading, and smoke curling out of both muzzles after a burst.
        void TwinRifleTick(Player p, DpsSeen s, float dt)
        {
            if (p.Weapon.Id == WeaponId.TwinRifle)
                UpdateTwinTubes(p, s);  // TwinNadeLook.cs: a tube loading
            float age = juiceClock - s.T, heat = Mathf.Max(0f, s.Heat - TwinHeatDown * age);
            if (age > 0.15f && age < 1.05f && heat > 0.2f && Rnd(0f, 1f) < dt * 30f * heat)  // smoke curls out of both muzzles after a burst
            {
                for (int k = 0; k < 2; k++)
                    if (!s.Muzzle[k].IsZero && s.Muzzle[k].DistanceTo(p.Disp) < 90f)
                    {
                        V2 dir = V2.FromAngle(p.Weapon.Id == WeaponId.TwinRifleAuto ? game.AutoHeading(p, k) : p.Aim);  // (each Auto gun smokes along its own barrel)
                        AddPart(new Part { Kind = PDust, Pos = s.Muzzle[k], Vel = dir * Rnd(8f, 18f) + dir.Orthogonal() * Rnd(-6f, 6f), Life = Rnd(0.6f, 1f), Max = 1f, Size = 2.4f, Col = SmokeCol });
                    }
            }
        }

        /// WeaponLook.DpsTick of the Burst SMG: its barrels turn, faster as it spins up.
        void BurstSmgTick(Player p, DpsSeen s, float dt) => s.Phase = MathX.Fmod(s.Phase + dt * (6f + 60f * p.SpinUp), MathX.Tau);

        /// WeaponLook.DpsTick of the Railgun: a fired Railgun's coils steam.
        void RailgunTick(Player p, DpsSeen s, float dt)
        {
            if (juiceClock - s.T < RailVent && Rnd(0f, 1f) < dt * 50f)
            {
                V2.Axes(p.Aim, out V2 dir, out V2 side);
                float sg = Rnd(0f, 1f) < 0.5f ? -1f : 1f;
                AddPart(new Part { Kind = PDust, Pos = p.Disp + dir * Rnd(16f, 38f) + side * (sg * 6f), Vel = side * (sg * Rnd(10f, 30f)) + dir * Rnd(-15f, 15f), Life = Rnd(0.35f, 0.6f), Max = 0.6f, Size = 3.2f, Col = SteamCol });
            }
        }

        /// How the model's gun sits with the effects on: `kick` slides the hands (and gun) back along the aim; the rest is
        /// left in the m* fields for DrawHeld (the Twin Rifle's handle and barrel heat, the Railgun's coil charge and
        /// discharge, the SMG's spin phase and spin).
        void DpsGunPose(Player p, out float kick)
        {
            kick = 0f;
            mNadeKick0 = mNadeKick1 = mNadeClimb0 = mNadeClimb1 = mTwinKick = mTwinClimb = 0f;
            mDpsOn = Juicy && IsDpsGun(p.Weapon.Id);
            if (!mDpsOn)
                return;
            DpsSeen s;
            if (!dpsSeen.TryGetValue(p.Id, out s))
                s = dpsIdle;
            float age = juiceClock - s.T;
            mDAge = age; mBolt = 0f; mTwinKick = mTwinClimb = 0f; mDHeat = 0f; mCharge = 1f; mSpinUp = 0f; mSpinPhase = s.Phase;
            LookOf(p.Weapon.Id).DpsPose(this, p, s, age, ref kick);  // the weapon's own (WeaponLook.DpsPose)
        }

        /// WeaponLook.DpsPose of the Twin Rifles (and Out of Ammo's rifle arms): each gun's recoil, handle and heat.
        void TwinRiflePose(Player p, DpsSeen s, float age, ref float kick)
        {
            if (age < TwinKick)  // each gun recoils along its own barrel and climbs outward (TwinSpreadPose), not along the aim
            {
                float f = 1f - age / TwinKick;
                mTwinKick = 6f * f * f;
                mTwinClimb = 0.12f * Mathf.Pow(f, 1.5f);
                mBolt = 5f * Mathf.Sin(MathX.Pi * age / TwinKick);
            }
            else if (age < TwinKick + 0.04f)  // a small spring past rest
            {
                float k = Mathf.Sin(MathX.Pi * (age - TwinKick) / 0.04f);
                mTwinKick = -1f * k;
                mTwinClimb = -0.02f * k;
            }
            mDHeat = Mathf.Max(0f, s.Heat - TwinHeatDown * age);
            NadeKick(juiceClock - s.Launch[0], out mNadeKick0, out mNadeClimb0);  // a grenade launch kicks its own gun (TwinNadeLook.cs)
            NadeKick(juiceClock - s.Launch[1], out mNadeKick1, out mNadeClimb1);
        }

        /// WeaponLook.DpsPose of the Railgun: the kick, the coils' charge and the rails' heat.
        void RailgunPose(Player p, DpsSeen s, float age, ref float kick)
        {
            if (age < RailKick)
                kick = (3f + 9f * s.Heat) * Sq(1f - age / RailKick);  // (s.Heat: the last shot's charge)
            mCharge = p.DrawT > 0f ? game.BowCharge(p, p.DrawT) : 0f;  // the coils light as you hold fire
            mRailPerfect = p.DrawT > 0f && game.BowPerfect(p, p.DrawT);
            mRailHeat = Mathf.Clamp01(p.RailHeat / Game.RailHeatMax);
            mRailShotC = s.Heat;
        }

        /// WeaponLook.DpsPose of the Burst SMG: its spin and kick.
        void BurstSmgPose(Player p, DpsSeen s, float age, ref float kick)
        {
            mSpinUp = p.SpinUp;
            if (age < SmgKick)
                kick = (1.6f + 1.4f * p.SpinUp) * Sq(1f - age / SmgKick);
        }

        // --- the models (CharacterModels.cs DrawHeld hands over here when the effects are on) ---------------------------

        void DrawTwinRifleFx()
        {
            MRect(-6f, -2f, 8f, 4f, MWood); MRect(0f, -2.6f, 14f, 5.2f, MDark);
            // the barrel heats from the muzzle back with sustained fire: steel, orange, then white at the tip
            float h = mDHeat;
            var mid = MSteel.Mix(TwinHotCol, Mathf.Clamp01(h * 1.3f - 0.3f));
            var tip = h > 0.75f ? TwinHotCol.Mix(TwinWhiteCol, (h - 0.75f) * 4f) : MSteel.Mix(TwinHotCol, Mathf.Clamp01(h * 1.4f));
            MLine(12f, 0f, 19f, 0f, 2.4f, MSteel.Mix(mid, 0.5f)); MLine(19f, 0f, 24f, 0f, 2.4f, mid.Mix(tip, 0.5f));
            MRect(24f, -1.9f, 2.6f, 3.8f, Hx(0x2b313a).Mix(tip, 0.6f * h));                 // the muzzle brake
            MRect(24.6f, -1.9f, 0.6f, 0.8f, Hx(0x11151b)); MRect(24.6f, 1.1f, 0.6f, 0.8f, Hx(0x11151b));  // its side ports
            MRect(5f, -5f, 8f, 2.6f, Hx(0x7bd0ff));
            MRect(5f - mBolt, 2.6f, 5f, 1.4f, MSteel);                                      // the charging handle flicks back
            if (h > 0.45f)  // hot air shimmering off the barrel
            {
                var hz = new Rgba(1f, 0.86f, 0.7f, 0.22f * (h - 0.45f) / 0.55f);
                for (int k = 0; k < 2; k++)
                    for (int x = 14; x < 27; x += 2)
                    {
                        float y0 = -3.2f - k * 1.6f + Mathf.Sin(x * 0.9f + juiceClock * 22f + k * 2f) * 0.6f, y1 = -3.2f - k * 1.6f + Mathf.Sin((x + 2) * 0.9f + juiceClock * 22f + k * 2f) * 0.6f;
                        MLine(x, y0, x + 2f, y1, 0.6f, hz);
                    }
            }
            if (mHeldOwner != null)
                DpsOf(mHeldOwner.Id).Muzzle[mHeldTube] = MW(26.6f, 0f);  // (smoke curls out of here after a burst)
            DrawTwinTube();
        }

        // --- the Twin Rifle held wide: Spread arms (Docs/Mockups/twin-rifle-firing.html) ----------------------------------

        static readonly Rgba TwinHotCol = new Rgba(1f, 0.55f, 0.2f), TwinWhiteCol = new Rgba(1f, 0.95f, 0.8f);

        /// How far from your middle the Twin Rifle's muzzles are along the aim (where its rounds start, Weapons.cs).
        static float TwinOut(Player p) => p.Def.BodyR + Game.TwinMuzzleOut;

        /// The hands for the Twin Rifle in the model's units (+x along the aim, +y to the right): each gun turned in toward the
        /// crossing so its muzzle sits where its stream starts (Game.TwinBarrelGap apart, TwinOut ahead), then recoiling back
        /// along its own barrel. `twist` is the inward turn; climbR/climbL turn each gun outward (its own recoil and a grenade).
        void TwinSpreadPose(Player p, float wk, ref float rhx, ref float rhy, ref float lhx, ref float lhy, out float twist, out float climbR, out float climbL)
        {
            float half = Game.TwinBarrelGap * 0.5f, mx = TwinOut(p);
            // where the streams cross: your own cursor (the host knows everyone's); otherwise read from their rounds
            float cross = p.Id == game.MyId || session != null && session.IsHost ? game.TwinCrossDistance(p) : DpsOf(p.Id).Cross;
            twist = Mathf.Atan2(half, Mathf.Max(20f, cross - mx));
            float len = 26f * wk, c = Mathf.Cos(twist), sn = Mathf.Sin(twist);
            float tx = mx / mU, ty = half / mU;
            rhx = tx - len * c; rhy = ty + len * sn;
            lhx = tx - len * c; lhy = -ty - len * sn;
            float kR = mTwinKick + mNadeKick1, kL = mTwinKick + mNadeKick0;
            rhx -= kR * c; rhy += kR * sn;
            lhx -= kL * c; lhy -= kL * sn;
            climbR = mTwinClimb + mNadeClimb1;
            climbL = mTwinClimb + mNadeClimb0;
        }

        /// The hands for the Twin Rifle Auto (Core/TwinAuto.cs): each gun's grip out at your side, the gun turned to its own heading
        /// (Player.TwinAimL / TwinAimR, relative to your facing, + toward your right), recoiling back along its barrel and climbing outward.
        /// headR / headL are the headings the model draws each gun at.
        void TwinAutoPose(Player p, ref float rhx, ref float rhy, ref float lhx, ref float lhy, out float headR, out float headL)
        {
            float gx = Game.AutoForward / mU, gy = Game.AutoShoulder / mU;
            headR = p.TwinAimR;
            headL = p.TwinAimL;
            float cR = Mathf.Cos(headR), sR = Mathf.Sin(headR), cL = Mathf.Cos(headL), sL = Mathf.Sin(headL), k = mTwinKick;
            rhx = gx - k * cR; rhy = gy - k * sR;
            lhx = gx - k * cL; lhy = -gy - k * sL;
            headR += mTwinClimb;  // (each gun's own recoil turns it outward)
            headL -= mTwinClimb;
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
            var s = DpsOf(p.Id);
            s.Mouth[mHeldTube] = MW(24.4f, 4.6f);  // where the launch effects go (TwinNadeLook.cs)
            s.Back[mHeldTube] = MW(11f, 4.6f);
            float open = 0f, slide = -1f;
            if (mDpsOn)
            {
                open = TubeOpen(s, mHeldTube);
                float la = juiceClock - s.Load[mHeldTube];
                if (la < NadeShellIn)
                {
                    loaded = false;  // the new shell is still sliding in
                    slide = Mathf.Clamp01((la - 0.05f) / (NadeShellIn - 0.05f));
                }
            }
            if (open > 0f)
            {
                // the breech end swings down and out about its front (+y here is the outer side; the left gun is mirrored)
                MTubeQuad(open, 11f, 2.9f, 23f, 6.3f, TubeEmpty);
                MTubeQuad(open, 11f, 2.9f, 12f, 6.3f, Hx(0x262c18));
            }
            else
                MRect(11f, 2.9f, 12f, 3.4f, loaded ? TubeOlive : TubeEmpty);
            if (slide >= 0f)
            {
                float x = 11f - 8f * (1f - slide);
                MTubeQuad(open, x, 3.3f, x + 5f, 5.9f, NadeOlive);
                MTubeQuad(open, x - 1f, 3.3f, x + 0.2f, 5.9f, BrassCol);
            }
            MRect(22f, 2.6f, 2.4f, 4f, Hx(0x2a301c));
            if (next && open <= 0f)
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
            float flash = mDAge < RailFlash ? (1f - mDAge / RailFlash) * (0.4f + 0.6f * mRailShotC) : 0f, kick = mDAge < RailKick ? (3f + 9f * mRailShotC) * Sq(1f - mDAge / RailKick) : 0f, sp = 3f + 0.12f * kick;
            var lit3 = mRailPerfect ? Rgba.Gold : RailCyan;
            var rail = MSteel.Mix(RailHot, Mathf.Clamp01(mRailHeat * 1.4f)).Mix(RailWhiteHot, Mathf.Clamp01(mRailHeat * 2.5f - 1.5f));  // the rails heat up with the meter
            MRect(-8f, -3f, 10f, 6f, MDark);
            MLine(2f, -sp, 38f, -sp, 2.2f, rail); MLine(2f, sp, 38f, sp, 2.2f, rail);  // the rails shove apart on a shot
            for (int i = 0; i < 3; i++)                                                      // three coils light up one after another as it charges
            {
                float lit = Mathf.Clamp01(mCharge * 3f - i);
                MRect(10f + i * 8f, -4.6f, 3f, 9.2f, CoilOff.Mix(lit3.Mix(Rgba.White, 0.4f * lit), lit));
            }
            MLine(4f, 0f, 36f, 0f, 1.5f, new Rgba(0.75f, 0.95f, 1f, 0.25f + 0.75f * Mathf.Max(flash, mCharge)));
            if (mRailHeat > 0.45f)  // hot air shimmering off the rails
                for (int x = 6; x < 36; x += 3)
                {
                    float y0 = -sp - 2f + Mathf.Sin(x * 0.9f + juiceClock * 22f) * 0.6f, y1 = -sp - 2f + Mathf.Sin((x + 3) * 0.9f + juiceClock * 22f) * 0.6f;
                    MLine(x, y0, x + 3f, y1, 0.8f, new Rgba(1f, 0.86f, 0.7f, 0.25f * (mRailHeat - 0.45f) / 0.55f));
                }
            if (flash > 0f)                                                                  // arcs jumping between the rails
                for (int i = 0; i < 5; i++)
                {
                    float x = 6f + i * 7f, j = Mathf.Sin(mDAge * 400f + i * 7f) * 2f;
                    MLine(x, -sp, x + 2f, j, 1.4f, Rgba.White.WithAlpha(flash));
                    MLine(x + 2f, j, x + 4f, sp, 1.4f, Rgba.White.WithAlpha(flash));
                }
            if (mCharge > 0f)                                                                // charging: a ring closing on the muzzle, gold in the perfect window
                MArc(40f, 0f, 15f - 9f * mCharge, 0f, MathX.Tau, 1.4f + mCharge, lit3.WithAlpha(0.25f + 0.65f * mCharge));
        }

        /// A Railgun beam left the muzzle (RailBeamLook.cs RailBeamFeel): a white flash, a ring and sparks, bigger with the charge (0-1).
        void OnRailgunShot(Player owner, V2 at, V2 bd, float c)
        {
            var s = DpsOf(owner.Id);
            s.T = juiceClock;
            s.Heat = c;  // (how hard it kicks)
            AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.1f, Max = 0.1f, Size = 18f + 22f * c, Col = Rgba.White });
            AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = bd.Angle, Life = 0.09f, Max = 0.09f, Size = 24f + 30f * c, Col = RailFlashCol });
            AddPart(new Part { Kind = PRing, Pos = at, Life = 0.2f, Max = 0.2f, Size = 16f + 20f * c, Col = RailCyan });
            Sparks(at, RailFlashCol, 4 + (int)(10f * c), 360f, 0.16f, 1.6f);
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
