using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Every keyboard control you can change (Settings → Keybinds, KeybindsUi.cs). The arrow keys always move too; the
    /// mouse buttons (fire, parry) and the menu keys (Esc, Tab, Enter, 1-8, F1-F4) are fixed. Saved in PlayerPrefs.
    public enum KeyAction
    {
        MoveUp, MoveDown, MoveLeft, MoveRight, Ability, WeaponAction, Ultimate, AbilityQ, AbilityE, AbilityShift,
        TakeItem, DiscardItem, Reroll, AimLine, ToggleHud, Mute,
    }

    public static class Keybinds
    {
        public static readonly KeyAction[] All = (KeyAction[])Enum.GetValues(typeof(KeyAction));

        static readonly KeyCode[] Defaults =
        {
            KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.LeftAlt, KeyCode.F, KeyCode.Q, KeyCode.E, KeyCode.LeftShift,
            KeyCode.G, KeyCode.X, KeyCode.R, KeyCode.V, KeyCode.H, KeyCode.M,
        };
        static readonly string[] Titles =
        {
            "Move up", "Move down", "Move left", "Move right", "Role ability", "Weapon secondary", "Ultimate", "Bonus ability 1", "Bonus ability 2",
            "Bonus ability 3", "Take chest item", "Discard chest item", "Reroll upgrades", "Aim line on / off", "Hide / show the HUD", "Mute sound",
        };
        static readonly string[] Lines =
        {
            "Also the up arrow", "Also the down arrow", "Also the left arrow", "Also the right arrow",
            "Your role's ability", "Kunai blink, Twin Blades lunge, Shuriken recall, Bo Staff vault, Kusarigama reel",
            "One per role and class", "The first bonus ability slot (the key it starts on)", "The second bonus ability slot", "The third bonus ability slot",
            "While standing on it", "While standing on it", "In the upgrade picker", "A line from you toward the cursor", "A clean screen; a slim boss bar stays", "Everything off at once",
        };
        /// The groups on the Keybinds page: heading and the first action of each.
        public static readonly string[] GroupNames = { "MOVEMENT", "COMBAT", "ITEMS AND PICKS", "TOGGLES" };
        public static readonly KeyAction[] GroupStart = { KeyAction.MoveUp, KeyAction.Ability, KeyAction.TakeItem, KeyAction.AimLine };

        static readonly KeyCode[] keys = (KeyCode[])Defaults.Clone();

        /// Bumped whenever a key changes (the HUD's labels are rebuilt then).
        public static int Version { get; private set; }

        /// The action being rebound (the next key you press goes to it), or -1.
        public static int Listening = -1;
        static int swallowFrame = -1;

        /// Rebinding is going on (or just ended this frame): the game ignores keys, Esc included.
        public static bool Busy => Listening >= 0 || swallowFrame == Time.frameCount;

        public static string Title(KeyAction a) => Titles[(int)a];
        public static string Line(KeyAction a) => Lines[(int)a];
        public static KeyCode Get(KeyAction a) => keys[(int)a];
        public static KeyCode DefaultKey(KeyAction a) => Defaults[(int)a];
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

        /// Pressed this frame (never while rebinding).
        public static bool Down(KeyAction a) => !Busy && Either(keys[(int)a], Input.GetKeyDown);
        /// Held now (never while rebinding).
        public static bool Held(KeyAction a) => !Busy && Either(keys[(int)a], Input.GetKey);

        /// Movement: the bound key or its arrow.
        public static bool MoveHeld(KeyAction a)
        {
            KeyCode arrow = a == KeyAction.MoveUp ? KeyCode.UpArrow : a == KeyAction.MoveDown ? KeyCode.DownArrow : a == KeyAction.MoveLeft ? KeyCode.LeftArrow : KeyCode.RightArrow;
            return !Busy && (Either(keys[(int)a], Input.GetKey) || Input.GetKey(arrow));
        }

        /// Keys that can't be given to an action: the menus and the dev tools use them, and the mouse buttons are fire and parry.
        public static bool Allowed(KeyCode k)
        {
            if (k == KeyCode.None || k == KeyCode.Escape || k == KeyCode.Tab || k == KeyCode.Return || k == KeyCode.KeypadEnter
                || k == KeyCode.F1 || k == KeyCode.F2 || k == KeyCode.F3 || k == KeyCode.F4 || k == KeyCode.Mouse0 || k == KeyCode.Mouse1
                || k == KeyCode.UpArrow || k == KeyCode.DownArrow || k == KeyCode.LeftArrow || k == KeyCode.RightArrow)
                return false;
            return !(k >= KeyCode.Alpha1 && k <= KeyCode.Alpha8);  // the upgrade picker's card keys
        }

        /// Give `a` the key `k`. A key another action already has swaps over to this one's old key, so none is ever doubled.
        /// Returns the action that was swapped, or -1.
        public static int Set(KeyAction a, KeyCode k)
        {
            if (!Allowed(k))
                return -1;
            int swapped = -1;
            for (int i = 0; i < keys.Length; i++)
                if (i != (int)a && keys[i] == k)
                {
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
                keys[i] = Allowed((KeyCode)v) ? (KeyCode)v : Defaults[i];
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
                if (k != KeyCode.None && k != KeyCode.Escape && Input.GetKeyDown(k))
                {
                    if (!Allowed(k))
                    {
                        Notice = k == KeyCode.Mouse0 || k == KeyCode.Mouse1 ? "The mouse buttons are fire and parry" : "That key is used by the menus";
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
            {
                int other = Set(a, k);
                Notice = other >= 0 ? Titles[other] + " now uses " + KeyName(keys[other]) : null;
                NoticeAt = Time.unscaledTime;
            }
            return true;
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
