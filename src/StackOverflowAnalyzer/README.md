# Stack Overflow Response Time Analyzer

Aplikacja konsolowa .NET 8 analizująca szybkość pomocy na Stack Overflow dla różnych języków programowania przy użyciu publicznych danych z BigQuery.

## Cel biznesowy

**Pytanie**: Który język programowania ma najszybszą społeczność na Stack Overflow?

Aplikacja analizuje:
- Średni czas od zadania pytania do udzielenia **zaakceptowanej odpowiedzi**
- Liczbę rozwiązanych pytań (z zaakceptowaną odpowiedzią)
- Porównanie między językami: C#, Python, Rust

## Wymagania

- .NET 8 SDK
- Konto Google Cloud z dostępem do BigQuery API
- Skonfigurowane Google Cloud credentials

## Struktura projektu

```
StackOverflowAnalyzer/
├── StackOverflowAnalyzer.csproj  # Definicja projektu .NET 8
├── Program.cs                     # Główna logika i wyświetlanie wyników
├── StackOverflowService.cs        # Serwis do komunikacji z BigQuery
├── LanguageStats.cs               # Model danych (rekord)
└── README.md                      # Ten plik
```

## Funkcjonalność

### 1. Źródła danych BigQuery

Aplikacja łączy się z publicznymi datasetami Stack Overflow:

- **Pytania**: `bigquery-public-data.stackoverflow.posts_questions`
- **Odpowiedzi**: `bigquery-public-data.stackoverflow.posts_answers`

### 2. Klasa `StackOverflowService`

Główna klasa serwisowa z metodą:

```csharp
public async Task<LanguageStats?> GetLanguageStats(string tag, int year)
```

#### Parametry:
- `tag`: Tag języka programowania (np. "c#", "python", "rust")
- `year`: Rok do analizy (np. 2022)

#### Zwraca:
Rekord `LanguageStats` z trzema polami:
- `Tag`: Nazwa tagu
- `AvgMinutesToAnswer`: Średni czas w minutach
- `TotalSolvedQuestions`: Liczba rozwiązanych pytań

### 3. Zapytanie SQL

#### WAŻNE: Parametryzowane zapytanie z BigQueryParameter

**✅ POPRAWNIE** - używamy parametrów:
```csharp
var parameters = new[]
{
    new BigQueryParameter("tag", BigQueryDbType.String, tag),
    new BigQueryParameter("year", BigQueryDbType.Int64, year)
};

await _client.ExecuteQueryAsync(QueryTemplate, parameters: parameters);
```

**❌ NIEPOPRAWNIE** - string interpolacja:
```csharp
// NIE RÓB TEGO!
var query = $"SELECT ... WHERE tag = '{tag}' AND year = {year}";
```

#### Struktura zapytania SQL:

```sql
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
```

#### Logika zapytania:

1. **Filtrowanie po roku**:
   ```sql
   EXTRACT(YEAR FROM q.creation_date) = @year
   ```

2. **Filtrowanie po tagu**:
   ```sql
   @tag IN UNNEST(SPLIT(q.tags, '|'))
   ```

   Obsługuje format tagów typu `'c#|linq|entity-framework'`:
   - `SPLIT(q.tags, '|')` - dzieli string na tablicę
   - `UNNEST()` - rozwija tablicę do wierszy
   - `@tag IN (...)` - sprawdza czy tag jest w tablicy

3. **JOIN pytań z odpowiedziami**:
   ```sql
   INNER JOIN posts_answers a ON q.accepted_answer_id = a.id
   ```

   Używa `accepted_answer_id` - tylko pytania z zaakceptowaną odpowiedzią!

4. **Obliczanie czasu odpowiedzi**:
   ```sql
   AVG(TIMESTAMP_DIFF(a.creation_date, q.creation_date, MINUTE))
   ```

   - `TIMESTAMP_DIFF(nowszy, starszy, MINUTE)` - różnica w minutach
   - `a.creation_date` - kiedy udzielono odpowiedzi
   - `q.creation_date` - kiedy zadano pytanie
   - `AVG()` - średnia dla wszystkich pytań

5. **Liczenie pytań**:
   ```sql
   COUNT(*) AS total_questions
   ```

### 4. Model danych

```csharp
public record LanguageStats(
    string Tag,
    double AvgMinutesToAnswer,
    long TotalSolvedQuestions
);
```

### 5. Funkcje główne (Program.cs)

- Pobiera statystyki dla C#, Python, Rust (rok 2022)
- Sortuje wyniki po czasie odpowiedzi (rosnąco)
- Wyświetla tabelę porównawczą z rankingiem
- Pokazuje szczegółową analizę
- Formatuje czas na czytelny format (sekundy/minuty/godziny/dni)

## Konfiguracja Google Cloud

### 1. Utwórz projekt

```bash
gcloud auth login
gcloud projects create stackoverflow-analyzer
gcloud config set project stackoverflow-analyzer
```

### 2. Włącz BigQuery API

```bash
gcloud services enable bigquery.googleapis.com
```

### 3. Utwórz Service Account

```bash
gcloud iam service-accounts create bigquery-reader \
    --display-name="BigQuery Reader"

gcloud projects add-iam-policy-binding stackoverflow-analyzer \
    --member="serviceAccount:bigquery-reader@stackoverflow-analyzer.iam.gserviceaccount.com" \
    --role="roles/bigquery.user"

gcloud iam service-accounts keys create ~/bigquery-key.json \
    --iam-account=bigquery-reader@stackoverflow-analyzer.iam.gserviceaccount.com
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
cd src/StackOverflowAnalyzer

dotnet restore
dotnet run
```

## Przykładowe wyjście

```
═══════════════════════════════════════════════════════════════════
   Stack Overflow - Analiza Szybkości Pomocy (Response Time)
═══════════════════════════════════════════════════════════════════

🔗 Łączenie z BigQuery...
📊 Dataset: bigquery-public-data.stackoverflow

🔍 Analizuję języki programowania: c#, python, rust
📅 Rok: 2022

⏳ Pobieranie danych z BigQuery...
   (To może potrwać kilka sekund dla każdego języka)

   • c#         ... ✓ (45,234 pytań)
   • python     ... ✓ (78,901 pytań)
   • rust       ... ✓ (12,456 pytań)

📊 TABELA PORÓWNAWCZA - SZYBKOŚĆ POMOCY NA STACK OVERFLOW
═══════════════════════════════════════════════════════════════════

┌─────────────────┬──────────────────────────┬─────────────────────────┬─────────────────┐
│ Język           │ Śr. czas odpowiedzi      │ Liczba pytań            │ Ranking         │
├─────────────────┼──────────────────────────┼─────────────────────────┼─────────────────┤
│ python          │ 156.3 minut              │                  78,901 │ 🥇 Najszybszy   │
│ c#              │ 187.5 minut              │                  45,234 │ 🥈 Drugi        │
│ rust            │ 245.8 minut              │                  12,456 │ 🥉 Trzeci       │
└─────────────────┴──────────────────────────┴─────────────────────────┴─────────────────┘

💡 ANALIZA PORÓWNAWCZA:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   ✅ Najszybsza pomoc: python
      • Średni czas: 156.3 minut
      • Rozwiązanych pytań: 78,901

   🐌 Najwolniejsza pomoc: rust
      • Średni czas: 245.8 minut
      • Rozwiązanych pytań: 12,456

   📈 Różnica: Odpowiedzi dla 'rust' są 1.57x wolniejsze
      niż dla 'python'

   📊 Statystyki ogólne:
      • Całkowita liczba rozwiązanych pytań: 136,591
      • Średni czas odpowiedzi (wszystkie języki): 196.5 minut
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📋 SZCZEGÓŁOWE STATYSTYKI:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   🔸 PYTHON
      • Średni czas od pytania do zaakceptowanej odpowiedzi:
        156.3 minut
      • Całkowita liczba pytań z zaakceptowaną odpowiedzią:
        78,901 pytań
        ⚠️  Szybka społeczność

   🔸 C#
      • Średni czas od pytania do zaakceptowanej odpowiedzi:
        187.5 minut
      • Całkowita liczba pytań z zaakceptowaną odpowiedzią:
        45,234 pytań
        ⚠️  Szybka społeczność

   🔸 RUST
      • Średni czas od pytania do zaakceptowanej odpowiedzi:
        245.8 minut
      • Całkowita liczba pytań z zaakceptowaną odpowiedzią:
        12,456 pytań
        ⚠️  Szybka społeczność

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✅ Analiza zakończona pomyślnie!
```

## Implementacja techniczna - szczegóły

### ✅ Parametryzowane zapytania BigQuery

**Dlaczego używamy `BigQueryParameter` zamiast string interpolacji?**

1. **Bezpieczeństwo**: Zapobiega SQL injection
2. **Wydajność**: BigQuery może cache'ować zapytania z parametrami
3. **Type safety**: Jawne określenie typów danych
4. **Best practice**: Zgodne z rekomendacjami Google Cloud

```csharp
// POPRAWNIE ✅
new BigQueryParameter("tag", BigQueryDbType.String, tag)
new BigQueryParameter("year", BigQueryDbType.Int64, year)

// NIEPOPRAWNIE ❌
$"WHERE tag = '{tag}' AND year = {year}"
```

### ✅ Obsługa formatów tagów Stack Overflow

Tagi w Stack Overflow są zapisywane jako string z separatorem `|`:
- `"c#"` - pojedynczy tag
- `"c#|linq"` - dwa tagi
- `"c#|linq|entity-framework"` - trzy tagi

**Rozwiązanie**: `SPLIT()` + `UNNEST()` + `IN`

```sql
-- Dzielimy string na array:
SPLIT(q.tags, '|')  -- ['c#', 'linq', 'entity-framework']

-- Rozwijamy array do wierszy:
UNNEST(['c#', 'linq', 'entity-framework'])
-- Wynik:
-- 'c#'
-- 'linq'
-- 'entity-framework'

-- Sprawdzamy czy tag jest w liście:
@tag IN UNNEST(SPLIT(q.tags, '|'))
```

### ✅ JOIN pytań z odpowiedziami

Używamy `INNER JOIN` przez `accepted_answer_id`:

```sql
FROM posts_questions q
INNER JOIN posts_answers a
ON q.accepted_answer_id = a.id
```

To oznacza:
- Bierzemy tylko pytania, które **mają zaakceptowaną odpowiedź**
- Łączymy z konkretną odpowiedzią (nie wszystkimi odpowiedziami)
- Filtrujemy pytania bez zaakceptowanej odpowiedzi

### ✅ Obliczanie różnicy czasu

```sql
TIMESTAMP_DIFF(a.creation_date, q.creation_date, MINUTE)
```

- `a.creation_date` - kiedy utworzono odpowiedź (nowszy timestamp)
- `q.creation_date` - kiedy utworzono pytanie (starszy timestamp)
- `MINUTE` - jednostka czasu (minuty)

BigQuery automatycznie oblicza różnicę i zwraca liczbę całkowitą minut.

### ✅ Formatowanie czasu w C#

Funkcja `FormatMinutes()` konwertuje minuty na czytelny format:

```csharp
< 1 min     → "45 sekund"
< 60 min    → "23.5 minut"
< 1440 min  → "3.2 godzin (192 min)"
>= 1440 min → "2.5 dni (60 godz)"
```

## Wymagania - realizacja

✅ **Dataset Stack Overflow**: Połączenie z `posts_questions` i `posts_answers`

✅ **Klasa `StackOverflowService`**: Z metodą `GetLanguageStats(string tag, int year)`

✅ **Zapytanie SQL**:
   - Filtruje po tagu: `@tag IN UNNEST(SPLIT(q.tags, '|'))`
   - Filtruje po roku: `EXTRACT(YEAR FROM q.creation_date) = @year`
   - JOIN: `ON q.accepted_answer_id = a.id`
   - Oblicza czas: `AVG(TIMESTAMP_DIFF(a.creation_date, q.creation_date, MINUTE))`

✅ **BigQueryParameter**: Tag i rok jako parametry, nie string interpolacja

✅ **Rekord `LanguageStats`**: (Tag, AvgMinutesToAnswer, TotalSolvedQuestions)

✅ **Main**: Pobiera dla c#, python, rust dla 2022 i wyświetla w tabeli

## Możliwe rozszerzenia

- Analiza trendu w czasie (2020, 2021, 2022, 2023)
- Więcej języków (JavaScript, Java, Go, TypeScript, etc.)
- Mediana czasu odpowiedzi (nie tylko średnia)
- Analiza % pytań z zaakceptowaną odpowiedzią
- Wykres wizualizujący porównanie
- Eksport wyników do CSV/JSON
- Analiza najlepszych godzin do zadawania pytań

## Rozwiązywanie problemów

### Błąd: "Table not found"

Upewnij się, że używasz pełnej nazwy tabeli:
```
bigquery-public-data.stackoverflow.posts_questions
```

### Błąd: "Access Denied"

Service account musi mieć rolę `roles/bigquery.user`.

### Długi czas wykonania zapytania

Stack Overflow dataset jest bardzo duży. Zapytanie może potrwać 10-30 sekund dla każdego języka.

### Brak wyników dla danego tagu

- Sprawdź poprawność tagu (wielkość liter ma znaczenie: `c#` vs `C#`)
- Sprawdź czy tag istniał w danym roku
- Sprawdź czy są pytania z zaakceptowanymi odpowiedziami

## Koszty

Stack Overflow dataset jest **publiczny i darmowy**, ale BigQuery ma limity:
- Pierwsze 1 TB zapytań miesięcznie: **DARMOWE**
- Następne TB: $5/TB

Ta aplikacja używa bardzo małej ilości danych (~kilka MB na zapytanie), więc mieści się w darmowym limicie.

## Licencja

Projekt edukacyjny - dane publiczne z BigQuery Stack Overflow Archive.
