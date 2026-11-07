using System;

namespace Arena
{
    /// Titan (Tank Summoner ultimate; Docs/Mockups/titan-colossus.html, look C): a colossus of stone, a body 140 px wide.
    /// Rubble gathers into it over TitanRise (it blocks nothing and hits nothing yet), it stomps after the nearest enemy,
    /// raises its fists for TitanWindup before each slam (so players can see the hit coming), and falls apart for the last
    /// TitanCrumble. Its whole body stops enemy bullets and soaks 1000 boss damage (Minions.cs BlockedBySummons).
    /// Clients draw it from the minion's age (rising), T (crumbling), Aim (facing) and Wind (the slam); see Game/TitanLook.cs.
    public partial class Game
    {
        public const float TitanTime = 17f, TitanCapTime = 27f, TitanRise = 1.4f, TitanCrumble = 1.2f;
        public const float TitanWindup = 0.4f, TitanFollow = 0.25f, TitanKnock = 60f, TitanAhead = 100f, TitanTurn = 2.2f, TitanSpawnAhead = 120f;
        public const float TitanCrackTime = 4.5f, TitanRubbleTime = 3.5f;

        /// Standing and blocking: not while it is still rising or already crumbling.
        public static bool TitanBlocking(Minion m) => m.Duration - m.T >= TitanRise && m.T > TitanCrumble;

        /// The Golem Staff's Golem is a small Titan (same body, rise, slam and soak at GolemScale of the size). It is summoned with a
        /// very long life so its age still counts up for the rise; it never crumbles.
        public const float GolemScale = 0.32f, GolemLife = 100000f;
        public static bool IsTitanLike(MinionKind k) => k == MinionKind.Titan || k == MinionKind.Golem;

        void CastTitan(Player p, bool cap)
        {
            foreach (var old in Minions.Values)
                if (old.Owner == p.Id && old.Kind == MinionKind.Titan)
                    old.T = 0.01f;  // a new titan replaces the last
            V2 at = p.Pos + V2.FromAngle(p.Aim) * TitanSpawnAhead;
            var titan = SpawnMinion(MinionKind.Titan, p, at, cap ? TitanCapTime : TitanTime);
            titan.Aim = p.Aim;
            titan.SlamT = -1f;
            if (cap)
            {
                titan.Scale = 1.3f;
                titan.BlockHp *= 1.3f;
            }
            Sounds.Add(Sfx.Summon);
        }

        /// A shockwave at `at`: knocks enemies back and, for a slam, hurts them.
        void TitanQuake(Minion m, V2 at, float reach, float dmg, float kind)
        {
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f || e.Pos.DistanceTo(at) >= reach + e.Radius)
                    continue;
                V2 away = e.Pos - at;
                if (!Heavy(e))
                    KnockEnemy(e, (away.Length < 1f ? V2.FromAngle(m.Aim) : away.Normalized()) * (kind == 0f ? TitanKnock : TitanKnock * 0.7f));  // slides (Knock.cs)
                if (dmg > 0f)
                    HurtEnemy(e, dmg, m.Owner);
            }
            BroadcastFx(FxKind.TitanSlam, at, new V2(kind, 0f), m.Def.Color, reach, TitanCrackTime, kind == 0f ? Sfx.Shockwave : Sfx.BigExplode);
        }

        /// The last thing a titan does: its rubble (the look keeps the pile for a few seconds).
        void TitanFallen(Minion m)
        {
            if (!m.Stood)
                return;
            BroadcastFx(FxKind.TitanSlam, m.Pos, new V2(2f, 0f), m.Def.Color, m.Def.Radius * m.Scale, TitanRubbleTime, Sfx.Explode);
        }

        /// Host, each tick (m.Cd has already counted down).
        void TickTitan(Minion m, Player owner, float dt, float speed, float dmg, float reach)
        {
            var d = m.Def;
            float age = m.Duration - m.T;
            if (!m.Stood && age >= TitanRise)
            {
                m.Stood = true;
                m.Cd = 0.4f;
                TitanQuake(m, m.Pos, reach * 1.15f, 0f, 1f);  // stands up: a thud that shoves small enemies back
            }
            if (age < TitanRise || m.T <= TitanCrumble || State != MatchState.Fighting)
            {
                m.Wind = 0f;
                m.SlamT = -1f;
                return;
            }
            Enemy target = NearestEnemy(m.Pos, MinionChaseRange);
            V2 goal = target != null ? target.Pos : owner.Pos + V2.FromAngle(owner.Aim) * (m.Kind == MinionKind.Golem ? 70f : 140f);
            V2 to = goal - m.Pos;
            if (to.Length > 8f)
            {
                float da = to.Angle - m.Aim;
                while (da > MathX.Pi) da -= MathX.Tau;
                while (da < -MathX.Pi) da += MathX.Tau;
                m.Aim += Math.Max(-TitanTurn * dt, Math.Min(TitanTurn * dt, da));
            }
            if (m.SlamT >= 0f)  // winding up, then following through; it stands still
            {
                m.SlamT += dt;
                m.Wind = MathX.Clamp01(m.SlamT / (TitanWindup + TitanFollow));
                if (!m.SlamHit && m.SlamT >= TitanWindup)
                {
                    m.SlamHit = true;
                    TitanQuake(m, m.Pos + V2.FromAngle(m.Aim) * TitanAhead * m.Scale, reach, dmg, 0f);
                }
                if (m.SlamT >= TitanWindup + TitanFollow)
                {
                    m.SlamT = -1f;
                    m.SlamHit = false;
                    m.Wind = 0f;
                    m.Cd = d.Interval;
                }
                return;
            }
            m.Wind = 0f;
            V2 front = m.Pos + V2.FromAngle(m.Aim) * TitanAhead * m.Scale;
            if (target != null && m.Cd <= 0f && target.Pos.DistanceTo(front) < reach + target.Radius * 0.5f)
            {
                m.SlamT = 0f;
                m.SlamHit = false;
            }
            else if (to.Length > (target != null ? reach * 0.6f : 60f))
            {
                m.Pos += V2.FromAngle(m.Aim) * Math.Min(speed * dt, to.Length);
                m.Pos = ClampToArena(m.Pos, 60f);
            }
        }
    }
}
