using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace CursorSwap;

/// <summary>Pitch-black premium palette + drawing helpers shared by every custom control.</summary>
public static class Theme
{
    public static readonly Color Bg       = Color.FromArgb(0, 0, 0);
    public static readonly Color Surface  = Color.FromArgb(9, 9, 11);
    public static readonly Color Surface2 = Color.FromArgb(16, 16, 20);
    public static readonly Color Surface3 = Color.FromArgb(27, 27, 33);
    public static readonly Color Border   = Color.FromArgb(28, 28, 35);
    public static readonly Color BorderHi = Color.FromArgb(58, 58, 70);
    public static readonly Color Text     = Color.FromArgb(244, 244, 247);
    public static readonly Color Dim      = Color.FromArgb(140, 140, 154);
    public static readonly Color Faint    = Color.FromArgb(78, 78, 92);
    public static readonly Color Accent   = Color.FromArgb(139, 123, 255);
    public static readonly Color Accent2  = Color.FromArgb(79, 209, 255);
    public static readonly Color Success  = Color.FromArgb(61, 220, 151);
    public static readonly Color Danger   = Color.FromArgb(255, 83, 112);
    public static readonly Color Warn     = Color.FromArgb(255, 190, 90);

    /// <summary>DPI scale factor (1.0 = 100%).</summary>
    public static float Scale = 1f;
    public static int S(float v) => (int)MathF.Round(v * Scale);
    public static float Sf(float v) => v * Scale;

    static readonly Dictionary<(string, float, FontStyle), Font> Fonts = new();

    public static Font F(float size, FontStyle style = FontStyle.Regular, string family = "Segoe UI")
    {
        var key = (family, size, style);
        if (!Fonts.TryGetValue(key, out var f))
        {
            try { f = new Font(family, size, style, GraphicsUnit.Point); }
            catch { f = new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point); }
            Fonts[key] = f;
        }
        return f;
    }

    public static Font Semi(float size) => F(size, FontStyle.Regular, "Segoe UI Semibold");
    public static Font Bold(float size) => F(size, FontStyle.Bold, "Segoe UI");
    public static Font Icons(float size) => F(size, FontStyle.Regular, "Segoe MDL2 Assets");

    // Segoe MDL2 glyphs
    public const string GHome = "", GCursor = "", GBolt = "", GInfo = "",
        GClose = "", GMin = "", GRefresh = "", GSearch = "", GAdd = "",
        GX = "", GDelete = "", GTarget = "";

    public static void HQ(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    public static float EaseOut(float t) => 1f - MathF.Pow(1f - Clamp01(t), 3);
    public static float EaseInOut(float t) { t = Clamp01(t); return t < .5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2; }

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Clamp01(t);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    public static Color A(Color c, float alpha) => Color.FromArgb(Math.Clamp((int)(alpha * 255), 0, 255), c.R, c.G, c.B);

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Brush AccentBrush(RectangleF r, float alpha = 1f)
    {
        if (r.Width < 1 || r.Height < 1) return new SolidBrush(A(Accent, alpha));
        return new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2),
            A(Accent, alpha), A(Accent2, alpha), LinearGradientMode.ForwardDiagonal);
    }

    public static void DrawText(Graphics g, string s, Font f, Color c, RectangleF r,
        StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center)
    {
        using var sf = new StringFormat(StringFormatFlags.NoWrap) { Alignment = h, LineAlignment = v, Trimming = StringTrimming.EllipsisCharacter };
        using var b = new SolidBrush(c);
        g.DrawString(s, f, b, r, sf);
    }

    /// <summary>Soft glow: a few widening, fading strokes around a path.</summary>
    public static void Glow(Graphics g, GraphicsPath path, Color c, float strength, float width)
    {
        if (strength <= 0.01f) return;
        for (int i = 3; i >= 1; i--)
        {
            using var pen = new Pen(A(c, strength * 0.10f * (4 - i)), width * i) { LineJoin = LineJoin.Round };
            g.DrawPath(pen, path);
        }
    }

    public static Color HueShift(Color c, float degrees)
    {
        float h = (c.GetHue() + degrees) % 360f;
        if (h < 0) h += 360f;
        return FromHsl(c.A, h, c.GetSaturation(), c.GetBrightness());
    }

    static Color FromHsl(int a, float h, float s, float l)
    {
        float q = l < .5f ? l * (1 + s) : l + s - l * s, p = 2 * l - q, hk = h / 360f;
        Span<float> t = stackalloc float[] { hk + 1f / 3, hk, hk - 1f / 3 };
        Span<int> rgb = stackalloc int[3];
        for (int i = 0; i < 3; i++)
        {
            float tc = t[i];
            if (tc < 0) tc += 1;
            if (tc > 1) tc -= 1;
            float v = tc < 1f / 6 ? p + (q - p) * 6 * tc : tc < .5f ? q : tc < 2f / 3 ? p + (q - p) * (2f / 3 - tc) * 6 : p;
            rgb[i] = Math.Clamp((int)(v * 255), 0, 255);
        }
        return Color.FromArgb(a, rgb[0], rgb[1], rgb[2]);
    }

    /// <summary>App logo: gradient squircle with a white pointer.</summary>
    public static void DrawLogo(Graphics g, RectangleF r)
    {
        using (var p = Round(r, r.Width * 0.28f))
        using (var b = AccentBrush(r))
            g.FillPath(b, p);
        PointF P(float x, float y) => new(r.X + x * r.Width, r.Y + y * r.Height);
        var arrow = new[] { P(.33f, .2f), P(.33f, .76f), P(.46f, .63f), P(.56f, .83f), P(.64f, .79f), P(.54f, .59f), P(.72f, .59f) };
        using var wb = new SolidBrush(Color.White);
        g.FillPolygon(wb, arrow);
    }

    public static Icon MakeAppIcon(int size = 32)
    {
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            HQ(g);
            g.Clear(Color.Transparent);
            DrawLogo(g, new RectangleF(1, 1, size - 2, size - 2));
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public static ContextMenuStrip Menu() => new()
    {
        Renderer = new DarkMenuRenderer(),
        BackColor = Surface2,
        ForeColor = Text,
        Font = F(9.5f),
        ShowImageMargin = true,
    };
}

/// <summary>Dark context menus (tray + tile menus).</summary>
public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Faint;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        Theme.HQ(g);
        var r = e.ImageRectangle;
        float d = Math.Min(r.Width, r.Height) * 0.45f;
        var dot = new RectangleF(r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
        using var b = Theme.AccentBrush(dot);
        g.FillEllipse(b, dot);
    }

    sealed class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.Surface3;
        public override Color MenuItemBorder => Theme.Surface3;
        public override Color MenuBorder => Theme.Border;
        public override Color ToolStripDropDownBackground => Theme.Surface2;
        public override Color ImageMarginGradientBegin => Theme.Surface2;
        public override Color ImageMarginGradientMiddle => Theme.Surface2;
        public override Color ImageMarginGradientEnd => Theme.Surface2;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Surface2;
        public override Color MenuItemSelectedGradientBegin => Theme.Surface3;
        public override Color MenuItemSelectedGradientEnd => Theme.Surface3;
        public override Color MenuItemPressedGradientBegin => Theme.Surface3;
        public override Color MenuItemPressedGradientEnd => Theme.Surface3;
        public override Color CheckBackground => Theme.Surface2;
        public override Color CheckSelectedBackground => Theme.Surface3;
        public override Color CheckPressedBackground => Theme.Surface3;
    }
}
