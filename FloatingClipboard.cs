using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class Native
{
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern short GetKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, KeyboardHookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] internal static extern IntPtr GetModuleHandle(string module);
    internal delegate IntPtr KeyboardHookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] internal struct KBDLLHOOKSTRUCT
    { public uint key, scan, flags, time; public UIntPtr extra; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int size);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
    internal delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time);
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int left, top, right, bottom; }
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int x, y; public POINT(int x, int y) { this.x = x; this.y = y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct SIZE { public int width, height; public SIZE(int w, int h) { width = w; height = h; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct BLEND { public byte operation, flags, alpha, format; }
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDC,
        ref POINT destination, ref SIZE size, IntPtr sourceDC, ref POINT source, uint colorKey, ref BLEND blend, uint flags);
    [DllImport("user32.dll")] internal static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public INPUTDATA data; }
    [StructLayout(LayoutKind.Explicit)] internal struct INPUTDATA
    {
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public MOUSEINPUT mouse;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT
    { public ushort key, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT
    { public int x, y; public uint data, flags, time; public UIntPtr extra; }
}

internal sealed class Shortcut
{
    private readonly ushort[] keys;
    internal Shortcut(string value)
    {
        string[] parts = value.Split('+');
        keys = new ushort[parts.Length];
        if (parts.Length == 0) throw new ArgumentException("快捷键不能为空。");
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            Keys key;
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) key = Keys.ControlKey;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) key = Keys.ShiftKey;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) key = Keys.Menu;
            else if (!Enum.TryParse<Keys>(part, true, out key)) throw new ArgumentException("无法识别快捷键：" + part);
            if ((int)key <= 0 || (int)key > 255) throw new ArgumentException("不支持的按键：" + part);
            keys[i] = (ushort)key;
        }
    }
    internal void Send()
    {
        Native.INPUT[] down, up;
        BuildInputs(out down, out up);
        uint pressed = Native.SendInput((uint)down.Length, down, Marshal.SizeOf(typeof(Native.INPUT)));
        uint released;
        try
        {
            // 保留短暂按下状态，兼容通过异步键盘状态检测组合键的程序。
            Thread.Sleep(80);
        }
        finally { released = Native.SendInput((uint)up.Length, up, Marshal.SizeOf(typeof(Native.INPUT))); }
        if (pressed != down.Length || released != up.Length)
            throw new InvalidOperationException("快捷键发送失败。请检查目标程序是否以管理员身份运行。");
    }
    internal void BuildInputs(out Native.INPUT[] down, out Native.INPUT[] up)
    {
        down = new Native.INPUT[keys.Length];
        up = new Native.INPUT[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            down[i].type = 1;
            SetKey(ref down[i].data.keyboard, keys[i], false);
            up[i].type = 1;
            SetKey(ref up[i].data.keyboard, keys[keys.Length - 1 - i], true);
        }
    }
    internal bool TryGlobalHotkey(out uint modifiers, out uint key)
    {
        modifiers = key = 0;
        foreach (ushort part in keys)
        {
            if (part == (ushort)Keys.ControlKey || part == (ushort)Keys.LControlKey || part == (ushort)Keys.RControlKey) modifiers |= 2;
            else if (part == (ushort)Keys.Menu || part == (ushort)Keys.LMenu || part == (ushort)Keys.RMenu) modifiers |= 1;
            else if (part == (ushort)Keys.ShiftKey || part == (ushort)Keys.LShiftKey || part == (ushort)Keys.RShiftKey) modifiers |= 4;
            else if (part == (ushort)Keys.LWin || part == (ushort)Keys.RWin) modifiers |= 8;
            else if (key == 0) key = part;
            else return false;
        }
        return modifiers != 0 && key != 0;
    }
    private static void SetKey(ref Native.KEYBDINPUT input, ushort key, bool released)
    {
        if (key == (ushort)Keys.RControlKey)
        {
            // 右 Ctrl 与左 Ctrl 共用扫描码；扩展键标志负责区分右侧。
            input.scan = 0x1D;
            input.flags = (uint)(0x08 | 0x01 | (released ? 0x02 : 0));
        }
        else
        {
            input.key = key;
            input.flags = released ? 0x02u : 0u;
        }
    }
}

internal sealed class ShortcutRecorder : TextBox
{
    internal string Value { get; private set; }
    internal bool AllowEmpty { get; set; }
    private bool recording;
    private Keys rawKey;
    private int rawKeyData;
    private string heldCtrl, heldWin;
    private bool heldShift, heldAlt;
    private readonly Native.KeyboardHookProc keyboardCallback;
    private IntPtr keyboardHook;
    internal ShortcutRecorder(string value)
    {
        Value = value;
        keyboardCallback = OnKeyboard;
        ReadOnly = true;
        ShortcutsEnabled = false;
        TextAlign = HorizontalAlignment.Center;
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Color.White;
        ForeColor = Color.FromArgb(35, 65, 82);
        Text = Display(value);
        Cursor = Cursors.Hand;
    }
    internal static string Display(string value)
    { return value.Length == 0 ? "未绑定" : value.Replace("RControlKey", "右 Ctrl").Replace("LControlKey", "左 Ctrl")
        .Replace("Escape", "Esc").Replace("LWin", "Win").Replace("RWin", "Win").Replace("+", " + "); }
    private void BeginRecording()
    {
        recording = true;
        heldCtrl = heldWin = null;
        heldShift = heldAlt = false;
        Text = "请按快捷键…";
        BackColor = Color.FromArgb(232, 247, 252);
        Select(0, 0);
        if (keyboardHook == IntPtr.Zero)
            keyboardHook = Native.SetWindowsHookEx(13, keyboardCallback, Native.GetModuleHandle(null), 0);
    }
    protected override void OnEnter(EventArgs e) { base.OnEnter(e); BeginRecording(); }
    protected override void OnClick(EventArgs e) { base.OnClick(e); BeginRecording(); }
    protected override void OnLeave(EventArgs e)
    {
        StopHook();
        recording = false;
        Text = Display(Value);
        BackColor = Color.White;
        base.OnLeave(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) StopHook();
        base.Dispose(disposing);
    }
    private void StopHook()
    {
        if (keyboardHook == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(keyboardHook);
        keyboardHook = IntPtr.Zero;
    }
    protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
    {
        e.IsInputKey = true;
        base.OnPreviewKeyDown(e);
    }
    protected override void OnKeyPress(KeyPressEventArgs e) { e.Handled = true; base.OnKeyPress(e); }
    internal static string PhysicalModifier(Keys key, int keyData)
    {
        if (key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey)
            return (key == Keys.RControlKey || (keyData & 0x01000000) != 0) ? "RControlKey" : "Ctrl";
        if (key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey) return "Shift";
        if (key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu) return "Alt";
        return null;
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x100 || message.Msg == 0x101 || message.Msg == 0x104 || message.Msg == 0x105)
        {
            rawKey = (Keys)((int)message.WParam & 0xff);
            rawKeyData = unchecked((int)message.LParam.ToInt64());
        }
        base.WndProc(ref message);
    }
    private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
    {
        IntPtr currentHook = keyboardHook;
        if (code >= 0 && recording && keyboardHook != IntPtr.Zero)
        {
            int kind = message.ToInt32();
            if (kind == 0x100 || kind == 0x101 || kind == 0x104 || kind == 0x105)
            {
                Native.KBDLLHOOKSTRUCT input = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(data, typeof(Native.KBDLLHOOKSTRUCT));
                CaptureKey((Keys)input.key, (input.flags & 1) != 0, kind == 0x101 || kind == 0x105);
            }
        }
        // 仅观察录制期间的输入；按键继续交给系统和目标程序。
        return Native.CallNextHookEx(currentHook, code, message, data);
    }
    internal void CaptureKey(Keys key, bool extended, bool released)
    {
        if (!recording) return;
        string modifier = PhysicalModifier(key, extended ? 0x01000000 : 0);
        if (key == Keys.LWin || key == Keys.RWin) modifier = key.ToString();
        if (modifier != null)
        {
            string beforeRelease = ModifierChord();
            if (released)
            {
                if (beforeRelease.Length > 0) Finish(beforeRelease);
                if (modifier == "RControlKey" || modifier == "Ctrl") heldCtrl = null;
                else if (modifier == "Shift") heldShift = false;
                else if (modifier == "Alt") heldAlt = false;
                else heldWin = null;
            }
            else
            {
                if (modifier == "RControlKey" || modifier == "Ctrl") heldCtrl = modifier;
                else if (modifier == "Shift") heldShift = true;
                else if (modifier == "Alt") heldAlt = true;
                else heldWin = modifier;
                string chord = ModifierChord();
                if (chord.Length > 0) { Value = chord; Text = Display(chord) + " · 继续按键"; }
            }
            return;
        }
        if (released) return;
        string prefix = ModifierChord();
        if (AllowEmpty && key == Keys.Escape && prefix.Length == 0)
        {
            Value = "";
            Text = Display(Value);
            recording = false;
            BackColor = Color.White;
            StopHook();
            return;
        }
        string candidate = prefix.Length == 0 ? key.ToString() : prefix + "+" + key;
        try { Finish(candidate); }
        catch (ArgumentException) { Text = "此按键不支持，请重试"; }
    }
    private string ModifierChord()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (heldCtrl != null) parts.Add(heldCtrl);
        if (heldShift) parts.Add("Shift");
        if (heldAlt) parts.Add("Alt");
        if (heldWin != null) parts.Add(heldWin);
        return string.Join("+", parts.ToArray());
    }
    private void Finish(string candidate)
    {
        new Shortcut(candidate);
        Value = candidate;
        Text = Display(candidate);
        recording = false;
        BackColor = Color.White;
        StopHook();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
        if (keyboardHook != IntPtr.Zero || !recording) return;
        CaptureKey(e.KeyCode, rawKey == e.KeyCode && (rawKeyData & 0x01000000) != 0, false);
    }
    protected override void OnKeyUp(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
        if (keyboardHook == IntPtr.Zero && recording)
            CaptureKey(e.KeyCode, rawKey == e.KeyCode && (rawKeyData & 0x01000000) != 0, true);
        base.OnKeyUp(e);
    }
}

internal sealed class FloatingBar : Form
{
    private static readonly string[] labels = { "Esc", "←", "截图", "复制", "粘贴", "语音" };
    private static readonly string[] settingNames = { "退出", "后退", "截图", "复制", "粘贴", "语音" };
    private static readonly string[] shortcutKeys = { "exit", "back", "screenshot", "copy", "paste", "voice" };
    private static readonly string[] defaultShortcuts = { "Escape", "Back", "Ctrl+Alt+Z", "Ctrl+C", "Ctrl+V", "Ctrl+Shift+LWin" };
    private readonly Shortcut[] shortcuts;
    private readonly string[] shortcutTexts;
    private readonly ContextMenuStrip menu = new ContextMenuStrip();
    private readonly System.Windows.Forms.Timer delay = new System.Windows.Forms.Timer();
    private readonly System.Windows.Forms.Timer positionSave = new System.Windows.Forms.Timer();
    private readonly NotifyIcon tray = new NotifyIcon();
    private readonly EventWaitHandle showRequest;
    private RegisteredWaitHandle showRegistration;
    private readonly Native.WinEventProc windowEvent;
    private IntPtr foregroundHook, reorderHook;
    private Point downMouse, downLocation;
    private int downTick;
    private bool dragging, moved;
    private bool exitRequested;
    private bool screenshotHotkeyRegistered, screenshotHotkeyConflict, screenshotOpen, shown;
    private int hover = -1;
    private Action pending;
    private Form settings;
    private int backgroundOpacity = LoadIntSetting("opacity", 80, 30, 100);
    private int buttonSize = LoadIntSetting("button_size", 100, 70, 200);
    private int buttonGap = LoadIntSetting("button_gap", 3, 0, 20);
    private int visibleMask = LoadIntSetting("visible_buttons", 63, 1, 63);
    private Point? savedPosition = LoadPosition();
    private int circleDiameter, circleGap, circlePadding;

    internal FloatingBar(Shortcut paste, EventWaitHandle showRequest = null)
    {
        shortcutTexts = new string[shortcutKeys.Length];
        shortcuts = new Shortcut[shortcutKeys.Length];
        for (int i = 0; i < shortcutKeys.Length; i++)
        {
            shortcutTexts[i] = ReadSetting(shortcutKeys[i]) ?? defaultShortcuts[i];
            shortcuts[i] = i == 2 ? null : i == 4 ? paste : new Shortcut(shortcutTexts[i]);
        }
        windowEvent = OnWindowEvent;
        this.showRequest = showRequest;
        Text = "悬浮按钮";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ApplyLayout((int)Native.GetDpiForSystem());
        TransparencyKey = Color.Empty;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(area.Right - Width - 28, area.Top + area.Height / 3);
        BackColor = Color.FromArgb(232, 234, 237);
        DoubleBuffered = true;
        menu.Items.Add("显示悬浮按钮", null, delegate { Reveal(); });
        menu.Items.Add("隐藏悬浮按钮", null, delegate { Hide(); });
        menu.Items.Add("设置", null, delegate { ShowSettings(); });
        menu.Items.Add("退出", null, delegate { Exit(); });
        if (showRequest != null)
        {
            string iconFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            Icon = File.Exists(iconFile) ? new Icon(iconFile)
                : System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (Icon == null) Icon = SystemIcons.Application;
            tray.Icon = Icon;
            tray.Text = "悬浮按钮 · 正在运行";
            tray.ContextMenuStrip = new ContextMenuStrip();
            tray.ContextMenuStrip.Items.Add("显示悬浮按钮", null, delegate { Reveal(); });
            tray.ContextMenuStrip.Items.Add("设置", null, delegate { ShowSettings(); });
            tray.ContextMenuStrip.Items.Add("退出", null, delegate { Exit(); });
            tray.DoubleClick += delegate { Reveal(); };
            tray.Visible = true;
        }
        delay.Interval = 140;
        delay.Tick += delegate
        {
            delay.Stop();
            Action action = pending;
            pending = null;
            try { if (action != null) action(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "悬浮按钮"); }
        };
        positionSave.Interval = 300;
        positionSave.Tick += delegate
        {
            positionSave.Stop();
            SavePosition();
        };
    }
    private static string ReadSetting(string key)
    {
        string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts.ini");
        if (File.Exists(file)) foreach (string line in File.ReadAllLines(file))
        {
            string[] pair = line.Split(new char[] { '=' }, 2);
            if (pair.Length == 2 && pair[0].Trim() == key) return pair[1].Trim();
        }
        return null;
    }
    private static int LoadIntSetting(string key, int fallback, int minimum, int maximum)
    {
        int value;
        return int.TryParse(ReadSetting(key), out value) ? Math.Max(minimum, Math.Min(maximum, value)) : fallback;
    }
    private static Point? LoadPosition()
    {
        int x, y;
        return int.TryParse(ReadSetting("position_x"), out x) && int.TryParse(ReadSetting("position_y"), out y)
            ? new Point(x, y) : (Point?)null;
    }
    private static void WriteSettings(params string[] entries)
    {
        string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts.ini");
        string[] lines = MergeSettings(File.Exists(file) ? File.ReadAllLines(file) : new string[0], entries);
        string temporary = file + ".tmp";
        File.WriteAllLines(temporary, lines);
        if (File.Exists(file)) File.Replace(temporary, file, null);
        else File.Move(temporary, file);
    }
    internal static string[] MergeSettings(string[] existing, params string[] entries)
    {
        var lines = new System.Collections.Generic.List<string>(existing);
        foreach (string entry in entries)
        {
            string key = entry.Split('=')[0];
            lines.RemoveAll(delegate(string line) { return line.Split('=')[0].Trim() == key; });
            lines.Add(entry);
        }
        return lines.ToArray();
    }
    private void SavePosition()
    {
        try
        {
            savedPosition = Location;
            WriteSettings("position_x=" + Left, "position_y=" + Top);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "位置保存失败"); }
    }
    internal static Size LayoutSize(int dpi, int percent, int gap, int count)
    {
        int diameter = Math.Max(1, 27 * percent * dpi / 9600);
        int spacing = gap * dpi / 96;
        int padding = Math.Max(1, dpi / 96);
        return new Size(2 * padding + count * diameter + Math.Max(0, count - 1) * spacing,
            2 * padding + diameter);
    }
    private void ApplyLayout(int dpi)
    {
        int count = 0;
        for (int i = 0; i < labels.Length; i++) if ((visibleMask & (1 << i)) != 0) count++;
        circleDiameter = Math.Max(1, 27 * buttonSize * dpi / 9600);
        circleGap = buttonGap * dpi / 96;
        circlePadding = Math.Max(1, dpi / 96);
        ClientSize = LayoutSize(dpi, buttonSize, buttonGap, count);
        RenderLayered();
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyLayout((int)Native.GetDpiForWindow(Handle));
        shown = true;
        RegisterScreenshotHotkey();
        if (showRequest == null) return;
        foregroundHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, windowEvent, 0, 0, 2);
        reorderHook = Native.SetWinEventHook(0x8004, 0x8004, IntPtr.Zero, windowEvent, 0, 0, 2);
        Reveal();
        tray.ShowBalloonTip(3000, "悬浮按钮已启动", screenshotHotkeyConflict
            ? "截图组合键已被占用并清空；点击截图按钮仍可使用。"
            : "按钮已显示。双击托盘图标可找回，右键可退出。", ToolTipIcon.Info);
        showRegistration = ThreadPool.RegisterWaitForSingleObject(showRequest, delegate
        {
            try { BeginInvoke((MethodInvoker)delegate { if (!IsDisposed) Reveal(); }); }
            catch (InvalidOperationException) { }
        }, null, Timeout.Infinite, false);
    }
    private Screen CurrentScreen()
    {
        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground != IntPtr.Zero && (!IsHandleCreated || foreground != Handle) && (settings == null || !settings.IsHandleCreated || foreground != settings.Handle))
        {
            System.Text.StringBuilder name = new System.Text.StringBuilder(128);
            Native.GetClassName(foreground, name, name.Capacity);
            string windowClass = name.ToString();
            if (windowClass != "Shell_TrayWnd" && windowClass != "Shell_SecondaryTrayWnd" && windowClass != "#32768")
                return Screen.FromHandle(foreground);
        }
        return Screen.FromPoint(Cursor.Position);
    }
    private void ShowSettings()
    {
        if (settings != null && !settings.IsDisposed)
        {
            settings.WindowState = FormWindowState.Normal;
            settings.Show(); Native.ShowWindow(settings.Handle, 5);
            settings.BringToFront(); settings.Activate(); return;
        }
        if (screenshotHotkeyRegistered)
        {
            Native.UnregisterHotKey(Handle, 1);
            screenshotHotkeyRegistered = false;
        }
        int originalOpacity = backgroundOpacity, originalSize = buttonSize, originalGap = buttonGap, originalMask = visibleMask;
        settings = new Form { Text = "悬浮按钮设置", AutoScaleMode = AutoScaleMode.None,
            FormBorderStyle = FormBorderStyle.FixedDialog, ShowInTaskbar = true, MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.Manual, TopMost = true, Icon = Icon,
            BackColor = Color.FromArgb(245, 248, 251), Font = new Font("Microsoft YaHei UI", 10f) };
        Rectangle area = CurrentScreen().WorkingArea;
        int width = Math.Min(500, Math.Max(300, area.Width - 32));
        int margin = 20, innerWidth = width - margin * 2 - SystemInformation.VerticalScrollBarWidth;
        int lineHeight = Math.Max(settings.Font.Height + 8,
            TextRenderer.MeasureText("按钮大小：200%（文字重新绘制）", settings.Font,
                new Size(innerWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + 4);
        int sliderHeight = Math.Max(42, settings.Font.Height + 18);
        int rowHeight = Math.Max(38, settings.Font.Height + 18);
        int y = 16;
        Panel content = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        Label hint = new Label { Text = "调整外观，或点击快捷键框直接录制按键", Location = new Point(margin, y),
            Font = new Font("Microsoft YaHei UI", 9f), ForeColor = Color.FromArgb(99, 116, 128) };
        hint.Size = new Size(innerWidth, TextRenderer.MeasureText(hint.Text, hint.Font,
            new Size(innerWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + 4);
        y += hint.Height + 14;
        Label opacityLabel = new Label { Location = new Point(margin, y), Size = new Size(innerWidth, lineHeight), ForeColor = Color.FromArgb(35, 65, 82) };
        y += lineHeight;
        TrackBar opacitySlider = new TrackBar { AutoSize = false, Location = new Point(margin, y), Size = new Size(innerWidth, sliderHeight),
            Minimum = 30, Maximum = 100, TickStyle = TickStyle.None, Value = backgroundOpacity };
        y += sliderHeight + 14;
        Label sizeLabel = new Label { Location = new Point(margin, y), Size = new Size(innerWidth, lineHeight), ForeColor = Color.FromArgb(35, 65, 82) };
        y += lineHeight;
        TrackBar sizeSlider = new TrackBar { AutoSize = false, Location = new Point(margin, y), Size = new Size(innerWidth, sliderHeight),
            Minimum = 70, Maximum = 200, TickStyle = TickStyle.None, Value = buttonSize };
        y += sliderHeight + 14;
        Label gapLabel = new Label { Location = new Point(margin, y), Size = new Size(innerWidth, lineHeight), ForeColor = Color.FromArgb(35, 65, 82) };
        y += lineHeight;
        TrackBar gapSlider = new TrackBar { AutoSize = false, Location = new Point(margin, y), Size = new Size(innerWidth, sliderHeight),
            Minimum = 0, Maximum = 20, TickStyle = TickStyle.None, Value = buttonGap };
        y += sliderHeight + 20;
        Label buttonHeading = new Label { Text = "按钮与快捷键", Location = new Point(margin, y), Size = new Size(innerWidth, lineHeight),
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(35, 65, 82) };
        y += lineHeight + 6;
        Panel buttons = new Panel { Location = new Point(margin, y), Size = new Size(innerWidth, rowHeight * labels.Length + 2),
            BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        CheckBox[] checks = new CheckBox[labels.Length];
        ShortcutRecorder[] recorders = new ShortcutRecorder[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            int rowY = i * rowHeight;
            checks[i] = new CheckBox { Text = settingNames[i], Checked = (visibleMask & (1 << i)) != 0,
                Location = new Point(14, rowY + 2), Size = new Size(innerWidth / 2 - 18, rowHeight - 4), BackColor = Color.White };
            recorders[i] = new ShortcutRecorder(shortcutTexts[i]) { Location = new Point(innerWidth / 2, rowY + 2),
                Font = new Font("Segoe UI", 10f) };
            if (i == 2) recorders[i].AllowEmpty = true;
            recorders[i].Size = new Size(innerWidth / 2 - 14, Math.Min(rowHeight - 4, recorders[i].PreferredHeight));
            recorders[i].Top = rowY + (rowHeight - recorders[i].Height) / 2;
            buttons.Controls.Add(checks[i]);
            buttons.Controls.Add(recorders[i]);
            if (i < labels.Length - 1) buttons.Controls.Add(new Panel { Location = new Point(14, rowY + rowHeight - 1),
                Size = new Size(innerWidth - 28, 1), BackColor = Color.FromArgb(235, 239, 242) });
        }
        y += buttons.Height + 18;
        content.AutoScrollMinSize = new Size(0, y);
        Action update = delegate
        {
            int mask = 0;
            for (int i = 0; i < checks.Length; i++) if (checks[i].Checked) mask |= 1 << i;
            if (mask == 0) return;
            backgroundOpacity = opacitySlider.Value;
            buttonSize = sizeSlider.Value;
            buttonGap = gapSlider.Value;
            visibleMask = mask;
            opacityLabel.Text = "背景不透明度：" + backgroundOpacity + "%";
            sizeLabel.Text = "按钮大小：" + buttonSize + "%（文字重新绘制）";
            gapLabel.Text = "按钮间距：" + buttonGap + "（随屏幕比例调整）";
            ApplyLayout((int)Native.GetDpiForWindow(Handle));
            Location = KeepOnScreen(Location, Size, Screen.FromPoint(Location).Bounds);
        };
        opacitySlider.ValueChanged += delegate { update(); };
        sizeSlider.ValueChanged += delegate { update(); };
        gapSlider.ValueChanged += delegate { update(); };
        for (int i = 0; i < checks.Length; i++)
        {
            CheckBox check = checks[i];
            check.CheckedChanged += delegate
            {
                int count = 0;
                foreach (CheckBox item in checks) if (item.Checked) count++;
                if (count == 0) { check.Checked = true; return; }
                update();
            };
        }
        int footerHeight = Math.Max(64, settings.Font.Height + 34);
        int actionHeight = Math.Max(34, settings.Font.Height + 14);
        int saveWidth = Math.Max(104, TextRenderer.MeasureText("保存设置", settings.Font).Width + 24);
        int cancelWidth = Math.Max(72, TextRenderer.MeasureText("取消", settings.Font).Width + 24);
        Panel footer = new Panel { Dock = DockStyle.Bottom, Height = footerHeight };
        Button save = new Button { Text = "保存设置", Location = new Point(width - margin - saveWidth, (footerHeight - actionHeight) / 2),
            Size = new Size(saveWidth, actionHeight),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(31, 130, 177), ForeColor = Color.White };
        save.FlatAppearance.BorderSize = 0;
        Button cancel = new Button { Text = "取消", Location = new Point(save.Left - 12 - cancelWidth, save.Top),
            Size = new Size(cancelWidth, actionHeight),
            FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(49, 75, 90) };
        cancel.FlatAppearance.BorderColor = Color.FromArgb(213, 224, 230);
        bool saved = false;
        save.Click += delegate
        {
            try
            {
                string[] entries = new string[shortcutKeys.Length + 6];
                entries[0] = "opacity=" + backgroundOpacity;
                entries[1] = "button_size=" + buttonSize;
                entries[2] = "button_gap=" + buttonGap;
                entries[3] = "visible_buttons=" + visibleMask;
                entries[4] = "position_x=" + Left;
                entries[5] = "position_y=" + Top;
                Shortcut[] updated = new Shortcut[shortcutKeys.Length];
                for (int i = 0; i < shortcutKeys.Length; i++)
                {
                    if (i != 2) updated[i] = new Shortcut(recorders[i].Value);
                    else if (recorders[i].Value.Length > 0)
                    {
                        uint modifiers, key;
                        if (!new Shortcut(recorders[i].Value).TryGlobalHotkey(out modifiers, out key))
                            throw new ArgumentException("截图快捷键需要组合键，例如 Ctrl+Alt+Z。");
                    }
                    entries[i + 6] = shortcutKeys[i] + "=" + recorders[i].Value;
                }
                WriteSettings(entries);
                for (int i = 0; i < shortcuts.Length; i++)
                {
                    shortcutTexts[i] = recorders[i].Value;
                    shortcuts[i] = updated[i];
                }
                RegisterScreenshotHotkey();
                if (screenshotHotkeyConflict && showRequest != null)
                    tray.ShowBalloonTip(3000, "截图快捷键未绑定", "组合键已被占用；点击截图按钮仍可使用。", ToolTipIcon.Warning);
                savedPosition = Location;
                saved = true; settings.Close();
            }
            catch (Exception ex) { MessageBox.Show(settings, ex.Message, "设置保存失败"); }
        };
        cancel.Click += delegate { settings.Close(); };
        settings.FormClosed += delegate
        {
            if (!saved)
            {
                backgroundOpacity = originalOpacity; buttonSize = originalSize;
                buttonGap = originalGap; visibleMask = originalMask;
                ApplyLayout((int)Native.GetDpiForWindow(Handle));
                RegisterScreenshotHotkey();
            }
            if (positionSave.Enabled) { positionSave.Stop(); SavePosition(); }
        };
        content.Controls.AddRange(new Control[] { hint, opacityLabel, opacitySlider, sizeLabel, sizeSlider,
            gapLabel, gapSlider, buttonHeading, buttons });
        footer.Controls.AddRange(new Control[] { save, cancel });
        settings.Controls.Add(content);
        settings.Controls.Add(footer);
        footer.SendToBack();
        settings.ClientSize = new Size(width, Math.Min(y + footerHeight, Math.Max(280, area.Height - 32)));
        settings.Location = new Point(area.Left + (area.Width - settings.Width) / 2,
            area.Top + (area.Height - settings.Height) / 2);
        update();
        settings.Show();
        Native.ShowWindow(settings.Handle, 5);
        settings.BringToFront();
        settings.Activate();
        settings.BeginInvoke((MethodInvoker)delegate
        { if (!settings.IsDisposed) { settings.BringToFront(); settings.Activate(); } });
    }
    internal void Reveal()
    {
        Screen screen = CurrentScreen();
        Rectangle area = screen.WorkingArea;
        Point position = savedPosition ?? new Point(area.Left + (area.Width - Width) / 2,
            area.Top + (area.Height - Height) / 2);
        Location = KeepOnScreen(position, Size, Screen.FromPoint(position).Bounds);
        Show();
        // 明确显示，覆盖启动器传入的 SW_HIDE；置顶且不激活。
        Native.ShowWindow(Handle, 4);
        Native.SetWindowPos(Handle, new IntPtr(-1), Left, Top, Width, Height, 0x50);
        RenderLayered();
        try
        {
            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".codex", "output");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "runtime-status.txt"),
                "visible=" + Native.IsWindowVisible(Handle) + Environment.NewLine +
                "bounds=" + Bounds + Environment.NewLine + "focus_screen=" + screen.DeviceName + Environment.NewLine +
                "background_opacity=" + backgroundOpacity + Environment.NewLine + "dpi=" + Native.GetDpiForWindow(Handle) + Environment.NewLine + "tray_icon=" + tray.Visible + Environment.NewLine +
                "taskbar_icon=" + ShowInTaskbar + Environment.NewLine + "topmost=" + TopMost + Environment.NewLine +
                "revealed_utc=" + DateTime.UtcNow.ToString("o"));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    private void RegisterScreenshotHotkey()
    {
        screenshotHotkeyConflict = false;
        if (screenshotHotkeyRegistered)
        {
            Native.UnregisterHotKey(Handle, 1);
            screenshotHotkeyRegistered = false;
        }
        string configured = shortcutTexts[2];
        if (configured.Length == 0) return;
        uint modifiers, key;
        if (new Shortcut(configured).TryGlobalHotkey(out modifiers, out key)
            && Native.RegisterHotKey(Handle, 1, modifiers | 0x4000, key))
        {
            screenshotHotkeyRegistered = true;
            if (showRequest != null) tray.Text = "悬浮按钮 · 正在运行";
            return;
        }
        // 快捷键被其他程序占用时退让；截图按钮本身仍可使用。
        screenshotHotkeyConflict = true;
        shortcutTexts[2] = "";
        WriteSettings("screenshot=");
        if (showRequest != null) tray.Text = "悬浮按钮 · 截图快捷键未绑定";
    }
    private void QueueScreenshot()
    {
        if (screenshotOpen || pending != null) return;
        pending = BeginScreenshot;
        delay.Start();
    }
    private void BeginScreenshot()
    {
        if (screenshotOpen) return;
        screenshotOpen = true;
        bool wasVisible = Visible;
        Hide();
        System.Windows.Forms.Timer captureDelay = new System.Windows.Forms.Timer { Interval = 140 };
        captureDelay.Tick += delegate
        {
            captureDelay.Stop();
            captureDelay.Dispose();
            Bitmap bitmap = null;
            try
            {
                Rectangle bounds = SystemInformation.VirtualScreen;
                bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                using (ScreenshotOverlay overlay = new ScreenshotOverlay(bitmap, bounds))
                {
                    bitmap = null;
                    overlay.ShowDialog();
                }
            }
            catch (Exception error) { MessageBox.Show(error.Message, "截图失败"); }
            finally
            {
                if (bitmap != null) bitmap.Dispose();
                screenshotOpen = false;
                if (wasVisible && !IsDisposed) Reveal();
            }
        };
        captureDelay.Start();
    }
    private void OnWindowEvent(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        if (!IsHandleCreated || IsDisposed || !Native.IsWindowVisible(Handle)) return;
        Native.RECT bar;
        if (!Native.GetWindowRect(Handle, out bar)) return;
        if (TaskbarAbove(Native.FindWindow("Shell_TrayWnd", null), bar)) return;
        IntPtr taskbar = IntPtr.Zero;
        while ((taskbar = Native.FindWindowEx(IntPtr.Zero, taskbar, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            if (TaskbarAbove(taskbar, bar)) return;
    }
    private bool TaskbarAbove(IntPtr taskbar, Native.RECT bar)
    {
        Native.RECT area;
        if (taskbar == IntPtr.Zero || !Native.GetWindowRect(taskbar, out area)
            || bar.left >= area.right || bar.right <= area.left || bar.top >= area.bottom || bar.bottom <= area.top) return false;
        for (IntPtr above = Native.GetWindow(Handle, 3); above != IntPtr.Zero; above = Native.GetWindow(above, 3))
            if (above == taskbar)
            {
                Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
                return true;
            }
        return false;
    }
    private void Exit() { exitRequested = true; Close(); }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exitRequested && showRequest != null && e.CloseReason != CloseReason.WindowsShutDown
            && e.CloseReason != CloseReason.ApplicationExitCall)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }
    protected override CreateParams CreateParams
    {
        get { CreateParams p = base.CreateParams; p.ExStyle = (p.ExStyle & ~0x00040000) | 0x08000000 | 0x00080000 | 0x00000080; return p; }
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x312 && message.WParam.ToInt32() == 1)
        {
            QueueScreenshot();
            return;
        }
        if (message.Msg == 0x21)
        {
            message.Result = new IntPtr(3);
            return;
        }
        if (message.Msg == 0x2E0)
        {
            int dpi = (int)(message.WParam.ToInt64() & 0xffff);
            ApplyLayout(dpi);
            message.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref message);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dpi = (int)Native.GetDpiForWindow(Handle);
        ApplyLayout(dpi);
        Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
        if (shown && (settings == null || settings.IsDisposed || !settings.Visible)) RegisterScreenshotHotkey();
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (screenshotHotkeyRegistered)
        {
            Native.UnregisterHotKey(Handle, 1);
            screenshotHotkeyRegistered = false;
        }
        base.OnHandleDestroyed(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        RenderLayered();
    }
    private Bitmap DrawSurface()
    {
        Bitmap bitmap = new Bitmap(ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            int diameter = circleDiameter;
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Brush background = new SolidBrush(Color.FromArgb(backgroundOpacity * 255 / 100, BackColor)))
            using (Brush highlight = new SolidBrush(Color.FromArgb(backgroundOpacity * 255 / 100, 208, 214, 222)))
            {
                for (int i = 0; i < labels.Length; i++)
                    if ((visibleMask & (1 << i)) != 0)
                        graphics.FillEllipse(i == hover ? highlight : background, CircleBounds(i));
            }
            using (Bitmap textMask = new Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                TextFormatFlags measureFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                TextFormatFlags drawFlags = measureFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
                // 按目标屏幕的整数像素字号重新绘字，约为原字体的一半。
                int availableWidth = Math.Max(1, diameter - 6);
                int availableHeight = Math.Max(1, diameter - 6);
                Font font = null;
                for (int size = Math.Min(availableHeight, Math.Max(1, diameter * 3 / 10)); size >= 1; size--)
                {
                    Font candidate = new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Pixel);
                    bool fits = true;
                    for (int i = 0; i < labels.Length; i++)
                    {
                        if ((visibleMask & (1 << i)) == 0) continue;
                        Size measured = TextRenderer.MeasureText(labels[i], candidate, new Size(int.MaxValue, int.MaxValue), measureFlags);
                        if (measured.Width > availableWidth || measured.Height > availableHeight)
                        { fits = false; break; }
                    }
                    if (fits || size == 1) { font = candidate; break; }
                    candidate.Dispose();
                }
                using (font)
                using (Graphics textGraphics = Graphics.FromImage(textMask))
                {
                    textGraphics.Clear(Color.White);
                    for (int i = 0; i < labels.Length; i++)
                    {
                        if ((visibleMask & (1 << i)) == 0) continue;
                        // GDI 使用最终像素字号和整数按钮矩形，对字形笔画进行原生网格适配。
                        TextRenderer.DrawText(textGraphics, labels[i], font, CircleBounds(i),
                            Color.Black, Color.White, drawFlags);
                    }
                }
                // 提取原生字形的覆盖率，保留背景透明；不拉伸或压缩任何字形像素。
                var pixels = textMask.LockBits(new Rectangle(0, 0, textMask.Width, textMask.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    byte[] row = new byte[textMask.Width * 4];
                    for (int y = 0; y < textMask.Height; y++)
                    {
                        IntPtr address = IntPtr.Add(pixels.Scan0, y * pixels.Stride);
                        Marshal.Copy(address, row, 0, row.Length);
                        for (int x = 0; x < row.Length; x += 4)
                        {
                            int coverage = 255 - (row[x] + row[x + 1] + row[x + 2] + 1) / 3;
                            row[x] = 34; row[x + 1] = 29; row[x + 2] = 25; row[x + 3] = (byte)coverage;
                        }
                        Marshal.Copy(row, 0, address, row.Length);
                    }
                }
                finally { textMask.UnlockBits(pixels); }
                graphics.DrawImageUnscaled(textMask, 0, 0);
            }
        }
        return bitmap;
    }
    private Rectangle CircleBounds(int index)
    {
        int before = 0;
        for (int i = 0; i < index; i++) if ((visibleMask & (1 << i)) != 0) before++;
        return new Rectangle(circlePadding + before * (circleDiameter + circleGap),
            circlePadding, circleDiameter, circleDiameter);
    }
    private int HitIndex(Point point)
    {
        for (int i = 0; i < labels.Length; i++)
        {
            if ((visibleMask & (1 << i)) == 0) continue;
            Rectangle circle = CircleBounds(i);
            double dx = point.X - circle.Left - circle.Width / 2.0;
            double dy = point.Y - circle.Top - circle.Height / 2.0;
            if (dx * dx + dy * dy <= circle.Width * circle.Height / 4.0) return i;
        }
        return -1;
    }
    internal static Point KeepOnScreen(Point position, Size size, Rectangle area)
    {
        return new Point(Math.Max(area.Left, Math.Min(position.X, area.Right - size.Width)),
            Math.Max(area.Top, Math.Min(position.Y, area.Bottom - size.Height)));
    }
    private void RenderLayered()
    {
        if (!IsHandleCreated || IsDisposed || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        using (Bitmap bitmap = DrawSurface())
        {
            IntPtr screenDC = Native.GetDC(IntPtr.Zero);
            IntPtr memoryDC = Native.CreateCompatibleDC(screenDC);
            IntPtr image = bitmap.GetHbitmap(Color.FromArgb(0));
            IntPtr previous = Native.SelectObject(memoryDC, image);
            try
            {
                Native.POINT destination = new Native.POINT(Left, Top), source = new Native.POINT(0, 0);
                Native.SIZE size = new Native.SIZE(bitmap.Width, bitmap.Height);
                Native.BLEND blend = new Native.BLEND { alpha = 255, format = 1 };
                if (!Native.UpdateLayeredWindow(Handle, screenDC, ref destination, ref size, memoryDC, ref source, 0, ref blend, 2))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                Native.SelectObject(memoryDC, previous); Native.DeleteObject(image);
                Native.DeleteDC(memoryDC); Native.ReleaseDC(IntPtr.Zero, screenDC);
            }
        }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || HitIndex(e.Location) < 0) return;
        downMouse = Cursor.Position;
        downLocation = Location;
        downTick = Environment.TickCount;
        dragging = true;
        moved = false;
        Capture = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        hover = HitIndex(e.Location);
        if (dragging)
        {
            Point current = Cursor.Position;
            int dx = current.X - downMouse.X, dy = current.Y - downMouse.Y;
            if (!moved && CanDrag(unchecked(Environment.TickCount - downTick), dx, dy)) moved = true;
            if (moved)
            {
                Point position = KeepOnScreen(new Point(downLocation.X + dx, downLocation.Y + dy), Size,
                    Screen.FromPoint(current).Bounds);
                if (Location != position)
                {
                    Location = position;
                    positionSave.Stop();
                    positionSave.Start();
                }
                Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
            }
        }
        Invalidate();
    }
    internal static bool CanDrag(int heldMilliseconds, int dx, int dy)
    { return heldMilliseconds >= 100 && (Math.Abs(dx) > 10 || Math.Abs(dy) > 10); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        bool wasDragging = dragging;
        dragging = false;
        Capture = false;
        if (e.Button == MouseButtons.Right) { menu.Show(this, e.Location); return; }
        if (e.Button != MouseButtons.Left || !wasDragging || moved) return;
        if (pending != null) return;
        int index = HitIndex(e.Location);
        if (index < 0) return;
        if (index == 2) { QueueScreenshot(); return; }
        Shortcut shortcut = shortcuts[index];
        pending = shortcut.Send;
        delay.Start();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (showRegistration != null) showRegistration.Unregister(null);
            if (foregroundHook != IntPtr.Zero) Native.UnhookWinEvent(foregroundHook);
            if (reorderHook != IntPtr.Zero) Native.UnhookWinEvent(reorderHook);
            if (screenshotHotkeyRegistered) Native.UnregisterHotKey(Handle, 1);
            tray.Visible = false;
            if (settings != null) settings.Dispose();
            if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose();
            tray.Dispose(); delay.Dispose(); positionSave.Dispose(); menu.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal static class Program
{
    [STAThread] private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 0 && args[0] == "--self-test") { SelfTest(); return; }
        bool created;
        using (Mutex mutex = new Mutex(true, "Local\\FloatingClipboardButtons", out created))
        using (EventWaitHandle showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\FloatingClipboardShow"))
        {
            if (!created) { showRequest.Set(); return; }
            try
            {
                string paste = "Ctrl+V";
                string config = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts.ini");
                if (File.Exists(config)) foreach (string raw in File.ReadAllLines(config))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("#") || !line.Contains("=")) continue;
                    string[] pair = line.Split(new char[] { '=' }, 2);
                    if (pair[0].Trim() == "paste") paste = pair[1].Trim();
                }
                using (FloatingBar bar = new FloatingBar(new Shortcut(paste), showRequest)) Application.Run(bar);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "悬浮按钮启动失败"); }
        }
    }
    private static void SelfTest()
    {
        string resultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".codex", "output", "self-test.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
        bool passed = Check(new Shortcut("Escape"), Keys.Escape)
            && Check(new Shortcut("Back"), Keys.Back)
            && Check(new Shortcut("Ctrl+Alt+Z"), Keys.ControlKey, Keys.Menu, Keys.Z)
            && Check(new Shortcut("Ctrl+C"), Keys.ControlKey, Keys.C)
            && Check(new Shortcut("Ctrl+V"), Keys.ControlKey, Keys.V)
            && Check(new Shortcut("Ctrl+Shift+LWin"), Keys.ControlKey, Keys.ShiftKey, Keys.LWin)
            && CheckHotkey("Ctrl+Alt+Z", 3, Keys.Z)
            && !CheckHotkey("RControlKey", 2, Keys.None)
            && CheckRightControl()
            && ShortcutRecorder.PhysicalModifier(Keys.ControlKey, 0x01000000) == "RControlKey"
            && ShortcutRecorder.PhysicalModifier(Keys.ControlKey, 0) == "Ctrl"
            && ShortcutRecorder.Display("RControlKey") == "右 Ctrl"
            && ShortcutRecorder.Display("Ctrl+Shift+LWin") == "Ctrl + Shift + Win"
            && FloatingBar.KeepOnScreen(new Point(95, 95), new Size(20, 20), new Rectangle(0, 0, 100, 100)) == new Point(80, 80)
            && FloatingBar.KeepOnScreen(new Point(-10, -10), new Size(20, 20), new Rectangle(0, 0, 100, 100)) == Point.Empty
            && !FloatingBar.CanDrag(99, 11, 0) && !FloatingBar.CanDrag(100, 10, 0)
            && FloatingBar.CanDrag(100, 11, 0)
            && FloatingBar.LayoutSize(96, 100, 3, 6) == new Size(179, 29)
            && FloatingBar.LayoutSize(96, 100, 3, 1) == new Size(29, 29)
            && FloatingBar.LayoutSize(96, 200, 10, 1).Width > FloatingBar.LayoutSize(96, 100, 3, 1).Width
            && ScreenshotOverlay.SelectionBounds(new Point(90, 70), new Point(20, 10)) == new Rectangle(20, 10, 70, 60)
            && string.Join("|", FloatingBar.MergeSettings(new string[] { "screenshot=Ctrl+Alt+Z", "opacity=80" },
                "opacity=70", "position_x=12")) == "screenshot=Ctrl+Alt+Z|opacity=70|position_x=12";
        File.WriteAllText(resultPath, "shortcut_sequences_and_drag_bounds_valid=" + passed + Environment.NewLine + "passed=" + passed);
        Environment.ExitCode = passed ? 0 : 1;
    }
    private static bool Check(Shortcut shortcut, params Keys[] expected)
    {
        Native.INPUT[] down, up;
        shortcut.BuildInputs(out down, out up);
        if (down.Length != expected.Length || up.Length != expected.Length) return false;
        for (int i = 0; i < expected.Length; i++)
            if (down[i].type != 1 || down[i].data.keyboard.key != (ushort)expected[i] || down[i].data.keyboard.flags != 0
                || up[i].type != 1 || up[i].data.keyboard.key != (ushort)expected[expected.Length - 1 - i] || up[i].data.keyboard.flags != 2)
                return false;
        return true;
    }
    private static bool CheckHotkey(string value, uint expectedModifiers, Keys expectedKey)
    {
        uint modifiers, key;
        return new Shortcut(value).TryGlobalHotkey(out modifiers, out key)
            && modifiers == expectedModifiers && key == (uint)expectedKey;
    }
    private static bool CheckRightControl()
    {
        Native.INPUT[] down, up;
        new Shortcut("RControlKey").BuildInputs(out down, out up);
        return down.Length == 1 && up.Length == 1
            && down[0].data.keyboard.key == 0 && down[0].data.keyboard.scan == 0x1D && down[0].data.keyboard.flags == 0x09
            && up[0].data.keyboard.key == 0 && up[0].data.keyboard.scan == 0x1D && up[0].data.keyboard.flags == 0x0B;
    }
}
