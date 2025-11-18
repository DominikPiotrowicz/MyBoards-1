namespace GitHubLanguageAnalyzer;

/// <summary>
/// Reprezentuje repozytorium GitHub z informacjami o używanych językach programowania.
/// Mapuje wiersz z BigQuery zawierający kolumnę REPEATED RECORD (language).
/// </summary>
public class Repository
{
    /// <summary>
    /// Nazwa repozytorium (format: "owner/repo-name")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Lista języków używanych w tym repozytorium.
    /// WAŻNE: To mapuje REPEATED RECORD z BigQuery - tablicę struktur { name, bytes }
    /// </summary>
    public List<LanguageStats> Languages { get; set; } = new();

    /// <summary>
    /// Całkowita liczba bajtów kodu we wszystkich językach.
    /// </summary>
    public long TotalBytes => Languages.Sum(l => l.Bytes);

    /// <summary>
    /// Sprawdza, czy repozytorium zawiera kod w danym języku.
    /// </summary>
    /// <param name="languageName">Nazwa języka (case-insensitive)</param>
    /// <returns>True jeśli repozytorium zawiera ten język</returns>
    public bool HasLanguage(string languageName)
    {
        return Languages.Any(l =>
            l.Name.Equals(languageName, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Zwraca język dominujący (największa liczba bajtów).
    /// </summary>
    public LanguageStats? DominantLanguage =>
        Languages.OrderByDescending(l => l.Bytes).FirstOrDefault();

    /// <summary>
    /// Formatuje listę języków z procentami.
    /// Przykład: "C# (50.2%), TypeScript (30.1%), HTML (19.7%)"
    /// </summary>
    public string GetFormattedLanguages()
    {
        if (!Languages.Any())
            return "No languages";

        var totalBytes = TotalBytes;

        var formatted = Languages
            .OrderByDescending(l => l.Bytes)
            .Select(l =>
            {
                var percentage = l.CalculatePercentage(totalBytes);
                return $"{l.Name} ({percentage:F1}%)";
            });

        return string.Join(", ", formatted);
    }

    public override string ToString()
    {
        return $"[{Name}]: {GetFormattedLanguages()}";
    }
}
