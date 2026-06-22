using System.Runtime.InteropServices;

namespace ClipboardPinViewer;

internal sealed class ClipboardPinViewerApp : ApplicationContext
{
    private const int HotkeyId = 0x435056;
    private const int MaxVisibleWindows = 24;

    private readonly ClipboardHistoryService _clipboardHistory = new();
    private readonly MessageWindow _messageWindow;
    private readonly NotifyIcon _trayIcon;
    private readonly List<ViewerForm> _viewers = [];
    private int _showIndex;
    private int _viewerCounter;

    public ClipboardPinViewerApp()
    {
        _messageWindow = new MessageWindow(this);
        NativeMethods.AddClipboardFormatListener(_messageWindow.Handle);

        if (!NativeMethods.RegisterHotKey(_messageWindow.Handle, HotkeyId, 0, (int)Keys.F1))
        {
            MessageBox.Show("注册 F1 全局热键失败。请关闭占用 F1 的程序后重启。", "Clipboard Pin Viewer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Clipboard Pin Viewer",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        _trayIcon.DoubleClick += async (_, _) => await ShowNextAsync();
    }

    public void OnClipboardChanged()
    {
        _showIndex = 0;
    }

    public async Task ShowNextAsync()
    {
        var result = await _clipboardHistory.GetItemAsync(_showIndex);
        try
        {
            if (!result.HasAnyItems)
            {
                _trayIcon.ShowBalloonTip(1200, "Clipboard Pin Viewer", "当前没有可展示的剪贴板历史。", ToolTipIcon.Info);
                return;
            }

            if (result.ReachedEnd || result.Item is null)
            {
                _trayIcon.ShowBalloonTip(1200, "Clipboard Pin Viewer", "已经到达系统剪贴板历史末尾。", ToolTipIcon.Info);
                return;
            }

            ShowItem(result.Item, _showIndex + 1);
            _showIndex++;
        }
        finally
        {
            if (result.Item?.DisposeImageAfterUse == true)
            {
                result.Item.Image?.Dispose();
            }
        }
    }

    public void CloseActiveViewer()
    {
        if (Form.ActiveForm is ViewerForm viewer)
        {
            viewer.Close();
        }
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示下一条 (F1)", null, async (_, _) => await ShowNextAsync());
        menu.Items.Add("关闭全部", null, (_, _) => CloseAllViewers());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());
        return menu;
    }

    private void ShowItem(ClipboardItem item, int index)
    {
        _viewerCounter++;
        var title = item.IsImage ? $"剪贴板图片 #{index}" : $"剪贴板文字 #{index}";
        var form = ViewerForm.Create(item, title);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = GetCascadeLocation(form.Size);
        form.FormClosed += (_, _) => _viewers.Remove(form);

        _viewers.Add(form);
        while (_viewers.Count > MaxVisibleWindows)
        {
            _viewers[0].Close();
        }

        form.Show();
        form.Activate();
    }

    private Point GetCascadeLocation(Size size)
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        var offset = ((_viewerCounter - 1) % 12) * 28;
        var x = Math.Min(area.Left + 80 + offset, Math.Max(area.Left, area.Right - size.Width));
        var y = Math.Min(area.Top + 80 + offset, Math.Max(area.Top, area.Bottom - size.Height));
        return new Point(x, y);
    }

    private void CloseAllViewers()
    {
        foreach (var viewer in _viewers.ToArray())
        {
            viewer.Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseAllViewers();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
            NativeMethods.RemoveClipboardFormatListener(_messageWindow.Handle);
            _messageWindow.DestroyHandle();
        }

        base.Dispose(disposing);
    }

    private sealed class MessageWindow : NativeWindow
    {
        private const int WmHotkey = 0x0312;
        private const int WmClipboardUpdate = 0x031D;
        private readonly ClipboardPinViewerApp _app;

        public MessageWindow(ClipboardPinViewerApp app)
        {
            _app = app;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                _ = _app.ShowNextAsync();
                return;
            }

            if (m.Msg == WmClipboardUpdate)
            {
                _app.OnClipboardChanged();
                return;
            }

            base.WndProc(ref m);
        }
    }
}

internal static partial class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
