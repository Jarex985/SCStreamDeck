using System.Collections.Generic;

namespace WindowsInput.Native
{
    /// <summary>
    ///     Maps Win32 Virtual-Key codes (VK_*) to Linux evdev keycodes
    ///     (KEY_* from /usr/include/linux/input-event-codes.h).
    ///
    ///     Used by <see cref="WindowsInput.LinuxInputBridge"/> to translate
    ///     <see cref="INPUT"/> arrays into <c>ydotool key</c> arguments when
    ///     the process is running under Wine on Linux.
    ///
    ///     Covers the keyboard keys SCStreamDeck actually emits (alpha-num,
    ///     punctuation, function keys, modifiers, navigation cluster, numpad).
    ///     Missing entries fall through to <see cref="TryGet"/> returning false,
    ///     which the bridge surfaces as a deduplicated log line.
    /// </summary>
    internal static class Vk2Evdev
    {
        private static readonly Dictionary<int, int> Map = new Dictionary<int, int>
        {
            // ── Modifiers ──────────────────────────────────────────
            // Generic VKs: VK_SHIFT/CONTROL/MENU map to left-side by default.
            // Win32 SendInput typically receives the L/R variants (0xA0-0xA5).
            { 0x10, 42 },   // VK_SHIFT       → KEY_LEFTSHIFT
            { 0xA0, 42 },   // VK_LSHIFT      → KEY_LEFTSHIFT
            { 0xA1, 54 },   // VK_RSHIFT      → KEY_RIGHTSHIFT
            { 0x11, 29 },   // VK_CONTROL     → KEY_LEFTCTRL
            { 0xA2, 29 },   // VK_LCONTROL    → KEY_LEFTCTRL
            { 0xA3, 97 },   // VK_RCONTROL    → KEY_RIGHTCTRL
            { 0x12, 56 },   // VK_MENU (alt)  → KEY_LEFTALT
            { 0xA4, 56 },   // VK_LMENU       → KEY_LEFTALT
            { 0xA5, 100 },  // VK_RMENU       → KEY_RIGHTALT
            { 0x5B, 125 },  // VK_LWIN        → KEY_LEFTMETA
            { 0x5C, 126 },  // VK_RWIN        → KEY_RIGHTMETA
            { 0x14, 58 },   // VK_CAPITAL     → KEY_CAPSLOCK

            // ── Whitespace / control ────────────────────────────────
            { 0x08, 14 },   // VK_BACK        → KEY_BACKSPACE
            { 0x09, 15 },   // VK_TAB         → KEY_TAB
            { 0x0D, 28 },   // VK_RETURN      → KEY_ENTER
            { 0x13, 119 },  // VK_PAUSE       → KEY_PAUSE
            { 0x1B,  1 },   // VK_ESCAPE      → KEY_ESC
            { 0x20, 57 },   // VK_SPACE       → KEY_SPACE

            // ── Navigation ──────────────────────────────────────────
            { 0x21, 104 },  // VK_PRIOR (PgUp)→ KEY_PAGEUP
            { 0x22, 109 },  // VK_NEXT (PgDn) → KEY_PAGEDOWN
            { 0x23, 107 },  // VK_END         → KEY_END
            { 0x24, 102 },  // VK_HOME        → KEY_HOME
            { 0x25, 105 },  // VK_LEFT        → KEY_LEFT
            { 0x26, 103 },  // VK_UP          → KEY_UP
            { 0x27, 106 },  // VK_RIGHT       → KEY_RIGHT
            { 0x28, 108 },  // VK_DOWN        → KEY_DOWN
            { 0x2C, 99 },   // VK_SNAPSHOT    → KEY_SYSRQ
            { 0x2D, 110 },  // VK_INSERT      → KEY_INSERT
            { 0x2E, 111 },  // VK_DELETE      → KEY_DELETE

            // ── Top-row digits ──────────────────────────────────────
            { 0x30, 11 },   // 0
            { 0x31,  2 },   // 1
            { 0x32,  3 },
            { 0x33,  4 },
            { 0x34,  5 },
            { 0x35,  6 },
            { 0x36,  7 },
            { 0x37,  8 },
            { 0x38,  9 },
            { 0x39, 10 },   // 9

            // ── Letters ─────────────────────────────────────────────
            { 0x41, 30 },   // A
            { 0x42, 48 },   // B
            { 0x43, 46 },   // C
            { 0x44, 32 },   // D
            { 0x45, 18 },   // E
            { 0x46, 33 },   // F
            { 0x47, 34 },   // G
            { 0x48, 35 },   // H
            { 0x49, 23 },   // I
            { 0x4A, 36 },   // J
            { 0x4B, 37 },   // K
            { 0x4C, 38 },   // L
            { 0x4D, 50 },   // M
            { 0x4E, 49 },   // N
            { 0x4F, 24 },   // O
            { 0x50, 25 },   // P
            { 0x51, 16 },   // Q
            { 0x52, 19 },   // R
            { 0x53, 31 },   // S
            { 0x54, 20 },   // T
            { 0x55, 22 },   // U
            { 0x56, 47 },   // V
            { 0x57, 17 },   // W
            { 0x58, 45 },   // X
            { 0x59, 21 },   // Y
            { 0x5A, 44 },   // Z

            // ── Numpad ──────────────────────────────────────────────
            { 0x60, 82 },   // VK_NUMPAD0     → KEY_KP0
            { 0x61, 79 },   // VK_NUMPAD1     → KEY_KP1
            { 0x62, 80 },
            { 0x63, 81 },
            { 0x64, 75 },
            { 0x65, 76 },
            { 0x66, 77 },
            { 0x67, 71 },
            { 0x68, 72 },
            { 0x69, 73 },   // VK_NUMPAD9     → KEY_KP9
            { 0x6A, 55 },   // VK_MULTIPLY    → KEY_KPASTERISK
            { 0x6B, 78 },   // VK_ADD         → KEY_KPPLUS
            { 0x6D, 74 },   // VK_SUBTRACT    → KEY_KPMINUS
            { 0x6E, 83 },   // VK_DECIMAL     → KEY_KPDOT
            { 0x6F, 98 },   // VK_DIVIDE      → KEY_KPSLASH
            { 0x90, 69 },   // VK_NUMLOCK     → KEY_NUMLOCK
            { 0x91, 70 },   // VK_SCROLL      → KEY_SCROLLLOCK

            // ── Function keys ───────────────────────────────────────
            { 0x70, 59 },   // F1
            { 0x71, 60 },
            { 0x72, 61 },
            { 0x73, 62 },
            { 0x74, 63 },
            { 0x75, 64 },
            { 0x76, 65 },
            { 0x77, 66 },
            { 0x78, 67 },
            { 0x79, 68 },
            { 0x7A, 87 },   // F11
            { 0x7B, 88 },   // F12
            { 0x7C, 183 },  // F13
            { 0x7D, 184 },  // F14
            { 0x7E, 185 },  // F15
            { 0x7F, 186 },
            { 0x80, 187 },
            { 0x81, 188 },
            { 0x82, 189 },
            { 0x83, 190 },
            { 0x84, 191 },
            { 0x85, 192 },
            { 0x86, 193 },
            { 0x87, 194 },  // F24

            // ── OEM punctuation (US layout assumptions; SC keybinds  ─
            //    typically reference the unshifted key) ──────────────
            { 0xBA, 39 },   // VK_OEM_1        ;:   → KEY_SEMICOLON
            { 0xBB, 13 },   // VK_OEM_PLUS     =+   → KEY_EQUAL
            { 0xBC, 51 },   // VK_OEM_COMMA    ,<   → KEY_COMMA
            { 0xBD, 12 },   // VK_OEM_MINUS    -_   → KEY_MINUS
            { 0xBE, 52 },   // VK_OEM_PERIOD   .>   → KEY_DOT
            { 0xBF, 53 },   // VK_OEM_2        /?   → KEY_SLASH
            { 0xC0, 41 },   // VK_OEM_3        `~   → KEY_GRAVE
            { 0xDB, 26 },   // VK_OEM_4        [{   → KEY_LEFTBRACE
            { 0xDC, 43 },   // VK_OEM_5        \|   → KEY_BACKSLASH
            { 0xDD, 27 },   // VK_OEM_6        ]}   → KEY_RIGHTBRACE
            { 0xDE, 40 },   // VK_OEM_7        '"   → KEY_APOSTROPHE
            { 0xE2, 86 },   // VK_OEM_102      <>   → KEY_102ND (ISO key)
        };

        public static bool TryGet(int vk, out int evdev)
        {
            return Map.TryGetValue(vk, out evdev);
        }

        /// <summary>
        ///     Map a PS/2 Set 1 scan code (a.k.a. DirectInput DIK_* code)
        ///     to a Linux evdev keycode.
        ///
        ///     For non-extended keys (no <see cref="KeyboardFlag.ExtendedKey"/>),
        ///     the PS/2 Set 1 scan codes 0x01–0x59 align numerically with
        ///     evdev keycodes 1–89, so a pass-through is correct for the bulk
        ///     of the main keyboard (letters, digits, function row, modifiers,
        ///     punctuation, numpad). For extended keys (Set 1 0xE0-prefix in
        ///     hardware → reported with the EXTENDED flag in <see cref="INPUT"/>),
        ///     the scan code itself is non-unique (e.g. 0x1D is both LCTRL
        ///     and RCTRL depending on the prefix); a small explicit table
        ///     handles those.
        /// </summary>
        public static bool TryFromScan(int scan, bool extended, out int evdev)
        {
            if (extended)
            {
                switch (scan)
                {
                    case 0x1C: evdev =  96; return true; // numpad enter
                    case 0x1D: evdev =  97; return true; // right ctrl
                    case 0x35: evdev =  98; return true; // numpad /
                    case 0x37: evdev =  99; return true; // print screen
                    case 0x38: evdev = 100; return true; // right alt
                    case 0x46: evdev = 119; return true; // ctrl+break / pause
                    case 0x47: evdev = 102; return true; // home
                    case 0x48: evdev = 103; return true; // up
                    case 0x49: evdev = 104; return true; // page up
                    case 0x4B: evdev = 105; return true; // left
                    case 0x4D: evdev = 106; return true; // right
                    case 0x4F: evdev = 107; return true; // end
                    case 0x50: evdev = 108; return true; // down
                    case 0x51: evdev = 109; return true; // page down
                    case 0x52: evdev = 110; return true; // insert
                    case 0x53: evdev = 111; return true; // delete
                    case 0x5B: evdev = 125; return true; // left win
                    case 0x5C: evdev = 126; return true; // right win
                    case 0x5D: evdev = 127; return true; // apps / menu
                    default:   evdev =   0; return false;
                }
            }
            // Non-extended: identity for the canonical range.
            if (scan > 0 && scan <= 0x59)
            {
                evdev = scan;
                return true;
            }
            evdev = 0;
            return false;
        }
    }
}
