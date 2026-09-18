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
        PasswordBox.Text = entry.Password;
        UrlBox.Text = entry.Url;
        NotesBox.Text = entry.Notes;

        Loaded += (s, e) => TitleBox.Focus();
    }

    private void LengthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LengthValueText != null) LengthValueText.Text = ((int)LengthSlider.Value).ToString();
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        PasswordBox.Text = PasswordGeneratorService.Generate(
            (int)LengthSlider.Value,
            UpperCheck.IsChecked == true,
            LowerCheck.IsChecked == true,
            DigitsCheck.IsChecked == true,
            SymbolsCheck.IsChecked == true);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "Please enter a title.", "Missing title", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _entry.Title = TitleBox.Text.Trim();
        _entry.Username = UsernameBox.Text.Trim();
        if (_entry.Password != PasswordBox.Text)
        {
            _entry.Password = PasswordBox.Text;
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
