using PingRunner.App.ViewModels;

namespace PingRunner.App.Views;

public partial class ConnectionPage
{
    public ConnectionPage(ConnectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.EnsureLoadedAsync();
    }
}
