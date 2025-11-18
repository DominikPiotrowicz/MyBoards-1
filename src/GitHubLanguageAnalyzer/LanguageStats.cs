namespace GitHubLanguageAnalyzer;

/// <summary>
/// Reprezentuje statystyki pojedynczego języka w repozytorium GitHub.
/// Mapuje RECORD z BigQuery: { name: STRING, bytes: INTEGER }
/// </summary>
public class LanguageStats
{
    /// <summary>
    /// Nazwa języka programowania (np. "C#", "TypeScript", "JavaScript")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Liczba bajtów kodu w tym języku
    /// </summary>
    public long Bytes { get; set; }

    /// <summary>
    /// Oblicza procent kodu dla tego języka w kontekście całkowitej liczby bajtów.
    /// </summary>
    /// <param name="totalBytes">Całkowita liczba bajtów we wszystkich językach</param>
    /// <returns>Procent kodu w tym języku (0-100)</returns>
    public double CalculatePercentage(long totalBytes)
    {
        if (totalBytes == 0)
            return 0.0;

        return (Bytes / (double)totalBytes) * 100.0;
    }

    public override string ToString()
    {
        return $"{Name} ({Bytes:N0} bytes)";
    }
}
