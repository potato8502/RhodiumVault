using System.Windows;
using System.Windows.Input;
using RhodiumVault.Services;

namespace RhodiumVault.Views;

public partial class UnlockWindow : Window
{
    private readonly VaultService _vault = new();
    private readonly bool _isFirstRun;
    private bool _proceedingToMain;

    public UnlockWindow()
    {
        InitializeComponent();
        _isFirstRun = !VaultService.VaultExists();

        if (_isFirstRun)
        {
            SubtitleText.Text = "Welcome - let's set up your vault.";
            CreateNotice.Visibility = Visibility.Visible;
            ConfirmLabel.Visibility = Visibility.Visible;
            PasswordBox2.Visibility = Visibility.Visible;
            ActionButton.Content = "Create Vault";
        }

        Loaded += (s, e) => PasswordBox1.Focus();
        Closed += (s, e) =>
        {
            if (!_proceedingToMain && !App.IsExiting) App.ExitApp();
        };
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ActionButton_Click(sender, e);
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        var password = PasswordBox1.Password;

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter a master password.");
            return;
        }

        if (_isFirstRun)
        {
            if (password.Length < 8)
            {
                ShowError("Use at least 8 characters for your master password.");
                return;
            }
            if (password != PasswordBox2.Password)
            {
                ShowError("Passwords don't match.");
                return;
            }

            _vault.CreateNew(password);
            OpenMain(new List<Models.VaultEntry>());
            return;
        }

        var entries = _vault.Unlock(password);
        if (entries == null)
        {
            ShowError("Wrong master password.");
            PasswordBox1.Clear();
            PasswordBox1.Focus();
            return;
        }

        OpenMain(entries);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OpenMain(List<Models.VaultEntry> entries)
    {
        _proceedingToMain = true;
        var main = new MainWindow(_vault, entries);
        main.Show();
        App.ShowMain(main);
        Close();
    }
}
