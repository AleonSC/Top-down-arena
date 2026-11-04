using System;

namespace Arena
{
    /// The Tank Soldier's Fortress ultimate: a fixed dome that only players and player bullets can cross (host).
    /// It's set where the ultimate is cast and doesn't move. Enemies (bosses too) are held outside it, enemy bullets are
    /// erased the moment they touch it, and nothing hostile can hurt a player standing inside. Player.DomeT / DomePos are
    /// replicated so every screen draws it (ArenaApp DrawFortressDome, Docs/Mockups/fortress-dome.html, look B).
    public partial class Game
    {
        public const float DomeRadius = 240f;
        public const float DomeTime = 10f, DomeTimeMastered = 14f;

        public bool AnyDome()
        {
            foreach (var p in Players.Values)
                if (p.DomeT > 0f)
                    return true;
            return false;
        }

        /// Is this point inside a standing dome? `pad` grows (+) or shrinks (-) it.
        public bool InDome(V2 pos, float pad = 0f)
        {
            foreach (var p in Players.Values)
                if (p.DomeT > 0f && p.DomePos.DistanceTo(pos) < DomeRadius + pad)
                    return true;
            return false;
        }

        /// Enemies can't be inside a dome: anything that is (at the cast, or pressed in by a push) is moved to its edge.
        void KeepOutOfDomes()
        {
            foreach (var p in Players.Values)
            {
                if (p.DomeT <= 0f)
                    continue;
                foreach (var e in Enemies.Values)
                {
                    float lim = DomeRadius + e.Radius + 16f;  // clear of the wall, so nothing can touch a player at the edge
                    V2 off = e.Pos - p.DomePos;
                    float d = off.Length;
                    if (d >= lim)
                        continue;
                    V2 dir = d > 0.01f ? off * (1f / d) : new V2(1f, 0f);
                    e.Pos = ClampToArena(p.DomePos + dir * lim, e.Radius);
                }
            }
        }
    }
}
