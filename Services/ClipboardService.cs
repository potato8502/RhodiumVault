using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace RhodiumVault.Services;

public static class ClipboardService
{
    private static DispatcherTimer? _clearTimer;
    private static string? _lastCopied;

    /// <summary>
    /// Copies text to the clipboard and asks Windows / clipboard managers not to record it
    /// (clipboard history, Cloud Clipboard, third-party monitors). Auto-clears after
    /// <paramref name="clearAfterSeconds"/> seconds - only if the clipboard still holds exactly what we put there.
    /// These flags are requests, not guarantees: software that ignores them can still read the clipboard.
    /// </summary>
    public static void CopySecurely(string text, int clearAfterSeconds = 25)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text);

        // Documented as DWORD values (0 = do not include/upload), so they must be 4 bytes.
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
        // Presence of this format tells clipboard monitors (Ditto, ClipboardFusion, ...) to skip the item.
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(BitConverter.GetBytes(1)));

        Clipboard.SetDataObject(data, copy: true);
        _lastCopied = text;

        _clearTimer?.Stop();
        _clearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(clearAfterSeconds) };
        _clearTimer.Tick += (s, e) => ClearIfStillOurs();
        _clearTimer.Start();
    }

    /// <summary>Clears the clipboard now if it still contains the last secret we copied. Call on lock and on exit.</summary>
    public static void ClearIfStillOurs()
    {
        _clearTimer?.Stop();
        var ours = _lastCopied;
        _lastCopied = null;
        if (ours == null) return;

        try
        {
            if (Clipboard.ContainsText() && Clipboard.GetText() == ours)
                Clipboard.Clear();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another app is holding the clipboard open right now - not worth retrying.
        }
    }
}
