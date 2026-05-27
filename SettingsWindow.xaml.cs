using System.Windows;
using PrayingTime.Models;
using PrayingTime.Services;

namespace PrayingTime;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _current;
    public AppSettings? ResultSettings { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        _current = current;
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        CityBox.Text = _current.CityName;
        LatBox.Text  = _current.Latitude.ToString("F4");
        LonBox.Text  = _current.Longitude.ToString("F4");

        WibRadio.IsChecked  = _current.TimezoneOffset == 7;
        WitaRadio.IsChecked = _current.TimezoneOffset == 8;
        WitRadio.IsChecked  = _current.TimezoneOffset == 9;

        if (!WibRadio.IsChecked == true && !WitaRadio.IsChecked == true && !WitRadio.IsChecked == true)
            WibRadio.IsChecked = true;
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        ErrorLabel.Visibility = System.Windows.Visibility.Collapsed;

        if (!double.TryParse(LatBox.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double lat) || lat < -90 || lat > 90)
        {
            ShowError("Invalid latitude. Enter a number between -90 and 90 (e.g. -6.2088)");
            return;
        }

        if (!double.TryParse(LonBox.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double lon) || lon < -180 || lon > 180)
        {
            ShowError("Invalid longitude. Enter a number between -180 and 180 (e.g. 106.8456)");
            return;
        }

        int tz = WitaRadio.IsChecked == true ? 8
               : WitRadio.IsChecked  == true ? 9
               : 7;

        var settings = new AppSettings
        {
            Latitude       = lat,
            Longitude      = lon,
            TimezoneOffset = tz,
            CityName       = string.IsNullOrWhiteSpace(CityBox.Text) ? "My City" : CityBox.Text.Trim()
        };

        SettingsService.Save(settings);
        ResultSettings = settings;
        DialogResult = true;
    }

    private void BrowseMapBtn_Click(object sender, RoutedEventArgs e)
    {
        double.TryParse(LatBox.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double lat);
        double.TryParse(LonBox.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double lon);

        var picker = new MapPickerWindow(lat == 0 ? _current.Latitude : lat,
                                         lon == 0 ? _current.Longitude : lon) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            LatBox.Text = picker.ResultLat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            LonBox.Text = picker.ResultLon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.Visibility = System.Windows.Visibility.Visible;
    }
}
