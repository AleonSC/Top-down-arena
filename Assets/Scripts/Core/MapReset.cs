using System;
using System.Collections.Generic;

namespace Arena
{
    /// Map reset (Docs/MapReset.md step 1): in the Campaign the Expanding map goes back to the bare hub after every boss, so
    /// a run plays on ten maps instead of piling 100 waves of rooms, nodes, doors and breaches onto one. Room kits are kept
    /// and each player gets half the vectors they spent on the old map back (on top of the boss refill).
    public partial class Game
    {
        /// The share of a map's spent vectors each player gets back when it resets.
        public const float MapRefundShare = 0.5f;

        /// Which map of the run this is (1 at the start, +1 per reset; synced in Msg.ExpandState, saved).
        public int MapNumber = 1;

        /// Every game, for the look: seconds since the map last reset (99: not this run).
        public float MapResetAge = 99f;

        /// Host: the vectors the team got back at the last reset, all players together (for the log and the tests).
        public int MapRefund;

        /// This mode resets its map after each boss (the Campaign on the Expanding arena).
        public bool MapResetOn => ExpandingOn && ModeRules.MapResets && ModeRules.RunEvents;

        /// What a player gets back for spending this many vectors on the map.
        public static int RefundFor(int spent) => Math.Max(0, (int)(spent * MapRefundShare));

        /// Host, as a boss wave is cleared (WaveCleared in Waves.cs, after the boss refill): everything built on the map goes
        /// and the team stands in the bare hub. Kits stay; each player gets RefundFor(MapSpent) on top of their vectors (above
        /// the cap, like the boss bonus). Coins still lying in the old rooms go to the nearest player. Not after the run's last boss.
        void ResetMap()
        {
            if (!IsHost || !MapResetOn || (Wave >= LastWave && !Endless))
                return;
            int rooms = Expansion.BoughtCount(Expanded);
            CollectStrayCoins();
            MapRefund = 0;
            foreach (var p in Players.Values)
            {
                int back = RefundFor(p.MapSpent);
                p.Vectors = Math.Min(short.MaxValue, p.Vectors + back);
                MapRefund += back;
                p.MapSpent = 0;
            }
            Expanded.Clear();
            Openings.Clear();
            Links.Clear();
            Nodes.Clear();
            Placed.Clear();
            duelBoss.Clear();
            ResetGates();
            ResetMysteryDoors();
            EventMark = null;
            ExpandOpen = false;
            ExpandProposals.Clear();
            ExpandVotes.Clear();
            botProposed = false;
            expWorld = null;
            LayoutAge = 0f;
            expandSeed = sideRng.Next(1, 1 << 30);  // a new map rolls new breaches, nodes and doors (sideRng: ResetExpansion)
            if (RoomsUp)
            {
                RoomsUp = false;
                PullIntoHub();
            }
            MovePlayersIntoHub();
            MapNumber++;
            MapResetAge = 0f;
            Sounds.Add(Sfx.Pick);
            DoLog("map reset: map " + MapNumber + ", " + rooms + " rooms gone, " + MapRefund + " vectors back, " + RoomTokens.Count + " kits kept");
            BroadcastExpansion();
        }

        /// Host: coins and vector pickups outside the hub go straight to the nearest living player (their rooms are going).
        void CollectStrayCoins()
        {
            var gone = new List<int>();
            foreach (var c in Coins.Values)
            {
                if (InHub(c.Pos, 0f))
                    continue;
                Player nearest = null;
                foreach (var p in Players.Values)
                    if (!p.Downed && (nearest == null || p.Pos.DistanceSq(c.Pos) < nearest.Pos.DistanceSq(c.Pos)))
                        nearest = p;
                if (nearest == null)
                    continue;
                if (c.Vec)
                    AddVectors(nearest, c.Value);
                else
                    nearest.Gold += c.Value;
                gone.Add(c.Id);
            }
            foreach (int id in gone)
                Coins.Remove(id);
        }

        /// Host: anyone standing outside the hub (the boss wave keeps the rooms down, so normally nobody) steps back inside it.
        void MovePlayersIntoHub()
        {
            const float margin = 90f;
            foreach (var p in Players.Values)
                if (!InHub(p.Pos, -1f))
                    MovePlayer(p, PushOutOfProps(p.Pos.Clamp(new V2(margin, margin), new V2(Width - margin, Height - margin)), p.Def.BodyR));
        }

        static bool InHub(V2 pos, float pad) => pos.X >= -pad && pos.Y >= -pad && pos.X <= Width + pad && pos.Y <= Height + pad;

        /// Every game but the host's: the map number from Msg.ExpandState; a higher one is a reset (the look's clock starts).
        void ReadMapNumber(int n)
        {
            if (n > MapNumber)
                MapResetAge = 0f;
            MapNumber = Math.Max(1, n);
        }
    }
}
