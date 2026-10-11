using System.Runtime.InteropServices;

namespace ZOA.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new PetForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ZOA 启动失败：\n{ex.Message}", "ZOA", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal enum PetAction
{
    Idle = 0, WalkRight = 1, WalkLeft = 2, Wave = 3, Jump = 4,
    Failed = 5, Thinking = 7, Review = 8
}

internal sealed class PetForm : Form
{
    private const int CellWidth = 192;
    private const int CellHeight = 208;
    private const int FrameTickMs = 25;
    private const int TypingHoldMs = 1600;
    private const int AmbientIntervalMs = 60_000;
    private const int BlinkIntervalMs = 5_000;
    // Row 6 (the seventh strip) was removed from the atlas.
    private static readonly int[] FrameCounts = [6, 8, 8, 4, 5, 8, 0, 6, 6];
    private static readonly PetAction[] RandomActions =
    [
        PetAction.Wave, PetAction.Jump, PetAction.Failed,
        PetAction.Thinking, PetAction.Review
    ];

    private readonly Bitmap _atlas;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly KeyboardHook _keyboard;
    private readonly NotifyIcon _tray;
    private readonly Random _random = new();
    private readonly Queue<PetAction> _actionBag = new();
    private readonly string _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private readonly ContextMenuStrip _menu = new();
    private readonly ContextMenuStrip _sizeMenu = new();
    private readonly List<(ToolStripMenuItem Item, double Scale)> _sizeOptions = [];
    private PetAction _action = PetAction.Idle;
    private int _frame;
    private long _frameUntil;
    private long _nextAmbientAt;
    private long _typingUntil;
    private int _lookDirection = -1;
    private bool _oneShot;
    private bool _ambientPlaying;
    private bool _pointerNearby;
    private bool _dragCandidate;
    private bool _dragging;
    private bool _sizeButtonPressed;
    private bool _sizeButtonHovered;
    private bool _sizeButtonVisible;
    private Point _pressCursor;
    private Point _pressWindow;
    private int _lastDragCursorX;
    private double _scale = 1;
    private bool _ready;

    public PetForm()
    {
        var atlasPath = Path.Combine(AppContext.BaseDirectory, "spritesheet.png");
        if (!File.Exists(atlasPath))
            throw new FileNotFoundException("找不到 spritesheet.png，请将它与 ZOA.exe 放在同一文件夹。", atlasPath);
        _atlas = new Bitmap(atlasPath);
        if (_atlas.Width != 1536 || _atlas.Height != 2288)
            throw new InvalidDataException($"精灵图尺寸应为 1536 × 2288：{atlasPath}");

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Size = new Size(CellWidth, CellHeight);

        LoadSettings();
        BuildMenu();
        _tray = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "ZOA 桌宠",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => { Show(); TopMost = true; RenderFrame(); };

        _keyboard = new KeyboardHook(OnKeyPressed);
        _timer = new System.Windows.Forms.Timer { Interval = FrameTickMs };
        _timer.Tick += (_, _) => Tick();
        MouseDown += OnPetMouseDown;
        MouseMove += OnPetMouseMove;
        MouseUp += OnPetMouseUp;
        MouseLeave += (_, _) =>
        {
            SetSizeButtonHover(false);
        };
        MouseCaptureChanged += (_, _) =>
        {
            if (!Capture)
            {
                _dragCandidate = false;
                _dragging = false;
                _sizeButtonPressed = false;
            }
        };
        Shown += (_, _) =>
        {
            _ready = true;
            _keyboard.Start();
            _timer.Start();
            _nextAmbientAt = Now() + AmbientIntervalMs;
            _frameUntil = Now() + FrameDuration(_action, _frame);
            UpdateProximity();
            RenderFrame();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x00080000 | 0x00000080 | 0x08000000; // LAYERED, TOOLWINDOW, NOACTIVATE
            return parameters;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0021) // WM_MOUSEACTIVATE
        {
            m.Result = new IntPtr(3); // MA_NOACTIVATE
            return;
        }
        base.WndProc(ref m);
    }

    private void BuildMenu()
    {
        _menu.Items.Add("让 ZOA 挥手", null, (_, _) => StartOneShot(PetAction.Wave));
        _menu.Items.Add("让 ZOA 跳一下", null, (_, _) => StartOneShot(PetAction.Jump));
        var scaleMenu = new ToolStripMenuItem("大小");
        foreach (var scale in new[] { 0.75, 1.0, 1.25, 1.5 })
        {
            double chosen = scale;
            var menuItem = new ToolStripMenuItem($"{scale * 100:0}%", null, (_, _) => ChangeScale(chosen));
            var buttonItem = new ToolStripMenuItem($"{scale * 100:0}%", null, (_, _) => ChangeScale(chosen));
            scaleMenu.DropDownItems.Add(menuItem);
            _sizeMenu.Items.Add(buttonItem);
            _sizeOptions.Add((menuItem, scale));
            _sizeOptions.Add((buttonItem, scale));
        }
        _menu.Items.Add(scaleMenu);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("退出 ZOA", null, (_, _) => Close());
        UpdateSizeChecks();
    }

    private Rectangle SizeButtonBounds => new(Width - 40, 7, 33, 33);

    private void UpdateSizeChecks()
    {
        foreach (var (item, scale) in _sizeOptions)
            item.Checked = Math.Abs(scale - _scale) < 0.01;
    }

    private void SetSizeButtonHover(bool hovered)
    {
        if (_sizeButtonHovered == hovered) return;
        _sizeButtonHovered = hovered;
        Cursor = hovered ? Cursors.Hand : Cursors.Default;
        RenderFrame();
    }

    private void OnPetMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _menu.Show(Cursor.Position);
            return;
        }
        if (e.Button != MouseButtons.Left) return;
        if (_sizeButtonVisible && SizeButtonBounds.Contains(e.Location))
        {
            _sizeButtonPressed = true;
            Capture = true;
            RenderFrame();
            return;
        }
        _dragCandidate = true;
        _dragging = false;
        _pressCursor = Cursor.Position;
        _pressWindow = Location;
        _lastDragCursorX = _pressCursor.X;
        Capture = true;
    }

    private void OnPetMouseMove(object? sender, MouseEventArgs e)
    {
        if (_sizeButtonPressed) return;
        if (!_dragCandidate)
        {
            SetSizeButtonHover(_sizeButtonVisible && SizeButtonBounds.Contains(e.Location));
            return;
        }
        var delta = new Size(Cursor.Position.X - _pressCursor.X, Cursor.Position.Y - _pressCursor.Y);
        if (!_dragging && Math.Abs(delta.Width) + Math.Abs(delta.Height) >= 5)
        {
            _dragging = true;
            _oneShot = false;
            _ambientPlaying = false;
            SwitchAction(PetAction.Idle, true);
        }
        if (!_dragging) return;
        var screen = SystemInformation.VirtualScreen;
        int x = Math.Clamp(_pressWindow.X + delta.Width, screen.Left, screen.Right - Width);
        int y = Math.Clamp(_pressWindow.Y + delta.Height, screen.Top, screen.Bottom - Height);
        Location = new Point(x, y);
        int horizontalStep = Cursor.Position.X - _lastDragCursorX;
        if (Math.Abs(horizontalStep) > 1)
            SwitchAction(horizontalStep > 0 ? PetAction.WalkRight : PetAction.WalkLeft, false);
        _lastDragCursorX = Cursor.Position.X;
        RenderFrame();
    }

    private void OnPetMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (_sizeButtonPressed)
        {
            _sizeButtonPressed = false;
            Capture = false;
            RenderFrame();
            if (SizeButtonBounds.Contains(e.Location))
                _sizeMenu.Show(this, new Point(SizeButtonBounds.Left, SizeButtonBounds.Bottom));
            return;
        }
        if (!_dragCandidate) return;
        bool wasDragging = _dragging;
        _dragCandidate = false;
        _dragging = false;
        Capture = false;
        if (wasDragging)
        {
            SaveSettings();
            SwitchAction(PetAction.Idle, false);
        }
        else
        {
            StartOneShot(e.Clicks >= 2 ? PetAction.Jump : PetAction.Wave);
        }
    }

    private void OnKeyPressed(int virtualKey)
    {
        // Only note the time of ordinary typing. No key text or history is stored.
        if (!IsTypingKey(virtualKey)) return;
        _typingUntil = Now() + TypingHoldMs;
        if (!_dragging && !_oneShot)
        {
            _ambientPlaying = false;
            SwitchAction(PetAction.Thinking, false);
        }
    }

    private static bool IsTypingKey(int key)
    {
        if ((KeyboardHook.GetAsyncKeyState(0x11) & 0x8000) != 0 ||
            (KeyboardHook.GetAsyncKeyState(0x12) & 0x8000) != 0 ||
            (KeyboardHook.GetAsyncKeyState(0x5B) & 0x8000) != 0 ||
            (KeyboardHook.GetAsyncKeyState(0x5C) & 0x8000) != 0)
            return false;
        return key is >= 0x30 and <= 0x5A or >= 0x60 and <= 0x6F
            or >= 0xBA and <= 0xE2 or 0x08 or 0x0D or 0x20 or 0x09 or 0x2E;
    }

    private void Tick()
    {
        long now = Now();
        UpdateProximity();
        if (!_dragging && !_oneShot && now < _typingUntil && _action != PetAction.Thinking)
        {
            _ambientPlaying = false;
            SwitchAction(PetAction.Thinking, false);
        }

        if (now >= _frameUntil)
        {
            _frame++;
            if (_frame >= FrameCounts[(int)_action])
            {
                _frame = 0;
                if (_oneShot)
                {
                    _oneShot = false;
                    SwitchAction(now < _typingUntil ? PetAction.Thinking : PetAction.Idle, false);
                }
                else if (_ambientPlaying)
                {
                    _ambientPlaying = false;
                    SwitchAction(PetAction.Idle, false);
                }
                else if (!_dragging && _action != PetAction.Idle && now >= _typingUntil)
                {
                    SwitchAction(PetAction.Idle, false);
                }
            }
            _frameUntil = now + FrameDuration(_action, _frame);
            RenderFrame();
        }

        if (_dragging || _oneShot || now < _typingUntil)
        {
            ClearLook();
            return;
        }
        if (_pointerNearby)
        {
            if (_ambientPlaying)
            {
                _ambientPlaying = false;
                SwitchAction(PetAction.Idle, false);
            }
            if (_action != PetAction.Idle)
                SwitchAction(PetAction.Idle, false);
            SetLookDirection(Cursor.Position);
            return;
        }
        ClearLook();
        if (!_ambientPlaying && _action == PetAction.Idle && now >= _nextAmbientAt)
        {
            if (_actionBag.Count == 0) RefillBag();
            _ambientPlaying = true;
            _nextAmbientAt = now + AmbientIntervalMs;
            SwitchAction(_actionBag.Dequeue(), false);
        }
    }

    private void UpdateProximity()
    {
        Point pointer = Cursor.Position;
        int nearestX = Math.Clamp(pointer.X, Left, Right);
        int nearestY = Math.Clamp(pointer.Y, Top, Bottom);
        int dx = pointer.X - nearestX;
        int dy = pointer.Y - nearestY;
        _pointerNearby = dx * dx + dy * dy <= 120 * 120;
        bool showButton = _pointerNearby || _sizeButtonPressed;
        if (_sizeButtonVisible == showButton) return;
        _sizeButtonVisible = showButton;
        if (!showButton) SetSizeButtonHover(false);
        RenderFrame();
    }

    private void ClearLook()
    {
        if (_lookDirection >= 0)
        {
            _lookDirection = -1;
            RenderFrame();
        }
    }

    private void SetLookDirection(Point pointer)
    {
        double dx = pointer.X - (Left + Width / 2.0);
        double dy = pointer.Y - (Top + Height / 2.0);
        double degrees = (Math.Atan2(dx, -dy) * 180 / Math.PI + 360) % 360;
        int direction = ((int)Math.Round(degrees / 22.5, MidpointRounding.AwayFromZero)) % 16;
        if (direction == _lookDirection) return;
        _lookDirection = direction;
        RenderFrame();
    }

    private void RefillBag()
    {
        var choices = RandomActions.ToArray();
        for (int i = choices.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (choices[i], choices[j]) = (choices[j], choices[i]);
        }
        foreach (var choice in choices) _actionBag.Enqueue(choice);
    }

    private void StartOneShot(PetAction action)
    {
        if (_dragging) return;
        _ambientPlaying = false;
        _oneShot = true;
        SwitchAction(action, true);
    }

    private void SwitchAction(PetAction action, bool force)
    {
        if (!force && _action == action) return;
        _action = action;
        _frame = 0;
        _frameUntil = Now() + FrameDuration(action, 0);
        _lookDirection = -1;
        RenderFrame();
    }

    private static int FrameDuration(PetAction action, int frame)
    {
        if (action == PetAction.Idle)
            return new[] { BlinkIntervalMs, 110, 110, 140, 140, 320 }[frame];
        int normal = action switch
        {
            PetAction.WalkRight or PetAction.WalkLeft or PetAction.Thinking => 120,
            PetAction.Review => 150,
            _ => 140
        };
        if (frame != FrameCounts[(int)action] - 1) return normal;
        return action switch
        {
            PetAction.Wave or PetAction.Jump or PetAction.Review => 280,
            PetAction.Failed => 240,
            _ => 220
        };
    }

    private void RenderFrame()
    {
        if (!_ready || !IsHandleCreated) return;
        int width = Width;
        int height = Height;
        using var image = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            int row = _lookDirection >= 0 ? 9 + _lookDirection / 8 : (int)_action;
            int column = _lookDirection >= 0 ? _lookDirection % 8 : _frame;
            graphics.DrawImage(_atlas,
                new Rectangle(0, 0, width, height),
                new Rectangle(column * CellWidth, row * CellHeight, CellWidth, CellHeight),
                GraphicsUnit.Pixel);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var button = SizeButtonBounds;
            using var background = new SolidBrush(_sizeButtonHovered || _sizeButtonPressed
                ? Color.FromArgb(245, 43, 91, 130)
                : Color.FromArgb(220, 37, 54, 70));
            using var border = new Pen(Color.FromArgb(240, 170, 223, 255), 1.5f);
            using var label = new SolidBrush(Color.White);
            using var font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold, GraphicsUnit.Pixel);
            using var alignment = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            if (_sizeButtonVisible)
            {
                graphics.FillEllipse(background, button);
                graphics.DrawEllipse(border, button);
                graphics.DrawString("大小", font, label, button, alignment);
            }
        }
        LayeredWindow.Draw(Handle, Location, image);
    }

    private void ChangeScale(double scale)
    {
        if (Math.Abs(scale - _scale) < 0.01) return;
        _scale = scale;
        Size = new Size((int)Math.Round(CellWidth * scale), (int)Math.Round(CellHeight * scale));
        var screen = SystemInformation.VirtualScreen;
        Location = new Point(
            Math.Clamp(Left, screen.Left, Math.Max(screen.Left, screen.Right - Width)),
            Math.Clamp(Top, screen.Top, Math.Max(screen.Top, screen.Bottom - Height)));
        UpdateSizeChecks();
        SaveSettings();
        RenderFrame();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var saved = System.Text.Json.JsonSerializer.Deserialize<PetSettings>(File.ReadAllText(_settingsPath));
                if (saved != null)
                {
                    _scale = Math.Clamp(saved.Scale, 0.75, 1.5);
                    Size = new Size((int)Math.Round(CellWidth * _scale), (int)Math.Round(CellHeight * _scale));
                    var candidate = new Point(saved.X, saved.Y);
                    var screen = SystemInformation.VirtualScreen;
                    if (screen.Contains(candidate))
                    {
                        Location = candidate;
                        return;
                    }
                }
            }
        }
        catch { /* An invalid settings file only resets placement. */ }
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        Location = new Point(workArea.Right - Width - 28, workArea.Bottom - Height - 12);
    }

    private void SaveSettings()
    {
        try
        {
            var data = new PetSettings(Left, Top, _scale);
            File.WriteAllText(_settingsPath, System.Text.Json.JsonSerializer.Serialize(data));
        }
        catch { /* Read-only folders should not prevent ZOA from running. */ }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _keyboard.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _sizeMenu.Dispose();
        _menu.Dispose();
        _atlas.Dispose();
        base.OnFormClosed(e);
    }

    private static long Now() => Environment.TickCount64;

    private sealed record PetSettings(int X, int Y, double Scale);
}

internal sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private readonly Action<int> _onKey;
    private readonly HookProc _procedure;
    private IntPtr _hook;

    public KeyboardHook(Action<int> onKey)
    {
        _onKey = onKey;
        _procedure = Callback;
    }

    public void Start()
    {
        _hook = SetWindowsHookEx(WhKeyboardLl, _procedure, GetModuleHandle(null), 0);
    }

    private IntPtr Callback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && (message.ToInt32() == WmKeyDown || message.ToInt32() == WmSysKeyDown))
            _onKey(Marshal.ReadInt32(data));
        return CallNextHookEx(_hook, code, message, data);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc procedure, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int key);
}

internal static class LayeredWindow
{
    private const int UlwAlpha = 2;
    private const byte AcSrcOver = 0;
    private const byte AcSrcAlpha = 1;

    public static void Draw(IntPtr window, Point location, Bitmap bitmap)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memoryDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr oldBitmap = SelectObject(memoryDc, hBitmap);
        try
        {
            var destination = new NativePoint(location.X, location.Y);
            var source = new NativePoint(0, 0);
            var size = new NativeSize(bitmap.Width, bitmap.Height);
            var blend = new BlendFunction(AcSrcOver, 0, 255, AcSrcAlpha);
            if (!UpdateLayeredWindow(window, screenDc, ref destination, ref size,
                    memoryDc, ref source, 0, ref blend, UlwAlpha))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            SelectObject(memoryDc, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y) { public int X = x; public int Y = y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize(int width, int height) { public int Width = width; public int Height = height; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction(byte operation, byte flags, byte alpha, byte format)
    {
        public byte BlendOp = operation;
        public byte BlendFlags = flags;
        public byte SourceConstantAlpha = alpha;
        public byte AlphaFormat = format;
    }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screenDc,
        ref NativePoint destination, ref NativeSize size, IntPtr sourceDc,
        ref NativePoint source, int colorKey, ref BlendFunction blend, int flags);
}
