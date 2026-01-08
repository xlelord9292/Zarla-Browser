using System.Windows;
using System.Windows.Controls;

namespace Zarla.Browser.Views;

public partial class DocsPage : UserControl
{
    private readonly (string Key, string Action)[] _shortcuts = new[]
    {
        ("Ctrl + T", "New Tab"),
        ("Ctrl + W", "Close Tab"),
        ("Ctrl + Tab", "Next Tab"),
        ("Ctrl + Shift + Tab", "Previous Tab"),
        ("Ctrl + L", "Focus Address Bar"),
        ("Ctrl + R / F5", "Refresh"),
        ("Ctrl + D", "Bookmark Page"),
        ("Ctrl + H", "History"),
        ("Ctrl + J", "Downloads"),
        ("Ctrl + Shift + N", "New Window"),
        ("Ctrl + F", "Find in Page"),
        ("Ctrl + +", "Zoom In"),
        ("Ctrl + -", "Zoom Out"),
        ("Ctrl + 0", "Reset Zoom"),
        ("F12", "Developer Tools"),
        ("Alt + Left", "Back"),
        ("Alt + Right", "Forward")
    };

    public DocsPage()
    {
        InitializeComponent();
        Loaded += DocsPage_Loaded;
    }

    private void DocsPage_Loaded(object sender, RoutedEventArgs e)
    {
        PopulateShortcuts();
    }

    private void PopulateShortcuts()
    {
        ShortcutsPanel.Children.Clear();

        foreach (var (key, action) in _shortcuts)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyText = new TextBlock
            {
                Text = key,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var actionText = new TextBlock
            {
                Text = action,
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(keyText, 0);
            Grid.SetColumn(actionText, 1);

            grid.Children.Add(keyText);
            grid.Children.Add(actionText);

            ShortcutsPanel.Children.Add(grid);
        }
    }

    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            // Hide all sections
            if (GettingStartedSection != null) GettingStartedSection.Visibility = Visibility.Collapsed;
            if (SecuritySection != null) SecuritySection.Visibility = Visibility.Collapsed;
            if (PrivacySection != null) PrivacySection.Visibility = Visibility.Collapsed;
            if (AISection != null) AISection.Visibility = Visibility.Collapsed;
            if (NetworkSection != null) NetworkSection.Visibility = Visibility.Collapsed;
            if (ErrorCodesSection != null) ErrorCodesSection.Visibility = Visibility.Collapsed;
            if (ShortcutsSection != null) ShortcutsSection.Visibility = Visibility.Collapsed;
            if (FAQSection != null) FAQSection.Visibility = Visibility.Collapsed;

            // Show selected section
            var section = tag switch
            {
                "GettingStarted" => GettingStartedSection,
                "Security" => SecuritySection,
                "Privacy" => PrivacySection,
                "AI" => AISection,
                "Network" => NetworkSection,
                "ErrorCodes" => ErrorCodesSection,
                "Shortcuts" => ShortcutsSection,
                "FAQ" => FAQSection,
                _ => GettingStartedSection
            };

            if (section != null)
                section.Visibility = Visibility.Visible;
        }
    }
}
