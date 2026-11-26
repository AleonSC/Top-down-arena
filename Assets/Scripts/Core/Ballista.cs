using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Ballista: the DPS Knight's third ultimate (key 2 of the lobby's Ultimate row; the Drawbridge stays the default), from the
    /// mockup (claude.ai artifact "Knight Ballista Ultimate"). A `ZoneKind.Ballista` zone:
    ///   - The Knight calls it down BaAhead px in front of them along the aim (or where it fits). Its shadow grows for BaFall s,
    ///     then it lands: BaSlam to every enemy within BaSlamR px (times the cast's power).
    ///   - It stands for BaLife s. Any player (the Knight or a teammate) within BaReach px presses Interact (F) to mount it
    ///     (`UseBallista`, `Msg.BallistaMount`); one rider at a time, Interact again to get off. The rider is seated (`Seated`:
    ///     rooted on the seat behind the bow, no dodging, no moving abilities, the weapon away) and takes BaCut less damage: that
    ///     share wears the ballista's BaHp health instead (`BallistaSoak`).
    ///   - The bow turns onto the rider's aim at BaTurn rad/s. Holding Fire looses a giant bolt (`Defs.BallistaBolt`) every
    ///     BaReload s: BaDamage times the rider's damage, through every enemy, BaRange px; each enemy it passes is knocked
    ///     BaKnock px along it (heavies half, bosses not at all) and ordinary enemy shots in its path are knocked out of the air.
    ///   - It holds BaBolts bolts (BaBoltsMastered mastered). It falls apart BaBreakT s after its last bolt, when its time or
    ///     health runs out; it folds away quietly when the fight ends.
    /// Mastered: BaBoltsMastered bolts, and each bolt pins what it hits (BaPin slower for BaPinTime s; not bosses). The rare card
    /// Windlass (Stats.BaExtraBolts, BaReloadMult) adds 2 bolts and reloads 25% faster a stack.
    /// Replicated in the zone: Pos, Aim (the bow's heading), Radius (the mount reach; 0 = breaking), T/Duration, GuardFrac (its
    /// health), Rider (0 = none), BoltsLeft and GunCd (seconds to the next bolt). The look (Game/BallistaLook.cs) draws the drop,
    /// the frame, the bow, the bolts, the pips, the prompt and the wreck from those alone.
    public partial class Game
    {
        public const float BaCooldown = 75f, BaAhead = 70f, BaFall = 0.6f, BaSlam = 80f, BaSlamR = 90f, BaLife = 25f, BaBreakT = 0.6f;
        public const float BaReach = 80f, BaSeatBack = 46f, BaTurn = 3.84f, BaCut = 0.3f, BaHp = 600f;
        public const float BaReload = 0.9f, BaDamage = 260f, BaRange = 1600f, BaSpeed = 1500f, BaKnock = 90f, BaHeavyKnock = 0.5f, BaShotBand = 40f;
        public const float BaPin = 0.4f, BaPinTime = 3f, BaWindlassReload = 0.25f, BaMuzzle = 64f;
        public const int BaBolts = 8, BaBoltsMastered = 10, BaWindlassBolts = 2;
        public static readonly Rgba BaGold = new Rgba(1f, 0.8f, 0.35f), BaWood = new Rgba(0.54f, 0.35f, 0.2f);

        /// Ballista's entry: key 2 of the DPS Knight's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] BallistaUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.Ballista, Kit = Kit.Knight, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Ballista", Cooldown = BaCooldown,
                Desc = "A giant ballista drops in front of you. Anyone can mount it (Interact) and fire 8 giant bolts that go through everything.",
                Capstone = "10 bolts, and each one pins what it hits.",
                Bot = b => b.HaveTarget && b.TargetD < 800f,
            },
        };

        /// Seconds since the ballista was called.
        public static float BaAge(Zone z) => z.Duration - z.T;

        /// It has landed and stands (not falling, not breaking).
        public static bool BaStanding(Zone z) => z.Kind == ZoneKind.Ballista && z.Radius > 0f && BaAge(z) >= BaFall && z.T > BaBreakT;

        /// Where its rider sits: behind the bow, on the far side from where it points.
        public static V2 BaSeat(Zone z) => z.Pos - V2.FromAngle(z.Aim) * BaSeatBack;

        /// The ballista this player rides, or null.
        public Zone BallistaOf(Player p)
        {
            if (p == null)
                return null;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Ballista && z.Rider == p.Id && z.Radius > 0f)
                    return z;
            return null;
        }

        /// Riding a ballista (Seated covers it).
        public bool OnBallista(Player p) => p != null && !p.Downed && BallistaOf(p) != null;

        /// The free, standing ballista nearest this spot within reach (or the one you ride), or null: what Interact uses.
        public Zone BallistaNear(V2 at, int rider = 0)
        {
            Zone best = null;
            float bestD = float.MaxValue;
            foreach (var z in Zones.Values)
            {
                if (!BaStanding(z) || z.Rider != 0 && z.Rider != rider)
                    continue;
                float d = at.DistanceTo(z.Pos);
                if (d <= BaReach && d < bestD)
                {
                    best = z;
                    bestD = d;
                }
            }
            return best;
        }

        void CastBallista(Player p, V2 target, bool cap)
        {
            V2 dir = target - p.Pos;
            dir = dir.Length < 1f ? V2.FromAngle(p.Aim) : dir.Normalized();
            V2 at = ClampToArena(p.Pos + dir * BaAhead, 50f);
            var z = AddZone(ZoneKind.Ballista, p, at, BaReach, BaFall + BaLife + BaBreakT);
            z.DmgMult = castPower;  // (the rider's own damage multiplies each bolt as it leaves)
            z.Aim = (float)Math.Atan2(dir.Y, dir.X);
            z.Cap = cap;
            z.BoltsLeft = (cap ? BaBoltsMastered : BaBolts) + BaWindlassBolts * p.Stats.BaWindlass;
            z.BaReloadT = BaReload / (1f + BaWindlassReload * p.Stats.BaWindlass);
            z.SoakMax = z.BlockHp = BaHp;
            z.GuardFrac = 1f;
            z.Rider = 0;
            if (p.IsBot)
                z.Rider = p.Id;  // a bot climbs straight on (its aim and trigger drive the bow)
            Sounds.Add(Sfx.SwingHeavy);
        }

        /// Host, every tick, for each ballista (TickZones): the landing, the rider, the turn, the bolts and the end.
        void TickBallista(Zone z, float dt)
        {
            if (!Battle)
            {
                z.Quiet = true;  // the fight ended: it folds away
                z.Rider = 0;
                z.T = 0f;
                return;
            }
            float age = BaAge(z);
            if (age < BaFall)
                return;
            if (!z.Landed)
            {
                z.Landed = true;
                foreach (var e in FoesInCircle(z.Pos, BaSlamR, by: z.Owner))
                {
                    HurtEnemy(e, BaSlam * z.DmgMult, z.Owner);
                    if (!e.Def.IsBoss)
                        KnockEnemy(e, (e.Pos - z.Pos).Normalized() * 50f);
                    castHits++;
                }
                BroadcastFx(FxKind.Ring, z.Pos, BaGold, BaSlamR * 1.4f, 0.45f, Sfx.BossSlam);
            }
            if (z.Radius <= 0f)
                return;  // breaking up
            Player r = null;
            if (z.Rider != 0 && (!Players.TryGetValue(z.Rider, out r) || r.Downed))
            {
                z.Rider = 0;
                r = null;
            }
            if (r != null)
            {
                z.Aim += Math.Max(-dt * BaTurn, Math.Min(dt * BaTurn, MathX.WrapPi(r.Aim - z.Aim)));
                r.Pos = BaSeat(z);
            }
            z.GunCd = Math.Max(0f, z.GunCd - dt);
            if (r != null && r.Firing && !r.ParryActive && z.GunCd <= 0f && z.BoltsLeft > 0)
                LooseBolt(z, r);
            if (z.BoltsLeft <= 0 && z.GunCd <= z.BaReloadT - 0.35f || z.T <= BaBreakT)
                BreakBallista(z);  // out of bolts (a beat after the last), or out of time
        }

        /// Host: a giant bolt leaves the bow.
        void LooseBolt(Zone z, Player r)
        {
            V2 d = V2.FromAngle(z.Aim);
            var b = SpawnBullet(Defs.BallistaBolt, z.Pos + d * BaMuzzle, d * BaSpeed, 0f, BaRange / BaSpeed, BaDamage * z.DmgMult * r.Stats.DmgMult, r.Id);
            b.Pierce = 999;  // through everything
            b.Gusted = new HashSet<int>();  // (the enemies it has knocked, once each: TickBallistaBolts)
            b.Pins = z.Cap;
            z.BoltsLeft--;
            z.GunCd = z.BaReloadT;
            BallistaBoltsFired++;
            r.Run.AbilitiesUsed++;
        }

        /// Host: it falls apart (the rider is let off where they sit). Its last BaBreakT s are the wreck's look.
        void BreakBallista(Zone z)
        {
            z.Rider = 0;
            z.Radius = 0f;
            z.T = Math.Min(z.T, BaBreakT);
        }

        /// Host, from Damage: BaCut of a hit meant for the rider wears the ballista instead. At 0 health it breaks.
        float BallistaSoak(Player p, float amount)
        {
            if (amount <= 0f)
                return amount;
            var z = BallistaOf(p);
            if (z == null)
                return amount;
            float take = Math.Min(z.BlockHp, amount * BaCut);
            z.BlockHp -= take;
            z.GuardFrac = z.SoakMax > 0f ? Math.Max(0f, z.BlockHp / z.SoakMax) : 0f;
            if (z.BlockHp <= 0f)
                BreakBallista(z);
            return amount - take;
        }

        /// Host, every tick: each bolt knocks the enemies it passes along its path (once each, pinning them when mastered) and
        /// knocks ordinary enemy shots in its path out of the air.
        void TickBallistaBolts(float dt)
        {
            baShots.Clear();
            foreach (var b in Bullets.Values)
            {
                if (b.Kind != Defs.BallistaBolt || b.Gusted == null)
                    continue;
                V2 fwd = b.Vel.Normalized(), from = b.Pos - b.Vel * dt;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || b.Gusted.Contains(e.Id) || e.Pos.DistanceToSegment(from, b.Pos) > b.Radius + e.Radius)
                        continue;
                    b.Gusted.Add(e.Id);
                    if (e.Def.IsBoss)
                        continue;
                    KnockEnemy(e, fwd * BaKnock * (e.Def.Heavy ? BaHeavyKnock : 1f));
                    if (b.Pins)
                        Slow(e, BaPinTime, 1f - BaPin);
                }
                foreach (var s in Bullets.Values)
                    if (Defs.IsEnemyShot(s.Kind) && !Defs.Unblockable(s.Kind) && s.Pos.DistanceToSegment(from, b.Pos) <= BaShotBand)
                        baShots.Add(s.Id);
            }
            foreach (int id in baShots)
                KillBullet(id);
        }

        readonly List<int> baShots = new List<int>();

        /// Bolts loosed so far (tests).
        public int BallistaBoltsFired;

        /// This game's player presses Interact by a ballista: on it if it's free, off it if they ride it. The host decides.
        public void UseBallista()
        {
            var me = Me;
            if (me == null || me.Downed)
                return;
            if (IsHost)
            {
                HostUseBallista(MyId);
                return;
            }
            session.SendToHost(Proto.End(Proto.Begin(Msg.BallistaMount)));
        }

        public void OnBallistaMount(int peer, BinaryReader r) => HostUseBallista(peer);

        void HostUseBallista(int id)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p) || p.Downed)
                return;
            var ridden = BallistaOf(p);
            if (ridden != null)
            {
                ridden.Rider = 0;  // off: you stand where the seat was
                Sounds.Add(Sfx.Clang);
                return;
            }
            if (Seated(p) || TankOn(p) || ArmOn(p) || Riding(p))
                return;  // already in a seat, a shell or a Trojan Horse
            var z = BallistaNear(p.Pos);
            if (z == null)
                return;
            z.Rider = p.Id;
            Sounds.Add(Sfx.Clang);
            DoLog(p.Name + " mounts the ballista");
        }
    }
}
