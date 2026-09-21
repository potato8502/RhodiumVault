using System.IO;
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

            try
            {
                _vault.CreateNew(password);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowError("Could not create the vault file: " + ex.Message);
                return;
            }
            OpenMain(new List<Models.VaultEntry>());
            return;
        }

        List<Models.VaultEntry>? entries;
        var previousCursor = Mouse.OverrideCursor;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            entries = _vault.Unlock(password);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            ShowError("The vault file could not be read (" + ex.Message + "). "
                + "Your data was not changed. If this keeps happening, restore vault.dat.bak from "
                + System.IO.Path.GetDirectoryName(VaultService.DefaultPath) + ".");
            return;
        }
        finally
        {
            Mouse.OverrideCursor = previousCursor;
        }

        if (entries == null)
        {
            ShowError("Wrong master password.");
            PasswordBox1.Clear();
            PasswordBox1.Focus();
            return;
        }

        if (_vault.RecoveredFromBackup)
            MessageBox.Show(this, "vault.dat was damaged, so your last backup (vault.dat.bak) was restored. Your most recent change may be missing.",
                "Rhodium Vault", MessageBoxButton.OK, MessageBoxImage.Warning);

        OpenMain(entries);
    }

    private void PasswordBox1_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isFirstRun) return;
        var (label, color) = PasswordStrength.Describe(PasswordBox1.Password);
        StrengthText.Text = label;
        StrengthText.Foreground = (System.Windows.Media.Brush)FindResource(color);
        StrengthText.Visibility = PasswordBox1.Password.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
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
