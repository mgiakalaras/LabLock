using System;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace LabLock
{
    /// <summary>Where a registry value lives: the computer (all accounts) or the student's own settings.</summary>
    internal enum RegRoot
    {
        Machine,
        Student
    }

    /// <summary>One policy value that a setting writes.</summary>
    internal sealed class RegValue
    {
        public readonly RegRoot Root;
        public readonly string Path;
        public readonly string Name;
        public readonly object Value;
        public readonly RegistryValueKind Kind;

        public RegValue(RegRoot root, string path, string name, int value)
        {
            Root = root;
            Path = path;
            Name = name;
            Value = value;
            Kind = RegistryValueKind.DWord;
        }

        public RegValue(RegRoot root, string path, string name, string value)
        {
            Root = root;
            Path = path;
            Name = name;
            Value = value;
            Kind = RegistryValueKind.String;
        }
    }

    internal static class Reg
    {
        /// <summary>HKEY_LOCAL_MACHINE, always the 64-bit view so policies land where Windows reads them.</summary>
        public static RegistryKey Machine()
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        }

        private static RegistryKey OpenRoot(RunContext c, RegRoot root, bool writable)
        {
            if (root == RegRoot.Machine)
            {
                return Machine();
            }
            if (c.HiveRoot == null)
            {
                throw new InvalidOperationException(c.HiveProblem ?? "The student's settings are not available.");
            }
            RegistryKey key = Registry.Users.OpenSubKey(c.HiveRoot, writable);
            if (key == null)
            {
                throw new InvalidOperationException("The student's settings could not be opened.");
            }
            return key;
        }

        public static object Read(RunContext c, RegRoot root, string path, string name)
        {
            using (RegistryKey rootKey = OpenRoot(c, root, false))
            using (RegistryKey key = rootKey.OpenSubKey(path, false))
            {
                return key == null ? null : key.GetValue(name);
            }
        }

        public static void Write(RunContext c, RegValue value)
        {
            using (RegistryKey rootKey = OpenRoot(c, value.Root, true))
            using (RegistryKey key = rootKey.CreateSubKey(value.Path))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not create the registry key " + value.Path);
                }
                key.SetValue(value.Name, value.Value, value.Kind);
            }
        }

        public static void Delete(RunContext c, RegRoot root, string path, string name)
        {
            using (RegistryKey rootKey = OpenRoot(c, root, true))
            using (RegistryKey key = rootKey.OpenSubKey(path, true))
            {
                if (key != null)
                {
                    key.DeleteValue(name, false);
                }
            }
        }

        /// <summary>Reads a value exactly as stored. %VARIABLES% are NOT replaced (they would be the administrator's, not the student's).</summary>
        public static object ReadRaw(RunContext c, RegRoot root, string path, string name)
        {
            using (RegistryKey rootKey = OpenRoot(c, root, false))
            using (RegistryKey key = rootKey.OpenSubKey(path, false))
            {
                return key == null ? null : key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            }
        }

        public static bool KeyExists(RunContext c, RegRoot root, string path)
        {
            using (RegistryKey rootKey = OpenRoot(c, root, false))
            using (RegistryKey key = rootKey.OpenSubKey(path, false))
            {
                return key != null;
            }
        }

        public static string[] ValueNames(RunContext c, RegRoot root, string path)
        {
            using (RegistryKey rootKey = OpenRoot(c, root, false))
            using (RegistryKey key = rootKey.OpenSubKey(path, false))
            {
                return key == null ? new string[0] : key.GetValueNames();
            }
        }

        public static string[] SubKeyNames(RunContext c, RegRoot root, string path)
        {
            using (RegistryKey rootKey = OpenRoot(c, root, false))
            using (RegistryKey key = rootKey.OpenSubKey(path, false))
            {
                return key == null ? new string[0] : key.GetSubKeyNames();
            }
        }

        public static void WriteValue(RunContext c, RegRoot root, string path, string name, object value, RegistryValueKind kind)
        {
            using (RegistryKey rootKey = OpenRoot(c, root, true))
            using (RegistryKey key = rootKey.CreateSubKey(path))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not create the registry key " + path);
                }
                key.SetValue(name, value, kind);
            }
        }

        public static bool Matches(object actual, RegValue wanted)
        {
            if (actual == null) return false;
            if (wanted.Kind == RegistryValueKind.DWord)
            {
                return actual is int && (int)actual == (int)wanted.Value;
            }
            return string.Equals(Convert.ToString(actual), (string)wanted.Value, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Gives access to the student's own registry settings. When the student is signed in they are already
    /// loaded under HKEY_USERS\{SID}. Otherwise the profile's NTUSER.DAT is loaded temporarily and unloaded again.
    /// The profile folder is found through the account's SID, so its name (for example "Active") does not matter.
    /// </summary>
    internal sealed class StudentHive : IDisposable
    {
        private const string MountName = "LabLock_StudentHive";

        public readonly string Root;
        public bool UnloadFailed;
        private readonly bool mounted;
        private bool disposed;

        private StudentHive(string root, bool isMounted)
        {
            Root = root;
            mounted = isMounted;
        }

        private static bool KeyExists(string name)
        {
            using (RegistryKey key = Registry.Users.OpenSubKey(name, false))
            {
                return key != null;
            }
        }

        public static string ProfilePath(string sid)
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + sid, false))
            {
                if (key == null) return null;
                string path = Convert.ToString(key.GetValue("ProfileImagePath"));
                return string.IsNullOrEmpty(path) ? null : path;
            }
        }

        private static bool Unload()
        {
            string reg = Proc.SystemTool("reg.exe");
            for (int attempt = 0; attempt < 5; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                string output;
                Proc.Run(reg, "unload \"HKU\\" + MountName + "\"", out output);
                if (!KeyExists(MountName)) return true;
                Thread.Sleep(400);
            }
            return false;
        }

        /// <summary>Returns null and a reason when the student's settings cannot be reached.</summary>
        public static StudentHive TryOpen(string sid, out string problem)
        {
            problem = null;
            if (string.IsNullOrEmpty(sid))
            {
                problem = "No student account selected.";
                return null;
            }

            if (KeyExists(sid))
            {
                return new StudentHive(sid, false);
            }

            if (KeyExists(MountName) && !Unload())
            {
                problem = "A previous run left the student's settings open. Restart this PC and try again.";
                return null;
            }

            string profile = ProfilePath(sid);
            string file = profile == null ? null : System.IO.Path.Combine(profile, "NTUSER.DAT");
            if (file == null || !File.Exists(file))
            {
                problem = "No profile yet - sign in once with this account, then run LabLock again.";
                return null;
            }

            string output;
            int code = Proc.Run(Proc.SystemTool("reg.exe"), "load \"HKU\\" + MountName + "\" \"" + file + "\"", out output);
            if (code != 0 || !KeyExists(MountName))
            {
                problem = "Could not open the student's settings file: " + output;
                return null;
            }
            return new StudentHive(MountName, true);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (mounted && !Unload())
            {
                UnloadFailed = true;
            }
        }
    }
}
