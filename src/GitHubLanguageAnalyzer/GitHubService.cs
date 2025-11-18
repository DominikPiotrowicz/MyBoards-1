using Google.Cloud.BigQuery.V2;

namespace GitHubLanguageAnalyzer;

/// <summary>
/// Serwis do analizy danych GitHub Repos z BigQuery.
/// Obsługuje REPEATED RECORD (tablice struktur) przy użyciu UNNEST.
/// </summary>
public class GitHubService
{
    private readonly BigQueryClient _client;

    /// <summary>
    /// Zapytanie SQL znajdując repozytoria "hybrydowe" - zawierające kod zarówno w C# jak i TypeScript.
    ///
    /// KLUCZOWA TECHNIKA: UNNEST dla REPEATED RECORD
    ///
    /// W BigQuery tabela github_repos.languages ma kolumnę:
    ///   language ARRAY<STRUCT<name STRING, bytes INT64>>
    ///
    /// To jest REPEATED RECORD - tablica struktur. Aby pracować z tym typem:
    /// 1. Używamy UNNEST(language) aby "rozwinąć" tablicę do wierszy
    /// 2. Każdy element tablicy staje się osobnym wierszem
    /// 3. Możemy wtedy filtrować po polach struktury (lang.name, lang.bytes)
    ///
    /// Przykład UNNEST:
    /// Jeśli repo ma language = [
    ///   {name: "C#", bytes: 5000},
    ///   {name: "TypeScript", bytes: 3000}
    /// ]
    ///
    /// Po UNNEST(language) AS lang otrzymamy 2 wiersze:
    ///   lang.name = "C#", lang.bytes = 5000
    ///   lang.name = "TypeScript", lang.bytes = 3000
    ///
    /// Strategia:
    /// - Używamy EXISTS z UNNEST aby sprawdzić czy repo zawiera dany język
    /// - Filtrujemy repozytoria, które mają ZARÓWNO C# jak i TypeScript
    /// - Zwracamy repo_name oraz CAŁĄ tablicę language (nie tylko C# i TS)
    /// </summary>
    private const string QueryHybridRepos = @"
        SELECT
            repo_name,
            language  -- Zwracamy CAŁĄ tablicę języków (REPEATED RECORD)

        FROM
            `bigquery-public-data.github_repos.languages`

        WHERE
            -- Sprawdzamy czy repo zawiera C#
            EXISTS (
                SELECT 1
                FROM UNNEST(language) AS lang
                WHERE lang.name = 'C#'
            )
            -- I czy zawiera TypeScript
            AND EXISTS (
                SELECT 1
                FROM UNNEST(language) AS lang
                WHERE lang.name = 'TypeScript'
            )

        LIMIT 20
    ";

    public GitHubService(BigQueryClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Pobiera repozytoria hybrydowe (C# + TypeScript) z GitHub.
    /// </summary>
    /// <returns>Lista repozytoriów z pełną informacją o językach</returns>
    public async Task<List<Repository>> GetHybridRepositories()
    {
        var repositories = new List<Repository>();

        // Wykonanie zapytania
        var result = await _client.ExecuteQueryAsync(
            QueryHybridRepos,
            parameters: null
        );

        // Mapowanie wyników - TO JEST WYZWANIE!
        // BigQuery zwraca REPEATED RECORD w specjalny sposób
        await foreach (var row in result)
        {
            var repoName = row["repo_name"] as string ?? "Unknown";

            // KLUCZOWY MOMENT: Mapowanie REPEATED RECORD
            // BigQuery zwraca kolumnę 'language' jako obiekt, który trzeba rozwinąć
            var languagesField = row["language"];

            var languages = new List<LanguageStats>();

            // languagesField może być:
            // - IEnumerable (gdy jest to tablica)
            // - BigQueryRow[] (gdy jest to tablica struktur)
            // - null (gdy nie ma danych)

            if (languagesField != null)
            {
                // Próbujemy zcastować do IEnumerable
                if (languagesField is IEnumerable<object> languageArray)
                {
                    foreach (var langItem in languageArray)
                    {
                        // Każdy element to struktura (RECORD)
                        // BigQuery zwraca RECORD jako BigQueryRow lub Dictionary
                        if (langItem is Dictionary<string, object> langDict)
                        {
                            var langName = langDict.ContainsKey("name")
                                ? langDict["name"] as string ?? ""
                                : "";

                            var langBytes = langDict.ContainsKey("bytes")
                                ? Convert.ToInt64(langDict["bytes"])
                                : 0L;

                            languages.Add(new LanguageStats
                            {
                                Name = langName,
                                Bytes = langBytes
                            });
                        }
                        // Alternatywnie, może być zwrócone jako BigQueryRow
                        else if (langItem is BigQueryRow langRow)
                        {
                            var langName = langRow["name"] as string ?? "";
                            var langBytes = langRow["bytes"] != null
                                ? Convert.ToInt64(langRow["bytes"])
                                : 0L;

                            languages.Add(new LanguageStats
                            {
                                Name = langName,
                                Bytes = langBytes
                            });
                        }
                    }
                }
            }

            var repository = new Repository
            {
                Name = repoName,
                Languages = languages
            };

            repositories.Add(repository);
        }

        return repositories;
    }
}
