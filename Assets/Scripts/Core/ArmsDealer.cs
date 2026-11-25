using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Arms Dealer (Docs/ArmsDealer.md): a tiny service room (RoomKind.Arms, from its room card or a mystery door) that sells every
    /// weapon mod for gold while the room shops are open. The price rises with the stacks you already hold of that mod. Its card comes
    /// like the other service cards: wave drops, chests and the boss card pick.
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

        /// Host: the Arms Dealer sells this mod (an upgrade id) to the player, if it's a mod they can still take and they have the gold.
        bool BuyMod(Player p, int upgrade)
        {
            if (upgrade < 0 || upgrade >= Upgrades.All.Length || Upgrades.All[upgrade].Rarity != Rarity.Mod || !Upgrades.Available(upgrade, OfferFor(p)))
                return false;
            int price = RoomTypePrice(p, upgrade);
            if (p.Gold < price)
                return false;
            p.Gold -= price;
            GrantUpgrade(p.Id, upgrade);
            DoLog(p.Role + " bought " + Upgrades.All[upgrade].Name + " from the Arms Dealer for " + price);
            return true;
        }
    }
}
