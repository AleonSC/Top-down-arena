using UnityEngine;

namespace Arena
{
    /// Guardian's Oath's pick (Core/GuardiansOath.cs), your own Knight only: while the Oath waits for a click, a faint gold
    /// ring shows its reach, every ally in reach gets a soft ring, and the one under the cursor gets a bright, turning
    /// ring and a dashed tether from you: a click there grabs them and hauls them back to where you stand.
    public partial class ArenaApp
    {
        static readonly Rgba OathGold = new Rgba(1f, 0.85f, 0.4f);

        void DrawOathPick(Player me, float t)
        {
            float range = game.OathRangeFor(me);
            Arc(me.Disp, range, 1.5f, OathGold.WithAlpha(0.18f + 0.06f * Mathf.Sin(t * 3f)), 72);
            var pick = game.OathPickTarget(me, me.AimAt);
            foreach (var q in game.Players.Values)
            {
                if (q.Id == me.Id || q.Pos.FartherThan(me.Pos, range + q.Def.BodyR))
                    continue;
                float r = q.Def.BodyR + 10f;
                if (q != pick)
                {
                    Arc(q.Disp, r, 2f, OathGold.WithAlpha(0.6f), 24);
                    continue;
                }
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 9f);
                Circle(q.Disp, r + 2f * pulse, OathGold.WithAlpha(0.12f), 28);
                Arc(q.Disp, r + 2f * pulse, 3f, OathGold.WithAlpha(0.95f), 28);
                for (int k = 0; k < 4; k++)  // four brackets turning round them
                    Arc(q.Disp, r + 7f, 2f, OathGold.WithAlpha(0.8f), 6, t * 2f + k * MathX.Tau / 4f, 0.5f);
                V2 d = q.Disp - me.Disp;
                float len = d.Length;
                if (len < 1f)
                    continue;
                d /= len;
                for (float s = me.Def.BodyR + 6f - MathX.Fmod(t * 50f, 16f); s < len - r; s += 16f)  // the tether's dashes creep toward them
                {
                    float a = Mathf.Max(me.Def.BodyR + 6f, s), b = Mathf.Min(len - r, s + 8f);
                    if (b > a)
                        Line(me.Disp + d * a, me.Disp + d * b, 2.5f, OathGold.WithAlpha(0.85f));
                }
            }
        }
    }
}
