using System;

namespace Arena
{
    /// The DPS Hemomancer's Needle Swarm (WeaponId.NeedleSwarm, the 1st weapon; replaced Blood Bolts; Docs/NeedleSwarm.md, mockup
    /// Docs/Mockups/needle-swarm.html). A thick ring of blood needles circles you all the time (Player.NsCount, replicated); every
    /// screen flies the needles themselves from that count and the tendrils (Game/NeedleSwarmLook.cs).
    ///   The swarm: full and free when the weapon comes out. A missing needle is siphoned out of you at NsRate a second for NsCost of
    ///     your max health each (PayHealth), never while you're under NsFloor. An enemy shot coming in through the ring stops on a
    ///     needle and breaks it (NsBlocks, in Combat.cs ShieldsStop; red aimed shots go through). Going down loses the swarm.
    ///   Fire, held: a tendril grows (NsGrow px/s) from the ring to the enemy nearest your cursor within NsReach. Let go before it
    ///     arrives and it pulls back. Once it arrives it wraps the enemy in a spinning ring (Player.NsBound needles, up to NsWrapMax) for
    ///     good: each wrapped needle cuts for the weapon's Damage a second, slows it NsSlow, and every NsBleedEvery adds a bleed stack
    ///     (the stack that fills its bleed bursts it, NeedleBleed). It lets go when the enemy dies or gets NsLetGo x the reach away.
    ///     Holding on, once it has wrapped, the next enemy nearest the cursor gets the next tendril: NsTendrils at once.
    ///   Alt (Msg.WeaponDash, WeaponAlt.Needles): NsBurst needles leave the ring as a fan of needles at your aim (Defs.BloodShot), NsBurstCd.
    ///   Extra projectiles: +NsPerExtra needles in the swarm, +NsWrapPerExtra wrapped round each enemy, cutting NsCutPerExtra harder, +4 in the burst.
    public partial class Game
    {
        public const int NsBase = 160, NsPerExtra = 20, NsTendrils = 3, NsWrap = 30, NsWrapPerExtra = 5, NsBurst = 24;
        public const float NsCutPerExtra = 0.1f;  // each extra projectile: wrapped needles cut 10% harder
        public const float NsCost = 0.0014f, NsRate = 32f, NsFloor = 0.15f;      // health per needle (of max), needles a second, no siphon below
        public const float NsInner = 22f, NsOuter = 72f, NsEdge = 46f;          // the ring's band round you; where tendrils leave it
        public const float NsReach = 260f, NsLetGo = 1.5f, NsGrow = 140f, NsRetract = 3f, NsWrapRate = 26f, NsShare = 0.45f;
        public const float NsSlow = 0.5f, NsTick = 0.2f, NsBleedEvery = 0.5f, NsBleedHit = 9f, NsBlockFull = 0.35f;
        public const float NsBurstDmg = 14f, NsBurstSpeed = 900f, NsBurstLife = 0.45f, NsBurstFan = 0.02f, NsBurstCd = 2.5f;
        public static readonly Rgba NsBlood = new Rgba(0.76f, 0.08f, 0.18f);

        /// How many needles a full swarm holds.
        public int NsMax(Player p) => NsBase + NsPerExtra * ExtraShots(p);

        /// How far tendril k has to grow to reach its enemy: from the ring's edge to the enemy's middle.
        public static float NsSpan(V2 from, V2 enemy) => Math.Max(1f, from.DistanceTo(enemy) - NsEdge);

        /// How many needles tendril k may wrap round its enemy with this many tendrils out.
        public static int NsWrapCap(int count, int tendrils, int wrap = NsWrap) => Math.Min(wrap, (int)(count * NsShare / Math.Max(1, tendrils)));

        /// How many needles one tendril wraps round its enemy at most: NsWrap, +NsWrapPerExtra per extra projectile.
        public int NsWrapMax(Player p) => NsWrap + NsWrapPerExtra * ExtraShots(p);

        /// Host, every tick the Needle Swarm is out and you're up: the siphon, the tendrils, the wrapped enemies.
        void TickNeedleSwarm(Player p, float dt)
        {
            int max = NsMax(p);
            if (!p.NsReady)
            {
                p.NsReady = true;
                p.NsCount = max;  // a full swarm when the weapon comes out
            }
            NsSiphon(p, max, dt);
            p.NsBurstCd = Math.Max(0f, p.NsBurstCd - dt);
            bool firing = p.Firing && !p.ParryActive && p.StunT <= 0f && State != MatchState.GameOver;
            int out_ = 0;
            bool growing = false;
            for (int k = 0; k < NsTendrils; k++)
                if (p.NsTen[k] != 0)
                {
                    out_++;
                    growing |= p.NsBound[k] == 0;
                }
            if (firing && out_ < NsTendrils && !growing)
                NsReachOut(p);  // one tendril grows at a time: the next starts once this one has wrapped its enemy
            for (int k = 0; k < NsTendrils; k++)
                if (p.NsTen[k] != 0)
                    NsStepTendril(p, k, firing, dt);
        }

        void NsSiphon(Player p, int max, float dt)
        {
            if (p.NsCount >= max || p.Hp < p.MaxHp * NsFloor)
                p.NsAcc = 0f;
            else
            {
                p.NsAcc += NsRate * dt;
                while (p.NsAcc >= 1f && p.NsCount < max)
                {
                    p.NsAcc -= 1f;
                    p.NsPaid += PayHealth(p, p.MaxHp * NsCost, p.Weapon.Name);
                    p.NsCount++;
                }
            }
            if ((p.NsPaidT += dt) >= 0.6f)
            {
                if (p.NsPaid >= 0.5f)  // what the siphon cost lately, shown as a little spurt (FxKind.Bleed)
                    BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), new Rgba(0.85f, 0.12f, 0.2f), p.NsPaid, 0.55f, Sfx.None);
                p.NsPaid = p.NsPaidT = 0f;
            }
        }

        /// Fire held: a tendril starts for the enemy nearest the cursor, within reach, that no tendril holds yet.
        void NsReachOut(Player p)
        {
            Enemy best = null;
            float bd = float.MaxValue;
            var snap = SnapFoes(p.Id);
            foreach (var e in snap)
            {
                if (!Targetable(e) || !e.Pos.WithinReach(p.Pos, NsReach + e.Radius) || NsHolds(p, e.Id))
                    continue;
                float d = e.Pos.DistanceSq(p.AimAt);
                if (d < bd)
                {
                    bd = d;
                    best = e;
                }
            }
            Free(snap);
            if (best == null)
                return;
            for (int k = 0; k < NsTendrils; k++)
                if (p.NsTen[k] == 0)
                {
                    p.NsTen[k] = best.Id;
                    p.NsLen[k] = 0f;
                    p.NsBound[k] = 0;
                    p.NsWrapAcc[k] = p.NsHurtT[k] = p.NsBleedT[k] = 0f;
                    return;
                }
        }

        bool NsHolds(Player p, int enemyId)
        {
            for (int k = 0; k < NsTendrils; k++)
                if (p.NsTen[k] == enemyId)
                    return true;
            return false;
        }

        void NsStepTendril(Player p, int k, bool firing, float dt)
        {
            Enemy e;
            if (!Enemies.TryGetValue(p.NsTen[k], out e) || !Targetable(e) || !e.Pos.WithinReach(p.Pos, NsReach * NsLetGo + e.Radius))
            {
                NsLetGoOf(p, k);
                return;
            }
            float span = NsSpan(p.Pos, e.Pos);
            if (p.NsBound[k] == 0 && p.NsLen[k] < span)
            {
                if (!firing)
                {
                    if ((p.NsLen[k] -= NsGrow * NsRetract * dt) <= 0f)
                        NsLetGoOf(p, k);  // let go of Fire before it got there: it pulls back in
                    return;
                }
                p.NsLen[k] = Math.Min(span, p.NsLen[k] + NsGrow * dt);
                if (p.NsLen[k] < span)
                    return;
            }
            p.NsLen[k] = span;  // there: it follows the enemy now
            int tendrils = 0;
            for (int j = 0; j < NsTendrils; j++)
                if (p.NsTen[j] != 0)
                    tendrils++;
            int cap = NsWrapCap(p.NsCount, tendrils, NsWrapMax(p));
            p.NsWrapAcc[k] += NsWrapRate * dt;
            while (p.NsWrapAcc[k] >= 1f && p.NsBound[k] < cap)
            {
                p.NsWrapAcc[k] -= 1f;
                p.NsBound[k]++;
            }
            p.NsWrapAcc[k] = Math.Min(p.NsWrapAcc[k], 1f);
            if (p.NsBound[k] > cap)
                p.NsBound[k] = cap;
            if (p.NsBound[k] <= 0)
                return;
            Slow(e, NsTick + 0.05f, NsSlow);
            if ((p.NsHurtT[k] += dt) >= NsTick)
            {
                p.NsHurtT[k] -= NsTick;
                WeaponHurt(e, p.NsBound[k] * p.Weapon.Damage * NsTick * p.Stats.DmgMult * (1f + NsCutPerExtra * ExtraShots(p)), p.Id);
                p.Run.ShotsHit++;
            }
            if ((p.NsBleedT[k] += dt) >= NsBleedEvery && Targetable(e))
            {
                p.NsBleedT[k] -= NsBleedEvery;
                NeedleBleed(p, e, NsBleedHit * p.Stats.DmgMult);  // Hemomancer.cs
            }
        }

        void NsLetGoOf(Player p, int k)
        {
            p.NsTen[k] = 0;
            p.NsLen[k] = 0f;
            p.NsBound[k] = 0;
        }

        /// Combat.cs ShieldsStop: does an enemy shot coming in through p's ring stop on a needle? A thin swarm lets some through.
        bool NsBlocks(Player p, Bullet b)
        {
            if (p.Weapon.Id != WeaponId.NeedleSwarm || p.NsCount <= 0 || !Enters(b, p.Pos, NsOuter))
                return false;
            float thick = p.NsCount / (NsMax(p) * NsBlockFull);
            return thick >= 1f || sideRng.NextDouble() < thick;
        }

        /// The shot broke a needle where it struck.
        void NsBlockHit(Player p, Bullet b)
        {
            p.NsCount--;
            BroadcastFx(FxKind.NeedleSwarm, b.Pos, new V2(p.Id, 0f), Defs.BulletColor(b.Kind), b.Radius, 0.35f, Sfx.None);
        }

        /// Alt: NsBurst needles leave the ring as a fan at your aim.
        void HostNeedleBurst(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.NeedleSwarm || p.Downed || p.StunT > 0f || p.NsBurstCd > 0f || State == MatchState.GameOver)
                return;
            int n = Math.Min(p.NsCount, NsBurst + 4 * ExtraShots(p));
            if (n <= 0)
                return;
            V2 to = aimAt - p.Pos;
            float aim = to.Length > 4f ? to.Angle : p.Aim;
            p.NsCount -= n;
            p.NsBurstCd = NsBurstCd;
            float dmg = NsBurstDmg * p.Stats.DmgMult;
            for (int i = 0; i < n; i++)
            {
                float a = Fanned(aim, i, n, NsBurstFan), out_ = NsInner + (NsOuter - NsInner) * ((i * 7) % n) / n;
                SpawnBullet(Defs.BloodShot, p.Pos + V2.FromAngle(a) * out_, V2.FromAngle(a) * NsBurstSpeed * (0.9f + 0.2f * ((i * 5) % n) / n), 0f, NsBurstLife, dmg, p.Id);
            }
            p.Run.ShotsFired += n;
            BroadcastFx(FxKind.NeedleSwarm, p.Pos + V2.FromAngle(aim) * NsEdge, new V2(p.Id, 1f), NsBlood, n, 0.3f, Sfx.Throw);
        }

        /// Is there anything of a swarm to clear (ClearNeedleSwarm)?
        static bool NsAny(Player p) => p.NsCount != 0 || p.NsReady && p.Weapon.Id != WeaponId.NeedleSwarm || p.NsTen[0] != 0 || p.NsTen[1] != 0 || p.NsTen[2] != 0;

        /// Off when you go down or put it away: the swarm falls apart.
        void ClearNeedleSwarm(Player p)
        {
            p.NsCount = 0;
            p.NsAcc = p.NsPaid = p.NsPaidT = 0f;
            for (int k = 0; k < NsTendrils; k++)
                NsLetGoOf(p, k);
            if (p.Weapon.Id != WeaponId.NeedleSwarm)
                p.NsReady = false;  // (taken out again later, it comes back full)
        }

        /// Your own game: Alt was pressed with the Needle Swarm (the host checks the rest).
        bool LocalNeedleBurst(Player me, bool pressed, V2 aimAt)
        {
            if (me.Weapon.Id != WeaponId.NeedleSwarm || !pressed)
                return false;
            if (me.NsBurstCd <= 0f && me.NsCount > 0)
            {
                if (IsHost)
                    HostWeaponDash(MyId, WeaponAlt.Needles, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(WeaponAlt.Needles);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        // --- Network: the player's Needle Swarm fields (SyncPlayer) ---

        void SyncNeedleSwarm(NetSync s, Player p)
        {
            s.Short(ref p.NsCount);  // the Needle Swarm
            s.Float(ref p.NsBurstCd);
            for (int k = 0; k < Game.NsTendrils; k++)
            {
                s.Int(ref p.NsTen[k]);
                s.Float(ref p.NsLen[k]);
                s.Byte(ref p.NsBound[k]);
            }
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): a fan of needles at a pack ahead, while the swarm is thick enough to spare them
        static bool NeedleSwarmBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            return !late && e != null && v.ShootD < 320f && me.NsBurstCd <= 0f && me.NsCount > g.NsMax(me) * 0.6f && g.Tap(g.EnemiesNear(e.Pos, 120f) >= 2 || e.Def.IsBoss, 1f);
        }
    }
}
