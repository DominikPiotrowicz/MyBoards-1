using Google.Cloud.BigQuery.V2;
using StackOverflowAnalyzer;

Console.WriteLine("═══════════════════════════════════════════════════════════════════");
Console.WriteLine("   Stack Overflow - Analiza Szybkości Pomocy (Response Time)");
Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

try
{
    // ═══════════════════════════════════════════════════════════
    // KROK 1: Inicjalizacja klienta BigQuery
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🔗 Łączenie z BigQuery...");
    Console.WriteLine("📊 Dataset: bigquery-public-data.stackoverflow");
    Console.WriteLine();

    var client = BigQueryClient.Create("bigquery-public-data");
    var service = new StackOverflowService(client);

    // ═══════════════════════════════════════════════════════════
    // KROK 2: Definicja języków do analizy i roku
    // ═══════════════════════════════════════════════════════════

    var languages = new[] { "c#", "python", "rust" };
    const int year = 2022;

    Console.WriteLine($"🔍 Analizuję języki programowania: {string.Join(", ", languages)}");
    Console.WriteLine($"📅 Rok: {year}");
    Console.WriteLine();

    // ═══════════════════════════════════════════════════════════
    // KROK 3: Pobieranie statystyk dla każdego języka
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("⏳ Pobieranie danych z BigQuery...");
    Console.WriteLine("   (To może potrwać kilka sekund dla każdego języka)\n");

    var allStats = new List<LanguageStats>();

    foreach (var lang in languages)
    {
        Console.Write($"   • {lang,-10} ... ");

        var stats = await service.GetLanguageStats(lang, year);

        if (stats != null)
        {
            allStats.Add(stats);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ ({stats.TotalSolvedQuestions:N0} pytań)");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Brak danych");
            Console.ResetColor();
        }
    }

    Console.WriteLine();

    if (allStats.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  Nie znaleziono żadnych danych do wyświetlenia.");
        Console.ResetColor();
        return 1;
    }

    // ═══════════════════════════════════════════════════════════
    // KROK 4: Wyświetlenie tabeli porównawczej
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("📊 TABELA PORÓWNAWCZA - SZYBKOŚĆ POMOCY NA STACK OVERFLOW");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    Console.WriteLine();

    // Sortowanie po czasie odpowiedzi (rosnąco) - najszybsze na górze
    var sortedStats = allStats
        .OrderBy(s => s.AvgMinutesToAnswer)
        .ToList();

    // Nagłówek tabeli
    Console.WriteLine("┌─────────────────┬──────────────────────────┬─────────────────────────┬─────────────────┐");
    Console.WriteLine("│ Język           │ Śr. czas odpowiedzi      │ Liczba pytań            │ Ranking         │");
    Console.WriteLine("├─────────────────┼──────────────────────────┼─────────────────────────┼─────────────────┤");

    // Wiersze z danymi
    for (int i = 0; i < sortedStats.Count; i++)
    {
        var stats = sortedStats[i];
        var rank = i + 1;
        var rankDisplay = rank switch
        {
            1 => "🥇 Najszybszy",
            2 => "🥈 Drugi",
            3 => "🥉 Trzeci",
            _ => $"#{rank}"
        };

        // Konwersja minut na czytelny format
        string timeDisplay = FormatMinutes(stats.AvgMinutesToAnswer);

        Console.WriteLine(
            $"│ {stats.Tag,-15} │ {timeDisplay,-24} │ {stats.TotalSolvedQuestions,23:N0} │ {rankDisplay,-15} │"
        );
    }

    // Stopka tabeli
    Console.WriteLine("└─────────────────┴──────────────────────────┴─────────────────────────┴─────────────────┘");
    Console.WriteLine();

    // ═══════════════════════════════════════════════════════════
    // KROK 5: Analiza porównawcza
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("💡 ANALIZA PORÓWNAWCZA:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    var fastest = sortedStats.First();
    var slowest = sortedStats.Last();

    Console.WriteLine($"   ✅ Najszybsza pomoc: {fastest.Tag}");
    Console.WriteLine($"      • Średni czas: {FormatMinutes(fastest.AvgMinutesToAnswer)}");
    Console.WriteLine($"      • Rozwiązanych pytań: {fastest.TotalSolvedQuestions:N0}");
    Console.WriteLine();

    Console.WriteLine($"   🐌 Najwolniejsza pomoc: {slowest.Tag}");
    Console.WriteLine($"      • Średni czas: {FormatMinutes(slowest.AvgMinutesToAnswer)}");
    Console.WriteLine($"      • Rozwiązanych pytań: {slowest.TotalSolvedQuestions:N0}");
    Console.WriteLine();

    if (fastest.AvgMinutesToAnswer > 0)
    {
        var ratio = slowest.AvgMinutesToAnswer / fastest.AvgMinutesToAnswer;
        Console.WriteLine($"   📈 Różnica: Odpowiedzi dla '{slowest.Tag}' są {ratio:F2}x wolniejsze");
        Console.WriteLine($"      niż dla '{fastest.Tag}'");
        Console.WriteLine();
    }

    // Statystyki ogólne
    var totalQuestions = allStats.Sum(s => s.TotalSolvedQuestions);
    var avgTime = allStats.Average(s => s.AvgMinutesToAnswer);

    Console.WriteLine($"   📊 Statystyki ogólne:");
    Console.WriteLine($"      • Całkowita liczba rozwiązanych pytań: {totalQuestions:N0}");
    Console.WriteLine($"      • Średni czas odpowiedzi (wszystkie języki): {FormatMinutes(avgTime)}");

    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // ═══════════════════════════════════════════════════════════
    // KROK 6: Szczegółowe statystyki dla każdego języka
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("📋 SZCZEGÓŁOWE STATYSTYKI:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    foreach (var stats in sortedStats)
    {
        Console.WriteLine($"\n   🔸 {stats.Tag.ToUpper()}");
        Console.WriteLine($"      • Średni czas od pytania do zaakceptowanej odpowiedzi:");
        Console.WriteLine($"        {FormatMinutes(stats.AvgMinutesToAnswer)}");
        Console.WriteLine($"      • Całkowita liczba pytań z zaakceptowaną odpowiedzią:");
        Console.WriteLine($"        {stats.TotalSolvedQuestions:N0} pytań");

        // Dodatkowa interpretacja
        if (stats.AvgMinutesToAnswer < 60)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"        ✅ Bardzo szybka społeczność!");
            Console.ResetColor();
        }
        else if (stats.AvgMinutesToAnswer < 1440) // < 24h
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"        ⚠️  Szybka społeczność");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"        ❌ Wolna społeczność - może być problem z dostępnością ekspertów");
            Console.ResetColor();
        }
    }

    Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

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
/// Formatuje minuty na czytelny format czasu.
/// </summary>
static string FormatMinutes(double minutes)
{
    if (minutes < 1)
    {
        var seconds = (int)(minutes * 60);
        return $"{seconds} sekund";
    }
    else if (minutes < 60)
    {
        return $"{minutes:F1} minut";
    }
    else if (minutes < 1440) // < 24 hours
    {
        var hours = minutes / 60.0;
        return $"{hours:F1} godzin ({minutes:F0} min)";
    }
    else
    {
        var days = minutes / 1440.0;
        var hours = minutes / 60.0;
        return $"{days:F1} dni ({hours:F0} godz)";
    }
}
