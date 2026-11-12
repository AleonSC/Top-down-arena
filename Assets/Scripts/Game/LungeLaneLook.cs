using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The shared lunge telegraph (Docs/EnemyReadability.md section 2, option A "Lane" of Docs/Mockups/lunge-telegraph.html):
    /// the Lunger, Spike Block, Charger and Riot Shield all show the same thing while they wind up to lunge. A strip on the
    /// floor, a body wide and as long as the lunge reaches, in the Hunter red; it fills out from the enemy over the wind-up
    /// and flashes white for the last 0.1 s. It replaces their four older looks (a blinking line, a halo, a flashing slab).
    /// The wind-up timer isn't sent, so each client times the fill from the moment it first sees the wind-up. Front end only.
    public partial class ArenaApp
    {
        const float LaneFlash = 0.1f;
        readonly Dictionary<int, float> laneSince = new Dictionary<int, float>();

        /// Is this one of the lungers; if so its wind-up in seconds, how far it lunges, and whether it is winding up now.
        static bool LungeInfo(Enemy e, out float windUp, out float reach, out bool winding)
        {
            windUp = reach = 0f;
            winding = false;
            switch (e.Type)
            {
                case EnemyType.Lunger: case EnemyType.Shiv: windUp = 0.6f; reach = 230f; winding = e.Phase == 1; return true;
                case EnemyType.SpikeBlock: windUp = Game.SpikeWindUp; reach = 175f; winding = e.Phase == 1; return true;
                case EnemyType.Charger: windUp = 0.7f; reach = 260f; winding = e.Phase == 1; return true;
                case EnemyType.RiotShield:
                    windUp = Game.RiotWindup; reach = Game.RiotLungeSpeed * Game.RiotLunge + 20f;
                    winding = Game.RiotState(e) == Game.RiotWinding;
                    return true;
            }
            return false;
        }

        /// DrawEnemyUnderlay: the lane, while the enemy winds up. Returns true if this enemy is one of the lungers.
        bool DrawLungeLane(Enemy e, V2 pos, float r, float t)
        {
            if (!LungeInfo(e, out float windUp, out float reach, out bool winding))
                return false;
            if (!winding)
            {
                laneSince.Remove(e.Id);
                return true;
            }
            if (!laneSince.TryGetValue(e.Id, out float since))
            {
                if (laneSince.Count > 128)
                    laneSince.Clear();  // dead enemies leave entries behind
                since = laneSince[e.Id] = t;
            }
            float p = MathX.Clamp01((t - since) / windUp);
            V2 dir = V2.FromAngle(e.Facing), side = dir.Orthogonal();
            float half = r * 0.9f;
            Rgba red = JobColors[(int)EnemyJob.Hunter];
            V2 a = pos + dir * r, b = pos + dir * (r + reach);
            // The strip, faint, with a brighter edge; then the part that has filled so far.
            SetColor(red.WithAlpha(0.08f));
            Quad(a + side * half, b + side * half, b - side * half, a - side * half);
            Line(a + side * half, b + side * half, 1.5f, red.WithAlpha(0.45f));
            Line(a - side * half, b - side * half, 1.5f, red.WithAlpha(0.45f));
            bool flash = (1f - p) * windUp < LaneFlash;
            V2 f = pos + dir * (r + reach * p);
            SetColor(flash ? new Rgba(1f, 1f, 1f, 0.85f) : red.WithAlpha(0.45f));
            Quad(a + side * half, f + side * half, f - side * half, a - side * half);
            return true;
        }
    }
}
