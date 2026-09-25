using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace PingRunner.App.Tests.Hosting;

/// <summary>
/// One STA thread running a dispatcher with Ping Runner's <see cref="App"/> resources loaded (WPF UI
/// and the app's styles), shared by every WPF test: WPF allows only one Application per process.
/// Work runs on it through <see cref="RunAsync"/>; nothing is ever shown on screen.
/// </summary>
public static class WpfHost
{
    private static readonly Lazy<Dispatcher> Instance = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task RunAsync(Func<Task> work) => RunAsync(async () =>
    {
        await work();
        return true;
    });

    public static Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Instance.Value.BeginInvoke(async () =>
        {
            try
            {
                completion.SetResult(await work());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }

    private static Dispatcher Start()
    {
        // Streams in the speed test start on the thread pool; warm threads keep the fake clock honest.
        ThreadPool.GetMinThreads(out var workers, out var io);
        ThreadPool.SetMinThreads(Math.Max(workers, 32), io);

        Dispatcher? dispatcher = null;
        ExceptionDispatchInfo? failure = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                app.InitializeComponent();
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }

            ready.Set();
            if (failure is null)
            {
                Dispatcher.Run();
            }
        })
        {
            IsBackground = true,
            Name = "WPF test host",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        failure?.Throw();
        return dispatcher!;
    }
}
