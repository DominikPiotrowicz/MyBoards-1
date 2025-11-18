namespace ChicagoTaxiAnalyzer;

/// <summary>
/// Zawiera zapytania SQL do BigQuery.
/// </summary>
public static class Queries
{
    /// <summary>
    /// Pobiera 100 najdroższych przejazdów z 2023 roku, które NIE zostały opłacone gotówką.
    /// Sortowanie według fare + tips malejąco.
    /// </summary>
    public const string Top100ExpensiveNonCashTrips2023 = @"
        SELECT
            trip_start_timestamp,
            trip_miles,
            fare,
            tips,
            payment_type
        FROM
            `bigquery-public-data.chicago_taxi_trips.taxi_trips`
        WHERE
            EXTRACT(YEAR FROM trip_start_timestamp) = 2023
            AND payment_type != 'Cash'
            AND payment_type IS NOT NULL
        ORDER BY
            (IFNULL(fare, 0) + IFNULL(tips, 0)) DESC
        LIMIT 100
    ";
}
