using System.Threading;
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
    private static Mutex? _instanceMutex;
    private static EventWaitHandle? _showSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one instance may run: two instances would each hold their own copy of the entries
        // and silently overwrite each other's changes on save.
        _instanceMutex = new Mutex(true, @"Local\RhodiumVault.SingleInstance", out bool createdNew);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\RhodiumVault.Show");
        if (!createdNew)
        {
            _showSignal.Set(); // ask the running instance to come to the front
            _instanceMutex = null;
            Current.Shutdown();
            return;
        }
        StartShowSignalListener();

        _tray = new TrayService(onOpen: ShowVault, onLock: LockFromTray, onExit: ExitApp);

        ShowUnlock(show: true);

        // Deliberately not Ctrl+Shift+V (paste-as-plain-text in many apps and terminals).
        bool ok = Hotkeys.Register(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, Key.V, ShowVault);
        if (!ok)
            _tray.ShowNotification("Rhodium Vault", "The global shortcut Ctrl+Alt+Shift+V is already used by another program. Open the vault from the tray icon instead.");
    }

    private static void StartShowSignalListener()
    {
        var thread = new Thread(() =>
        {
            while (_showSignal!.WaitOne())
            {
                if (IsExiting) return;
                Current?.Dispatcher.BeginInvoke(ShowVault);
            }
        })
        { IsBackground = true, Name = "RhodiumVault.ShowSignal" };
        thread.Start();
    }

    /// <summary>Creates the unlock screen. <paramref name="show"/> = false keeps it hidden (used when the vault locks itself in the tray).</summary>
    public static void ShowUnlock(bool show)
    {
        MainWin = null;
        var win = new UnlockWindow();
        UnlockWin = win;
        if (show) win.Show();
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

    private static void LockFromTray() => MainWin?.TriggerLock(showUnlock: false);

    public static void ExitApp()
    {
        IsExiting = true;
        ClipboardService.ClearIfStillOurs();
        MainWin?.PrepareExit();
        _tray?.Dispose();
        _showSignal?.Set(); // release the listener thread
        Current.Shutdown();
    }
}
