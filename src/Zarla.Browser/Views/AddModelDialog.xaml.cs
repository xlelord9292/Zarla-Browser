using System.Windows;

namespace Zarla.Browser.Views;

public partial class AddModelDialog : Window
{
    public string ModelName => ModelNameTextBox.Text.Trim();
    public string ModelId => ModelIdTextBox.Text.Trim();
    public string? ApiKey => string.IsNullOrWhiteSpace(ApiKeyTextBox.Text) ? null : ApiKeyTextBox.Text.Trim();
    public string? BaseUrl => string.IsNullOrWhiteSpace(BaseUrlTextBox.Text) ? null : BaseUrlTextBox.Text.Trim();
    public int DailyLimit => int.TryParse(DailyLimitTextBox.Text, out var limit) ? limit : 50;

    public AddModelDialog()
    {
        InitializeComponent();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ModelName))
        {
            MessageBox.Show("Please enter a display name for the model.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            ModelNameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(ModelId))
        {
            MessageBox.Show("Please enter a model ID.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            ModelIdTextBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
