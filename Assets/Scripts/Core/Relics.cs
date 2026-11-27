using System;
using System.Collections.Generic;

namespace Arena
{
    /// A relic: a map-themed modifier, one slot per player (a new one replaces the old). Earned by building a room round a
    /// relic node on the Expanding arena (Docs/Nodes.md, "Relic nodes"). Separate from the bosses' weapon mods.
    public enum RelicId : byte
    {
        WallBounce, Backup, HallwayRunner,
        // Faction relics (Docs/RelicNodes.md), in boss order (Game.BossCycle): Warden 1, Hive Queen 3, Reaper of Dusk 2, Singularity 2, Colossus 1, Duelist 2, Lich 3, Storm Herald 2, Devourer 1, Eclipse 3.
        WardensSeal,
        BroodCrown, RoyalJelly, Stinger,
        TwinScythe, DuskLantern,
        EventHorizon, GravityWell,
        StoneHeart,
        SilverFoil, DuelGem,
        Phylactery, GraveFang, ColdHeart,
        StormCoil, StaticCell,
        MawTooth,
        BlackSun, CoronaSpiral, UmbraShard,
        Thorns,  // a classic relic added after the factions' (Classic stays 3; it rolls on a plain relic node)
        None
    }

    public static class Relics
    {
        public const int WallBounces = 1;        // Wall Bounce: bounces off walls and pillars per shot
        public const float WallBounceDmg = 0.7f; // the damage a shot keeps each bounce
        public const float TakeReach = 46f;      // how close a player walks to a relic node to take it
        public const float BackupHp = 0.5f;      // Backup: the share of max health you get back up with (owner Nov 17: an auto revive, gone after use)
        public const float BackupInvuln = 2f;    // and the grace after it
        public const float HallSpeed = 1.25f;    // Hallway Runner: move speed in a hallway
        public const float ThornsDmg = 14f;      // Thorns: damage an enemy takes each time its touch hurts you (times your damage stat)
        public const float HallWidth = 300f;     // a box this narrow or less (3 tiles) is a hallway
        public const int Count = (int)RelicId.None;
        public const int Classic = 3;                  // the first three relics (Wall Bounce, Backup, Hallway Runner) are not a faction's
        public static readonly int[] PerFaction = { 1, 3, 2, 2, 1, 2, 3, 2, 1, 3 };  // 1-3 guaranteed relic nodes for each boss in Game.BossCycle order

        static readonly string[] FactionNames =
        {
            "Warden's Seal", "Brood Crown", "Royal Jelly", "Stinger", "Twin Scythe", "Dusk Lantern", "Event Horizon", "Gravity Well", "Stone Heart",
            "Silver Foil", "Duel Gem", "Phylactery", "Grave Fang", "Cold Heart", "Storm Coil", "Static Cell", "Maw Tooth", "Black Sun", "Corona Spiral", "Umbra Shard",
        };

        static readonly string[] FactionDescs =
        {
            "You take 12% less damage.", "You fire 12% faster.", "You regenerate 1.5 health a second.", "Your damage is up 10%.",
            "Your damage is up 8% and your shots pierce one more enemy.", "You move 8% faster.", "Your abilities recharge 12% faster.", "Your shots pierce one more enemy.",
            "You have 30 more max health.", "You fire 10% faster and deal 5% more damage.", "Your damage is up 12%, but you take 8% more.",
            "You have 20 more max health and regenerate 1 a second.", "Your damage is up 10%.", "You take 8% less damage and have 10 more max health.",
            "Your abilities recharge 10% faster.", "You fire 8% faster and move 5% faster.", "You regenerate 2 health a second and deal 5% more damage.",
            "Your damage is up 12%.", "Your abilities recharge 10% faster and you move 4% faster.", "You take 10% less damage and deal 4% more.",
        };

        public static bool IsFaction(RelicId r) => r >= RelicId.WardensSeal && r < RelicId.Thorns;

        /// The boss (index into Game.BossCycle) whose faction a relic belongs to; -1 for a classic relic.
        public static int FactionOf(RelicId r)
        {
            if (!IsFaction(r))
                return -1;
            int n = (int)r - Classic;
            for (int f = 0; f < PerFaction.Length; f++)
            {
                if (n < PerFaction[f])
                    return f;
                n -= PerFaction[f];
            }
            return -1;
        }

        /// A faction's i-th relic.
        public static RelicId OfFaction(int faction, int i)
        {
            int n = Classic;
            for (int f = 0; f < faction && f < PerFaction.Length; f++)
                n += PerFaction[f];
            return (RelicId)(n + i);
        }

        public static string Name(RelicId r) => r == RelicId.WallBounce ? "Wall Bounce" : r == RelicId.Backup ? "Backup" : r == RelicId.HallwayRunner ? "Hallway Runner" : r == RelicId.Thorns ? "Thorns" : IsFaction(r) ? FactionNames[(int)r - Classic] : "";

        public static string Desc(RelicId r) => r == RelicId.WallBounce ? "Your shots bounce off walls and pillars once, keeping 70% of their damage."
            : r == RelicId.Backup ? "The first time you would go down, you get straight back up with half your health. Then it's gone."
            : r == RelicId.HallwayRunner ? "You move 25% faster in hallways (3 tiles wide or less)." : r == RelicId.Thorns ? "Enemies that hurt you by touch take 14 damage back." : IsFaction(r) ? FactionDescs[(int)r - Classic] : "";

        /// A faction relic's stat changes, applied to a player's stats (Game.Recalc, on every peer).
        public static void Apply(RelicId r, Stats s)
        {
            switch (r)
            {
                case RelicId.WardensSeal: s.DamageTaken *= 0.88f; break;
                case RelicId.BroodCrown: s.FireRateMult *= 1.12f; break;
                case RelicId.RoyalJelly: s.Regen += 1.5f; break;
                case RelicId.Stinger: s.DmgMult *= 1.10f; break;
                case RelicId.TwinScythe: s.DmgMult *= 1.08f; s.Pierce += 1; break;
                case RelicId.DuskLantern: s.SpeedMult *= 1.08f; break;
                case RelicId.EventHorizon: s.CdMult *= 0.88f; break;
                case RelicId.GravityWell: s.Pierce += 1; break;
                case RelicId.StoneHeart: s.BonusHp += 30f; break;
                case RelicId.SilverFoil: s.FireRateMult *= 1.10f; s.DmgMult *= 1.05f; break;
                case RelicId.DuelGem: s.DmgMult *= 1.12f; s.DamageTaken *= 1.08f; break;
                case RelicId.Phylactery: s.BonusHp += 20f; s.Regen += 1f; break;
                case RelicId.GraveFang: s.DmgMult *= 1.10f; break;
                case RelicId.ColdHeart: s.DamageTaken *= 0.92f; s.BonusHp += 10f; break;
                case RelicId.StormCoil: s.CdMult *= 0.90f; break;
                case RelicId.StaticCell: s.FireRateMult *= 1.08f; s.SpeedMult *= 1.05f; break;
                case RelicId.MawTooth: s.Regen += 2f; s.DmgMult *= 1.05f; break;
                case RelicId.BlackSun: s.DmgMult *= 1.12f; break;
                case RelicId.CoronaSpiral: s.CdMult *= 0.90f; s.SpeedMult *= 1.04f; break;
                case RelicId.UmbraShard: s.DamageTaken *= 0.90f; s.DmgMult *= 1.04f; break;
            }
        }

        /// The relic a node starts with: rolled from its spot (the same on every machine).
        public static RelicId ForNode(MapNode n)
        {
            if (n.Rule > 0 && n.Rule <= Count)
                return (RelicId)(n.Rule - 1);  // a faction relic node: PlaceRelicNodes set it
            uint h = (uint)(n.X * 73856093 ^ n.Y * 19349663);
            int k = (int)(h % (uint)(Classic + 1));
            return k < Classic ? (RelicId)k : RelicId.Thorns;
        }

        /// The relic a node holds now: the one a player left in it, else the one rolled from its spot.
        public static RelicId Held(MapNode n) => n.Drop != 0 && n.Drop <= Count ? (RelicId)(n.Drop - 1) : ForNode(n);

        /// Whether a point is in a hallway of this world: its box is 3 tiles wide or less (Hallway Runner).
        public static bool InHall(World w, V2 at)
        {
            if (w == null || w.Areas.Length == 0)
                return false;
            var a = w.Areas[w.AreaAt(at)];
            return a.Contains(at) && Math.Min(a.Size.X, a.Size.Y) <= HallWidth + 1f;
        }
    }

    public partial class Game
    {
        /// Host: player id -> the relic node (index) they just swapped at; they can take from it again only after walking out of its reach.
        readonly Dictionary<int, int> relicSwapped = new Dictionary<int, int>();

        /// Host, every tick of a wave (from TickNodes): a claimed relic node gives its relic to the first player to walk up to it (one who
        /// doesn't already hold that relic). That player's own relic, if any, is left in the node in its place; with no old relic the node is spent.
        void TickRelicNodes()
        {
            if (relicSwapped.Count > 0)
            {
                var gone = new List<int>();
                foreach (var kv in relicSwapped)
                {
                    Player sp;
                    if (!Players.TryGetValue(kv.Key, out sp) || kv.Value >= Nodes.Count || !sp.Pos.CloserThan(NodeSpot(kv.Value), Relics.TakeReach + sp.Def.BodyR + 12f))
                        gone.Add(kv.Key);
                }
                foreach (var id in gone)
                    relicSwapped.Remove(id);
            }
            for (int i = 0; i < Nodes.Count; i++)
            {
                var nd = Nodes[i];
                if (nd.Kind != NodeKind.Relic || nd.Spent || !Expansion.NodeClaimed(Expanded, nd))
                    continue;
                var r = Relics.Held(nd);
                Player taker = null;
                foreach (var p in Players.Values)
                {
                    int lockedAt;
                    if (p.Downed || p.Relic == r || (relicSwapped.TryGetValue(p.Id, out lockedAt) && lockedAt == i) || !p.Pos.CloserThan(NodeSpot(i), Relics.TakeReach + p.Def.BodyR))
                        continue;
                    taker = p;
                    break;
                }
                if (taker == null)
                    continue;
                var old = taker.Relic;
                taker.Relic = r;
                Recalc(taker);  // (a faction relic changes stats)
                if (old == RelicId.None)
                {
                    nd.Spent = true;
                    nd.Drop = 0;
                }
                else
                {
                    nd.Drop = (byte)((int)old + 1);  // (their old relic stays in the node for anyone to take)
                    relicSwapped[taker.Id] = i;
                }
                Nodes[i] = nd;
                BroadcastFx(FxKind.Ring, NodeSpot(i), Rgba.Gold, 90f, 0.5f, Sfx.Pick);
                DoLog("player " + taker.Id + " took a relic: " + Relics.Name(r) + (old != RelicId.None ? " (left " + Relics.Name(old) + ")" : ""));
                BroadcastExpansion();
            }
        }

        /// Host, from PlaceFloatingRooms each boss cycle: the 1-3 relic nodes of that cycle's boss faction (Game.BossCycle order), each holding one of its
        /// relics. They are guaranteed: the waiting-nodes cap doesn't apply. Returns whether any was placed.
        bool PlaceRelicNodes(int cycle)
        {
            int faction = cycle % BossCycle.Length;
            bool added = false;
            for (int i = 0; i < Relics.PerFaction[faction]; i++)
            {
                // (guaranteed: if the outer tier has no room left, try nearer tiers, then each side of the hub)
                MapNode? nd = null;
                for (int attempt = 0; attempt < 7 && !nd.HasValue; attempt++)
                    nd = Expansion.GridNodeFor(expandSeed, cycle, Expanded, Nodes, NodeKind.Relic, attempt < 3 ? -1 : attempt - 3, attempt < 3 ? Expansion.CycleTier(NodeKind.Relic, cycle) - attempt : 0);
                if (!nd.HasValue)
                    continue;
                var n = nd.Value;
                n.Rule = (byte)((int)Relics.OfFaction(faction, i) + 1);
                Nodes.Add(n);
                added = true;
                DoLog("a relic node appears at " + n.X + "," + n.Y + ": " + Relics.Name(Relics.OfFaction(faction, i)));
            }
            return added;
        }

        /// Host, as a player would go down (DownIfDead): Backup gets them straight back up once, then the relic is gone.
        bool UseBackup(Player p)
        {
            if (p.Relic != RelicId.Backup)
                return false;
            p.Relic = RelicId.None;
            p.Hp = Math.Max(1f, Math.Min(p.HealCap, p.MaxHp * Relics.BackupHp));
            p.InvulnT = Math.Max(p.InvulnT, Relics.BackupInvuln);
            BroadcastFx(FxKind.Ring, p.Pos, Rgba.Gold, 120f, 0.6f, Sfx.Revive);
            DoLog("Backup gets player " + p.Id + " back up");
            return true;
        }

        /// Your own game, moving you: Hallway Runner's speed in a hallway.
        float RelicSpeedAt(Player p) => p.Relic == RelicId.HallwayRunner && Relics.InHall(CurrentWorld, p.Pos) ? Relics.HallSpeed : 1f;

        readonly List<Bullet> bounced = new List<Bullet>();

        /// Host, from Collide: a player's shot touching a wall or pillar bounces off it if its owner holds Wall Bounce (Relics.cs).
        /// Returns true when it bounced (the shot flies on); false lets the wall stop it as usual.
        bool WallBounce(Bullet b)
        {
            bool fang = FangBouncesOffWalls(b);  // a Dragon Fang on its chain always bounces (DragonFang.cs)
            if (!fang && (b.WallHops >= Relics.WallBounces || Defs.IsEnemyShot(b.Kind) || b.Sticks))
                return false;
            Player p;
            if (!fang && (!Players.TryGetValue(b.Owner, out p) || p.Relic != RelicId.WallBounce))
                return false;
            var d = ActiveLayout;
            foreach (var s in d.Solids)
            {
                if (!Counts(s) || !Layouts.Inside(s, b.Pos, b.Radius))
                    continue;
                V2 n;
                if (s.Round)
                {
                    n = (b.Pos - s.Pos).Normalized();
                    if (n.IsZero)
                        n = -b.Vel.Normalized();
                    b.Pos = s.Pos + n * (s.R + b.Radius + 1f);
                }
                else
                {
                    float dx = s.HW + b.Radius - System.Math.Abs(b.Pos.X - s.Pos.X), dy = s.HH + b.Radius - System.Math.Abs(b.Pos.Y - s.Pos.Y);
                    if (dx < dy)
                    {
                        n = new V2(b.Pos.X < s.Pos.X ? -1f : 1f, 0f);
                        b.Pos = new V2(s.Pos.X + n.X * (s.HW + b.Radius + 1f), b.Pos.Y);
                    }
                    else
                    {
                        n = new V2(0f, b.Pos.Y < s.Pos.Y ? -1f : 1f);
                        b.Pos = new V2(b.Pos.X, s.Pos.Y + n.Y * (s.HH + b.Radius + 1f));
                    }
                }
                float into = b.Vel.X * n.X + b.Vel.Y * n.Y;
                if (into < 0f)
                    b.Vel -= n * (2f * into);  // mirror it off the face
                if (!fang)
                {
                    b.WallHops++;
                    b.Dmg *= Relics.WallBounceDmg;
                }
                b.LastHit = 0;   // it may come back for what it just hit
                b.Homing = 0f;   // (homing would steer it straight back into the wall)
                b.Pulled = true; // the host sends its course from now on (GameNet.cs SendSteer)
                if (fang)
                    FangWalled(b);  // (it picks its next enemy)
                bounced.Add(b);
                AddFx(FxKind.Ring, b.Pos, Defs.BulletColor(b.Kind), 7f, 0.18f);  // a spark on the wall
                return true;
            }
            return false;
        }

        /// Host, after Collide: every game gets the new course of a shot that just bounced at once, not at the next snapshot.
        void SendBounces()
        {
            if (bounced.Count == 0)
                return;
            SendSteerOf(bounced);
            bounced.Clear();
        }
    }
}
