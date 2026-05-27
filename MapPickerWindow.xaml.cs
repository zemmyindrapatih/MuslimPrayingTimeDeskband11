using System.Windows;
using System.Windows.Input;

namespace PrayingTime;

public partial class MapPickerWindow : Window
{
    public double ResultLat { get; private set; }
    public double ResultLon { get; private set; }

    private readonly double _initLat;
    private readonly double _initLon;

    public MapPickerWindow(double lat, double lon)
    {
        _initLat = lat;
        _initLon = lon;
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await MapView.EnsureCoreWebView2Async(null);
        MapView.WebMessageReceived += (s, args) =>
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(args.WebMessageAsJson);
                ResultLat = json.RootElement.GetProperty("lat").GetDouble();
                ResultLon = json.RootElement.GetProperty("lng").GetDouble();
                DialogResult = true;
            }
            catch { }
        };
        string latStr = _initLat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string lonStr = _initLon.ToString(System.Globalization.CultureInfo.InvariantCulture);
        MapView.NavigateToString(MapHtml.Replace("__LAT__", latStr).Replace("__LON__", lonStr));
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private const string MapHtml = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8"/>
            <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
            <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
            <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body { background: #0D1117; }
                #map { width: 100vw; height: 100vh; }
                .leaflet-control-attribution { font-size: 9px; }
            </style>
        </head>
        <body>
            <div id="map"></div>
            <script>
                var initLat = __LAT__;
                var initLon = __LON__;
                var map = L.map('map').setView([initLat, initLon], 10);
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    attribution: '&copy; OpenStreetMap contributors',
                    maxZoom: 19
                }).addTo(map);
                var marker = L.marker([initLat, initLon], { draggable: true }).addTo(map);
                function pick(latlng) {
                    marker.setLatLng(latlng);
                    window.chrome.webview.postMessage(JSON.stringify({ lat: latlng.lat, lng: latlng.lng }));
                }
                marker.on('dragend', function(e) { pick(marker.getLatLng()); });
                map.on('click', function(e) { pick(e.latlng); });
            </script>
        </body>
        </html>
        """;
}
