namespace ClipboardPinViewer;

internal sealed record ClipboardItem(string Kind, string? Text, Image? Image, string Signature, bool DisposeImageAfterUse = false)
{
    public bool IsImage => Image is not null;
}
