// TEMPLATE for a new class's look (see "Adding a class" in HANDOFF.md). Copy to Assets/Scripts/Game/<YourClass>Look.cs
// and add its line to KitLooks in Assets/Scripts/Game/ClassLooks.cs:
//     new KitLook { Body = (a, p, r, t, col) => a.DrawMyClass(p, r, t, col) },
using UnityEngine;

namespace Arena
{
    /// TODO: what the class looks like.
    public partial class ArenaApp
    {
        /// Drawn over the player's circle (not while downed): r is the body radius, t the time, col the body colour.
        /// GL only: Circle, Arc, Line, Tri (WorldRenderer.cs). Point things along p.Aim to show the weapon.
        void DrawMyClass(Player p, float r, float t, Rgba col)
        {
            V2 dir = V2.FromAngle(p.Aim);
            Line(p.Disp + dir * (r * 0.3f), p.Disp + dir * (r + 12f), 3f, new Rgba(1f, 0.9f, 0.4f, 0.9f));
        }
    }
}
