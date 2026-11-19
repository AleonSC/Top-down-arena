using System;

namespace Arena
{
    /// Enemy recycling: a regular enemy left far behind on a big map (the Expanding arena, Survival's room) comes back in
    /// at a fresh spawn spot near the party, so stragglers keep up the pressure instead of wandering across the map.
    public static class Recycling
    {
        public const float Far = 1800f;   // farther than this from every player standing...
        public const float After = 4f;    // ...for this long (s) and it is recycled
        public const float Respawn = 0.6f;  // its spawn-in at the new spot (s): the usual spawn look, no hurting or shooting
    }

    public partial class Game
    {
        /// Host: how many enemies have been recycled this run (tests).
        public int Recycled;

        /// Host, after the enemies move: count each regular enemy's time far from every player; past Recycling.After,
        /// move it to a new spawn spot (the same one a new enemy would get) if that is nearer the party.
        void TickRecycling(float dt)
        {
            bool survival = SurvivalOn;
            if (!survival && !RoomsOn)
                return;  // the open arena and the layouts fit the screen: nothing is ever left behind
            foreach (var e in Enemies.Values)
            {
                float near = NearestStanding(e.Pos);
                if (!Recyclable(e) || near <= Recycling.Far || near == float.MaxValue)
                {
                    e.FarT = 0f;
                    continue;
                }
                if ((e.FarT += dt) < Recycling.After)
                    continue;
                e.FarT = 0f;
                V2 to = survival ? SurvivalSpot(e.Radius) : RoomsRandomSpot(e.Radius);
                if (NearestStanding(to) >= near)
                    continue;  // (no better spot on this map: try again later)
                e.Pos = e.Disp = e.RoomAt = to;
                e.HasRoomAt = true;
                e.KnockLeft = V2.Zero;
                e.RetargetT = 0f;
                e.SpawnT = Recycling.Respawn;
                Recycled++;
            }
        }

        /// Not bosses, mini-bosses, things that don't walk (portals, nests), a duel room's own, a bound pair, dummies, or
        /// one held in place (frozen, caged, grasped) or still spawning in.
        static bool Recyclable(Enemy e) =>
            !e.Def.IsBoss && !e.Def.Mini && e.Def.Speed > 0f && e.Type != EnemyType.SpawnPortal && e.Home < 0
            && e.LinkId == 0 && e.Hp > 0f && e.SpawnT <= 0f && e.FrozenT <= 0f && e.CageT <= 0f && e.GraspT <= 0f;

        /// How far the nearest standing player is (MaxValue with nobody up: nothing recycles toward a wiped party).
        float NearestStanding(V2 at)
        {
            float d = float.MaxValue;
            foreach (var p in Players.Values)
                if (!p.Downed)
                    d = Math.Min(d, p.Pos.DistanceTo(at));
            return d;
        }
    }
}
