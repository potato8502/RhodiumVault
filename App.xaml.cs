using System.Windows;
using System.Windows.Input;
using RhodiumVault.Services;
using RhodiumVault.Views;

namespace RhodiumVault;

public partial class App : Application
{
    public static readonly HotkeyService Hotkeys = new();
    public static UnlockWindow? UnlockWin;
    public static MainWindow? MainWin;
    public static bool IsExiting { get; private set; }

    private static TrayService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _tray = new TrayService(onOpen: ShowVault, onLock: LockFromTray, onExit: ExitApp);

        ShowUnlock();
        Hotkeys.Register(HotkeyModifiers.Control | HotkeyModifiers.Shift, Key.V, ShowVault);
    }

    public static void ShowUnlock()
    {
        MainWin = null;
        var win = new UnlockWindow();
        UnlockWin = win;
        win.Show();
        Hotkeys.AttachTo(win);
    }

    public static void ShowMain(MainWindow main)
    {
        UnlockWin = null;
        MainWin = main;
        Hotkeys.AttachTo(main);
    }

    public static void ShowVault()
    {
        Window? target = MainWin ?? (Window?)UnlockWin;
        if (target == null) return;
        target.Show();
        target.WindowState = WindowState.Normal;
        target.Activate();
    }

    private static void LockFromTray() => MainWin?.TriggerLock();

    public static void ExitApp()
    {
        IsExiting = true;
        _tray?.Dispose();
        Current.Shutdown();
    }
}
