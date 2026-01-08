using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Zarla.Core.Security;

namespace Zarla.Browser.Views;

public partial class PasswordsPage : UserControl
{
    private PasswordManager? _passwordManager;
    private string? _editingId;

    public PasswordsPage()
    {
        InitializeComponent();
        Loaded += PasswordsPage_Loaded;
    }

    public void SetPasswordManager(PasswordManager manager)
    {
        _passwordManager = manager;
        _passwordManager.Locked += (s, e) => Dispatcher.Invoke(ShowLockedState);
        _passwordManager.Unlocked += (s, e) => Dispatcher.Invoke(ShowUnlockedState);
    }

    private void PasswordsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_passwordManager == null)
            return;

        if (_passwordManager.IsUnlocked)
        {
            ShowUnlockedState();
        }
        else
        {
            ShowLockedState();
        }

        MasterPasswordBox.Focus();
    }

    private void ShowLockedState()
    {
        LockedPanel.Visibility = Visibility.Visible;
        PasswordsScrollViewer.Visibility = Visibility.Collapsed;
        AddPasswordButton.Visibility = Visibility.Collapsed;
        SearchBox.Visibility = Visibility.Collapsed;

        if (_passwordManager != null && !_passwordManager.HasMasterPassword)
        {
            // First time setup
            LockTitle.Text = "Create Master Password";
            LockSubtitle.Text = "Create a strong master password to protect your saved passwords. You'll need this password to access your passwords.";
            UnlockButtonText.Text = "Create Password";
            ConfirmPasswordBox.Visibility = Visibility.Visible;
        }
        else
        {
            LockTitle.Text = "Unlock Password Manager";
            LockSubtitle.Text = "Enter your master password to access your saved passwords.";
            UnlockButtonText.Text = "Unlock";
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
        }

        MasterPasswordBox.Password = "";
        ConfirmPasswordBox.Password = "";
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowUnlockedState()
    {
        LockedPanel.Visibility = Visibility.Collapsed;
        PasswordsScrollViewer.Visibility = Visibility.Visible;
        AddPasswordButton.Visibility = Visibility.Visible;
        SearchBox.Visibility = Visibility.Visible;

        RefreshPasswordsList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshPasswordsList();
    }

    private void RefreshPasswordsList()
    {
        if (_passwordManager == null || !_passwordManager.IsUnlocked)
            return;

        var searchText = SearchTextBox.Text?.Trim() ?? "";
        var passwords = string.IsNullOrEmpty(searchText)
            ? _passwordManager.Entries.ToList()
            : _passwordManager.Search(searchText);

        PasswordsList.ItemsSource = null;
        PasswordsList.ItemsSource = passwords;
        EmptyState.Visibility = passwords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MasterPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Unlock_Click(sender, e);
        }
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (_passwordManager == null)
            return;

        var password = MasterPasswordBox.Password;

        if (!_passwordManager.HasMasterPassword)
        {
            // Creating new master password
            var confirm = ConfirmPasswordBox.Password;

            if (password.Length < 8)
            {
                ShowError("Password must be at least 8 characters long.");
                return;
            }

            if (password != confirm)
            {
                ShowError("Passwords do not match.");
                return;
            }

            if (_passwordManager.SetMasterPassword(password))
            {
                ShowUnlockedState();
            }
            else
            {
                ShowError("Failed to create master password.");
            }
        }
        else
        {
            // Unlocking existing
            if (_passwordManager.Unlock(password))
            {
                ShowUnlockedState();
            }
            else
            {
                ShowError("Incorrect password. Please try again.");
                MasterPasswordBox.Password = "";
                MasterPasswordBox.Focus();
            }
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void AddPassword_Click(object sender, RoutedEventArgs e)
    {
        _editingId = null;
        DialogTitle.Text = "Add Password";
        DialogWebsite.Text = "";
        DialogUsername.Text = "";
        DialogPassword.Text = "";
        DialogNotes.Text = "";
        PasswordDialog.Visibility = Visibility.Visible;
        DialogWebsite.Focus();
    }

    private void EditPassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id)
            return;

        if (_passwordManager == null || !_passwordManager.IsUnlocked)
            return;

        var entry = _passwordManager.Entries.FirstOrDefault(p => p.Id == id);
        if (entry == null)
            return;

        _editingId = id;
        DialogTitle.Text = "Edit Password";
        DialogWebsite.Text = entry.Website;
        DialogUsername.Text = entry.Username;
        DialogPassword.Text = entry.Password;
        DialogNotes.Text = entry.Notes ?? "";
        PasswordDialog.Visibility = Visibility.Visible;
        DialogWebsite.Focus();
    }

    private void DeletePassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id)
            return;

        if (_passwordManager == null || !_passwordManager.IsUnlocked)
            return;

        var entry = _passwordManager.Entries.FirstOrDefault(p => p.Id == id);
        if (entry == null)
            return;

        var result = MessageBox.Show(
            $"Delete password for {entry.Website}?",
            "Delete Password",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _passwordManager.DeletePassword(id);
            RefreshPasswordsList();
        }
    }

    private void CopyPassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id)
            return;

        if (_passwordManager == null || !_passwordManager.IsUnlocked)
            return;

        var entry = _passwordManager.Entries.FirstOrDefault(p => p.Id == id);
        if (entry == null)
            return;

        try
        {
            Clipboard.SetText(entry.Password);

            // Visual feedback
            var originalContent = button.Content;
            button.Content = "Copied!";
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, args) =>
            {
                button.Content = originalContent;
                timer.Stop();
            };
            timer.Start();
        }
        catch { }
    }

    private void GeneratePassword_Click(object sender, RoutedEventArgs e)
    {
        DialogPassword.Text = PasswordManager.GeneratePassword(16, true);
    }

    private void SaveDialog_Click(object sender, RoutedEventArgs e)
    {
        if (_passwordManager == null || !_passwordManager.IsUnlocked)
            return;

        var website = DialogWebsite.Text.Trim();
        var username = DialogUsername.Text.Trim();
        var password = DialogPassword.Text;
        var notes = string.IsNullOrWhiteSpace(DialogNotes.Text) ? null : DialogNotes.Text.Trim();

        if (string.IsNullOrEmpty(website))
        {
            MessageBox.Show("Please enter a website.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(username))
        {
            MessageBox.Show("Please enter a username or email.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            MessageBox.Show("Please enter a password.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool success;
        if (_editingId != null)
        {
            success = _passwordManager.UpdatePassword(_editingId, website, username, password, notes);
        }
        else
        {
            success = _passwordManager.AddPassword(website, username, password, notes);
        }

        if (success)
        {
            PasswordDialog.Visibility = Visibility.Collapsed;
            RefreshPasswordsList();
        }
        else
        {
            MessageBox.Show("Failed to save password.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelDialog_Click(object sender, RoutedEventArgs e)
    {
        PasswordDialog.Visibility = Visibility.Collapsed;
    }
}
