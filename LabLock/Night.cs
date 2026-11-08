using System;
using System.Globalization;

namespace LabLock
{
    /// <summary>
    /// Shuts the PC down every night if it was left on. A scheduled task that runs as the system starts
    /// "shutdown" five minutes before the chosen time, so whoever is still working sees a warning first.
    /// The time comes from LabLock.ini (shutdown=00:00).
    /// </summary>
    internal sealed class NightShutdownSetting : Setting
    {
        private const string TaskName = "LabLock night shutdown";
        private const string LabLockKey = @"SOFTWARE\LabLock";
        private const string TimeValue = "NightShutdownTime";
        private const int WarningMinutes = 5;
        public const string DefaultTime = "00:00";

        public NightShutdownSetting()
        {
            Id = Catalog.IdNightShutdown;
            Group = Catalog.GroupComputer;
            Title = "Shut the PC down at night if it is left on (00:00, with a warning)";
            Hint = "Every night the PC shows a 5-minute warning and then shuts down, so no PC stays on until the morning. A normal shut-down also keeps Wake-on-LAN working. The time can be changed in LabLock.ini (shutdown=00:00).";
            DefaultOn = true;
        }

        private static string Schtasks
        {
            get { return Proc.SystemTool("schtasks.exe"); }
        }

        /// <summary>True when the text is a time of day such as 21:00 or 9:30.</summary>
        public static bool IsTime(string typed)
        {
            DateTime parsed;
            return !string.IsNullOrEmpty(typed) &&
                DateTime.TryParseExact(typed.Trim(), new string[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
        }

        /// <summary>The typed time as HH:mm, or the default when it is not a valid time.</summary>
        public static string CleanTime(string typed)
        {
            if (!IsTime(typed)) return DefaultTime;
            return DateTime.ParseExact(typed.Trim(), new string[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None)
                .ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>When the task starts: the shut-down time minus the warning.</summary>
        private static string StartTime(string time)
        {
            DateTime at = DateTime.ParseExact(CleanTime(time), "HH:mm", CultureInfo.InvariantCulture).AddMinutes(-WarningMinutes);
            return at.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        public override string State(RunContext c)
        {
            string output;
            if (Proc.Run(Schtasks, "/query /tn \"" + TaskName + "\"", out output) != 0) return "Not applied";
            string time = Convert.ToString(Reg.Read(c, RegRoot.Machine, LabLockKey, TimeValue));
            return time.Length > 0 ? "Applied (every day at " + time + ")" : "Applied";
        }

        public override void Apply(RunContext c)
        {
            string time = CleanTime(c.ShutdownTime);
            string shutdown = Proc.SystemTool("shutdown.exe");
            string message = L.Greek
                ? "Το PC θα κλείσει σε " + WarningMinutes + " λεπτά. Αποθήκευσε τη δουλειά σου. (Ακύρωση: Win+R, shutdown /a)"
                : "This PC shuts down in " + WarningMinutes + " minutes. Save your work. (To cancel: Win+R, shutdown /a)";
            string command = "\\\"" + shutdown + "\\\" /s /f /t " + (WarningMinutes * 60) + " /c \\\"" + message + "\\\"";

            Proc.RunOrThrow(Schtasks,
                "/create /f /tn \"" + TaskName + "\" /sc daily /st " + StartTime(time) + " /ru SYSTEM /rl highest /tr \"" + command + "\"",
                "Could not create the night shut-down task");
            Reg.Write(c, new RegValue(RegRoot.Machine, LabLockKey, TimeValue, time));
            c.Log("This PC shuts down every night at " + time + " if it is still on (warning at " + StartTime(time) + ").");
        }

        public override void Undo(RunContext c)
        {
            string output;
            Proc.Run(Schtasks, "/delete /f /tn \"" + TaskName + "\"", out output);
            Reg.Delete(c, RegRoot.Machine, LabLockKey, TimeValue);
        }
    }
}
