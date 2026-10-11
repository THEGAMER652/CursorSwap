using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CursorSwap;

/// <summary>
/// Swaps the real Windows cursors (one image per cursor state) while active and
/// restores the user's own cursor scheme afterwards. Because it replaces the actual
/// system cursors there is no lag and no double cursor.
/// </summary>
public sealed class CursorManager : IDisposable
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadCursorFromFile(string fileName);

    sealed class Src
    {
        public Bitmap? Image;     // .png/.jpg/.bmp/.gif/.ico
        public string? File;      // .cur/.ani
        public int HX, HY;
    }

    public static readonly IReadOnlyDictionary<CursorRole, uint[]> RoleIds = new Dictionary<CursorRole, uint[]>
    {
        [CursorRole.Normal]      = new uint[] { 32512, 32516 },               // arrow, up-arrow
        [CursorRole.Link]        = new uint[] { 32649 },                      // hand
        [CursorRole.Text]        = new uint[] { 32513 },                      // I-beam
        [CursorRole.Busy]        = new uint[] { 32514 },                      // wait
        [CursorRole.Working]     = new uint[] { 32650 },                      // app starting
        [CursorRole.Precision]   = new uint[] { 32515 },                      // cross
        [CursorRole.Unavailable] = new uint[] { 32648 },                      // no
        [CursorRole.Move]        = new uint[] { 32646 },                      // size all
        [CursorRole.Resize]      = new uint[] { 32642, 32643, 32644, 32645 }, // all resize arrows
        [CursorRole.Help]        = new uint[] { 32651 },                      // help
    };

    readonly Dictionary<CursorRole, Src> _src = new();
    bool _fallbackToMain = true;

    public bool IsApplied { get; private set; }
    public bool HasCursor => _src.Count > 0;
    public bool Has(CursorRole r) => _src.ContainsKey(r);

    /// <summary>Loads every configured cursor. Returns a list of error messages (empty = all good).</summary>
    public List<string> Load(Settings s)
    {
        bool wasApplied = IsApplied;
        Restore();
        Clear();
        _fallbackToMain = s.UnsetUseMain;
        var errors = new List<string>();

        foreach (var role in Enum.GetValues<CursorRole>())
        {
            if (!s.Cursors.TryGetValue(role.ToString(), out var rs) || rs == null || string.IsNullOrWhiteSpace(rs.Path))
                continue;
            try { _src[role] = LoadSrc(rs.Path, s.CursorSize, rs.Hotspot); }
            catch (Exception ex) { errors.Add($"{role}: {ex.Message}"); }
        }

        if (wasApplied) Apply();
        return errors;
    }

    static Src LoadSrc(string path, int size, Hotspot hs)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("file not found");
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext is ".cur" or ".ani")
        {
            var test = LoadCursorFromFile(path);
            if (test == IntPtr.Zero) throw new InvalidDataException("could not load cursor file");
            Native.DestroyCursor(test);
            return new Src { File = path };
        }

        using var src = new Bitmap(path);
        float scale = Math.Min((float)size / src.Width, (float)size / src.Height);
        int w = Math.Max(1, (int)(src.Width * scale)), h = Math.Max(1, (int)(src.Height * scale));
        var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.DrawImage(src, 0, 0, w, h);
        }
        return new Src
        {
            Image = bmp,
            HX = hs is Hotspot.Center or Hotspot.TopCenter ? w / 2 : 0,
            HY = hs == Hotspot.Center ? h / 2 : 0
        };
    }

    /// <summary>Small preview image for the UI (works for png, .cur and the first frame of .ani).</summary>
    public static Image? MakePreview(string path, int box)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Bitmap full;
            if (ext is ".cur" or ".ani")
            {
                var h = LoadCursorFromFile(path);
                if (h == IntPtr.Zero) return null;
                try { using var ic = Icon.FromHandle(h); full = ic.ToBitmap(); }
                finally { Native.DestroyCursor(h); }
            }
            else full = new Bitmap(path);

            using (full)
            {
                float scale = Math.Min((float)box / full.Width, (float)box / full.Height);
                int w = Math.Max(1, (int)(full.Width * scale)), hh = Math.Max(1, (int)(full.Height * scale));
                var bmp = new Bitmap(w, hh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(full, 0, 0, w, hh);
                return bmp;
            }
        }
        catch { return null; }
    }

    IntPtr CreateHandle(Src s)
    {
        if (s.File != null) return LoadCursorFromFile(s.File);
        if (s.Image == null) return IntPtr.Zero;

        IntPtr color = s.Image.GetHbitmap(Color.FromArgb(0, 0, 0, 0));
        IntPtr mask = Native.CreateBitmap(s.Image.Width, s.Image.Height, 1, 1, null);
        var info = new Native.ICONINFO { fIcon = false, xHotspot = s.HX, yHotspot = s.HY, hbmColor = color, hbmMask = mask };
        IntPtr h = Native.CreateIconIndirect(ref info);
        Native.DeleteObject(color);
        Native.DeleteObject(mask);
        return h;
    }

    public void Apply()
    {
        if (IsApplied || !HasCursor) return;
        foreach (var (role, ids) in RoleIds)
        {
            Src? src = _src.GetValueOrDefault(role) ?? (_fallbackToMain ? _src.GetValueOrDefault(CursorRole.Normal) : null);
            if (src == null) continue; // keep Windows' own cursor for this state
            foreach (var id in ids)
            {
                // SetSystemCursor takes ownership of (and destroys) the handle, so each id needs its own one
                var h = CreateHandle(src);
                if (h != IntPtr.Zero) Native.SetSystemCursor(h, id);
            }
        }
        IsApplied = true;
    }

    public void Restore()
    {
        if (!IsApplied) return;
        ForceRestore();
        IsApplied = false;
    }

    /// <summary>Reloads the user's own cursor scheme from the registry.</summary>
    public static void ForceRestore() =>
        Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, 0);

    void Clear()
    {
        foreach (var s in _src.Values) s.Image?.Dispose();
        _src.Clear();
    }

    public void Dispose()
    {
        Restore();
        Clear();
    }
}
