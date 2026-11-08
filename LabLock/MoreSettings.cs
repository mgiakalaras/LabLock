using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Management;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace LabLock
{
    /// <summary>
    /// The files that are built into LabLock.exe, so the program is one single file: the school image and the
    /// push script. They are added by the project file (EmbeddedResource) and by build.cmd (/resource).
    /// </summary>
    internal static class Embedded
    {
        public const string PushScript = "LabLock.push.ps1";

        /// <summary>Null when this copy of the program was built without that file.</summary>
        public static Stream Open(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }

        /// <summary>Writes a built-in file to disk. Returns false when it is not built in.</summary>
        public static bool SaveTo(string name, string path)
        {
            using (Stream stream = Open(name))
            {
                if (stream == null) return false;
                using (FileStream file = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        file.Write(buffer, 0, read);
                    }
                }
            }
            return true;
        }
    }

    /// <summary>C:\ProgramData\LabLock: where LabLock keeps the files a PC needs after the USB stick is gone.</summary>
    internal static class LabFolder
    {
        public static string FolderPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LabLock"); }
        }

        /// <summary>Creates the folder so that students can read it but only administrators can change it.</summary>
        public static string Ensure()
        {
            string path = FolderPath;
            DirectoryInfo folder = Directory.CreateDirectory(path);
            try
            {
                const InheritanceFlags Inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                DirectorySecurity security = new DirectorySecurity();
                security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    FileSystemRights.FullControl, Inherit, PropagationFlags.None, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                    FileSystemRights.FullControl, Inherit, PropagationFlags.None, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    FileSystemRights.ReadAndExecute, Inherit, PropagationFlags.None, AccessControlType.Allow));
                folder.SetAccessControl(security);
            }
            catch (Exception)
            {
                // The default permissions of ProgramData already stop students from changing files they did not create.
            }
            return path;
        }
    }

    /// <summary>Finds the real folders of the student account (they may be redirected or have another language's name).</summary>
    internal static class StudentFolders
    {
        private const string ShellFolders = @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";
        private const string DownloadsId = "{374DE290-123F-4565-9164-39C4925E467B}";

        public static string Profile(RunContext c)
        {
            string profile = StudentHive.ProfilePath(c.Student.Sid);
            if (string.IsNullOrEmpty(profile) || !Directory.Exists(profile))
            {
                throw new InvalidOperationException("The student's profile folder was not found. Sign in once with that account first.");
            }
            return profile.TrimEnd('\\');
        }

        private static string Resolve(RunContext c, string valueName, string fallbackName)
        {
            string profile = Profile(c);
            string path = Convert.ToString(Reg.ReadRaw(c, RegRoot.Student, ShellFolders, valueName)).Trim();

            const string Variable = "%USERPROFILE%";
            int at = path.IndexOf(Variable, StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                path = path.Substring(0, at) + profile + path.Substring(at + Variable.Length);
            }

            bool usable = path.Length > 3 && path.IndexOf('%') < 0 && Path.IsPathRooted(path) && Directory.Exists(path);
            return usable ? path.TrimEnd('\\') : Path.Combine(profile, fallbackName);
        }

        public static string Desktop(RunContext c)
        {
            return Resolve(c, "Desktop", "Desktop");
        }

        public static string Downloads(RunContext c)
        {
            return Resolve(c, DownloadsId, "Downloads");
        }

        public static string Temp(RunContext c)
        {
            return Path.Combine(Profile(c), @"AppData\Local\Temp");
        }

        public static string StartupFolder(RunContext c)
        {
            return Path.Combine(Profile(c), @"AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup");
        }

        public static bool IsInsideProfile(RunContext c, string path)
        {
            string profile = Profile(c) + "\\";
            return path.Length > profile.Length && path.StartsWith(profile, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Stops the student from starting programs that sit in Downloads or on the Desktop. It is done with a
    /// "deny execute" permission for the student on the files in those folders, which works on every edition
    /// of Windows. Documents, pictures and shortcuts open as usual.
    /// </summary>
    internal sealed class NoRunSetting : Setting
    {
        public NoRunSetting()
        {
            Id = "no-run-downloads";
            Group = Catalog.GroupStudent;
            Title = "Block running programs from the student's Downloads and Desktop";
            Hint = "A game or installer that was downloaded or copied there does not start. Documents and shortcuts still open. A determined student can move the file to another folder, so this stops the easy way, not every way.";
            DefaultOn = true;
            NeedsStudent = true;
            NeedsHive = true;
        }

        private static List<string> Folders(RunContext c)
        {
            List<string> list = new List<string>();
            list.Add(StudentFolders.Downloads(c));
            string desktop = StudentFolders.Desktop(c);
            if (!list.Contains(desktop)) list.Add(desktop);
            return list;
        }

        private static bool IsBlocked(string folder, SecurityIdentifier student)
        {
            if (!Directory.Exists(folder)) return false;
            DirectorySecurity security = new DirectoryInfo(folder).GetAccessControl(AccessControlSections.Access);
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType == AccessControlType.Deny && rule.IdentityReference.Equals(student) &&
                    (rule.FileSystemRights & FileSystemRights.ExecuteFile) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        public override string State(RunContext c)
        {
            SecurityIdentifier student = new SecurityIdentifier(c.Student.Sid);
            List<string> folders = Folders(c);
            int blocked = 0;
            foreach (string folder in folders)
            {
                if (IsBlocked(folder, student)) blocked++;
            }
            if (blocked == folders.Count) return "Applied";
            return blocked == 0 ? "Not applied" : "Partly applied";
        }

        public override void Apply(RunContext c)
        {
            SecurityIdentifier student = new SecurityIdentifier(c.Student.Sid);
            foreach (string folder in Folders(c))
            {
                if (!Directory.Exists(folder))
                {
                    c.Log("Folder not found, skipped: " + folder);
                    continue;
                }
                if (IsBlocked(folder, student)) continue;

                DirectoryInfo info = new DirectoryInfo(folder);
                DirectorySecurity security = info.GetAccessControl(AccessControlSections.Access);

                // Files in this folder and below: the student may not execute them. Folders themselves are not touched.
                security.AddAccessRule(new FileSystemAccessRule(student, FileSystemRights.ExecuteFile,
                    InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Deny));
                // The student may not take the block off again.
                security.AddAccessRule(new FileSystemAccessRule(student,
                    FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny));
                info.SetAccessControl(security);
                c.Log("Programs can no longer be started from " + folder + ".");
            }
        }

        public override void Undo(RunContext c)
        {
            SecurityIdentifier student = new SecurityIdentifier(c.Student.Sid);
            foreach (string folder in Folders(c))
            {
                if (!Directory.Exists(folder)) continue;
                DirectoryInfo info = new DirectoryInfo(folder);
                DirectorySecurity security = info.GetAccessControl(AccessControlSections.Access);
                security.RemoveAccessRuleAll(new FileSystemAccessRule(student, FileSystemRights.ExecuteFile, AccessControlType.Deny));
                info.SetAccessControl(security);
            }
        }
    }

    /// <summary>
    /// Empties the student's Downloads and Temp folders every time somebody signs in. A scheduled task that runs
    /// as the system starts a small script kept in C:\ProgramData\LabLock.
    /// </summary>
    internal sealed class CleanupSetting : Setting
    {
        private const string TaskName = "LabLock clean-up";
        private const string ScriptName = "cleanup.cmd";

        public CleanupSetting()
        {
            Id = Catalog.IdCleanup;
            Group = Catalog.GroupStudent;
            Title = "Empty the student's Downloads and Temp at every sign-in";
            Hint = "Everything in the student's Downloads and temporary files is DELETED each time somebody signs in to the PC. Documents, Desktop and Pictures are not touched. Tell the students to save their work in Documents or on a USB stick.";
            DefaultOn = false;
            NeedsStudent = true;
            NeedsHive = true;
        }

        private static string Schtasks
        {
            get { return Proc.SystemTool("schtasks.exe"); }
        }

        public override string State(RunContext c)
        {
            string output;
            return Proc.Run(Schtasks, "/query /tn \"" + TaskName + "\"", out output) == 0 ? "Applied" : "Not applied";
        }

        public override void Apply(RunContext c)
        {
            List<string> folders = new List<string>();
            folders.Add(StudentFolders.Downloads(c));
            folders.Add(StudentFolders.Temp(c));

            StringBuilder script = new StringBuilder();
            script.AppendLine("@echo off");
            script.AppendLine("rem Written by LabLock. Empties these folders of the student account at every sign-in.");
            script.AppendLine("chcp 65001 >nul");
            int count = 0;
            foreach (string folder in folders)
            {
                // Never clean anything outside the student's own profile, and nothing with characters a script could trip over.
                if (!StudentFolders.IsInsideProfile(c, folder) || folder.IndexOfAny(new char[] { '%', '"', '&', '^', '!' }) >= 0)
                {
                    c.Log("Not cleaned (outside the student's profile or an unusual name): " + folder);
                    continue;
                }
                script.AppendLine("call :empty \"" + folder + "\"");
                count++;
            }
            script.AppendLine("exit /b 0");
            script.AppendLine();
            script.AppendLine(":empty");
            script.AppendLine("if not exist \"%~1\\\" exit /b 0");
            script.AppendLine("del /f /s /q \"%~1\\*\" >nul 2>&1");
            script.AppendLine("for /d %%D in (\"%~1\\*\") do rd /s /q \"%%D\" >nul 2>&1");
            script.AppendLine("exit /b 0");

            if (count == 0)
            {
                throw new InvalidOperationException("No folder of the student could be cleaned safely.");
            }

            string scriptPath = Path.Combine(LabFolder.Ensure(), ScriptName);
            File.WriteAllText(scriptPath, script.ToString(), new UTF8Encoding(false));

            string command = scriptPath.IndexOf(' ') >= 0 ? "\\\"" + scriptPath + "\\\"" : scriptPath;
            Proc.RunOrThrow(Schtasks,
                "/create /f /tn \"" + TaskName + "\" /sc onlogon /ru SYSTEM /rl highest /tr \"" + command + "\"",
                "Could not create the clean-up task");
            c.Log("The student's Downloads and Temp are emptied at every sign-in from now on.");
        }

        public override void Undo(RunContext c)
        {
            string output;
            Proc.Run(Schtasks, "/delete /f /tn \"" + TaskName + "\"", out output);
            string scriptPath = Path.Combine(LabFolder.FolderPath, ScriptName);
            if (File.Exists(scriptPath)) File.Delete(scriptPath);
        }
    }

    /// <summary>
    /// Windows Firewall rules that let DNS questions go only to the DNS servers typed in the window.
    /// A program or a student who points a browser at another DNS server gets no answer.
    /// </summary>
    internal sealed class DnsLockSetting : Setting
    {
        private static readonly string[] RuleNames = { "LabLock DNS lock UDP", "LabLock DNS lock TCP", "LabLock DNS lock DoT" };
        private static readonly string[] RuleFilters = { "protocol=UDP remoteport=53", "protocol=TCP remoteport=53", "protocol=TCP remoteport=853" };

        public DnsLockSetting()
        {
            Id = Catalog.IdDnsLock;
            Group = Catalog.GroupComputer;
            Title = "Allow DNS only through the DNS servers typed on the left (firewall)";
            Hint = "Firewall rules block DNS to every other server, so the Pi-hole cannot be bypassed with another DNS. The PC must already use the typed servers. If you type a second server, it stays allowed as the spare for when the Pi-hole is down.";
            DefaultOn = false;
        }

        private static string Netsh
        {
            get { return Proc.SystemTool("netsh.exe"); }
        }

        private static string Dotted(uint value)
        {
            return (value >> 24) + "." + ((value >> 16) & 255) + "." + ((value >> 8) & 255) + "." + (value & 255);
        }

        /// <summary>Every IPv4 address except the allowed ones, written as ranges for netsh.</summary>
        private static string BlockedRanges(List<string> allowed)
        {
            List<uint> numbers = new List<uint>();
            foreach (string ip in allowed)
            {
                uint number = NetConfig.ToNumber(ip);
                if (!numbers.Contains(number)) numbers.Add(number);
            }
            numbers.Sort();

            const uint First = 0x01000000;   // 1.0.0.0
            const uint Last = 0xFFFFFFFE;    // 255.255.255.254
            List<string> ranges = new List<string>();
            uint start = First;
            bool finished = false;
            foreach (uint number in numbers)
            {
                if (number < start) continue;
                if (number > start) ranges.Add(Dotted(start) + "-" + Dotted(number - 1));
                if (number >= Last)
                {
                    finished = true;
                    break;
                }
                start = number + 1;
            }
            if (!finished) ranges.Add(Dotted(start) + "-" + Dotted(Last));
            return string.Join(",", ranges.ToArray());
        }

        private static bool RuleExists(string name)
        {
            string output;
            return Proc.Run(Netsh, "advfirewall firewall show rule name=\"" + name + "\"", out output) == 0;
        }

        private static void DeleteRules()
        {
            foreach (string name in RuleNames)
            {
                string output;
                Proc.Run(Netsh, "advfirewall firewall delete rule name=\"" + name + "\"", out output);
            }
        }

        public override string State(RunContext c)
        {
            int found = 0;
            foreach (string name in RuleNames)
            {
                if (RuleExists(name)) found++;
            }
            if (found == RuleNames.Length) return "Applied";
            return found == 0 ? "Not applied" : "Partly applied";
        }

        public override void Apply(RunContext c)
        {
            string problem = NetConfig.DnsProblem(c.DnsServer, c.DnsServer2);
            if (problem != null) throw new InvalidOperationException(problem);
            List<string> allowed = NetConfig.DnsList(c.DnsServer, c.DnsServer2);

            // Never cut the PC off from the DNS server it is using right now.
            foreach (string inUse in NetConfig.AllCurrentDns())
            {
                if (!allowed.Contains(inUse))
                {
                    throw new InvalidOperationException("This PC now uses the DNS server " + inUse +
                        ", which would be blocked. Tick the fixed IP or the DNS setting as well, so the PC uses the typed servers first.");
                }
            }

            DeleteRules();
            string ranges = BlockedRanges(allowed);
            for (int i = 0; i < RuleNames.Length; i++)
            {
                Proc.RunOrThrow(Netsh,
                    "advfirewall firewall add rule name=\"" + RuleNames[i] + "\" dir=out action=block " + RuleFilters[i] + " remoteip=" + ranges,
                    "Could not add the firewall rule");
            }
            c.Log("DNS is only allowed to " + string.Join(", ", allowed.ToArray()) + ".");
        }

        public override void Undo(RunContext c)
        {
            DeleteRules();
        }
    }

    internal sealed class LockoutSetting : Setting
    {
        private const int Attempts = 10;
        private const int Minutes = 10;
        private const string LabLockKey = @"SOFTWARE\LabLock";
        private const string PreviousValue = "PreviousLockoutThreshold";

        public LockoutSetting()
        {
            Id = "account-lockout";
            Group = Catalog.GroupAccounts;
            Title = "Lock an account for 10 minutes after 10 wrong passwords";
            Hint = "Stops students from guessing the administrator's password by trying again and again. It counts for every account, so somebody can also lock the student account for 10 minutes on purpose.";
            DefaultOn = false;
        }

        public override string State(RunContext c)
        {
            int threshold = Accounts.LockoutThreshold();
            return threshold > 0 ? "Applied (after " + threshold + " wrong passwords)" : "Not applied";
        }

        public override void Apply(RunContext c)
        {
            // Remember the rule the PC had before, once, so Undo can put it back.
            if (Reg.Read(c, RegRoot.Machine, LabLockKey, PreviousValue) == null)
            {
                Reg.Write(c, new RegValue(RegRoot.Machine, LabLockKey, PreviousValue, Accounts.LockoutThreshold()));
            }
            Proc.RunOrThrow(Proc.SystemTool("net.exe"),
                "accounts /lockoutthreshold:" + Attempts + " /lockoutwindow:" + Minutes + " /lockoutduration:" + Minutes,
                "Could not set the lockout rule");
        }

        public override void Undo(RunContext c)
        {
            object saved = Reg.Read(c, RegRoot.Machine, LabLockKey, PreviousValue);
            int previous = saved is int ? (int)saved : 0;
            if (previous < 0) previous = 0;
            Proc.RunOrThrow(Proc.SystemTool("net.exe"), "accounts /lockoutthreshold:" + previous, "Could not remove the lockout rule");
            Reg.Delete(c, RegRoot.Machine, LabLockKey, PreviousValue);
            c.Log(previous == 0 ? "Accounts are no longer locked after wrong passwords." : "The lockout rule is back to " + previous + " wrong passwords, as before LabLock.");
        }
    }

    /// <summary>Makes a Windows restore point before the other settings run. Always the first setting in the list.</summary>
    internal sealed class RestorePointSetting : Setting
    {
        private const string PointName = "LabLock";

        public RestorePointSetting()
        {
            Id = Catalog.IdRestorePoint;
            Group = Catalog.GroupSafety;
            Title = "Make a Windows restore point first";
            Hint = "Turns on System Protection for the Windows drive and makes a restore point named LabLock before anything else is changed. Windows makes at most one per day. It is a general safety net; to take LabLock's settings back, use Undo.";
            DefaultOn = true;
        }

        public override string State(RunContext c)
        {
            try
            {
                DateTime latest = DateTime.MinValue;
                ManagementScope scope = new ManagementScope(@"\\.\root\default");
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT Description, CreationTime FROM SystemRestore")))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementBaseObject item in results)
                    {
                        if (Convert.ToString(item["Description"]) != PointName) continue;
                        DateTime created = ManagementDateTimeConverter.ToDateTime(Convert.ToString(item["CreationTime"]));
                        if (created > latest) latest = created;
                    }
                }
                return latest == DateTime.MinValue ? "Not applied" : "Applied (restore point of " + latest.ToString("yyyy-MM-dd") + ")";
            }
            catch (Exception)
            {
                return "Not applied";
            }
        }

        public override void Apply(RunContext c)
        {
            c.Log("Making a restore point. This can take a minute...");
            string script = "$ErrorActionPreference='Stop'; Enable-ComputerRestore -Drive ($env:SystemDrive + '\\'); " +
                            "Checkpoint-Computer -Description '" + PointName + "' -RestorePointType 'MODIFY_SETTINGS' -WarningAction SilentlyContinue";
            Proc.RunOrThrow(Proc.PowerShell, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"",
                "Windows could not make a restore point");
        }

        public override void Undo(RunContext c)
        {
            c.Log("Restore points stay on the PC. Windows removes old ones by itself.");
        }
    }

    /// <summary>Turns the school image into the pictures Windows needs.</summary>
    internal static class SchoolImage
    {
        public const string DefaultFileName = "LabLock-school.png";
        public const string BuiltIn = "the image built into LabLock";
        private const string ResourceName = "LabLock.school.png";

        /// <summary>
        /// The image file to use: the one typed in the window, or a LabLock-school.png next to the program.
        /// Null when there is neither; then the image that is built into LabLock.exe is used.
        /// </summary>
        public static string FileOrNull(string typed)
        {
            if (!string.IsNullOrEmpty(typed) && File.Exists(typed)) return typed;
            string beside = Path.Combine(AppConfig.BaseDir, DefaultFileName);
            return File.Exists(beside) ? beside : null;
        }

        /// <summary>Which image will be used, in words: a file name, or the built-in one.</summary>
        public static string Describe(string typed)
        {
            string file = FileOrNull(typed);
            return file == null ? BuiltIn : Path.GetFileName(file);
        }

        public static bool Available(string typed)
        {
            if (FileOrNull(typed) != null) return true;
            using (Stream stream = Embedded.Open(ResourceName))
            {
                return stream != null;
            }
        }

        /// <summary>Opens the image (a copy in memory, so no file stays open). The caller disposes it.</summary>
        public static Image Load(string typed)
        {
            string file = FileOrNull(typed);
            if (file != null)
            {
                using (Image fromFile = Image.FromFile(file))
                {
                    return new Bitmap(fromFile);
                }
            }
            using (Stream stream = Embedded.Open(ResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Choose a school image first: this copy of LabLock has no built-in image.");
                }
                using (Image fromStream = Image.FromStream(stream))
                {
                    return new Bitmap(fromStream);
                }
            }
        }

        private static void Prepare(Graphics g)
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        }

        /// <summary>A square picture on white with some air around the image (Windows shows account pictures in a circle).</summary>
        public static Bitmap Square(Image source, int size)
        {
            Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                Prepare(g);
                g.Clear(Color.White);
                double room = size * 0.80;
                double scale = Math.Min(room / source.Width, room / source.Height);
                int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                g.DrawImage(source, new Rectangle((size - width) / 2, (size - height) / 2, width, height));
            }
            return bitmap;
        }

        /// <summary>A 1920 x 1080 picture: a wide photo fills it, a logo is centred on white.</summary>
        public static Bitmap LockScreen(Image source)
        {
            const int Width = 1920;
            const int Height = 1080;
            Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                Prepare(g);
                g.Clear(Color.White);
                bool wide = (double)source.Width / source.Height >= 1.3;
                double scale = wide
                    ? Math.Max((double)Width / source.Width, (double)Height / source.Height)
                    : Math.Min(Width * 0.60 / source.Width, Height * 0.62 / source.Height);
                int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                g.DrawImage(source, new Rectangle((Width - width) / 2, (Height - height) / 2, width, height));
            }
            return bitmap;
        }
    }

    /// <summary>Gives every account the school image as its picture and stops users from choosing another one.</summary>
    internal sealed class AccountPictureSetting : Setting
    {
        private const string LabLockKey = @"SOFTWARE\LabLock";
        private const string Marker = "AccountPicture";
        private const string PolicyKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        private const string PolicyName = "UseDefaultTile";
        private const string BackupName = "AccountPictures-original";

        private static readonly string[] FileNames = { "user.png", "user.bmp", "user-32.png", "user-40.png", "user-48.png", "user-192.png" };
        private static readonly int[] FileSizes = { 448, 448, 32, 40, 48, 192 };

        public AccountPictureSetting()
        {
            Id = Catalog.IdAccountPicture;
            Group = Catalog.GroupComputer;
            Title = "Use the school image as the picture of every account, and lock it";
            Hint = "Replaces Windows' standard account picture with the school image and makes every account use it, so nobody can put another picture there. The original pictures are kept and come back with Undo.";
            DefaultOn = true;
        }

        private static string PicturesFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\User Account Pictures"); }
        }

        public override string State(RunContext c)
        {
            object policy = Reg.Read(c, RegRoot.Machine, PolicyKey, PolicyName);
            bool forced = policy is int && (int)policy == 1;
            bool ours = Convert.ToString(Reg.Read(c, RegRoot.Machine, LabLockKey, Marker)).Length > 0;
            if (forced && ours) return "Applied";
            if (forced) return "Partly applied (locked, but with Windows' own picture)";
            return "Not applied";
        }

        public override void Apply(RunContext c)
        {
            string source = SchoolImage.Describe(c.SchoolImagePath);
            string folder = PicturesFolder;
            if (!Directory.Exists(folder))
            {
                throw new InvalidOperationException("The folder with Windows' account pictures was not found: " + folder);
            }

            // Keep the original pictures once.
            string backup = Path.Combine(LabFolder.Ensure(), BackupName);
            if (!Directory.Exists(backup))
            {
                Directory.CreateDirectory(backup);
                foreach (string name in FileNames)
                {
                    string original = Path.Combine(folder, name);
                    if (File.Exists(original)) File.Copy(original, Path.Combine(backup, name), true);
                }
            }

            using (Image image = SchoolImage.Load(c.SchoolImagePath))
            {
                for (int i = 0; i < FileNames.Length; i++)
                {
                    using (Bitmap picture = SchoolImage.Square(image, FileSizes[i]))
                    {
                        bool bmp = FileNames[i].EndsWith(".bmp", StringComparison.OrdinalIgnoreCase);
                        picture.Save(Path.Combine(folder, FileNames[i]), bmp ? ImageFormat.Bmp : ImageFormat.Png);
                    }
                }
            }

            Reg.Write(c, new RegValue(RegRoot.Machine, PolicyKey, PolicyName, 1));
            Reg.Write(c, new RegValue(RegRoot.Machine, LabLockKey, Marker, source));
        }

        public override void Undo(RunContext c)
        {
            string backup = Path.Combine(LabFolder.FolderPath, BackupName);
            if (Directory.Exists(backup) && Directory.Exists(PicturesFolder))
            {
                foreach (string name in FileNames)
                {
                    string saved = Path.Combine(backup, name);
                    if (File.Exists(saved)) File.Copy(saved, Path.Combine(PicturesFolder, name), true);
                }
            }
            Reg.Delete(c, RegRoot.Machine, PolicyKey, PolicyName);
            Reg.Delete(c, RegRoot.Machine, LabLockKey, Marker);
        }
    }

    /// <summary>Puts the school image on the lock screen and stops users from changing it.</summary>
    internal sealed class LockScreenSetting : Setting
    {
        private const string CspKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP";
        private const string PolicyKey = @"SOFTWARE\Policies\Microsoft\Windows\Personalization";
        private const string FileName = "lockscreen.jpg";

        public LockScreenSetting()
        {
            Id = Catalog.IdLockScreen;
            Group = Catalog.GroupComputer;
            Title = "Use the school image on the lock screen, and lock it";
            Hint = "Makes a lock screen picture from the school image (a logo is centred on white, a wide photo fills the screen) and stops everybody from choosing another one.";
            DefaultOn = true;
        }

        private static string Target
        {
            get { return Path.Combine(LabFolder.FolderPath, FileName); }
        }

        public override string State(RunContext c)
        {
            string path = Convert.ToString(Reg.Read(c, RegRoot.Machine, CspKey, "LockScreenImagePath"));
            object status = Reg.Read(c, RegRoot.Machine, CspKey, "LockScreenImageStatus");
            bool ours = string.Equals(path, Target, StringComparison.OrdinalIgnoreCase) && File.Exists(Target);
            bool active = status is int && (int)status == 1;
            if (ours && active) return "Applied";
            return path.Length > 0 ? "Another lock screen picture is forced" : "Not applied";
        }

        public override void Apply(RunContext c)
        {
            LabFolder.Ensure();
            using (Image image = SchoolImage.Load(c.SchoolImagePath))
            using (Bitmap picture = SchoolImage.LockScreen(image))
            {
                picture.Save(Target, ImageFormat.Jpeg);
            }

            // Works on Pro as well as Education / Enterprise.
            Reg.Write(c, new RegValue(RegRoot.Machine, CspKey, "LockScreenImagePath", Target));
            Reg.Write(c, new RegValue(RegRoot.Machine, CspKey, "LockScreenImageUrl", Target));
            Reg.Write(c, new RegValue(RegRoot.Machine, CspKey, "LockScreenImageStatus", 1));
            // The classic policy, which Education / Enterprise obey.
            Reg.Write(c, new RegValue(RegRoot.Machine, PolicyKey, "LockScreenImage", Target));
            Reg.Write(c, new RegValue(RegRoot.Machine, PolicyKey, "NoChangingLockScreen", 1));
        }

        public override void Undo(RunContext c)
        {
            Reg.Delete(c, RegRoot.Machine, CspKey, "LockScreenImagePath");
            Reg.Delete(c, RegRoot.Machine, CspKey, "LockScreenImageUrl");
            Reg.Delete(c, RegRoot.Machine, CspKey, "LockScreenImageStatus");
            Reg.Delete(c, RegRoot.Machine, PolicyKey, "LockScreenImage");
            Reg.Delete(c, RegRoot.Machine, PolicyKey, "NoChangingLockScreen");
        }
    }
}
