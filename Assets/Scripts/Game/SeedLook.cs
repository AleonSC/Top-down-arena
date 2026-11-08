namespace Arena
{
    /// The Treant's seed as a Thorn Dart (look C of Docs/Mockups/treant-rod-sounds-seeds.html): a thin green sliver with a bright tip, pointing
    /// along its path. Visual effects on adds a faint tail. Drawing only; the seed flies and hurts the same either way.
    public partial class ArenaApp
    {
        static readonly Rgba DartBody = new Rgba(0.31f, 0.6f, 0.22f);

        void DrawThornDart(Bullet b)
        {
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), side = dir.Orthogonal();
            if (Juicy)
                Line(b.Pos - dir * 15f, b.Pos - dir * 7f, 1.2f, DartBody.WithAlpha(0.4f));
            SetColor(DartBody);
            Tri(b.Pos - dir * 8f, b.Pos + side * 2.2f - dir, b.Pos - side * 2.2f - dir);
            SetColor(Defs.BulletColor(b.Kind));
            Tri(b.Pos + dir * 7f, b.Pos + side * 2.2f - dir, b.Pos - side * 2.2f - dir);
        }
    }
}
