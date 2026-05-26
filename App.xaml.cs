using System.Threading;
using System.Windows;

namespace PrayingTime;

public partial class App : Application
{
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "PrayingTime_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("PrayingTime sudah berjalan.", "PrayingTime", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
