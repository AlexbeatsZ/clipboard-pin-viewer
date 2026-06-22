using System.Runtime.InteropServices;

namespace ClipboardPinViewer;

internal sealed partial class ViewerForm : Form
{
    private const int PaddingSize = 1;
    private const int ResizeGripSize = 30;
    private readonly Image? _ownedImage;
    private readonly double? _fixedAspectRatio;

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

        var box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = form.BackColor,
            ForeColor = form.ForeColor,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.None,
            Text = text,
            WordWrap = true
        };

        form.Controls.Add(box);
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

        var picture = new PictureBox
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
        const int wmNcHitTest = 0x0084;
        const int wmSizing = 0x0214;

        if (m.Msg == wmSizing && _fixedAspectRatio is not null)
        {
            KeepSizingAspectRatio(m.WParam, m.LParam, _fixedAspectRatio.Value);
            m.Result = 1;
            return;
        }

        if (m.Msg == wmNcHitTest)
        {
            base.WndProc(ref m);
            if ((int)m.Result == HitTestValues.Client)
            {
                m.Result = HitTestForResize(m.LParam);
            }
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

        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, 0x00A1, HitTestValues.Caption, 0);
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

    private static Size EstimateTextWindowSize(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var longest = Math.Max(1, lines.Max(EstimateVisualLength));
        var width = Math.Clamp(longest * 9 + 40, 260, 920);
        var visualLines = lines.Sum(line => Math.Max(1, (int)Math.Ceiling(EstimateVisualLength(line) * 9.0 / Math.Max(1, width - 40))));
        var height = Math.Clamp(visualLines * 26 + 32, 80, 720);
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
