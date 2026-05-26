using PrayingTime.Models;

namespace PrayingTime.Core;

public static class PrayerCalculator
{
    private const double IhtiyatMinutes = 2.0;

    public static PrayerTimes Calculate(DateOnly date, AppSettings settings)
    {
        double jd = SunPosition.ComputeJulianDay(date);
        var (dec, eqt) = SunPosition.ComputeSunParameters(jd);
        double transit = SunPosition.ComputeTransit(eqt, settings.Longitude, settings.TimezoneOffset);

        double lat = settings.Latitude;

        // Asr altitude: arctan(1 / (1 + tan(|lat - dec|))) — Shafi'i/Standard
        double asrAngle = SunPosition.ToDeg(Math.Atan(1.0 / (1.0 + Math.Tan(Math.Abs(SunPosition.ToRad(lat - dec))))));

        double fajrHA    = SunPosition.ComputeHourAngle(-20.0, lat, dec);
        double sunriseHA = SunPosition.ComputeHourAngle(-1.0, lat, dec);
        double asrHA     = SunPosition.ComputeHourAngle(asrAngle, lat, dec);
        double maghribHA = SunPosition.ComputeHourAngle(-1.0, lat, dec);
        double ishaHA    = SunPosition.ComputeHourAngle(-18.0, lat, dec);

        DateTime base0 = date.ToDateTime(TimeOnly.MinValue);

        DateTime ToLocal(double decimalHours, bool addIhtiyat = true)
        {
            double minutes = decimalHours * 60.0 + (addIhtiyat ? IhtiyatMinutes : 0);
            return base0.AddMinutes(minutes);
        }

        DateTime fajr    = ToLocal(transit - fajrHA,    addIhtiyat: true);
        DateTime sunrise = ToLocal(transit - sunriseHA,  addIhtiyat: false);
        DateTime dhuhr   = ToLocal(transit,              addIhtiyat: true);
        DateTime asr     = ToLocal(transit + asrHA,      addIhtiyat: true);
        DateTime maghrib = ToLocal(transit + maghribHA,  addIhtiyat: true);
        DateTime isha    = ToLocal(transit + ishaHA,     addIhtiyat: true);

        return new PrayerTimes(fajr, sunrise, dhuhr, asr, maghrib, isha);
    }
}
