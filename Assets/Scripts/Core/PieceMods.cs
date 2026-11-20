using System;
using System.Collections.Generic;
using System.Text;

namespace Arena
{
    /// One modifier a shape piece card can carry (Docs/PieceMods.md). A card holds a *set* of them: each modifier owns a few bits of the card code
    /// (Shift, Bits) and holds a value in them (0 = not carried). To add one: subclass this, give it the next free Shift, and add it to PieceMods.All.
    /// To remove one: delete it from All; cards still carrying it just lose it (PieceMods.Clean), nothing breaks.
    public abstract class PieceMod
    {
        public readonly string Id;
        public readonly int Shift, Bits;
        /// The chance each shape card rolls this modifier when it is offered (a plain 0..1, per modifier, so a card can roll several).
        public float Chance;

        protected PieceMod(string id, int shift, int bits, float chance)
        {
            Id = id; Shift = shift; Bits = bits; Chance = chance;
        }

        public int Max => (1 << Bits) - 1;
        public int Get(int mods) => (mods >> Shift) & Max;
        public int Set(int mods, int value) => (mods & ~(Max << Shift)) | ((value & Max) << Shift);

        /// A value this modifier defines (1..): anything else in its bits is dropped.
        public abstract bool Valid(int value);
        /// A value to roll at this wave (seeded), or 0 when nothing is available yet.
        public abstract int RollValue(DetRandom rng, int wave);
        /// The modifier's name for the card (a floor's name, "Combat").
        public abstract string Name(int value);
        /// A clause for the card's tip, after "with": "Burning Grounds ground: ...", "enemies waiting inside".
        public abstract string Blurb(int value);
        /// What it adds to the Builder Shop price of the card, in vectors.
        public virtual int Price(int value) => 8;
        /// Puts the modifier on one of the rooms the piece builds (its kind, floor, ...).
        public virtual ExpansionRoom ApplyRoom(ExpansionRoom q, int value) => q;
        /// Host, once the piece's rooms are built: whatever the modifier does beyond the room itself (enemies, pickups, ...).
        public virtual void OnPlaced(Game g, IList<ExpansionRoom> rooms, int value) { }
    }

    /// Floor modifier: the piece's rooms get a rule floor's ground (Burning Grounds, Sanctuary, ...). Values 1..10 are the PathId, so old cards read the same.
    public sealed class FloorMod : PieceMod
    {
        public FloorMod() : base("floor", 0, 4, 0.3f) { }

        public override bool Valid(int value) => value >= 1 && value <= (int)PathId.Bazaar;

        public override int RollValue(DetRandom rng, int wave)
        {
            var open = new List<PathId>();
            foreach (var f in Expansion.Floors)
                if (Expansion.ThemeUnlocked(f, wave))
                    open.Add(f);
            return open.Count == 0 ? 0 : (int)open[rng.Int(open.Count)];
        }

        public override string Name(int value) => Paths.Get((PathId)value).Name;

        public override string Blurb(int value) => Paths.Get((PathId)value).Name + " ground: " + Paths.Get((PathId)value).Blurb;

        public override ExpansionRoom ApplyRoom(ExpansionRoom q, int value)
        {
            q.Kind = RoomKind.Floor;
            q.Floor = (PathId)value;
            return q;
        }
    }

    /// Combat modifier: enemies wait in the piece's rooms (dormant groups, DormantGroups.cs, at least one). A plain piece spawns none.
    public sealed class CombatMod : PieceMod
    {
        public CombatMod() : base("combat", 4, 1, 0.3f) { }

        public override bool Valid(int value) => value == 1;

        public override int RollValue(DetRandom rng, int wave) => 1;

        public override string Name(int value) => "Combat";

        public override string Blurb(int value) => "enemies waiting inside";

        public override void OnPlaced(Game g, IList<ExpansionRoom> rooms, int value) => g.PlantDormantGroups(rooms, true);
    }

    /// Loot modifier: a chest of one kind is guaranteed in the piece (value = ChestKind + 1: Upgrade, Gold, Vector, Relic, Piece, Buildable, Gadget). It replaces the
    /// 30% room chest roll a piece otherwise gets (RoomChests.cs), and the kind is rolled by rarity like a room chest's.
    public sealed class LootMod : PieceMod
    {
        public LootMod() : base("loot", 5, 3, 0.2f) { }

        public override bool Valid(int value) => value >= 1 && value <= 7;

        public override int RollValue(DetRandom rng, int wave) => (int)Expansion.RollChestKind(rng) + 1;

        static string Kind(int value) => ((ChestKind)(value - 1)).ToString();

        public override string Name(int value) => Kind(value) + " Loot";

        public override string Blurb(int value) => "a guaranteed " + Kind(value) + " chest";

        public override void OnPlaced(Game g, IList<ExpansionRoom> rooms, int value)
            => g.PlaceRoomChest(rooms, "loot piece", (ChestKind)(value - 1), new DetRandom(rooms[0].X * 7919 + rooms[0].Y * 31 + value));
    }

    /// The modifier list and the card-code helpers. Modifier bits sit above the shape in a shape card: code = PieceCodeBase + shape + 16 * mods.
    public static class PieceMods
    {
        public const int Bits = 10;  // modifier bits in a card code (card codes stay under Game's KitBase, and in a 2-byte field)

        public static readonly FloorMod Floor = new FloorMod();
        public static readonly CombatMod Combat = new CombatMod();
        public static readonly LootMod Loot = new LootMod();

        /// Every modifier, in roll order. Add yours here with a Shift that no other uses (PieceMods.Check).
        public static readonly PieceMod[] All = { Floor, Combat, Loot };

        /// The mods number with anything undefined (a removed modifier, a bad value) cleared.
        public static int Clean(int mods)
        {
            int res = 0;
            foreach (var m in All)
            {
                int v = m.Get(mods);
                if (v != 0 && m.Valid(v))
                    res = m.Set(res, v);
            }
            return res;
        }

        /// What each modifier's chance rolls for one offered card at this wave: a mods number (0 = plain). atLeastOne forces one modifier when none hit.
        public static int Roll(DetRandom rng, int wave, bool atLeastOne = false)
        {
            int mods = 0;
            foreach (var m in All)
            {
                if (rng.Float() >= m.Chance)
                    continue;
                int v = m.RollValue(rng, wave);
                if (v != 0)
                    mods = m.Set(mods, v);
            }
            if (atLeastOne && mods == 0)
            {
                var pool = new List<PieceMod>();
                foreach (var m in All)
                    if (m.Chance > 0f)
                        pool.Add(m);
                for (int tries = 0; tries < 8 && mods == 0 && pool.Count > 0; tries++)
                {
                    var m = pool[rng.Int(pool.Count)];
                    int v = m.RollValue(rng, wave);
                    if (v != 0)
                        mods = m.Set(mods, v);
                }
            }
            return mods;
        }

        /// The names of the modifiers a card carries, in list order.
        public static List<string> Names(int mods)
        {
            var res = new List<string>();
            foreach (var m in All)
            {
                int v = m.Get(Clean(mods));
                if (v != 0)
                    res.Add(m.Name(v));
            }
            return res;
        }

        /// "plain", or "with A ground: ..., and enemies waiting inside" (the card's tip).
        public static string Blurb(int mods)
        {
            var parts = new List<string>();
            foreach (var m in All)
            {
                int v = m.Get(Clean(mods));
                if (v != 0)
                    parts.Add(m.Blurb(v));
            }
            if (parts.Count == 0)
                return "a plain empty room";
            var sb = new StringBuilder("with ");
            for (int i = 0; i < parts.Count; i++)
                sb.Append(i == 0 ? "" : i == parts.Count - 1 ? " and " : ", ").Append(parts[i]);
            return sb.ToString();
        }

        /// What the modifiers add to a card's Builder Shop price.
        public static int Price(int mods)
        {
            int p = 0;
            foreach (var m in All)
            {
                int v = m.Get(Clean(mods));
                if (v != 0)
                    p += m.Price(v);
            }
            return p;
        }

        /// Puts every modifier a card carries on one room the piece builds.
        public static ExpansionRoom ApplyRoom(ExpansionRoom q, int mods)
        {
            mods = Clean(mods);
            foreach (var m in All)
            {
                int v = m.Get(mods);
                if (v != 0)
                    q = m.ApplyRoom(q, v);
            }
            return q;
        }

        /// Host, after the piece is built: each modifier's extra work.
        public static void OnPlaced(Game g, IList<ExpansionRoom> rooms, int mods)
        {
            mods = Clean(mods);
            foreach (var m in All)
            {
                int v = m.Get(mods);
                if (v != 0)
                    m.OnPlaced(g, rooms, v);
            }
        }

        /// Test helper: the modifiers' bits overlap nowhere and fit in Bits.
        public static bool Check()
        {
            int used = 0;
            foreach (var m in All)
            {
                int mask = m.Max << m.Shift;
                if ((used & mask) != 0 || m.Shift + m.Bits > Bits)
                    return false;
                used |= mask;
            }
            return true;
        }
    }
}
