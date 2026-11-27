using System;
using System.Collections.Generic;

namespace Arena
{
    /// Shadow Clone Medics: the Healer Ninja's fourth ultimate (lobby key 3 of the Ultimate row; Blossom Storm stays the default).
    /// From its mockup (Docs/Mockups/shadow-clone-medics.html):
    ///   - The cast: a hand seal, and a puff of ink smoke beside every player (you included) out of which steps a shadow clone
    ///     medic (`MinionKind.CloneMedic`, LinkTo its player) for ScmTime s.
    ///   - Each clone trails its player ScmTrail px away on the side of the nearest enemy, and heals them ScmHeal every
    ///     ScmHealEvery s while they're up.
    ///   - It steps into the shots aimed at its player: it dashes onto the line of the nearest enemy shot heading for them
    ///     (within ScmWatch px) and stops every enemy shot aimed at them that touches it, red aimed shots too (CloneMedicStops,
    ///     from Combat.cs). Shots aimed at anyone else pass through it, as do your team's; melee enemies ignore it.
    ///   - It has ScmHp + ScmHpPerWave a wave health (ScmSoloHp times that alone); each shot it stops takes its damage off.
    ///     At 0 it pops in smoke. When it goes (broken, on time, between waves), its player heals ScmFadeHeal of what it soaked.
    /// Mastered: ScmTimeMastered s and ScmMasteredHp times the health. A new cast replaces your old clones.
    /// Replicated as ordinary minions (position, timer, GuardFrac = its health, LinkTo); its puffs are `FxKind.CloneMedicPuff`
    /// (Radius 0: it steps out, 1: it goes, 2: a shot it stopped). Drawn by Game/ShadowCloneMedicsLook.cs.
    public partial class Game
    {
        public const float ScmCooldown = 80f, ScmTime = 10f, ScmTimeMastered = 12f;
        public const float ScmHp = 120f, ScmHpPerWave = 6f, ScmSoloHp = 1.5f, ScmMasteredHp = 1.4f;
        public const float ScmHeal = 4f, ScmHealEvery = 0.5f, ScmFadeHeal = 0.2f;
        public const float ScmTrail = 26f, ScmGuard = 30f, ScmWatch = 260f, ScmSpeed = 900f;  // its spot by them, where it stands against a shot, how far it looks for shots, how fast it moves
        public const float ScmAimSlack = 18f;  // a shot is aimed at the player when its line passes this close to their body
        public static readonly Rgba ScmJade = new Rgba(0.37f, 0.88f, 0.63f), ScmInk = new Rgba(0.70f, 0.61f, 0.96f);

        /// Shadow Clone Medics' entry: key 3 of the Healer Ninja's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] ScmUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.ShadowCloneMedics, Kit = Kit.Ninja, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Shadow Clone Medics", Cooldown = ScmCooldown,
                Desc = "A shadow clone medic follows every player for 10 s, you included: it heals them and steps into the shots aimed at them.",
                Capstone = "Lasts 12 s and clones have 40% more health.",
                Bot = b => b.AllyHurt || b.Danger,
            },
        };

        readonly Dictionary<int, float> scmSoaked = new Dictionary<int, float>();  // host: each clone's damage stopped (by minion id)

        void CastCloneMedics(Player p, bool mastered)
        {
            var old = new List<Minion>();
            foreach (var m0 in Minions.Values)
                if (m0.Owner == p.Id && m0.Kind == MinionKind.CloneMedic)
                    old.Add(m0);
            foreach (var m0 in old)
                CloneMedicGone(m0, true);
            float life = mastered ? ScmTimeMastered : ScmTime;
            float hp = (ScmHp + ScmHpPerWave * Wave) * (Players.Count <= 1 ? ScmSoloHp : 1f) * (mastered ? ScmMasteredHp : 1f);
            foreach (var q in Players.Values)
            {
                if (Jailed(q))
                    continue;
                V2 side = V2.FromAngle(q.Aim + MathX.Pi * 0.75f);
                var m = SpawnMinion(MinionKind.CloneMedic, p, ClampToArena(q.Pos + side * ScmTrail, 12f), life);
                m.LinkTo = q.Id;
                m.Hp = m.MaxHp = hp;
                m.Mastered = mastered;
                m.Ang = ScmHealEvery;  // (its heal timer: the minion loop runs Cd down by fire rate)
                m.Aim = q.Aim;
                scmSoaked[m.Id] = 0f;
                BroadcastFx(FxKind.CloneMedicPuff, m.Pos, ScmInk, 0f, 0.6f, Sfx.None);
            }
            p.UltT = life;
            BroadcastFx(FxKind.Ring, p.Pos, p.Pos, ScmJade, 80f, 0.5f, Sfx.NinjaVanish);
        }

        /// Host: a clone goes (broken, on time, between waves, replaced): a puff of smoke, and its player heals ScmFadeHeal of
        /// what it soaked. `remove`: take it out of Minions here (the minion loop removes the ones it expires itself).
        void CloneMedicGone(Minion m, bool remove)
        {
            float soaked;
            scmSoaked.TryGetValue(m.Id, out soaked);
            scmSoaked.Remove(m.Id);
            if (remove)
                Minions.Remove(m.Id);
            Player linked, owner;
            if (soaked > 0f && Players.TryGetValue(m.LinkTo, out linked) && !linked.Downed && Players.TryGetValue(m.Owner, out owner))
                Heal(linked, soaked * ScmFadeHeal, owner);
            BroadcastFx(FxKind.CloneMedicPuff, m.Pos, ScmInk, 1f, 0.7f, Sfx.None);
        }

        /// Is this enemy shot heading for `q` (its line passes within their body plus ScmAimSlack, ahead of it)?
        static bool ScmAimedAt(Bullet b, Player q)
        {
            V2 to = q.Pos - b.Pos;
            if (b.Vel.IsZero)
                return false;
            V2 dir = b.Vel.Normalized();
            float along = to.Dot(dir);
            if (along < -q.Def.BodyR)
                return false;
            return (to - dir * along).Length <= q.Def.BodyR + ScmAimSlack + b.Radius;
        }

        /// Host, each tick (Minions.cs). Returns false once it goes (its player gone, or the wave over).
        bool TickCloneMedic(Minion m, Player owner, float dt)
        {
            Player linked;
            if (!Players.TryGetValue(m.LinkTo, out linked) || !Battle)
            {
                CloneMedicGone(m, false);
                return false;
            }
            // Its spot: on the line of the nearest shot heading for its player, else beside them toward the nearest enemy.
            Bullet threat = null;
            float best = ScmWatch * ScmWatch;
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || b.Kind == Defs.HollowShot)
                    continue;
                float d = b.Pos.DistanceSq(linked.Pos);
                if (d < best && ScmAimedAt(b, linked))
                {
                    best = d;
                    threat = b;
                }
            }
            V2 goal;
            if (threat != null)
            {
                V2 away = threat.Pos - linked.Pos;
                goal = linked.Pos + (away.IsZero ? V2.FromAngle(m.Aim) : away.Normalized()) * ScmGuard;
                m.Aim = away.Angle;
            }
            else
            {
                var e = NearestEnemy(linked.Pos, 900f, by: owner.Id);
                float ang = e != null ? (e.Pos - linked.Pos).Angle : linked.Aim + MathX.Pi * 0.75f;
                goal = linked.Pos + V2.FromAngle(ang) * ScmTrail;
                m.Aim = ang;
            }
            m.Pos = ClampToArena(m.Pos + (goal - m.Pos).LimitLength(ScmSpeed * dt), 12f);
            // The heal: a pulse every ScmHealEvery s.
            m.Ang -= dt;
            if (m.Ang <= 0f)
            {
                m.Ang += ScmHealEvery;
                if (!linked.Downed)
                    Heal(linked, ScmHeal * m.Power, owner);
            }
            return true;
        }

        /// Host, from the enemy-shot loop in Combat.cs: does a clone step into this shot? Every enemy shot aimed at a clone's
        /// player that touches the clone does, red aimed shots too.
        bool CloneMedicStops(Bullet b)
        {
            foreach (var m in Minions.Values)
            {
                if (m.Kind != MinionKind.CloneMedic || m.Hp <= 0f || !b.Pos.CloserThan(m.Pos, m.Def.Radius + b.Radius))
                    continue;
                Player linked;
                if (!Players.TryGetValue(m.LinkTo, out linked) || !ScmAimedAt(b, linked))
                    continue;
                float dmg = Math.Min(b.Dmg, m.Hp);
                m.Hp -= b.Dmg;
                float soaked;
                scmSoaked.TryGetValue(m.Id, out soaked);
                scmSoaked[m.Id] = soaked + dmg;
                BroadcastFx(FxKind.CloneMedicPuff, b.Pos, ScmInk, 2f, 0.35f, Sfx.Hit);
                if (m.Hp <= 0f)
                    CloneMedicGone(m, true);
                return true;
            }
            return false;
        }
    }
}
