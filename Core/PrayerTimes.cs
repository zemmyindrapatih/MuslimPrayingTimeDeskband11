namespace PrayingTime.Core;

public record PrayerTimes(
    DateTime Fajr,
    DateTime Sunrise,
    DateTime Dhuhr,
    DateTime Asr,
    DateTime Maghrib,
    DateTime Isha);
