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
            var gone = new List<int>();
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
                float r = e.Radius * WorldToUi;
                Color c = ToColor(JobColors[job]);
                c.a = Mathf.Clamp01(left / TagFade);
                var box = new Rect(at.X - 70f, at.Y - r - 44f, 140f, 14f);
                Ui.Label(box, e.Def.Name.ToUpperInvariant(), 11, new Color(1f, 1f, 1f, c.a), TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(box.x, box.y + 12f, box.width, 12f), JobNames[job], 9, c, TextAnchor.MiddleCenter, true);
            }
            foreach (var id in gone)
                tagUntil.Remove(id);
        }
    }
}
