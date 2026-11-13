using UnityEngine;

namespace Arena
{
    /// The Soldier DPS's Double Barrel with Visual effects on (Docs/Mockups/double-barrel.html). Drawing only: pellets, damage and the
    /// reload timing are Core/DoubleBarrel.cs. With the effects off a plain model draws and nothing here runs.
    ///   Each barrel that fires kicks the gun back, flashes at its own muzzle (a cone, sparks, smoke, embers), drops its hammer and glows
    ///   from the muzzle back, cooling over DbHeatTime. The reload (Player.DbReload) breaks the gun open: the barrels slide forward and
    ///   shorten as they tip down, the lever swings, the hulls flip out, two fresh shells slide in, and it snaps shut with a click.
    public partial class ArenaApp
    {
        const float DbKickTime = 0.17f, DbHeatTime = 2f, DbEject = 0.12f, DbLoadFrom = 0.30f, DbLoadTo = 0.52f, DbCloseFrom = 0.56f, DbCloseTo = 0.72f;

        static readonly Rgba DbWood = Hx(0x8a5a34), DbWoodD = Hx(0x6f4527), DbBrass = Hx(0xd9a441), DbShellCol = Hx(0xc93b2b), DbHull = Hx(0x8d2a20);

        // the pose the model reads while it draws (DbPose)
        float mDbK0, mDbK1, mDbH0, mDbH1, mDbOpen, mDbIns;
        bool mDbSpent0, mDbSpent1;

        /// A pellet's brightness: 1 within the full-damage range, down to a dim ember at the end of its reach (its distance flown from its age).
        static float DbPelletFade(Bullet b)
        {
            float flown = Mathf.Max(0f, b.Life0 - b.Life) * b.Vel.Length;
            return 0.35f + 0.65f * (Game.DbFalloff(flown) - Game.DbMinMult) / (1f - Game.DbMinMult);
        }

        /// Where along its reload the gun is, in seconds (negative: not reloading).
        static float DbReloadAt(Player p) => p.DbReload > 0f ? Game.DbReloadTime - p.DbReload : -1f;

        static float DbSmooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        static float DbOpenOf(float t) => t < 0f ? 0f : t < DbEject ? DbSmooth(t / DbEject) : t < DbCloseFrom ? 1f : t < DbCloseTo ? 1f - DbSmooth((t - DbCloseFrom) / (DbCloseTo - DbCloseFrom)) : 0f;

        /// The model's pose for the player being drawn (DpsGunPose); `kick` slides the gun back along the aim.
        void DbPose(Player p, DpsSeen s, out float kick)
        {
            float a0 = juiceClock - s.Bar[0], a1 = juiceClock - s.Bar[1];
            mDbK0 = Mathf.Clamp01(1f - a0 / DbKickTime); mDbK1 = Mathf.Clamp01(1f - a1 / DbKickTime);
            mDbH0 = Mathf.Max(0f, 1f - a0 / DbHeatTime); mDbH1 = Mathf.Max(0f, 1f - a1 / DbHeatTime);
            float t = DbReloadAt(p);
            mDbOpen = DbOpenOf(t);
            mDbIns = t < DbLoadFrom ? 0f : Mathf.Clamp01((t - DbLoadFrom) / (DbLoadTo - DbLoadFrom));
            mDbSpent0 = p.DbUsed >= 1; mDbSpent1 = p.DbUsed >= 2;
            kick = 5f * Mathf.Max(mDbK0, mDbK1) * Mathf.Max(mDbK0, mDbK1);
        }

        /// A pellet of a Double Barrel volley has appeared (Juice's new-shots pass): the flash is on the barrel it came out of.
        void OnDoubleBarrelShot(Player owner, Bullet b)
        {
            var s = DpsOf(owner.Id);
            V2 dir = V2.FromAngle(owner.Aim), side = dir.Orthogonal();
            int bar = (b.Pos - owner.Pos).Dot(side) < 0f ? 0 : 1;  // (side is your right: the left barrel is 0)
            if (juiceClock - s.Bar[bar] < 0.05f)
                return;  // a barrel's pellets all appear together
            s.Bar[bar] = juiceClock;
            V2 drawn = s.Muzzle[bar];
            V2 at = !drawn.IsZero && drawn.DistanceTo(owner.Disp) < 110f ? drawn : owner.Disp + dir * 40f + side * ((bar == 0 ? -1f : 1f) * 5f);
            float fl = Rnd(0.85f, 1.15f);
            AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = dir.Angle, Life = 0.06f, Max = 0.06f, Size = 34f * fl, Col = TwinFlashCol });
            AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = dir.Angle, Life = 0.09f, Max = 0.09f, Size = 20f * fl, Col = TwinFlashOrange });
            AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.07f, Max = 0.07f, Size = 11f, Col = TwinFlashOrange });
            Sparks(at, TwinFlashOrange.Mix(Rgba.White, 0.4f), 8, 380f, 0.2f, 1.4f);
            for (int k = 0; k < 4; k++)
                AddPart(new Part { Kind = PDust, Pos = at, Vel = (dir + side * Rnd(-0.5f, 0.5f)) * Rnd(30f, 100f), Life = Rnd(0.5f, 0.9f), Max = 0.9f, Size = Rnd(3f, 5f), Col = SmokeCol });
            Shake(0.1f);
        }

        /// Each frame: the hulls flipping out when the gun breaks open, a snap when it shuts, wisps from the open chambers and the hot barrels.
        void UpdateDoubleBarrelLooks(float dt)
        {
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon.Id != WeaponId.DoubleBarrel)
                    continue;
                var s = DpsOf(p.Id);
                float t = DbReloadAt(p), prev = s.RelT;
                s.RelT = t;
                V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
                if (t >= DbEject && prev < DbEject)  // the hulls flip out of the chambers, back and to each side
                    for (int i = 0; i < 2; i++)
                    {
                        V2 at = !s.Back[i].IsZero && s.Back[i].DistanceTo(p.Disp) < 90f ? s.Back[i] : p.Disp + dir * 14f + side * ((i == 0 ? -1f : 1f) * 4f);
                        float sd = i == 0 ? -1f : 1f;
                        AddPart(new Part { Kind = PCasing, Pos = at, Vel = side * (sd * Rnd(90f, 150f)) - dir * Rnd(20f, 70f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-16f, 16f), Life = 0.8f, Max = 0.8f, Size = 3.6f, Col = DbShellCol });
                        for (int k = 0; k < 3; k++)
                            AddPart(new Part { Kind = PDust, Pos = at, Vel = new V2(Rnd(-12f, 12f), Rnd(-30f, -8f)), Life = Rnd(0.5f, 0.8f), Max = 0.8f, Size = Rnd(2.5f, 4f), Col = SmokeCol });
                    }
                if (t >= DbCloseTo && prev < DbCloseTo && prev >= 0f)  // it snaps shut
                {
                    V2 hinge = !s.Back[0].IsZero ? (s.Back[0] + s.Back[1]) * 0.5f : p.Disp + dir * 16f;
                    AddPart(new Part { Kind = PRing, Pos = hinge, Life = 0.2f, Max = 0.2f, Size = 18f, Col = TwinFlashCol });
                    Sparks(hinge, TwinFlashCol, 4, 160f, 0.14f, 1.2f);
                    Shake(0.04f);
                }
                float hot = Mathf.Max(Mathf.Max(0f, 1f - (juiceClock - s.Bar[0]) / DbHeatTime), Mathf.Max(0f, 1f - (juiceClock - s.Bar[1]) / DbHeatTime));
                if (hot > 0.55f && Rnd(0f, 1f) < dt * 14f * hot)  // a wisp curls up off a barrel that just fired
                {
                    int bar = juiceClock - s.Bar[0] < juiceClock - s.Bar[1] ? 0 : 1;
                    if (!s.Muzzle[bar].IsZero && s.Muzzle[bar].DistanceTo(p.Disp) < 110f)
                        AddPart(new Part { Kind = PDust, Pos = s.Muzzle[bar], Vel = dir * Rnd(6f, 16f) + side * Rnd(-5f, 5f), Life = Rnd(0.7f, 1.1f), Max = 1.1f, Size = 2.6f, Col = SmokeCol });
                }
            }
        }

        // --- the model (CharacterModels.cs DrawHeld) -------------------------------------------------------------------
        // Drawn in the mockup's gun units (stock to muzzle about 67), scaled DbU into the model's frame, the rear hand at the grip.

        const float DbU = 0.62f, DbGrip = 9f;
        static float Dx(float g) => (g - DbGrip) * DbU;
        static float Dy(float g) => g * DbU;
        void DbRect(float gx, float gy, float w, float h, Rgba c) => MRect(Dx(gx), Dy(gy), w * DbU, h * DbU, c);
        void DbLine(float x0, float y0, float x1, float y1, float w, Rgba c) => MLine(Dx(x0), Dy(y0), Dx(x1), Dy(y1), w * DbU, c);
        void DbShell(float gx, float gy, bool fired) { DbRect(gx - 3.4f, gy - 1.9f, 6.8f, 3.8f, fired ? DbHull : DbShellCol); DbRect(gx - 3.4f, gy - 1.9f, 2.8f, 3.8f, DbBrass); }

        void DrawDoubleBarrelFx()
        {
            float o = mDbOpen, sx = 1f - 0.38f * o, bx = 24f + 7f * o;
            // stock and butt plate
            MPoly(DbWood, Dx(-6f), Dy(-3f), Dx(15f), Dy(-4.6f), Dx(15f), Dy(4.6f), Dx(-6f), Dy(3f));
            DbLine(-5f, -1.5f, 14f, -2f, 0.5f, DbWoodD); DbLine(-5f, 1.5f, 14f, 2f, 0.5f, DbWoodD);
            DbRect(-7f, -3.3f, 1.8f, 6.6f, Hx(0x262c33));
            // the open breech and its chambers (the barrels have slid forward)
            if (o > 0.06f)
            {
                DbRect(23.4f, -6.4f, bx - 22.4f, 12.8f, Hx(0x0a0d11));
                for (int i = 0; i < 2; i++)
                {
                    float cy = i == 0 ? -3.5f : 3.5f;
                    MCirc(Dx(25.4f), Dy(cy), 2.5f * DbU, Hx(0x050709), 10);
                    if (mDbIns > 0f)
                    {
                        float e = DbSmooth(mDbIns);
                        DbShell(25.4f + (1f - e) * 8f, cy + (i == 0 ? -1f : 1f) * (1f - e) * 9f, false);
                    }
                }
            }
            // the receiver, hammers, lever and hinge pin
            DbRect(12f, -6.2f, 13f, 12.4f, Hx(0x39434d));
            DbRect(13.5f, -5.4f, 6f, 2f, DbBrass); DbRect(13.5f, 3.4f, 6f, 2f, DbBrass);
            for (int i = 0; i < 2; i++)
            {
                bool spent = i == 0 ? mDbSpent0 : mDbSpent1;
                DbRect(spent ? 15.6f : 13.4f, (i == 0 ? -3.4f : 3.4f) - 1.3f, 2.8f, 2.6f, Hx(0x14181d));
            }
            float la = o * 0.6f, lc = Mathf.Cos(la), ls = Mathf.Sin(la);
            DbLine(22f - 8f * lc, 8f * ls * 0f - 8f * ls, 22f, 0f, 1.8f, DbBrass);  // the lever swings about the hinge
            MCirc(Dx(24f), Dy(0f), 2.1f * DbU, DbBrass, 10);
            // fore-end and barrels, one group that shortens as it tips open
            DbRect(bx + 3f * sx, -7f, 15f * sx, 14f, DbWoodD);
            DbRect(bx + 3.6f * sx, -6.4f, 13.8f * sx, 2.2f, DbWood); DbRect(bx + 3.6f * sx, 4.2f, 13.8f * sx, 2.2f, DbWood);
            for (int i = 0; i < 2; i++)
            {
                float yc = i == 0 ? -3.5f : 3.5f, h = i == 0 ? mDbH0 : mDbH1;
                DbRect(bx, yc - 2.6f, 38f * sx, 5.2f, Hx(0x5e6a76));
                DbRect(bx, yc - 2.6f, 38f * sx, 1.4f, Hx(0x98a5b1));
                DbRect(bx, yc + 1.4f, 38f * sx, 1.2f, Hx(0x2f3740));
                if (h > 0.02f)  // glowing from the muzzle back
                {
                    DbRect(bx + 24f * sx, yc - 2.6f, 14f * sx, 5.2f, Hx(0xff7a2a, 0.55f * h));
                    DbRect(bx + 32f * sx, yc - 2.6f, 6f * sx, 5.2f, Hx(0xffd9a0, 0.5f * h));
                }
                DbRect(bx + 36f * sx, yc - 2.9f, 2.8f * sx, 5.8f, Hx(0x1c2228));
            }
            DbRect(bx, -0.8f, 38.6f * sx, 1.6f, Hx(0xb5c0ca));              // the ventilated rib
            DbRect(bx + 17.4f * sx, -7f, 1.4f, 14f, DbBrass); DbRect(bx - 1f, -6.4f, 2.4f, 12.8f, DbBrass);
            MCirc(Dx(bx + 35.8f * sx), Dy(0f), 1.1f * DbU, DbBrass, 8);       // the bead
            if (mHeldOwner != null)
            {
                var s = DpsOf(mHeldOwner.Id);
                s.Muzzle[0] = MW(Dx(bx + 38.6f * sx), Dy(-3.5f)); s.Muzzle[1] = MW(Dx(bx + 38.6f * sx), Dy(3.5f));  // (the flashes go here)
                s.Back[0] = MW(Dx(25.4f), Dy(-3.5f)); s.Back[1] = MW(Dx(25.4f), Dy(3.5f));                        // (the hulls come out of here)
            }
        }

        /// The model with the effects off: a plain side-by-side.
        void DrawDoubleBarrelPlain()
        {
            DbRect(-6f, -3f, 20f, 6f, DbWood); DbRect(12f, -6f, 13f, 12f, Hx(0x39434d));
            DbRect(24f, -7f, 15f, 14f, DbWoodD);
            DbRect(24f, -6.1f, 38f, 5.2f, Hx(0x5e6a76)); DbRect(24f, 0.9f, 38f, 5.2f, Hx(0x5e6a76)); DbRect(24f, -0.8f, 38f, 1.6f, Hx(0xb5c0ca));
        }
    }
}
