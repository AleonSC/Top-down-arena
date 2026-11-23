using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Tank passive Ironhide (Ironhide.cs): five steel plates on a ring round the Tank, each a fifth of the shield.
    /// Full plates are bright, the one refilling grows along its arc, spent ones are faint outlines; while the refill
    /// waits the ring dims, and when the last plate goes the ring flashes and blows out.
    public partial class ArenaApp
    {
        static readonly Rgba IronSteel = new Rgba(0.62f, 0.85f, 1f);  // CrtStyle.Steel
        const int IronPlates = 5;
        readonly Dictionary<int, float> ironSeen = new Dictionary<int, float>();   // each Tank's shield last frame
        readonly Dictionary<int, float> ironBroke = new Dictionary<int, float>();  // when it last emptied (AnimT)

        void DrawIronhide(Player p, float t)
        {
            float now = Game.Ironhide(p);
            ironSeen.TryGetValue(p.Id, out float before);
            if (before > 0.5f && now <= 0.01f)
                ironBroke[p.Id] = t;
            ironSeen[p.Id] = now;
            if (p.Downed)
                return;
            float r = p.Def.BodyR + 13f;  // outside the body's own blue rim
            float frac = now / Game.IronhideMax;
            bool waiting = p.IronWait > 0f && now < Game.IronhideMax;
            float slot = MathX.Tau / IronPlates, gap = 0.22f;
            float spin = t * 0.15f;
            for (int i = 0; i < IronPlates; i++)
            {
                float a0 = spin + i * slot + gap / 2f, sweep = slot - gap;
                float fill = Mathf.Clamp01(frac * IronPlates - i);  // this plate's share: 1 full, 0 spent
                Arc(p.Disp, r, 6f, new Rgba(0.02f, 0.04f, 0.09f, 0.55f), 8, a0, sweep);  // a dark backing so it reads over the body and the floor
                Arc(p.Disp, r, 1.2f, IronSteel.WithAlpha(0.3f), 8, a0, sweep);  // the outline every plate keeps
                if (fill <= 0f)
                    continue;
                float alpha = fill >= 1f ? (waiting ? 0.65f : 0.95f) : 0.5f;
                Arc(p.Disp, r, 4f, IronSteel.WithAlpha(alpha), 8, a0, sweep * fill);
                if (fill >= 1f)  // a white glint along full plates
                    Arc(p.Disp, r + 1f, 1f, new Rgba(1f, 1f, 1f, waiting ? 0.25f : 0.5f), 8, a0 + sweep * 0.2f, sweep * 0.6f);
            }
            if (ironBroke.TryGetValue(p.Id, out float bt) && t - bt < 0.45f)  // broken: a flash and the plates flung out
            {
                float f = (t - bt) / 0.45f;
                Arc(p.Disp, r + 14f * f, 2.5f * (1f - f), IronSteel.WithAlpha(0.8f * (1f - f)), 32);
                for (int i = 0; i < IronPlates; i++)
                {
                    V2 d = V2.FromAngle(spin + (i + 0.5f) * slot);
                    V2 at = p.Disp + d * (r + 22f * f);
                    Line(at - d.Orthogonal() * 4f, at + d.Orthogonal() * 4f, 2f, IronSteel.WithAlpha(0.7f * (1f - f)));
                }
            }
        }
    }
}
