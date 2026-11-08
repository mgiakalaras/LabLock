using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Win32;

namespace LabLock
{
    /// <summary>
    /// Sends the Wake-on-LAN "magic packet" (6 x FF, then the MAC address 16 times) to the PCs in the record.
    /// It goes to the general broadcast and to the broadcast of each PC's own network, three times.
    /// </summary>
    internal static class WakeOnLan
    {
        private const int Port = 9;

        private static byte[] Packet(string mac)
        {
            string hex = mac.Replace("-", "").Replace(":", "").Replace(".", "").Trim();
            if (hex.Length != 12) return null;
            byte[] address = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out address[i])) return null;
            }
            byte[] packet = new byte[6 + 16 * 6];
            for (int i = 0; i < 6; i++) packet[i] = 0xFF;
            for (int i = 0; i < 16; i++) Array.Copy(address, 0, packet, 6 + i * 6, 6);
            return packet;
        }

        /// <summary>The broadcast address of the PC's network, for example 192.168.2.255, or null.</summary>
        private static IPAddress DirectedBroadcast(string ip, string mask)
        {
            if (!NetConfig.IsIPv4(ip)) return null;
            if (!NetConfig.IsMask(mask)) mask = "255.255.255.0";
            uint value = NetConfig.ToNumber(ip) | ~NetConfig.ToNumber(mask);
            return new IPAddress(new byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
        }

        /// <summary>Wakes one PC. Returns false when the MAC address is not usable.</summary>
        public static bool Wake(string mac, string ip, string mask)
        {
            byte[] packet = Packet(mac);
            if (packet == null) return false;

            List<IPEndPoint> targets = new List<IPEndPoint>();
            targets.Add(new IPEndPoint(IPAddress.Broadcast, Port));
            IPAddress directed = DirectedBroadcast(ip, mask);
            if (directed != null) targets.Add(new IPEndPoint(directed, Port));

            using (UdpClient client = new UdpClient())
            {
                client.EnableBroadcast = true;
                for (int round = 0; round < 3; round++)
                {
                    foreach (IPEndPoint target in targets)
                    {
                        try
                        {
                            client.Send(packet, packet.Length, target);
                        }
                        catch (SocketException)
                        {
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>Wakes every PC of the record that has a MAC address, except this one. Returns how many were sent.</summary>
        public static int WakeAll(Action<string> log, string defaultMask)
        {
            string ownMac = "";
            try
            {
                NetInfo net = NetConfig.Current();
                if (net != null) ownMac = net.Mac;
            }
            catch (Exception)
            {
            }

            int sent = 0;
            foreach (string[] row in PcRecord.Rows())
            {
                string mac = row[PcRecord.ColumnMac];
                if (mac.Length == 0 || ComputerName.Same(mac, ownMac)) continue;
                string mask = row[PcRecord.ColumnMask].Length > 0 ? row[PcRecord.ColumnMask] : defaultMask;
                if (Wake(mac, row[PcRecord.ColumnIp], mask))
                {
                    sent++;
                    log("Wake-up signal sent to " + row[PcRecord.ColumnName] + " (" + mac + ")");
                    Thread.Sleep(150);   // a little gap, so the PCs do not all start at the same moment
                }
                else
                {
                    log("Not a usable MAC address, skipped: " + row[PcRecord.ColumnName] + " (" + mac + ")");
                }
            }
            return sent;
        }
    }

    /// <summary>
    /// Prepares this PC to be woken over the network: the network card answers the magic packet also when the PC
    /// is shut down, the card may wake the PC, and Fast Startup is off (with Fast Startup on, "Shut down" is a
    /// half-hibernation and many cards then ignore the packet). The BIOS setting must be on already.
    /// Everything it changes is remembered and put back by Undo.
    /// </summary>
    internal sealed class WolSetting : Setting
    {
        private const string ClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        private const string PowerKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";
        private const string BackupKey = @"SOFTWARE\LabLock\WolBackup";
        private const string Absent = "<absent>";

        // Card settings (as in Device Manager > network card > Advanced) and the value that allows waking.
        // Names differ between makers; only the ones the card has are changed.
        // As Dell asks for its Realtek cards (Vostro, OptiPlex): shutdown wake on, magic packet on, the link kept
        // at 10 Mbps after shutdown, and Energy-Efficient / Green Ethernet off (it can drop the link).
        private static readonly string[] Keywords = { "*WakeOnMagicPacket", "S5WakeOnLan", "EnablePME", "WolShutdownLinkSpeed", "*EEE", "EEE", "EnableGreenEthernet" };
        private static readonly string[] Wanted = { "1", "1", "1", "0", "0", "0", "0" };

        public WolSetting()
        {
            Id = Catalog.IdWol;
            Group = Catalog.GroupComputer;
            Title = "Prepare this PC for Wake-on-LAN (network card, Fast Startup off)";
            Hint = "So you can switch the PCs on from the teacher PC (This PC page, or Veyon's Power on). Sets the wired network card to wake on the magic packet, also after Shut down, and turns off Fast Startup. Wake-on-LAN must be on in the BIOS. Takes effect after the next restart.";
            DefaultOn = true;
        }

        private static NetInfo WiredAdapter()
        {
            NetInfo net = NetConfig.Current();
            if (net == null) throw new InvalidOperationException("No connected network adapter was found. Plug in the network cable first.");
            if (net.Wireless) throw new InvalidOperationException("This PC is on Wi-Fi. Wake-on-LAN needs the network cable.");
            return net;
        }

        /// <summary>The registry key of the card's driver settings, found by the adapter's id.</summary>
        private static string CardKey(string adapterId)
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey cls = machine.OpenSubKey(ClassKey, false))
            {
                if (cls == null) return null;
                foreach (string name in cls.GetSubKeyNames())
                {
                    try
                    {
                        using (RegistryKey card = cls.OpenSubKey(name, false))
                        {
                            if (card != null && string.Equals(Convert.ToString(card.GetValue("NetCfgInstanceId")), adapterId, StringComparison.OrdinalIgnoreCase))
                            {
                                return ClassKey + "\\" + name;
                            }
                        }
                    }
                    catch (System.Security.SecurityException)
                    {
                        // a few of these keys are closed even to administrators ("Properties"); they are not cards
                    }
                }
            }
            return null;
        }

        /// <summary>True when the card's driver offers this setting (it is listed under Ndi\Params or already set).</summary>
        private static bool CardHas(RegistryKey card, string keyword)
        {
            if (card.GetValue(keyword) != null) return true;
            using (RegistryKey param = card.OpenSubKey(@"Ndi\Params\" + keyword, false))
            {
                return param != null;
            }
        }

        private static string Powercfg
        {
            get { return Proc.SystemTool("powercfg.exe"); }
        }

        private static bool WakeArmed(string description)
        {
            string output;
            Proc.Run(Powercfg, "/devicequery wake_armed", out output);
            foreach (string line in (output ?? "").Split('\n'))
            {
                if (string.Equals(line.Trim(), description, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool FastStartupOff()
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey power = machine.OpenSubKey(PowerKey, false))
            {
                object value = power == null ? null : power.GetValue("HiberbootEnabled");
                return value is int && (int)value == 0;
            }
        }

        public override string State(RunContext c)
        {
            NetInfo net = NetConfig.Current();
            if (net == null) return "No connected network adapter";
            if (net.Wireless) return "Not applied (the PC is on Wi-Fi; Wake-on-LAN needs the cable)";

            bool card = false;
            string key = CardKey(net.AdapterId);
            if (key != null)
            {
                using (RegistryKey machine = Reg.Machine())
                using (RegistryKey cardKey = machine.OpenSubKey(key, false))
                {
                    if (cardKey != null)
                    {
                        object magic = cardKey.GetValue("*WakeOnMagicPacket");
                        card = magic == null || Convert.ToString(magic) == "1";
                    }
                }
            }
            bool fast = FastStartupOff();
            bool armed = WakeArmed(net.Description);
            if (card && fast && armed) return "Applied";
            if (!card && !fast && !armed) return "Not applied";
            return "Partly applied" + (fast ? "" : " (Fast Startup is on)") + (armed ? "" : " (the card may not wake the PC)");
        }

        public override void Apply(RunContext c)
        {
            NetInfo net = WiredAdapter();

            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey backup = machine.CreateSubKey(BackupKey))
            {
                if (backup == null) throw new InvalidOperationException("Could not create the registry key " + BackupKey);
                bool firstTime = backup.GetValue("Adapter") == null;
                if (firstTime) backup.SetValue("Adapter", net.AdapterId, RegistryValueKind.String);

                // 1. the network card
                string key = CardKey(net.AdapterId);
                if (key == null)
                {
                    c.Log("The settings of the network card were not found; only Fast Startup and waking are set.");
                }
                else
                {
                    using (RegistryKey card = machine.OpenSubKey(key, true))
                    {
                        for (int i = 0; i < Keywords.Length; i++)
                        {
                            if (card == null || !CardHas(card, Keywords[i])) continue;
                            object old = card.GetValue(Keywords[i]);
                            if (firstTime) backup.SetValue("Card " + Keywords[i], old == null ? Absent : Convert.ToString(old), RegistryValueKind.String);
                            card.SetValue(Keywords[i], Wanted[i], RegistryValueKind.String);
                        }
                    }
                }

                // 2. Fast Startup off
                using (RegistryKey power = machine.CreateSubKey(PowerKey))
                {
                    if (power != null)
                    {
                        object old = power.GetValue("HiberbootEnabled");
                        if (firstTime) backup.SetValue("HiberbootEnabled", old is int ? (int)old : -1, RegistryValueKind.DWord);
                        power.SetValue("HiberbootEnabled", 0, RegistryValueKind.DWord);
                    }
                }

                // 3. the card may wake the PC ("Allow this device to wake the computer")
                bool armed = WakeArmed(net.Description);
                if (firstTime) backup.SetValue("WakeArmed", armed ? 1 : 0, RegistryValueKind.DWord);
                if (!armed)
                {
                    string output;
                    if (Proc.Run(Powercfg, "/deviceenablewake \"" + net.Description + "\"", out output) != 0)
                    {
                        c.Log("WARNING: Windows did not allow '" + net.Description + "' to wake the PC. In Device Manager > the network card > Power Management, tick \"Allow this device to wake the computer\".");
                    }
                }
            }
            c.Log("Wake-on-LAN is ready on '" + net.Description + "' (MAC " + net.Mac + "). It works after the next restart, when the PC is shut down normally and the network cable stays in.");
        }

        public override void Undo(RunContext c)
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey backup = machine.OpenSubKey(BackupKey, false))
            {
                if (backup == null)
                {
                    c.Log("LabLock has not prepared Wake-on-LAN on this PC, so nothing was changed.");
                    return;
                }

                string adapterId = Convert.ToString(backup.GetValue("Adapter"));
                string key = adapterId.Length > 0 ? CardKey(adapterId) : null;
                if (key != null)
                {
                    using (RegistryKey card = machine.OpenSubKey(key, true))
                    {
                        foreach (string keyword in Keywords)
                        {
                            object old = backup.GetValue("Card " + keyword);
                            if (old == null || card == null) continue;
                            string text = Convert.ToString(old);
                            if (text == Absent) card.DeleteValue(keyword, false);
                            else card.SetValue(keyword, text, RegistryValueKind.String);
                        }
                    }
                }

                object hiberboot = backup.GetValue("HiberbootEnabled");
                if (hiberboot is int)
                {
                    using (RegistryKey power = machine.OpenSubKey(PowerKey, true))
                    {
                        if (power != null)
                        {
                            if ((int)hiberboot < 0) power.DeleteValue("HiberbootEnabled", false);
                            else power.SetValue("HiberbootEnabled", (int)hiberboot, RegistryValueKind.DWord);
                        }
                    }
                }

                object armed = backup.GetValue("WakeArmed");
                if (armed is int && (int)armed == 0)
                {
                    NetInfo net = NetConfig.Current();
                    if (net != null && string.Equals(net.AdapterId, adapterId, StringComparison.OrdinalIgnoreCase))
                    {
                        string output;
                        Proc.Run(Powercfg, "/devicedisablewake \"" + net.Description + "\"", out output);
                    }
                }
            }
            using (RegistryKey machine = Reg.Machine())
            {
                machine.DeleteSubKeyTree(BackupKey, false);
            }
            c.Log("The network card and Fast Startup are back as they were before LabLock (after the next restart).");
        }
    }
}
