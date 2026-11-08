using System;
using System.Collections.Generic;

namespace LabLock
{
    /// <summary>
    /// The record, kept on each PC, of what LabLock has changed there (HKLM\SOFTWARE\LabLock\Changes).
    /// Every setting that is applied is written down; every setting that is undone is crossed off.
    /// "Restore this PC" takes back exactly what is written here, newest setting first, whatever is ticked.
    /// </summary>
    internal static class Journal
    {
        private const string LabLockKey = @"SOFTWARE\LabLock";
        private const string ChangesKey = @"SOFTWARE\LabLock\Changes";
        private const string StudentValue = "JournalStudentSid";

        /// <summary>Settings with nothing to take back: restore points stay, and LabLock never deletes accounts.</summary>
        private static bool NothingToUndo(Setting setting)
        {
            return setting.Id == Catalog.IdRestorePoint || setting.Id == Catalog.IdAdmin;
        }

        public static void Record(RunContext c, Setting setting)
        {
            if (NothingToUndo(setting)) return;
            Reg.Write(c, new RegValue(RegRoot.Machine, ChangesKey, setting.Id, DateTime.Now.ToString("yyyy-MM-dd HH:mm")));
            if (setting.NeedsStudent && c.Student != null)
            {
                Reg.Write(c, new RegValue(RegRoot.Machine, LabLockKey, StudentValue, c.Student.Sid));
            }
        }

        public static void Forget(RunContext c, Setting setting)
        {
            Reg.Delete(c, RegRoot.Machine, ChangesKey, setting.Id);
        }

        /// <summary>The ids of the settings LabLock has applied on this PC and not undone since.</summary>
        public static List<string> Ids(RunContext c)
        {
            return new List<string>(Reg.ValueNames(c, RegRoot.Machine, ChangesKey));
        }

        public static string When(RunContext c, string id)
        {
            return Convert.ToString(Reg.Read(c, RegRoot.Machine, ChangesKey, id));
        }

        /// <summary>The student account the recorded settings were applied to, or null.</summary>
        public static LocalUser Student(RunContext c)
        {
            string sid = Convert.ToString(Reg.Read(c, RegRoot.Machine, LabLockKey, StudentValue));
            if (sid.Length == 0) return null;
            foreach (LocalUser user in c.Users)
            {
                if (string.Equals(user.Sid, sid, StringComparison.OrdinalIgnoreCase)) return user;
            }
            return null;
        }

        /// <summary>Called when nothing is left in the record, so the next lockdown starts a fresh one.</summary>
        public static void ClearStudentWhenEmpty(RunContext c)
        {
            if (Ids(c).Count == 0) Reg.Delete(c, RegRoot.Machine, LabLockKey, StudentValue);
        }

        /// <summary>
        /// The recorded settings in the order they must be undone: the reverse of the checklist, so for example
        /// the DNS lock goes before the DNS servers and the DNS servers before the fixed IP address.
        /// In a run from the teacher PC (remote) the fixed IP address and remote management are left alone,
        /// because undoing them would cut the connection the run depends on.
        /// </summary>
        public static List<Setting> RestoreList(RunContext c, bool remote, List<Setting> leftAlone)
        {
            List<string> ids = Ids(c);
            List<Setting> list = new List<Setting>();
            List<Setting> catalog = Catalog.Build();
            for (int i = catalog.Count - 1; i >= 0; i--)
            {
                Setting setting = catalog[i];
                if (!ids.Contains(setting.Id)) continue;
                if (remote && (setting.Id == Catalog.IdFixedIp || setting.Id == Catalog.IdRemoting))
                {
                    if (leftAlone != null) leftAlone.Add(setting);
                    continue;
                }
                list.Add(setting);
            }
            return list;
        }
    }
}
