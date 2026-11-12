using Arena;

namespace Arena
{
    /// The Warden's prisoner reskins (Docs/Approaches.md): the Thug is a Brute dragging a ball and chain, the Shiv a Lunger
    /// with a taped shank held out front. Both keep their base enemy's body shape, spikes and lunge lane.
    public partial class ArenaApp
    {
        static readonly Rgba PrisonInk = new Rgba(0.15f, 0.12f, 0.16f, 0.9f), PrisonSteel = new Rgba(0.82f, 0.86f, 0.9f);

        void DrawThugDetails(Enemy e, V2 pos, float r, Rgba col)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            V2 ball = pos - f * r * 2.1f;
            Line(pos - f * r * 0.7f, ball, 3f, PrisonInk);  // the chain
            Circle(ball, r * 0.5f, PrisonInk, 12);  // the ball
            Circle(ball - s * 2f - f * 2f, r * 0.18f, new Rgba(0.5f, 0.5f, 0.55f), 8);  // its shine
            Circle(pos + f * r * 0.95f + s * r * 0.6f, r * 0.34f, PrisonSteel, 10);  // cuffed fists
            Circle(pos + f * r * 0.95f - s * r * 0.6f, r * 0.34f, PrisonSteel, 10);
            Line(pos + f * r * 0.2f - s * r * 0.45f, pos + f * r * 0.2f + s * r * 0.45f, 3f, PrisonInk);  // the brow
        }

        void DrawShivDetails(Enemy e, V2 pos, float r, Rgba col)
        {
            V2 f = V2.FromAngle(e.Facing);
            V2 tip = pos + f * r * 2.3f;
            Line(pos + f * r * 0.4f, tip, 3f, PrisonSteel);  // the blade
            Line(pos + f * r * 0.4f, pos + f * r * 1.0f, 5f, PrisonInk);  // the taped grip
            Circle(tip, 2.5f, new Rgba(1f, 1f, 1f, 0.9f), 6);
        }
    }
}
