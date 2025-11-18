using Google.Cloud.BigQuery.V2;
using GitHubLanguageAnalyzer;

Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
Console.WriteLine("   GitHub - Analiza Języków Hybrydowych (C# + TypeScript)");
Console.WriteLine("   Demonstracja UNNEST dla REPEATED RECORD w BigQuery");
Console.WriteLine("═══════════════════════════════════════════════════════════════════════\n");

try
{
    // ═══════════════════════════════════════════════════════════
    // KROK 1: Inicjalizacja klienta BigQuery
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🔗 Łączenie z BigQuery...");
    Console.WriteLine("📊 Dataset: bigquery-public-data.github_repos.languages");
    Console.WriteLine();
    Console.WriteLine("ℹ️  Informacja techniczna:");
    Console.WriteLine("   Kolumna 'language' to REPEATED RECORD (ARRAY<STRUCT<name, bytes>>)");
    Console.WriteLine("   Używamy UNNEST do rozwinięcia tablicy struktur");
    Console.WriteLine();

    var client = BigQueryClient.Create("bigquery-public-data");
    var service = new GitHubService(client);

    // ═══════════════════════════════════════════════════════════
    // KROK 2: Pobieranie repozytoriów hybrydowych
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🔍 Szukam repozytoriów hybrydowych...");
    Console.WriteLine("   Kryteria: Repozytorium musi zawierać KOD w C# I TypeScript");
    Console.WriteLine();
    Console.WriteLine("⏳ Wykonywanie zapytania z UNNEST...");
    Console.WriteLine("   (To może potrwać ~10-20 sekund - dataset jest duży)\n");

    var repositories = await service.GetHybridRepositories();

    if (repositories.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  Nie znaleziono żadnych repozytoriów hybrydowych.");
        Console.ResetColor();
        return 1;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✅ Znaleziono {repositories.Count} repozytoriów hybrydowych (C# + TypeScript)!\n");
    Console.ResetColor();

    // ═══════════════════════════════════════════════════════════
    // KROK 3: Wyświetlenie wyników z procentami
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("📋 REPOZYTORIA HYBRYDOWE - C# + TYPESCRIPT");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine("Format: [RepoName]: Język1 (%), Język2 (%), ...");
    Console.WriteLine("Procenty obliczone w C# na podstawie liczby bajtów kodu\n");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    for (int i = 0; i < repositories.Count; i++)
    {
        var repo = repositories[i];

        // Numeracja
        Console.Write($"{i + 1,2}. ");

        // Nazwa repozytorium
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"[{repo.Name}]");
        Console.ResetColor();
        Console.Write(": ");

        // Lista języków z procentami (obliczone w C#!)
        var totalBytes = repo.TotalBytes;

        var languagesFormatted = repo.Languages
            .OrderByDescending(l => l.Bytes)
            .Select(l =>
            {
                var percentage = l.CalculatePercentage(totalBytes);

                // Kolorowanie głównych języków
                var color = l.Name switch
                {
                    "C#" => ConsoleColor.Green,
                    "TypeScript" => ConsoleColor.Blue,
                    "JavaScript" => ConsoleColor.Yellow,
                    _ => ConsoleColor.Gray
                };

                return (Language: l.Name, Percentage: percentage, Color: color);
            })
            .ToList();

        // Wyświetlenie języków z kolorami
        for (int j = 0; j < languagesFormatted.Count; j++)
        {
            var (language, percentage, color) = languagesFormatted[j];

            Console.ForegroundColor = color;
            Console.Write($"{language} ({percentage:F1}%)");
            Console.ResetColor();

            if (j < languagesFormatted.Count - 1)
            {
                Console.Write(", ");
            }
        }

        Console.WriteLine();
    }

    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // ═══════════════════════════════════════════════════════════
    // KROK 4: Statystyki ogólne
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("📈 STATYSTYKI:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    // Liczba języków używanych w repozytoriach
    var allLanguages = repositories
        .SelectMany(r => r.Languages)
        .Select(l => l.Name)
        .Distinct()
        .OrderBy(n => n)
        .ToList();

    Console.WriteLine($"   Całkowita liczba znalezionych repozytoriów: {repositories.Count}");
    Console.WriteLine($"   Różne języki używane w tych repozytoriach: {allLanguages.Count}");
    Console.WriteLine();

    // Średnia liczba języków na repo
    var avgLanguages = repositories.Average(r => r.Languages.Count);
    Console.WriteLine($"   Średnia liczba języków na repozytorium: {avgLanguages:F1}");
    Console.WriteLine();

    // Top 5 najczęściej występujących języków (oprócz C# i TS)
    var languageFrequency = repositories
        .SelectMany(r => r.Languages)
        .GroupBy(l => l.Name)
        .Select(g => new { Language = g.Key, Count = g.Count() })
        .OrderByDescending(x => x.Count)
        .ToList();

    Console.WriteLine("   Top 10 najczęściej występujących języków:");
    foreach (var lang in languageFrequency.Take(10))
    {
        var percentage = (lang.Count / (double)repositories.Count) * 100;
        var bar = new string('█', Math.Min((int)(percentage / 5), 20));

        var color = lang.Language switch
        {
            "C#" => ConsoleColor.Green,
            "TypeScript" => ConsoleColor.Blue,
            "JavaScript" => ConsoleColor.Yellow,
            _ => ConsoleColor.Gray
        };

        Console.ForegroundColor = color;
        Console.Write($"      {lang.Language,-15}");
        Console.ResetColor();
        Console.WriteLine($" │ {lang.Count,2} repos ({percentage,5:F1}%) {bar}");
    }

    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // ═══════════════════════════════════════════════════════════
    // KROK 5: Analiza dominacji języków
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🏆 DOMINACJA JĘZYKÓW:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    var csharpDominant = repositories.Count(r =>
        r.DominantLanguage?.Name.Equals("C#", StringComparison.OrdinalIgnoreCase) == true
    );

    var tsDominant = repositories.Count(r =>
        r.DominantLanguage?.Name.Equals("TypeScript", StringComparison.OrdinalIgnoreCase) == true
    );

    var otherDominant = repositories.Count - csharpDominant - tsDominant;

    Console.WriteLine($"   Repozytoria z C# jako językiem dominującym:        {csharpDominant,2} ({(csharpDominant / (double)repositories.Count * 100):F1}%)");
    Console.WriteLine($"   Repozytoria z TypeScript jako językiem dominującym: {tsDominant,2} ({(tsDominant / (double)repositories.Count * 100):F1}%)");
    Console.WriteLine($"   Repozytoria z innym językiem dominującym:          {otherDominant,2} ({(otherDominant / (double)repositories.Count * 100):F1}%)");
    Console.WriteLine();

    if (csharpDominant > tsDominant)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("   💡 W większości repozytoriów C# jest językiem dominującym.");
    }
    else if (tsDominant > csharpDominant)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("   💡 W większości repozytoriów TypeScript jest językiem dominującym.");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   💡 C# i TypeScript są równie często dominujące.");
    }
    Console.ResetColor();

    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    // ═══════════════════════════════════════════════════════════
    // KROK 6: Przykłady szczegółowe
    // ═══════════════════════════════════════════════════════════

    Console.WriteLine("🔍 PRZYKŁADY SZCZEGÓŁOWE:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    // Najbardziej zbalansowane repo (C# i TS mają podobne procenty)
    var balancedRepo = repositories
        .Select(r =>
        {
            var csharpPercent = r.Languages.FirstOrDefault(l => l.Name == "C#")?.CalculatePercentage(r.TotalBytes) ?? 0;
            var tsPercent = r.Languages.FirstOrDefault(l => l.Name == "TypeScript")?.CalculatePercentage(r.TotalBytes) ?? 0;
            var diff = Math.Abs(csharpPercent - tsPercent);
            return (Repo: r, Diff: diff, CSharpPercent: csharpPercent, TsPercent: tsPercent);
        })
        .OrderBy(x => x.Diff)
        .FirstOrDefault();

    if (balancedRepo.Repo != null)
    {
        Console.WriteLine($"\n   🎯 Najbardziej zbalansowane repo (C# ≈ TypeScript):");
        Console.WriteLine($"      {balancedRepo.Repo.Name}");
        Console.WriteLine($"      C#: {balancedRepo.CSharpPercent:F1}%, TypeScript: {balancedRepo.TsPercent:F1}%");
        Console.WriteLine($"      Różnica: {balancedRepo.Diff:F1}%");
    }

    // Repo z największą liczbą języków
    var mostLanguages = repositories.OrderByDescending(r => r.Languages.Count).First();
    Console.WriteLine($"\n   🌐 Repo z największą różnorodnością języków:");
    Console.WriteLine($"      {mostLanguages.Name}");
    Console.WriteLine($"      Liczba języków: {mostLanguages.Languages.Count}");
    Console.WriteLine($"      Języki: {string.Join(", ", mostLanguages.Languages.Select(l => l.Name).Take(10))}");

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
