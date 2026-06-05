using System.Windows;
using System.Threading;

namespace LyricTaskbarOverlay;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex = null;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool createdNew;
        _mutex = new Mutex(true, "LyricTaskbarOverlay_SingleInstanceMutex", out createdNew);
        if (!createdNew)
        {
            System.Environment.Exit(0);
            return;
        }
        base.OnStartup(e);
    }
}
