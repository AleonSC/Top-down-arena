using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Health bars: the damage ghost (with Visual effects on: a pale chunk that lingers where health was lost, then drains
    /// after it, so a big hit reads at a glance), and the small boss bar that stays at the top when the HUD is hidden (H).
    public partial class ArenaApp
    {
        public const float GhostHold = 0.45f, GhostDrain = 0.7f;  // seconds the chunk waits, then share of the bar per second

        struct GhostState { public float Ghost, Frac, Dropped, Seen; }
        readonly Dictionary<long, GhostState> ghosts = new Dictionary<long, GhostState>();

        // Keys: an enemy, a player, so the HUD's bar and the one over the character keep their own ghost.
        static long EnemyKey(int id) => id;
        static long PlayerKey(int id, bool hud) => (hud ? 2L : 3L) << 32 | (uint)id;

        /// Where the pale chunk ends for a bar now at `frac`: up where health was until GhostHold after the last drop,
        /// then draining down to it. Healing pulls it up at once.
        float Ghost(long key, float frac)
        {
            float now = Time.unscaledTime;
            GhostState g;
            if (!ghosts.TryGetValue(key, out g))
                g = new GhostState { Ghost = frac, Frac = frac, Dropped = -1f, Seen = now };
            if (frac < g.Frac - 1e-4f)
                g.Dropped = now;
            float dt = Mathf.Max(0f, now - g.Seen);
            if (frac >= g.Ghost)
                g.Ghost = frac;
            else if (now - g.Dropped > GhostHold)
                g.Ghost = Mathf.Max(frac, g.Ghost - GhostDrain * dt);
            g.Frac = frac;
            g.Seen = now;
            ghosts[key] = g;
            if (ghosts.Count > 400)
                ghosts.Clear();  // enemies come and go: start afresh rather than keep the old ones
            return g.Ghost;
        }

        static float Pale(float v) => v + (1f - v) * 0.65f;  // most of the way to white
        static Color GhostColor(Color fill) => new Color(Pale(fill.r), Pale(fill.g), Pale(fill.b), 0.75f);

        /// A HUD bar with its damage ghost (just Ui.Bar with Visual effects off).
        void GhostBar(Rect r, float frac, Color fill, long key)
        {
            if (!fxOn)
            {
                Ui.Bar(r, frac, fill);
                return;
            }
            frac = Mathf.Clamp01(frac);
            float g = Ghost(key, frac);
            Ui.Pill(r, new Color(0f, 0f, 0f, 0.55f));
            if (g > frac + 1e-3f)
            {
                GUI.BeginGroup(new Rect(r.x, r.y, r.width * g, r.height));
                Ui.Pill(new Rect(0f, 0f, r.width, r.height), GhostColor(fill));
                GUI.EndGroup();
            }
            Ui.Bar(r, frac, fill, new Color(0f, 0f, 0f, 0f));
        }

        /// A bar over a character in the arena (GL), with its damage ghost.
        void GhostBar(V2 pos, float w, float h, float frac, Rgba c, long key)
        {
            if (!Juicy)
            {
                Bar(pos, w, h, frac, c);
                return;
            }
            frac = MathX.Clamp01(frac);
            float g = Ghost(key, frac);
            FillRect(pos.X, pos.Y, w, h, new Rgba(0f, 0f, 0f, 0.6f));
            if (g > frac + 1e-3f)
                FillRect(pos.X, pos.Y, w * g, h, new Rgba(Pale(c.R), Pale(c.G), Pale(c.B), 0.75f));
            FillRect(pos.X, pos.Y, w * frac, h, c);
        }

        /// The HUD hidden (H): a slim bar per boss at the top, so you still know how it's going. Fades in as the HUD goes.
        void DrawMiniBossBars(float a)
        {
            if (a <= 0.01f)
                return;
            float y = 10f, w = 360f, x = W / 2f - w / 2f;
            foreach (var e in game.Enemies.Values)
            {
                if (!e.Def.IsBoss && !e.Def.Mini)
                    continue;
                var col = ToColor(e.Def.Color);
                float frac = Mathf.Clamp01(e.Hp / Mathf.Max(1f, e.MaxHp));
                var old = GUI.color;
                GUI.color = new Color(old.r, old.g, old.b, old.a * a);
                Ui.Alpha = a;
                Ui.Shadowed(new Rect(x, y, w * 0.7f, 14f), e.Def.Name.ToUpperInvariant(), 10, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleLeft);
                Ui.Shadowed(new Rect(x + w * 0.7f, y, w * 0.3f, 14f), Mathf.CeilToInt(frac * 100f) + "%", 10, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleRight);
                GhostBar(new Rect(x, y + 16f, w, 6f), frac, col, EnemyKey(e.Id) | 1L << 40);
                if (e.Type == EnemyType.Warden && e.Warden != null && e.Warden.Armor > 0f && e.Warden.ArmorMax > 0f)  // its last-stand armor, above
                    GhostBar(new Rect(x, y + 12f, w, 3f), Mathf.Clamp01(e.Warden.Armor / e.Warden.ArmorMax), new Color(0.45f, 0.65f, 1f), EnemyKey(e.Id) | 2L << 40);
                Ui.Alpha = 1f;
                GUI.color = old;
                y += 30f;
            }
        }
    }
}
