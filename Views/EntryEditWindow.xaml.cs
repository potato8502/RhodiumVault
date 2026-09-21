using System.Windows;
using RhodiumVault.Models;
using RhodiumVault.Services;

namespace RhodiumVault.Views;

public partial class EntryEditWindow : Window
{
    private readonly VaultEntry _entry;

    public EntryEditWindow(VaultEntry entry)
    {
        InitializeComponent();
        _entry = entry;

        var isNew = string.IsNullOrEmpty(entry.Title) && string.IsNullOrEmpty(entry.Username) && string.IsNullOrEmpty(entry.Password);
        HeaderText.Text = isNew ? "Add entry" : "Edit entry";

        TitleBox.Text = entry.Title;
        UsernameBox.Text = entry.Username;
        PasswordMasked.Password = entry.Password; // shown masked - opening an entry never displays the password
        UrlBox.Text = entry.Url;
        NotesBox.Text = entry.Notes;

        Loaded += (s, e) => TitleBox.Focus();
    }

    private string CurrentPassword => RevealToggle.IsChecked == true ? PasswordPlain.Text : PasswordMasked.Password;

    private void RevealToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (RevealToggle.IsChecked == true)
        {
            PasswordPlain.Text = PasswordMasked.Password;
            PasswordMasked.Visibility = Visibility.Collapsed;
            PasswordPlain.Visibility = Visibility.Visible;
            RevealToggle.Content = "Hide";
        }
        else
        {
            PasswordMasked.Password = PasswordPlain.Text;
            PasswordPlain.Visibility = Visibility.Collapsed;
            PasswordMasked.Visibility = Visibility.Visible;
            RevealToggle.Content = "Show";
        }
    }

    private void LengthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LengthValueText != null) LengthValueText.Text = ((int)LengthSlider.Value).ToString();
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        var generated = PasswordGeneratorService.Generate(
            (int)LengthSlider.Value,
            UpperCheck.IsChecked == true,
            LowerCheck.IsChecked == true,
            DigitsCheck.IsChecked == true,
            SymbolsCheck.IsChecked == true);

        // Show the freshly generated password so the user can see what was created.
        PasswordMasked.Password = generated;
        PasswordPlain.Text = generated;
        RevealToggle.IsChecked = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "Please enter a title.", "Missing title", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var password = CurrentPassword;
        _entry.Title = TitleBox.Text.Trim();
        _entry.Username = UsernameBox.Text.Trim();
        if (_entry.Password != password)
        {
            _entry.Password = password;
            _entry.PasswordChangedAt = DateTime.UtcNow;
        }
        _entry.Url = UrlBox.Text.Trim();
        _entry.Notes = NotesBox.Text;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
