using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LabLock
{
    /// <summary>The Lockdown page: accounts, this PC, options on the left; the checklist, the hint and the log on the right.</summary>
    internal sealed partial class MainForm
    {
        // Left column: a label in front of each field.
        private const int LabelX = 14;
        private const int LabelWidth = 100;
        private const int FieldX = 118;
        private const int FieldWidth = 208;
        private const int HalfWidth = 100;
        private const int SecondHalfX = 226;

        private readonly List<Setting> settings = Catalog.Build();
        private readonly List<ListRow> rows = new List<ListRow>();
        private List<LocalUser> users = new List<LocalUser>();

        private Picker pickStudent;
        private InputBox inAdmin;
        private InputBox inPass1;
        private InputBox inPass2;
        private InputBox inName;
        private InputBox inIp;
        private InputBox inMask;
        private InputBox inGateway;
        private InputBox inDns;
        private InputBox inDns2;
        private InputBox inWallpaper;
        private InputBox inSchool;
        private Label lblBlocklist;
        private Panel listPanel;
        private Label lblHint;
        private TextBox txtLog;
        private Button btnApply;
        private Button btnUndo;
        private Button btnRestore;

        /// <summary>A field in the left column with its label in front of it.</summary>
        private InputBox AddField(Control card, string label, int y, int x, int width)
        {
            if (label != null) AddLabel(card, label, LabelX, y + 5, LabelWidth, RoleSubtle);
            return AddInput(card, x, y, width);
        }

        private void BuildLockdownPage(Panel page)
        {
            // ---- accounts
            Panel accounts = AddCard(page, 16, 6, 340, 166);
            AddLabel(accounts, "ACCOUNTS", LabelX, 10, 300, RoleGroup);

            AddLabel(accounts, "Student account", LabelX, 34, 300, RoleSubtle);
            pickStudent = new Picker();
            pickStudent.EmptyText = L.T("(choose the student account)");
            pickStudent.SetBounds(LabelX, 54, 248, 28);
            pickStudent.SelectionChanged += OnStudentChanged;
            accounts.Controls.Add(pickStudent);
            pickStudent.SelectedIndex = -1;
            AddButton(accounts, "New...", 270, 54, 56, 28, null, OnNewStudentClick);

            inAdmin = AddField(accounts, "Administrator", 92, FieldX, FieldWidth);
            inAdmin.Text = config.Get("admin", "LabAdmin");
            inPass1 = AddField(accounts, "Password, twice", 126, FieldX, HalfWidth);
            inPass1.Password = true;
            inPass2 = AddField(accounts, null, 126, SecondHalfX, HalfWidth);
            inPass2.Password = true;

            // ---- this PC (name and network)
            Panel pc = AddCard(page, 16, 178, 340, 210);
            AddLabel(pc, "THIS PC", LabelX, 10, 170, RoleGroup);
            btnRestore = AddButton(pc, "Restore this PC...", 186, 4, 140, 26, null, OnRestoreClick);
            inName = AddField(pc, "Computer name", 34, FieldX, FieldWidth);
            inIp = AddField(pc, "IP address", 68, FieldX, FieldWidth);
            inMask = AddField(pc, "Subnet mask", 102, FieldX, FieldWidth);
            inGateway = AddField(pc, "Gateway", 136, FieldX, FieldWidth);
            inDns = AddField(pc, "DNS servers", 170, FieldX, HalfWidth);
            inDns2 = AddField(pc, null, 170, SecondHalfX, HalfWidth);

            // ---- options
            Panel options = AddCard(page, 16, 394, 340, 142);
            AddLabel(options, "OPTIONS", LabelX, 10, 300, RoleGroup);
            inWallpaper = AddField(options, "Wallpaper", 34, FieldX, 122);
            inWallpaper.Text = AppConfig.FromPortablePath(config.Get("wallpaper", ""));
            AddButton(options, "Browse...", 248, 34, 78, 28, null, OnBrowseWallpaperClick);
            inSchool = AddField(options, "School image", 68, FieldX, 122);
            inSchool.Text = AppConfig.FromPortablePath(config.Get("schoolimage", ""));
            AddButton(options, "Browse...", 248, 68, 78, 28, null, OnBrowseSchoolClick);
            lblBlocklist = AddLabel(options, "", LabelX, 107, 226, RoleSubtle);
            AddButton(options, "Edit...", 248, 102, 78, 28, null, OnEditBlocklistClick);

            // ---- actions
            btnApply = AddButton(page, "Apply ticked", 16, 544, 166, 36, RolePrimary, OnApplyClick);
            btnApply.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F);
            btnUndo = AddButton(page, "Undo ticked", 190, 544, 166, 36, null, OnUndoClick);

            // ---- checklist
            listPanel = AddCard(page, 372, 6, 612, 370);
            listPanel.Anchor = AnchorAll;
            listPanel.AutoScroll = true;
            listPanel.Resize += OnListResize;
            FillList();

            lblHint = AddLabel(page, "Tick the settings you want and press Apply. Point at a line to read what it does.", 372, 382, 612, RoleSubtle);
            lblHint.Height = 72;
            lblHint.Anchor = AnchorBottomWide;

            // ---- log
            Panel logCard = AddCard(page, 372, 460, 612, 120);
            logCard.Anchor = AnchorBottomWide;
            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.SetBounds(10, 8, 596, 104);
            txtLog.Anchor = AnchorAll;
            logCard.Controls.Add(txtLog);
        }

        private void FillList()
        {
            // Ticks come from LabLock.ini when it exists, otherwise from each setting's default.
            // A setting that is newer than the saved file keeps its default.
            List<string> ticked = new List<string>(config.Get("ticked", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            List<string> known = new List<string>(config.Get("known", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

            int y = 4;
            string group = null;
            foreach (Setting setting in settings)
            {
                if (setting.Group != group)
                {
                    group = setting.Group;
                    AddLabel(listPanel, group.ToUpperInvariant(), 14, y + 12, 560, RoleGroup);
                    y += 36;
                }

                ListRow row = new ListRow(true);
                row.Data = setting;
                row.Title = setting.Title;
                row.Hint = setting.Hint;
                row.SetBounds(0, y, 590, ListRow.RowHeight);
                row.Checked = known.Contains(setting.Id) ? ticked.Contains(setting.Id) : setting.DefaultOn;
                row.HintWanted += OnRowHintWanted;
                listPanel.Controls.Add(row);
                rows.Add(row);
                y += ListRow.RowHeight;
            }

            // a little air under the last row
            Label spacer = AddLabel(listPanel, "", 0, y, 10, null);
            spacer.Height = 8;
            FitRows(listPanel);
        }

        // ------------------------------------------------------------------ data

        /// <summary>
        /// Fills the "This PC" fields with what the PC has right now. The name and the IP address are different
        /// on every PC, so they always come from the PC itself; the DNS servers are the same for the whole lab,
        /// so they come from LabLock.ini once you have typed them.
        /// </summary>
        private void FillThisPc()
        {
            inName.Text = ComputerName.Pending();

            NetInfo net = null;
            try
            {
                net = NetConfig.Current();
            }
            catch (Exception ex)
            {
                WriteLog("Could not read the network settings of this PC: " + ex.Message);
            }

            inIp.Text = net == null ? "" : net.Address;
            inMask.Text = (net != null && net.Mask.Length > 0) ? net.Mask : config.Get("mask", "255.255.255.0");
            inGateway.Text = (net != null && net.Gateway.Length > 0) ? net.Gateway : config.Get("gateway", "");

            if (config.Get("dns", "").Length > 0)
            {
                inDns.Text = config.Get("dns", "");
                inDns2.Text = config.Get("dns2", "");
            }
            else if (net != null)
            {
                inDns.Text = net.Dns.Count > 0 ? net.Dns[0] : "";
                inDns2.Text = net.Dns.Count > 1 ? net.Dns[1] : "";
            }
        }

        private int FindUserIndex(string wanted)
        {
            if (string.IsNullOrEmpty(wanted)) return -1;
            for (int i = 0; i < pickStudent.Count; i++)
            {
                LocalUser user = (LocalUser)pickStudent.ItemAt(i);
                if (string.Equals(user.Name, wanted, StringComparison.OrdinalIgnoreCase)) return i;
            }
            for (int i = 0; i < pickStudent.Count; i++)
            {
                LocalUser user = (LocalUser)pickStudent.ItemAt(i);
                if (string.Equals(user.FullName, wanted, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>Reads the accounts of this PC again and selects the wanted student account if it is there.</summary>
        private void ReloadUsers(string preferred)
        {
            try
            {
                users = Accounts.ListUsers();
            }
            catch (Exception ex)
            {
                users = new List<LocalUser>();
                WriteLog("Could not read the accounts of this PC: " + ex.Message);
            }

            pickStudent.ClearItems();
            foreach (LocalUser user in users)
            {
                if (!user.IsBuiltIn) pickStudent.AddItem(user);
            }

            int index = FindUserIndex(preferred);
            if (index < 0) index = FindUserIndex("Student");
            pickStudent.SelectedIndex = index;
        }

        private string SelectedStudentName()
        {
            LocalUser user = pickStudent.SelectedItem as LocalUser;
            return user == null ? null : user.Name;
        }

        private RunContext BuildContext()
        {
            RunContext c = new RunContext();
            c.Users = users;
            c.StudentName = SelectedStudentName();
            c.AdminName = inAdmin.Text.Trim();
            c.AdminPassword = inPass1.Text;
            c.NewComputerName = inName.Text.Trim();
            c.IpAddress = inIp.Text.Trim();
            c.SubnetMask = inMask.Text.Trim();
            c.Gateway = inGateway.Text.Trim();
            c.DnsServer = inDns.Text.Trim();
            c.DnsServer2 = inDns2.Text.Trim();
            c.WallpaperPath = inWallpaper.Text.Trim();
            c.SchoolImagePath = inSchool.Text.Trim();
            c.ShutdownTime = NightShutdownSetting.CleanTime(config.Get("shutdown", NightShutdownSetting.DefaultTime));
            c.Log = WriteLog;
            return c;
        }

        private void RefreshStates()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                Dictionary<string, string> states = Engine.States(settings, BuildContext());
                foreach (ListRow row in rows)
                {
                    string state;
                    if (!states.TryGetValue(((Setting)row.Data).Id, out state)) state = "";
                    row.SetDetail(state, ListRow.KindOfState(state));
                }
                lblBlocklist.Text = L.T("Blocklist: ") + Blocklist.Load().Count + L.T(" game sites");
            }
            catch (Exception ex)
            {
                WriteLog("Could not read the current state: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private List<Setting> TickedSettings()
        {
            List<Setting> ticked = new List<Setting>();
            foreach (ListRow row in rows)
            {
                if (row.Checked) ticked.Add((Setting)row.Data);
            }
            return ticked;
        }

        private static bool Has(List<Setting> ticked, string id)
        {
            foreach (Setting setting in ticked)
            {
                if (setting.Id == id) return true;
            }
            return false;
        }

        private void SaveConfig()
        {
            // The computer name and the IP address belong to one PC, so they are not remembered.
            string student = SelectedStudentName();
            if (student != null) config.Set("student", student);
            config.Set("admin", inAdmin.Text.Trim());
            config.Set("mask", inMask.Text.Trim());
            config.Set("gateway", inGateway.Text.Trim());
            config.Set("dns", inDns.Text.Trim());
            config.Set("dns2", inDns2.Text.Trim());
            config.Set("wallpaper", AppConfig.ToPortablePath(inWallpaper.Text.Trim()));
            config.Set("schoolimage", AppConfig.ToPortablePath(inSchool.Text.Trim()));
            config.Set("room", inRoom.Text.Trim());
            config.Set("shutdown", NightShutdownSetting.CleanTime(config.Get("shutdown", NightShutdownSetting.DefaultTime)));   // written so it can be changed in the file
            config.Set("theme", theme.ModeName);
            config.Set("accent", theme.AccentName);
            config.Set("language", L.Greek ? "el" : "en");

            List<string> ticked = new List<string>();
            List<string> known = new List<string>();
            foreach (ListRow row in rows)
            {
                string id = ((Setting)row.Data).Id;
                known.Add(id);
                if (row.Checked) ticked.Add(id);
            }
            config.Set("ticked", string.Join(",", ticked.ToArray()));
            config.Set("known", string.Join(",", known.ToArray()));
            config.Save();
        }

        /// <summary>
        /// Writes this PC's line in the record of PCs. Notes that are null keep what the record already has.
        /// Returns false (and logs why) when the record could not be written.
        /// </summary>
        private bool SaveRecord(string notes)
        {
            NetInfo net = null;
            try
            {
                net = NetConfig.Current();
            }
            catch (Exception)
            {
            }

            string room = inRoom.Text.Trim();
            string problem = PcRecord.Save(ComputerName.Pending(), net, WindowsName(), Hardware.Read(), room.Length > 0 ? room : null, notes);
            if (problem == null) return true;
            WriteLog("The record of PCs (" + PcRecord.FileName + ") could not be updated: " + problem);
            return false;
        }

        // ------------------------------------------------------------------ apply and undo

        /// <summary>Returns a message when something is missing, or null when the run can start.</summary>
        private string CheckInputs(List<Setting> ticked, RunContext c, bool apply)
        {
            bool needStudent = false;
            foreach (Setting setting in ticked)
            {
                if (setting.NeedsStudent) needStudent = true;
            }
            if (needStudent && c.Student == null)
            {
                return "Choose the student account first.";
            }
            if (!apply) return null;

            bool createAdmin = Has(ticked, Catalog.IdAdmin);
            bool demote = Has(ticked, Catalog.IdDemote);
            LocalUser admin = Accounts.Find(c.Users, c.AdminName);

            if (createAdmin || demote)
            {
                if (c.AdminName.Length == 0)
                {
                    return "Type a name for the administrator account.";
                }
                if (c.Student != null && string.Equals(c.AdminName, c.Student.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return "The administrator account must be a different account from the student account.";
                }
            }
            if (createAdmin && admin == null)
            {
                if (inPass1.Text.Length < 8)
                {
                    return "Choose a password of at least 8 characters for the new administrator account.";
                }
                if (inPass1.Text != inPass2.Text)
                {
                    return "The two passwords are not the same.";
                }
            }
            if (demote && !createAdmin && (admin == null || admin.Disabled || !admin.IsAdmin))
            {
                return "'" + c.AdminName + "' is not a working administrator account on this PC.\r\n\r\n" +
                       "Tick 'Create a separate administrator account' as well, so the PC is not left without an administrator.";
            }

            string problem;
            if (Has(ticked, Catalog.IdFixedIp))
            {
                problem = NetConfig.AddressProblem(c.IpAddress, c.SubnetMask, c.Gateway);
                if (problem != null) return problem;
            }
            if (Has(ticked, Catalog.IdFixedIp) || Has(ticked, Catalog.IdDns) || Has(ticked, Catalog.IdDnsLock))
            {
                problem = NetConfig.DnsProblem(c.DnsServer, c.DnsServer2);
                if (problem != null) return problem;
            }
            if (Has(ticked, Catalog.IdRename))
            {
                problem = ComputerName.Problem(c.NewComputerName);
                if (problem != null) return problem;
            }
            if (Has(ticked, Catalog.IdWallpaper) && c.WallpaperPath.Length > 0 && !File.Exists(c.WallpaperPath))
            {
                return "The wallpaper image was not found:\r\n" + c.WallpaperPath;
            }
            if ((Has(ticked, Catalog.IdAccountPicture) || Has(ticked, Catalog.IdLockScreen)) && !SchoolImage.Available(c.SchoolImagePath))
            {
                return "Choose a school image first: this copy of LabLock has no built-in image.";
            }
            if (Has(ticked, Catalog.IdBlocklist) && Blocklist.Load().Count == 0)
            {
                return "The game sites blocklist is empty. Press Edit... and add at least one site.";
            }
            return null;
        }

        private void SetBusy(bool busy)
        {
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            btnApply.Enabled = !busy;
            btnUndo.Enabled = !busy;
            btnRestore.Enabled = !busy;
            btnRefresh.Enabled = !busy;
        }

        private void RunTicked(bool apply)
        {
            List<Setting> ticked = TickedSettings();
            if (ticked.Count == 0)
            {
                Tell("Tick at least one setting first.", MessageBoxIcon.Information);
                return;
            }

            ReloadUsers(SelectedStudentName());
            RunContext c = BuildContext();

            string problem = CheckInputs(ticked, c, apply);
            if (problem != null)
            {
                Tell(problem, MessageBoxIcon.Warning);
                return;
            }

            StringBuilder question = new StringBuilder();
            question.Append(L.T(apply ? "Apply the ticked settings on " : "Undo the ticked settings on "));
            question.Append(ComputerName.Active + "? (" + ticked.Count + ")");
            if (c.Student != null)
            {
                question.Append("\r\n\r\n" + L.T("Student account: ") + c.Student.Name);
            }
            if (apply && Has(ticked, Catalog.IdFixedIp))
            {
                question.Append("\r\n" + L.T("Fixed IP address: ") + c.IpAddress);
            }
            if (apply && Has(ticked, Catalog.IdRename) && !ComputerName.Same(c.NewComputerName, ComputerName.Active))
            {
                question.Append("\r\n" + L.T("New computer name: ") + c.NewComputerName);
            }
            if (apply && (Has(ticked, Catalog.IdAccountPicture) || Has(ticked, Catalog.IdLockScreen)))
            {
                // say which picture goes on the PCs: another school must not get this school's logo by accident
                question.Append("\r\n" + L.T("School image: ") + L.T(SchoolImage.Describe(c.SchoolImagePath)));
            }
            if (apply && Has(ticked, Catalog.IdCleanup))
            {
                question.Append("\r\n\r\n" + L.T("The student's Downloads will be emptied at every sign-in."));
            }
            bool demote = apply && Has(ticked, Catalog.IdDemote);
            if (demote && Accounts.Find(c.Users, c.AdminName) != null)
            {
                question.Append("\r\n\r\n" + L.T("The administrator account already exists and its password will NOT be changed. Make sure you know that password: afterwards it is the only way to administer this PC."));
            }
            if (!Confirm(question.ToString(), MessageBoxIcon.Question))
            {
                return;
            }

            int done = 0;
            int failed = 0;
            SetBusy(true);
            try
            {
                WriteLog("---- " + (apply ? "Applying on " : "Undoing on ") + ComputerName.Active + " ----");
                done = Engine.Run(ticked, c, apply, out failed);
            }
            finally
            {
                SetBusy(false);
            }

            inPass1.Clear();
            inPass2.Clear();
            SaveConfig();
            SaveRecord(null);
            ReloadUsers(c.StudentName);
            RefreshStates();
            ReportToSnm(c, apply ? "apply" : "undo");

            string summary = done + L.T(" done, ") + failed + L.T(" failed.");
            if (failed > 0)
            {
                summary += "\r\n" + L.T("Read the lines marked FAILED in the log.");
            }

            if (c.RestartNeeded)
            {
                summary += "\r\n\r\n" + L.T("The new computer name is used after a restart. The restart also makes all the other changes take hold.");
                if (demote)
                {
                    summary += "\r\n" + L.T("After the restart, sign in with the administrator account to check that it works.");
                }
                summary += "\r\n\r\n" + L.T("Restart this PC now?");
                if (Confirm(summary, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question))
                {
                    RestartPc();
                }
                return;
            }

            if (demote)
            {
                summary += "\r\n\r\n" + L.T("Before you go to the next PC: sign out and check that you can sign in with the administrator account.");
            }
            else if (apply)
            {
                summary += "\r\n\r\n" + L.T("Settings for the student take effect the next time the student signs in.");
            }
            Tell(summary, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        /// <summary>
        /// Takes back everything LabLock has changed on this PC, from the record kept on the PC itself,
        /// whatever is ticked in the list. Accounts made by LabLock and restore points stay.
        /// </summary>
        private void RestoreThisPc()
        {
            ReloadUsers(SelectedStudentName());
            RunContext c = BuildContext();
            c.Restoring = true;

            List<Setting> list = Journal.RestoreList(c, false, null);
            if (list.Count == 0)
            {
                Tell("LabLock has not changed anything on this PC (there is nothing in its record of changes).\r\n\r\n" +
                     "Settings applied by hand or by an older LabLock can still be taken back with Undo ticked.", MessageBoxIcon.Information);
                return;
            }

            // The student settings go back on the account they were applied to, even if another one is chosen now.
            LocalUser recorded = Journal.Student(c);
            if (recorded != null) c.StudentName = recorded.Name;

            bool needStudent = false;
            foreach (Setting setting in list)
            {
                if (setting.NeedsStudent) needStudent = true;
            }
            if (needStudent && c.Student == null)
            {
                Tell("Choose the student account first.", MessageBoxIcon.Warning);
                return;
            }

            StringBuilder question = new StringBuilder();
            question.Append(L.T("Restore this PC to how it was before LabLock?") + "\r\n" + L.T("Computer: ") + ComputerName.Active);
            question.Append("\r\n\r\n" + L.T("These changes will be taken back:"));
            foreach (Setting setting in list)
            {
                string stamp = Journal.When(c, setting.Id);
                question.Append("\r\n  - " + L.T(setting.Title) + (stamp.Length > 0 ? "  (" + stamp + ")" : ""));
            }
            if (c.Student != null && needStudent)
            {
                question.Append("\r\n\r\n" + L.T("Student account: ") + c.Student.Name);
            }
            if (Has(list, Catalog.IdDemote))
            {
                question.Append("\r\n\r\n" + L.T("If LabLock took the administrator rights away from the student account, the student account becomes an administrator again."));
            }
            if (Has(list, Catalog.IdFixedIp))
            {
                question.Append("\r\n" + L.T("The network address goes back to what it was before LabLock. If it changes, update it in Veyon."));
            }
            if (Has(list, Catalog.IdRename))
            {
                question.Append("\r\n" + L.T("The computer name goes back to the earlier one after a restart."));
            }
            question.Append("\r\n\r\n" + L.T("Accounts made by LabLock are not deleted."));
            if (!Confirm(question.ToString(), MessageBoxIcon.Warning))
            {
                return;
            }

            int done = 0;
            int failed = 0;
            SetBusy(true);
            try
            {
                WriteLog("---- " + "Restoring " + ComputerName.Active + " to how it was before LabLock" + " ----");
                done = Engine.Run(list, c, false, out failed);
            }
            finally
            {
                SetBusy(false);
            }

            SaveRecord(null);
            ReloadUsers(c.StudentName);
            RefreshStates();
            ReportToSnm(c, "restore");

            string summary = done + L.T(" done, ") + failed + L.T(" failed.");
            if (failed > 0)
            {
                summary += "\r\n" + L.T("Read the lines marked FAILED in the log.") + "\r\n" +
                           L.T("What failed stays in the record, so Restore this PC can try it again.");
            }
            if (c.RestartNeeded)
            {
                summary += "\r\n\r\n" + L.T("The earlier computer name is used after a restart.") + "\r\n\r\n" + L.T("Restart this PC now?");
                if (Confirm(summary, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question))
                {
                    RestartPc();
                }
                return;
            }
            summary += "\r\n\r\n" + L.T("Settings for the student take effect the next time the student signs in.");
            Tell(summary, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        /// <summary>Tells School Network Manager what is locked on this PC, when it is set in LabLock.ini.</summary>
        private void ReportToSnm(RunContext c, string eventName)
        {
            if (!Snm.Configured(config)) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                Snm.Report(config, c, eventName, WriteLog);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RestartPc()
        {
            try
            {
                WriteLog("Restarting this PC.");
                Proc.RunOrThrow(Proc.SystemTool("shutdown.exe"), "/r /t 5 /c \"LabLock\"", "The restart could not be started");
                Close();
            }
            catch (Exception ex)
            {
                Tell(ex.Message + "\r\n\r\n" + L.T("Restart the PC yourself from the Start menu."), MessageBoxIcon.Warning);
            }
        }

        // ------------------------------------------------------------------ events

        private void OnApplyClick(object sender, EventArgs e)
        {
            RunTicked(true);
        }

        private void OnUndoClick(object sender, EventArgs e)
        {
            RunTicked(false);
        }

        private void OnRestoreClick(object sender, EventArgs e)
        {
            RestoreThisPc();
        }

        private void OnStudentChanged(object sender, EventArgs e)
        {
            RefreshStates();
        }

        private void OnRowHintWanted(object sender, EventArgs e)
        {
            ListRow row = (ListRow)sender;
            if (row.Hint.Length > 0) lblHint.Text = L.T(row.Hint);
        }

        /// <summary>Creates a new standard account for the students, for a PC that has none yet.</summary>
        private void OnNewStudentClick(object sender, EventArgs e)
        {
            using (PromptDialog dialog = new PromptDialog(theme, "New student account", "Name of the new account (for example Student)", "Password (may be left empty)"))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string name = dialog.FirstText;
                if (name.Length == 0) return;
                try
                {
                    if (Accounts.Find(Accounts.ListUsers(), name) != null)
                    {
                        Tell("An account with that name already exists. Choose it from the list.", MessageBoxIcon.Information);
                    }
                    else
                    {
                        Accounts.CreateUser(name, dialog.SecondText, "Student account (created by LabLock)");
                        Accounts.AddToGroup(Accounts.UsersSid, Accounts.SidOf(name));
                        WriteLog("Created the standard account '" + name + "'. Sign in with it once, so Windows makes its profile, then run LabLock again.");
                    }
                }
                catch (Exception ex)
                {
                    Tell(ex.Message, MessageBoxIcon.Warning);
                }
                ReloadUsers(name);
                RefreshStates();
            }
        }

        private string BrowseForImage(string title)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = L.T(title);
                dialog.Filter = L.T("Images") + " (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|" + L.T("All files") + " (*.*)|*.*";
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private void OnBrowseWallpaperClick(object sender, EventArgs e)
        {
            string file = BrowseForImage("Choose the wallpaper image");
            if (file != null) inWallpaper.Text = file;
        }

        private void OnBrowseSchoolClick(object sender, EventArgs e)
        {
            string file = BrowseForImage("Choose the school image (logo or photo)");
            if (file == null) return;
            inSchool.Text = file;
            LoadAboutPicture();
        }

        private void OnEditBlocklistClick(object sender, EventArgs e)
        {
            if (!Blocklist.EnsureFile())
            {
                Tell("The blocklist file could not be created next to LabLock.exe (is the USB stick write-protected?).\r\n\r\nThe built-in starter list is used instead.", MessageBoxIcon.Warning);
                return;
            }
            try
            {
                Process.Start(Proc.SystemTool("notepad.exe"), "\"" + Blocklist.FilePath + "\"");
                lblHint.Text = L.T("Save the file in Notepad, then press Refresh. The new list is used the next time you apply the blocklist setting.");
            }
            catch (Exception ex)
            {
                Tell("Notepad could not be started: " + ex.Message, MessageBoxIcon.Warning);
            }
        }
    }
}
