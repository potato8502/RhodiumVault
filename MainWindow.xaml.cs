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
        _autoLockTimer.Tick += (s, e) => LockNow();
        _autoLockTimer.Start();

        Closing += (s, e) =>
        {
            if (App.IsExiting || _isClosingIntentionally) return;
            // Closing the window (the X button) just hides it to the tray - the vault stays
            // unlocked in memory so the global hotkey can bring it straight back without
            // re-entering the master password. Use "Lock" to actually clear the session.
            e.Cancel = true;
            Hide();
        };

        Render();
    }

    /// <summary>Called from the tray menu's "Lock" item.</summary>
    public void TriggerLock() => LockNow();

    private void Window_Activity(object sender, InputEventArgs e)
    {
        _autoLockTimer.Stop();
        _autoLockTimer.Start();
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
            _vault.Save(_entries);
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

    private void EditEntry(VaultEntry entry)
    {
        var editor = new EntryEditWindow(entry) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            entry.ModifiedAt = DateTime.UtcNow;
            _vault.Save(_entries);
            Render();
        }
    }

    private void DeleteEntry(VaultEntry entry)
    {
        var result = MessageBox.Show(this, $"Delete \"{entry.Title}\"? This can't be undone.", "Delete entry",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        _entries.Remove(entry);
        _vault.Save(_entries);
        Render();
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        var newEntry = new VaultEntry();
        var editor = new EntryEditWindow(newEntry) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            _entries.Add(newEntry);
            _vault.Save(_entries);
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

    private void Lock_Click(object sender, RoutedEventArgs e) => LockNow();

    private void LockNow()
    {
        _autoLockTimer.Stop();
        _vault.Lock();
        _isClosingIntentionally = true;
        App.ShowUnlock();
        Close();
    }
}
