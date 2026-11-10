using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arena
{
    /// The lobby's Training tab (the Practice mode's second section; look A of Docs/Mockups/crt-practice-redesign.html): short drills, one
    /// mechanic each, as cartridges in rows by group, with the selected drill's goals on the right. Groundwork only: the drills come from
    /// TrainingLibrary (Core/Training.cs) plus any `.training` files in the training folder. "Load this drill" puts the drill's plan into
    /// the Practice setup, as if the host had built it, and Start then plays it (the same rules: no XP, respawn on).
    public partial class ArenaApp
    {
        const int TrainingTab = 7;
        string trainSelId = "", trainStatus = "";
        List<TrainingDrill> trainExtra;
        static string TrainingFolder => Path.Combine(Application.persistentDataPath, "training");

        void LoadTrainingFiles()
        {
            trainExtra = new List<TrainingDrill>();
            try
            {
                if (!Directory.Exists(TrainingFolder))
                    return;
                foreach (var f in Directory.GetFiles(TrainingFolder, "*.training").OrderBy(f => f))
                {
                    List<string> problems;
                    var d = TrainingDrill.Parse(Path.GetFileNameWithoutExtension(f), File.ReadAllText(f), out problems);
                    if (d.Plan.Waves.Count > 0)
                        trainExtra.Add(d);
                }
            }
            catch (Exception e)
            {
                trainStatus = "Couldn't read the training folder: " + e.Message;
            }
        }

        void DrawTraining(Rect r, float t)
        {
            bool calm = fxReduced, canEdit = session.CanEditPractice;
            if (trainExtra == null)
                LoadTrainingFiles();
            LobbyKit.Panel(r);
            Color am = TrainAmber, dimAm = new Color(am.r, am.g, am.b, 0.65f);

            // Header: the section, and the same rules as the Practice waves.
            float x0 = r.x + 16f, gw = 500f;
            LobbyKit.Spaced(new Rect(x0, r.y + 14f, 300f, 28f), "TRAINING", 22, am, TextAnchor.MiddleLeft, 3f);
            Ui.Label(new Rect(x0 + 130f, r.y + 14f, gw - 130f, 28f), "SHORT DRILLS  ·  ONE MECHANIC EACH", 11, dimAm, TextAnchor.MiddleRight, true);
            Ui.Fill(new Rect(x0, r.y + 46f, gw, 1f), new Color(am.r, am.g, am.b, 0.4f));

            var all = TrainingLibrary.All.Concat(trainExtra).ToList();
            if (trainSelId.Length == 0 && all.Count > 0)
                trainSelId = all[0].Id;
            TrainingDrill sel = all.Find(d => d.Id == trainSelId);

            // The cartridges, row by row.
            float y = r.y + 56f;
            const float cartH = 66f, cartGap = 8f, rowStep = 18f + cartH + 12f;
            float cw = (gw - cartGap * (TrainingLibrary.SlotsPerGroup - 1)) / TrainingLibrary.SlotsPerGroup;
            for (int g = 0; g < TrainingLibrary.Groups.Length; g++)
            {
                LobbyKit.Spaced(new Rect(x0, y, gw, 16f), TrainingLibrary.Groups[g].ToUpperInvariant(), 10, new Color(am.r * 0.85f, am.g * 0.7f, am.b * 0.5f), TextAnchor.MiddleLeft, 1.5f);
                var drills = TrainingLibrary.InGroup(g, trainExtra);
                for (int i = 0; i < TrainingLibrary.SlotsPerGroup; i++)
                {
                    var c = new Rect(x0 + i * (cw + cartGap), y + 18f, cw, cartH);
                    if (i < drills.Count)
                        DrawCartridge(c, drills[i], drills[i] == sel, am, calm);
                    else
                        DrawEmptyCartridge(c, am);
                }
                if (drills.Count > TrainingLibrary.SlotsPerGroup)
                    Ui.Label(new Rect(x0, y + 18f + cartH - 2f, gw, 14f), "+" + (drills.Count - TrainingLibrary.SlotsPerGroup) + " more in this group are not shown yet", 10, dimAm, TextAnchor.UpperLeft, false);
                y += rowStep;
            }

            // The selected drill: name, goal, steps, rules and the key that loads it.
            float dx = x0 + gw + 18f, dw = r.xMax - 16f - dx;
            Ui.Fill(new Rect(dx - 9f, r.y + 54f, 1f, r.height - 54f - 70f), new Color(am.r, am.g, am.b, 0.25f));
            if (sel != null)
            {
                LobbyKit.Spaced(new Rect(dx, r.y + 56f, dw, 16f), "DRILL" + (sel.Example ? "  ·  EXAMPLE" : ""), 10, dimAm, TextAnchor.MiddleLeft, 1.5f);
                CrtUi.Title(new Rect(dx, r.y + 74f, dw, 38f), LobbyKit.Truncate(sel.Name.ToUpperInvariant(), 28, dw, false), 28, Color.white, TextAnchor.MiddleLeft, calm);
                float dy = r.y + 118f;
                if (sel.Goal.Length > 0)
                {
                    float gh = Ui.TextHeight(sel.Goal, 12, dw);
                    Ui.Label(new Rect(dx, dy, dw, gh + 4f), sel.Goal, 12, CrtStyle.Body, TextAnchor.UpperLeft, false, true);
                    dy += gh + 14f;
                }
                if (sel.Steps.Count > 0)
                {
                    LobbyKit.Spaced(new Rect(dx, dy, dw, 16f), "GOALS", 10, dimAm, TextAnchor.MiddleLeft, 1.5f);
                    dy += 20f;
                    foreach (var step in sel.Steps)
                    {
                        float sh = Ui.TextHeight(step, 12, dw - 22f);
                        CrtKit.Frame(new Rect(dx + 2f, dy + 3f, 11f, 11f), 1f, am);
                        Ui.Label(new Rect(dx + 22f, dy, dw - 22f, sh + 4f), step, 12, CrtStyle.Body, TextAnchor.UpperLeft, false, true);
                        dy += sh + 8f;
                    }
                    dy += 6f;
                }
                LobbyKit.Spaced(new Rect(dx, dy, dw, 16f), "RULES", 10, dimAm, TextAnchor.MiddleLeft, 1.5f);
                dy += 20f;
                Ui.Fill(new Rect(dx + 2f, dy + 3f, 11f, 11f), ReviveMintUi);
                Ui.Label(new Rect(dx + 22f, dy, dw - 22f, 18f), "No XP  ·  respawn on", 12, ReviveMintUi, TextAnchor.UpperLeft, false);
                dy += 22f;
                Ui.Label(new Rect(dx + 22f, dy, dw - 22f, 18f), "Your own loadout  ·  solo or with the lobby", 12, CrtStyle.Body, TextAnchor.UpperLeft, false);
                string label = canEdit ? "LOAD THIS DRILL" : "HOST LOADS THE DRILL";
                if (LobbyKit.Key(new Rect(dx, r.yMax - 70f - 54f, dw, 48f), label, false, calm, size: 18, minSize: 11, enabled: canEdit, accent: am))
                {
                    LoadDrill(sel);
                    trainStatus = "Loaded " + sel.Name + ": press Ready, then Start.";
                }
            }
            else
                Ui.Label(new Rect(dx, r.y + 70f, dw, 60f), "No drills yet. Add one to the training folder (FOLDER), then press RELOAD.", 12, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);

            // Footer: where the drills live, and what happened last.
            float fy = r.yMax - 48f;
            Ui.Fill(new Rect(r.x + 14f, fy - 8f, r.width - 28f, 1f), new Color(am.r, am.g, am.b, 0.25f));
            if (LobbyKit.Key(new Rect(x0, fy, 104f, 32f), "FOLDER", false, calm, size: 14, minSize: 10, accent: am))
            {
                Directory.CreateDirectory(TrainingFolder);
                Application.OpenURL("file:///" + TrainingFolder.Replace('\\', '/'));
            }
            if (LobbyKit.Key(new Rect(x0 + 110f, fy, 104f, 32f), "RELOAD", false, calm, size: 14, minSize: 10, accent: am))
            {
                LoadTrainingFiles();
                trainStatus = trainExtra.Count + " drill" + (trainExtra.Count == 1 ? "" : "s") + " in the training folder.";
            }
            Ui.Label(new Rect(x0 + 226f, fy, r.xMax - 16f - (x0 + 226f), 32f), trainStatus.Length > 0 ? trainStatus : "Each drill is a .training text file in the training folder (see Docs/Training.md).", 11,
                trainStatus.Length > 0 ? CrtStyle.Desc : dimAm, TextAnchor.MiddleRight, false);
        }

        void DrawCartridge(Rect c, TrainingDrill d, bool on, Color am, bool calm)
        {
            bool hover = c.Contains(Event.current.mousePosition);
            Ui.Fill(c, new Color(am.r, am.g, am.b, on ? 0.2f : hover ? 0.12f : 0.06f));
            CrtKit.Frame(c, on ? 2f : 1f, new Color(am.r, am.g, am.b, on ? 0.95f : 0.55f));
            Ui.Label(new Rect(c.x + 7f, c.y + 5f, c.width - 14f, 30f), d.Name.ToUpperInvariant(), 12, Color.white, TextAnchor.UpperLeft, true, true);
            if (d.Goal.Length > 0)
                Ui.Label(new Rect(c.x + 7f, c.y + 27f, c.width - 14f, 22f), d.Goal, 10, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            if (d.Example)
                LobbyKit.Spaced(new Rect(c.xMax - 62f, c.yMax - 15f, 56f, 12f), "EXAMPLE", 8, am, TextAnchor.MiddleRight, 1f);
            if (!on && Ui.HitArea(c))
                trainSelId = d.Id;
        }

        static void DrawEmptyCartridge(Rect c, Color am)
        {
            CrtKit.Dashed(c, 1f, new Color(am.r, am.g, am.b, 0.3f));
            LobbyKit.Spaced(c, "EMPTY SLOT", 10, new Color(am.r, am.g, am.b, 0.38f), TextAnchor.MiddleCenter, 1.5f);
        }

        void LoadDrill(TrainingDrill d)
        {
            if (!session.CanEditPractice)
                return;
            practiceSel = practiceScroll = 0;
            practiceName = d.Name;
            practiceLoadOpen = false;
            session.Plan = d.Plan.Copy();
            PracticeChanged();
        }
    }
}
