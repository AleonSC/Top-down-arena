using System.Collections.Generic;

namespace Arena
{
    /// "What does this hit?" in one place (Docs/Targeting.md). A player's attack asks for the foes in a shape and hurts each one:
    ///   foreach (var e in FoesInCircle(b.Pos, b.Splash, by: b.Owner)) HurtEnemy(e, b.Dmg, b.Owner);
    /// A foe is a Targetable enemy (spawned in, alive) whose body touches the shape (its radius counts). Pass `cover` (where the hit
    /// comes from) to skip one that blocks it from there: a boss changing phase, a Gutworm underground, a Duelist's guard (BlocksShot).
    /// `by` is the attacking player's id: with PvP on (PvpOn: the Hangout) the query also gives every other player in the shape, as
    /// their stand-in enemy (StandOf, Enemy.Stand), and HurtEnemy sends what it takes to that player (PvpHurt, Pvp.cs).
    /// Every query walks the enemy list as it goes, allocating nothing, so they nest; like any loop over Enemies, don't add or remove
    /// enemies inside one (HurtEnemy is fine: kills are swept later).
    public partial class Game
    {
        /// Host: every foe, for an attack with its own shape (a spray, a wall, a ring): it tests each one itself.
        public FoeQuery AllFoes(V2? cover = null, int by = 0) => new FoeQuery(this, FoeShape.All, default(V2), default(V2), 0f, 0f, cover, by);

        /// Host: foes touching the circle round `c`.
        public FoeQuery FoesInCircle(V2 c, float r, V2? cover = null, int by = 0) => new FoeQuery(this, FoeShape.Circle, c, default(V2), r, 0f, cover, by);

        /// Host: foes touching the strip from `a` to `b`, `halfWidth` either side of it (a lane, a thrust, a beam).
        public FoeQuery FoesInBox(V2 a, V2 b, float halfWidth, V2? cover = null, int by = 0) => new FoeQuery(this, FoeShape.Box, a, b, halfWidth, 0f, cover, by);

        /// Host: foes within `reach` of `apex` (their body counts) whose middle is within `half` radians of `aim` (a swing, a blast cone).
        public FoeQuery FoesInCone(V2 apex, float aim, float half, float reach, V2? cover = null, int by = 0) => new FoeQuery(this, FoeShape.Cone, apex, V2.FromAngle(aim), reach, half, cover, by);

        /// The one test every query uses: is this enemy a foe in that shape?
        internal bool FoeIn(Enemy e, FoeShape shape, V2 a, V2 b, float size, float half, V2? cover)
        {
            if (!Targetable(e) || !InShape(e, shape, a, b, size, half))
                return false;
            return !cover.HasValue || !BlocksShot(e, cover.Value);
        }

        static bool InShape(Enemy e, FoeShape shape, V2 a, V2 b, float size, float half)
        {
            switch (shape)
            {
                case FoeShape.Circle:
                    return !e.Pos.FartherThan(a, size + e.Radius);
                case FoeShape.Box:
                    return e.Pos.DistanceToSegment(a, b) <= size + e.Radius;
                case FoeShape.Cone:
                    return MathX.InCone(a, b.Angle, half, size, e.Pos, e.Radius, inner: -1f, padBody: false);
                default:
                    return true;
            }
        }

        /// PvP: player q's stand-in, if attacker `by` can hit them in that shape (up, not mid-beam, not themselves), placed on them.
        internal Enemy StandIn(Player q, int by, FoeShape shape, V2 a, V2 b, float size, float half)
        {
            if (q.Id == by || q.Downed || q.WarpT > 0f)
                return null;
            var s = StandOf(q);
            return InShape(s, shape, a, b, size, half) ? s : null;
        }
    }

    public enum FoeShape : byte { All, Circle, Box, Cone }

    /// A query over the enemies (Game.FoesInCircle, FoesInBox, FoesInCone, AllFoes): use it in a foreach.
    public struct FoeQuery
    {
        readonly Game game;
        readonly FoeShape shape;
        readonly V2 a, b;
        readonly float size, half;
        readonly V2? cover;
        readonly int by;

        internal FoeQuery(Game game, FoeShape shape, V2 a, V2 b, float size, float half, V2? cover, int by)
        {
            this.game = game;
            this.shape = shape;
            this.a = a;
            this.b = b;
            this.size = size;
            this.half = half;
            this.cover = cover;
            this.by = by;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        /// How many foes are in it.
        public int Count()
        {
            int n = 0;
            foreach (var e in this)
                n++;
            return n;
        }

        public struct Enumerator
        {
            readonly FoeQuery q;
            Dictionary<int, Enemy>.ValueCollection.Enumerator it;
            SortedDictionary<int, Player>.ValueCollection.Enumerator players;
            bool onPlayers;
            readonly bool pvp;
            Enemy current;

            internal Enumerator(FoeQuery q)
            {
                this.q = q;
                it = q.game.Enemies.Values.GetEnumerator();
                players = q.game.Players.Values.GetEnumerator();
                onPlayers = false;
                pvp = q.by > 0 && q.game.PvpOn;
                current = null;
            }

            public Enemy Current => current;

            public bool MoveNext()
            {
                if (!onPlayers)
                {
                    while (it.MoveNext())
                        if (q.game.FoeIn(it.Current, q.shape, q.a, q.b, q.size, q.half, q.cover))
                        {
                            current = it.Current;
                            return true;
                        }
                    onPlayers = true;
                }
                if (!pvp)
                    return false;
                while (players.MoveNext())
                {
                    var s = q.game.StandIn(players.Current, q.by, q.shape, q.a, q.b, q.size, q.half);
                    if (s != null)
                    {
                        current = s;
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
