using UnityEngine;

namespace Arena
{
    /// A small marker under each regular enemy that says what its job is (Docs/EnemyReadability.md, look B "Underline"):
    /// a bar in the job's colour with a shape on it. Shape carries the job, colour backs it up. Pressure = dot, Hunter =
    /// chevron, Zoner = ring, Disruptor = diamond, Anchor = square. Nothing else is drawn below a body, so it can't
    /// clash with the elite ring, the health bar or the status icons. The job comes from Game.JobOf (never sent).
    /// Bosses, spawning enemies, zombies, mines and eggs get none. Front end only.
    public partial class ArenaApp
    {
        static readonly Rgba[] JobColors =
        {
            new Rgba(1f, 0.60f, 0.24f),   // Pressure
            new Rgba(1f, 0.35f, 0.35f),   // Hunter
            new Rgba(1f, 0.85f, 0.29f),   // Zoner
            new Rgba(0.69f, 0.49f, 1f),   // Disruptor
            new Rgba(0.31f, 0.88f, 0.88f) // Anchor
        };

        bool HasJobMarker(Enemy e)
        {
            return !e.Def.IsBoss && !game.IsSpawning(e) && e.Hp > 0f && !Zombies.Is(e.Type)
                && e.Type != EnemyType.SapperMine && e.Type != EnemyType.HiveEgg && e.Type != EnemyType.HiveBomber && e.Type != EnemyType.Portal
                && e.Type != EnemyType.Gatekeeper && !FactionMinis.Is(e.Type) && e.Type != EnemyType.Shadow && e.Type != EnemyType.ReaperPillar
                && e.Type != EnemyType.MiniWell && e.Type != EnemyType.Implosion;  // (the Infall's summons: Infall.cs)
        }

        /// DrawEnemy, after the body: the job bar and shape just below it.
        void DrawJobMarker(Enemy e, V2 pos, float r)
        {
            if (!HasJobMarker(e))
                return;
            var job = Game.JobOf(e.Type);
            Rgba c = JobColors[(int)job].WithAlpha(0.9f);
            V2 at = pos + new V2(0f, r + 7f);
            float half = Mathf.Max(8f, r * 0.7f);
            Line(at + new V2(-half, 0f), at + new V2(half, 0f), 2f, c.WithAlpha(0.55f));
            V2 m = at + new V2(0f, 6f);
            switch (job)
            {
                case EnemyJob.Pressure: Polygon(m, 2.8f, 12, 0f, c); break;
                case EnemyJob.Hunter:
                    Line(m + new V2(-4f, -2.5f), m + new V2(0f, 2.5f), 2f, c);
                    Line(m + new V2(4f, -2.5f), m + new V2(0f, 2.5f), 2f, c);
                    break;
                case EnemyJob.Zoner: Arc(m, 3.4f, 1.8f, c, 16); break;
                case EnemyJob.Disruptor: Polygon(m, 3.8f, 4, 0f, c); break;
                default: Polygon(m, 3.6f, 4, MathX.Pi / 4f, c); break;
            }
        }
    }
}
