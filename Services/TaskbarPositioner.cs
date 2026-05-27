using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PrayingTime.Services;

public static class TaskbarPositioner
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public static void PositionAboveTaskbar(Window window)
    {
        double dpiScale = GetDpiScale(window);

        double widgetW = window.ActualWidth;
        double widgetH = window.Height;

        IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        double taskbarTop;
        if (taskbarHwnd != IntPtr.Zero && GetWindowRect(taskbarHwnd, out RECT tr))
        {
            taskbarTop = tr.Top / dpiScale;
        }
        else
        {
            taskbarTop = screenH - 48; // fallback: assume 48px taskbar
        }

        window.Left = (screenW - widgetW) / 2.0;
        window.Top = taskbarTop - widgetH - 6;
    }

    public static void PositionBesideSysTray(Window window)
    {
        double dpiScale = GetDpiScale(window);

        IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
        if (taskbarHwnd == IntPtr.Zero)
        {
            PositionAboveTaskbar(window);
            return;
        }

        if (!GetWindowRect(taskbarHwnd, out RECT taskbarRect))
        {
            PositionAboveTaskbar(window);
            return;
        }

        IntPtr trayHwnd = FindWindowEx(taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
        if (trayHwnd == IntPtr.Zero)
        {
            PositionAboveTaskbar(window);
            return;
        }

        if (!GetWindowRect(trayHwnd, out RECT trayRect))
        {
            PositionAboveTaskbar(window);
            return;
        }

        double taskbarTop = taskbarRect.Top / dpiScale;
        double taskbarHeight = (taskbarRect.Bottom - taskbarRect.Top) / dpiScale;
        double trayLeft = trayRect.Left / dpiScale;

        window.Top = taskbarTop;
        window.Height = taskbarHeight;
        window.Left = trayLeft - window.ActualWidth - 2;
    }

    public static void PositionBesideSysTrayAtLeft(Window window, double left)
    {
        double dpiScale = GetDpiScale(window);
        IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
        if (taskbarHwnd == IntPtr.Zero || !GetWindowRect(taskbarHwnd, out RECT taskbarRect)) return;
        window.Top    = taskbarRect.Top    / dpiScale;
        window.Height = (taskbarRect.Bottom - taskbarRect.Top) / dpiScale;
        window.Left   = left;
    }

    public static (double Top, double Height)? GetTaskbarInfo(Window window)
    {
        double dpi = GetDpiScale(window);
        IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
        if (taskbarHwnd == IntPtr.Zero) return null;
        if (!GetWindowRect(taskbarHwnd, out RECT tr)) return null;
        return (tr.Top / dpi, (tr.Bottom - tr.Top) / dpi);
    }

    public static double GetDpiScale(Window window)
    {
        try
        {
            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget != null)
                return source.CompositionTarget.TransformToDevice.M11;
        }
        catch { }
        return 1.0;
    }
}
