using System;
using System.IO;

namespace Arena
{
    /// The Builder Shop (Docs/RoomCards.md, "Utility cards"): a utility card whose one-cell room (`RoomKind.Builder`) is a shop for pieces.
    /// Standing in it during a build round shows its shelf (BuilderShopUi.cs): BuilderOfferCount cards, a new seeded shelf each wave (the same on
    /// every machine), paid in vectors at a quarter off the full price. Bought cards go to your hand. It never sells itself.
    public static partial class Expansion
    {
        public const int BuilderOfferCount = 5;

        /// What the Builder Shop charges for a card: three quarters of the full price.
        public static int BuilderPrice(PieceCard c) => (CardPrice(c) * 3 + 3) / 4;
    }

    public partial class Game
    {
        /// The Builder Shop's shelf this wave.
        public PieceCard[] BuilderCards => Expansion.GridBuilderOffers(expandSeed, Wave);

        public bool InBuilderRoom(Player p)
        {
            var a = ExpandedAreaAt(p.Disp);
            return a.HasValue && a.Value.Kind == Arena.RoomKind.Builder;
        }

        /// The Builder Shop's shelf is open for this player: a build round, standing in a Builder room.
        public bool BuilderShopOpen(Player p) => p != null && ExpandingOn && ExpandOpen && !p.Downed && InBuilderRoom(p);

        /// Why this player can't buy that offer right now (null if they can).
        public string BuilderRefusal(Player p, int offer)
        {
            if (!BuilderShopOpen(p) || offer < 0 || offer >= Expansion.BuilderOfferCount)
                return "Closed";
            if (HandCount(p) >= Expansion.HandMax)
                return "Hand full";
            if (SpendableVectors(p) < Expansion.BuilderPrice(BuilderCards[offer]))
                return "Not enough vectors";
            return null;
        }

        /// This game's player buys the Builder Shop's card at this offer.
        public void BuyBuilderCard(int offer)
        {
            if (Me == null || BuilderRefusal(Me, offer) != null)
                return;
            Sounds.Add(Sfx.Pick);
            if (IsHost)
            {
                HostBuilderBuy(MyId, offer);
                return;
            }
            var w = Proto.Begin(Msg.BuilderBuy);
            w.Write((byte)offer);
            session.SendToHost(Proto.End(w));
        }

        public void OnBuilderBuy(int peer, BinaryReader r) => HostBuilderBuy(peer, r.ReadByte());

        /// Host: the card goes to the buyer's hand for its Builder price in vectors.
        void HostBuilderBuy(int id, int offer)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p) || BuilderRefusal(p, offer) != null)
                return;
            var c = BuilderCards[offer];
            int price = Expansion.BuilderPrice(c);
            PayVectors(p, price);
            GiveCard(p, c, "Builder Shop, " + price + " vectors");
        }
    }
}
