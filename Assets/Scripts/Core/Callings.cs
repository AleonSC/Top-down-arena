using System;
using System.Collections.Generic;

namespace Arena
{
    /// A Calling (Docs/Callings.md): an upgrade card that commits the run to one path. Taking one takes the other
    /// Callings out of the pool and puts its own cards in. The Summoner's three each bring a new minion for the run.
    public enum CallingId : byte { None, Spider, Fairy, Demon }

    public partial class Game
    {
        // --- Numbers (first guesses, Docs/Callings.md) ---------------------------------------------------------------

        public const int CallingGuaranteeLevel = 5;  // a Summoner with no Calling gets one in this level's offer

        public const float SpiderHpBase = 120f, SpiderHpPerWave = 12f, SpiderRespawn = 8f, SpiderLeash = 260f;
        public const float SpiderWebTime = 1.5f, SpiderWebMult = 0.6f;     // a bite webs: 40% slower for 1.5 s
        public const float SpiderLeap = 130f, SpiderLeapSpeed = 720f;      // it leaps the last stretch onto its prey
        public const float QueenEvery = 6f, QueenLife = 4f, QueenRange = 300f;
        public const int QueenBrood = 3;                                   // Spider Queen: 3 spiderlings every 6 s
        public const float SpiderlingScale = 0.5f;

        public const float FairyHeal = 4f, FairyReach = 120f, FairyOrbit = 26f, FairyLow = 0.3f;
        public const float FairyDustEvery = 0.5f, FairyDustReach = 90f;
        public const float FairyRingEvery = 45f, FairyRingHealth = 0.4f;

        public const float DemonRange = 460f, DemonShotSpeed = 520f, DemonSplash = 46f;
        public const float FeastReach = 300f, FeastFade = 6f;
        public const float HellfireRadius = 40f, HellfireLife = 2f;
        public const float PactEvery = 12f, PactTime = 4f;

        // --- Cards -----------------------------------------------------------------------------------------------------

        /// The minion a Calling brings (None: no minion).
        public static MinionKind CallingMinion(CallingId c) =>
            c == CallingId.Spider ? MinionKind.Spider : c == CallingId.Fairy ? MinionKind.Fairy : c == CallingId.Demon ? MinionKind.Demon : MinionKind.None;

        /// The role a Calling suits: its card is twice as likely for that role.
        public static Role CallingRole(CallingId c) => c == CallingId.Spider ? Role.Tank : c == CallingId.Fairy ? Role.Healer : Role.Dps;

        public static bool IsCalling(MinionKind k) => k == MinionKind.Spider || k == MinionKind.Fairy || k == MinionKind.Demon;

        /// The Calling minion itself (not a spiderling).
        static bool CallingCompanion(Minion m) => m.Companion && IsCalling(m.Kind);

        float SpiderHp(Player p) => (SpiderHpBase + SpiderHpPerWave * Math.Max(1, Wave)) * p.Stats.SpiderHpMult * p.Stats.MinionToughMult;

        // --- Upkeep (host, from TickMinions) -----------------------------------------------------------------------------

        /// Keep the player's Calling minion up: it comes when the Calling is taken, a fallen spider comes back after
        /// SpiderRespawn s, and the capstones' timers run.
        void TickCallings(Player p, float dt)
        {
            var want = CallingMinion((CallingId)p.Stats.Calling);
            Minion have = null, stray = null;
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && CallingCompanion(m))
                {
                    if (m.Kind == want && have == null)
                        have = m;
                    else
                        stray = m;
                }
            if (stray != null)
                Minions.Remove(stray.Id);  // a dev tool changed the Calling
            if (p.CallingT > 0f)
                p.CallingT -= dt;
            if (want == MinionKind.None)
                return;
            if (have != null)
            {
                if (want == MinionKind.Spider)
                {
                    float max = SpiderHp(p);
                    if (Math.Abs(max - have.MaxHp) > 0.5f)
                    {
                        have.Hp = have.Hp / Math.Max(1f, have.MaxHp) * max;  // a new wave or card: the same share of a bigger spider
                        have.MaxHp = max;
                    }
                    if (p.Stats.SpiderQueen && p.CallingT <= 0f && Battle)
                        LayBrood(have, p);
                }
                return;
            }
            if (p.CallingCd > 0f)
            {
                p.CallingCd -= dt;
                return;
            }
            var c = SpawnMinion(want, p, p.Pos + RandDir() * 30f, -1f, true);
            if (want == MinionKind.Spider)
                c.MaxHp = c.Hp = SpiderHp(p);
            BroadcastFx(FxKind.Ring, c.Pos, c.Def.Color, 34f, 0.4f, Sfx.Summon);
        }

        /// A spider fell (HurtGuard): it comes back after SpiderRespawn s (Thick Carapace: sooner).
        void SpiderFallen(Minion m, Player owner)
        {
            if (m.Companion)
                owner.CallingCd = Math.Max(1f, SpiderRespawn - owner.Stats.SpiderRespawnCut);
        }

        /// One Calling minion's turn (TickMinions): `dmg` is its hit with every multiplier, its Cd already run down.
        void TickCalling(Minion m, Player owner, float dt, float speed, float dmg)
        {
            switch (m.Kind)
            {
                case MinionKind.Spider: TickSpider(m, owner, dt, speed, dmg); break;
                case MinionKind.Fairy: TickFairy(m, owner, dt, speed); break;
                case MinionKind.Demon: TickDemon(m, owner, dt, speed, dmg); break;
            }
            m.Pos = ClampToArena(m.Pos, m.Def.Radius);
        }

        // --- Spider ---------------------------------------------------------------------------------------------------

        void TickSpider(Minion m, Player owner, float dt, float speed, float dmg)
        {
            var d = m.Def;
            bool ling = !m.Companion;  // a Spider Queen spiderling: leaps on one enemy, bites once and is gone
            Enemy target = ling ? NearestEnemy(m.Pos, QueenRange, by: owner.Id) : NearestInLeash(m.Pos, owner.Pos, SpiderLeash, by: owner.Id);
            V2 goal = target != null ? target.Pos : ling ? m.Pos : BesideOwner(owner, 40f, m.Ang);
            V2 to = goal - m.Pos;
            float stop = target != null ? target.Radius + d.Radius * m.Scale : 4f;
            float pace = target != null && to.Length < SpiderLeap + stop ? SpiderLeapSpeed : speed;  // the leap
            m.Wind = pace > speed ? 1f : Math.Max(0f, m.Wind - dt * 4f);  // (replicated: the look draws its legs tucked in a leap)
            if (to.Length > stop)
                m.Pos += to.Normalized() * Math.Min(pace * dt, to.Length - stop);
            if (to.Length > 1f)
                m.Aim = to.Angle;
            if (target != null && m.Cd <= 0f && Battle && target.Pos.CloserThan(m.Pos, target.Radius + d.Radius * m.Scale + 6f))
            {
                m.Cd = d.Interval;
                HurtEnemy(target, dmg, m.Owner);
                Slow(target, SpiderWebTime + owner.Stats.WebBonusTime, Math.Max(0.2f, SpiderWebMult - owner.Stats.WebBonusSlow));
                BroadcastFx(FxKind.SpiderWeb, target.Pos, d.Color, target.Radius + 10f, 0.45f, Sfx.None);
                if (ling)
                {
                    m.T = 0.001f;  // spent: it goes at the next tick
                    return;
                }
            }
        }

        /// Spider Queen (from TickCallings, outside the minion loop): the spider lays QueenBrood spiderlings.
        void LayBrood(Minion m, Player owner)
        {
            owner.CallingT = QueenEvery;
            for (int i = 0; i < QueenBrood; i++)
            {
                var s = SpawnMinion(MinionKind.Spider, owner, m.Pos + V2.FromAngle(MathX.Tau * i / QueenBrood) * 12f, QueenLife);
                s.Scale = SpiderlingScale;
                s.Power = m.Power;
                s.Cd = 0.4f;  // hatching: a moment before it can bite
            }
            BroadcastFx(FxKind.Ring, m.Pos, m.Def.Color, 26f, 0.3f, Sfx.Summon);
        }

        // --- Fairy ----------------------------------------------------------------------------------------------------

        void TickFairy(Minion m, Player owner, float dt, float speed)
        {
            // Fairy Ring: a downed ally comes first, while it's ready.
            Player down = null;
            if (owner.Stats.FairyRing && owner.CallingT <= 0f)
                foreach (var q in Players.Values)
                    if (q.Downed && (down == null || q.Pos.DistanceTo(m.Pos) < down.Pos.DistanceTo(m.Pos)))
                        down = q;
            if (down != null)
            {
                m.Pos += (down.Pos - m.Pos).LimitLength(speed * 1.4f * dt);
                m.LinkTo = down.Id;
                if (m.Pos.CloserThan(down.Pos, 24f))
                {
                    RevivePlayer(down, FairyRingHealth, owner);
                    owner.CallingT = FairyRingEvery;
                    BroadcastFx(FxKind.Ring, down.Pos, m.Def.Color, 70f, 0.6f, Sfx.Revive);
                }
                return;
            }
            Player who = null;
            foreach (var q in Players.Values)
                if (!q.Downed && q.Hp < q.MaxHp && MoreHurt(q, who))
                    who = q;
            var tend = who ?? owner;
            m.LinkTo = tend.Id;
            m.Ang += 3f * dt;
            m.Pos += (tend.Pos + V2.FromAngle(m.Ang) * FairyOrbit - m.Pos).LimitLength(speed * dt);
            bool healing = !tend.Downed && tend.Hp < tend.MaxHp && tend.Pos.CloserThan(m.Pos, FairyReach) && Battle;
            m.Wind = healing ? Math.Min(1f, m.Wind + dt * 3f) : Math.Max(0f, m.Wind - dt * 2f);  // (replicated: its glow)
            if (healing)
            {
                float mult = owner.Stats.FairyHealMult * (tend.Hp < tend.MaxHp * FairyLow ? owner.Stats.FairyLowMult : 1f) * m.Power;
                Heal(tend, FairyHeal * mult * dt, owner);
            }
            if (owner.Stats.FairyDust > 0f && m.Cd <= 0f && Battle)
            {
                m.Cd = FairyDustEvery;
                foreach (var e in FoesInCircle(tend.Pos, FairyDustReach, by: owner.Id))
                    HurtEnemy(e, owner.Stats.FairyDust * FairyDustEvery * owner.Stats.DmgMult * m.Power, owner.Id);
            }
        }

        // --- Demon ----------------------------------------------------------------------------------------------------

        void TickDemon(Minion m, Player owner, float dt, float speed, float dmg)
        {
            V2 perch = BesideOwner(owner, 44f) + new V2(0f, -6f * (float)Math.Sin(AnimT * 2.4f + m.Id));
            m.Pos += (perch - m.Pos).LimitLength(speed * dt);
            if (m.FeastT > 0f && (m.FeastT -= dt) <= 0f)
                m.Feast = 0f;  // the hunger fades
            bool pact = owner.Stats.InfernalPact;
            if (pact && owner.CallingT <= 0f && Battle)
                owner.CallingT = PactEvery;
            bool berserk = pact && owner.CallingT > PactEvery - PactTime;
            m.Wind = berserk ? 1f : Math.Max(0f, m.Wind - dt * 3f);  // (replicated: the berserk blaze)
            if (berserk)
                m.Cd -= dt;  // twice as fast (TickMinions already ran it down once)
            var e = NearestEnemy(m.Pos, DemonRange, by: owner.Id);
            if (e == null)
                return;
            m.Aim = (e.Pos - m.Pos).Angle;
            if (m.Cd > 0f || !Battle)
                return;
            m.Cd = m.Def.Interval;
            V2 dir = V2.FromAngle(m.Aim);
            var b = SpawnBullet(Defs.FireShot, m.Pos + dir * m.Def.Radius, dir * DemonShotSpeed, 0f, DemonRange / DemonShotSpeed, dmg * (1f + m.Feast), m.Owner);
            b.Splash = DemonSplash * (berserk ? 1.5f : 1f);
            b.Hellfire = owner.Stats.Hellfire;
        }

        /// A Hellfire fireball burst (the splash loop in Combat.cs): the ground burns where it landed.
        void HellfireGround(Bullet b)
        {
            Player p;
            if (b.Hellfire <= 0 || !Players.TryGetValue(b.Owner, out p))
                return;
            var burn = AddZone(ZoneKind.FlameWall, p, b.Pos, HellfireRadius + 12f * (b.Hellfire - 1), HellfireLife);
            burn.BlockHp = 0f;  // (burning ground, not a wall: shots pass)
        }

        /// Soul Feast (the Summoner's OnKill): a kill near your demon feeds it.
        internal void CallingKill(Enemy e)
        {
            foreach (var m in Minions.Values)
            {
                if (m.Kind != MinionKind.Demon || !m.Companion || !e.Pos.CloserThan(m.Pos, FeastReach))
                    continue;
                Player owner;
                if (!Players.TryGetValue(m.Owner, out owner) || owner.Stats.FeastPer <= 0f)
                    continue;
                m.Feast = Math.Min(0.4f, m.Feast + owner.Stats.FeastPer);
                m.FeastT = FeastFade;
            }
        }
    }
}
