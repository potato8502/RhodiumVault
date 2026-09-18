using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace RhodiumVault.Services;

public static class ClipboardService
{
    private static DispatcherTimer? _clearTimer;

    /// <summary>
    /// Copies text to the clipboard, best-effort excluded from Windows' clipboard history (Win+V)
    /// and Cloud Clipboard sync, and auto-clears after <paramref name="clearAfterSeconds"/> seconds
    /// - but only if the clipboard still contains exactly what we put there.
    /// </summary>
    public static void CopySecurely(string text, int clearAfterSeconds = 25)
    {
        var data = new DataObject();
        data.SetData(DataFormats.Text, text);

        // Documented Windows 10+ format names that ask the shell not to keep this
        // content in clipboard history or sync it via Cloud Clipboard.
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(false)));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(false)));

        Clipboard.SetDataObject(data, copy: true);

        _clearTimer?.Stop();
        _clearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(clearAfterSeconds) };
        _clearTimer.Tick += (s, e) =>
        {
            _clearTimer!.Stop();
            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText() == text)
                    Clipboard.Clear();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Another app is holding the clipboard open right now - not worth retrying for this purpose.
            }
        };
        _clearTimer.Start();
    }
}
