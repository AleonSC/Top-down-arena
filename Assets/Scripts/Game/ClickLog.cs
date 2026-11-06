using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// A temporary debugging aid for lost clicks: F9 shows a readout of the last mouse presses and releases (top-left) and writes the
    /// same lines to the Unity console. Per press / release: when, where, whether some control swallowed the event (Used), whether a
    /// button reported a click, the frame time, and the IMGUI hot control. A press with no "CLICK" after its release means no button took it.
    public partial class ArenaApp
    {
        bool clickLogOn;
        readonly List<string> clickLogLines = new List<string>();
        bool clickLogPending;
        EventType clickLogType;
        Vector2 clickLogPos;
        int clickLogCount, clickLogHotBefore;

        void ClickLogBegin()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F9)
            {
                clickLogOn = !clickLogOn;
                clickLogLines.Clear();
            }
            clickLogPending = false;
            if (clickLogOn && (e.type == EventType.MouseDown || e.type == EventType.MouseUp) && e.button == 0)
            {
                clickLogPending = true;
                clickLogType = e.type;
                clickLogPos = e.mousePosition;
                clickLogCount = e.clickCount;
                clickLogHotBefore = GUIUtility.hotControl;
            }
        }

        void ClickLogEnd(bool clicked)
        {
            if (!clickLogOn)
                return;
            var e = Event.current;
            if (clickLogPending)
            {
                string line = Time.unscaledTime.ToString("0.000") + "  " + (clickLogType == EventType.MouseDown ? "DOWN" : "UP  ") + " x" + clickLogCount
                    + "  (" + Mathf.RoundToInt(clickLogPos.x) + "," + Mathf.RoundToInt(clickLogPos.y) + ")"
                    + "  " + (e.type == EventType.Used ? "used" : "NOT used") + "  hot " + clickLogHotBefore + "->" + GUIUtility.hotControl
                    + "  " + (Time.unscaledDeltaTime * 1000f).ToString("0") + "ms/frame" + (clicked ? "  CLICK" : "");
                clickLogLines.Add(line);
                Debug.Log("[click] " + line);
                if (clickLogLines.Count > 16)
                    clickLogLines.RemoveAt(0);
            }
            else if (clicked && clickLogLines.Count > 0)
            {
                clickLogLines[clickLogLines.Count - 1] += "  CLICK";
            }
            if (e.type != EventType.Repaint)
                return;
            float h = 16f * (clickLogLines.Count + 1) + 8f;
            Ui.Fill(new Rect(6f, 6f, 560f, h), new Color(0f, 0f, 0f, 0.8f));
            Ui.Label(new Rect(12f, 8f, 548f, 16f), "CLICK LOG (F9 to close): press / release, swallowed?, hot control, frame time", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < clickLogLines.Count; i++)
                Ui.Label(new Rect(12f, 24f + 16f * i, 548f, 16f), clickLogLines[i], 11, Color.white, TextAnchor.MiddleLeft, false);
        }
    }
}
