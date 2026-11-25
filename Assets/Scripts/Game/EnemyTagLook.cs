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

        // Settings -> Gameplay -> "Name labels" (or the N key): a name over you and over every enemy, all the time. Off by default.
        bool nameLabels;
        void LoadNameLabels() => nameLabels = PlayerPrefs.GetInt("name_labels", 0) == 1;

        void SetNameLabels(bool on)
        {
            nameLabels = on;
            PlayerPrefs.SetInt("name_labels", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// Hud: the name labels, when they're on.
        void DrawNameLabels()
        {
            if (!nameLabels)
                return;
            foreach (var e in game.Enemies.Values)
            {
                V2 at = ToUi(e.Disp);
                Ui.Label(new Rect(at.X - 70f, at.Y - e.Radius * UiWorldScale - 20f, 140f, 14f), Defs.ListName(e.Type), 10, CrtStyle.Danger, TextAnchor.MiddleCenter, true);
            }
            var me = game.Me;
            if (me != null)
            {
                V2 at = ToUi(me.Disp);
                Ui.Label(new Rect(at.X - 100f, at.Y - me.Def.BodyR * UiWorldScale - 30f, 200f, 16f), me.Name, 12, CrtStyle.Gold, TextAnchor.MiddleCenter, true);
            }
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
            var typesUp = tagTypesUp;  // one tag per type at a time: a squad of three Guards spends one sighting, not all three
            typesUp.Clear();
            foreach (var kv in tagUntil)
                if (kv.Value > now && game.Enemies.TryGetValue(kv.Key, out var te))
                    typesUp.Add(te.Type);
            foreach (var e in game.Enemies.Values)
            {
                if (!HasJobMarker(e) || tagChecked.Contains(e.Id))
                    continue;
                tagChecked.Add(e.Id);
                if (!typesUp.Add(e.Type))
                    continue;
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
            tagBoxes.Clear();
            foreach (var kv in tagUntil)
            {
                Enemy e;
                float left = kv.Value - now;
                if (left <= 0f || !game.Enemies.TryGetValue(kv.Key, out e) || e.Hp <= 0f)
                {
                    gone.Add(kv.Key);
                    continue;
                }
                V2 at = ToUi(e.Disp);
                float r = e.Radius * UiWorldScale;
                tagBoxes.Add(new TagBox { E = e, Box = new Rect(at.X - TagW / 2f, at.Y - r - 44f, TagW, TagH), A = Mathf.Clamp01(left / TagFade) });
            }
            LayOutTags(tagBoxes);
            foreach (var t in tagBoxes)
            {
                int job = (int)Game.JobOf(t.E.Type);
                DrawTag(t.Box, t.E.Def.Name.ToUpperInvariant(), JobNames[job], JobColors[job].ToColor(), t.A);
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
                if (e.Hp <= 0f || game.IsSpawning(e) || Game.IsPlanted(e.Type) || e.Type == EnemyType.SpawnPortal || tagUntil.ContainsKey(e.Id))
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
            tagBoxes.Clear();
            tagBoxes.Add(new TagBox { E = best, Box = new Rect(at.X - TagW / 2f, at.Y - r - 44f, TagW, TagH), A = 1f });
            LayOutTags(tagBoxes);
            var tb = tagBoxes[0];
            int jb = (int)Game.JobOf(best.Type);
            DrawTag(tb.Box, best.Def.Name.ToUpperInvariant(), HasJobMarker(best) ? JobNames[jb] : null, JobColors[jb].ToColor(), tb.A);
        }

        // --- Laying the tags out: no two overlap, none sits on the console ----------------------------------------------------------

        const float TagW = 140f, TagH = 26f, TagGap = 2f;
        struct TagBox { public Enemy E; public Rect Box; public float A; }
        readonly List<TagBox> tagBoxes = new List<TagBox>();
        readonly HashSet<EnemyType> tagTypesUp = new HashSet<EnemyType>();

        /// Top to bottom, each tag that would overlap one already placed moves up past it (its text is narrower than TagW, so
        /// only the text's width counts). A tag over the bottom console fades to a ghost: the enemy is under the HUD anyway.
        static void LayOutTags(List<TagBox> tags)
        {
            tags.Sort((a, b) => a.Box.y.CompareTo(b.Box.y));
            for (int i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                var box = t.Box;
                for (int guard = 0; guard < 8; guard++)
                {
                    bool hit = false;
                    for (int j = 0; j < i; j++)
                    {
                        var o = tags[j].Box;
                        if (Mathf.Abs(o.center.x - box.center.x) < 96f && box.y < o.yMax + TagGap && box.yMax > o.y - TagGap)
                        {
                            box.y = o.y - TagH - TagGap;
                            hit = true;
                        }
                    }
                    if (!hit)
                        break;
                }
                if (box.yMax > HudXpStrip.y)
                    t.A *= 0.25f;
                t.Box = box;
                tags[i] = t;
            }
        }

        /// One tag: the name in white over the job in its colour.
        static void DrawTag(Rect box, string name, string job, Color jobC, float a)
        {
            Ui.Label(new Rect(box.x, box.y, box.width, 14f), name, 11, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, true);
            if (job != null)
            {
                jobC.a = a;
                Ui.Label(new Rect(box.x, box.y + 12f, box.width, 12f), job, 9, jobC, TextAnchor.MiddleCenter, true);
            }
        }
    }
}
