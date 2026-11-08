using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LabLock
{
    /// <summary>
    /// Remembers the names, options and ticks in LabLock.ini next to the program (on the USB stick),
    /// so they do not have to be typed again on every PC. Passwords are never saved.
    /// </summary>
    internal sealed class AppConfig
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string BaseDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        private static string FilePath
        {
            get { return Path.Combine(BaseDir, "LabLock.ini"); }
        }

        public static AppConfig Load()
        {
            AppConfig config = new AppConfig();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                        int equals = line.IndexOf('=');
                        if (equals <= 0) continue;
                        config.values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                    }
                }
            }
            catch (Exception)
            {
                // An unreadable settings file only means starting with the defaults.
            }
            return config;
        }

        public bool Has(string key)
        {
            return values.ContainsKey(key);
        }

        public string Get(string key, string fallback)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : fallback;
        }

        public void Set(string key, string value)
        {
            values[key] = value ?? "";
        }

        public bool Save()
        {
            try
            {
                List<string> lines = new List<string>();
                lines.Add("# LabLock settings. Safe to edit or delete. Passwords are never stored here.");
                foreach (KeyValuePair<string, string> pair in values)
                {
                    lines.Add(pair.Key + "=" + pair.Value);
                }
                File.WriteAllLines(FilePath, lines.ToArray(), Encoding.UTF8);
                return true;
            }
            catch (Exception)
            {
                return false;   // for example a write-protected USB stick
            }
        }

        /// <summary>Stores a file path relative to the program folder when possible, because USB drive letters change.</summary>
        public static string ToPortablePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            try
            {
                string full = Path.GetFullPath(path);
                if (full.StartsWith(BaseDir, StringComparison.OrdinalIgnoreCase))
                {
                    return full.Substring(BaseDir.Length);
                }
                return full;
            }
            catch (Exception)
            {
                return path;
            }
        }

        public static string FromPortablePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            try
            {
                return Path.IsPathRooted(path) ? path : Path.Combine(BaseDir, path);
            }
            catch (Exception)
            {
                return path;
            }
        }
    }
}
