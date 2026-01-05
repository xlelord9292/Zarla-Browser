using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Zarla.Browser.Views;

public partial class NewTabPage : UserControl
{
    private readonly MainWindow _mainWindow;

    private readonly (string Name, string Url, string Icon)[] _defaultQuickLinks = new[]
    {
        ("Google", "https://www.google.com", "🔍"),
        ("YouTube", "https://www.youtube.com", "▶"),
        ("GitHub", "https://www.github.com", "🐙"),
        ("Reddit", "https://www.reddit.com", "🔴"),
        ("Twitter", "https://www.twitter.com", "🐦"),
        ("Wikipedia", "https://www.wikipedia.org", "📚")
    };

    public NewTabPage(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;

        Loaded += NewTabPage_Loaded;
    }

    private async void NewTabPage_Loaded(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus();

        // Populate quick links
        PopulateQuickLinks();

        // Load recent history
        await LoadRecentHistory();
    }

    private void PopulateQuickLinks()
    {
        QuickLinksPanel.Children.Clear();

        foreach (var link in _defaultQuickLinks)
        {
            var button = CreateQuickLinkButton(link.Name, link.Url, link.Icon);
            QuickLinksPanel.Children.Add(button);
        }
    }

    private Button CreateQuickLinkButton(string name, string url, string icon)
    {
        var button = new Button
        {
            Style = (Style)FindResource("IconButton"),
            Width = 80,
            Height = 80,
            Margin = new Thickness(8),
            Tag = url
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };

        stack.Children.Add(new Border
        {
            Background = (Brush)FindResource("SurfaceBrush"),
            CornerRadius = new CornerRadius(12),
            Width = 48,
            Height = 48,
            Margin = new Thickness(0, 0, 0, 8),
            Child = new TextBlock
            {
                Text = icon,
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        stack.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        button.Content = stack;
        button.Click += QuickLink_Click;

        return button;
    }

    private void QuickLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string url)
        {
            _mainWindow.NavigateToUrl(url);
        }
    }

    private async Task LoadRecentHistory()
    {
        var history = await App.Database.GetHistoryAsync(8);

        RecentHistory.Items.Clear();

        foreach (var entry in history)
        {
            var button = new Button
            {
                Style = (Style)FindResource("IconButton"),
                Width = 140,
                Height = 100,
                Margin = new Thickness(4),
                Tag = entry.Url
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            stack.Children.Add(new Border
            {
                Background = (Brush)FindResource("SurfaceBrush"),
                CornerRadius = new CornerRadius(8),
                Width = 120,
                Height = 60,
                Margin = new Thickness(0, 0, 0, 8),
                Child = new TextBlock
                {
                    Text = GetDomainInitial(entry.Url),
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)FindResource("AccentBrush")
                }
            });

            stack.Children.Add(new TextBlock
            {
                Text = entry.Title,
                FontSize = 11,
                MaxWidth = 130,
                Foreground = (Brush)FindResource("TextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            button.Content = stack;
            button.Click += (s, e) => _mainWindow.NavigateToUrl(entry.Url);

            RecentHistory.Items.Add(button);
        }
    }

    private string GetDomainInitial(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host.Replace("www.", "");
            return host[0].ToString().ToUpper();
        }
        catch
        {
            return "?";
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            _mainWindow.NavigateToUrl(SearchBox.Text);
        }
    }
}
