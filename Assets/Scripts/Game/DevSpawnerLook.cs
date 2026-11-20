using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Dev spawners (Core/DevSpawners.cs): the Spawn-an-enemy sheet's Spawner row (DrawSpawnerRow), the pad on the floor with a ghost of
    /// its enemy and the ring filling to the next spawn (DrawDevSpawners, world pass), and its name tag (DrawDevSpawnerTags, HUD).
    public partial class ArenaApp
    {
        static readonly Rgba SpawnerRed = new Rgba(1f, 0.35f, 0.3f);
        readonly Dictionary<EnemyType, Enemy> spawnerGhosts = new Dictionary<EnemyType, Enemy>();

        /// Spawner on/off; with it on: how often, how many alive at once, how many are placed, Remove last and Clear.
        void DrawSpawnerRow(Rect r, bool host)
        {
            bool was = GUI.enabled;
            GUI.enabled = was && host;
            devSpawnerMode = DkSwitch(new Rect(r.x, r.y, 120f, r.height), devSpawnerMode, "Spawner");
            float x = r.x + 128f;
            if (devSpawnerMode)
            {
                Ui.Label(new Rect(x, r.y, 42f, r.height), "EVERY", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
                x += 42f;
                for (int i = 0; i < Game.DevSpawnerEvery.Length; i++, x += 34f)
                    if (DkChoice(new Rect(x, r.y, 31f, r.height), Game.DevSpawnerEvery[i] + "s", devSpawnerEvery == i, CrtStyle.Gold))
                        devSpawnerEvery = i;
                x += 10f;
                Ui.Label(new Rect(x, r.y, 36f, r.height), "MAX", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
                x += 36f;
                for (int i = 0; i < Game.DevSpawnerCaps.Length; i++, x += 34f)
                    if (DkChoice(new Rect(x, r.y, 31f, r.height), Game.DevSpawnerCaps[i].ToString(), devSpawnerCap == i, CrtStyle.Gold))
                        devSpawnerCap = i;
            }
            else
                Ui.Label(new Rect(x, r.y, r.xMax - x - 200f, r.height), "on: a click places a pad that keeps bringing in that enemy", 11, CrtStyle.Desc, TextAnchor.MiddleLeft);
            int n = game.DevSpawners.Count;
            GUI.enabled = was && host && n > 0;
            var clear = new Rect(r.xMax - 62f, r.y, 62f, r.height);
            var last = new Rect(clear.x - 6f - 62f, r.y, 62f, r.height);
            if (DkKey(last, "UNDO", Ui.Neutral, false, 10))
            {
                int newest = 0;
                foreach (var id in game.DevSpawners.Keys)
                    newest = Mathf.Max(newest, id);
                game.DevRemoveSpawner(newest);
            }
            if (DkKey(clear, "CLEAR", CrtStyle.Danger, false, 10))
                game.DevClearSpawners();
            GUI.enabled = was;
            Ui.Label(new Rect(last.x - 50f, r.y, 44f, r.height), n + "/" + Game.DevSpawnerMax, 11, n > 0 ? SpawnerRed.ToColor() : CrtStyle.Desc, TextAnchor.MiddleRight, true);
        }

        /// World pass: each spawner's pad: a dark disc, a turning dashed rim, the ring filling to its next spawn, pips for its enemies alive,
        /// and a faint ghost of the enemy it brings in.
        void DrawDevSpawners(float t)
        {
            if (game.DevSpawners.Count == 0)
                return;
            foreach (var s in game.DevSpawners.Values)
            {
                V2 c = s.Pos;
                const float R = 34f;
                bool full = s.Alive >= s.Cap;
                Rgba ink = s.Elite ? new Rgba(1f, 0.8f, 0.3f) : SpawnerRed;
                Circle(c, R + 4f, new Rgba(0f, 0f, 0f, 0.45f), 28);
                Circle(c, R - 2f, ink.WithAlpha(0.08f), 28);
                const int dashes = 12;
                for (int i = 0; i < dashes; i++)  // the turning rim
                {
                    float a0 = t * 0.6f + i * MathX.Tau / dashes;
                    Arc(c, R + 2f, 2f, ink.WithAlpha(0.65f), 4, a0, MathX.Tau / dashes * 0.55f);
                }
                float prog = full || s.Every <= 0f ? 1f : Mathf.Clamp01(1f - s.T / s.Every);
                Arc(c, R - 6f, 3f, ink.WithAlpha(full ? 0.25f : 0.9f), 30, -Mathf.PI / 2f, MathX.Tau * prog);
                for (int i = 0; i < s.Cap && i < 10; i++)  // its enemies alive (lit) out of its cap
                {
                    float a = -Mathf.PI / 2f + (i - (Mathf.Min(s.Cap, 10) - 1) / 2f) * 0.22f;
                    Circle(c + V2.FromAngle(a + Mathf.PI) * (R + 12f), 3f, i < s.Alive ? ink : ink.WithAlpha(0.2f), 8);
                }
                Enemy e;
                if (!spawnerGhosts.TryGetValue(s.Type, out e))
                {
                    var d = Defs.Get(s.Type);
                    spawnerGhosts[s.Type] = e = new Enemy { Id = 800 + (int)s.Type, Type = s.Type, Def = d, Radius = d.Radius, Hp = 1f, MaxHp = 1f };
                }
                e.Disp = c + new V2(0f, Mathf.Sin(t * 2f + s.Id) * 2f);
                e.Facing = t * 0.7f + s.Id;
                e.Elite = s.Elite;
                float keep = glAlpha;
                glAlpha = keep * (0.45f + 0.3f * prog);
                menuPreview = true;
                DrawEnemy(e, t);
                menuPreview = false;
                glAlpha = keep;
            }
        }

        /// HUD: the spawner's enemy and its timing over the pad.
        void DrawDevSpawnerTags()
        {
            foreach (var s in game.DevSpawners.Values)
            {
                V2 at = ToUi(s.Pos + new V2(0f, -50f));
                var ink = (s.Elite ? new Rgba(1f, 0.8f, 0.3f) : SpawnerRed).ToColor();
                var box = new Rect(at.X - 80f, at.Y - 22f, 160f, 13f);
                Ui.Label(box, "SPAWNER  ·  " + (s.Elite ? "ELITE " : "") + PracticeEnemyName(s.Type).ToUpperInvariant(), 10, ink, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(box.x, box.y + 12f, box.width, 12f), "every " + s.Every + " s  ·  " + s.Alive + "/" + s.Cap + " alive", 9, ink.WithAlpha(0.7f), TextAnchor.MiddleCenter);
            }
        }
    }
}
