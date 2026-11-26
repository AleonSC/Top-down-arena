using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Tank Summoner's Wyvern Staff (the tank weapon on the lobby's 4th key; mockup: the Wyvern Staff artifact).
    /// The Wyvern is a companion Minion (MinionKind.Wyvern, MinionMove.FollowOwner, flown by MoveWyvern); its mode is the replicated
    /// Minion.Wind: 0 hovering, 1 breathing, 2 diving.
    ///   Gale (always on): it hovers just above you and keeps a wind blowing WyvGaleR round itself: enemies in it are slowed
    ///     (WyvGaleSlow, bosses WyvGaleBossSlow) and shoved WyvGaleShove px/s (not bosses or heavies), and enemy shots in it fly WyvGaleShotSlow as
    ///     fast. Each enemy in the gale feeds the Cinder meter (CinderPerFoe a second). After Fire or a slam it drifts slowly back to
    ///     you (WyvReturn) instead of snapping home (WyvFollow).
    ///   Fire (held): the Wyvern flies out to BreathStand short of your cursor (at most BreathReach from you) and streams fire at it
    ///     while you hold Fire and Player.Cinder lasts (CinderDrain a second; it needs CinderMin to start). Every BreathTick the stream
    ///     hurts each enemy near the line from it to your cursor for the weapon's damage a second and sets it burning.
    ///   Alt (Msg.WeaponDash WeaponAlt.WyvernSlam, WyvernSlamCd cooldown): Wyrm Slam: it dives to your cursor and slams the ground:
    ///     WyvernSlamDmg to every enemy within WyvernSlamR, stunned (heavies and bosses for less), the small enemy shots in it cleared,
    ///     and CinderBack Cinder back for each enemy stunned (up to CinderBackMax).
    ///   Extra projectiles: +25% breath damage and 10% bigger gale and slam each. Homing Rounds widen the gale and the slam (AreaMult).
    ///   Stormwing (Rare card): +25% gale shove and slow, and the slam is 25 px wider.
    public partial class Game
    {
        public const float WyvGaleR = 95f, WyvGaleShove = 34f, WyvGaleSlow = 0.7f, WyvGaleBossSlow = 0.85f, WyvGaleShotSlow = 0.7f;
        public const float CinderMax = 100f, CinderRegen = 6f, CinderPerFoe = 1.5f, CinderDrain = 30f, CinderMin = 15f;
        public const float BreathReach = 420f, BreathStand = 120f, BreathWidth = 26f, BreathTick = 0.1f, BreathBurnTime = 2f, BreathBurnDps = 4f;
        public const float WyvernSlamCd = 7f, WyvernSlamR = 130f, WyvernSlamDmg = 30f, SlamStun = 1.2f, SlamStunHeavy = 0.6f, SlamStunBoss = 0.4f;
        public const float CinderBack = 10f, CinderBackMax = 4f, WyvDiveSpeed = 1400f, WyvFollow = 240f, WyvReturn = 90f, WyvFly = 420f;
        public static readonly Rgba WyvColor = new Rgba(0.44f, 0.88f, 0.81f);

        /// How hard Stormwing and extra projectiles make the gale's shove and slow.
        static float WyvGaleBoost(Player p) => 1f + p.Stats.WyvernPower;
        public static float WyvGaleRadiusOf(Player p) => WyvGaleR * AreaMult(p) * (1f + 0.1f * p.Stats.ExtraShots);
        public static float WyvernSlamRadiusOf(Player p) => (WyvernSlamR + 25f * p.Stats.WyvernPower) * AreaMult(p) * (1f + 0.1f * p.Stats.ExtraShots);

        Minion WyvernOf(Player p)
        {
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Wyvern && m.Companion)
                    return m;
            return null;
        }

        /// Where the breath goes: your cursor, held within BreathReach of you.
        static V2 BreathPoint(Player p) => p.Pos + (p.AimAt - p.Pos).LimitLength(BreathReach);

        /// Host, each tick for a Wyvern (instead of the usual follow): it hovers above you, flies out to breathe, or dives.
        void MoveWyvern(Minion m, Player owner, float dt)
        {
            if (m.Wind == 2f)
            {
                V2 to = m.Goal - m.Pos;
                float d = to.Length, step = WyvDiveSpeed * dt;
                if (d <= Math.Max(14f, step))
                {
                    m.Pos = m.Goal;
                    WyvernSlam(m, owner);
                    m.Wind = 0f;
                    m.Returning = true;
                }
                else
                {
                    m.Pos += to / d * step;
                    m.Aim = to.Angle;
                }
                return;
            }
            V2 home = owner.Pos + new V2((float)Math.Cos(hostClock * 1.1f) * 34f, -64f + (float)Math.Sin(hostClock * 1.6f) * 10f);
            V2 goal = home, face = default(V2);
            float speed;
            bool breathing = m.Wind == 1f;
            if (breathing)
            {
                V2 at = BreathPoint(owner), back = owner.Pos - at;
                goal = at + (back.IsZero ? new V2(0f, 1f) : back.Normalized()) * BreathStand;
                face = at - m.Pos;
                speed = WyvFly;
            }
            else
            {
                speed = m.Returning ? WyvReturn : WyvFollow;
                if (m.Returning && m.Pos.CloserThan(home, 40f))
                    m.Returning = false;
                if (m.Pos.FartherThan(owner.Pos, 380f))
                    speed = Math.Max(speed, 300f);  // never left behind
            }
            V2 off = goal - m.Pos;
            float dd = off.Length, mv = Math.Min(dd, Math.Min(speed, dd * 3.2f) * dt);
            if (dd > 0.5f)
                m.Pos += off / dd * mv;
            if (face.IsZero)
                face = dd > 0.5f && mv / dt > 60f ? off : owner.AimAt - m.Pos;
            if (!face.IsZero)
                m.Aim += MathX.WrapPi(face.Angle - m.Aim) * Math.Min(1f, dt * 8f);
        }

        /// Host, every tick the Wyvern Staff is out (and you're up): the gale, the Cinder meter, the breath.
        void TickWyvernStaff(Player p, float dt)
        {
            p.WyvSlamCd = Math.Max(0f, p.WyvSlamCd - dt);
            var m = WyvernOf(p);
            if (m == null)
            {
                p.Cinder = Math.Min(CinderMax, p.Cinder + CinderRegen * dt);
                return;
            }
            if (m.Wind != 2f)
                TickWyvGale(m, p, dt);
            bool can = !p.ParryActive && p.StunT <= 0f && p.SwallowT <= 0f && State != MatchState.GameOver;
            bool want = p.Firing && can && m.Wind != 2f;
            if (m.Wind == 1f && (!want || p.Cinder <= 0f))
            {
                m.Wind = 0f;
                m.Returning = true;
            }
            else if (m.Wind == 0f && want && p.Cinder >= CinderMin)
            {
                m.Wind = 1f;
                m.Returning = false;
                p.WyvTick = 0.0001f;  // (never exactly 0 while breathing: Game.cs clears the breath when the staff is put away)
                BroadcastSound(Sfx.SpellFire);
            }
            bool breathing = m.Wind == 1f;
            p.Cinder = Math.Max(0f, Math.Min(CinderMax, p.Cinder + (CinderRegen + CinderPerFoe * p.WyvFoes - (breathing ? CinderDrain : 0f)) * dt));
            if (!breathing)
                return;
            p.WyvTick -= dt;
            while (p.WyvTick <= 0f)
            {
                p.WyvTick += BreathTick;
                Breathe(m, p);
            }
        }

        /// The gale: slows and shoves every enemy in it, and counts them for the meter.
        void TickWyvGale(Minion m, Player p, float dt)
        {
            float R = WyvGaleRadiusOf(p), boost = WyvGaleBoost(p);
            int foes = 0;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.FartherThan(m.Pos, R + e.Radius))
                    continue;
                foes++;
                WindHit(e, e.Pos - m.Pos);  // Wind.cs
                Slow(e, 0.25f, e.Def.IsBoss ? 1f - (1f - WyvGaleBossSlow) * boost : 1f - (1f - WyvGaleSlow) * boost);
                if (!Heavy(e))
                {
                    V2 rel = e.Pos - m.Pos;
                    if (!rel.IsZero)
                        KnockEnemy(e, rel.Normalized() * (WyvGaleShove * boost * dt));
                }
            }
            p.WyvFoes = foes;
        }

        /// One tick of the stream: hurts and ignites every enemy near the line from the Wyvern to your cursor.
        void Breathe(Minion m, Player p)
        {
            V2 a = m.Pos, b = BreathPoint(p), ab = b - a;
            float len2 = Math.Max(1f, ab.X * ab.X + ab.Y * ab.Y);
            float dmg = p.Weapon.Damage * BreathTick * p.Stats.DmgMult * p.Stats.MinionDmgMult * (1f + 0.25f * ExtraShots(p));
            bool any = false;
            foreach (var e in AllFoes(by: p.Id))
            {
                V2 ae = e.Pos - a;
                float t = Math.Max(0f, Math.Min(1f, (ae.X * ab.X + ae.Y * ab.Y) / len2));
                if (e.Pos.FartherThan(a + ab * t, BreathWidth + e.Radius))
                    continue;
                any = true;
                WeaponHurt(e, dmg, p.Id);
                if (e.Hp > 0f)
                {
                    e.BurnT = Math.Max(e.BurnT, BreathBurnTime);
                    e.BurnDps = Math.Max(e.BurnDps, BreathBurnDps * p.Stats.DmgMult);
                }
            }
            p.Run.ShotsFired++;
            if (any)
                p.Run.ShotsHit++;
        }

        readonly List<int> wyvShots = new List<int>();

        /// The Wyvern lands: the shockwave hurts, stuns and clears; each enemy stunned gives some Cinder back.
        void WyvernSlam(Minion m, Player p)
        {
            float R = WyvernSlamRadiusOf(p);
            float dmg = WyvernSlamDmg * p.Stats.DmgMult * p.Stats.MinionDmgMult;
            int back = 0;
            bool any = false;
            foreach (var e in FoesInCircle(m.Pos, R, by: p.Id))
            {
                any = true;
                WeaponHurt(e, dmg, p.Id);
                if (e.Hp <= 0f)
                    continue;
                Stun(e, e.Def.IsBoss ? SlamStunBoss : e.Def.Heavy ? SlamStunHeavy : SlamStun);
                if (back < CinderBackMax)
                {
                    back++;
                    p.Cinder = Math.Min(CinderMax, p.Cinder + CinderBack);
                }
            }
            wyvShots.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Pos.WithinReach(m.Pos, R))
                    wyvShots.Add(b.Id);
            foreach (int id in wyvShots)
                KillBullet(id);
            p.Run.ShotsFired++;
            if (any)
                p.Run.ShotsHit++;
            BroadcastFx(FxKind.WyvernSlam, m.Pos, WyvColor, R, 0.55f, Sfx.BossSlam);
        }

        /// Host: Alt pressed with the Wyvern Staff: the Wyvern dives to the cursor, if the slam is ready.
        void HostWyvernSlam(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.WyvernStaff || p.Downed || p.StunT > 0f || p.WyvSlamCd > 0f || State == MatchState.GameOver)
                return;
            var m = WyvernOf(p);
            if (m == null || m.Wind == 2f)
                return;
            p.WyvSlamCd = WyvernSlamCd;
            m.Wind = 2f;
            m.Goal = ClampToArena(p.Pos + (aimAt - p.Pos).LimitLength(BreathReach), 20f);
            BroadcastSound(Sfx.BossWhoosh);
        }

        float wyvLocalCd;  // your own game: until the slam is ready again (the ring under your feet)
        public float WyvernSlamLocalCd => wyvLocalCd;

        /// Your own game: Alt with the Wyvern Staff. True when it sent something (LocalWeaponDash stops there).
        bool LocalWyvernSlam(Player me, V2 aimAt, bool pressed, float dt)
        {
            wyvLocalCd = Math.Max(0f, wyvLocalCd - dt);
            if (!pressed || me.Weapon.Id != WeaponId.WyvernStaff || wyvLocalCd > 0f)
                return false;
            wyvLocalCd = WyvernSlamCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.WyvernSlam, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.WyvernSlam);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// Off when you put the staff away or go down: the Wyvern stops breathing and the slam cools down.
        void ClearWyvernStaff(Player p)
        {
            var m = WyvernOf(p);
            if (m != null && m.Wind != 0f)
                m.Wind = 0f;
            p.WyvTick = 0f;
        }

        // --- the Gale slows enemy shots (on every peer, from the replicated Wyverns) ----------------------------------------

        struct WyvGaleSpot { public V2 Pos; public float R; }
        readonly List<WyvGaleSpot> wyvGales = new List<WyvGaleSpot>();

        void PrepareWyvGales()
        {
            wyvGales.Clear();
            foreach (var m in Minions.Values)
            {
                Player p;
                if (m.Kind != MinionKind.Wyvern || m.Wind == 2f || !Players.TryGetValue(m.Owner, out p))
                    continue;
                wyvGales.Add(new WyvGaleSpot { Pos = m.Pos, R = WyvGaleRadiusOf(p) });
            }
        }

        /// Enemy shots inside a Gale fly at WyvGaleShotSlow of their speed.
        float WyvGaleSpeed(Bullet b)
        {
            if (wyvGales.Count == 0 || !Defs.IsEnemyShot(b.Kind))
                return 1f;
            foreach (var g in wyvGales)
                if (b.Pos.DistanceSq(g.Pos) < g.R * g.R)
                    return WyvGaleShotSlow;
            return 1f;
        }
    }
}
