using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SpaceLens.Core.Settings;

namespace SpaceLens;

public partial class App : Application
{
    private static Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private CancellationTokenSource? _listenCts;

    private const string MutexName = @"Local\SpaceLens.Desktop.SingleInstance";
    private const string ShowEventName = @"Local\SpaceLens.Desktop.ShowWindow";

    /// <summary>Optional startup folder scan for demos/screenshots (--demo-scan PATH).</summary>
    public static string? DemoScanPath { get; private set; }

    /// <summary>Skip welcome overlay on startup (--skip-welcome).</summary>
    public static bool SkipWelcomeOnStart { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        ParseArgs(e.Args);
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            MessageBox.Show(
                "Something went wrong. A crash log was saved under your local SpaceLens data folder.\n\n" + args.Exception.Message,
                "SpaceLens error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                WriteCrashLog(ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            args.SetObserved();
        };

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // Another instance is running — ask it to restore/focus, then exit.
            try
            {
                using var signal = EventWaitHandle.OpenExisting(ShowEventName);
                signal.Set();
            }
            catch
            {
                // ignore
            }

            Shutdown();
            return;
        }

        try
        {
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        }
        catch
        {
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset);
        }

        _listenCts = new CancellationTokenSource();
        _ = Task.Run(() => ListenForActivation(_listenCts.Token));

        base.OnStartup(e);
    }

    private static void ParseArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--skip-welcome", StringComparison.OrdinalIgnoreCase))
            {
                SkipWelcomeOnStart = true;
                continue;
            }

            if (args[i].Equals("--demo-scan", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                DemoScanPath = args[++i];
                SkipWelcomeOnStart = true;
            }
        }
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var dir = Path.Combine(AppSettings.SettingsDirectory, "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, $"{DateTime.Now:O}\n{ex}");
            // Keep a stable pointer for support
            File.WriteAllText(Path.Combine(dir, "latest-crash.txt"), path + "\n\n" + ex);
        }
        catch
        {
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "spacelens-crash.txt"), ex.ToString());
            }
            catch { /* ignore */ }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _listenCts?.Cancel();
        _showSignal?.Dispose();
        if (_mutex != null)
        {
            try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
            _mutex.Dispose();
        }
        base.OnExit(e);
    }

    private void ListenForActivation(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_showSignal != null && _showSignal.WaitOne(500))
                {
                    Dispatcher.BeginInvoke(ActivateMainWindow, DispatcherPriority.Normal);
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                // keep listening
            }
        }
    }

    private static void ActivateMainWindow()
    {
        if (Current.MainWindow is not Window window)
            return;

        if (!window.IsVisible)
            window.Show();

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();

        FlashWindow(window);
    }

    private static void FlashWindow(Window window)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
            return;

        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = helper.Handle,
            dwFlags = 3, // FLASHW_ALL
            uCount = 2,
            dwTimeout = 0
        };
        FlashWindowEx(ref info);
    }

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }
}
