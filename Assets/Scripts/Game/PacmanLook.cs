using UnityEngine;

namespace Arena
{
    /// The Pac-Man skin, on while the active profile has the "pac" disk look (a name with Pacman, Pac-Man, Chomper or Waka in it,
    /// ProfileStore.DiskLookFor): every player draws as a chomper, regular enemies draw as ghosts coloured by their role
    /// (DPS red, Tank cyan, Healer pink, Wildcard orange) and every bullet is a dot. Front end only: nothing is sent, and
    /// gameplay and hitboxes are untouched. Also holds the Pac-Man disk on the "Who's playing?" shelf.
    public partial class ArenaApp
    {
        static bool PacSkin => Profile.Current != null && Profile.Current.DiskLook == "pac";

        static readonly Rgba PacYellow = new Rgba(1f, 0.9f, 0.1f);
        static readonly Rgba PacDpsRed = new Rgba(1f, 0.2f, 0.15f), PacTankCyan = new Rgba(0.2f, 0.9f, 1f), PacHealPink = new Rgba(1f, 0.6f, 0.8f), PacWildOrange = new Rgba(1f, 0.65f, 0.15f);

        /// Regular enemies become ghosts; bosses, spawners, mines, eggs and zombies keep their own look.
        static bool PacGhostable(Enemy e)
        {
            return !e.Def.IsBoss && !Zombies.Is(e.Type) && e.Type != EnemyType.SapperMine && e.Type != EnemyType.HiveEgg && e.Type != EnemyType.HiveBomber
                && e.Type != EnemyType.Gatekeeper && e.Type != EnemyType.Shadow && e.Type != EnemyType.ReaperPillar && e.Type != EnemyType.Singularity;
        }

        /// The ghost's role: support enemies are Healers, Pressure and Hunters are DPS, Anchors and the plated types are Tanks, the rest Wildcards.
        static Rgba PacRoleColor(Enemy e)
        {
            switch (e.Type)
            {
                case EnemyType.Medic: case EnemyType.Warder: case EnemyType.Nest:
                    return PacHealPink;
                case EnemyType.Shielder: case EnemyType.Brute: case EnemyType.Juggernaut: case EnemyType.Colossus:
                    return PacTankCyan;
            }
            switch (Game.JobOf(e.Type))
            {
                case EnemyJob.Pressure: case EnemyJob.Hunter: return PacDpsRed;
                case EnemyJob.Anchor: return PacTankCyan;
                default: return PacWildOrange;
            }
        }

        /// A ghost: a dome, a body, a scalloped skirt that wobbles, and eyes that look at the nearest player.
        void DrawPacGhost(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float wob = Mathf.Sin(t * 9f + e.Id) * 0.12f;
            Circle(pos + new V2(0f, -r * 0.12f), r * 0.88f, col, 24);
            FillRect(pos.X - r * 0.88f, pos.Y - r * 0.12f, r * 1.76f, r * 0.86f, col);
            const int teeth = 4;
            float tw = r * 1.76f / teeth, baseY = pos.Y + r * 0.74f;
            SetColor(col);
            for (int i = 0; i < teeth; i++)
            {
                float x0 = pos.X - r * 0.88f + i * tw;
                float sh = (i % 2 == 0 ? 1f : -1f) * wob * r;
                Tri(new V2(x0, baseY), new V2(x0 + tw, baseY), new V2(x0 + tw * 0.5f, baseY + r * (0.3f + sh)));
            }
            V2 look = V2.FromAngle(e.Facing);
            float best = 99999f;
            foreach (var p in game.Players.Values)
                if (!p.Downed && p.Disp.DistanceTo(pos) < best)
                {
                    best = p.Disp.DistanceTo(pos);
                    look = (p.Disp - pos).Normalized();
                }
            for (int s = -1; s <= 1; s += 2)
            {
                V2 eye = pos + new V2(s * r * 0.36f, -r * 0.28f);
                Circle(eye, r * 0.27f, Rgba.White, 14);
                Circle(eye + look * r * 0.1f, r * 0.14f, new Rgba(0.1f, 0.2f, 0.8f), 10);
            }
        }

        /// A player: a yellow disc with a wedge mouth that points where they aim and chomps while they live.
        void DrawPacChomper(Player p, float r, float t, Rgba col)
        {
            float chew = 0.5f + 0.5f * Mathf.Sin(t * 12f + p.Id * 1.3f);
            float open = p.Downed ? 0.05f : 0.08f + 0.5f * chew;
            Rgba body = PacYellow.WithAlpha(col.A);
            if (p.Flash > 0f)
                body = Rgba.White;
            V2 c = p.Disp;
            SetColor(body);
            const int segs = 20;
            V2 prev = c + V2.FromAngle(p.Aim + open) * r;
            for (int i = 1; i <= segs; i++)
            {
                V2 next = c + V2.FromAngle(p.Aim + open + (MathX.Tau - 2f * open) * i / segs) * r;
                Tri(c, prev, next);
                prev = next;
            }
            Arc(c, r, 1.8f, new Rgba(0.6f, 0.45f, 0f, 0.8f * col.A), 24, p.Aim + open, MathX.Tau - 2f * open);
        }

        /// Every bullet is a dot to eat: small cream pellets for the enemy's, pale yellow for yours, a big blinking power pellet for large shots.
        void DrawPacDot(Bullet b, float t)
        {
            bool enemy = Defs.IsEnemyShot(b.Kind);
            Rgba c = enemy ? new Rgba(1f, 0.93f, 0.82f) : new Rgba(1f, 0.95f, 0.5f);
            float r = Mathf.Max(3f, b.Radius * 0.55f);
            if (b.Radius > 9f)
            {
                r = b.Radius * 0.7f;
                c = c.WithAlpha(0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 6f)));
            }
            Circle(b.Pos, r, c, 12);
        }

        /// The Pac-Man disk: a black floppy with a blue maze-edged label, a chomper eating a row of dots, and a ghost behind it.
        void DrawPacDisk(Rect d)
        {
            float k = d.width / 200f;
            Color plastic = new Color(0.07f, 0.07f, 0.13f), blue = new Color(0.15f, 0.25f, 0.95f), yellow = new Color(1f, 0.9f, 0.1f);
            Ui.Pill(new Rect(d.x + 3f, d.y + 12f * k, d.width - 6f, d.height - 8f * k), new Color(0f, 0f, 0f, 0.4f));
            float ch = 20f * k;
            for (float y = 0f; y < d.height; y += 2f)
            {
                float w = y < ch ? d.width - ch + y : d.width;
                Ui.Fill(new Rect(d.x, d.y + y, w, 2.5f), Color.Lerp(plastic, new Color(0.03f, 0.03f, 0.07f), y / d.height));
            }
            Ui.Fill(new Rect(d.x + 2f, d.y, d.width - ch - 2f, 2f), new Color(0.3f, 0.4f, 1f, 0.3f));
            Ui.Fill(new Rect(d.x + 46f * k, d.y, 108f * k, 54f * k), new Color(0.72f, 0.73f, 0.78f));           // the shutter
            Ui.Fill(new Rect(d.x + 118f * k, d.y, 36f * k, 54f * k), new Color(0.84f, 0.85f, 0.9f));
            Ui.Fill(new Rect(d.x + 60f * k, d.y + 8f * k, 24f * k, 36f * k), new Color(0.03f, 0.03f, 0.06f));
            var lb = new Rect(d.x + 14f * k, d.y + 66f * k, 172f * k, 112f * k);                                 // the label: a maze wall round a black screen
            Ui.Fill(lb, blue);
            var inner = new Rect(lb.x + 4f * k, lb.y + 4f * k, lb.width - 8f * k, lb.height - 8f * k);
            Ui.Fill(inner, new Color(0f, 0f, 0f));
            Ui.Label(new Rect(inner.x, inner.y + 4f * k, inner.width, 18f * k), "READY!", Mathf.RoundToInt(14f * k), yellow, TextAnchor.MiddleCenter, true);
            float cy = inner.y + 54f * k, cx = inner.x + 26f * k, cr = 17f * k;                                   // the chomper, mouth open to the right
            Ui.Dot(new Rect(cx - cr, cy - cr, cr * 2f, cr * 2f), yellow);
            Ui.Fill(new Rect(cx, cy - 1f, cr + 1f, 1f), new Color(0f, 0f, 0f));
            for (int i = 0; i < 3; i++)                                                                           // the line of dots, then a power pellet
                Ui.Dot(new Rect(cx + cr + 12f * k + i * 18f * k, cy - 2.5f * k, 5f * k, 5f * k), new Color(1f, 0.93f, 0.82f));
            Ui.Dot(new Rect(inner.xMax - 22f * k, cy - 6f * k, 12f * k, 12f * k), new Color(1f, 0.93f, 0.82f));
            float gx = inner.x + 16f * k, gy = inner.yMax - 24f * k;                                              // a little ghost along the bottom
            Color gc = new Color(1f, 0.2f, 0.15f);
            Ui.Dot(new Rect(gx, gy, 16f * k, 14f * k), gc);
            Ui.Fill(new Rect(gx, gy + 7f * k, 16f * k, 9f * k), gc);
            Ui.Dot(new Rect(gx + 3f * k, gy + 4f * k, 4.5f * k, 5f * k), Color.white);
            Ui.Dot(new Rect(gx + 9f * k, gy + 4f * k, 4.5f * k, 5f * k), Color.white);
            Ui.Label(new Rect(inner.x + 40f * k, inner.yMax - 24f * k, inner.width - 44f * k, 16f * k), "1UP  000", Mathf.Max(6, Mathf.RoundToInt(9f * k)), Color.white, TextAnchor.MiddleLeft, true);
        }
    }
}
