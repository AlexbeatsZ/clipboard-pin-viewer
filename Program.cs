using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ClipboardPinViewer;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args is ["--check-dependencies"])
        {
            try
            {
                WriteDependencyReport();
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new ClipboardPinViewerApp());
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteDependencyReport()
    {
        // Keep the WinRT call outside Main so load/JIT failures reach its catch.
        var windowsSdk = Assembly.Load("Microsoft.Windows.SDK.NET");
        var winRt = Assembly.Load("WinRT.Runtime");
        var historyEnabled = Windows.ApplicationModel.DataTransfer.Clipboard.IsHistoryEnabled();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Status = "ok",
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            WindowsSdk = windowsSdk.GetName().Version?.ToString(),
            WinRt = winRt.GetName().Version?.ToString(),
            ClipboardHistoryEnabled = historyEnabled
        }));
    }
}
