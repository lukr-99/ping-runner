using PingRunner.App.ViewModels;

namespace PingRunner.App.Views;

public partial class SpeedTestPage
{
    public SpeedTestPage(SpeedTestViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
