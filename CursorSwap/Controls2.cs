using System.Drawing.Drawing2D;
using static CursorSwap.Theme;

namespace CursorSwap;

/// <summary>A tile for one cursor state: preview, name, click to choose, × to clear, right-click for hotspot.</summary>
public class RoleTile : AnimControl
{
    float _h, _p, _x, _pop;
    bool _overX;
    Image? _preview;

    public CursorRole Role { get; init; }
    public string Title { get; init; } = "";
    public string Caption { get; init; } = "";
    public Hotspot Hotspot { get; set; }
    public bool IsMain { get; init; }

    public event EventHandler? BrowseRequested, ClearRequested;
    public event Action<RoleTile, Hotspot>? HotspotRequested;

    public RoleTile() { Cursor = Cursors.Hand; }

    public Image? Preview
    {
        get => _preview;
        set { _preview?.Dispose(); _preview = value; _pop = 0; Kick(); }
    }

    RectangleF XRect => new(Width - Sf(30), Sf(10), Sf(20), Sf(20));

    protected override bool Step()
    {
        bool a = Approach(ref _h, Hovered ? 1 : 0, 0.2f);
        bool b = Approach(ref _p, Pressed ? 1 : 0, 0.4f);
        bool c = Approach(ref _x, _overX ? 1 : 0, 0.3f);
        bool d = Approach(ref _pop, 1, 0.14f);
        return a | b | c | d;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool ox = _preview != null && XRect.Contains(e.Location);
        if (ox != _overX) { _overX = ox; Kick(); }
    }

    protected override void OnMouseLeave(EventArgs e) { _overX = false; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left)
        {
            if (_overX) ClearRequested?.Invoke(this, EventArgs.Empty);
            else BrowseRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Button == MouseButtons.Right)
        {
            var m = Theme.Menu();
            void Add(string text, Hotspot hs)
            {
                var it = new ToolStripMenuItem(text) { Checked = Hotspot == hs };
                it.Click += (_, _) => HotspotRequested?.Invoke(this, hs);
                m.Items.Add(it);
            }
            m.Items.Add(new ToolStripMenuItem("Hotspot (click point)") { Enabled = false });
            Add("Top-left", Hotspot.TopLeft);
            Add("Center", Hotspot.Center);
            Add("Top-center", Hotspot.TopCenter);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Choose image...", null, (_, _) => BrowseRequested?.Invoke(this, EventArgs.Empty));
            var clear = m.Items.Add("Clear", null, (_, _) => ClearRequested?.Invoke(this, EventArgs.Empty));
            clear.Enabled = _preview != null;
            m.Closed += (_, _) => BeginInvoke(new Action(m.Dispose));
            m.Show(this, e.Location);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);

        float lift = _h * Sf(3) - _p * Sf(1.5f);
        float pad = Sf(4);
        var r = new RectangleF(pad, pad + Sf(3) - lift, Width - pad * 2 - 1, Height - pad * 2 - Sf(3) - 1);
        using var path = Round(r, Sf(14));

        Glow(g, path, Accent, _h * 0.7f, Sf(3));
        using (var b = new SolidBrush(Lerp(Surface, Surface2, _h))) g.FillPath(b, path);
        using (var lg = new LinearGradientBrush(r, A(Accent, 0.10f * _h), A(Accent, 0f), LinearGradientMode.Vertical))
            g.FillPath(lg, path);
        using (var pen = new Pen(Lerp(Border, A(Accent, 0.75f), _h), 1)) g.DrawPath(pen, path);

        // preview area
        float box = Sf(52);
        var pr = new RectangleF(r.X + (r.Width - box) / 2, r.Y + Sf(16), box, box);
        if (_preview != null)
        {
            using (var gp = new GraphicsPath())
            {
                var spot = RectangleF.Inflate(pr, Sf(10), Sf(10));
                gp.AddEllipse(spot);
                using var pb = new PathGradientBrush(gp) { CenterColor = A(Accent, 0.16f + 0.14f * _h), SurroundColors = new[] { A(Accent, 0f) } };
                g.FillPath(pb, gp);
            }
            float pop = EaseOut(_pop);
            float sc = (0.6f + 0.4f * pop) * (1f + 0.08f * _h);
            float iw = _preview.Width * sc, ih = _preview.Height * sc;
            var ir = new RectangleF(pr.X + (pr.Width - iw) / 2, pr.Y + (pr.Height - ih) / 2, iw, ih);
            g.DrawImage(_preview, ir);
        }
        else
        {
            using var dash = new Pen(Lerp(Faint, Accent, _h), Sf(1.4f)) { DashStyle = DashStyle.Dash };
            var cr = RectangleF.Inflate(pr, -Sf(6), -Sf(6));
            g.DrawEllipse(dash, cr);
            DrawText(g, GAdd, Icons(12f), Lerp(Dim, Accent, _h), cr, StringAlignment.Center);
        }

        DrawText(g, Title, Semi(9.75f), Theme.Text, new RectangleF(r.X + Sf(6), r.Bottom - Sf(48), r.Width - Sf(12), Sf(20)), StringAlignment.Center);
        string cap = _preview == null ? (IsMain ? "Click to choose" : Caption) : Caption;
        DrawText(g, cap, F(8f), IsMain && _preview == null ? Accent : Dim,
            new RectangleF(r.X + Sf(6), r.Bottom - Sf(28), r.Width - Sf(12), Sf(18)), StringAlignment.Center);

        if (IsMain)
        {
            var badge = new RectangleF(r.X + Sf(10), r.Y + Sf(10), Sf(36), Sf(16));
            using var bp = Round(badge, Sf(8));
            using var bb = AccentBrush(badge, 0.9f);
            g.FillPath(bb, bp);
            DrawText(g, "MAIN", Bold(6.5f), Color.White, badge, StringAlignment.Center);
        }

        if (_preview != null && _h > 0.02f)
        {
            var xr = XRect;
            xr.Y -= lift - Sf(3);
            using var xb = new SolidBrush(A(Lerp(Surface3, Theme.Danger, _x), _h));
            g.FillEllipse(xb, xr);
            DrawText(g, GX, Icons(7f), A(Color.White, _h), xr, StringAlignment.Center);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _preview?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Wrapping chips, one selectable. Selected chip is filled with the accent gradient.</summary>
public class ChipGroup : AnimControl
{
    string[] _items = Array.Empty<string>();
    RectangleF[] _rects = Array.Empty<RectangleF>();
    float[] _hv = Array.Empty<float>(), _sv = Array.Empty<float>();
    int _sel, _hover = -1;

    public event EventHandler? SelectedChanged;

    public ChipGroup() { Cursor = Cursors.Hand; }

    public string[] Items
    {
        get => _items;
        set { _items = value; _hv = new float[value.Length]; _sv = new float[value.Length]; if (_sel < _sv.Length) _sv[_sel] = 1; Relayout(); }
    }

    public int Selected
    {
        get => _sel;
        set { if (value == _sel) return; _sel = value; Kick(); SelectedChanged?.Invoke(this, EventArgs.Empty); }
    }

    public void SetSilently(int i)
    {
        _sel = i;
        for (int k = 0; k < _sv.Length; k++) _sv[k] = k == i ? 1 : 0;
        Invalidate();
    }

    public int MeasureHeight() { Relayout(); return _rects.Length == 0 ? 0 : (int)Math.Ceiling(_rects.Max(r => r.Bottom)) + 2; }

    void Relayout()
    {
        _rects = new RectangleF[_items.Length];
        float x = 0, y = 0, h = Sf(30), gap = Sf(7);
        for (int i = 0; i < _items.Length; i++)
        {
            float w = TextRenderer.MeasureText(_items[i], Semi(9f)).Width + Sf(22);
            if (x > 0 && x + w > Width) { x = 0; y += h + gap; }
            _rects[i] = new RectangleF(x, y, w, h);
            x += w + gap;
        }
        Invalidate();
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }

    protected override bool Step()
    {
        bool more = false;
        for (int i = 0; i < _items.Length; i++)
        {
            more |= Approach(ref _hv[i], i == _hover && Enabled ? 1 : 0, 0.22f);
            more |= Approach(ref _sv[i], i == _sel ? 1 : 0, 0.2f);
        }
        return more;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = -1;
        for (int i = 0; i < _rects.Length; i++) if (_rects[i].Contains(e.Location)) h = i;
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
        for (int i = 0; i < _items.Length; i++)
        {
            var r = _rects[i];
            r.Width -= 1; r.Height -= 1;
            using var p = Round(r, r.Height / 2);
            using (var b = new SolidBrush(Lerp(Surface2, Surface3, _hv[i]))) g.FillPath(b, p);
            if (_sv[i] > 0.01f)
            {
                Glow(g, p, Accent, _sv[i] * 0.6f, Sf(2));
                using var ab = AccentBrush(r, _sv[i]);
                g.FillPath(ab, p);
            }
            using (var pen = new Pen(A(Lerp(Border, BorderHi, _hv[i]), 1 - _sv[i]))) g.DrawPath(pen, p);
            DrawText(g, _items[i], Semi(9f), Lerp(Lerp(Dim, Theme.Text, _hv[i]), Color.White, _sv[i]), r, StringAlignment.Center);
        }
        DimIfDisabled(g);
    }
}

/// <summary>Labeled slider with a gradient fill and a growing thumb.</summary>
public class Slider : AnimControl
{
    float _h, _value;
    bool _drag;
    public float Min { get; set; } = 0;
    public float Max { get; set; } = 1;
    public float StepSize { get; set; } = 0.05f;
    public string Label { get; set; } = "";
    public Func<float, string> Format { get; set; } = v => v.ToString("0.##");
    public event EventHandler? ValueChanged, Committed;

    public float Value
    {
        get => _value;
        set { _value = Math.Clamp(value, Min, Max); Invalidate(); }
    }

    public Slider() { Cursor = Cursors.Hand; Height = S(50); }

    float TrackLeft => Sf(9);
    float TrackWidth => Width - Sf(18);
    float TrackY => Height - Sf(14);

    void SetFromX(float x)
    {
        float t = Clamp01((x - TrackLeft) / TrackWidth);
        float v = Min + t * (Max - Min);
        v = MathF.Round(v / StepSize) * StepSize;
        v = Math.Clamp(v, Min, Max);
        if (Math.Abs(v - _value) > 0.0001f) { _value = v; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _drag = true; Capture = true; SetFromX(e.X); Kick();
    }

    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (_drag) SetFromX(e.X); }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_drag) return;
        _drag = false; Capture = false; Kick();
        Committed?.Invoke(this, EventArgs.Empty);
    }

    protected override bool Step() => Approach(ref _h, Hovered || _drag ? 1 : 0, 0.25f);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);
        DrawText(g, Label, Semi(9.5f), Theme.Text, new RectangleF(Sf(4), 0, Width / 2f, Sf(24)));
        DrawText(g, Format(_value), Semi(9.5f), Lerp(Dim, Accent, _h), new RectangleF(Width / 2f, 0, Width / 2f - Sf(4), Sf(24)), StringAlignment.Far);

        float th = Sf(5), t = (Max - Min) <= 0 ? 0 : (_value - Min) / (Max - Min);
        var track = new RectangleF(TrackLeft, TrackY - th / 2, TrackWidth, th);
        using (var p = Round(track, th / 2)) using (var b = new SolidBrush(Surface3)) g.FillPath(b, p);
        var fill = new RectangleF(track.X, track.Y, Math.Max(th, track.Width * t), th);
        using (var p = Round(fill, th / 2))
        {
            Glow(g, p, Accent, 0.5f + _h * 0.5f, Sf(2));
            using var b = AccentBrush(fill);
            g.FillPath(b, p);
        }
        float kr = Sf(7) + _h * Sf(2);
        float kx = track.X + track.Width * t;
        using (var gb = new SolidBrush(A(Accent, 0.25f * _h))) g.FillEllipse(gb, kx - kr - Sf(5), TrackY - kr - Sf(5), (kr + Sf(5)) * 2, (kr + Sf(5)) * 2);
        using (var kb = new SolidBrush(Color.White)) g.FillEllipse(kb, kx - kr, TrackY - kr, kr * 2, kr * 2);
        DimIfDisabled(g);
    }
}

/// <summary>Row of color presets plus a custom color picker circle.</summary>
public class SwatchRow : AnimControl
{
    public static readonly Color[] Presets =
    {
        Theme.Accent, Theme.Accent2, Theme.Success, Theme.Warn, Theme.Danger,
        Color.FromArgb(255, 120, 210), Color.White
    };

    Color _value = Theme.Accent;
    int _hover = -1;
    readonly float[] _hv = new float[Presets.Length + 1], _sv = new float[Presets.Length + 1];
    public event EventHandler? ValueChanged;

    public SwatchRow() { Cursor = Cursors.Hand; Height = S(36); }

    public Color Value
    {
        get => _value;
        set { _value = value; Kick(); }
    }

    int SelIndex()
    {
        for (int i = 0; i < Presets.Length; i++) if (Presets[i].ToArgb() == _value.ToArgb()) return i;
        return Presets.Length; // custom
    }

    RectangleF Circle(int i) { float d = Sf(24), gap = Sf(12); return new RectangleF(Sf(6) + i * (d + gap), (Height - d) / 2, d, d); }

    protected override bool Step()
    {
        int sel = SelIndex();
        bool more = false;
        for (int i = 0; i <= Presets.Length; i++)
        {
            more |= Approach(ref _hv[i], i == _hover && Enabled ? 1 : 0, 0.25f);
            more |= Approach(ref _sv[i], i == sel ? 1 : 0, 0.2f);
        }
        return more;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = -1;
        for (int i = 0; i <= Presets.Length; i++) if (RectangleF.Inflate(Circle(i), Sf(4), Sf(4)).Contains(e.Location)) h = i;
        if (h != _hover) { _hover = h; Kick(); }
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_hover < 0) return;
        if (_hover < Presets.Length) _value = Presets[_hover];
        else
        {
            using var dlg = new ColorDialog { Color = _value, FullOpen = true };
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            _value = dlg.Color;
        }
        Kick();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);
        int sel = SelIndex();
        for (int i = 0; i <= Presets.Length; i++)
        {
            var c = Circle(i);
            float grow = _hv[i] * Sf(2);
            var cr = RectangleF.Inflate(c, grow, grow);
            if (_sv[i] > 0.01f)
            {
                float ring = Sf(4) + _sv[i] * Sf(1);
                var rr = RectangleF.Inflate(cr, ring, ring);
                using var rp = new Pen(A(Color.White, 0.9f * _sv[i]), Sf(1.6f));
                g.DrawEllipse(rp, rr);
            }
            if (i < Presets.Length)
            {
                using var b = new SolidBrush(Presets[i]);
                g.FillEllipse(b, cr);
            }
            else
            {
                if (sel == Presets.Length)
                {
                    using var b = new SolidBrush(_value);
                    g.FillEllipse(b, cr);
                }
                else
                {
                    using (var b = new SolidBrush(Lerp(Surface3, BorderHi, _hv[i]))) g.FillEllipse(b, cr);
                    using (var p = new Pen(BorderHi) { DashStyle = DashStyle.Dot }) g.DrawEllipse(p, cr);
                }
                DrawText(g, GAdd, Icons(8f), sel == Presets.Length ? Color.Black : Theme.Text, cr, StringAlignment.Center);
            }
        }
        DimIfDisabled(g);
    }
}

/// <summary>Rounded text input with an animated focus glow.</summary>
public class InputBox : AnimControl
{
    float _f;
    public readonly TextBox Box = new() { BorderStyle = BorderStyle.None, BackColor = Theme.Surface2, ForeColor = Theme.Text };
    public string Glyph { get; set; } = GSearch;

    public InputBox()
    {
        Height = S(42);
        Cursor = Cursors.IBeam;
        Box.Font = F(10.5f);
        Controls.Add(Box);
        Box.GotFocus += (_, _) => Kick();
        Box.LostFocus += (_, _) => Kick();
        Box.MouseEnter += (_, _) => { Hovered = true; Kick(); };
        Box.MouseLeave += (_, _) => { Hovered = false; Kick(); };
    }

    protected override void OnClick(EventArgs e) { base.OnClick(e); Box.Focus(); }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Box.Left = Glyph != "" ? S(40) : S(14);
        Box.Width = Math.Max(10, Width - Box.Left - S(14));
        Box.Top = (Height - Box.Height) / 2;
    }

    protected override bool Step() => Approach(ref _f, Box.Focused ? 1 : Hovered ? 0.35f : 0, 0.22f);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);
        float inset = Sf(2);
        var r = new RectangleF(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1);
        using var p = Round(r, Sf(11));
        if (Box.Focused) Glow(g, p, Accent, _f * 0.7f, Sf(1.5f));
        using (var b = new SolidBrush(Surface2)) g.FillPath(b, p);
        using (var pen = new Pen(Lerp(Border, Accent, _f), 1)) g.DrawPath(pen, p);
        if (Glyph != "")
            DrawText(g, Glyph, Icons(10f), Lerp(Dim, Accent, _f), new RectangleF(Sf(14), 0, Sf(24), Height));
    }
}

/// <summary>Smooth-scrolling list of running apps with icons, hover highlight and a staggered fade-in.</summary>
public class AppList : AnimControl
{
    public sealed record Item(string Name, Image? Icon);

    List<Item> _all = new(), _view = new();
    string _filter = "", _selected = "";
    float _scroll, _scrollTarget, _appear;
    float[] _hv = Array.Empty<float>();
    int _hover = -1;
    public bool Loading { get; set; }

    public event Action<string>? Picked;

    public AppList() { Cursor = Cursors.Hand; }

    float RowH => Sf(44);
    float MaxScroll => Math.Max(0, _view.Count * RowH + Sf(12) - Height);

    public void SetItems(List<Item> items)
    {
        foreach (var it in _all) it.Icon?.Dispose();
        _all = items;
        Loading = false;
        _appear = 0;
        Apply();
    }

    public void SetFilter(string f) { _filter = f.Trim(); Apply(); }
    public void SetSelected(string name) { _selected = name; Invalidate(); }

    void Apply()
    {
        _view = _filter == ""
            ? _all.ToList()
            : _all.Where(i => i.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                  .OrderBy(i => i.Name.Equals(_filter, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                  .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        _hv = new float[_view.Count];
        _scrollTarget = Math.Clamp(_scrollTarget, 0, MaxScroll);
        _hover = -1;
        Kick();
    }

    public void ScrollBy(int wheelDelta)
    {
        _scrollTarget = Math.Clamp(_scrollTarget - wheelDelta / 120f * RowH * 1.5f, 0, MaxScroll);
        Kick();
    }

    protected override bool Step()
    {
        bool more = Approach(ref _scroll, _scrollTarget, 0.22f, 0.3f);
        more |= Approach(ref _appear, 1, 0.06f, 0.001f);
        for (int i = 0; i < _hv.Length; i++) more |= Approach(ref _hv[i], i == _hover ? 1 : 0, 0.25f);
        return more || Loading;
    }

    int RowAt(Point p)
    {
        int i = (int)((p.Y + _scroll - Sf(6)) / RowH);
        return i >= 0 && i < _view.Count && p.Y >= Sf(6) ? i : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = RowAt(e.Location);
        if (h != _hover) { _hover = h; Kick(); }
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int i = RowAt(e.Location);
        if (i < 0) return;
        _selected = _view[i].Name;
        Picked?.Invoke(_selected);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);
        var outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var op = Round(outer, Sf(12));
        using (var b = new SolidBrush(Surface2)) g.FillPath(b, op);
        using (var pen = new Pen(Border)) g.DrawPath(pen, op);

        var oldClip = g.Clip;
        g.SetClip(op);

        if (Loading)
        {
            float ph = (Environment.TickCount64 % 1000) / 1000f;
            for (int k = 0; k < 3; k++)
            {
                float a = 0.3f + 0.7f * MathF.Max(0, MathF.Sin((ph - k * 0.15f) * MathF.PI * 2));
                float d = Sf(7), x = Width / 2f - Sf(16) + k * Sf(14);
                using var db = new SolidBrush(A(Accent, a));
                g.FillEllipse(db, x, Height / 2f - d / 2, d, d);
            }
        }
        else if (_view.Count == 0)
        {
            DrawText(g, _filter == "" ? "No windows found" : $"No running app matches \"{_filter}\" – it will still work once it's open",
                F(9f), Faint, new RectangleF(Sf(12), 0, Width - Sf(24), Height), StringAlignment.Center);
        }

        int first = Math.Max(0, (int)(_scroll / RowH) - 1);
        for (int i = first; i < _view.Count; i++)
        {
            float y = Sf(6) + i * RowH - _scroll;
            if (y > Height) break;
            float ap = EaseOut(Clamp01(_appear * 2.2f - Math.Min(i - first, 12) * 0.08f));
            if (ap <= 0) continue;
            y += (1 - ap) * Sf(14);

            var it = _view[i];
            var row = new RectangleF(Sf(6), y, Width - Sf(16), RowH - Sf(4));
            bool sel = it.Name.Equals(_selected, StringComparison.OrdinalIgnoreCase);
            using (var rp = Round(row, Sf(10)))
            {
                if (sel)
                {
                    using var lg = new LinearGradientBrush(row, A(Accent, 0.22f * ap), A(Accent2, 0.05f * ap), LinearGradientMode.Horizontal);
                    g.FillPath(lg, rp);
                    using var sp = new Pen(A(Accent, 0.45f * ap));
                    g.DrawPath(sp, rp);
                }
                else if (_hv[i] > 0.01f)
                {
                    using var hb = new SolidBrush(A(Surface3, _hv[i] * ap));
                    g.FillPath(hb, rp);
                }
            }

            float isz = Sf(22), shift = _hv[i] * Sf(3);
            var ir = new RectangleF(row.X + Sf(12) + shift, row.Y + (row.Height - isz) / 2, isz, isz);
            if (it.Icon != null) g.DrawImage(it.Icon, ir);
            else
            {
                using var ip = Round(ir, Sf(6));
                using var ib = AccentBrush(ir, 0.8f * ap);
                g.FillPath(ib, ip);
                DrawText(g, it.Name[..1].ToUpperInvariant(), Bold(8f), Color.White, ir, StringAlignment.Center);
            }
            DrawText(g, it.Name, F(10f), A(Theme.Text, ap), new RectangleF(ir.Right + Sf(12), row.Y, row.Width - Sf(140), row.Height));
            string right = sel ? "Selected" : _hv[i] > 0.3f ? "Use this" : "";
            if (right != "")
                DrawText(g, right, Semi(8.5f), sel ? Accent : A(Dim, _hv[i]), new RectangleF(row.Right - Sf(110), row.Y, Sf(98), row.Height), StringAlignment.Far);
        }

        // scrollbar
        if (MaxScroll > 0)
        {
            float viewH = Height - Sf(12), contentH = _view.Count * RowH;
            float bh = Math.Max(Sf(24), viewH * viewH / contentH);
            float by = Sf(6) + (viewH - bh) * (_scroll / MaxScroll);
            var sb = new RectangleF(Width - Sf(7), by, Sf(3), bh);
            using var sp = Round(sb, Sf(1.5f));
            using var sbb = new SolidBrush(Hovered ? BorderHi : Surface3);
            g.FillPath(sbb, sp);
        }
        g.Clip = oldClip;
    }
}

/// <summary>Routes mouse-wheel messages to the AppList under the cursor (no focus needed).</summary>
public sealed class WheelRouter : IMessageFilter
{
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != 0x020A) return false; // WM_MOUSEWHEEL
        Native.GetCursorPos(out var pt);
        var c = Control.FromChildHandle(Native.WindowFromPoint(pt));
        while (c != null && c is not AppList) c = c.Parent;
        if (c is AppList list)
        {
            list.ScrollBy((short)((long)m.WParam >> 16));
            return true;
        }
        return false;
    }
}

/// <summary>Live looping preview of the left/right click effects.</summary>
public class EffectPreview : AnimControl
{
    readonly List<(bool Right, long Start, int Seed)> _live = new();
    long _next;
    bool _right;

    public Func<(ClickAnimation Anim, Color Color, bool On)> LeftFx { get; set; } = () => (ClickAnimation.Ripple, Theme.Accent, true);
    public Func<(ClickAnimation Anim, Color Color, bool On)> RightFx { get; set; } = () => (ClickAnimation.Sparkle, Theme.Accent2, true);
    public Func<float> ScaleFx { get; set; } = () => 1f;
    public Func<float> SpeedFx { get; set; } = () => 1f;
    public Func<bool> MasterOn { get; set; } = () => true;

    public EffectPreview() { Cursor = Cursors.Hand; }

    /// <summary>Fire an effect immediately on one side (e.g. when a setting changes).</summary>
    public void Fire(bool right)
    {
        _live.Add((right, Environment.TickCount64, Environment.TickCount + (right ? 7 : 0)));
        Kick();
    }

    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) Kick(); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Fire(e.Button == MouseButtons.Right || e.X > Width / 2);
    }

    float Duration => EffectRenderer.BaseDurationMs / Math.Max(0.1f, SpeedFx());

    protected override bool Step()
    {
        if (!Visible || FindForm() is not { Visible: true }) return false;
        long now = Environment.TickCount64;
        if (now >= _next)
        {
            _live.Add((_right, now, (int)now));
            _right = !_right;
            _next = now + (long)Math.Max(380, Duration + 280);
        }
        _live.RemoveAll(l => now - l.Start > Duration);
        return true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        HQ(g);
        g.Clear(BackColor);
        var outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var op = Round(outer, Sf(16));
        using (var b = new SolidBrush(Surface)) g.FillPath(b, op);
        var oldClip = g.Clip;
        g.SetClip(op);

        // dotted grid
        float step = Sf(18);
        using (var db = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
            for (float y = step / 2; y < Height; y += step)
                for (float x = step / 2; x < Width; x += step)
                    g.FillEllipse(db, x - 1, y - 1, 2, 2);

        // divider
        using (var dp = new Pen(Border) { DashStyle = DashStyle.Dash }) g.DrawLine(dp, Width / 2f, Sf(14), Width / 2f, Height - Sf(14));

        var cl = new PointF(Width * 0.25f, Height / 2f + Sf(8));
        var cr = new PointF(Width * 0.75f, Height / 2f + Sf(8));
        var lf = LeftFx(); var rf = RightFx();
        bool master = MasterOn();

        DrawText(g, "LEFT CLICK", Bold(7.5f), Faint, new RectangleF(0, Sf(10), Width / 2f, Sf(16)), StringAlignment.Center);
        DrawText(g, "RIGHT CLICK", Bold(7.5f), Faint, new RectangleF(Width / 2f, Sf(10), Width / 2f, Sf(16)), StringAlignment.Center);

        long now = Environment.TickCount64;
        foreach (var l in _live)
        {
            var fx = l.Right ? rf : lf;
            if (!master || !fx.On) continue;
            float t = (now - l.Start) / Duration;
            EffectRenderer.Draw(g, fx.Anim, t, l.Right ? cr : cl, fx.Color, ScaleFx() * Theme.Scale, l.Seed);
        }

        foreach (var (c, on) in new[] { (cl, master && lf.On), (cr, master && rf.On) })
        {
            if (!on) DrawText(g, "Off", Semi(9f), Faint, new RectangleF(c.X - Sf(40), c.Y + Sf(18), Sf(80), Sf(18)), StringAlignment.Center);
            Pointer(g, c, on);
        }

        DrawText(g, "Click here to test", F(7.5f), Faint, new RectangleF(0, Height - Sf(24), Width - Sf(14), Sf(16)), StringAlignment.Far);
        g.Clip = oldClip;
        using (var pen = new Pen(Border)) g.DrawPath(pen, op);
    }

    static void Pointer(Graphics g, PointF tip, bool on)
    {
        float s = Sf(1.05f);
        PointF P(float x, float y) => new(tip.X + x * s, tip.Y + y * s);
        var pts = new[] { P(0, 0), P(0, 17), P(4.2f, 13), P(7.4f, 19.4f), P(10, 18.2f), P(6.9f, 11.9f), P(12.4f, 11.9f) };
        using var b = new SolidBrush(on ? Color.White : Faint);
        using var p = new Pen(Color.Black, Sf(1.2f)) { LineJoin = LineJoin.Round };
        g.FillPolygon(b, pts);
        g.DrawPolygon(p, pts);
    }
}
