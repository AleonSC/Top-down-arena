using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Arms Dealer (Docs/ArmsDealer.md): a tiny service room (RoomKind.Arms, from its room card or a mystery door) that sells every
    /// weapon mod for gold while the room shops are open. The price rises with the stacks you already hold of that mod. Every boss cycle
    /// without one on the map deals someone an Arms Dealer card (GuaranteeArmsCard).
    public partial class Game
    {
        /// What your 1st, 2nd and 3rd stack of a mod cost (first guesses).
        public static readonly int[] ModPrices = { 150, 250, 400 };

        static int[] modIds;

        /// Every weapon mod, in the upgrade list's order (the Arms Dealer's wares).
        public static int[] ModIds
        {
            get
            {
                if (modIds == null)
                {
                    var ids = new List<int>();
                    for (int id = 0; id < Upgrades.All.Length; id++)
                        if (Upgrades.All[id].Rarity == Rarity.Mod)
                            ids.Add(id);
                    modIds = ids.ToArray();
                }
                return modIds;
            }
        }

        /// What this player's next stack of the mod costs (the last price again past the table).
        public static int RoomTypePrice(Player p, int upgrade) => ModPrices[Math.Min(ModPrices.Length - 1, Upgrades.Count(p.Taken, upgrade))];

        /// The map has an open Arms Dealer room.
        public bool HasArmsRoom
        {
            get
            {
                foreach (var a in ExpansionWorld.Areas)
                    if (a.Kind == Arena.RoomKind.Arms && !a.Sealed)
                        return true;
                return false;
            }
        }

        public bool InArmsRoom(Player p)
        {
            var a = ExpandedAreaAt(p.Disp);
            return a.HasValue && a.Value.Kind == Arena.RoomKind.Arms;
        }

        /// The boss cycle that got its Arms Dealer card (host only, not sent; -1 none yet).
        int armsCardCycle = -1;

        /// Host, as a build round opens: no boss cycle passes without a way to buy weapon mods. With no Arms Dealer on the map and no
        /// Arms Dealer card in anyone's hand, the player with the most room in their hand gets one, once a boss cycle (the cycle of
        /// the coming wave). With every hand full it tries again next round.
        void GuaranteeArmsCard()
        {
            if (!IsHost || !ExpandingOn || !ModeRules.RunEvents || HasArmsRoom)
                return;
            int cycle = Math.Max(0, Wave) / Expansion.Milestone;
            if (cycle == armsCardCycle)
                return;
            var ids = SnapKeys(Players);
            ids.Sort();
            Player to = null;
            int room = 0;
            bool held = false;
            foreach (int id in ids)
            {
                var p = Players[id];
                foreach (byte c in p.Hand)
                    if (c == (byte)Piece.Arms)
                        held = true;
                int free = Expansion.HandMax - HandCount(p);
                if (free > room)
                {
                    room = free;
                    to = p;
                }
            }
            Free(ids);
            if (held)
            {
                armsCardCycle = cycle;  // (someone has one to play)
                return;
            }
            if (to == null)
                return;
            armsCardCycle = cycle;
            GiveCard(to, Piece.Arms, "this boss cycle's Arms Dealer");
        }

        /// Host: the Arms Dealer sells this mod (an upgrade id) to the player, if it's a mod they can still take and they have the gold.
        bool BuyMod(Player p, int upgrade)
        {
            if (upgrade < 0 || upgrade >= Upgrades.All.Length || Upgrades.All[upgrade].Rarity != Rarity.Mod || !Upgrades.Available(upgrade, OfferFor(p)))
                return false;
            int price = RoomTypePrice(p, upgrade);
            if (p.Gold < price)
                return false;
            p.Gold -= price;
            Recorder?.Picked(this, p, ModIds, upgrade, "arms");
            GrantUpgrade(p.Id, upgrade);
            DoLog(p.Role + " bought " + Upgrades.All[upgrade].Name + " from the Arms Dealer for " + price);
            return true;
        }
    }
}
