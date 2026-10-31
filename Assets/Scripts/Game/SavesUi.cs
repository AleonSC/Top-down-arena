using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// Saved runs and records on screen (RunSaves.cs, SaveData.cs, Profile.cs): the main menu's Continue list and
    /// Records page, the lobby's seats when continuing a run, and "Unlocked" toasts in a match.
    public partial class ArenaApp
    {
        bool continueOpen, recordsOpen;
        List<RunSave> savedRuns = new List<RunSave>();
        string deleteArmed;  // the save whose Delete was clicked once (a second click deletes it)

        static readonly Color SaveColor = new Color(0.42f, 0.86f, 0.62f);

        /// Where saves and the profile live (called once at start).
        static void InitSaveFolders()
        {
            SaveStore.Folder = Path.Combine(Application.persistentDataPath, "saves");
            Profile.Folder = Application.persistentDataPath;
        }

        float savesCountT;
        int savesCount;

        /// How many runs are saved (for the menu's button), looked up every few seconds rather than every frame.
        int savedRunsCount
        {
            get
            {
                if (Time.unscaledTime >= savesCountT)
                {
                    savesCountT = Time.unscaledTime + 3f;
                    savesCount = SaveStore.List().Count;
                }
                return savesCount;
            }
        }

        void OpenContinue()
        {
            savedRuns = SaveStore.List();
            deleteArmed = null;
            continueOpen = true;
        }

        // --- Continue a run ------------------------------------------------------------------------

        void DrawContinue()
        {
            var panel = new Rect(W / 2f - 470f, 40f, 940f, 640f);
            Ui.Panel(panel, true);
            Ui.Fancy(new Rect(panel.x, panel.y + 14f, panel.width, 50f), "CONTINUE A RUN", 34, Color.white, new Color(0.72f, 1f, 0.82f), TextAnchor.MiddleCenter, SaveColor);
            Ui.Label(new Rect(panel.x, panel.y + 60f, panel.width, 18f), "Runs save themselves as each wave starts, and when you leave between waves. You host the run; friends join and take their seats.",
                12, Ui.Muted, TextAnchor.MiddleCenter);
            float x = panel.x + 30f, w = panel.width - 60f, y = panel.y + 96f;
            if (savedRuns.Count == 0)
                Ui.Label(new Rect(x, y + 120f, w, 60f), "No saved runs yet.\nStart a run: it's saved from its first wave on, until it's won or lost.", 16, Ui.Muted, TextAnchor.MiddleCenter, false, true);
            for (int i = 0; i < savedRuns.Count && i < 5; i++)
            {
                DrawSaveRow(new Rect(x, y, w, 92f), savedRuns[i]);
                y += 100f;
            }
            if (savedRuns.Count > 5)
                Ui.Label(new Rect(x, y - 4f, w, 16f), "and " + (savedRuns.Count - 5) + " older (delete some to see them)", 11, Ui.Muted, TextAnchor.MiddleCenter);
            if (session.Status.Length > 0)
                Ui.Label(new Rect(x, panel.yMax - 100f, w, 30f), session.Status, 13, Ui.Warn, TextAnchor.MiddleCenter, false, true);
            if (Ui.FancyButton(new Rect(panel.x + panel.width / 2f - 100f, panel.yMax - 62f, 200f, 44f), "Back", Ui.Primary, PixelArt.Get("ui_back"), Time.unscaledTime))
                continueOpen = false;
        }

        void DrawSaveRow(Rect r, RunSave s)
        {
            Ui.Card(r, SaveColor, 0.25f);
            float x = r.x + 18f;
            Ui.Label(new Rect(x, r.y + 8f, 260f, 30f), "Wave " + (s.Wave + 1) + (s.Endless ? "  (endless)" : ""), 22, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x, r.y + 38f, 260f, 18f), "Saved " + s.SavedAt.ToLocalTime().ToString("MMM d, HH:mm"), 12, Ui.Muted);
            Ui.Label(new Rect(x, r.y + 56f, 260f, 18f), "Score " + s.Score + "  ·  " + (int)(s.RunTime / 60f) + " min played", 12, Ui.Muted);
            // The seats, as they'll be taken back.
            float sx = x + 250f;
            for (int i = 0; i < s.Seats.Count; i++)
            {
                var seat = s.Seats[i];
                var c = ToColor(Defs.Get(seat.Role).Color);
                Ui.Fill(new Rect(sx, r.y + 12f + i * 24f, 4f, 18f), c);
                Ui.Label(new Rect(sx + 10f, r.y + 10f + i * 24f, 290f, 22f), seat.Name + "  —  " + Defs.KitName(seat.Kit) + " " + Defs.Get(seat.Role).Name + ", level " + seat.Level, 13, Ui.Text);
            }
            // What's special about it.
            float bx = r.xMax - 324f, by = r.y + 12f;
            if (s.CurrentPath != PathId.None && s.PathTo > s.Wave)
            {
                Ui.Badge(new Rect(bx, by, 150f, 20f), Game.PathName(s.CurrentPath).ToUpperInvariant(), new Color(0.55f, 0.4f, 0.85f));
                by += 26f;
            }
            int mods = Modifiers.Count(s.Mods);
            if (mods > 0)
            {
                Ui.Badge(new Rect(bx, by, 150f, 20f), mods + (mods == 1 ? " MODIFIER" : " MODIFIERS"), ModColor);
                by += 26f;
            }
            if (s.DevUsed)
                Ui.Badge(new Rect(bx, by, 150f, 20f), "DEV TOOLS USED", Ui.Neutral);
            // Continue, or delete (click twice).
            bool armed = deleteArmed == s.Id;
            if (Ui.FancyButton(new Rect(r.xMax - 150f, r.y + 10f, 136f, 40f), "Continue", Ui.Primary, PixelArt.Get("ui_play"), Time.unscaledTime))
            {
                if (session.HostResume(s, Port(), nameText))
                {
                    addressList = string.Join(", ", NetPeer.LocalAddresses().ToArray());
                    continueOpen = false;
                }
            }
            if (Ui.FancyButton(new Rect(r.xMax - 150f, r.y + 56f, 136f, 28f), armed ? "Really delete?" : "Delete", armed ? Ui.Danger : Ui.Neutral, PixelArt.Get("ui_trash"), Time.unscaledTime))
            {
                if (armed)
                {
                    SaveStore.Delete(s.Id);
                    savedRuns = SaveStore.List();
                    deleteArmed = null;
                }
                else
                    deleteArmed = s.Id;
            }
        }

        // --- Records ---------------------------------------------------------------------------------

        static readonly EnemyType[] RecordBosses =
        {
            EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus,
            EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse,
        };

        static string BossName(EnemyType t) => t == EnemyType.TwinA ? "Twin Reapers" : t == EnemyType.Boss ? "The Warden" : Defs.Get(t).Name;

        void DrawRecords()
        {
            var p = Profile.Current;
            var panel = new Rect(W / 2f - 520f, 30f, 1040f, 660f);
            Ui.Panel(panel, true);
            Ui.Fancy(new Rect(panel.x, panel.y + 14f, panel.width, 50f), "RECORDS", 34, Color.white, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleCenter, Ui.Gold);
            Ui.Label(new Rect(panel.x, panel.y + 60f, panel.width, 18f), "Kept on this computer. Practice Range matches, and runs where dev tools or the bot were used, don't count.", 12, Ui.Muted, TextAnchor.MiddleCenter);
            float top = panel.y + 96f, colW = (panel.width - 90f) / 3f;

            // Runs.
            float x = panel.x + 30f, y = top;
            Ui.Heading(x, y, colW, "Runs");
            y += 30f;
            RecordLine(x, ref y, colW, "Started", p.RunsStarted.ToString());
            RecordLine(x, ref y, colW, "Won", p.RunsWon.ToString(), p.RunsWon > 0 ? Ui.Gold : Ui.Text);
            RecordLine(x, ref y, colW, "Lost", p.RunsLost.ToString());
            RecordLine(x, ref y, colW, "Best wave", p.BestWave > 0 ? p.BestWave.ToString() : "—");
            RecordLine(x, ref y, colW, "Best endless wave", p.BestEndlessWave > 0 ? p.BestEndlessWave.ToString() : "—");
            RecordLine(x, ref y, colW, "Best score", p.BestScore > 0 ? p.BestScore.ToString("N0") : "—");
            RecordLine(x, ref y, colW, "Time played", TimeSpan.FromSeconds(p.PlayTime).TotalHours >= 1 ? (int)TimeSpan.FromSeconds(p.PlayTime).TotalHours + " h " + TimeSpan.FromSeconds(p.PlayTime).Minutes + " min" : (int)(p.PlayTime / 60f) + " min");
            y += 12f;
            Ui.Heading(x, y, colW, "Totals");
            y += 30f;
            RecordLine(x, ref y, colW, "Enemies killed", p.Kills.ToString("N0"));
            RecordLine(x, ref y, colW, "Damage dealt", ((long)p.DamageDealt).ToString("N0"));
            RecordLine(x, ref y, colW, "Healing done", ((long)p.HealingDone).ToString("N0"));
            RecordLine(x, ref y, colW, "Times downed", p.Downs.ToString());
            RecordLine(x, ref y, colW, "Allies revived", p.Revives.ToString());

            // Bosses.
            x += colW + 15f;
            y = top;
            Ui.Heading(x, y, colW, "Bosses beaten");
            y += 30f;
            foreach (var b in RecordBosses)
            {
                int n = p.Beaten(b);
                RecordLine(x, ref y, colW, BossName(b), n > 0 ? "× " + n : "—", n > 0 ? Ui.Good : Ui.Muted);
            }

            // Classes and unlocks.
            x += colW + 15f;
            y = top;
            Ui.Heading(x, y, colW, "Wins by class");
            y += 30f;
            var classes = new List<KeyValuePair<string, int>>(p.RunsAs);
            classes.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (classes.Count == 0)
                Ui.Label(new Rect(x, y, colW, 20f), "Finish a run to see your classes here.", 12, Ui.Muted);
            for (int i = 0; i < classes.Count && i < 6; i++)
            {
                int wins;
                p.WinsAs.TryGetValue(classes[i].Key, out wins);
                RecordLine(x, ref y, colW, classes[i].Key, wins + " of " + classes[i].Value, wins > 0 ? Ui.Gold : Ui.Text);
            }
            y = Mathf.Max(y, top + 30f + 6 * 26f) + 12f;
            Ui.Heading(x, y, colW, "Unlocks");
            y += 30f;
            if (Unlocks.All.Count == 0)
                Ui.Label(new Rect(x, y, colW, 60f), "Unlocks are on their way. Your records are already being kept, so anything you've done will count.", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
            foreach (var u in Unlocks.All)
            {
                bool on = p.IsUnlocked(u.Id);
                Ui.Pill(new Rect(x, y, colW, 40f), new Color(1f, 1f, 1f, on ? 0.07f : 0.03f));
                Ui.Label(new Rect(x + 10f, y + 3f, colW - 100f, 18f), u.Name, 13, on ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
                if (on)
                    Ui.Badge(new Rect(x + colW - 86f, y + 4f, 78f, 16f), "UNLOCKED", Ui.GoldButton);
                Ui.Label(new Rect(x + 10f, y + 20f, colW - 20f, 16f), u.Desc, 11, Ui.Muted);
                y += 46f;
            }
            if (Ui.FancyButton(new Rect(panel.x + panel.width / 2f - 100f, panel.yMax - 62f, 200f, 44f), "Back", Ui.Primary, PixelArt.Get("ui_back"), Time.unscaledTime))
                recordsOpen = false;
        }

        void RecordLine(float x, ref float y, float w, string name, string value, Color? valueColor = null)
        {
            Ui.Fill(new Rect(x, y + 23f, w, 1f), new Color(1f, 1f, 1f, 0.05f));
            Ui.Label(new Rect(x, y, w * 0.65f, 22f), name, 13, Ui.Muted);
            Ui.Label(new Rect(x + w * 0.5f, y, w * 0.5f, 22f), value, 14, valueColor ?? Ui.Text, TextAnchor.MiddleRight, true);
            y += 26f;
        }

        // --- The lobby, continuing a run --------------------------------------------------------------

        /// Instead of the mode switch: which run this is.
        void DrawResumeBanner(Rect r, float t)
        {
            var s = session.Resume;
            Ui.Pill(r, new Color(0.04f, 0.05f, 0.08f, 0.9f));
            var inner = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f);
            Ui.Pill(inner, new Color(SaveColor.r * 0.45f, SaveColor.g * 0.45f, SaveColor.b * 0.45f, 0.9f));
            Ui.Fill(new Rect(inner.x + 10f, inner.yMax - 3f, inner.width - 20f, 2f), new Color(1f, 1f, 1f, 0.35f + 0.15f * Mathf.Sin(t * 3f)));
            Ui.Fancy(new Rect(inner.x, inner.y + 4f, inner.width, 24f), "CONTINUING A SAVED RUN", 17, Color.white, new Color(0.85f, 1f, 0.9f), TextAnchor.MiddleCenter);
            Ui.Label(new Rect(inner.x, inner.y + 27f, inner.width, 16f), "Starts at wave " + (s.Wave + 1) + (s.Endless ? " (endless)" : "") + "  ·  each player takes a seat", 11, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
        }

        /// Instead of your loadout: the run's seats, who's in each, and a button to move to a free one.
        void DrawResumeSeats(Rect body, float t)
        {
            var s = session.Resume;
            Ui.Label(new Rect(body.x, body.y, body.width, 20f), "Everyone plays the character they had. Players take back the seat with their name; move to a free seat if yours isn't picked.", 12, Ui.Muted, TextAnchor.MiddleLeft, false, true);
            float y = body.y + 34f;
            for (int i = 0; i < s.Seats.Count; i++)
            {
                var seat = s.Seats[i];
                var color = ToColor(Defs.Get(seat.Role).Color);
                LobbyEntry sitter = null;
                foreach (var e in session.Lobby.Values)
                    if (e.Seat == i)
                        sitter = e;
                bool mine = sitter != null && sitter.Id == session.MyId;
                var r = new Rect(body.x, y, body.width, 104f);
                Ui.Card(r, color, mine ? 0.85f : 0.3f);
                var face = new Rect(r.x + 12f, r.y + 12f, 80f, 80f);
                Ui.Pill(face, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.9f));
                if (BeginUiGl())
                {
                    UiModelAt(new V2(face.center.x, face.center.y), 1.05f);
                    DrawPreviewPlayer(seat.Role, seat.Kit, seat.Weapon, t + i * 1.7f);
                    EndUiGl();
                }
                float tx = face.xMax + 16f;
                Ui.Label(new Rect(tx, r.y + 10f, 360f, 26f), seat.Name, 20, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(tx, r.y + 38f, 420f, 20f), Defs.KitName(seat.Kit) + " " + Defs.Get(seat.Role).Name + "  ·  level " + seat.Level, 14, Color.Lerp(color, Color.white, 0.15f), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(tx, r.y + 62f, 460f, 20f), Defs.Weapon(seat.Role, seat.Kit, seat.Weapon).Name + "  ·  " + Upgrades.Ability(Upgrades.RoleAbility(seat.Role, seat.Kit, seat.Ability)).Name, 12, Ui.Muted);
                var who = new Rect(r.xMax - 210f, r.y + 16f, 196f, 24f);
                if (sitter != null)
                    Ui.Badge(who, mine ? "YOU" : sitter.Name.ToUpperInvariant(), mine ? Ui.Accent : Ui.Neutral);
                else
                {
                    Ui.Badge(who, "FREE SEAT", new Color(0.3f, 0.34f, 0.42f));
                    if (Ui.Button(new Rect(r.xMax - 210f, r.y + 54f, 196f, 36f), "Take this seat", Ui.Primary))
                        session.RequestSeat(i);
                }
                y += 112f;
            }
            if (session.Lobby.Count < s.Seats.Count)
                Ui.Label(new Rect(body.x, y + 4f, body.width, 36f), "Seats nobody takes are left out of the continued run.", 12, Ui.Warn, TextAnchor.UpperLeft, false, true);
        }

        // --- In a match ------------------------------------------------------------------------------------

        /// "Unlocked: ..." when this player's records earn something (RunSaves.cs).
        void DrawUnlockToasts(float cx, float y)
        {
            var toasts = game.UnlockToasts;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                toasts[i].T -= Time.unscaledDeltaTime;
                if (toasts[i].T <= 0f)
                    toasts.RemoveAt(i);
            }
            for (int i = 0; i < toasts.Count && i < 3; i++)
            {
                var t = toasts[toasts.Count - 1 - i];
                float a = Mathf.Clamp01(t.T / 0.5f);
                var r = new Rect(cx - 260f, y + i * 48f, 520f, 42f);
                Ui.Pill(r, new Color(0.12f, 0.09f, 0.02f, 0.85f * a));
                Ui.Shadowed(new Rect(r.x, r.y + 2f, r.width, 22f), "UNLOCKED: " + t.Unlock.Name.ToUpperInvariant(), 16, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, a));
                Ui.Label(new Rect(r.x, r.y + 22f, r.width, 16f), t.Unlock.Desc, 11, new Color(1f, 1f, 1f, 0.8f * a), TextAnchor.MiddleCenter);
            }
        }
    }
}
