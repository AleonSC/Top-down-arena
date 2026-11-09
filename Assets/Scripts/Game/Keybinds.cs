using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Every control you can change (Settings → Keybinds, KeybindsUi.cs). Fire and Parry may be a key or any mouse button
    /// (left and right click by default); the rest are keys (or the middle and side mouse buttons). The arrow keys always
    /// move too; the menu keys (Esc, Tab, Enter, F1-F5) are fixed. The number keys can be bound: while the upgrade picker
    /// or the emote bar is open, the numbers it uses pick a card or an emote instead (NumberTaken). Saved in PlayerPrefs by name, so the order here is
    /// only the order on the page (each group is a run of this list).
    public enum KeyAction
    {
        MoveUp, MoveDown, MoveLeft, MoveRight, Dodge, Ability, Ultimate, AbilityQ, AbilityE, AbilityShift, Fire, Parry, WeaponAction,
        TakeItem, DiscardItem, Reroll, AimLine, ToggleHud, Mute, Emote, Ping, ReadyUp,
    }

    public static class Keybinds
    {
        public static readonly KeyAction[] All = (KeyAction[])Enum.GetValues(typeof(KeyAction));

        static readonly KeyCode[] Defaults =
        {
            KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.LeftControl, KeyCode.Space, KeyCode.F, KeyCode.Q, KeyCode.E, KeyCode.LeftShift,
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.LeftAlt,
            KeyCode.G, KeyCode.X, KeyCode.R, KeyCode.V, KeyCode.H, KeyCode.M, KeyCode.T, KeyCode.Mouse2, KeyCode.Y,
        };
        static readonly string[] Titles =
        {
            "Move up", "Move down", "Move left", "Move right", "Dodge", "Role ability", "Ultimate", "Bonus ability 1", "Bonus ability 2",
            "Bonus ability 3", "Fire", "Parry", "Weapon secondary", "Take chest item", "Discard chest item", "Reroll upgrades", "Aim line on / off", "Hide / show the HUD", "Mute sound", "Emotes", "Ping", "Ready for the next wave",
        };
        static readonly string[] Lines =
        {
            "Also the up arrow", "Also the down arrow", "Also the left arrow", "Also the right arrow",
            "A tiny dash with a moment of invulnerability; double tap a movement key does it too",
            "Your role's ability", "One per role and class", "The first bonus ability slot (the key it starts on)", "The second bonus ability slot",
            "The third bonus ability slot", "Hold to shoot (left click by default)", "Hold for the shield toward your cursor (right click by default)",
            "Kunai blink, Twin Blades lunge, Shuriken recall, Bo Staff vault, Kusarigama whirl",
            "While standing on it", "While standing on it", "In the upgrade picker", "A line from you toward the cursor", "A clean screen; a slim boss bar stays", "Everything off at once", "Hold to open the emote bar: scroll or click to pick one", "Tap to ping your cursor, hold for the wheel (middle mouse by default)", "Between waves: say you are ready (the wave starts once everyone is)",
        };
        /// The groups on the Keybinds page: heading and the first action of each.
        public static readonly string[] GroupNames = { "MOVEMENT", "ABILITIES", "WEAPON", "ITEMS AND PICKS", "TOGGLES" };
        public static readonly KeyAction[] GroupStart = { KeyAction.MoveUp, KeyAction.Ability, KeyAction.Fire, KeyAction.TakeItem, KeyAction.AimLine };

        static readonly KeyCode[] keys = (KeyCode[])Defaults.Clone();

        /// Bumped whenever a key changes (the HUD's labels are rebuilt then).
        public static int Version { get; private set; }

        /// The action being rebound (the next key you press goes to it), or -1.
        public static int Listening = -1;
        static int swallowFrame = -1;

        /// Rebinding is going on (or just ended this frame): the game ignores keys, Esc included.
        public static bool Busy => Listening >= 0 || swallowFrame == Time.frameCount || ChatOpen;

        /// The chat box has the keyboard (ChatBox.cs): typing must not move, cast or pause.
        public static bool ChatOpen;

        public static string Title(KeyAction a) => Titles[(int)a];
        public static string Line(KeyAction a) => Lines[(int)a];
        public static KeyCode Get(KeyAction a) => keys[(int)a];
        public static bool IsDefault(KeyAction a) => keys[(int)a] == Defaults[(int)a];

        static bool Either(KeyCode k, Func<KeyCode, bool> test)
        {
            if (test(k))
                return true;
            if (k == KeyCode.LeftShift) return test(KeyCode.RightShift);   // either Shift counts as Shift
            if (k == KeyCode.LeftAlt) return test(KeyCode.RightAlt);
            if (k == KeyCode.LeftControl) return test(KeyCode.RightControl);
            return false;
        }

        /// How many number keys (1, 2, ...) the upgrade picker (its cards) and the emote bar (its slots) are using right now,
        /// set each frame by ArenaApp.Update and EmoteBar.TickEmoteBar. An action bound to one of those keys sits out meanwhile.
        /// A count not set this frame or the last has gone stale (the screen that set it has closed) and counts as 0.
        static int pickerNumbers, emoteNumbers, pickerFrame = -10, emoteFrame = -10;

        public static void SetPickerNumbers(int n) { pickerNumbers = n; pickerFrame = Time.frameCount; }
        public static void SetEmoteNumbers(int n) { emoteNumbers = n; emoteFrame = Time.frameCount; }

        static bool NumberTaken(KeyAction a)
        {
            KeyCode k = keys[(int)a];
            if (k < KeyCode.Alpha1 || k > KeyCode.Alpha9)
                return false;
            int n = k - KeyCode.Alpha0;
            if (Time.frameCount - pickerFrame <= 1 && n <= pickerNumbers)
                return true;
            return a != KeyAction.Emote && Time.frameCount - emoteFrame <= 1 && n <= emoteNumbers;  // (the Emote key itself keeps the bar open)
        }

        /// Pressed this frame (never while rebinding).
        public static bool Down(KeyAction a) => !Busy && !NumberTaken(a) && Either(keys[(int)a], Input.GetKeyDown);
        /// Held now (never while rebinding).
        public static bool Held(KeyAction a) => !Busy && !NumberTaken(a) && Either(keys[(int)a], Input.GetKey);

        /// Movement: the bound key or its arrow.
        public static bool MoveHeld(KeyAction a)
        {
            KeyCode arrow = a == KeyAction.MoveUp ? KeyCode.UpArrow : a == KeyAction.MoveDown ? KeyCode.DownArrow : a == KeyAction.MoveLeft ? KeyCode.LeftArrow : KeyCode.RightArrow;
            return !Busy && ((!NumberTaken(a) && Either(keys[(int)a], Input.GetKey)) || Input.GetKey(arrow));
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
                || k == KeyCode.F1 || k == KeyCode.F2 || k == KeyCode.F3 || k == KeyCode.F4 || k == KeyCode.F5
                || k == KeyCode.UpArrow || k == KeyCode.DownArrow || k == KeyCode.LeftArrow || k == KeyCode.RightArrow)
                return false;
            return true;
        }

        /// Give `a` the key `k`. A key another action already has swaps over to this one's old key, so none is ever doubled.
        /// Returns the action that was swapped, or -1.
        public static int Set(KeyAction a, KeyCode k)
        {
            if (!Allowed(k, a))
                return -1;
            int swapped = -1;
            for (int i = 0; i < keys.Length; i++)
                if (i != (int)a && keys[i] == k)
                {
                    if (!Allowed(keys[(int)a], (KeyAction)i))  // it would be left holding a mouse button it can't use
                    {
                        Notice = KeyName(k) + " is " + Titles[i] + "'s, which can't use " + KeyName(keys[(int)a]) + ": give it another key first";
                        NoticeAt = Time.unscaledTime;
                        return -1;
                    }
                    keys[i] = keys[(int)a];
                    swapped = i;
                }
            keys[(int)a] = k;
            Changed();
            return swapped;
        }

        public static void Reset(KeyAction a)
        {
            // Back to its own default, unless another action sits on it now: that one takes this action's key.
            Set(a, Defaults[(int)a]);
        }

        public static void ResetAll()
        {
            Array.Copy(Defaults, keys, keys.Length);
            Changed();
        }

        static void Changed()
        {
            Version++;
            Save();
        }

        public static void Load()
        {
            for (int i = 0; i < keys.Length; i++)
            {
                int v = PlayerPrefs.GetInt("key_" + (KeyAction)i, (int)Defaults[i]);
                keys[i] = Allowed((KeyCode)v, (KeyAction)i) ? (KeyCode)v : Defaults[i];
            }
            for (int i = 0; i < keys.Length; i++)  // a hand-edited save with a key on two actions: the later one goes back
                for (int j = 0; j < i; j++)
                    if (keys[i] == keys[j])
                        keys[i] = Defaults[i];
            Version++;
        }

        static void Save()
        {
            for (int i = 0; i < keys.Length; i++)
                PlayerPrefs.SetInt("key_" + (KeyAction)i, (int)keys[i]);
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
                Assign(a, k);
            return true;
        }

        /// Set a key and say what swapped (the page's mouse-button choices use this too).
        public static void Assign(KeyAction a, KeyCode k)
        {
            Notice = null;
            int other = Set(a, k);  // (a refused swap leaves its own message)
            if (other >= 0)
                Notice = Titles[other] + " now uses " + KeyName(keys[other]);
            NoticeAt = Time.unscaledTime;
        }

        // --- Names ----------------------------------------------------------------------------------------------------

        /// A key as the HUD and the page write it ("Left Alt", "Space", "Q", "Mouse 3").
        public static string KeyName(KeyCode k)
        {
            switch (k)
            {
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

        static string[] slotLabels;
        static int slotVersion = -1;

        /// The ability bar's key labels, in slot order (Space, Q, E, Shift, ultimate), upper case, rebuilt when a key changes.
        public static string[] SlotLabels
        {
            get
            {
                if (slotVersion != Version || slotLabels == null)
                {
                    slotVersion = Version;
                    slotLabels = new[]
                    {
                        KeyName(Get(KeyAction.Ability)).ToUpperInvariant(), KeyName(Get(KeyAction.AbilityQ)).ToUpperInvariant(),
                        KeyName(Get(KeyAction.AbilityE)).ToUpperInvariant(), KeyName(Get(KeyAction.AbilityShift)).ToUpperInvariant(),
                        KeyName(Get(KeyAction.Ultimate)).ToUpperInvariant(),
                    };
                }
                return slotLabels;
            }
        }
    }
}
