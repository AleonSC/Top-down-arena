using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// The Bazaar (a 1-skull path, Paths.cs): kills on its waves drop gold, the same amount to every player, and
    /// before the boss everyone gets a shop. Each player has their own shelf (four upgrades for their class, one
    /// legendary, one weapon mod) and services (restock, remove an upgrade, full heal, revive token), and can give
    /// gold to teammates. There's no timer: the shop closes when everyone has clicked Done (the host can close it
    /// after a minute), and each player's leftover gold becomes their XP. Host-authoritative: clients send
    /// Msg.Shop requests and see the result in the snapshot.
    public enum ShopAction : byte { Buy, Restock, Remove, Heal, Revive, Done, Give, Close }

    public class ShopNotice
    {
        public string Text;
        public float Life;
    }

    public partial class Game
    {
        public const int ShelfSize = 6;                 // four upgrades, a legendary, a weapon mod
        public const float GoldPerXp = 0.3f;            // gold dropped per point of XP a kill gives
        public const float GoldToXp = 1f;               // XP per leftover gold when the shop closes
        public const float ShopHostCloseAfter = 60f;    // the host can close the shop for everyone after this long
        public const int RemovePrice = 75, HealPrice = 40, RevivePrice = 120;
        const float TokenReviveTime = 2f;               // a revive token gets you up this long after you go down

        public bool ShopOpen;      // the Bazaar's shop is open (replicated)
        public float ShopT;        // how long it's been open (replicated)
        public readonly List<ShopNotice> ShopNotices = new List<ShopNotice>();  // "Mira gave you 40 gold" (local)

        public static int Price(int upgrade)
        {
            switch (Upgrades.All[upgrade].Rarity)
            {
                case Rarity.Common: return 40;
                case Rarity.Uncommon: return 60;
                case Rarity.Rare: return 100;
                case Rarity.VeryRare: return 160;
                case Rarity.Mod: return 200;
                default: return 280;  // legendary
            }
        }

        public static int RestockPrice(Player p) => 25 + 25 * p.Restocks;

        public static bool Sold(Player p, int slot) => (p.ShelfSold & (1 << slot)) != 0;

        /// Host: a kill on the Bazaar's waves gives every player the same gold.
        void GrantGold(Enemy e)
        {
            int gold = (int)Math.Round(e.Def.Xp * (e.Elite ? 3 : 1) * GoldPerXp);
            if (gold <= 0)
                return;
            foreach (var p in Players.Values)
                p.Gold += gold;
            BroadcastFx(FxKind.Spark, e.Pos, new Rgba(1f, 0.82f, 0.3f), 14f, 0.35f, Sfx.None);
        }

        /// Host, when the Bazaar's last wave is cleared: everyone gets a shelf and the shop opens.
        void OpenShop()
        {
            ShopOpen = true;
            ShopT = 0f;
            foreach (var p in Players.Values)
            {
                p.ShopDone = false;
                p.Restocks = 0;
                RollShelf(p);
            }
            DoLog("the Bazaar opens");
        }

        void RollShelf(Player p)
        {
            var shelf = new List<int>();
            for (int tries = 0; tries < 6 && shelf.Count < 4; tries++)
                foreach (int u in Upgrades.RollOffer(rng, p.Role, p.RoleAbility, p.Taken, p.Level))
                    if (u >= 0 && shelf.Count < 4 && !shelf.Contains(u))
                        shelf.Add(u);
            while (shelf.Count < 4)
                shelf.Add(-1);
            var boss = BossForWave(Math.Max(5, PathFrom - 1));
            var legendaries = Upgrades.LegendaryOffer(rng, IsTwin(boss) ? EnemyType.TwinA : boss, p.Role, p.RoleAbility, p.Taken);
            shelf.Add(legendaries != null && legendaries.Length > 0 ? legendaries[rng.Next(legendaries.Length)] : -1);
            var mods = Upgrades.ModOffer(p.Role, p.RoleAbility, p.Taken);
            shelf.Add(mods != null ? mods[rng.Next(mods.Length)] : -1);
            for (int i = 0; i < ShelfSize; i++)
                p.Shelf[i] = shelf[i];
            p.ShelfSold = 0;
        }

        /// The local player does something in the shop (arg: shelf slot, upgrade to remove, Done on/off, or the
        /// teammate to give to; arg2: how much gold to give).
        public void Shop(ShopAction action, int arg = 0, int arg2 = 0)
        {
            if (Me == null || !ShopOpen)
                return;
            if (action != ShopAction.Done && action != ShopAction.Give)
                Sounds.Add(Sfx.Pick);
            if (IsHost)
            {
                HostShop(MyId, action, arg, arg2);
                return;
            }
            var w = Proto.Begin(Msg.Shop);
            w.Write((byte)action);
            w.Write(arg);
            w.Write(arg2);
            session.SendToHost(Proto.End(w));
        }

        public void OnShop(int peer, BinaryReader r)
        {
            var action = (ShopAction)r.ReadByte();
            int arg = r.ReadInt32();
            HostShop(peer, action, arg, r.ReadInt32());
        }

        void HostShop(int id, ShopAction action, int arg, int arg2)
        {
            Player p;
            if (!IsHost || !ShopOpen || !Players.TryGetValue(id, out p))
                return;
            bool shopping = !p.ShopDone;
            switch (action)
            {
                case ShopAction.Buy:
                {
                    if (!shopping || arg < 0 || arg >= ShelfSize || Sold(p, arg) || p.Shelf[arg] < 0)
                        return;
                    int u = p.Shelf[arg], price = Price(u);
                    if (p.Gold < price || !Upgrades.Available(u, p.Role, p.RoleAbility, p.Taken))
                        return;
                    p.Gold -= price;
                    p.ShelfSold |= (byte)(1 << arg);
                    Recorder?.Picked(this, p, (int[])p.Shelf.Clone(), u, "shop");
                    GrantUpgrade(id, u);
                    DoLog(p.Role + " bought " + Upgrades.All[u].Name + " for " + price);
                    break;
                }
                case ShopAction.Restock:
                    if (!shopping || p.Gold < RestockPrice(p))
                        return;
                    p.Gold -= RestockPrice(p);
                    p.Restocks++;
                    RollShelf(p);
                    break;
                case ShopAction.Remove:
                    if (!shopping || p.Gold < RemovePrice || !p.Taken.Contains(arg))
                        return;
                    p.Gold -= RemovePrice;
                    p.Taken.Remove(arg);
                    Recalc(p);
                    p.Hp = Math.Min(p.Hp, p.MaxHp);
                    BroadcastUpgrades(p);
                    DoLog(p.Role + " removed " + Upgrades.All[arg].Name);
                    break;
                case ShopAction.Heal:
                    if (!shopping || p.Gold < HealPrice || p.Downed || p.Hp >= p.MaxHp)
                        return;
                    p.Gold -= HealPrice;
                    p.Hp = p.MaxHp;
                    break;
                case ShopAction.Revive:
                    if (!shopping || p.Gold < RevivePrice || p.ReviveToken)
                        return;
                    p.Gold -= RevivePrice;
                    p.ReviveToken = true;
                    break;
                case ShopAction.Done:
                    p.ShopDone = arg != 0;
                    break;
                case ShopAction.Give:
                {
                    Player to;
                    int amount = Math.Min(arg2, p.Gold);
                    if (arg == id || amount <= 0 || !Players.TryGetValue(arg, out to))
                        return;
                    p.Gold -= amount;
                    to.Gold += amount;
                    to.ShopDone = false;  // giving to someone who's done reopens their shop, so they can choose
                    var w = Proto.Begin(Msg.ShopGift);
                    w.Write(id);
                    w.Write(arg);
                    w.Write(amount);
                    session.Broadcast(Proto.End(w));
                    GiftNotice(id, arg, amount);
                    DoLog(p.Role + " gave " + to.Role + " " + amount + " gold");
                    break;
                }
                case ShopAction.Close:
                    if (id == MyId && ShopT >= ShopHostCloseAfter)
                        CloseShop();
                    return;
            }
            SendSnapshot();
        }

        /// Host, in the intermission: the shop closes once everyone is done.
        void TickShop(float dt)
        {
            if (!ShopOpen)
                return;
            ShopT += dt;
            foreach (var p in Players.Values)
                if (!p.ShopDone)
                    return;
            CloseShop();
        }

        /// Everyone's leftover gold becomes their own XP.
        void CloseShop()
        {
            ShopOpen = false;
            foreach (var p in Players.Values)
            {
                if (p.Gold > 0)
                {
                    GrantXpTo(p, p.Gold * GoldToXp);
                    DoLog(p.Role + ": " + p.Gold + " leftover gold -> XP");
                }
                p.Gold = 0;
                p.ShopDone = false;
                for (int i = 0; i < ShelfSize; i++)
                    p.Shelf[i] = -1;
                p.ShelfSold = 0;
                if (p.Offer == null)
                    NextOffer(p);  // a level-up from the XP
            }
            DoLog("the Bazaar closes");
            SendSnapshot();
        }

        /// Host: a downed player with a revive token gets up on their own.
        void TickReviveToken(Player p, float dt)
        {
            if (!p.Downed || !p.ReviveToken)
            {
                p.TokenT = 0f;
                return;
            }
            p.TokenT += dt;
            if (p.TokenT < TokenReviveTime)
                return;
            p.ReviveToken = false;
            p.TokenT = 0f;
            RevivePlayer(p, 0.5f);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.85f, 0.4f), 90f, 0.6f, Sfx.Revive);
            DoLog(p.Role + " used a revive token");
        }

        void ReadShopGift(BinaryReader r)
        {
            int from = r.ReadInt32(), to = r.ReadInt32(), amount = r.ReadInt32();
            GiftNotice(from, to, amount);
        }

        void GiftNotice(int from, int to, int amount)
        {
            Player a, b;
            if (!Players.TryGetValue(from, out a) || !Players.TryGetValue(to, out b))
                return;
            if (to == MyId)
                ShopNotices.Add(new ShopNotice { Text = a.Name + " gave you " + amount + " gold", Life = 3f });
            else if (from == MyId)
                ShopNotices.Add(new ShopNotice { Text = "You gave " + b.Name + " " + amount + " gold", Life = 3f });
            if (to == MyId || from == MyId)
                Sounds.Add(Sfx.Pickup);
        }

        void TickShopNotices(float dt)
        {
            for (int i = ShopNotices.Count - 1; i >= 0; i--)
            {
                ShopNotices[i].Life -= dt;
                if (ShopNotices[i].Life <= 0f)
                    ShopNotices.RemoveAt(i);
            }
        }

        /// A bot shops: the cheapest thing it can afford, then a heal if it needs one, then done.
        void BotShop(Player me)
        {
            int best = -1;
            for (int i = 0; i < ShelfSize; i++)
                if (me.Shelf[i] >= 0 && !Sold(me, i) && Price(me.Shelf[i]) <= me.Gold && Upgrades.Available(me.Shelf[i], me.Role, me.RoleAbility, me.Taken)
                    && (best < 0 || Price(me.Shelf[i]) < Price(me.Shelf[best])))
                    best = i;
            if (best >= 0)
                Shop(ShopAction.Buy, best);
            else if (me.Hp < me.MaxHp && !me.Downed && me.Gold >= HealPrice)
                Shop(ShopAction.Heal);
            else
                Shop(ShopAction.Done, 1);
        }

        void WriteShop(BinaryWriter w, Player p)
        {
            w.Write(p.Gold);
            w.Write(p.ShopDone);
            w.Write(p.ReviveToken);
            w.Write((byte)p.Restocks);
            w.Write(p.ShelfSold);
            for (int i = 0; i < ShelfSize; i++)
                w.Write((short)p.Shelf[i]);
        }

        void ReadShop(BinaryReader r, Player p)
        {
            int gold = r.ReadInt32();
            bool done = r.ReadBoolean(), token = r.ReadBoolean();
            int restocks = r.ReadByte();
            byte sold = r.ReadByte();
            var shelf = new int[ShelfSize];
            for (int i = 0; i < ShelfSize; i++)
                shelf[i] = r.ReadInt16();
            if (p == null)
                return;
            p.Gold = gold;
            p.ShopDone = done;
            p.ReviveToken = token;
            p.Restocks = restocks;
            p.ShelfSold = sold;
            Array.Copy(shelf, p.Shelf, ShelfSize);
        }
    }
}
