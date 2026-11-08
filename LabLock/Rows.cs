using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LabLock
{
    /// <summary>Colours for ordinary buttons, shared by the main window and the small dialogs.</summary>
    internal static class Styling
    {
        public static void StyleButton(Button button, Theme theme, bool primary)
        {
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            if (primary)
            {
                button.BackColor = theme.Accent;
                button.ForeColor = theme.AccentText;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Theme.Mix(theme.Accent, theme.AccentText, 0.10);
                button.FlatAppearance.MouseDownBackColor = Theme.Mix(theme.Accent, theme.AccentText, 0.18);
            }
            else
            {
                button.BackColor = theme.Input;
                button.ForeColor = theme.Text;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = theme.Border;
                button.FlatAppearance.MouseOverBackColor = theme.Hover;
                button.FlatAppearance.MouseDownBackColor = theme.Border;
            }
        }
    }

    /// <summary>
    /// One line of a list: a title with a second, smaller line under it. With a tick box it is a line of the
    /// checklist or of the startup programs; without one it is a line of the health check, with a coloured dot.
    /// Texts are translated when they are painted, so the rest of the program only deals with English.
    /// </summary>
    internal sealed class ListRow : Control, IThemed
    {
        public const int RowHeight = 42;

        public const int Plain = 0;
        public const int Good = 1;
        public const int Warning = 2;

        /// <summary>Whatever the line stands for (a Setting, a StartupEntry...).</summary>
        public object Data;
        public string Hint = "";

        private readonly bool hasBox;
        private string title = "";
        private string detail = "";
        private int detailKind;
        private bool isChecked;
        private bool hot;
        private Theme theme;

        /// <summary>Raised when the mouse or the keyboard focus enters the row, so the window can show the hint.</summary>
        public event EventHandler HintWanted;

        public ListRow(bool withTickBox)
        {
            hasBox = withTickBox;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, withTickBox);
            TabStop = withTickBox;
            if (withTickBox) Cursor = Cursors.Hand;
            Height = RowHeight;
        }

        public string Title
        {
            get { return title; }
            set
            {
                title = value ?? "";
                Invalidate();
            }
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                isChecked = value;
                Invalidate();
            }
        }

        public void SetDetail(string text, int kind)
        {
            detail = text ?? "";
            detailKind = kind;
            Invalidate();
        }

        /// <summary>How the state of a setting is coloured: done, not done, or something to look at.</summary>
        public static int KindOfState(string state)
        {
            if (state == null) return Plain;
            if (state.StartsWith("Applied", StringComparison.Ordinal)) return Good;
            if (state.StartsWith("Not applied", StringComparison.Ordinal)) return Plain;
            return Warning;
        }

        public void ApplyTheme(Theme newTheme)
        {
            theme = newTheme;
            Invalidate();
        }

        private void RaiseHint()
        {
            if (HintWanted != null) HintWanted(this, EventArgs.Empty);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hot = true;
            Invalidate();
            RaiseHint();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hot = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!hasBox) return;
            Focus();
            if (e.Button == MouseButtons.Left) Checked = !isChecked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (hasBox && e.KeyCode == Keys.Space)
            {
                Checked = !isChecked;
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
            RaiseHint();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        private Color KindColor()
        {
            if (detailKind == Good) return theme.Good;
            if (detailKind == Warning) return theme.Warn;
            return theme.Subtle;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (theme == null)
            {
                g.Clear(SystemColors.Window);
                return;
            }
            g.Clear(hot && hasBox ? theme.Hover : theme.Surface);

            const int BoxSize = 18;
            int boxX = 14;
            int boxY = (Height - BoxSize) / 2;

            if (!hasBox)
            {
                // a coloured dot instead of a tick box
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush brush = new SolidBrush(KindColor()))
                {
                    g.FillEllipse(brush, boxX + 4, boxY + 4, 10, 10);
                }
                g.SmoothingMode = SmoothingMode.Default;
            }
            else if (isChecked)
            {
                using (SolidBrush brush = new SolidBrush(theme.Accent))
                {
                    g.FillRectangle(brush, boxX, boxY, BoxSize, BoxSize);
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(theme.AccentText, 2F))
                {
                    g.DrawLines(pen, new Point[]
                    {
                        new Point(boxX + 4, boxY + 9), new Point(boxX + 8, boxY + 13), new Point(boxX + 14, boxY + 5)
                    });
                }
                g.SmoothingMode = SmoothingMode.Default;
            }
            else
            {
                using (Pen pen = new Pen(theme.Subtle))
                {
                    g.DrawRectangle(pen, boxX, boxY, BoxSize - 1, BoxSize - 1);
                }
            }

            int textX = 44;
            int textWidth = Math.Max(10, Width - textX - 8);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, L.T(title), Font, new Rectangle(textX, 3, textWidth, 19), theme.Text, flags);
            TextRenderer.DrawText(g, L.T(detail), Font, new Rectangle(textX, 21, textWidth, 17), KindColor(), flags);

            if (hasBox && Focused)
            {
                using (SolidBrush brush = new SolidBrush(theme.Accent))
                {
                    g.FillRectangle(brush, 0, 5, 3, Height - 10);
                }
            }
        }
    }

    /// <summary>One of the page names under the header. The page that is open gets the accent colour and a line under it.</summary>
    internal sealed class NavButton : Button, IThemed
    {
        private Theme theme;
        private bool selected;

        public NavButton()
        {
            FlatStyle = FlatStyle.Flat;
            UseMnemonic = false;
            TabStop = false;
        }

        public bool Selected
        {
            get { return selected; }
            set
            {
                selected = value;
                Recolour();
            }
        }

        private void Recolour()
        {
            if (theme == null) return;
            ForeColor = selected ? theme.AccentInk : theme.Subtle;
            Invalidate();
        }

        public void ApplyTheme(Theme newTheme)
        {
            theme = newTheme;
            UseVisualStyleBackColor = false;
            BackColor = theme.Window;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = theme.Hover;
            FlatAppearance.MouseDownBackColor = theme.Hover;
            Recolour();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (theme == null || !selected) return;
            using (SolidBrush brush = new SolidBrush(theme.Accent))
            {
                e.Graphics.FillRectangle(brush, 6, Height - 3, Width - 12, 3);
            }
        }
    }

    /// <summary>A small window that asks for one or two pieces of text (a name, and optionally a password).</summary>
    internal sealed class PromptDialog : Form
    {
        private readonly InputBox first = new InputBox();
        private readonly InputBox second = new InputBox();
        private readonly bool dark;

        public PromptDialog(Theme theme, string title, string firstLabel, string secondLabel)
        {
            dark = theme.Dark;
            bool two = secondLabel != null;

            Text = L.T(title);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, two ? 190 : 130);
            BackColor = theme.Window;
            ForeColor = theme.Text;

            AddLabel(theme, firstLabel, 14);
            first.SetBounds(16, 36, 348, 28);
            first.ApplyTheme(theme);
            Controls.Add(first);

            if (two)
            {
                AddLabel(theme, secondLabel, 74);
                second.SetBounds(16, 96, 348, 28);
                second.Password = true;
                second.ApplyTheme(theme);
                Controls.Add(second);
            }

            int buttonY = two ? 144 : 84;
            Button ok = new Button();
            ok.Text = L.T("OK");
            ok.SetBounds(164, buttonY, 96, 30);
            ok.DialogResult = DialogResult.OK;
            Styling.StyleButton(ok, theme, true);
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = L.T("Cancel");
            cancel.SetBounds(268, buttonY, 96, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Styling.StyleButton(cancel, theme, false);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void AddLabel(Theme theme, string text, int y)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.UseMnemonic = false;
            label.Text = L.T(text);
            label.ForeColor = theme.Subtle;
            label.BackColor = theme.Window;
            label.SetBounds(16, y, 348, 18);
            Controls.Add(label);
        }

        /// <summary>Puts the current values in the boxes before the window opens.</summary>
        public void Fill(string firstValue, string secondValue)
        {
            first.Text = firstValue ?? "";
            second.Text = secondValue ?? "";
        }

        public string FirstText
        {
            get { return first.Text.Trim(); }
        }

        public string SecondText
        {
            get { return second.Text; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.TitleBar(Handle, dark);
        }
    }
}
