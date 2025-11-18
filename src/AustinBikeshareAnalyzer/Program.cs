using Google.Cloud.BigQuery.V2;
using AustinBikeshareAnalyzer;

Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
Console.WriteLine("   Austin Bikeshare - Analiza Szybkości Przejazdów (Geography API)");
Console.WriteLine("═══════════════════════════════════════════════════════════════════════\n");

try
{
    // ═══════════════════════════════════════════════════════════
    // KROK 1: Inicjalizacja klienta BigQuery
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🔗 Łączenie z BigQuery...");
    Console.WriteLine("📊 Dataset: bigquery-public-data.austin_bikeshare");
    Console.WriteLine("   • bikeshare_trips - dane przejazdów");
    Console.WriteLine("   • bikeshare_stations - lokalizacje stacji");
    Console.WriteLine();

    var client = BigQueryClient.Create("bigquery-public-data");
    var service = new BikeshareService(client);

    // ═══════════════════════════════════════════════════════════
    // KROK 2: Pobieranie Top 20 najszybszych przejazdów
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🚴 Pobieranie Top 20 najszybszych przejazdów rowerowych...");
    Console.WriteLine("   Filtr: Dystans > 1 km, Rok: 2023");
    Console.WriteLine("   Używam funkcji geograficznych BigQuery:");
    Console.WriteLine("   • ST_GEOGPOINT(lon, lat) - tworzenie punktów geograficznych");
    Console.WriteLine("   • ST_DISTANCE(p1, p2) - dystans geodezyjny w metrach");
    Console.WriteLine();
    Console.WriteLine("⏳ Wykonywanie zapytania...");
    Console.WriteLine("   (To może potrwać ~10-20 sekund)\n");

    var trips = await service.GetFastestTrips();

    if (trips.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  Nie znaleziono żadnych przejazdów spełniających kryteria.");
        Console.ResetColor();
        return 1;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✅ Pobrano {trips.Count} przejazdów.\n");
    Console.ResetColor();

    // ═══════════════════════════════════════════════════════════
    // KROK 3: Wyświetlenie tabeli z wynikami
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("📊 TOP 20 NAJSZYBSZYCH PRZEJAZDÓW ROWEROWYCH - AUSTIN 2023");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
    Console.WriteLine();

    // Nagłówek tabeli
    Console.WriteLine("┌─────┬────────────────────────────┬────────────────────────────┬──────────┬──────────┬──────────┐");
    Console.WriteLine("│ Nr  │ Stacja startowa            │ Stacja końcowa             │ Dystans  │ Czas     │ Prędkość │");
    Console.WriteLine("├─────┼────────────────────────────┼────────────────────────────┼──────────┼──────────┼──────────┤");

    // Wiersze z danymi
    for (int i = 0; i < trips.Count; i++)
    {
        var trip = trips[i];
        var rank = i + 1;

        // Skrócenie nazw stacji dla lepszego formatowania
        var startName = TruncateString(trip.StartStation, 26);
        var endName = TruncateString(trip.EndStation, 26);

        // Kolorowanie wierszy z anomaliami
        if (trip.IsAnomalousSpeed)
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }

        Console.WriteLine(
            $"│ {rank,3} │ {startName,-26} │ {endName,-26} │ {trip.DistanceKm,6:F2} km │ {trip.DurationMin,6:F1} min │ {trip.SpeedKmh,6:F1} km/h │"
        );

        if (trip.IsAnomalousSpeed)
        {
            Console.ResetColor();
        }
    }

    // Stopka tabeli
    Console.WriteLine("└─────┴────────────────────────────┴────────────────────────────┴──────────┴──────────┴──────────┘");
    Console.WriteLine();

    // ═══════════════════════════════════════════════════════════
    // KROK 4: Analiza anomalii (absurdalnych prędkości)
    // ═══════════════════════════════════════════════════════════

    var anomalies = trips.Where(t => t.IsAnomalousSpeed).ToList();

    if (anomalies.Any())
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  WYKRYTO ANOMALIE - PODEJRZANE PRĘDKOŚCI");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"Znaleziono {anomalies.Count} przejazdów z prędkością > 50 km/h.");
        Console.WriteLine("To prawdopodobnie błędy danych, ponieważ rower rzadko osiąga taką prędkość.\n");

        Console.WriteLine("Możliwe przyczyny:");
        Console.WriteLine("  1. 🚗 Rower przewożony samochodem/autobusem");
        Console.WriteLine("  2. ⏱️  Błędnie zarejestrowany czas (np. zgubiony sygnał GPS)");
        Console.WriteLine("  3. 📍 Niepoprawne lokalizacje stacji w bazie danych");
        Console.WriteLine("  4. 🔄 Rower zwrócony do innej stacji bez rejestracji końca podróży");
        Console.WriteLine();

        Console.WriteLine("Szczegóły anomalii:");
        Console.WriteLine("┌─────┬──────────┬──────────┬──────────────────────────────┐");
        Console.WriteLine("│ Nr  │ Prędkość │ Dystans  │ Interpretacja                │");
        Console.WriteLine("├─────┼──────────┼──────────┼──────────────────────────────┤");

        for (int i = 0; i < anomalies.Count; i++)
        {
            var trip = anomalies[i];
            var rank = i + 1;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"│ {rank,3} │ {trip.SpeedKmh,6:F1} km/h │ {trip.DistanceKm,6:F2} km │ ");
            Console.ResetColor();
            Console.WriteLine($"{trip.SpeedCategory,-28} │");
        }

        Console.WriteLine("└─────┴──────────┴──────────┴──────────────────────────────┘");
        Console.WriteLine();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✅ Brak wykrytych anomalii - wszystkie prędkości wydają się realistyczne.");
        Console.ResetColor();
        Console.WriteLine();
    }

    // ═══════════════════════════════════════════════════════════
    // KROK 5: Statystyki ogólne
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("📈 STATYSTYKI OGÓLNE:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    var avgSpeed = trips.Average(t => t.SpeedKmh);
    var maxSpeed = trips.Max(t => t.SpeedKmh);
    var minSpeed = trips.Min(t => t.SpeedKmh);
    var avgDistance = trips.Average(t => t.DistanceKm);
    var avgDuration = trips.Average(t => t.DurationMin);

    Console.WriteLine($"   Prędkość:");
    Console.WriteLine($"   • Średnia:  {avgSpeed:F2} km/h");
    Console.WriteLine($"   • Minimum:  {minSpeed:F2} km/h");
    Console.WriteLine($"   • Maximum:  {maxSpeed:F2} km/h");
    Console.WriteLine();
    Console.WriteLine($"   Dystans:");
    Console.WriteLine($"   • Średnia:  {avgDistance:F2} km");
    Console.WriteLine();
    Console.WriteLine($"   Czas trwania:");
    Console.WriteLine($"   • Średnia:  {avgDuration:F2} minut");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // ═══════════════════════════════════════════════════════════
    // KROK 6: Kategorie prędkości
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🏷️  ROZKŁAD KATEGORII PRĘDKOŚCI:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    var categories = trips
        .GroupBy(t => t.SpeedCategory)
        .Select(g => new { Category = g.Key, Count = g.Count() })
        .OrderByDescending(x => x.Count);

    foreach (var cat in categories)
    {
        var percentage = (cat.Count / (double)trips.Count) * 100;
        var bar = new string('█', (int)(percentage / 5)); // Skala 1:5

        if (cat.Category.Contains("NIEPRAWDOPODOBNA"))
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }
        else if (cat.Category.Contains("Bardzo szybka"))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
        }

        Console.WriteLine($"   {cat.Category,-35} │ {cat.Count,2} │ {percentage,5:F1}% {bar}");
        Console.ResetColor();
    }

    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // ═══════════════════════════════════════════════════════════
    // KROK 7: Najciekawsze przejazdy
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🎯 NAJCIEKAWSZE PRZEJAZDY:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    var fastest = trips.First();
    Console.WriteLine($"   🥇 Najszybszy przejazd:");
    Console.WriteLine($"      {fastest.StartStation} → {fastest.EndStation}");
    Console.WriteLine($"      Prędkość: {fastest.SpeedKmh:F2} km/h");
    Console.WriteLine($"      Dystans: {fastest.DistanceKm:F2} km w {fastest.DurationMin:F1} minut");
    Console.WriteLine();

    var longest = trips.OrderByDescending(t => t.DistanceKm).First();
    Console.WriteLine($"   📏 Najdłuższy dystans:");
    Console.WriteLine($"      {longest.StartStation} → {longest.EndStation}");
    Console.WriteLine($"      Dystans: {longest.DistanceKm:F2} km");
    Console.WriteLine($"      Prędkość: {longest.SpeedKmh:F2} km/h w {longest.DurationMin:F1} minut");
    Console.WriteLine();

    var shortest = trips.OrderBy(t => t.DurationMin).First();
    Console.WriteLine($"   ⏱️  Najkrótszy czas:");
    Console.WriteLine($"      {shortest.StartStation} → {shortest.EndStation}");
    Console.WriteLine($"      Czas: {shortest.DurationMin:F1} minut");
    Console.WriteLine($"      Dystans: {shortest.DistanceKm:F2} km, Prędkość: {shortest.SpeedKmh:F2} km/h");

    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✅ Analiza zakończona pomyślnie!");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n❌ BŁĄD: {ex.Message}");
    Console.ResetColor();

    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Szczegóły: {ex.InnerException.Message}");
    }

    Console.WriteLine("\n💡 Upewnij się, że:");
    Console.WriteLine("   1. Masz skonfigurowane Google Cloud credentials (GOOGLE_APPLICATION_CREDENTIALS)");
    Console.WriteLine("   2. Projekt ma dostęp do BigQuery API");
    Console.WriteLine("   3. Połączenie z Internetem działa poprawnie");

    return 1;
}

return 0;

/// <summary>
/// Skraca string do określonej długości, dodając "..." jeśli jest dłuższy.
/// </summary>
static string TruncateString(string str, int maxLength)
{
    if (string.IsNullOrEmpty(str))
        return string.Empty;

    if (str.Length <= maxLength)
        return str;

    return str.Substring(0, maxLength - 3) + "...";
}
