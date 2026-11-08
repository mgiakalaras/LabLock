using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace LabLock
{
    /// <summary>
    /// The list of blocked web sites. It lives in LabLock-blocklist.txt next to the program (one site per line)
    /// and is applied to each PC in two places:
    ///   - the Windows hosts file, which works for every program but only for the exact site name (and www.);
    ///   - the block lists of Chrome, Edge, Brave and Firefox, which also cover sub-sites and "site/path" rules.
    /// </summary>
    internal static class Blocklist
    {
        public const string FileName = "LabLock-blocklist.txt";

        private const string HostsBeginPrefix = "# >>> LabLock";
        private const string HostsEndPrefix = "# <<< LabLock";
        private const string HostsBegin = "# >>> LabLock blocklist (written by LabLock, do not edit between these two lines)";
        private const string HostsEnd = "# <<< LabLock blocklist";
        private const string HostsAddress = "0.0.0.0 ";

        private const string TrackKey = @"SOFTWARE\LabLock";
        private const string TrackValue = "BlockedEntries";

        private const string FirefoxKey = @"SOFTWARE\Policies\Mozilla\Firefox\WebsiteFilter\Block";
        private static readonly string[] ChromiumKeys =
        {
            @"SOFTWARE\Policies\Google\Chrome\URLBlocklist",
            @"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist",
            @"SOFTWARE\Policies\BraveSoftware\Brave\URLBlocklist"
        };

        /// <summary>The starter list, written to LabLock-blocklist.txt the first time. Edit the file, not this.</summary>
        private static readonly string[] DefaultLines =
        {
            "# LabLock blocklist - web sites that are blocked on the lab PCs.",
            "#",
            "# One site per line. Lines that start with # are ignored.",
            "#   example.com            blocks example.com and www.example.com in every program,",
            "#                          and all its sub-sites in Chrome, Edge, Brave and Firefox.",
            "#   example.com/some/page  blocks only that part of a site (browsers only).",
            "#",
            "# After changing this file, run LabLock on each PC and apply",
            "# \"Block the game sites in the blocklist\" again.",
            "",
            "# ---- game portals",
            "poki.com",
            "crazygames.com",
            "friv.com",
            "y8.com",
            "miniclip.com",
            "kizi.com",
            "coolmathgames.com",
            "addictinggames.com",
            "armorgames.com",
            "kongregate.com",
            "newgrounds.com",
            "silvergames.com",
            "agame.com",
            "gamesgames.com",
            "1001games.com",
            "gameflare.com",
            "lagged.com",
            "gamepix.com",
            "kbhgames.com",
            "twoplayergames.org",
            "primarygames.com",
            "gamedistribution.com",
            "iogames.space",
            "now.gg",
            "roblox.com",
            "",
            "# ---- single games",
            "1v1.lol",
            "krunker.io",
            "shellshock.io",
            "smashkarts.io",
            "venge.io",
            "ev.io",
            "bloxd.io",
            "zombsroyale.io",
            "surviv.io",
            "moomoo.io",
            "agar.io",
            "diep.io",
            "paper-io.com",
            "hole-io.com",
            "skribbl.io",
            "tetr.io",
            "fireboynwatergirl.com",
            "",
            "# ---- snake and worm games",
            "slither.io",
            "snake.io",
            "wormate.io",
            "worms.zone",
            "gulper.io",
            "littlebigsnake.com",
            "playsnake.org",
            "",
            "# ---- games inside other sites (browsers only)",
            "google.com/fbx",
            "chromedino.com",
            "elgoog.im",
            "sites.google.com/site/tyronesgameshack",
            "sites.google.com/site/unblockedgames66ez",
            "sites.google.com/site/unblockedgame76",
            "",
            "# ---- game stores: remove the # to block them too",
            "# store.steampowered.com",
            "# epicgames.com",
            "# minecraft.net"
        };

        public static string FilePath
        {
            get { return Path.Combine(AppConfig.BaseDir, FileName); }
        }

        /// <summary>Creates the blocklist file with the starter list when it does not exist yet.</summary>
        public static bool EnsureFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    File.WriteAllLines(FilePath, DefaultLines, new UTF8Encoding(false));
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string HostOf(string entry)
        {
            int slash = entry.IndexOf('/');
            return slash < 0 ? entry : entry.Substring(0, slash);
        }

        /// <summary>Turns one line of the file into "site" or "site/path", or null when the line is not a site.</summary>
        private static string Normalize(string raw)
        {
            string text = raw;
            int hash = text.IndexOf('#');
            if (hash >= 0) text = text.Substring(0, hash);
            text = text.Trim().ToLowerInvariant();
            if (text.Length == 0) return null;

            // Accept a pasted hosts-file line such as "0.0.0.0 example.com": keep the last word.
            int space = text.LastIndexOfAny(new char[] { ' ', '\t' });
            if (space >= 0) text = text.Substring(space + 1);

            int scheme = text.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0) text = text.Substring(scheme + 3);
            if (text.StartsWith("*.", StringComparison.Ordinal)) text = text.Substring(2);
            if (text.StartsWith("www.", StringComparison.Ordinal)) text = text.Substring(4);
            text = text.TrimEnd('/');

            string host = HostOf(text);
            if (host.Length < 4 || host.IndexOf('.') <= 0 || host.EndsWith(".", StringComparison.Ordinal)) return null;
            foreach (char ch in host)
            {
                bool ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '-';
                if (!ok) return null;
            }
            foreach (char ch in text)
            {
                if (ch < 33 || ch > 126 || ch == '"') return null;
            }
            return text;
        }

        private static bool Contains(List<string> list, string value)
        {
            foreach (string item in list)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>The sites to block: from the file next to the program, or the starter list when there is no file.</summary>
        public static List<string> Load()
        {
            string[] lines = DefaultLines;
            try
            {
                if (File.Exists(FilePath)) lines = File.ReadAllLines(FilePath);
            }
            catch (Exception)
            {
                lines = DefaultLines;
            }

            List<string> entries = new List<string>();
            foreach (string line in lines)
            {
                string entry = Normalize(line);
                if (entry != null && !Contains(entries, entry)) entries.Add(entry);
            }
            return entries;
        }

        // ------------------------------------------------------------------ what LabLock wrote last time

        private static List<string> ReadTracked()
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey key = machine.OpenSubKey(TrackKey, false))
            {
                if (key == null) return new List<string>();
                string[] values = key.GetValue(TrackValue) as string[];
                return values == null ? new List<string>() : new List<string>(values);
            }
        }

        private static void WriteTracked(List<string> entries)
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey key = machine.CreateSubKey(TrackKey))
            {
                if (key == null) return;
                if (entries.Count == 0)
                {
                    key.DeleteValue(TrackValue, false);
                }
                else
                {
                    key.SetValue(TrackValue, entries.ToArray(), RegistryValueKind.MultiString);
                }
            }
        }

        // ------------------------------------------------------------------ browsers

        private static string FirefoxPattern(string entry)
        {
            string host = HostOf(entry);
            string path = entry.Substring(host.Length);
            return "*://*." + host + (path.Length == 0 ? "/*" : path + "*");
        }

        private static List<string> FirefoxPatterns(List<string> entries)
        {
            List<string> patterns = new List<string>();
            foreach (string entry in entries)
            {
                patterns.Add(FirefoxPattern(entry));
            }
            return patterns;
        }

        /// <summary>
        /// Rewrites a browser's block list: entries somebody else put there are kept, the entries LabLock wrote
        /// last time are taken out and the wanted ones are added. The values are numbered 1, 2, 3...
        /// </summary>
        private static void SyncList(string keyPath, List<string> previous, List<string> wanted)
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey key = machine.CreateSubKey(keyPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not open the registry key " + keyPath);
                }

                List<string> result = new List<string>();
                string[] names = key.GetValueNames();
                foreach (string name in names)
                {
                    string value = Convert.ToString(key.GetValue(name)).Trim();
                    if (value.Length == 0) continue;
                    if (Contains(previous, value) || Contains(wanted, value) || Contains(result, value)) continue;
                    result.Add(value);
                }
                foreach (string name in names)
                {
                    key.DeleteValue(name, false);
                }

                result.AddRange(wanted);
                for (int i = 0; i < result.Count; i++)
                {
                    key.SetValue((i + 1).ToString(CultureInfo.InvariantCulture), result[i], RegistryValueKind.String);
                }
            }
        }

        private static void SyncBrowsers(List<string> previous, List<string> wanted)
        {
            foreach (string keyPath in ChromiumKeys)
            {
                SyncList(keyPath, previous, wanted);
            }
            SyncList(FirefoxKey, FirefoxPatterns(previous), FirefoxPatterns(wanted));
        }

        // ------------------------------------------------------------------ hosts file

        private static string HostsPath()
        {
            string system = Environment.SystemDirectory;
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                // A 32-bit program is redirected away from the real System32; "Sysnative" reaches it.
                system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative");
            }
            return Path.Combine(system, @"drivers\etc\hosts");
        }

        /// <summary>Replaces LabLock's own part of the hosts file. Everything else in the file is left alone.</summary>
        private static void WriteHosts(List<string> entries)
        {
            string path = HostsPath();
            List<string> lines = new List<string>();

            if (File.Exists(path))
            {
                bool inside = false;
                foreach (string line in File.ReadAllLines(path))
                {
                    if (line.StartsWith(HostsBeginPrefix, StringComparison.Ordinal))
                    {
                        inside = true;
                        continue;
                    }
                    if (line.StartsWith(HostsEndPrefix, StringComparison.Ordinal))
                    {
                        inside = false;
                        continue;
                    }
                    if (inside && line.StartsWith(HostsAddress, StringComparison.Ordinal))
                    {
                        continue;   // one of LabLock's own lines
                    }
                    inside = false;
                    lines.Add(line);
                }

                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                }
            }

            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            bool any = false;
            foreach (string entry in entries)
            {
                if (entry.IndexOf('/') >= 0) continue;   // "site/path" rules cannot go into a hosts file
                if (!any)
                {
                    lines.Add("");
                    lines.Add(HostsBegin);
                    any = true;
                }
                lines.Add(HostsAddress + entry);
                lines.Add(HostsAddress + "www." + entry);
            }
            if (any) lines.Add(HostsEnd);

            File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));

            string ignored;
            Proc.Run(Proc.SystemTool("ipconfig.exe"), "/flushdns", out ignored);
        }

        /// <summary>How many active lines of the hosts file were not written by LabLock (and are not Windows' own localhost lines).</summary>
        public static int ForeignHostsEntries()
        {
            string path = HostsPath();
            if (!File.Exists(path)) return 0;

            int count = 0;
            bool inside = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith(HostsBeginPrefix, StringComparison.Ordinal))
                {
                    inside = true;
                    continue;
                }
                if (line.StartsWith(HostsEndPrefix, StringComparison.Ordinal))
                {
                    inside = false;
                    continue;
                }
                if (inside || line.Length == 0 || line[0] == '#') continue;
                if (line.EndsWith("localhost", StringComparison.OrdinalIgnoreCase)) continue;
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ the setting

        public static string State()
        {
            List<string> tracked = ReadTracked();
            if (tracked.Count == 0) return "Not applied";

            List<string> current = Load();
            bool same = tracked.Count == current.Count;
            if (same)
            {
                foreach (string entry in current)
                {
                    if (!Contains(tracked, entry)) same = false;
                }
            }
            return same ? "Applied (" + tracked.Count + " entries)" : "Applied, but the list was edited - apply again";
        }

        /// <summary>Blocks the current list on this PC and returns the number of entries.</summary>
        public static int Apply(Action<string> log)
        {
            List<string> wanted = Load();
            if (wanted.Count == 0)
            {
                throw new InvalidOperationException("The blocklist is empty. Add sites to " + FileName + " first.");
            }

            SyncBrowsers(ReadTracked(), wanted);
            WriteTracked(wanted);

            try
            {
                WriteHosts(wanted);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("The browsers' block lists were set, but the Windows hosts file could not be changed (" +
                    ex.Message + "). An antivirus program may be protecting it.");
            }
            return wanted.Count;
        }

        public static void Undo(Action<string> log)
        {
            // Take out what was written last time and, to be sure, whatever is in the list now.
            List<string> previous = ReadTracked();
            foreach (string entry in Load())
            {
                if (!Contains(previous, entry)) previous.Add(entry);
            }

            SyncBrowsers(previous, new List<string>());
            WriteTracked(new List<string>());
            WriteHosts(new List<string>());
            log("The blocklist was removed from the browsers and the hosts file.");
        }
    }
}
