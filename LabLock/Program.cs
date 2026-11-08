using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LabLock
{
    /// <summary>Everything LabLock does is also written to LabLock-log.txt next to the program.</summary>
    internal static class LogFile
    {
        public static string FilePath
        {
            get { return Path.Combine(AppConfig.BaseDir, "LabLock-log.txt"); }
        }

        public static void Write(string message)
        {
            try
            {
                File.AppendAllText(FilePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ComputerName.Active + "  " + message + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch (Exception)
            {
                // The USB stick may be write-protected; then there is simply no log file.
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            AppConfig config = AppConfig.Load();
            string language = config.Get("language", "");
            L.Greek = language == "el" || (language != "en" && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "el");

            if (Headless.Wanted(args))
            {
                if (!IsAdministrator())
                {
                    LogFile.Write("Silent run refused: LabLock was not started as administrator.");
                    return 2;
                }
                return Headless.Run(args);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;

            if (!IsAdministrator())
            {
                MessageBox.Show(
                    L.T("LabLock changes accounts and system settings, so it must run as administrator.\r\n\r\nRight-click LabLock.exe and choose \"Run as administrator\"."),
                    "LabLock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            Application.Run(new MainForm());
            return 0;
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            LogFile.Write("ERROR  " + e.Exception);
            MessageBox.Show(L.T("Something went wrong:") + "\r\n\r\n" + e.Exception.Message,
                "LabLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }

    /// <summary>
    /// Running without a window, for doing many PCs from the teacher's PC:
    ///     LabLock.exe /apply            applies the settings that are ticked in LabLock.ini
    ///     LabLock.exe /undo             takes them back
    ///     LabLock.exe /restore          takes back everything LabLock has changed on this PC (its record of changes),
    ///                                   except the fixed IP address and remote management, which the remote run needs
    ///     LabLock.exe /wake             switches on (Wake-on-LAN) every PC of LabLock-computers.csv
    /// Optional for the night shut-down: /shutdown:21:00 (another time than shutdown= in LabLock.ini, for example on a board)
    /// Optional:  /student:NAME   /adminpass:PASSWORD   /ip:ADDRESS   /name:NEWNAME   /restart
    /// The fixed IP and the rename are skipped unless /ip: and /name: are given, because they differ per PC.
    /// The result goes to LabLock-log.txt; the exit code is 0 (all done), 1 (some failed) or 2 / 3 (did not run).
    /// </summary>
    internal static class Headless
    {
        private static bool Flag(string[] args, string name)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string Value(string[] args, string name)
        {
            string prefix = name + ":";
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return arg.Substring(prefix.Length).Trim('"');
            }
            return null;
        }

        public static bool Wanted(string[] args)
        {
            return Flag(args, "/apply") || Flag(args, "/undo") || Flag(args, "/restore") || Flag(args, "/wake");
        }

        private static void Log(string message)
        {
            LogFile.Write(L.T(message));
        }

        public static int Run(string[] args)
        {
            if (Flag(args, "/wake"))
            {
                try
                {
                    Log("---- Wake-on-LAN ----");
                    int sent = WakeOnLan.WakeAll(Log, AppConfig.Load().Get("mask", "255.255.255.0"));
                    Log("Wake-up signal sent to " + sent + " PCs.");
                    return sent > 0 ? 0 : 2;
                }
                catch (Exception ex)
                {
                    Log("The silent run stopped: " + ex.Message);
                    return 3;
                }
            }

            bool restore = Flag(args, "/restore");
            bool apply = !Flag(args, "/undo") && !restore;
            try
            {
                AppConfig config = AppConfig.Load();
                Log("---- " + (restore ? "Restoring " : apply ? "Applying on " : "Undoing on ") + ComputerName.Active + " (silent run) ----");

                RunContext c = new RunContext();
                c.Users = Accounts.ListUsers();
                c.Log = Log;

                // the student account: by its real name, or by the name shown on the sign-in screen
                string wanted = Value(args, "/student") ?? config.Get("student", "Student");
                LocalUser student = Accounts.Find(c.Users, wanted);
                if (student == null)
                {
                    foreach (LocalUser user in c.Users)
                    {
                        if (!user.IsBuiltIn && string.Equals(user.FullName, wanted, StringComparison.OrdinalIgnoreCase)) student = user;
                    }
                }
                c.StudentName = student == null ? null : student.Name;
                if (student == null) Log("No account named '" + wanted + "' on this PC: the settings for the student will fail.");

                c.AdminName = config.Get("admin", "LabAdmin");
                c.AdminPassword = Value(args, "/adminpass") ?? "";
                c.DnsServer = config.Get("dns", "");
                c.DnsServer2 = config.Get("dns2", "");
                c.SubnetMask = config.Get("mask", "255.255.255.0");
                c.Gateway = config.Get("gateway", "");
                c.IpAddress = Value(args, "/ip") ?? "";
                c.NewComputerName = Value(args, "/name") ?? "";
                c.WallpaperPath = AppConfig.FromPortablePath(config.Get("wallpaper", ""));
                c.SchoolImagePath = AppConfig.FromPortablePath(config.Get("schoolimage", ""));
                c.ShutdownTime = NightShutdownSetting.CleanTime(Value(args, "/shutdown") ?? config.Get("shutdown", NightShutdownSetting.DefaultTime));

                List<Setting> run = new List<Setting>();
                if (restore)
                {
                    c.Restoring = true;
                    LocalUser recorded = Journal.Student(c);
                    if (recorded != null) c.StudentName = recorded.Name;

                    List<Setting> leftAlone = new List<Setting>();
                    run = Journal.RestoreList(c, true, leftAlone);
                    foreach (Setting setting in leftAlone)
                    {
                        Log("Left as it is in a silent run (restore it at the PC itself): " + setting.Title);
                    }
                    if (run.Count == 0)
                    {
                        Log("Nothing to restore: LabLock has not changed anything else on this PC.");
                        return 0;
                    }
                }
                else
                {
                    List<string> ticked = new List<string>(config.Get("ticked", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                    foreach (Setting setting in Catalog.Build())
                    {
                        if (!ticked.Contains(setting.Id)) continue;
                        bool perPc = (setting.Id == Catalog.IdFixedIp && c.IpAddress.Length == 0) ||
                                     (setting.Id == Catalog.IdRename && c.NewComputerName.Length == 0);
                        if (perPc)
                        {
                            Log("Skipped in a silent run (it differs per PC): " + setting.Title);
                            continue;
                        }
                        run.Add(setting);
                    }
                    if (run.Count == 0)
                    {
                        Log("Nothing to do: LabLock.ini has no ticked settings. Open LabLock once, tick what you want and close it.");
                        return 2;
                    }
                }

                int failed;
                int done = Engine.Run(run, c, apply, out failed);
                Log(done + " done, " + failed + " failed.");
                Snm.Report(config, c, restore ? "restore" : apply ? "apply" : "undo", Log);

                NetInfo net = null;
                try
                {
                    net = NetConfig.Current();
                }
                catch (Exception)
                {
                }
                string room = config.Get("room", "");
                PcRecord.Save(ComputerName.Pending(), net, MainForm.WindowsName(), Hardware.Read(), room.Length > 0 ? room : null, null);

                if (Flag(args, "/restart"))
                {
                    string output;
                    Proc.Run(Proc.SystemTool("shutdown.exe"), "/r /t 30 /c \"LabLock\"", out output);
                    Log("Restarting this PC.");
                }
                return failed > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                Log("The silent run stopped: " + ex.Message);
                return 3;
            }
        }
    }
}
