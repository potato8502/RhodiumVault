using System.Windows;
using RhodiumVault.Models;
using RhodiumVault.Services;

namespace RhodiumVault.Views;

public partial class ChangeMasterPasswordWindow : Window
{
    private readonly VaultService _vault;
    private readonly List<VaultEntry> _entries;

    public ChangeMasterPasswordWindow(VaultService vault, List<VaultEntry> entries)
    {
        InitializeComponent();
        _vault = vault;
        _entries = entries;
        Loaded += (s, e) => CurrentBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var current = CurrentBox.Password;
        var newPw = NewBox.Password;
        var confirm = ConfirmBox.Password;

        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(newPw))
        {
            ShowError("Please fill in all fields.");
            return;
        }
        if (newPw.Length < 8)
        {
            ShowError("Use at least 8 characters for your new master password.");
            return;
        }
        if (newPw != confirm)
        {
            ShowError("New passwords don't match.");
            return;
        }

        // Re-verify the current password before allowing a change, so an unattended
        // unlocked session can't be used to lock the real owner out.
        if (_vault.Unlock(current) == null)
        {
            ShowError("Current master password is wrong.");
            return;
        }

        _vault.ChangeMasterPassword(_entries, newPw);
        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
