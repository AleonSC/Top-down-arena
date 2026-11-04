using UnityEngine;

namespace Arena
{
    /// The Tank Soldier's Fortress dome in style B, "Bulwark wall" (Docs/Mockups/fortress-dome.html, the owner's pick): a ring
    /// of gold armour plates around the edge with a faint glass dome inside. Plates flash white where an enemy shot is
    /// about to touch the rim. It drops in over a quarter second and blinks for the last two seconds.
    /// Drawing only: everything comes from the replicated DomeT and DomePos and the bullets this screen already has.
    public partial class ArenaApp
    {
        const int DomePlates = 28;
        readonly System.Collections.Generic.Dictionary<int, Vector2> domeSeen = new System.Collections.Generic.Dictionary<int, Vector2>();  // per player: the dome's time at its cast and last frame

        void DrawFortressDome(Player p, float t)
        {
            float peak, last;
            domeSeen.TryGetValue(p.Id, out var seen);
            peak = seen.x; last = seen.y;
            if (p.DomeT > last + 0.5f || peak <= 0f)
                peak = p.DomeT;  // a fresh cast
            domeSeen[p.Id] = new Vector2(peak, p.DomeT);
            float age = Mathf.Max(0f, peak - p.DomeT);
            float drop = Mathf.Clamp01(age / 0.25f);                      // plates slam in
            float fade = p.DomeT < 2f ? 0.55f + 0.45f * Mathf.Sin(t * 14f) : 1f;  // blinking near the end
            float a = drop * Mathf.Max(0.3f, fade);
            V2 c = p.DomePos;
            float R = Game.DomeRadius;

            // the glass: brighter toward the wall
            for (int k = 0; k < 4; k++)
                Circle(c, R * (1f - k * 0.04f), new Rgba(1f, 0.78f, 0.35f, 0.035f * a), 48);
            Arc(c, R - 10f, 20f, new Rgba(1f, 0.8f, 0.35f, 0.10f * a), 48);

            // plates, flashing where an enemy shot is close to the rim
            float step = MathX.Tau / DomePlates, gap = 0.012f;
            for (int i = 0; i < DomePlates; i++)
            {
                float a0 = i * step + gap, sweep = step - gap * 2f;
                V2 mid = c + V2.FromAngle(a0 + sweep / 2f) * R;
                float flash = 0f;
                foreach (var b in game.Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(mid) < 46f && b.Pos.DistanceTo(c) < R + 40f)
                        flash = 1f;
                Rgba body = flash > 0f ? new Rgba(1f, 0.94f, 0.78f, 0.95f * a) : new Rgba(0.47f, 0.35f, 0.11f, 0.9f * a);
                Rgba rim = flash > 0f ? new Rgba(1f, 1f, 1f, a) : new Rgba(0.88f, 0.66f, 0.25f, a);
                Arc(c, R + 1f, 18f, body, 2, a0, sweep);
                Arc(c, R - 7f, 2f, rim, 2, a0, sweep);
                Arc(c, R + 9f, 2f, rim, 2, a0, sweep);
            }
        }
    }
}
