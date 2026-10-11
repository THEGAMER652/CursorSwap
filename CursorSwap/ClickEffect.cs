using System.Drawing.Imaging;

namespace CursorSwap;

/// <summary>
/// A small transparent, click-through, always-on-top window that plays one click
/// animation at a screen point and then closes itself.
/// </summary>
public sealed class ClickEffect : Form
{
    readonly int _size;
    readonly float _scale, _durationMs;
    readonly ClickAnimation _anim;
    readonly Color _color;
    readonly Point _center;
    readonly int _seed = Environment.TickCount;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    readonly DateTime _start = DateTime.UtcNow;

    public ClickEffect(Point screenPoint, ClickAnimation anim, Color color, float scale, float speed)
    {
        _anim = anim;
        _color = color;
        _center = screenPoint;
        _scale = scale;
        _durationMs = EffectRenderer.BaseDurationMs / Math.Max(0.1f, speed);
        _size = (int)(2 * (EffectRenderer.Reach + 10) * scale);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Bounds = new Rectangle(screenPoint.X - _size / 2, screenPoint.Y - _size / 2, _size, _size);

        _timer.Tick += (_, _) => Render();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW
                          | Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Render();
        _timer.Start();
    }

    void Render()
    {
        float t = (float)(DateTime.UtcNow - _start).TotalMilliseconds / _durationMs;
        if (t >= 1f) { _timer.Stop(); Close(); return; }

        using var bmp = new Bitmap(_size, _size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            EffectRenderer.Draw(g, _anim, t, new PointF(_size / 2f, _size / 2f), _color, _scale, _seed);
        }
        Push(bmp);
    }

    void Push(Bitmap bmp)
    {
        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr old = Native.SelectObject(memDc, hBmp);
        try
        {
            var size = new Native.SIZE(_size, _size);
            var src = new Native.POINT();
            var dst = new Native.POINT { X = _center.X - _size / 2, Y = _center.Y - _size / 2 };
            var blend = new Native.BLENDFUNCTION
            {
                BlendOp = Native.AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = Native.AC_SRC_ALPHA
            };
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
        }
        finally
        {
            Native.SelectObject(memDc, old);
            Native.DeleteObject(hBmp);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
