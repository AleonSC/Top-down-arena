using System;

namespace Arena
{
    /// The Engineer's second weapons (choice 2 of each role): each summons a robot Engineer of its role, which does the fighting.
    ///   Fire with no robot out (and its rebuild done) summons it at the cursor (up to BotReach away); a click on an enemy also
    ///   sends it at that enemy. With the robot out, Fire is a basic attack, and a fresh click on an enemy re-targets the robot.
    ///   Enemies within the robot's taunt radius go for it instead of you (StriderTarget). A broken robot can be summoned
    ///   again after BotRebuild seconds. The robot is healed between waves and grows 8% a wave, like the Strider.
    ///   Tank: Iron Fist: a robotic fist shoves an arc in front of you (a little damage, a long push); its Tank Bot is a heavy
    ///     robot with a nail shotgun that draws enemies from 240 px.
    ///   Healer: Stasis Gun: a small field at the cursor while you hold Fire: enemies in it are held in place (bosses slowed) and
    ///     hurt; its Medic Bot shoots rivets and heals allies near it (with nothing to shoot, it goes to the most hurt ally).
    ///   DPS: Vector Blade: a melee swipe; its Assault Bot is a fast rivet gunner.
    public partial class Game
    {
        // --- Numbers ---
        public const float TankBotHp = 850f, MedicBotHp = 420f, AssaultBotHp = 380f, BotHpPerWave = 0.08f;
        public const float BotRebuild = 8f, BotBuild = 0.8f, BotReach = 320f, BotPick = 60f, BotLeash = 360f;
        public const float TankBotTaunt = 240f, MedicBotTaunt = 150f, AssaultBotTaunt = 150f;
        public const float MedicBotHealR = 110f;
        public const float FistHalf = 0.95f, FistShove = 180f;
        public const float StasisReach = 300f, StasisRadius = 60f, StasisGrow = 8f, StasisHold = 0.15f, StasisBossSlow = 0.5f;
        public const float BladeHalf = 1.1f, BotSummonPause = 0.4f;

        public static bool IsBot(MinionKind k) => k == MinionKind.TankBot || k == MinionKind.MedicBot || k == MinionKind.AssaultBot;

        public static bool IsBotWeapon(WeaponId w) => BotFor(w) != MinionKind.None;

        public static MinionKind BotFor(WeaponId w) =>
            w == WeaponId.IronFist ? MinionKind.TankBot : w == WeaponId.StasisGun ? MinionKind.MedicBot : w == WeaponId.VectorBlade ? MinionKind.AssaultBot : MinionKind.None;

        public static float BotMaxHp(int wave, MinionKind k) =>
            (k == MinionKind.TankBot ? TankBotHp : k == MinionKind.MedicBot ? MedicBotHp : AssaultBotHp) * (1f + BotHpPerWave * Math.Max(0, wave - 1));

        public static float BotTaunt(MinionKind k) => k == MinionKind.TankBot ? TankBotTaunt : k == MinionKind.MedicBot ? MedicBotTaunt : AssaultBotTaunt;

        /// How far the robot likes to stand from what it shoots.
        static float BotStandoff(MinionKind k) => k == MinionKind.TankBot ? 110f : k == MinionKind.MedicBot ? 210f : 190f;

        /// The player's robot, or null.
        Minion BotOf(Player p)
        {
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && IsBot(m.Kind))
                    return m;
            return null;
        }

        /// The player's stasis field, or null.
        Zone StasisOf(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Owner == p.Id && z.Kind == ZoneKind.Stasis)
                    return z;
            return null;
        }

        public float StasisRadiusOf(Player p) => (StasisRadius + StasisGrow * ExtraShots(p)) * AreaMult(p);

        /// Host, from the fire loop (Game.cs): summon, re-target and the basic attacks.
        void TickBotWeapon(Player p, float dt)
        {
            bool fire = p.Firing && !p.ParryActive && State != MatchState.GameOver;
            bool pressed = fire && !p.BotFireWas;
            p.BotFireWas = fire;
            p.BotSummonT = fire ? p.BotSummonT - dt : 0f;
            if (!fire || p.BotSummonT > 0f || p.Weapon.Id != WeaponId.StasisGun)
                EndStasis(p);
            if (!fire)
                return;
            var bot = BotOf(p);
            if (bot == null && p.BotCd <= 0f)
            {
                SummonBot(p, p.AimAt);
                p.BotSummonT = BotSummonPause;  // this click summoned: no attack with it (holding on attacks after a moment)
                return;
            }
            if (p.BotSummonT > 0f)
                return;
            if (pressed && bot != null)
                SendBot(p, bot, p.AimAt);
            var w = p.Weapon;
            int x = ExtraShots(p);
            if (w.Id == WeaponId.StasisGun)
            {
                HoldStasis(p, dt);
                return;
            }
            if (p.FireCd > 0f)
                return;
            p.FireCd = w.FireInterval / FireRate(p);
            float reach = MeleeReach(p, w.Reach + w.Grow * x), dmg = w.Damage * p.Stats.DmgMult, face = p.Aim;
            bool fist = w.Id == WeaponId.IronFist;
            float half = fist ? FistHalf : BladeHalf;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || !InCone(p.Pos, face, half, reach, e.Pos, e.Radius))
                    continue;
                HurtEnemy(e, dmg, p.Id);
                if (fist && !e.Def.IsBoss)
                {
                    V2 away = e.Pos - p.Pos;
                    away = away.IsZero ? V2.FromAngle(face) : away.Normalized();
                    KnockEnemy(e, away * FistShove * (Heavy(e) ? 0.35f : 1f));
                }
            }
            if (fist)
                BroadcastFx(FxKind.FistShove, p.Pos, p.Pos + V2.FromAngle(face) * reach, Kits.Get(MinionKind.TankBot).Color, half, 0.35f, w.Sound);
            else
                BroadcastFx(FxKind.Slash, p.Pos, p.Pos + V2.FromAngle(face) * reach, Kits.Get(MinionKind.AssaultBot).Color, half, 0.22f, w.Sound);
        }

        /// The Stasis Gun held: its field sits at the cursor (up to StasisReach away), holding and hurting the enemies in it.
        void HoldStasis(Player p, float dt)
        {
            V2 at = ClampToArena(p.Pos + (p.AimAt - p.Pos).LimitLength(StasisReach * p.Stats.ShotSpeedMult), 10f);
            var z = StasisOf(p);
            float r = StasisRadiusOf(p);
            if (z == null)
            {
                z = AddZone(ZoneKind.Stasis, p, at, r, 100000f);
                BroadcastFx(FxKind.Ring, at, z.Def.Color, r, 0.3f, Sfx.Freeze);
            }
            z.Pos = at;
            z.Radius = r;
            z.T = 100000f;
            float dps = p.Weapon.Damage * p.Stats.DmgMult;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.FartherThan(at, r + e.Radius * 0.5f))
                    continue;
                HurtEnemy(e, dps * dt, p.Id);
                if (Heavy(e))
                    Slow(e, StasisHold, StasisBossSlow);
                else
                {
                    e.FrozenT = Math.Max(e.FrozenT, StasisHold);
                    e.KnockLeft = V2.Zero;  // held: no sliding out
                }
            }
        }

        void EndStasis(Player p)
        {
            var z = StasisOf(p);
            if (z != null)
                Zones.Remove(z.Id);
        }

        /// A new robot at `at` (up to BotReach from you); an enemy under the click is its first target.
        void SummonBot(Player p, V2 at)
        {
            var kind = BotFor(p.Weapon.Id);
            V2 spot = ClampToArena(p.Pos + (at - p.Pos).LimitLength(BotReach), Kits.Get(kind).Radius);
            var m = SpawnMinion(kind, p, spot, -1f, true);
            m.MaxHp = m.Hp = BotMaxHp(Wave, kind);
            m.Aim = p.Aim;
            BroadcastFx(FxKind.Ring, spot, m.Def.Color, 46f, BotBuild, Sfx.Summon);
            SendBot(p, m, at);
        }

        /// A click on an enemy sends the robot at it (until it dies); on the floor it changes nothing.
        void SendBot(Player p, Minion m, V2 at)
        {
            Enemy pick = null;
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.CloserThan(at, BotPick + e.Radius) && (pick == null || e.Pos.DistanceSq(at) < pick.Pos.DistanceSq(at)))
                    pick = e;
            if (pick == null || pick.Id == m.GoalEnemy)
                return;
            m.GoalEnemy = pick.Id;
            BroadcastFx(FxKind.Target, pick.Pos, m.Def.Color, pick.Radius + 12f, 0.5f, Sfx.Pick);
        }

        /// Host, from TickEngineer: robots leave with their weapon, grow with the wave, are patched up between waves.
        void KeepBots(Player p, float dt)
        {
            p.BotCd -= dt;
            var m = BotOf(p);
            if (m == null)
                return;
            var kind = BotFor(p.Weapon.Id);
            if (m.Kind != kind)
            {
                Minions.Remove(m.Id);  // re-armed: the old robot goes
                return;
            }
            float max = BotMaxHp(Wave, kind);
            if (max > m.MaxHp)
                m.Hp += max - m.MaxHp;
            m.MaxHp = Math.Max(m.MaxHp, max);
            if (!Battle)
                m.Hp = m.MaxHp;
        }

        /// Host, from TickMinions: the robot walks into range of its target (its click target, else the enemy nearest you) and
        /// shoots it; the Medic Bot also heals allies near it. Replicated for the look: Aim, Wind (2-3 building, then its recoil),
        /// Stacks (1 sent at a target), LinkTo (that target's id).
        void TickBot(Minion m, Player owner, float dt, float speed, float dmg)
        {
            var d = m.Def;
            m.SentT += dt;
            if (m.SentT < BotBuild)
            {
                m.Wind = 2f + m.SentT / BotBuild;
                return;
            }
            m.Wind = m.Wind >= 2f ? 0f : Math.Max(0f, m.Wind - dt * 6f);
            Enemy target = null;
            if (m.GoalEnemy != 0)
            {
                Enemy locked;
                if (Enemies.TryGetValue(m.GoalEnemy, out locked) && Targetable(locked))
                    target = locked;
                else
                    m.GoalEnemy = 0;
            }
            bool sent = target != null;
            if (target == null && Battle)
                target = NearestInLeash(m.Pos, owner.Pos, BotLeash);
            V2 goal;
            float stop;
            if (target != null)
            {
                V2 back = m.Pos - target.Pos;
                float want = Math.Min(BotStandoff(m.Kind), d.Range * 0.8f);
                goal = target.Pos + (back.IsZero ? V2.FromAngle(m.Aim + MathX.Pi) : back.Normalized()) * want;
                stop = 8f;
            }
            else
            {
                goal = owner.Pos + V2.FromAngle(owner.Aim + MathX.Pi * (m.Kind == MinionKind.TankBot ? 0.25f : 0.75f)) * 60f;
                stop = 10f;
                Player hurt = m.Kind == MinionKind.MedicBot ? MostHurtAlly() : null;
                if (hurt != null)
                {
                    goal = hurt.Pos;  // the Medic Bot, with nothing to shoot, goes to whoever is most hurt
                    stop = MedicBotHealR * 0.4f;
                }
            }
            V2 to = goal - m.Pos;
            if (to.Length > stop)
                m.Pos += to.Normalized() * Math.Min(speed * dt, to.Length - stop);
            m.Pos = ClampToArena(m.Pos, d.Radius);
            if (target != null)
                m.Aim = (target.Pos - m.Pos).Angle;
            else if (to.Length > 1f)
                m.Aim = to.Angle;
            if (target != null && m.Cd <= 0f && target.Pos.CloserThan(m.Pos, d.Range + target.Radius))
            {
                m.Cd = d.Interval;
                m.Wind = 1f;
                V2 dir = V2.FromAngle(m.Aim), muzzle = m.Pos + dir * (d.Radius + 4f);
                switch (m.Kind)
                {
                    case MinionKind.TankBot:
                        for (int i = 0; i < 5; i++)
                            SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(m.Aim + (i - 2f) * 0.11f + Rand(-0.03f, 0.03f)) * 650f * Rand(0.92f, 1.05f), 0f, d.Range / 600f, dmg, m.Owner);
                        Sounds.Add(Sfx.ShotShotgun);
                        break;
                    case MinionKind.MedicBot:
                        SpawnBullet(Defs.HealerShot, muzzle, dir * 850f, 0f, d.Range / 820f, dmg, m.Owner);
                        break;
                    default:
                        SpawnBullet(Defs.DpsShot, muzzle, V2.FromAngle(m.Aim + Rand(-0.04f, 0.04f)) * 900f, 0f, d.Range / 870f, dmg, m.Owner);
                        break;
                }
            }
            if (m.Kind == MinionKind.MedicBot)
                foreach (var q in Players.Values)
                    if (!q.Downed && q.Pos.CloserThan(m.Pos, MedicBotHealR))
                        Heal(q, d.HealRate * dt, owner, "Medic Bot");
            m.Stacks = sent ? 1 : 0;
            m.LinkTo = sent ? target.Id : 0;
        }

        /// The standing player with the least health for their maximum, if anyone is hurt.
        Player MostHurtAlly()
        {
            Player best = null;
            foreach (var q in Players.Values)
                if (!q.Downed && q.Hp < q.MaxHp - 1f && (best == null || q.Hp / q.MaxHp < best.Hp / best.MaxHp))
                    best = q;
            return best;
        }

        /// A robot fell (HurtGuard): its owner can summon the next after BotRebuild.
        void BotFallen(Minion m)
        {
            Player owner;
            if (Players.TryGetValue(m.Owner, out owner))
                owner.BotCd = BotRebuild;
            BroadcastFx(FxKind.Boom, m.Pos, m.Def.Color, 50f, 0.5f, Sfx.Explode);
        }
    }
}
