using System;
using System.IO;

namespace Arena
{
    /// What the Black Market (the Black Market, Docs/BlackMarket.md) sells to build around the map. Bought with gold into the
    /// buyer's stash, placed in the break between waves (the draw screen's Teleporter and Trap tools). The teleporter and the turret
    /// (Turret.cs) work; the landmine and slow field are bought and placed, and have no effect yet.
    public enum BuildKind : byte { Teleporter, Turret, Landmine, SlowField }  // (new kinds go at the end; Turret took the retired Spike trap's slot)

    public struct PlacedBuild : IEquatable<PlacedBuild>
    {
        public BuildKind Kind;
        public short X, Y;
        public byte Owner;   // who placed it (a turret's shots are theirs)
        public byte Waves;   // a turret: the waves it still fights (Market.TurretWaves when placed; it goes when this reaches 0)
        public float Cd;     // a turret's shot cooldown (host only, not sent)
        public V2 Pos => new V2(X, Y);
        public bool Equals(PlacedBuild o) => Kind == o.Kind && X == o.X && Y == o.Y && Owner == o.Owner && Waves == o.Waves;
    }

    public static class Market
    {
        public const int Count = 4;
        public const int StashMax = 5;     // of one kind a player carries unplaced
        public const int MaxPlaced = 12;   // traps standing on the map at once (teleporters are Expansion.MaxLinks pairs)
        public const float PlaceClear = 120f;

        public const int TurretWaves = 5;      // a turret fights this many waves, then goes
        public const int TurretsEach = 3;      // turrets one player may have standing at once

        public static readonly string[] Names = { "Teleporter pair", "Turret", "Landmine", "Slow field" };
        public static readonly int[] Prices = { 150, 120, 80, 90 };
        public static readonly string[] Blurbs =
        {
            "Two portals in two rooms. Step in one, come out the other. Enemies use them too.",
            "A sentry that shoots the nearest enemy in range for 5 waves. Up to 3 of yours at once.",
            "Goes off once when an enemy walks over it.",
            "Slows everything that crosses it.",
        };

        public static bool Valid(int kind) => kind >= 0 && kind < Count;

        /// A trap's kind is placed with one click (the teleporter takes two: Expansion.LinkFits).
        public static bool IsTrap(BuildKind k) => k != BuildKind.Teleporter;
    }

    public partial class Game
    {
        /// The traps standing on the map (synced in Msg.ExpandState, saved as `build` lines).
        public readonly System.Collections.Generic.List<PlacedBuild> Placed = new System.Collections.Generic.List<PlacedBuild>();

        /// This game's player puts a bought trap on the map at (x, y), a spot in a connected room (the draw screen's Trap tool).
        public void PlaceBuild(BuildKind kind, int x, int y)
        {
            if (!ExpandingOn || Me == null || !Market.IsTrap(kind))
                return;
            if (IsHost)
            {
                HostPlaceBuild(MyId, kind, x, y);
                return;
            }
            var w = Proto.Begin(Msg.PlaceBuild);
            w.Write((byte)kind);
            w.Write((short)x);
            w.Write((short)y);
            session.SendToHost(Proto.End(w));
        }

        public void OnPlaceBuild(int peer, BinaryReader r) => HostPlaceBuild(peer, (BuildKind)r.ReadByte(), r.ReadInt16(), r.ReadInt16());

        /// Host: a trap from the stash goes on the map, if it's the break, the spot is clear and the player has one.
        void HostPlaceBuild(int id, BuildKind kind, int x, int y)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !Players.TryGetValue(id, out p) || !Market.Valid((int)kind) || !Market.IsTrap(kind) || p.Stash[(int)kind] == 0)
                return;
            string why;
            if (kind == BuildKind.Turret && TurretsOf(id) >= Market.TurretsEach)
                why = "You have " + Market.TurretsEach + " turrets up already";
            else
                Expansion.PlaceSpot(ExpansionWorld, Placed, new V2(x, y), out why);
            if (why != null)
            {
                DoLog("trap refused (" + why + ")");
                return;
            }
            p.Stash[(int)kind]--;
            Placed.Add(new PlacedBuild { Kind = kind, X = (short)x, Y = (short)y, Owner = (byte)id, Waves = kind == BuildKind.Turret ? (byte)Market.TurretWaves : (byte)0 });
            DoLog(p.Name + " placed a " + Market.Names[(int)kind].ToLowerInvariant());
            Sounds.Add(Sfx.Pick);
            BroadcastExpansion();
            SendSnapshot();
        }
    }
}
