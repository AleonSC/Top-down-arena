using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Name tags (Docs/EnemyReadability.md section 4): the first TagShows times you see an enemy type it carries its name and
    /// job for a few seconds, in the job's colour (the same colours as the job marker under it). After that it is left
    /// alone. How often each type has been shown is kept on the profile ("tag" lines), so a new profile is taught
    /// and an old one isn't nagged. Front end only: it counts what this player sees, nothing is sent.
    public partial class ArenaApp
    {
        const float TagTime = 2.6f, TagFade = 0.5f;
        readonly Dictionary<int, float> tagUntil = new Dictionary<int, float>();   // enemy id -> when its tag ends
        readonly HashSet<int> tagChecked = new HashSet<int>();                      // enemies already counted
        int tagRunKey = -1;

        // Settings -> Gameplay -> "Enemy names on hover": point at an enemy to see its name (and job), any time. Off by default.
        bool hoverNames;
        void LoadHoverNames() => hoverNames = PlayerPrefs.GetInt("hover_names", 0) == 1;

        void SetHoverNames(bool on)
        {
            hoverNames = on;
            PlayerPrefs.SetInt("hover_names", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        static readonly string[] JobNames = { "PRESSURE", "HUNTER", "ZONER", "DISRUPTOR", "ANCHOR" };

        /// Hud: count the enemies that just came into view and draw the tags that are up.
        void DrawEnemyTags()
        {
            if (game.State != MatchState.Fighting)
                return;
            var prof = game.Profile;
            if (prof == null)
                return;
            if (tagRunKey != game.Wave)  // new wave: forget the old ids (they are never reused, this just keeps the sets small)
            {
                tagRunKey = game.Wave;
                if (tagChecked.Count > 400)
                {
                    tagChecked.Clear();
                    tagUntil.Clear();
                }
            }
            float now = Time.unscaledTime;
            bool dirty = false;
            foreach (var e in game.Enemies.Values)
            {
                if (!HasJobMarker(e) || tagChecked.Contains(e.Id))
                    continue;
                tagChecked.Add(e.Id);
                string key = e.Type.ToString();
                prof.TagSeen.TryGetValue(key, out int seen);
                if (seen >= Profile.TagShows)
                    continue;
                prof.TagSeen[key] = seen + 1;
                tagUntil[e.Id] = now + TagTime;
                dirty = true;
            }
            if (dirty)
                prof.Save();
            if (tagUntil.Count == 0)
                return;
            var gone = DrawScratch.FreshIds();  // (only labels are drawn while it fills)
            foreach (var kv in tagUntil)
            {
                Enemy e;
                float left = kv.Value - now;
                if (left <= 0f || !game.Enemies.TryGetValue(kv.Key, out e) || e.Hp <= 0f)
                {
                    gone.Add(kv.Key);
                    continue;
                }
                int job = (int)Game.JobOf(e.Type);
                V2 at = ToUi(e.Disp);
                float r = e.Radius * UiWorldScale;
                Color c = ToColor(JobColors[job]);
                c.a = Mathf.Clamp01(left / TagFade);
                var box = new Rect(at.X - 70f, at.Y - r - 44f, 140f, 14f);
                Ui.Label(box, e.Def.Name.ToUpperInvariant(), 11, new Color(1f, 1f, 1f, c.a), TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(box.x, box.y + 12f, box.width, 12f), JobNames[job], 9, c, TextAnchor.MiddleCenter, true);
            }
            foreach (var id in gone)
                tagUntil.Remove(id);
        }

        /// Hud: the name of the enemy the cursor is on (and its job, for the regular ones), when the setting is on.
        /// Skipped while that enemy already carries a teaching tag, so the two never stack.
        void DrawHoverName()
        {
            if (!hoverNames || game.State != MatchState.Fighting || game.Paused || pauseMenuOpen)
                return;
            V2 m = MouseArena();
            Enemy best = null;
            float bestD = 1e9f;
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f || game.IsSpawning(e) || e.Type == EnemyType.SapperMine || tagUntil.ContainsKey(e.Id))
                    continue;
                float d = e.Disp.DistanceTo(m) - e.Radius;
                if (d < 10f && d < bestD)
                {
                    best = e;
                    bestD = d;
                }
            }
            if (best == null)
                return;
            V2 at = ToUi(best.Disp);
            float r = best.Radius * UiWorldScale;
            var box = new Rect(at.X - 70f, at.Y - r - 44f, 140f, 14f);
            Ui.Label(box, best.Def.Name.ToUpperInvariant(), 11, Color.white, TextAnchor.MiddleCenter, true);
            if (HasJobMarker(best))
            {
                int job = (int)Game.JobOf(best.Type);
                Ui.Label(new Rect(box.x, box.y + 12f, box.width, 12f), JobNames[job], 9, ToColor(JobColors[job]), TextAnchor.MiddleCenter, true);
            }
        }
    }
}
