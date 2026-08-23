using System.Windows;
using System.Windows.Input;

namespace WinClipboard.App.Views;

/// <summary>Minimal text-input dialog. WPF has no built-in InputBox, and pulling in Microsoft.VisualBasic for one would look out of place against the app's own styling.</summary>
public partial class RenameDialog : Window
{
    public string Value => ValueBox.Text.Trim();

    private RenameDialog(string prompt, string initialValue)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        ValueBox.Text = initialValue;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            // Preselect the stem, not the extension — renaming a file almost never means
            // changing ".png", so typing straight away replaces only the part that matters.
            var extensionIndex = initialValue.LastIndexOf('.');
            ValueBox.Select(0, extensionIndex > 0 ? extensionIndex : initialValue.Length);
        };
    }

    /// <summary>Returns the new value, or null if the user cancelled or did not change anything.</summary>
    public static string? Prompt(Window owner, string prompt, string initialValue)
    {
        var dialog = new RenameDialog(prompt, initialValue) { Owner = owner };
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var value = dialog.Value;
        return string.IsNullOrWhiteSpace(value) || value == initialValue ? null : value;
    }

    private void OnConfirmClicked(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnValueKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
        }
    }
}
