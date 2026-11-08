using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management;
using System.Text;

namespace LabLock
{
    /// <summary>One line of the health check.</summary>
    internal sealed class HealthItem
    {
        public const int Info = 0;
        public const int Good = 1;
        public const int Warning = 2;

        public int Kind;
        public string Title;
        public string Detail;

        public HealthItem(int kind, string title, string detail)
        {
            Kind = kind;
            Title = title;
            Detail = detail;
        }
    }

    /// <summary>
    /// Looks at the things on a lab PC that usually go wrong. It only reads; it never changes anything.
    /// Every check stands alone: when one cannot be done it says so and the others still run.
    /// </summary>
    internal static class Health
    {
        private delegate HealthItem Check(RunContext c);

        public static List<HealthItem> Run(RunContext c)
        {
            Check[] checks =
            {
                Administrators, BuiltInAccounts, StudentAccount, AutoSignIn, UserAccountControl,
                Defender, DefenderExclusions, Firewall, Updates, PendingRestart, DiskSpace,
                StudentPrograms, StudentDownloads, StartupPrograms, HostsFile, DnsServers, Veyon
            };

            List<HealthItem> items = new List<HealthItem>();
            StudentHive hive = Engine.OpenHive(c);
            try
            {
                foreach (Check check in checks)
                {
                    try
                    {
                        HealthItem item = check(c);
                        if (item != null) items.Add(item);
                    }
                    catch (Exception ex)
                    {
                        items.Add(new HealthItem(HealthItem.Info, "A check could not be done", ex.Message));
                    }
                }
            }
            finally
            {
                Engine.CloseHive(hive, c);
            }
            return items;
        }

        /// <summary>The whole check as plain text, for a file or an e-mail.</summary>
        public static string Report(List<HealthItem> items)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("LabLock health check - " + ComputerName.Active + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            text.AppendLine();
            foreach (HealthItem item in items)
            {
                string mark = item.Kind == HealthItem.Good ? "[ OK ]" : (item.Kind == HealthItem.Warning ? "[ !! ]" : "[ .. ]");
                text.AppendLine(mark + " " + L.T(item.Title));
                text.AppendLine("       " + L.T(item.Detail));
            }
            return text.ToString();
        }

        private static string Short(List<string> names, int most)
        {
            if (names.Count <= most) return string.Join(", ", names.ToArray());
            return string.Join(", ", names.GetRange(0, most).ToArray()) + " (+" + (names.Count - most) + ")";
        }

        // ------------------------------------------------------------------ accounts

        private static HealthItem Administrators(RunContext c)
        {
            List<string> admins = new List<string>();
            foreach (LocalUser user in c.Users)
            {
                if (user.IsAdmin && !user.Disabled) admins.Add(user.Name);
            }
            const string Title = "Administrator accounts";
            if (admins.Count == 0) return new HealthItem(HealthItem.Warning, Title, "No enabled administrator account was found.");
            bool onlyChosen = admins.Count == 1 && string.Equals(admins[0], c.AdminName, StringComparison.OrdinalIgnoreCase);
            return new HealthItem(onlyChosen ? HealthItem.Good : HealthItem.Warning, Title,
                onlyChosen ? "The only administrator is " + admins[0] + "."
                           : "These accounts are administrators: " + Short(admins, 6) + ". There should be only the one you use.");
        }

        private static HealthItem BuiltInAccounts(RunContext c)
        {
            List<string> open = new List<string>();
            foreach (LocalUser user in c.Users)
            {
                bool builtIn = user.Sid != null && (user.Sid.EndsWith("-500") || user.Sid.EndsWith("-501"));
                if (builtIn && !user.Disabled) open.Add(user.Name);
            }
            const string Title = "Windows' built-in Administrator and Guest accounts";
            return open.Count == 0
                ? new HealthItem(HealthItem.Good, Title, "Both are switched off.")
                : new HealthItem(HealthItem.Warning, Title, "Switched on: " + Short(open, 4) + ". They are best left off.");
        }

        private static HealthItem StudentAccount(RunContext c)
        {
            const string Title = "The student account";
            LocalUser student = c.Student;
            if (student == null) return new HealthItem(HealthItem.Info, Title, "No student account is chosen on the Lockdown page.");
            return student.IsAdmin
                ? new HealthItem(HealthItem.Warning, Title, student.Name + " is an administrator: students can install and change anything.")
                : new HealthItem(HealthItem.Good, Title, student.Name + " is a standard user.");
        }

        private static HealthItem AutoSignIn(RunContext c)
        {
            const string Key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            const string Title = "Automatic sign-in";
            bool on = Convert.ToString(Reg.Read(c, RegRoot.Machine, Key, "AutoAdminLogon")) == "1";
            bool password = Convert.ToString(Reg.Read(c, RegRoot.Machine, Key, "DefaultPassword")).Length > 0;
            if (password) return new HealthItem(HealthItem.Warning, Title, "A password is stored in the registry in readable form for automatic sign-in.");
            return on ? new HealthItem(HealthItem.Info, Title, "This PC signs in automatically as " + Convert.ToString(Reg.Read(c, RegRoot.Machine, Key, "DefaultUserName")) + ".")
                      : new HealthItem(HealthItem.Good, Title, "Off.");
        }

        private static HealthItem UserAccountControl(RunContext c)
        {
            object value = Reg.Read(c, RegRoot.Machine, Catalog.PolSystemMachine, "EnableLUA");
            bool on = !(value is int) || (int)value != 0;
            const string Title = "User Account Control (the administrator prompt)";
            return on ? new HealthItem(HealthItem.Good, Title, "On.")
                      : new HealthItem(HealthItem.Warning, Title, "Off. Programs get administrator rights without asking.");
        }

        // ------------------------------------------------------------------ protection

        private static ManagementObjectCollection DefenderQuery(string className)
        {
            ManagementScope scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Defender");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM " + className));
            return searcher.Get();
        }

        private static HealthItem Defender(RunContext c)
        {
            const string Title = "Microsoft Defender antivirus";
            try
            {
                using (ManagementObjectCollection results = DefenderQuery("MSFT_MpComputerStatus"))
                {
                    foreach (ManagementBaseObject item in results)
                    {
                        bool realTime = Convert.ToBoolean(item["RealTimeProtectionEnabled"]);
                        string updated = "";
                        try
                        {
                            DateTime stamp = ManagementDateTimeConverter.ToDateTime(Convert.ToString(item["AntivirusSignatureLastUpdated"]));
                            updated = " Definitions of " + stamp.ToString("yyyy-MM-dd") + ".";
                        }
                        catch (Exception)
                        {
                        }
                        return realTime ? new HealthItem(HealthItem.Good, Title, "Real-time protection is on." + updated)
                                        : new HealthItem(HealthItem.Warning, Title, "Real-time protection is OFF." + updated);
                    }
                }
            }
            catch (Exception)
            {
            }
            return new HealthItem(HealthItem.Info, Title, "Could not be read. Another antivirus program may be installed.");
        }

        private static HealthItem DefenderExclusions(RunContext c)
        {
            const string Title = "Antivirus exclusions";
            try
            {
                List<string> excluded = new List<string>();
                using (ManagementObjectCollection results = DefenderQuery("MSFT_MpPreference"))
                {
                    foreach (ManagementBaseObject item in results)
                    {
                        foreach (string property in new string[] { "ExclusionPath", "ExclusionProcess", "ExclusionExtension" })
                        {
                            string[] values = item[property] as string[];
                            if (values != null) excluded.AddRange(values);
                        }
                    }
                }
                return excluded.Count == 0
                    ? new HealthItem(HealthItem.Good, Title, "None.")
                    : new HealthItem(HealthItem.Warning, Title, "The antivirus skips: " + Short(excluded, 4) + ". Games and cracks often add themselves here.");
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static HealthItem Firewall(RunContext c)
        {
            const string Base = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\";
            List<string> off = new List<string>();
            string[] keys = { "DomainProfile", "StandardProfile", "PublicProfile" };
            string[] names = { "domain", "private", "public" };
            for (int i = 0; i < keys.Length; i++)
            {
                object value = Reg.Read(c, RegRoot.Machine, Base + keys[i], "EnableFirewall");
                if (value is int && (int)value == 0) off.Add(names[i]);
            }
            const string Title = "Windows Firewall";
            return off.Count == 0 ? new HealthItem(HealthItem.Good, Title, "On for all network types.")
                                  : new HealthItem(HealthItem.Warning, Title, "Off for these network types: " + Short(off, 3) + ".");
        }

        private static HealthItem Updates(RunContext c)
        {
            const string Title = "Windows updates";
            DateTime latest = DateTime.MinValue;
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT InstalledOn FROM Win32_QuickFixEngineering"))
            using (ManagementObjectCollection results = searcher.Get())
            {
                foreach (ManagementBaseObject item in results)
                {
                    DateTime stamp;
                    string text = Convert.ToString(item["InstalledOn"]);
                    if ((DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out stamp) ||
                         DateTime.TryParse(text, out stamp)) && stamp > latest)
                    {
                        latest = stamp;
                    }
                }
            }
            if (latest == DateTime.MinValue) return new HealthItem(HealthItem.Info, Title, "The date of the last update could not be read.");
            int days = (int)(DateTime.Now - latest).TotalDays;
            string detail = "The last update was installed on " + latest.ToString("yyyy-MM-dd") + " (" + days + " days ago).";
            return new HealthItem(days > 60 ? HealthItem.Warning : HealthItem.Good, Title, detail);
        }

        private static HealthItem PendingRestart(RunContext c)
        {
            bool waiting =
                Reg.KeyExists(c, RegRoot.Machine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") ||
                Reg.KeyExists(c, RegRoot.Machine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") ||
                !ComputerName.Same(ComputerName.Active, ComputerName.Pending());
            const string Title = "Restart";
            return waiting ? new HealthItem(HealthItem.Warning, Title, "Windows is waiting for a restart to finish updates or a new computer name.")
                           : new HealthItem(HealthItem.Good, Title, "No restart is waiting.");
        }

        private static HealthItem DiskSpace(RunContext c)
        {
            DriveInfo drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            double freeGb = drive.AvailableFreeSpace / 1073741824.0;
            double percent = drive.TotalSize > 0 ? 100.0 * drive.AvailableFreeSpace / drive.TotalSize : 0;
            string detail = Math.Round(freeGb).ToString(CultureInfo.InvariantCulture) + " GB free on " + drive.Name +
                            " (" + Math.Round(percent).ToString(CultureInfo.InvariantCulture) + "%).";
            return new HealthItem(percent < 10 ? HealthItem.Warning : HealthItem.Good, "Free disk space", detail);
        }

        // ------------------------------------------------------------------ what students leave behind

        private static HealthItem StudentPrograms(RunContext c)
        {
            const string Title = "Programs installed inside the student's account";
            if (c.Student == null || c.HiveRoot == null) return null;

            const string Uninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
            List<string> programs = new List<string>();
            foreach (string key in Reg.SubKeyNames(c, RegRoot.Student, Uninstall))
            {
                string name = Convert.ToString(Reg.Read(c, RegRoot.Student, Uninstall + "\\" + key, "DisplayName")).Trim();
                if (name.Length > 0 && !programs.Contains(name)) programs.Add(name);
            }
            return programs.Count == 0
                ? new HealthItem(HealthItem.Good, Title, "None.")
                : new HealthItem(HealthItem.Warning, Title, Short(programs, 6) + ". These were installed without administrator rights; remove them while signed in as the student.");
        }

        private static int CountPrograms(string folder)
        {
            int count = 0;
            if (!Directory.Exists(folder)) return 0;
            foreach (string pattern in new string[] { "*.exe", "*.msi" })
            {
                try
                {
                    count += Directory.GetFiles(folder, pattern, SearchOption.AllDirectories).Length;
                }
                catch (Exception)
                {
                    // a subfolder that cannot be opened: count what can be seen at the top
                    try
                    {
                        count += Directory.GetFiles(folder, pattern).Length;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return count;
        }

        private static HealthItem StudentDownloads(RunContext c)
        {
            const string Title = "Program files in the student's Downloads and Desktop";
            if (c.Student == null || c.HiveRoot == null) return null;
            int count = CountPrograms(StudentFolders.Downloads(c)) + CountPrograms(StudentFolders.Desktop(c));
            return count == 0 ? new HealthItem(HealthItem.Good, Title, "None.")
                              : new HealthItem(HealthItem.Warning, Title, count + " program or installer files (.exe, .msi) are lying there.");
        }

        private static HealthItem StartupPrograms(RunContext c)
        {
            int enabled = 0;
            foreach (StartupEntry entry in StartupScan.Scan(c))
            {
                if (entry.Enabled) enabled++;
            }
            return new HealthItem(HealthItem.Info, "Programs that start with Windows", enabled + " are switched on. See the Startup page.");
        }

        private static HealthItem HostsFile(RunContext c)
        {
            const string Title = "The hosts file";
            int foreign = Blocklist.ForeignHostsEntries();
            return foreign == 0 ? new HealthItem(HealthItem.Good, Title, "Only Windows' own lines and LabLock's blocklist.")
                                : new HealthItem(HealthItem.Info, Title, foreign + " lines were not written by LabLock. Check them if you did not add them yourself.");
        }

        private static HealthItem DnsServers(RunContext c)
        {
            List<string> servers = NetConfig.AllCurrentDns();
            const string Title = "DNS servers in use";
            if (servers.Count == 0) return new HealthItem(HealthItem.Info, Title, "None found (no network?).");
            bool typed = c.DnsServer.Length > 0 && servers[0] == c.DnsServer;
            return new HealthItem(typed ? HealthItem.Good : HealthItem.Info, Title,
                Short(servers, 4) + (typed ? "." : ". The first one is not the DNS server typed on the Lockdown page."));
        }

        private static HealthItem Veyon(RunContext c)
        {
            if (!Reg.KeyExists(c, RegRoot.Machine, @"SYSTEM\CurrentControlSet\Services\VeyonService")) return null;
            string state = "";
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT State FROM Win32_Service WHERE Name = 'VeyonService'"))
            using (ManagementObjectCollection results = searcher.Get())
            {
                foreach (ManagementBaseObject item in results)
                {
                    state = Convert.ToString(item["State"]);
                }
            }
            bool running = string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase);
            return new HealthItem(running ? HealthItem.Good : HealthItem.Warning, "Veyon service",
                running ? "Running." : "Installed but not running, so this PC cannot be seen from Veyon Master.");
        }
    }
}
