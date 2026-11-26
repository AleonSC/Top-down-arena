namespace Arena
{
    /// The Medic: a tiny service room (RoomKind.Medic, from its service card or a mystery door) and the only room shop that sells a full
    /// heal and a revive token (Shop.cs: HealPrice, RevivePrice) while the room shops are open. The shop room, the Black Market and the
    /// Arms Dealer don't sell them; the Bazaar's shop (no rooms) still does.
    public partial class Game
    {
        /// The map has an open Medic room.
        public bool HasMedicRoom
        {
            get
            {
                foreach (var a in ExpansionWorld.Areas)
                    if (a.Kind == Arena.RoomKind.Medic && !a.Sealed)
                        return true;
                return false;
            }
        }

        public bool InMedicRoom(Player p)
        {
            var a = ExpandedAreaAt(p.Disp);
            return a.HasValue && a.Value.Kind == Arena.RoomKind.Medic;
        }
    }
}
