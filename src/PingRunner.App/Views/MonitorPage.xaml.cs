using PingRunner.App.ViewModels;

namespace PingRunner.App.Views;

public partial class MonitorPage
{
    public MonitorPage(MonitorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
