using System.Collections.Generic;

namespace Arena
{
    /// "What does this hit?" in one place (Docs/Targeting.md). A player's attack asks for the foes in a shape and hurts each one:
    ///   foreach (var e in FoesInCircle(b.Pos, b.Splash)) HurtEnemy(e, b.Dmg, b.Owner);
    /// A foe is a Targetable enemy (spawned in, alive) whose body touches the shape (its radius counts). Pass `cover` (where the hit
    /// comes from) to skip one that blocks it from there: a boss changing phase, a Gutworm underground, a Duelist's guard (BlocksShot).
    /// Every query walks the enemy list as it goes, allocating nothing, so they nest; like any loop over Enemies, don't add or remove
    /// enemies inside one (HurtEnemy is fine: kills are swept later).
    public partial class Game
    {
        /// Host: every foe, for an attack with its own shape (a spray, a wall, a ring): it tests each one itself.
        public FoeQuery AllFoes(V2? cover = null) => new FoeQuery(this, FoeShape.All, default(V2), default(V2), 0f, 0f, cover);

        /// Host: foes touching the circle round `c`.
        public FoeQuery FoesInCircle(V2 c, float r, V2? cover = null) => new FoeQuery(this, FoeShape.Circle, c, default(V2), r, 0f, cover);

        /// Host: foes touching the strip from `a` to `b`, `halfWidth` either side of it (a lane, a thrust, a beam).
        public FoeQuery FoesInBox(V2 a, V2 b, float halfWidth, V2? cover = null) => new FoeQuery(this, FoeShape.Box, a, b, halfWidth, 0f, cover);

        /// Host: foes within `reach` of `apex` (their body counts) whose middle is within `half` radians of `aim` (a swing, a blast cone).
        public FoeQuery FoesInCone(V2 apex, float aim, float half, float reach, V2? cover = null) => new FoeQuery(this, FoeShape.Cone, apex, V2.FromAngle(aim), reach, half, cover);

        /// The one test every query uses: is this enemy a foe in that shape?
        internal bool FoeIn(Enemy e, FoeShape shape, V2 a, V2 b, float size, float half, V2? cover)
        {
            if (!Targetable(e))
                return false;
            switch (shape)
            {
                case FoeShape.All:
                    break;
                case FoeShape.Circle:
                    if (e.Pos.FartherThan(a, size + e.Radius))
                        return false;
                    break;
                case FoeShape.Box:
                    if (e.Pos.DistanceToSegment(a, b) > size + e.Radius)
                        return false;
                    break;
                case FoeShape.Cone:
                    if (e.Pos.FartherThan(a, size + e.Radius))
                        return false;
                    V2 to = e.Pos - a;
                    if (!to.IsZero && System.Math.Abs(MathX.WrapPi(to.Angle - b.Angle)) > half)
                        return false;
                    break;
            }
            return !cover.HasValue || !BlocksShot(e, cover.Value);
        }
    }

    public enum FoeShape : byte { All, Circle, Box, Cone }

    /// A query over the enemies (Game.FoesInCircle, FoesInBox, FoesInCone): use it in a foreach.
    public struct FoeQuery
    {
        readonly Game game;
        readonly FoeShape shape;
        readonly V2 a, b;
        readonly float size, half;
        readonly V2? cover;

        internal FoeQuery(Game game, FoeShape shape, V2 a, V2 b, float size, float half, V2? cover)
        {
            this.game = game;
            this.shape = shape;
            this.a = a;
            this.b = b;
            this.size = size;
            this.half = half;
            this.cover = cover;
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

            internal Enumerator(FoeQuery q)
            {
                this.q = q;
                it = q.game.Enemies.Values.GetEnumerator();
            }

            public Enemy Current => it.Current;

            public bool MoveNext()
            {
                while (it.MoveNext())
                    if (q.game.FoeIn(it.Current, q.shape, q.a, q.b, q.size, q.half, q.cover))
                        return true;
                return false;
            }
        }
    }
}
