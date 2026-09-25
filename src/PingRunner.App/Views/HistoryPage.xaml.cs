using PingRunner.App.ViewModels;

namespace PingRunner.App.Views;

public partial class HistoryPage
{
    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.EnsureLoadedAsync();
    }
}
