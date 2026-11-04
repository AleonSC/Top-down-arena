using System;

namespace Arena
{
    /// The Elementalist tank's Aeroaegis (once Arcane Aegis): a small wind vortex around the player the caster's cursor is
    /// nearest to (the caster too). Enemies can't walk into it, they are pressed out to its rim and swept round it; bullets
    /// that touch it are still reflected (Combat.cs). Player.AegisT is replicated, so every screen draws it on the right
    /// player (Game/AeroaegisLook.cs, Docs/Mockups/aeroaegis.html, look B "Gust shell").
    public partial class Game
    {
        public const float AegisRadius = 56f;
        const float AegisSweep = 2.4f;  // radians a second an enemy slides round the rim

        /// Who the vortex goes to: the standing player nearest `cursor`, the caster if no one else is closer.
        Player AegisTarget(Player caster, V2 cursor)
        {
            Player best = caster;
            float bestD = caster.Pos.DistanceTo(cursor);
            foreach (var o in Players.Values)
            {
                if (o == caster || o.Downed)
                    continue;
                float d = o.Pos.DistanceTo(cursor);
                if (d < bestD)
                {
                    best = o;
                    bestD = d;
                }
            }
            return best;
        }

        /// Enemies can't be inside a vortex: anything that is (at the cast, or pressed in) is moved to its rim and slid round it.
        void KeepOutOfAegis(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.AegisT <= 0f || p.Downed)
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f)
                        continue;
                    float lim = AegisRadius + e.Radius;
                    V2 off = e.Pos - p.Pos;
                    float d = off.Length;
                    if (d >= lim)
                        continue;
                    float a = d > 0.01f ? (float)Math.Atan2(off.Y, off.X) : (e.Id % 7) * 0.9f;
                    a += (e.Id % 2 == 0 ? 1f : -1f) * AegisSweep * dt;
                    e.Pos = ClampToArena(p.Pos + V2.FromAngle(a) * lim, e.Radius);
                }
            }
        }
    }
}
