using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A Boss Hunt played as a hunt map (Docs/HuntMap.md; the lobby's "Hunt map" switch on the Boss Hunt tab). The host rolls a
    /// map when the hunt begins; between waves everyone votes for the next node, the most votes wins (the host breaks a tie,
    /// then chance), and the wave that follows is that node's: an Elite node is a tougher wave (HuntBosses in BossSchedule.cs).
    /// Clearing the wave clears the node. The hunt still ends with the boss on wave 10, so everything else about a hunt holds.
    /// Off, a hunt is the ten fixed waves it always was. The host sends everyone the map's seed, the cleared path, the node
    /// being fought and the vote so far (Msg.HuntMapState); players send their vote and lock-in to the host (Msg.HuntMapVote).
    public partial class Game
    {
        public const float HuntMapVoteTime = 20f;   // seconds to vote once nobody is picking upgrades

        /// This hunt is played as a map (set with Hunt, by the lobby's switch).
        public bool HuntMapOn;

        /// The map: made by the host at the start (null before that, and on a client until the first message).
        public HuntMap HuntRoute;

        /// Everyone is voting on the next node (the map screen is up).
        public bool HuntMapVoting;

        /// Seconds left to vote (counted down on every machine from the number the host sent).
        public float HuntMapSeconds;

        /// Each player's vote (an index in the choices) and who has locked it in.
        public readonly Dictionary<int, int> MapVotes = new Dictionary<int, int>();
        public readonly HashSet<int> MapLocked = new HashSet<int>();

        /// The next wave waits for a node to be picked.
        public bool HuntMapBlocksWave => HuntMapOn && Hunt != null && (HuntRoute == null || (!HuntRoute.Complete && HuntRoute.Current == null));

        void ResetHuntMap()
        {
            HuntRoute = null;
            HuntMapVoting = false;
            HuntMapSeconds = 0f;
            MapVotes.Clear();
            MapLocked.Clear();
        }

        /// The node type of a wave of the hunt (Skirmish where the map doesn't say yet).
        public HuntNodeType HuntNodeAt(int wave)
        {
            if (HuntRoute == null || wave < 1 || wave > HuntMap.Steps)
                return HuntNodeType.Skirmish;
            int row = wave - 1;
            var path = HuntRoute.Path;
            if (row < path.Length)
                return HuntRoute.Rows[row][path[row]].Type;
            if (row == HuntRoute.Step && HuntRoute.Current != null)
                return HuntRoute.Current.Type;
            return HuntNodeType.Skirmish;
        }

        // ---------- host ----------

        /// Host, each tick of the break: make the map, open the vote, close it when everyone has locked in or the time is up.
        void TickHuntMap(float dt)
        {
            if (!IsHost || !HuntMapOn || Hunt == null || Practice != null)
                return;
            if (HuntRoute == null)
            {
                HuntRoute = HuntMap.Generate(rng.Next(1, 1 << 30));
                DoLog("hunt map: seed " + HuntRoute.Seed);
                BroadcastHuntMap();
            }
            if (HuntRoute.Complete || HuntRoute.Current != null || AnyonePicking())
                return;
            var choices = HuntRoute.Choices();
            if (choices.Length == 0)
                return;
            if (choices.Length == 1)   // the first node, or a single way on: nothing to vote on
            {
                HuntRoute.Pick(choices[0]);
                HuntMapVoting = false;
                BroadcastHuntMap();
                return;
            }
            if (!HuntMapVoting)
            {
                HuntMapVoting = true;
                HuntMapSeconds = HuntMapVoteTime;
                MapVotes.Clear();
                MapLocked.Clear();
                BroadcastHuntMap();
            }
            HuntMapSeconds -= dt;
            bool everyone = Players.Count > 0;
            foreach (var p in Players.Values)
                everyone &= MapLocked.Contains(p.Id);
            if (everyone || HuntMapSeconds <= 0f)
                ResolveHuntVote();
        }

        /// The node with the most votes; a tie goes to the host's vote if it is one of them, else to chance.
        void ResolveHuntVote()
        {
            var choices = HuntRoute.Choices();
            var tally = new int[choices.Length];
            foreach (var kv in MapVotes)
                if (kv.Value >= 0 && kv.Value < tally.Length)
                    tally[kv.Value]++;
            int best = 0;
            foreach (int n in tally) best = Math.Max(best, n);
            var tied = new List<int>();
            for (int i = 0; i < tally.Length; i++)
                if (tally[i] == best)
                    tied.Add(i);
            int pick;
            int hostVote;
            if (tied.Count == 1)
                pick = tied[0];
            else if (MapVotes.TryGetValue(MyId, out hostVote) && tied.Contains(hostVote))
                pick = hostVote;
            else
                pick = tied[rng.Next(tied.Count)];
            HuntRoute.Pick(choices[pick]);
            HuntMapVoting = false;
            MapVotes.Clear();
            MapLocked.Clear();
            BroadcastHuntMap();
            Sounds.Add(Sfx.Pick);
            DoLog("hunt map: " + HuntMap.Name(HuntRoute.Current.Type) + " (step " + (HuntRoute.Step + 1) + ")");
        }

        /// Host, when a wave is cleared: its node is cleared.
        void HuntMapNodeCleared()
        {
            if (!IsHost || !HuntMapOn || HuntRoute == null || HuntRoute.Current == null)
                return;
            HuntRoute.Clear();
            BroadcastHuntMap();
        }

        /// This player's vote and whether it's locked in (the host's own goes straight in).
        public void SendHuntVote(int choice, bool locked)
        {
            if (Me == null)
                return;
            if (IsHost)
            {
                HostHuntVote(MyId, choice, locked);
                return;
            }
            var w = Proto.Begin(Msg.HuntMapVote);
            w.Write((sbyte)choice);
            w.Write(locked);
            session.SendToHost(Proto.End(w));
        }

        public void OnHuntMapVote(int peer, BinaryReader r)
        {
            int choice = r.ReadSByte();
            HostHuntVote(peer, choice, r.ReadBoolean());
        }

        void HostHuntVote(int id, int choice, bool locked)
        {
            if (!IsHost || !HuntMapVoting || HuntRoute == null || !Players.ContainsKey(id) || choice >= HuntRoute.Choices().Length)
                return;
            if (choice < 0)
            {
                MapVotes.Remove(id);
                MapLocked.Remove(id);
            }
            else
            {
                MapVotes[id] = choice;
                if (locked) MapLocked.Add(id); else MapLocked.Remove(id);
            }
            BroadcastHuntMap();
        }

        void BroadcastHuntMap()
        {
            if (!IsHost || HuntRoute == null)
                return;
            var w = Proto.Begin(Msg.HuntMapState);
            w.Write(HuntRoute.Seed);
            var path = HuntRoute.Path;
            w.Write((byte)path.Length);
            foreach (int p in path)
                w.Write((byte)p);
            w.Write((sbyte)(HuntRoute.Current == null ? -1 : HuntRoute.Current.Index));
            w.Write(HuntMapVoting);
            w.Write(HuntMapSeconds);
            w.Write((byte)MapVotes.Count);
            foreach (var kv in MapVotes)
            {
                w.Write(kv.Key);
                w.Write((sbyte)kv.Value);
                w.Write(MapLocked.Contains(kv.Key));
            }
            session.Broadcast(Proto.End(w));
        }

        // ---------- everyone ----------

        void ReadHuntMapState(BinaryReader r)
        {
            int seed = r.ReadInt32();
            var path = new int[r.ReadByte()];
            for (int i = 0; i < path.Length; i++)
                path[i] = r.ReadByte();
            int current = r.ReadSByte();
            bool voting = r.ReadBoolean();
            float seconds = r.ReadSingle();
            int n = r.ReadByte();
            var votes = new Dictionary<int, int>();
            var locked = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32(), v = r.ReadSByte();
                votes[id] = v;
                if (r.ReadBoolean()) locked.Add(id);
            }
            if (IsHost)
                return;
            var map = HuntMap.Restore(seed, path);
            if (map == null || (current >= 0 && !map.Pick(current)))
                return;   // (a map that doesn't fit what we were told: wait for the next message)
            HuntRoute = map;
            HuntMapVoting = voting;
            HuntMapSeconds = seconds;
            MapVotes.Clear();
            MapLocked.Clear();
            foreach (var kv in votes) MapVotes[kv.Key] = kv.Value;
            foreach (int id in locked) MapLocked.Add(id);
        }

        /// Every machine, each frame: the timer runs down for the screen, and a bot player votes at random.
        void TickHuntMapView(float dt)
        {
            if (!HuntMapOn || !HuntMapVoting)
                return;
            if (!IsHost)
                HuntMapSeconds = Math.Max(0f, HuntMapSeconds - dt);
            if (Bot && Me != null && HuntRoute != null && !MapLocked.Contains(MyId))
            {
                int n = HuntRoute.Choices().Length;
                if (n > 0)
                    SendHuntVote(rng.Next(n), true);
            }
        }
    }
}
