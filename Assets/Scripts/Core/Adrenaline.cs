using System;

namespace Arena
{
    public partial class Game
    {
        /// The Healer Soldier's Adrenaline Shot: the living player closest to the cursor (you, if alone) gets AdrenalineTime
        /// seconds of faster movement (Player.Speed), faster fire (FireRate) and less damage taken (Combat.cs).
        /// Mastered, the nearest other ally within AdrenalineSplashRange gets a short dose too.
        void Adrenaline(Player p, V2 cursor, bool cap, float pw)
        {
            Player to = p;
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.DistanceSq(cursor) < to.Pos.DistanceSq(cursor))
                    to = q;
            float time = (AdrenalineTime + p.Stats.AdrenalineBonusTime) * pw;
            Inject(to, time, p);
            if (!cap)
                return;
            Player other = null;
            foreach (var q in Players.Values)
                if (q != to && !q.Downed && q.Pos.DistanceTo(to.Pos) <= AdrenalineSplashRange && (other == null || q.Pos.DistanceSq(to.Pos) < other.Pos.DistanceSq(to.Pos)))
                    other = q;
            if (other != null)
                Inject(other, time * AdrenalineSplashTime, p);
        }

        void Inject(Player to, float time, Player by)
        {
            to.AdrenalineT = Math.Max(to.AdrenalineT, time);
            if (to != by)
                castHits++;
            BroadcastFx(FxKind.Ring, to.Pos, new Rgba(1f, 0.31f, 0.35f), 60f, 0.5f, Sfx.Ability);
        }
    }
}
