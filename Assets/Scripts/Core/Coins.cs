using System;
using System.Collections.Generic;

namespace Arena
{
    /// A gold coin an enemy dropped in the Expanding arena. It never despawns; whoever walks over it (or is drawn to
    /// it within CoinMagnetRange) gets its gold.
    public class Coin
    {
        public int Id, Value;
        public int Owner;  // the player this copy is for (Docs/RoundFlow.md step 3: every player gets their own copy of a drop); 0: anyone's
        public bool Vec;  // a vector pickup (small: VectorDropSmall, big from elites and bosses: VectorDropBig), not gold
        public V2 Pos, Disp;
        public float Radius => Vec ? (Value >= VectorDropBigAt ? 11f : Value <= 2 ? 6f : 8f) : Value >= 5 ? 7f : 5f;
        public const int VectorDropBigAt = 12;
    }

    public partial class Game
    {
        public readonly Dictionary<int, Coin> Coins = new Dictionary<int, Coin>();
        int nextCoinId = 1;
        const float CoinMagnetRange = 90f;
        /// A coin or vector pickup under a player's cursor (`Player.AimAt`, sent with every input) is taken on the spot, however far away
        /// they stand (the weird hover mechanic, Nov 2026; humans only, a bot's aim point doesn't count; gadgets, health orbs and cards still need walking to).
        const float CoinHoverRange = 14f;
        public const int MaxCoinsPerKill = 6;
        /// Vector pickups (Docs/VectorBuilding.md): a rare drop that adds vectors to whoever collects it. A normal kill drops one
        /// 1 time in VectorDropChance, an elite 1 in 8 (a big one), a boss always (a big one). It never despawns and waits while
        /// your stock is full.
        public const int VectorDropSmall = 6, VectorDropBig = 12, VectorDropChance = 60;
        readonly List<int> scratchCoinsGone = new List<int>();
        readonly List<int> lootOwners = new List<int>();

        /// Host: who a drop is copied for (Docs/RoundFlow.md step 3): every player gets a full copy of each coin, vector pickup, health
        /// orb and Magnet, sees only their own and is the only one who can pick it up. (0 when there are no players, a test's empty game.)
        List<int> LootOwners()
        {
            lootOwners.Clear();
            foreach (int id in Players.Keys)
                lootOwners.Add(id);
            if (lootOwners.Count == 0)
                lootOwners.Add(0);
            return lootOwners;
        }

        /// This client shows (and its player can take) a pickup with this owner: its own, or one anyone can take.
        public bool MinePickup(int owner) => owner == 0 || owner == MyId;

        /// Host: this player may take a pickup with this owner.
        static bool CanTake(Player p, int owner) => owner == 0 || owner == p.Id;

        /// Host, as the next wave starts (Docs/RoundFlow.md step 4): every player's own coins, vector pickups and unclaimed chest cards
        /// still lying about go straight to them, so readying early loses nothing. A vector pickup waits while its player's stock is
        /// full (as on the floor); a card that no longer fits their build is thrown away. Health orbs and Magnets stay where they are,
        /// and so does anything anyone's (Owner 0). The app shows "+n GOLD" from the coins it saw go (LootSweepLook.cs).
        void SweepFloorLoot()
        {
            var gone = scratchCoinsGone;
            gone.Clear();
            foreach (var c in Coins.Values)
            {
                Player p;
                if (c.Owner == 0 || !Players.TryGetValue(c.Owner, out p))
                    continue;
                if (c.Vec)
                {
                    if (p.Vectors >= p.VectorCap)
                        continue;
                    AddVectors(p, c.Value);
                }
                else
                    p.Gold += c.Value;
                gone.Add(c.Id);
            }
            foreach (int id in gone)
                Coins.Remove(id);
            if (Loots.Count == 0)
                return;
            var cards = new List<Loot>(Loots.Values);
            foreach (var l in cards)
            {
                Player p;
                if (!Players.TryGetValue(l.Owner, out p))
                    continue;
                Loots.Remove(l.Id);
                TakeLoot(p, l);  // (one that no longer fits is thrown away: Props.cs)
            }
        }

        /// Host: a coin (or vector pickup) for owner, scattered round pos between `near` and `near + spread` px out.
        Coin AddCoin(int owner, int value, bool vec, V2 pos, float near, float spread)
        {
            var c = new Coin { Id = nextCoinId++, Value = value, Vec = vec, Owner = owner };
            c.Pos = ClampToArena(pos + V2.FromAngle((float)rng.NextDouble() * MathX.Tau) * (near + spread * (float)rng.NextDouble()), 12f);
            c.Disp = c.Pos;
            Coins[c.Id] = c;
            return c;
        }

        /// Host: a kill in the Expanding arena drops its gold as coins (a coin per 5 gold, then the rest in ones; at most
        /// MaxCoinsPerKill coins, the last one worth what is left).
        void DropCoins(Enemy e)
        {
            foreach (int owner in LootOwners())  // (a full copy each)
            {
                int gold = (int)Math.Round(e.Def.Xp * (e.Elite ? 3 : 1) * GoldPerXp);  // (not scaled by Pace, unlike the Bazaar's GrantGold: the owner's choice, Nov 2026; this rate felt right in play)
                int n = 0;
                while (gold > 0)
                {
                    int v = ++n >= MaxCoinsPerKill ? gold : gold >= 5 ? 5 : 1;  // the last coin carries the rest (a boss's or an elite's gold)
                    gold -= v;
                    AddCoin(owner, v, false, e.Pos, 8f, 14f);
                }
            }
            DropVector(e);
            DropGadget(e);  // Gadgets.cs: now and then a Magnet
        }

        void DropVector(Enemy e)
        {
            bool big = e.Elite || e.Def.IsBoss;
            int odds = e.Def.IsBoss ? 1 : e.Elite ? 8 : VectorDropChance;
            if (rng.Next(odds) != 0)
                return;
            DropVectorAt(e.Pos, big ? VectorDropBig : VectorDropSmall);
        }

        /// A vector pickup worth value, dropped a little way from pos.
        void DropVectorAt(V2 pos, int value)
        {
            foreach (int owner in LootOwners())  // (a full copy each)
                AddCoin(owner, value, true, pos, 10f, 14f);
        }

        void TickCoins(float dt)
        {
            if (Coins.Count == 0)
                return;
            var gone = scratchCoinsGone;
            gone.Clear();
            foreach (var c in Coins.Values)
            {
                if (c.Owner != 0 && !Players.ContainsKey(c.Owner))
                {
                    gone.Add(c.Id);  // its player left
                    continue;
                }
                Player hover = null;
                foreach (var p in Players.Values)
                    if (!p.Downed && !p.IsBot && CanTake(p, c.Owner) && (!c.Vec || p.Vectors < p.VectorCap) && p.AimAt.CloserThan(c.Pos, CoinHoverRange + c.Radius))
                    {
                        hover = p;
                        break;
                    }
                if (hover != null)
                {
                    TakeCoin(hover, c);
                    gone.Add(c.Id);
                    continue;
                }
                Player nearest = null;
                foreach (var p in Players.Values)  // a switched-on Magnet (Gadgets.cs) pulls it from anywhere, ahead of anyone's reach
                    if (CanTake(p, c.Owner) && MagnetOn(p) && (!c.Vec || p.Vectors < p.VectorCap) && (nearest == null || p.Pos.DistanceSq(c.Pos) < nearest.Pos.DistanceSq(c.Pos)))
                        nearest = p;
                bool pulled = nearest != null;
                if (!pulled)
                    foreach (var p in Players.Values)
                        if (!p.Downed && CanTake(p, c.Owner) && (!c.Vec || p.Vectors < p.VectorCap) && p.Pos.CloserThan(c.Pos, CoinMagnetRange) &&
                            (nearest == null || p.Pos.DistanceSq(c.Pos) < nearest.Pos.DistanceSq(c.Pos)))
                            nearest = p;
                if (nearest == null)
                    continue;
                V2 to = nearest.Pos - c.Pos;
                c.Pos += to.LimitLength((pulled ? MagnetPull : 280f) * dt);
                if (to.Length < nearest.Def.BodyR + c.Radius)
                {
                    TakeCoin(nearest, c);
                    gone.Add(c.Id);
                }
            }
            foreach (int id in gone)
                Coins.Remove(id);
        }

        void TakeCoin(Player p, Coin c)
        {
            if (c.Vec)
            {
                AddVectors(p, c.Value);
                BroadcastFx(FxKind.Ring, c.Pos, new Rgba(0.3f, 0.88f, 1f), c.Value >= Coin.VectorDropBigAt ? 40f : 26f, 0.4f, c.Value >= Coin.VectorDropBigAt ? Sfx.PickupBig : Sfx.Pickup);
            }
            else
            {
                p.Gold += c.Value;
                BroadcastFx(FxKind.Spark, c.Pos, Rgba.Gold, 12f, 0.25f, Sfx.Pickup);
            }
        }

        /// Host: the coins as the clients last heard of them (id: position), so a snapshot sends only what changed. Coins mostly lie
        /// still and pile up all wave (every player's copy), so sending them all 20 times a second swamped the link (Nov 2026:
        /// 1000 coins were 22 KB a snapshot, 440 KB/s per client).
        readonly Dictionary<int, V2> coinsSent = new Dictionary<int, V2>();
        readonly List<Coin> scratchCoinsChanged = new List<Coin>();
        int coinSnaps, coinPlayersSent = -1;
        /// Snapshots between full coin lists: a client that missed a change (joined late, or wasn't running) is right again within ~3 s.
        public const int CoinFullEvery = 60;

        /// Host: send the full coin list in the next snapshot.
        void ForgetSentCoins()
        {
            coinsSent.Clear();
            coinPlayersSent = -1;
        }

        /// The coins in a snapshot: a full list every CoinFullEvery snapshots (and when the party changes), otherwise only the coins
        /// dropped or moved since the last one and the ids of those gone. The connection is ordered and reliable, so changes add up.
        void SyncCoins(NetSync s)
        {
            bool full = false;
            if (!s.Reading)
            {
                full = ++coinSnaps >= CoinFullEvery || coinPlayersSent != Players.Count;
                if (full)
                {
                    coinSnaps = 0;
                    coinPlayersSent = Players.Count;
                }
            }
            s.Bool(ref full);
            int count = 0;
            if (full)
            {
                count = Coins.Count;
                s.Short(ref count);
                if (s.Reading)
                {
                    snapSeen.Clear();
                    for (int i = 0; i < count; i++)
                        snapSeen.Add(SyncCoin(s, null));
                    RemoveUnseen(Coins);
                    return;
                }
                coinsSent.Clear();
                foreach (var c in Coins.Values)
                {
                    SyncCoin(s, c);
                    coinsSent[c.Id] = c.Pos;
                }
                return;
            }
            if (s.Reading)
            {
                count = 0;
                s.Short(ref count);
                for (int i = 0; i < count; i++)
                    SyncCoin(s, null);
                s.Short(ref count);
                for (int i = 0; i < count; i++)
                {
                    int id = 0;
                    s.Int(ref id);
                    Coins.Remove(id);
                }
                return;
            }
            var changed = scratchCoinsChanged;
            changed.Clear();
            foreach (var c in Coins.Values)
            {
                V2 was;
                if (!coinsSent.TryGetValue(c.Id, out was) || was.X != c.Pos.X || was.Y != c.Pos.Y)
                    changed.Add(c);
            }
            count = changed.Count;
            s.Short(ref count);
            foreach (var c in changed)
            {
                SyncCoin(s, c);
                coinsSent[c.Id] = c.Pos;
            }
            var gone = scratchCoinsGone;
            gone.Clear();
            foreach (int id in coinsSent.Keys)
                if (!Coins.ContainsKey(id))
                    gone.Add(id);
            count = gone.Count;
            s.Short(ref count);
            foreach (int id in gone)
            {
                int k = id;
                s.Int(ref k);
                coinsSent.Remove(id);
            }
        }

        int SyncCoin(NetSync s, Coin c)
        {
            int id = c != null ? c.Id : 0, value = c != null ? c.Value : 0, owner = c != null ? c.Owner : 0;
            bool vec = c != null && c.Vec;
            V2 pos = c != null ? c.Pos : default(V2);
            s.Int(ref id);
            s.Int(ref value);
            s.Bool(ref vec);
            s.Int(ref owner);
            s.Vec(ref pos);
            if (s.Reading)
            {
                if (!Coins.TryGetValue(id, out c))
                {
                    c = new Coin { Id = id, Value = value, Vec = vec, Owner = owner, Pos = pos, Disp = pos };
                    Coins[id] = c;
                }
                c.Pos = pos;
            }
            return id;
        }
    }
}
