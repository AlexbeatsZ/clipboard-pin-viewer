using System.Security.Cryptography;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using WinRtClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace ClipboardPinViewer;

internal sealed class ClipboardHistoryService
{
    private const int MaxItems = 30;
    private readonly List<ClipboardItem> _fallbackItems = [];

    public async Task<ClipboardReadResult> GetNextUnshownItemAsync(IReadOnlySet<string> shownSignatures)
    {
        var current = ReadCurrentClipboard();
        if (current is not null)
        {
            AddFallbackItem(current);
            if (!shownSignatures.Contains(current.Signature))
            {
                return new ClipboardReadResult(current, HasAnyItems: true, ReachedEnd: false);
            }
        }

        try
        {
            if (WinRtClipboard.IsHistoryEnabled())
            {
                var result = await WinRtClipboard.GetHistoryItemsAsync();
                if (result.Status == ClipboardHistoryItemsResultStatus.Success)
                {
                    var supportedCount = 0;
                    foreach (var historyItem in result.Items.Take(MaxItems))
                    {
                        if (!IsSupported(historyItem.Content))
                        {
                            continue;
                        }

                        supportedCount++;
                        var item = await ReadDataPackageAsync(historyItem.Content);
                        if (item is null)
                        {
                            continue;
                        }

                        if (!shownSignatures.Contains(item.Signature))
                        {
                            return new ClipboardReadResult(item, HasAnyItems: true, ReachedEnd: false);
                        }

                        if (item.DisposeImageAfterUse)
                        {
                            item.Image?.Dispose();
                        }
                    }

                    return new ClipboardReadResult(null, supportedCount > 0 || current is not null, ReachedEnd: true);
                }
            }
        }
        catch
        {
            // Fall back to the current clipboard below. Clipboard history can be blocked by policy or transient access failures.
        }

        if (_fallbackItems.Count == 0)
        {
            return new ClipboardReadResult(null, HasAnyItems: false, ReachedEnd: false);
        }

        foreach (var item in _fallbackItems)
        {
            if (!shownSignatures.Contains(item.Signature))
            {
                return new ClipboardReadResult(item, HasAnyItems: true, ReachedEnd: false);
            }
        }

        return new ClipboardReadResult(null, HasAnyItems: true, ReachedEnd: true);
    }

    private static bool IsSupported(DataPackageView content)
    {
        return content.Contains(StandardDataFormats.Bitmap) || content.Contains(StandardDataFormats.Text);
    }

    private static async Task<ClipboardItem?> ReadDataPackageAsync(DataPackageView content)
    {
        if (content.Contains(StandardDataFormats.Bitmap))
        {
            try
            {
                var reference = await content.GetBitmapAsync();
                using var randomStream = await reference.OpenReadAsync();
                using var stream = randomStream.AsStreamForRead();
                using var loaded = Image.FromStream(stream);
                var bitmap = new Bitmap(loaded);
                return new ClipboardItem("image", null, bitmap, ImageSignature(bitmap), DisposeImageAfterUse: true);
            }
            catch
            {
                return null;
            }
        }

        if (content.Contains(StandardDataFormats.Text))
        {
            try
            {
                var text = await content.GetTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    return new ClipboardItem("text", text, null, TextSignature(text));
                }
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static ClipboardItem? ReadCurrentClipboard()
    {
        try
        {
            if (System.Windows.Forms.Clipboard.ContainsImage())
            {
                using var image = System.Windows.Forms.Clipboard.GetImage();
                if (image is not null)
                {
                    var bitmap = new Bitmap(image);
                    return new ClipboardItem("image", null, bitmap, ImageSignature(bitmap));
                }
            }

            if (System.Windows.Forms.Clipboard.ContainsText())
            {
                var text = System.Windows.Forms.Clipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return new ClipboardItem("text", text, null, TextSignature(text));
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private void AddFallbackItem(ClipboardItem item)
    {
        if (_fallbackItems.Count > 0 && _fallbackItems[0].Signature == item.Signature)
        {
            item.Image?.Dispose();
            return;
        }

        _fallbackItems.Insert(0, item);
        while (_fallbackItems.Count > MaxItems)
        {
            _fallbackItems[^1].Image?.Dispose();
            _fallbackItems.RemoveAt(_fallbackItems.Count - 1);
        }
    }

    private static string TextSignature(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return $"text:{Convert.ToHexString(bytes.AsSpan(0, 8))}:{text.Length}";
    }

    private static string ImageSignature(Image image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        var bytes = SHA256.HashData(stream.ToArray());
        return $"image:{image.Width}x{image.Height}:{Convert.ToHexString(bytes.AsSpan(0, 8))}";
    }
}

internal sealed record ClipboardReadResult(ClipboardItem? Item, bool HasAnyItems, bool ReachedEnd);
