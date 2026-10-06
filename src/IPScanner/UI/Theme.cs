using System.Drawing.Drawing2D;

namespace IPScanner.UI;

/// <summary>IPZ renkleri ve kontrol biçimlendirme yardımcıları. Yalnızca görünümü değiştirir.</summary>
internal static class Theme
{
    // IPZ kurumsal lacivert (ipzproje.com.tr logosundaki zemin).
    public static readonly Color Navy = Color.FromArgb(20, 33, 61);
    public static readonly Color NavyDark = Color.FromArgb(13, 22, 42);
    public static readonly Color Primary = Color.FromArgb(31, 52, 94);
    public static readonly Color PrimaryDark = Navy;
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color DangerDark = Color.FromArgb(185, 28, 28);
    public static readonly Color Background = Color.FromArgb(244, 245, 248);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(216, 220, 228);
    public static readonly Color Text = Color.FromArgb(30, 41, 59);
    public static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    public static readonly Color AltRow = Color.FromArgb(246, 247, 250);
    public static readonly Color Alive = Color.FromArgb(22, 163, 74);
    public static readonly Color Dead = Color.FromArgb(160, 170, 184);

    public const string Author = "Kutluhan";
    public const string Company = "IPZ PROJE";

    public static Image? LoadLogo()
    {
        using var stream = typeof(Theme).Assembly.GetManifestResourceStream("IPScanner.ipz-logo-white.png");
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
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(234, 237, 243);
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
            public override Color MenuItemSelected => Color.FromArgb(234, 237, 243);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(234, 237, 243);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(234, 237, 243);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(222, 227, 237);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(222, 227, 237);
            public override Color MenuItemBorder => Color.FromArgb(196, 204, 220);
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
    private readonly Font _titleFont = new("Segoe UI Semibold", 15F);
    private readonly Font _subtitleFont = new("Segoe UI", 9.5F);

    public HeaderPanel()
    {
        Dock = DockStyle.Top;
        Height = 76;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using (var brush = new LinearGradientBrush(ClientRectangle, Theme.Navy, Theme.NavyDark, LinearGradientMode.Horizontal))
            g.FillRectangle(brush, ClientRectangle);

        float k = DeviceDpi / 96f;
        int pad = (int)(12 * k);
        int x = (int)(18 * k);
        if (_logo != null)
        {
            int h = Height - 2 * pad;
            int w = _logo.Width * h / _logo.Height;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_logo, new Rectangle(x, pad, w, h));
            x += w + (int)(18 * k);

            // Logo ile başlık arasında ince dikey çizgi.
            using var line = new Pen(Color.FromArgb(70, 255, 255, 255), 1);
            g.DrawLine(line, x, pad + 4, x, Height - pad - 4);
            x += (int)(18 * k);
        }

        var titleSize = g.MeasureString("IP Tarayıcı", _titleFont);
        float textTop = (Height - titleSize.Height - _subtitleFont.Height + 6) / 2f;
        g.DrawString("IP Tarayıcı", _titleFont, Brushes.White, x, textTop);
        using var sub = new SolidBrush(Color.FromArgb(190, 255, 255, 255));
        g.DrawString("Ağdaki cihazları bul ve tek tıkla bağlan", _subtitleFont, sub, x + 2, textTop + titleSize.Height - 4);

        // Sağda geliştirici bilgisi.
        string credit = $"Geliştiren: {Theme.Author}  ·  {Theme.Company}";
        var creditSize = g.MeasureString(credit, _subtitleFont);
        g.DrawString(credit, _subtitleFont, sub, Width - creditSize.Width - 18 * k, (Height - creditSize.Height) / 2f);
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
