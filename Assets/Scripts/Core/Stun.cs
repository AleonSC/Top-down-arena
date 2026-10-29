using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Warden's legendaries: the Stun Gun (a Q / E / Shift ability that swaps your weapon for a six-shot stun
    /// gun, its cooldown starting once the shots are spent) and Warden's Guard (guards that follow you and fight,
    /// coming back a while after they fall).
    public partial class Game
    {
        public const int StunGunShots = 6, StunGunMasteredShots = 9;
        public const float StunBoltSpeed = 900f, StunBoltLife = 0.7f;  // it reaches 630 px
        const float StunGunInterval = 0.3f, StunBoltDamage = 15f, StunGunStun = 1.5f, StunBossSlow = 0.5f;
        public const float GuardRespawn = 15f;
        const float GuardLeash = 240f, GuardHurtEvery = 1f, GuardBaseHp = 150f, GuardHpPerWave = 15f;

        // --- Stun Gun ---------------------------------------------------------------------

        /// Cast: the weapon goes away and the stun gun comes out, loaded.
        void DrawStunGun(Player p, bool mastered)
        {
            p.StunShots = mastered ? StunGunMasteredShots : StunGunShots;
            p.FireCd = Math.Min(p.FireCd, 0.1f);
            p.BowRelease = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.StunBolt), 40f, 0.3f, Sfx.SpellZap);
        }

        /// While the gun is out its slot's cooldown waits (TickSlots); it starts with the last shot.
        static bool StunHeld(Player p, AbilitySlot s) => s.Id == AbilityId.StunGun && p.StunShots > 0;

        /// Host, from TickPlayers while the gun is out: a bolt per trigger pull, at its own pace.
        void TickStunGun(Player p)
        {
            p.BowRelease = 0f;  // a bow draw let go meanwhile doesn't fire later
            if (!p.Firing || p.ParryActive || p.FireCd > 0f || State == MatchState.GameOver)
                return;
            p.FireCd = StunGunInterval / FireRate(p);
            V2 dir = V2.FromAngle(p.Aim);
            firingWeapon = true;  // it's your gun for now: weapon mods and legendaries ride its bolts
            SpawnBullet(Defs.StunBolt, p.Pos + dir * p.Def.BodyR, dir * StunBoltSpeed, 0f, StunBoltLife, StunBoltDamage * p.Stats.DmgMult, p.Id);
            firingWeapon = false;
            p.StunShots--;
            if (p.StunShots == 0)
                p.FireCd = Math.Max(p.FireCd, 0.25f);  // a beat before the weapon is back in hand
        }

        /// A stun bolt landing (OnBulletHit): regular enemies are stunned, bosses slowed.
        void StunBoltHit(Bullet b, Enemy e)
        {
            if (b.Kind != Defs.StunBolt)
                return;
            if (e.Def.IsBoss)
            {
                e.SlowT = Math.Max(e.SlowT, StunGunStun);
                e.SlowMult = Math.Min(e.SlowMult, 1f - StunBossSlow);
            }
            else
            {
                e.FrozenT = Math.Max(e.FrozenT, StunGunStun);
                e.SlowT = Math.Max(e.SlowT, StunGunStun);
            }
        }

        /// How far the stun gun reaches (WeaponRange, while it's out).
        float StunGunRange(Player p) => p.Def.BodyR + StunBoltSpeed * StunBoltLife * p.Stats.ShotSpeedMult;

        // --- Warden's Guard ---------------------------------------------------------------

        /// Host, from TickMinions: keep each player's guards up, one stack each; a fallen one comes back after
        /// GuardRespawn seconds.
        void TickGuards(Player p, float dt)
        {
            int want = p.Stats.Guards, have = 0;
            Minion surplus = null;
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.WardenGuard)
                {
                    have++;
                    surplus = m;
                }
            if (have > want && surplus != null)
                Minions.Remove(surplus.Id);  // a dev tool took the upgrade away
            if (have >= want)
                return;
            p.GuardCd -= dt;
            if (p.GuardCd > 0f)
                return;
            var g = SpawnMinion(MinionKind.WardenGuard, p, p.Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * 40f, -1f, true);
            g.MaxHp = g.Hp = GuardBaseHp + GuardHpPerWave * Wave;
            g.Ang = MathX.Tau * have / Math.Max(1, want);  // where it stands beside you
            p.GuardCd = 0.5f;  // the next one (several stacks) steps in shortly after
            BroadcastFx(FxKind.Ring, g.Pos, g.Def.Color, 30f, 0.3f, Sfx.Summon);
        }

        /// A guard's movement: it goes for enemies near you (never far from you), else walks at your side.
        Enemy MoveGuard(Minion m, Player owner, float speed, float dt)
        {
            Enemy target = null;
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(owner.Pos) < GuardLeash + e.Radius
                    && (target == null || e.Pos.DistanceSq(m.Pos) < target.Pos.DistanceSq(m.Pos)))
                    target = e;
            V2 goal = target != null ? target.Pos : owner.Pos + V2.FromAngle(owner.Aim + MathX.Pi * 0.75f + m.Ang) * 45f;
            V2 to = goal - m.Pos;
            float stop = target != null ? target.Radius + m.Def.Radius : 4f;
            if (to.Length > stop)
                m.Pos += to.Normalized() * Math.Min(speed * dt, to.Length - stop);
            if (to.Length > 1f)
                m.Aim = to.Angle;
            return target;
        }

        /// Damage to a guard; at zero it falls, and its owner's next guard is on the way.
        void HurtGuard(Minion m, float dmg)
        {
            if (m.Hp <= 0f || !Minions.ContainsKey(m.Id))
                return;
            m.Hp -= dmg;
            if (m.Hp > 0f)
                return;
            Minions.Remove(m.Id);
            Player owner;
            if (Players.TryGetValue(m.Owner, out owner))
                owner.GuardCd = GuardRespawn;
            BroadcastFx(FxKind.Boom, m.Pos, m.Def.Color, 30f, 0.35f, Sfx.Hit);
        }

        /// Collide: an enemy bullet that hits a guard stops there and hurts it. Bosses' red aimed shots fly past.
        bool GuardStopsBullet(Bullet b)
        {
            if (Defs.Unblockable(b.Kind))
                return false;
            foreach (var m in Minions.Values)
                if (m.Kind == MinionKind.WardenGuard && b.Pos.DistanceTo(m.Pos) < m.Def.Radius + b.Radius)  // its body, from any side
                {
                    HurtGuard(m, b.Dmg);
                    return true;
                }
            return false;
        }

        readonly List<Minion> guardsHit = new List<Minion>();

        /// Enemy contact (TickEnemies): enemies touching a guard hurt it, at most every GuardHurtEvery seconds each.
        void GuardContact(Enemy e, float contact)
        {
            guardsHit.Clear();
            foreach (var m in Minions.Values)
                if (m.Kind == MinionKind.WardenGuard && m.HurtT <= 0f && m.Pos.DistanceTo(e.Pos) < e.Radius + m.Def.Radius + 4f)  // it stands right up against what it fights
                    guardsHit.Add(m);
            foreach (var m in guardsHit)
            {
                m.HurtT = GuardHurtEvery;
                HurtGuard(m, contact);
            }
        }
    }
}
