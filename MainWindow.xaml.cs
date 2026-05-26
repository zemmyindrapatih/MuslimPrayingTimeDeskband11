using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using PrayingTime.Core;
using PrayingTime.Models;
using PrayingTime.Services;

namespace PrayingTime;

public partial class MainWindow : Window
{
    private AppSettings _settings = new();
    private PrayerTimes? _todayTimes;
    private PrayerTimes? _tomorrowTimes;
    private DateOnly _lastCalculated;
    private readonly DispatcherTimer _timer;
    private bool _isPulsing;
    private bool _recoveryPending;

    // Dock / drag state
    private bool _isDocked;
    private bool _isLocked;
    private bool _isDragging;
    private int _dragStartScreenX, _dragStartScreenY;
    private double _windowLeftOnDown, _windowTopOnDown;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const uint SWP_NOZORDER = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    private static readonly (string Name, string Arabic, Func<PrayerTimes, DateTime> Getter)[] PrayerDefs =
    [
        ("Subuh",   "الفجر",    t => t.Fajr),
        ("Syuruq",  "الشروق",   t => t.Sunrise),
        ("Zuhur",   "الظهر",    t => t.Dhuhr),
        ("Ashar",   "العصر",    t => t.Asr),
        ("Maghrib", "المغرب",   t => t.Maghrib),
        ("Isya",    "العشاء",   t => t.Isha),
    ];

    public MainWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var src = System.Windows.Interop.HwndSource.FromHwnd(
            new System.Windows.Interop.WindowInteropHelper(this).Handle);
        src?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING && _isDocked)
        {
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            if ((pos.flags & SWP_NOZORDER) == 0 && pos.hwndInsertAfter != HWND_TOPMOST && !_recoveryPending)
            {
                pos.flags |= SWP_NOZORDER;
                Marshal.StructureToPtr(pos, lParam, false);
                _recoveryPending = true;
                Logger.Info("WM_WINDOWPOSCHANGING: blocked z-order demotion");
                Dispatcher.BeginInvoke(() => _recoveryPending = false,
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        return IntPtr.Zero;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Info($"App started. Log: {System.IO.Path.Combine(AppContext.BaseDirectory, "praying-time.log")}");
        _settings = SettingsService.Load();
        Logger.Info($"Settings loaded: city={_settings.CityName}, lat={_settings.Latitude}, lon={_settings.Longitude}, tz=+{_settings.TimezoneOffset}");
        RecalculateTimes();
        SetDockedState();
        _timer.Start();
        UpdateDisplay();
        Deactivated += Window_Deactivated;
    }

    private void RecalculateTimes()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        _todayTimes = PrayerCalculator.Calculate(today, _settings);
        _tomorrowTimes = PrayerCalculator.Calculate(today.AddDays(1), _settings);
        _lastCalculated = today;
        if (_todayTimes != null)
            Logger.Info($"Prayer times ({today}): Fajr={_todayTimes.Fajr:HH:mm} Sunrise={_todayTimes.Sunrise:HH:mm} " +
                        $"Dhuhr={_todayTimes.Dhuhr:HH:mm} Asr={_todayTimes.Asr:HH:mm} " +
                        $"Maghrib={_todayTimes.Maghrib:HH:mm} Isha={_todayTimes.Isha:HH:mm}");
    }

    private void SetDockedState()
    {
        _isDocked = true;
        MainBorder.Background = new SolidColorBrush(Color.FromArgb(0xBB, 0x0D, 0x11, 0x17));
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
        MainBorder.BorderThickness = new Thickness(1);
        MainBorder.CornerRadius = new CornerRadius(6);
        MainBorder.Effect = null;
        TaskbarPositioner.PositionBesideSysTray(this);
        Logger.Info($"State: docked — Left={Left:F0} Top={Top:F0} Height={Height:F0}");
    }

    private void SetFloatingState()
    {
        _isDocked = false;
        MainBorder.Background = (Brush)Application.Current.Resources["BgBrush"];
        MainBorder.BorderThickness = new Thickness(1);
        MainBorder.CornerRadius = new CornerRadius(12);
        MainBorder.Effect = (Effect)Resources["FloatingGlow"];
        Height = 50;
        Logger.Info($"State: floating — Left={Left:F0} Top={Top:F0}");
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today != _lastCalculated) RecalculateTimes();
        UpdateDisplay();
        if (_isDocked) TaskbarPositioner.PositionBesideSysTray(this);
    }

    private void UpdateDisplay()
    {
        if (_todayTimes == null || _tomorrowTimes == null) return;

        var now = DateTime.Now;
        var (nextName, nextArabic, nextTime, prevTime) = FindNextPrayer(now);

        TimeSpan countdown = nextTime - now;
        if (countdown < TimeSpan.Zero) countdown = TimeSpan.Zero;

        PrayerNameText.Text = nextName;
        ArabicNameText.Text = nextArabic;
        CountdownText.Text  = FormatCountdown(countdown);

        // Progress bar: fraction of elapsed time in current interval
        double totalSeconds = (nextTime - prevTime).TotalSeconds;
        double elapsedSeconds = (now - prevTime).TotalSeconds;
        double fraction = totalSeconds > 0 ? Math.Clamp(elapsedSeconds / totalSeconds, 0, 1) : 0;

        var progressParent = (System.Windows.Controls.Grid)ProgressBar.Parent;
        double maxWidth = progressParent.ActualWidth > 0 ? progressParent.ActualWidth : 60;
        ProgressBar.Width = Math.Max(4, maxWidth * fraction);

        // Pulse animation when under 60 seconds
        if (countdown.TotalSeconds <= 60 && !_isPulsing)
        {
            _isPulsing = true;
            try { ((Storyboard)Resources["PulseStoryboard"]).Begin(this); }
            catch (Exception ex) { Logger.Error("PulseStoryboard.Begin failed", ex); }
        }
        else if (countdown.TotalSeconds > 60 && _isPulsing)
        {
            _isPulsing = false;
            try
            {
                ((Storyboard)Resources["PulseStoryboard"]).Stop(this);
                CountdownText.Opacity = 1;
            }
            catch (Exception ex) { Logger.Error("PulseStoryboard.Stop failed", ex); }
        }
    }

    private (string Name, string Arabic, DateTime Next, DateTime Prev) FindNextPrayer(DateTime now)
    {
        var times = _todayTimes!;
        var tomorrow = _tomorrowTimes!;

        var sequence = new (string Name, string Arabic, DateTime Time)[]
        {
            (PrayerDefs[0].Name, PrayerDefs[0].Arabic, times.Fajr),
            (PrayerDefs[1].Name, PrayerDefs[1].Arabic, times.Sunrise),
            (PrayerDefs[2].Name, PrayerDefs[2].Arabic, times.Dhuhr),
            (PrayerDefs[3].Name, PrayerDefs[3].Arabic, times.Asr),
            (PrayerDefs[4].Name, PrayerDefs[4].Arabic, times.Maghrib),
            (PrayerDefs[5].Name, PrayerDefs[5].Arabic, times.Isha),
            (PrayerDefs[0].Name, PrayerDefs[0].Arabic, tomorrow.Fajr),
        };

        for (int i = 0; i < sequence.Length; i++)
        {
            if (now < sequence[i].Time)
            {
                DateTime prevTime = i == 0
                    ? sequence[i].Time.AddHours(-12)
                    : sequence[i - 1].Time;
                return (sequence[i].Name, sequence[i].Arabic, sequence[i].Time, prevTime);
            }
        }

        return (sequence[^1].Name, sequence[^1].Arabic, sequence[^1].Time, sequence[^2].Time);
    }

    private static string FormatCountdown(TimeSpan ts)
        => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}";

    // ── Drag handlers ───────────────────────────────────────────────────────

    private void OuterGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isLocked) { Logger.Info("MouseDown blocked: locked"); return; }
        if (!GetCursorPos(out POINT p)) return;
        Logger.Info($"DragStart: isDocked={_isDocked}, cursor=({p.X},{p.Y}), win=({Left:F0},{Top:F0})");
        _dragStartScreenX = p.X;
        _dragStartScreenY = p.Y;
        _windowLeftOnDown = Left;
        _windowTopOnDown  = Top;
        OuterGrid.CaptureMouse();
        _isDragging = true;
        e.Handled = true;
    }

    private void OuterGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            if (_isDragging) StopDrag();
            return;
        }
        if (!GetCursorPos(out POINT p)) return;

        int dx = p.X - _dragStartScreenX;
        int dy = p.Y - _dragStartScreenY;
        double dpi = TaskbarPositioner.GetDpiScale(this);

        if (_isDocked)
        {
            double dist = Math.Sqrt(dx * dx + dy * dy);
            Logger.Info($"DockDrag: dist={dist:F0}px, dx={dx}, dy={dy}");
            if (dist < 40) return;
            Logger.Info("Escaping dock → floating");
            SetFloatingState();
        }

        double newLeft = _windowLeftOnDown + dx / dpi;
        double newTop  = _windowTopOnDown  + dy / dpi;

        var taskbarInfo = TaskbarPositioner.GetTaskbarInfo(this);
        if (taskbarInfo.HasValue && newTop >= taskbarInfo.Value.Top - 10)
        {
            Logger.Info($"SnapBack: newTop={newTop:F1} taskbarTop={taskbarInfo.Value.Top:F1}");
            StopDrag();
            SetDockedState();
            return;
        }

        Left = newLeft;
        Top  = newTop;
    }

    private void OuterGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging) StopDrag();
    }

    private void StopDrag()
    {
        _isDragging = false;
        OuterGrid.ReleaseMouseCapture();
    }

    // ── Menu handlers ───────────────────────────────────────────────────────

    public void ApplyNewSettings(AppSettings settings)
    {
        _settings = settings;
        Logger.Info($"Settings updated: city={settings.CityName}, lat={settings.Latitude}, lon={settings.Longitude}, tz=+{settings.TimezoneOffset}");
        RecalculateTimes();
        UpdateDisplay();
        try { ((Storyboard)Resources["FlashStoryboard"]).Begin(this); }
        catch (Exception ex) { Logger.Error("FlashStoryboard.Begin failed", ex); }
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var cm = OuterGrid.ContextMenu;
        cm.PlacementTarget = OuterGrid;
        cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        cm.IsOpen = true;
        e.Handled = true;
    }

    private void MenuLocation_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.ResultSettings != null)
            ApplyNewSettings(dlg.ResultSettings);
    }

    private void MenuRefresh_Click(object sender, RoutedEventArgs e)
    {
        RecalculateTimes();
        UpdateDisplay();
        try { ((Storyboard)Resources["FlashStoryboard"]).Begin(this); }
        catch (Exception ex) { Logger.Error("FlashStoryboard.Begin failed", ex); }
    }

    private void MenuDock_Click(object sender, RoutedEventArgs e)
    {
        SetDockedState();
        Logger.Info("Manual dock to SysTray via menu");
    }

    private void MenuLock_Click(object sender, RoutedEventArgs e)
    {
        _isLocked = !_isLocked;
        MenuLock.Header = _isLocked ? "🔓 Buka Kunci" : "🔒 Kunci Posisi";
        Logger.Info($"Movement lock: {_isLocked}");
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        Logger.Info($"Deactivated: isDocked={_isDocked}");
        if (!_isDocked) return;
        Dispatcher.BeginInvoke(() =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            bool ok = SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            Logger.Info($"SetWindowPos(TOPMOST) after deactivate: ok={ok}, hwnd={hwnd}");
        }, System.Windows.Threading.DispatcherPriority.Normal);
    }
}
