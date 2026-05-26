namespace PrayingTime.Core;

public static class SunPosition
{
    public static double ComputeJulianDay(DateOnly date)
    {
        int y = date.Year;
        int m = date.Month;
        int d = date.Day;
        if (m <= 2) { y--; m += 12; }
        int a = y / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;
    }

    public static (double Declination, double EquationOfTime) ComputeSunParameters(double jd)
    {
        double T = (jd - 2451545.0) / 36525.0;

        double L0 = 280.46646 + 36000.76983 * T;
        L0 = NormalizeAngle(L0);

        double M = 357.52911 + 35999.05029 * T - 0.0001537 * T * T;
        M = NormalizeAngle(M);
        double Mrad = ToRad(M);

        double C = (1.914602 - 0.004817 * T - 0.000014 * T * T) * Math.Sin(Mrad)
                 + (0.019993 - 0.000101 * T) * Math.Sin(2 * Mrad)
                 + 0.000289 * Math.Sin(3 * Mrad);

        double sunLon = L0 + C;
        double e = 23.439291111 - 0.013004167 * T;
        double eRad = ToRad(e);
        double sunLonRad = ToRad(sunLon);

        double decRad = Math.Asin(Math.Sin(eRad) * Math.Sin(sunLonRad));
        double declination = ToDeg(decRad);

        double y = Math.Tan(eRad / 2);
        y *= y;
        double ecc = 0.016708634 - 0.000042037 * T;
        double L0rad = ToRad(L0);

        double eqtRad = y * Math.Sin(2 * L0rad)
                      - 2 * ecc * Math.Sin(Mrad)
                      + 4 * ecc * y * Math.Sin(Mrad) * Math.Cos(2 * L0rad)
                      - 0.5 * y * y * Math.Sin(4 * L0rad)
                      - 1.25 * ecc * ecc * Math.Sin(2 * Mrad);
        double equationOfTime = ToDeg(eqtRad) * 4.0; // in minutes

        return (declination, equationOfTime);
    }

    // Returns transit time in decimal hours (local time)
    public static double ComputeTransit(double equationOfTime, double longitude, int timezoneOffset)
    {
        return 12.0 + timezoneOffset - longitude / 15.0 - equationOfTime / 60.0;
    }

    // Returns hour angle offset in decimal hours for a given altitude
    // Returns NaN if sun never reaches the altitude (polar case)
    public static double ComputeHourAngle(double altitudeDeg, double latitudeDeg, double declinationDeg)
    {
        double latRad = ToRad(latitudeDeg);
        double decRad = ToRad(declinationDeg);
        double altRad = ToRad(altitudeDeg);

        double cosHA = (Math.Sin(altRad) - Math.Sin(latRad) * Math.Sin(decRad))
                     / (Math.Cos(latRad) * Math.Cos(decRad));

        if (cosHA < -1 || cosHA > 1) return double.NaN;
        return ToDeg(Math.Acos(cosHA)) / 15.0;
    }

    public static double ToRad(double deg) => deg * Math.PI / 180.0;
    public static double ToDeg(double rad) => rad * 180.0 / Math.PI;

    private static double NormalizeAngle(double angle)
    {
        angle %= 360;
        return angle < 0 ? angle + 360 : angle;
    }
}
