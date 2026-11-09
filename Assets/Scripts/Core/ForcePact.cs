using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Tank Summoner's Force Pact (replaces Guardian Spirit; Docs/Mockups/force-pact.html, Look A, Soul Chains). Cast it
    /// near a regular enemy: the one nearest your cursor is taken out of the wave and, after ForcePactBind seconds of
    /// chains, comes back as your Thrall: an enemy of that type on your side. Like the Warden's Guard (Stun.cs) it fights
    /// near you, has health that enemy shots and bodies wear down, and a fallen thrall returns GuardRespawn seconds later
    /// as the same type. Casting again on another enemy moves the pact to that type. Mastered, the pact holds two thralls
    /// (the last two types you chose). Look: Game/ForcePactLook.cs.
    public partial class Game
    {
        public const float ForcePactBind = 0.9f;
        const float ThrallLeash = 240f, ThrallRangedLeash = 320f, ThrallHpPerHp = 2.5f, ThrallDmgMult = 0.75f;

        /// Can the pact take this enemy? Regular enemies only: no bosses, boss adds or mini-bosses (their Picker group is Hidden,
        /// Late or Boss), no practice dummies.
        static bool CanPact(Enemy e) => Targetable(e) && !e.Dummy && !e.Def.IsBoss && !e.Def.Mini
            && (e.Def.Picker == PickerGroup.Melee || e.Def.Picker == PickerGroup.Ranged);

        /// How big a thrall is: its enemy type's size (a Brute is a big thrall).
        static float MinionRadius(Minion m) => m.Kind == MinionKind.Thrall && m.Pact != 255 ? Defs.Get((EnemyType)m.Pact).Radius : m.Def.Radius;

        static float ThrallMaxHp(EnemyType t, int wave) => Defs.Get(t).Hp * ThrallHpPerHp + 15f * wave;

        Minion ThrallIn(Player p, int slot)
        {
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Thrall && m.PactSlot == slot)
                    return m;
            return null;
        }

        /// The ability: bind the enemy nearest your cursor.
        void ForcePactCast(Player p, V2 target, bool mastered)
        {
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (CanPact(e) && (best == null || e.Pos.DistanceSq(target) < best.Pos.DistanceSq(target)))
                    best = e;
            if (best == null)
            {
                foreach (var sl in p.Slots)  // nothing to take: it fizzles and the cooldown is refunded
                    if (sl.Id == AbilityId.GuardianSpirit)
                    {
                        sl.Charges = Math.Min(sl.MaxCharges, sl.Charges + 1);
                        sl.Timer = 0f;
                    }
                BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.6f, 0.6f, 0.7f), 40f, 0.3f, Sfx.None);
                return;
            }
            castHits++;
            V2 at = best.Pos;
            Minion old0 = ThrallIn(p, 0), old1 = ThrallIn(p, 1);
            if (mastered)
            {
                if (p.PactSet[0])  // the last pact moves to the second slot, and the one before it is let go
                {
                    if (old1 != null)
                        Dissolve(old1, at);
                    p.PactSet[1] = true;
                    p.PactType[1] = p.PactType[0];
                    p.PactCd[1] = p.PactCd[0];
                    p.PactBinding[1] = p.PactBinding[0];
                    p.PactAt[1] = p.PactAt[0];
                    if (old0 != null)
                        old0.PactSlot = 1;
                }
            }
            else
            {
                p.PactSet[1] = false;
                if (old1 != null)
                    Dissolve(old1, at);
                if (old0 != null)
                    Dissolve(old0, at);
            }
            p.PactSet[0] = true;
            p.PactType[0] = best.Type;
            p.PactCd[0] = ForcePactBind;
            p.PactBinding[0] = true;
            p.PactAt[0] = at;
            BroadcastFx(FxKind.PactBind, p.Pos, at, best.Def.Color, (float)best.Type, ForcePactBind, Sfx.Summon);
            Enemies.Remove(best.Id);  // taken out of the wave: no kill, no reward
        }

        void Dissolve(Minion m, V2 toward)
        {
            BroadcastFx(FxKind.PactDissolve, m.Pos, toward, new Rgba(0.75f, 0.55f, 1f), (float)m.Pact, 0.9f, Sfx.None);
            Minions.Remove(m.Id);
        }

        /// Host, from TickMinions: each slot's thrall binds in, or comes back after it fell.
        void TickPact(Player p, float dt)
        {
            int slots = Mastered(p, AbilityId.GuardianSpirit) ? 2 : 1;
            float rez = -1f;
            for (int s = 0; s < 2; s++)
            {
                var m = ThrallIn(p, s);
                if (s >= slots && p.PactSet[s])
                {
                    p.PactSet[s] = false;  // the mastery is gone (a dev tool)
                    if (m != null)
                        Minions.Remove(m.Id);
                    continue;
                }
                if (!p.PactSet[s] || m != null)
                    continue;
                p.PactCd[s] -= dt;
                if (!p.PactBinding[s])
                    rez = Math.Max(rez, 1f - Math.Max(0f, p.PactCd[s]) / GuardRespawn);
                if (p.PactCd[s] > 0f)
                    continue;
                V2 home = p.Pos + V2.FromAngle(p.Aim + MathX.Pi * 0.75f + s * 1.6f) * 45f;
                V2 spot = p.PactBinding[s] ? p.PactAt[s] : home;
                var t = SpawnMinion(MinionKind.Thrall, p, spot, -1f, true);
                t.Pact = (byte)p.PactType[s];
                t.PactSlot = s;
                t.Ang = s * 1.6f;
                t.MaxHp = t.Hp = ThrallMaxHp(p.PactType[s], Wave);
                if (p.PactBinding[s])
                    BroadcastFx(FxKind.Ring, t.Pos, new Rgba(0.75f, 0.55f, 1f), 90f, 0.6f, Sfx.None);
                else
                    BroadcastFx(FxKind.PactRise, p.Pos, t.Pos, new Rgba(0.75f, 0.55f, 1f), (float)p.PactType[s], 0.5f, Sfx.Summon);
                p.PactBinding[s] = false;
            }
            p.PactRez = rez < 0f ? (byte)0 : (byte)Math.Max(1, Math.Min(255, (int)(rez * 255f)));
        }

        /// A thrall fell (HurtGuard): its chain snaps, and the slot's timer starts.
        void ThrallFallen(Minion m, Player owner)
        {
            owner.PactCd[m.PactSlot] = GuardRespawn;
            owner.PactBinding[m.PactSlot] = false;
            BroadcastFx(FxKind.PactSnap, m.Pos, owner.Pos, new Rgba(0.75f, 0.55f, 1f), (float)m.Pact, 1f, Sfx.Hit);
        }

        struct ThrallAtk { public bool Melee, Ring, Heal; public float Dmg, Interval, Range, Spread; public int Shots; }

        /// What each enemy type does for you (Docs/Mockups/force-pact.html's roster). Numbers before the 75% a thrall deals.
        static ThrallAtk ThrallAttack(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Grunt: return new ThrallAtk { Shots = 3, Spread = 0.25f, Dmg = 9f, Interval = 1.1f, Range = 380f };
                case EnemyType.Sniper: return new ThrallAtk { Shots = 1, Dmg = 25f, Interval = 0.8f, Range = 540f };
                case EnemyType.Burster: return new ThrallAtk { Ring = true, Shots = 10, Dmg = 7f, Interval = 2.2f, Range = 300f };
                case EnemyType.Spinner: return new ThrallAtk { Ring = true, Shots = 6, Dmg = 6f, Interval = 1.2f, Range = 280f };
                case EnemyType.Mortar: return new ThrallAtk { Shots = 1, Dmg = 30f, Interval = 1.8f, Range = 420f };
                case EnemyType.Medic: return new ThrallAtk { Heal = true, Dmg = 8f, Range = 200f };
                case EnemyType.Shielder: return new ThrallAtk { Melee = true, Dmg = 16f, Interval = 0.8f };
                case EnemyType.Charger: return new ThrallAtk { Melee = true, Dmg = 30f, Interval = 1f };
                case EnemyType.Splitter: return new ThrallAtk { Melee = true, Dmg = 18f, Interval = 0.7f };
                case EnemyType.Spiker: return new ThrallAtk { Melee = true, Dmg = 10f, Interval = 0.45f };
                case EnemyType.Brute: return new ThrallAtk { Melee = true, Dmg = 24f, Interval = 0.7f };
                case EnemyType.Lunger: return new ThrallAtk { Melee = true, Dmg = 18f, Interval = 0.6f };
            }
            var d = Defs.Get(t);
            return d.Melee ? new ThrallAtk { Melee = true, Dmg = Math.Max(10f, d.Contact), Interval = 0.7f }
                : new ThrallAtk { Shots = 1, Dmg = 12f, Interval = 1f, Range = 380f };
        }

        /// A thrall's turn (TickMinions): it goes for enemies near you (melee ones up close, shooters from a distance), else
        /// walks at your side; a Medic heals you and your minions instead.
        void TickThrall(Minion m, Player owner, float dt)
        {
            var type = (EnemyType)m.Pact;
            var def = Defs.Get(type);
            var atk = ThrallAttack(type);
            float speed = Math.Max(140f, Math.Min(260f, def.Speed * 2f));
            float dmg = atk.Dmg * ThrallDmgMult * owner.Stats.DmgMult * m.Power;
            float leash = atk.Melee ? ThrallLeash : ThrallRangedLeash;
            Enemy target = null;
            if (!atk.Heal)
                foreach (var e in Enemies.Values)
                    if (Targetable(e) && e.Pos.DistanceTo(owner.Pos) < leash + e.Radius
                        && (target == null || e.Pos.DistanceSq(m.Pos) < target.Pos.DistanceSq(m.Pos)))
                        target = e;
            V2 home = owner.Pos + V2.FromAngle(owner.Aim + MathX.Pi * 0.75f + m.Ang) * 45f;
            V2 goal = home;
            float stop = 4f;
            if (target != null)
            {
                float dist = target.Pos.DistanceTo(m.Pos);
                m.Aim = (target.Pos - m.Pos).Angle;
                if (atk.Melee)
                {
                    goal = target.Pos;
                    stop = target.Radius + def.Radius;
                }
                else if (dist > atk.Range * 0.7f)
                {
                    goal = target.Pos;
                    stop = atk.Range * 0.65f;
                }
                else if (dist < atk.Range * 0.3f)
                {
                    goal = m.Pos - V2.FromAngle(m.Aim) * 80f;  // keeps its distance
                    stop = 0f;
                }
                else
                    goal = m.Pos;
            }
            V2 to = goal - m.Pos;
            if (to.Length > stop)
                m.Pos += to.Normalized() * Math.Min(speed * dt, to.Length - stop);
            if (target == null && to.Length > 1f)
                m.Aim = to.Angle;
            m.Pos = ClampToArena(m.Pos, def.Radius);

            if (atk.Heal)
            {
                foreach (var q in Players.Values)
                    if (!q.Downed && q.Pos.DistanceTo(m.Pos) < atk.Range)
                        Heal(q, atk.Dmg * m.Power * dt, owner);
                return;
            }
            if (target == null || m.Cd > 0f || State != MatchState.Fighting)
                return;
            if (atk.Melee)
            {
                if (target.Pos.DistanceTo(m.Pos) < target.Radius + def.Radius + 6f)
                {
                    m.Cd = atk.Interval;
                    HurtEnemy(target, dmg, m.Owner);
                    BroadcastFx(FxKind.Slash, m.Pos, target.Pos, new Rgba(0.75f, 0.55f, 1f), 0.9f, 0.2f, Sfx.None);
                }
                return;
            }
            if (target.Pos.DistanceTo(m.Pos) > atk.Range)
                return;
            m.Cd = atk.Interval;
            if (atk.Ring)
            {
                for (int i = 0; i < atk.Shots; i++)
                {
                    V2 dir = V2.FromAngle(MathX.Tau * i / atk.Shots + AnimT);
                    SpawnBullet(Defs.MinionShot, m.Pos + dir * def.Radius, dir * 420f, 0f, atk.Range / 420f, dmg, m.Owner);
                }
                return;
            }
            for (int i = 0; i < atk.Shots; i++)
            {
                V2 dir = V2.FromAngle(m.Aim + (i - (atk.Shots - 1) * 0.5f) * atk.Spread);
                SpawnBullet(Defs.MinionShot, m.Pos + dir * def.Radius, dir * 650f, 0f, atk.Range / 650f, dmg, m.Owner);
            }
        }
    }
}
