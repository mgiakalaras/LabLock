using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LabLock
{
    /// <summary>The pages next to Lockdown: This PC, Startup, Health check, Help and About.</summary>
    internal sealed partial class MainForm
    {
        // ================================================================== This PC

        private static readonly string[] HardwareLabels =
        {
            "Computer name", "Manufacturer", "Model", "Serial number", "Processor", "Memory", "Disk", "MAC address", "Windows"
        };
        private readonly Label[] hardwareValues = new Label[HardwareLabels.Length];
        private InputBox inRoom;
        private InputBox inNotes;
        private Label lblRecordInfo;
        private bool notesLoaded;

        private void BuildPcPage(Panel page)
        {
            Panel hardware = AddCard(page, 16, 6, 476, 342);
            AddLabel(hardware, "WHAT THIS PC IS MADE OF", LabelX, 10, 400, RoleGroup);
            for (int i = 0; i < HardwareLabels.Length; i++)
            {
                AddLabel(hardware, HardwareLabels[i], LabelX, 38 + i * 24, 116, RoleSubtle);
                hardwareValues[i] = AddLabel(hardware, "", 134, 38 + i * 24, 330, null);
                hardwareValues[i].AutoEllipsis = true;
            }

            Panel record = AddCard(page, 508, 6, 476, 342);
            AddLabel(record, "THE RECORD OF THE LAB'S PCS", LabelX, 10, 400, RoleGroup);
            AddLabel(record, "Room / lab", LabelX, 39, 100, RoleSubtle);
            inRoom = AddInput(record, 118, 34, 344);
            inRoom.Text = config.Get("room", "");
            AddLabel(record, "Notes for this PC", LabelX, 73, 100, RoleSubtle);
            inNotes = AddInput(record, 118, 68, 344);
            lblRecordInfo = AddLabel(record, "", LabelX, 108, 448, RoleSubtle);
            lblRecordInfo.Height = 36;

            AddButton(record, "Save this PC to the record", 14, 152, 220, 32, RolePrimary, OnSaveRecordClick);
            AddButton(record, "Open the record", 242, 152, 220, 32, null, OnOpenRecordClick);
            AddButton(record, "Export for Veyon", 14, 192, 220, 32, null, OnExportVeyonClick);
            AddButton(record, "Export for NetHawk", 242, 192, 220, 32, null, OnExportNetHawkClick);
            AddButton(record, "Wake up the PCs of the record (Wake-on-LAN)", 14, 232, 448, 32, null, OnWakeClick);
            AddButton(record, "School Network Manager...", 14, 272, 448, 32, null, OnSnmClick);

            Label info = AddLabel(page,
                "The record is the file LabLock-computers.csv next to LabLock.exe: one line per PC, kept up to date after every Apply. " +
                "A PC is recognised by its MAC address, so a new name or IP address updates its line. The name, IP address and DNS of this PC are set on the Lockdown page.",
                16, 360, 968, RoleSubtle);
            info.Height = 54;
            info.Anchor = AnchorTopWide;
        }

        /// <summary>Shows the hardware of this PC and what the record already knows about it.</summary>
        private void LoadPcPage()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                HardwareInfo hardware = Hardware.Read();
                NetInfo net = null;
                try
                {
                    net = NetConfig.Current();
                }
                catch (Exception)
                {
                }

                string[] values =
                {
                    ComputerName.Active, hardware.Manufacturer, hardware.Model, hardware.Serial, hardware.Cpu,
                    hardware.Memory, hardware.Disk, net == null ? "" : net.Mac, WindowsName()
                };
                for (int i = 0; i < values.Length; i++)
                {
                    hardwareValues[i].Text = values[i].Length > 0 ? values[i] : "-";
                }

                if (!notesLoaded)
                {
                    // what the record already says about this PC
                    string[] row = PcRecord.Find(net == null ? "" : net.Mac, ComputerName.Pending());
                    if (row != null)
                    {
                        inNotes.Text = row[PcRecord.ColumnNotes];
                        if (inRoom.Text.Trim().Length == 0) inRoom.Text = row[PcRecord.ColumnRoom];
                    }
                    notesLoaded = true;
                }
                ShowRecordCount();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ShowRecordCount()
        {
            lblRecordInfo.Text = PcRecord.Count() + L.T(" PCs are in the record so far.");
        }

        private void OnSaveRecordClick(object sender, EventArgs e)
        {
            if (!SaveRecord(inNotes.Text.Trim()))
            {
                Tell("The record of PCs could not be written. If the file is open in Excel, close it and try again.", MessageBoxIcon.Warning);
                return;
            }
            ShowRecordCount();
            WriteLog("This PC is in the record of PCs.");
        }

        private void OpenFile(string path)
        {
            try
            {
                Process.Start(path);
            }
            catch (Exception)
            {
                try
                {
                    Process.Start(Proc.SystemTool("notepad.exe"), "\"" + path + "\"");
                }
                catch (Exception ex)
                {
                    Tell("The file could not be opened: " + ex.Message, MessageBoxIcon.Warning);
                }
            }
        }

        private void OnOpenRecordClick(object sender, EventArgs e)
        {
            if (!File.Exists(PcRecord.FilePath) && !SaveRecord(inNotes.Text.Trim()))
            {
                Tell("The record of PCs could not be written. If the file is open in Excel, close it and try again.", MessageBoxIcon.Warning);
                return;
            }
            OpenFile(PcRecord.FilePath);
        }

        private void OnExportVeyonClick(object sender, EventArgs e)
        {
            try
            {
                string path = PcRecord.ExportForVeyon(inRoom.Text.Trim());
                Tell(L.T("Written: ") + path + "\r\n\r\n" +
                     L.T("On the teacher's PC, in a command prompt opened as administrator, run:") + "\r\n\r\n" +
                     "veyon-cli networkobjects import \"" + path + "\" format \"%location%;%name%;%host%;%mac%\"",
                     MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Tell(ex.Message, MessageBoxIcon.Warning);
            }
        }

        private void OnExportNetHawkClick(object sender, EventArgs e)
        {
            try
            {
                string path = PcRecord.ExportForNetHawk(inRoom.Text.Trim());
                Tell(L.T("Written: ") + path + "\r\n\r\n" +
                     L.T("It has the format of NetHawk's machines.json. With NetHawk closed, copy its entries into the file machines.json in NetHawk's data folder."),
                     MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Tell(ex.Message, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Switches on the lab PCs from this PC (the teacher's), with the MAC addresses of the record.</summary>
        private void OnWakeClick(object sender, EventArgs e)
        {
            int withMac = 0;
            foreach (string[] row in PcRecord.Rows())
            {
                if (row[PcRecord.ColumnMac].Length > 0) withMac++;
            }
            if (withMac == 0)
            {
                Tell("The record has no PC with a MAC address yet. Run LabLock once on every lab PC (Apply, or Save this PC to the record), then bring the record here.", MessageBoxIcon.Information);
                return;
            }
            if (!Confirm(L.T("Send a wake-up signal to the PCs of the record?") + " (" + withMac + ")", MessageBoxIcon.Question))
            {
                return;
            }

            int sent;
            Cursor = Cursors.WaitCursor;
            try
            {
                WriteLog("---- Wake-on-LAN ----");
                sent = WakeOnLan.WakeAll(WriteLog, config.Get("mask", "255.255.255.0"));
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            Tell(L.T("Wake-up signal sent to ") + sent + L.T(" PCs.") + "\r\n\r\n" +
                 L.T("A PC that stays off: check that Wake-on-LAN is on in its BIOS, that the setting \"Prepare this PC for Wake-on-LAN\" was applied and the PC restarted once, that it was shut down normally (not unplugged) and that this PC is in the same network."),
                 MessageBoxIcon.Information);
        }

        /// <summary>The address and key of School Network Manager, so every PC reports what LabLock locked on it.</summary>
        private void OnSnmClick(object sender, EventArgs e)
        {
            using (PromptDialog dialog = new PromptDialog(theme, "School Network Manager",
                       "Address of SNM (for example http://192.168.2.114:5055), empty = no link",
                       "Agent key (SNM: Tools > Computer agent > Show)"))
            {
                dialog.Fill(config.Get("snm", ""), config.Get("snmkey", ""));
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string url = dialog.FirstText;
                string key = dialog.SecondText.Trim();
                if (url.Length == 0)
                {
                    config.Set("snm", "");
                    config.Set("snmkey", "");
                    config.Save();
                    Tell("The link to School Network Manager is off.", MessageBoxIcon.Information);
                    return;
                }

                string problem;
                Cursor = Cursors.WaitCursor;
                try
                {
                    problem = Snm.Test(url, key);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
                if (problem != null)
                {
                    Tell(problem, MessageBoxIcon.Warning);
                    return;
                }

                config.Set("snm", url);
                config.Set("snmkey", key);
                config.Save();
                WriteLog("---- School Network Manager ----");
                ReportToSnm(BuildContext(), "check");
                Tell("Linked. From now on every Apply, Undo and Restore on this PC is shown in School Network Manager, on the page of this PC. LabLock.ini keeps the address and key, so the other PCs get them from the USB stick.", MessageBoxIcon.Information);
            }
        }

        // ================================================================== Startup

        private Panel startupPanel;
        private Label lblStartupStatus;
        private bool startupScanned;

        private void BuildStartupPage(Panel page)
        {
            Label intro = AddLabel(page,
                "These programs start by themselves when somebody signs in. Untick the ones that should not start and press Save changes. " +
                "Nothing is deleted or uninstalled: tick a line again to bring it back. It is the same switch Task Manager shows.",
                16, 6, 968, RoleSubtle);
            intro.Height = 36;
            intro.Anchor = AnchorTopWide;

            startupPanel = AddCard(page, 16, 48, 968, 478);
            startupPanel.Anchor = AnchorAll;
            startupPanel.AutoScroll = true;
            startupPanel.Resize += OnListResize;

            Button save = AddButton(page, "Save changes", 16, 536, 180, 36, RolePrimary, OnSaveStartupClick);
            save.Anchor = AnchorBottomLeft;
            lblStartupStatus = AddLabel(page, "", 210, 545, 770, RoleSubtle);
            lblStartupStatus.Anchor = AnchorBottomWide;
        }

        /// <summary>A context with the accounts and the chosen student, for the pages that only read.</summary>
        private RunContext ReadingContext()
        {
            RunContext c = BuildContext();
            c.AdminPassword = "";
            return c;
        }

        private void ScanStartup()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                RunContext c = ReadingContext();
                List<StartupEntry> entries;
                StudentHive hive = Engine.OpenHive(c);
                try
                {
                    entries = StartupScan.Scan(c);
                }
                finally
                {
                    Engine.CloseHive(hive, c);
                }

                ClearList(startupPanel);
                int y = 4;
                string source = null;
                foreach (StartupEntry entry in entries)
                {
                    if (entry.Source != source)
                    {
                        source = entry.Source;
                        AddLabel(startupPanel, source.ToUpperInvariant(), 14, y + 12, 900, RoleGroup);
                        y += 36;
                    }
                    ListRow row = new ListRow(true);
                    row.Data = entry;
                    row.Title = entry.Name;
                    row.SetDetail(entry.Command, ListRow.Plain);
                    row.Checked = entry.Enabled;
                    row.SetBounds(0, y, 900, ListRow.RowHeight);
                    startupPanel.Controls.Add(row);
                    y += ListRow.RowHeight;
                }
                Label spacer = AddLabel(startupPanel, "", 0, y, 10, null);
                spacer.Height = 8;

                FitRows(startupPanel);
                ThemeChildren(startupPanel, theme.Surface);
                startupScanned = true;

                string status = entries.Count + L.T(" startup programs found.");
                if (c.Student == null) status += " " + L.T("Choose the student account on the Lockdown page to see the student's own ones too.");
                else if (c.HiveProblem != null) status += " " + L.T(c.HiveProblem);
                lblStartupStatus.Text = status;
            }
            catch (Exception ex)
            {
                lblStartupStatus.Text = L.T("The startup programs could not be read: ") + ex.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void OnSaveStartupClick(object sender, EventArgs e)
        {
            List<ListRow> changed = new List<ListRow>();
            foreach (Control child in startupPanel.Controls)
            {
                ListRow row = child as ListRow;
                if (row != null && row.Checked != ((StartupEntry)row.Data).Enabled) changed.Add(row);
            }
            if (changed.Count == 0)
            {
                Tell("Nothing was changed. Tick or untick a program first.", MessageBoxIcon.Information);
                return;
            }

            int done = 0;
            int failed = 0;
            Cursor = Cursors.WaitCursor;
            try
            {
                RunContext c = ReadingContext();
                StudentHive hive = Engine.OpenHive(c);
                try
                {
                    foreach (ListRow row in changed)
                    {
                        StartupEntry entry = (StartupEntry)row.Data;
                        try
                        {
                            StartupScan.SetEnabled(c, entry, row.Checked);
                            WriteLog((row.Checked ? "Startup switched ON:  " : "Startup switched OFF: ") + entry.Name + "  (" + entry.Source + ")");
                            done++;
                        }
                        catch (Exception ex)
                        {
                            WriteLog("FAILED  Startup program " + entry.Name + "  ->  " + ex.Message);
                            failed++;
                        }
                    }
                }
                finally
                {
                    Engine.CloseHive(hive, c);
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            ScanStartup();
            lblStartupStatus.Text = done + L.T(" changed, ") + failed + L.T(" failed.") + " " + L.T("The change counts from the next sign-in.");
        }

        // ================================================================== Health check

        private Panel healthPanel;
        private Label lblHealthStatus;
        private List<HealthItem> lastHealth;

        private void BuildHealthPage(Panel page)
        {
            Label intro = AddLabel(page,
                "A look at the things that usually go wrong on a lab PC. It only reads: nothing is changed. " +
                "Orange lines deserve a look, green ones are fine.",
                16, 6, 968, RoleSubtle);
            intro.Height = 36;
            intro.Anchor = AnchorTopWide;

            healthPanel = AddCard(page, 16, 48, 968, 478);
            healthPanel.Anchor = AnchorAll;
            healthPanel.AutoScroll = true;
            healthPanel.Resize += OnListResize;

            Button run = AddButton(page, "Run the check", 16, 536, 180, 36, RolePrimary, OnRunHealthClick);
            run.Anchor = AnchorBottomLeft;
            Button save = AddButton(page, "Save the report", 204, 536, 180, 36, null, OnSaveHealthClick);
            save.Anchor = AnchorBottomLeft;
            lblHealthStatus = AddLabel(page, "Press Run the check. It takes a few seconds.", 398, 545, 580, RoleSubtle);
            lblHealthStatus.Anchor = AnchorBottomWide;
        }

        private void OnRunHealthClick(object sender, EventArgs e)
        {
            RunHealthCheck();
        }

        private void RunHealthCheck()
        {
            Cursor = Cursors.WaitCursor;
            lblHealthStatus.Text = L.T("Checking...");
            lblHealthStatus.Update();
            try
            {
                ReloadUsers(SelectedStudentName());
                List<HealthItem> items = Health.Run(ReadingContext());
                lastHealth = items;

                ClearList(healthPanel);
                int y = 6;
                int warnings = 0;
                // the lines that deserve a look first
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (HealthItem item in items)
                    {
                        bool warning = item.Kind == HealthItem.Warning;
                        if (warning != (pass == 0)) continue;
                        if (warning) warnings++;

                        ListRow row = new ListRow(false);
                        row.Title = item.Title;
                        row.SetDetail(item.Detail, warning ? ListRow.Warning : (item.Kind == HealthItem.Good ? ListRow.Good : ListRow.Plain));
                        row.SetBounds(0, y, 900, ListRow.RowHeight);
                        healthPanel.Controls.Add(row);
                        y += ListRow.RowHeight;
                    }
                }
                Label spacer = AddLabel(healthPanel, "", 0, y, 10, null);
                spacer.Height = 8;

                FitRows(healthPanel);
                ThemeChildren(healthPanel, theme.Surface);
                lblHealthStatus.Text = warnings == 0 ? L.T("Nothing needs attention.") : warnings + L.T(" lines deserve a look.");
            }
            catch (Exception ex)
            {
                lblHealthStatus.Text = L.T("The check could not be done: ") + ex.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void OnSaveHealthClick(object sender, EventArgs e)
        {
            if (lastHealth == null)
            {
                Tell("Run the check first.", MessageBoxIcon.Information);
                return;
            }
            try
            {
                string path = Path.Combine(AppConfig.BaseDir, "LabLock-health-" + ComputerName.Active + ".txt");
                File.WriteAllText(path, Health.Report(lastHealth), new UTF8Encoding(true));
                OpenFile(path);
            }
            catch (Exception ex)
            {
                Tell("The report could not be saved: " + ex.Message, MessageBoxIcon.Warning);
            }
        }

        // ================================================================== Help

        private void BuildHelpPage(Panel page)
        {
            Panel card = AddCard(page, 16, 6, 968, 574);
            card.Anchor = AnchorAll;

            TextBox text = new TextBox();
            text.Multiline = true;
            text.ReadOnly = true;
            text.BorderStyle = BorderStyle.None;
            text.ScrollBars = ScrollBars.Vertical;
            text.Font = new Font("Segoe UI", 9.75F);
            text.SetBounds(16, 12, 944, 508);
            text.Anchor = AnchorAll;
            text.Text = Texts.Help.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            text.Select(0, 0);
            card.Controls.Add(text);

            // the script for doing many PCs at once travels inside the program; this puts it on disk
            Button push = AddButton(card, "Save Push-LabLock.ps1 next to LabLock.exe", 16, 530, 340, 32, null, OnSavePushClick);
            push.Anchor = AnchorBottomLeft;
        }

        private void OnSavePushClick(object sender, EventArgs e)
        {
            try
            {
                string path = Path.Combine(AppConfig.BaseDir, "Push-LabLock.ps1");
                if (Embedded.SaveTo(Embedded.PushScript, path))
                {
                    Tell(L.T("Written: ") + path, MessageBoxIcon.Information);
                }
                else
                {
                    Tell("This copy of LabLock was built without the push script.", MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Tell(ex.Message, MessageBoxIcon.Warning);
            }
        }

        // ================================================================== About

        private PictureBox aboutPicture;

        private void BuildAboutPage(Panel page)
        {
            Panel card = AddCard(page, 16, 6, 968, 574);
            card.Anchor = AnchorAll;

            LogoBox logo = new LogoBox();
            logo.SetBounds(24, 22, 72, 72);
            card.Controls.Add(logo);

            Label name = AddLabel(card, AppTitle, 112, 20, 520, null);
            name.Font = new Font("Segoe UI Semibold", 22F);
            name.Height = 44;
            AddLabel(card, L.T("Version ") + Application.ProductVersion + "  -  " + L.T("school lab lockdown"), 116, 66, 520, RoleSubtle);

            Label about = AddLabel(card, Texts.AboutApp, 24, 112, 640, null);
            about.Height = 76;

            AddLabel(card, "CREATED BY", 24, 200, 400, RoleGroup);
            Label author = AddLabel(card, Texts.AuthorName, 24, 224, 640, null);
            author.Font = new Font("Segoe UI Semibold", 11.25F);
            author.Height = 24;
            Label role = AddLabel(card, Texts.AuthorRole, 24, 250, 640, RoleSubtle);
            role.Height = 36;
            Label bio = AddLabel(card, Texts.AuthorBio, 24, 292, 640, null);
            bio.Height = 96;

            AddLabel(card, "SUPPORT THE PROJECT", 24, 400, 400, RoleGroup);
            Label support = AddLabel(card, Texts.Support, 24, 424, 640, RoleSubtle);
            support.Height = 54;

            Button github = AddButton(card, "GitHub profile", 24, 524, 150, 32, RolePrimary, OnGitHubClick);
            github.Anchor = AnchorBottomLeft;
            Button folder = AddButton(card, "Open the LabLock folder", 182, 524, 250, 32, null, OnOpenFolderClick);
            folder.Anchor = AnchorBottomLeft;

            // the school image, the one used for the account picture and the lock screen
            aboutPicture = new PictureBox();
            aboutPicture.SizeMode = PictureBoxSizeMode.Zoom;
            aboutPicture.SetBounds(704, 22, 240, 240);
            aboutPicture.Anchor = AnchorTopRight;
            card.Controls.Add(aboutPicture);
            Label caption = AddLabel(card, "The school image (change it on the Lockdown page)", 664, 268, 290, RoleSubtle);
            caption.TextAlign = ContentAlignment.TopCenter;
            caption.Height = 36;
            caption.Anchor = AnchorTopRight;
            LoadAboutPicture();
        }

        private void LoadAboutPicture()
        {
            if (aboutPicture == null) return;
            try
            {
                Image old = aboutPicture.Image;
                aboutPicture.Image = null;
                if (old != null) old.Dispose();
                aboutPicture.Image = SchoolImage.Load(inSchool.Text.Trim());   // the chosen file, or the built-in image
            }
            catch (Exception)
            {
                // an unreadable picture only means an empty frame
            }
        }

        private void OnGitHubClick(object sender, EventArgs e)
        {
            try
            {
                Process.Start(Texts.GitHubUrl);
            }
            catch (Exception ex)
            {
                Tell(Texts.GitHubUrl + "\r\n\r\n" + ex.Message, MessageBoxIcon.Information);
            }
        }

        private void OnOpenFolderClick(object sender, EventArgs e)
        {
            OpenFile(AppConfig.BaseDir);
        }
    }
}
