using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Every control you can change (Settings → Keybinds, KeybindsUi.cs). Fire and Parry may be a key or any mouse button
    /// (left and right click by default); the rest are keys (or the middle and side mouse buttons). The arrow keys always
    /// move too; the menu keys (Esc, Tab, Enter, F1-F5) are fixed. The number keys can be bound: while the upgrade picker
    /// or the emote bar is open, the numbers it uses pick a card or an emote instead (NumberTaken). Saved in PlayerPrefs by name; the page's
    /// groups and their order are Keybinds.Groups, so new actions go at the end here and wherever they fit there.
    public enum KeyAction
    {
        MoveUp, MoveDown, MoveLeft, MoveRight, Dodge, Ability1, Ultimate, Ability2, Ability3, Ability4, Fire, Parry, WeaponAction,
        Interact, DiscardItem, Gadget, PushWall, Reroll, AimLine, ToggleHud, Mute, Emote, Ping, ReadyUp, OpenDoors, Map, GadgetWheel, Wiki, RotateLeft, RotateRight,
    }

    public static class Keybinds
    {
        public static readonly KeyAction[] All = (KeyAction[])Enum.GetValues(typeof(KeyAction));

        static readonly KeyCode[] Defaults =
        {
            KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.LeftControl, KeyCode.Alpha1, KeyCode.Q, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.LeftAlt,
            KeyCode.F, KeyCode.B, KeyCode.X, KeyCode.None, KeyCode.R, KeyCode.V, KeyCode.H, KeyCode.None, KeyCode.T, KeyCode.Mouse2, KeyCode.Y, KeyCode.Z, KeyCode.M, KeyCode.C, KeyCode.I, KeyCode.LeftBracket, KeyCode.RightBracket,
        };
        static readonly string[] Titles =
        {
            "Move up", "Move down", "Move left", "Move right", "Dodge", "1st ability", "Ultimate", "2nd ability", "3rd ability",
            "4th ability", "Fire", "Parry", "Alternative fire", "Interact", "Discard chest item", "Use gadget", "Push wall", "Reroll upgrades", "Aim line on / off", "Hide / show the HUD", "Mute sound", "Emotes", "Ping", "Ready for the next wave", "Open a door", "Map", "Gadget wheel", "Wiki", "Rotate camera left", "Rotate camera right",
        };
        static readonly string[] Lines =
        {
            "Also the up arrow", "Also the down arrow", "Also the left arrow", "Also the right arrow",
            "A tiny dash with a moment of invulnerability; double tap a movement key does it too",
            "Picked in the lobby", "Picked in the lobby, one per role and class", "Filled by a very rare upgrade card", "Filled by a very rare upgrade card",
            "Filled by a very rare upgrade card", "Hold to shoot (left click by default)", "Hold for the shield toward your cursor (right click by default)",
            "Kunai blink, Twin Blades lunge, Shuriken recall, Bo Staff vault, Kusarigama whirl",
            "Take a chest item, open a mystery door, shut a gate, open the shop again in a shop room", "While standing on it", "Tap: use your gadget (switch it on or off, cast it, lob a charge). Hold and let go: throw it to a teammate where you aim", "Between waves, by a wall (Expanding arena): push it out a tile at a time", "In the upgrade picker", "A line from you toward the cursor", "A clean screen; a slim boss bar stays", "Everything off at once (no key until you give it one)", "Hold to open the emote bar: scroll or click to pick one", "Tap to ping your cursor, hold for the wheel (middle mouse by default)", "Between waves: say you are ready (the wave starts once everyone is)", "Between waves (Expanding arena): open the door picker",
            "Cycles the minimap, the full map and no map",
            "Hold to open the gadget wheel: let go on a gadget to swap it into your hand, in the middle to cancel",
            "Tap: look anything up. Hold and click something on screen (an enemy, a card, a name) to open its page",
            "Turn the view a quarter turn anticlockwise, to see above or below you",
            "Turn the view a quarter turn clockwise, to see above or below you",
        };
        /// The groups on the Keybinds page: heading and the actions in each, in page order (every action in exactly one, except PushWall
        /// (retired with wall push, Nov 18; no key, its C went to the Gadget wheel) and OpenDoors (door picker removed): both kept so saved keys keep their slots).
        public static readonly string[] GroupNames = { "MOVEMENT", "ABILITIES", "WEAPON", "ITEMS AND PICKS", "TOGGLES" };
        public static readonly KeyAction[][] Groups =
        {
            new[] { KeyAction.MoveUp, KeyAction.MoveDown, KeyAction.MoveLeft, KeyAction.MoveRight, KeyAction.Dodge },
            new[] { KeyAction.Ability1, KeyAction.Ultimate, KeyAction.Ability2, KeyAction.Ability3, KeyAction.Ability4 },
            new[] { KeyAction.Fire, KeyAction.Parry, KeyAction.WeaponAction },
            new[] { KeyAction.Interact, KeyAction.DiscardItem, KeyAction.Gadget, KeyAction.GadgetWheel, KeyAction.Reroll },
            new[] { KeyAction.AimLine, KeyAction.ToggleHud, KeyAction.Map, KeyAction.Mute, KeyAction.Emote, KeyAction.Ping, KeyAction.ReadyUp, KeyAction.Wiki, KeyAction.RotateLeft, KeyAction.RotateRight },
        };

        static readonly KeyCode[] keys = (KeyCode[])Defaults.Clone();
        /// The optional second key of each action (KeyCode.None = unset); either key triggers the action.
        static readonly KeyCode[] keys2 = new KeyCode[Defaults.Length];

        /// Bumped whenever a key changes (the HUD's labels are rebuilt then).
        public static int Version { get; private set; }

        /// The action being rebound (the next key you press goes to it), or -1.
        public static int Listening = -1;
        /// Which key of the action is being rebound: 0 = main, 1 = secondary.
        public static int ListeningSlot;
        static int swallowFrame = -1;

        /// Rebinding is going on (or just ended this frame): the game ignores keys, Esc included.
        public static bool Busy => Listening >= 0 || swallowFrame == Time.frameCount || ChatOpen;

        /// The chat box has the keyboard (ChatBox.cs): typing must not move, cast or pause.
        public static bool ChatOpen;

        public static string Title(KeyAction a) => Titles[(int)a];
        public static string Line(KeyAction a) => Lines[(int)a];
        public static KeyCode Get(KeyAction a) => keys[(int)a];
        public static KeyCode DefaultKey(KeyAction a) => Defaults[(int)a];
        public static KeyCode Get2(KeyAction a) => keys2[(int)a];
        public static KeyCode Get(KeyAction a, int slot) => slot == 0 ? keys[(int)a] : keys2[(int)a];
        public static bool IsDefault(KeyAction a) => keys[(int)a] == Defaults[(int)a] && keys2[(int)a] == KeyCode.None;
        /// Starts with no key (Mute).
        public static bool Unbindable(KeyAction a) => Defaults[(int)a] == KeyCode.None;
        /// The key's name, or null when the action has none (for hints that should then say nothing).
        public static string NameOrNull(KeyAction a) => Get(a) == KeyCode.None ? null : KeyName(Get(a));

        static bool Either(KeyCode k, Func<KeyCode, bool> test)
        {
            if (test(k))
                return true;
            if (k == KeyCode.LeftShift) return test(KeyCode.RightShift);   // either Shift counts as Shift
            if (k == KeyCode.LeftAlt) return test(KeyCode.RightAlt);
            if (k == KeyCode.LeftControl) return test(KeyCode.RightControl);
            return false;
        }

        /// How many number keys (1, 2, ...) the upgrade picker (its cards), the emote bar (its slots) and a number-key screen (SetScreenNumbers) are using right now,
        /// set each frame by ArenaApp.Update and EmoteBar.TickEmoteBar. An action bound to one of those keys sits out meanwhile.
        /// A count not set this frame or the last has gone stale (the screen that set it has closed) and counts as 0.
        static int pickerNumbers, emoteNumbers, screenNumbers, pickerFrame = -10, emoteFrame = -10, screenFrame = -10;

        public static void SetPickerNumbers(int n) { pickerNumbers = n; pickerFrame = Time.frameCount; }
        public static void SetEmoteNumbers(int n) { emoteNumbers = n; emoteFrame = Time.frameCount; }
        /// A screen drawn in OnGUI that picks with the number keys (the Hunt map's vote, the build window's tool rail): set each time it draws.
        public static void SetScreenNumbers(int n) { screenNumbers = n; screenFrame = Time.frameCount; }

        static bool NumberTaken(KeyAction a, KeyCode k)
        {
            if (k < KeyCode.Alpha1 || k > KeyCode.Alpha9)
                return false;
            int n = k - KeyCode.Alpha0;
            if (Time.frameCount - pickerFrame <= 1 && n <= pickerNumbers || Time.frameCount - screenFrame <= 1 && n <= screenNumbers)
                return true;
            return a != KeyAction.Emote && Time.frameCount - emoteFrame <= 1 && n <= emoteNumbers;  // (the Emote key itself keeps the bar open)
        }

        // Cached so Down/Held/MoveHeld (many calls a frame) don't allocate a delegate each call.
        static readonly Func<KeyCode, bool> KeyDownTest = Input.GetKeyDown, KeyHeldTest = Input.GetKey;

        /// Pressed this frame (never while rebinding).
        public static bool Down(KeyAction a) => !Busy && (Slot(a, keys[(int)a], KeyDownTest) || Slot(a, keys2[(int)a], KeyDownTest));
        /// Held now (never while rebinding).
        public static bool Held(KeyAction a) => !Busy && (Slot(a, keys[(int)a], KeyHeldTest) || Slot(a, keys2[(int)a], KeyHeldTest));

        static bool Same(KeyCode bound, KeyCode k) => bound != KeyCode.None && Either(bound, c => c == k);

        static bool Slot(KeyAction a, KeyCode k, Func<KeyCode, bool> test) => k != KeyCode.None && !NumberTaken(a, k) && Either(k, test);

        /// Movement: the bound key or its arrow.
        public static bool MoveHeld(KeyAction a)
        {
            KeyCode arrow = a == KeyAction.MoveUp ? KeyCode.UpArrow : a == KeyAction.MoveDown ? KeyCode.DownArrow : a == KeyAction.MoveLeft ? KeyCode.LeftArrow : KeyCode.RightArrow;
            return !Busy && (Slot(a, keys[(int)a], KeyHeldTest) || Slot(a, keys2[(int)a], KeyHeldTest) || Input.GetKey(arrow));
        }

        static bool Mouse01(KeyCode k) => k == KeyCode.Mouse0 || k == KeyCode.Mouse1;

        /// Keys that can't be given to an action: the menus and the dev tools use them (the number keys are fine: NumberTaken
        /// hands them to the upgrade picker and the emote bar while those are open), and left and right click are for
        /// Fire and Parry only (they click everything else).
        public static bool Allowed(KeyCode k, KeyAction a)
        {
            if (Mouse01(k) && a != KeyAction.Fire && a != KeyAction.Parry)
                return false;
            if (k == KeyCode.None || k == KeyCode.Escape || k == KeyCode.Tab || k == KeyCode.Return || k == KeyCode.KeypadEnter
                || k == KeyCode.F1 || k == KeyCode.F2 || k == KeyCode.F3 || k == KeyCode.F4 || k == KeyCode.F5 || k == KeyCode.F6 || k == KeyCode.F7
                || k == KeyCode.UpArrow || k == KeyCode.DownArrow || k == KeyCode.LeftArrow || k == KeyCode.RightArrow)
                return false;
            return true;
        }

        /// Give `a` the key `k` in `slot` (0 = main, 1 = secondary). A key another action (or the other slot) already has swaps
        /// over to this slot's old key, so none is ever doubled; with no old key, the other action loses it (its second key moves
        /// up, else it has no key). Returns the action that was swapped, or -1.
        public static int Set(KeyAction a, KeyCode k, int slot = 0)
        {
            if (!Allowed(k, a))
                return -1;
            var mine = slot == 0 ? keys : keys2;
            KeyCode old = mine[(int)a];
            if (old == k)
                return -1;
            for (int s = 0; s < 2; s++)
            {
                var arr = s == 0 ? keys : keys2;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] != k || (i == (int)a && s == slot))
                        continue;
                    if (old != KeyCode.None && !Allowed(old, (KeyAction)i))  // it would get a mouse button it can't use
                    {
                        Notice = KeyName(k) + " is " + Titles[i] + "'s main key: give that another key first";
                        NoticeAt = Time.unscaledTime;
                        return -1;
                    }
                    arr[i] = old;
                    mine[(int)a] = k;
                    if (old == KeyCode.None && s == 0)  // (an unbound action took its main key: the second moves up)
                    {
                        keys[i] = keys2[i];
                        keys2[i] = KeyCode.None;
                    }
                    Changed();
                    return i == (int)a ? -1 : i;
                }
            }
            mine[(int)a] = k;
            Changed();
            return -1;
        }

        /// Clear the main key; the second key moves up (with none, the action has no key at all).
        public static void Clear(KeyAction a)
        {
            keys[(int)a] = keys2[(int)a];
            keys2[(int)a] = KeyCode.None;
            Changed();
        }

        /// Take every key off the action (the X beside its keys): it can't be pressed until given one again.
        public static void Unbind(KeyAction a)
        {
            if (keys[(int)a] == KeyCode.None && keys2[(int)a] == KeyCode.None)
                return;
            keys[(int)a] = keys2[(int)a] = KeyCode.None;
            Changed();
        }

        /// The action has any key (main or second).
        public static bool Bound(KeyAction a) => keys[(int)a] != KeyCode.None || keys2[(int)a] != KeyCode.None;

        /// Clear the secondary key.
        public static void Clear2(KeyAction a)
        {
            keys2[(int)a] = KeyCode.None;
            Changed();
        }

        public static void Reset(KeyAction a)
        {
            // Back to its own default and no second key, unless another action sits on it now: that one takes this action's key.
            keys2[(int)a] = KeyCode.None;
            if (Unbindable(a))
                keys[(int)a] = KeyCode.None;
            else
                Set(a, Defaults[(int)a]);
            Changed();
        }

        public static void ResetAll()
        {
            Array.Copy(Defaults, keys, keys.Length);
            Array.Clear(keys2, 0, keys2.Length);
            Changed();
        }

        static void Changed()
        {
            Version++;
            SyncKeyText();
            Save();
        }

        /// Hand the bound keys to Core's game text (weapon and upgrade descriptions name them: KeyText.cs).
        static void SyncKeyText()
        {
            Func<KeyAction, string> n = a => Get(a) == KeyCode.LeftAlt ? "Alt" : KeyName(Get(a));
            KeyText.Set(n(KeyAction.WeaponAction), n(KeyAction.Ability1), n(KeyAction.Ability2), n(KeyAction.Ability3), n(KeyAction.Ability4));
        }

        /// The name an action's key is saved under: the new name once saved, else the one before the abilities were
        /// renamed 1st-4th (Ability, AbilityQ, AbilityE, AbilityShift) and Interact (TakeItem), so old keybinds still load.
        static string SavedName(KeyAction a)
        {
            string now = a.ToString();
            if (PlayerPrefs.HasKey("key_" + now))
                return now;
            switch (a)
            {
                case KeyAction.Ability1: return "Ability";
                case KeyAction.Ability2: return "AbilityQ";
                case KeyAction.Ability3: return "AbilityE";
                case KeyAction.Ability4: return "AbilityShift";
                case KeyAction.Interact: return "TakeItem";
                default: return now;
            }
        }

        /// Nov 21's layout (a save from before it): each action still on its old default moves to its new one (Space, Q, E, Left Shift to
        /// 1-4, F to Q for the Ultimate, G to F for Interact), unless another action holds the new key: then it keeps the old one.
        static void MoveOldDefaults()
        {
            PlayerPrefs.SetInt("key_interact_f", 1);
            var acts = new[] { KeyAction.Ability1, KeyAction.Ability2, KeyAction.Ability3, KeyAction.Ability4, KeyAction.Ultimate, KeyAction.Interact };
            var old = new[] { KeyCode.Space, KeyCode.Q, KeyCode.E, KeyCode.LeftShift, KeyCode.F, KeyCode.G };
            var moving = new bool[acts.Length];
            bool any = false;
            for (int i = 0; i < acts.Length; i++)
                if (keys[(int)acts[i]] == old[i])
                {
                    moving[i] = any = true;
                    keys[(int)acts[i]] = KeyCode.None;
                }
            for (int i = 0; i < acts.Length; i++)
                if (moving[i])
                    keys[(int)acts[i]] = KeyUsed(Defaults[(int)acts[i]]) ? (KeyUsed(old[i]) ? KeyCode.None : old[i]) : Defaults[(int)acts[i]];
            if (any)
                Save();
        }

        static bool KeyUsed(KeyCode k)
        {
            for (int j = 0; j < keys.Length; j++)
                if (keys[j] == k || keys2[j] == k)
                    return true;
            return false;
        }

        public static void Load()
        {
            for (int i = 0; i < keys.Length; i++)
            {
                int v = PlayerPrefs.GetInt("key_" + SavedName((KeyAction)i), (int)Defaults[i]);
                keys[i] = v == 0 ? KeyCode.None : Allowed((KeyCode)v, (KeyAction)i) ? (KeyCode)v : Defaults[i];  // (0: cleared with the X)
                if (i == (int)KeyAction.Mute && keys[i] == KeyCode.M && !PlayerPrefs.HasKey("key_" + KeyAction.Map))
                    keys[i] = KeyCode.None;  // a save from before the Map key: M was Mute's old default, and goes to the map now
                int v2 = PlayerPrefs.GetInt("key2_" + SavedName((KeyAction)i), 0);
                keys2[i] = Allowed((KeyCode)v2, (KeyAction)i) ? (KeyCode)v2 : KeyCode.None;
            }
            if (!PlayerPrefs.HasKey("key_gadget_x"))  // a save from before X became the gadget key: swap the old defaults (gadget B, discard X)
            {
                PlayerPrefs.SetInt("key_gadget_x", 1);
                int g = (int)KeyAction.Gadget, d = (int)KeyAction.DiscardItem;
                if (keys[g] == KeyCode.B && keys[d] == KeyCode.X)
                {
                    keys[g] = KeyCode.X;
                    keys[d] = KeyCode.B;
                    Save();
                }
            }
            if (!PlayerPrefs.HasKey("key_interact_f"))  // a save from before Nov 21's layout: abilities on 1-4, Q the Ultimate, F Interact
                MoveOldDefaults();
            keys[(int)KeyAction.PushWall] = keys2[(int)KeyAction.PushWall] = KeyCode.None;  // (retired: its old C goes to the Gadget wheel)
            for (int i = 0; i < keys.Length; i++)  // a hand-edited save with a key on two actions: the later one goes back
            {
                for (int j = 0; j < i; j++)
                    if (keys[i] != KeyCode.None && keys[i] == keys[j])
                        keys[i] = Defaults[i];
                for (int j = 0; j < i; j++)  // (its default is taken too, e.g. an older save gave M to another action: left empty)
                    if (keys[i] != KeyCode.None && keys[i] == keys[j])
                        keys[i] = KeyCode.None;
                for (int j = 0; j < keys.Length; j++)
                    if (keys2[i] != KeyCode.None && (keys2[i] == keys[j] || (j < i && keys2[i] == keys2[j])))
                        keys2[i] = KeyCode.None;
            }
            Version++;
            SyncKeyText();
        }

        static void Save()
        {
            for (int i = 0; i < keys.Length; i++)
            {
                PlayerPrefs.SetInt("key_" + (KeyAction)i, (int)keys[i]);
                PlayerPrefs.SetInt("key2_" + (KeyAction)i, (int)keys2[i]);
            }
            PlayerPrefs.Save();
        }

        // --- Rebinding ------------------------------------------------------------------------------------------------

        static KeyCode[] scan;
        public static string Notice;     // "Swapped with Ultimate", shown on the page for a moment
        public static float NoticeAt = -10f;

        /// Every frame (ArenaApp.Update): while an action is waiting for its key, take the first key or extra mouse button
        /// pressed. Esc cancels. Returns true on the frame it finished.
        public static bool Tick()
        {
            if (Listening < 0)
                return false;
            if (Input.GetKeyDown(KeyCode.Escape))
                return Finish(false, KeyCode.None);
            if (scan == null)
                scan = (KeyCode[])Enum.GetValues(typeof(KeyCode));
            foreach (var k in scan)
                if (k != KeyCode.None && k != KeyCode.Escape && !Mouse01(k) && Input.GetKeyDown(k))  // (left and right click are the page's own buttons)
                {
                    if (!Allowed(k, (KeyAction)Listening))
                    {
                        Notice = "That key is used by the menus";
                        NoticeAt = Time.unscaledTime;
                        return false;  // keep waiting
                    }
                    return Finish(true, k);
                }
            return false;
        }

        static bool Finish(bool set, KeyCode k)
        {
            var a = (KeyAction)Listening;
            Listening = -1;
            swallowFrame = Time.frameCount;
            if (set)
                Assign(a, k, ListeningSlot);
            return true;
        }

        /// Set a key and say what swapped (the page's mouse-button choices use this too).
        public static void Assign(KeyAction a, KeyCode k, int slot = 0)
        {
            Notice = null;
            int other = Set(a, k, slot);  // (a refused swap leaves its own message)
            if (other >= 0 && keys[other] == KeyCode.None)
                Notice = Titles[other] + " has no key now";
            else if (other >= 0)
                Notice = Titles[other] + " now uses " + KeyName(keys[other]) + (keys2[other] != KeyCode.None ? " / " + KeyName(keys2[other]) : "");
            NoticeAt = Time.unscaledTime;
        }

        // --- Names ----------------------------------------------------------------------------------------------------

        /// A key as the HUD and the page write it ("Left Alt", "Space", "Q", "Mouse 3").
        public static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.None: return "—";  // (an action cleared with the X)
                case KeyCode.LeftAlt: return "Left Alt";
                case KeyCode.RightAlt: return "Right Alt";
                case KeyCode.LeftShift: return "Shift";
                case KeyCode.RightShift: return "Right Shift";
                case KeyCode.LeftControl: return "Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.LeftCommand: return "Command";
                case KeyCode.Space: return "Space";
                case KeyCode.Mouse0: return "Left click";
                case KeyCode.Mouse1: return "Right click";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.CapsLock: return "Caps";
                case KeyCode.Delete: return "Delete";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
            }
            if (k >= KeyCode.Mouse2 && k <= KeyCode.Mouse6)
                return k == KeyCode.Mouse2 ? "Middle click" : "Mouse " + ((int)k - (int)KeyCode.Mouse0 + 1);
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9)
                return ((int)k - (int)KeyCode.Alpha0).ToString();
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9)
                return "Num " + ((int)k - (int)KeyCode.Keypad0);
            return k.ToString();
        }

        /// `s` with each "{Action}" (a KeyAction name, e.g. "{Dodge}") swapped for that action's key name.
        public static string Fill(string s)
        {
            if (s == null || s.IndexOf('{') < 0)
                return s;
            foreach (var a in All)
                s = s.Replace("{" + a + "}", KeyName(Get(a)));
            return s;
        }

        static string[] slotLabels;
        static int slotVersion = -1;

        /// The ability bar's key labels, in slot order (1st, 2nd, 3rd, 4th ability, Ultimate), upper case, rebuilt when a key changes.
        public static string[] SlotLabels
        {
            get
            {
                if (slotVersion != Version || slotLabels == null)
                {
                    slotVersion = Version;
                    slotLabels = new[]
                    {
                        KeyName(Get(KeyAction.Ability1)).ToUpperInvariant(), KeyName(Get(KeyAction.Ability2)).ToUpperInvariant(),
                        KeyName(Get(KeyAction.Ability3)).ToUpperInvariant(), KeyName(Get(KeyAction.Ability4)).ToUpperInvariant(),
                        KeyName(Get(KeyAction.Ultimate)).ToUpperInvariant(),
                    };
                }
                return slotLabels;
            }
        }
    }
}
