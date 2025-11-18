# Google Analytics Linear Regression

Aplikacja konsolowa .NET 8 implementująca prostą regresję liniową **od podstaw** (bez ML.NET) do analizy zależności między liczbą odsłon strony a przychodem z transakcji w danych Google Analytics.

## Cel biznesowy

Odpowiedź na pytanie: **"Ile zarobimy, jeśli użytkownik wykona 20 odsłon strony?"**

Analiza korelacji między:
- **X (zmienna niezależna)**: liczba odsłon strony (`totals.pageviews`)
- **Y (zmienna zależna)**: przychód z transakcji (`totals.transactionRevenue`)

## Wymagania

- .NET 8 SDK
- Konto Google Cloud z dostępem do BigQuery API
- Skonfigurowane Google Cloud credentials

## Struktura projektu

```
GoogleAnalyticsRegression/
├── GoogleAnalyticsRegression.csproj  # Definicja projektu .NET 8
├── Program.cs                         # Główna logika aplikacji
├── SessionData.cs                     # Model danych GA
├── SimpleLinearRegression.cs          # Implementacja OLS od zera
├── Queries.cs                         # Zapytania SQL
└── README.md                          # Ten plik
```

## Funkcjonalność

### 1. Pobranie danych z BigQuery

- Dataset: `bigquery-public-data.google_analytics_sample.ga_sessions_20170801`
- Filtrowanie: tylko sesje, które zakończyły się zakupem (`transactionRevenue > 0`)
- Normalizacja: `transactionRevenue / 1,000,000` (wartości w GA są pomnożone przez milion)

### 2. Model regresji liniowej

**Klasa `SimpleLinearRegression`** - całkowicie własna implementacja bez ML.NET:

#### Metoda `Train(List<(double X, double Y)> data)`

Implementuje **metodę najmniejszych kwadratów (OLS - Ordinary Least Squares)**:

```csharp
// Krok 1: Oblicz średnie
meanX = Σ(Xi) / n
meanY = Σ(Yi) / n

// Krok 2: Oblicz współczynniki
slope (a) = Σ((Xi - meanX) * (Yi - meanY)) / Σ((Xi - meanX)²)
intercept (b) = meanY - (slope * meanX)
```

#### Metoda `Predict(double x)`

Zwraca przewidywaną wartość Y dla danego X:

```csharp
y = (slope * x) + intercept
```

#### Metoda `CalculateRSquared()`

Oblicza współczynnik determinacji R² (miara dopasowania modelu):

```csharp
R² = 1 - (SS_res / SS_tot)

gdzie:
- SS_res = Σ(Yi - predicted_Yi)²  // suma kwadratów reszt
- SS_tot = Σ(Yi - meanY)²          // suma kwadratów całkowitych
```

Interpretacja R²:
- **R² > 0.7**: dobre dopasowanie
- **0.4 < R² < 0.7**: średnie dopasowanie
- **R² < 0.4**: słabe dopasowanie

### 3. Wyświetlanie wyników

Aplikacja wyświetla:
- Równanie prostej regresji: `y = ax + b`
- Parametry modelu (slope, intercept)
- Współczynnik R²
- **Predykcję dla 20 odsłon strony**
- Dodatkowe predykcje dla różnych wartości
- Statystyki opisowe danych
- Interpretację biznesową

## Konfiguracja Google Cloud

### 1. Utwórz projekt w Google Cloud

```bash
gcloud auth login
gcloud projects create ga-regression-analyzer
gcloud config set project ga-regression-analyzer
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
gcloud projects add-iam-policy-binding ga-regression-analyzer \
    --member="serviceAccount:bigquery-reader@ga-regression-analyzer.iam.gserviceaccount.com" \
    --role="roles/bigquery.user"

# Wygeneruj klucz JSON
gcloud iam service-accounts keys create ~/bigquery-key.json \
    --iam-account=bigquery-reader@ga-regression-analyzer.iam.gserviceaccount.com
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

## Uruchomienie

```bash
cd src/GoogleAnalyticsRegression

# Przywróć pakiety
dotnet restore

# Uruchom aplikację
dotnet run
```

## Przykładowe wyjście

```
═══════════════════════════════════════════════════════════════
   Google Analytics - Regresja Liniowa (Własna Implementacja)
═══════════════════════════════════════════════════════════════

🔗 Łączenie z BigQuery...
📊 Dataset: bigquery-public-data.google_analytics_sample.ga_sessions_20170801

⏳ Wykonywanie zapytania SQL...
   Filtr: Tylko sesje, które zakończyły się zakupem

✅ Pobrano 81 sesji z transakcjami.

📈 Przygotowano 81 par danych (PageViews, Revenue) do trenowania.

📋 Przykładowe dane (pierwsze 5 rekordów):
┌─────────────┬──────────────────┐
│ PageViews   │ Revenue ($)      │
├─────────────┼──────────────────┤
│           5 │           $15.99 │
│          12 │          $156.24 │
│           8 │           $89.99 │
│          20 │          $234.50 │
│           3 │           $45.00 │
└─────────────┴──────────────────┘

🧠 Trenowanie modelu regresji liniowej (OLS - Ordinary Least Squares)...
✅ Model wytrenowany!

📐 RÓWNANIE PROSTEJ REGRESJI:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   y = 8.547234x + 45.321876

   gdzie:
   • y = przewidywany przychód (revenue) w $
   • x = liczba odsłon strony (pageviews)

   Parametry modelu:
   • Nachylenie (slope, a):     8.547234
   • Przecięcie (intercept, b): 45.321876
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📊 Współczynnik determinacji R²: 0.6453
   (R² = 64.53% - miara dopasowania modelu do danych)
   ⚠️  Średnie dopasowanie modelu.

🔮 PREDYKCJA:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   Pytanie: Ile zarobimy, jeśli klient zrobi 20 odsłon?

   Odpowiedź: $216.27
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📊 Dodatkowe predykcje dla różnych liczb odsłon:
┌─────────────┬──────────────────┐
│ PageViews   │ Predicted Revenue│
├─────────────┼──────────────────┤
│           1 │           $53.87 │
│           5 │           $88.06 │
│          10 │          $130.79 │
│          15 │          $173.53 │
│          20 │          $216.27 │
│          30 │          $301.74 │
│          50 │          $472.68 │
│         100 │          $900.05 │
└─────────────┴──────────────────┘

📈 STATYSTYKI DANYCH:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   PageViews:
   • Średnia:  11.45
   • Min:      1
   • Max:      47

   Revenue:
   • Średnia:  $143.21
   • Min:      $2.99
   • Max:      $649.95
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

💡 INTERPRETACJA BIZNESOWA:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   ✅ Każda dodatkowa odsłona strony zwiększa przewidywany
      przychód średnio o $8.55
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✅ Analiza zakończona pomyślnie!
```

## Implementacja techniczna - szczegóły

### Typy danych

✅ **`double` do obliczeń statystycznych**:
- Wszystkie obliczenia OLS używają `double` dla precyzji i wydajności
- Metody `Train()`, `Predict()`, `CalculateRSquared()` operują na `double`

✅ **`decimal` do wyświetlania waluty**:
- Konwersja na `decimal` tylko przy wyświetlaniu: `(decimal)predictedRevenue`
- Format waluty: `{value:C2}` (np. `$123.45`)

### Obsługa NULL-i

- `SessionData` używa nullable types (`long?`)
- Normalizacja przychodu w property `TransactionRevenue`
- Filtrowanie danych przed trenowaniem: `Where(s => s.PageViews.HasValue && s.PageViews > 0)`

### Własna implementacja OLS

**Bez ML.NET** - całkowicie od podstaw:

```csharp
// Wzór na slope (nachylenie)
double numerator = 0.0;
double denominator = 0.0;

foreach (var (x, y) in data)
{
    double xDiff = x - meanX;
    double yDiff = y - meanY;

    numerator += xDiff * yDiff;
    denominator += xDiff * xDiff;
}

slope = numerator / denominator;
intercept = meanY - (slope * meanX);
```

## Matematyka - metoda najmniejszych kwadratów

### Cel

Znaleźć prostą `y = ax + b`, która **minimalizuje sumę kwadratów reszt**:

```
minimize: Σ(Yi - (a*Xi + b))²
```

### Rozwiązanie analityczne

```
mean_x = (1/n) * Σ(Xi)
mean_y = (1/n) * Σ(Yi)

a = Σ((Xi - mean_x) * (Yi - mean_y)) / Σ((Xi - mean_x)²)
b = mean_y - a * mean_x
```

### Interpretacja parametrów

- **slope (a)**: o ile zmienia się Y, gdy X wzrasta o 1
  - W kontekście biznesowym: wzrost przychodu na każdą dodatkową odsłonę

- **intercept (b)**: wartość Y, gdy X = 0
  - W kontekście biznesowym: "bazowy" przychód niezależny od liczby odsłon

## Wymagania - realizacja

✅ **Pobranie danych**: Połączenie z BigQuery dataset `ga_sessions_20170801`

✅ **Zapytanie SQL**: Pobiera `pageviews` i `transactionRevenue` dla sesji z zakupami

✅ **Normalizacja**: `transactionRevenue / 1,000,000` (w property `TransactionRevenue`)

✅ **Klasa `SimpleLinearRegression`**: Własna implementacja OLS bez ML.NET

✅ **Metoda `Train()`**: Oblicza slope i intercept metodą najmniejszych kwadratów

✅ **Metoda `Predict()`**: Zwraca przewidywany przychód dla danej liczby odsłon

✅ **Równanie prostej**: Wyświetlane w formacie `y = ax + b`

✅ **Predykcja dla 20 odsłon**: Wyświetlana z formatowaniem waluty

✅ **`double` dla obliczeń**: Wszystkie obliczenia statystyczne używają `double`

✅ **`decimal` dla waluty**: Konwersja do `decimal` tylko przy wyświetlaniu

## Rozszerzenia i ulepszenia

Możliwe dodatkowe funkcje:
- Wizualizacja wykresu punktowego z prostą regresji
- Walidacja krzyżowa (cross-validation)
- Przedziały ufności dla predykcji
- Wieloraka regresja liniowa (więcej zmiennych niezależnych)
- Detekcja i usuwanie outlierów
- Eksport wyników do CSV/JSON

## Rozwiązywanie problemów

### Błąd: "The Application Default Credentials are not available"

Upewnij się, że zmienna `GOOGLE_APPLICATION_CREDENTIALS` jest poprawnie ustawiona.

### Błąd: "Access Denied: BigQuery"

Service account musi mieć przypisaną rolę `roles/bigquery.user`.

### Niski R²

Jeśli R² jest niski (<0.4), może to oznaczać:
- Słabą korelację liniową między zmiennymi
- Potrzebę dodatkowych zmiennych (regresja wieloraka)
- Obecność outlierów w danych
- Nieliniową zależność (potrzeba innego modelu)

## Licencja

Projekt edukacyjny - dane publiczne z BigQuery Google Analytics Sample.
