using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace LabLock
{
    /// <summary>One local account on this PC.</summary>
    internal sealed class LocalUser
    {
        public string Name;
        public string FullName;
        public string Sid;
        public bool Disabled;
        public bool IsAdmin;
        public bool PasswordChangeable;

        /// <summary>Administrator, Guest, DefaultAccount and WDAGUtilityAccount: never offered as the student account.</summary>
        public bool IsBuiltIn
        {
            get
            {
                if (Sid == null) return false;
                return Sid.EndsWith("-500") || Sid.EndsWith("-501") || Sid.EndsWith("-503") || Sid.EndsWith("-504");
            }
        }

        public override string ToString()
        {
            string text = Name;
            if (!string.IsNullOrEmpty(FullName) && !string.Equals(FullName, Name, StringComparison.OrdinalIgnoreCase))
            {
                text += "  (shown as \"" + FullName + "\")";
            }
            text += IsAdmin ? "  -  administrator" : "  -  standard user";
            if (Disabled) text += ", disabled";
            return text;
        }
    }

    /// <summary>Local accounts and groups. Groups are addressed by SID, so this works on Windows in any language.</summary>
    internal static class Accounts
    {
        public const string AdminsSid = "S-1-5-32-544";
        public const string UsersSid = "S-1-5-32-545";

        private const int ErrorMemberNotInAlias = 1377;
        private const int ErrorMemberInAlias = 1378;

        [StructLayout(LayoutKind.Sequential)]
        private struct LOCALGROUP_MEMBERS_INFO_0
        {
            public IntPtr lgrmi0_sid;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct USER_INFO_1
        {
            public string usri1_name;
            public string usri1_password;
            public int usri1_password_age;
            public int usri1_priv;
            public string usri1_home_dir;
            public string usri1_comment;
            public int usri1_flags;
            public string usri1_script_path;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetLocalGroupAddMembers(string servername, string groupname, int level,
            ref LOCALGROUP_MEMBERS_INFO_0 buf, int totalentries);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetLocalGroupDelMembers(string servername, string groupname, int level,
            ref LOCALGROUP_MEMBERS_INFO_0 buf, int totalentries);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetLocalGroupGetMembers(string servername, string localgroupname, int level,
            out IntPtr bufptr, int prefmaxlen, out int entriesread, out int totalentries, IntPtr resumehandle);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetUserAdd(string servername, int level, ref USER_INFO_1 buf, out int parmErr);

        [DllImport("netapi32.dll")]
        private static extern int NetApiBufferFree(IntPtr buffer);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetUserModalsGet(string servername, int level, out IntPtr bufptr);

        /// <summary>After how many wrong passwords Windows locks an account; 0 means never.</summary>
        public static int LockoutThreshold()
        {
            IntPtr buffer = IntPtr.Zero;
            int code = NetUserModalsGet(null, 3, out buffer);
            try
            {
                if (code != 0)
                {
                    throw new InvalidOperationException("Could not read the lockout rule: " + Describe(code));
                }
                // USER_MODALS_INFO_3: duration, observation window, threshold (three 32-bit numbers)
                return Marshal.ReadInt32(buffer, 8);
            }
            finally
            {
                if (buffer != IntPtr.Zero) NetApiBufferFree(buffer);
            }
        }

        private static string Describe(int code)
        {
            switch (code)
            {
                case 5: return "access denied";
                case 1371: return "this is a built-in account and cannot be changed this way";
                case 2202: return "the account name is not valid";
                case 2220: return "the group was not found";
                case 2221: return "the account was not found";
                case 2224: return "the account already exists";
                case 2245: return "the password does not meet this PC's password rules (too short or too simple)";
            }
            if (code < 2100)
            {
                return new Win32Exception(code).Message + " (error " + code + ")";
            }
            return "error " + code;
        }

        /// <summary>The local name of a built-in group, for example "Administrators" (it differs per Windows language).</summary>
        private static string GroupName(string groupSid)
        {
            SecurityIdentifier sid = new SecurityIdentifier(groupSid);
            NTAccount account = (NTAccount)sid.Translate(typeof(NTAccount));
            string value = account.Value;
            int slash = value.IndexOf('\\');
            return slash >= 0 ? value.Substring(slash + 1) : value;
        }

        /// <summary>SIDs of the direct members of a local group.</summary>
        public static HashSet<string> GroupMemberSids(string groupSid)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IntPtr buffer = IntPtr.Zero;
            int read;
            int total;
            int code = NetLocalGroupGetMembers(null, GroupName(groupSid), 0, out buffer, -1, out read, out total, IntPtr.Zero);
            try
            {
                if (code != 0)
                {
                    throw new InvalidOperationException("Could not read the members of the group: " + Describe(code));
                }
                for (int i = 0; i < read; i++)
                {
                    IntPtr sidPointer = Marshal.ReadIntPtr(buffer, i * IntPtr.Size);
                    if (sidPointer != IntPtr.Zero)
                    {
                        result.Add(new SecurityIdentifier(sidPointer).Value);
                    }
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero) NetApiBufferFree(buffer);
            }
            return result;
        }

        private static int ChangeMembership(string groupSid, string userSid, bool add)
        {
            SecurityIdentifier sid = new SecurityIdentifier(userSid);
            byte[] bytes = new byte[sid.BinaryLength];
            sid.GetBinaryForm(bytes, 0);

            IntPtr memory = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, memory, bytes.Length);
                LOCALGROUP_MEMBERS_INFO_0 info = new LOCALGROUP_MEMBERS_INFO_0();
                info.lgrmi0_sid = memory;
                string group = GroupName(groupSid);
                if (add)
                {
                    return NetLocalGroupAddMembers(null, group, 0, ref info, 1);
                }
                return NetLocalGroupDelMembers(null, group, 0, ref info, 1);
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }
        }

        public static void AddToGroup(string groupSid, string userSid)
        {
            int code = ChangeMembership(groupSid, userSid, true);
            if (code != 0 && code != ErrorMemberInAlias)
            {
                throw new InvalidOperationException("Could not add the account to the group: " + Describe(code));
            }
        }

        public static void RemoveFromGroup(string groupSid, string userSid)
        {
            int code = ChangeMembership(groupSid, userSid, false);
            if (code != 0 && code != ErrorMemberNotInAlias)
            {
                throw new InvalidOperationException("Could not remove the account from the group: " + Describe(code));
            }
        }

        /// <summary>Creates a local account whose password never expires. It is not in any group yet.</summary>
        public static void CreateUser(string name, string password, string comment)
        {
            const int UserPrivUser = 1;
            const int UfScript = 0x0001;
            const int UfDontExpirePassword = 0x10000;

            USER_INFO_1 info = new USER_INFO_1();
            info.usri1_name = name;
            info.usri1_password = password;
            info.usri1_password_age = 0;
            info.usri1_priv = UserPrivUser;
            info.usri1_home_dir = null;
            info.usri1_comment = comment;
            info.usri1_flags = UfScript | UfDontExpirePassword;
            info.usri1_script_path = null;

            int parameterError;
            int code = NetUserAdd(null, 1, ref info, out parameterError);
            if (code != 0)
            {
                throw new InvalidOperationException("Could not create the account '" + name + "': " + Describe(code));
            }
        }

        public static string SidOf(string userName)
        {
            NTAccount account = new NTAccount(Environment.MachineName, userName);
            SecurityIdentifier sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
            return sid.Value;
        }

        /// <summary>All local accounts of this PC, sorted by name.</summary>
        public static List<LocalUser> ListUsers()
        {
            HashSet<string> admins;
            try
            {
                admins = GroupMemberSids(AdminsSid);
            }
            catch (Exception)
            {
                admins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            List<LocalUser> list = new List<LocalUser>();
            string query = "SELECT Name, FullName, SID, Disabled, PasswordChangeable FROM Win32_UserAccount WHERE LocalAccount = True";
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            using (ManagementObjectCollection results = searcher.Get())
            {
                foreach (ManagementBaseObject item in results)
                {
                    LocalUser user = new LocalUser();
                    user.Name = Convert.ToString(item["Name"]);
                    user.FullName = Convert.ToString(item["FullName"]);
                    user.Sid = Convert.ToString(item["SID"]);
                    object disabled = item["Disabled"];
                    user.Disabled = disabled != null && Convert.ToBoolean(disabled);
                    object changeable = item["PasswordChangeable"];
                    user.PasswordChangeable = changeable == null || Convert.ToBoolean(changeable);
                    user.IsAdmin = admins.Contains(user.Sid);
                    list.Add(user);
                    item.Dispose();
                }
            }

            list.Sort(delegate(LocalUser a, LocalUser b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        public static LocalUser Find(List<LocalUser> users, string name)
        {
            if (users == null || string.IsNullOrEmpty(name)) return null;
            foreach (LocalUser user in users)
            {
                if (string.Equals(user.Name, name, StringComparison.OrdinalIgnoreCase)) return user;
            }
            return null;
        }
    }
}
