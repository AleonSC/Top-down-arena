using UnityEngine;

namespace Arena
{
    /// The Colossus's shell blasting off when a stagger breaks (Colossus.cs BreakColShell): two layers of slabs from
    /// its old body (the outer ring first, the inner a moment later) fly out, spin up off the floor, skid to a stop
    /// and lie as rubble under everything until they fade (ColShardLife). All from the phase change's clock (TransT),
    /// so every game draws the same shards: in the air over everyone, then on the floor under everyone.
    public partial class ArenaApp
    {
        const int ColShardsPerLayer = 22;
        const float ColShardLife = 2f, ColShardAir = 0.9f;

        /// air: the shards still up (over everyone, from DrawColossusAir); otherwise the landed ones (DrawColossusFloor).
        void DrawColossusShell(Enemy e, ColossusState c, float t, bool air)
        {
            if (e == null || c == null || c.TransT <= 0f)
                return;
            float since = Game.ColTransTime - c.TransT;
            if (since > ColShardLife + 0.1f)
                return;
            float r0 = e.Def.Radius * Game.ColSizes[(int)c.Stage];  // its size before the blast (the stage changes as TransT ends)
            bool charred = c.Stage >= ColStage.MoltenCore;
            Rgba face = charred ? CharBase : c.Stage == ColStage.Crumbling ? SandBase : SandLite, edge = charred ? CharDark : SandDark;
            for (int layer = 0; layer < 2; layer++)
            {
                float rIn = layer == 0 ? 0.72f : 0.4f, rOut = layer == 0 ? 1.12f : 0.72f, delay = layer * 0.08f;
                for (int j = 0; j < ColShardsPerLayer; j++)
                {
                    float seed = layer * 100f + j, tt = since - delay;
                    if (tt < 0f || tt > ColShardLife || (tt < ColShardAir) != air)
                        continue;
                    float span = MathX.Tau / ColShardsPerLayer, a = span * (j + 0.5f) + (TH(seed) - 0.5f) * 0.12f;
                    float speed = layer == 0 ? 360f + TH(seed * 1.7f) * 260f : 240f + TH(seed * 1.7f) * 180f;
                    float go = speed * (1f - Mathf.Exp(-3f * tt)) / 3f, spin = (TH(seed * 2.3f) - 0.5f) * 18f * (1f - Mathf.Exp(-3f * tt)) / 3f;
                    float up = tt < ColShardAir ? Mathf.Sin(tt / ColShardAir * Mathf.PI) * (40f + 40f * TH(seed * 3.1f)) * Mathf.Sqrt(r0 / 54f) : 0f;
                    float alpha = Mathf.Clamp01((ColShardLife - tt) / 0.5f), grow = 1f + 0.5f * up / 80f;
                    float rMid = r0 * (rIn + rOut) * 0.5f;
                    V2 mid = e.Disp + V2.FromAngle(a) * (rMid + go);
                    // its four corners, round its own middle, turned by its spin
                    var q = new V2[4];
                    for (int k = 0; k < 4; k++)
                    {
                        float ca = a + (k == 0 || k == 3 ? -span / 2f : span / 2f), cr = r0 * (k < 2 ? rOut : rIn);
                        V2 rel = V2.FromAngle(ca) * cr - V2.FromAngle(a) * rMid;
                        rel = V2.FromAngle(rel.Angle + spin) * (rel.Length * grow);
                        q[k] = mid + rel;
                    }
                    V2 lift = new V2(0f, -up);
                    if (air)  // its shadow on the floor below it
                    {
                        SetColor(new Rgba(0f, 0f, 0f, 0.3f * alpha));
                        V2 sh = new V2(up * 0.4f, up * 0.5f);
                        Quad(q[0] + sh, q[1] + sh, q[2] + sh, q[3] + sh);
                    }
                    SetColor(edge.WithAlpha(alpha));
                    Quad(q[0] + lift, q[1] + lift, q[2] + lift, q[3] + lift);
                    V2 c0 = mid + lift;
                    SetColor(face.WithAlpha(alpha));
                    Quad(V2.Lerp(c0, q[0] + lift, 0.78f), V2.Lerp(c0, q[1] + lift, 0.78f), V2.Lerp(c0, q[2] + lift, 0.78f), V2.Lerp(c0, q[3] + lift, 0.78f));
                    if (charred)  // a seam of lava along its inner edge
                        Line(q[2] + lift, q[3] + lift, 2f, LavaHot.WithAlpha(0.8f * alpha));
                }
            }
        }
    }
}
