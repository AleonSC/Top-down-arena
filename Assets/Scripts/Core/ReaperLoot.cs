using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Twin Reapers' legendaries, each one of their own tricks turned yours (like the Warden's in Stun.cs and the
    /// Queen's in QueenLoot.cs): Twin Scythes (their scythes circle you in opposite directions, cutting enemies and
    /// small bullets), Final Reap (a phantom scythe finishes off what you've nearly killed; bosses near the end take
    /// more) and Scythe Throw (a Q / E / Shift ability: a giant scythe out to your cursor and back; catch it for a
    /// shorter cooldown). Plan and numbers: Docs/ReaperLoot.md.
    public partial class Game
    {
        // --- Twin Scythes ------------------------------------------------------------------

        public const float ScytheInner = 60f, ScytheOuter = 100f, ScytheMiddle = 80f, ScytheSpin = 2.4f, ScytheFastSpin = 1.4f;
        public const float ScytheBlade = 16f, ScytheEvery = 0.5f;  // how close an enemy's edge must come to the blade; how soon one scythe can cut the same enemy again
        public const float ScytheSweepReach = 240f, ScytheSweepEvery = 1.6f, ScytheSweepFast = 1.1f, ScytheSweepSpeed = 700f;  // each scythe reaps out at an enemy this near, this often (2nd stack: faster)

        float scytheClock;                                                       // host: seconds, for the cut cooldowns
        readonly Dictionary<long, float> scytheCuts = new Dictionary<long, float>();  // (scythe, enemy) -> when it last cut it

        /// Host, from TickMinions (before the minions move): keep each player's scythes up: two, a third at three stacks.
        void TickTwinScythes(Player p, float dt)
        {
            int want = p.Stats.TwinScythes <= 0 ? 0 : p.Stats.TwinScythes >= 3 ? 3 : 2, have = 0;
            Minion surplus = null;
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Scythe)
                {
                    have++;
                    surplus = m;
                }
            if (have > want && surplus != null)
                Minions.Remove(surplus.Id);  // a dev tool took the card away
            for (int i = have; i < want; i++)
            {
                // Dusk close and turning one way, Dawn wider and turning the other; a third between them.
                float ring = i == 0 ? ScytheInner : i == 1 ? -ScytheOuter : ScytheMiddle;
                var m = SpawnMinion(MinionKind.Scythe, p, p.Pos + V2.FromAngle(MathX.Pi * i) * Math.Abs(ring), -1f, true);
                m.Ring = ring;
                m.Ang = MathX.Pi * i;
            }
        }

        /// Host, each tick for a scythe (instead of its usual moves): circle its owner cutting what it passes, and
        /// every ScytheSweepEvery seconds reap out at the nearest enemy within ScytheSweepReach (their reaps), then fly
        /// back to its circle.
        void MoveScythe(Minion m, Player owner, float dt)
        {
            float spin = ScytheSpin * (owner.Stats.TwinScythes >= 2 ? ScytheFastSpin : 1f) * Math.Sign(m.Ring == 0f ? 1f : m.Ring);
            m.Ang += spin * dt * ScytheInner / Math.Max(ScytheInner, Math.Abs(m.Ring)) * 1.3f;  // the wide one turns a little slower, so their blades move alike
            V2 slot = owner.Pos + V2.FromAngle(m.Ang) * Math.Abs(m.Ring);
            float step = ScytheSweepSpeed * dt;
            m.Cd -= dt;
            if (!m.Sent && m.Cd <= 0f && !owner.Downed)
            {
                var prey = NearestEnemy(owner.Pos, ScytheSweepReach);
                if (prey != null)
                {
                    m.Sent = true;
                    m.Returning = false;
                    m.GoalEnemy = prey.Id;
                    m.SentT = 0f;
                }
            }
            if (m.Sent)
            {
                m.SentT += dt;
                Enemy prey;
                bool alive = Enemies.TryGetValue(m.GoalEnemy, out prey) && prey.Hp > 0f && prey.SpawnT <= 0f;
                V2 goal = !m.Returning && alive ? prey.Pos : slot;
                V2 to = goal - m.Pos;
                m.Aim = to.Angle;
                if (!m.Returning && (!alive || m.SentT > 1.2f))
                    m.Returning = true;
                else if (!m.Returning && to.Length <= prey.Radius + ScytheBlade + step)
                {
                    m.Returning = true;  // the reap lands (the cut below), then back
                    m.Pos = prey.Pos - to.Normalized() * (prey.Radius + ScytheBlade * 0.5f);
                }
                else if (m.Returning && to.Length <= step + 2f)
                {
                    m.Pos = slot;
                    m.Sent = m.Returning = false;
                    m.Cd = owner.Stats.TwinScythes >= 2 ? ScytheSweepFast : ScytheSweepEvery;
                }
                else
                    m.Pos += to.Normalized() * step;
            }
            else
            {
                m.Pos += (slot - m.Pos).LimitLength(step);
                m.Aim = m.Ang + Math.Sign(spin) * MathX.Pi / 2f;  // the blade leads
            }
            if (owner.Downed)
                return;
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f || e.Pos.DistanceTo(m.Pos) > e.Radius + ScytheBlade)
                    continue;
                long key = ((long)m.Id << 32) | (uint)e.Id;
                float last;
                if (scytheCuts.TryGetValue(key, out last) && scytheClock - last < ScytheEvery)
                    continue;
                scytheCuts[key] = scytheClock;
                HurtEnemy(e, m.Def.Damage * owner.Stats.DmgMult, owner.Id);
                BroadcastFx(FxKind.Spark, e.Pos + (m.Pos - e.Pos).Normalized() * e.Radius, m.Def.Color, 12f, 0.15f, Sfx.None);
            }
            CutSmallBullets(m.Pos, ScytheBlade);
        }

        /// Small enemy bullets (the ones a Knight's swing can cut) within reach of a point are cut out of the air.
        void CutSmallBullets(V2 at, float reach)
        {
            scytheCut.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && (b.Kind == Defs.SmallShot || b.Kind == Defs.FastShot)
                    && b.Pos.DistanceTo(at) < reach + b.Radius)
                    scytheCut.Add(b.Id);
            foreach (int id in scytheCut)
            {
                Bullets.Remove(id);
                pendingRemoved.Add(id);  // and gone on every game
            }
        }

        readonly List<int> scytheCut = new List<int>();

        /// Host, every tick: the scythes' clock, and old cut records cleared now and then.
        void TickScytheClock(float dt)
        {
            scytheClock += dt;
            if (scytheCuts.Count > 400)
            {
                var old = new List<long>();
                foreach (var kv in scytheCuts)
                    if (scytheClock - kv.Value > ScytheEvery)
                        old.Add(kv.Key);
                foreach (var k in old)
                    scytheCuts.Remove(k);
            }
        }

        // --- Final Reap --------------------------------------------------------------------

        public const float FinalReapHeal = 3f, FinalReapBossBelow = 0.4f;

        /// HurtEnemy, after the hit: a regular enemy your hit left below your reap line is reaped outright.
        void FinalReap(Player p, Enemy e)
        {
            if (e.Hp <= 0f || e.Def.IsBoss || e.Def.Xp <= 0 || e.Hp >= e.MaxHp * p.Stats.ReapBelow)
                return;
            Recorder?.DamageDealt(p.Id, e, e.Hp);
            p.Run.DamageDealt += e.Hp;
            e.Hp = 0f;
            e.LastHitBy = p.Id;
            if (!p.Downed)
                Heal(p, FinalReapHeal, p);
            BroadcastFx(FxKind.Slash, e.Pos + new V2(-e.Radius - 14f, -e.Radius - 14f), e.Pos + new V2(e.Radius + 14f, e.Radius + 14f), new Rgba(0.85f, 0.75f, 1f), 0.12f, 0.25f, Sfx.SwingLight);
        }

        // --- Scythe Throw ------------------------------------------------------------------

        public const float ThrowRange = 380f, ThrowOutTime = 0.45f, ThrowBackSpeed = 760f, ThrowLife = 3f, ThrowCatch = 0.4f, ThrowCatchMastered = 0.6f;
        public const float ThrowReach = 30f;  // the spinning blade's reach

        /// Scythe Throw, from the Twin Reapers' legendary card (Q / E / Shift only). (Upgrades.Abilities gathers it.)
        internal static readonly AbilityDef[] ReaperAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.ScytheThrow,
                Name = "Scythe Throw", Cooldown = 9f,
                Desc = "Throw a giant scythe at your cursor (up to 380 px). It cuts everything it passes, 45 on the way out and 45 on the way back, and slices small enemy bullets. Catch it as it comes back to take 40% off the cooldown.",
                Capstone = "Catching it takes 60% off, and it cuts 50% harder.",
                Bot = b => b.HaveTarget && b.TargetD < 380f,
            },
        };

        readonly Dictionary<int, HashSet<int>> throwHits = new Dictionary<int, HashSet<int>>();  // thrown scythe -> enemies it cut on this leg

        /// Cast (CastAbility): the scythe leaves you toward the cursor.
        void ThrowScythe(Player p, V2 target, float power, bool mastered)
        {
            V2 goal = ClampToArena(p.Pos + (target - p.Pos).LimitLength(ThrowRange), 20f);
            var m = SpawnMinion(MinionKind.ThrownScythe, p, p.Pos + (goal - p.Pos).LimitLength(p.Def.BodyR), ThrowLife);
            m.Goal = goal;
            m.Power = power * (mastered ? 1.5f : 1f);
            m.Mastered = mastered;
            m.Sent = true;
            m.SentT = 0f;
            m.Aim = (goal - p.Pos).Angle;
            throwHits[m.Id] = new HashSet<int>();
            Sounds.Add(new SoundCue { Sfx = Sfx.SwingHeavy, Ally = p.Id != MyId });
        }

        /// Host, each tick for a thrown scythe: out to its point, then back to its owner. True once it's caught (it's
        /// removed then); it also cuts what it passes, once per enemy per leg.
        bool MoveThrownScythe(Minion m, Player owner, float dt)
        {
            m.SentT += dt;
            m.Ang += 18f * dt;  // spinning
            if (!m.Returning)
            {
                float speed = Math.Max(400f, ThrowRange / ThrowOutTime);
                V2 to = m.Goal - m.Pos;
                if (to.Length <= speed * dt)
                {
                    m.Pos = m.Goal;
                    m.Returning = true;
                    HashSet<int> hit;
                    if (throwHits.TryGetValue(m.Id, out hit))
                        hit.Clear();  // the way back cuts everything again
                }
                else
                    m.Pos += to.Normalized() * speed * dt;
            }
            else
            {
                V2 back = owner.Pos - m.Pos;
                if (back.Length <= ThrowBackSpeed * dt + owner.Def.BodyR)
                {
                    CutCooldown(owner, AbilityId.ScytheThrow, m.Mastered ? ThrowCatchMastered : ThrowCatch);  // caught
                    BroadcastFx(FxKind.Ring, owner.Pos, m.Def.Color, 30f, 0.25f, Sfx.Clang);
                    throwHits.Remove(m.Id);
                    return true;
                }
                m.Pos += back.Normalized() * ThrowBackSpeed * dt;
            }
            HashSet<int> cut;
            if (!throwHits.TryGetValue(m.Id, out cut))
                throwHits[m.Id] = cut = new HashSet<int>();
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && !cut.Contains(e.Id) && e.Pos.DistanceTo(m.Pos) < e.Radius + ThrowReach)
                {
                    cut.Add(e.Id);
                    HurtEnemy(e, m.Def.Damage * owner.Stats.DmgMult * m.Power, owner.Id);
                    BroadcastFx(FxKind.Spark, e.Pos, m.Def.Color, e.Radius + 8f, 0.2f, Sfx.None);
                }
            CutSmallBullets(m.Pos, ThrowReach);
            return false;
        }

        /// Take a share off one of a player's ability cooldowns, on the host's copy and on that player's own game
        /// (which runs its own cooldowns).
        void CutCooldown(Player p, AbilityId id, float share)
        {
            ApplyCooldownCut(p, id, share);
            var w = Proto.Begin(Msg.CooldownCut);
            w.Write(p.Id);
            w.Write((byte)id);
            w.Write(share);
            session.Broadcast(Proto.End(w));
        }

        void ApplyCooldownCut(Player p, AbilityId id, float share)
        {
            foreach (var s in p.Slots)
                if (s.Id == id && s.Timer > 0f)
                    s.Timer *= 1f - share;
        }
    }
}
