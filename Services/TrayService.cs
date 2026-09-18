using System.Drawing;
using System.Windows.Forms;

namespace RhodiumVault.Services;

/// <summary>System-tray icon so Rhodium Vault can keep running (global hotkey) while its window is closed.</summary>
public class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayService(Action onOpen, Action onLock, Action onExit)
    {
        _icon = new NotifyIcon
        {
            Icon = BuildIcon(),
            Visible = true,
            Text = "Rhodium Vault"
        };

        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open Vault");
        openItem.Click += (s, e) => onOpen();

        var lockItem = new ToolStripMenuItem("Lock");
        lockItem.Click += (s, e) => onLock();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => onExit();

        menu.Items.Add(openItem);
        menu.Items.Add(lockItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (s, e) => onOpen();
    }

    private static Icon BuildIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Color.FromArgb(255, 36, 81, 214));
            g.FillRectangle(bg, 0, 0, 32, 32);

            using var pen = new Pen(Color.White, 3.2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawArc(pen, 9, 4, 14, 15, 180, 180);

            using var white = new SolidBrush(Color.White);
            g.FillRectangle(white, 6, 15, 20, 14);

            using var bgBrush = new SolidBrush(Color.FromArgb(255, 36, 81, 214));
            g.FillEllipse(bgBrush, 14, 19, 4, 4);
            g.FillRectangle(bgBrush, 15, 22, 2, 4);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
