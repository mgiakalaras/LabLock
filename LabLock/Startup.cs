using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace LabLock
{
    /// <summary>One program that starts by itself when somebody signs in.</summary>
    internal sealed class StartupEntry
    {
        public string Name = "";
        public string Command = "";
        public string Source = "";      // shown to the user
        public bool Enabled = true;

        // Where Windows keeps the "enabled / disabled" switch of this entry (the one Task Manager shows).
        public RegRoot Root;
        public string ApprovedKey = "";
        public string ApprovedName = "";
    }

    /// <summary>
    /// Finds the programs that start with Windows, for all accounts and for the student, and switches them
    /// on or off the same way Task Manager does. Nothing is deleted, so every change can be taken back.
    /// </summary>
    internal static class StartupScan
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunKey32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        public const string SourceAll = "All accounts";
        public const string SourceStudent = "Student account";

        /// <summary>Task Manager stores 02 (or 06) for "enabled" and 03 (or 07) for "disabled" in the first byte.</summary>
        private static bool IsEnabled(RunContext c, RegRoot root, string approvedKey, string name)
        {
            byte[] data = Reg.ReadRaw(c, root, approvedKey, name) as byte[];
            if (data == null || data.Length == 0) return true;
            return (data[0] & 1) == 0;
        }

        private static void AddRegistryEntries(List<StartupEntry> list, RunContext c, RegRoot root, string runKey, string approvedSubKey, string source)
        {
            foreach (string name in Reg.ValueNames(c, root, runKey))
            {
                if (name.Length == 0) continue;
                StartupEntry entry = new StartupEntry();
                entry.Name = name;
                entry.Command = Convert.ToString(Reg.ReadRaw(c, root, runKey, name));
                entry.Source = source;
                entry.Root = root;
                entry.ApprovedKey = ApprovedBase + approvedSubKey;
                entry.ApprovedName = name;
                entry.Enabled = IsEnabled(c, root, entry.ApprovedKey, name);
                list.Add(entry);
            }
        }

        private static void AddFolderEntries(List<StartupEntry> list, RunContext c, RegRoot root, string folder, string source)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(file);
                if (string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                StartupEntry entry = new StartupEntry();
                entry.Name = Path.GetFileNameWithoutExtension(file);
                entry.Command = file;
                entry.Source = source + ", Startup folder";
                entry.Root = root;
                entry.ApprovedKey = ApprovedBase + "StartupFolder";
                entry.ApprovedName = name;
                entry.Enabled = IsEnabled(c, root, entry.ApprovedKey, name);
                list.Add(entry);
            }
        }

        /// <summary>
        /// Lists the startup programs. The student's own entries are included when the student's settings are
        /// open (c.HiveRoot is set); otherwise only the entries for all accounts are returned.
        /// </summary>
        public static List<StartupEntry> Scan(RunContext c)
        {
            List<StartupEntry> list = new List<StartupEntry>();

            AddRegistryEntries(list, c, RegRoot.Machine, RunKey, "Run", SourceAll);
            AddRegistryEntries(list, c, RegRoot.Machine, RunKey32, "Run32", SourceAll);
            AddFolderEntries(list, c, RegRoot.Machine, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), SourceAll);

            if (c.HiveRoot != null && c.Student != null)
            {
                AddRegistryEntries(list, c, RegRoot.Student, RunKey, "Run", SourceStudent);
                string folder = null;
                try
                {
                    folder = StudentFolders.StartupFolder(c);
                }
                catch (Exception)
                {
                }
                AddFolderEntries(list, c, RegRoot.Student, folder, SourceStudent);
            }

            list.Sort(delegate(StartupEntry a, StartupEntry b)
            {
                int bySource = string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase);
                return bySource != 0 ? bySource : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        /// <summary>Switches one entry on or off. The student's entries need the student's settings to be open.</summary>
        public static void SetEnabled(RunContext c, StartupEntry entry, bool enabled)
        {
            byte[] data = new byte[12];
            if (enabled)
            {
                data[0] = 2;
            }
            else
            {
                data[0] = 3;
                Array.Copy(BitConverter.GetBytes(DateTime.Now.ToFileTime()), 0, data, 4, 8);   // when it was switched off
            }
            Reg.WriteValue(c, entry.Root, entry.ApprovedKey, entry.ApprovedName, data, RegistryValueKind.Binary);
            entry.Enabled = enabled;
        }
    }
}
