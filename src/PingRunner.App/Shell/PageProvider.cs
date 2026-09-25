using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions;

namespace PingRunner.App.Shell;

/// <summary>
/// Hands NavigationView the pages built from the composition root instead of letting it create them,
/// and keeps each one so a page's state (a zoomed graph, a finished speed test) survives switching
/// away. Pages lay themselves out to the window, so none is wrapped in an extra scroll viewer.
/// </summary>
public sealed class PageProvider(IReadOnlyDictionary<Type, Func<object>> factories) : INavigationViewPageProvider
{
    private readonly Dictionary<Type, object> created = [];

    public object? GetPage(Type pageType)
    {
        if (created.TryGetValue(pageType, out var page))
        {
            return page;
        }

        if (!factories.TryGetValue(pageType, out var factory))
        {
            return null;
        }

        page = factory();
        if (page is DependencyObject element)
        {
            ScrollViewer.SetCanContentScroll(element, false);
        }

        created[pageType] = page;
        return page;
    }
}
