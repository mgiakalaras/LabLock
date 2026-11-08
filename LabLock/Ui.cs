using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LabLock
{
    /// <summary>The colours of the window: a mode (dark or light) plus one accent colour.</summary>
    internal sealed class Theme
    {
        public static readonly string[] ModeNames = { "Dark", "Light" };

        // To add an accent colour, add a name here and its colour (RRGGBB) at the same position below.
        public static readonly string[] AccentNames = { "Lime", "Green", "Teal", "Blue", "Purple", "Red", "Orange" };
        private static readonly int[] AccentValues = { 0xA3E635, 0x22C55E, 0x14B8A6, 0x3B82F6, 0x8B5CF6, 0xEF4444, 0xF59E0B };

        public bool Dark;
        public string ModeName;
        public string AccentName;

        public Color Window;      // behind everything
        public Color Surface;     // cards and the checklist
        public Color Input;       // text boxes and pickers
        public Color Hover;       // row or button under the mouse
        public Color Border;
        public Color Text;
        public Color Subtle;      // secondary text
        public Color Accent;      // ticks and the main button
        public Color AccentText;  // text on top of the accent colour
        public Color AccentInk;   // accent used as text on a card
        public Color Good;
        public Color Warn;

        private static Color Rgb(int value)
        {
            return Color.FromArgb(255, (value >> 16) & 255, (value >> 8) & 255, value & 255);
        }

        public static Color Mix(Color from, Color to, double amount)
        {
            int r = (int)Math.Round(from.R + (to.R - from.R) * amount);
            int g = (int)Math.Round(from.G + (to.G - from.G) * amount);
            int b = (int)Math.Round(from.B + (to.B - from.B) * amount);
            return Color.FromArgb(255, Math.Max(0, Math.Min(255, r)), Math.Max(0, Math.Min(255, g)), Math.Max(0, Math.Min(255, b)));
        }

        /// <summary>Accepts one of the accent names or a custom colour written as #RRGGBB.</summary>
        private static Color ResolveAccent(string wanted, out string name)
        {
            if (!string.IsNullOrEmpty(wanted))
            {
                for (int i = 0; i < AccentNames.Length; i++)
                {
                    if (string.Equals(AccentNames[i], wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        name = AccentNames[i];
                        return Rgb(AccentValues[i]);
                    }
                }

                int custom;
                if (wanted.Length == 7 && wanted[0] == '#' &&
                    int.TryParse(wanted.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out custom))
                {
                    name = wanted.ToUpperInvariant();
                    return Rgb(custom);
                }
            }
            name = AccentNames[0];
            return Rgb(AccentValues[0]);
        }

        public static Theme Create(string mode, string accent)
        {
            Theme t = new Theme();
            t.Dark = !string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase);
            t.ModeName = t.Dark ? "Dark" : "Light";

            string accentName;
            t.Accent = ResolveAccent(accent, out accentName);
            t.AccentName = accentName;

            double brightness = 0.299 * t.Accent.R + 0.587 * t.Accent.G + 0.114 * t.Accent.B;
            t.AccentText = brightness > 150 ? Rgb(0x111318) : Color.White;

            if (t.Dark)
            {
                t.Window = Rgb(0x15171B);
                t.Surface = Rgb(0x1E2127);
                t.Input = Rgb(0x282C34);
                t.Border = Rgb(0x3A3F4B);
                t.Text = Rgb(0xE8EAED);
                t.Subtle = Rgb(0x9AA1AD);
                t.Good = Rgb(0x4ADE80);
                t.Warn = Rgb(0xFBBF24);
                t.AccentInk = t.Accent;
            }
            else
            {
                t.Window = Rgb(0xF0F2F5);
                t.Surface = Rgb(0xFFFFFF);
                t.Input = Rgb(0xFFFFFF);
                t.Border = Rgb(0xCDD2DA);
                t.Text = Rgb(0x1F2328);
                t.Subtle = Rgb(0x667085);
                t.Good = Rgb(0x15803D);
                t.Warn = Rgb(0xB45309);
                t.AccentInk = Mix(t.Accent, Color.Black, 0.40);   // bright accents are unreadable as text on white
            }
            t.Hover = Mix(t.Surface, t.Text, 0.08);
            return t;
        }
    }

    /// <summary>A control that knows how to colour itself.</summary>
    internal interface IThemed
    {
        void ApplyTheme(Theme theme);
    }

    internal static class Native
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        /// <summary>Dark or light window title bar (Windows 10 1809 and later; silently ignored on older versions).</summary>
        public static void TitleBar(IntPtr handle, bool dark)
        {
            try
            {
                int value = dark ? 1 : 0;
                if (DwmSetWindowAttribute(handle, 20, ref value, 4) != 0)
                {
                    DwmSetWindowAttribute(handle, 19, ref value, 4);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Dark or light scroll bars for one control.</summary>
        public static void ScrollBars(Control control, bool dark)
        {
            try
            {
                if (control.IsHandleCreated)
                {
                    SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Colours for the drop-down menu of a Picker.</summary>
    internal sealed class MenuColors : ProfessionalColorTable
    {
        private readonly Theme theme;

        public MenuColors(Theme theme)
        {
            this.theme = theme;
        }

        public override Color ToolStripDropDownBackground { get { return theme.Input; } }
        public override Color ImageMarginGradientBegin { get { return theme.Input; } }
        public override Color ImageMarginGradientMiddle { get { return theme.Input; } }
        public override Color ImageMarginGradientEnd { get { return theme.Input; } }
        public override Color MenuBorder { get { return theme.Border; } }
        public override Color MenuItemBorder { get { return theme.Accent; } }
        public override Color MenuItemSelected { get { return theme.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return theme.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return theme.Hover; } }
    }

    /// <summary>A flat text box with a one-pixel border that takes the accent colour while typing.</summary>
    internal sealed class InputBox : Control, IThemed
    {
        private readonly TextBox inner = new TextBox();
        private Color border = Color.Gray;
        private Color focusBorder = Color.DodgerBlue;

        public InputBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;

            inner.BorderStyle = BorderStyle.None;
            inner.GotFocus += OnInnerFocusChanged;
            inner.LostFocus += OnInnerFocusChanged;
            Controls.Add(inner);

            Size = new Size(160, 28);
            PlaceInner();
        }

        public override string Text
        {
            get { return inner.Text; }
            set { inner.Text = value; }
        }

        public bool Password
        {
            get { return inner.UseSystemPasswordChar; }
            set { inner.UseSystemPasswordChar = value; }
        }

        public void Clear()
        {
            inner.Clear();
        }

        private void OnInnerFocusChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void PlaceInner()
        {
            int height = inner.PreferredHeight;
            inner.SetBounds(8, Math.Max(1, (Height - height) / 2), Math.Max(10, Width - 16), height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PlaceInner();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            inner.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            using (Pen pen = new Pen(inner.Focused ? focusBorder : border))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        public void ApplyTheme(Theme theme)
        {
            BackColor = theme.Input;
            inner.BackColor = theme.Input;
            inner.ForeColor = theme.Text;
            border = theme.Border;
            focusBorder = theme.Accent;
            Invalidate();
        }
    }

    /// <summary>A flat drop-down list: a button that opens a menu with the choices.</summary>
    internal sealed class Picker : Button, IThemed
    {
        private readonly List<object> items = new List<object>();
        private int selectedIndex = -1;
        private Theme theme;
        private ContextMenuStrip menu;

        public string EmptyText = "(choose)";

        /// <summary>Raised when the user picks another item (not when the program sets SelectedIndex).</summary>
        public event EventHandler SelectionChanged;

        public Picker()
        {
            FlatStyle = FlatStyle.Flat;
            UseMnemonic = false;
            AutoEllipsis = true;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(4, 0, 22, 0);
            Size = new Size(160, 28);
            Text = EmptyText;
        }

        public int Count
        {
            get { return items.Count; }
        }

        public object ItemAt(int index)
        {
            return items[index];
        }

        public void ClearItems()
        {
            items.Clear();
            selectedIndex = -1;
            ShowSelection();
        }

        public void AddItem(object item)
        {
            items.Add(item);
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                selectedIndex = (value < 0 || value >= items.Count) ? -1 : value;
                ShowSelection();
            }
        }

        public object SelectedItem
        {
            get { return selectedIndex < 0 ? null : items[selectedIndex]; }
        }

        private void ShowSelection()
        {
            Text = selectedIndex < 0 ? EmptyText : L.T(Convert.ToString(items[selectedIndex]));
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (items.Count == 0) return;

            if (menu != null) menu.Dispose();
            menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Font = Font;
            if (theme != null)
            {
                menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors(theme));
                menu.BackColor = theme.Input;
                menu.ForeColor = theme.Text;
            }

            for (int i = 0; i < items.Count; i++)
            {
                ToolStripMenuItem entry = new ToolStripMenuItem(L.T(Convert.ToString(items[i])).Replace("&", "&&"));
                entry.Tag = i;
                if (theme != null)
                {
                    entry.ForeColor = i == selectedIndex ? theme.AccentInk : theme.Text;
                }
                entry.Click += OnMenuEntryClick;
                menu.Items.Add(entry);
            }
            menu.Show(this, new Point(0, Height));
        }

        private void OnMenuEntryClick(object sender, EventArgs e)
        {
            int index = (int)((ToolStripMenuItem)sender).Tag;
            if (index == selectedIndex) return;
            selectedIndex = index;
            ShowSelection();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // the small arrow on the right
            int x = Width - 15;
            int y = Height / 2 - 1;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(ForeColor, 1.6F))
            {
                e.Graphics.DrawLines(pen, new Point[] { new Point(x - 4, y - 2), new Point(x, y + 2), new Point(x + 4, y - 2) });
            }
        }

        public void ApplyTheme(Theme newTheme)
        {
            theme = newTheme;
            UseVisualStyleBackColor = false;
            BackColor = theme.Input;
            ForeColor = theme.Text;
            FlatAppearance.BorderSize = 1;
            FlatAppearance.BorderColor = theme.Border;
            FlatAppearance.MouseOverBackColor = theme.Hover;
            FlatAppearance.MouseDownBackColor = theme.Hover;
            Invalidate();
        }
    }
}
