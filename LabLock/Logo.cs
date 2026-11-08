using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LabLock
{
    /// <summary>
    /// The LabLock logo (a padlock on a rounded tile), painted in the accent colour of the current theme.
    /// The same drawing, in lime, is the program icon (app.ico). Coordinates are on a 256 x 256 grid.
    /// </summary>
    internal sealed class LogoBox : Control, IThemed
    {
        private Theme theme;

        public LogoBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Size = new Size(40, 40);
        }

        public void ApplyTheme(Theme newTheme)
        {
            theme = newTheme;
            Invalidate();
        }

        private static GraphicsPath Rounded(float x, float y, float width, float height, float radius)
        {
            float d = radius * 2F;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(x, y, d, d, 180F, 90F);
            path.AddArc(x + width - d, y, d, d, 270F, 90F);
            path.AddArc(x + width - d, y + height - d, d, d, 0F, 90F);
            path.AddArc(x, y + height - d, d, d, 90F, 90F);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (theme == null) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float s = Math.Min(Width, Height) / 256F;

            using (SolidBrush tile = new SolidBrush(theme.Accent))
            using (SolidBrush ink = new SolidBrush(theme.AccentText))
            using (Pen shacklePen = new Pen(theme.AccentText, 22F * s))
            {
                using (GraphicsPath path = Rounded(8F * s, 8F * s, 240F * s, 240F * s, 56F * s))
                {
                    g.FillPath(tile, path);
                }
                using (GraphicsPath path = Rounded(95F * s, 63F * s, 66F * s, 90F * s, 33F * s))
                {
                    g.DrawPath(shacklePen, path);   // the shackle; its lower half is hidden by the body
                }
                using (GraphicsPath path = Rounded(60F * s, 112F * s, 136F * s, 96F * s, 18F * s))
                {
                    g.FillPath(ink, path);
                }
                g.FillEllipse(tile, 114F * s, 138F * s, 28F * s, 28F * s);
                g.FillRectangle(tile, 122F * s, 154F * s, 12F * s, 30F * s);
            }
        }
    }
}
