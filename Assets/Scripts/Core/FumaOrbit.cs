using System;

namespace Arena
{
    /// The Fuma Shuriken's two stances (Docs/FumaRework.md; Docs/Mockups/fuma-shuriken-rework.html, concept C).
    ///   - Throw stance (0): the oval loop as before (FireNinjaWeapon, FumaTurn in Ninja.cs). Raising the parry while one of your stars
    ///     is out catches it straight into the Orbit stance, circling from where it was.
    ///   - Orbit stance (1, the weapon-action key): the star circles you at FumaOrbitR and cuts what it passes, small enemy bullets too. Fire Lashes it out
    ///     across your aim (one way, then back), and every third press is the Cyclone, a wide lap. Raising the parry is Reversal: the
    ///     orbit flips and spins faster for a while, and everything in its ring is cut. A perfect parry steals the shot instead of
    ///     sending it back: it circles you as a small star, then flies at whoever fired it.
    ///   - The circle is worked out from a clock (FumaPose): the host replicates the clock and an anchor, and each screen runs its own
    ///     copy of the clock between snapshots (Game/FumaLook.cs), so the star moves as smoothly as the mockup on every screen.
    public partial class Game
    {
        public const float FumaOrbitR = 80f, FumaOmega = MathX.Tau / 0.75f, FumaOutTime = 0.3f, FumaStarR = 22f;  // the circle, its turn rate, how long it takes to get out to it, a star's cutting reach
        public const float FumaOrbitMult = 8f / 30f, FumaOrbitGap = 0.3f;  // a pass's damage against the weapon's own (30); the gap between passes on one enemy
        public const float FumaLashTime = 0.35f, FumaLashReach = 110f, FumaLashSwing = 0.75f, FumaLashMult = 24f / 30f, FumaLashCd = 0.4f;  // the Lash: how long, how far past the circle, how far either side of your aim, damage, the wait after
        public const float FumaCycloneTime = 0.65f, FumaCycloneReach = 80f, FumaCycloneMult = 20f / 30f, FumaCycloneCd = 0.75f;
        public const float FumaRevMult = 1f, FumaRevBoost = 1.5f, FumaRevTime = 2f, FumaRevCooldown = 1f, FumaRevIn = 25f, FumaRevOut = 50f;  // Reversal: damage, the speed-up and how long, its cooldown, the ring it cuts (inside and outside the circle)
        public const int FumaStealCap = 5;
        public const float FumaStealHold = 1.3f, FumaStealMult = 25f / 30f, FumaStealSpeed = 700f, FumaStealR = 46f;  // stolen shots: how long they circle, damage, speed back, their circle
        public const float FumaSwitchCd = 0.3f, FumaBlend = 0.15f;
        public const int FumaMaxStars = 4;
        public static readonly Rgba FumaInk = new Rgba(0.7f, 0.64f, 1f), FumaHot = new Rgba(1f, 0.84f, 0.59f);

        static float FumaEase(float k) { k = MathX.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
        static float FumaSmooth(float k) { k = MathX.Clamp01(k); return k * k * (3f - 2f * k); }

        /// The orbit's base angle at a clock time (star 0, before any move).
        public static float FumaBaseAng(Player p, float clock) => p.FumaAng0 + p.FumaSpin * (clock - p.FumaClk0);

        /// How long a move lasts (0 for circling).
        public static float FumaMoveTime(int move) => move == 3 ? FumaCycloneTime : move != 0 ? FumaLashTime : 0f;

        /// Where star i of n is, from the player's centre, at a clock time. The host cuts with it and every screen draws it.
        public static V2 FumaPose(Player p, float clock, int i, int n)
        {
            float a = FumaBaseAng(p, clock) + (n > 1 ? i * MathX.Tau / n : 0f);
            float r = p.FumaR0 + (FumaOrbitR - p.FumaR0) * FumaEase(clock / FumaOutTime);
            int move = p.FumaMove;
            float u = move != 0 ? (clock - p.FumaMoveAt) / FumaMoveTime(move) : -1f;
            if (u < 0f || u > 1f)
                return V2.FromAngle(a) * r;
            float bump = (float)Math.Sin(MathX.Pi * u);
            if (move == 3)
                return V2.FromAngle(a) * (r + FumaCycloneReach * bump);  // the Cyclone: every star swings wide
            V2 orbit = V2.FromAngle(a) * r;
            if (i != 0)
                return orbit;
            float side = move == 1 ? 1f : -1f;
            V2 lash = V2.FromAngle(p.FumaLashAim + side * FumaLashSwing * (2f * u - 1f)) * (r + FumaLashReach * bump);
            float w = FumaSmooth(u / FumaBlend) * FumaSmooth((1f - u) / FumaBlend);  // eases off the circle and back onto it
            return orbit + (lash - orbit) * w;
        }

        /// Where stolen star i circles, from the player's centre.
        public static V2 FumaMiniPose(Player p, float clock, int i) => V2.FromAngle(i * MathX.Tau / FumaStealCap + 2f * FumaOmega * clock) * FumaStealR;

        // --- Host -----------------------------------------------------------------------------------------------------

        /// The weapon-action key: Throw stance to Orbit and back.
        void HostFumaSwitch(Player p)
        {
            if (p.Weapon.Id != WeaponId.FumaShuriken || p.Downed || p.StunT > 0f || p.FumaSwitchCd > 0f)
                return;
            p.FumaSwitchCd = FumaSwitchCd - 0.05f;  // (slack for a guest's trip, which keeps the real cooldown)
            if (p.FumaStance == 0)
            {
                EnterFumaOrbit(p, 0f, p.Aim);
                BroadcastSound(Sfx.SwingLight);
                return;
            }
            ReleaseFumaMinis(p);
            p.FumaStance = 0;
            p.FumaMove = 0;
            p.FumaStep = 0;
            p.FireCd = Math.Max(p.FireCd, 0.15f);
            BroadcastSound(Sfx.SwingLight);
        }

        void EnterFumaOrbit(Player p, float r0, float ang)
        {
            p.FumaStance = 1;
            p.FumaClock = 0f;
            p.FumaClk0 = 0f;
            p.FumaAng0 = ang;
            p.FumaSpin = FumaOmega;
            p.FumaR0 = r0;
            p.FumaMove = 0;
            p.FumaStep = 0;
            p.FumaFastT = 0f;
            p.FumaCut.Clear();
            p.FireCd = Math.Max(p.FireCd, 0.2f);
        }

        /// Fire in the Orbit stance: a Lash one way, a Lash back, then the Cyclone.
        void FireFumaOrbit(Player p, float aim, float dmg)
        {
            int step = p.FumaStep;
            p.FumaStep = (byte)((step + 1) % 3);
            p.FumaMove = (byte)(step == 2 ? 3 : step + 1);
            p.FumaMoveAt = p.FumaClock;
            p.FumaLashAim = aim;
            p.FumaMoveDmg = p.Weapon.Damage * dmg * (step == 2 ? FumaCycloneMult : FumaLashMult);
            p.FumaCut.Clear();  // (a fresh move cuts what the circle just did)
            p.FireCd = (step == 2 ? FumaCycloneCd : FumaLashCd) / FireRate(p);
            p.Run.ShotsFired++;
            BroadcastSound(step == 2 ? Sfx.SwingHeavy : Sfx.SwingLight);
        }

        /// Raising the parry with the Fuma Shuriken: in the Throw stance a star in flight is caught into orbit; in the Orbit stance it's Reversal.
        void FumaParry(Player p)
        {
            if (p.FumaStance == 0)
            {
                Bullet caught = null;
                foreach (var b in Bullets.Values)
                    if (b.Owner == p.Id && b.Kind == Defs.FumaStar && (caught == null || b.Pos.DistanceSq(p.Pos) < caught.Pos.DistanceSq(p.Pos)))
                        caught = b;
                if (caught == null)
                    return;
                V2 rel = caught.Pos - p.Pos;
                var gone = new System.Collections.Generic.List<int>();
                foreach (var b in Bullets.Values)
                    if (b.Owner == p.Id && b.Kind == Defs.FumaStar)
                        gone.Add(b.Id);
                foreach (int id in gone)
                {
                    Bullets.Remove(id);
                    pendingRemoved.Add(id);
                }
                EnterFumaOrbit(p, Math.Min(rel.Length, FumaReach), rel.IsZero ? p.Aim : rel.Angle);
                BroadcastFx(FxKind.Ring, caught.Pos, FumaHot, 30f, 0.25f, Sfx.SwingLight);
                return;
            }
            p.FumaMove = 0;  // the parry cancels a Lash or Cyclone under way
            p.FumaStep = 0;
            p.FireCd = 0f;
            if (p.FumaRevCd > 0f || p.FumaClock < FumaOutTime)
                return;
            float sign = p.FumaSpin >= 0f ? -1f : 1f;
            FumaAnchor(p, sign * FumaOmega * FumaRevBoost);
            p.FumaFastT = FumaRevTime;
            p.FumaRevCd = FumaRevCooldown;
            p.FumaCut.Clear();
            float dmg = p.Weapon.Damage * p.Stats.DmgMult * FumaRevMult;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                float d = e.Pos.DistanceTo(p.Pos);
                if (d - e.Radius > FumaOrbitR + FumaRevOut || d + e.Radius < FumaOrbitR - FumaRevIn)
                    continue;
                FumaHit(p, e, dmg);
            }
            BroadcastFx(FxKind.Ring, p.Pos, FumaHot, FumaOrbitR + FumaRevOut, 0.35f, Sfx.SwingHeavy);
            BroadcastFx(FxKind.Ring, p.Pos, FumaInk, FumaOrbitR, 0.3f, Sfx.None);
        }

        /// The orbit turns at a new rate from here on, carrying on from where it is now.
        static void FumaAnchor(Player p, float spin)
        {
            p.FumaAng0 = MathX.Fmod(FumaBaseAng(p, p.FumaClock), MathX.Tau);
            p.FumaClk0 = p.FumaClock;
            p.FumaSpin = spin;
        }

        /// A perfect parry in the Orbit stance keeps the shot as a small star (instead of a needle). False: send it back as usual.
        bool FumaSteal(Player p, Bullet b)
        {
            if (p.Weapon.Id != WeaponId.FumaShuriken || p.FumaStance != 1 || p.FumaMinis >= FumaStealCap || b.Kind == Defs.StunShot || b.Kind == Defs.TaserBolt)
                return false;
            p.FumaMiniFrom.Add(b.Shooter);
            p.FumaMinis = (byte)p.FumaMiniFrom.Count;
            p.FumaMiniAt = p.FumaClock;
            return true;
        }

        /// The stolen stars fly back at whoever fired them (or the nearest enemy, or straight ahead).
        void ReleaseFumaMinis(Player p)
        {
            for (int i = 0; i < p.FumaMiniFrom.Count; i++)
            {
                V2 from = p.Pos + FumaMiniPose(p, p.FumaClock, i);
                Enemy e;
                if (!Enemies.TryGetValue(p.FumaMiniFrom[i], out e) || !Targetable(e))
                    e = NearestEnemyTo(from);
                V2 dir = e != null ? (e.Pos - from).Normalized() : V2.FromAngle(p.Aim);
                var b = SpawnBullet(Defs.StarShot, from, dir * FumaStealSpeed, 0f, 1.1f, p.Weapon.Damage * p.Stats.DmgMult * FumaStealMult, p.Id);
                b.Pierce = 0;
            }
            if (p.FumaMiniFrom.Count > 0)
                BroadcastSound(Sfx.SwingLight);
            p.FumaMiniFrom.Clear();
            p.FumaMinis = 0;
        }

        Enemy NearestEnemyTo(V2 at)
        {
            Enemy best = null;
            float bd = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                float d = e.Pos.DistanceSq(at);
                if (d < bd)
                {
                    bd = d;
                    best = e;
                }
            }
            return best;
        }

        void FumaHit(Player p, Enemy e, float dmg)
        {
            if (BlocksShot(e, p.Pos))
            {
                WearShield(e, dmg);
                return;
            }
            dmg *= NinjaHitMult(p.Id, Defs.FumaStar, e);
            p.Run.ShotsHit++;
            WeaponHurt(e, dmg, p.Id);
            V2 rel = e.Pos - p.Pos;
            OnBulletHit(new Bullet { Pos = e.Pos, Vel = rel.IsZero ? V2.FromAngle(p.Aim) : rel.Normalized(), Owner = p.Id, Dmg = dmg, Kind = Defs.FumaStar }, e);
            BroadcastFx(FxKind.Spark, e.Pos - rel.Normalized() * e.Radius, new Rgba(1f, 1f, 1f), 8f, 0.15f, Sfx.None);
        }

        /// Host, every tick: the clock, the moves ending, Reversal's speed-up wearing off, the stars cutting, stolen stars flying back.
        void TickFuma(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.FumaSwitchCd = Math.Max(0f, p.FumaSwitchCd - dt);
                p.FumaRevCd = Math.Max(0f, p.FumaRevCd - dt);
                if (p.Weapon.Id != WeaponId.FumaShuriken || p.Downed || State == MatchState.GameOver)
                {
                    if (p.FumaStance != 0 || p.FumaMinis != 0)
                    {
                        p.FumaMiniFrom.Clear();
                        p.FumaMinis = 0;
                        p.FumaStance = 0;
                        p.FumaMove = 0;
                    }
                    continue;
                }
                if (p.FumaStance != 1)
                    continue;
                p.FumaStars = (byte)Math.Min(FumaMaxStars, 1 + ExtraShots(p));
                p.FumaClock += dt;
                if (p.FumaFastT > 0f && (p.FumaFastT -= dt) <= 0f)
                    FumaAnchor(p, Math.Sign(p.FumaSpin) * FumaOmega);
                if (p.FumaMove != 0 && p.FumaClock - p.FumaMoveAt >= FumaMoveTime(p.FumaMove))
                {
                    p.FumaMove = 0;
                    p.FumaCut.Clear();
                }
                if (p.FumaMinis > 0 && p.FumaClock - p.FumaMiniAt >= FumaStealHold)
                    ReleaseFumaMinis(p);
                if (p.StunT > 0f || p.SwallowT > 0f)
                    continue;
                float orbitDmg = p.Weapon.Damage * p.Stats.DmgMult * FumaOrbitMult;
                int n = p.FumaStars;
                for (int i = 0; i < n; i++)
                {
                    V2 at = p.Pos + FumaPose(p, p.FumaClock, i, n);
                    int before = Bullets.Count;
                    CutBulletsInCircle(at, FumaStarR, false);  // a blade: it slices small enemy bullets out of the air (Knight.cs)
                    if (Bullets.Count < before)
                        BroadcastFx(FxKind.Spark, at, FumaHot, 10f, 0.15f, Sfx.None);
                    bool moving = p.FumaMove != 0 && (i == 0 || p.FumaMove == 3);
                    foreach (var e in Enemies.Values)
                    {
                        if (!Targetable(e))
                            continue;
                        float reach = e.Radius + FumaStarR;
                        if (e.Pos.DistanceSq(at) > reach * reach)
                            continue;
                        float last;
                        if (p.FumaCut.TryGetValue(e.Id, out last) && (moving || p.FumaClock - last < FumaOrbitGap))
                            continue;  // (a move cuts each enemy once)
                        p.FumaCut[e.Id] = p.FumaClock;
                        FumaHit(p, e, moving ? p.FumaMoveDmg : orbitDmg);
                    }
                }
            }
        }
    }
}
