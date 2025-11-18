# BigQuery Analytics Suite - .NET 8 Applications

Kolekcja 5 zaawansowanych aplikacji konsolowych .NET 8 demonstrująca różne techniki pracy z **Google BigQuery** przy użyciu publicznych datasetów.

## 📋 Spis Aplikacji

| # | Aplikacja | Dataset | Kluczowe Techniki | Cel Biznesowy |
|---|-----------|---------|-------------------|---------------|
| 1 | [Chicago Taxi Analyzer](#1-chicago-taxi-analyzer) | Chicago Taxi Trips | Nullable types, JOIN, filtrowanie | Analiza najdroższych przejazdów |
| 2 | [Google Analytics Regression](#2-google-analytics-regression) | GA Sample | Własna regresja liniowa (OLS) | Predykcja przychodu z pageviews |
| 3 | [Stack Overflow Analyzer](#3-stack-overflow-analyzer) | Stack Overflow | BigQueryParameter, JOIN, SPLIT+UNNEST | Szybkość pomocy dla języków |
| 4 | [Austin Bikeshare Analyzer](#4-austin-bikeshare-analyzer) | Austin Bikeshare | Geography API (ST_GEOGPOINT, ST_DISTANCE) | Analiza prędkości przejazdów |
| 5 | [GitHub Language Analyzer](#5-github-language-analyzer) | GitHub Repos | REPEATED RECORD, UNNEST, nested objects | Repozytoria hybrydowe C#+TypeScript |

## 🎯 Omówienie Aplikacji

### 1. Chicago Taxi Analyzer

**Lokalizacja**: `src/ChicagoTaxiAnalyzer/`

**Cel**: Znalezienie 100 najdroższych przejazdów taksówką z Chicago (2023, płatności bezgotówkowe).

**Techniki BigQuery**:
- JOIN między `taxi_trips` a stacjami
- Filtrowanie: `payment_type != 'Cash'` i rok 2023
- Obliczanie procentu napiwku w C# (nie w SQL)
- Obsługa NULL-i przez typy nullable (`decimal?`, `double?`)

**Kluczowy kod SQL**:
```sql
SELECT trip_start_timestamp, trip_miles, fare, tips, payment_type
FROM `bigquery-public-data.chicago_taxi_trips.taxi_trips`
WHERE EXTRACT(YEAR FROM trip_start_timestamp) = 2023
  AND payment_type != 'Cash'
ORDER BY (IFNULL(fare, 0) + IFNULL(tips, 0)) DESC
LIMIT 100
```

**Output**: Tabela z dystansem, opłatą, napiwkiem i % napiwku dla każdego przejazdu.

---

### 2. Google Analytics Regression

**Lokalizacja**: `src/GoogleAnalyticsRegression/`

**Cel**: Przewidywanie przychodu na podstawie liczby odsłon strony - "Ile zarobimy, jeśli użytkownik wykona 20 odsłon?"

**Techniki BigQuery**:
- Filtrowanie sesji z transakcjami (`transactionRevenue > 0`)
- Normalizacja: dzielenie przez 1,000,000 (format GA)

**Techniki C#**:
- **Własna implementacja regresji liniowej** (bez ML.NET!)
- Metoda najmniejszych kwadratów (OLS)
- Obliczanie R² (coefficient of determination)

**Wzory matematyczne**:
```
slope = Σ((Xi - mean_x) * (Yi - mean_y)) / Σ((Xi - mean_x)²)
intercept = mean_y - slope * mean_x
prediction = slope * x + intercept
```

**Output**: Równanie prostej `y = ax + b` i predykcja dla 20 pageviews.

---

### 3. Stack Overflow Analyzer

**Lokalizacja**: `src/StackOverflowAnalyzer/`

**Cel**: Która społeczność programistów jest najszybsza? (C# vs Python vs Rust - 2022)

**Techniki BigQuery**:
- **BigQueryParameter** - parametryzowane zapytania (bezpieczeństwo!)
- JOIN `posts_questions` z `posts_answers` przez `accepted_answer_id`
- SPLIT dla tagów (`'c#|linq'` → tablica)
- TIMESTAMP_DIFF dla czasu w minutach

**Kluczowy kod SQL**:
```sql
-- Używamy parametrów zamiast string interpolacji!
WHERE @tag IN UNNEST(SPLIT(q.tags, '|'))
  AND EXTRACT(YEAR FROM q.creation_date) = @year
```

**Kluczowy kod C#**:
```csharp
// POPRAWNIE ✅
var parameters = new[] {
    new BigQueryParameter("tag", BigQueryDbType.String, "c#"),
    new BigQueryParameter("year", BigQueryDbType.Int64, 2022)
};

// NIEPOPRAWNIE ❌ - NIE RÓB TEGO!
var query = $"WHERE tag = '{tag}' AND year = {year}";
```

**Output**: Tabela porównawcza z średnim czasem odpowiedzi dla każdego języka.

---

### 4. Austin Bikeshare Analyzer

**Lokalizacja**: `src/AustinBikeshareAnalyzer/`

**Cel**: Znajdź najszybsze przejazdy rowerowe. Wykryj anomalie (absurdalne prędkości > 50 km/h).

**Techniki BigQuery - Geography API**:
- **ST_GEOGPOINT(longitude, latitude)** - tworzenie punktów geograficznych
- **ST_DISTANCE(point1, point2)** - dystans geodezyjny w metrach
- Podwójny JOIN (dla stacji startowej i końcowej)
- Obliczanie prędkości: `(distance_km) / (time_hours)`

**Kluczowy kod SQL**:
```sql
-- Tworzenie punktów geograficznych
ST_GEOGPOINT(start_station.longitude, start_station.latitude) AS start_point

-- Obliczanie dystansu w metrach
ST_DISTANCE(start_point, end_point) AS distance_meters

-- Prędkość w km/h
(ST_DISTANCE(start_point, end_point) / 1000.0) / (duration_minutes / 60.0) AS speed_kmh
```

**⚠️ WAŻNE**: Kolejność w ST_GEOGPOINT to `(longitude, latitude)`, nie odwrotnie!

**Output**: Top 20 najszybszych przejazdów z detekcją anomalii (prawdopodobnie błędy danych).

---

### 5. GitHub Language Analyzer

**Lokalizacja**: `src/GitHubLanguageAnalyzer/`

**Cel**: Znajdź repozytoria używające C# i TypeScript jednocześnie. Pokaż wszystkie języki z procentami.

**Techniki BigQuery - REPEATED RECORD**:
- Kolumna `language` to `ARRAY<STRUCT<name STRING, bytes INT64>>`
- **UNNEST** - rozwijanie tablicy struktur do wierszy
- **EXISTS** z UNNEST dla filtrowania
- Zwracanie całej tablicy (nie tylko przefiltrowanych elementów)

**Kluczowy kod SQL**:
```sql
-- Sprawdzenie czy tablica zawiera C#
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
```

**Wyzwanie C#**:
Mapowanie REPEATED RECORD do `List<LanguageStats>`:
```csharp
if (languagesField is IEnumerable<object> languageArray)
{
    foreach (var langItem in languageArray)
    {
        if (langItem is Dictionary<string, object> langDict)
        {
            // Przetwarzanie Dictionary
        }
        else if (langItem is BigQueryRow langRow)
        {
            // Przetwarzanie BigQueryRow
        }
    }
}
```

**Output**:
```
[microsoft/vscode]: TypeScript (72.3%), C# (15.2%), JavaScript (8.5%), CSS (4.0%)
```

## 🛠️ Wymagania

### Oprogramowanie

- **.NET 8 SDK** - [Pobierz](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Google Cloud Account** - [Zarejestruj się](https://cloud.google.com/)
- **BigQuery API** włączony w projekcie GCP

### Pakiety NuGet

Wszystkie projekty używają:
```xml
<PackageReference Include="Google.Cloud.BigQuery.V2" Version="3.7.0" />
```

## ⚙️ Konfiguracja

### 1. Utwórz projekt Google Cloud

```bash
# Zaloguj się
gcloud auth login

# Utwórz projekt
gcloud projects create bigquery-analytics-suite

# Ustaw aktywny projekt
gcloud config set project bigquery-analytics-suite
```

### 2. Włącz BigQuery API

```bash
gcloud services enable bigquery.googleapis.com
```

### 3. Utwórz Service Account

```bash
# Utwórz service account
gcloud iam service-accounts create bigquery-reader \
    --display-name="BigQuery Reader"

# Przypisz rolę BigQuery User
gcloud projects add-iam-policy-binding bigquery-analytics-suite \
    --member="serviceAccount:bigquery-reader@bigquery-analytics-suite.iam.gserviceaccount.com" \
    --role="roles/bigquery.user"

# Wygeneruj klucz JSON
gcloud iam service-accounts keys create ~/bigquery-key.json \
    --iam-account=bigquery-reader@bigquery-analytics-suite.iam.gserviceaccount.com
```

### 4. Ustaw zmienną środowiskową

**Windows (PowerShell):**
```powershell
$env:GOOGLE_APPLICATION_CREDENTIALS="C:\path\to\bigquery-key.json"
```

**Windows (CMD):**
```cmd
set GOOGLE_APPLICATION_CREDENTIALS=C:\path\to\bigquery-key.json
```

**Linux/macOS:**
```bash
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/bigquery-key.json"
```

**Trwała konfiguracja (Linux/macOS)**:
```bash
# Dodaj do ~/.bashrc lub ~/.zshrc
echo 'export GOOGLE_APPLICATION_CREDENTIALS="/path/to/bigquery-key.json"' >> ~/.bashrc
source ~/.bashrc
```

## 🚀 Uruchomienie Aplikacji

### Ogólny schemat

```bash
# Przejdź do folderu aplikacji
cd src/[NazwaAplikacji]

# Przywróć pakiety NuGet
dotnet restore

# Uruchom aplikację
dotnet run
```

### Konkretne aplikacje

```bash
# 1. Chicago Taxi Analyzer
cd src/ChicagoTaxiAnalyzer && dotnet run

# 2. Google Analytics Regression
cd src/GoogleAnalyticsRegression && dotnet run

# 3. Stack Overflow Analyzer
cd src/StackOverflowAnalyzer && dotnet run

# 4. Austin Bikeshare Analyzer
cd src/AustinBikeshareAnalyzer && dotnet run

# 5. GitHub Language Analyzer
cd src/GitHubLanguageAnalyzer && dotnet run
```

### Budowanie w trybie Release

```bash
cd src/[NazwaAplikacji]

# Zbuduj Release
dotnet build -c Release

# Uruchom zbudowaną aplikację
dotnet run -c Release
```

## 📚 Najważniejsze Techniki BigQuery

### 1. Typy Nullable (Chicago Taxi)

```csharp
// ✅ POPRAWNIE - obsługa NULL
public record TaxiTrip(
    DateTime? TripStartTimestamp,
    double? TripMiles,
    decimal? Fare
);

// ❌ NIEPOPRAWNIE - wywali się na NULL
public record TaxiTrip(
    DateTime TripStartTimestamp,  // Boom! jeśli NULL
    double TripMiles,              // Boom! jeśli NULL
    decimal Fare                   // Boom! jeśli NULL
);
```

### 2. BigQueryParameter (Stack Overflow)

```csharp
// ✅ POPRAWNIE - bezpieczne
var parameters = new[] {
    new BigQueryParameter("tag", BigQueryDbType.String, tag),
    new BigQueryParameter("year", BigQueryDbType.Int64, year)
};

// ❌ NIEPOPRAWNIE - SQL injection!
var query = $"WHERE tag = '{tag}' AND year = {year}";
```

### 3. Geography API (Austin Bikeshare)

```sql
-- ⚠️ KOLEJNOŚĆ MA ZNACZENIE!

-- ✅ POPRAWNIE
ST_GEOGPOINT(longitude, latitude)  -- lon, potem lat

-- ❌ NIEPOPRAWNIE
ST_GEOGPOINT(latitude, longitude)  -- Błąd!
```

### 4. UNNEST dla REPEATED (GitHub)

```sql
-- Tablica: language = [{name: "C#", bytes: 5000}, {name: "TS", bytes: 3000}]

-- ✅ POPRAWNIE - używamy UNNEST
WHERE EXISTS (
    SELECT 1 FROM UNNEST(language) AS lang
    WHERE lang.name = 'C#'
)

-- ❌ NIEPOPRAWNIE - nie można bezpośrednio
WHERE language.name = 'C#'  -- Błąd! language to tablica
```

## 📊 Datasety BigQuery (Publiczne)

Wszystkie aplikacje używają publicznych datasetów Google:

| Dataset | Projekt | Tabela | Rozmiar (GB) |
|---------|---------|--------|--------------|
| Chicago Taxi | `bigquery-public-data` | `chicago_taxi_trips.taxi_trips` | ~70 GB |
| Google Analytics | `bigquery-public-data` | `google_analytics_sample.ga_sessions_*` | ~1 GB |
| Stack Overflow | `bigquery-public-data` | `stackoverflow.posts_*` | ~300 GB |
| Austin Bikeshare | `bigquery-public-data` | `austin_bikeshare.bikeshare_*` | ~100 MB |
| GitHub Repos | `bigquery-public-data` | `github_repos.languages` | ~200 GB |

**Koszty**: BigQuery oferuje **1 TB darmowych zapytań miesięcznie**. Wszystkie aplikacje mieszczą się w tym limicie.

## 🔍 Rozwiązywanie Problemów

### Błąd: "The Application Default Credentials are not available"

**Przyczyna**: Brak skonfigurowanej zmiennej `GOOGLE_APPLICATION_CREDENTIALS`.

**Rozwiązanie**:
```bash
# Sprawdź czy zmienna jest ustawiona
echo $GOOGLE_APPLICATION_CREDENTIALS  # Linux/macOS
echo %GOOGLE_APPLICATION_CREDENTIALS%  # Windows CMD

# Ustaw zmienną
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/key.json"
```

### Błąd: "Access Denied: BigQuery"

**Przyczyna**: Service account nie ma wystarczających uprawnień.

**Rozwiązanie**:
```bash
# Przypisz rolę BigQuery User
gcloud projects add-iam-policy-binding [PROJECT_ID] \
    --member="serviceAccount:[SERVICE_ACCOUNT_EMAIL]" \
    --role="roles/bigquery.user"
```

### Długi czas wykonania zapytania

**To normalne!** Niektóre datasety są bardzo duże:
- Stack Overflow: 20-60 sekund
- GitHub: 30-90 sekund
- Chicago Taxi: 10-30 sekund

**Optymalizacje**:
- Zmniejsz LIMIT w zapytaniach
- Dodaj więcej filtrów WHERE
- Użyj partycjonowanych tabel (jeśli dostępne)

### Błąd konwersji typów (REPEATED RECORD)

**Przyczyna**: BigQuery może zwracać REPEATED RECORD jako różne typy C#.

**Rozwiązanie**: Sprawdź typ przed castowaniem:
```csharp
if (field is Dictionary<string, object> dict)
{
    // Przetwórz jako Dictionary
}
else if (field is BigQueryRow row)
{
    // Przetwórz jako BigQueryRow
}
```

## 📖 Dokumentacja

Każda aplikacja ma własny szczegółowy README.md:

- [Chicago Taxi Analyzer README](src/ChicagoTaxiAnalyzer/README.md)
- [Google Analytics Regression README](src/GoogleAnalyticsRegression/README.md)
- [Stack Overflow Analyzer README](src/StackOverflowAnalyzer/README.md)
- [Austin Bikeshare Analyzer README](src/AustinBikeshareAnalyzer/README.md)
- [GitHub Language Analyzer README](src/GitHubLanguageAnalyzer/README.md)

## 🎓 Czego się nauczysz

### SQL/BigQuery

- ✅ Filtrowanie i JOIN-y na dużych datasetach
- ✅ Funkcje agregujące (AVG, COUNT, SUM)
- ✅ Window functions i CTE
- ✅ Geography API (ST_GEOGPOINT, ST_DISTANCE)
- ✅ UNNEST dla tablic i struktur zagnieżdżonych
- ✅ EXISTS vs IN (performance)
- ✅ Parametryzowane zapytania (bezpieczeństwo)

### C# / .NET

- ✅ Nullable types i null safety
- ✅ Records i pattern matching
- ✅ LINQ (Select, Where, OrderBy, GroupBy)
- ✅ Async/await z BigQuery
- ✅ Własna implementacja algorytmów (regresja liniowa)
- ✅ Mapowanie zagnieżdżonych struktur danych
- ✅ Type checking i konwersje

### Architektura

- ✅ Separation of concerns (Service, Model, Program)
- ✅ Clean code i SOLID principles
- ✅ Error handling i walidacja
- ✅ Formatowanie output dla użytkownika
- ✅ Dokumentacja i README

## 💡 Przypadki Użycia Biznesowego

### 1. Transport & Logistics (Chicago Taxi)
- Analiza rentowności przejazdów
- Optymalizacja cen
- Analiza napiwków dla kierowców

### 2. E-commerce (Google Analytics)
- Predykcja konwersji
- Optymalizacja UX (pageviews → revenue)
- A/B testing

### 3. Developer Community (Stack Overflow)
- Wybór technologii (aktywność społeczności)
- HR - rekrutacja (gdzie szybko znajdziesz pomoc)
- Marketing produktów developerskich

### 4. Bike Sharing / Urban Mobility (Austin Bikeshare)
- Wykrywanie anomalii w danych GPS
- Optymalizacja rozmieszczenia stacji
- Analiza popularnych tras

### 5. Open Source Analytics (GitHub)
- Trendy w technologiach
- Analiza stacków technologicznych
- Badanie migracji między językami

## 📝 Licencja

Projekt edukacyjny. Datasety BigQuery są publiczne i udostępnione przez Google pod różnymi licencjami open data.

## 🤝 Współpraca

Jeśli chcesz dodać nową aplikację BigQuery:

1. Stwórz folder w `src/[NazwaAplikacji]/`
2. Dodaj `.csproj`, `Program.cs`, modele danych
3. Napisz szczegółowy README.md
4. Dodaj przykłady SQL i C#
5. Zaktualizuj ten główny README.md

## 🔗 Przydatne Linki

- [BigQuery Documentation](https://cloud.google.com/bigquery/docs)
- [BigQuery Public Datasets](https://cloud.google.com/bigquery/public-data)
- [Google.Cloud.BigQuery.V2 NuGet](https://www.nuget.org/packages/Google.Cloud.BigQuery.V2)
- [BigQuery Geography Functions](https://cloud.google.com/bigquery/docs/reference/standard-sql/geography_functions)
- [BigQuery SQL Reference](https://cloud.google.com/bigquery/docs/reference/standard-sql/query-syntax)

## 📧 Kontakt

Dla pytań dotyczących implementacji lub BigQuery, sprawdź:
- README poszczególnych aplikacji
- Oficjalną dokumentację BigQuery
- Stack Overflow z tagiem `google-bigquery`

---

**Wykonane**: Listopad 2025 | **Framework**: .NET 8 | **Język**: C# 12 | **Cloud**: Google BigQuery
