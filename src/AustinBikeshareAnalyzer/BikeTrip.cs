namespace AustinBikeshareAnalyzer;

/// <summary>
/// Reprezentuje pojedynczą podróż rowerem w Austin Bikeshare.
/// Zawiera informacje o stacjach, dystansie, prędkości i czasie trwania.
/// </summary>
/// <param name="StartStation">Nazwa stacji startowej</param>
/// <param name="EndStation">Nazwa stacji końcowej</param>
/// <param name="DistanceMeters">Dystans w linii prostej między stacjami w metrach (obliczony przez ST_DISTANCE)</param>
/// <param name="SpeedKmh">Średnia prędkość w km/h (obliczona z dystansu i czasu trwania)</param>
/// <param name="DurationMin">Czas trwania podróży w minutach</param>
public record BikeTrip(
    string StartStation,
    string EndStation,
    double DistanceMeters,
    double SpeedKmh,
    double DurationMin
)
{
    /// <summary>
    /// Zwraca dystans w kilometrach (dla czytelności).
    /// </summary>
    public double DistanceKm => DistanceMeters / 1000.0;

    /// <summary>
    /// Sprawdza, czy prędkość jest podejrzana (prawdopodobnie błąd danych).
    /// Rower normalnie nie przekracza 30-40 km/h średniej prędkości.
    /// </summary>
    public bool IsAnomalousSpeed => SpeedKmh > 50.0;

    /// <summary>
    /// Zwraca kategorię prędkości dla interpretacji.
    /// </summary>
    public string SpeedCategory => SpeedKmh switch
    {
        < 10 => "Wolna (spacer)",
        < 20 => "Normalna (rekreacja)",
        < 30 => "Szybka (sport)",
        < 50 => "Bardzo szybka (wyścig)",
        _ => "⚠️ NIEPRAWDOPODOBNA - błąd danych!"
    };
}
