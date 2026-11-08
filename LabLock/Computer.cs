using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace LabLock
{
    /// <summary>Reads and changes the name of this PC. A new name only becomes active after a restart.</summary>
    internal static class ComputerName
    {
        private const int PhysicalDnsHostname = 5;   // COMPUTER_NAME_FORMAT.ComputerNamePhysicalDnsHostname
        private const string PendingKey = @"SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName";

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetComputerNameEx(int nameType, string name);

        /// <summary>The name this PC uses right now.</summary>
        public static string Active
        {
            get { return Environment.MachineName; }
        }

        /// <summary>The name this PC will have after the next restart (the same as Active when no rename is waiting).</summary>
        public static string Pending()
        {
            try
            {
                using (RegistryKey machine = Reg.Machine())
                using (RegistryKey key = machine.OpenSubKey(PendingKey, false))
                {
                    string name = key == null ? "" : Convert.ToString(key.GetValue("ComputerName")).Trim();
                    return name.Length > 0 ? name : Active;
                }
            }
            catch (Exception)
            {
                return Active;
            }
        }

        public static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Returns what is wrong with a computer name, or null when it can be used.</summary>
        public static string Problem(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Type a computer name.";
            if (name.Length > 15) return "A computer name can be at most 15 characters long.";

            bool onlyDigits = true;
            foreach (char ch in name)
            {
                bool letter = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z');
                bool digit = ch >= '0' && ch <= '9';
                if (!letter && !digit && ch != '-')
                {
                    return "A computer name may only contain the letters A-Z, digits and the hyphen (-), without spaces.";
                }
                if (!digit) onlyDigits = false;
            }
            if (onlyDigits) return "A computer name cannot consist of digits only.";
            if (name[0] == '-' || name[name.Length - 1] == '-') return "A computer name cannot start or end with a hyphen.";
            return null;
        }

        public static bool InDomain()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT PartOfDomain FROM Win32_ComputerSystem"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementBaseObject item in results)
                    {
                        object value = item["PartOfDomain"];
                        if (value != null && Convert.ToBoolean(value)) return true;
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        /// <summary>Sets the new name. Windows starts using it at the next restart.</summary>
        public static void Rename(string newName)
        {
            string problem = Problem(newName);
            if (problem != null)
            {
                throw new InvalidOperationException(problem);
            }
            if (InDomain())
            {
                throw new InvalidOperationException("This PC is joined to a domain. Rename it from the domain, not with LabLock.");
            }
            if (!SetComputerNameEx(PhysicalDnsHostname, newName))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("Windows refused the new computer name: " + new Win32Exception(error).Message);
            }
        }
    }

    /// <summary>What the PC is made of. Read once from Windows (WMI); nothing has to be typed.</summary>
    internal sealed class HardwareInfo
    {
        public string Manufacturer = "";
        public string Model = "";
        public string Serial = "";
        public string Cpu = "";
        public string Memory = "";
        public string Disk = "";
    }

    internal static class Hardware
    {
        private static HardwareInfo cached;

        private static string First(string query, string property)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementBaseObject item in results)
                    {
                        string value = Convert.ToString(item[property]).Trim();
                        if (value.Length > 0) return value;
                    }
                }
            }
            catch (Exception)
            {
            }
            return "";
        }

        private static string Gigabytes(string bytes)
        {
            double number;
            if (!double.TryParse(bytes, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || number <= 0) return "";
            return Math.Round(number / 1073741824.0).ToString(CultureInfo.InvariantCulture) + " GB";
        }

        private static string FirstDisk()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Model, Size, InterfaceType FROM Win32_DiskDrive"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementBaseObject item in results)
                    {
                        if (string.Equals(Convert.ToString(item["InterfaceType"]), "USB", StringComparison.OrdinalIgnoreCase)) continue;
                        string model = Convert.ToString(item["Model"]).Trim();
                        string size = Gigabytes(Convert.ToString(item["Size"]));
                        if (model.Length == 0 && size.Length == 0) continue;
                        return size.Length > 0 ? model + " (" + size + ")" : model;
                    }
                }
            }
            catch (Exception)
            {
            }
            return "";
        }

        public static HardwareInfo Read()
        {
            if (cached != null) return cached;

            HardwareInfo info = new HardwareInfo();
            info.Manufacturer = First("SELECT Manufacturer FROM Win32_ComputerSystem", "Manufacturer");
            info.Model = First("SELECT Model FROM Win32_ComputerSystem", "Model");
            info.Serial = First("SELECT SerialNumber FROM Win32_BIOS", "SerialNumber");
            info.Cpu = First("SELECT Name FROM Win32_Processor", "Name");
            info.Memory = Gigabytes(First("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem", "TotalPhysicalMemory"));
            info.Disk = FirstDisk();

            // Self-built PCs often report placeholders instead of a serial number.
            string serial = info.Serial.ToLowerInvariant();
            if (serial.Contains("o.e.m") || serial.Contains("default") || serial.Contains("system serial")) info.Serial = "";

            cached = info;
            return info;
        }
    }

    /// <summary>
    /// The record of the lab's PCs: LabLock-computers.csv next to the program, one line per PC with its name,
    /// network settings and hardware. A PC is recognised by the MAC address of its network adapter, so its line
    /// is updated (not doubled) when the PC is renamed or gets another IP address. Opens in Excel.
    /// </summary>
    internal static class PcRecord
    {
        public const string FileName = "LabLock-computers.csv";
        public const string VeyonFileName = "LabLock-veyon.csv";
        public const string NetHawkFileName = "LabLock-nethawk-machines.json";

        // New columns are only ever added at the end, so older files stay readable.
        private static readonly string[] Header =
        {
            "Computer name", "IP address", "Mode", "Subnet mask", "Gateway", "DNS", "MAC address", "Windows", "Updated",
            "Room", "Manufacturer", "Model", "Serial number", "Processor", "Memory", "Disk", "Notes"
        };
        public const int ColumnName = 0;
        public const int ColumnIp = 1;
        public const int ColumnMode = 2;
        public const int ColumnMask = 3;
        public const int ColumnMac = 6;
        public const int ColumnWindows = 7;
        public const int ColumnRoom = 9;
        public const int ColumnManufacturer = 10;
        public const int ColumnModel = 11;
        public const int ColumnSerial = 12;
        public const int ColumnCpu = 13;
        public const int ColumnMemory = 14;
        public const int ColumnDisk = 15;
        public const int ColumnNotes = 16;

        public const string ModeFixed = "Fixed";

        public static string FilePath
        {
            get { return Path.Combine(AppConfig.BaseDir, FileName); }
        }

        /// <summary>Excel in Greek and most European settings wants ';' between columns, English settings want ','.</summary>
        private static char Separator(string headerLine)
        {
            if (headerLine != null)
            {
                if (headerLine.IndexOf(';') >= 0) return ';';
                if (headerLine.IndexOf(',') >= 0) return ',';
            }
            return CultureInfo.CurrentCulture.TextInfo.ListSeparator == "," ? ',' : ';';
        }

        private static string[] Split(string line, char separator)
        {
            List<string> fields = new List<string>();
            StringBuilder current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else if (ch == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        current.Append(ch);
                    }
                }
                else if (ch == '"')
                {
                    quoted = true;
                }
                else if (ch == separator)
                {
                    fields.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(ch);
                }
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }

        private static string Join(string[] fields, char separator)
        {
            StringBuilder line = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) line.Append(separator);
                string field = (fields[i] ?? "").Replace("\r", " ").Replace("\n", " ");
                if (field.IndexOf(separator) >= 0 || field.IndexOf('"') >= 0)
                {
                    field = "\"" + field.Replace("\"", "\"\"") + "\"";
                }
                line.Append(field);
            }
            return line.ToString();
        }

        private static List<string[]> ReadRows(out char separator)
        {
            List<string[]> rows = new List<string[]>();
            separator = Separator(null);
            if (!File.Exists(FilePath)) return rows;

            string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
            if (lines.Length == 0) return rows;
            separator = Separator(lines[0]);

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length == 0) continue;
                string[] fields = Split(lines[i], separator);
                string[] row = new string[Header.Length];
                for (int column = 0; column < row.Length; column++)
                {
                    row[column] = column < fields.Length ? fields[column].Trim() : "";
                }
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>All PCs in the record; an empty list when there is no record or it cannot be read.</summary>
        public static List<string[]> Rows()
        {
            try
            {
                char separator;
                return ReadRows(out separator);
            }
            catch (Exception)
            {
                return new List<string[]>();
            }
        }

        public static int Count()
        {
            return Rows().Count;
        }

        private static int IndexOf(List<string[]> rows, string mac, string name)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                // The same PC is the one with the same MAC address; without a MAC, the one with the same name.
                bool sameMac = mac.Length > 0 && ComputerName.Same(rows[i][ColumnMac], mac);
                bool sameName = mac.Length == 0 && rows[i][ColumnMac].Length == 0 && ComputerName.Same(rows[i][ColumnName], name);
                if (sameMac || sameName) return i;
            }
            return -1;
        }

        /// <summary>The line of this PC, or null when it is not in the record yet.</summary>
        public static string[] Find(string mac, string name)
        {
            List<string[]> rows = Rows();
            int index = IndexOf(rows, mac ?? "", name ?? "");
            return index < 0 ? null : rows[index];
        }

        /// <summary>
        /// Adds this PC to the record or updates its line. Room and notes that are null keep what the line already had.
        /// Returns null, or the reason the record could not be saved.
        /// </summary>
        public static string Save(string computerName, NetInfo net, string windows, HardwareInfo hardware, string room, string notes)
        {
            try
            {
                char separator;
                List<string[]> rows = ReadRows(out separator);

                string mac = net == null ? "" : net.Mac;
                int found = IndexOf(rows, mac, computerName);

                string[] mine = new string[Header.Length];
                for (int i = 0; i < mine.Length; i++) mine[i] = found >= 0 ? rows[found][i] : "";

                mine[ColumnName] = computerName;
                mine[ColumnIp] = net == null ? "" : net.Address;
                mine[ColumnMode] = net == null ? "No network" : (net.Dhcp ? "Automatic (DHCP)" : ModeFixed);
                mine[3] = net == null ? "" : net.Mask;
                mine[4] = net == null ? "" : net.Gateway;
                mine[5] = net == null ? "" : string.Join(" ", net.Dns.ToArray());
                mine[ColumnMac] = mac;
                mine[ColumnWindows] = windows;
                mine[8] = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                if (room != null) mine[ColumnRoom] = room;
                if (hardware != null)
                {
                    mine[ColumnManufacturer] = hardware.Manufacturer;
                    mine[ColumnModel] = hardware.Model;
                    mine[ColumnSerial] = hardware.Serial;
                    mine[ColumnCpu] = hardware.Cpu;
                    mine[ColumnMemory] = hardware.Memory;
                    mine[ColumnDisk] = hardware.Disk;
                }
                if (notes != null) mine[ColumnNotes] = notes;

                if (found >= 0) rows[found] = mine; else rows.Add(mine);

                rows.Sort(delegate(string[] a, string[] b)
                {
                    return string.Compare(a[ColumnName], b[ColumnName], StringComparison.OrdinalIgnoreCase);
                });

                List<string> lines = new List<string>();
                lines.Add(Join(Header, separator));
                foreach (string[] row in rows)
                {
                    lines.Add(Join(row, separator));
                }
                File.WriteAllLines(FilePath, lines.ToArray(), new UTF8Encoding(true));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ------------------------------------------------------------------ exports

        /// <summary>
        /// Writes room;name;address;MAC for every PC, for Veyon's own importer:
        ///   veyon-cli networkobjects import LabLock-veyon.csv format "%location%;%name%;%host%;%mac%"
        /// Returns the path of the file.
        /// </summary>
        public static string ExportForVeyon(string defaultRoom)
        {
            List<string> lines = new List<string>();
            foreach (string[] row in Rows())
            {
                if (row[ColumnName].Length == 0) continue;
                string room = row[ColumnRoom].Length > 0 ? row[ColumnRoom] : defaultRoom;
                if (string.IsNullOrEmpty(room)) room = "Lab";
                string host = row[ColumnIp].Length > 0 ? row[ColumnIp] : row[ColumnName];
                lines.Add(room.Replace(";", " ") + ";" + row[ColumnName] + ";" + host + ";" + row[ColumnMac].Replace('-', ':'));
            }
            string path = Path.Combine(AppConfig.BaseDir, VeyonFileName);
            File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            return path;
        }

        private static string Json(string text)
        {
            StringBuilder result = new StringBuilder("\"");
            foreach (char ch in text ?? "")
            {
                if (ch == '"' || ch == '\\') result.Append('\\').Append(ch);
                else if (ch < ' ') result.Append(' ');
                else result.Append(ch);
            }
            return result.Append('"').ToString();
        }

        /// <summary>The same PC always gets the same id, so a second export updates instead of doubling.</summary>
        private static string StableId(string key)
        {
            try
            {
                using (MD5 md5 = MD5.Create())
                {
                    return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes("LabLock:" + key.ToUpperInvariant()))).ToString();
                }
            }
            catch (Exception)
            {
                return Guid.NewGuid().ToString();   // some locked-down PCs do not allow MD5
            }
        }

        /// <summary>Writes the PCs in the format of NetHawk's machine inventory (machines.json). Returns the path of the file.</summary>
        public static string ExportForNetHawk(string defaultRoom)
        {
            string now = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);
            StringBuilder json = new StringBuilder();
            json.AppendLine("[");
            List<string[]> rows = Rows();
            for (int i = 0; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string key = row[ColumnMac].Length > 0 ? row[ColumnMac] : row[ColumnName];
                string room = row[ColumnRoom].Length > 0 ? row[ColumnRoom] : (defaultRoom ?? "");

                json.AppendLine("  {");
                json.AppendLine("    \"Id\": " + Json(StableId(key)) + ",");
                json.AppendLine("    \"Name\": " + Json(row[ColumnName]) + ",");
                json.AppendLine("    \"DeviceType\": \"DesktopComputer\",");
                json.AppendLine("    \"IpAddress\": " + Json(row[ColumnIp]) + ",");
                json.AppendLine("    \"IpMode\": " + Json(row[ColumnMode] == ModeFixed ? "Static" : "Dynamic") + ",");
                json.AppendLine("    \"Hostname\": " + Json(row[ColumnName]) + ",");
                json.AppendLine("    \"MacAddress\": " + Json(row[ColumnMac]) + ",");
                json.AppendLine("    \"Username\": \"\",");
                json.AppendLine("    \"OperatingSystem\": " + Json(row[ColumnWindows]) + ",");
                json.AppendLine("    \"Manufacturer\": " + Json(row[ColumnManufacturer]) + ",");
                json.AppendLine("    \"Model\": " + Json(row[ColumnModel]) + ",");
                json.AppendLine("    \"Firmware\": \"\",");
                json.AppendLine("    \"SerialNumber\": " + Json(row[ColumnSerial]) + ",");
                json.AppendLine("    \"ManagementUrl\": \"\",");
                json.AppendLine("    \"Cpu\": " + Json(row[ColumnCpu]) + ",");
                json.AppendLine("    \"Memory\": " + Json(row[ColumnMemory]) + ",");
                json.AppendLine("    \"Storage\": " + Json(row[ColumnDisk]) + ",");
                json.AppendLine("    \"Location\": " + Json(room) + ",");
                json.AppendLine("    \"Notes\": " + Json(row[ColumnNotes]) + ",");
                json.AppendLine("    \"CreatedAt\": " + Json(now) + ",");
                json.AppendLine("    \"UpdatedAt\": " + Json(now));
                json.AppendLine(i + 1 < rows.Count ? "  }," : "  }");
            }
            json.AppendLine("]");

            string path = Path.Combine(AppConfig.BaseDir, NetHawkFileName);
            File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
            return path;
        }
    }
}
