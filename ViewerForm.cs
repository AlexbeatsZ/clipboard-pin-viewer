using System.Runtime.InteropServices;

namespace ClipboardPinViewer;

internal sealed partial class ViewerForm : Form
{
    private const int PaddingSize = 1;
    private const int ResizeGripSize = 8;
    private const int MinTextWidth = 260;
    private const int MinTextHeight = 80;
    private const int WmNcHitTest = 0x0084;
    private const int WmSizing = 0x0214;
    private const int WmMouseWheel = 0x020A;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonDblClk = 0x0203;
    private const int EmLineScroll = 0x00B6;
    private const int WheelNotch = 120;

    private readonly Image? _ownedImage;
    private readonly double? _fixedAspectRatio;
    private TextBoxBase? _textContent;
    private int _wheelRemainder;

    private ViewerForm(Image? ownedImage, double? fixedAspectRatio = null)
    {
        _ownedImage = ownedImage;
        _fixedAspectRatio = fixedAspectRatio;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(24, 27, 28);
        ForeColor = Color.FromArgb(242, 242, 242);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        MinimumSize = new Size(120, 80);
        ShowInTaskbar = false;
        TopMost = true;
        Padding = new Padding(PaddingSize);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };
    }

    public static ViewerForm Create(ClipboardItem item, string title)
    {
        return item.IsImage
            ? CreateImageViewer(item.Image!, title)
            : CreateTextViewer(item.Text ?? string.Empty, title);
    }

    private static ViewerForm CreateTextViewer(string text, string title)
    {
        var form = new ViewerForm(null)
        {
            Text = title,
            Size = EstimateTextWindowSize(text)
        };

        var box = new ViewerTextBox(form)
        {
            BorderStyle = BorderStyle.None,
            BackColor = form.BackColor,
            ForeColor = form.ForeColor,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point),
            Multiline = true,
            ReadOnly = true,
            // 滚动条保持隐藏；滚轮由 ScrollTextContent 直接发 EM_LINESCROLL 驱动。
            ScrollBars = ScrollBars.None,
            TabStop = false,
            Text = text,
            WordWrap = true
        };

        form._textContent = box;
        form.Controls.Add(box);

        // 只读 TextBox 拿到焦点时会整篇反选，显示成一片高亮。
        form.Shown += (_, _) => box.DeselectAll();
        return form;
    }

    private static ViewerForm CreateImageViewer(Image image, string title)
    {
        var bitmap = new Bitmap(image);
        var form = new ViewerForm(bitmap, (double)bitmap.Width / bitmap.Height)
        {
            Text = title,
            Size = EstimateImageWindowSize(bitmap.Size)
        };

        var picture = new ViewerPictureBox
        {
            BackColor = Color.FromArgb(24, 27, 28),
            Dock = DockStyle.Fill,
            Image = bitmap,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        picture.MouseDown += (_, e) => form.BeginDrag(e);

        form.Controls.Add(picture);
        return form;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        BeginDrag(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _ownedImage?.Dispose();
        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmSizing && _fixedAspectRatio is not null)
        {
            KeepSizingAspectRatio(m.WParam, m.LParam, _fixedAspectRatio.Value);
            m.Result = 1;
            return;
        }

        if (m.Msg == WmNcHitTest)
        {
            base.WndProc(ref m);
            if ((int)m.Result == HitTestValues.Client)
            {
                m.Result = HitTestForResize(m.LParam);
            }
            return;
        }

        // 焦点落在窗体自身时滚轮也要能滚动文字。
        if (m.Msg == WmMouseWheel && ScrollTextContent(m.WParam))
        {
            m.Result = 0;
            return;
        }

        base.WndProc(ref m);
    }

    private void BeginDrag(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        BeginDragWindow();
    }

    private void BeginDragWindow()
    {
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, 0x00A1, HitTestValues.Caption, 0);
    }

    /// <summary>
    /// 把滚轮转数折算成显示行数，直接驱动 Edit 控件滚动。
    /// Edit 在没有 WS_VSCROLL 时不自行响应 WM_MOUSEWHEEL，但 EM_LINESCROLL 依然有效且会自动夹在首尾。
    /// </summary>
    private bool ScrollTextContent(IntPtr wParam)
    {
        if (_textContent is null || _textContent.IsDisposed)
        {
            return false;
        }

        _wheelRemainder += (short)(((long)wParam >> 16) & 0xFFFF);
        var notches = _wheelRemainder / WheelNotch;
        if (notches == 0)
        {
            // 高精度触摸板会发小于一转的增量，先累积。
            return true;
        }

        _wheelRemainder -= notches * WheelNotch;

        var linesPerNotch = SystemInformation.MouseWheelScrollLines;
        if (linesPerNotch < 0)
        {
            linesPerNotch = VisibleTextLineCount();
        }

        var lines = -notches * Math.Max(1, linesPerNotch);
        NativeMethods.SendMessage(_textContent.Handle, EmLineScroll, 0, lines);
        return true;
    }

    private int VisibleTextLineCount()
    {
        if (_textContent is null)
        {
            return 1;
        }

        var lineHeight = Math.Max(1, _textContent.Font.Height);
        return Math.Max(1, _textContent.ClientSize.Height / lineHeight);
    }

    /// <summary>
    /// 子控件铺满客户区会吃掉窗体的 WM_NCHITTEST，于是边缘一圈由子控件回报 HTTRANSPARENT，
    /// 让命中穿透到窗体的 <see cref="HitTestForResize"/>，拿到原生的缩放光标和缩放行为。
    /// </summary>
    private static bool IsInResizeBand(Control control, IntPtr lParam)
    {
        var cursor = control.PointToClient(new Point((short)((long)lParam & 0xFFFF), (short)(((long)lParam >> 16) & 0xFFFF)));

        return cursor.X <= ResizeGripSize
            || cursor.Y <= ResizeGripSize
            || cursor.X >= control.ClientSize.Width - ResizeGripSize
            || cursor.Y >= control.ClientSize.Height - ResizeGripSize;
    }

    private sealed class ViewerTextBox(ViewerForm owner) : TextBox
    {
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WmNcHitTest when IsInResizeBand(this, m.LParam):
                    m.Result = HitTestValues.Transparent;
                    return;

                case WmMouseWheel when owner.ScrollTextContent(m.WParam):
                    m.Result = 0;
                    return;

                // 不带修饰键的左键用来移动窗口，和图片浮层一致；按住 Shift/Ctrl 才进入选词。
                case WmLButtonDown or WmLButtonDblClk when ModifierKeys == Keys.None:
                    owner.BeginDragWindow();
                    return;
            }

            base.WndProc(ref m);
        }
    }

    private sealed class ViewerPictureBox : PictureBox
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest && IsInResizeBand(this, m.LParam))
            {
                m.Result = HitTestValues.Transparent;
                return;
            }

            base.WndProc(ref m);
        }
    }

    private IntPtr HitTestForResize(IntPtr lParam)
    {
        var cursor = PointToClient(new Point((short)((long)lParam & 0xFFFF), (short)(((long)lParam >> 16) & 0xFFFF)));

        var left = cursor.X <= ResizeGripSize;
        var right = cursor.X >= ClientSize.Width - ResizeGripSize;
        var top = cursor.Y <= ResizeGripSize;
        var bottom = cursor.Y >= ClientSize.Height - ResizeGripSize;

        return (top, bottom, left, right) switch
        {
            (true, _, true, _) => HitTestValues.TopLeft,
            (true, _, _, true) => HitTestValues.TopRight,
            (_, true, true, _) => HitTestValues.BottomLeft,
            (_, true, _, true) => HitTestValues.BottomRight,
            (true, _, _, _) => HitTestValues.Top,
            (_, true, _, _) => HitTestValues.Bottom,
            (_, _, true, _) => HitTestValues.Left,
            (_, _, _, true) => HitTestValues.Right,
            _ => HitTestValues.Client
        };
    }

    private static void KeepSizingAspectRatio(IntPtr edge, IntPtr rectPointer, double aspectRatio)
    {
        var rect = Marshal.PtrToStructure<WindowRect>(rectPointer);
        var width = Math.Max(120, rect.Right - rect.Left);
        var height = Math.Max(80, rect.Bottom - rect.Top);
        var edgeValue = edge.ToInt32();

        if (edgeValue is SizingEdges.Left or SizingEdges.Right)
        {
            height = Math.Max(80, (int)Math.Round(width / aspectRatio));
            rect.Bottom = rect.Top + height;
        }
        else
        {
            width = Math.Max(120, (int)Math.Round(height * aspectRatio));
            if (edgeValue is SizingEdges.Left or SizingEdges.TopLeft or SizingEdges.BottomLeft)
            {
                rect.Left = rect.Right - width;
            }
            else
            {
                rect.Right = rect.Left + width;
            }
        }

        if (edgeValue is SizingEdges.Top or SizingEdges.TopLeft or SizingEdges.TopRight)
        {
            rect.Top = rect.Bottom - height;
        }
        else
        {
            rect.Bottom = rect.Top + height;
        }

        Marshal.StructureToPtr(rect, rectPointer, false);
    }

    private static Size EstimateImageWindowSize(Size imageSize)
    {
        var area = Screen.PrimaryScreen?.WorkingArea.Size ?? new Size(1280, 720);
        var maxWidth = Math.Max(240, area.Width - 180);
        var maxHeight = Math.Max(180, area.Height - 180);
        var scale = Math.Min(1.0, Math.Min((double)maxWidth / imageSize.Width, (double)maxHeight / imageSize.Height));
        return new Size(Math.Max(160, (int)Math.Round(imageSize.Width * scale)), Math.Max(120, (int)Math.Round(imageSize.Height * scale)));
    }

    /// <summary>
    /// 自动生成的文字浮层最大只到主屏工作区的 1/3 宽、1/3 高；更长的内容靠滚轮翻，
    /// 不在这里把窗口撑大。窗口本身不设 MaximumSize，用户仍可手动拖到任意尺寸。
    /// </summary>
    private static Size EstimateTextWindowSize(string text)
    {
        var area = Screen.PrimaryScreen?.WorkingArea.Size ?? new Size(1280, 720);
        var maxWidth = Math.Max(MinTextWidth, area.Width / 3);
        var maxHeight = Math.Max(MinTextHeight, area.Height / 3);

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var longest = Math.Max(1, lines.Max(EstimateVisualLength));
        var width = Math.Clamp(longest * 9 + 40, MinTextWidth, maxWidth);
        var visualLines = lines.Sum(line => Math.Max(1, (int)Math.Ceiling(EstimateVisualLength(line) * 9.0 / Math.Max(1, width - 40))));
        var height = Math.Clamp(visualLines * 26 + 32, MinTextHeight, maxHeight);
        return new Size(width, height);
    }

    private static int EstimateVisualLength(string text)
    {
        var length = 0;
        foreach (var c in text)
        {
            length += c > 255 ? 2 : 1;
        }
        return length;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct WindowRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

internal static class SizingEdges
{
    public const int Left = 1;
    public const int Right = 2;
    public const int Top = 3;
    public const int TopLeft = 4;
    public const int TopRight = 5;
    public const int Bottom = 6;
    public const int BottomLeft = 7;
    public const int BottomRight = 8;
}

internal static partial class NativeMethods
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}

internal static class HitTestValues
{
    public static readonly IntPtr Transparent = -1;
    public static readonly IntPtr Client = 1;
    public static readonly IntPtr Caption = 2;
    public static readonly IntPtr Left = 10;
    public static readonly IntPtr Right = 11;
    public static readonly IntPtr Top = 12;
    public static readonly IntPtr TopLeft = 13;
    public static readonly IntPtr TopRight = 14;
    public static readonly IntPtr Bottom = 15;
    public static readonly IntPtr BottomLeft = 16;
    public static readonly IntPtr BottomRight = 17;
}
