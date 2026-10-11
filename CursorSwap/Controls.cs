using System.Drawing.Drawing2D;
using static CursorSwap.Theme;

namespace CursorSwap;

/// <summary>Base for all custom-drawn, animated controls. One shared 60fps ticker drives every animation.</summary>
public abstract class AnimControl : Control
{
    static readonly System.Windows.Forms.Timer Ticker = new() { Interval = 15 };
    static readonly HashSet<AnimControl> Active = new();

    static AnimControl()
    {
        Ticker.Tick += (_, _) =>
        {
            foreach (var c in Active.ToArray())
            {
                if (c.IsDisposed) { Active.Remove(c); continue; }
                bool more = c.Step();
                c.Invalidate();
                if (!more) Active.Remove(c);
            }
            if (Active.Count == 0) Ticker.Stop();
        };
    }

    protected bool Hovered, Pressed;

    protected AnimControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
    }

    public void Kick()
    {
        Active.Add(this);
        if (!Ticker.Enabled) Ticker.Start();
    }

    /// <summary>Advance animations one frame. Return true while still animating.</summary>
    protected virtual bool Step() => false;

    protected static bool Approach(ref float v, float target, float k = 0.2f, float eps = 0.002f)
    {
        float d = target - v;
        if (Math.Abs(d) < eps) { v = target; return false; }
        v += d * k;
        return true;
    }

    protected void DimIfDisabled(Graphics g)
    {
        if (Enabled) return;
        using var b = new SolidBrush(Color.FromArgb(170, BackColor));
        g.FillRectangle(b, ClientRectangle);
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (Parent != null) BackColor = Parent.BackColor;
    }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hovered = true; Kick(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hovered = false; Pressed = false; Kick(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) { Pressed = true; Kick(); }
    }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Pressed = false; Kick(); }
}

/// <summary>Rounded pitch-black card with optional title.</summary>
public class Card : Panel
{
    public string Title { get; set; } = "";
    public string Sub { get; set; } = "";

    public Card()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Surface;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(Parent?.BackColor ?? Bg);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Round(r, Sf(16));
        using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
        using (var lg = new LinearGradientBrush(new RectangleF(0, 0, Width, Math.Max(1, Height)),
                   Color.FromArgb(10, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
            g.FillPath(lg, path);
        using (var pen = new Pen(Border, 1)) g.DrawPath(pen, path);

        if (Title != "")
        {
            DrawText(g, Title, Semi(10.5f), Theme.Text, new RectangleF(S(20), S(14), Width - S(40), S(22)));
            if (Sub != "")
                DrawText(g, Sub, F(8.75f), Dim, new RectangleF(S(20), S(36), Width - S(40), S(18)));
        }
    }
}

/// <summary>Title + description on the left, animated toggle switch on the right.</summary>
public class ToggleRow : AnimControl
{
    bool _checked;
    float _t, _h, _p;

    public string Title { get; set; } = "";
    public string Desc { get; set; } = "";
    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set { if (_checked == value) return; _checked = value; Kick(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    public void SetSilently(bool v) { _checked = v; _t = v ? 1 : 0; Invalidate(); }

    public ToggleRow() { Cursor = Cursors.Hand; Height = S(56); }

    protected override bool Step()
    {
        bool a = Approach(ref _t, _checked ? 1 : 0, 0.2f);
        bool b = Approach(ref _h, Hovered ? 1 : 0, 0.2f);
        bool c = Approach(ref _p, Pressed ? 1 : 0, 0.35f);
        return a | b | c;
    }

    protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);

        if (_h > 0.01f)
        {
            using var hp = Round(new RectangleF(0, 0, Width - 1, Height - 1), Sf(12));
            using var hb = new SolidBrush(A(Color.White, 0.025f * _h));
            g.FillPath(hb, hp);
        }

        float sw = Sf(44), sh = Sf(24);
        float sx = Width - sw - Sf(12), sy = (Height - sh) / 2f;
        float textW = sx - Sf(24);
        if (Desc != "")
        {
            DrawText(g, Title, Semi(10f), Theme.Text, new RectangleF(S(12), Height / 2f - Sf(19), textW, Sf(20)));
            DrawText(g, Desc, F(8.5f), Dim, new RectangleF(S(12), Height / 2f + Sf(1), textW, Sf(18)));
        }
        else DrawText(g, Title, Semi(10f), Theme.Text, new RectangleF(S(12), 0, textW, Height));

        var track = new RectangleF(sx, sy, sw, sh);
        using (var tp = Round(track, sh / 2))
        {
            using (var b = new SolidBrush(Lerp(Surface3, BorderHi, _h * 0.4f))) g.FillPath(b, tp);
            if (_t > 0.01f)
            {
                Glow(g, tp, Accent, _t * 0.8f, Sf(2));
                using var ab = AccentBrush(track, _t);
                g.FillPath(ab, tp);
            }
        }

        float kd = sh - Sf(6), stretch = _p * Sf(5);
        float kx = sx + Sf(3) + _t * (sw - kd - Sf(6) - stretch);
        var knob = new RectangleF(kx, sy + Sf(3), kd + stretch, kd);
        using (var kp = Round(knob, kd / 2))
        using (var kb = new SolidBrush(Lerp(Color.FromArgb(150, 150, 165), Color.White, _t)))
            g.FillPath(kb, kp);

        DimIfDisabled(g);
    }
}

/// <summary>Rounded button with hover glow and press squish. Primary = accent gradient.</summary>
public class FlatButton : AnimControl
{
    float _h, _p;
    public bool Primary { get; set; }
    public string Glyph { get; set; } = "";
    public bool Danger { get; set; }

    public FlatButton() { Cursor = Cursors.Hand; Height = S(38); }

    protected override bool Step()
    {
        bool a = Approach(ref _h, Hovered && Enabled ? 1 : 0, 0.2f);
        bool b = Approach(ref _p, Pressed ? 1 : 0, 0.4f);
        return a | b;
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);

        float inset = Sf(3) + _p * Sf(1.5f);
        var r = new RectangleF(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1);
        using var path = Round(r, Sf(10));

        Color fg;
        if (Primary)
        {
            Glow(g, path, Accent, 0.35f + _h * 0.65f, Sf(2));
            using (var b = AccentBrush(r)) g.FillPath(b, path);
            using (var w = new SolidBrush(A(Color.White, 0.14f * _h))) g.FillPath(w, path);
            fg = Color.White;
        }
        else
        {
            Color hoverBorder = Danger ? Theme.Danger : BorderHi;
            using (var b = new SolidBrush(Lerp(Surface2, Surface3, _h))) g.FillPath(b, path);
            using (var p = new Pen(Lerp(Border, hoverBorder, _h), 1)) g.DrawPath(p, path);
            fg = Danger ? Lerp(Theme.Text, Theme.Danger, _h) : Theme.Text;
        }

        var font = Semi(9.5f);
        var gf = Icons(10f);
        float tw = g.MeasureString(Text, font).Width;
        float gw = Glyph == "" ? 0 : g.MeasureString(Glyph, gf).Width;
        float gap = Glyph != "" && Text != "" ? Sf(2) : 0;
        float x = (Width - (tw + gw + gap)) / 2f;
        if (Glyph != "") DrawText(g, Glyph, gf, fg, new RectangleF(x, 0, gw + 2, Height));
        DrawText(g, Text, font, fg, new RectangleF(x + gw + gap, 0, tw + 4, Height));
        DimIfDisabled(g);
    }
}

/// <summary>Custom title bar: logo, title, minimize and close with hover animations. Drags the window.</summary>
public class TitleBar : AnimControl
{
    float _hMin, _hClose;
    int _hover; // 0 none, 1 min, 2 close
    public event EventHandler? MinimizeClicked, CloseClicked;

    public TitleBar() { Height = S(44); }

    RectangleF CloseRect => new(Width - Sf(48), 0, Sf(48), Height);
    RectangleF MinRect => new(Width - Sf(96), 0, Sf(48), Height);

    protected override bool Step()
    {
        bool a = Approach(ref _hMin, _hover == 1 ? 1 : 0, 0.25f);
        bool b = Approach(ref _hClose, _hover == 2 ? 1 : 0, 0.25f);
        return a | b;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = CloseRect.Contains(e.Location) ? 2 : MinRect.Contains(e.Location) ? 1 : 0;
        if (h != _hover) { _hover = h; Kick(); }
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = 0; base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && _hover == 0 && FindForm() is Form f)
        {
            Native.ReleaseCapture();
            Native.SendMessage(f.Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_hover == 1) MinimizeClicked?.Invoke(this, EventArgs.Empty);
        else if (_hover == 2) CloseClicked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);
        float ls = Sf(20);
        DrawLogo(g, new RectangleF(Sf(18), (Height - ls) / 2, ls, ls));
        DrawText(g, "CursorSwap", Semi(10f), Theme.Text, new RectangleF(Sf(46), 0, Sf(200), Height));

        using (var b = new SolidBrush(A(Surface3, _hMin))) g.FillRectangle(b, MinRect);
        using (var b = new SolidBrush(A(Theme.Danger, _hClose * 0.9f))) g.FillRectangle(b, CloseRect);
        DrawText(g, GMin, Icons(8.5f), Lerp(Dim, Theme.Text, _hMin), MinRect, StringAlignment.Center);
        DrawText(g, GClose, Icons(8.5f), Lerp(Dim, Color.White, _hClose), CloseRect, StringAlignment.Center);
    }
}

/// <summary>Vertical navigation with a sliding, glowing selection pill.</summary>
public class Sidebar : AnimControl
{
    public readonly List<(string Glyph, string Text)> Items = new();
    public string Footer { get; set; } = "";
    int _sel, _hover = -1;
    float _indY = -1;
    float[] _hv = Array.Empty<float>();
    public event EventHandler? SelectedChanged;

    public int Selected
    {
        get => _sel;
        set { if (value == _sel) return; _sel = value; Kick(); SelectedChanged?.Invoke(this, EventArgs.Empty); }
    }

    public Sidebar() { Cursor = Cursors.Hand; }

    RectangleF ItemRect(int i) => new(Sf(10), Sf(8) + i * Sf(50), Width - Sf(20), Sf(44));

    protected override bool Step()
    {
        if (_hv.Length != Items.Count) _hv = new float[Items.Count];
        bool more = Approach(ref _indY, ItemRect(_sel).Y, 0.2f, 0.3f);
        for (int i = 0; i < _hv.Length; i++) more |= Approach(ref _hv[i], i == _hover ? 1 : 0, 0.2f);
        return more;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = -1;
        for (int i = 0; i < Items.Count; i++) if (ItemRect(i).Contains(e.Location)) h = i;
        if (h != _hover) { _hover = h; Kick(); }
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_hover >= 0) Selected = _hover;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);
        if (_hv.Length != Items.Count) _hv = new float[Items.Count];
        if (_indY < 0) _indY = ItemRect(_sel).Y;

        // sliding selection
        var sel = ItemRect(0);
        sel.Y = _indY;
        using (var p = Round(sel, Sf(12)))
        {
            using (var b = new SolidBrush(Surface2)) g.FillPath(b, p);
            using (var lg = new LinearGradientBrush(sel, A(Accent, 0.16f), A(Accent, 0f), LinearGradientMode.Horizontal))
                g.FillPath(lg, p);
            using (var pen = new Pen(Border)) g.DrawPath(pen, p);
        }
        var bar = new RectangleF(sel.X + Sf(1), sel.Y + Sf(12), Sf(3), sel.Height - Sf(24));
        using (var bp = Round(bar, Sf(1.5f)))
        {
            Glow(g, bp, Accent, 0.9f, Sf(3));
            using var bb = AccentBrush(bar);
            g.FillPath(bb, bp);
        }

        for (int i = 0; i < Items.Count; i++)
        {
            var r = ItemRect(i);
            float active = Clamp01(1f - Math.Abs(_indY - r.Y) / r.Height);
            if (_hv[i] > 0.01f && i != _sel)
            {
                using var hp = Round(r, Sf(12));
                using var hb = new SolidBrush(A(Color.White, 0.03f * _hv[i]));
                g.FillPath(hb, hp);
            }
            Color c = Lerp(Dim, Theme.Text, Math.Max(active, _hv[i] * 0.7f));
            Color gc = Lerp(Lerp(Dim, Theme.Text, _hv[i] * 0.7f), Accent, active);
            float shift = _hv[i] * Sf(2);
            DrawText(g, Items[i].Glyph, Icons(12f), gc, new RectangleF(r.X + Sf(16) + shift, r.Y, Sf(24), r.Height));
            DrawText(g, Items[i].Text, Semi(10f), c, new RectangleF(r.X + Sf(48) + shift, r.Y, r.Width - Sf(52), r.Height));
        }

        if (Footer != "")
            DrawText(g, Footer, F(8f), Faint, new RectangleF(Sf(22), Height - Sf(36), Width - Sf(30), Sf(24)));
    }
}

/// <summary>Pill showing the live status with a pulsing dot.</summary>
public class StatusPill : AnimControl
{
    string _label = "";
    Color _dot = Faint;
    bool _pulse;

    public void Set(string text, Color dot, bool pulse)
    {
        if (text == _label && dot == _dot && pulse == _pulse) return;
        _label = text; _dot = dot; _pulse = pulse;
        int right = Right;
        Width = S(42) + TextRenderer.MeasureText(text, F(9f)).Width;
        Left = right - Width;
        Invalidate();
        if (pulse) Kick();
    }

    public StatusPill() { Height = S(32); }

    protected override bool Step() => _pulse && Visible;

    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible && _pulse) Kick(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var p = Round(r, r.Height / 2))
        {
            using (var b = new SolidBrush(Surface2)) g.FillPath(b, p);
            using (var pen = new Pen(Border)) g.DrawPath(pen, p);
        }
        float d = Sf(8), cx = Sf(18), cy = Height / 2f;
        if (_pulse)
        {
            float ph = (Environment.TickCount64 % 1400) / 1400f;
            float rr = d / 2 + ph * Sf(8);
            using var pb = new SolidBrush(A(_dot, 0.45f * (1 - ph)));
            g.FillEllipse(pb, cx - rr, cy - rr, rr * 2, rr * 2);
        }
        using (var b = new SolidBrush(_dot)) g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
        DrawText(g, _label, F(9f), Theme.Text, new RectangleF(Sf(30), 0, Width - Sf(36), Height));
    }
}
