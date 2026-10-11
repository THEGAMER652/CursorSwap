using System.Diagnostics;
using Microsoft.Win32;
using static CursorSwap.Theme;

namespace CursorSwap;

public sealed class MainForm : Form
{
    readonly Settings _s = Settings.Load();
    readonly CursorManager _cursor = new();
    readonly System.Windows.Forms.Timer _loop = new() { Interval = 10 };
    readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 400 };
    readonly System.Windows.Forms.Timer _uiAnim = new() { Interval = 15 };
    readonly Dictionary<uint, string> _pidNames = new();
    readonly int _ownPid = Environment.ProcessId;
    DateTime _pidCacheTime = DateTime.UtcNow;
    bool _leftWasDown, _rightWasDown, _exiting, _startHidden;
    string? _notice;
    DateTime _noticeUntil;

    // window / page animation state
    Panel? _pageIn;
    DateTime _pageAnimStart, _fadeStart;
    int _pageBaseX;
    float _fadeFrom, _fadeTo;
    bool _fadeHideAfter;
    int _shownTop;

    // chrome
    readonly TitleBar _title = new();
    readonly Sidebar _side = new();
    readonly StatusPill _pill = new();
    readonly NotifyIcon _tray = new();
    readonly List<Panel> _pages = new();

    // home
    readonly ToggleRow _tEnabled = new() { Title = "Enabled", Desc = "Swap the cursor while hovering the target" };
    readonly ToggleRow _tStartup = new() { Title = "Start with Windows", Desc = "Launches hidden in the tray when you log in" };
    readonly InputBox _target = new() { Glyph = GTarget };
    readonly FlatButton _refresh = new() { Text = "Refresh", Glyph = GRefresh };
    readonly FlatButton _pick = new() { Text = "Pick window", Glyph = GCursor, Primary = true };
    readonly AppList _apps = new();

    // cursors
    readonly Dictionary<CursorRole, RoleTile> _tiles = new();
    readonly Slider _size = new() { Label = "Cursor size", Min = 16, Max = 128, StepSize = 4, Format = v => $"{v:0} px" };
    readonly ToggleRow _tFallback = new() { Title = "Use main cursor for unset states", Desc = "Off = keep Windows' own cursor for those" };
    readonly FlatButton _clearAll = new() { Text = "Clear all", Glyph = GDelete, Danger = true };

    // effects
    readonly ToggleRow _tEffects = new() { Title = "Click effects", Desc = "Master switch for every click animation" };
    readonly Slider _fxScale = new() { Label = "Size", Min = 0.5f, Max = 2f, StepSize = 0.05f, Format = v => $"{v * 100:0}%" };
    readonly Slider _fxSpeed = new() { Label = "Speed", Min = 0.5f, Max = 2f, StepSize = 0.05f, Format = v => $"{v:0.0#}x" };
    readonly ToggleRow _tLeft = new() { Title = "Left click", Desc = "Primary button" };
    readonly ToggleRow _tRight = new() { Title = "Right click", Desc = "Secondary button" };
    readonly ChipGroup _chipL = new(), _chipR = new();
    readonly SwatchRow _swL = new(), _swR = new();
    readonly EffectPreview _preview = new();

    static readonly (CursorRole Role, string Title, string Caption)[] RoleInfo =
    {
        (CursorRole.Normal, "Main cursor", "Normal pointer"),
        (CursorRole.Link, "Link", "Links & buttons"),
        (CursorRole.Text, "Text", "Text fields"),
        (CursorRole.Busy, "Busy", "Loading"),
        (CursorRole.Working, "Working", "Background work"),
        (CursorRole.Precision, "Precision", "Crosshair"),
        (CursorRole.Unavailable, "Unavailable", "Not allowed"),
        (CursorRole.Move, "Move", "Drag & move"),
        (CursorRole.Resize, "Resize", "Edges & corners"),
        (CursorRole.Help, "Help", "Help select"),
    };

    public MainForm(bool startInTray)
    {
        Theme.Scale = DeviceDpi / 96f;
        _startHidden = startInTray;

        Text = "CursorSwap";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = Theme.Text;
        Font = F(9.5f);
        ClientSize = new Size(S(940), S(660));
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? MakeAppIcon(32); }
        catch { Icon = MakeAppIcon(32); }
        DoubleBuffered = true;
        Opacity = 0;

        BuildChrome();
        BuildHome();
        BuildCursors();
        BuildEffects();
        LoadIntoUi();
        HookEvents();
        SetupTray();
        ShowPage(0, animate: false);

        Application.AddMessageFilter(new WheelRouter());
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _s.Save(); };
        _uiAnim.Tick += (_, _) => AnimateUi();
        _loop.Tick += (_, _) => Tick();
        _loop.Start();
        SystemEvents.SessionEnding += (_, _) => _cursor.Restore();
    }

    // ================= window chrome =================

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
            cp.Style |= 0x20000;      // WS_MINIMIZEBOX (taskbar minimize/restore)
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int round = 2, dark = 1, border = Border.R | (Border.G << 8) | (Border.B << 16);
            Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);  // rounded corners (Win11)
            Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);   // dark mode
            Native.DwmSetWindowAttribute(Handle, 34, ref border, 4); // border color (Win11)
        }
        catch { }
    }

    protected override void SetVisibleCore(bool value)
    {
        // When launched at Windows startup (--tray), stay hidden in the tray
        if (_startHidden)
        {
            _startHidden = false;
            if (!IsHandleCreated) CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FadeIn();
    }

    void FadeIn()
    {
        if (_fadeTo == 1 && _fadeFrom != _fadeTo) return; // already fading in
        _shownTop = Top;
        Top = _shownTop + S(16);
        _fadeFrom = (float)Opacity; _fadeTo = 1; _fadeHideAfter = false;
        _fadeStart = DateTime.UtcNow;
        _uiAnim.Start();
        _preview.Kick();
    }

    void FadeOutToTray()
    {
        _shownTop = Top;
        _fadeFrom = (float)Opacity; _fadeTo = 0; _fadeHideAfter = true;
        _fadeStart = DateTime.UtcNow;
        _uiAnim.Start();
        if (!_s.TrayHintShown)
        {
            _s.TrayHintShown = true; _s.Save();
            _tray.ShowBalloonTip(2000, "CursorSwap", "Still running in the tray. Right-click the icon to exit.", ToolTipIcon.Info);
        }
    }

    void ShowFromTray()
    {
        if (!Visible) { Opacity = 0; Show(); }
        WindowState = FormWindowState.Normal;
        Activate();
        if (Opacity < 1) FadeIn();
    }

    void AnimateUi()
    {
        bool busy = false;

        // window fade
        if (_fadeFrom != _fadeTo)
        {
            float t = Clamp01((float)(DateTime.UtcNow - _fadeStart).TotalMilliseconds / 260f);
            float k = EaseOut(t);
            Opacity = _fadeFrom + (_fadeTo - _fadeFrom) * k;
            Top = _fadeTo > _fadeFrom ? _shownTop + (int)((1 - k) * S(16)) : _shownTop + (int)(k * S(10));
            if (t >= 1)
            {
                Top = _shownTop;
                _fadeFrom = _fadeTo;
                if (_fadeHideAfter) { Hide(); Opacity = 0; }
            }
            else busy = true;
        }

        // page slide-in
        if (_pageIn != null)
        {
            float t = Clamp01((float)(DateTime.UtcNow - _pageAnimStart).TotalMilliseconds / 280f);
            _pageIn.Left = _pageBaseX + (int)((1 - EaseOut(t)) * S(40));
            if (t >= 1) _pageIn = null; else busy = true;
        }

        if (!busy) _uiAnim.Stop();
    }

    void BuildChrome()
    {
        _title.SetBounds(0, 0, ClientSize.Width, S(44));
        _title.BackColor = Bg;
        _title.MinimizeClicked += (_, _) => WindowState = FormWindowState.Minimized;
        _title.CloseClicked += (_, _) => FadeOutToTray();
        Controls.Add(_title);

        _side.SetBounds(0, S(48), S(212), ClientSize.Height - S(48));
        _side.Items.Add((GHome, "Home"));
        _side.Items.Add((GCursor, "Cursors"));
        _side.Items.Add((GBolt, "Click effects"));
        _side.Footer = "CursorSwap v1.0";
        Controls.Add(_side);

        Controls.Add(_pill);
        _pill.BackColor = Bg;
        _pill.Top = (S(44) - _pill.Height) / 2;
        _pill.Left = ClientSize.Width - S(108) - _pill.Width;
        _pill.BringToFront();

        _pageBaseX = S(224);
    }

    Panel NewPage()
    {
        var p = new DoubleBufferedPanel { BackColor = Bg, Visible = false };
        p.SetBounds(_pageBaseX, S(56), ClientSize.Width - _pageBaseX - S(24), ClientSize.Height - S(56) - S(22));
        Controls.Add(p);
        _pages.Add(p);
        return p;
    }

    static void Header(Panel page, string title, string sub)
    {
        page.Controls.Add(new Label { Text = title, Font = Semi(20f), ForeColor = Theme.Text, BackColor = Bg, AutoSize = true, Location = new Point(0, 0) });
        page.Controls.Add(new Label { Text = sub, Font = F(9.5f), ForeColor = Dim, BackColor = Bg, AutoSize = true, Location = new Point(S(2), S(44)) });
    }

    static Card AddCard(Panel page, int x, int y, int w, int h, string title = "", string sub = "")
    {
        var c = new Card { Title = title, Sub = sub };
        c.SetBounds(x, y, w, h);
        page.Controls.Add(c);
        return c;
    }

    // ================= pages =================

    void BuildHome()
    {
        var p = NewPage();
        int W = p.Width, H = p.Height;
        Header(p, "Home", "Your cursor changes only while the mouse is over the program you choose.");

        var gen = AddCard(p, 0, S(84), W, S(122), "General");
        int half = (W - S(24)) / 2;
        gen.Controls.Add(_tEnabled); _tEnabled.SetBounds(S(8), S(50), half, S(56));
        gen.Controls.Add(_tStartup); _tStartup.SetBounds(S(16) + half, S(50), half, S(56));

        int ty = S(220);
        var tc = AddCard(p, 0, ty, W, H - ty, "Target program", "Type a process name, use Pick window, or choose a running app below.");
        int bw = S(140);
        tc.Controls.Add(_target); _target.SetBounds(S(18), S(66), W - S(36) - bw * 2 - S(16), S(42));
        tc.Controls.Add(_refresh); _refresh.SetBounds(W - S(18) - bw * 2 - S(8), S(66), bw, S(42));
        tc.Controls.Add(_pick); _pick.SetBounds(W - S(18) - bw, S(66), bw, S(42));
        tc.Controls.Add(_apps); _apps.SetBounds(S(18), S(122), W - S(36), tc.Height - S(122) - S(18));
        _target.Box.PlaceholderText = "Empty = Discord (default)";
    }

    void BuildCursors()
    {
        var p = NewPage();
        int W = p.Width, H = p.Height;
        Header(p, "Cursors", "Pick an image per state – links, text, loading and more. Right-click a tile for its hotspot.");

        int cols = 5, gap = S(10);
        int tw = (W - gap * (cols - 1)) / cols, th = S(152);
        for (int i = 0; i < RoleInfo.Length; i++)
        {
            var (role, title, cap) = RoleInfo[i];
            var tile = new RoleTile { Role = role, Title = title, Caption = cap, IsMain = role == CursorRole.Normal };
            tile.SetBounds((i % cols) * (tw + gap), S(84) + (i / cols) * (th + gap), tw, th);
            tile.BrowseRequested += (_, _) => BrowseFor(tile.Role);
            tile.ClearRequested += (_, _) => { _s.Role(tile.Role).Path = ""; _s.Save(); ReloadCursors(); UpdateTile(tile.Role); };
            tile.HotspotRequested += (t, hs) => { _s.Role(t.Role).Hotspot = hs; t.Hotspot = hs; _s.Save(); ReloadCursors(); };
            p.Controls.Add(tile);
            _tiles[role] = tile;
        }

        int oy = S(84) + th * 2 + gap + S(16);
        var oc = AddCard(p, 0, oy, W, H - oy, "Options");
        int half = W / 2;
        oc.Controls.Add(_size); _size.SetBounds(S(20), S(52), half - S(44), S(50));
        oc.Controls.Add(_tFallback); _tFallback.SetBounds(half, S(48), half - S(12), S(56));
        oc.Controls.Add(_clearAll); _clearAll.SetBounds(S(18), oc.Height - S(56), S(150), S(42));
        oc.Controls.Add(new Label
        {
            Text = "Supports .png (transparent), .cur and animated .ani files.",
            Font = F(8.5f), ForeColor = Faint, BackColor = Surface, AutoSize = true,
            Location = new Point(S(184), oc.Height - S(45))
        });
    }

    void BuildEffects()
    {
        var p = NewPage();
        int W = p.Width, H = p.Height;
        Header(p, "Click effects", "Animations that play where you click inside the target program.");

        var mc = AddCard(p, 0, S(84), W, S(100));
        mc.Controls.Add(_tEffects); _tEffects.SetBounds(S(8), S(22), (int)(W * 0.36f), S(56));
        mc.Controls.Add(_fxScale); _fxScale.SetBounds((int)(W * 0.41f), S(26), (int)(W * 0.25f), S(50));
        mc.Controls.Add(_fxSpeed); _fxSpeed.SetBounds((int)(W * 0.70f), S(26), (int)(W * 0.30f) - S(22), S(50));

        string[] names = EffectRenderer.Selectable.Select(a => a.ToString()).ToArray();
        int cw = (W - S(14)) / 2, cy = S(198);
        int cardH = 0;
        foreach (var (toggle, chips, sw, x) in new[] { (_tLeft, _chipL, _swL, 0), (_tRight, _chipR, _swR, cw + S(14)) })
        {
            var c = AddCard(p, x, cy, cw, S(200));
            c.Controls.Add(toggle); toggle.SetBounds(S(8), S(8), cw - S(16), S(56));
            c.Controls.Add(chips); chips.SetBounds(S(20), S(70), cw - S(40), S(40));
            chips.Items = names;
            chips.Height = chips.MeasureHeight();
            c.Controls.Add(sw); sw.SetBounds(S(14), chips.Bottom + S(12), cw - S(28), S(36));
            c.Height = sw.Bottom + S(16);
            cardH = c.Height;
        }

        int py = cy + cardH + S(14);
        p.Controls.Add(_preview);
        _preview.SetBounds(0, py, W, H - py);
        _preview.LeftFx = () => (_s.LeftAnimation, Color.FromArgb(_s.LeftColor), _s.LeftEnabled);
        _preview.RightFx = () => (_s.RightAnimation, Color.FromArgb(_s.RightColor), _s.RightEnabled);
        _preview.ScaleFx = () => _s.EffectScale;
        _preview.SpeedFx = () => _s.EffectSpeed;
        _preview.MasterOn = () => _s.ClickEffects;
    }

    void ShowPage(int index, bool animate = true)
    {
        for (int i = 0; i < _pages.Count; i++)
            if (i != index) _pages[i].Visible = false;
        var page = _pages[index];
        page.Left = animate ? _pageBaseX + S(40) : _pageBaseX;
        page.Visible = true;
        page.BringToFront();
        _pill.BringToFront();
        if (animate)
        {
            _pageIn = page;
            _pageAnimStart = DateTime.UtcNow;
            _uiAnim.Start();
        }
        if (index == 2) _preview.Kick();
    }

    // ================= state <-> UI =================

    void LoadIntoUi()
    {
        _tEnabled.SetSilently(_s.Enabled);
        try { _tStartup.SetSilently(Startup.IsEnabled()); } catch { }
        _target.Box.Text = _s.TargetProcess;
        _apps.SetSelected(_s.TargetProcess);
        LoadApps();

        _size.Value = _s.CursorSize;
        _tFallback.SetSilently(_s.UnsetUseMain);
        foreach (var role in _tiles.Keys)
        {
            _tiles[role].Hotspot = _s.Role(role).Hotspot;
            UpdateTile(role);
        }
        ReloadCursors();

        _tEffects.SetSilently(_s.ClickEffects);
        _fxScale.Value = _s.EffectScale;
        _fxSpeed.Value = _s.EffectSpeed;
        _tLeft.SetSilently(_s.LeftEnabled);
        _tRight.SetSilently(_s.RightEnabled);
        var sel = EffectRenderer.Selectable;
        _chipL.SetSilently(Math.Max(0, Array.IndexOf(sel, _s.LeftAnimation)));
        _chipR.SetSilently(Math.Max(0, Array.IndexOf(sel, _s.RightAnimation)));
        _swL.Value = Color.FromArgb(_s.LeftColor);
        _swR.Value = Color.FromArgb(_s.RightColor);
        UpdateEffectEnables();
    }

    void HookEvents()
    {
        _side.SelectedChanged += (_, _) => ShowPage(_side.Selected);

        // home
        _tEnabled.CheckedChanged += (_, _) => { _s.Enabled = _tEnabled.Checked; SaveSoon(); };
        _tStartup.CheckedChanged += (_, _) =>
        {
            try { Startup.Set(_tStartup.Checked); }
            catch (Exception ex) { Notice("Startup setting failed: " + ex.Message); _tStartup.SetSilently(!_tStartup.Checked); }
        };
        _target.Box.TextChanged += (_, _) =>
        {
            var typed = CleanName(_target.Box.Text);
            _s.TargetProcess = typed == "" ? Settings.DefaultTarget : typed;
            _apps.SetSelected(_s.TargetProcess);
            if (_target.Box.Focused) _apps.SetFilter(_target.Box.Text);
            SaveSoon();
        };
        _target.Box.LostFocus += (_, _) =>
        {
            _apps.SetFilter("");
            if (string.IsNullOrWhiteSpace(_target.Box.Text)) _target.Box.Text = Settings.DefaultTarget;
        };
        _apps.Picked += name => { _target.Box.Text = name; _apps.SetFilter(""); };
        _refresh.Click += (_, _) => LoadApps();
        _pick.Click += async (_, _) => await PickWindow();

        // cursors
        _size.Committed += (_, _) => { _s.CursorSize = (int)_size.Value; _s.Save(); ReloadCursors(); };
        _tFallback.CheckedChanged += (_, _) => { _s.UnsetUseMain = _tFallback.Checked; _s.Save(); ReloadCursors(); };
        _clearAll.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Remove all chosen cursors?", "CursorSwap", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (var role in _tiles.Keys) _s.Role(role).Path = "";
            _s.Save();
            ReloadCursors();
            foreach (var role in _tiles.Keys) UpdateTile(role);
        };

        // effects
        var sel = EffectRenderer.Selectable;
        _tEffects.CheckedChanged += (_, _) => { _s.ClickEffects = _tEffects.Checked; UpdateEffectEnables(); SaveSoon(); };
        _tLeft.CheckedChanged += (_, _) => { _s.LeftEnabled = _tLeft.Checked; UpdateEffectEnables(); SaveSoon(); _preview.Fire(false); };
        _tRight.CheckedChanged += (_, _) => { _s.RightEnabled = _tRight.Checked; UpdateEffectEnables(); SaveSoon(); _preview.Fire(true); };
        _chipL.SelectedChanged += (_, _) => { _s.LeftAnimation = sel[_chipL.Selected]; SaveSoon(); _preview.Fire(false); };
        _chipR.SelectedChanged += (_, _) => { _s.RightAnimation = sel[_chipR.Selected]; SaveSoon(); _preview.Fire(true); };
        _swL.ValueChanged += (_, _) => { _s.LeftColor = _swL.Value.ToArgb(); SaveSoon(); _preview.Fire(false); };
        _swR.ValueChanged += (_, _) => { _s.RightColor = _swR.Value.ToArgb(); SaveSoon(); _preview.Fire(true); };
        _fxScale.ValueChanged += (_, _) => { _s.EffectScale = _fxScale.Value; SaveSoon(); };
        _fxSpeed.ValueChanged += (_, _) => { _s.EffectSpeed = _fxSpeed.Value; SaveSoon(); };
    }

    void UpdateEffectEnables()
    {
        bool m = _s.ClickEffects;
        _tLeft.Enabled = _tRight.Enabled = _fxScale.Enabled = _fxSpeed.Enabled = m;
        _chipL.Enabled = _swL.Enabled = m && _s.LeftEnabled;
        _chipR.Enabled = _swR.Enabled = m && _s.RightEnabled;
    }

    void SaveSoon() { _saveTimer.Stop(); _saveTimer.Start(); }

    void SetupTray()
    {
        _tray.Icon = Icon;
        _tray.Text = "CursorSwap";
        _tray.Visible = true;
        var menu = Theme.Menu();
        var open = new ToolStripMenuItem("Open CursorSwap") { Font = Semi(9.5f) };
        open.Click += (_, _) => ShowFromTray();
        var enabled = new ToolStripMenuItem("Enabled");
        enabled.Click += (_, _) => _tEnabled.Checked = !_tEnabled.Checked;
        var fx = new ToolStripMenuItem("Click effects");
        fx.Click += (_, _) => _tEffects.Checked = !_tEffects.Checked;
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => { _exiting = true; Close(); };
        menu.Items.AddRange(new ToolStripItem[] { open, new ToolStripSeparator(), enabled, fx, new ToolStripSeparator(), exit });
        menu.Opening += (_, _) => { enabled.Checked = _s.Enabled; fx.Checked = _s.ClickEffects; };
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            FadeOutToTray();
            return;
        }
        _loop.Stop();
        _s.Save();
        _cursor.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosing(e);
    }

    // ================= actions =================

    async void LoadApps()
    {
        _apps.Loading = true;
        _apps.Kick();
        int own = _ownPid, iconSize = S(22);
        var items = await Task.Run(() =>
        {
            var list = new List<AppList.Item>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    using (proc)
                    {
                        if (proc.Id == own || proc.MainWindowHandle == IntPtr.Zero || !seen.Add(proc.ProcessName)) continue;
                        Image? img = null;
                        try
                        {
                            var file = proc.MainModule?.FileName;
                            if (file != null)
                            {
                                using var ic = Icon.ExtractAssociatedIcon(file);
                                if (ic != null) { using var b = ic.ToBitmap(); img = new Bitmap(b, iconSize, iconSize); }
                            }
                        }
                        catch { } // protected/elevated processes
                        list.Add(new AppList.Item(proc.ProcessName, img));
                    }
                }
                catch { }
            }
            return list.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        });
        if (IsDisposed) return;
        _apps.SetItems(items);
        _apps.SetSelected(_s.TargetProcess);
    }

    async Task PickWindow()
    {
        _pick.Enabled = false;
        for (int i = 3; i > 0; i--)
        {
            _pick.Text = $"Hover target {i}";
            await Task.Delay(1000);
        }
        _pick.Text = "Pick window";
        _pick.Enabled = true;

        Native.GetCursorPos(out var pt);
        var name = ProcessNameAt(pt, out uint pid);
        if (name != null && pid != _ownPid) _target.Box.Text = name;
        else Notice("Nothing picked – hover another program during the countdown");
    }

    void BrowseFor(CursorRole role)
    {
        using var dlg = new OpenFileDialog
        {
            Title = $"Choose a cursor for \"{RoleInfo.First(r => r.Role == role).Title}\"",
            Filter = "Cursor images|*.png;*.cur;*.ani;*.ico;*.bmp;*.gif;*.jpg;*.jpeg|All files|*.*"
        };
        var current = _s.Role(role).Path;
        if (!string.IsNullOrEmpty(current) && Directory.Exists(Path.GetDirectoryName(current)))
            dlg.InitialDirectory = Path.GetDirectoryName(current);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _s.Role(role).Path = dlg.FileName;
        _s.Save();
        ReloadCursors();
        UpdateTile(role);
    }

    void UpdateTile(CursorRole role)
    {
        if (_tiles.TryGetValue(role, out var tile))
            tile.Preview = CursorManager.MakePreview(_s.Role(role).Path, S(46));
    }

    void ReloadCursors()
    {
        var errors = _cursor.Load(_s);
        if (errors.Count > 0) Notice("Cursor error – " + errors[0]);
    }

    // ================= main loop =================

    void Tick()
    {
        if ((DateTime.UtcNow - _pidCacheTime).TotalSeconds > 10) { _pidNames.Clear(); _pidCacheTime = DateTime.UtcNow; }

        Native.GetCursorPos(out var pt);
        bool overTarget = false;
        if (_s.Enabled && !string.IsNullOrWhiteSpace(_s.TargetProcess))
        {
            var name = ProcessNameAt(pt, out uint pid);
            overTarget = pid != _ownPid && name != null &&
                         string.Equals(name, _s.TargetProcess, StringComparison.OrdinalIgnoreCase);
        }

        if (overTarget && _cursor.HasCursor) _cursor.Apply();
        else _cursor.Restore();

        bool left = (Native.GetAsyncKeyState(Native.VK_LBUTTON) & 0x8000) != 0;
        bool right = (Native.GetAsyncKeyState(Native.VK_RBUTTON) & 0x8000) != 0;
        if (overTarget && _s.ClickEffects)
        {
            var p = new Point(pt.X, pt.Y);
            if (left && !_leftWasDown && _s.LeftEnabled) PlayEffect(p, _s.LeftAnimation, _s.LeftColor);
            if (right && !_rightWasDown && _s.RightEnabled) PlayEffect(p, _s.RightAnimation, _s.RightColor);
        }
        _leftWasDown = left;
        _rightWasDown = right;

        if (_notice != null && DateTime.UtcNow < _noticeUntil) _pill.Set(_notice, Theme.Danger, false);
        else if (!_s.Enabled) _pill.Set("Paused", Faint, false);
        else if (string.IsNullOrWhiteSpace(_s.TargetProcess)) _pill.Set("Choose a target program", Warn, false);
        else if (!_cursor.HasCursor) _pill.Set("Choose a cursor", Warn, false);
        else if (overTarget) _pill.Set($"Active in {_s.TargetProcess}", Success, true);
        else _pill.Set($"Waiting for {_s.TargetProcess}", Accent, false);
    }

    void Notice(string text)
    {
        _notice = text;
        _noticeUntil = DateTime.UtcNow.AddSeconds(5);
    }

    void PlayEffect(Point p, ClickAnimation anim, int argb)
    {
        if (anim == ClickAnimation.None) return;
        new ClickEffect(p, anim, Color.FromArgb(argb), _s.EffectScale * Theme.Scale, _s.EffectSpeed).Show();
    }

    string? ProcessNameAt(Native.POINT pt, out uint pid)
    {
        pid = 0;
        var hwnd = Native.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return null;
        var root = Native.GetAncestor(hwnd, Native.GA_ROOT);
        Native.GetWindowThreadProcessId(root != IntPtr.Zero ? root : hwnd, out pid);
        if (pid == 0) return null;
        if (_pidNames.TryGetValue(pid, out var cached)) return cached;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return _pidNames[pid] = proc.ProcessName;
        }
        catch { return null; }
    }

    static string CleanName(string s)
    {
        s = s.Trim();
        return s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s;
    }

    sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }
}
