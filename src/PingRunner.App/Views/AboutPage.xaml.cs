using PingRunner.App.ViewModels;

namespace PingRunner.App.Views;

public partial class AboutPage
{
    public AboutPage(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Pages are kept alive between visits; always open at the top, never where a button last had focus.
        Loaded += (_, _) => Scroller.ScrollToHome();
    }
}
