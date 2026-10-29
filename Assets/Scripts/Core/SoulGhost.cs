using System;

namespace Arena
{
    /// Soul Link with no ally to link to: a ghost of the healer appears beside them, linked as an ally would be. It
    /// takes half damage while the link lasts and the link heals it; it fires the healer's light bolts at enemies
    /// nearby, and fades when the link ends (or sooner, if enemies bring it down).
    public partial class Game
    {
        const float GhostHpShare = 0.5f;  // of its healer's max health

        void SpawnSoulGhost(Player p, float life, bool mastered)
        {
            foreach (var old in Minions.Values)
                if (old.Owner == p.Id && old.Kind == MinionKind.SoulGhost)
                    old.T = 0.01f;  // a new link replaces the last ghost
            var g = SpawnMinion(MinionKind.SoulGhost, p, p.Pos + V2.FromAngle(p.Aim + 2f) * 60f, life);
            g.MaxHp = g.Hp = p.MaxHp * GhostHpShare;
            g.Aim = p.Aim;
            if (mastered)
                g.InvulnT = 1f;  // like an ally, it can't be hurt for the first second
            BroadcastFx(FxKind.Ring, g.Pos, g.Def.Color, 40f, 0.4f, Sfx.Summon);
        }

        /// What a hit does to a Soul Ghost: nothing while it's invulnerable, half while its link lasts.
        float GhostDamage(Minion m, float dmg)
        {
            if (m.InvulnT > 0f)
                return 0f;
            Player owner;
            return Players.TryGetValue(m.Owner, out owner) && owner.LinkT > 0f ? dmg * 0.5f : dmg;
        }

        /// The link's regeneration reaches the ghost too (TickPlayers).
        void HealSoulGhost(Player p, float heal)
        {
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.SoulGhost)
                    m.Hp = Math.Min(m.MaxHp, m.Hp + heal);
        }
    }
}
