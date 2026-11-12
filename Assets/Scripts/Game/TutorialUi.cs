using UnityEngine;

namespace Arena
{
    /// The first-run tutorial's prompt (Core/Tutorial.cs, Docs/QuickPlay.md step 3): a CRT card at the top left with the
    /// current lesson, a lamp per step that lights as you do it, the key to press, and SKIP. It slides in with each new lesson.
    public partial class ArenaApp
    {
        int tutorialShownLesson = -1;
        float tutorialLessonAt = -10f;

        static readonly Rect TutorialRect = new Rect(16f, 40f, 300f, 0f);

        /// The step's line and the key that does it (your own bindings).
        static string TutorialLine(TutorialStep s, out string key)
        {
            key = "";
            switch (s)
            {
                case TutorialStep.Move:
                    key = KeyLabel(KeyAction.MoveUp) + KeyLabel(KeyAction.MoveLeft) + KeyLabel(KeyAction.MoveDown) + KeyLabel(KeyAction.MoveRight);
                    return "Move";
                case TutorialStep.Aim: key = "MOUSE"; return "Aim with the mouse";
                case TutorialStep.Shoot: key = KeyLabel(KeyAction.Fire); return "Shoot";
                case TutorialStep.Dodge: key = KeyLabel(KeyAction.Dodge); return "Dodge through danger";
                case TutorialStep.PickCard: return "Pick an upgrade card";
                case TutorialStep.Ability: key = KeyLabel(KeyAction.Ability); return "Use your ability";
                case TutorialStep.Parry: key = KeyLabel(KeyAction.Parry); return "Raise your parry shield";
                case TutorialStep.OpenBuild: return "Open BUILD ROOMS";
                case TutorialStep.DrawRoom: return "Draw a room (click its corners)";
                case TutorialStep.Vote: return "Vote it in";
                case TutorialStep.Gold: return "Collect gold";
                case TutorialStep.Shop: return "Walk into the shop or vendor";
            }
            return s.ToString();
        }

        static string KeyLabel(KeyAction a)
        {
            var k = Keybinds.Get(a);
            return k == KeyCode.None ? "" : Keybinds.KeyName(k);
        }

        void DrawTutorial()
        {
            var tut = game.Tutorial;
            if (ExpandWindowUp)
                tut.Note(TutorialStep.OpenBuild);
            if (!tut.Active || game.State == MatchState.Victory || game.State == MatchState.GameOver)
                return;
            int li = tut.Current;
            if (li < 0)
                return;
            if (li != tutorialShownLesson)
            {
                if (tutorialShownLesson >= 0)
                    tutorialLessonAt = Time.unscaledTime;  // a new lesson slides in (the first is there from the start)
                tutorialShownLesson = li;
            }
            var lesson = Tutorial.Lessons[li];
            bool calm = CabCalm;
            float t = Time.unscaledTime - tutorialLessonAt;
            float slide = calm ? 1f : Mathf.Clamp01(t / 0.35f);
            slide = 1f - (1f - slide) * (1f - slide);
            var gold = CrtStyle.Gold;
            float rowH = 24f;
            float h = 74f + lesson.Steps.Length * rowH + 34f;
            var r = new Rect(TutorialRect.x - (1f - slide) * 40f, TutorialRect.y, TutorialRect.width, h);
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = oldAlpha * slide;
            DkHud(r, gold);
            if (!calm)
                Ui.Fill(new Rect(r.x, r.y, 3f, r.height), new Color(gold.r, gold.g, gold.b, 0.85f));
            // Header: TUTORIAL · lesson n / m, and SKIP.
            HudCrt(new Rect(r.x + 14f, r.y + 6f, 200f, 16f), "TUTORIAL  ·  " + (li + 1) + " / " + Tutorial.Lessons.Length, 15, CrtUi.Dim);
            if (DkKey(new Rect(r.xMax - 62f, r.y + 6f, 52f, 20f), "SKIP", Ui.Neutral, false, 11))
                tut.Skip(game);
            DkTitle(new Rect(r.x + 14f, r.y + 24f, r.width - 28f, 24f), lesson.Title, 20, gold);
            Ui.Label(new Rect(r.x + 14f, r.y + 48f, r.width - 28f, 16f), lesson.When.ToUpperInvariant(), 10, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            float y = r.y + 70f;
            foreach (var s in lesson.Steps)
            {
                string key;
                string line = TutorialLine(s, out key);
                bool ok = tut.Done(s);
                var lamp = new Rect(r.x + 16f, y + 6f, 12f, 12f);
                Color lc = ok ? Ui.Good : CrtUi.Ink;
                CrtKit.Frame(lamp, 1f, new Color(lc.r, lc.g, lc.b, ok ? 1f : 0.55f));
                if (ok)
                    Ui.Fill(new Rect(lamp.x + 3f, lamp.y + 3f, 6f, 6f), lc);
                Ui.Label(new Rect(r.x + 38f, y, r.width - 120f, rowH), line, 13, ok ? CrtUi.Dim : Color.white, TextAnchor.MiddleLeft, false);
                if (key.Length > 0)
                {
                    float kw = Mathf.Max(30f, Ui.TextWidth(key, 11, true) + 14f);
                    var kr = new Rect(r.xMax - 14f - kw, y + 3f, kw, rowH - 6f);
                    DkTag(kr, key, ok ? CrtUi.Dim : CrtUi.Ink);
                }
                y += rowH;
            }
            Ui.Label(new Rect(r.x + 14f, y + 4f, r.width - 28f, 28f), lesson.Hint, 11, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            Ui.Alpha = oldAlpha;
        }
    }
}
