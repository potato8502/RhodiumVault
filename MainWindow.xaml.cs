using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RhodiumVault.Models;
using RhodiumVault.Services;
using RhodiumVault.Views;

namespace RhodiumVault;

public partial class MainWindow : Window
{
    private readonly VaultService _vault;
    private readonly List<VaultEntry> _entries;
    private readonly HashSet<string> _revealed = new();
    private readonly DispatcherTimer _autoLockTimer;
    private const int AutoLockMinutes = 5;
    private bool _isClosingIntentionally;

    public MainWindow(VaultService vault, List<VaultEntry> entries)
    {
        InitializeComponent();
        _vault = vault;
        _entries = entries;

        _autoLockTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(AutoLockMinutes) };
        _autoLockTimer.Tick += (s, e) =>
        {
            // Don't lock while the user is busy in an Add/Edit/Change-password dialog.
            if (OwnedWindows.Count > 0) { ResetAutoLock(); return; }
            LockNow(showUnlock: IsVisible);
        };
        _autoLockTimer.Start();

        // Any mouse/keyboard input anywhere in the app (including dialogs) counts as activity.
        InputManager.Current.PreProcessInput += OnAppInput;
        Closed += (s, e) => InputManager.Current.PreProcessInput -= OnAppInput;

        Closing += (s, e) =>
        {
            if (App.IsExiting || _isClosingIntentionally) return;
            // Closing the window with X locks the vault and leaves the app in the tray.
            // The hotkey / tray icon brings up the unlock screen again.
            e.Cancel = true;
            LockNow(showUnlock: false);
        };

        Render();
    }

    /// <summary>Called from the tray menu's "Lock" item.</summary>
    public void TriggerLock(bool showUnlock = true) => LockNow(showUnlock);

    /// <summary>Wipes the in-memory session before the app exits.</summary>
    public void PrepareExit()
    {
        _autoLockTimer.Stop();
        _vault.Lock();
        _entries.Clear();
    }

    private void OnAppInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseButtonEventArgs or KeyEventArgs)
            ResetAutoLock();
    }

    private void ResetAutoLock()
    {
        _autoLockTimer.Stop();
        _autoLockTimer.Start();
    }

    private void Window_Activity(object sender, InputEventArgs e) => ResetAutoLock();

    /// <summary>Saves the vault; on failure the user is told instead of the app crashing.</summary>
    private bool TrySave()
    {
        try
        {
            _vault.Save(_entries);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this,
                "Your change could not be saved: " + ex.Message + "\n\nThe previous version of your vault on disk is unchanged. "
                + "Check that no other program (antivirus, sync tool) is locking the file and that the disk isn't full, then try again.",
                "Rhodium Vault", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void Render()
    {
        var filter = SearchBox.Text?.Trim() ?? "";
        EntriesPanel.Children.Clear();

        var visible = _entries
            .Where(e => filter.Length == 0
                || e.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || e.Username.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.IsFavorite)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in visible)
            EntriesPanel.Children.Add(BuildEntryCard(entry));

        if (!visible.Any())
        {
            EntriesPanel.Children.Add(new TextBlock
            {
                Text = _entries.Count == 0 ? "No entries yet - add your first one above." : "No entries match your search.",
                Style = (Style)FindResource("Caption"),
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays < 1) return "Changed today";
        if (age.TotalDays < 2) return "Changed yesterday";
        if (age.TotalDays < 30) return $"Changed {(int)age.TotalDays} days ago";
        if (age.TotalDays < 365) return $"Changed {(int)(age.TotalDays / 30)} months ago";
        var years = age.TotalDays / 365;
        return years < 2 ? "Changed over a year ago" : $"Changed {(int)years} years ago";
    }

    private Border BuildEntryCard(VaultEntry entry)
    {
        var card = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0, 0, 0, 10) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var infoPanel = new StackPanel();

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        var favoriteBtn = new Button
        {
            Content = entry.IsFavorite ? "★" : "☆",
            Style = (Style)FindResource("IconButton"),
            Width = 26,
            Height = 26,
            Margin = new Thickness(-6, 0, 4, 0),
            Foreground = entry.IsFavorite ? (System.Windows.Media.Brush)FindResource("WarningBrush") : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush")
        };
        favoriteBtn.Click += (s, e) =>
        {
            entry.IsFavorite = !entry.IsFavorite;
            if (!TrySave()) entry.IsFavorite = !entry.IsFavorite;
            Render();
        };
        titleRow.Children.Add(favoriteBtn);
        titleRow.Children.Add(new TextBlock { Text = entry.Title, Style = (Style)FindResource("H2"), VerticalAlignment = VerticalAlignment.Center });
        infoPanel.Children.Add(titleRow);

        infoPanel.Children.Add(new TextBlock { Text = entry.Username, Style = (Style)FindResource("Caption"), Margin = new Thickness(0, 2, 0, 6) });

        var revealed = _revealed.Contains(entry.Id);
        var passwordText = new TextBlock
        {
            Text = revealed ? entry.Password : new string('•', Math.Min(entry.Password.Length, 14)),
            Style = (Style)FindResource("MonoValue")
        };
        infoPanel.Children.Add(passwordText);

        var age = DateTime.UtcNow - entry.PasswordChangedAt;
        var isStale = age.TotalDays > 365;
        infoPanel.Children.Add(new TextBlock
        {
            Text = FormatAge(age) + (isStale ? " - consider updating" : ""),
            Style = (Style)FindResource("Caption"),
            Foreground = isStale ? (System.Windows.Media.Brush)FindResource("WarningBrush") : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
            Margin = new Thickness(0, 4, 0, 0)
        });

        Grid.SetColumn(infoPanel, 0);
        grid.Children.Add(infoPanel);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };

        var revealBtn = new Button { Content = revealed ? "Hide" : "Show", Style = (Style)FindResource("GhostButton"), Margin = new Thickness(0, 0, 6, 0) };
        revealBtn.Click += (s, e) =>
        {
            if (!_revealed.Add(entry.Id)) _revealed.Remove(entry.Id);
            Render();
        };
        buttons.Children.Add(revealBtn);

        var copyUserBtn = new Button { Content = "Copy user", Style = (Style)FindResource("GhostButton"), Margin = new Thickness(0, 0, 6, 0) };
        copyUserBtn.Click += (s, e) => ClipboardService.CopySecurely(entry.Username);
        buttons.Children.Add(copyUserBtn);

        var copyPassBtn = new Button { Content = "Copy password", Style = (Style)FindResource("AccentButton"), Margin = new Thickness(0, 0, 6, 0) };
        copyPassBtn.Click += (s, e) => ClipboardService.CopySecurely(entry.Password);
        buttons.Children.Add(copyPassBtn);

        var editBtn = new Button { Content = "Edit", Style = (Style)FindResource("GhostButton"), Margin = new Thickness(0, 0, 6, 0) };
        editBtn.Click += (s, e) => EditEntry(entry);
        buttons.Children.Add(editBtn);

        var deleteBtn = new Button { Content = "Delete", Style = (Style)FindResource("DangerButton") };
        deleteBtn.Click += (s, e) => DeleteEntry(entry);
        buttons.Children.Add(deleteBtn);

        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        card.Child = grid;
        return card;
    }

    private static VaultEntry Snapshot(VaultEntry e) => new()
    {
        Id = e.Id, Title = e.Title, Username = e.Username, Password = e.Password, Url = e.Url, Notes = e.Notes,
        CreatedAt = e.CreatedAt, ModifiedAt = e.ModifiedAt, PasswordChangedAt = e.PasswordChangedAt, IsFavorite = e.IsFavorite
    };

    private static void Restore(VaultEntry target, VaultEntry from)
    {
        target.Title = from.Title; target.Username = from.Username; target.Password = from.Password;
        target.Url = from.Url; target.Notes = from.Notes; target.ModifiedAt = from.ModifiedAt;
        target.PasswordChangedAt = from.PasswordChangedAt; target.IsFavorite = from.IsFavorite;
    }

    private void EditEntry(VaultEntry entry)
    {
        var before = Snapshot(entry);
        var editor = new EntryEditWindow(entry) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            entry.ModifiedAt = DateTime.UtcNow;
            if (!TrySave()) Restore(entry, before); // keep memory in sync with what is really on disk
            Render();
        }
    }

    private void DeleteEntry(VaultEntry entry)
    {
        var result = MessageBox.Show(this, $"Delete \"{entry.Title}\"? This can't be undone.", "Delete entry",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        var index = _entries.IndexOf(entry);
        _entries.Remove(entry);
        if (!TrySave()) _entries.Insert(Math.Min(index, _entries.Count), entry);
        Render();
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        var newEntry = new VaultEntry();
        var editor = new EntryEditWindow(newEntry) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            _entries.Add(newEntry);
            if (!TrySave()) _entries.Remove(newEntry);
            Render();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Render();

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.ChangeMasterPasswordWindow(_vault, _entries) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            MessageBox.Show(this, "Master password changed.", "Rhodium Vault", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save an encrypted backup of your vault",
            FileName = $"RhodiumVault-backup-{DateTime.Now:yyyyMMdd}.dat",
            Filter = "Rhodium Vault backup (*.dat)|*.dat|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            // vault.dat is always encrypted, so copying it never exposes your passwords.
            File.Copy(_vault.FilePath, dialog.FileName, overwrite: true);
            MessageBox.Show(this,
                "Backup saved. It is encrypted with your current master password - keep that password safe, the backup is useless without it.",
                "Rhodium Vault", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "The backup could not be saved: " + ex.Message, "Rhodium Vault", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Lock_Click(object sender, RoutedEventArgs e) => LockNow(showUnlock: true);

    private void LockNow(bool showUnlock)
    {
        _autoLockTimer.Stop();
        ClipboardService.ClearIfStillOurs();
        _vault.Lock();
        _entries.Clear(); // drop decrypted entries; the window itself is discarded next
        _isClosingIntentionally = true;
        App.ShowUnlock(showUnlock);
        Close();
    }
}
