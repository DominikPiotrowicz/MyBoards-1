using Google.Cloud.BigQuery.V2;
using GoogleAnalyticsRegression;

Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("   Google Analytics - Regresja Liniowa (Własna Implementacja)");
Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

try
{
    // ═══════════════════════════════════════════════════════════
    // KROK 1: Pobranie danych z BigQuery
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🔗 Łączenie z BigQuery...");
    Console.WriteLine("📊 Dataset: bigquery-public-data.google_analytics_sample.ga_sessions_20170801");
    Console.WriteLine();

    var client = BigQueryClient.Create("bigquery-public-data");

    Console.WriteLine("⏳ Wykonywanie zapytania SQL...");
    Console.WriteLine("   Filtr: Tylko sesje, które zakończyły się zakupem");
    Console.WriteLine();

    var result = await client.ExecuteQueryAsync(
        Queries.GetSessionsWithTransactions,
        parameters: null
    );

    // Mapowanie wyników do obiektów SessionData
    var sessions = new List<SessionData>();

    await foreach (var row in result)
    {
        var session = new SessionData(
            PageViews: row["pageviews"] as long?,
            TransactionRevenueRaw: row["transactionRevenue"] as long?
        );

        sessions.Add(session);
    }

    Console.WriteLine($"✅ Pobrano {sessions.Count} sesji z transakcjami.\n");

    if (sessions.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  Brak danych do analizy. Sprawdź połączenie z BigQuery.");
        Console.ResetColor();
        return 1;
    }

    // Przygotowanie danych do trenowania (konwersja do par (X, Y))
    var trainingData = sessions
        .Where(s => s.PageViews.HasValue && s.PageViews > 0)
        .Select(s => (X: (double)s.PageViews!.Value, Y: s.TransactionRevenue))
        .ToList();

    Console.WriteLine($"📈 Przygotowano {trainingData.Count} par danych (PageViews, Revenue) do trenowania.\n");

    // Wyświetlenie przykładowych danych
    Console.WriteLine("📋 Przykładowe dane (pierwsze 5 rekordów):");
    Console.WriteLine("┌─────────────┬──────────────────┐");
    Console.WriteLine("│ PageViews   │ Revenue ($)      │");
    Console.WriteLine("├─────────────┼──────────────────┤");

    foreach (var (x, y) in trainingData.Take(5))
    {
        Console.WriteLine($"│ {x,11:F0} │ {y,16:C2} │");
    }

    Console.WriteLine("└─────────────┴──────────────────┘\n");

    // ═══════════════════════════════════════════════════════════
    // KROK 2: Trenowanie modelu regresji liniowej
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🧠 Trenowanie modelu regresji liniowej (OLS - Ordinary Least Squares)...");

    var model = new SimpleLinearRegression();
    model.Train(trainingData);

    Console.WriteLine("✅ Model wytrenowany!\n");

    // ═══════════════════════════════════════════════════════════
    // KROK 3: Wyświetlenie równania prostej
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("📐 RÓWNANIE PROSTEJ REGRESJI:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine($"   {model.GetEquation()}");
    Console.WriteLine();
    Console.WriteLine($"   gdzie:");
    Console.WriteLine($"   • y = przewidywany przychód (revenue) w $");
    Console.WriteLine($"   • x = liczba odsłon strony (pageviews)");
    Console.WriteLine();
    Console.WriteLine($"   Parametry modelu:");
    Console.WriteLine($"   • Nachylenie (slope, a):     {model.Slope:F6}");
    Console.WriteLine($"   • Przecięcie (intercept, b): {model.Intercept:F6}");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // Oblicz R² (współczynnik determinacji)
    double rSquared = model.CalculateRSquared(trainingData);
    Console.WriteLine($"📊 Współczynnik determinacji R²: {rSquared:F4}");
    Console.WriteLine($"   (R² = {rSquared:P2} - miara dopasowania modelu do danych)");

    if (rSquared > 0.7)
    {
        Console.WriteLine("   ✅ Dobre dopasowanie modelu!");
    }
    else if (rSquared > 0.4)
    {
        Console.WriteLine("   ⚠️  Średnie dopasowanie modelu.");
    }
    else
    {
        Console.WriteLine("   ❌ Słabe dopasowanie modelu.");
    }

    Console.WriteLine();

    // ═══════════════════════════════════════════════════════════
    // KROK 4: Predykcja dla 20 odsłon strony
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🔮 PREDYKCJA:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    double pageViewsToPredict = 20.0;
    double predictedRevenue = model.Predict(pageViewsToPredict);
    decimal predictedRevenueDecimal = (decimal)predictedRevenue;

    Console.WriteLine($"   Pytanie: Ile zarobimy, jeśli klient zrobi {pageViewsToPredict:F0} odsłon?");
    Console.WriteLine();
    Console.WriteLine($"   Odpowiedź: {predictedRevenueDecimal:C2}");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // Dodatkowe predykcje dla różnych wartości
    Console.WriteLine("📊 Dodatkowe predykcje dla różnych liczb odsłon:");
    Console.WriteLine("┌─────────────┬──────────────────┐");
    Console.WriteLine("│ PageViews   │ Predicted Revenue│");
    Console.WriteLine("├─────────────┼──────────────────┤");

    int[] testPageViews = { 1, 5, 10, 15, 20, 30, 50, 100 };

    foreach (int pv in testPageViews)
    {
        double predicted = model.Predict(pv);
        decimal predictedDecimal = (decimal)predicted;
        Console.WriteLine($"│ {pv,11} │ {predictedDecimal,16:C2} │");
    }

    Console.WriteLine("└─────────────┴──────────────────┘\n");

    // ═══════════════════════════════════════════════════════════
    // Statystyki opisowe
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("📈 STATYSTYKI DANYCH:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    double avgPageViews = trainingData.Average(d => d.X);
    double avgRevenue = trainingData.Average(d => d.Y);
    double minPageViews = trainingData.Min(d => d.X);
    double maxPageViews = trainingData.Max(d => d.X);
    double minRevenue = trainingData.Min(d => d.Y);
    double maxRevenue = trainingData.Max(d => d.Y);

    Console.WriteLine($"   PageViews:");
    Console.WriteLine($"   • Średnia:  {avgPageViews:F2}");
    Console.WriteLine($"   • Min:      {minPageViews:F0}");
    Console.WriteLine($"   • Max:      {maxPageViews:F0}");
    Console.WriteLine();
    Console.WriteLine($"   Revenue:");
    Console.WriteLine($"   • Średnia:  {(decimal)avgRevenue:C2}");
    Console.WriteLine($"   • Min:      {(decimal)minRevenue:C2}");
    Console.WriteLine($"   • Max:      {(decimal)maxRevenue:C2}");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // ═══════════════════════════════════════════════════════════
    // Interpretacja biznesowa
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("💡 INTERPRETACJA BIZNESOWA:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    decimal revenuePerPageView = (decimal)model.Slope;

    if (model.Slope > 0)
    {
        Console.WriteLine($"   ✅ Każda dodatkowa odsłona strony zwiększa przewidywany");
        Console.WriteLine($"      przychód średnio o {revenuePerPageView:C2}");
    }
    else if (model.Slope < 0)
    {
        Console.WriteLine($"   ⚠️  Negatywna korelacja - więcej odsłon = mniejszy przychód!");
        Console.WriteLine($"      (To może sugerować problemy z UX lub ścieżką zakupową)");
    }
    else
    {
        Console.WriteLine($"   ⚠️  Brak korelacji między liczbą odsłon a przychodem.");
    }

    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

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
