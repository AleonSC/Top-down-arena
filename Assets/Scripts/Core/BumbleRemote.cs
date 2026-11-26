using System;

namespace Arena
{
    /// The DPS Engineer's Bumble Remote (the DPS weapon on the lobby's 2nd key; mockup: the Hive Drone artifact).
    /// You hold a remote and never attack yourself: a giant robot bee, the Bumble Bot (MinionKind.BumbleBot, a weapon companion
    /// flown by MoveBumble), follows your cursor on a springy flight and does all the fighting. Enemies still go for you, never for it,
    /// and it can't be hurt. Its mode is the replicated Minion.Wind: 0 cruising, 1 Fire held, 2 diving.
    ///   Stinger Stream (always on): it shoots stingers (Defs.DpsShot) at the enemy nearest it (within BumbleRange) every BumbleAuto s;
    ///     holding Fire doubles the rate (BumbleHeld) and, with nothing in range, sprays them the way it faces. Each extra projectile adds
    ///     one more stinger to each shot, fanned.
    ///   Swarm Burst (Alt, Msg.WeaponDash WeaponAlt.BumbleBurst, BumbleBurstCd cooldown): it dives to your cursor and lands with a shockwave:
    ///     BumbleBurstDmg to every enemy within BumbleBurstR, shoved out (heavies less, bosses not at all), and BumbleBees free bees
    ///     (the Honeybee Staff's MinionKind.Bee, one more per extra projectile) burst out of it to sting the enemies nearest them.
    ///   It cruises no farther than BumbleLeash from you; while you're down it hovers over you and holds its fire.
    public partial class Game
    {
        public const float BumbleRange = 340f, BumbleAuto = 0.24f, BumbleHeld = 0.12f, BumbleShotSpeed = 900f, BumbleFan = 0.09f;
        public const float BumbleSpring = 14f, BumbleDamp = 0.86f, BumbleMaxSpeed = 1100f, BumbleLeash = 460f;
        public const float BumbleBurstCd = 6f, BumbleBurstR = 95f, BumbleBurstDmg = 30f, BumbleShove = 120f, BumbleDiveSpeed = 1500f;
        public const float BumbleBeeDmg = 8f, BumbleBeeSpeed = 240f;
        public const int BumbleBees = 6;
        public static readonly Rgba BumbleGold = new Rgba(1f, 0.8f, 0.3f);

        public static float BumbleBurstRadiusOf(Player p) => BumbleBurstR * AreaMult(p);

        Minion BumbleOf(Player p)
        {
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.BumbleBot && m.Companion)
                    return m;
            return null;
        }

        /// Where the Bumble Bot heads: your cursor, held within BumbleLeash of you (over you while you're down).
        static V2 BumbleGoal(Player p) => p.Downed ? p.Pos + new V2(0f, -40f) : p.Pos + (p.AimAt - p.Pos).LimitLength(BumbleLeash);

        /// Host, each tick for the Bumble Bot (instead of the usual moves): flies, shoots, dives. True when it should go (re-armed).
        bool MoveBumble(Minion m, Player owner, float dt)
        {
            if (owner.Weapon.Id != WeaponId.BumbleRemote)
                return true;
            float r = m.Def.Radius;
            if (m.Wind == 2f)
                return false;  // diving: TickBumbleDive flies it (the burst hatches bees, which can't happen inside the minion loop)

            // A springy flight after the cursor: it overshoots a little and settles.
            V2 goal = BumbleGoal(owner);
            m.Vel += (goal - m.Pos) * (BumbleSpring * dt);
            m.Vel *= (float)Math.Pow(BumbleDamp, dt * 60f);
            m.Vel = m.Vel.LimitLength(BumbleMaxSpeed);
            m.Pos = ClampToArena(m.Pos + m.Vel * dt, r);

            bool can = !owner.Downed && owner.StunT <= 0f && owner.SwallowT <= 0f && State != MatchState.GameOver;
            bool held = can && owner.Firing && !owner.ParryActive;
            m.Wind = held ? 1f : 0f;
            var target = can ? NearestEnemy(m.Pos, BumbleRange, by: owner.Id) : null;
            float face = target != null ? (target.Pos - m.Pos).Angle : m.Vel.Length > 40f ? m.Vel.Angle : (owner.AimAt - owner.Pos).Angle;
            m.Aim += MathX.WrapPi(face - m.Aim) * Math.Min(1f, dt * 12f);
            m.LinkTo = target != null ? target.Id : 0;

            m.Cd -= dt * FireRate(owner);
            if (!can || m.Cd > 0f || target == null && !held)
                return false;
            m.Cd = held ? BumbleHeld : BumbleAuto;
            float aim = target != null ? (target.Pos - m.Pos).Angle : m.Aim;
            int n = 1 + ExtraShots(owner);
            float dmg = owner.Weapon.Damage * owner.Stats.DmgMult;
            V2 muzzle = m.Pos + V2.FromAngle(aim) * (r + 4f);
            if (target != null && muzzle.CloserThan(target.Pos, target.Radius + 6f))
                muzzle = target.Pos - V2.FromAngle(aim) * (target.Radius + 8f);  // hovering on top of it: shots spawned inside an enemy miss it, so they start at its edge
            for (int i = 0; i < n; i++)
            {
                float a = aim + (i - (n - 1) / 2f) * BumbleFan + Rand(-0.035f, 0.035f);
                SpawnBullet(Defs.DpsShot, muzzle, V2.FromAngle(a) * BumbleShotSpeed * owner.Stats.ShotSpeedMult, 0f, BumbleRange * 1.15f / BumbleShotSpeed, dmg, owner.Id);
            }
            owner.Run.ShotsFired++;
            Sounds.Add(Sfx.ShotSmg);
            return false;
        }

        /// Host, from TickEngineer (outside the minion loop): the Bumble Bot's dive to the cursor, and the burst when it lands.
        void TickBumbleDive(Player p, float dt)
        {
            if (p.Weapon.Id != WeaponId.BumbleRemote)
                return;
            var m = BumbleOf(p);
            if (m == null || m.Wind != 2f)
                return;
            V2 to = m.Goal - m.Pos;
            float d = to.Length, step = BumbleDiveSpeed * dt;
            if (d <= Math.Max(12f, step))
            {
                m.Pos = m.Goal;
                m.Vel = V2.Zero;
                m.Wind = 0f;
                string was = CreditTo("weapon");
                BumbleBurst(m, p);
                dmgSrc = was;
            }
            else
            {
                m.Pos = ClampToArena(m.Pos + to / d * step, m.Def.Radius);
                m.Vel = to / d * (BumbleDiveSpeed * 0.3f);
                m.Aim = to.Angle;
            }
        }

        /// The Bumble Bot lands from its dive: a shockwave that hurts and shoves, and a burst of bees.
        void BumbleBurst(Minion m, Player p)
        {
            float R = BumbleBurstRadiusOf(p), dmg = BumbleBurstDmg * p.Stats.DmgMult;
            bool any = false;
            foreach (var e in FoesInCircle(m.Pos, R, by: p.Id))
            {
                any = true;
                WeaponHurt(e, dmg, p.Id);
                if (e.Hp <= 0f || e.Def.IsBoss)
                    continue;
                V2 away = e.Pos - m.Pos;
                away = away.IsZero ? V2.FromAngle(m.Aim) : away.Normalized();
                KnockEnemy(e, away * BumbleShove * (Heavy(e) ? 0.35f : 1f));
            }
            int n = BumbleBees + ExtraShots(p);
            float power = BumbleBeeDmg / Defs.WeaponOf(WeaponId.HoneybeeStaff).Damage * p.Stats.DmgMult * p.Stats.MinionDmgMult;
            for (int i = 0; i < n; i++)
            {
                float a = MathX.Tau * i / n + m.Aim;
                var b = SpawnMinion(MinionKind.Bee, p, m.Pos + V2.FromAngle(a) * 10f, BeeLife);
                b.Power = power;
                b.Mastered = true;  // a free bee: it doesn't count against the Honeybee Staff's cap (HoneybeeStaff.cs)
                b.Hold = 0f;
                b.Wind = 1f;
                b.Vel = V2.FromAngle(a) * BumbleBeeSpeed;
                b.Aim = a;
            }
            p.Run.ShotsFired++;
            if (any)
                p.Run.ShotsHit++;
            BroadcastFx(FxKind.BumbleBurst, m.Pos, BumbleGold, R, 0.5f, Sfx.BossSlam);
        }

        /// Host: Alt pressed with the Bumble Remote: the Bumble Bot dives to the cursor, if the burst is ready.
        void HostBumbleBurst(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.BumbleRemote || p.Downed || p.StunT > 0f || p.BumbleCd > 0f || State == MatchState.GameOver)
                return;
            var m = BumbleOf(p);
            if (m == null || m.Wind == 2f)
                return;
            p.BumbleCd = BumbleBurstCd;
            m.Wind = 2f;
            m.Goal = ClampToArena(p.Pos + (aimAt - p.Pos).LimitLength(BumbleLeash), m.Def.Radius);
            BroadcastSound(Sfx.BossWhoosh);
        }

        /// Host, from the fire loop: the burst cooling down. Fire never shoots from you: the Bumble Bot reads it (MoveBumble).
        void TickBumbleRemote(Player p, float dt) => p.BumbleCd = Math.Max(0f, p.BumbleCd - dt);

        float bumbleLocalCd;  // your own game: until the Swarm Burst is ready again (the ring under your feet)
        public float BumbleBurstLocalCd => bumbleLocalCd;

        /// Your own game: Alt with the Bumble Remote. True when it sent something (LocalWeaponDash stops there).
        bool LocalBumbleBurst(Player me, V2 aimAt, bool pressed, float dt)
        {
            bumbleLocalCd = Math.Max(0f, bumbleLocalCd - dt);
            if (!pressed || me.Weapon.Id != WeaponId.BumbleRemote || bumbleLocalCd > 0f)
                return false;
            bumbleLocalCd = BumbleBurstCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.BumbleBurst, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.BumbleBurst);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): the Swarm Burst on a group
        static bool BumbleRemoteBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            return g.Tap(g.BumbleBurstLocalCd <= 0f && g.EnemiesNear(v.Me.Pos, 260f) >= 3, 1f);
        }
    }
}
