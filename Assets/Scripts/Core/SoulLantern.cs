using System;

namespace Arena
{
    /// Soul Lantern (Healer Summoner): a lantern lands beside the player nearest your cursor (you, when you're alone)
    /// and links to them. It hovers by them for 6 s, heals them a little every moment, and spits teal flames at the
    /// nearest enemy within reach. Mastered: 9 s, and the linked player can't go down (InLantern, Minions.cs).
    public partial class Game
    {
        const float LanternTime = 6f, LanternMasteredTime = 9f;
        const float LanternFlameRange = 260f, LanternFlameDamage = 7f, LanternFlameSpeed = 520f;
        static readonly V2 LanternOffset = new V2(-32f, -40f);

        void SpawnSoulLantern(Player p, V2 target, bool mastered, float power)
        {
            // The player nearest the cursor, you included (downed allies count).
            Player linked = p;
            foreach (var q in Players.Values)
                if (q.Pos.DistanceSq(target) < linked.Pos.DistanceSq(target))
                    linked = q;
            foreach (var old in Minions.Values)
                if (old.Owner == p.Id && old.Kind == MinionKind.SoulLantern)
                    old.T = 0.01f;  // a new lantern replaces the last
            var m = SpawnMinion(MinionKind.SoulLantern, p, linked.Pos + LanternOffset, (mastered ? LanternMasteredTime : LanternTime) * power);
            m.LinkTo = linked.Id;
            BroadcastFx(FxKind.Ring, m.Pos, m.Def.Color, 60f, 0.5f, Sfx.Summon);
        }

        /// Host, each tick. Returns false once the player it was linked to has gone.
        bool TickSoulLantern(Minion m, Player owner, float dt)
        {
            Player linked;
            if (!Players.TryGetValue(m.LinkTo, out linked))
                return false;
            V2 goal = linked.Pos + LanternOffset;
            m.Pos += (goal - m.Pos).LimitLength(450f * dt);
            // The link: a steady trickle of health (Kindred Spirits doubles it).
            if (!linked.Downed)
                Heal(linked, LinkHeal * owner.Stats.LinkHealMult * m.Power * dt, owner);
            var e = NearestEnemy(m.Pos, LanternFlameRange);
            if (e == null)
                return true;
            m.Aim = (e.Pos - m.Pos).Angle;
            if (m.Cd <= 0f && Battle)
            {
                m.Cd = 0.6f;
                V2 dir = V2.FromAngle(m.Aim);
                SpawnBullet(Defs.WispShot, m.Pos + dir * 12f, dir * LanternFlameSpeed, 0f, LanternFlameRange / LanternFlameSpeed,
                    LanternFlameDamage * owner.Stats.DmgMult * m.Power, m.Owner);
            }
            return true;
        }
    }
}
