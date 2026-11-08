using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LabLock
{
    /// <summary>
    /// The window. It is built in code, there is no designer file, and it is split over three files:
    ///   MainForm.cs           the frame: header, page names, theme, language, log
    ///   MainForm.Lockdown.cs  the Lockdown page (accounts, this PC, options, checklist, Apply / Undo)
    ///   MainForm.Pages.cs     the other pages (This PC, Startup, Health check, Help, About)
    /// Every text passes through L.T(), which turns it into Greek when Greek is chosen.
    /// Colours are never set directly: every control gets them from the current Theme in ApplyTheme.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Code")]
    internal sealed partial class MainForm : Form
    {
        private const string AppTitle = "LabLock";

        // Roles stored in Control.Tag; ApplyTheme uses them to pick colours.
        private const string RoleCard = "card";
        private const string RoleGroup = "group";
        private const string RoleSubtle = "subtle";
        private const string RolePrimary = "primary";

        private const int PageLock = 0;
        private const int PagePc = 1;
        private const int PageBoards = 2;
        private const int PageStartup = 3;
        private const int PageHealth = 4;
        private const int PageHelp = 5;
        private const int PageAbout = 6;
        private static readonly string[] PageNames = { "Lockdown", "This PC", "Boards", "Startup", "Health check", "Help", "About" };
        private static readonly int[] PageNameWidths = { 104, 104, 96, 96, 128, 84, 84 };

        private const AnchorStyles AnchorAll = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        private const AnchorStyles AnchorTopWide = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        private const AnchorStyles AnchorTopRight = AnchorStyles.Top | AnchorStyles.Right;
        private const AnchorStyles AnchorBottomWide = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        private const AnchorStyles AnchorBottomLeft = AnchorStyles.Bottom | AnchorStyles.Left;

        private readonly AppConfig config = AppConfig.Load();
        private Theme theme;
        private Font groupFont;
        private int currentPage;

        private Label lblPc;
        private Picker pickMode;
        private Picker pickAccent;
        private Picker pickLanguage;
        private Button btnRefresh;
        private readonly Panel[] pages = new Panel[PageNames.Length];
        private readonly NavButton[] navButtons = new NavButton[PageNames.Length];

        public MainForm()
        {
            theme = Theme.Create(config.Get("theme", "Dark"), config.Get("accent", "Lime"));
            BuildUi();
            ApplyTheme();
        }

        // ------------------------------------------------------------------ small helpers used by every page

        private Label AddLabel(Control parent, string text, int x, int y, int width, string role)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.UseMnemonic = false;
            label.Text = L.T(text);
            label.Tag = role;
            label.SetBounds(x, y, width, 18);
            if (role == RoleGroup) label.Font = groupFont;
            parent.Controls.Add(label);
            return label;
        }

        private static InputBox AddInput(Control parent, int x, int y, int width)
        {
            InputBox box = new InputBox();
            box.SetBounds(x, y, width, 28);
            parent.Controls.Add(box);
            return box;
        }

        private static Button AddButton(Control parent, string text, int x, int y, int width, int height, string role, EventHandler onClick)
        {
            Button button = new Button();
            button.Text = L.T(text);
            button.Tag = role;
            button.UseMnemonic = false;
            button.FlatStyle = FlatStyle.Flat;
            button.SetBounds(x, y, width, height);
            button.Click += onClick;
            parent.Controls.Add(button);
            return button;
        }

        private static Panel AddCard(Control parent, int x, int y, int width, int height)
        {
            Panel card = new Panel();
            card.Tag = RoleCard;
            card.SetBounds(x, y, width, height);
            parent.Controls.Add(card);
            return card;
        }

        private void Tell(string text, MessageBoxIcon icon)
        {
            MessageBox.Show(this, L.T(text), AppTitle, MessageBoxButtons.OK, icon);
        }

        private bool Confirm(string text, MessageBoxIcon icon)
        {
            return MessageBox.Show(this, L.T(text), AppTitle, MessageBoxButtons.YesNo, icon) == DialogResult.Yes;
        }

        private void WriteLog(string message)
        {
            string text = L.T(message);
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
            txtLog.Update();
            LogFile.Write(text);
        }

        internal static string WindowsName()
        {
            try
            {
                using (RegistryKey machine = Reg.Machine())
                using (RegistryKey key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (key == null) return "";
                    string product = Convert.ToString(key.GetValue("ProductName"));
                    string version = Convert.ToString(key.GetValue("DisplayVersion"));
                    int build;
                    if (int.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")), out build) && build >= 22000)
                    {
                        // Windows 11 still calls itself "Windows 10" in this registry value.
                        product = product.Replace("Windows 10", "Windows 11");
                    }
                    return (product + " " + version).Trim();
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>Makes the rows of a scrolling list as wide as the list, always leaving room for the scroll bar.</summary>
        private static void FitRows(Panel list)
        {
            int width = Math.Max(100, list.Width - SystemInformation.VerticalScrollBarWidth - 2);
            foreach (Control child in list.Controls)
            {
                if (child is ListRow && child.Width != width) child.Width = width;
            }
        }

        private void OnListResize(object sender, EventArgs e)
        {
            FitRows((Panel)sender);
        }

        /// <summary>Empties a scrolling list before it is filled again.</summary>
        private static void ClearList(Panel list)
        {
            list.AutoScrollPosition = new Point(0, 0);
            while (list.Controls.Count > 0)
            {
                Control old = list.Controls[0];
                list.Controls.RemoveAt(0);
                old.Dispose();
            }
        }

        // ------------------------------------------------------------------ the frame

        private void BuildUi()
        {
            SuspendLayout();

            Text = AppTitle + " - " + L.T("school lab lockdown");
            Font = new Font("Segoe UI", 9F);
            groupFont = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 680);
            MinimumSize = new Size(960, 712);
            StartPosition = FormStartPosition.CenterScreen;
            try
            {
                // the icon that is built into LabLock.exe (app.ico)
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception)
            {
            }

            // ---- header: logo, name, this PC, theme, language
            Panel bar = AddCard(this, 0, 0, 1000, 60);
            bar.Anchor = AnchorTopWide;

            LogoBox logo = new LogoBox();
            logo.SetBounds(16, 10, 40, 40);
            bar.Controls.Add(logo);

            Label title = AddLabel(bar, AppTitle, 64, 6, 300, null);
            title.Font = new Font("Segoe UI Semibold", 15F);
            title.Height = 30;
            lblPc = AddLabel(bar, "", 66, 36, 440, RoleSubtle);

            pickMode = new Picker();
            pickMode.SetBounds(658, 16, 96, 28);
            pickMode.Anchor = AnchorTopRight;
            foreach (string name in Theme.ModeNames) pickMode.AddItem(name);
            pickMode.SelectedIndex = theme.Dark ? 0 : 1;
            pickMode.SelectionChanged += OnThemeChanged;
            bar.Controls.Add(pickMode);

            pickAccent = new Picker();
            pickAccent.SetBounds(762, 16, 110, 28);
            pickAccent.Anchor = AnchorTopRight;
            int accentIndex = -1;
            for (int i = 0; i < Theme.AccentNames.Length; i++)
            {
                pickAccent.AddItem(Theme.AccentNames[i]);
                if (Theme.AccentNames[i] == theme.AccentName) accentIndex = i;
            }
            if (accentIndex < 0)
            {
                // a custom colour (#RRGGBB) typed into LabLock.ini
                pickAccent.AddItem(theme.AccentName);
                accentIndex = pickAccent.Count - 1;
            }
            pickAccent.SelectedIndex = accentIndex;
            pickAccent.SelectionChanged += OnThemeChanged;
            bar.Controls.Add(pickAccent);

            pickLanguage = new Picker();
            pickLanguage.SetBounds(880, 16, 104, 28);
            pickLanguage.Anchor = AnchorTopRight;
            pickLanguage.AddItem("English");
            pickLanguage.AddItem(GreekText.LanguageName);
            pickLanguage.SelectedIndex = L.Greek ? 1 : 0;
            pickLanguage.SelectionChanged += OnLanguageChanged;
            bar.Controls.Add(pickLanguage);

            // ---- the page names
            Panel nav = new Panel();
            nav.SetBounds(0, 60, 1000, 32);
            nav.Anchor = AnchorTopWide;
            Controls.Add(nav);

            int x = 12;
            for (int i = 0; i < PageNames.Length; i++)
            {
                NavButton button = new NavButton();
                button.Text = L.T(PageNames[i]);
                button.Tag = i;
                button.SetBounds(x, 0, PageNameWidths[i], 32);
                button.Click += OnNavClick;
                nav.Controls.Add(button);
                navButtons[i] = button;
                x += PageNameWidths[i] + 4;
            }

            btnRefresh = AddButton(nav, "Refresh", 884, 2, 100, 28, null, OnRefreshClick);
            btnRefresh.Anchor = AnchorTopRight;

            // ---- the pages: all in the same place, one visible at a time
            for (int i = 0; i < pages.Length; i++)
            {
                Panel page = new Panel();
                page.SetBounds(0, 92, 1000, 588);
                page.Anchor = AnchorAll;
                page.Visible = false;
                Controls.Add(page);
                pages[i] = page;
            }
            BuildLockdownPage(pages[PageLock]);
            BuildPcPage(pages[PagePc]);
            BuildBoardsPage(pages[PageBoards]);
            BuildStartupPage(pages[PageStartup]);
            BuildHealthPage(pages[PageHealth]);
            BuildHelpPage(pages[PageHelp]);
            BuildAboutPage(pages[PageAbout]);

            ResumeLayout(false);
            PerformLayout();
            ShowPage(PageLock);
        }

        private void ShowPage(int index)
        {
            currentPage = index;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = i == index;
                navButtons[i].Selected = i == index;
            }
            btnRefresh.Visible = index <= PageHealth;

            if (!IsHandleCreated) return;   // the first page is filled in OnShown
            if (index == PagePc) LoadPcPage();
            if (index == PageBoards) ShowBoardState();
            if (index == PageStartup && !startupScanned) ScanStartup();
        }

        private void OnNavClick(object sender, EventArgs e)
        {
            ShowPage((int)((Control)sender).Tag);
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (currentPage == PageLock)
            {
                ReloadUsers(SelectedStudentName());
                RefreshStates();
            }
            else if (currentPage == PagePc)
            {
                LoadPcPage();
            }
            else if (currentPage == PageBoards)
            {
                ShowBoardState();
            }
            else if (currentPage == PageStartup)
            {
                ScanStartup();
            }
            else if (currentPage == PageHealth)
            {
                RunHealthCheck();
            }
        }

        // ------------------------------------------------------------------ theme and language

        private void ApplyTheme()
        {
            BackColor = theme.Window;
            ForeColor = theme.Text;
            ThemeChildren(this, theme.Window);

            if (IsHandleCreated)
            {
                Native.TitleBar(Handle, theme.Dark);
                ThemeScrollBars(this);
            }
            Invalidate(true);
        }

        private void ThemeChildren(Control parent, Color parentBack)
        {
            foreach (Control child in parent.Controls)
            {
                IThemed themed = child as IThemed;
                if (themed != null)
                {
                    child.BackColor = parentBack;   // the custom controls choose their own colours next
                    themed.ApplyTheme(theme);
                    continue;
                }

                string role = child.Tag as string;
                if (child is Label)
                {
                    child.BackColor = parentBack;
                    child.ForeColor = role == RoleGroup ? theme.AccentInk : (role == RoleSubtle ? theme.Subtle : theme.Text);
                }
                else if (child is Button)
                {
                    Styling.StyleButton((Button)child, theme, role == RolePrimary);
                }
                else if (child is Panel)
                {
                    Color back = role == RoleCard ? theme.Surface : parentBack;
                    child.BackColor = back;
                    child.ForeColor = theme.Text;
                    ThemeChildren(child, back);
                }
                else
                {
                    child.BackColor = parentBack;
                    child.ForeColor = theme.Text;
                }
            }
        }

        /// <summary>Scroll bars are drawn by Windows; this asks it for dark or light ones on every list and text box.</summary>
        private void ThemeScrollBars(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is TextBox || (child is Panel && ((Panel)child).AutoScroll))
                {
                    Native.ScrollBars(child, theme.Dark);
                }
                if (child.Controls.Count > 0) ThemeScrollBars(child);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.TitleBar(Handle, theme.Dark);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            theme = Theme.Create(Convert.ToString(pickMode.SelectedItem), Convert.ToString(pickAccent.SelectedItem));
            ApplyTheme();

            // Windows repaints the title bar in its new colour only when the window frame changes.
            if (WindowState == FormWindowState.Normal)
            {
                Size current = Size;
                Size = new Size(current.Width + 1, current.Height);
                Size = current;
            }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            bool greek = pickLanguage.SelectedIndex == 1;
            if (greek == L.Greek) return;
            config.Set("language", greek ? "el" : "en");
            SaveConfig();
            // The texts are placed when the window is built, so the simplest sure way is to build it again.
            Application.Restart();
        }

        // ------------------------------------------------------------------ start and end

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyTheme();   // once more, now that the scroll bars exist
            lblPc.Text = L.T("This PC: ") + ComputerName.Active + "      " + WindowsName();
            Blocklist.EnsureFile();
            FillThisPc();
            ReloadUsers(config.Get("student", "Student"));
            RefreshStates();
            WriteLog("Ready. Nothing is changed until you press Apply.");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveConfig();
            base.OnFormClosing(e);
        }
    }
}
