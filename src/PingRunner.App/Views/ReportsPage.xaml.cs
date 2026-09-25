using PingRunner.App.ViewModels;

namespace PingRunner.App.Views;

public partial class ReportsPage
{
    public ReportsPage(ReportsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.EnsureLoadedAsync();
    }
}
