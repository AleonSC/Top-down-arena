using UnityEngine;

namespace Arena
{
    /// The Vowguard's chain editor (Docs/Vowguard.md): with the Vowguard picked, the weapon's detail box has an EDIT CHAINS key
    /// that opens this over the loadout's right column (as the Saved loadouts drawer does), while the preview on the left keeps
    /// playing your chains. For each stance: its chain as up to ten step keys (click one to select it), the tools for the
    /// selected step (move it earlier or later, remove it; CLEAR leaves one step), and the stance's nine moves: a click adds the
    /// move to the end, or puts it in place of the selected step. The presets fill both chains. Every change is saved to the
    /// profile and shared with the lobby (Session.ShareCosmetics: the host sends everyone's chains with the run's start).
    public partial class ArenaApp
    {
        bool vgEditOpen;   // the editor is open
        bool vgEditable;   // the loadout drawn is yours (a bot's or a saved run's has no editor)
        int vgSelSt = -1, vgSelIdx = -1;

        static readonly string[] VgShort =
        {
            "EDGE CUT", "RIM BASH", "LUNGE", "HOOK", "TWIN STRIKE", "MERCY SPIN", "CLEAVE", "CHARGE", "SLAM",
            "GUARD", "JAB", "DEFLECT", "SIDESTEP", "COVER", "RALLY TAP", "ABSORB", "DOME", "WARD",
        };

        static Color VgUiCol(int stance) => (stance == 0 ? Game.VgGold : Game.VgJade).ToColor();

        /// The detail box's key that opens the editor (its header row, left of the number tiles).
        void DrawVgEditKey(Rect r, bool calm)
        {
            if (DkKey(r, "EDIT CHAINS", vgEditOpen, calm, size: 13, minSize: 9, accent: CrtStyle.Gold))
            {
                vgEditOpen = !vgEditOpen;
                vgSelSt = vgSelIdx = -1;
            }
        }

        byte[] VgPack() => VowguardMotion.Clean(session.Game.Profile?.VgChain);

        void VgSave(VgMove[] valor, VgMove[] sanct)
        {
            var prof = session.Game.Profile;
            if (prof == null)
                return;
            prof.VgChain = VowguardMotion.Pack(valor, sanct);
            prof.Save();
            session.ShareCosmetics();  // (the host refreshes its own entry; a client tells the host)
            if (showcase != null && showcase.Me != null)
            {
                showcase.Me.VgChain = prof.VgChain;  // the preview plays the new chains from their first step
                showcase.Me.VgAtV = showcase.Me.VgAtS = 0;
            }
        }

        void DrawVgEditor(Rect r, bool calm)
        {
            Ui.Fill(r, new Color(0.02f, 0.035f, 0.07f, 0.97f));  // (covers the picks under it)
            LobbyKit.Panel(r);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                vgEditOpen = false;
                Event.current.Use();
                return;
            }
            float x = r.x + 16f, w = r.width - 32f, y = r.y + 10f;
            LobbyKit.Heading(x, y, w - 96f, "Vowguard chains", "up to " + VowguardMotion.ChainMax + " steps a stance", calm);
            if (DkKey(new Rect(r.xMax - 16f - 86f, y, 86f, 26f), "DONE", false, calm, size: 13, minSize: 9))
            {
                vgEditOpen = false;
                return;
            }
            y += 38f;
            var chains = new[] { VowguardMotion.Chain(VgPack(), 0), VowguardMotion.Chain(VgPack(), 1) };
            float avail = r.yMax - 10f - 34f - y;  // (the presets row at the bottom)
            float block = Mathf.Min(214f, (avail - 10f) / 2f);
            for (int st = 0; st < 2; st++)
            {
                if (DrawVgStance(new Rect(x, y, w, block), st, chains, calm))
                    return;  // (changed: drawn again next frame)
                y += block + 10f;
            }
            // The presets.
            float py = r.yMax - 10f - 30f, pw = (w - 6f * 4f) / 5f;
            Ui.Label(new Rect(x, py, pw, 30f), "PRESETS", 11, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < VowguardMotion.Presets.Length; i++)
            {
                var b = new Rect(x + (i + 1) * (pw + 6f), py, pw, 30f);
                var pr = VowguardMotion.Presets[i];
                bool on = VowguardMotion.SamePack(VgPack(), VowguardMotion.Pack(pr[0], pr[1]));
                if (DkKey(b, VowguardMotion.PresetNames[i].ToUpperInvariant(), on, calm, size: 12, minSize: 9))
                {
                    vgSelSt = vgSelIdx = -1;
                    VgSave(pr[0], pr[1]);
                    return;
                }
            }
        }

        /// One stance's block: its name and length, the tools, the step keys, then its nine moves. True when it changed a chain.
        bool DrawVgStance(Rect r, int st, VgMove[][] chains, bool calm)
        {
            var chain = new System.Collections.Generic.List<VgMove>(chains[st]);
            Color col = VgUiCol(st);
            const float gap = 6f;
            float x = r.x, w = r.width, y = r.y;
            Ui.Label(new Rect(x, y, 200f, 22f), Game.VgStanceNames[st].ToUpperInvariant() + "  " + chain.Count + "/" + VowguardMotion.ChainMax, 15, col, TextAnchor.MiddleLeft, true);
            bool sel = vgSelSt == st && vgSelIdx >= 0 && vgSelIdx < chain.Count;
            // The tools: move the selected step earlier or later, remove it, clear to one step.
            float tw = 40f, tx = r.xMax - (tw * 3f + 74f + gap * 3f);
            bool changed = false;
            if (DkKey(new Rect(tx, y, tw, 22f), "<", false, calm, size: 13, minSize: 9, enabled: sel && vgSelIdx > 0) && sel && vgSelIdx > 0)
            {
                var m = chain[vgSelIdx]; chain[vgSelIdx] = chain[vgSelIdx - 1]; chain[vgSelIdx - 1] = m; vgSelIdx--; changed = true;
            }
            if (DkKey(new Rect(tx + tw + gap, y, tw, 22f), ">", false, calm, size: 13, minSize: 9, enabled: sel && vgSelIdx < chain.Count - 1) && sel && vgSelIdx < chain.Count - 1)
            {
                var m = chain[vgSelIdx]; chain[vgSelIdx] = chain[vgSelIdx + 1]; chain[vgSelIdx + 1] = m; vgSelIdx++; changed = true;
            }
            if (DkKey(new Rect(tx + (tw + gap) * 2f, y, tw, 22f), "X", false, calm, size: 13, minSize: 9, danger: true, enabled: chain.Count > 1) && chain.Count > 1)
            {
                chain.RemoveAt(sel ? vgSelIdx : chain.Count - 1);  // (nothing selected: the last step)
                vgSelIdx = sel ? Mathf.Min(vgSelIdx, chain.Count - 1) : -1;
                changed = true;
            }
            if (DkKey(new Rect(tx + (tw + gap) * 3f, y, 74f, 22f), "CLEAR", false, calm, size: 12, minSize: 9, enabled: chain.Count > 1) && chain.Count > 1)
            {
                chain.RemoveRange(1, chain.Count - 1);
                vgSelSt = vgSelIdx = -1;
                changed = true;
            }
            y += 28f;
            // The steps: two rows of five; an empty place after the last says where the next move goes.
            float sw = (w - gap * 4f) / 5f, sh = 28f;
            for (int i = 0; i < VowguardMotion.ChainMax; i++)
            {
                var b = new Rect(x + (i % 5) * (sw + gap), y + (i / 5) * (sh + gap), sw, sh);
                if (i >= chain.Count)
                {
                    DkKey(b, i == chain.Count ? "+ ADD" : "", false, calm, size: 11, minSize: 8, quiet: true, enabled: false);
                    continue;
                }
                var mv = VowguardMotion.Of(chain[i]);
                bool on = vgSelSt == st && vgSelIdx == i;
                if (DkKey(b, VgShort[(int)chain[i]], on, calm, size: 11, minSize: 8, accent: mv.Fin ? Color.white : (Color?)null))
                {
                    vgSelSt = on ? -1 : st;
                    vgSelIdx = on ? -1 : i;
                }
                Ui.Label(new Rect(b.x + 3f, b.y + 1f, 20f, 11f), (i + 1).ToString(), 9, col.WithAlpha(0.8f), TextAnchor.UpperLeft, true);
                if (b.Contains(Event.current.mousePosition))
                    VgTip(b, mv);
            }
            y += sh * 2f + gap + 10f;
            // The stance's nine moves: a click adds one (or puts it in place of the selected step).
            float mw = (w - gap * 2f) / 3f, mh = Mathf.Min(28f, (r.yMax - y - gap * 2f) / 3f);
            for (int i = 0; i < VowguardMotion.MovesPerStance; i++)
            {
                var id = (VgMove)(st * VowguardMotion.MovesPerStance + i);
                var mv = VowguardMotion.Of(id);
                var b = new Rect(x + (i % 3) * (mw + gap), y + (i / 3) * (mh + gap), mw, mh);
                bool canAdd = sel || chain.Count < VowguardMotion.ChainMax;
                if (DkKey(b, mv.Name.ToUpperInvariant(), false, calm, size: 12, minSize: 8, accent: mv.Fin ? Color.white : col, enabled: canAdd) && canAdd)
                {
                    if (sel)
                        chain[vgSelIdx] = id;
                    else
                        chain.Add(id);
                    changed = true;
                }
                if (b.Contains(Event.current.mousePosition))
                    VgTip(b, mv);
            }
            if (!changed)
                return false;
            chains[st] = chain.ToArray();
            VgSave(chains[0], chains[1]);
            return true;
        }

        void VgTip(Rect b, VowguardMotion.Move mv)
        {
            TipStart(b, mv.Name, VgUiCol(mv.Stance));
            TipAdd((mv.Fin ? "Finisher · " : "Link · ") + mv.Dur.ToString("0.00") + " s", 11, CrtStyle.Dim, true);
            TipAdd(mv.Blurb, 13, Ui.Text);
        }
    }
}
