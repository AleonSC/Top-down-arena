using System.Collections.Generic;

namespace Arena
{
    /// A relic: a map-themed modifier, one slot per player (a new one replaces the old). Earned by building a room round a
    /// relic node on the Expanding arena (Docs/Nodes.md, "Relic nodes"). Separate from the bosses' weapon mods.
    public enum RelicId : byte { WallBounce, None }

    public static class Relics
    {
        public const int WallBounces = 1;        // Wall Bounce: bounces off walls and pillars per shot
        public const float WallBounceDmg = 0.7f; // the damage a shot keeps each bounce
        public const float TakeReach = 46f;      // how close a player walks to a relic node to take it

        public static string Name(RelicId r) => r == RelicId.WallBounce ? "Wall Bounce" : "";

        public static string Desc(RelicId r) => r == RelicId.WallBounce
            ? "Your shots bounce off walls and pillars once, keeping 70% of their damage." : "";

        /// The relic a node holds: one kind for now (later rolled from the node's spot).
        public static RelicId ForNode(MapNode n) => RelicId.WallBounce;
    }

    public partial class Game
    {
        /// Host, every tick of a wave (from TickNodes): a claimed relic node is taken by the first player to walk up to it. Everyone in the
        /// party gets the relic in their slot (replacing the old one) and the node is spent.
        void TickRelicNodes()
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                var nd = Nodes[i];
                if (nd.Kind != NodeKind.Relic && !(Expansion.RuleHoldsRelic(nd) && nd.Cleared) || nd.Spent || !Expansion.NodeClaimed(Expanded, nd))
                    continue;
                bool touched = false;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.Pos.DistanceTo(nd.Pos) < Relics.TakeReach + p.Def.BodyR)
                        touched = true;
                if (!touched)
                    continue;
                var r = Relics.ForNode(nd);
                foreach (var p in Players.Values)
                    p.Relic = r;
                nd.Spent = true;
                Nodes[i] = nd;
                BroadcastFx(FxKind.Ring, nd.Pos, new Rgba(1f, 0.82f, 0.3f), 90f, 0.5f, Sfx.Pick);
                DoLog("a relic node was taken: " + Relics.Name(r));
                BroadcastExpansion();
            }
        }

        readonly List<Bullet> bounced = new List<Bullet>();

        /// Host, from Collide: a player's shot touching a wall or pillar bounces off it if its owner holds Wall Bounce (Relics.cs).
        /// Returns true when it bounced (the shot flies on); false lets the wall stop it as usual.
        bool WallBounce(Bullet b)
        {
            if (b.WallHops >= Relics.WallBounces || Defs.IsEnemyShot(b.Kind) || b.Sticks || b.DropsSpear || b.Rush)
                return false;
            Player p;
            if (!Players.TryGetValue(b.Owner, out p) || p.Relic != RelicId.WallBounce)
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
                b.WallHops++;
                b.Dmg *= Relics.WallBounceDmg;
                b.LastHit = 0;   // it may come back for what it just hit
                b.Homing = 0f;   // (homing would steer it straight back into the wall)
                b.Pulled = true; // the host sends its course from now on (GameNet.cs SendSteer)
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
