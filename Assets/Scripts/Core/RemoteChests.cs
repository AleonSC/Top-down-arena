using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// An unopened chest the wave box's Chests tab lists (Docs/RoundFlow.md step 5): a treasure chest prop (Id: its prop id).
    public struct ChestSpot
    {
        public int Id;
        public ChestKind Kind;
        public V2 Pos;
    }

    /// Remote chests (Docs/RoundFlow.md step 5): between waves anyone can open a chest from the wave box's Chests tab for a fee
    /// (ChestCollectFee, paid by whoever presses Collect) instead of walking to it. It opens where it stands and everyone gets their
    /// share, as in person: a Gold chest's gold straight into each wallet, a vector chest's vectors straight to each stock, an upgrade
    /// chest's card at each player's feet (take it or throw it away; it's swept to you as the wave starts), a piece chest's cards and
    /// a Buildables chest's builds as usual. Walking there stays free. Client -> host: Msg.CollectChest (id int).
    public partial class Game
    {
        public const int ChestCollectBase = 15, ChestCollectPerWave = 2;

        /// What collecting a chest remotely costs now: 15 + 2 per wave.
        public int ChestCollectFee => ChestCollectBase + ChestCollectPerWave * Math.Max(1, ShownWave);

        /// The break, when the Chests tab can collect (not the Practice Range).
        public bool CanCollectChests => State == MatchState.Intermission && Practice == null;

        /// Every unopened chest on the map. Fills `into` (cleared first).
        public List<ChestSpot> UncollectedChests(List<ChestSpot> into)
        {
            into.Clear();
            foreach (var p in Props.Values)
                if (p.Kind == PropKind.Chest && !p.Open && p.Hp > 0f)
                    into.Add(new ChestSpot { Id = p.Id, Kind = p.Chest, Pos = p.Pos });
            return into;
        }

        /// The local player presses Collect on a chest.
        public void CollectChest(ChestSpot c)
        {
            if (Me == null)
                return;
            if (IsHost)
            {
                HostCollectChest(MyId, c.Id);
                return;
            }
            var w = Proto.Begin(Msg.CollectChest);
            w.Write(c.Id);
            session.SendToHost(Proto.End(w));
        }

        public void OnCollectChest(int peer, BinaryReader r)
        {
            HostCollectChest(peer, r.ReadInt32());
        }

        /// Host: a player collects a chest from afar, if it's the break, the chest is still there and they can pay.
        void HostCollectChest(int playerId, int id)
        {
            Player by;
            if (!IsHost || !CanCollectChests || !Players.TryGetValue(playerId, out by))
                return;
            int fee = ChestCollectFee;
            if (by.Gold < fee)
                return;
            Prop c;
            if (!Props.TryGetValue(id, out c) || c.Kind != PropKind.Chest || c.Open || c.Hp <= 0f)
                return;
            by.Gold -= fee;
            OpenChest(c, true);
            BroadcastFx(FxKind.Ring, by.Pos, new Rgba(1f, 0.85f, 0.4f), 46f, 0.4f, Sfx.PickupBig);  // (on whoever paid)
            DoLog(by.Role + " collected a chest from afar for " + fee + " gold");
            SendSnapshot();
        }
    }
}
