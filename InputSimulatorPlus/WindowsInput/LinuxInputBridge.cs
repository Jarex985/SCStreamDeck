using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using WindowsInput.Native;

namespace WindowsInput
{
    /// <summary>
    ///     Receives <see cref="INPUT"/> arrays built for Win32 SendInput and
    ///     forwards them through the Linux <c>ydotool</c> CLI. Used only
    ///     when the host process is running under Wine
    ///     (see <see cref="WineDetector"/>).
    ///
    ///     Strategy: bucket all keyboard events from one DispatchInput call
    ///     into a SINGLE <c>ydotool key …</c> invocation so a 4-key combo
    ///     costs one fork+exec instead of four. Mouse clicks and wheel
    ///     events are emitted separately because they use different
    ///     ydotool verbs.
    /// </summary>
    internal static class LinuxInputBridge
    {
        // Wine maps `/` to `Z:\`, so Linux absolute paths become Z: paths.
        private static readonly string YdotoolPath   = ResolveYdotool();
        private static readonly string YdotoolSocket = ResolveSocketPath();

        // Surface unknown VK and scan codes once per process. The reader of
        // these logs will see them via Wine's stderr aggregated in the
        // OpenDeck plugin log file.
        private static readonly HashSet<int> _unknownVks   = new HashSet<int>();
        private static readonly HashSet<long> _unknownScans = new HashSet<long>();
        private static readonly object _unknownLock = new object();

        public static void DispatchInput(INPUT[] inputs)
        {
            if (inputs == null || inputs.Length == 0) return;

            // Bucket keyboard events into one ydotool invocation.
            var keyArgs = new List<string>(inputs.Length);
            // Mouse events run sequentially (typically 1 per call anyway).
            var mouseCmds = new List<(string verb, string args)>();

            foreach (var inp in inputs)
            {
                if (inp.Type == (uint)InputType.Keyboard)
                {
                    AccumulateKeyboard(inp.Data.Keyboard, keyArgs);
                }
                else if (inp.Type == (uint)InputType.Mouse)
                {
                    AccumulateMouse(inp.Data.Mouse, mouseCmds);
                }
                // Hardware input (InputType.Hardware) is ignored — SCStreamDeck
                // doesn't synthesize HW events.
            }

            if (keyArgs.Count > 0)
            {
                Run("key", string.Join(" ", keyArgs));
            }
            foreach (var (verb, args) in mouseCmds)
            {
                Run(verb, args);
            }
        }

        private static void AccumulateKeyboard(KEYBDINPUT k, List<string> args)
        {
            bool isUp     = (k.Flags & (uint)KeyboardFlag.KeyUp)      != 0;
            bool isScan   = (k.Flags & (uint)KeyboardFlag.ScanCode)   != 0;
            bool isExt    = (k.Flags & (uint)KeyboardFlag.ExtendedKey) != 0;

            // KEYEVENTF_UNICODE is rare here (SCStreamDeck targets game
            // keybinds, not text input). If we ever see one, fall back
            // to `ydotool type` for the single char.
            if ((k.Flags & (uint)KeyboardFlag.Unicode) != 0)
            {
                if (!isUp && k.Scan != 0)
                {
                    var s = char.ConvertFromUtf32(k.Scan);
                    Run("type", "--key-delay=0 -- " + EscapeShell(s));
                }
                return;
            }

            int evdev;

            // SCStreamDeck builds inputs with KEYEVENTF_SCANCODE: the real
            // key identity is in `Scan` (a DirectInput DIK_*/PS/2 Set 1
            // scan code), and `KeyCode`(Vk) is 0. Translate via the scan-code
            // path. Also try the scan path as a fallback whenever Vk is 0
            // but Scan is populated, even if the SCANCODE flag wasn't set.
            if (isScan || (k.KeyCode == 0 && k.Scan != 0))
            {
                if (Vk2Evdev.TryFromScan(k.Scan, isExt, out evdev))
                {
                    args.Add(evdev + ":" + (isUp ? "0" : "1"));
                    return;
                }
                RecordUnknownScan(k.Scan, isExt);
                return;
            }

            int vk = k.KeyCode;
            if (Vk2Evdev.TryGet(vk, out evdev))
            {
                args.Add(evdev + ":" + (isUp ? "0" : "1"));
            }
            else
            {
                RecordUnknownVk(vk);
            }
        }

        private static void AccumulateMouse(MOUSEINPUT m, List<(string, string)> cmds)
        {
            uint f = m.Flags;

            // Wheel / horizontal wheel — `mouseData` is a signed int as wheel delta.
            // ydotool's wheel API uses `mousemove --wheel -- 0 <delta>` (vertical)
            // or with --xwheel for horizontal (depending on ydotool version).
            if ((f & (uint)MouseFlag.VerticalWheel) != 0)
            {
                int delta = unchecked((int)m.MouseData);
                cmds.Add(("mousemove", "--wheel -- 0 " + delta));
                return;
            }
            if ((f & (uint)MouseFlag.HorizontalWheel) != 0)
            {
                int delta = unchecked((int)m.MouseData);
                cmds.Add(("mousemove", "--wheel -- " + delta + " 0"));
                return;
            }

            // Buttons. ydotool encodes click commands as hex bitmask:
            //   high nibble: 0x4 = press, 0x8 = release, 0xC = press+release
            //   low nibble : 0x0 = left, 0x1 = right, 0x2 = middle, 0x3/0x4 = side
            // We do the simplest faithful thing: separate down/up if they
            // arrive separately, atomic press+release when both flags are
            // set in the same INPUT (which is what SendInput-style users
            // usually do for simple clicks).
            int btn = -1;
            bool down = false, up = false;
            if      ((f & (uint)MouseFlag.LeftDown)   != 0) { btn = 0; down = true; }
            else if ((f & (uint)MouseFlag.LeftUp)     != 0) { btn = 0; up   = true; }
            else if ((f & (uint)MouseFlag.RightDown)  != 0) { btn = 1; down = true; }
            else if ((f & (uint)MouseFlag.RightUp)    != 0) { btn = 1; up   = true; }
            else if ((f & (uint)MouseFlag.MiddleDown) != 0) { btn = 2; down = true; }
            else if ((f & (uint)MouseFlag.MiddleUp)   != 0) { btn = 2; up   = true; }
            else if ((f & (uint)MouseFlag.XDown)      != 0) { btn = 3 + (int)(m.MouseData - 1); down = true; }
            else if ((f & (uint)MouseFlag.XUp)        != 0) { btn = 3 + (int)(m.MouseData - 1); up   = true; }

            if (btn < 0) return;

            int hi = (down && up) ? 0xC : (down ? 0x4 : (up ? 0x8 : 0));
            if (hi == 0) return;
            int code = (hi << 4) | (btn & 0x0F);
            cmds.Add(("click", "0x" + code.ToString("X2")));
        }

        private static void Run(string verb, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = YdotoolPath,
                    Arguments              = verb + " " + args,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                };
                psi.EnvironmentVariables["YDOTOOL_SOCKET"] = YdotoolSocket;
                using (var p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        // 500 ms is a generous guard — ydotool typically returns in 2-5 ms.
                        p.WaitForExit(500);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("[LinuxInputBridge] ydotool spawn failed: " + e.Message);
            }
        }

        private static string ResolveYdotool()
        {
            // Override via env var for non-standard installs.
            string fromEnv = Environment.GetEnvironmentVariable("YDOTOOL_PATH");
            if (!string.IsNullOrEmpty(fromEnv))
            {
                if (fromEnv.StartsWith("/")) fromEnv = "Z:" + fromEnv.Replace("/", "\\");
                if (File.Exists(fromEnv)) return fromEnv;
            }
            foreach (var p in new[]
            {
                @"Z:\usr\bin\ydotool",
                @"Z:\usr\local\bin\ydotool",
            })
            {
                if (File.Exists(p)) return p;
            }
            // Last-resort guess; Process.Start will surface the failure clearly.
            return @"Z:\usr\bin\ydotool";
        }

        private static string ResolveSocketPath()
        {
            // ydotool defaults to $XDG_RUNTIME_DIR/.ydotool_socket on modern
            // systemd-user setups (CachyOS/Arch/Fedora), falls back to
            // /tmp/.ydotool_socket on older configurations.
            //
            // Wine exposes Linux env vars via /proc/self/environ which is
            // reachable as Z:\proc\self\environ. We read it once at startup
            // to find XDG_RUNTIME_DIR.
            try
            {
                byte[] bytes = File.ReadAllBytes(@"Z:\proc\self\environ");
                string text = Encoding.UTF8.GetString(bytes);
                foreach (var entry in text.Split('\0'))
                {
                    const string prefix = "XDG_RUNTIME_DIR=";
                    if (entry.StartsWith(prefix))
                    {
                        string unixDir = entry.Substring(prefix.Length);
                        if (!string.IsNullOrEmpty(unixDir))
                        {
                            return "Z:" + (unixDir + "/.ydotool_socket").Replace("/", "\\");
                        }
                    }
                }
            }
            catch { }
            return @"Z:\tmp\.ydotool_socket";
        }

        private static void RecordUnknownVk(int vk)
        {
            lock (_unknownLock)
            {
                if (_unknownVks.Add(vk))
                {
                    Console.Error.WriteLine(
                        "[LinuxInputBridge] unknown VK 0x" + vk.ToString("X2") +
                        " — no evdev mapping. File a Vk2Evdev addition.");
                }
            }
        }

        private static void RecordUnknownScan(int scan, bool extended)
        {
            long key = ((long)(extended ? 1 : 0) << 32) | (uint)scan;
            lock (_unknownLock)
            {
                if (_unknownScans.Add(key))
                {
                    Console.Error.WriteLine(
                        "[LinuxInputBridge] unknown SCAN 0x" + scan.ToString("X2") +
                        (extended ? " (extended)" : "") +
                        " — no evdev mapping. File a Vk2Evdev.TryFromScan addition.");
                }
            }
        }

        private static string EscapeShell(string s)
        {
            // ydotool type accepts the string after `--`. We still need to avoid
            // double-quotes breaking out of our single-argument string.
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
