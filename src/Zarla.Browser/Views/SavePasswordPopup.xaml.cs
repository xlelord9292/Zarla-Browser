using System;
using System.Windows;
using System.Windows.Controls;

namespace Zarla.Browser.Views;

public partial class SavePasswordPopup : UserControl
{
    private string _actualPassword = "";
    private bool _isPasswordVisible = false;

    public event EventHandler<SavePasswordEventArgs>? SaveRequested;
    public event EventHandler? NeverRequested;
    public event EventHandler? CloseRequested;

    public SavePasswordPopup()
    {
        InitializeComponent();
    }

    public void SetCredentials(string site, string username, string password)
    {
        SiteText.Text = site;
        UsernameText.Text = username;
        _actualPassword = password;
        PasswordText.Text = new string('•', Math.Min(password.Length, 16));
        _isPasswordVisible = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveRequested?.Invoke(this, new SavePasswordEventArgs
        {
            Site = SiteText.Text,
            Username = UsernameText.Text,
            Password = _actualPassword
        });
    }

    private void Never_Click(object sender, RoutedEventArgs e)
    {
        NeverRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        PasswordText.Text = _isPasswordVisible
            ? _actualPassword
            : new string('•', Math.Min(_actualPassword.Length, 16));
    }
}

public class SavePasswordEventArgs : EventArgs
{
    public string Site { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
