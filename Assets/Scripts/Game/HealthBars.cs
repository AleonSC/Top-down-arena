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
        static readonly Color WoundUi = new Color(0.55f, 0.08f, 0.12f, 0.95f), WoundStripe = new Color(0.9f, 0.22f, 0.28f, 0.95f), WoundLine = new Color(1f, 0.55f, 0.58f, 1f);
        static readonly Rgba WoundWorld = new Rgba(0.5f, 0.07f, 0.11f, 0.95f), WoundWorldStripe = new Rgba(0.92f, 0.24f, 0.3f, 0.95f), WoundWorldLine = new Rgba(1f, 0.6f, 0.62f, 1f);

        public const float WoundFlashTime = 0.7f;  // seconds the wound tick flashes after the wound grows
        struct WoundSeen { public float Wound, At; }
        readonly Dictionary<int, WoundSeen> woundSeen = new Dictionary<int, WoundSeen>();

        /// 1 the moment a player's wound grows, fading to 0 over WoundFlashTime: the tick flashes white and stands out further.
        float WoundFlash(Player p)
        {
            float now = Time.unscaledTime;
            WoundSeen w;
            if (!woundSeen.TryGetValue(p.Id, out w))
                w = new WoundSeen { Wound = p.Wound, At = -99f };  // first sight: no flash for a wound brought in (a save, joining)
            if (p.Wound > w.Wound + 0.05f)
                w.At = now;
            w.Wound = p.Wound;
            woundSeen[p.Id] = w;
            float k = 1f - Mathf.Clamp01((now - w.At) / WoundFlashTime);
            return k * k;
        }

        static Color WoundTick(Color line, float flash) => Color.Lerp(line, Color.white, flash);

        /// A wound (Combat.cs, Docs/PowerStacking.md): the end of a player's health bar no heal can reach, striped dark and bright red,
        /// with a light tick standing out past the bar where healing stops.
        void DrawWound(Rect hp, Player p)
        {
            if (p.Wound <= 0.5f || p.MaxHp <= 0f)
                return;
            float w = hp.width * Mathf.Clamp01(p.Wound / p.MaxHp), x0 = hp.xMax - w;
            Ui.Fill(new Rect(x0, hp.y, w, hp.height), WoundUi);
            for (float x = x0 + 2f; x < hp.xMax; x += 5f)  // stripes: a locked section, not empty health
                Ui.Fill(new Rect(x, hp.y, Mathf.Min(2f, hp.xMax - x), hp.height), WoundStripe);
            float fl = WoundFlash(p);
            if (fl > 0f)  // just wounded: a red glow round the tick
                Ui.Fill(new Rect(x0 - 4f, hp.y - 5f, 8f, hp.height + 10f), CrtStyle.Danger.WithAlpha(0.7f * fl));
            float tw = 2f + 2f * fl, th = 2f + 4f * fl;
            Ui.Fill(new Rect(x0 - tw / 2f, hp.y - th, tw, hp.height + 2f * th), WoundTick(WoundLine, fl));
        }

        /// The Hemomancer's blood floor (Game.HemoFloor): a crimson tick on the bar where health costs stop, with a dim
        /// band under it, so you can see how much you have left to spend.
        void DrawBloodFloor(Rect hp, Player p)
        {
            if (p.Kit != Kit.Hemomancer || p.MaxHp <= 0f)
                return;
            float x = hp.x + hp.width * Game.HemoFloor;
            bool at = Game.AtHealthFloor(p);
            Ui.Fill(new Rect(hp.x, hp.y, x - hp.x, hp.height), CrtStyle.Danger.WithAlpha(at ? 0.22f : 0.1f));
            Ui.Fill(new Rect(x - 1f, hp.y - 2f, 2f, hp.height + 4f), at ? CrtStyle.Danger : CrtStyle.Danger.WithAlpha(0.75f));
        }

        /// The same over a player's bar in the world (WorldPlayers.cs): x, y, width, height of the whole bar.
        void DrawWorldWound(float bx, float by, float bw, float bh, Player p)
        {
            if (p.Wound <= 0.5f || p.MaxHp <= 0f)
                return;
            float w = bw * Mathf.Clamp01(p.Wound / p.MaxHp), x0 = bx + bw - w;
            FillRect(x0, by, w, bh, WoundWorld);
            for (float x = x0 + 1.5f; x < bx + bw; x += 4f)
                FillRect(x, by, Mathf.Min(1.5f, bx + bw - x), bh, WoundWorldStripe);
            float fl = WoundFlash(p);
            if (fl > 0f)  // just wounded: a red glow round the tick
                FillRect(x0 - 3f, by - 4f, 6f, bh + 8f, new Rgba(1f, 0.25f, 0.3f, 0.75f * fl));
            float tw = 1.5f + 1.5f * fl, th = 2f + 3f * fl;
            var tc = WoundTick(new Color(WoundWorldLine.R, WoundWorldLine.G, WoundWorldLine.B, 1f), fl);
            FillRect(x0 - tw / 2f, by - th, tw, bh + 2f * th, new Rgba(tc.r, tc.g, tc.b, 1f));
        }

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
                var col = e.Def.Color.ToColor();
                float frac = Mathf.Clamp01(e.Hp / Mathf.Max(1f, e.MaxHp));
                var old = GUI.color;
                GUI.color = old.ScaleAlpha(a);
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
