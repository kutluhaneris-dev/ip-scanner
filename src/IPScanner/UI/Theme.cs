using System.Drawing.Drawing2D;

namespace IPScanner.UI;

/// <summary>IPZ renkleri ve kontrol biçimlendirme yardımcıları. Yalnızca görünümü değiştirir.</summary>
internal static class Theme
{
    public static readonly Color Primary = Color.FromArgb(37, 99, 235);
    public static readonly Color PrimaryDark = Color.FromArgb(29, 78, 216);
    public static readonly Color Accent = Color.FromArgb(6, 182, 212);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color DangerDark = Color.FromArgb(185, 28, 28);
    public static readonly Color Background = Color.FromArgb(243, 246, 251);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(214, 222, 235);
    public static readonly Color Text = Color.FromArgb(30, 41, 59);
    public static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    public static readonly Color AltRow = Color.FromArgb(246, 249, 253);
    public static readonly Color Alive = Color.FromArgb(22, 163, 74);
    public static readonly Color Dead = Color.FromArgb(160, 170, 184);

    public static Image? LoadLogo()
    {
        using var stream = typeof(Theme).Assembly.GetManifestResourceStream("IPScanner.ipz-logo.png");
        return stream == null ? null : new Bitmap(stream);
    }

    /// <summary>Dolgulu ana düğme (Tara / Durdur).</summary>
    public static void StylePrimary(Button b, Color back, Color hover)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = back;
        b.FlatAppearance.MouseOverBackColor = hover;
        b.FlatAppearance.MouseDownBackColor = hover;
        b.ForeColor = Color.White;
        b.Font = new Font(b.Font, FontStyle.Bold);
        b.Cursor = Cursors.Hand;
        b.UseVisualStyleBackColor = false;
    }

    /// <summary>Çerçeveli ikincil düğme.</summary>
    public static void StyleSecondary(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 241, 252);
        b.BackColor = Surface;
        b.ForeColor = Text;
        b.Cursor = Cursors.Hand;
        b.UseVisualStyleBackColor = false;
    }

    /// <summary>Menüler için sade, beyaz zeminli çizim.</summary>
    public sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer() : base(new Colors()) => RoundedEdges = false;

        private sealed class Colors : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => Surface;
            public override Color MenuStripGradientEnd => Surface;
            public override Color MenuItemSelected => Color.FromArgb(235, 241, 252);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(235, 241, 252);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(235, 241, 252);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(222, 233, 251);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(222, 233, 251);
            public override Color MenuItemBorder => Color.FromArgb(191, 211, 247);
            public override Color MenuBorder => Border;
            public override Color ToolStripDropDownBackground => Surface;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color SeparatorDark => Border;
            public override Color StatusStripGradientBegin => Surface;
            public override Color StatusStripGradientEnd => Surface;
        }
    }
}

/// <summary>Üstteki logolu, degrade zeminli başlık bandı.</summary>
internal sealed class HeaderPanel : Panel
{
    private readonly Image? _logo = Theme.LoadLogo();
    private readonly Font _titleFont = new("Segoe UI Semibold", 17F);
    private readonly Font _subtitleFont = new("Segoe UI", 9.5F);

    public HeaderPanel()
    {
        Dock = DockStyle.Top;
        Height = 68;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using (var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(30, 64, 175), Theme.Accent, LinearGradientMode.Horizontal))
            g.FillRectangle(brush, ClientRectangle);

        int pad = (int)(14 * DeviceDpi / 96f);
        int size = Height - 2 * pad;
        int x = pad;
        if (_logo != null)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_logo, new Rectangle(x, pad, size, size));
            x += size + pad;
        }

        var titleSize = g.MeasureString("IPZ", _titleFont);
        float textTop = (Height - titleSize.Height - _subtitleFont.Height + 4) / 2f;
        g.DrawString("IPZ", _titleFont, Brushes.White, x, textTop);
        using var sub = new SolidBrush(Color.FromArgb(225, 255, 255, 255));
        g.DrawString("IP Tarayıcı  ·  ağdaki cihazları bul ve tek tıkla bağlan", _subtitleFont, sub, x + 2, textTop + titleSize.Height - 6);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logo?.Dispose();
            _titleFont.Dispose();
            _subtitleFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
