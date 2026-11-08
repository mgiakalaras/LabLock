using System;
using System.Collections.Generic;
using System.IO;

namespace LabLock
{
    /// <summary>Everything a setting needs to know about this run: the chosen accounts and the typed options.</summary>
    internal sealed class RunContext
    {
        public List<LocalUser> Users = new List<LocalUser>();
        public string StudentName;
        public string AdminName = "";
        public string AdminPassword = "";
        public string DnsServer = "";
        public string DnsServer2 = "";
        public string IpAddress = "";
        public string SubnetMask = "";
        public string Gateway = "";
        public string NewComputerName = "";
        public string WallpaperPath = "";
        public string SchoolImagePath = "";
        public string ShutdownTime = NightShutdownSetting.DefaultTime;
        public Action<string> Log;

        /// <summary>Set by a setting when its change only takes effect after a restart (the computer name).</summary>
        public bool RestartNeeded;

        /// <summary>
        /// True while "Restore this PC" runs: settings then only take back what LabLock itself changed
        /// (for example the student becomes an administrator again only if LabLock took that away).
        /// </summary>
        public bool Restoring;

        /// <summary>Registry key name under HKEY_USERS that holds the student's settings, or null.</summary>
        public string HiveRoot;
        public string HiveProblem;

        public LocalUser Student
        {
            get { return Accounts.Find(Users, StudentName); }
        }
    }

    /// <summary>One line of the checklist. Every setting can report its state, be applied and be undone.</summary>
    internal abstract class Setting
    {
        public string Id;
        public string Group;
        public string Title;
        public string Hint;
        public bool DefaultOn;

        /// <summary>The setting acts on the student account, so one must be chosen.</summary>
        public bool NeedsStudent;

        /// <summary>The setting is stored in the student's own registry settings.</summary>
        public bool NeedsHive;

        public abstract string State(RunContext c);
        public abstract void Apply(RunContext c);
        public abstract void Undo(RunContext c);
    }

    /// <summary>A setting that is nothing more than a few policy values in the registry.</summary>
    internal sealed class RegistrySetting : Setting
    {
        private readonly RegValue[] values;

        public RegistrySetting(string id, string group, string title, string hint, bool defaultOn, params RegValue[] policyValues)
        {
            Id = id;
            Group = group;
            Title = title;
            Hint = hint;
            DefaultOn = defaultOn;
            values = policyValues;
            foreach (RegValue value in values)
            {
                if (value.Root == RegRoot.Student)
                {
                    NeedsStudent = true;
                    NeedsHive = true;
                }
            }
        }

        public override string State(RunContext c)
        {
            int matching = 0;
            foreach (RegValue value in values)
            {
                if (Reg.Matches(Reg.Read(c, value.Root, value.Path, value.Name), value)) matching++;
            }
            if (matching == values.Length) return "Applied";
            return matching == 0 ? "Not applied" : "Partly applied";
        }

        public override void Apply(RunContext c)
        {
            foreach (RegValue value in values)
            {
                Reg.Write(c, value);
            }
        }

        public override void Undo(RunContext c)
        {
            foreach (RegValue value in values)
            {
                Reg.Delete(c, value.Root, value.Path, value.Name);
            }
        }
    }

    internal sealed class AdminAccountSetting : Setting
    {
        public AdminAccountSetting()
        {
            Id = Catalog.IdAdmin;
            Group = Catalog.GroupAccounts;
            Title = "Create a separate administrator account";
            Hint = "Creates the administrator account typed on the left with the password you type. If it already exists it is only checked; its password is not changed.";
            DefaultOn = true;
        }

        public override string State(RunContext c)
        {
            if (c.AdminName.Length == 0) return "Type an administrator name on the left";
            LocalUser user = Accounts.Find(c.Users, c.AdminName);
            if (user == null) return "Not applied ('" + c.AdminName + "' does not exist)";
            if (user.Disabled) return "'" + c.AdminName + "' exists but is disabled";
            return user.IsAdmin ? "Applied ('" + c.AdminName + "' is an administrator)" : "'" + c.AdminName + "' exists, not an administrator";
        }

        public override void Apply(RunContext c)
        {
            string name = c.AdminName;
            if (name.Length == 0)
            {
                throw new InvalidOperationException("Type a name for the administrator account.");
            }
            if (c.Student != null && string.Equals(name, c.Student.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The administrator account must not be the student account.");
            }

            LocalUser existing = Accounts.Find(Accounts.ListUsers(), name);
            if (existing == null)
            {
                if (string.IsNullOrEmpty(c.AdminPassword))
                {
                    throw new InvalidOperationException("Type a password for the new administrator account.");
                }
                Accounts.CreateUser(name, c.AdminPassword, "Lab administrator (created by LabLock)");
                c.Log("Created the account '" + name + "'.");
            }
            else if (existing.Disabled)
            {
                throw new InvalidOperationException("The account '" + name + "' exists but is disabled. Enable it or choose another name.");
            }
            else
            {
                c.Log("The account '" + name + "' already exists; its password was left unchanged.");
            }

            string sid = Accounts.SidOf(name);
            Accounts.AddToGroup(Accounts.AdminsSid, sid);
            Accounts.AddToGroup(Accounts.UsersSid, sid);
            if (!Accounts.GroupMemberSids(Accounts.AdminsSid).Contains(sid))
            {
                throw new InvalidOperationException("Could not confirm that '" + name + "' is an administrator.");
            }
            c.Users = Accounts.ListUsers();
        }

        public override void Undo(RunContext c)
        {
            c.Log("LabLock never deletes accounts. To remove '" + c.AdminName + "', use Computer Management > Local Users and Groups.");
        }
    }

    internal sealed class DemoteStudentSetting : Setting
    {
        private const string LabLockKey = @"SOFTWARE\LabLock";
        private const string DemotedValue = "DemotedStudentSid";

        public DemoteStudentSetting()
        {
            Id = Catalog.IdDemote;
            Group = Catalog.GroupAccounts;
            Title = "Make the student account a standard user (no admin rights)";
            Hint = "The most important setting: without admin rights students cannot install programs or change the system. Only done after the administrator account above is confirmed to work.";
            DefaultOn = true;
            NeedsStudent = true;
        }

        public override string State(RunContext c)
        {
            return c.Student.IsAdmin ? "Not applied (student is an administrator)" : "Applied (standard user)";
        }

        public override void Apply(RunContext c)
        {
            List<LocalUser> users = Accounts.ListUsers();
            LocalUser student = Accounts.Find(users, c.StudentName);
            if (student == null)
            {
                throw new InvalidOperationException("The student account was not found.");
            }

            // Safety: never take admin rights away unless another working administrator exists.
            LocalUser admin = Accounts.Find(users, c.AdminName);
            bool adminIsUsable = admin != null && !admin.Disabled && admin.IsAdmin &&
                !string.Equals(admin.Sid, student.Sid, StringComparison.OrdinalIgnoreCase);
            if (!adminIsUsable)
            {
                throw new InvalidOperationException("'" + c.AdminName + "' is not a working administrator account on this PC, so the student account was NOT changed. Tick 'Create a separate administrator account' too.");
            }

            // Remember that LabLock took the rights away, so "Restore this PC" gives them back and nothing more.
            if (student.IsAdmin)
            {
                Reg.Write(c, new RegValue(RegRoot.Machine, LabLockKey, DemotedValue, student.Sid));
            }
            Accounts.AddToGroup(Accounts.UsersSid, student.Sid);
            Accounts.RemoveFromGroup(Accounts.AdminsSid, student.Sid);
            c.Users = Accounts.ListUsers();
            c.Log("'" + student.Name + "' is now a standard user. This takes effect the next time the student signs in.");
        }

        public override void Undo(RunContext c)
        {
            LocalUser student = c.Student;
            string demoted = Convert.ToString(Reg.Read(c, RegRoot.Machine, LabLockKey, DemotedValue));
            if (c.Restoring && !string.Equals(demoted, student.Sid, StringComparison.OrdinalIgnoreCase))
            {
                c.Log("'" + student.Name + "' was not an administrator before LabLock, so it stays a standard user.");
                return;
            }
            Accounts.AddToGroup(Accounts.AdminsSid, student.Sid);
            Reg.Delete(c, RegRoot.Machine, LabLockKey, DemotedValue);
            c.Users = Accounts.ListUsers();
            c.Log("'" + student.Name + "' is an administrator again (from the next sign-in).");
        }
    }

    internal sealed class PasswordChangeSetting : Setting
    {
        public PasswordChangeSetting()
        {
            Id = "no-password-change";
            Group = Catalog.GroupAccounts;
            Title = "Student cannot change the account's password";
            Hint = "Stops a student from changing or setting the password of the shared account and locking everybody else out.";
            DefaultOn = true;
            NeedsStudent = true;
        }

        public override string State(RunContext c)
        {
            return c.Student.PasswordChangeable ? "Not applied" : "Applied";
        }

        private static void Set(RunContext c, bool allowed)
        {
            string arguments = "user \"" + c.Student.Name + "\" /passwordchg:" + (allowed ? "yes" : "no");
            Proc.RunOrThrow(Proc.SystemTool("net.exe"), arguments, "Could not change the password rule");
            c.Users = Accounts.ListUsers();
        }

        public override void Apply(RunContext c)
        {
            Set(c, false);
        }

        public override void Undo(RunContext c)
        {
            Set(c, true);
        }
    }

    internal sealed class WallpaperSetting : Setting
    {
        private const string LockName = "NoChangingWallPaper";

        public WallpaperSetting()
        {
            Id = Catalog.IdWallpaper;
            Group = Catalog.GroupStudent;
            Title = "Lock the desktop wallpaper";
            Hint = "Forces one wallpaper for the student, so it cannot be changed from anywhere (not even with 'Set as desktop background'). Uses the image you chose, or the standard Windows wallpaper if you chose none.";
            DefaultOn = true;
            NeedsStudent = true;
            NeedsHive = true;
        }

        public override string State(RunContext c)
        {
            object locked = Reg.Read(c, RegRoot.Student, Catalog.PolActiveDesktop, LockName);
            if (!(locked is int) || (int)locked != 1) return "Not applied";
            string image = Convert.ToString(Reg.Read(c, RegRoot.Student, Catalog.PolSystem, "Wallpaper"));
            return image.Length > 0 ? "Applied (image forced)" : "Applied (no image forced)";
        }

        public override void Apply(RunContext c)
        {
            Reg.Write(c, new RegValue(RegRoot.Student, Catalog.PolActiveDesktop, LockName, 1));

            string image;
            if (c.WallpaperPath.Length == 0)
            {
                // No image chosen: force the standard Windows wallpaper. Only a forced image makes the lock complete.
                image = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Web\Wallpaper\Windows\img0.jpg");
                if (!File.Exists(image))
                {
                    c.Log("The standard Windows wallpaper was not found, so no image is forced. Choose a wallpaper image to make the lock complete.");
                    return;
                }
                c.Log("No wallpaper image chosen: the standard Windows wallpaper is forced.");
            }
            else
            {
                if (!File.Exists(c.WallpaperPath))
                {
                    throw new InvalidOperationException("The wallpaper image was not found: " + c.WallpaperPath);
                }

                // Copy the image onto the PC so it still works when the USB stick is gone.
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LabLock");
                Directory.CreateDirectory(folder);
                image = Path.Combine(folder, "wallpaper" + Path.GetExtension(c.WallpaperPath).ToLowerInvariant());
                string source = Path.GetFullPath(c.WallpaperPath);
                if (!string.Equals(source, image, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(source, image, true);
                }
                c.Log("Wallpaper image copied to " + image + ".");
            }

            Reg.Write(c, new RegValue(RegRoot.Student, Catalog.PolSystem, "Wallpaper", image));
            Reg.Write(c, new RegValue(RegRoot.Student, Catalog.PolSystem, "WallpaperStyle", "4"));   // 4 = fill the screen
        }

        public override void Undo(RunContext c)
        {
            Reg.Delete(c, RegRoot.Student, Catalog.PolActiveDesktop, LockName);
            Reg.Delete(c, RegRoot.Student, Catalog.PolSystem, "Wallpaper");
            Reg.Delete(c, RegRoot.Student, Catalog.PolSystem, "WallpaperStyle");
        }
    }

    /// <summary>Left Alt + Left Shift + Print Screen switches Windows to High Contrast; this turns that shortcut off.</summary>
    internal sealed class HighContrastSetting : Setting
    {
        private const string KeyPath = @"Control Panel\Accessibility\HighContrast";
        private const int HotkeyActive = 4;
        private const int DefaultFlags = 126;

        public HighContrastSetting()
        {
            Id = "no-high-contrast-key";
            Group = Catalog.GroupStudent;
            Title = "Turn off the High Contrast keyboard shortcut";
            Hint = "Left Alt + Left Shift + Print Screen flips the whole screen to High Contrast colours even when Settings is blocked. Leave this off if a student needs High Contrast.";
            DefaultOn = true;
            NeedsStudent = true;
            NeedsHive = true;
        }

        private static int ReadFlags(RunContext c)
        {
            int flags;
            string text = Convert.ToString(Reg.Read(c, RegRoot.Student, KeyPath, "Flags"));
            return int.TryParse(text, out flags) ? flags : DefaultFlags;
        }

        public override string State(RunContext c)
        {
            return (ReadFlags(c) & HotkeyActive) == 0 ? "Applied" : "Not applied";
        }

        public override void Apply(RunContext c)
        {
            int flags = ReadFlags(c) & ~HotkeyActive;
            Reg.Write(c, new RegValue(RegRoot.Student, KeyPath, "Flags", flags.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        public override void Undo(RunContext c)
        {
            int flags = ReadFlags(c) | HotkeyActive;
            Reg.Write(c, new RegValue(RegRoot.Student, KeyPath, "Flags", flags.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>Blocks the web game sites listed in LabLock-blocklist.txt, plus the games built into the browsers.</summary>
    internal sealed class BlocklistSetting : Setting
    {
        private static readonly RegValue[] BuiltInGames =
        {
            new RegValue(RegRoot.Machine, @"SOFTWARE\Policies\Google\Chrome", "AllowDinosaurEasterEgg", 0),
            new RegValue(RegRoot.Machine, @"SOFTWARE\Policies\BraveSoftware\Brave", "AllowDinosaurEasterEgg", 0),
            new RegValue(RegRoot.Machine, @"SOFTWARE\Policies\Microsoft\Edge", "AllowSurfGame", 0)
        };

        public BlocklistSetting()
        {
            Id = Catalog.IdBlocklist;
            Group = Catalog.GroupComputer;
            Title = "Block the game sites in the blocklist";
            Hint = "Blocks every site in " + Blocklist.FileName + " on this PC (hosts file plus the block lists of Chrome, Edge, Brave and Firefox) and the browsers' built-in games. Edit the list, then apply again.";
            DefaultOn = true;
        }

        public override string State(RunContext c)
        {
            return Blocklist.State();
        }

        public override void Apply(RunContext c)
        {
            int count = Blocklist.Apply(c.Log);
            foreach (RegValue value in BuiltInGames)
            {
                Reg.Write(c, value);
            }
            c.Log(count + " blocklist entries are now blocked on this PC.");
        }

        public override void Undo(RunContext c)
        {
            Blocklist.Undo(c.Log);
            foreach (RegValue value in BuiltInGames)
            {
                Reg.Delete(c, value.Root, value.Path, value.Name);
            }
        }
    }

    internal sealed class DnsSetting : Setting
    {
        public DnsSetting()
        {
            Id = Catalog.IdDns;
            Group = Catalog.GroupComputer;
            Title = "Use the DNS servers typed on the left (Pi-hole), keep the IP address as it is";
            Hint = "Only changes the DNS servers of the connected network adapters. Not needed together with the fixed IP setting, which sets the DNS too. A second DNS server that is not the Pi-hole lets blocked sites through.";
            DefaultOn = false;
        }

        public override string State(RunContext c)
        {
            return NetConfig.DnsState(NetConfig.DnsList(c.DnsServer, c.DnsServer2));
        }

        public override void Apply(RunContext c)
        {
            string problem = NetConfig.DnsProblem(c.DnsServer, c.DnsServer2);
            if (problem != null) throw new InvalidOperationException(problem);
            NetConfig.ApplyDns(NetConfig.DnsList(c.DnsServer, c.DnsServer2), c.Log);
        }

        public override void Undo(RunContext c)
        {
            NetConfig.UndoDns(c.Log);
        }
    }

    /// <summary>Gives the PC the fixed IP address, mask, gateway and DNS servers typed in the window.</summary>
    internal sealed class FixedIpSetting : Setting
    {
        public FixedIpSetting()
        {
            Id = Catalog.IdFixedIp;
            Group = Catalog.GroupComputer;
            Title = "Give this PC the fixed IP address typed on the left";
            Hint = "Sets the IP address, subnet mask, gateway and DNS servers on the PC's network adapter, after checking that no other device answers on that address. Undo puts back the address the PC had before LabLock (or an automatic address, if it had one).";
            DefaultOn = false;
        }

        public override string State(RunContext c)
        {
            return NetConfig.FixedState(c.IpAddress);
        }

        public override void Apply(RunContext c)
        {
            string problem = NetConfig.DnsProblem(c.DnsServer, c.DnsServer2);
            if (problem != null) throw new InvalidOperationException(problem);
            NetConfig.ApplyFixed(c.IpAddress, c.SubnetMask, c.Gateway, NetConfig.DnsList(c.DnsServer, c.DnsServer2), c.Log);
        }

        public override void Undo(RunContext c)
        {
            NetConfig.UndoFixed(c.Log);
        }
    }

    /// <summary>Renames the PC. Always the last setting in the list, because the new name needs a restart.</summary>
    internal sealed class RenameSetting : Setting
    {
        private const string LabLockKey = @"SOFTWARE\LabLock";
        private const string PreviousName = "PreviousComputerName";

        public RenameSetting()
        {
            Id = Catalog.IdRename;
            Group = Catalog.GroupComputer;
            Title = "Rename this PC to the name typed on the left (done last, then restart)";
            Hint = "Changes the computer name. It runs after all other settings and LabLock then offers to restart the PC, because Windows only uses the new name after a restart.";
            DefaultOn = false;
        }

        public override string State(RunContext c)
        {
            string wanted = c.NewComputerName;
            string active = ComputerName.Active;
            string pending = ComputerName.Pending();

            if (wanted.Length == 0) return "Type a computer name on the left";
            if (!ComputerName.Same(pending, active))
            {
                return ComputerName.Same(wanted, pending)
                    ? "Applied (" + pending + " after the restart, still " + active + ")"
                    : "Restart waiting: " + active + " -> " + pending;
            }
            return ComputerName.Same(wanted, active) ? "Applied (this PC is called " + active + ")" : "Not applied (this PC is called " + active + ")";
        }

        private static void Change(RunContext c, string newName)
        {
            ComputerName.Rename(newName);
            c.RestartNeeded = true;
            c.Log("The computer name becomes " + newName + " after the restart.");
        }

        public override void Apply(RunContext c)
        {
            string wanted = c.NewComputerName;
            string problem = ComputerName.Problem(wanted);
            if (problem != null) throw new InvalidOperationException(problem);

            string active = ComputerName.Active;
            if (ComputerName.Same(wanted, ComputerName.Pending()))
            {
                if (!ComputerName.Same(wanted, active)) c.RestartNeeded = true;   // renamed earlier, restart still waiting
                c.Log("This PC already has the name " + wanted + ".");
                return;
            }

            if (!ComputerName.Same(wanted, active))
            {
                // Remember the old name once, so Undo can go back to it.
                if (Reg.Read(c, RegRoot.Machine, LabLockKey, PreviousName) == null)
                {
                    Reg.Write(c, new RegValue(RegRoot.Machine, LabLockKey, PreviousName, active));
                }
            }
            Change(c, wanted);
        }

        public override void Undo(RunContext c)
        {
            string previous = Convert.ToString(Reg.Read(c, RegRoot.Machine, LabLockKey, PreviousName));
            if (previous.Length == 0)
            {
                if (c.Restoring)
                {
                    c.Log("LabLock has not renamed this PC, so its name stays as it is.");
                    return;
                }
                throw new InvalidOperationException("LabLock has not renamed this PC, so there is no earlier name to go back to.");
            }
            if (!ComputerName.Same(previous, ComputerName.Pending()))
            {
                Change(c, previous);
            }
            Reg.Delete(c, RegRoot.Machine, LabLockKey, PreviousName);
        }
    }

    internal sealed class RemotingSetting : Setting
    {
        private const string WinRmKey = @"SYSTEM\CurrentControlSet\Services\WinRM";
        private const string TokenPolicy = "LocalAccountTokenFilterPolicy";

        public RemotingSetting()
        {
            Id = Catalog.IdRemoting;
            Group = Catalog.GroupComputer;
            Title = "Allow remote management from the teacher PC (PowerShell remoting)";
            Hint = "Turns on PowerShell remoting so that later you can send commands to this PC with Invoke-Command, using the administrator account. Leave it off if you do not need that.";
            DefaultOn = false;
        }

        public override string State(RunContext c)
        {
            object start = Reg.Read(c, RegRoot.Machine, WinRmKey, "Start");
            object token = Reg.Read(c, RegRoot.Machine, Catalog.PolSystemMachine, TokenPolicy);
            bool serviceOn = start is int && (int)start == 2;
            bool tokenOn = token is int && (int)token == 1;
            if (serviceOn && tokenOn) return "Applied";
            return serviceOn || tokenOn ? "Partly applied" : "Not applied";
        }

        private static void RunPowerShell(string script, string what)
        {
            string arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"$ErrorActionPreference='Stop'; " + script + "\"";
            Proc.RunOrThrow(Proc.PowerShell, arguments, what);
        }

        public override void Apply(RunContext c)
        {
            RunPowerShell("Enable-PSRemoting -Force -SkipNetworkProfileCheck | Out-Null", "Could not turn on PowerShell remoting");
            Reg.Write(c, new RegValue(RegRoot.Machine, Catalog.PolSystemMachine, TokenPolicy, 1));
        }

        public override void Undo(RunContext c)
        {
            Reg.Delete(c, RegRoot.Machine, Catalog.PolSystemMachine, TokenPolicy);
            RunPowerShell("Disable-PSRemoting -Force -WarningAction SilentlyContinue | Out-Null; Stop-Service WinRM -Force; Set-Service WinRM -StartupType Manual",
                "Could not turn off PowerShell remoting");
        }
    }

    /// <summary>The checklist. To add a simple policy, add one more RegistrySetting line in Build().</summary>
    internal static class Catalog
    {
        public const string GroupSafety = "Safety net";
        public const string GroupAccounts = "Accounts";
        public const string GroupStudent = "Student account only";
        public const string GroupBrowsers = "Browsers (Chrome, Edge, Brave, Firefox) - all accounts";
        public const string GroupComputer = "Whole computer (all accounts)";

        public const string IdAdmin = "admin-account";
        public const string IdDemote = "student-standard";
        public const string IdWallpaper = "wallpaper";
        public const string IdDns = "dns-server";
        public const string IdBlocklist = "game-blocklist";
        public const string IdFixedIp = "fixed-ip";
        public const string IdRename = "rename-pc";
        public const string IdDnsLock = "dns-lock";
        public const string IdCleanup = "cleanup-at-signin";
        public const string IdAccountPicture = "account-picture";
        public const string IdLockScreen = "lock-screen";
        public const string IdRestorePoint = "restore-point";
        public const string IdRemoting = "remoting";
        public const string IdWol = "wake-on-lan";
        public const string IdNightShutdown = "night-shutdown";

        // Paths inside the student's own settings (HKEY_CURRENT_USER of the student).
        public const string PolExplorer = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        public const string PolSystem = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        public const string PolActiveDesktop = @"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop";
        public const string PolWindowsSystem = @"Software\Policies\Microsoft\Windows\System";
        public const string PolPersonalization = @"Software\Policies\Microsoft\Windows\Personalization";
        public const string PolWindowsExplorer = @"Software\Policies\Microsoft\Windows\Explorer";

        // Paths under HKEY_LOCAL_MACHINE.
        public const string PolSystemMachine = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

        private const string Chrome = @"SOFTWARE\Policies\Google\Chrome";
        private const string Edge = @"SOFTWARE\Policies\Microsoft\Edge";
        private const string Brave = @"SOFTWARE\Policies\BraveSoftware\Brave";
        private const string Firefox = @"SOFTWARE\Policies\Mozilla\Firefox";

        /// <summary>The same policy for the three Chromium browsers.</summary>
        private static void Chromium(List<RegValue> values, string name, int value)
        {
            values.Add(new RegValue(RegRoot.Machine, Chrome, name, value));
            values.Add(new RegValue(RegRoot.Machine, Edge, name, value));
            values.Add(new RegValue(RegRoot.Machine, Brave, name, value));
        }

        /// <summary>Settings that matter when many students share one account. They are machine policies, so they count for every account.</summary>
        private static void AddBrowserSettings(List<Setting> list)
        {
            List<RegValue> values = new List<RegValue>();
            values.Add(new RegValue(RegRoot.Machine, Chrome, "DnsOverHttpsMode", "off"));
            values.Add(new RegValue(RegRoot.Machine, Edge, "DnsOverHttpsMode", "off"));
            values.Add(new RegValue(RegRoot.Machine, Brave, "DnsOverHttpsMode", "off"));
            values.Add(new RegValue(RegRoot.Machine, Firefox + @"\DNSOverHTTPS", "Enabled", 0));
            values.Add(new RegValue(RegRoot.Machine, Firefox + @"\DNSOverHTTPS", "Locked", 1));
            list.Add(new RegistrySetting("no-doh", GroupBrowsers,
                "Browsers must use the PC's DNS (so they cannot bypass Pi-hole)",
                "Turns off DNS-over-HTTPS. Without this a browser can ask another DNS server directly and skip the Pi-hole and the local blocklist.",
                true, values.ToArray()));

            values = new List<RegValue>();
            Chromium(values, "ForceEphemeralProfiles", 1);
            values.Add(new RegValue(RegRoot.Machine, Edge, "ClearBrowsingDataOnExit", 1));
            values.Add(new RegValue(RegRoot.Machine, Firefox + @"\SanitizeOnShutdown", "Cache", 1));
            values.Add(new RegValue(RegRoot.Machine, Firefox + @"\SanitizeOnShutdown", "Cookies", 1));
            values.Add(new RegValue(RegRoot.Machine, Firefox + @"\SanitizeOnShutdown", "History", 1));
            values.Add(new RegValue(RegRoot.Machine, Firefox + @"\SanitizeOnShutdown", "Sessions", 1));
            values.Add(new RegValue(RegRoot.Machine, Firefox + @"\SanitizeOnShutdown", "FormData", 1));
            values.Add(new RegValue(RegRoot.Machine, Firefox + @"\SanitizeOnShutdown", "Locked", 1));
            list.Add(new RegistrySetting("browser-forget", GroupBrowsers,
                "Browsers forget everything when they are closed",
                "History, cookies and sign-ins are thrown away when the browser closes, so a student never finds the previous student's e-mail or social accounts open. Bookmarks made by students are lost too.",
                true, values.ToArray()));

            values = new List<RegValue>();
            Chromium(values, "PasswordManagerEnabled", 0);
            Chromium(values, "AutofillAddressEnabled", 0);
            Chromium(values, "AutofillCreditCardEnabled", 0);
            values.Add(new RegValue(RegRoot.Machine, Firefox, "PasswordManagerEnabled", 0));
            values.Add(new RegValue(RegRoot.Machine, Firefox, "OfferToSaveLogins", 0));
            values.Add(new RegValue(RegRoot.Machine, Firefox, "DisableFormHistory", 1));
            list.Add(new RegistrySetting("browser-no-passwords", GroupBrowsers,
                "Browsers do not save passwords or form data",
                "The browser never offers to remember a password, an address or a card. On a shared account a saved password belongs to whoever sits down next.",
                true, values.ToArray()));

            values = new List<RegValue>();
            values.Add(new RegValue(RegRoot.Machine, Chrome, "IncognitoModeAvailability", 1));
            values.Add(new RegValue(RegRoot.Machine, Brave, "IncognitoModeAvailability", 1));
            values.Add(new RegValue(RegRoot.Machine, Edge, "InPrivateModeAvailability", 1));
            Chromium(values, "BrowserGuestModeEnabled", 0);
            values.Add(new RegValue(RegRoot.Machine, Chrome, "BrowserAddPersonEnabled", 0));
            values.Add(new RegValue(RegRoot.Machine, Brave, "BrowserAddPersonEnabled", 0));
            values.Add(new RegValue(RegRoot.Machine, Edge, "BrowserAddProfileEnabled", 0));
            Chromium(values, "BrowserSignin", 0);
            Chromium(values, "SyncDisabled", 1);
            values.Add(new RegValue(RegRoot.Machine, Firefox, "DisablePrivateBrowsing", 1));
            values.Add(new RegValue(RegRoot.Machine, Firefox, "DisableFirefoxAccounts", 1));
            list.Add(new RegistrySetting("browser-no-private", GroupBrowsers,
                "No private windows, guest mode, extra profiles or browser sign-in",
                "Students cannot open a private or guest window, add a browser profile or sign the browser itself in to an account. Signing in to web sites (e-mail, e-class) still works.",
                true, values.ToArray()));

            values = new List<RegValue>();
            values.Add(new RegValue(RegRoot.Machine, Chrome + @"\ExtensionInstallBlocklist", "1", "*"));
            values.Add(new RegValue(RegRoot.Machine, Edge + @"\ExtensionInstallBlocklist", "1", "*"));
            values.Add(new RegValue(RegRoot.Machine, Brave + @"\ExtensionInstallBlocklist", "1", "*"));
            values.Add(new RegValue(RegRoot.Machine, Firefox, "ExtensionSettings", "{\"*\": {\"installation_mode\": \"blocked\"}}"));
            list.Add(new RegistrySetting("browser-no-extensions", GroupBrowsers,
                "No browser extensions",
                "Extensions cannot be installed and the ones already there are switched off. This also stops VPN and proxy extensions, the usual way around a filter. Leave it off if the lab depends on an extension.",
                true, values.ToArray()));

            values = new List<RegValue>();
            Chromium(values, "ForceGoogleSafeSearch", 1);
            Chromium(values, "ForceYouTubeRestrict", 1);
            values.Add(new RegValue(RegRoot.Machine, Edge, "ForceBingSafeSearch", 2));
            list.Add(new RegistrySetting("browser-safe-search", GroupBrowsers,
                "Safe search on Google and Bing, restricted mode on YouTube",
                "Forces SafeSearch on Google and Bing and the moderate restricted mode on YouTube in Chrome, Edge and Brave. Firefox has no such policy.",
                true, values.ToArray()));
        }

        public static List<Setting> Build()
        {
            List<Setting> list = new List<Setting>();

            // ---- Safety net (keep first: it runs before everything else)
            list.Add(new RestorePointSetting());

            // ---- Accounts
            list.Add(new AdminAccountSetting());
            list.Add(new DemoteStudentSetting());
            list.Add(new PasswordChangeSetting());
            list.Add(new LockoutSetting());

            // ---- Student account only
            list.Add(new WallpaperSetting());
            list.Add(new RegistrySetting("no-control-panel", GroupStudent,
                "Block Settings and Control Panel completely",
                "The student cannot open the Settings app, any Settings page or Control Panel at all. They close again at once.",
                true,
                new RegValue(RegRoot.Student, PolExplorer, "NoControlPanel", 1)));
            list.Add(new RegistrySetting("lock-look", GroupStudent,
                "Lock the look of Windows (theme, colours, sounds, pointers, screen saver)",
                "A second lock behind the blocked Settings: themes and .theme files, window colours, sounds, mouse pointers, desktop icons, screen saver and display options cannot be changed.",
                true,
                new RegValue(RegRoot.Student, PolExplorer, "NoThemesTab", 1),
                new RegValue(RegRoot.Student, PolSystem, "NoDispCPL", 1),
                new RegValue(RegRoot.Student, PolSystem, "NoDispAppearancePage", 1),
                new RegValue(RegRoot.Student, PolSystem, "NoColorChoice", 1),
                new RegValue(RegRoot.Student, PolSystem, "NoVisualStyleChoice", 1),
                new RegValue(RegRoot.Student, PolSystem, "NoSizeChoice", 1),
                new RegValue(RegRoot.Student, PolSystem, "NoDispBackgroundPage", 1),
                new RegValue(RegRoot.Student, PolSystem, "NoDispScrSavPage", 1),
                new RegValue(RegRoot.Student, PolPersonalization, "NoChangingSoundScheme", 1),
                new RegValue(RegRoot.Student, PolPersonalization, "NoChangingMousePointers", 1)));
            list.Add(new RegistrySetting("lock-taskbar", GroupStudent,
                "Lock the taskbar and the Start menu",
                "The taskbar cannot be moved, resized or reconfigured, programs cannot be pinned or unpinned, and the Start menu layout cannot be changed.",
                true,
                new RegValue(RegRoot.Student, PolExplorer, "LockTaskbar", 1),
                new RegValue(RegRoot.Student, PolExplorer, "TaskbarLockAll", 1),
                new RegValue(RegRoot.Student, PolExplorer, "TaskbarNoResize", 1),
                new RegValue(RegRoot.Student, PolExplorer, "NoSetTaskbar", 1),
                new RegValue(RegRoot.Student, PolExplorer, "NoChangeStartMenu", 1),
                new RegValue(RegRoot.Student, PolWindowsExplorer, "NoPinningToTaskbar", 1)));
            list.Add(new HighContrastSetting());
            list.Add(new NoRunSetting());
            list.Add(new CleanupSetting());
            list.Add(new RegistrySetting("no-regedit", GroupStudent,
                "Block Registry Editor",
                "The student cannot run regedit or import .reg files.",
                true,
                new RegValue(RegRoot.Student, PolSystem, "DisableRegistryTools", 2)));
            list.Add(new RegistrySetting("no-taskmgr", GroupStudent,
                "Block Task Manager",
                "The student cannot open Task Manager. Leave it off if students need it to close a frozen program.",
                false,
                new RegValue(RegRoot.Student, PolSystem, "DisableTaskMgr", 1)));
            list.Add(new RegistrySetting("no-cmd", GroupStudent,
                "Block Command Prompt",
                "The student cannot open cmd.exe (batch files still run). Leave it off in a lab where students learn the command line.",
                false,
                new RegValue(RegRoot.Student, PolWindowsSystem, "DisableCMD", 2)));

            // ---- Browsers
            AddBrowserSettings(list);

            // ---- Whole computer
            list.Add(new BlocklistSetting());
            list.Add(new AccountPictureSetting());
            list.Add(new LockScreenSetting());
            list.Add(new RegistrySetting("no-usb-run", GroupComputer,
                "Block running programs from USB sticks (all accounts)",
                "Programs on a USB stick or USB disk do not start; files on it still open. It counts for every account, so afterwards copy LabLock.exe to the PC's disk to run it. Takes effect when the stick is plugged in again.",
                false,
                new RegValue(RegRoot.Machine, @"SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices\{53f5630d-b6bf-11d0-94f2-00a0c91efb8b}", "Deny_Execute", 1)));
            list.Add(new FixedIpSetting());
            list.Add(new DnsSetting());
            list.Add(new DnsLockSetting());
            list.Add(new RegistrySetting("no-store", GroupComputer,
                "Turn off Microsoft Store (Education / Enterprise editions only)",
                "Windows only obeys this policy on Education and Enterprise. On Home and Pro it is written but has no effect.",
                false,
                new RegValue(RegRoot.Machine, @"SOFTWARE\Policies\Microsoft\WindowsStore", "RemoveWindowsStore", 1)));
            list.Add(new WolSetting());
            list.Add(new NightShutdownSetting());
            list.Add(new RemotingSetting());

            // Keep this one last: the new name needs a restart, which also makes every other change take hold.
            list.Add(new RenameSetting());

            return list;
        }
    }

    /// <summary>Runs a batch of settings and reports their state. Opens the student's settings once per batch.</summary>
    internal static class Engine
    {
        public static StudentHive OpenHive(RunContext c)
        {
            c.HiveRoot = null;
            c.HiveProblem = null;
            LocalUser student = c.Student;
            if (student == null)
            {
                c.HiveProblem = "No student account selected.";
                return null;
            }

            string problem;
            StudentHive hive = StudentHive.TryOpen(student.Sid, out problem);
            c.HiveProblem = problem;
            if (hive != null) c.HiveRoot = hive.Root;
            return hive;
        }

        public static void CloseHive(StudentHive hive, RunContext c)
        {
            c.HiveRoot = null;
            if (hive == null) return;
            hive.Dispose();
            if (hive.UnloadFailed && c.Log != null)
            {
                c.Log("WARNING: the student's settings file could not be closed. Restart this PC before the student signs in.");
            }
        }

        public static Dictionary<string, string> States(List<Setting> settings, RunContext c)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            StudentHive hive = OpenHive(c);
            try
            {
                foreach (Setting setting in settings)
                {
                    string state;
                    try
                    {
                        if (setting.NeedsStudent && c.Student == null)
                        {
                            state = "Choose the student account";
                        }
                        else if (setting.NeedsHive && c.HiveRoot == null)
                        {
                            state = "Cannot check: " + c.HiveProblem;
                        }
                        else
                        {
                            state = setting.State(c);
                        }
                    }
                    catch (Exception ex)
                    {
                        state = "Cannot check: " + ex.Message;
                    }
                    result[setting.Id] = state;
                }
            }
            finally
            {
                CloseHive(hive, c);
            }
            return result;
        }

        /// <summary>Writes the setting into this PC's record of changes, or crosses it off. A failure here never stops the run.</summary>
        private static void NoteInJournal(RunContext c, Setting setting, bool apply)
        {
            try
            {
                if (apply) Journal.Record(c, setting); else Journal.Forget(c, setting);
            }
            catch (Exception ex)
            {
                c.Log("WARNING: the record of changes on this PC could not be updated: " + ex.Message);
            }
        }

        /// <summary>Applies or undoes the given settings in order. Returns how many worked.</summary>
        public static int Run(List<Setting> settings, RunContext c, bool apply, out int failed)
        {
            int done = 0;
            failed = 0;
            StudentHive hive = OpenHive(c);
            try
            {
                foreach (Setting setting in settings)
                {
                    try
                    {
                        if (setting.NeedsStudent && c.Student == null)
                        {
                            throw new InvalidOperationException("No student account selected.");
                        }
                        if (setting.NeedsHive && c.HiveRoot == null)
                        {
                            throw new InvalidOperationException(c.HiveProblem);
                        }

                        if (apply) setting.Apply(c); else setting.Undo(c);
                        c.Log((apply ? "OK      " : "UNDONE  ") + setting.Title);
                        done++;
                        NoteInJournal(c, setting, apply);
                    }
                    catch (Exception ex)
                    {
                        c.Log("FAILED  " + setting.Title + "  ->  " + ex.Message);
                        failed++;
                    }
                }
            }
            finally
            {
                CloseHive(hive, c);
                if (!apply)
                {
                    try
                    {
                        Journal.ClearStudentWhenEmpty(c);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return done;
        }
    }
}
