using System;

namespace Arena
{
    /// The smart bot's parry (BotBrain.cs): it raises the shield toward a shot or a sword cut it can't step out of, timed
    /// for a perfect parry (raised a moment before the hit, after the shield has been down long enough to rearm), holds it
    /// while more keep coming from that side, and drops it when the side is clear or the shield is wearing thin.
    /// Skill sets how well it times the raise and how much danger it waits for before it gives up shooting to block.
    public partial class Game
    {
        float botParryHeld, botParryDown = 10f, botParryFace;
        public int BotParries;  // raises (tests)

        /// The parry part of each skill: Lead, how long before the hit it raises (± Wobble); Nerve, how dangerous its best
        /// step has to look (BotDanger) before it blocks instead (an Expert takes any clean perfect parry).
        static void ParrySkill(BotSkill s, out float lead, out float wobble, out float nerve)
        {
            switch (s)
            {
                case BotSkill.Casual: lead = 0.2f; wobble = 0.16f; nerve = 1.6f; return;
                case BotSkill.Expert: lead = 0.12f; wobble = 0.03f; nerve = 0f; return;
                default: lead = 0.16f; wobble = 0.07f; nerve = 0.8f; return;
            }
        }

        const float BotParryLook = 0.45f;   // how far ahead it looks for hits while the shield is up
        const float BotParryLetGo = 0.25f;  // drops it below this share of the shield left
        const float BotParryRearm = 0.5f;   // Parry.cs PerfectRearm: down this long, so the next raise is perfect

        /// Is the bot parrying this tick? Turns its aim toward what's coming while it is (or is about to be).
        bool BotParry(Player me, SkillParams sk, float dt, bool busy)
        {
            bool wasUp = botParryHeld > 0f;
            bool up = !busy && !me.Downed && me.ParryBroken <= 0f && me.StunT <= 0f && WantParry(me, sk, wasUp);
            if (up)
            {
                if (!wasUp)
                    BotParries++;
                botParryHeld += dt;
                botParryDown = 0f;
                TurnAim(me, botParryFace, sk.Turn * 1.5f * dt);
            }
            else
            {
                botParryHeld = 0f;
                botParryDown += dt;
            }
            return up;
        }

        bool WantParry(Player me, SkillParams sk, bool wasUp)
        {
            ParrySkill(BotLevel, out float lead, out float wobble, out float nerve);
            lead += (float)Math.Sin(botClock * 3.7f + me.Id * 1.3f) * wobble;  // a person's timing drifts
            float look = wasUp ? BotParryLook : lead;
            if (!FirstHit(me, look, out float when, out float from, out int hits))
                return wasUp && botParryHeld < 0.2f;  // a short hold at least: a person doesn't flick it
            if (wasUp)
            {
                if (me.ParryHp < ParryMax(me) * BotParryLetGo || botParryHeld > 0.6f && BotDanger < Math.Max(nerve, 1f))
                    return false;  // wearing thin, or past the perfect window with a step aside that would do
                botParryFace = from;
                return true;
            }
            // Raise: a hit it can't walk out of (or any hit, for an Expert with a perfect parry ready).
            bool perfect = botParryDown >= BotParryRearm;
            if (BotDanger < nerve && !(perfect && hits >= 2) && !(perfect && nerve <= 0f))
                return false;
            if (me.ParryHp < ParryMax(me) * 0.4f && !perfect)
                return false;
            botParryFace = from;
            return true;
        }

        /// The soonest hit coming at the bot within `look` s: a noticed enemy shot on course for it, or a sword cut or rake
        /// winding up with it in the cone. `from` is the way to face; `hits` counts the hits a shield facing there would stop.
        bool FirstHit(Player me, float look, out float when, out float from, out int hits)
        {
            when = 99f;
            from = me.Aim;
            hits = 0;
            float hitR = me.Def.HitR;
            V2 myVel = botMove * me.Speed;
            foreach (var b in botThreats)
            {
                if (!Bullets.ContainsKey(b.Id))
                    continue;
                V2 rel = b.Pos - me.Pos, rv = b.Vel - myVel;
                float vv = rv.LengthSq;
                if (vv < 1f)
                    continue;
                float t = -rel.Dot(rv) / vv;
                if (t < 0f || t > look)
                    continue;
                if ((rel + rv * t).Length > hitR + b.Radius + 3f)
                    continue;
                if (t < when)
                {
                    when = t;
                    from = rel.Angle;
                }
            }
            foreach (var e in Enemies.Values)
            {
                if (IsSpawning(e) || e.Hp <= 0f || e.Phase != 1)
                    continue;
                float reach, half;
                if (IsSwordsman(e.Type))
                {
                    reach = SwordReachOf(e.Type);
                    half = SwordHalfOf(e.Type);
                }
                else
                    continue;
                V2 rel = me.Pos - e.Pos;
                float off = Math.Abs(MathX.WrapPi(rel.Angle - e.Facing));
                if (rel.Length > reach + hitR + 10f || off > half + 0.2f || e.PhaseT > look + 0.05f)
                    continue;
                if (e.PhaseT < when)
                {
                    when = Math.Max(0f, e.PhaseT);
                    from = (e.Pos - me.Pos).Angle;
                }
            }
            if (when > look)
                return false;
            // How many of the hits a shield facing that way would take (a spray from one side is worth blocking).
            float arc = ParryArc(me);
            foreach (var b in botThreats)
            {
                if (!Bullets.ContainsKey(b.Id))
                    continue;
                V2 rel = b.Pos - me.Pos, rv = b.Vel - myVel;
                float vv = rv.LengthSq;
                float t = vv < 1f ? -1f : -rel.Dot(rv) / vv;
                if (t >= 0f && t <= BotParryLook && (rel + rv * t).Length <= hitR + b.Radius + 3f
                    && MathX.AngleWithin(rel.Angle, from, arc))
                    hits++;
            }
            hits = Math.Max(hits, 1);
            return true;
        }
    }
}
