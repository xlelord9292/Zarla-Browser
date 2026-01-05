using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Zarla.Core.Data.Models;

namespace Zarla.Browser.Views;

public class HistoryViewModel
{
    public long Id { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public DateTime VisitedAt { get; set; }
    public int VisitCount { get; set; }

    public string Initial => GetInitial();
    public string VisitCountText => VisitCount > 1 ? $"{VisitCount} visits" : "1 visit";
    public string TimeAgo => GetTimeAgo();

    private string GetInitial()
    {
        try
        {
            var uri = new Uri(Url);
            var host = uri.Host.Replace("www.", "");
            return host[0].ToString().ToUpper();
        }
        catch
        {
            return "?";
        }
    }

    private string GetTimeAgo()
    {
        var diff = DateTime.UtcNow - VisitedAt;

        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return VisitedAt.ToLocalTime().ToString("MMM d");
    }
}

public partial class HistoryPage : UserControl
{
    private readonly MainWindow _mainWindow;
    private List<HistoryViewModel> _allHistory = new();

    public HistoryPage(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        Loaded += HistoryPage_Loaded;
    }

    private async void HistoryPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadHistory();
    }

    private async Task LoadHistory()
    {
        var entries = await App.Database.GetHistoryAsync(500);

        _allHistory = entries.Select(e => new HistoryViewModel
        {
            Id = e.Id,
            Title = e.Title,
            Url = e.Url,
            VisitedAt = e.VisitedAt,
            VisitCount = e.VisitCount
        }).ToList();

        UpdateList(_allHistory);
    }

    private void UpdateList(List<HistoryViewModel> items)
    {
        HistoryList.ItemsSource = items;
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();

        if (string.IsNullOrEmpty(query))
        {
            UpdateList(_allHistory);
        }
        else
        {
            var results = await App.Database.SearchHistoryAsync(query, 100);
            var viewModels = results.Select(r => new HistoryViewModel
            {
                Id = r.Id,
                Title = r.Title,
                Url = r.Url,
                VisitedAt = r.VisitedAt,
                VisitCount = r.VisitCount
            }).ToList();
            UpdateList(viewModels);
        }
    }

    private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is HistoryViewModel item)
        {
            _mainWindow.NavigateToUrl(item.Url);
        }
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all browsing history?",
            "Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await App.Database.ClearHistoryAsync();
            _allHistory.Clear();
            UpdateList(_allHistory);
        }
    }
}
