using Google.Cloud.BigQuery.V2;
using ChicagoTaxiAnalyzer;

Console.WriteLine("Chicago Taxi Analyzer - Analiza najdroższych przejazdów 2023");
Console.WriteLine("=================================================================\n");

try
{
    // Inicjalizacja klienta BigQuery
    // Wymaga: GOOGLE_APPLICATION_CREDENTIALS w zmiennych środowiskowych
    // lub domyślnych credentials z Google Cloud SDK
    var client = BigQueryClient.Create("bigquery-public-data");

    Console.WriteLine("Łączenie z BigQuery...");
    Console.WriteLine("Wykonywanie zapytania...\n");

    // Wykonanie zapytania
    var result = await client.ExecuteQueryAsync(
        Queries.Top100ExpensiveNonCashTrips2023,
        parameters: null
    );

    // Mapowanie wyników do obiektów TaxiTrip
    var trips = new List<TaxiTrip>();

    await foreach (var row in result)
    {
        var trip = new TaxiTrip(
            TripStartTimestamp: row["trip_start_timestamp"] as DateTime?,
            TripMiles: row["trip_miles"] as double?,
            Fare: row["fare"] != null ? Convert.ToDecimal(row["fare"]) : null,
            Tips: row["tips"] != null ? Convert.ToDecimal(row["tips"]) : null,
            PaymentType: row["payment_type"] as string
        );

        trips.Add(trip);
    }

    Console.WriteLine($"Pobrano {trips.Count} rekordów.\n");

    // Nagłówek tabeli
    Console.WriteLine("┌─────┬─────────────────────┬─────────────┬───────────┬───────────┬──────────────┬──────────────┐");
    Console.WriteLine("│ Nr  │ Data i czas         │ Mile        │ Fare      │ Tips      │ % napiwku    │ Payment      │");
    Console.WriteLine("├─────┼─────────────────────┼─────────────┼───────────┼───────────┼──────────────┼──────────────┤");

    // Przetwarzanie i wyświetlanie każdego przejazdu
    int index = 1;
    foreach (var trip in trips)
    {
        // Obliczanie procentu napiwku w C# (zgodnie z wymaganiem - logika w C#, nie w SQL)
        decimal? tipPercentage = null;

        if (trip.Fare.HasValue && trip.Tips.HasValue)
        {
            decimal totalAmount = trip.Fare.Value + trip.Tips.Value;

            if (totalAmount > 0)
            {
                tipPercentage = (trip.Tips.Value / totalAmount) * 100;
            }
        }

        // Formatowanie danych do wyświetlenia
        string timestamp = trip.TripStartTimestamp?.ToString("yyyy-MM-dd HH:mm") ?? "N/A";
        string miles = trip.TripMiles?.ToString("F2") ?? "N/A";
        string fare = trip.Fare?.ToString("C2") ?? "N/A";
        string tips = trip.Tips?.ToString("C2") ?? "N/A";
        string tipPct = tipPercentage.HasValue ? $"{tipPercentage.Value:F2}%" : "N/A";
        string payment = trip.PaymentType ?? "N/A";

        // Wyświetlenie wiersza tabeli
        Console.WriteLine(
            $"│ {index,3} │ {timestamp,-19} │ {miles,11} │ {fare,9} │ {tips,9} │ {tipPct,12} │ {payment,-12} │"
        );

        index++;
    }

    // Stopka tabeli
    Console.WriteLine("└─────┴─────────────────────┴─────────────┴───────────┴───────────┴──────────────┴──────────────┘");

    // Statystyki
    Console.WriteLine("\nStatystyki:");
    Console.WriteLine($"  Całkowita liczba przejazdów: {trips.Count}");

    var tripsWithTips = trips.Where(t => t.Tips.HasValue && t.Tips.Value > 0).ToList();
    Console.WriteLine($"  Przejazdów z napiwkiem: {tripsWithTips.Count}");

    if (tripsWithTips.Any())
    {
        var avgFare = trips.Where(t => t.Fare.HasValue).Average(t => t.Fare!.Value);
        var avgTips = tripsWithTips.Average(t => t.Tips!.Value);
        var maxFare = trips.Where(t => t.Fare.HasValue).Max(t => t.Fare!.Value);

        Console.WriteLine($"  Średnia opłata (fare): {avgFare:C2}");
        Console.WriteLine($"  Średni napiwek (tips): {avgTips:C2}");
        Console.WriteLine($"  Maksymalna opłata: {maxFare:C2}");
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nBŁĄD: {ex.Message}");
    Console.ResetColor();

    if (ex.InnerException != null)
    {
        Console.WriteLine($"Szczegóły: {ex.InnerException.Message}");
    }

    Console.WriteLine("\nUpewnij się, że:");
    Console.WriteLine("1. Masz skonfigurowane Google Cloud credentials (GOOGLE_APPLICATION_CREDENTIALS)");
    Console.WriteLine("2. Projekt ma dostęp do BigQuery API");
    Console.WriteLine("3. Połączenie z Internetem działa poprawnie");

    return 1;
}

Console.WriteLine("\nAnaliza zakończona pomyślnie!");
return 0;
