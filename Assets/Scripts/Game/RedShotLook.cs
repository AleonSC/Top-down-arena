namespace Arena
{
    /// A boss's red aimed shot (Defs.PierceShot, unblockable): a barbed, black-cored spearhead with a red streak, pointing
    /// where it flies. Its glow is a ring round it (ShotLooks.cs ShotGlow), so the black heart stays dark. Preview REDSHOT=1.
    public partial class ArenaApp
    {
        static readonly Rgba RedHot = new Rgba(1f, 0.12f, 0.08f), RedCore = new Rgba(0.08f, 0f, 0f, 1f);

        void DrawRedShot(Bullet b)
        {
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), side = dir.Orthogonal();
            float r = b.Radius;
            V2 tip = b.Pos + dir * (r * 2.4f), back = b.Pos - dir * (r * 1.6f);
            TaperTrail(b, 12, r * 1.4f, RedHot, 0.7f);                                                  // the streak (ShotLooks.cs)
            SetColor(RedHot.WithAlpha(0.3f));
            Tri(tip + dir * 4f, back + side * (r + 4f), back - side * (r + 4f));                        // a haze round the head
            SetColor(RedHot);
            Tri(tip, back + side * (r + 1f), back - side * (r + 1f));                                   // the head
            Tri(back + side * (r + 1f), back + side * (r + 4f) - dir * 5f, back + side * (r * 0.3f));  // barbs
            Tri(back - side * (r + 1f), back - side * (r + 4f) - dir * 5f, back - side * (r * 0.3f));
            SetColor(RedCore);
            Tri(b.Pos + dir * (r * 1.3f), back + side * (r * 0.45f), back - side * (r * 0.45f));       // the black heart
        }
    }
}
