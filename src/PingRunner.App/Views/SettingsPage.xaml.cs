using PingRunner.App.ViewModels;

namespace PingRunner.App.Views;

public partial class SettingsPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.RefreshHistoryAsync();
    }
}
