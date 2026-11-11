using System;
using System.Collections.Generic;

namespace Arena
{
    /// A gold coin an enemy dropped in the Expanding arena. It never despawns; whoever walks over it (or is drawn to
    /// it within CoinMagnetRange) gets its gold.
    public class Coin
    {
        public int Id, Value;
        public bool Vec;  // a vector pickup (small: VectorDropSmall, big from elites and bosses: VectorDropBig), not gold
        public V2 Pos, Disp;
        public float Radius => Vec ? (Value >= VectorDropBigAt ? 11f : 8f) : Value >= 5 ? 7f : 5f;
        public const int VectorDropBigAt = 12;
    }

    public partial class Game
    {
        public readonly Dictionary<int, Coin> Coins = new Dictionary<int, Coin>();
        int nextCoinId = 1;
        const float CoinMagnetRange = 90f;
        /// Vector pickups (Docs/VectorBuilding.md): a rare drop that adds vectors to whoever collects it. A normal kill drops one
        /// 1 time in VectorDropChance, an elite 1 in 8 (a big one), a boss always (a big one). It never despawns and waits while
        /// your stock is full.
        public const int VectorDropSmall = 6, VectorDropBig = 12, VectorDropChance = 60;
        readonly List<int> scratchCoinsGone = new List<int>();

        /// Host: a kill in the Expanding arena drops its gold as coins (a coin per 5 gold, then the rest in ones).
        void DropCoins(Enemy e)
        {
            int gold = (int)Math.Round(e.Def.Xp * (e.Elite ? 3 : 1) * GoldPerXp);
            int n = 0;
            while (gold > 0 && n++ < 6)
            {
                int v = gold >= 5 ? 5 : 1;
                gold -= v;
                var c = new Coin { Id = nextCoinId++, Value = v };
                c.Pos = ClampToArena(e.Pos + V2.FromAngle((float)rng.NextDouble() * MathX.Tau) * (8f + 14f * (float)rng.NextDouble()), 12f);
                c.Disp = c.Pos;
                Coins[c.Id] = c;
            }
            DropVector(e);
        }

        void DropVector(Enemy e)
        {
            bool big = e.Elite || e.Def.IsBoss;
            int odds = e.Def.IsBoss ? 1 : e.Elite ? 8 : VectorDropChance;
            if (rng.Next(odds) != 0)
                return;
            var c = new Coin { Id = nextCoinId++, Vec = true, Value = big ? VectorDropBig : VectorDropSmall };
            c.Pos = ClampToArena(e.Pos + V2.FromAngle((float)rng.NextDouble() * MathX.Tau) * (10f + 14f * (float)rng.NextDouble()), 12f);
            c.Disp = c.Pos;
            Coins[c.Id] = c;
        }

        void TickCoins(float dt)
        {
            if (Coins.Count == 0)
                return;
            var gone = scratchCoinsGone;
            gone.Clear();
            foreach (var c in Coins.Values)
            {
                Player nearest = null;
                foreach (var p in Players.Values)
                    if (!p.Downed && (!c.Vec || p.Vectors < p.VectorCap) && p.Pos.DistanceTo(c.Pos) < CoinMagnetRange &&
                        (nearest == null || p.Pos.DistanceSq(c.Pos) < nearest.Pos.DistanceSq(c.Pos)))
                        nearest = p;
                if (nearest == null)
                    continue;
                V2 to = nearest.Pos - c.Pos;
                c.Pos += to.LimitLength(280f * dt);
                if (to.Length < nearest.Def.BodyR + c.Radius)
                {
                    if (c.Vec)
                    {
                        AddVectors(nearest, c.Value);
                        BroadcastFx(FxKind.Ring, c.Pos, new Rgba(0.3f, 0.88f, 1f), c.Value >= Coin.VectorDropBigAt ? 40f : 26f, 0.4f, c.Value >= Coin.VectorDropBigAt ? Sfx.PickupBig : Sfx.Pickup);
                    }
                    else
                    {
                        nearest.Gold += c.Value;
                        BroadcastFx(FxKind.Spark, c.Pos, new Rgba(1f, 0.82f, 0.3f), 12f, 0.25f, Sfx.Pickup);
                    }
                    gone.Add(c.Id);
                }
            }
            foreach (int id in gone)
                Coins.Remove(id);
        }

        int SyncCoin(NetSync s, Coin c)
        {
            int id = c != null ? c.Id : 0, value = c != null ? c.Value : 0;
            bool vec = c != null && c.Vec;
            V2 pos = c != null ? c.Pos : default(V2);
            s.Int(ref id);
            s.Int(ref value);
            s.Bool(ref vec);
            s.Vec(ref pos);
            if (s.Reading)
            {
                if (!Coins.TryGetValue(id, out c))
                {
                    c = new Coin { Id = id, Value = value, Vec = vec, Pos = pos, Disp = pos };
                    Coins[id] = c;
                }
                c.Pos = pos;
            }
            return id;
        }
    }
}
