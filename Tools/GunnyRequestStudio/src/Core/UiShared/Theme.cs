using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GunnyRequestStudio.Core.Ui
{
    /// <summary>
    /// Bang mau + font + ham ve giao dien SANG (light theme) theo phong cach
    /// trang web Gunny Panel: nen kem am, card trang, sidebar navy, diem nhan vang amber.
    /// </summary>
    public static class Theme
    {
        // ---- palette (giong Gunny Panel web)
        public static readonly Color Bg = Color.FromArgb(246, 244, 239);        // #F6F4EF nen kem am
        public static readonly Color BgDeep = Color.FromArgb(255, 255, 255);    // nen input / console
        public static readonly Color Sidebar = Color.FromArgb(45, 74, 118);     // #2D4A76 navy
        public static readonly Color SidebarTop = Color.FromArgb(43, 68, 107);  // #2B446B
        public static readonly Color SidebarBottom = Color.FromArgb(53, 84, 128); // #355480
        public static readonly Color Panel = Color.FromArgb(255, 255, 255);
        public static readonly Color PanelAlt = Color.FromArgb(244, 247, 251);  // #F4F7FB
        public static readonly Color Card = Color.FromArgb(255, 255, 255);
        public static readonly Color Border = Color.FromArgb(220, 227, 236);    // #DCE3EC
        public static readonly Color BorderSoft = Color.FromArgb(232, 237, 244); // #E8EDF4
        public static readonly Color Text = Color.FromArgb(42, 53, 80);         // #2A3550
        public static readonly Color TextDim = Color.FromArgb(107, 122, 148);   // #6B7A94
        public static readonly Color TextFaint = Color.FromArgb(147, 162, 184);
        public static readonly Color Accent = Color.FromArgb(63, 110, 166);     // #3F6EA6 xanh
        public static readonly Color AccentDeep = Color.FromArgb(36, 59, 99);   // #243B63 navy
        public static readonly Color Green = Color.FromArgb(47, 143, 107);      // #2F8F6B
        public static readonly Color GreenDeep = Color.FromArgb(36, 115, 82);
        public static readonly Color Yellow = Color.FromArgb(201, 138, 60);     // #C98A3C amber
        public static readonly Color Orange = Color.FromArgb(176, 118, 44);     // #B0762C amber-dark
        public static readonly Color Red = Color.FromArgb(184, 73, 91);         // #B8495B
        public static readonly Color Purple = Color.FromArgb(122, 107, 176);
        public static readonly Color Pink = Color.FromArgb(196, 106, 142);
        public static readonly Color AmberSoft = Color.FromArgb(246, 232, 210);  // #F6E8D2
        public static readonly Color SkySoft = Color.FromArgb(234, 242, 251);    // #EAF2FB
        public static readonly Color Shadow = Color.FromArgb(36, 59, 99);

        // ---- fonts
        public static Font FontBase(float size = 9.5f) { return new Font("Segoe UI", size, FontStyle.Regular); }
        public static Font FontBold(float size = 9.5f) { return new Font("Segoe UI", size, FontStyle.Bold); }
        public static Font FontTitle(float size = 20f) { return new Font("Segoe UI Semibold", size, FontStyle.Bold); }
        public static Font FontH2(float size = 13f) { return new Font("Segoe UI Semibold", size, FontStyle.Bold); }
        public static Font FontMono(float size = 9f) { return new Font("Consolas", size, FontStyle.Regular); }
        public static Font FontIcon(float size = 11f) { return new Font("Segoe UI Symbol", size, FontStyle.Regular); }

        private static readonly Dictionary<string, string> IconMap = new Dictionary<string, string>
        {
            { "📊", "▥" }, { "📈", "▥" }, { "📉", "▥" }, { "📋", "☰" }, { "📄", "▤" }, { "📃", "▤" },
            { "🗂", "▤" }, { "📁", "▤" }, { "📂", "▤" }, { "💾", "◫" }, { "🗄", "▥" }, { "🛢", "▤" },
            { "📦", "▣" }, { "🧩", "◧" }, { "⬇", "↓" }, { "📥", "↓" }, { "⬆", "↑" }, { "📤", "↑" },
            { "🔍", "⌕" }, { "🔎", "⌕" }, { "🛠", "⚒" }, { "🔧", "⚒" }, { "⚙", "⚙" }, { "⚠", "⚠" },
            { "✔", "✓" }, { "✅", "✓" }, { "✖", "✕" }, { "❌", "✕" }, { "💣", "◉" }, { "🔥", "♨" },
            { "⭐", "★" }, { "🎯", "◎" }, { "➡", "→" }, { "◀", "◀" }, { "▶", "▶" }, { "❔", "?" },
            { "🧪", "⚗" }, { "🏥", "＋" }, { "🧾", "▤" }, { "🗃", "▥" }, { "💡", "☀" }, { "🔔", "◔" },
            { "🛡", "⛨" }, { "⚔", "⚔" }, { "🌐", "⊕" }, { "🧭", "✥" }, { "⏱", "◷" }, { "🧱", "▦" }
        };

        /// <summary>Doi emoji sang glyph BMP an toan de GDI+ ve duoc (tranh loi "Parameter is not valid").</summary>
        public static string Glyph(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var cleaned = s.Replace("\uFE0F", string.Empty).Replace("\uFE0E", string.Empty).Replace("\u200D", string.Empty);
            string mapped;
            if (IconMap.TryGetValue(cleaned, out mapped)) return mapped;
            var sb = new System.Text.StringBuilder();
            foreach (var ch in cleaned)
            {
                if (char.IsSurrogate(ch)) continue;
                if (ch >= 0x2190 && ch <= 0x2BFF) { sb.Append(ch); continue; }
                if (ch >= 0x2600 && ch <= 0x27BF) { sb.Append(ch); continue; }
                if (ch > 0x2000 && ch < 0xFFFF) { sb.Append(ch); continue; }
                sb.Append(ch);
            }
            var result = sb.ToString().Trim();
            return result.Length == 0 ? "◆" : result;
        }

        // ---- drawing helpers

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color color)
        {
            using (var p = RoundedRect(r, radius))
            using (var b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static void DrawRounded(Graphics g, Rectangle r, int radius, Color color, float width = 1f)
        {
            using (var p = RoundedRect(r, radius))
            using (var pen = new Pen(color, width))
                g.DrawPath(pen, p);
        }

        public static void DrawGlow(Graphics g, Rectangle r, int radius, Color color, int alpha = 46)
        {
            for (int i = 3; i >= 1; i--)
            {
                using (var p = RoundedRect(new Rectangle(r.X - i, r.Y - i, r.Width + i * 2, r.Height + i * 2), radius + i))
                using (var b = new SolidBrush(Color.FromArgb(Math.Max(4, alpha / i), color)))
                    g.FillPath(b, p);
            }
        }

        /// <summary>Bong do nhe (1 lop de giam chi phi ve lai khi nhieu card).</summary>
        public static void DrawShadow(Graphics g, Rectangle r, int radius, int spread = 1)
        {
            if (spread <= 0) return;
            var rr = new Rectangle(r.X - 1, r.Y, r.Width + 2, r.Height + 2);
            using (var p = RoundedRect(rr, radius + 1))
            using (var b = new SolidBrush(Color.FromArgb(10, Shadow)))
                g.FillPath(b, p);
        }

        /// <summary>Bat double-buffer cho control khong expose property (DataGridView...).</summary>
        public static void EnableDoubleBuffer(Control c)
        {
            if (c == null) return;
            try
            {
                var pi = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (pi != null) pi.SetValue(c, true, null);
            }
            catch { }
        }

        public static void SetupGraphics(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        /// <summary>Ve panel ngang chu de (light card) + tieu de, tra ve vung noi dung ben trong.</summary>
        public static Rectangle DrawCard(Graphics g, Rectangle bounds, string title, string subtitle = null, Color? accent = null)
        {
            SetupGraphics(g);
            DrawShadow(g, bounds, 12, 2);
            using (var b = new SolidBrush(Card)) g.FillPath(b, RoundedRect(bounds, 12));
            DrawRounded(g, bounds, 12, BorderSoft);
            var ac = accent ?? Accent;
            using (var b = new SolidBrush(ac)) g.FillRectangle(b, bounds.X + 1, bounds.Y + 12, 4, 18);
            int y = bounds.Y + 10;
            if (!string.IsNullOrEmpty(title))
            {
                using (var f = FontH2(11.5f))
                using (var b = new SolidBrush(Text))
                    g.DrawString(title, f, b, bounds.X + 14, y - 1);
                y += 24;
            }
            if (!string.IsNullOrEmpty(subtitle))
            {
                using (var f = FontBase(9f))
                using (var b = new SolidBrush(TextDim))
                    g.DrawString(subtitle, f, b, bounds.X + 14, y);
                y += 20;
            }
            return new Rectangle(bounds.X + 4, y, bounds.Width - 8, bounds.Bottom - y - 4);
        }
    }

    /// <summary>Trang con cua tool: moi tab ke thua lop nay de MainForm quan ly thong nhat.</summary>
    public abstract class PageBase : UserControl
    {
        protected StudioContext Ctx { get; private set; }
        public abstract string PageTitle { get; }
        public abstract string PageIcon { get; }
        public virtual string PageSubtitle { get { return null; } }

        protected PageBase(StudioContext context)
        {
            Ctx = context;
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Dock = DockStyle.Fill;
            Font = Theme.FontBase();
        }

        public virtual void OnShownPage() { }
        public virtual void OnSettingsChanged() { }

        protected void UiInvoke(Action action)
        {
            if (IsHandleCreated && InvokeRequired) BeginInvoke(action);
            else action();
        }
    }

    /// <summary>Panel tu ve theo theme (card trang, vien mem, bong nhe).</summary>
    public class ThemedPanel : Panel
    {
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }
        public bool DrawBorder { get; set; }
        public bool DrawShadow { get; set; }
        public Color? TopAccent { get; set; }

        public ThemedPanel()
        {
            Radius = 12;
            FillColor = Theme.Card;
            BorderColor = Theme.BorderSoft;
            DrawBorder = true;
            DrawShadow = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.SetupGraphics(e.Graphics);
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            if (DrawShadow) Theme.DrawShadow(e.Graphics, r, Radius, 1);
            using (var p = Theme.RoundedRect(r, Radius))
            using (var b = new SolidBrush(FillColor))
                e.Graphics.FillPath(b, p);
            if (DrawBorder)
            {
                using (var p = Theme.RoundedRect(r, Radius))
                using (var pen = new Pen(BorderColor))
                    e.Graphics.DrawPath(pen, p);
            }
            if (TopAccent.HasValue)
            {
                var ac = new Rectangle(r.X + Radius / 2, r.Y, Math.Max(12, r.Width - Radius), 3);
                using (var p = Theme.RoundedRect(ac, 2))
                using (var b = new SolidBrush(TopAccent.Value))
                    e.Graphics.FillPath(b, p);
            }
            base.OnPaint(e);
        }
    }

    /// <summary>Nut bam phang (mau dac / vien trang) theo kieu web.</summary>
    public class FlatButton : Button
    {
        public Color AccentColor { get; set; }
        public Color HoverColor { get; set; }
        public Color BorderColor2 { get; set; }
        public int Radius { get; set; }
        public bool DrawShadow { get; set; }
        private bool _hover;

        public FlatButton()
        {
            AccentColor = Theme.Yellow;            // primary = amber nhu web
            HoverColor = Theme.Orange;
            BorderColor2 = Color.Transparent;
            Radius = 9;
            DrawShadow = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.White;
            Font = Theme.FontBold(9.5f);
            Height = 34;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.SetupGraphics(e.Graphics);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (!Enabled)
            {
                using (var p = Theme.RoundedRect(r, Radius))
                using (var b = new SolidBrush(Color.FromArgb(230, 234, 240)))
                    e.Graphics.FillPath(b, p);
                TextRenderer.DrawText(e.Graphics, Text, Font, r, Theme.TextFaint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }
            var fill = _hover ? HoverColor : AccentColor;
            if (DrawShadow)
            {
                var shr = new Rectangle(r.X, r.Y + 1, r.Width, r.Height);
                using (var p = Theme.RoundedRect(shr, Radius))
                using (var b = new SolidBrush(Color.FromArgb(28, Color.Black)))
                    e.Graphics.FillPath(b, p);
            }
            using (var p = Theme.RoundedRect(r, Radius))
            using (var b = new SolidBrush(fill))
                e.Graphics.FillPath(b, p);
            if (BorderColor2 != Color.Transparent)
            {
                using (var p = Theme.RoundedRect(r, Radius))
                using (var pen = new Pen(BorderColor2))
                    e.Graphics.DrawPath(pen, p);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, r, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Thanh tien trinh dep (gradient) + %.</summary>
    public class NiceProgress : Control
    {
        private int _value;
        private string _caption = "";
        public Color Color1 { get; set; }
        public Color Color2 { get; set; }

        public NiceProgress()
        {
            Color1 = Theme.Accent;
            Color2 = Color.FromArgb(106, 147, 196);
            Height = 26;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public string Caption
        {
            get { return _caption; }
            set { _caption = value ?? ""; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.SetupGraphics(e.Graphics);
            var r = new Rectangle(0, (Height - 12) / 2, Width - 1, 12);
            using (var p = Theme.RoundedRect(r, 6))
            using (var b = new SolidBrush(Color.FromArgb(238, 242, 247)))
                e.Graphics.FillPath(b, p);
            using (var p = Theme.RoundedRect(r, 6))
            using (var pen = new Pen(Color.FromArgb(225, 232, 240)))
                e.Graphics.DrawPath(pen, p);
            if (_value > 0)
            {
                var width = (int)(r.Width * (_value / 100.0));
                if (width > 4)
                {
                    var rr = new Rectangle(r.X, r.Y, width, r.Height);
                    using (var p = Theme.RoundedRect(rr, 6))
                    using (var b = new LinearGradientBrush(rr, Color1, Color2, LinearGradientMode.Horizontal))
                        e.Graphics.FillPath(b, p);
                }
            }
            var text = string.IsNullOrEmpty(_caption) ? _value + "%" : _caption;
            TextRenderer.DrawText(e.Graphics, text, Theme.FontBase(8.5f), ClientRectangle, Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>O so lieu lon (KPI card) kieu web: card trang, vach mau tren dinh.</summary>
    public class StatCard : ThemedPanel
    {
        private string _value = "0";
        public string Caption { get; set; }
        public string Value
        {
            get { return _value; }
            set { _value = value; Invalidate(); }
        }
        public Color ValueColor { get; set; }
        public string Hint { get; set; }
        public string Icon { get; set; }

        public StatCard()
        {
            Height = 92;
            Caption = "";
            Hint = "";
            ValueColor = Theme.AccentDeep;
            Icon = "";
            Radius = 12;
            FillColor = Theme.Card;
            DrawShadow = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.SetupGraphics(g);
            var pad = 14;
            // vach accent tren dinh card
            using (var b = new SolidBrush(ValueColor))
            {
                var bar = new Rectangle(pad, 1, Math.Max(12, Width - pad * 2), 3);
                using (var p = Theme.RoundedRect(bar, 2)) g.FillPath(b, p);
            }
            if (!string.IsNullOrEmpty(Icon))
            {
                using (var f = Theme.FontIcon(15f))
                {
                    TextRenderer.DrawText(g, Theme.Glyph(Icon), f, new Rectangle(Width - 46, 10, 34, 26), Theme.TextFaint,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }
            }
            using (var f = Theme.FontBold(8.3f))
            using (var b = new SolidBrush(Theme.TextDim))
                g.DrawString(Caption.ToUpperInvariant(), f, b, pad, pad + 2);
            using (var f = Theme.FontTitle(19f))
            using (var b = new SolidBrush(ValueColor))
                g.DrawString(Value, f, b, pad - 1, pad + 20);
            if (!string.IsNullOrEmpty(Hint))
            {
                using (var f = Theme.FontBase(8.2f))
                using (var b = new SolidBrush(Theme.TextFaint))
                    g.DrawString(Hint, f, b, pad, Height - 22);
            }
        }
    }

    /// <summary>Label duong dan (nen xam nhe).</summary>
    public class PathLabel : Label
    {
        public PathLabel()
        {
            AutoSize = false;
            Height = 26;
            TextAlign = ContentAlignment.MiddleLeft;
            ForeColor = Theme.Accent;
            BackColor = Theme.SkySoft;
            Font = Theme.FontMono(8.5f);
            Padding = new Padding(8, 0, 8, 0);
        }
    }

    /// <summary>Style cho control chuan (TextBox/ComboBox/DataGridView/RichTextBox) theo theme sang.</summary>
    public static class Style
    {
        public static void Apply(TextBox t)
        {
            t.BackColor = Theme.BgDeep;
            t.ForeColor = Theme.Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = Theme.FontBase(9.5f);
        }

        public static void Apply(ComboBox c)
        {
            c.BackColor = Theme.BgDeep;
            c.ForeColor = Theme.Text;
            c.FlatStyle = FlatStyle.Flat;
            c.Font = Theme.FontBase(9.5f);
            c.DropDownStyle = ComboBoxStyle.DropDown;
        }

        public static void Apply(DataGridView g)
        {
            g.BackgroundColor = Theme.Card;
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersDefaultCellStyle.BackColor = Theme.PanelAlt;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(85, 104, 138);
            g.ColumnHeadersDefaultCellStyle.Font = Theme.FontBold(8.6f);
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.PanelAlt;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(85, 104, 138);
            g.ColumnHeadersHeight = 32;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.DefaultCellStyle.BackColor = Theme.Card;
            g.DefaultCellStyle.ForeColor = Theme.Text;
            g.DefaultCellStyle.SelectionBackColor = Theme.SkySoft;
            g.DefaultCellStyle.SelectionForeColor = Theme.AccentDeep;
            g.DefaultCellStyle.Font = Theme.FontBase(9f);
            g.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            g.RowHeadersVisible = false;
            g.RowTemplate.Height = 26;
            g.GridColor = Theme.BorderSoft;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
            g.AlternatingRowsDefaultCellStyle.ForeColor = Theme.Text;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.ReadOnly = true;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            g.ScrollBars = ScrollBars.Both;
            Theme.EnableDoubleBuffer(g);
        }

        public static void Apply(RichTextBox r)
        {
            r.BackColor = Color.FromArgb(251, 252, 254);
            r.ForeColor = Theme.Text;
            r.BorderStyle = BorderStyle.None;
            r.Font = Theme.FontMono(8.8f);
            r.ReadOnly = true;
            r.DetectUrls = false;
        }
    }
}
