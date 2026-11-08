using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace LabLock
{
    /// <summary>The network settings of this PC's main network adapter, as they are right now.</summary>
    internal sealed class NetInfo
    {
        public string AdapterName = "";
        public string AdapterId = "";
        public string Description = "";
        public bool Wireless;
        public string Mac = "";
        public string Address = "";
        public string Mask = "";
        public string Gateway = "";
        public List<string> Dns = new List<string>();
        public bool Dhcp;
    }

    /// <summary>Reads and changes the IP address and the DNS servers of this PC (through netsh).</summary>
    internal static class NetConfig
    {
        private const string InterfacesKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\";
        private const string BackupKey = @"SOFTWARE\LabLock\DnsBackup";
        private const string IpBackupKey = @"SOFTWARE\LabLock\IpBackup";

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(int destinationIp, int sourceIp, byte[] macAddress, ref int macAddressLength);

        // ------------------------------------------------------------------ checking what was typed

        public static bool IsIPv4(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.Split('.').Length != 4) return false;
            IPAddress address;
            return IPAddress.TryParse(text, out address) && address.AddressFamily == AddressFamily.InterNetwork;
        }

        public static uint ToNumber(string ipv4)
        {
            byte[] b = IPAddress.Parse(ipv4).GetAddressBytes();
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | (uint)b[3];
        }

        /// <summary>A subnet mask is a row of ones followed by a row of zeros, for example 255.255.255.0.</summary>
        public static bool IsMask(string text)
        {
            if (!IsIPv4(text)) return false;
            uint mask = ToNumber(text);
            if (mask == 0 || mask == 0xFFFFFFFF) return false;
            uint zeros = ~mask;
            return (zeros & (zeros + 1)) == 0;
        }

        /// <summary>Returns what is wrong with a fixed IP configuration, or null when it is usable.</summary>
        public static string AddressProblem(string ip, string mask, string gateway)
        {
            if (!IsIPv4(ip)) return "Type the IP address for this PC, for example 192.168.2.21.";
            if (!IsMask(mask)) return "Type the subnet mask, for example 255.255.255.0.";

            uint maskNumber = ToNumber(mask);
            uint host = ToNumber(ip) & ~maskNumber;
            if (host == 0 || host == ~maskNumber)
            {
                return ip + " is the network or broadcast address of that network and cannot be given to a PC.";
            }
            if (!string.IsNullOrEmpty(gateway))
            {
                if (!IsIPv4(gateway)) return "The gateway is not a valid IP address (leave it empty if there is none).";
                if ((ToNumber(gateway) & maskNumber) != (ToNumber(ip) & maskNumber))
                {
                    return "The gateway " + gateway + " is not in the same network as " + ip + ". Check the address, the mask and the gateway.";
                }
                if (gateway == ip) return "The PC and the gateway cannot have the same IP address.";
            }
            return null;
        }

        /// <summary>The DNS servers that were typed: the first is needed, the second is optional.</summary>
        public static List<string> DnsList(string first, string second)
        {
            List<string> list = new List<string>();
            if (!string.IsNullOrEmpty(first)) list.Add(first);
            if (!string.IsNullOrEmpty(second) && second != first) list.Add(second);
            return list;
        }

        public static string DnsProblem(string first, string second)
        {
            if (!IsIPv4(first)) return "Type the IP address of the DNS server, for example 192.168.2.48.";
            if (!string.IsNullOrEmpty(second) && !IsIPv4(second)) return "The second DNS server is not a valid IP address (leave it empty if there is none).";
            return null;
        }

        // ------------------------------------------------------------------ finding the adapter

        private static bool IsCandidate(NetworkInterface adapter)
        {
            if (adapter.OperationalStatus != OperationalStatus.Up) return false;
            NetworkInterfaceType type = adapter.NetworkInterfaceType;
            return type == NetworkInterfaceType.Ethernet || type == NetworkInterfaceType.GigabitEthernet ||
                   type == NetworkInterfaceType.Wireless80211;
        }

        private static bool HasGateway(NetworkInterface adapter)
        {
            foreach (GatewayIPAddressInformation gateway in adapter.GetIPProperties().GatewayAddresses)
            {
                if (gateway.Address.AddressFamily == AddressFamily.InterNetwork && !gateway.Address.Equals(IPAddress.Any))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool LooksVirtual(NetworkInterface adapter)
        {
            string text = (adapter.Description + " " + adapter.Name).ToLowerInvariant();
            return text.Contains("virtual") || text.Contains("vmware") || text.Contains("hyper-v") ||
                   text.Contains("loopback") || text.Contains("bluetooth") || text.Contains("vpn") || text.Contains("tap-");
        }

        /// <summary>Connected wired or wireless adapters that have a gateway (this skips virtual adapters).</summary>
        private static List<NetworkInterface> ActiveAdapters()
        {
            List<NetworkInterface> list = new List<NetworkInterface>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (IsCandidate(adapter) && HasGateway(adapter)) list.Add(adapter);
            }
            return list;
        }

        /// <summary>
        /// The adapter LabLock gives a fixed address to: a connected one with a gateway if there is one,
        /// wired before wireless. Without any gateway, a connected adapter that is not a virtual one.
        /// </summary>
        private static NetworkInterface PrimaryAdapter()
        {
            NetworkInterface best = null;
            int bestScore = -1;
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!IsCandidate(adapter)) continue;
                bool gateway = HasGateway(adapter);
                if (!gateway && LooksVirtual(adapter)) continue;

                int score = (gateway ? 2 : 0) + (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1);
                if (score > bestScore)
                {
                    best = adapter;
                    bestScore = score;
                }
            }
            return best;
        }

        private static List<string> CurrentDns(NetworkInterface adapter)
        {
            List<string> list = new List<string>();
            foreach (IPAddress address in adapter.GetIPProperties().DnsAddresses)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork) list.Add(address.ToString());
            }
            return list;
        }

        /// <summary>Every DNS server the connected adapters use right now.</summary>
        public static List<string> AllCurrentDns()
        {
            List<string> all = new List<string>();
            foreach (NetworkInterface adapter in ActiveAdapters())
            {
                foreach (string server in CurrentDns(adapter))
                {
                    if (!all.Contains(server)) all.Add(server);
                }
            }
            return all;
        }

        /// <summary>The current settings of the main adapter, or null when no network adapter is connected.</summary>
        public static NetInfo Current()
        {
            NetworkInterface adapter = PrimaryAdapter();
            if (adapter == null) return null;

            NetInfo info = new NetInfo();
            info.AdapterName = adapter.Name;
            info.AdapterId = adapter.Id;
            info.Description = adapter.Description;
            info.Wireless = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
            info.Mac = BitConverter.ToString(adapter.GetPhysicalAddress().GetAddressBytes());

            IPInterfaceProperties properties = adapter.GetIPProperties();
            foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                info.Address = unicast.Address.ToString();
                if (unicast.IPv4Mask != null) info.Mask = unicast.IPv4Mask.ToString();
                break;
            }
            foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
            {
                if (gateway.Address.AddressFamily != AddressFamily.InterNetwork || gateway.Address.Equals(IPAddress.Any)) continue;
                info.Gateway = gateway.Address.ToString();
                break;
            }
            info.Dns = CurrentDns(adapter);
            try
            {
                info.Dhcp = properties.GetIPv4Properties().IsDhcpEnabled;
            }
            catch (Exception)
            {
                info.Dhcp = false;
            }
            return info;
        }

        // ------------------------------------------------------------------ netsh helpers

        private static void Netsh(string arguments, string what)
        {
            Proc.RunOrThrow(Proc.SystemTool("netsh.exe"), arguments, what);
        }

        private static void SetDnsServers(string adapterName, IList<string> servers)
        {
            Netsh("interface ipv4 set dnsservers name=\"" + adapterName + "\" source=static address=" + servers[0] + " validate=no",
                "Could not set the DNS server on '" + adapterName + "'");
            for (int i = 1; i < servers.Count; i++)
            {
                Netsh("interface ipv4 add dnsservers name=\"" + adapterName + "\" address=" + servers[i] + " index=" + (i + 1) + " validate=no",
                    "Could not set the second DNS server on '" + adapterName + "'");
            }
        }

        // ------------------------------------------------------------------ DNS only (the PC keeps its address)

        public static string DnsState(List<string> wanted)
        {
            List<NetworkInterface> adapters = ActiveAdapters();
            if (adapters.Count == 0) return "No connected network adapter";

            bool allSet = wanted.Count > 0;
            List<string> seen = new List<string>();
            foreach (NetworkInterface adapter in adapters)
            {
                List<string> dns = CurrentDns(adapter);
                if (dns.Count != wanted.Count) allSet = false;
                for (int i = 0; i < dns.Count; i++)
                {
                    if (i >= wanted.Count || dns[i] != wanted[i]) allSet = false;
                    if (!seen.Contains(dns[i])) seen.Add(dns[i]);
                }
            }

            string now = seen.Count == 0 ? "none" : string.Join(", ", seen.ToArray());
            return allSet ? "Applied (" + now + ")" : "Not applied (DNS now: " + now + ")";
        }

        /// <summary>The manually set DNS servers of an adapter; an empty text means "automatic (DHCP)".</summary>
        private static string StaticDns(NetworkInterface adapter)
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey key = machine.OpenSubKey(InterfacesKey + adapter.Id, false))
            {
                if (key == null) return "";
                return Convert.ToString(key.GetValue("NameServer")).Trim();
            }
        }

        public static void ApplyDns(List<string> servers, Action<string> log)
        {
            if (servers.Count == 0)
            {
                throw new InvalidOperationException("Type the IP address of the DNS server first.");
            }
            List<NetworkInterface> adapters = ActiveAdapters();
            if (adapters.Count == 0)
            {
                throw new InvalidOperationException("No connected network adapter was found.");
            }

            foreach (NetworkInterface adapter in adapters)
            {
                // Remember the original setting once, so "Undo" can restore it even after several runs.
                using (RegistryKey machine = Reg.Machine())
                using (RegistryKey backup = machine.CreateSubKey(BackupKey))
                {
                    if (backup != null && backup.GetValue(adapter.Id) == null)
                    {
                        backup.SetValue(adapter.Id, StaticDns(adapter), RegistryValueKind.String);
                    }
                }

                SetDnsServers(adapter.Name, servers);
                log("DNS on '" + adapter.Name + "' set to " + string.Join(", ", servers.ToArray()) + ".");
            }
        }

        public static void UndoDns(Action<string> log)
        {
            foreach (NetworkInterface adapter in ActiveAdapters())
            {
                string previous = null;
                using (RegistryKey machine = Reg.Machine())
                using (RegistryKey backup = machine.OpenSubKey(BackupKey, false))
                {
                    if (backup != null)
                    {
                        object saved = backup.GetValue(adapter.Id);
                        if (saved != null) previous = Convert.ToString(saved);
                    }
                }

                if (previous == null)
                {
                    log("DNS on '" + adapter.Name + "' was not changed by this setting, so it was left as it is.");
                    continue;
                }

                string[] servers = previous.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (servers.Length == 0)
                {
                    Netsh("interface ipv4 set dnsservers name=\"" + adapter.Name + "\" source=dhcp",
                        "Could not set the DNS on '" + adapter.Name + "' back to automatic");
                    log("DNS on '" + adapter.Name + "' is automatic again.");
                }
                else
                {
                    SetDnsServers(adapter.Name, servers);
                    log("DNS on '" + adapter.Name + "' restored to " + string.Join(", ", servers) + ".");
                }

                using (RegistryKey machine = Reg.Machine())
                using (RegistryKey backup = machine.OpenSubKey(BackupKey, true))
                {
                    if (backup != null) backup.DeleteValue(adapter.Id, false);
                }
            }
        }

        // ------------------------------------------------------------------ fixed IP address

        /// <summary>Asks the network whether some device already answers on this address (works inside the PC's own network).</summary>
        private static bool AddressInUse(string ip)
        {
            try
            {
                int destination = BitConverter.ToInt32(IPAddress.Parse(ip).GetAddressBytes(), 0);
                byte[] mac = new byte[6];
                int length = mac.Length;
                return SendARP(destination, 0, mac, ref length) == 0 && length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string FixedState(string wantedIp)
        {
            NetInfo now = Current();
            if (now == null) return "No connected network adapter";
            if (now.Dhcp) return "Not applied (automatic, now " + (now.Address.Length > 0 ? now.Address : "no address") + ")";
            if (now.Address == wantedIp) return "Applied (fixed at " + now.Address + ")";
            return "Fixed at " + now.Address + " now, not the address typed";
        }

        /// <summary>Gives the main adapter a fixed address, gateway and DNS servers.</summary>
        public static void ApplyFixed(string ip, string mask, string gateway, List<string> dns, Action<string> log)
        {
            string problem = AddressProblem(ip, mask, gateway);
            if (problem != null) throw new InvalidOperationException(problem);
            if (dns.Count == 0) throw new InvalidOperationException("A PC with a fixed address needs a DNS server. Type one first.");

            NetInfo now = Current();
            if (now == null)
            {
                throw new InvalidOperationException("No connected network adapter was found. Plug in the network cable first.");
            }
            if (now.Address != ip && AddressInUse(ip))
            {
                throw new InvalidOperationException("Another device on the network already answers on " + ip + ". Nothing was changed.");
            }

            SaveIpBackup(now);

            string adapter = now.AdapterName;
            Netsh("interface ipv4 set address name=\"" + adapter + "\" source=static address=" + ip + " mask=" + mask +
                  " gateway=" + (string.IsNullOrEmpty(gateway) ? "none" : gateway),
                "Could not set the IP address on '" + adapter + "'");
            SetDnsServers(adapter, dns);

            // Windows needs a moment before the adapter reports the new address.
            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(500);
                NetInfo check = Current();
                if (check != null && check.Address == ip) break;
            }
            log("'" + adapter + "' now has the fixed address " + ip + " (mask " + mask + ", gateway " +
                (string.IsNullOrEmpty(gateway) ? "none" : gateway) + ", DNS " + string.Join(", ", dns.ToArray()) + ").");
        }

        /// <summary>
        /// Remembers, once, how the adapter was set before LabLock gave it a fixed address: automatic, or a fixed
        /// address typed by hand (as in a lab that was already set up for Veyon). Undo puts exactly that back.
        /// </summary>
        private static void SaveIpBackup(NetInfo now)
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey backup = machine.CreateSubKey(IpBackupKey))
            {
                if (backup == null || backup.GetValue("Dhcp") != null) return;
                backup.SetValue("Address", now.Address, RegistryValueKind.String);
                backup.SetValue("Mask", now.Mask, RegistryValueKind.String);
                backup.SetValue("Gateway", now.Gateway, RegistryValueKind.String);
                backup.SetValue("Dns", now.Dhcp ? "" : StaticDnsOf(now.AdapterId), RegistryValueKind.String);
                backup.SetValue("Dhcp", now.Dhcp ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        private static string StaticDnsOf(string adapterId)
        {
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey key = machine.OpenSubKey(InterfacesKey + adapterId, false))
            {
                return key == null ? "" : Convert.ToString(key.GetValue("NameServer")).Trim();
            }
        }

        private static void DeleteIpBackup()
        {
            using (RegistryKey machine = Reg.Machine())
            {
                machine.DeleteSubKeyTree(IpBackupKey, false);
            }
        }

        /// <summary>
        /// Puts the main adapter back the way it was before LabLock: the earlier fixed address when it had one,
        /// otherwise automatic addressing (DHCP) for the address and the DNS.
        /// </summary>
        public static void UndoFixed(Action<string> log)
        {
            NetInfo now = Current();
            if (now == null)
            {
                throw new InvalidOperationException("No connected network adapter was found.");
            }

            string address = "", mask = "", gateway = "", dns = "";
            bool hadBackup = false, wasDhcp = true;
            using (RegistryKey machine = Reg.Machine())
            using (RegistryKey backup = machine.OpenSubKey(IpBackupKey, false))
            {
                if (backup != null && backup.GetValue("Dhcp") != null)
                {
                    hadBackup = true;
                    wasDhcp = Convert.ToInt32(backup.GetValue("Dhcp")) != 0;
                    address = Convert.ToString(backup.GetValue("Address"));
                    mask = Convert.ToString(backup.GetValue("Mask"));
                    gateway = Convert.ToString(backup.GetValue("Gateway"));
                    dns = Convert.ToString(backup.GetValue("Dns"));
                }
            }

            if (hadBackup && !wasDhcp && IsIPv4(address) && IsMask(mask))
            {
                Netsh("interface ipv4 set address name=\"" + now.AdapterName + "\" source=static address=" + address + " mask=" + mask +
                      " gateway=" + (IsIPv4(gateway) ? gateway : "none"),
                    "Could not put the earlier address back on '" + now.AdapterName + "'");
                string[] servers = dns.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (servers.Length > 0)
                {
                    SetDnsServers(now.AdapterName, servers);
                }
                else
                {
                    string ignoredOutput;
                    Proc.Run(Proc.SystemTool("netsh.exe"), "interface ipv4 set dnsservers name=\"" + now.AdapterName + "\" source=dhcp", out ignoredOutput);
                }
                DeleteIpBackup();
                Thread.Sleep(3000);
                log("'" + now.AdapterName + "' has its earlier fixed address " + address + " again (as before LabLock).");
                return;
            }

            if (now.Dhcp)
            {
                DeleteIpBackup();
                log("'" + now.AdapterName + "' already gets its address automatically.");
                return;
            }

            Netsh("interface ipv4 set address name=\"" + now.AdapterName + "\" source=dhcp",
                "Could not set '" + now.AdapterName + "' back to an automatic address");
            string ignored;
            Proc.Run(Proc.SystemTool("netsh.exe"), "interface ipv4 set dnsservers name=\"" + now.AdapterName + "\" source=dhcp", out ignored);
            DeleteIpBackup();
            Thread.Sleep(3000);
            log("'" + now.AdapterName + "' gets its address and DNS automatically (DHCP) again.");
        }
    }
}
