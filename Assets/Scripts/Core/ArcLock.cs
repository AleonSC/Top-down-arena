using System;
using System.Collections.Generic;

namespace Arena
{
    /// Arc Lightning, the lock-on chain (mockup: the "Arc Lightning Lock-On" artifact, Static Brand look; Game/ArcLockLook.cs draws it).
    ///   Hold fire: a wide cone (ArcConeHalf either side of your aim, ArcConeReach long) is scanned, and every ArcTagGap the enemy in
    ///     it nearest your cursor is tagged (Player.ArcTags, replicated; FxKind.ArcTag), up to ArcMaxTags. A tag sticks when the enemy
    ///     walks out of the cone; it's only lost when the enemy dies or you go down, switch weapon or the draw is cancelled.
    ///   Let go (FireVolley, KitWeapons.cs): lightning chains from your staff through every tag in the order they were tagged, one hop
    ///     every ArcHop (Player.ArcHops, landed by TickArcLock; FxKind.ArcLink). Each tag hit takes (Damage + ArcPerTag x tags), and a
    ///     ArcStun stun (not bosses). Nothing tagged: a fizzle (an ArcLink with no target).
    ///   Alt, Forked Volley (Msg.WeaponDash, WeaponAlt.Fork): three quick forked bolts curve into the three enemies nearest your cursor
    ///     (within ForkPick of it and ForkRange of you), ForkGap apart, ForkDamage each; ForkCd cooldown (FxKind.ArcFork).
    ///   Upgrades: fire rate and Static Field tag faster; extra projectiles add ArcGrow damage; Barrage adds ArcFanTags tags a stack;
    ///     Homing Rounds widen the cone; Velocity and Piercing Rounds stretch it.
    public partial class Game
    {
        public const float ArcConeHalf = 0.873f, ArcConeMaxHalf = 1.2f, ArcConeReach = 360f, ArcTagEvery = 0.14f, ArcHop = 0.06f, ArcPerTag = 4f;
        public const float ArcCancel = 0.25f;  // tags left with no draw this long are dropped (ArcStun and ArcGrow: KitWeapons.cs)
        public const int ArcMaxTags = 10, ArcFanTags = 2, ArcTagCap = 16;
        public const float ForkCd = 1.4f, ForkGap = 0.07f, ForkDamage = 14f, ForkPick = 200f, ForkRange = 560f, ForkBend = 55f;
        public const int ForkBolts = 3;

        /// A chain or fork hit still to land (host only).
        public struct ArcHit
        {
            public int Enemy;    // 0: no target (a fizzle or a fork at empty ground)
            public float T;      // until it lands
            public float Dmg;
            public bool Fork;
            public int Index;    // the hop's place in the chain, or the fork bolt's
            public float Bend;   // a fork bolt's curve (px sideways at its middle, signed)
            public V2 To;        // where a bolt with no target strikes
        }

        /// The cone's half angle and reach for this player.
        public static float ArcHalf(Player p) => Math.Min(ArcConeMaxHalf, ArcConeHalf * AreaMult(p));
        public static float ArcReach(Player p) => ArcConeReach * p.Stats.ShotSpeedMult * PierceReach(p);
        public static int ArcTagMax(Player p) => Math.Min(ArcTagCap, ArcMaxTags + ArcFanTags * p.Stats.Fan);

        /// Seconds between tags while you hold (fire rate and Static Field make it shorter).
        public float ArcTagGap(Player p) => ArcTagEvery / FireRate(p) * p.Stats.ArcChargeMult;

        /// Where the lightning leaves your staff.
        public static V2 ArcStaff(Player p, float aim) => p.Pos + V2.FromAngle(aim) * (p.Def.BodyR + 12f);

        /// Inside the cone (scanned from your centre along `aim`).
        public static bool InArcCone(Player p, float aim, V2 at, float r)
            => MathX.InCone(p.Pos, aim, ArcHalf(p), ArcReach(p), at, r, inner: r + p.Def.BodyR, padBody: true);

        /// Host, every tick for every player: tags while the draw is held, drops them when it's cancelled, lands the chain's and the
        /// fork's hits, counts the fork's cooldown down.
        void TickArcLock(Player p, float dt)
        {
            p.ForkCdT -= dt;
            if (p.ArcHits.Count > 0)
                LandArcHits(p, dt);
            if (p.ArcTags.Count == 0 && (p.Weapon.Id != WeaponId.ArcLightning || p.DrawT <= 0f))
            {
                p.ArcTagT = 0f;
                return;
            }
            if (p.Weapon.Id != WeaponId.ArcLightning || p.Downed || p.ParryActive || p.StunShots > 0 || State == MatchState.GameOver)
            {
                p.ArcTags.Clear();
                return;
            }
            for (int i = p.ArcTags.Count - 1; i >= 0; i--)  // the dead come off
            {
                Enemy t;
                if (!TryFoe(p.ArcTags[i], out t) || !Targetable(t))
                    p.ArcTags.RemoveAt(i);
            }
            if (p.DrawT <= 0f)
            {
                if (p.BowRelease <= 0f && (p.ArcIdleT += dt) > ArcCancel)
                    p.ArcTags.Clear();  // a draw that ended without a shot (the shooter's game cancelled it)
                return;
            }
            p.ArcIdleT = 0f;
            if ((p.ArcTagT -= dt) > 0f || p.ArcTags.Count >= ArcTagMax(p))
                return;
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (!Targetable(e) || p.ArcTags.Contains(e.Id) || !InArcCone(p, p.Aim, e.Pos, e.Radius))
                    continue;
                float d = e.Pos.DistanceTo(p.AimAt);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            if (best == null)
                return;  // (the next one in is tagged at once)
            p.ArcTags.Add(best.Id);
            p.ArcTagT = ArcTagGap(p);
            BroadcastFx(FxKind.ArcTag, best.Pos, new V2(p.Id, best.Id), Defs.BulletColor(Defs.BoltShot), p.ArcTags.Count - 1, 0.35f, Sfx.None);
        }

        /// Host, on letting go (FireVolley): the chain through every tag, in tag order. `mult` scales the damage (extra projectiles,
        /// Homing's cost, your damage).
        void ArcChain(Player p, float aim, float mult)
        {
            V2 tip = ArcStaff(p, aim);
            p.ArcFrom = tip;
            p.Run.ShotsFired++;
            GateLockStrike(q => InArcCone(p, aim, q, LockW / 2f));  // a Warden gate's lock in the cone takes a hit (Warden.cs)
            int n = p.ArcTags.Count;
            if (n == 0)
            {
                BroadcastFx(FxKind.ArcLink, tip, tip, Defs.BulletColor(Defs.BoltShot), -1f, 0.4f, Sfx.None);  // a fizzle
                return;
            }
            float dmg = (p.Weapon.Damage + ArcPerTag * n) * mult;
            for (int i = 0; i < n; i++)
                p.ArcHits.Add(new ArcHit { Enemy = p.ArcTags[i], T = i * ArcHop, Dmg = dmg, Index = i });
            p.ArcTags.Clear();
            p.Run.ShotsHit++;
        }

        /// Host: the chain's and the fork's hits that are due.
        void LandArcHits(Player p, float dt)
        {
            var bolt = Defs.BulletColor(Defs.BoltShot);
            bool was = firingWeapon;
            firingWeapon = true;  // (credited to the weapon)
            for (int i = 0; i < p.ArcHits.Count; i++)
            {
                var h = p.ArcHits[i];
                if ((h.T -= dt) > 0f)
                {
                    p.ArcHits[i] = h;
                    continue;
                }
                p.ArcHits.RemoveAt(i--);
                Enemy e;
                bool alive = TryFoe(h.Enemy, out e) && Targetable(e);
                V2 from = h.Fork ? ArcStaff(p, (h.To - p.Pos).Length > 1f ? (h.To - p.Pos).Angle : p.Aim) : p.ArcFrom;
                if (!alive)
                {
                    if (h.Fork)
                        BroadcastFx(FxKind.ArcFork, from, h.To, bolt, h.Bend, 0.45f, Sfx.None);  // a bolt into empty ground
                    continue;  // (a dead link: the chain jumps on from where it was)
                }
                if (h.Fork)
                    BroadcastFx(FxKind.ArcFork, from, e.Pos, bolt, h.Bend, 0.45f, Sfx.None);
                else
                {
                    BroadcastFx(FxKind.ArcLink, from, e.Pos, bolt, h.Index, 0.75f, Sfx.None);
                    p.ArcFrom = e.Pos;
                }
                if (BlocksShot(e, from))
                {
                    WearShield(e, h.Dmg);
                    continue;
                }
                HurtEnemy(e, h.Dmg, p.Id);
                ModHit(e, p.Id, h.Dmg, Defs.BoltShot, (e.Pos - from).Normalized());
                WetArcs(e.Pos, e, h.Dmg, p.Id);  // Tsunami.cs
                if (!h.Fork && !e.Def.IsBoss)
                    Stun(e, ArcStun + p.Stats.ArcStunBonus);  // held, but not iced: the brand's crackle and the daze stars (Daze.cs; Static Field: longer)
            }
            firingWeapon = was;
        }

        /// Host: Forked Volley. Three bolts, ForkGap apart, at the three enemies nearest your cursor (the nearest again when there are
        /// fewer), bending out to alternate sides; none there and they strike the ground round your cursor.
        void HostForkVolley(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.ArcLightning || p.ForkCdT > 0f || p.StunT > 0f || State == MatchState.GameOver)
                return;
            p.ForkCdT = ForkCd;
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            V2 side = V2.FromAngle(aim).Orthogonal();
            forkPick.Clear();
            foreach (var e in AllFoes(by: p.Id))
                if (Targetable(e) && e.Pos.DistanceTo(aimAt) <= ForkPick + e.Radius && e.Pos.DistanceTo(p.Pos) <= ForkRange)
                    forkPick.Add(e);
            forkPick.Sort((a, b) => a.Pos.DistanceTo(aimAt).CompareTo(b.Pos.DistanceTo(aimAt)));
            if (forkPick.Count > ForkBolts)
                forkPick.RemoveRange(ForkBolts, forkPick.Count - ForkBolts);
            forkPick.Sort((a, b) => (a.Pos - p.Pos).Dot(side).CompareTo((b.Pos - p.Pos).Dot(side)));  // left to right across your aim
            float dmg = ForkDamage * p.Stats.DmgMult;
            for (int i = 0; i < ForkBolts; i++)
            {
                float bend = (i - 1) * ForkBend + (i == 1 ? ForkBend * 0.35f : 0f);
                var h = new ArcHit { Fork = true, T = i * ForkGap, Dmg = dmg, Index = i, Bend = bend };
                if (forkPick.Count > 0)
                {
                    var e = forkPick[forkPick.Count == ForkBolts ? i : i % forkPick.Count];
                    h.Enemy = e.Id;
                    h.To = e.Pos;
                }
                else
                    h.To = aimAt + side * ((i - 1) * 26f);
                p.ArcHits.Add(h);
            }
            p.Run.ShotsFired++;
        }

        readonly List<Enemy> forkPick = new List<Enemy>();
        float forkLocalCd;
        public float ForkLocalCd => forkLocalCd;

        /// Your own game: Alt with Arc Lightning out (the host checks the rest). True when it was Arc Lightning's key.
        bool LocalArcFork(Player me, bool pressed, V2 aimAt, float dt)
        {
            forkLocalCd = Math.Max(0f, forkLocalCd - dt);
            if (me.Weapon.Id != WeaponId.ArcLightning || !pressed)
                return false;
            if (forkLocalCd > 0f)
                return true;
            forkLocalCd = ForkCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.Fork, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Fork);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        // --- Network: the player's Arc Lightning fields (SyncPlayer) ---

        void SyncArcTags(NetSync s, Player p)
        {
            int tagged = Math.Min(Game.ArcTagCap, p.ArcTags.Count);  // Arc Lightning's tags
            s.Byte(ref tagged);
            if (s.Reading)
                p.ArcTags.Clear();
            for (int k = 0; k < tagged; k++)
            {
                int tagId = s.Reading ? 0 : p.ArcTags[k];
                s.Int(ref tagId);
                if (s.Reading)
                    p.ArcTags.Add(tagId);
            }
        }
    }
}
