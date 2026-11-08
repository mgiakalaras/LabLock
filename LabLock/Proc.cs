using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LabLock
{
    /// <summary>Runs the Windows command line tools (reg, net, netsh, powershell) and captures their output.</summary>
    internal static class Proc
    {
        [DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();

        public static string SystemTool(string exeName)
        {
            return Path.Combine(Environment.SystemDirectory, exeName);
        }

        public static string PowerShell
        {
            get { return Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"); }
        }

        private static Encoding OemEncoding()
        {
            try
            {
                return Encoding.GetEncoding((int)GetOEMCP());
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Runs a program hidden, waits for it and returns its exit code.</summary>
        public static int Run(string exePath, string arguments, out string output)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exePath, arguments);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            Encoding oem = OemEncoding();
            if (oem != null)
            {
                psi.StandardOutputEncoding = oem;
                psi.StandardErrorEncoding = oem;
            }

            StringBuilder errors = new StringBuilder();
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (errors)
                        {
                            errors.AppendLine(e.Data);
                        }
                    }
                };

                p.Start();
                p.BeginErrorReadLine();
                string stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                string all;
                lock (errors)
                {
                    all = stdout + Environment.NewLine + errors.ToString();
                }
                output = all.Trim();
                return p.ExitCode;
            }
        }

        /// <summary>Runs a program and throws when it reports a failure.</summary>
        public static void RunOrThrow(string exePath, string arguments, string what)
        {
            string output;
            int code = Run(exePath, arguments, out output);
            if (code != 0)
            {
                string detail = output.Length > 0 ? output : "exit code " + code;
                throw new InvalidOperationException(what + ": " + detail);
            }
        }
    }
}
