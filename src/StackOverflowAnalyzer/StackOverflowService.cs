using Google.Cloud.BigQuery.V2;

namespace StackOverflowAnalyzer;

/// <summary>
/// Serwis do analizy danych Stack Overflow z BigQuery.
/// </summary>
public class StackOverflowService
{
    private readonly BigQueryClient _client;

    /// <summary>
    /// Zapytanie SQL pobierające statystyki czasu odpowiedzi dla danego tagu i roku.
    ///
    /// Używa parametrów BigQuery (@tag, @year) dla bezpieczeństwa i wydajności.
    ///
    /// Logika:
    /// 1. Filtruje pytania po roku (EXTRACT(YEAR FROM creation_date))
    /// 2. Filtruje po tagu używając SPLIT - obsługuje format 'tag1|tag2|tag3'
    /// 3. JOIN z odpowiedziami przez accepted_answer_id
    /// 4. Oblicza średni czas w minutach (TIMESTAMP_DIFF)
    /// 5. Liczy całkowitą liczbę rozwiązanych pytań
    /// </summary>
    private const string QueryTemplate = @"
        SELECT
            @tag AS tag,
            AVG(TIMESTAMP_DIFF(a.creation_date, q.creation_date, MINUTE)) AS avg_minutes,
            COUNT(*) AS total_questions
        FROM
            `bigquery-public-data.stackoverflow.posts_questions` q
        INNER JOIN
            `bigquery-public-data.stackoverflow.posts_answers` a
        ON
            q.accepted_answer_id = a.id
        WHERE
            EXTRACT(YEAR FROM q.creation_date) = @year
            AND @tag IN UNNEST(SPLIT(q.tags, '|'))
            AND q.accepted_answer_id IS NOT NULL
    ";

    public StackOverflowService(BigQueryClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Pobiera statystyki szybkości odpowiedzi dla danego języka/tagu w danym roku.
    /// </summary>
    /// <param name="tag">Tag języka programowania (np. "c#", "python", "rust")</param>
    /// <param name="year">Rok do analizy (np. 2022)</param>
    /// <returns>Statystyki dla danego tagu</returns>
    /// <exception cref="ArgumentException">Gdy tag jest pusty lub rok jest nieprawidłowy</exception>
    public async Task<LanguageStats?> GetLanguageStats(string tag, int year)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag nie może być pusty.", nameof(tag));
        }

        if (year < 2008 || year > DateTime.UtcNow.Year)
        {
            throw new ArgumentException(
                $"Rok musi być pomiędzy 2008 a {DateTime.UtcNow.Year}.",
                nameof(year)
            );
        }

        // WAŻNE: Używamy BigQueryParameter zamiast string interpolacji ($"")
        // To zapobiega SQL injection i zwiększa wydajność (query caching)
        var parameters = new[]
        {
            new BigQueryParameter("tag", BigQueryDbType.String, tag),
            new BigQueryParameter("year", BigQueryDbType.Int64, year)
        };

        // Wykonanie zapytania z parametrami
        var result = await _client.ExecuteQueryAsync(
            QueryTemplate,
            parameters: parameters
        );

        // Mapowanie wyniku do rekordu LanguageStats
        await foreach (var row in result)
        {
            var returnedTag = row["tag"] as string ?? tag;

            // avg_minutes może być null jeśli nie ma danych
            var avgMinutes = row["avg_minutes"] != null
                ? Convert.ToDouble(row["avg_minutes"])
                : 0.0;

            var totalQuestions = row["total_questions"] != null
                ? Convert.ToInt64(row["total_questions"])
                : 0L;

            return new LanguageStats(
                Tag: returnedTag,
                AvgMinutesToAnswer: avgMinutes,
                TotalSolvedQuestions: totalQuestions
            );
        }

        // Jeśli nie znaleziono żadnych danych, zwróć statystyki z zerami
        return new LanguageStats(
            Tag: tag,
            AvgMinutesToAnswer: 0.0,
            TotalSolvedQuestions: 0L
        );
    }

    /// <summary>
    /// Pobiera statystyki dla wielu tagów jednocześnie.
    /// </summary>
    /// <param name="tags">Lista tagów do analizy</param>
    /// <param name="year">Rok do analizy</param>
    /// <returns>Lista statystyk dla wszystkich tagów</returns>
    public async Task<List<LanguageStats>> GetMultipleLanguageStats(IEnumerable<string> tags, int year)
    {
        var results = new List<LanguageStats>();

        foreach (var tag in tags)
        {
            var stats = await GetLanguageStats(tag, year);
            if (stats != null)
            {
                results.Add(stats);
            }
        }

        return results;
    }
}
