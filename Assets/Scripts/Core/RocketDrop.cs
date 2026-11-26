using System;

namespace Arena
{
    /// Rocket Drop: the DPS Soldier's fourth ultimate (`AbilityId.RocketDrop`, key 3 of the lobby's Ultimate row), from
    /// Docs/Mockups/rocket-drop.html (look B, the drop pod, with the Thermobaric warhead).
    ///   - The call: a red ring locks on at your cursor (up to `RocketReach` away) and a steel pod streaks in. After
    ///     `RocketCallIn` s it slams down: `RocketSlamDmg` to every enemy within `RocketSlamR`, small ones shoved off.
    ///   - Its petals open over `RocketOpen` s and a heavy launcher rises on a rack. Only you can take it: step within
    ///     `RocketTakeR` of the pod. Left there `RocketWait` s, the pod folds away with it.
    ///   - Taken: for `RocketTime` s (mastered `RocketTimeMastered`) the launcher replaces your weapon (`Player.RocketT`,
    ///     replicated). Fire shoots a rocket every `RocketRate` s, ammo infinite (Defs.RocketShell, `RocketSpeed`). Your weapon's
    ///     Alt is locked; abilities and the Dodge still work; you move `RocketSlow` as fast.
    ///   - A rocket bursts on the first enemy it touches, a wall, the arena's edge, or at your cursor (`RocketBurst`): a thermobaric
    ///     blast of `RocketBlastR`, `RocketMid` at the middle falling to `RocketEdge` at the rim (times your damage), small enemies
    ///     shoved out, then a fire pool (`ZoneKind.RocketFire`, `RocketFireR`) burning `RocketFireDps` for `RocketFireTime` s.
    ///     Mastered: each blast throws `RocketBomblets` bomblets that pop `RocketBombletR` px for `RocketBombletDmg`.
    ///   - It ends when the time runs out, you go down, or the wave is over: you drop the empty tube.
    /// Look: Game/RocketDropLook.cs. Test suite `rocketdrop`; preview `ROCKETDROP=<s since the cast>`.
    public partial class Game
    {
        public const float RocketReach = 220f, RocketCallIn = 1.1f, RocketOpen = 0.4f, RocketWait = 15f, RocketFold = 0.5f;
        public const float RocketSlamR = 90f, RocketSlamDmg = 120f, RocketSlamShove = 60f;
        public const float RocketTakeR = 48f;                     // how close you step to take the launcher (to your middle)
        public const float RocketTime = 10f, RocketTimeMastered = 13f;
        public const float RocketRate = 0.6f, RocketSlow = 0.85f;
        public const float RocketSpeed = 820f, RocketRange = 900f, RocketMinRange = 120f, RocketMuzzle = 40f;
        public const float RocketBlastR = 190f, RocketMid = 150f, RocketEdge = 60f, RocketShove = 60f;
        public const float RocketFireR = 118f, RocketFireTime = 2.6f, RocketFireDps = 35f, RocketBurnTick = 0.25f;
        public const int RocketBomblets = 3;
        public const float RocketBombletR = 60f, RocketBombletDmg = 40f;
        public const float RocketBotRange = 520f;
        public static readonly Rgba RocketOrange = new Rgba(1f, 0.54f, 0.11f), RocketOlive = new Rgba(0.33f, 0.38f, 0.18f);

        /// Its entry: key 3 of the DPS Soldier's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] RocketUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.RocketDrop, Kit = Kit.Soldier, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Rocket Drop", Cooldown = 80f,
                Desc = "Call in a pod with a rocket launcher, infinite ammo, for 10 s. Each rocket's blast is huge and leaves the ground burning.",
                Capstone = "13 s, and each blast throws 3 bomblets.",
                Bot = b => b.HaveTarget && b.TargetD < RocketBotRange,
            },
        };

        /// The launcher is on your shoulder.
        public static bool RocketOn(Player p) => p.RocketT > 0f && !p.Downed;

        /// Seconds since the pod was called, from the zone's replicated timer.
        public static float RocketAge(Zone z) => z.Duration - z.T;

        /// The pod has landed.
        public static bool RocketLanded(Zone z) => RocketAge(z) >= RocketCallIn;

        /// The pod is open with the launcher still on its rack (not taken, not folding).
        public static bool RocketReady(Zone z) => RocketAge(z) >= RocketCallIn + RocketOpen && z.T > RocketFold;

        /// The burn under a rocket's blast is this far through (0-1); for the look.
        public static float RocketFireLeft(Zone z) => MathX.Clamp01(z.T / Math.Max(0.01f, z.Duration));

        /// Host: call the pod.
        void CastRocketDrop(Player p, V2 target, bool mastered)
        {
            V2 to = target - p.Pos;
            if (to.Length > RocketReach)
                to = to.Normalized() * RocketReach;
            V2 at = ClampToArena(p.Pos + to, 60f);
            var z = AddZone(ZoneKind.RocketDrop, p, at, RocketSlamR, RocketCallIn + RocketOpen + RocketWait + RocketFold);
            z.Side = mastered ? 1 : 0;  // (host: a mastered launcher)
            BroadcastFx(FxKind.Ring, at, new Rgba(1f, 0.3f, 0.25f), RocketSlamR, 0.5f, Sfx.LitTankFall);
        }

        /// Host, each tick for a Rocket Drop pod: the slam, and its owner taking the launcher.
        void TickRocketDrop(Zone z, float dt)
        {
            float age = RocketAge(z), before = age - dt;
            if (before < RocketCallIn && age >= RocketCallIn)
                RocketSlam(z);
            if (!RocketReady(z))
                return;
            Player p;
            if (!Players.TryGetValue(z.Owner, out p) || p.Downed || RocketOn(p) || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.RocketDrop
                || !p.Pos.CloserThan(z.Pos, RocketTakeR))
                return;
            TakeRocket(p, z.Side == 1);
            z.T = RocketFold;  // the empty pod folds away
        }

        void RocketSlam(Zone z)
        {
            foreach (var e in FoesInCircle(z.Pos, RocketSlamR, by: z.Owner))
            {
                HurtEnemy(e, RocketSlamDmg * z.DmgMult, z.Owner);
                if (!Heavy(e))
                    ShoveFrom(e, z.Pos, RocketSlamShove);
            }
            BroadcastFx(FxKind.Ring, z.Pos, RocketOrange, RocketSlamR, 0.45f, Sfx.LitTankLand);
            PropBlast(z.Pos, RocketSlamR, z.Owner);
        }

        /// Host: the launcher onto your shoulder.
        void TakeRocket(Player p, bool mastered)
        {
            p.RocketT = p.UltT = mastered ? RocketTimeMastered : RocketTime;
            p.RocketMastered = mastered;
            p.RocketCd = 0.3f;
            BroadcastFx(FxKind.Ring, p.Pos, Rgba.Gold, 40f, 0.4f, Sfx.PickupBig);
        }

        /// Host: you drop the empty tube (time, down, the wave over, or the ultimate swapped).
        void EndRocket(Player p)
        {
            if (p.RocketT <= 0f)
                return;
            p.RocketT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.RocketDrop)
                p.UltT = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, RocketOlive, 26f, 0.3f, Sfx.Clang);
        }

        /// Host, every tick: the launcher's time.
        void TickRocketLaunchers(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.RocketT <= 0f)
                    continue;
                if (p.RocketT <= dt || p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.RocketDrop)
                {
                    EndRocket(p);
                    continue;
                }
                p.RocketT -= dt;
                p.UltT = p.RocketT;
            }
        }

        /// Host, from TickPlayers: the launcher, in place of your weapon.
        void TickRocketGun(Player p, float dt)
        {
            p.RocketCd -= dt;
            if (!p.Firing || p.RocketCd > 0f || State == MatchState.GameOver)
                return;
            p.RocketCd = RocketRate;
            V2 dir = V2.FromAngle(p.Aim);
            V2 from = p.Pos + dir * RocketMuzzle;
            float range = Math.Max(RocketMinRange, Math.Min(RocketRange, p.AimAt.IsZero ? RocketRange : p.AimAt.DistanceTo(p.Pos)));
            float life = Math.Max(0.05f, (range - RocketMuzzle) / RocketSpeed);
            var b = SpawnBullet(Defs.RocketShell, from, dir * RocketSpeed, 0f, life, RocketMid * p.Stats.DmgMult, p.Id);
            b.Splash = RocketBlastR;
            b.Burst = 1;  // bursts where its flight ends (MoveBullets: RocketBurst)
            p.RocketShots++;
        }

        /// A rocket bursts: on an enemy, a wall, the arena's edge, or at the end of its flight. The blast falls off to the rim,
        /// shoves small enemies out, and leaves a fire pool; mastered, it throws bomblets too.
        void RocketBurst(Bullet b)
        {
            float mult = b.Dmg / RocketMid;  // (the shooter's damage when it was fired)
            foreach (var e in FoesInCircle(b.Pos, b.Splash, by: b.Owner))
            {
                float f = MathX.Clamp01(Math.Max(0f, e.Pos.DistanceTo(b.Pos) - e.Radius) / b.Splash);
                HurtEnemy(e, (RocketMid + (RocketEdge - RocketMid) * f) * mult, b.Owner);
                if (!Heavy(e))
                    ShoveFrom(e, b.Pos, RocketShove * (1f - 0.5f * f));
            }
            BroadcastFx(FxKind.RocketBlast, b.Pos, RocketOrange, b.Splash, 0.9f, Sfx.BigExplode);
            PropBlast(b.Pos, b.Splash, b.Owner);
            GateLockBlast(b.Pos, b.Splash, b.Owner);
            Player p;
            if (!Players.TryGetValue(b.Owner, out p))
                return;
            var fire = AddZone(ZoneKind.RocketFire, p, ClampToArena(b.Pos, 10f), RocketFireR, RocketFireTime);
            fire.DmgMult = mult;
            fire.TickT = 0f;  // (host: the time to the next burn tick)
            if (!p.RocketMastered)
                return;
            float turn = Rand(0f, MathX.Tau);
            for (int i = 0; i < RocketBomblets; i++)
            {
                V2 d = V2.FromAngle(turn + MathX.Tau * i / RocketBomblets);
                var nb = SpawnBullet(Defs.GrenadeShot, b.Pos + d * 8f, d * 380f, 0f, 0.32f, RocketBombletDmg * mult, b.Owner);
                nb.Splash = RocketBombletR;
                nb.Bomblet = true;
                nb.Burst = 1;
            }
        }

        /// Host, from MoveBullets: a rocket touched an enemy or a wall; it bursts there, once.
        void RocketHits(Bullet b)
        {
            if (b.Burst <= 0)
                return;
            b.Burst = 0;  // (so the end of its flight doesn't burst it again)
            string was = CreditTo(BulletSrc(b));
            RocketBurst(b);
            dmgSrc = was;
        }

        /// Host, each tick for a rocket's fire pool: burn the enemies in it.
        void TickRocketFire(Zone z, float dt)
        {
            z.TickT -= dt;
            if (z.TickT > 0f)
                return;
            z.TickT += RocketBurnTick;
            foreach (var e in FoesInCircle(z.Pos, z.Radius, by: z.Owner))
                HurtEnemy(e, RocketFireDps * RocketBurnTick * z.DmgMult, z.Owner);
        }

        /// A bot that has called the pod walks to it (BotBrain.cs): where it is, or null.
        internal Zone RocketPodFor(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.RocketDrop && z.Owner == p.Id && RocketLanded(z) && z.T > RocketFold)
                    return z;
            return null;
        }

        // --- Network: the player's Rocket Drop field (SyncPlayer) ---

        void SyncRocketDrop(NetSync s, Player p)
        {
            s.Float(ref p.RocketT);
        }
    }
}
