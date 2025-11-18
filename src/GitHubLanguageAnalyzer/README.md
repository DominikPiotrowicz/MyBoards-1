# GitHub Language Analyzer - Hybrid Repositories (C# + TypeScript)

Aplikacja konsolowa .NET 8 analizująca repozytoria GitHub zawierające kod w **C# i TypeScript** jednocześnie, z demonstracją obsługi **REPEATED RECORD** (zagnieżdżonych tablic struktur) w BigQuery.

## Cel biznesowy

**Pytanie**: Które repozytoria GitHub używają zarówno C# jak i TypeScript? Jakie inne języki w nich występują?

Aplikacja:
- Znajduje repozytoria "hybrydowe" (C# + TypeScript)
- Wyświetla wszystkie języki używane w każdym repozytorium
- Oblicza procentowy udział każdego języka (w C#, na podstawie bajtów)
- Analizuje dominację języków i różnorodność

## Wymagania

- .NET 8 SDK
- Konto Google Cloud z dostępem do BigQuery API
- Skonfigurowane Google Cloud credentials

## Struktura projektu

```
GitHubLanguageAnalyzer/
├── GitHubLanguageAnalyzer.csproj  # Definicja projektu .NET 8
├── Program.cs                      # Główna logika i wyświetlanie
├── GitHubService.cs                # Serwis BigQuery z UNNEST
├── Repository.cs                   # Model repozytorium z List<LanguageStats>
├── LanguageStats.cs                # Model języka (name + bytes)
└── README.md                       # Ten plik
```

## Funkcjonalność

### 1. Źródło danych BigQuery

**Tabela**:
```
bigquery-public-data.github_repos.languages
```

**Schemat tabeli**:
```sql
repo_name STRING
language ARRAY<STRUCT<name STRING, bytes INT64>>  -- REPEATED RECORD!
```

### 2. REPEATED RECORD - Co to jest?

**REPEATED RECORD** to typ danych BigQuery reprezentujący **tablicę struktur** (array of structs).

W naszym przypadku:
```sql
language ARRAY<STRUCT<name STRING, bytes INT64>>
```

**Przykład danych**:
```json
{
  "repo_name": "microsoft/TypeScript",
  "language": [
    { "name": "TypeScript", "bytes": 5234567 },
    { "name": "JavaScript", "bytes": 892345 },
    { "name": "HTML", "bytes": 45678 }
  ]
}
```

**W BigQuery GUI** kolumna `language` wyświetla się jako:
```
REPEATED RECORD
  ├─ name: STRING
  └─ bytes: INTEGER
```

### 3. UNNEST - Rozwijanie tablic

**UNNEST** to funkcja BigQuery, która **rozwija tablicę do wierszy**.

#### Przykład bez UNNEST:

```sql
SELECT repo_name, language
FROM github_repos.languages
LIMIT 1
```

**Wynik**: 1 wiersz z CAŁĄ tablicą:
```
repo_name: "microsoft/TypeScript"
language: [
  {name: "TypeScript", bytes: 5234567},
  {name: "JavaScript", bytes: 892345},
  {name: "HTML", bytes: 45678}
]
```

#### Przykład z UNNEST:

```sql
SELECT repo_name, lang.name, lang.bytes
FROM github_repos.languages
CROSS JOIN UNNEST(language) AS lang
LIMIT 3
```

**Wynik**: 3 wiersze (po jednym dla każdego języka):
```
repo_name: "microsoft/TypeScript", lang.name: "TypeScript", lang.bytes: 5234567
repo_name: "microsoft/TypeScript", lang.name: "JavaScript", lang.bytes: 892345
repo_name: "microsoft/TypeScript", lang.name: "HTML", lang.bytes: 45678
```

**UNNEST**:
- Zamienia każdy element tablicy w osobny wiersz
- Używa się `CROSS JOIN` (każdy wiersz tabeli × każdy element tablicy)
- Alias (`AS lang`) pozwala odwoływać się do pól struktury (`lang.name`, `lang.bytes`)

### 4. Zapytanie SQL

#### Strategia:

Chcemy znaleźć repozytoria, które mają **ZARÓWNO** C# jak i TypeScript.

**Nie możemy** po prostu użyć:
```sql
-- ❌ To NIE DZIAŁA!
WHERE language.name = 'C#' AND language.name = 'TypeScript'
```

Dlaczego? Bo `language` to **tablica**, nie pojedyncza wartość.

**Rozwiązanie**: Użyć `EXISTS` z `UNNEST`:

```sql
SELECT
    repo_name,
    language  -- Zwracamy CAŁĄ tablicę

FROM
    `bigquery-public-data.github_repos.languages`

WHERE
    -- Sprawdzamy czy tablica zawiera C#
    EXISTS (
        SELECT 1
        FROM UNNEST(language) AS lang
        WHERE lang.name = 'C#'
    )
    -- I czy tablica zawiera TypeScript
    AND EXISTS (
        SELECT 1
        FROM UNNEST(language) AS lang
        WHERE lang.name = 'TypeScript'
    )

LIMIT 20
```

#### Jak to działa:

1. **Zewnętrzne SELECT**: Pobiera `repo_name` i całą tablicę `language`
2. **Pierwszy EXISTS**: Rozwija tablicę `language` i sprawdza czy zawiera element z `name = 'C#'`
3. **Drugi EXISTS**: Rozwija tablicę `language` i sprawdza czy zawiera element z `name = 'TypeScript'`
4. **LIMIT 20**: Zwraca tylko 20 pierwszych wyników

**Alternatywne podejście** (używając podzapytań):

```sql
SELECT repo_name, language
FROM `bigquery-public-data.github_repos.languages`
WHERE repo_name IN (
    SELECT repo_name
    FROM `bigquery-public-data.github_repos.languages`
    CROSS JOIN UNNEST(language) AS lang
    WHERE lang.name = 'C#'
)
AND repo_name IN (
    SELECT repo_name
    FROM `bigquery-public-data.github_repos.languages`
    CROSS JOIN UNNEST(language) AS lang
    WHERE lang.name = 'TypeScript'
)
LIMIT 20
```

**Preferujemy EXISTS** bo jest bardziej wydajne (short-circuit evaluation).

### 5. Mapowanie w C#

#### Wyzwanie:

BigQuery zwraca kolumnę `language` (REPEATED RECORD) jako specjalny obiekt, który trzeba **ręcznie zmapować** do `List<LanguageStats>`.

#### Typy zwracane przez BigQuery:

Kolumna REPEATED RECORD może być zwrócona jako:
1. `IEnumerable<object>` - kolekcja obiektów
2. `BigQueryRow[]` - tablica wierszy BigQuery
3. `Dictionary<string, object>[]` - tablica słowników

Każdy element tablicy (RECORD) może być:
1. `Dictionary<string, object>` - słownik z kluczami `name`, `bytes`
2. `BigQueryRow` - wiersz BigQuery z dostępem przez indekser `row["name"]`

#### Implementacja mapowania:

```csharp
var languagesField = row["language"];
var languages = new List<LanguageStats>();

if (languagesField != null)
{
    if (languagesField is IEnumerable<object> languageArray)
    {
        foreach (var langItem in languageArray)
        {
            // Opcja 1: Dictionary
            if (langItem is Dictionary<string, object> langDict)
            {
                var langName = langDict["name"] as string ?? "";
                var langBytes = Convert.ToInt64(langDict["bytes"]);

                languages.Add(new LanguageStats
                {
                    Name = langName,
                    Bytes = langBytes
                });
            }
            // Opcja 2: BigQueryRow
            else if (langItem is BigQueryRow langRow)
            {
                var langName = langRow["name"] as string ?? "";
                var langBytes = Convert.ToInt64(langRow["bytes"]);

                languages.Add(new LanguageStats
                {
                    Name = langName,
                    Bytes = langBytes
                });
            }
        }
    }
}
```

**Kluczowe punkty**:
- Sprawdzamy typ `languagesField` (czy jest to `IEnumerable`)
- Iterujemy po elementach tablicy
- Dla każdego elementu sprawdzamy typ (Dictionary vs BigQueryRow)
- Ekstrahujemy pola `name` i `bytes`
- Tworzymy obiekt `LanguageStats`

### 6. Obliczanie procentów

Procenty obliczane są **w C#**, nie w SQL (zgodnie z wymaganiem).

```csharp
public double CalculatePercentage(long totalBytes)
{
    if (totalBytes == 0)
        return 0.0;

    return (Bytes / (double)totalBytes) * 100.0;
}
```

**Wzór**:
```
percentage = (bytes_języka / bytes_wszystkich_języków) * 100
```

**Przykład**:
- C#: 5000 bajtów
- TypeScript: 3000 bajtów
- HTML: 2000 bajtów
- **Total**: 10000 bajtów

**Procenty**:
- C#: (5000 / 10000) × 100 = 50%
- TypeScript: (3000 / 10000) × 100 = 30%
- HTML: (2000 / 10000) × 100 = 20%

### 7. Format wyświetlania

Wymagany format:
```
[RepoName]: C# (50.0%), TypeScript (30.0%), HTML (20.0%)
```

Implementacja:
```csharp
public string GetFormattedLanguages()
{
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
```

## Konfiguracja Google Cloud

### 1. Utwórz projekt

```bash
gcloud auth login
gcloud projects create github-lang-analyzer
gcloud config set project github-lang-analyzer
```

### 2. Włącz BigQuery API

```bash
gcloud services enable bigquery.googleapis.com
```

### 3. Utwórz Service Account

```bash
gcloud iam service-accounts create bigquery-reader \
    --display-name="BigQuery Reader"

gcloud projects add-iam-policy-binding github-lang-analyzer \
    --member="serviceAccount:bigquery-reader@github-lang-analyzer.iam.gserviceaccount.com" \
    --role="roles/bigquery.user"

gcloud iam service-accounts keys create ~/bigquery-key.json \
    --iam-account=bigquery-reader@github-lang-analyzer.iam.gserviceaccount.com
```

### 4. Ustaw zmienną środowiskową

**Windows (PowerShell):**
```powershell
$env:GOOGLE_APPLICATION_CREDENTIALS="C:\path\to\bigquery-key.json"
```

**Linux/macOS:**
```bash
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/bigquery-key.json"
```

## Uruchomienie

```bash
cd src/GitHubLanguageAnalyzer

dotnet restore
dotnet run
```

## Przykładowe wyjście

```
═══════════════════════════════════════════════════════════════════════
   GitHub - Analiza Języków Hybrydowych (C# + TypeScript)
   Demonstracja UNNEST dla REPEATED RECORD w BigQuery
═══════════════════════════════════════════════════════════════════════

🔗 Łączenie z BigQuery...
📊 Dataset: bigquery-public-data.github_repos.languages

ℹ️  Informacja techniczna:
   Kolumna 'language' to REPEATED RECORD (ARRAY<STRUCT<name, bytes>>)
   Używamy UNNEST do rozwinięcia tablicy struktur

✅ Znaleziono 20 repozytoriów hybrydowych (C# + TypeScript)!

📋 REPOZYTORIA HYBRYDOWE - C# + TYPESCRIPT
═══════════════════════════════════════════════════════════════════════

Format: [RepoName]: Język1 (%), Język2 (%), ...
Procenty obliczone w C# na podstawie liczby bajtów kodu

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 1. [microsoft/vscode]: TypeScript (72.3%), C# (15.2%), JavaScript (8.5%), CSS (4.0%)
 2. [dotnet/roslyn]: C# (89.4%), TypeScript (6.2%), HTML (4.4%)
 3. [aspnet/AspNetCore]: C# (75.1%), TypeScript (12.3%), JavaScript (8.6%), HTML (4.0%)
 4. [JamesNK/Newtonsoft.Json]: C# (92.5%), TypeScript (5.3%), HTML (2.2%)
...
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📈 STATYSTYKI:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   Całkowita liczba znalezionych repozytoriów: 20
   Różne języki używane w tych repozytoriach: 15

   Średnia liczba języków na repozytorium: 4.5

   Top 10 najczęściej występujących języków:
      C#              │ 20 repos (100.0%) ████████████████████
      TypeScript      │ 20 repos (100.0%) ████████████████████
      JavaScript      │ 18 repos ( 90.0%) ██████████████████
      HTML            │ 15 repos ( 75.0%) ███████████████
      CSS             │ 12 repos ( 60.0%) ████████████
      JSON            │  8 repos ( 40.0%) ████████
      Shell           │  5 repos ( 25.0%) █████
      Python          │  3 repos ( 15.0%) ███
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🏆 DOMINACJA JĘZYKÓW:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   Repozytoria z C# jako językiem dominującym:        12 (60.0%)
   Repozytoria z TypeScript jako językiem dominującym:  6 (30.0%)
   Repozytoria z innym językiem dominującym:           2 (10.0%)

   💡 W większości repozytoriów C# jest językiem dominującym.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✅ Analiza zakończona pomyślnie!
```

## Szczegóły techniczne

### REPEATED vs NESTED

**BigQuery ma dwa typy dla struktur zagnieżdżonych**:

1. **RECORD (STRUCT)** - pojedyncza struktura:
   ```sql
   address STRUCT<street STRING, city STRING>
   ```

   W jednym wierszu: `address.street`, `address.city`

2. **REPEATED RECORD (ARRAY<STRUCT>)** - tablica struktur:
   ```sql
   language ARRAY<STRUCT<name STRING, bytes INT64>>
   ```

   W jednym wierszu: cała tablica struktur

**Nasza sytuacja**: REPEATED RECORD (tablica struktur)

### CROSS JOIN UNNEST vs LEFT JOIN UNNEST

**CROSS JOIN UNNEST**:
```sql
FROM table
CROSS JOIN UNNEST(array_column) AS element
```

Zachowanie:
- Jeśli tablica ma 3 elementy → 3 wiersze
- Jeśli tablica jest pusta → 0 wierszy (wiersz znika!)
- Jeśli tablica jest NULL → 0 wierszy

**LEFT JOIN UNNEST**:
```sql
FROM table
LEFT JOIN UNNEST(array_column) AS element
```

Zachowanie:
- Jeśli tablica ma 3 elementy → 3 wiersze
- Jeśli tablica jest pusta → 1 wiersz (z NULL w element)
- Jeśli tablica jest NULL → 1 wiersz (z NULL w element)

**W naszym przypadku**: Używamy `CROSS JOIN` (w podzapytaniach `EXISTS`), bo nie chcemy wierszy z pustymi tablicami.

### EXISTS vs IN

**Dlaczego EXISTS jest lepsze**:

```sql
-- ✅ EXISTS - efektywne
WHERE EXISTS (
    SELECT 1
    FROM UNNEST(language) AS lang
    WHERE lang.name = 'C#'
)

-- ❌ IN - mniej efektywne
WHERE 'C#' IN (
    SELECT lang.name
    FROM UNNEST(language) AS lang
)
```

**Różnice**:
1. **EXISTS** przestaje szukać po znalezieniu pierwszego dopasowania (short-circuit)
2. **IN** musi zwrócić wszystkie wartości, a potem sprawdzić czy 'C#' jest w zbiorze
3. **EXISTS** jest bardziej czytelne dla intencji "czy istnieje element spełniający warunek"

### Mapowanie typów BigQuery → C#

| BigQuery Type | C# Type |
|---------------|---------|
| STRING | `string` |
| INT64 | `long` |
| FLOAT64 | `double` |
| BOOL | `bool` |
| TIMESTAMP | `DateTime` |
| STRUCT | `Dictionary<string, object>` lub custom class |
| ARRAY | `IEnumerable<T>` lub `List<T>` |
| ARRAY<STRUCT> | `List<Dictionary<string, object>>` lub `List<CustomClass>` |

## Wymagania - realizacja

✅ **Dataset**: `bigquery-public-data.github_repos.languages`

✅ **REPEATED RECORD**: Kolumna `language` typu `ARRAY<STRUCT<name, bytes>>`

✅ **UNNEST**: Użycie `UNNEST(language)` w podzapytaniach `EXISTS`

✅ **Filtrowanie hybrydowe**: Repozytoria z C# **I** TypeScript

✅ **Zwracanie całej tablicy**: SELECT zwraca `repo_name` i całą kolumnę `language`

✅ **Limit 20**: LIMIT 20 w zapytaniu

✅ **Klasa Repository**: Z właściwością `public List<LanguageStats> Languages`

✅ **Mapowanie nested**: Ręczne mapowanie REPEATED RECORD do `List<LanguageStats>`

✅ **Procenty w C#**: Obliczanie procentów w metodzie `CalculatePercentage()`

✅ **Format wyświetlania**: `[RepoName]: C# (50%), TypeScript (30%), ...`

## Możliwe rozszerzenia

- Analiza trendów w czasie (kiedy język został dodany/usunięty)
- Wykres korelacji między językami (które często występują razem)
- Analiza według organizacji (microsoft vs google vs facebook)
- Porównanie z innymi kombinacjami (Python + JavaScript, Java + Kotlin)
- Wizualizacja wykresów kołowych dla każdego repo
- Eksport do CSV/JSON
- Analiza zmian w czasie (commits)

## Rozwiązywanie problemów

### Błąd: "Cannot access field language on a value with type ARRAY"

To znaczy, że próbujesz użyć `language.name` bezpośrednio, zamiast `UNNEST`.

**Rozwiązanie**: Użyj UNNEST:
```sql
-- ❌ NIEPOPRAWNIE
WHERE language.name = 'C#'

-- ✅ POPRAWNIE
WHERE EXISTS (
    SELECT 1 FROM UNNEST(language) AS lang
    WHERE lang.name = 'C#'
)
```

### Błąd w C#: "Unable to cast object of type 'X' to type 'Y'"

Biblioteka BigQuery może zwracać różne typy dla REPEATED RECORD.

**Rozwiązanie**: Sprawdź typ przed castowaniem:
```csharp
if (langItem is Dictionary<string, object> langDict)
{
    // Przetwarzaj jako Dictionary
}
else if (langItem is BigQueryRow langRow)
{
    // Przetwarzaj jako BigQueryRow
}
```

### Długi czas wykonania zapytania

GitHub dataset jest **bardzo duży** (setki GB). Zapytanie może potrwać 20-60 sekund.

**Optymalizacje**:
- Zmniejsz LIMIT (np. do 10 zamiast 20)
- Dodaj WHERE z datą (jeśli tabela ma timestamp)
- Użyj partycjonowanej wersji tabeli (jeśli dostępna)

## Koszty

GitHub dataset jest **publiczny i darmowy**, ale BigQuery ma limity:
- Pierwsze 1 TB zapytań miesięcznie: **DARMOWE**
- Następne TB: $5/TB

Nasze zapytanie skanuje ~100-500 MB, więc mieści się w darmowym limicie.

## Licencja

Projekt edukacyjny - dane publiczne z BigQuery GitHub Archive.
