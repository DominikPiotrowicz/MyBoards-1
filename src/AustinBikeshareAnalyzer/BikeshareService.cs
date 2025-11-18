using Google.Cloud.BigQuery.V2;

namespace AustinBikeshareAnalyzer;

/// <summary>
/// Serwis do analizy danych Austin Bikeshare z BigQuery.
/// Używa funkcji geograficznych BigQuery (ST_GEOGPOINT, ST_DISTANCE).
/// </summary>
public class BikeshareService
{
    private readonly BigQueryClient _client;

    /// <summary>
    /// Zapytanie SQL pobierające Top 20 najszybszych przejazdów rowerowych z Austin w 2023 roku.
    ///
    /// Funkcje geograficzne BigQuery:
    /// - ST_GEOGPOINT(longitude, latitude): Tworzy punkt geograficzny z koordynatów
    /// - ST_DISTANCE(point1, point2): Oblicza dystans w metrach między dwoma punktami (geodezyjnie)
    ///
    /// Logika:
    /// 1. JOIN trips z stations dwa razy (dla start_station i end_station)
    /// 2. Tworzy punkty geograficzne ze współrzędnych (ST_GEOGPOINT)
    /// 3. Oblicza dystans w metrach (ST_DISTANCE)
    /// 4. Oblicza prędkość w km/h: (dystans_km) / (czas_h)
    /// 5. Filtruje: tylko podróże > 1 km z 2023 roku
    /// 6. Sortuje po prędkości malejąco, TOP 20
    ///
    /// UWAGA: Wyniki mogą zawierać absurdalne prędkości (np. 100 km/h) - to błędy danych!
    /// Mogą wynikać z:
    /// - Niepoprawnie zarejestrowanego czasu
    /// - Rowerów przewożonych samochodem/autobusem
    /// - Błędów w systemie lokalizacji stacji
    /// </summary>
    private const string QueryFastestTrips = @"
        WITH trip_distances AS (
            SELECT
                t.trip_id,
                t.start_time,
                t.duration_minutes,
                start_station.name AS start_station_name,
                end_station.name AS end_station_name,

                -- Tworzenie punktów geograficznych z koordynatów stacji
                -- ST_GEOGPOINT(longitude, latitude) - kolejność jest WAŻNA!
                ST_GEOGPOINT(start_station.longitude, start_station.latitude) AS start_point,
                ST_GEOGPOINT(end_station.longitude, end_station.latitude) AS end_point

            FROM
                `bigquery-public-data.austin_bikeshare.bikeshare_trips` t

            -- JOIN ze stacją startową
            INNER JOIN
                `bigquery-public-data.austin_bikeshare.bikeshare_stations` start_station
            ON
                t.start_station_id = start_station.station_id

            -- JOIN ze stacją końcową
            INNER JOIN
                `bigquery-public-data.austin_bikeshare.bikeshare_stations` end_station
            ON
                t.end_station_id = end_station.station_id

            WHERE
                -- Rok 2023
                EXTRACT(YEAR FROM t.start_time) = 2023
                -- Musimy mieć czas trwania
                AND t.duration_minutes > 0
                AND t.duration_minutes IS NOT NULL
                -- Musimy mieć współrzędne dla obu stacji
                AND start_station.latitude IS NOT NULL
                AND start_station.longitude IS NOT NULL
                AND end_station.latitude IS NOT NULL
                AND end_station.longitude IS NOT NULL
        )

        SELECT
            start_station_name,
            end_station_name,

            -- Obliczenie dystansu w metrach (geodezyjnie - po powierzchni Ziemi)
            -- ST_DISTANCE zwraca dystans w metrach
            ST_DISTANCE(start_point, end_point) AS distance_meters,

            duration_minutes,

            -- Obliczenie prędkości w km/h
            -- Wzór: speed = (distance_km) / (time_hours)
            --     = (distance_meters / 1000) / (duration_minutes / 60)
            --     = (distance_meters / 1000) * (60 / duration_minutes)
            --     = distance_meters * 60 / (duration_minutes * 1000)
            (ST_DISTANCE(start_point, end_point) / 1000.0) / (duration_minutes / 60.0) AS speed_kmh

        FROM
            trip_distances

        WHERE
            -- Tylko podróże powyżej 1 km (1000 metrów)
            ST_DISTANCE(start_point, end_point) > 1000

        ORDER BY
            speed_kmh DESC

        LIMIT 20
    ";

    public BikeshareService(BigQueryClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Pobiera Top 20 najszybszych przejazdów rowerowych z Austin w 2023 roku.
    /// Filtruje tylko podróże powyżej 1 km.
    /// </summary>
    /// <returns>Lista najszybszych przejazdów</returns>
    public async Task<List<BikeTrip>> GetFastestTrips()
    {
        var trips = new List<BikeTrip>();

        // Wykonanie zapytania
        var result = await _client.ExecuteQueryAsync(
            QueryFastestTrips,
            parameters: null
        );

        // Mapowanie wyników do obiektów BikeTrip
        await foreach (var row in result)
        {
            var startStation = row["start_station_name"] as string ?? "Unknown";
            var endStation = row["end_station_name"] as string ?? "Unknown";

            var distanceMeters = row["distance_meters"] != null
                ? Convert.ToDouble(row["distance_meters"])
                : 0.0;

            var durationMin = row["duration_minutes"] != null
                ? Convert.ToDouble(row["duration_minutes"])
                : 0.0;

            var speedKmh = row["speed_kmh"] != null
                ? Convert.ToDouble(row["speed_kmh"])
                : 0.0;

            var trip = new BikeTrip(
                StartStation: startStation,
                EndStation: endStation,
                DistanceMeters: distanceMeters,
                SpeedKmh: speedKmh,
                DurationMin: durationMin
            );

            trips.Add(trip);
        }

        return trips;
    }
}
